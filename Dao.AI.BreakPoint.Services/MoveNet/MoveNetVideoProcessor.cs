using System.Numerics;
using Dao.AI.BreakPoint.Data.Enums;
using Dao.AI.BreakPoint.Services.SwingAnalyzer;

namespace Dao.AI.BreakPoint.Services.MoveNet;

/// <summary>
/// Creates a new MoveNetVideoProcessor with pose estimation and phase classification.
/// </summary>
/// <param name="moveNetModelPath">Path to MoveNet ONNX model for pose estimation.</param>
/// <param name="phaseClassifierModelPath">Path to swing phase classifier ONNX model.</param>
public partial class MoveNetVideoProcessor(string moveNetModelPath, string phaseClassifierModelPath)
    : IDisposable
{
    public static readonly Dictionary<JointFeatures, int> KeypointDict = new()
    {
        { JointFeatures.Nose, 0 },
        { JointFeatures.LeftEye, 1 },
        { JointFeatures.RightEye, 2 },
        { JointFeatures.LeftEar, 3 },
        { JointFeatures.RightEar, 4 },
        { JointFeatures.LeftShoulder, 5 },
        { JointFeatures.RightShoulder, 6 },
        { JointFeatures.LeftElbow, 7 },
        { JointFeatures.RightElbow, 8 },
        { JointFeatures.LeftWrist, 9 },
        { JointFeatures.RightWrist, 10 },
        { JointFeatures.LeftHip, 11 },
        { JointFeatures.RightHip, 12 },
        { JointFeatures.LeftKnee, 13 },
        { JointFeatures.RightKnee, 14 },
        { JointFeatures.LeftAnkle, 15 },
        { JointFeatures.RightAnkle, 16 },
    };
    public static int NumKeyPoints => KeypointDict.Count;

    private readonly MoveNetInferenceService _inferenceService = new(moveNetModelPath);
    private readonly SwingPhaseClassifierService _phaseClassifier = new(phaseClassifierModelPath);
    private const float MinCropKeypointScore = 0.2f;
    private const float MinConfidence = 0.3f;
    private const int MinSwingFrames = 15;

    public ProcessedSwingVideo ProcessVideoFrames(
        List<byte[]> frameImages,
        VideoMetadata videoMetadata,
        bool isRightHanded = true
    )
    {
        List<SwingData> swings = [];
        var cropRegion = CropRegion.InitCropRegion(videoMetadata.Height, videoMetadata.Width);

        List<FrameData> currentSwingFrames = [];

        float deltaTime = 1.0f / videoMetadata.FrameRate;

        FrameData? prev2Frame = null;
        FrameData? prevFrame = null;

        for (int frameIndex = 0; frameIndex < frameImages.Count; frameIndex++)
        {
            var frame = frameImages[frameIndex];

            var keypoints = _inferenceService.RunInference(
                frame,
                cropRegion,
                videoMetadata.Height,
                videoMetadata.Width,
                prevFrame,
                prev2Frame,
                deltaTime
            );

            float[] angles = _inferenceService.ComputeJointAngles(
                keypoints,
                videoMetadata.Height,
                videoMetadata.Width
            );

            var classificationResult = _phaseClassifier.ClassifyPhase(
                keypoints,
                angles,
                isRightHanded,
                prevFrame,
                prev2Frame
            );

            var phase = classificationResult.Phase;

            // Non-swing phases (None/Preparation) - not part of active swing
            if (!IsActiveSwingPhase(phase))
            {
                // Check if we were tracking a swing that's now complete
                if (
                    currentSwingFrames.Count >= MinSwingFrames
                    && HasCompletedSwingProgression(currentSwingFrames)
                )
                {
                    swings.Add(new SwingData { Frames = [.. currentSwingFrames] });
                }

                // Reset tracking
                currentSwingFrames.Clear();
                cropRegion = CropRegion.InitCropRegion(videoMetadata.Height, videoMetadata.Width);
                prev2Frame = prevFrame;
                prevFrame = null;
                continue;
            }

            var currentFrame = new FrameData
            {
                Joints = keypoints,
                SwingPhase = phase,
                LeftElbowAngle = angles[0],
                RightElbowAngle = angles[1],
                LeftShoulderAngle = angles[2],
                RightShoulderAngle = angles[3],
                LeftHipAngle = angles[4],
                RightHipAngle = angles[5],
                LeftKneeAngle = angles[6],
                RightKneeAngle = angles[7],
                WristSpeed = Math.Max(
                    keypoints[(int)JointFeatures.LeftWrist].Speed ?? 0,
                    keypoints[(int)JointFeatures.RightWrist].Speed ?? 0
                ),
                WristAcceleration = Math.Max(
                    keypoints[(int)JointFeatures.LeftWrist].Acceleration ?? 0,
                    keypoints[(int)JointFeatures.RightWrist].Acceleration ?? 0
                ),
                ShoulderSpeed = Math.Max(
                    keypoints[(int)JointFeatures.LeftShoulder].Speed ?? 0,
                    keypoints[(int)JointFeatures.RightShoulder].Speed ?? 0
                ),
                ElbowSpeed = Math.Max(
                    keypoints[(int)JointFeatures.LeftElbow].Speed ?? 0,
                    keypoints[(int)JointFeatures.RightElbow].Speed ?? 0
                ),
                HipRotationSpeed = CalculateHipRotationSpeed(keypoints, prevFrame, deltaTime),
                FrameIndex = frameIndex,
            };

            // Check if this completes the current swing
            if (IsSwingComplete(currentSwingFrames, currentFrame))
            {
                currentSwingFrames.Add(currentFrame);
                swings.Add(new SwingData { Frames = [.. currentSwingFrames] });

                currentSwingFrames.Clear();
                cropRegion = CropRegion.InitCropRegion(videoMetadata.Height, videoMetadata.Width);
                prev2Frame = null;
                prevFrame = null;
                continue;
            }

            // Check if this frame should be included in the current swing
            if (!ShouldIncludeFrame(currentFrame, currentSwingFrames))
            {
                continue;
            }

            currentSwingFrames.Add(currentFrame);

            cropRegion = DetermineCropRegion(keypoints, videoMetadata.Height, videoMetadata.Width);

            prev2Frame = prevFrame;
            prevFrame = currentFrame;
        }

        // Handle any remaining swing at end of video
        if (
            currentSwingFrames.Count >= MinSwingFrames
            && HasCompletedSwingProgression(currentSwingFrames)
        )
        {
            swings.Add(new SwingData { Frames = currentSwingFrames });
        }

        return new ProcessedSwingVideo
        {
            Swings = swings,
            ImageHeight = videoMetadata.Height,
            ImageWidth = videoMetadata.Width,
            FrameRate = videoMetadata.FrameRate,
        };
    }

    /// <summary>
    /// Checks if the phase is an active swing phase (Backswing, Swing, or FollowThrough).
    /// None and Preparation are not considered active swing phases.
    /// </summary>
    private static bool IsActiveSwingPhase(SwingPhase phase) =>
        phase is SwingPhase.Backswing or SwingPhase.Swing or SwingPhase.FollowThrough;

    /// <summary>
    /// A swing is complete when:
    /// 1. We've accumulated enough frames
    /// 2. We've reached FollowThrough phase
    /// 3. The current frame shows a phase regression (e.g., back to Backswing after FollowThrough)
    /// </summary>
    private static bool IsSwingComplete(List<FrameData> currentSwingFrames, FrameData currentFrame)
    {
        if (currentSwingFrames.Count < MinSwingFrames)
        {
            return false;
        }

        // Check if we've completed the swing progression (have FollowThrough frames)
        bool hasFollowThrough = currentSwingFrames.Any(f =>
            f.SwingPhase == SwingPhase.FollowThrough
        );
        if (!hasFollowThrough)
        {
            return false;
        }

        var lastPhase = currentSwingFrames.Last().SwingPhase;
        var currentPhase = currentFrame.SwingPhase;

        // Swing is complete when AI detects a phase regression from FollowThrough
        // (e.g., FollowThrough -> Backswing indicates a new swing is starting)
        return lastPhase == SwingPhase.FollowThrough && currentPhase == SwingPhase.Backswing;
    }

    /// <summary>
    /// Checks if a swing has completed the full progression (Backswing -> Swing -> FollowThrough).
    /// </summary>
    private static bool HasCompletedSwingProgression(List<FrameData> swingFrames)
    {
        bool hasBackswing = swingFrames.Any(f => f.SwingPhase == SwingPhase.Backswing);
        bool hasSwing = swingFrames.Any(f => f.SwingPhase == SwingPhase.Swing);
        bool hasFollowThrough = swingFrames.Any(f => f.SwingPhase == SwingPhase.FollowThrough);

        return hasBackswing && hasSwing && hasFollowThrough;
    }

    /// <summary>
    /// Determines if a frame should be included in the current swing based on AI phase classification.
    /// Relies primarily on the AI's phase detection while enforcing basic data quality checks.
    /// </summary>
    private static bool ShouldIncludeFrame(
        FrameData currentFrame,
        List<FrameData> currentSwingFrames
    )
    {
        var keypoints = currentFrame.Joints;
        var phase = currentFrame.SwingPhase;

        // Data quality check: ensure upper body is visible for reliable swing analysis
        if (!IsUpperBodyVisible(keypoints))
        {
            return false;
        }

        // Starting a new swing - must begin with Backswing (AI determines this)
        if (currentSwingFrames.Count == 0)
        {
            return phase == SwingPhase.Backswing;
        }

        var lastPhase = currentSwingFrames.Last().SwingPhase;

        // Backswing -> Backswing or Swing
        // Swing -> Swing or FollowThrough
        // FollowThrough -> FollowThrough
        return (lastPhase, phase) switch
        {
            (SwingPhase.Backswing, SwingPhase.Backswing) => true,
            (SwingPhase.Backswing, SwingPhase.Swing) => true,
            (SwingPhase.Swing, SwingPhase.Swing) => true,
            (SwingPhase.Swing, SwingPhase.FollowThrough) => true,
            (SwingPhase.FollowThrough, SwingPhase.FollowThrough) => true,
            _ => false,
        };
    }

    /// <summary>
    /// Checks if upper body joints are visible with sufficient confidence for reliable analysis.
    /// </summary>
    private static bool IsUpperBodyVisible(JointData[] keypoints)
    {
        var leftShoulder = keypoints[(int)JointFeatures.LeftShoulder];
        var rightShoulder = keypoints[(int)JointFeatures.RightShoulder];
        var leftElbow = keypoints[(int)JointFeatures.LeftElbow];
        var rightElbow = keypoints[(int)JointFeatures.RightElbow];
        var leftWrist = keypoints[(int)JointFeatures.LeftWrist];
        var rightWrist = keypoints[(int)JointFeatures.RightWrist];

        return (leftShoulder.Confidence > MinConfidence || rightShoulder.Confidence > MinConfidence)
            && (leftElbow.Confidence > MinConfidence || rightElbow.Confidence > MinConfidence)
            && (leftWrist.Confidence > MinConfidence || rightWrist.Confidence > MinConfidence);
    }

    private static CropRegion DetermineCropRegion(
        JointData[] keypoints,
        int imageHeight,
        int imageWidth
    )
    {
        // Convert to pixel coordinates
        var targetKeypoints = new Dictionary<JointFeatures, Vector2>();

        foreach (var kvp in KeypointDict)
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
            float centerY =
                (
                    targetKeypoints[JointFeatures.LeftHip].Y
                    + targetKeypoints[JointFeatures.RightHip].Y
                ) / 2;
            float centerX =
                (
                    targetKeypoints[JointFeatures.LeftHip].X
                    + targetKeypoints[JointFeatures.RightHip].X
                ) / 2;

            var (maxTorsoYRange, maxTorsoXRange, maxBodyYRange, maxBodyXRange) =
                DetermineTorsoAndBodyRange(keypoints, targetKeypoints, centerY, centerX);

            float cropLengthHalf = Math.Max(
                Math.Max(maxTorsoXRange * 1.9f, maxTorsoYRange * 1.9f),
                Math.Max(maxBodyYRange * 1.2f, maxBodyXRange * 1.2f)
            );

            float[] tmp = [centerX, imageWidth - centerX, centerY, imageHeight - centerY];
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
                    Width = cropLength / imageWidth,
                };
            }
        }
        else
        {
            return CropRegion.InitCropRegion(imageHeight, imageWidth);
        }
    }

    private static bool IsTorsoVisible(JointData[] keypoints)
    {
        return (
                keypoints[(int)JointFeatures.LeftHip].Confidence > MinCropKeypointScore
                || keypoints[(int)JointFeatures.RightHip].Confidence > MinCropKeypointScore
            )
            && (
                keypoints[(int)JointFeatures.LeftShoulder].Confidence > MinCropKeypointScore
                || keypoints[(int)JointFeatures.RightShoulder].Confidence > MinCropKeypointScore
            );
    }

    private static (float, float, float, float) DetermineTorsoAndBodyRange(
        JointData[] keypoints,
        Dictionary<JointFeatures, Vector2> targetKeypoints,
        float centerY,
        float centerX
    )
    {
        JointFeatures[] torsoJoints =
        {
            JointFeatures.LeftShoulder,
            JointFeatures.RightShoulder,
            JointFeatures.LeftHip,
            JointFeatures.RightHip,
        };
        float maxTorsoYRange = 0.0f;
        float maxTorsoXRange = 0.0f;

        foreach (JointFeatures joint in torsoJoints)
        {
            float distY = Math.Abs(centerY - targetKeypoints[joint].Y);
            float distX = Math.Abs(centerX - targetKeypoints[joint].X);
            if (distY > maxTorsoYRange)
                maxTorsoYRange = distY;
            if (distX > maxTorsoXRange)
                maxTorsoXRange = distX;
        }

        float maxBodyYRange = 0.0f;
        float maxBodyXRange = 0.0f;

        foreach (var kvp in KeypointDict)
        {
            if (keypoints[kvp.Value].Confidence < MinCropKeypointScore)
                continue;

            float distY = Math.Abs(centerY - targetKeypoints[kvp.Key].Y);
            float distX = Math.Abs(centerX - targetKeypoints[kvp.Key].X);
            if (distY > maxBodyYRange)
                maxBodyYRange = distY;
            if (distX > maxBodyXRange)
                maxBodyXRange = distX;
        }

        return (maxTorsoYRange, maxTorsoXRange, maxBodyYRange, maxBodyXRange);
    }

    private static float CalculateHipRotationSpeed(
        JointData[] keypoints,
        FrameData? prevFrame,
        float deltaTime
    )
    {
        if (prevFrame == null)
            return 0;

        var leftHip = keypoints[(int)JointFeatures.LeftHip];
        var rightHip = keypoints[(int)JointFeatures.RightHip];
        var prevLeftHip = prevFrame.Joints[(int)JointFeatures.LeftHip];
        var prevRightHip = prevFrame.Joints[(int)JointFeatures.RightHip];

        if (leftHip.Confidence < 0.3f || rightHip.Confidence < 0.3f)
            return 0;

        float currentHipWidth = rightHip.X - leftHip.X;
        float prevHipWidth = prevRightHip.X - prevLeftHip.X;

        return Math.Abs((currentHipWidth - prevHipWidth) / deltaTime);
    }

    public void Dispose()
    {
        _inferenceService?.Dispose();
        _phaseClassifier?.Dispose();
        GC.SuppressFinalize(this);
    }
}
