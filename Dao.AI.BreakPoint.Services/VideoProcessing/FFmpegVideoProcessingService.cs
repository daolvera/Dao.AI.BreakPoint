using Dao.AI.BreakPoint.Services.MoveNet;
using FFMpegCore;
using FFMpegCore.Enums;
using Microsoft.Extensions.Logging;

namespace Dao.AI.BreakPoint.Services.VideoProcessing;

/// <summary>
/// FFmpeg-based video processing service designed for Linux Azure Functions.
/// Requires FFmpeg to be installed on the host system or container.
/// </summary>
public class FFmpegVideoProcessingService(ILogger<FFmpegVideoProcessingService> logger) : IVideoProcessingService
{
    public VideoMetadata GetVideoMetadata(string videoPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(videoPath);

        if (!File.Exists(videoPath))
        {
            throw new FileNotFoundException("Video file not found.", videoPath);
        }

        var mediaInfo = FFProbe.Analyse(videoPath);
        var videoStream = mediaInfo.PrimaryVideoStream
            ?? throw new InvalidOperationException("No video stream found in file.");

        var frameRate = videoStream.FrameRate > 0 ? videoStream.FrameRate : 30;
        var totalFrames = (int)(mediaInfo.Duration.TotalSeconds * frameRate);

        return new VideoMetadata
        {
            FileName = Path.GetFileName(videoPath),
            Width = videoStream.Width,
            Height = videoStream.Height,
            FrameRate = (int)Math.Round(frameRate),
            TotalFrames = totalFrames,
            DurationSeconds = mediaInfo.Duration.TotalSeconds
        };
    }

    public List<byte[]> ExtractFrames(string videoPath, int maxFrames = -1, int skipFrames = 0)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(videoPath);

        if (!File.Exists(videoPath))
        {
            throw new FileNotFoundException("Video file not found.", videoPath);
        }

        var metadata = GetVideoMetadata(videoPath);
        var totalFrames = metadata.TotalFrames;

        if (maxFrames <= 0)
        {
            maxFrames = totalFrames;
        }

        var frameIndices = new List<int>();
        for (int i = 0; i < totalFrames && frameIndices.Count < maxFrames; i += (skipFrames + 1))
        {
            frameIndices.Add(i);
        }

        logger.LogDebug(
            "Extracting {FrameCount} frames from {VideoPath} (total: {TotalFrames}, skip: {SkipFrames})",
            frameIndices.Count,
            videoPath,
            totalFrames,
            skipFrames
        );

        return ExtractFramesAtIndices(videoPath, frameIndices, metadata.FrameRate);
    }

    public List<byte[]> ExtractFrameRange(string videoPath, int startFrame, int endFrame)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(videoPath);

        if (!File.Exists(videoPath))
        {
            throw new FileNotFoundException("Video file not found.", videoPath);
        }

        if (startFrame < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(startFrame), "Start frame must be non-negative.");
        }

        if (endFrame < startFrame)
        {
            throw new ArgumentOutOfRangeException(nameof(endFrame), "End frame must be greater than or equal to start frame.");
        }

        var metadata = GetVideoMetadata(videoPath);
        var frameIndices = Enumerable.Range(startFrame, endFrame - startFrame + 1).ToList();

        logger.LogDebug(
            "Extracting frame range [{StartFrame}-{EndFrame}] from {VideoPath}",
            startFrame,
            endFrame,
            videoPath
        );

        return ExtractFramesAtIndices(videoPath, frameIndices, metadata.FrameRate);
    }

    private List<byte[]> ExtractFramesAtIndices(string videoPath, List<int> frameIndices, int frameRate)
    {
        var frames = new List<byte[]>();
        var tempDirectory = Path.Combine(Path.GetTempPath(), $"ffmpeg_frames_{Guid.NewGuid():N}");

        try
        {
            Directory.CreateDirectory(tempDirectory);

            foreach (var frameIndex in frameIndices)
            {
                var frameBytes = ExtractSingleFrame(videoPath, frameIndex, frameRate, tempDirectory);
                if (frameBytes != null)
                {
                    frames.Add(frameBytes);
                }
            }

            return frames;
        }
        finally
        {
            CleanupTempDirectory(tempDirectory);
        }
    }

    private byte[]? ExtractSingleFrame(string videoPath, int frameIndex, int frameRate, string tempDirectory)
    {
        var timestamp = TimeSpan.FromSeconds((double)frameIndex / frameRate);
        var outputPath = Path.Combine(tempDirectory, $"frame_{frameIndex:D6}.png");

        try
        {
            var success = FFMpegArguments
                .FromFileInput(videoPath, verifyExists: false, options => options
                    .Seek(timestamp))
                .OutputToFile(outputPath, overwrite: true, options => options
                    .WithFrameOutputCount(1)
                    .WithVideoCodec(VideoCodec.Png)
                    .ForceFormat("image2"))
                .ProcessSynchronously();

            if (success && File.Exists(outputPath))
            {
                return File.ReadAllBytes(outputPath);
            }

            logger.LogWarning("Failed to extract frame {FrameIndex} from {VideoPath}", frameIndex, videoPath);
            return null;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Error extracting frame {FrameIndex} from {VideoPath}", frameIndex, videoPath);
            return null;
        }
        finally
        {
            TryDeleteFile(outputPath);
        }
    }

    private void CleanupTempDirectory(string tempDirectory)
    {
        try
        {
            if (Directory.Exists(tempDirectory))
            {
                Directory.Delete(tempDirectory, recursive: true);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to cleanup temp directory: {TempDirectory}", tempDirectory);
        }
    }

    private static void TryDeleteFile(string filePath)
    {
        try
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }
        catch
        {
            // Ignore cleanup errors
        }
    }
}
