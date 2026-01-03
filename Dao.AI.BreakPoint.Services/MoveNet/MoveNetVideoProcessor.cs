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
    private const float MinPhaseConfidence = 0.6f;
    public static readonly int MinSwingFrames = 15;

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

        for (int frameIndex = 3; frameIndex < frameImages.Count; frameIndex += 3)
        {
            var firstFrame = frameImages[frameIndex - 2];
            (FrameData firstFrameData, double firstPhaseConfidence) = GetFrameData(
                firstFrame,
                prevFrame,
                prev2Frame,
                frameIndex - 2
            );
            prev2Frame = prevFrame;
            prevFrame = firstFrameData;

            var secondFrame = frameImages[frameIndex - 1];
            (FrameData secondFrameData, double secondPhaseConfidence) = GetFrameData(
                secondFrame,
                prevFrame,
                prev2Frame,
                frameIndex - 1
            );
            prev2Frame = prevFrame;
            prevFrame = secondFrameData;

            var thirdFrame = frameImages[frameIndex];
            (FrameData thirdFrameData, double thirdPhaseConfidence) = GetFrameData(
                thirdFrame,
                prevFrame,
                prev2Frame,
                frameIndex
            );
            prev2Frame = prevFrame;
            prevFrame = thirdFrameData;

            (FrameData, double) GetFrameData(
                byte[] frame,
                FrameData? prevFrame,
                FrameData? prev2Frame,
                int index
            )
            {
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
                return (
                    new FrameData
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
                        HipRotationSpeed = CalculateHipRotationSpeed(
                            keypoints,
                            prevFrame,
                            deltaTime
                        ),
                        FrameIndex = index,
                    },
                    classificationResult.Confidence
                );
            }

            // take every three frames and smooth out the phase by weighted majority voting
            SwingPhase averageSwingPhase = WeightedVotingSwingPhase([
                (firstFrameData.SwingPhase, firstPhaseConfidence),
                (secondFrameData.SwingPhase, secondPhaseConfidence),
                (thirdFrameData.SwingPhase, thirdPhaseConfidence),
            ]);

            SwingPhase WeightedVotingSwingPhase(
                List<(SwingPhase phase, double confidence)> swingPhasesByConfidence
            )
            {
                // if there are all the same, then return the consensus
                if (
                    swingPhasesByConfidence.All(sp =>
                        sp.phase == swingPhasesByConfidence.First().phase
                    )
                )
                {
                    return swingPhasesByConfidence.First().phase;
                }

                // if swing appears with high confidence, return that
                if (
                    swingPhasesByConfidence.Any(o =>
                        o.phase == SwingPhase.Swing && o.confidence > MinPhaseConfidence
                    )
                )
                {
                    return SwingPhase.Swing;
                }

                // Prioritize active swing phases (Backswing, Swing, FollowThrough) over None/Preparation
                // Weight = confidence + 0.3 bonus for each occurrence of the same phase
                var weightedScores = swingPhasesByConfidence
                    .GroupBy(sp => sp.phase)
                    .Select(g => new
                    {
                        Phase = g.Key,
                        // Base weight from sum of confidences
                        ConfidenceSum = g.Sum(x => x.confidence),
                        // Bonus for multiple occurrences (0.3 per occurrence)
                        OccurrenceBonus = g.Count() * 0.3,
                        // Bonus for active swing phases (0.2)
                        ActiveBonus = IsActiveSwingPhase(g.Key) ? 0.2 : 0.0,
                    })
                    .Select(x => new
                    {
                        x.Phase,
                        TotalWeight = x.ConfidenceSum + x.OccurrenceBonus + x.ActiveBonus,
                    })
                    .OrderByDescending(x => x.TotalWeight)
                    .First();

                return weightedScores.Phase;
            }
            // change the swing phase to be in congruency
            firstFrameData.SwingPhase =
                secondFrameData.SwingPhase =
                thirdFrameData.SwingPhase =
                    averageSwingPhase;
            // Non-swing phases (None/Preparation) - not part of active swing
            if (!IsActiveSwingPhase(averageSwingPhase))
            {
                // Check if we were tracking a swing that's now complete
                if (
                    currentSwingFrames.Count >= MinSwingFrames
                    && HasCompletedSwingProgression(currentSwingFrames)
                )
                {
                    // Ensure at least one Swing frame exists - find the transition point from Backswing to FollowThrough
                    CompleteSwing(swings, currentSwingFrames);
                    currentSwingFrames.Clear();
                }
                // Reset tracking because it has gotten too far away from the other swings
                else if (
                    currentSwingFrames.Count > 0
                    && currentSwingFrames.Last().FrameIndex + 15 < frameIndex
                )
                {
                    currentSwingFrames.Clear();
                }
                continue;
            }

            // Check if this frame should be included in the current swing
            if (!ShouldIncludeFrame(currentSwingFrames, averageSwingPhase))
            {
                continue;
            }

            currentSwingFrames.AddRange(firstFrameData, secondFrameData, thirdFrameData);

            cropRegion = DetermineCropRegion(
                thirdFrameData.Joints,
                videoMetadata.Height,
                videoMetadata.Width
            );
        }

        // Handle any remaining swing at end of video
        if (
            currentSwingFrames.Count >= MinSwingFrames
            && HasCompletedSwingProgression(currentSwingFrames)
        )
        {
            CompleteSwing(swings, currentSwingFrames);
        }

        return new ProcessedSwingVideo
        {
            Swings = swings,
            ImageHeight = videoMetadata.Height,
            ImageWidth = videoMetadata.Width,
            FrameRate = videoMetadata.FrameRate,
        };
    }

    private static void CompleteSwing(List<SwingData> swings, List<FrameData> currentSwingFrames)
    {
        if (!currentSwingFrames.Any(o => o.SwingPhase == SwingPhase.Swing))
        {
            // Find the last Backswing frame (right before FollowThrough begins)
            for (int i = currentSwingFrames.Count - 1; i >= 0; i--)
            {
                if (currentSwingFrames[i].SwingPhase == SwingPhase.Backswing)
                {
                    currentSwingFrames[i].SwingPhase = SwingPhase.Swing;
                    break;
                }
            }
        }
        swings.Add(new SwingData { Frames = [.. currentSwingFrames] });
    }

    /// <summary>
    /// Checks if the phase is an active swing phase (Backswing, Swing, or FollowThrough).
    /// None and Preparation are not considered active swing phases.
    /// </summary>
    private static bool IsActiveSwingPhase(SwingPhase phase) =>
        (phase is SwingPhase.Backswing or SwingPhase.Swing or SwingPhase.FollowThrough);

    /// <summary>
    /// Checks if a swing has completed a swing progression (Backswing -> Swing -> FollowThrough).
    /// Doesnt check the swing phase because it can easily be missed since it is the contact point
    /// </summary>
    private static bool HasCompletedSwingProgression(List<FrameData> swingFrames)
    {
        bool hasBackswing = swingFrames.Any(f => f.SwingPhase == SwingPhase.Backswing);
        bool hasFollowThrough = swingFrames.Any(f => f.SwingPhase == SwingPhase.FollowThrough);

        return hasBackswing && hasFollowThrough;
    }

    /// <summary>
    /// Determines if a frame should be included in the current swing based on AI phase classification.
    /// Relies primarily on the AI's phase detection while enforcing basic data quality checks.
    /// </summary>
    private static bool ShouldIncludeFrame(
        List<FrameData> currentSwingFrames,
        SwingPhase averageSwingPhase
    )
    {
        // Starting a new swing - must begin with Backswing (AI determines this)
        if (currentSwingFrames.Count == 0)
        {
            return averageSwingPhase == SwingPhase.Backswing;
        }

        var lastPhase = currentSwingFrames.Last().SwingPhase;

        // Backswing -> Backswing or Swing
        // Backswing -> FollowThrough (this is allowed in case Swing phase is missed)
        // Swing -> Swing or FollowThrough
        // FollowThrough -> FollowThrough
        return (lastPhase, averageSwingPhase) switch
        {
            (SwingPhase.Backswing, SwingPhase.Backswing) => true,
            (SwingPhase.Backswing, SwingPhase.Swing) => true,
            (SwingPhase.Backswing, SwingPhase.FollowThrough) => true,
            (SwingPhase.Swing, SwingPhase.Swing) => true,
            (SwingPhase.Swing, SwingPhase.FollowThrough) => true,
            (SwingPhase.FollowThrough, SwingPhase.FollowThrough) => true,
            _ => false,
        };
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
