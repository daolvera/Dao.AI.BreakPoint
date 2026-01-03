namespace Dao.AI.BreakPoint.Services.MoveNet;

/// <summary>
/// Basic video metadata needed for processing
/// </summary>
public class VideoMetadata
{
    public int FrameRate { get; set; } = 30;
    public int Width { get; set; }
    public int Height { get; set; }
    public int TotalFrames { get; set; }
    public double DurationSeconds { get; set; }
    public string? FileName { get; set; }

    public override string ToString()
    {
        return $"VideoMetadata: {FileName}, {Width}x{Height}, {FrameRate}fps, {TotalFrames} frames, {DurationSeconds}s";
    }
}
