using Dao.AI.BreakPoint.Services.MoveNet;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace Dao.AI.BreakPoint.Services.SwingAnalyzer;

/// <summary>
/// Inference service for the trained swing quality model.
/// Aggregates features directly from SwingData and runs inference.
/// </summary>
public class SwingQualityInferenceService : IDisposable
{
    private readonly InferenceSession? _session;
    private bool _disposed;

    /// <summary>
    /// Number of raw features extracted per frame.
    /// 6 key joints × 2 (velocity + acceleration) = 12 motion features + 4 angles = 16
    /// </summary>
    public const int FeaturesPerFrame = 16;

    /// <summary>
    /// Number of statistics computed per feature during aggregation (mean, std, range).
    /// </summary>
    private const int StatsPerFeature = 3;

    /// <summary>
    /// Total aggregated feature count: 16 features × 3 stats = 48
    /// </summary>
    public const int AggregatedFeatureCount = FeaturesPerFrame * StatsPerFeature;

    /// <summary>
    /// Key joints for tennis swing analysis (most relevant for technique quality).
    /// </summary>
    private static readonly JointFeatures[] KeyJoints =
    [
        JointFeatures.LeftShoulder,
        JointFeatures.RightShoulder,
        JointFeatures.LeftElbow,
        JointFeatures.RightElbow,
        JointFeatures.LeftWrist,
        JointFeatures.RightWrist,
    ];

    /// <summary>
    /// Feature names for the 16 raw features.
    /// </summary>
    private static readonly string[] FeatureNames =
    [
        "Left Shoulder Velocity",
        "Left Shoulder Acceleration",
        "Right Shoulder Velocity",
        "Right Shoulder Acceleration",
        "Left Elbow Velocity",
        "Left Elbow Acceleration",
        "Right Elbow Velocity",
        "Right Elbow Acceleration",
        "Left Wrist Velocity",
        "Left Wrist Acceleration",
        "Right Wrist Velocity",
        "Right Wrist Acceleration",
        "Left Elbow Angle",
        "Right Elbow Angle",
        "Left Shoulder Angle",
        "Right Shoulder Angle",
    ];

