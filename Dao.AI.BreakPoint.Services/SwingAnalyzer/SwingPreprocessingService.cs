using Dao.AI.BreakPoint.Services.MoveNet;

namespace Dao.AI.BreakPoint.Services.SwingAnalyzer;

/// <summary>
/// Service for preprocessing swing data into feature vectors for ML model input.
/// Uses a focused feature set optimized for tennis swing quality analysis.
/// </summary>
public static class SwingPreprocessingService
{
    /// <summary>
    /// Number of features per frame for the focused feature set.
    /// 6 key joints (wrist, elbow, shoulder) × 2 sides × 2 (velocity + acceleration) = 12 motion features
    /// + 4 key angles (elbow, shoulder) = 16 total features per frame
    /// Note: The full 28 features includes additional derived features in aggregation.
    /// </summary>
    public const int FocusedFeatureCount = 16;

    /// <summary>
    /// Key joints for tennis swing analysis (most relevant for technique quality).
    /// Order: Left/Right pairs for Shoulder, Elbow, Wrist
    /// </summary>
    private static readonly JointFeatures[] KeyJoints =
    [
        JointFeatures.LeftShoulder,
        JointFeatures.RightShoulder,
        JointFeatures.LeftElbow,
        JointFeatures.RightElbow,
        JointFeatures.LeftWrist,
        JointFeatures.RightWrist,
    ];

    public static Task<float[,]> PreprocessSwingAsync(
        SwingData swing,
        int sequenceLength,
        int numFeatures
    )
    {
        var frameFeatures = new List<float[]>();

        foreach (var frame in swing.Frames)
        {
            var features = BuildFeaturesFromFrame(frame);

            if (features == null || features.Length != numFeatures)
            {
                continue;
            }

            frameFeatures.Add(features);
        }

        if (frameFeatures.Count == 0)
        {
            throw new InvalidOperationException("No valid frame features extracted from swing");
        }

        return Task.FromResult(PadSequence(frameFeatures, sequenceLength, numFeatures));
    }

    /// <summary>
    /// Build focused feature set optimized for tennis swing analysis.
    /// Total: 16 features per frame
    /// - 12 motion features (6 joints × 2 velocity/acceleration)
    /// - 4 key angles (elbow and shoulder, both sides)
    /// </summary>
    private static float[] BuildFeaturesFromFrame(FrameData frame)
    {
        List<float> features = [];

        // Motion features for key joints (velocity and acceleration)
        // These are the most important for swing quality
        foreach (var joint in KeyJoints)
        {
            int idx = (int)joint;
            var jointData = frame.Joints[idx];

            // Use 0 for low-confidence joints instead of NaN to avoid training issues
            float velocity = jointData.Confidence >= 0.2f ? (jointData.Speed ?? 0f) : 0f;
            float acceleration = jointData.Confidence >= 0.2f ? (jointData.Acceleration ?? 0f) : 0f;

            features.Add(velocity);
            features.Add(acceleration);
        }

        // Key angles for technique assessment
        // Elbow and shoulder angles are most relevant for tennis strokes
        features.Add(frame.LeftElbowAngle);
        features.Add(frame.RightElbowAngle);
        features.Add(frame.LeftShoulderAngle);
        features.Add(frame.RightShoulderAngle);

        return [.. features];
    }

    private static float[,] PadSequence(
        List<float[]> frameFeatures,
        int sequenceLength,
        int numFeatures
    )
    {
        var paddedSequence = new float[sequenceLength, numFeatures];
        var actualLength = Math.Min(frameFeatures.Count, sequenceLength);

        for (int frameIdx = 0; frameIdx < actualLength; frameIdx++)
        {
            var features = frameFeatures[frameIdx];
            for (int featIdx = 0; featIdx < Math.Min(features.Length, numFeatures); featIdx++)
            {
                paddedSequence[frameIdx, featIdx] = features[featIdx];
            }
        }

        return paddedSequence;
    }
}
