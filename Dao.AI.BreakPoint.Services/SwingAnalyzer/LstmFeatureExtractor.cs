using Dao.AI.BreakPoint.Services.MoveNet;

namespace Dao.AI.BreakPoint.Services.SwingAnalyzer;

/// <summary>
/// Extracts 20 features per frame for LSTM-based swing phase classification.
/// Features are designed to capture swing biomechanics in a handedness-agnostic way.
/// </summary>
public static class LstmFeatureExtractor
{
    /// <summary>
    /// Number of features extracted per frame.
    /// </summary>
    public const int FeatureCount = 20;

    // MoveNet joint indices
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
    /// Feature names for documentation and debugging.
    /// </summary>
    public static readonly string[] FeatureNames =
    [
        "dominant_wrist_velocity", // 0
        "dominant_elbow_velocity", // 1
        "non_dominant_wrist_velocity", // 2
        "non_dominant_elbow_velocity", // 3
        "dominant_shoulder_velocity", // 4
        "non_dominant_shoulder_velocity", // 5
        "dominant_elbow_angle", // 6
        "non_dominant_elbow_angle", // 7
        "dominant_shoulder_angle", // 8
        "non_dominant_shoulder_angle", // 9
        "hip_rotation_angle", // 10
        "shoulder_rotation_angle", // 11
        "dominant_wrist_rel_shoulder_x", // 12
        "dominant_wrist_rel_shoulder_y", // 13
        "dominant_elbow_rel_hip_x", // 14
        "dominant_elbow_rel_hip_y", // 15
        "dominant_arm_extension", // 16
        "wrist_height_relative", // 17
        "elbow_forward_position", // 18
        "is_right_handed", // 19
    ];

    /// <summary>
    /// Extract features from frame data for LSTM input.
    /// </summary>
    /// <param name="frame">Current frame data.</param>
    /// <param name="prevFrame">Previous frame for velocity calculation (optional).</param>
    /// <param name="isRightHanded">Whether the player is right-handed.</param>
    /// <returns>Array of 20 normalized features.</returns>
    public static float[] ExtractFeatures(FrameData frame, FrameData? prevFrame, bool isRightHanded)
    {
        var features = new float[FeatureCount];
        var joints = frame.Joints;

        // Determine dominant/non-dominant joints
        int domWrist = isRightHanded ? RightWrist : LeftWrist;
        int domElbow = isRightHanded ? RightElbow : LeftElbow;
        int domShoulder = isRightHanded ? RightShoulder : LeftShoulder;
        int nonDomWrist = isRightHanded ? LeftWrist : RightWrist;
        int nonDomElbow = isRightHanded ? LeftElbow : RightElbow;
        int nonDomShoulder = isRightHanded ? LeftShoulder : RightShoulder;

        // Calculate torso reference
        float hipCenterX = (joints[LeftHip].X + joints[RightHip].X) / 2f;
        float hipCenterY = (joints[LeftHip].Y + joints[RightHip].Y) / 2f;
        float shoulderCenterY = (joints[LeftShoulder].Y + joints[RightShoulder].Y) / 2f;
        float torsoHeight = Math.Max(0.1f, Math.Abs(hipCenterY - shoulderCenterY));

        // 1. VELOCITIES (6 features, indices 0-5)
        // Use stored speeds from MoveNet tracking, normalized by typical max speed
        features[0] = Normalize(joints[domWrist].Speed ?? 0, 800f); // dominant wrist velocity
        features[1] = Normalize(joints[domElbow].Speed ?? 0, 600f); // dominant elbow velocity
        features[2] = Normalize(joints[nonDomWrist].Speed ?? 0, 600f); // non-dominant wrist velocity
        features[3] = Normalize(joints[nonDomElbow].Speed ?? 0, 500f); // non-dominant elbow velocity
        features[4] = Normalize(joints[domShoulder].Speed ?? 0, 400f); // dominant shoulder velocity
        features[5] = Normalize(joints[nonDomShoulder].Speed ?? 0, 400f); // non-dominant shoulder velocity

        // 2. ANGLES (6 features, indices 6-11)
        // Elbow angles (from FrameData pre-calculated angles)
        features[6] = Normalize(isRightHanded ? frame.RightElbowAngle : frame.LeftElbowAngle, 180f);
        features[7] = Normalize(isRightHanded ? frame.LeftElbowAngle : frame.RightElbowAngle, 180f);

        // Shoulder angles
        features[8] = Normalize(
            isRightHanded ? frame.RightShoulderAngle : frame.LeftShoulderAngle,
            180f
        );
        features[9] = Normalize(
            isRightHanded ? frame.LeftShoulderAngle : frame.RightShoulderAngle,
            180f
        );

        // Hip rotation (approximate from hip positions relative to shoulders)
        float hipLineAngle = CalculateLineAngle(joints[LeftHip], joints[RightHip]);
        float shoulderLineAngle = CalculateLineAngle(joints[LeftShoulder], joints[RightShoulder]);
        features[10] = Normalize(hipLineAngle, 90f); // hip rotation
        features[11] = Normalize(shoulderLineAngle - hipLineAngle, 45f); // shoulder-hip separation

        // 3. RELATIVE POSITIONS (4 features, indices 12-15)
        // Dominant wrist relative to shoulder
        features[12] = Normalize((joints[domWrist].X - joints[domShoulder].X) / torsoHeight, 3f);
        features[13] = Normalize((joints[domWrist].Y - joints[domShoulder].Y) / torsoHeight, 3f);

        // Dominant elbow relative to hip center
        features[14] = Normalize((joints[domElbow].X - hipCenterX) / torsoHeight, 2f);
        features[15] = Normalize((joints[domElbow].Y - hipCenterY) / torsoHeight, 2f);

        // 4. ARM CONFIGURATION (3 features, indices 16-18)
        // Arm extension (distance from shoulder to wrist normalized by arm length estimate)
        float armLength =
            Distance(joints[domShoulder], joints[domElbow])
            + Distance(joints[domElbow], joints[domWrist]);
        float wristShoulderDist = Distance(joints[domShoulder], joints[domWrist]);
        features[16] = Normalize(wristShoulderDist / Math.Max(0.1f, armLength), 1.2f);

        // Wrist height relative to shoulder
        features[17] = Normalize((joints[domShoulder].Y - joints[domWrist].Y) / torsoHeight, 2f);

        // Elbow forward position (x-distance in front of shoulder, adjusted for handedness)
        float elbowForward = isRightHanded
            ? (joints[domElbow].X - joints[domShoulder].X)
            : (joints[domShoulder].X - joints[domElbow].X);
        features[18] = Normalize(elbowForward / torsoHeight, 1.5f);

        // 5. HANDEDNESS (1 feature, index 19)
        features[19] = isRightHanded ? 1f : 0f;

        return features;
    }

    private static float Normalize(float value, float maxExpected)
    {
        var normalized = value / maxExpected;
        return Sanitize(normalized);
    }

    private static float Sanitize(float value)
    {
        if (float.IsNaN(value) || float.IsInfinity(value))
            return 0f;
        return Math.Clamp(value, -3f, 3f);
    }

    private static float CalculateLineAngle(JointData p1, JointData p2)
    {
        float dx = p2.X - p1.X;
        float dy = p2.Y - p1.Y;
        return MathF.Atan2(dy, dx) * (180f / MathF.PI);
    }

    private static float Distance(JointData p1, JointData p2)
    {
        float dx = p2.X - p1.X;
        float dy = p2.Y - p1.Y;
        return MathF.Sqrt(dx * dx + dy * dy);
    }
}