    /// <summary>
    /// Creates a new SwingQualityInferenceService.
    /// If modelPath is null or file doesn't exist, inference returns heuristic values.
    /// </summary>
    public SwingQualityInferenceService(string? modelPath)
    {
        if (!string.IsNullOrEmpty(modelPath) && File.Exists(modelPath))
        {
            try
            {
                var sessionOptions = new SessionOptions();
                sessionOptions.GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_EXTENDED;
                _session = new InferenceSession(modelPath, sessionOptions);

                // Log model metadata for debugging
                LogModelMetadata();
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
    /// Log model input/output metadata for debugging
    /// </summary>
    private void LogModelMetadata()
    {
        if (_session == null)
            return;

        Console.WriteLine("=== Model Metadata ===");
        Console.WriteLine("Inputs:");
        foreach (var input in _session.InputMetadata)
        {
            var shape = string.Join(",", input.Value.Dimensions);
            Console.WriteLine($"  {input.Key}: {input.Value.ElementType} [{shape}]");
        }
        Console.WriteLine("Outputs:");
        foreach (var output in _session.OutputMetadata)
        {
            var shape = string.Join(",", output.Value.Dimensions);
            Console.WriteLine($"  {output.Key}: {output.Value.ElementType} [{shape}]");
        }
        Console.WriteLine("======================");
    }

    /// <summary>
    /// Whether the model is loaded and ready for inference
    /// </summary>
    public bool IsModelLoaded => _session != null;

    /// <summary>
    /// Run inference on swing data to get quality score and feature importance.
    /// Aggregates features directly from SwingData frames.
    /// </summary>
    public SwingQualityResult RunInference(SwingData swing)
    {
        if (swing.Frames.Count == 0)
        {
            return new SwingQualityResult
            {
                QualityScore = 0,
                FeatureImportance = [],
                IsFromModel = false,
            };
        }

        // Extract and aggregate features from all frames
        float[] aggregatedFeatures = AggregateFeatures(swing);

        if (_session == null)
        {
            return GetHeuristicResult(aggregatedFeatures);
        }

        try
        {
            float qualityScore = RunModelInference(aggregatedFeatures);

            var featureImportance = ExtractFeatureImportance(aggregatedFeatures);

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
            return GetHeuristicResult(aggregatedFeatures);
        }
    }

    /// <summary>
    /// Extract raw features from a single frame.
    /// Returns 16 features: 12 motion (6 joints × velocity + acceleration) + 4 angles
    /// </summary>
    private static float[] ExtractFrameFeatures(FrameData frame)
    {
        var features = new float[FeaturesPerFrame];

        // Motion features for key joints (velocity and acceleration)
        int featureIdx = 0;
        foreach (var joint in KeyJoints)
        {
            int idx = (int)joint;
            var jointData = frame.Joints[idx];

            // Use 0 for low-confidence joints
            float velocity = jointData.Confidence >= 0.2f ? (jointData.Speed ?? 0f) : 0f;
            float acceleration = jointData.Confidence >= 0.2f ? (jointData.Acceleration ?? 0f) : 0f;

            features[featureIdx++] = velocity;
            features[featureIdx++] = acceleration;
        }

        // Key angles for technique assessment
        features[featureIdx++] = frame.LeftElbowAngle;
        features[featureIdx++] = frame.RightElbowAngle;
        features[featureIdx++] = frame.LeftShoulderAngle;
        features[featureIdx++] = frame.RightShoulderAngle;

        return features;
    }

    /// <summary>
    /// Aggregate features from all frames using statistics (mean, std, range).
    /// This matches the training pipeline in SwingModelTrainingService.
    /// </summary>
    private static float[] AggregateFeatures(SwingData swing)
    {
        // Collect all frame features
        var frameFeatures = new List<float[]>();
        foreach (var frame in swing.Frames)
        {
            frameFeatures.Add(ExtractFrameFeatures(frame));
        }

        // Aggregate: 3 stats per feature (mean, std, range)
        var aggregated = new float[AggregatedFeatureCount];

        for (int f = 0; f < FeaturesPerFrame; f++)
        {
            var values = new List<float>();
            foreach (var features in frameFeatures)
            {
                float val = features[f];
                if (!float.IsNaN(val) && !float.IsInfinity(val))
                {
                    values.Add(val);
                }
            }

            int baseIdx = f * StatsPerFeature;

            if (values.Count == 0)
            {
                aggregated[baseIdx + 0] = 0f; // mean
                aggregated[baseIdx + 1] = 0f; // std
                aggregated[baseIdx + 2] = 0f; // range
                continue;
            }

            float mean = values.Average();
            float variance = values.Sum(v => (v - mean) * (v - mean)) / values.Count;
            float std = MathF.Sqrt(variance);
            float range = values.Max() - values.Min();

            aggregated[baseIdx + 0] = SanitizeFloat(mean);
            aggregated[baseIdx + 1] = SanitizeFloat(std);
            aggregated[baseIdx + 2] = SanitizeFloat(range);
        }

        return aggregated;
    }

    private static float SanitizeFloat(float value)
    {
        if (float.IsNaN(value) || float.IsInfinity(value))
            return 0f;
        return value;
    }

    /// <summary>
    /// Run the ONNX model and return the quality score
    /// </summary>
    private float RunModelInference(float[] aggregatedFeatures)
    {
        // Create 2D tensor with shape [1, 48] - batch size 1, 48 features
        var featureTensor = new DenseTensor<float>([1, AggregatedFeatureCount]);

        for (int i = 0; i < Math.Min(aggregatedFeatures.Length, AggregatedFeatureCount); i++)
        {
            float val = aggregatedFeatures[i];
            featureTensor[0, i] = (float.IsNaN(val) || float.IsInfinity(val)) ? 0f : val;
        }

        // ML.NET exported ONNX models require both Features and Label inputs
        // Label tensor shape [1, 1] with dummy value (regression models need this for inference)
        var labelTensor = new DenseTensor<float>(new float[] { 0f }, [1, 1]);

        var inputs = new List<NamedOnnxValue>
        {
            NamedOnnxValue.CreateFromTensor("Features", featureTensor),
            NamedOnnxValue.CreateFromTensor("QualityScore", labelTensor),
        };

        using var results = _session!.Run(inputs);

        // ML.NET regression models output "Score.output" for predictions
        var scoreOutput =
            results.FirstOrDefault(r => r.Name == "Score.output")
            ?? results.FirstOrDefault(r => r.Name == "Score")
            ?? results.First();

        // Handle different output types from ML.NET ONNX export
        if (scoreOutput.Value is DenseTensor<float> floatTensor)
        {
            return floatTensor.First();
        }

        // Try to convert the output value directly
        try
        {
            var tensor2 = scoreOutput.AsTensor<float>();
            return tensor2.First();
        }
        catch
        {
            throw new InvalidOperationException(
                $"Unable to read model output. Output name: '{scoreOutput.Name}', Type: '{scoreOutput.Value?.GetType().Name ?? "null"}'"
            );
        }
    }

    /// <summary>
    /// Extract feature importance using range values as importance proxy.
    /// </summary>
    private static Dictionary<string, double> ExtractFeatureImportance(float[] aggregatedFeatures)
    {
        var importance = new Dictionary<string, double>();
        var rangeValues = new List<(string Name, float Value)>();

        for (int f = 0; f < Math.Min(FeaturesPerFrame, FeatureNames.Length); f++)
        {
            int rangeIdx = (f * StatsPerFeature) + 2; // Range is at offset 2
            if (rangeIdx < aggregatedFeatures.Length)
            {
                rangeValues.Add((FeatureNames[f], Math.Abs(aggregatedFeatures[rangeIdx])));
            }
        }

        float maxRange = rangeValues.Count > 0 ? rangeValues.Max(r => r.Value) : 1f;
        if (maxRange <= 0)
            maxRange = 1f;

        foreach (var (name, value) in rangeValues)
        {
            importance[name] = Math.Round(value / maxRange, 4);
        }

        return importance;
    }

    /// <summary>
    /// Creates a heuristic result when model is not available
    /// </summary>
    private static SwingQualityResult GetHeuristicResult(float[] aggregatedFeatures)
    {
        var featureImportance = ExtractFeatureImportance(aggregatedFeatures);
        float qualityScore = CalculateHeuristicQualityScore(aggregatedFeatures, featureImportance);

        return new SwingQualityResult
        {
            QualityScore = qualityScore,
            FeatureImportance = featureImportance,
            IsFromModel = false,
        };
    }

    /// <summary>
    /// Calculate a heuristic quality score when no model is available.
    /// </summary>
    private static float CalculateHeuristicQualityScore(
        float[] aggregatedFeatures,
        Dictionary<string, double> featureImportance
    )
    {
        float score = 50f;

        if (featureImportance.TryGetValue("Right Wrist Velocity", out var rightWristVelocity))
        {
            score += (float)(rightWristVelocity * 15);
        }
        if (featureImportance.TryGetValue("Left Wrist Velocity", out var leftWristVelocity))
        {
            score += (float)(leftWristVelocity * 10);
        }
        if (featureImportance.TryGetValue("Right Shoulder Angle", out var shoulderAngle))
        {
            score += (float)(shoulderAngle * 10);
        }
        if (featureImportance.TryGetValue("Right Elbow Angle", out var elbowAngle))
        {
            score += (float)(elbowAngle * 10);
        }

        // Bonus for having actual movement
        float totalMeanActivity = 0f;
        for (
            int f = 0;
            f < FeaturesPerFrame && (f * StatsPerFeature) < aggregatedFeatures.Length;
            f++
        )
        {
            totalMeanActivity += Math.Abs(aggregatedFeatures[f * StatsPerFeature]);
        }
        if (totalMeanActivity > 0.1f)
        {
            score += 5f;
        }

        return Math.Clamp(score, 0, 100);
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
    public double QualityScore { get; set; }
    public Dictionary<string, double> FeatureImportance { get; set; } = [];
    public bool IsFromModel { get; set; }

    public List<(string FeatureName, double Importance)> GetTopFeatures(int topN = 5)
    {
        return FeatureImportance
            .OrderByDescending(kvp => kvp.Value)
            .Take(topN)
            .Select(kvp => (kvp.Key, kvp.Value))
            .ToList();
    }

    public List<(string FeatureName, double Importance)> GetWeakFeatures(int topN = 3)
    {
        return FeatureImportance
            .OrderBy(kvp => kvp.Value)
            .Take(topN)
            .Select(kvp => (kvp.Key, kvp.Value))
            .ToList();
    }
}
