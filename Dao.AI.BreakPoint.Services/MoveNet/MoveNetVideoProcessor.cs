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
    private readonly MoveNetInferenceService _inferenceService = new(modelPath);
    private const float MinCropKeypointScore = 0.2f;

    public ProcessedSwingVideo ProcessVideoFrames(
        List<byte[]> frameImages,
        VideoMetadata videoMetadata)
    {
        List<SwingData> swings = [];
        var cropRegion = CropRegion.InitCropRegion(videoMetadata.Height, videoMetadata.Width);

        List<FrameData> currentSwingFrames = [];
        bool lookForPrep = true;
        int framesSinceLastSwing = 0;

        foreach (var frame in frameImages)
        {
            framesSinceLastSwing++;

            // Run inference on cropped/resized image
            var keypoints = RunInferenceWithCrop(frame, cropRegion);

            // Check if this frame is during a swing
            if (!IsFrameDuringSwing(keypoints, currentSwingFrames, lookForPrep))
            {
                // If we were tracking a swing and now we're not, check if swing is complete
                if (currentSwingFrames.Count > 0 && framesSinceLastSwing > 10)
                {
                    // Force completion of incomplete swing if we've lost tracking
                    if (currentSwingFrames.Count >= 10) // Only save if we have minimum frames
                    {
                        var contactFrame = ContactFrameDetector.DetectContactFrameAdvanced(currentSwingFrames, videoMetadata.Height, videoMetadata.Width);
                        swings.Add(new SwingData
                        {
                            Frames = [.. currentSwingFrames],
                            ContactFrameIndex = contactFrame
                        });
                    }

                    currentSwingFrames.Clear();
                    lookForPrep = true;
                    cropRegion = CropRegion.InitCropRegion(videoMetadata.Height, videoMetadata.Width);
                }
                continue;
            }

            var phase = DetermineSwingPhase(keypoints);

            // Reset prep flag when we leave preparation phase
            if (phase != SwingPhase.Preparation)
            {
                lookForPrep = false;
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

            // Check if swing is complete
            if (IsSwingComplete(currentSwingFrames))
            {
                var contactFrame = ContactFrameDetector.DetectContactFrameAdvanced(currentSwingFrames, videoMetadata.Height, videoMetadata.Width);
                swings.Add(new SwingData
                {
                    Frames = [.. currentSwingFrames],
                    ContactFrameIndex = contactFrame
                });

                currentSwingFrames.Clear();
                lookForPrep = true;
                framesSinceLastSwing = 0;
                cropRegion = CropRegion.InitCropRegion(videoMetadata.Height, videoMetadata.Width);
            }
        }

        // Handle any remaining swing in progress
        if (currentSwingFrames.Count >= 10) // Only save if we have minimum frames
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

    private static bool IsFrameDuringSwing(SwingPoseFeatures[] keypoints, List<FrameData> currentSwingFrames, bool lookForPrep)
    {
        // Check if key tennis swing joints are visible with sufficient confidence
        var leftShoulder = keypoints[KeypointDict["left_shoulder"]];
        var rightShoulder = keypoints[KeypointDict["right_shoulder"]];
        var leftElbow = keypoints[KeypointDict["left_elbow"]];
        var rightElbow = keypoints[KeypointDict["right_elbow"]];
        var leftWrist = keypoints[KeypointDict["left_wrist"]];
        var rightWrist = keypoints[KeypointDict["right_wrist"]];
        var leftHip = keypoints[KeypointDict["left_hip"]];
        var rightHip = keypoints[KeypointDict["right_hip"]];

        // Minimum confidence threshold for swing detection
        const float minConfidence = 0.3f;

        // Check if upper body is visible (essential for swing detection)
        bool upperBodyVisible = (leftShoulder.Confidence > minConfidence || rightShoulder.Confidence > minConfidence) &&
                               (leftElbow.Confidence > minConfidence || rightElbow.Confidence > minConfidence) &&
                               (leftWrist.Confidence > minConfidence || rightWrist.Confidence > minConfidence);

        if (!upperBodyVisible)
            return false;

        // Check if person is in athletic stance (hips visible)
        bool athleticStance = leftHip.Confidence > minConfidence || rightHip.Confidence > minConfidence;

        if (!athleticStance)
            return false;

        // If we're looking for preparation and already tracking a swing, be more selective
        if (lookForPrep && currentSwingFrames.Count == 0)
        {
            // Only start new swing if we detect preparation-like pose
            var phase = DetermineSwingPhase(keypoints);
            return phase == SwingPhase.Preparation;
        }

        // If we're already tracking a swing, continue as long as upper body is visible
        if (currentSwingFrames.Count > 0)
        {
            return upperBodyVisible && athleticStance;
        }

        // Calculate arm activity to detect motion
        float armActivity = 0f;
        int validArms = 0;

        // Check left arm extension
        if (leftShoulder.Confidence > minConfidence && leftWrist.Confidence > minConfidence)
        {
            float dx = leftShoulder.X - leftWrist.X;
            float dy = leftShoulder.Y - leftWrist.Y;
            armActivity += (float)Math.Sqrt((dx * dx) + (dy * dy));
            validArms++;
        }

        // Check right arm extension
        if (rightShoulder.Confidence > minConfidence && rightWrist.Confidence > minConfidence)
        {
            float dx = rightShoulder.X - rightWrist.X;
            float dy = rightShoulder.Y - rightWrist.Y;
            armActivity += (float)Math.Sqrt((dx * dx) + (dy * dy));
            validArms++;
        }

        if (validArms > 0)
        {
            armActivity /= validArms;
            // If arms are reasonably extended (typical during tennis swing), consider it a swing frame
            // Normalized coordinates: 0.12 is roughly 12% of image width/height
            bool armsActive = armActivity > 0.12f;
            return upperBodyVisible && athleticStance && armsActive;
        }

        // Fallback: if we have good upper body visibility and athletic stance, assume potential swing
        return upperBodyVisible && athleticStance;
    }

    private static SwingPhase DetermineSwingPhase(SwingPoseFeatures[] keypoints)
    {
        // Get key points for swing analysis
        var leftShoulder = keypoints[KeypointDict["left_shoulder"]];
        var rightShoulder = keypoints[KeypointDict["right_shoulder"]];
        var leftElbow = keypoints[KeypointDict["left_elbow"]];
        var rightElbow = keypoints[KeypointDict["right_elbow"]];
        var leftWrist = keypoints[KeypointDict["left_wrist"]];
        var rightWrist = keypoints[KeypointDict["right_wrist"]];
        var leftHip = keypoints[KeypointDict["left_hip"]];
        var rightHip = keypoints[KeypointDict["right_hip"]];

        const float minConfidence = 0.3f;

        // Determine which arm is the hitting arm (higher confidence or more extended)
        bool useLeftArm = false;
        if (leftShoulder.Confidence > minConfidence && leftWrist.Confidence > minConfidence &&
            rightShoulder.Confidence > minConfidence && rightWrist.Confidence > minConfidence)
        {
            // Use the arm with higher average confidence
            float leftArmConfidence = (leftShoulder.Confidence + leftElbow.Confidence + leftWrist.Confidence) / 3;
            float rightArmConfidence = (rightShoulder.Confidence + rightElbow.Confidence + rightWrist.Confidence) / 3;
            useLeftArm = leftArmConfidence > rightArmConfidence;
        }
        else
        {
            useLeftArm = leftShoulder.Confidence > rightShoulder.Confidence;
        }

        var shoulder = useLeftArm ? leftShoulder : rightShoulder;
        var elbow = useLeftArm ? leftElbow : rightElbow;
        var wrist = useLeftArm ? leftWrist : rightWrist;
        var hip = useLeftArm ? leftHip : rightHip;

        if (shoulder.Confidence < minConfidence || wrist.Confidence < minConfidence)
        {
            // Default to preparation if we can't determine arm position
            return SwingPhase.Preparation;
        }

        // Calculate arm angle relative to body
        // Vector from shoulder to wrist
        float armVectorX = wrist.X - shoulder.X;
        float armVectorY = wrist.Y - shoulder.Y;

        // Calculate body center for reference
        float bodyCenterX = (leftShoulder.X + rightShoulder.X) / 2;
        float bodyCenterY = (leftShoulder.Y + rightShoulder.Y) / 2;

        // Determine swing phase based on arm position relative to body
        if (useLeftArm)
        {
            // For left arm swing (left-handed player or backhand)
            if (wrist.X > bodyCenterX + 0.1f) // Wrist is significantly to the right of body center
            {
                return SwingPhase.FollowThrough;
            }
            else if (wrist.X < shoulder.X - 0.05f) // Wrist is behind shoulder (backswing)
            {
                return SwingPhase.Backswing;
            }
            else
            {
                return SwingPhase.Preparation;
            }
        }
        else
        {
            // For right arm swing (right-handed player or forehand)
            if (wrist.X < bodyCenterX - 0.1f) // Wrist is significantly to the left of body center
            {
                return SwingPhase.FollowThrough;
            }
            else if (wrist.X > shoulder.X + 0.05f) // Wrist is behind shoulder (backswing)
            {
                return SwingPhase.Backswing;
            }
            else
            {
                return SwingPhase.Preparation;
            }
        }
    }

    private static bool IsSwingComplete(List<FrameData> currentSwingFrames)
    {
        if (currentSwingFrames.Count < 10) // Minimum frames for a valid swing
        {
            return false;
        }

        // Check if we have all required phases in sequence
        bool hasPreparation = currentSwingFrames.Any(f => f.SwingPhase == SwingPhase.Preparation);
        bool hasBackswing = currentSwingFrames.Any(f => f.SwingPhase == SwingPhase.Backswing);
        bool hasFollowThrough = currentSwingFrames.Any(f => f.SwingPhase == SwingPhase.FollowThrough);

        // Must have at least preparation and one other phase
        if (!hasPreparation || (!hasBackswing && !hasFollowThrough))
        {
            // If swing has gone on for a while without proper phases, force completion
            if (currentSwingFrames.Count > 60) // 2 seconds at 30 FPS
            {
                return true;
            }
            return false;
        }

        const int minFollowThroughFrames = 5;
        int consecutiveFollowThroughFrames = 0;

        // Count consecutive follow-through frames from the end
        for (int i = currentSwingFrames.Count - 1; i >= 0; i--)
        {
            if (currentSwingFrames[i].SwingPhase == SwingPhase.FollowThrough)
            {
                consecutiveFollowThroughFrames++;
            }
            else
            {
                break;
            }
        }

        // Check for stable follow-through
        bool hasStableFollowThrough = consecutiveFollowThroughFrames >= minFollowThroughFrames;

        // Check for return to preparation (indicates swing reset)
        bool hasReturnToPrep = false;
        if (currentSwingFrames.Count > 20) // Only check after sufficient frames
        {
            // Look for preparation phase in the last 10 frames after having other phases
            var lastTenFrames = currentSwingFrames.TakeLast(10);
            var hasRecentPrep = lastTenFrames.Any(f => f.SwingPhase == SwingPhase.Preparation);
            var hasEarlierNonPrep = currentSwingFrames.Take(currentSwingFrames.Count - 10)
                                                     .Any(f => f.SwingPhase != SwingPhase.Preparation);
            hasReturnToPrep = hasRecentPrep && hasEarlierNonPrep;
        }

        // Maximum swing duration check (prevent infinite swings)
        const int maxSwingFrames = 120; // ~4 seconds at 30 FPS
        bool exceedsMaxDuration = currentSwingFrames.Count > maxSwingFrames;

        // Swing completion conditions
        return hasStableFollowThrough || hasReturnToPrep || exceedsMaxDuration;
    }

    private SwingPoseFeatures[] RunInferenceWithCrop(byte[] imageBytes, CropRegion cropRegion)
    {
        var keypoints = _inferenceService.RunInference(imageBytes, cropRegion);

        // Update coordinates from crop region to original image coordinates
        for (int idx = 0; idx < 17; idx++)
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