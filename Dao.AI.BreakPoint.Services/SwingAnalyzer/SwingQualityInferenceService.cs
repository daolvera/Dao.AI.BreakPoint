using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace Dao.AI.BreakPoint.Services.SwingAnalyzer;

/// <summary>
/// Inference service for the trained swing quality model.
/// Provides quality score prediction and gradient-based feature importance extraction.
/// Uses the focused 28-feature model optimized for tennis swing analysis.
/// </summary>
public class SwingQualityInferenceService : IDisposable
{
    private readonly InferenceSession? _session;
    private readonly int _sequenceLength;
    private readonly int _numFeatures;
    private bool _disposed;

    // Epsilon for finite difference gradient computation
    private const float GradientEpsilon = 0.01f;

    /// <summary>
    /// Feature indices for the focused 28-feature model.
    /// Structure: 6 joints × 2 (velocity/accel) = 12 motion features, then 4 angles
    /// Layout per frame:
    ///   0-1: Left Shoulder (velocity, acceleration)
    ///   2-3: Right Shoulder (velocity, acceleration)
    ///   4-5: Left Elbow (velocity, acceleration)
    ///   6-7: Right Elbow (velocity, acceleration)
    ///   8-9: Left Wrist (velocity, acceleration)
    ///  10-11: Right Wrist (velocity, acceleration)
    ///  12-15: Angles (left elbow, right elbow, left shoulder, right shoulder)
    /// </summary>
    private static readonly int[] KeyFeatureIndices =
    [
        // All 16 key features for coaching tip generation
        0,
        1, // Left Shoulder Velocity/Acceleration
        2,
        3, // Right Shoulder Velocity/Acceleration
        4,
        5, // Left Elbow Velocity/Acceleration
        6,
        7, // Right Elbow Velocity/Acceleration
        8,
        9, // Left Wrist Velocity/Acceleration
        10,
        11, // Right Wrist Velocity/Acceleration
        12,
        13, // Left/Right Elbow Angle
        14,
        15, // Left/Right Shoulder Angle
    ];

    /// <summary>
    /// Creates a new SwingQualityInferenceService.
    /// If modelPath is null or file doesn't exist, inference returns heuristic values.
    /// </summary>
    public SwingQualityInferenceService(
        string? modelPath,
        int sequenceLength = 90,
        int numFeatures = SwingPreprocessingService.FocusedFeatureCount
    )
    {
        _sequenceLength = sequenceLength;
        _numFeatures = numFeatures;

        if (!string.IsNullOrEmpty(modelPath) && File.Exists(modelPath))
        {
            try
            {
                var sessionOptions = new SessionOptions();
                sessionOptions.GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_EXTENDED;
                _session = new InferenceSession(modelPath, sessionOptions);
            }
            catch (Exception ex)
            {
                Console.WriteLine(
                    $"Warning: Failed to load swing quality model from {modelPath}: {ex.Message}"
                );
                _session = null;
            }
        }
    }

    /// <summary>
    /// Whether the model is loaded and ready for inference
    /// </summary>
    public bool IsModelLoaded => _session != null;

    /// <summary>
    /// Run inference on preprocessed swing data to get quality score and feature importance.
    /// Uses gradient-based saliency when model is loaded, heuristics otherwise.
    /// </summary>
    public SwingQualityResult RunInference(float[,] preprocessedSwing)
    {
        if (_session == null)
        {
            return GetHeuristicResult(preprocessedSwing);
        }

        try
        {
            // Get base quality score
            float qualityScore = RunModelInference(preprocessedSwing);

            // Compute gradient-based feature importance
            var featureImportance = ComputeGradientImportance(preprocessedSwing, qualityScore);

            return new SwingQualityResult
            {
                QualityScore = Math.Clamp(qualityScore, 0, 100),
                FeatureImportance = featureImportance,
                IsFromModel = true,
            };
        }
        catch (Exception ex)
        {
            Console.WriteLine(
                $"Warning: Inference failed: {ex.Message}. Using heuristic fallback."
            );
            return GetHeuristicResult(preprocessedSwing);
        }
    }

    /// <summary>
    /// Run the ONNX model and return the quality score (0-100)
    /// </summary>
    private float RunModelInference(float[,] preprocessedSwing)
    {
        var inputTensor = CreateInputTensor(preprocessedSwing);
        var inputs = new List<NamedOnnxValue>
        {
            NamedOnnxValue.CreateFromTensor("input_1", inputTensor),
        };

        using var results = _session!.Run(inputs);
        var outputTensor = results.First().AsTensor<float>();

        // Scale sigmoid output (0-1) to quality score (0-100)
        return outputTensor.First() * 100f;
    }

