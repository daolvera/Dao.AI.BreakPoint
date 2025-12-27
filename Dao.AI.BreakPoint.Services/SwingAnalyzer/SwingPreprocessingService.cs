using Dao.AI.BreakPoint.Services.MoveNet;

namespace Dao.AI.BreakPoint.Services.SwingAnalyzer;

public static class SwingPreprocessingService
{
    public static async Task<float[,]> PreprocessSwingAsync(
        SwingData swing,
        int sequenceLength,
        int numFeatures)
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

        return PadSequence(frameFeatures, sequenceLength, numFeatures);
    }

    private static float[] BuildFeaturesFromFrame(FrameData frame)
    {
        List<float> features = [];

        var trackedJoints = new[]
        {
            JointFeatures.LeftShoulder, JointFeatures.RightShoulder,
            JointFeatures.LeftElbow, JointFeatures.RightElbow,
            JointFeatures.LeftWrist, JointFeatures.RightWrist,
            JointFeatures.LeftHip, JointFeatures.RightHip,
            JointFeatures.LeftKnee, JointFeatures.RightKnee,
            JointFeatures.LeftAnkle, JointFeatures.RightAnkle
        };

        foreach (var joint in trackedJoints)
        {
            int idx = (int)joint;
            features.Add(frame.Joints[idx].Speed ?? float.NaN);
            features.Add(frame.Joints[idx].Acceleration ?? float.NaN);
        }

        features.Add(frame.LeftElbowAngle);
        features.Add(frame.RightElbowAngle);
        features.Add(frame.LeftShoulderAngle);
        features.Add(frame.RightShoulderAngle);
        features.Add(frame.LeftHipAngle);
        features.Add(frame.RightHipAngle);
        features.Add(frame.LeftKneeAngle);
        features.Add(frame.RightKneeAngle);

        for (int i = 0; i < MoveNetVideoProcessor.NumKeyPoints; i++)
        {
            float x = frame.Joints[i].Confidence < 0.2f ? float.NaN : frame.Joints[i].X;
            float y = frame.Joints[i].Confidence < 0.2f ? float.NaN : frame.Joints[i].Y;
            features.Add(x);
            features.Add(y);
        }

        return features.ToArray();
    }

    private static float[,] PadSequence(List<float[]> frameFeatures, int sequenceLength, int numFeatures)
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
