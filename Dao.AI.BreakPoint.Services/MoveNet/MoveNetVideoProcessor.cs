using Dao.AI.BreakPoint.Data.Enums;
using Dao.AI.BreakPoint.Services.SwingAnalyzer;
using System.Numerics;

namespace Dao.AI.BreakPoint.Services.MoveNet;

public partial class MoveNetVideoProcessor(string modelPath) : IDisposable
{
    public static readonly Dictionary<JointFeatures, int> KeypointDict = new()
    {
        {JointFeatures.Nose, 0},
        {JointFeatures.LeftEye, 1},
        {JointFeatures.RightEye, 2},
        {JointFeatures.LeftEar, 3},
        {JointFeatures.RightEar, 4},
        {JointFeatures.LeftShoulder, 5},
        {JointFeatures.RightShoulder, 6},
        {JointFeatures.LeftElbow, 7},
        {JointFeatures.RightElbow, 8},
        {JointFeatures.LeftWrist, 9},
        {JointFeatures.RightWrist, 10},
        {JointFeatures.LeftHip, 11},
        {JointFeatures.RightHip, 12},
        {JointFeatures.LeftKnee, 13},
        {JointFeatures.RightKnee, 14},
        {JointFeatures.LeftAnkle, 15},
        {JointFeatures.RightAnkle, 16}
    };
    public static int NumKeyPoints => KeypointDict.Count;
    private readonly MoveNetInferenceService _inferenceService = new(modelPath);
    private const float MinCropKeypointScore = 0.2f;
    private const float MinConfidence = 0.3f;

    public ProcessedSwingVideo ProcessVideoFrames(
        List<byte[]> frameImages,
        VideoMetadata videoMetadata)
    {
        List<SwingData> swings = [];
        var cropRegion = CropRegion.InitCropRegion(videoMetadata.Height, videoMetadata.Width);

        List<FrameData> currentSwingFrames = [];
        int framesSinceLastSwing = 0;

        float deltaTime = 1.0f / videoMetadata.FrameRate;

        FrameData? prev2Frame = null;
        FrameData? prevFrame = null;

        for (int frameIndex = 0; frameIndex < frameImages.Count; frameIndex++)
        {
            var frame = frameImages[frameIndex];
            framesSinceLastSwing++;

            var keypoints = _inferenceService.RunInference(
                frame,
                cropRegion,
                videoMetadata.Height,
                videoMetadata.Width,
                prevFrame,
                prev2Frame,
                deltaTime);

            var angles = _inferenceService.ComputeJointAngles(keypoints, videoMetadata.Height, videoMetadata.Width);

            var phase = DetermineSwingPhase(keypoints);
            if (phase == SwingPhase.Preparation)
            {
                continue;
            }

            if (IsSwingComplete(currentSwingFrames, phase))
            {
                swings.Add(new SwingData
                {
                    Frames = [.. currentSwingFrames]
                });

                currentSwingFrames.Clear();
                framesSinceLastSwing = 0;
                cropRegion = CropRegion.InitCropRegion(videoMetadata.Height, videoMetadata.Width);
                prev2Frame = null;
                prevFrame = null;
                continue;
            }

            if (!IsFrameDuringSwing(keypoints, currentSwingFrames, phase))
            {
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
                RightKneeAngle = angles[7]
            };

            currentSwingFrames.Add(currentFrame);

            cropRegion = DetermineCropRegion(keypoints, videoMetadata.Height, videoMetadata.Width);

            prev2Frame = prevFrame;
            prevFrame = currentFrame;
        }

        if (currentSwingFrames.Count >= 15)
        {
            swings.Add(new SwingData
            {
                Frames = currentSwingFrames
            });
        }

        return new ProcessedSwingVideo
        {
            Swings = swings,
            ImageHeight = videoMetadata.Height,
            ImageWidth = videoMetadata.Width,
            FrameRate = videoMetadata.FrameRate
        };
    }

    private static bool IsSwingComplete(
        List<FrameData> currentSwingFrames,
        SwingPhase phase)
    {
        bool containsSufficientBackswing = currentSwingFrames
            .Where(frame => frame.SwingPhase == SwingPhase.Backswing).Count() >= 10;
        bool containsSufficientSwing = currentSwingFrames
            .Where(frame => frame.SwingPhase == SwingPhase.Swing).Count() >= 5;
        bool containsSufficientFollowThrough = currentSwingFrames
            .Where(frame => frame.SwingPhase == SwingPhase.FollowThrough).Count() >= 5;
        return phase != SwingPhase.FollowThrough &&
               containsSufficientBackswing &&
               containsSufficientSwing &&
               containsSufficientFollowThrough;
    }

