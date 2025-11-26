using Dao.AI.BreakPoint.Services.SwingAnalyzer;
using System.Numerics;

namespace Dao.AI.BreakPoint.ModelTraining.VideoAnalysis;

public static class ContactFrameDetector
{
    /// <summary>
    /// Detect contact frame based on wrist velocity analysis
    /// </summary>
    public static int DetectContactFrameByWristVelocity(List<FrameData> frames, int imageHeight = 480, int imageWidth = 640)
    {
        if (frames.Count < 3)
        {
            return frames.Count / 2; // Return middle frame if insufficient data
        }

        var rightWristVelocities = new List<float>();
        var leftWristVelocities = new List<float>();

        // Calculate velocities for both wrists
        for (int i = 1; i < frames.Count; i++)
        {
            var prevFrame = frames[i - 1];
            var currentFrame = frames[i];

            // Right wrist (index 10)
            var rightWristPrev = prevFrame.SwingPoseFeatures[10].ToPixelCoordinates(imageHeight, imageWidth);
            var rightWristCurrent = currentFrame.SwingPoseFeatures[10].ToPixelCoordinates(imageHeight, imageWidth);
            var rightWristVelocity = Vector2.Distance(rightWristCurrent, rightWristPrev);
            rightWristVelocities.Add(rightWristVelocity);

            // Left wrist (index 9)
            var leftWristPrev = prevFrame.SwingPoseFeatures[9].ToPixelCoordinates(imageHeight, imageWidth);
            var leftWristCurrent = currentFrame.SwingPoseFeatures[9].ToPixelCoordinates(imageHeight, imageWidth);
            var leftWristVelocity = Vector2.Distance(leftWristCurrent, leftWristPrev);
            leftWristVelocities.Add(leftWristVelocity);
        }

        // Find the frame with peak velocity (likely around contact)
        var maxRightVelocityIndex = rightWristVelocities.IndexOf(rightWristVelocities.Max());
        var maxLeftVelocityIndex = leftWristVelocities.IndexOf(leftWristVelocities.Max());

        // Use the dominant hand (higher peak velocity)
        var contactFrame = rightWristVelocities[maxRightVelocityIndex] > leftWristVelocities[maxLeftVelocityIndex]
            ? maxRightVelocityIndex + 1 // +1 because velocity array is offset by 1
            : maxLeftVelocityIndex + 1;

        return Math.Max(0, Math.Min(contactFrame, frames.Count - 1));
    }

    /// <summary>
    /// Detect contact frame based on racket trajectory analysis
    /// Assumes right-handed player with racket extension beyond wrist
    /// </summary>
    public static int DetectContactFrameByRacketTrajectory(List<FrameData> frames, int imageHeight = 480, int imageWidth = 640)
    {
        if (frames.Count < 5)
        {
            return frames.Count / 2;
        }

        var accelerations = new List<float>();

        // Calculate acceleration of right wrist (proxy for racket)
        for (int i = 2; i < frames.Count; i++)
        {
            var prevPrevFrame = frames[i - 2];
            var prevFrame = frames[i - 1];
            var currentFrame = frames[i];

            var wristPrevPrev = prevPrevFrame.SwingPoseFeatures[10].ToPixelCoordinates(imageHeight, imageWidth);
            var wristPrev = prevFrame.SwingPoseFeatures[10].ToPixelCoordinates(imageHeight, imageWidth);
            var wristCurrent = currentFrame.SwingPoseFeatures[10].ToPixelCoordinates(imageHeight, imageWidth);

            // Calculate velocity for two consecutive intervals
            var velocity1 = Vector2.Distance(wristPrev, wristPrevPrev);
            var velocity2 = Vector2.Distance(wristCurrent, wristPrev);

            // Calculate acceleration (change in velocity)
            var acceleration = Math.Abs(velocity2 - velocity1);
            accelerations.Add(acceleration);
        }

        // Find frame with maximum acceleration change (likely contact point)
        var maxAccelerationIndex = accelerations.IndexOf(accelerations.Max());
        var contactFrame = maxAccelerationIndex + 2; // +2 because acceleration array is offset by 2

        return Math.Max(0, Math.Min(contactFrame, frames.Count - 1));
    }

    /// <summary>
    /// Detect contact frame using combined velocity and direction analysis
    /// </summary>
    public static int DetectContactFrameAdvanced(List<FrameData> frames, int imageHeight = 480, int imageWidth = 640)
    {
        if (frames.Count < 5)
        {
            return frames.Count / 2;
        }

        var scores = new List<float>();

        for (int i = 2; i < frames.Count - 2; i++)
        {
            var score = CalculateContactProbabilityScore(frames, i, imageHeight, imageWidth);
            scores.Add(score);
        }

        if (scores.Count == 0)
        {
            return frames.Count / 2;
        }

        var maxScoreIndex = scores.IndexOf(scores.Max());
        var contactFrame = maxScoreIndex + 2; // +2 because we started from index 2

        return Math.Max(0, Math.Min(contactFrame, frames.Count - 1));
    }

