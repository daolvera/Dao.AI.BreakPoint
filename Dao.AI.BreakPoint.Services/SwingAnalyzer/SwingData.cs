namespace Dao.AI.BreakPoint.Services.SwingAnalyzer;

public class SwingData
{
    public List<FrameData> Frames { get; set; } = [];
    public float OverallScore { get; set; }
    public int ContactFrame { get; set; } = 30; // Default to frame 30 (1 second in)
}
