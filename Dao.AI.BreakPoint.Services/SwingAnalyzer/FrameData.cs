using Dao.AI.BreakPoint.Data.Enums;

namespace Dao.AI.BreakPoint.Services.SwingAnalyzer;

public class FrameData
{
    public SwingPoseFeatures[] SwingPoseFeatures { get; set; } = new SwingPoseFeatures[17];
    public SwingPhase SwingPhase { get; set; }
}
