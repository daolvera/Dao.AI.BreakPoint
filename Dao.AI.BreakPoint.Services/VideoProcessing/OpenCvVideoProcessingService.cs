using Dao.AI.BreakPoint.Services.MoveNet;
using OpenCvSharp;

namespace Dao.AI.BreakPoint.Services.VideoProcessing;

public class OpenCvVideoProcessingService : IVideoProcessingService
{
    public List<byte[]> ExtractFrames(string videoPath, int maxFrames = -1, int skipFrames = 0)
    {
        if (!File.Exists(videoPath))
        {
            throw new FileNotFoundException($"Video file not found: {videoPath}");
        }

        var frames = new List<byte[]>();

        using var capture = new VideoCapture(videoPath);
        if (!capture.IsOpened())
        {
            throw new InvalidOperationException($"Could not open video file: {videoPath}");
        }

        using var frame = new Mat();
        int frameCount = 0;
        int extractedCount = 0;

        while (capture.Read(frame) && (maxFrames == -1 || extractedCount < maxFrames))
        {
            if (frame.Empty()) break;

            // Skip frames if specified
            if (frameCount % (skipFrames + 1) == 0)
            {
                // Convert frame to JPEG bytes
                var frameBytes = frame.ToBytes(".jpg");
                frames.Add(frameBytes);
                extractedCount++;
            }

            frameCount++;
        }

        Console.WriteLine($"Extracted {frames.Count} frames from {videoPath}");
        return frames;
    }

    public VideoMetadata GetVideoMetadata(string videoPath)
    {
        if (!File.Exists(videoPath))
        {
            throw new FileNotFoundException($"Video file not found: {videoPath}");
        }

        using var capture = new VideoCapture(videoPath);
        if (!capture.IsOpened())
        {
            throw new InvalidOperationException($"Could not open video file: {videoPath}");
        }

        var metadata = new VideoMetadata
        {
            Width = (int)capture.Get(VideoCaptureProperties.FrameWidth),
            Height = (int)capture.Get(VideoCaptureProperties.FrameHeight),
            FrameRate = (int)capture.Get(VideoCaptureProperties.Fps),
            TotalFrames = (int)capture.Get(VideoCaptureProperties.FrameCount),
            DurationSeconds = capture.Get(VideoCaptureProperties.FrameCount) / capture.Get(VideoCaptureProperties.Fps)
        };

        return metadata;
    }

    public List<byte[]> ExtractFrameRange(string videoPath, int startFrame, int endFrame)
    {
        if (!File.Exists(videoPath))
        {
            throw new FileNotFoundException($"Video file not found: {videoPath}");
        }

        var frames = new List<byte[]>();

        using var capture = new VideoCapture(videoPath);
        if (!capture.IsOpened())
        {
            throw new InvalidOperationException($"Could not open video file: {videoPath}");
        }

        // Set starting position
        capture.Set(VideoCaptureProperties.PosFrames, startFrame);

        using var frame = new Mat();
        int currentFrame = startFrame;

        while (currentFrame <= endFrame && capture.Read(frame))
        {
            if (frame.Empty()) break;

            var frameBytes = frame.ToBytes(".jpg");
            frames.Add(frameBytes);
            currentFrame++;
        }

        Console.WriteLine($"Extracted frames {startFrame}-{endFrame} from {videoPath}");
        return frames;
    }
}
