using Dao.AI.BreakPoint.Data.Enums;
using Dao.AI.BreakPoint.Services.SwingAnalyzer;
using System.Numerics;

namespace Dao.AI.BreakPoint.Services.MoveNet;

public class MoveNetVideoProcessor(string modelPath) : IDisposable
{
    public static readonly Dictionary<string, int> KeypointDict = new()
    {
        {"nose", 0},
        {"left_eye", 1},
        {"right_eye", 2},
        {"left_ear", 3},
        {"right_ear", 4},
        {"left_shoulder", 5},
        {"right_shoulder", 6},
        {"left_elbow", 7},
        {"right_elbow", 8},
        {"left_wrist", 9},
        {"right_wrist", 10},
        {"left_hip", 11},
        {"right_hip", 12},
        {"left_knee", 13},
        {"right_knee", 14},
        {"left_ankle", 15},
        {"right_ankle", 16}
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

        for (int frameIndex = 0; frameIndex < frameImages.Count; frameIndex++)
        {
            var frame = frameImages[frameIndex];
            framesSinceLastSwing++;

            // Run inference on cropped/resized image
            var keypoints = RunInferenceWithCrop(frame, cropRegion);

            var phase = DetermineSwingPhase(keypoints);
            if (phase == SwingPhase.Preparation)
            {
                // Not yet in swing, skip frame
                continue;
            }
            // Check if swing is complete
            if (IsSwingComplete(currentSwingFrames, phase))
            {
                var contactFrame = ContactFrameDetector
                    .DetectContactFrameAdvanced(
                        currentSwingFrames,
                        videoMetadata.Height,
                        videoMetadata.Width
                    );
                swings.Add(new SwingData
                {
                    Frames = [.. currentSwingFrames],
                    ContactFrameIndex = contactFrame
                });

                currentSwingFrames.Clear();
                framesSinceLastSwing = 0;
                cropRegion = CropRegion.InitCropRegion(videoMetadata.Height, videoMetadata.Width);
                continue;
            }
            // Check if this frame is during a swing
            if (!IsFrameDuringSwing(keypoints, currentSwingFrames, phase))
            {
                // prepping for next swing
                continue;
            }

            // Create frame data
            var frameData = new FrameData
            {
                SwingPoseFeatures = keypoints,
                SwingPhase = phase
            };

            currentSwingFrames.Add(frameData);

            // Update crop region for next frame (tracking)
            cropRegion = DetermineCropRegion(keypoints, videoMetadata.Height, videoMetadata.Width);
        }

        // Handle any remaining swing in progress
        if (currentSwingFrames.Count >= 15)
        {
            var contactFrame = ContactFrameDetector.DetectContactFrameAdvanced(currentSwingFrames, videoMetadata.Height, videoMetadata.Width);
            swings.Add(new SwingData
            {
                Frames = currentSwingFrames,
                ContactFrameIndex = contactFrame
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
        SwingPoseFeatures[] keypoints,
        List<FrameData> currentSwingFrames,
        SwingPhase swingPhase)
    {
        // Check if key tennis swing joints are visible with sufficient confidence
        var leftShoulder = keypoints[KeypointDict["left_shoulder"]];
        var rightShoulder = keypoints[KeypointDict["right_shoulder"]];
        var leftElbow = keypoints[KeypointDict["left_elbow"]];
        var rightElbow = keypoints[KeypointDict["right_elbow"]];
        var leftWrist = keypoints[KeypointDict["left_wrist"]];
        var rightWrist = keypoints[KeypointDict["right_wrist"]];

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
        SwingPoseFeatures[] keypoints)
    {
        // Get key points for swing analysis
        var leftShoulder = keypoints[KeypointDict["left_shoulder"]];
        var rightShoulder = keypoints[KeypointDict["right_shoulder"]];
        var leftWrist = keypoints[KeypointDict["left_wrist"]];
        var rightWrist = keypoints[KeypointDict["right_wrist"]];
        var leftHip = keypoints[KeypointDict["left_hip"]];
        var rightHip = keypoints[KeypointDict["right_hip"]];

        // Need both shoulders and at least one hip visible
        if (leftShoulder.Confidence < MinConfidence || rightShoulder.Confidence < MinConfidence ||
            (leftHip.Confidence < MinConfidence && rightHip.Confidence < MinConfidence))
        {
            return SwingPhase.Preparation;
        }

        // Determine hitting arm and get wrist position
        bool useRightArm = DetermineHittingArm(leftShoulder, rightShoulder, leftWrist, rightWrist,
                                              keypoints[KeypointDict["left_elbow"]], keypoints[KeypointDict["right_elbow"]], MinConfidence);

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

    private static bool DetermineHittingArm(SwingPoseFeatures leftShoulder, SwingPoseFeatures rightShoulder,
        SwingPoseFeatures leftWrist, SwingPoseFeatures rightWrist,
        SwingPoseFeatures leftElbow, SwingPoseFeatures rightElbow, float MinConfidence)
    {
        // Prefer right arm for tennis (most players are right-handed)
        if (rightShoulder.Confidence > MinConfidence && rightWrist.Confidence > MinConfidence &&
            rightElbow.Confidence > MinConfidence)
        {
            return true; // Use right arm
        }

        if (leftShoulder.Confidence > MinConfidence && leftWrist.Confidence > MinConfidence &&
            leftElbow.Confidence > MinConfidence)
        {
            return false; // Use left arm
        }

        // Fallback: use arm with better visibility
        return rightShoulder.Confidence + rightWrist.Confidence > leftShoulder.Confidence + leftWrist.Confidence;
    }

    private struct BodyPosition
    {
        public float ShoulderRotation;  // How much shoulders are rotated (positive = open to court)
        public float HipRotation;       // How much hips are rotated (positive = open to court)
        public float RacketPosition;    // Where racket is relative to body center (negative = back, positive = forward)
        public bool ShouldersSquared;   // Are shoulders roughly parallel to baseline
        public bool HipsOpen;           // Are hips opened to the court
        public bool IsCoiled;           // Is body in coiled backswing position
    }

    private static BodyPosition AnalyzeBodyPosition(SwingPoseFeatures leftShoulder, SwingPoseFeatures rightShoulder,
                                                   SwingPoseFeatures leftHip, SwingPoseFeatures rightHip,
                                                   SwingPoseFeatures hittingWrist, bool useRightArm,
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

    private SwingPoseFeatures[] RunInferenceWithCrop(byte[] imageBytes, CropRegion cropRegion)
    {
        var keypoints = _inferenceService.RunInference(imageBytes, cropRegion);

        // Update coordinates from crop region to original image coordinates
        for (int idx = 0; idx < MoveNetVideoProcessor.NumKeyPoints; idx++)
        {
            keypoints[idx].Y = cropRegion.YMin + (cropRegion.Height * keypoints[idx].Y);
            keypoints[idx].X = cropRegion.XMin + (cropRegion.Width * keypoints[idx].X);
        }

        return keypoints;
    }

    private static CropRegion DetermineCropRegion(SwingPoseFeatures[] keypoints, int imageHeight, int imageWidth)
    {
        // Convert to pixel coordinates
        var targetKeypoints = new Dictionary<string, Vector2>();

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
            float centerY = (targetKeypoints["left_hip"].Y + targetKeypoints["right_hip"].Y) / 2;
            float centerX = (targetKeypoints["left_hip"].X + targetKeypoints["right_hip"].X) / 2;

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

    private static bool IsTorsoVisible(SwingPoseFeatures[] keypoints)
    {
        return (keypoints[KeypointDict["left_hip"]].Confidence > MinCropKeypointScore ||
                keypoints[KeypointDict["right_hip"]].Confidence > MinCropKeypointScore) &&
               (keypoints[KeypointDict["left_shoulder"]].Confidence > MinCropKeypointScore ||
                keypoints[KeypointDict["right_shoulder"]].Confidence > MinCropKeypointScore);
    }

    private static (float, float, float, float) DetermineTorsoAndBodyRange(
        SwingPoseFeatures[] keypoints,
        Dictionary<string, Vector2> targetKeypoints,
        float centerY,
        float centerX)
    {
        string[] torsoJoints = ["left_shoulder", "right_shoulder", "left_hip", "right_hip"];
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