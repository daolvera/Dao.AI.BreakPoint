using System.Text.Json;
using Dao.AI.BreakPoint.Services.MoveNet;
using Dao.AI.BreakPoint.Services.SwingAnalyzer;

namespace Dao.AI.BreakPoint.ModelTraining.VideoAnalysis;

public interface ILabeledVideoDatasetLoader
{
    SimpleVideoDataset LoadSimpleDataset(string datasetPath);
    LabeledVideoDataset LoadDataset(string datasetPath);
    List<SwingData> ProcessSimpleVideos(SimpleVideoDataset dataset, string moveNetModelPath);
    List<SwingData> ProcessLabeledVideos(LabeledVideoDataset dataset, string moveNetModelPath);
    void SaveSimpleDataset(SimpleVideoDataset dataset, string outputPath);
    void SaveDataset(LabeledVideoDataset dataset, string outputPath);
}

public class LabeledVideoDatasetLoader : ILabeledVideoDatasetLoader
{
    private readonly IVideoProcessor _videoProcessor;

    public LabeledVideoDatasetLoader(IVideoProcessor? videoProcessor = null)
    {
        _videoProcessor = videoProcessor ?? new OpenCvVideoProcessor();
    }

    public SimpleVideoDataset LoadSimpleDataset(string datasetPath)
    {
        if (!File.Exists(datasetPath))
        {
            throw new FileNotFoundException($"Dataset file not found: {datasetPath}");
        }

        var jsonContent = File.ReadAllText(datasetPath);
        var dataset = JsonSerializer.Deserialize<SimpleVideoDataset>(jsonContent, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        if (dataset == null)
        {
            throw new InvalidOperationException($"Failed to deserialize dataset from: {datasetPath}");
        }

        // Validate video files exist
        var basePath = Path.GetDirectoryName(datasetPath) ?? "";
        foreach (var video in dataset.Videos)
        {
            var fullVideoPath = Path.IsPathRooted(video.Path) 
                ? video.Path 
                : Path.Combine(basePath, video.Path);
            
            if (!File.Exists(fullVideoPath))
            {
                Console.WriteLine($"Warning: Video file not found: {fullVideoPath}");
            }
            else
            {
                video.Path = fullVideoPath; // Update to full path
            }
        }

        Console.WriteLine($"Loaded simple dataset with {dataset.Videos.Count} videos");
        return dataset;
    }

    public List<SwingData> ProcessSimpleVideos(SimpleVideoDataset dataset, string moveNetModelPath)
    {
        var swingDataList = new List<SwingData>();

        using var processor = new MoveNetVideoProcessor(moveNetModelPath);

        foreach (var videoLabel in dataset.Videos)
        {
            try
            {
                Console.WriteLine($"Processing video: {Path.GetFileName(videoLabel.Path)} (Score: {videoLabel.Score})");

                if (!File.Exists(videoLabel.Path))
                {
                    Console.WriteLine($"Skipping missing video: {videoLabel.Path}");
                    continue;
                }

                // Extract frames from video
                var frameImages = _videoProcessor.ExtractFrames(videoLabel.Path, maxFrames: 300);

                if (frameImages.Count == 0)
                {
                    Console.WriteLine($"No frames extracted from video: {videoLabel.Path}");
                    continue;
                }

                // Get video metadata
                var metadata = _videoProcessor.GetVideoMetadata(videoLabel.Path);

                // Process with MoveNet to get pose data
                var frames = processor.ProcessVideoFrames(frameImages, metadata.Height, metadata.Width);

                // Detect contact frame automatically
                var contactFrame = ContactFrameDetector.DetectContactFrameAdvanced(frames);

                // Create SwingData from simple video label
                var swingData = new SwingData
                {
                    Frames = frames,
                    OverallScore = videoLabel.Score,
                    ContactFrame = contactFrame
                };

                swingDataList.Add(swingData);
                Console.WriteLine($"? Processed {frames.Count} frames, contact frame: {contactFrame}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"? Error processing video {videoLabel.Path}: {ex.Message}");
                continue;
            }
        }

        Console.WriteLine($"\nSuccessfully processed {swingDataList.Count} videos out of {dataset.Videos.Count}");
        return swingDataList;
    }

    public LabeledVideoDataset LoadDataset(string datasetPath)
    {
        if (!File.Exists(datasetPath))
        {
            throw new FileNotFoundException($"Dataset file not found: {datasetPath}");
        }

        var jsonContent = File.ReadAllText(datasetPath);
        var dataset = JsonSerializer.Deserialize<LabeledVideoDataset>(jsonContent, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        if (dataset == null)
        {
            throw new InvalidOperationException($"Failed to deserialize dataset from: {datasetPath}");
        }

        // Validate video files exist
        var basePath = Path.GetDirectoryName(datasetPath) ?? "";
        foreach (var video in dataset.Videos)
        {
            var fullVideoPath = Path.IsPathRooted(video.VideoPath) 
                ? video.VideoPath 
                : Path.Combine(basePath, video.VideoPath);
            
            if (!File.Exists(fullVideoPath))
            {
                Console.WriteLine($"Warning: Video file not found: {fullVideoPath}");
            }
            else
            {
                video.VideoPath = fullVideoPath; // Update to full path
            }
        }

        Console.WriteLine($"Loaded dataset with {dataset.Videos.Count} videos");
        return dataset;
    }

    public List<SwingData> ProcessLabeledVideos(LabeledVideoDataset dataset, string moveNetModelPath)
    {
        var swingDataList = new List<SwingData>();

        using var processor = new MoveNetVideoProcessor(moveNetModelPath);

        foreach (var videoLabel in dataset.Videos)
        {
            try
            {
                Console.WriteLine($"Processing video: {videoLabel.VideoId}");

                if (!File.Exists(videoLabel.VideoPath))
                {
                    Console.WriteLine($"Skipping missing video: {videoLabel.VideoPath}");
                    continue;
                }

                // Extract frames from video
                var frameImages = _videoProcessor.ExtractFrames(videoLabel.VideoPath, maxFrames: 300);

                if (frameImages.Count == 0)
                {
                    Console.WriteLine($"No frames extracted from video: {videoLabel.VideoPath}");
                    continue;
                }

                // Update metadata if not set
                if (videoLabel.Metadata.Width == 0)
                {
                    var metadata = _videoProcessor.GetVideoMetadata(videoLabel.VideoPath);
                    videoLabel.Metadata.Width = metadata.Width;
                    videoLabel.Metadata.Height = metadata.Height;
                    videoLabel.Metadata.FrameRate = metadata.FrameRate;
                    videoLabel.Metadata.TotalFrames = metadata.TotalFrames;
                    videoLabel.Metadata.DurationSeconds = metadata.DurationSeconds;
                }

                // Process with MoveNet to get pose data
                var frames = processor.ProcessVideoFrames(
                    frameImages, 
                    videoLabel.Metadata.Height, 
                    videoLabel.Metadata.Width
                );

                // Create SwingData from labeled video
                var swingData = new SwingData
                {
                    Frames = frames,
                    OverallScore = videoLabel.OverallScore,
                    ContactFrame = Math.Min(videoLabel.ContactFrame, frames.Count - 1)
                };

                swingDataList.Add(swingData);
                Console.WriteLine($"Processed {frames.Count} frames for video {videoLabel.VideoId}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error processing video {videoLabel.VideoId}: {ex.Message}");
                continue;
            }
        }

        Console.WriteLine($"Successfully processed {swingDataList.Count} videos out of {dataset.Videos.Count}");
        return swingDataList;
    }

    public void SaveSimpleDataset(SimpleVideoDataset dataset, string outputPath)
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        var jsonContent = JsonSerializer.Serialize(dataset, options);
        File.WriteAllText(outputPath, jsonContent);
        
        Console.WriteLine($"Simple dataset saved to: {outputPath}");
    }

    public void SaveDataset(LabeledVideoDataset dataset, string outputPath)
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        var jsonContent = JsonSerializer.Serialize(dataset, options);
        File.WriteAllText(outputPath, jsonContent);
        
        Console.WriteLine($"Dataset saved to: {outputPath}");
    }

    public static SimpleVideoDataset CreateSampleSimpleDataset(string videoDirectory)
    {
        var dataset = new SimpleVideoDataset
        {
            Videos = new List<SimpleVideoLabel>
            {
                new SimpleVideoLabel { Path = Path.Combine(videoDirectory, "swing_001.mp4"), Score = 9.2f },
                new SimpleVideoLabel { Path = Path.Combine(videoDirectory, "swing_002.mp4"), Score = 6.5f },
                new SimpleVideoLabel { Path = Path.Combine(videoDirectory, "swing_003.mp4"), Score = 7.8f },
                new SimpleVideoLabel { Path = Path.Combine(videoDirectory, "swing_004.mp4"), Score = 4.3f },
                new SimpleVideoLabel { Path = Path.Combine(videoDirectory, "swing_005.mp4"), Score = 8.1f }
            }
        };

        return dataset;
    }
}