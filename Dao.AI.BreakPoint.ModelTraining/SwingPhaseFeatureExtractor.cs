namespace Dao.AI.BreakPoint.ModelTraining;

using Dao.AI.BreakPoint.Services.MoveNet;
using Dao.AI.BreakPoint.Services.SwingAnalyzer;

/// <summary>
/// Extracts pose-relative features for swing phase classification.
/// Uses relative positions (normalized to torso) instead of absolute positions,
/// which makes the features invariant to player location in frame.
///
/// Feature set (per frame):
/// - 8 joint angles (elbows, shoulders, hips, knees)
/// - 12 relative positions (key joints relative to hip center)
/// - 6 key velocities (wrists, elbows, shoulders)
/// - 4 arm configuration features (wrist position relative to shoulder)
/// = 30 features per frame
///
/// With 3-frame temporal window + handedness:
/// 30 × 3 + 1 = 91 total features (down from 250)
/// </summary>
public static class SwingPhaseFeatureExtractor
{
    // Feature counts
    public const int AngleFeatures = 8;
    public const int RelativePositionFeatures = 12; // 6 joints × 2 (x, y)
    public const int VelocityFeatures = 6;
    public const int ArmConfigFeatures = 4;
    public const int FeaturesPerFrame =
        AngleFeatures + RelativePositionFeatures + VelocityFeatures + ArmConfigFeatures; // 30
    public const int TotalFeatures = (FeaturesPerFrame * 3) + 1; // 91

    // Key joint indices
    private const int LeftShoulder = 5;
    private const int RightShoulder = 6;
    private const int LeftElbow = 7;
    private const int RightElbow = 8;
    private const int LeftWrist = 9;
    private const int RightWrist = 10;
    private const int LeftHip = 11;
    private const int RightHip = 12;
    private const int LeftKnee = 13;
    private const int RightKnee = 14;

    /// <summary>
    /// Extract features for swing phase classification.
    /// </summary>
    public static float[] ExtractFeatures(
        JointData[] keypoints,
        float[] angles,
        bool isRightHanded,
        FrameData? prevFrame = null,
        FrameData? prev2Frame = null
    )
    {
        var features = new List<float>();

        // Current frame
        AddFrameFeatures(features, keypoints, angles, isRightHanded);

        // Previous frame
        if (prevFrame != null)
        {
            AddFrameFeatures(
                features,
                prevFrame.Joints,
                GetAnglesFromFrameData(prevFrame),
                isRightHanded
            );
        }
        else
        {
            AddZeroFeatures(features, FeaturesPerFrame);
        }

        // Two frames ago
        if (prev2Frame != null)
        {
            AddFrameFeatures(
                features,
                prev2Frame.Joints,
                GetAnglesFromFrameData(prev2Frame),
                isRightHanded
            );
        }
        else
        {
            AddZeroFeatures(features, FeaturesPerFrame);
        }

        // Handedness
        features.Add(isRightHanded ? 1.0f : 0.0f);

        return [.. features];
    }

    private static void AddFrameFeatures(
        List<float> features,
        JointData[] keypoints,
        float[] angles,
        bool isRightHanded
    )
    {
        // 1. Joint angles (8 features) - these are pose-invariant
        foreach (var angle in angles)
        {
            features.Add(Sanitize(angle / 180.0f));
        }

        // Calculate hip center as reference point
        var hipCenterX = (keypoints[LeftHip].X + keypoints[RightHip].X) / 2;
        var hipCenterY = (keypoints[LeftHip].Y + keypoints[RightHip].Y) / 2;

        // Calculate torso scale (for normalization)
        var shoulderCenterY = (keypoints[LeftShoulder].Y + keypoints[RightShoulder].Y) / 2;
        var torsoHeight = Math.Max(0.1f, Math.Abs(hipCenterY - shoulderCenterY));

        // 2. Relative positions of key joints (12 features)
        // Positions normalized relative to hip center and torso height
        int[] relativeJoints =
        [
            LeftWrist,
            RightWrist,
            LeftElbow,
            RightElbow,
            LeftShoulder,
            RightShoulder,
        ];
        foreach (var jointIdx in relativeJoints)
        {
            var relX = (keypoints[jointIdx].X - hipCenterX) / torsoHeight;
            var relY = (keypoints[jointIdx].Y - hipCenterY) / torsoHeight;
            features.Add(Sanitize(relX));
            features.Add(Sanitize(relY));
        }

        // 3. Key velocities (6 features) - wrists, elbows, shoulders
        // These are the most discriminative for phase detection
        int[] velocityJoints =
        [
            LeftWrist,
            RightWrist,
            LeftElbow,
            RightElbow,
            LeftShoulder,
            RightShoulder,
        ];
        foreach (var jointIdx in velocityJoints)
        {
            // Normalize by a reasonable max velocity (pixels/second in normalized coords)
            var speed = keypoints[jointIdx].Speed ?? 0;
            features.Add(Sanitize(speed / 500.0f));
        }

        // 4. Arm configuration features (4 features)
        // These capture the racket arm position relative to the body
        int dominantWrist = isRightHanded ? RightWrist : LeftWrist;
        int dominantElbow = isRightHanded ? RightElbow : LeftElbow;
        int dominantShoulder = isRightHanded ? RightShoulder : LeftShoulder;

        // Wrist position relative to shoulder (captures arm extension/position)
        var wristToShoulderX = keypoints[dominantWrist].X - keypoints[dominantShoulder].X;
        var wristToShoulderY = keypoints[dominantWrist].Y - keypoints[dominantShoulder].Y;
        features.Add(Sanitize(wristToShoulderX / torsoHeight));
        features.Add(Sanitize(wristToShoulderY / torsoHeight));

        // Elbow angle relative to body centerline
        var elbowToHipX = keypoints[dominantElbow].X - hipCenterX;
        features.Add(Sanitize(elbowToHipX / torsoHeight));

        // Wrist height relative to shoulder (key for backswing vs followthrough)
        var wristHeightDiff = keypoints[dominantWrist].Y - keypoints[dominantShoulder].Y;
        features.Add(Sanitize(wristHeightDiff / torsoHeight));
    }

    private static void AddZeroFeatures(List<float> features, int count)
    {
        for (int i = 0; i < count; i++)
        {
            features.Add(0.0f);
        }
    }

    private static float[] GetAnglesFromFrameData(FrameData frame)
    {
        return
        [
            frame.LeftElbowAngle,
            frame.RightElbowAngle,
            frame.LeftShoulderAngle,
            frame.RightShoulderAngle,
            frame.LeftHipAngle,
            frame.RightHipAngle,
            frame.LeftKneeAngle,
            frame.RightKneeAngle,
        ];
    }

    private static float Sanitize(float value)
    {
        if (float.IsNaN(value) || float.IsInfinity(value))
            return 0f;
        // Clamp to reasonable range to avoid outliers
        return Math.Clamp(value, -10f, 10f);
    }
}
