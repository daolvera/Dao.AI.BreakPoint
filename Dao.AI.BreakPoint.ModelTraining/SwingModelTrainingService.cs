using Dao.AI.BreakPoint.Services.MoveNet;
using Dao.AI.BreakPoint.Services.SwingAnalyzer;
using Microsoft.ML;

namespace Dao.AI.BreakPoint.ModelTraining;

/// <summary>
/// Service for training the swing quality analyzer model using ML.NET.
/// Handles data preprocessing, training, and ONNX model export.
/// </summary>
internal class SwingModelTrainingService
{
    private readonly MLContext _mlContext;

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
        // Validate input parameters
        ValidateTrainingInputs(processedSwingVideos, config);

        Console.WriteLine("Starting data preprocessing...");
        var trainingData = await PreprocessTrainingDataAsync(processedSwingVideos, config);

        Console.WriteLine($"Preprocessed {trainingData.Count} training samples");
        Console.WriteLine(
            $"Feature count: {config.NumFeatures * 3} aggregated features (mean, std, range) → quality score"
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
        // Using simplified model architecture for small datasets
        var pipeline = _mlContext
            .Transforms
            // Normalize features
            .NormalizeMinMax("Features")
            // Train regression model using FastTree with reduced complexity
            // Fewer trees and leaves prevent overfitting with limited training data
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
        Console.WriteLine($"Using FastTree regression with {config.Epochs} trees");

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

        // Validate video data
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

                // Check if swing has reasonable number of frames (at least 1 second)
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

    private static async Task<List<SwingQualityInput>> PreprocessTrainingDataAsync(
        List<TrainingSwingVideo> processedSwingVideos,
        TrainingConfiguration config
    )
    {
        var trainingData = new List<SwingQualityInput>();

        foreach (var video in processedSwingVideos)
        {
            foreach (var swing in video.SwingVideo.Swings)
            {
                try
                {
                    var features = await AggregateSequenceFeaturesAsync(
                        swing,
                        config.SequenceLength,
                        config.NumFeatures
                    );

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
                    // Skip problematic swings
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
    /// Aggregate sequence features into a fixed-size vector using statistics.
    /// Since ML.NET doesn't support variable-length sequences like CNNs,
    /// we compute mean, std, and range for each feature across time.
    /// Reduced from 5 to 3 statistics to prevent feature explosion with small datasets.
    /// </summary>
    private static Task<float[]?> AggregateSequenceFeaturesAsync(
        SwingData swing,
        int targetLength,
        int numFeatures
    )
    {
        return Task.Run(() =>
        {
            if (swing.Frames == null || swing.Frames.Count == 0)
                return null;

            // First, get the raw sequence
            var rawSequence = SwingPreprocessingService
                .PreprocessSwingAsync(swing, targetLength, numFeatures)
                .GetAwaiter()
                .GetResult();

            if (rawSequence == null)
                return null;

            // Compute aggregated statistics for each feature
            // 3 statistics per feature: mean, std, range (reduced from 5 to prevent overfitting)
            var aggregatedFeatures = new float[numFeatures * 3];

            for (int f = 0; f < numFeatures; f++)
            {
                var values = new List<float>();
                for (int t = 0; t < targetLength; t++)
                {
                    var val = rawSequence[t, f];
                    // Skip NaN values for cleaner statistics
                    if (!float.IsNaN(val) && !float.IsInfinity(val))
                    {
                        values.Add(val);
                    }
                }

                int baseIdx = f * 3;

                // Handle empty or all-NaN case
                if (values.Count == 0)
                {
                    aggregatedFeatures[baseIdx + 0] = 0f; // mean
                    aggregatedFeatures[baseIdx + 1] = 0f; // std
                    aggregatedFeatures[baseIdx + 2] = 0f; // range
                    continue;
                }

                // Calculate statistics
                float mean = values.Average();
                float variance = values.Sum(v => (v - mean) * (v - mean)) / values.Count;
                float std = MathF.Sqrt(variance);
                float min = values.Min();
                float max = values.Max();
                float range = max - min;

                // Store in aggregated vector
                aggregatedFeatures[baseIdx + 0] = SanitizeFloat(mean);
                aggregatedFeatures[baseIdx + 1] = SanitizeFloat(std);
                aggregatedFeatures[baseIdx + 2] = SanitizeFloat(range);
            }

            return aggregatedFeatures;
        });
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
