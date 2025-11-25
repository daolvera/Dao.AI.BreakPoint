namespace Dao.AI.BreakPoint.Services.SwingAnalyzer;

public class FrameData
{
    public SwingPoseFeatures[] SwingPoseFeatures { get; set; } = new SwingPoseFeatures[17];
    public int FrameNumber { get; set; }
}
