using Dao.AI.BreakPoint.ModelTraining.VideoAnalysis;
using Dao.AI.BreakPoint.Services.MoveNet;
using Dao.AI.BreakPoint.Services.VideoProcessing;

namespace Dao.AI.BreakPoint.ModelTraining;

internal class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("Starting Tennis Swing Analysis Training...");
        var options = ParseArguments(args);

        if (options == null)
        {
            return; // Help was displayed or parsing failed
        }

        if (options.IsTestingHeuristicFeatures)
        {
            if (!Directory.Exists(options.ImageDirectory))
            {
                throw new DirectoryNotFoundException(
                    $"test images directory not found: {options.ImageDirectory}"
                );
            }
            var testImagePaths = Directory.GetFiles(options.ImageDirectory).ToArray();
            List<byte[]> testImages = [];
            foreach (var testImage in testImagePaths)
            {
                var imageBytes = await File.ReadAllBytesAsync(testImage);
                testImages.Add(imageBytes);
            }
            using var processor = new MoveNetVideoProcessor(options.InputModelPath);
            // Default to right-handed for testing; in production this comes from the label/player
            var processedVideo = processor.ProcessVideoFrames(
                testImages,
                new()
                {
                    Width = 1920,
                    Height = 1080,
                    FrameRate = 30,
                    TotalFrames = testImages.Count,
                },
                isRightHanded: true
            );
            return;
        }

        TrainingDatasetLoader datasetLoader = new(new OpenCvVideoProcessingService());

        if (
            string.IsNullOrEmpty(options.VideoDirectory)
            || !Directory.Exists(options.VideoDirectory)
        )
        {
            throw new ArgumentException($"Video directory not found: {options.VideoDirectory}");
        }

        Console.WriteLine(
            $"Processing individual video labels from directory: {options.VideoDirectory}"
        );
        List<TrainingSwingVideo> processedSwingVideos =
            await datasetLoader.ProcessVideoDirectoryAsync(
                options.VideoDirectory,
                options.InputModelPath
            );

        if (processedSwingVideos.Count == 0)
        {
            Console.WriteLine("No processed swing videos available. Exiting.");
            return;
        }

        var trainingService = new SwingModelTrainingService();

        if (options.BatchSize > processedSwingVideos.Count)
        {
            options.BatchSize = processedSwingVideos.Count;
            Console.WriteLine(
                $"Adjusted batch size to {options.BatchSize} due to limited training data."
            );
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
        int index = 0;
        foreach (var arg in args)
        {
            switch (arg.ToLower())
            {
                case "--video-dir":
                case "--directory":
                case "--dir":
                case "-d":
                    options.VideoDirectory = getNextArg();
                    break;
                case "--movenet":
                case "-m":
                    options.InputModelPath = getNextArg();
                    break;
                case "--output":
                case "-o":
                    options.ModelOutputPath = getNextArg();
                    break;
                case "--image":
                case "-i":
                    options.ImageDirectory = getNextArg();
                    break;
                case "--help":
                case "-h":
                    PrintUsage();
                    Environment.Exit(0);
                    break;
            }

            string getNextArg()
            {
                if (index + 1 < args.Length)
                {
                    return args[index + 1];
                }
                else
                {
                    throw new ArgumentException($"Expected value after {arg}");
                }
            }
            index++;
        }

        return options;
    }

    private static void PrintUsage()
    {
        Console.WriteLine("Tennis Swing Analysis Model Training");
        Console.WriteLine();
        Console.WriteLine("Usage:");
        Console.WriteLine(
            "  --video-dir|-d <path>   Path to directory with videos and individual label files"
        );
        Console.WriteLine(
            "  --movenet|-m <path>     Path to MoveNet model file (default: movenet/saved_model.pb)"
        );
        Console.WriteLine(
            "  --output|-o <path>      Output model path (default: usta_swing_model.h5)"
        );
        Console.WriteLine("  --help|-h               Show this help message");
        Console.WriteLine();
        Console.WriteLine("Example:");
        Console.WriteLine("  dotnet run --video-dir training_videos/ --movenet models/movenet.pb");
    }
}
