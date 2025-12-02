using Dao.AI.BreakPoint.Data.Enums;
using Dao.AI.BreakPoint.Services.MoveNet;

namespace Dao.AI.BreakPoint.Services.SwingAnalyzer;

public class FrameData
{
    public JointData[] Joints { get; set; } = new JointData[MoveNetVideoProcessor.NumKeyPoints];
    public SwingPhase SwingPhase { get; set; }
    public float LeftElbowAngle { get; set; }
    public float RightElbowAngle { get; set; }
    public float LeftShoulderAngle { get; set; }
    public float RightShoulderAngle { get; set; }
    public float LeftHipAngle { get; set; }
    public float RightHipAngle { get; set; }
    public float LeftKneeAngle { get; set; }
    public float RightKneeAngle { get; set; }
}
