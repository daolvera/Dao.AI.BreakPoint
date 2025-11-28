using Dao.AI.BreakPoint.ModelTraining.VideoAnalysis;
using Dao.AI.BreakPoint.Services.MoveNet;
using Dao.AI.BreakPoint.Services.SwingAnalyzer;

namespace Dao.AI.BreakPoint.ModelTraining;

internal class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("Starting Tennis Swing Analysis Training...");
        var options = ParseArguments(args);

        TrainingDatasetLoader datasetLoader = new(new OpenCvVideoProcessor());

        if (string.IsNullOrEmpty(options.VideoDirectory) || !Directory.Exists(options.VideoDirectory))
        {
            throw new ArgumentException($"Video directory not found: {options.VideoDirectory}");
        }

        Console.WriteLine($"Processing individual video labels from directory: {options.VideoDirectory}");
        List<ProcessedSwingVideo> processedSwingVideos = await datasetLoader.ProcessVideoDirectoryAsync(options.VideoDirectory, options.InputModelPath);

        if (processedSwingVideos.Count == 0)
        {
            Console.WriteLine("No processed swing videos available. Exiting.");
            return;
        }

        var poseExtractor = new MoveNetPoseFeatureExtractorService();
        var trainingService = new SwingModelTrainingService(poseExtractor);

        if (options.BatchSize > processedSwingVideos.Count)
        {
            options.BatchSize = processedSwingVideos.Count;
            Console.WriteLine($"Adjusted batch size to {options.BatchSize} due to limited training data.");
        }

        try
        {
            // Train the model
            var modelPath = await trainingService.TrainTensorFlowModelAsync(
                processedSwingVideos,
                options
            );

            Console.WriteLine($"Training completed! Model saved at: {modelPath}");
            Console.WriteLine($"Trained on {processedSwingVideos.Count} swing examples");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Training failed: {ex.Message}");
        }
    }

    private static TrainingConfiguration ParseArguments(string[] args)
    {
        var options = new TrainingConfiguration();

        foreach (var arg in args)
        {
            switch (arg.ToLower())
            {
                case "--video-dir":
                case "--directory":
                case "--dir":
                case "-d":
                    options.VideoDirectory = arg;
                    break;
                case "--movenet":
                case "-m":
                    options.InputModelPath = arg;
                    break;
                case "--epochs":
                case "-e":
                    if (int.TryParse(arg, out int epochs))
                    {
                        options.Epochs = epochs;
                    }
                    break;
                case "--output":
                case "-o":
                    options.ModelOutputPath = arg;
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
}