    private static bool IsFrameDuringSwing(
        JointData[] keypoints,
        List<FrameData> currentSwingFrames,
        SwingPhase swingPhase)
    {
        // Check if key tennis swing joints are visible with sufficient confidence
        var leftShoulder = keypoints[(int)JointFeatures.LeftShoulder];
        var rightShoulder = keypoints[(int)JointFeatures.RightShoulder];
        var leftElbow = keypoints[(int)JointFeatures.LeftElbow];
        var rightElbow = keypoints[(int)JointFeatures.RightElbow];
        var leftWrist = keypoints[(int)JointFeatures.LeftWrist];
        var rightWrist = keypoints[(int)JointFeatures.RightWrist];

        // Check if upper body is visible (essential for swing detection)
        bool upperBodyVisible = (leftShoulder.Confidence > MinConfidence || rightShoulder.Confidence > MinConfidence) &&
                               (leftElbow.Confidence > MinConfidence || rightElbow.Confidence > MinConfidence) &&
                               (leftWrist.Confidence > MinConfidence || rightWrist.Confidence > MinConfidence);

        if (!upperBodyVisible)
        {
            return false;
        }
        bool isStartOfSwing = currentSwingFrames.Count == 0;

        // If we're not currently tracking a swing, only start if we detect backswing
        if (isStartOfSwing)
        {
            return swingPhase == SwingPhase.Backswing;
        }
        var lastSwingFramePhase = currentSwingFrames.Last().SwingPhase;
        // the swing must progress in order: backswing -> swing -> follow through
        // if a person was in back swing, they should either be in swing or still in backswing
        if (lastSwingFramePhase == SwingPhase.Backswing)
        {
            return swingPhase == SwingPhase.Backswing || swingPhase == SwingPhase.Swing;
        }
        // if last frame was swing, only continue if still in swing or follow through
        else if (lastSwingFramePhase == SwingPhase.Swing)
        {
            return swingPhase == SwingPhase.Swing || swingPhase == SwingPhase.FollowThrough;
        }
        // if last frame was follow through, only continue if still in follow through
        else if (lastSwingFramePhase == SwingPhase.FollowThrough)
        {
            return swingPhase == SwingPhase.FollowThrough;
        }
        return false;
    }

    private static SwingPhase DetermineSwingPhase(
        JointData[] keypoints)
    {
        // Get key points for swing analysis
        var leftShoulder = keypoints[(int)JointFeatures.LeftShoulder];
        var rightShoulder = keypoints[(int)JointFeatures.RightShoulder];
        var leftWrist = keypoints[(int)JointFeatures.LeftWrist];
        var rightWrist = keypoints[(int)JointFeatures.RightWrist];
        var leftHip = keypoints[(int)JointFeatures.LeftHip];
        var rightHip = keypoints[(int)JointFeatures.RightHip];

        // Need both shoulders and at least one hip visible
        if (leftShoulder.Confidence < MinConfidence || rightShoulder.Confidence < MinConfidence ||
            (leftHip.Confidence < MinConfidence && rightHip.Confidence < MinConfidence))
        {
            return SwingPhase.Preparation;
        }

        // Determine hitting arm and get wrist position
        bool useRightArm = DetermineHittingArm(keypoints, MinConfidence);

        var hittingWrist = useRightArm ? rightWrist : leftWrist;
        if (hittingWrist.Confidence < MinConfidence)
        {
            return SwingPhase.Preparation;
        }

        // Calculate body positioning metrics
        var bodyPosition = AnalyzeBodyPosition(leftShoulder, rightShoulder, leftHip, rightHip,
                                             hittingWrist, useRightArm, MinConfidence);

        return DeterminePhaseFromBodyPosition(bodyPosition, useRightArm);
    }

    /// <summary>
    /// true for right arm
    /// </summary>
    /// <returns></returns>
    private static bool DetermineHittingArm(JointData[] keypoints, float minConfidence)
    {
        // todo: need to write based on what features are available
        return true;
    }

