using Dao.AI.BreakPoint.Services.SwingAnalyzer;
using System.Numerics;

namespace Dao.AI.BreakPoint.Services.MoveNet;

public class MoveNetPoseFeatureExtractorService : IPoseFeatureExtractorService
{
    private const float MIN_CONFIDENCE = 0.2f;
    // Joints to track for velocity/acceleration (matching Python)
    private static readonly JointFeatures[] TrackedJoints =
    [
        JointFeatures.LeftShoulder, JointFeatures.RightShoulder,
        JointFeatures.LeftElbow, JointFeatures.RightElbow,
        JointFeatures.LeftWrist, JointFeatures.RightWrist,
        JointFeatures.LeftHip, JointFeatures.RightHip,
        JointFeatures.LeftKnee, JointFeatures.RightKnee,
        JointFeatures.LeftAnkle, JointFeatures.RightAnkle
    ];

    /// <summary>
    /// Extract pixel coordinates from MoveNet frame
    /// </summary>
    public static (Vector2[] positions, float[] confidences) KeypointsToPixels(FrameData frame, int height, int width)
    {
        var positions = new Vector2[17];
        var confidences = new float[17];

        for (int i = 0; i < 17; i++)
        {
            positions[i] = frame.SwingPoseFeatures[i].ToPixelCoordinates(height, width);
            confidences[i] = frame.SwingPoseFeatures[i].Confidence;
        }

        return (positions, confidences);
    }

    /// <summary>
    /// Build feature vector for a single frame
    /// Matches Python build_frame_features function
    /// </summary>
    public float[] BuildFrameFeatures(
        Vector2[]? prev2Positions,
        Vector2[]? prevPositions,
        Vector2[] currentPositions,
        float[] confidences,
        float deltaTime = 1 / 30f)
    {
        List<float> features = [];

        // Compute velocities and accelerations
        var velocities = new Vector2[17];
        var accelerations = new Vector2[17];

        if (prevPositions != null)
        {
            for (int i = 0; i < 17; i++)
            {
                velocities[i] = (currentPositions[i] - prevPositions[i]) / deltaTime;
            }
        }

        if (prev2Positions != null && prevPositions != null)
        {
            for (int i = 0; i < 17; i++)
            {
                accelerations[i] = (currentPositions[i] - 2 * prevPositions[i] + prev2Positions[i]) / (deltaTime * deltaTime);
            }
        }

        // Add speed and acceleration magnitudes for tracked joints
        foreach (var joint in TrackedJoints)
        {
            int idx = (int)joint;
            float speed = velocities[idx].Length();
            float accMag = accelerations[idx].Length();

            // Mask by confidence
            if (confidences[idx] < MIN_CONFIDENCE)
            {
                speed = float.NaN;
                accMag = float.NaN;
            }

            features.Add(speed);
            features.Add(accMag);
        }

        // Compute joint angles
        var angles = currentPositions.ComputeJointAngles(confidences, MIN_CONFIDENCE);
        features.AddRange(angles);

        // Add flattened positions (masked by confidence)
        for (int i = 0; i < 17; i++)
        {
            float x = confidences[i] < MIN_CONFIDENCE ? float.NaN : currentPositions[i].X;
            float y = confidences[i] < MIN_CONFIDENCE ? float.NaN : currentPositions[i].Y;
            features.Add(x);
            features.Add(y);
        }

        return features.ToArray();
    }
}