    /// <summary>
    /// Compute feature importance using finite difference gradients.
    /// For each key feature, perturb it slightly and measure the change in output.
    /// </summary>
    private Dictionary<string, double> ComputeGradientImportance(
        float[,] preprocessedSwing,
        float baseScore
    )
    {
        var importance = new Dictionary<string, double>();
        var gradients = new Dictionary<int, float>();
        int sequenceLength = preprocessedSwing.GetLength(0);

        // Compute gradients for all key features used in coaching tips
        foreach (int featureIdx in KeyFeatureIndices)
        {
            if (featureIdx >= _numFeatures)
                continue;

            // Aggregate gradient across all frames for this feature
            float totalGradient = 0f;

            // Sample frames instead of all frames for efficiency
            // Use frames at 25%, 50%, 75% of swing (key moments)
            int[] sampleFrames = [sequenceLength / 4, sequenceLength / 2, (3 * sequenceLength) / 4];

            foreach (int frameIdx in sampleFrames)
            {
                if (frameIdx >= sequenceLength)
                    continue;

                // Create perturbed copy
                var perturbed = CloneSwingData(preprocessedSwing);
                float originalValue = perturbed[frameIdx, featureIdx];

                // Skip if NaN or Infinity
                if (float.IsNaN(originalValue) || float.IsInfinity(originalValue))
                    continue;

                // Perturb the feature
                perturbed[frameIdx, featureIdx] = originalValue + GradientEpsilon;

                // Run inference on perturbed input
                float perturbedScore = RunModelInference(perturbed);

                // Compute gradient: ∂score/∂feature
                float gradient = (perturbedScore - baseScore) / GradientEpsilon;
                totalGradient += Math.Abs(gradient); // Use absolute value for importance
            }

            gradients[featureIdx] = totalGradient / sampleFrames.Length;
        }

        // Normalize gradients to 0-1 range
        float maxGradient = gradients.Values.DefaultIfEmpty(1f).Max();
        if (maxGradient > 0)
        {
            foreach (var kvp in gradients)
            {
                string featureName = GetFeatureName(kvp.Key);
                double normalizedImportance = kvp.Value / maxGradient;
                importance[featureName] = Math.Round(normalizedImportance, 4);
            }
        }

        return importance;
    }

    /// <summary>
    /// Clone the swing data array for perturbation
    /// </summary>
    private static float[,] CloneSwingData(float[,] original)
    {
        int rows = original.GetLength(0);
        int cols = original.GetLength(1);
        var clone = new float[rows, cols];
        Buffer.BlockCopy(original, 0, clone, 0, rows * cols * sizeof(float));
        return clone;
    }

    /// <summary>
    /// Creates a heuristic result using variance analysis when model is not available
    /// </summary>
    private SwingQualityResult GetHeuristicResult(float[,] preprocessedSwing)
    {
        var featureImportance = CalculateVarianceImportance(preprocessedSwing);
        float qualityScore = CalculateHeuristicQualityScore(preprocessedSwing, featureImportance);

        return new SwingQualityResult
        {
            QualityScore = qualityScore,
            FeatureImportance = featureImportance,
            IsFromModel = false,
        };
    }

    /// <summary>
    /// Calculate feature importance based on variance (heuristic fallback).
    /// NOTE: This measures activity, not contribution to quality.
    /// Only used when no trained model is available.
    /// </summary>
    private Dictionary<string, double> CalculateVarianceImportance(float[,] preprocessedSwing)
    {
        var importance = new Dictionary<string, double>();
        int sequenceLength = preprocessedSwing.GetLength(0);
        int numFeatures = preprocessedSwing.GetLength(1);

        var featureVariances = new float[numFeatures];

        for (int f = 0; f < numFeatures; f++)
        {
            float sum = 0;
            int validCount = 0;
            for (int t = 0; t < sequenceLength; t++)
            {
                float val = preprocessedSwing[t, f];
                if (!float.IsNaN(val))
                {
                    sum += val;
                    validCount++;
                }
            }

            if (validCount == 0)
            {
                featureVariances[f] = 0;
                continue;
            }

            float mean = sum / validCount;
            float varianceSum = 0;
            for (int t = 0; t < sequenceLength; t++)
            {
                float val = preprocessedSwing[t, f];
                if (!float.IsNaN(val))
                {
                    varianceSum += (val - mean) * (val - mean);
                }
            }
            featureVariances[f] = varianceSum / validCount;
        }

        // Normalize
        float maxVariance = featureVariances.Max();
        if (maxVariance > 0)
        {
            for (int i = 0; i < featureVariances.Length; i++)
            {
                featureVariances[i] /= maxVariance;
            }
        }

        // Map to feature names (16 key features for coaching)
        for (int i = 0; i < Math.Min(featureVariances.Length, KeyFeatureIndices.Length); i++)
        {
            string featureName = GetFeatureName(i);
            importance[featureName] = Math.Round(featureVariances[i], 4);
        }

        return importance;
    }

