using Dao.AI.BreakPoint.ModelTraining.VideoAnalysis;
using Dao.AI.BreakPoint.Services.MoveNet;
using Dao.AI.BreakPoint.Services.SwingAnalyzer;

namespace Dao.AI.BreakPoint.ModelTraining;

internal class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("Starting Tennis Swing Analysis Training...");
        // Parse command line arguments
        var options = ParseArguments(args);

        List<RawSwingData> trainingData = await ProcessTrainingDataset(options.VideoDirectory, options.InputModelPath);

        if (trainingData.Count == 0)
        {
            Console.WriteLine("No training data available. Exiting.");
            return;
        }

        // Initialize services for feature extraction
        var poseExtractor = new MoveNetPoseFeatureExtractorService();
        var trainingService = new SwingModelTrainingService(poseExtractor);

        if (options.BatchSize > trainingData.Count)
        {
            options.BatchSize = trainingData.Count;
            Console.WriteLine($"Adjusted batch size to {options.BatchSize} due to limited training data.");
        }

        try
        {
            // Train the model
            var modelPath = await trainingService.TrainTensorFlowModelAsync(
                trainingData,
                options
            );

            Console.WriteLine($"Training completed! Model saved at: {modelPath}");
            Console.WriteLine($"Trained on {trainingData.Count} swing examples");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Training failed: {ex.Message}");
        }
    }

    private static TrainingConfiguration ParseArguments(string[] args)
    {
        var options = new TrainingConfiguration();

        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i].ToLower())
            {
                case "--video-dir":
                case "--directory":
                case "--dir":
                case "-d":
                    if (i + 1 < args.Length)
                    {
                        options.VideoDirectory = args[++i];
                    }
                    break;
                case "--movenet":
                case "-m":
                    if (i + 1 < args.Length)
                    {
                        options.InputModelPath = args[++i];
                    }
                    break;
                case "--epochs":
                case "-e":
                    if (i + 1 < args.Length && int.TryParse(args[++i], out int epochs))
                    {
                        options.Epochs = epochs;
                    }
                    break;
                case "--output":
                case "-o":
                    if (i + 1 < args.Length)
                    {
                        options.ModelOutputPath = args[++i];
                    }
                    break;
                case "--help":
                case "-h":
                    PrintUsage();
                    Environment.Exit(0);
                    break;
            }
        }

        return options;
    }

    private static void PrintUsage()
    {
        Console.WriteLine("Tennis Swing Analysis Model Training");
        Console.WriteLine("Flow: Video + USTA Rating → MoveNet → Feature Extraction → Training");
        Console.WriteLine();
        Console.WriteLine("Usage:");
        Console.WriteLine("  --video-dir|-d <path>   Path to directory with videos and individual label files");
        Console.WriteLine("  --movenet|-m <path>     Path to MoveNet model file (default: movenet/saved_model.pb)");
        Console.WriteLine("  --epochs|-e <number>    Number of training epochs (default: 5)");
        Console.WriteLine("  --output|-o <path>      Output model path (default: usta_swing_model.h5)");
        Console.WriteLine("  --help|-h               Show this help message");
        Console.WriteLine();
        Console.WriteLine("Example:");
        Console.WriteLine("  dotnet run --video-dir training_videos/ --movenet models/movenet.pb");
    }

    /// <summary>
    /// Process training dataset: Video + USTA Rating → MoveNet → Feature Extraction
    /// </summary>
    private static async Task<List<RawSwingData>> ProcessTrainingDataset(string videoDirectory, string moveNetModelPath)
    {
        var datasetLoader = new TrainingDatasetLoader(new OpenCvVideoProcessor());

        if (string.IsNullOrEmpty(videoDirectory) || !Directory.Exists(videoDirectory))
        {
            throw new ArgumentException($"Video directory not found: {videoDirectory}");
        }

        Console.WriteLine($"Processing individual video labels from directory: {videoDirectory}");
        var swingData = await datasetLoader.ProcessVideoDirectoryAsync(videoDirectory, moveNetModelPath);

        Console.WriteLine($"Successfully processed {swingData.Count} videos");

        return swingData;
    }
}
