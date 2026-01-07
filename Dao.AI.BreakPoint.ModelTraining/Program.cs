using Dao.AI.BreakPoint.ModelTraining.VideoAnalysis;
using Dao.AI.BreakPoint.Services.MoveNet;
using Dao.AI.BreakPoint.Services.VideoProcessing;

namespace Dao.AI.BreakPoint.ModelTraining;

internal class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("Starting Tennis Swing Analysis Training...");

        // Check if training phase classifier
        if (args.Contains("--train-phase-classifier") || args.Contains("-p"))
        {
            await TrainPhaseClassifierAsync(args);
            return;
        }
        if (args.Contains("--extract-frames") || args.Contains("-f"))
        {
            await ExtractFramesForLabelingAsync(args);
            return;
        }
        if (args.Contains("--augment") || args.Contains("-a"))
        {
            await AugmentLabeledDataAsync(args);
            return;
        }
        if (args.Contains("--reextract") || args.Contains("-r"))
        {
            await ReExtractFeaturesAsync(args);
            return;
        }

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
            using var processor = new MoveNetVideoProcessor(
                options.InputModelPath,
                options.PhaseClassifierModelPath
            );
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
                options.InputModelPath,
                options.PhaseClassifierModelPath
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
            // Train the model and export to ONNX
            var modelPath = await trainingService.TrainAsync(processedSwingVideos, options);

            Console.WriteLine($"Training completed! Model saved at: {modelPath}");
            Console.WriteLine(
                $"Trained on {processedSwingVideos.Sum(o => o.SwingVideo.Swings.Count)} Swings"
            );
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Training failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Train the swing phase classifier model from labeled frame data
    /// </summary>
    private static async Task TrainPhaseClassifierAsync(string[] args)
    {
        Console.WriteLine("Training Swing Phase Classifier...");
        var config = ParsePhaseClassifierArguments(args);

        if (!Directory.Exists(config.LabeledFramesDirectory))
        {
            throw new DirectoryNotFoundException(
                $"Labeled frames directory not found: {config.LabeledFramesDirectory}"
            );
        }

        // Load labeled frame data from JSON files
        var labeledFrames = await LoadLabeledFramesAsync(config);

        if (labeledFrames.Count == 0)
        {
            Console.WriteLine("No labeled frames found. Exiting.");
            Console.WriteLine();
            Console.WriteLine(
                "Expected format: JSON files in the labeled frames directory with structure:"
            );
            Console.WriteLine(
                "  { \"phase\": 0-4, \"keypoints\": [...], \"angles\": [...], \"isRightHanded\": true/false }"
            );
            return;
        }

        var trainingService = new SwingPhaseClassifierTrainingService();

        try
        {
            var modelPath = await trainingService.TrainAsync(labeledFrames, config);
            Console.WriteLine($"Phase classifier training completed! Model saved at: {modelPath}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Training failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Extract frames from videos and prepare them for labeling
    /// </summary>
    private static async Task ExtractFramesForLabelingAsync(string[] args)
    {
        Console.WriteLine("Extracting frames for labeling...");
        var config = ParseFrameExtractionArguments(args);

        if (!Directory.Exists(config.VideoDirectory))
        {
            throw new DirectoryNotFoundException(
                $"Video directory not found: {config.VideoDirectory}"
            );
        }

        var videoService = new OpenCvVideoProcessingService();
        var helper = new FrameLabelingHelper(config.MoveNetModelPath, config.OutputDirectory);

        var videoExtensions = new[] { ".mp4", ".avi", ".mov", ".mkv", ".webm" };
        var videoFiles = Directory
            .GetFiles(config.VideoDirectory)
            .Where(f => videoExtensions.Contains(Path.GetExtension(f).ToLower()))
            .ToArray();

        Console.WriteLine($"Found {videoFiles.Length} video files");

        foreach (var videoPath in videoFiles)
        {
            try
            {
                Console.WriteLine($"Processing: {Path.GetFileName(videoPath)}");

                var frameImages = videoService.ExtractFrames(videoPath);
                var metadata = videoService.GetVideoMetadata(videoPath);

                await helper.ExtractFramesForLabelingAsync(
                    videoPath,
                    frameImages,
                    metadata,
                    config.IsRightHanded,
                    config.SampleEveryNFrames
                );
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error processing {videoPath}: {ex.Message}");
            }
        }

        Console.WriteLine();
        Console.WriteLine($"Frames extracted to: {config.OutputDirectory}");
        Console.WriteLine();
        FrameLabelingHelper.PrintLabelingInstructions();
    }

    /// <summary>
    /// Re-extract features from existing labeled JSON files using the new feature extractor.
    /// Preserves labels but recomputes features.
    /// </summary>
    private static async Task ReExtractFeaturesAsync(string[] args)
    {
        Console.WriteLine("Re-extracting features from labeled data...");
        var config = ParseAugmentationArguments(args); // Reuse same config

        if (!Directory.Exists(config.LabeledDataDirectory))
        {
            throw new DirectoryNotFoundException(
                $"Labeled data directory not found: {config.LabeledDataDirectory}"
            );
        }

        if (!Directory.Exists(config.VideoDirectory))
        {
            throw new DirectoryNotFoundException(
                $"Video directory not found: {config.VideoDirectory}"
            );
        }

        var helper = new TemporalAugmentationHelper(
            config.MoveNetModelPath,
            config.VideoDirectory,
            config.LabeledDataDirectory
        );

        await helper.ReExtractFeaturesAsync(config.OutputDirectory);
        helper.PrintClassDistribution();
    }

    /// <summary>
    /// Augment labeled data by extracting adjacent frames with same labels
    /// </summary>
    private static async Task AugmentLabeledDataAsync(string[] args)
    {
        Console.WriteLine("Augmenting labeled data with adjacent frames...");
        var config = ParseAugmentationArguments(args);

        if (!Directory.Exists(config.LabeledDataDirectory))
        {
            throw new DirectoryNotFoundException(
                $"Labeled data directory not found: {config.LabeledDataDirectory}"
            );
        }

        if (!Directory.Exists(config.VideoDirectory))
        {
            throw new DirectoryNotFoundException(
                $"Video directory not found: {config.VideoDirectory}"
            );
        }

        var helper = new TemporalAugmentationHelper(
            config.MoveNetModelPath,
            config.VideoDirectory,
            config.LabeledDataDirectory
        );

        await helper.AugmentLabeledDataAsync(config.OffsetFrames, config.OutputDirectory);

        helper.PrintClassDistribution();
    }

    private static AugmentationConfig ParseAugmentationArguments(string[] args)
    {
        var config = new AugmentationConfig();
        int index = 0;

        foreach (var arg in args)
        {
            switch (arg.ToLower())
            {
                case "--labeled-data":
                case "-l":
                    config.LabeledDataDirectory = GetNextArg(args, index);
                    break;
                case "--video-dir":
                case "-d":
                    config.VideoDirectory = GetNextArg(args, index);
                    break;
                case "--output":
                case "-o":
                    config.OutputDirectory = GetNextArg(args, index);
                    break;
                case "--movenet":
                case "-m":
                    config.MoveNetModelPath = GetNextArg(args, index);
                    break;
                case "--offset":
                case "-n":
                    config.OffsetFrames = int.Parse(GetNextArg(args, index));
                    break;
            }
            index++;
        }

        return config;
    }

    private static FrameExtractionConfig ParseFrameExtractionArguments(string[] args)
    {
        var config = new FrameExtractionConfig();
        int index = 0;

        foreach (var arg in args)
        {
            switch (arg.ToLower())
            {
                case "--video-dir":
                case "-d":
                    config.VideoDirectory = GetNextArg(args, index);
                    break;
                case "--output":
                case "-o":
                    config.OutputDirectory = GetNextArg(args, index);
                    break;
                case "--movenet":
                case "-m":
                    config.MoveNetModelPath = GetNextArg(args, index);
                    break;
                case "--sample-rate":
                case "-s":
                    config.SampleEveryNFrames = int.Parse(GetNextArg(args, index));
                    break;
                case "--left-handed":
                    config.IsRightHanded = false;
                    break;
            }
            index++;
        }

        return config;
    }

    /// <summary>
    /// Load labeled frame data from the labeled frames directory
    /// Expected format: JSON files with frame data and phase labels
    /// </summary>
    private static async Task<List<LabeledFrameData>> LoadLabeledFramesAsync(
        PhaseClassifierTrainingConfiguration config
    )
    {
        var labeledFrames = new List<LabeledFrameData>();
        var jsonFiles = Directory.GetFiles(config.LabeledFramesDirectory, "*.json");

        Console.WriteLine(
            $"Found {jsonFiles.Length} JSON files in {config.LabeledFramesDirectory}"
        );

        foreach (var jsonFile in jsonFiles)
        {
            try
            {
                var json = await File.ReadAllTextAsync(jsonFile);
                var frameData = System.Text.Json.JsonSerializer.Deserialize<LabeledFrameJson>(json);

                if (frameData != null && frameData.Features != null)
                {
                    labeledFrames.Add(
                        new LabeledFrameData
                        {
                            PhaseLabel = frameData.Phase,
                            Features = frameData.Features,
                        }
                    );
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Warning: Failed to load {jsonFile}: {ex.Message}");
            }
        }

        return labeledFrames;
    }

    private static PhaseClassifierTrainingConfiguration ParsePhaseClassifierArguments(string[] args)
    {
        var config = new PhaseClassifierTrainingConfiguration();
        int index = 0;

        foreach (var arg in args)
        {
            switch (arg.ToLower())
            {
                case "--labeled-frames":
                case "-l":
                    config.LabeledFramesDirectory = GetNextArg(args, index);
                    break;
                case "--output":
                case "-o":
                    config.ModelOutputPath = GetNextArg(args, index);
                    break;
            }
            index++;
        }

        return config;
    }

    private static string GetNextArg(string[] args, int currentIndex)
    {
        if (currentIndex + 1 < args.Length)
        {
            return args[currentIndex + 1];
        }
        throw new ArgumentException($"Expected value after {args[currentIndex]}");
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
                    options.VideoDirectory = GetNextArg(args, index);
                    break;
                case "--movenet":
                case "-m":
                    options.InputModelPath = GetNextArg(args, index);
                    break;
                case "--phase-classifier":
                case "-c":
                    options.PhaseClassifierModelPath = GetNextArg(args, index);
                    break;
                case "--output":
                case "-o":
                    options.ModelOutputPath = GetNextArg(args, index);
                    break;
                case "--image":
                case "-i":
                    options.ImageDirectory = GetNextArg(args, index);
                    break;
                case "--help":
                case "-h":
                    PrintUsage();
                    Environment.Exit(0);
                    break;
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
        Console.WriteLine();
        Console.WriteLine("  SWING QUALITY MODEL (default):");
        Console.WriteLine(
            "    --video-dir|-d <path>       Path to directory with videos and individual label files"
        );
        Console.WriteLine(
            "    --movenet|-m <path>         Path to MoveNet model file (default: movenet/saved_model.pb)"
        );
        Console.WriteLine(
            "    --phase-classifier|-c <path>  Path to swing phase classifier model (required)"
        );
        Console.WriteLine(
            "    --output|-o <path>          Output model path (default: swing_model)"
        );
        Console.WriteLine();
        Console.WriteLine("  SWING PHASE CLASSIFIER:");
        Console.WriteLine(
            "    --train-phase-classifier|-p   Train the swing phase classifier instead"
        );
        Console.WriteLine(
            "    --labeled-frames|-l <path>    Path to directory with labeled frame JSON files"
        );
        Console.WriteLine(
            "    --max-iterations|-i <num>     Max training iterations (default: 100)"
        );
        Console.WriteLine("    --l2 <float>                  L2 regularization (default: 0.1)");
        Console.WriteLine();
        Console.WriteLine("  FRAME EXTRACTION (for labeling):");
        Console.WriteLine(
            "    --extract-frames|-f           Extract frames from videos for manual labeling"
        );
        Console.WriteLine("    --video-dir|-d <path>         Path to directory with video files");
        Console.WriteLine(
            "    --output|-o <path>            Output directory for JSON files (default: labeled_frames)"
        );
        Console.WriteLine("    --sample-rate|-s <num>        Extract every N frames (default: 5)");
        Console.WriteLine("    --left-handed                 Mark frames as left-handed player");
        Console.WriteLine();
        Console.WriteLine("  DATA AUGMENTATION:");
        Console.WriteLine(
            "    --augment|-a                  Augment labeled data by extracting adjacent frames"
        );
        Console.WriteLine(
            "    --labeled-data|-l <path>      Path to directory with labeled frame JSON files (default: phasedata)"
        );
        Console.WriteLine(
            "    --video-dir|-d <path>         Path to directory with video files (default: data)"
        );
        Console.WriteLine(
            "    --offset|-n <num>             Number of frames before/after to extract (default: 1)"
        );
        Console.WriteLine(
            "    --output|-o <path>            Output directory (default: same as labeled-data)"
        );
        Console.WriteLine();
        Console.WriteLine("  FEATURE RE-EXTRACTION:");
        Console.WriteLine(
            "    --reextract|-r                Re-extract features from existing labeled JSON files"
        );
        Console.WriteLine(
            "    --labeled-data|-l <path>      Path to directory with labeled frame JSON files (default: phasedata)"
        );
        Console.WriteLine(
            "    --video-dir|-d <path>         Path to directory with video files (default: data)"
        );
        Console.WriteLine(
            "    --output|-o <path>            Output directory (default: overwrites input files)"
        );
        Console.WriteLine();
        Console.WriteLine("  --help|-h               Show this help message");
        Console.WriteLine();
        Console.WriteLine("Examples:");
        Console.WriteLine("  dotnet run --video-dir training_videos/ --movenet models/movenet.pb");
        Console.WriteLine(
            "  dotnet run -p --labeled-frames data/labeled_frames/ -o phase_classifier"
        );
        Console.WriteLine("  dotnet run -f -d videos/ -o labeled_frames/ -s 10");
        Console.WriteLine("  dotnet run -a -l phasedata -d data -n 2");
        Console.WriteLine("  dotnet run -r -l phasedata -d data");
    }
}

/// <summary>
/// Configuration for frame extraction
/// </summary>
internal class FrameExtractionConfig
{
    public string VideoDirectory { get; set; } = "data";
    public string OutputDirectory { get; set; } = "labeled_frames";
    public string MoveNetModelPath { get; set; } = "movenet/saved_model.pb";
    public int SampleEveryNFrames { get; set; } = 50;
    public bool IsRightHanded { get; set; } = true;
}

/// <summary>
/// JSON structure for labeled frame files
/// </summary>
internal class LabeledFrameJson
{
    public int Phase { get; set; }
    public float[]? Features { get; set; }
}

/// <summary>
/// Configuration for data augmentation
/// </summary>
internal class AugmentationConfig
{
    public string LabeledDataDirectory { get; set; } = "phasedata";
    public string VideoDirectory { get; set; } = "data";
    public string? OutputDirectory { get; set; } = null; // Default: same as LabeledDataDirectory
    public string MoveNetModelPath { get; set; } = "movenet/saved_model.pb";
    public int OffsetFrames { get; set; } = 1;
}
