using System.Text.Json;
using System.Text.RegularExpressions;
using Dao.AI.BreakPoint.Services.MoveNet;
using Dao.AI.BreakPoint.Services.SwingAnalyzer;
using Dao.AI.BreakPoint.Services.VideoProcessing;

namespace Dao.AI.BreakPoint.ModelTraining;

/// <summary>
/// Helper service to augment labeled training data by extracting adjacent frames
/// and assigning them the same phase label as the hand-labeled frames.
/// This is a common temporal augmentation technique since consecutive frames
/// in a video are very similar and almost certainly in the same swing phase.
/// </summary>
public class TemporalAugmentationHelper
{
    private readonly string _moveNetModelPath;
    private readonly string _videoDirectory;
    private readonly string _labeledDataDirectory;
    private readonly IVideoProcessingService _videoService;

    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
    };

    public TemporalAugmentationHelper(
        string moveNetModelPath,
        string videoDirectory,
        string labeledDataDirectory
    )
    {
        _moveNetModelPath = moveNetModelPath;
        _videoDirectory = videoDirectory;
        _labeledDataDirectory = labeledDataDirectory;
        _videoService = new OpenCvVideoProcessingService();
    }

    /// <summary>
    /// Augment existing labeled data by extracting adjacent frames from videos.
    /// For each labeled frame, extracts frames before and after and assigns the same label.
    /// </summary>
    /// <param name="offsetFrames">Number of frames before/after to extract (default: 1)</param>
    /// <param name="outputDirectory">Where to save augmented frames (default: same as labeled data)</param>
    public async Task AugmentLabeledDataAsync(int offsetFrames = 1, string? outputDirectory = null)
    {
        outputDirectory ??= _labeledDataDirectory;

        if (!Directory.Exists(outputDirectory))
        {
            Directory.CreateDirectory(outputDirectory);
        }

        // Get all labeled JSON files
        var labeledFiles = Directory.GetFiles(_labeledDataDirectory, "*.json");
        Console.WriteLine($"Found {labeledFiles.Length} labeled frame files");

        // Group files by video name for efficient processing
        var framesByVideo = GroupFramesByVideo(labeledFiles);

        Console.WriteLine($"Processing {framesByVideo.Count} videos for augmentation...");
        Console.WriteLine(
            $"Extracting {offsetFrames} frame(s) before and after each labeled frame"
        );
        Console.WriteLine();

        int totalAugmented = 0;
        int totalSkipped = 0;

        foreach (var (videoName, frames) in framesByVideo)
        {
            var result = await ProcessVideoForAugmentationAsync(
                videoName,
                frames,
                offsetFrames,
                outputDirectory
            );

            totalAugmented += result.augmented;
            totalSkipped += result.skipped;
        }

        Console.WriteLine();
        Console.WriteLine($"=== Augmentation Complete ===");
        Console.WriteLine($"Total new samples created: {totalAugmented}");
        Console.WriteLine($"Skipped (already exists or out of bounds): {totalSkipped}");
    }

    /// <summary>
    /// Re-extract features for all existing labeled JSON files using the new feature extractor.
    /// Preserves the phase labels but recomputes features from the source videos.
    /// </summary>
    /// <param name="outputDirectory">Where to save updated files (default: same as input, overwrites)</param>
    public async Task ReExtractFeaturesAsync(string? outputDirectory = null)
    {
        outputDirectory ??= _labeledDataDirectory;

        if (!Directory.Exists(outputDirectory))
        {
            Directory.CreateDirectory(outputDirectory);
        }

        var labeledFiles = Directory.GetFiles(_labeledDataDirectory, "*.json");
        Console.WriteLine($"Found {labeledFiles.Length} labeled frame files to re-extract");

        var framesByVideo = GroupFramesByVideo(labeledFiles);
        Console.WriteLine($"Processing {framesByVideo.Count} videos...");
        Console.WriteLine();

        int totalProcessed = 0;
        int totalFailed = 0;

        foreach (var (videoName, frames) in framesByVideo)
        {
            var result = await ReExtractVideoFramesAsync(videoName, frames, outputDirectory);
            totalProcessed += result.processed;
            totalFailed += result.failed;
        }

        Console.WriteLine();
        Console.WriteLine($"=== Re-extraction Complete ===");
        Console.WriteLine($"Total frames re-extracted: {totalProcessed}");
        Console.WriteLine($"Failed: {totalFailed}");
    }

    /// <summary>
    /// Re-extract features for frames from a single video
    /// </summary>
    private async Task<(int processed, int failed)> ReExtractVideoFramesAsync(
        string videoName,
        List<LabeledFrameInfo> labeledFrames,
        string outputDirectory
    )
    {
        var videoPath = FindVideoFile(videoName);
        if (videoPath is null)
        {
            Console.WriteLine($"Warning: Could not find video for {videoName}");
            return (0, labeledFrames.Count);
        }

        Console.WriteLine($"Processing: {videoName} ({labeledFrames.Count} frames)");

        var metadata = _videoService.GetVideoMetadata(videoPath);

        // Get all frame indices we need
        var frameIndices = labeledFrames.Select(f => f.FrameIndex).ToHashSet();

        // Extract and process frames
        var frameData = await ExtractAndProcessFramesAsync(
            videoPath,
            frameIndices,
            metadata,
            labeledFrames[0].IsRightHanded
        );

        int processed = 0;
        int failed = 0;

        foreach (var labeled in labeledFrames)
        {
            if (!frameData.TryGetValue(labeled.FrameIndex, out var data))
            {
                Console.WriteLine($"  Warning: Could not extract frame {labeled.FrameIndex}");
                failed++;
                continue;
            }

            var outputPath = Path.Combine(
                outputDirectory,
                $"{videoName}_frame_{labeled.FrameIndex:D5}.json"
            );

            var updatedFrame = new UnlabeledFrameJson
            {
                VideoName = videoName,
                FrameIndex = labeled.FrameIndex,
                Timestamp = labeled.FrameIndex / metadata.FrameRate,
                IsRightHanded = labeled.IsRightHanded,
                Features = data.Features,
                Phase = labeled.PhaseLabel, // Preserve the original label!
            };

            var json = JsonSerializer.Serialize(updatedFrame, _jsonOptions);
            await File.WriteAllTextAsync(outputPath, json);
            processed++;
        }

        Console.WriteLine($"  Re-extracted {processed} frames");
        return (processed, failed);
    }

    /// <summary>
    /// Group labeled files by their source video name
    /// </summary>
    private Dictionary<string, List<LabeledFrameInfo>> GroupFramesByVideo(string[] labeledFiles)
    {
        var grouped = new Dictionary<string, List<LabeledFrameInfo>>();

        // Pattern to extract video name and frame index from filename
        // e.g., "federer_forehand_frame_00043.json" -> videoName: "federer_forehand", frameIndex: 43
        var filenamePattern = new Regex(@"^(.+)_frame_(\d+)\.json$", RegexOptions.IgnoreCase);

        foreach (var file in labeledFiles)
        {
            var filename = Path.GetFileName(file);
            var match = filenamePattern.Match(filename);

            if (!match.Success)
            {
                Console.WriteLine($"Warning: Could not parse filename: {filename}");
                continue;
            }

            var videoName = match.Groups[1].Value;
            var frameIndex = int.Parse(match.Groups[2].Value);

            // Load the frame data to get the phase label
            try
            {
                var json = File.ReadAllText(file);
                var frameData = JsonSerializer.Deserialize<UnlabeledFrameJson>(json, _jsonOptions);

                if (frameData is null || frameData.Phase < 0)
                {
                    Console.WriteLine($"Warning: Unlabeled or invalid frame: {filename}");
                    continue;
                }

                if (!grouped.ContainsKey(videoName))
                {
                    grouped[videoName] = [];
                }

                grouped[videoName]
                    .Add(
                        new LabeledFrameInfo
                        {
                            FrameIndex = frameIndex,
                            PhaseLabel = frameData.Phase,
                            IsRightHanded = frameData.IsRightHanded,
                            FilePath = file,
                        }
                    );
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Warning: Could not load {filename}: {ex.Message}");
            }
        }

        return grouped;
    }

    /// <summary>
    /// Process a single video to create augmented frames
    /// </summary>
    private async Task<(int augmented, int skipped)> ProcessVideoForAugmentationAsync(
        string videoName,
        List<LabeledFrameInfo> labeledFrames,
        int offsetFrames,
        string outputDirectory
    )
    {
        // Find the video file
        var videoPath = FindVideoFile(videoName);
        if (videoPath is null)
        {
            Console.WriteLine($"Warning: Could not find video for {videoName}");
            return (0, labeledFrames.Count * offsetFrames * 2);
        }

        Console.WriteLine($"Processing: {videoName} ({labeledFrames.Count} labeled frames)");

        // Get video metadata
        var metadata = _videoService.GetVideoMetadata(videoPath);

        // Collect all frame indices we need to extract
        var framesToExtract = CollectFramesToExtract(
            labeledFrames,
            offsetFrames,
            metadata.TotalFrames
        );

        if (framesToExtract.Count == 0)
        {
            Console.WriteLine($"  No new frames to extract for {videoName}");
            return (0, 0);
        }

        // Extract the specific frames we need
        var frameData = await ExtractAndProcessFramesAsync(
            videoPath,
            framesToExtract,
            metadata,
            labeledFrames[0].IsRightHanded
        );

        // Save augmented frames
        int augmented = 0;
        int skipped = 0;

        foreach (var (frameIndex, data) in frameData)
        {
            // Find the source frame this was based on
            var sourceFrame = FindNearestLabeledFrame(labeledFrames, frameIndex);
            if (sourceFrame is null)
            {
                skipped++;
                continue;
            }

            var outputPath = Path.Combine(
                outputDirectory,
                $"{videoName}_frame_{frameIndex:D5}.json"
            );

            // Skip if file already exists
            if (File.Exists(outputPath))
            {
                skipped++;
                continue;
            }

            var augmentedFrame = new UnlabeledFrameJson
            {
                VideoName = videoName,
                FrameIndex = frameIndex,
                Timestamp = frameIndex / metadata.FrameRate,
                IsRightHanded = sourceFrame.IsRightHanded,
                Features = data.Features,
                Phase = sourceFrame.PhaseLabel,
            };

            var json = JsonSerializer.Serialize(augmentedFrame, _jsonOptions);
            await File.WriteAllTextAsync(outputPath, json);
            augmented++;
        }

        Console.WriteLine($"  Created {augmented} augmented samples, skipped {skipped}");
        return (augmented, skipped);
    }

    /// <summary>
    /// Collect all frame indices that need to be extracted (adjacent to labeled frames)
    /// </summary>
    private HashSet<int> CollectFramesToExtract(
        List<LabeledFrameInfo> labeledFrames,
        int offsetFrames,
        int totalFrames
    )
    {
        var framesToExtract = new HashSet<int>();

        foreach (var labeled in labeledFrames)
        {
            for (int offset = -offsetFrames; offset <= offsetFrames; offset++)
            {
                if (offset == 0)
                    continue; // Skip the already-labeled frame

                int targetFrame = labeled.FrameIndex + offset;

                // Bounds check
                if (targetFrame >= 1 && targetFrame < totalFrames)
                {
                    // Check if this frame already exists
                    var existingPath = Path.Combine(
                        _labeledDataDirectory,
                        $"{GetVideoNameFromPath(labeled.FilePath)}_frame_{targetFrame:D5}.json"
                    );

                    if (!File.Exists(existingPath))
                    {
                        framesToExtract.Add(targetFrame);
                    }
                }
            }
        }

        return framesToExtract;
    }

    /// <summary>
    /// Extract and process specific frames from a video
    /// </summary>
    private async Task<Dictionary<int, ProcessedFrameData>> ExtractAndProcessFramesAsync(
        string videoPath,
        HashSet<int> frameIndices,
        VideoMetadata metadata,
        bool isRightHanded
    )
    {
        var result = new Dictionary<int, ProcessedFrameData>();

        if (frameIndices.Count == 0)
            return result;

        // Sort frame indices for sequential processing
        var sortedIndices = frameIndices.OrderBy(i => i).ToList();

        // Determine range to extract (with buffer for temporal context)
        int minFrame = Math.Max(0, sortedIndices.Min() - 2);
        int maxFrame = Math.Min(metadata.TotalFrames - 1, sortedIndices.Max() + 2);

        Console.WriteLine($"  Extracting frames {minFrame}-{maxFrame} from video...");

        // Extract the frame range
        var frameImages = _videoService.ExtractFrameRange(videoPath, minFrame, maxFrame);

        // Process frames using MoveNet
        var cropRegion = CropRegion.InitCropRegion(metadata.Height, metadata.Width);
        float deltaTime = 1.0f / metadata.FrameRate;

        using var inferenceService = new MoveNetInferenceService(_moveNetModelPath);

        FrameData? prevFrame = null;
        FrameData? prev2Frame = null;

        for (int i = 0; i < frameImages.Count; i++)
        {
            int actualFrameIndex = minFrame + i;
            var frameImage = frameImages[i];

            // Run pose inference
            var keypoints = inferenceService.RunInference(
                frameImage,
                cropRegion,
                metadata.Height,
                metadata.Width,
                prevFrame,
                prev2Frame,
                deltaTime
            );

            var angles = inferenceService.ComputeJointAngles(
                keypoints,
                metadata.Height,
                metadata.Width
            );

            // If this is a frame we need, extract features
            if (frameIndices.Contains(actualFrameIndex))
            {
                var features = SwingPhaseClassifierTrainingService.ExtractFrameFeatures(
                    keypoints,
                    angles,
                    isRightHanded,
                    prevFrame,
                    prev2Frame
                );

                result[actualFrameIndex] = new ProcessedFrameData { Features = features };
            }

            // Update temporal context
            prev2Frame = prevFrame;
            prevFrame = CreateFrameData(keypoints, angles, actualFrameIndex);

            // Update crop region
            cropRegion = GetCropRegion(keypoints, metadata);
        }

        return result;
    }

    private string? FindVideoFile(string videoName)
    {
        var extensions = new[] { ".mp4", ".avi", ".mov", ".mkv", ".webm" };

        foreach (var ext in extensions)
        {
            var path = Path.Combine(_videoDirectory, videoName + ext);
            if (File.Exists(path))
            {
                return path;
            }
        }

        return null;
    }

    private static string GetVideoNameFromPath(string filePath)
    {
        var filename = Path.GetFileNameWithoutExtension(filePath);
        var match = Regex.Match(filename, @"^(.+)_frame_\d+$");
        return match.Success ? match.Groups[1].Value : filename;
    }

    private static LabeledFrameInfo? FindNearestLabeledFrame(
        List<LabeledFrameInfo> labeledFrames,
        int targetFrameIndex
    )
    {
        return labeledFrames
            .OrderBy(f => Math.Abs(f.FrameIndex - targetFrameIndex))
            .FirstOrDefault();
    }

    private static FrameData CreateFrameData(JointData[] keypoints, float[] angles, int frameIndex)
    {
        return new FrameData
        {
            Joints = keypoints,
            SwingPhase = Data.Enums.SwingPhase.Preparation, // Placeholder
            LeftElbowAngle = angles[0],
            RightElbowAngle = angles[1],
            LeftShoulderAngle = angles[2],
            RightShoulderAngle = angles[3],
            LeftHipAngle = angles[4],
            RightHipAngle = angles[5],
            LeftKneeAngle = angles[6],
            RightKneeAngle = angles[7],
            FrameIndex = frameIndex,
        };
    }

    private static CropRegion GetCropRegion(JointData[] keypoints, VideoMetadata metadata)
    {
        var leftHip = keypoints[(int)JointFeatures.LeftHip];
        var rightHip = keypoints[(int)JointFeatures.RightHip];

        if (leftHip.Confidence < 0.2f && rightHip.Confidence < 0.2f)
        {
            return CropRegion.InitCropRegion(metadata.Height, metadata.Width);
        }

        return CropRegion.InitCropRegion(metadata.Height, metadata.Width);
    }

    /// <summary>
    /// Print class distribution after augmentation
    /// </summary>
    public void PrintClassDistribution()
    {
        var labeledFiles = Directory.GetFiles(_labeledDataDirectory, "*.json");
        var classCounts = new int[5];

        foreach (var file in labeledFiles)
        {
            try
            {
                var json = File.ReadAllText(file);
                var frameData = JsonSerializer.Deserialize<UnlabeledFrameJson>(json, _jsonOptions);

                if (frameData?.Phase >= 0 && frameData.Phase < 5)
                {
                    classCounts[frameData.Phase]++;
                }
            }
            catch
            {
                // Skip files that can't be parsed
            }
        }

        Console.WriteLine();
        Console.WriteLine("=== Class Distribution ===");
        string[] classNames = ["None", "Preparation", "Backswing", "Swing", "FollowThrough"];
        int total = classCounts.Sum();

        for (int i = 0; i < 5; i++)
        {
            Console.WriteLine(
                $"  {classNames[i]}: {classCounts[i]} samples ({(float)classCounts[i] / total:P1})"
            );
        }

        Console.WriteLine($"  Total: {total} samples");
    }
}

/// <summary>
/// Info about a labeled frame
/// </summary>
internal class LabeledFrameInfo
{
    public int FrameIndex { get; set; }
    public int PhaseLabel { get; set; }
    public bool IsRightHanded { get; set; }
    public required string FilePath { get; set; }
}

/// <summary>
/// Processed frame data with features
/// </summary>
internal class ProcessedFrameData
{
    public required float[] Features { get; set; }
}
