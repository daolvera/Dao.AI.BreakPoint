using Dao.AI.BreakPoint.Services.MoveNet;

namespace Dao.AI.BreakPoint.Services.VideoProcessing;

public interface IVideoProcessingService
{
    List<byte[]> ExtractFrameRange(string videoPath, int startFrame, int endFrame);
    List<byte[]> ExtractFrames(string videoPath, int maxFrames = -1, int skipFrames = 0);
    VideoMetadata GetVideoMetadata(string videoPath);
}