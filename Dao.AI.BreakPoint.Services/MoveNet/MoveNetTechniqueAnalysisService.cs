using Dao.AI.BreakPoint.Services.SwingAnalyzer;
using System.Numerics;

namespace Dao.AI.BreakPoint.Services.MoveNet;

public static class MoveNetTechniqueAnalysisService
{
    public static TechniqueIssues AnalyzeSwing(List<FrameData> frames, int contactFrame)
    {
        var issues = new List<string>();

        var shoulderScore = AnalyzeShoulderRotation(frames, contactFrame, issues);
        var contactScore = AnalyzeContactPoint(frames, contactFrame, issues);
        var prepScore = AnalyzePreparationTiming(frames, contactFrame, issues);
        var balanceScore = AnalyzeBalance(frames, issues);
        var followScore = AnalyzeFollowThrough(frames, contactFrame, issues);

        return new TechniqueIssues
        {
            ShoulderRotationScore = shoulderScore,
            ContactPointScore = contactScore,
            PreparationTimingScore = prepScore,
            BalanceScore = balanceScore,
            FollowThroughScore = followScore,
            DetectedIssues = issues.ToArray(),
        };
    }

    private static float AnalyzeShoulderRotation(
        List<FrameData> frames,
        int contactFrame,
        List<string> issues
    )
    {
        if (contactFrame >= frames.Count)
            return 0.5f;

        var contactFrameData = frames[contactFrame];
        var leftShoulder = contactFrameData.SwingPoseFeatures[5];
        var rightShoulder = contactFrameData.SwingPoseFeatures[6];

        var shoulderAngle = Math.Atan2(
            rightShoulder.Y - leftShoulder.Y,
            rightShoulder.X - leftShoulder.X
        );

        var normalizedAngle = Math.Abs(shoulderAngle);

        if (normalizedAngle < 0.2)
        {
            issues.Add("Insufficient shoulder rotation");
            return 0.3f;
        }
        if (normalizedAngle > 1.2)
        {
            issues.Add("Excessive shoulder rotation");
            return 0.4f;
        }

        return Math.Min(1.0f, (float)normalizedAngle / 0.8f);
    }

    private static float AnalyzeContactPoint(
        List<FrameData> frames,
        int contactFrame,
        List<string> issues
    )
    {
        if (contactFrame >= frames.Count)
            return 0.5f;

        var contactFrameData = frames[contactFrame];
        var rightWrist = contactFrameData.SwingPoseFeatures[10];
        var rightShoulder = contactFrameData.SwingPoseFeatures[6];
        var rightHip = contactFrameData.SwingPoseFeatures[12];

        var wristToHip = new Vector2(rightWrist.X - rightHip.X, rightWrist.Y - rightHip.Y);
        var shoulderToHip = new Vector2(rightShoulder.X - rightHip.X, rightShoulder.Y - rightHip.Y);

        if (wristToHip.X < shoulderToHip.X - 0.1f)
        {
            issues.Add("Contact point too far back");
            return 0.2f;
        }
        if (wristToHip.X > shoulderToHip.X + 0.3f)
        {
            issues.Add("Contact point too far forward");
            return 0.4f;
        }

        return 0.8f;
    }

    private static float AnalyzePreparationTiming(
        List<FrameData> frames,
        int contactFrame,
        List<string> issues
    )
    {
        var prepFrames = Math.Max(0, contactFrame - 15);
        var prepTime = contactFrame - prepFrames;

        if (prepTime < 8)
        {
            issues.Add("Rushed preparation");
            return 0.3f;
        }
        if (prepTime > 25)
        {
            issues.Add("Late preparation");
            return 0.4f;
        }

        return 0.8f;
    }

    private static float AnalyzeBalance(List<FrameData> frames, List<string> issues)
    {
        var stanceWidths = new List<float>();

        foreach (var frame in frames)
        {
            var leftAnkle = frame.SwingPoseFeatures[15];
            var rightAnkle = frame.SwingPoseFeatures[16];
            var width = Math.Abs(leftAnkle.X - rightAnkle.X);
            stanceWidths.Add(width);
        }

        var avgWidth = stanceWidths.Average();

        if (avgWidth < 0.15f)
        {
            issues.Add("Narrow stance");
            return 0.4f;
        }
        if (avgWidth > 0.8f)
        {
            issues.Add("Wide stance");
            return 0.5f;
        }

        return 0.7f;
    }

    private static float AnalyzeFollowThrough(
        List<FrameData> frames,
        int contactFrame,
        List<string> issues
    )
    {
        if (contactFrame + 10 >= frames.Count)
            return 0.5f;

        var contactFrame_data = frames[contactFrame];
        var followFrame_data = frames[contactFrame + 10];

        var contactWrist = contactFrame_data.SwingPoseFeatures[10];
        var followWrist = followFrame_data.SwingPoseFeatures[10];

        var extension = followWrist.Y - contactWrist.Y;

        if (extension < 0.1f)
        {
            issues.Add("Poor follow through extension");
            return 0.3f;
        }

        return 0.8f;
    }
}
