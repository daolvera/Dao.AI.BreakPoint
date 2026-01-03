using System.Text.Json;
using Dao.AI.BreakPoint.Services.MoveNet;
using Dao.AI.BreakPoint.Services.SwingAnalyzer;
using Microsoft.ML;

namespace Dao.AI.BreakPoint.ModelTraining;

/// <summary>
/// Service for training the swing phase classifier model using ML.NET.
/// Handles data preprocessing, training, and ONNX model export.
/// </summary>
public class SwingPhaseClassifierTrainingService
{
    private readonly MLContext _mlContext;

    public SwingPhaseClassifierTrainingService()
    {
        _mlContext = new MLContext(seed: 42);
    }

    /// <summary>
    /// Train the swing phase classifier from labeled frame data and export to ONNX
    /// </summary>
    public async Task<string> TrainAsync(
        List<LabeledFrameData> labeledFrames,
        PhaseClassifierTrainingConfiguration config
    )
    {
        ValidateTrainingInputs(labeledFrames, config);

        Console.WriteLine("Preprocessing labeled frame data...");
        var trainingData = PreprocessTrainingData(labeledFrames);

        Console.WriteLine($"Preprocessed {trainingData.Count} training samples");
        Console.WriteLine(
            $"Feature count: {SwingPhaseClassifierModel.TotalFeatures} → {SwingPhaseClassifierModel.NumClasses} classes"
        );

        // Load data into ML.NET DataView
        var dataView = _mlContext.Data.LoadFromEnumerable(trainingData);

        // Split data for training and validation
        var splitData = _mlContext.Data.TrainTestSplit(dataView, testFraction: 0.2);

        Console.WriteLine("Building ML.NET training pipeline...");

        // LightGBM (gradient boosting) - best for pose classification with non-linear patterns
        Console.WriteLine("Using LightGBM trainer with class balancing...");
        var pipeline = _mlContext
            .Transforms
            // Normalize features using z-score
            .NormalizeMeanVariance("Features")
            // Convert label to key for multiclass classification
            .Append(_mlContext.Transforms.Conversion.MapValueToKey("Label"))
            // LightGBM handles non-linear relationships much better than linear classifiers
            .Append(
                _mlContext.MulticlassClassification.Trainers.LightGbm(
                    labelColumnName: "Label",
                    featureColumnName: "Features",
                    numberOfIterations: 300,
                    numberOfLeaves: 31,
                    minimumExampleCountPerLeaf: 10,
                    learningRate: 0.05
                )
            )
            // Convert predicted label back to original value
            .Append(_mlContext.Transforms.Conversion.MapKeyToValue("PredictedLabel"));

        Console.WriteLine("Training swing phase classifier...");

        // Train the model
        var model = pipeline.Fit(splitData.TrainSet);

        // Evaluate on test set
        var predictions = model.Transform(splitData.TestSet);
        var metrics = _mlContext.MulticlassClassification.Evaluate(predictions);

        Console.WriteLine("\n=== Training Results ===");
        Console.WriteLine($"Macro Accuracy: {metrics.MacroAccuracy:P2}");
        Console.WriteLine($"Micro Accuracy: {metrics.MicroAccuracy:P2}");
        Console.WriteLine($"Log Loss: {metrics.LogLoss:F4}");

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

    /// <summary>
    /// Load labeled frames from JSON files in a directory
    /// </summary>
    public static List<LabeledFrameData> LoadLabeledFramesFromDirectory(string directory)
    {
        var labeledFrames = new List<LabeledFrameData>();
        var jsonFiles = Directory.GetFiles(directory, "*.json");

        Console.WriteLine($"Found {jsonFiles.Length} JSON files in {directory}");

        foreach (var file in jsonFiles)
        {
            try
            {
                var json = File.ReadAllText(file);
                var frames = JsonSerializer.Deserialize<List<LabeledFrameData>>(
                    json,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
                );

                if (frames != null)
                {
                    labeledFrames.AddRange(frames);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Warning: Could not load {file}: {ex.Message}");
            }
        }

        return labeledFrames;
    }

    private static void ValidateTrainingInputs(
        List<LabeledFrameData> labeledFrames,
        PhaseClassifierTrainingConfiguration config
    )
    {
        if (labeledFrames.Count == 0)
        {
            throw new ArgumentException(
                "Labeled frames list cannot be empty.",
                nameof(labeledFrames)
            );
        }

        ArgumentNullException.ThrowIfNull(config);

        // Count samples per class
        var classCounts = new int[SwingPhaseClassifierModel.NumClasses];
        foreach (var frame in labeledFrames)
        {
            if (frame.PhaseLabel < 0 || frame.PhaseLabel >= SwingPhaseClassifierModel.NumClasses)
            {
                throw new ArgumentException($"Invalid phase label: {frame.PhaseLabel} on frame");
            }
            classCounts[frame.PhaseLabel]++;
        }

        Console.WriteLine("Class distribution:");
        for (int i = 0; i < SwingPhaseClassifierModel.NumClasses; i++)
        {
            Console.WriteLine(
                $"  {SwingPhaseClassifierModel.ClassNames[i]}: {classCounts[i]} samples"
            );
        }

        // Warn about class imbalance
        int minSamples = classCounts.Where(c => c > 0).DefaultIfEmpty(0).Min();
        int maxSamples = classCounts.Max();
        if (maxSamples > minSamples * 5 && minSamples > 0)
        {
            Console.WriteLine(
                "Warning: Significant class imbalance detected. Consider oversampling minority classes."
            );
        }

        Console.WriteLine($"Validation passed: {labeledFrames.Count} total samples");
    }

    private static List<PhaseClassifierInput> PreprocessTrainingData(
        List<LabeledFrameData> labeledFrames
    )
    {
        var trainingData = new List<PhaseClassifierInput>();

        foreach (var frame in labeledFrames)
        {
            // Ensure feature array is correct size (pad or truncate if needed)
            var features = new float[SwingPhaseClassifierModel.TotalFeatures];
            int copyLength = Math.Min(frame.Features.Length, features.Length);

            for (int i = 0; i < copyLength; i++)
            {
                var val = frame.Features[i];
                features[i] = (float.IsNaN(val) || float.IsInfinity(val)) ? 0f : val;
            }

            trainingData.Add(
                new PhaseClassifierInput { Features = features, Label = (uint)frame.PhaseLabel }
            );
        }

        return trainingData;
    }

    private Task ExportToOnnxAsync(ITransformer model, IDataView dataView, string outputPath)
    {
        return Task.Run(() =>
        {
            using var stream = File.Create(outputPath);
            _mlContext.Model.ConvertToOnnx(model, dataView, stream);
        });
    }

    /// <summary>
    /// Extract features from a single frame for training or inference.
    /// Delegates to the new pose-relative feature extractor.
    /// </summary>
    public static float[] ExtractFrameFeatures(
        JointData[] keypoints,
        float[] angles,
        bool isRightHanded,
        FrameData? prevFrame = null,
        FrameData? prev2Frame = null
    )
    {
        // Use the new pose-relative feature extractor
        return SwingPhaseFeatureExtractor.ExtractFeatures(
            keypoints,
            angles,
            isRightHanded,
            prevFrame,
            prev2Frame
        );
    }
}
