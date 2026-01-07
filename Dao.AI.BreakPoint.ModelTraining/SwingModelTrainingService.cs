using Dao.AI.BreakPoint.Services.MoveNet;
using Dao.AI.BreakPoint.Services.SwingAnalyzer;
using Microsoft.ML;

namespace Dao.AI.BreakPoint.ModelTraining;

/// <summary>
/// Service for training the swing quality analyzer model using ML.NET.
/// Uses the same feature extraction as SwingQualityInferenceService for consistency.
/// </summary>
internal class SwingModelTrainingService
{
    private readonly MLContext _mlContext;

    /// <summary>
    /// Key joints for tennis swing analysis (same as inference service).
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

    public SwingModelTrainingService()
    {
        _mlContext = new MLContext(seed: 42);
    }

    /// <summary>
    /// Train the swing quality model and export to ONNX
    /// </summary>
    public async Task<string> TrainAsync(
        List<TrainingSwingVideo> processedSwingVideos,
        TrainingConfiguration config
    )
    {
        ValidateTrainingInputs(processedSwingVideos, config);

        Console.WriteLine("Starting data preprocessing...");
        var trainingData = PreprocessTrainingData(processedSwingVideos);

        Console.WriteLine($"Preprocessed {trainingData.Count} training samples");
        Console.WriteLine(
            $"Feature count: {SwingQualityInferenceService.AggregatedFeatureCount} aggregated features (mean, std, range) → quality score"
        );

        // Load data into ML.NET DataView
        var dataView = _mlContext.Data.LoadFromEnumerable(trainingData);

        // Split data for training and validation
        var splitData = _mlContext.Data.TrainTestSplit(
            dataView,
            testFraction: config.ValidationSplit
        );

        Console.WriteLine("Building ML.NET training pipeline...");

        // Build the training pipeline for regression
        var pipeline = _mlContext
            .Transforms.NormalizeMinMax("Features")
            .Append(
                _mlContext.Regression.Trainers.FastTree(
                    labelColumnName: "QualityScore",
                    featureColumnName: "Features",
                    numberOfLeaves: 8,
                    numberOfTrees: 15,
                    minimumExampleCountPerLeaf: 2,
                    learningRate: config.LearningRate
                )
            );

        Console.WriteLine("Training swing quality model...");

        // Train the model
        var model = pipeline.Fit(splitData.TrainSet);

        // Evaluate on test set
        var predictions = model.Transform(splitData.TestSet);
        var metrics = _mlContext.Regression.Evaluate(predictions, labelColumnName: "QualityScore");

        Console.WriteLine("\n=== Training Results ===");
        Console.WriteLine($"R-Squared: {metrics.RSquared:F4}");
        Console.WriteLine($"Root Mean Squared Error: {metrics.RootMeanSquaredError:F4}");
        Console.WriteLine($"Mean Absolute Error: {metrics.MeanAbsoluteError:F4}");

        // Ensure output directory exists
        var outputDir = Path.GetDirectoryName(config.ModelOutputPath);
        if (!string.IsNullOrEmpty(outputDir) && !Directory.Exists(outputDir))
        {
            Directory.CreateDirectory(outputDir);
        }

        // Export to ONNX format
        Console.WriteLine($"\nExporting model to ONNX: {config.ModelOutputPath}");
        await ExportToOnnxAsync(model, dataView, config.ModelOutputPath);

        Console.WriteLine($"Model saved to {config.ModelOutputPath}");
        return config.ModelOutputPath;
    }

