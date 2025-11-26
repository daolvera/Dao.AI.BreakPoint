namespace Dao.AI.BreakPoint.Services.SwingAnalyzer;

public class RawSwingData
{
    public List<FrameData> Frames { get; set; } = [];
    public double OverallScore { get; set; }
    public int ContactFrame { get; set; } = 30; // Default to frame 30 (1 second in)
    public int ImageHeight { get; set; }
    public int ImageWidth { get; set; }
    public int FrameRate { get; set; } = 30;
}
