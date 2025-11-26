namespace Dao.AI.BreakPoint.Services.SwingAnalyzer;

public class SwingPrediction
{
    public float OverallScore { get; set; }
    public float ShoulderRotationScore { get; set; }
    public float ContactPointScore { get; set; }
    public float PreparationTimingScore { get; set; }
    public float BalanceScore { get; set; }
    public float FollowThroughScore { get; set; }
    public float[] IssueConfidences { get; set; } = [];
    public string[] DetectedIssues { get; set; } = [];

    public SwingPrediction(float[] output, string[] issueCategories)
    {
        OverallScore = output[0];
        ShoulderRotationScore = output[1];
        ContactPointScore = output[2];
        PreparationTimingScore = output[3];
        BalanceScore = output[4];
        FollowThroughScore = output[5];
        IssueConfidences = [.. output.Skip(6).Take(issueCategories.Length)];

        var threshold = 0.5f;
        DetectedIssues = [.. output.Skip(6)
                .Select((confidence, index) => new { confidence, index })
                .Where(x => x.confidence > threshold)
                .Select(x => issueCategories[x.index])];
    }
}
