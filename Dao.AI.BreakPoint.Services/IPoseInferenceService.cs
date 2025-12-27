using Dao.AI.BreakPoint.Services.MoveNet;
using Dao.AI.BreakPoint.Services.SwingAnalyzer;

namespace Dao.AI.BreakPoint.Services;

public interface IPoseInferenceService
{
    JointData[] RunInference(
        byte[] imageBytes, 
        CropRegion cropRegion, 
        int imageHeight,
        int imageWidth,
        FrameData? prevFrame = null,
        FrameData? prev2Frame = null,
        float deltaTime = 1/30f,
        int cropSize = 256);

    float[] ComputeJointAngles(JointData[] keypoints, int imageHeight, int imageWidth);
}