    private static BodyPosition AnalyzeBodyPosition(JointData leftShoulder, JointData rightShoulder,
                                                   JointData leftHip, JointData rightHip,
                                                   JointData hittingWrist, bool useRightArm,
                                                   float MinConfidence)
    {
        var position = new BodyPosition();

        // Calculate body center
        float bodyCenterX = (leftShoulder.X + rightShoulder.X) / 2;

        // Shoulder rotation: negative = sideways/coiled, positive = opening/squared
        float shoulderWidth = rightShoulder.X - leftShoulder.X;
        position.ShoulderRotation = shoulderWidth; // Wider = more squared to court

        // Hip rotation (if both hips visible)
        if (leftHip.Confidence > MinConfidence && rightHip.Confidence > MinConfidence)
        {
            float hipWidth = rightHip.X - leftHip.X;
            position.HipRotation = hipWidth;
            position.HipsOpen = Math.Abs(hipWidth) > 0.08f; // Significant hip separation = open
        }
        else
        {
            position.HipRotation = 0;
            position.HipsOpen = false;
        }

        // Racket position relative to body
        position.RacketPosition = hittingWrist.X - bodyCenterX;
        if (!useRightArm) position.RacketPosition *= -1; // Flip for left-handed

        // Body positioning states
        position.ShouldersSquared = Math.Abs(position.ShoulderRotation) > 0.06f; // Wide shoulder separation
        position.IsCoiled = position.RacketPosition < -0.05f || !position.ShouldersSquared; // Racket back, narrow shoulders

        return position;
    }

    private static SwingPhase DeterminePhaseFromBodyPosition(BodyPosition position, bool useRightArm)
    {
        // BACKSWING: Body is coiled, racket is back
        if (position.IsCoiled || position.RacketPosition < -0.03f)
        {
            return SwingPhase.Backswing;
        }

        // FOLLOW-THROUGH: Shoulders squared, racket extended forward
        if (position.ShouldersSquared || position.RacketPosition > 0.05f)
        {
            return SwingPhase.FollowThrough;
        }

        // SWING: Transitioning - hips opening, shoulders starting to open, racket moving forward
        if (position.HipsOpen || (position.RacketPosition > -0.03f && position.RacketPosition < 0.05f))
        {
            return SwingPhase.Swing;
        }

        // PREPARATION: Default - facing forward in athletic stance
        return SwingPhase.Preparation;
    }

    private static CropRegion DetermineCropRegion(JointData[] keypoints, int imageHeight, int imageWidth)
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
            float centerY = (targetKeypoints[JointFeatures.LeftHip].Y + targetKeypoints[JointFeatures.RightHip].Y) / 2;
            float centerX = (targetKeypoints[JointFeatures.LeftHip].X + targetKeypoints[JointFeatures.RightHip].X) / 2;

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
                    Width = cropLength / imageWidth
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
        return (keypoints[(int)JointFeatures.LeftHip].Confidence > MinCropKeypointScore ||
                keypoints[(int)JointFeatures.RightHip].Confidence > MinCropKeypointScore) &&
               (keypoints[(int)JointFeatures.LeftShoulder].Confidence > MinCropKeypointScore ||
                keypoints[(int)JointFeatures.RightShoulder].Confidence > MinCropKeypointScore);
    }

    private static (float, float, float, float) DetermineTorsoAndBodyRange(
        JointData[] keypoints,
        Dictionary<JointFeatures, Vector2> targetKeypoints,
        float centerY,
        float centerX)
    {
        JointFeatures[] torsoJoints = { JointFeatures.LeftShoulder, JointFeatures.RightShoulder, JointFeatures.LeftHip, JointFeatures.RightHip };
        float maxTorsoYRange = 0.0f;
        float maxTorsoXRange = 0.0f;

        foreach (JointFeatures joint in torsoJoints)
        {
            float distY = Math.Abs(centerY - targetKeypoints[joint].Y);
            float distX = Math.Abs(centerX - targetKeypoints[joint].X);
            if (distY > maxTorsoYRange) maxTorsoYRange = distY;
            if (distX > maxTorsoXRange) maxTorsoXRange = distX;
        }

        float maxBodyYRange = 0.0f;
        float maxBodyXRange = 0.0f;

        foreach (var kvp in KeypointDict)
        {
            if (keypoints[kvp.Value].Confidence < MinCropKeypointScore) continue;

            float distY = Math.Abs(centerY - targetKeypoints[kvp.Key].Y);
            float distX = Math.Abs(centerX - targetKeypoints[kvp.Key].X);
            if (distY > maxBodyYRange) maxBodyYRange = distY;
            if (distX > maxBodyXRange) maxBodyXRange = distX;
        }

        return (maxTorsoYRange, maxTorsoXRange, maxBodyYRange, maxBodyXRange);
    }

    public void Dispose()
    {
        _inferenceService?.Dispose();
        GC.SuppressFinalize(this);
    }
}