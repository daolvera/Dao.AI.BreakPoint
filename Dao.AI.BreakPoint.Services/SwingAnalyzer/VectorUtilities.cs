using Dao.AI.BreakPoint.Services.MoveNet;
using System.Numerics;

namespace Dao.AI.BreakPoint.Services.SwingAnalyzer;

public static class VectorUtilities
{
    /// <summary>
    /// Compute 8 joint angles (elbows, shoulders, hips, knees)
    /// </summary>
    public static float[] ComputeJointAngles(this Vector2[] positions, float[] confidences, float minConfidence)
    {
        var angles = new float[8];

        // Left elbow: shoulder-elbow-wrist
        angles[0] = AngleBetween(
            positions[(int)JointFeatures.LeftShoulder],
            positions[(int)JointFeatures.LeftElbow],
            positions[(int)JointFeatures.LeftWrist]);
        if (confidences[(int)JointFeatures.LeftShoulder] < minConfidence ||
            confidences[(int)JointFeatures.LeftElbow] < minConfidence ||
            confidences[(int)JointFeatures.LeftWrist] < minConfidence)
            angles[0] = float.NaN;

        // Right elbow
        angles[1] = AngleBetween(
            positions[(int)JointFeatures.RightShoulder],
            positions[(int)JointFeatures.RightElbow],
            positions[(int)JointFeatures.RightWrist]);
        if (confidences[(int)JointFeatures.RightShoulder] < minConfidence ||
            confidences[(int)JointFeatures.RightElbow] < minConfidence ||
            confidences[(int)JointFeatures.RightWrist] < minConfidence)
            angles[1] = float.NaN;

        // Left shoulder: elbow-shoulder-hip
        angles[2] = AngleBetween(
            positions[(int)JointFeatures.LeftElbow],
            positions[(int)JointFeatures.LeftShoulder],
            positions[(int)JointFeatures.LeftHip]);
        if (confidences[(int)JointFeatures.LeftElbow] < minConfidence ||
            confidences[(int)JointFeatures.LeftShoulder] < minConfidence ||
            confidences[(int)JointFeatures.LeftHip] < minConfidence)
            angles[2] = float.NaN;

        // Right shoulder
        angles[3] = AngleBetween(
            positions[(int)JointFeatures.RightElbow],
            positions[(int)JointFeatures.RightShoulder],
            positions[(int)JointFeatures.RightHip]);
        if (confidences[(int)JointFeatures.RightElbow] < minConfidence ||
            confidences[(int)JointFeatures.RightShoulder] < minConfidence ||
            confidences[(int)JointFeatures.RightHip] < minConfidence)
            angles[3] = float.NaN;

        // Left hip: shoulder-hip-knee
        angles[4] = AngleBetween(
            positions[(int)JointFeatures.LeftShoulder],
            positions[(int)JointFeatures.LeftHip],
            positions[(int)JointFeatures.LeftKnee]);
        if (confidences[(int)JointFeatures.LeftShoulder] < minConfidence ||
            confidences[(int)JointFeatures.LeftHip] < minConfidence ||
            confidences[(int)JointFeatures.LeftKnee] < minConfidence)
            angles[4] = float.NaN;

        // Right hip
        angles[5] = AngleBetween(
            positions[(int)JointFeatures.RightShoulder],
            positions[(int)JointFeatures.RightHip],
            positions[(int)JointFeatures.RightKnee]);
        if (confidences[(int)JointFeatures.RightShoulder] < minConfidence ||
            confidences[(int)JointFeatures.RightHip] < minConfidence ||
            confidences[(int)JointFeatures.RightKnee] < minConfidence)
            angles[5] = float.NaN;

        // Left knee: hip-knee-ankle
        angles[6] = AngleBetween(
            positions[(int)JointFeatures.LeftHip],
            positions[(int)JointFeatures.LeftKnee],
            positions[(int)JointFeatures.LeftAnkle]);
        if (confidences[(int)JointFeatures.LeftHip] < minConfidence ||
            confidences[(int)JointFeatures.LeftKnee] < minConfidence ||
            confidences[(int)JointFeatures.LeftAnkle] < minConfidence)
            angles[6] = float.NaN;

        // Right knee
        angles[7] = AngleBetween(
            positions[(int)JointFeatures.RightHip],
            positions[(int)JointFeatures.RightKnee],
            positions[(int)JointFeatures.RightAnkle]);
        if (confidences[(int)JointFeatures.RightHip] < minConfidence ||
            confidences[(int)JointFeatures.RightKnee] < minConfidence ||
            confidences[(int)JointFeatures.RightAnkle] < minConfidence)
            angles[7] = float.NaN;

        return angles;
    }

    /// <summary>
    /// Calculate angle at point b formed by points a-b-c (in degrees)
    /// </summary>
    public static float AngleBetween(Vector2 a, Vector2 b, Vector2 c)
    {
        var v1 = a - b;
        var v2 = c - b;

        float n1 = v1.Length();
        float n2 = v2.Length();

        if (n1 == 0 || n2 == 0)
            return float.NaN;

        float cosAngle = Vector2.Dot(v1, v2) / (n1 * n2);
        cosAngle = Math.Clamp(cosAngle, -1.0f, 1.0f);

        return (float)(Math.Acos(cosAngle) * 180.0 / Math.PI);
    }
}