    private static float CalculateContactProbabilityScore(List<FrameData> frames, int frameIndex, int imageHeight, int imageWidth)
    {
        var score = 0.0f;

        // Get key joint positions
        var rightWrist = frames[frameIndex].SwingPoseFeatures[10].ToPixelCoordinates(imageHeight, imageWidth);
        var rightElbow = frames[frameIndex].SwingPoseFeatures[8].ToPixelCoordinates(imageHeight, imageWidth);
        var rightShoulder = frames[frameIndex].SwingPoseFeatures[6].ToPixelCoordinates(imageHeight, imageWidth);

        // 1. Velocity component (higher velocity near contact)
        if (frameIndex > 0)
        {
            var prevRightWrist = frames[frameIndex - 1].SwingPoseFeatures[10].ToPixelCoordinates(imageHeight, imageWidth);
            var velocity = Vector2.Distance(rightWrist, prevRightWrist);
            score += velocity * 0.4f; // 40% weight for velocity
        }

        // 2. Arm extension component (arm should be extending at contact)
        var elbowToWrist = Vector2.Distance(rightElbow, rightWrist);
        var shoulderToElbow = Vector2.Distance(rightShoulder, rightElbow);
        var armExtensionRatio = elbowToWrist / Math.Max(shoulderToElbow, 1.0f);
        score += armExtensionRatio * 30.0f; // 30% weight for arm extension

        // 3. Height component (contact point should be at appropriate height)
        var shoulderHeight = rightShoulder.Y;
        var wristHeight = rightWrist.Y;
        var relativeHeight = Math.Abs(wristHeight - shoulderHeight) / imageHeight; // Use actual image height
        score += (1.0f - relativeHeight) * 20.0f; // 20% weight, prefer wrist near shoulder height

        // 4. Forward motion component (wrist should be moving forward)
        if (frameIndex > 0 && frameIndex < frames.Count - 1)
        {
            var prevWrist = frames[frameIndex - 1].SwingPoseFeatures[10].ToPixelCoordinates(imageHeight, imageWidth);
            var nextWrist = frames[frameIndex + 1].SwingPoseFeatures[10].ToPixelCoordinates(imageHeight, imageWidth);
            var forwardMotion = nextWrist.X - prevWrist.X; // Positive if moving right (forward for right-handed)
            score += Math.Max(0, forwardMotion) * 0.1f; // 10% weight for forward motion
        }

        return score;
    }

    /// <summary>
    /// Simple contact frame detection based on frame position (fallback method)
    /// </summary>
    public static int DetectContactFrameByPosition(List<FrameData> frames, float contactRatio = 0.6f)
    {
        // Assume contact happens at approximately 60% through the swing sequence
        var contactFrame = (int)(frames.Count * contactRatio);
        return Math.Max(0, Math.Min(contactFrame, frames.Count - 1));
    }

    /// <summary>
    /// Detect contact frame using multiple methods and return the most confident result
    /// </summary>
    public static ContactFrameResult DetectContactFrameMultiMethod(List<FrameData> frames, int imageHeight = 480, int imageWidth = 640)
    {
        var velocityMethod = DetectContactFrameByWristVelocity(frames, imageHeight, imageWidth);
        var trajectoryMethod = DetectContactFrameByRacketTrajectory(frames, imageHeight, imageWidth);
        var advancedMethod = DetectContactFrameAdvanced(frames, imageHeight, imageWidth);
        var positionMethod = DetectContactFrameByPosition(frames);

        // Calculate consensus
        var allResults = new[] { velocityMethod, trajectoryMethod, advancedMethod };
        var avgResult = (int)allResults.Average();
        var stdDev = Math.Sqrt(allResults.Select(x => Math.Pow(x - avgResult, 2)).Average());

        // Use advanced method if results are consistent, otherwise fall back to position-based
        var finalResult = stdDev < 5.0 ? advancedMethod : positionMethod;

        return new ContactFrameResult
        {
            ContactFrame = finalResult,
            Confidence = stdDev < 3.0 ? 0.9f : (stdDev < 5.0 ? 0.7f : 0.4f),
            VelocityMethod = velocityMethod,
            TrajectoryMethod = trajectoryMethod,
            AdvancedMethod = advancedMethod,
            PositionMethod = positionMethod
        };
    }
}

public class ContactFrameResult
{
    public int ContactFrame { get; set; }
    public float Confidence { get; set; }
    public int VelocityMethod { get; set; }
    public int TrajectoryMethod { get; set; }
    public int AdvancedMethod { get; set; }
    public int PositionMethod { get; set; }
}