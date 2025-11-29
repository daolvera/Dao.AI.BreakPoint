using Dao.AI.BreakPoint.Services.MoveNet;
using Dao.AI.BreakPoint.Services.VideoProcessing;
using System.Text.Json;

namespace Dao.AI.BreakPoint.ModelTraining.VideoAnalysis;

internal class TrainingDatasetLoader(IVideoProcessingService VideoProcessingService) : ITrainingDatasetLoader
{
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    /// <summary>
    /// Load training dataset from individual video/label files in a directory
    /// Separate out the original video into swing segments
    /// </summary>
    public async Task<List<TrainingSwingVideo>> ProcessVideoDirectoryAsync(string videoDirectory, string moveNetModelPath)
    {
        if (!Directory.Exists(videoDirectory))
        {
            throw new DirectoryNotFoundException($"Video directory not found: {videoDirectory}");
        }

        var processedTrainingSwingVideos = new List<TrainingSwingVideo>();
        var videoExtensions = new[] { ".mp4", ".avi", ".mov", ".mkv", ".webm" };

        var videoFiles = Directory.GetFiles(videoDirectory)
            .Where(f => videoExtensions.Contains(Path.GetExtension(f).ToLower()))
            .ToArray();

        using var processor = new MoveNetVideoProcessor(moveNetModelPath);

        foreach (var videoPath in videoFiles)
        {
            try
            {
                var labelPath = Path.ChangeExtension(videoPath, ".json");


                var label = await LoadVideoLabelAsync(labelPath);
                Console.WriteLine($"Processing video: {Path.GetFileName(videoPath)} (USTA Rating: {label.UstaRating})");

                // Extract frames from video
                var frameImages = VideoProcessingService.ExtractFrames(videoPath);

                if (frameImages.Count == 0)
                {
                    Console.WriteLine($"No frames extracted from video: {videoPath}");
                    continue;
                }

                // Get video metadata
                var metadata = VideoProcessingService.GetVideoMetadata(videoPath);

                // Split the videos in the different swings and analyze them
                processedTrainingSwingVideos.Add(new()
                {
                    TrainingLabel = label,
                    SwingVideo = processor.ProcessVideoFrames(frameImages, metadata),
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error processing video {videoPath}: {ex.Message}");
                continue;
            }
        }

        Console.WriteLine($"Successfully processed {processedTrainingSwingVideos.Count} videos from directory");
        return processedTrainingSwingVideos;
    }

    /// <summary>
    /// Load a single video label file
    /// </summary>
    public async Task<VideoLabel> LoadVideoLabelAsync(string labelPath)
    {
        if (!File.Exists(labelPath))
        {
            throw new FileNotFoundException($"Label file not found: {labelPath}");
        }

        var jsonContent = await File.ReadAllTextAsync(labelPath);
        var label = JsonSerializer.Deserialize<VideoLabel>(jsonContent, _jsonOptions)
            ?? throw new InvalidOperationException($"Failed to deserialize label from: {labelPath}");

        // Validate USTA rating range
        if (label.UstaRating < 1.0 || label.UstaRating > 7.0)
        {
            throw new ArgumentException($"Invalid USTA rating {label.UstaRating} in {labelPath}. Must be between 1.0 and 7.0");
        }

        return label;
    }

    /// <summary>
    /// Save a video label file
    /// </summary>
    public void SaveVideoLabel(VideoLabel label, string labelPath)
    {
        var jsonContent = JsonSerializer.Serialize(label, _jsonOptions);
        File.WriteAllText(labelPath, jsonContent);
        Console.WriteLine($"Video label saved to: {labelPath}");
    }
}