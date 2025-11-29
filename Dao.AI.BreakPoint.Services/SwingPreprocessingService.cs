using Dao.AI.BreakPoint.Services.MoveNet;
using Dao.AI.BreakPoint.Services.SwingAnalyzer;
using System.Numerics;

namespace Dao.AI.BreakPoint.Services
{
    public class SwingPreprocessingService(IPoseFeatureExtractorService poseFeatureExtractorService)
    {
        private const float MIN_CONFIDENCE = 0.2f;

        public async Task<float[,]> PreprocessSwingAsync(
            SwingData swing,
            ProcessedSwingVideo video,
            int sequenceLength,
            int numFeatures)
        {
            var frameFeatures = new List<float[]>();
            Vector2[]? prev2Positions = null;
            Vector2[]? prevPositions = null;

            foreach (var frame in swing.Frames)
            {
                var (currentPositions, confidences) = MoveNetPoseFeatureExtractorService.KeypointsToPixels(
                    frame, video.ImageHeight, video.ImageWidth);

                var validKeypoints = confidences.Count(c => c > MIN_CONFIDENCE);
                if (validKeypoints < 8)
                {
                    // Optionally log warning
                }

                var features = poseFeatureExtractorService.BuildFrameFeatures(
                    prev2Positions,
                    prevPositions,
                    currentPositions,
                    confidences,
                    1.0f / video.FrameRate);

                if (features == null || features.Length != numFeatures)
                {
                    // Optionally log warning
                    continue;
                }

                frameFeatures.Add(features);

                prev2Positions = prevPositions;
                prevPositions = currentPositions;
            }

            if (frameFeatures.Count == 0)
            {
                throw new InvalidOperationException("No valid frame features extracted from swing");
            }

            return NormalizeAndPadSequence(frameFeatures, sequenceLength, numFeatures);
        }

        public static float[,] NormalizeAndPadSequence(List<float[]> frameFeatures, int sequenceLength, int numFeatures)
        {
            var normalizedSequence = new float[sequenceLength, numFeatures];
            var actualLength = Math.Min(frameFeatures.Count, sequenceLength);

            for (int frameIdx = 0; frameIdx < actualLength; frameIdx++)
            {
                var features = frameFeatures[frameIdx];
                for (int featIdx = 0; featIdx < Math.Min(features.Length, numFeatures); featIdx++)
                {
                    var value = features[featIdx];
                    if (float.IsNaN(value) || float.IsInfinity(value))
                    {
                        normalizedSequence[frameIdx, featIdx] = 0.0f;
                    }
                    else
                    {
                        normalizedSequence[frameIdx, featIdx] = Math.Max(-1000.0f, Math.Min(1000.0f, value));
                    }
                }
            }
            return normalizedSequence;
        }
    }
}