    private static void ValidateTrainingInputs(
        List<TrainingSwingVideo> videos,
        TrainingConfiguration? config
    )
    {
        if (videos.Count == 0)
        {
            throw new ArgumentException("Training videos list cannot be empty.", nameof(videos));
        }

        ArgumentNullException.ThrowIfNull(config);

        var totalSwings = 0;
        var validVideos = 0;

        foreach (var trainingVideo in videos)
        {
            if (
                trainingVideo.TrainingLabel.QualityScore < 0
                || trainingVideo.TrainingLabel.QualityScore > 100
            )
            {
                Console.WriteLine(
                    $"Warning: Video has invalid quality score {trainingVideo.TrainingLabel.QualityScore}. Should be between 0 and 100."
                );
                continue;
            }

            if (
                trainingVideo.SwingVideo.Swings == null
                || trainingVideo.SwingVideo.Swings.Count == 0
            )
            {
                Console.WriteLine("Warning: Video contains no swing data.");
                continue;
            }

            foreach (var swing in trainingVideo.SwingVideo.Swings)
            {
                if (swing.Frames == null || swing.Frames.Count == 0)
                {
                    Console.WriteLine("Warning: Swing contains no frame data.");
                    continue;
                }

                if (swing.Frames.Count < MoveNetVideoProcessor.MinSwingFrames)
                {
                    Console.WriteLine(
                        $"Warning: Swing has only {swing.Frames.Count} frames, expected at least {MoveNetVideoProcessor.MinSwingFrames}."
                    );
                }

                totalSwings++;
            }

            validVideos++;
        }

        if (validVideos == 0)
        {
            throw new ArgumentException("No valid training videos found.");
        }

        Console.WriteLine($"Validation passed: {validVideos} videos, {totalSwings} total swings");
    }

    private static List<SwingQualityInput> PreprocessTrainingData(
        List<TrainingSwingVideo> processedSwingVideos
    )
    {
        var trainingData = new List<SwingQualityInput>();

        foreach (var video in processedSwingVideos)
        {
            foreach (var swing in video.SwingVideo.Swings)
            {
                try
                {
                    var features = AggregateFeatures(swing);

                    if (features != null)
                    {
                        trainingData.Add(
                            new SwingQualityInput
                            {
                                Features = features,
                                QualityScore = (float)video.TrainingLabel.QualityScore,
                            }
                        );
                    }
                }
                catch
                {
                    continue;
                }
            }
        }

        if (trainingData.Count == 0)
        {
            throw new InvalidOperationException(
                "No valid training sequences found in the provided data."
            );
        }

        return trainingData;
    }

    /// <summary>
    /// Extract raw features from a single frame.
    /// Returns 16 features: 12 motion (6 joints × velocity + acceleration) + 4 angles.
    /// This matches SwingQualityInferenceService.ExtractFrameFeatures exactly.
    /// </summary>
    private static float[] ExtractFrameFeatures(FrameData frame)
    {
        var features = new float[SwingQualityInferenceService.FeaturesPerFrame];

        int featureIdx = 0;
        foreach (var joint in KeyJoints)
        {
            int idx = (int)joint;
            var jointData = frame.Joints[idx];

            float velocity = jointData.Confidence >= 0.2f ? (jointData.Speed ?? 0f) : 0f;
            float acceleration = jointData.Confidence >= 0.2f ? (jointData.Acceleration ?? 0f) : 0f;

            features[featureIdx++] = velocity;
            features[featureIdx++] = acceleration;
        }

        features[featureIdx++] = frame.LeftElbowAngle;
        features[featureIdx++] = frame.RightElbowAngle;
        features[featureIdx++] = frame.LeftShoulderAngle;
        features[featureIdx++] = frame.RightShoulderAngle;

        return features;
    }

    /// <summary>
    /// Aggregate features from all frames using statistics (mean, std, range).
    /// This matches SwingQualityInferenceService.AggregateFeatures exactly.
    /// </summary>
    private static float[]? AggregateFeatures(SwingData swing)
    {
        if (swing.Frames == null || swing.Frames.Count == 0)
            return null;

        var frameFeatures = new List<float[]>();
        foreach (var frame in swing.Frames)
        {
            frameFeatures.Add(ExtractFrameFeatures(frame));
        }

        var aggregated = new float[SwingQualityInferenceService.AggregatedFeatureCount];

        for (int f = 0; f < SwingQualityInferenceService.FeaturesPerFrame; f++)
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

            int baseIdx = f * 3;

            if (values.Count == 0)
            {
                aggregated[baseIdx + 0] = 0f;
                aggregated[baseIdx + 1] = 0f;
                aggregated[baseIdx + 2] = 0f;
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

    private Task ExportToOnnxAsync(ITransformer model, IDataView dataView, string outputPath)
    {
        return Task.Run(() =>
        {
            using var stream = File.Create(outputPath);
            _mlContext.Model.ConvertToOnnx(model, dataView, stream);
        });
    }
}
