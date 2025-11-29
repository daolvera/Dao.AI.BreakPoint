using Dao.AI.BreakPoint.Data.Enums;
using Dao.AI.BreakPoint.Services.MoveNet;

namespace Dao.AI.BreakPoint.Services.SwingAnalyzer;

public class FrameData
{
    public SwingPoseFeatures[] SwingPoseFeatures { get; set; } = new SwingPoseFeatures[MoveNetVideoProcessor.NumKeyPoints];
    public SwingPhase SwingPhase { get; set; }
}