    /// <summary>
    /// Calculate a heuristic quality score when no model is available
    /// </summary>
    private static float CalculateHeuristicQualityScore(
        float[,] preprocessedSwing,
        Dictionary<string, double> featureImportance
    )
    {
        // Base score starts at 50 (average)
        float score = 50f;

        // Bonus for consistent arm movement (wrist velocity variance)
        if (featureImportance.TryGetValue("Right Wrist Velocity", out var rightWristVelocity))
        {
            score += (float)(rightWristVelocity * 15); // Up to 15 points
        }
        if (featureImportance.TryGetValue("Left Wrist Velocity", out var leftWristVelocity))
        {
            score += (float)(leftWristVelocity * 15); // Up to 15 points
        }

        // Bonus for shoulder rotation
        if (featureImportance.TryGetValue("Right Shoulder Angle", out var shoulderAngle))
        {
            score += (float)(shoulderAngle * 10); // Up to 10 points
        }

        // Bonus for hip rotation (power generation)
        if (featureImportance.TryGetValue("Right Hip Angle", out var hipAngle))
        {
            score += (float)(hipAngle * 10); // Up to 10 points
        }

        return Math.Clamp(score, 0, 100);
    }

    /// <summary>
    /// Create input tensor from preprocessed swing data
    /// </summary>
    private DenseTensor<float> CreateInputTensor(float[,] preprocessedSwing)
    {
        int actualSeqLen = preprocessedSwing.GetLength(0);
        int actualNumFeatures = preprocessedSwing.GetLength(1);

        // Create tensor with expected dimensions
        var tensor = new DenseTensor<float>([1, _sequenceLength, _numFeatures]);

        // Copy data, padding or truncating as needed
        for (int t = 0; t < Math.Min(actualSeqLen, _sequenceLength); t++)
        {
            for (int f = 0; f < Math.Min(actualNumFeatures, _numFeatures); f++)
            {
                float val = preprocessedSwing[t, f];
                tensor[0, t, f] = (float.IsNaN(val) || float.IsInfinity(val)) ? 0f : val;
            }
        }

        return tensor;
    }

    /// <summary>
    /// Get human-readable name for a feature index.
    /// Maps to the focused 28-feature layout used for tennis swing analysis.
    /// </summary>
    private static string GetFeatureName(int index)
    {
        string[] jointNames =
        [
            "Left Shoulder",
            "Right Shoulder",
            "Left Elbow",
            "Right Elbow",
            "Left Wrist",
            "Right Wrist",
        ];

        string[] angleNames =
        [
            "Left Elbow Angle",
            "Right Elbow Angle",
            "Left Shoulder Angle",
            "Right Shoulder Angle",
        ];

        // Motion features: 0-11 (6 joints × 2 for velocity + acceleration interleaved)
        if (index < 12)
        {
            int jointIdx = index / 2;
            string metric = index % 2 == 0 ? "Velocity" : "Acceleration";
            if (jointIdx < jointNames.Length)
            {
                return $"{jointNames[jointIdx]} {metric}";
            }
        }

        // Angle features: 12-15
        if (index < 16)
        {
            int angleIdx = index - 12;
            if (angleIdx < angleNames.Length)
            {
                return angleNames[angleIdx];
            }
        }

        return $"Feature {index}";
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _session?.Dispose();
            _disposed = true;
        }
        GC.SuppressFinalize(this);
    }
}

/// <summary>
/// Result from swing quality inference
/// </summary>
public class SwingQualityResult
{
    /// <summary>
    /// Quality score from 0-100
    /// </summary>
    public double QualityScore { get; set; }

    /// <summary>
    /// Feature importance scores (feature name -> normalized importance 0-1)
    /// </summary>
    public Dictionary<string, double> FeatureImportance { get; set; } = [];

    /// <summary>
    /// Whether this result came from the trained model (true) or heuristics (false)
    /// </summary>
    public bool IsFromModel { get; set; }

    /// <summary>
    /// Get top N most important features
    /// </summary>
    public List<(string FeatureName, double Importance)> GetTopFeatures(int topN = 5)
    {
        return FeatureImportance
            .OrderByDescending(kvp => kvp.Value)
            .Take(topN)
            .Select(kvp => (kvp.Key, kvp.Value))
            .ToList();
    }

    /// <summary>
    /// Get features that need improvement (lowest importance scores)
    /// </summary>
    public List<(string FeatureName, double Importance)> GetWeakFeatures(int topN = 3)
    {
        return FeatureImportance
            .OrderBy(kvp => kvp.Value)
            .Take(topN)
            .Select(kvp => (kvp.Key, kvp.Value))
            .ToList();
    }
}
