using Dao.AI.BreakPoint.Services.SwingAnalyzer;
using System.Numerics;

namespace Dao.AI.BreakPoint.Services.MoveNet;

public class MoveNetVideoProcessor(string modelPath) : IDisposable
{
    private readonly MoveNetInferenceService _inferenceService = new(modelPath);
    private readonly MoveNetPoseFeatureExtractorService _featureExtractor = new();
    private const float MinCropKeypointScore = 0.2f;

    public List<FrameData> ProcessVideoFrames(List<byte[]> frameImages, int imageHeight, int imageWidth)
    {
        var frames = new List<FrameData>();
        var cropRegion = CropRegion.InitCropRegion(imageHeight, imageWidth);

        for (int i = 0; i < frameImages.Count; i++)
        {
            // Run inference on cropped/resized image
            var keypoints = RunInferenceWithCrop(frameImages[i], cropRegion);

            // Create frame data
            var frameData = new FrameData
            {
                FrameNumber = i,
                SwingPoseFeatures = keypoints
            };

            frames.Add(frameData);

            // Update crop region for next frame (tracking)
            cropRegion = DetermineCropRegion(keypoints, imageHeight, imageWidth);
        }

        return frames;
    }

    private SwingPoseFeatures[] RunInferenceWithCrop(byte[] imageBytes, CropRegion cropRegion)
    {
        var keypoints = _inferenceService.RunInference(imageBytes, cropRegion);

        // Update coordinates from crop region to original image coordinates
        // (similar to your Python run_inference function)
        for (int idx = 0; idx < 17; idx++)
        {
            keypoints[idx].Y = cropRegion.YMin + (cropRegion.Height * keypoints[idx].Y);
            keypoints[idx].X = cropRegion.XMin + (cropRegion.Width * keypoints[idx].X);
        }

        return keypoints;
    }

    private CropRegion DetermineCropRegion(SwingPoseFeatures[] keypoints, int imageHeight, int imageWidth)
    {
        // Convert to pixel coordinates
        var targetKeypoints = new Dictionary<string, Vector2>();
        var keypointDict = GetKeypointDict();

        foreach (var kvp in keypointDict)
        {
            int idx = kvp.Value;
            targetKeypoints[kvp.Key] = new Vector2(
                keypoints[idx].X * imageWidth,
                keypoints[idx].Y * imageHeight
            );
        }

        if (IsTorsoVisible(keypoints))
        {
            // Calculate center from hips
            float centerY = (targetKeypoints["left_hip"].Y + targetKeypoints["right_hip"].Y) / 2;
            float centerX = (targetKeypoints["left_hip"].X + targetKeypoints["right_hip"].X) / 2;

            var (maxTorsoYRange, maxTorsoXRange, maxBodyYRange, maxBodyXRange) =
                DetermineTorsoAndBodyRange(keypoints, targetKeypoints, centerY, centerX);

            float cropLengthHalf = Math.Max(
                Math.Max(maxTorsoXRange * 1.9f, maxTorsoYRange * 1.9f),
                Math.Max(maxBodyYRange * 1.2f, maxBodyXRange * 1.2f)
            );

            float[] tmp = { centerX, imageWidth - centerX, centerY, imageHeight - centerY };
            cropLengthHalf = Math.Min(cropLengthHalf, tmp.Max());

            var cropCorner = new Vector2(centerY - cropLengthHalf, centerX - cropLengthHalf);

            if (cropLengthHalf > Math.Max(imageWidth, imageHeight) / 2.0f)
            {
                return CropRegion.InitCropRegion(imageHeight, imageWidth);
            }
            else
            {
                float cropLength = cropLengthHalf * 2;
                return new CropRegion
                {
                    YMin = cropCorner.Y / imageHeight,
                    XMin = cropCorner.X / imageWidth,
                    YMax = (cropCorner.Y + cropLength) / imageHeight,
                    XMax = (cropCorner.X + cropLength) / imageWidth,
                    Height = cropLength / imageHeight,
                    Width = cropLength / imageWidth
                };
            }
        }
        else
        {
            return CropRegion.InitCropRegion(imageHeight, imageWidth);
        }
    }

    private bool IsTorsoVisible(SwingPoseFeatures[] keypoints)
    {
        var dict = GetKeypointDict();
        return (keypoints[dict["left_hip"]].Confidence > MinCropKeypointScore ||
                keypoints[dict["right_hip"]].Confidence > MinCropKeypointScore) &&
               (keypoints[dict["left_shoulder"]].Confidence > MinCropKeypointScore ||
                keypoints[dict["right_shoulder"]].Confidence > MinCropKeypointScore);
    }

    private (float, float, float, float) DetermineTorsoAndBodyRange(
        SwingPoseFeatures[] keypoints,
        Dictionary<string, Vector2> targetKeypoints,
        float centerY,
        float centerX)
    {
        string[] torsoJoints = { "left_shoulder", "right_shoulder", "left_hip", "right_hip" };
        float maxTorsoYRange = 0.0f;
        float maxTorsoXRange = 0.0f;

        foreach (string joint in torsoJoints)
        {
            float distY = Math.Abs(centerY - targetKeypoints[joint].Y);
            float distX = Math.Abs(centerX - targetKeypoints[joint].X);
            if (distY > maxTorsoYRange) maxTorsoYRange = distY;
            if (distX > maxTorsoXRange) maxTorsoXRange = distX;
        }

        float maxBodyYRange = 0.0f;
        float maxBodyXRange = 0.0f;
        var keypointDict = GetKeypointDict();

        foreach (var kvp in keypointDict)
        {
            if (keypoints[kvp.Value].Confidence < MinCropKeypointScore) continue;

            float distY = Math.Abs(centerY - targetKeypoints[kvp.Key].Y);
            float distX = Math.Abs(centerX - targetKeypoints[kvp.Key].X);
            if (distY > maxBodyYRange) maxBodyYRange = distY;
            if (distX > maxBodyXRange) maxBodyXRange = distX;
        }

        return (maxTorsoYRange, maxTorsoXRange, maxBodyYRange, maxBodyXRange);
    }

    public static Dictionary<string, int> GetKeypointDict()
    {
        return new Dictionary<string, int>
        {
            {"nose", 0}, {"left_eye", 1}, {"right_eye", 2}, {"left_ear", 3}, {"right_ear", 4},
            {"left_shoulder", 5}, {"right_shoulder", 6}, {"left_elbow", 7}, {"right_elbow", 8},
            {"left_wrist", 9}, {"right_wrist", 10}, {"left_hip", 11}, {"right_hip", 12},
            {"left_knee", 13}, {"right_knee", 14}, {"left_ankle", 15}, {"right_ankle", 16}
        };
    }

    public void Dispose()
    {
        _inferenceService?.Dispose();
    }
}