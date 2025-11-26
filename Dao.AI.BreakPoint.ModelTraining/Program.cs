using Dao.AI.BreakPoint.ModelTraining.VideoAnalysis;
using Dao.AI.BreakPoint.Services.MoveNet;
using Dao.AI.BreakPoint.Services.SwingAnalyzer;
using System.Text.Json;

namespace Dao.AI.BreakPoint.ModelTraining;

internal class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("Starting TensorFlow.NET Swing Analysis Training...");

        // Parse command line arguments
        var options = ParseArguments(args);

        List<SwingData> trainingData;

        if (options.UseRealVideo && !string.IsNullOrEmpty(options.DatasetPath))
        {
            // Load labeled video dataset
            Console.WriteLine($"Loading labeled video dataset: {options.DatasetPath}");
            trainingData = ProcessLabeledVideoDataset(options.DatasetPath, options.MoveNetModelPath);
        }
        else if (options.UseRealVideo && !string.IsNullOrEmpty(options.VideoPath))
        {
            // Process single video (legacy mode)
            Console.WriteLine($"Processing single video: {options.VideoPath}");
            trainingData = ProcessRealVideoData(options.VideoPath, options.MoveNetModelPath);
        }
        else
        {
            // Use dummy data
            Console.WriteLine("Using dummy training data");
            trainingData = CreateDummyTrainingData();
        }

        if (trainingData.Count == 0)
        {
            Console.WriteLine("No training data available. Exiting.");
            return;
        }

        // Initialize services
        var poseExtractor = new MoveNetPoseFeatureExtractorService();
        var trainingService = new SwingModelTrainingService(poseExtractor);

        // Configuration
        var config = new TensorFlowTrainingConfiguration
        {
            SequenceLength = 30,
            BatchSize = Math.Min(4, trainingData.Count), // Adjust batch size based on data
            Epochs = options.Epochs,
            ModelOutputPath = options.ModelOutputPath,
        };

        try
        {
            // Train the model
            var modelPath = await trainingService.TrainTensorFlowModelAsync(
                trainingData,
                config,
                imageHeight: 480,
                imageWidth: 640
            );

            Console.WriteLine($"Training completed! Model saved at: {modelPath}");
            Console.WriteLine($"Trained on {trainingData.Count} swing examples");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Training failed: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
        }
    }

    private static TrainingConfiguration ParseArguments(string[] args)
    {
        var options = new TrainingConfiguration();

        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i].ToLower())
            {
                case "--dataset":
                case "-d":
                    if (i + 1 < args.Length)
                    {
                        options.DatasetPath = args[++i];
                    }
                    break;
                case "--video":
                case "-v":
                    if (i + 1 < args.Length)
                    {
                        options.VideoPath = args[++i];
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
        Console.WriteLine("Usage:");
        Console.WriteLine("  --dataset|-d <path>     Path to labeled video dataset JSON file");
        Console.WriteLine("  --video|-v <path>       Path to single video file (legacy mode)");
        Console.WriteLine("  --movenet|-m <path>     Path to MoveNet model file (default: movenet/saved_model.pb)");
        Console.WriteLine("  --epochs|-e <number>    Number of training epochs (default: 5)");
        Console.WriteLine("  --output|-o <path>      Output model path (default: test_swing_model.h5)");
        Console.WriteLine("  --create-sample         Create sample dataset file");
        Console.WriteLine("  --help|-h               Show this help message");
        Console.WriteLine();
        Console.WriteLine("Examples:");
        Console.WriteLine("  dotnet run --dataset videos/dataset.json --movenet models/movenet.pb");
        Console.WriteLine("  dotnet run --video sample.mp4 --movenet models/movenet.pb");
        Console.WriteLine("  dotnet run --create-sample");
    }

    /// <summary>
    /// Process labeled video dataset
    /// </summary>
    private static List<SwingData> ProcessLabeledVideoDataset(string datasetPath, string moveNetModelPath)
    {
        try
        {
            var datasetLoader = new LabeledVideoDatasetLoader();

            // Try to load as simple dataset first
            try
            {
                var simpleDataset = datasetLoader.LoadSimpleDataset(datasetPath);
                var swingData = datasetLoader.ProcessSimpleVideos(simpleDataset, moveNetModelPath);
                Console.WriteLine($"✓ Loaded {swingData.Count} videos from simple dataset format");
                return swingData;
            }
            catch (JsonException)
            {
                // Fall back to complex dataset format
                Console.WriteLine("Simple format failed, trying complex dataset format...");
                var dataset = datasetLoader.LoadDataset(datasetPath);
                var swingData = datasetLoader.ProcessLabeledVideos(dataset, moveNetModelPath);
                Console.WriteLine($"✓ Loaded {swingData.Count} videos from complex dataset format");
                return swingData;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error processing labeled video dataset: {ex.Message}");
            Console.WriteLine("Falling back to dummy data...");
            return CreateDummyTrainingData();
        }
    }

    /// <summary>
    /// Process real video data using MoveNet for pose estimation (legacy single video mode)
    /// </summary>
    private static List<SwingData> ProcessRealVideoData(string videoPath, string moveNetModelPath)
    {
        try
        {
            Console.WriteLine("Processing single video with MoveNet...");

            using var processor = new MoveNetVideoProcessor(moveNetModelPath);
            var videoProcessor = new OpenCvVideoProcessor();

            // Extract frames from video
            var frameImages = videoProcessor.ExtractFrames(videoPath, maxFrames: 300);

            if (frameImages.Count == 0)
            {
                throw new InvalidOperationException("No frames extracted from video");
            }

            // Get video metadata
            var metadata = videoProcessor.GetVideoMetadata(videoPath);

            // Process with MoveNet to get pose data
            var frames = processor.ProcessVideoFrames(frameImages, metadata.Height, metadata.Width);

            // Create SwingData objects
            var swingData = new List<SwingData>
            {
                new SwingData
                {
                    Frames = frames,
                    OverallScore = 7.5f, // Default score for single video
                    ContactFrame = DetectContactFrame(frames)
                }
            };

            Console.WriteLine($"Processed {frames.Count} frames from single video");
            return swingData;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error processing single video: {ex.Message}");
            Console.WriteLine("Falling back to dummy data...");
            return CreateDummyTrainingData();
        }
    }

    /// <summary>
    /// Detect the contact frame in a swing sequence using multiple analysis methods
    /// </summary>
    private static int DetectContactFrame(List<FrameData> frames)
    {
        if (frames.Count == 0)
        {
            return 0;
        }

        try
        {
            // Use advanced multi-method contact frame detection
            var result = ContactFrameDetector.DetectContactFrameMultiMethod(frames);

            Console.WriteLine($"Contact frame detection:");
            Console.WriteLine($"  Final result: {result.ContactFrame} (confidence: {result.Confidence:F2})");
            Console.WriteLine($"  Velocity method: {result.VelocityMethod}");
            Console.WriteLine($"  Trajectory method: {result.TrajectoryMethod}");
            Console.WriteLine($"  Advanced method: {result.AdvancedMethod}");
            Console.WriteLine($"  Position method: {result.PositionMethod}");

            return result.ContactFrame;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in contact frame detection: {ex.Message}");
            Console.WriteLine("Using fallback method (middle of sequence)");
            return frames.Count / 2;
        }
    }

    private static List<SwingData> CreateDummyTrainingData()
    {
        var random = new Random(42);
        var trainingData = new List<SwingData>();

        // Create 10 dummy swings for testing
        for (int i = 0; i < 10; i++)
        {
            var frames = new List<FrameData>();

            // Create 30 dummy frames per swing
            for (int frameIdx = 0; frameIdx < 30; frameIdx++)
            {
                var poseFeatures = new SwingPoseFeatures[17]; // 17 joints

                for (int jointIdx = 0; jointIdx < 17; jointIdx++)
                {
                    poseFeatures[jointIdx] = new SwingPoseFeatures
                    {
                        X = (float)(random.NextDouble() * 640), // Random X position
                        Y = (float)(random.NextDouble() * 480), // Random Y position
                        Confidence = (float)(0.7 + (random.NextDouble() * 0.3)), // High confidence
                    };
                }

                frames.Add(
                    new FrameData { SwingPoseFeatures = poseFeatures, FrameNumber = frameIdx }
                );
            }

            trainingData.Add(
                new SwingData
                {
                    Frames = frames,
                    OverallScore = 3.0f + (float)(random.NextDouble() * 4.0), // Score between 3-7
                    ContactFrame = 15 + random.Next(-3, 4), // Contact around frame 15
                }
            );
        }

        Console.WriteLine($"Created {trainingData.Count} dummy training examples");
        return trainingData;
    }
}
