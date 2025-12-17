using Dao.AI.BreakPoint.Data.Enums;
using Dao.AI.BreakPoint.Services.SwingAnalyzer;
using System.Numerics;

namespace Dao.AI.BreakPoint.Services.MoveNet;

public partial class MoveNetVideoProcessor(string modelPath) : IDisposable
{
    public const string ModelPath = "";
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

            float[] angles = _inferenceService.ComputeJointAngles(keypoints, videoMetadata.Height, videoMetadata.Width);

            var phase = DetermineSwingPhase(keypoints, angles, prevFrame, prev2Frame, deltaTime);
            if (phase == SwingPhase.Preparation)
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
                RightKneeAngle = angles[7],
                WristSpeed = Math.Max(keypoints[(int)JointFeatures.LeftWrist].Speed ?? 0,
                                    keypoints[(int)JointFeatures.RightWrist].Speed ?? 0),
                WristAcceleration = Math.Max(keypoints[(int)JointFeatures.LeftWrist].Acceleration ?? 0,
                                           keypoints[(int)JointFeatures.RightWrist].Acceleration ?? 0),
                ShoulderSpeed = Math.Max(keypoints[(int)JointFeatures.LeftShoulder].Speed ?? 0,
                                       keypoints[(int)JointFeatures.RightShoulder].Speed ?? 0),
                ElbowSpeed = Math.Max(keypoints[(int)JointFeatures.LeftElbow].Speed ?? 0,
                                    keypoints[(int)JointFeatures.RightElbow].Speed ?? 0),
                HipRotationSpeed = CalculateHipRotationSpeed(keypoints, prevFrame, deltaTime),
                FrameIndex = frameIndex
            };

            if (IsSwingComplete(currentSwingFrames, currentFrame))
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

            if (!IsFrameDuringSwing(currentFrame, currentSwingFrames))
            {
                continue;
            }

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
        FrameData currentFrame)
    {
        bool containsSufficientBackswing = currentSwingFrames
            .Where(frame => frame.SwingPhase == SwingPhase.Backswing).Count() >= 10;
        bool containsSufficientSwing = currentSwingFrames
            .Where(frame => frame.SwingPhase == SwingPhase.Swing).Count() >= 5;
        bool containsSufficientFollowThrough = currentSwingFrames
            .Where(frame => frame.SwingPhase == SwingPhase.FollowThrough).Count() >= 5;
        return currentFrame.SwingPhase != SwingPhase.FollowThrough &&
               containsSufficientBackswing &&
               containsSufficientSwing &&
               containsSufficientFollowThrough;
    }

    private static bool IsFrameDuringSwing(
        FrameData currentFrame,
        List<FrameData> currentSwingFrames)
    {
        var keypoints = currentFrame.Joints;
        var swingPhase = currentFrame.SwingPhase;
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
        JointData[] keypoints,
        float[] angles,
        FrameData? prevFrame = null,
        FrameData? prev2Frame = null,
        float deltaTime = 1 / 30f)
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

        // Calculate body positioning metrics with motion analysis
        return AnalyzeBodyPosition(leftShoulder, rightShoulder, leftHip, rightHip,
                                             hittingWrist, useRightArm, angles, prevFrame, prev2Frame, deltaTime, MinConfidence);
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

    private static SwingPhase AnalyzeBodyPosition(JointData leftShoulder, JointData rightShoulder,
                                                   JointData leftHip, JointData rightHip,
                                                   JointData hittingWrist, bool useRightArm,
                                                   float[] angles, FrameData? prevFrame, FrameData? prev2Frame,
                                                   float deltaTime, float MinConfidence)
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

        // Calculate motion-based metrics
        position.RacketSpeed = hittingWrist.Speed ?? 0;
        position.RacketAcceleration = hittingWrist.Acceleration ?? 0;

        // Elbow angle and angular velocity for hitting arm
        int elbowIndex = useRightArm ? 1 : 0; // RightElbowAngle : LeftElbowAngle
        position.ElbowAngle = angles[elbowIndex];
        position.ElbowAngularVelocity = CalculateAngularVelocity(elbowIndex, prevFrame, angles, deltaTime);

        // Shoulder angular velocity
        var hittingShoulder = useRightArm ? rightShoulder : leftShoulder;
        position.ShoulderAngularVelocity = hittingShoulder.Speed ?? 0;

        // Body positioning states
        position.ShouldersSquared = Math.Abs(position.ShoulderRotation) > 0.06f; // Wide shoulder separation
        position.IsCoiled = position.RacketPosition < -0.05f || !position.ShouldersSquared; // Racket back, narrow shoulders

        // Enhanced tennis-specific phase detection using motion analysis
        return DetermineSwingPhaseFromMotion(position, prevFrame, prev2Frame);
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

    private static float CalculateHipRotationSpeed(JointData[] keypoints, FrameData? prevFrame, float deltaTime)
    {
        if (prevFrame == null) return 0;

        var leftHip = keypoints[(int)JointFeatures.LeftHip];
        var rightHip = keypoints[(int)JointFeatures.RightHip];
        var prevLeftHip = prevFrame.Joints[(int)JointFeatures.LeftHip];
        var prevRightHip = prevFrame.Joints[(int)JointFeatures.RightHip];

        if (leftHip.Confidence < 0.3f || rightHip.Confidence < 0.3f) return 0;

        float currentHipWidth = rightHip.X - leftHip.X;
        float prevHipWidth = prevRightHip.X - prevLeftHip.X;

        return Math.Abs((currentHipWidth - prevHipWidth) / deltaTime);
    }

    private static float CalculateAngularVelocity(int angleIndex, FrameData? prevFrame, float[] currentAngles, float deltaTime)
    {
        if (prevFrame == null) return 0;

        float currentAngle = currentAngles[angleIndex];
        float prevAngle = angleIndex switch
        {
            0 => prevFrame.LeftElbowAngle,
            1 => prevFrame.RightElbowAngle,
            2 => prevFrame.LeftShoulderAngle,
            3 => prevFrame.RightShoulderAngle,
            4 => prevFrame.LeftHipAngle,
            5 => prevFrame.RightHipAngle,
            6 => prevFrame.LeftKneeAngle,
            7 => prevFrame.RightKneeAngle,
            _ => 0
        };

        return (currentAngle - prevAngle) / deltaTime;
    }

    private static SwingPhase DetermineSwingPhaseFromMotion(BodyPosition position, FrameData? prevFrame, FrameData? prev2Frame)
    {
        // Tennis-specific motion thresholds
        const float HighRacketSpeed = 15.0f; // pixels per frame
        const float MediumRacketSpeed = 8.0f;
        const float LowRacketSpeed = 3.0f;
        const float HighAcceleration = 5.0f;
        const float BackswingElbowAngle = 100.0f; // degrees
        const float ContactElbowAngle = 150.0f;

        // PREPARATION: Low speed, neutral position
        if (position.RacketSpeed < LowRacketSpeed &&
            Math.Abs(position.RacketPosition) < 0.03f &&
            position.ElbowAngle > 120 && position.ElbowAngle < 170)
        {
            return SwingPhase.Preparation;
        }

        // BACKSWING: Building speed, racket going back, elbow bent
        if ((position.IsCoiled || position.RacketPosition < -0.03f) &&
            position.RacketSpeed >= LowRacketSpeed &&
            position.ElbowAngle < BackswingElbowAngle)
        {
            return SwingPhase.Backswing;
        }

        // SWING: High speed with high acceleration, transitioning position
        if (position.RacketSpeed > MediumRacketSpeed &&
            position.RacketAcceleration > HighAcceleration &&
            position.ElbowAngle > BackswingElbowAngle &&
            position.ElbowAngle < ContactElbowAngle)
        {
            return SwingPhase.Swing;
        }

        // FOLLOW-THROUGH: High speed but decreasing acceleration, racket extended
        if ((position.ShouldersSquared || position.RacketPosition > 0.05f) &&
            (position.RacketSpeed > LowRacketSpeed || position.ElbowAngle > ContactElbowAngle))
        {
            return SwingPhase.FollowThrough;
        }

        // Enhanced logic using motion history
        if (prevFrame != null)
        {
            float speedTrend = position.RacketSpeed - prevFrame.WristSpeed;

            // Accelerating racket with coiled position = backswing
            if (speedTrend > 0 && position.IsCoiled)
            {
                return SwingPhase.Backswing;
            }

            // Decelerating racket with extended position = follow through
            if (speedTrend < -2.0f && position.ShouldersSquared)
            {
                return SwingPhase.FollowThrough;
            }
        }

        // Fallback to original position-based logic
        if (position.IsCoiled || position.RacketPosition < -0.03f)
        {
            return SwingPhase.Backswing;
        }

        if (position.ShouldersSquared || position.RacketPosition > 0.05f)
        {
            return SwingPhase.FollowThrough;
        }

        if (position.HipsOpen || (position.RacketPosition > -0.03f && position.RacketPosition < 0.05f))
        {
            return SwingPhase.Swing;
        }

        return SwingPhase.Preparation;
    }

    public void Dispose()
    {
        _inferenceService?.Dispose();
        GC.SuppressFinalize(this);
    }
}