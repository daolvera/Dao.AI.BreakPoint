using System.Text.Json;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace Dao.AI.BreakPoint.Services.SwingAnalyzer;

/// <summary>
/// Inference service for the trained swing quality CNN model.
/// Provides quality score prediction and feature importance extraction.
/// </summary>
public class SwingQualityInferenceService : IDisposable
{
    private readonly InferenceSession? _session;
    private readonly int _sequenceLength;
    private readonly int _numFeatures;
    private bool _disposed;

    /// <summary>
    /// Creates a new SwingQualityInferenceService.
    /// If modelPath is null or file doesn't exist, inference returns default values.
    /// </summary>
    /// <param name="modelPath">Path to the ONNX model file</param>
    /// <param name="sequenceLength">Expected sequence length (default 90 frames)</param>
    /// <param name="numFeatures">Expected number of features per frame (default 66)</param>
    public SwingQualityInferenceService(
        string? modelPath,
        int sequenceLength = 90,
        int numFeatures = 66
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
    /// Run inference on preprocessed swing data to get quality score and feature importance
    /// </summary>
    /// <param name="preprocessedSwing">Preprocessed swing data [sequenceLength, numFeatures]</param>
    /// <returns>Inference result with quality score and feature importance</returns>
    public SwingQualityResult RunInference(float[,] preprocessedSwing)
    {
        if (_session == null)
        {
            return GetDefaultResult(preprocessedSwing);
        }

        try
        {
            // Create input tensor [1, sequenceLength, numFeatures]
            var inputTensor = CreateInputTensor(preprocessedSwing);

            // Run inference
            var inputs = new List<NamedOnnxValue>
            {
                NamedOnnxValue.CreateFromTensor("input_1", inputTensor),
            };

            using var results = _session.Run(inputs);

            // Extract quality score from output
            var outputTensor = results.First().AsTensor<float>();
            float qualityScore = outputTensor.First() * 100f; // Scale sigmoid output to 0-100

            // Calculate feature importance from variance in input data
            var featureImportance = CalculateFeatureImportance(preprocessedSwing);

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
            return GetDefaultResult(preprocessedSwing);
        }
    }

    /// <summary>
    /// Creates a default result using heuristic analysis when model is not available
    /// </summary>
    private SwingQualityResult GetDefaultResult(float[,] preprocessedSwing)
    {
        // Calculate feature importance from variance in input data
        var featureImportance = CalculateFeatureImportance(preprocessedSwing);

        // Heuristic quality score based on movement consistency
        float qualityScore = CalculateHeuristicQualityScore(preprocessedSwing, featureImportance);

        return new SwingQualityResult
        {
            QualityScore = qualityScore,
            FeatureImportance = featureImportance,
            IsFromModel = false,
        };
    }

    /// <summary>
    /// Calculate feature importance based on variance and motion characteristics
    /// Higher variance typically indicates more deliberate movement
    /// </summary>
    private Dictionary<string, double> CalculateFeatureImportance(float[,] preprocessedSwing)
    {
        var importance = new Dictionary<string, double>();
        int sequenceLength = preprocessedSwing.GetLength(0);
        int numFeatures = preprocessedSwing.GetLength(1);

        // Calculate variance for each feature across frames
        var featureVariances = new float[numFeatures];

        for (int f = 0; f < numFeatures; f++)
        {
            // Calculate mean
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

            // Calculate variance
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

        // Normalize variances to 0-1 range
        float maxVariance = featureVariances.Max();
        if (maxVariance > 0)
        {
            for (int i = 0; i < featureVariances.Length; i++)
            {
                featureVariances[i] /= maxVariance;
            }
        }

        // Map to feature names (from AttentionInterpreter)
        for (int i = 0; i < Math.Min(featureVariances.Length, 66); i++)
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
                tensor[0, t, f] = float.IsNaN(val) ? 0f : val;
            }
        }

        return tensor;
    }

    /// <summary>
    /// Get human-readable name for a feature index
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
            "Left Hip",
            "Right Hip",
            "Left Knee",
            "Right Knee",
            "Left Ankle",
            "Right Ankle",
        ];

        string[] angleNames =
        [
            "Left Elbow Angle",
            "Right Elbow Angle",
            "Left Shoulder Angle",
            "Right Shoulder Angle",
            "Left Hip Angle",
            "Right Hip Angle",
            "Left Knee Angle",
            "Right Knee Angle",
        ];

        // Velocity features: 0-11 (12 joints)
        if (index < 12)
        {
            return $"{jointNames[index]} Velocity";
        }
        // Acceleration features: 12-23
        if (index < 24)
        {
            return $"{jointNames[index - 12]} Acceleration";
        }
        // Angle features: 24-31
        if (index < 32)
        {
            return angleNames[index - 24];
        }
        // Position features: 32-65 (17 joints x 2 coords)
        int posIndex = index - 32;
        int jointIndex = posIndex / 2;
        string coord = posIndex % 2 == 0 ? "X" : "Y";

        string[] allJointNames =
        [
            "Nose",
            "Left Eye",
            "Right Eye",
            "Left Ear",
            "Right Ear",
            "Left Shoulder",
            "Right Shoulder",
            "Left Elbow",
            "Right Elbow",
            "Left Wrist",
            "Right Wrist",
            "Left Hip",
            "Right Hip",
            "Left Knee",
            "Right Knee",
            "Left Ankle",
            "Right Ankle",
        ];

        if (jointIndex < allJointNames.Length)
        {
            return $"{allJointNames[jointIndex]} Position {coord}";
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
