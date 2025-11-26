namespace Dao.AI.BreakPoint.Services.SwingAnalyzer;

public class TechniqueIssues
{
    public float ShoulderRotationScore { get; set; }
    public float ContactPointScore { get; set; }
    public float PreparationTimingScore { get; set; }
    public float BalanceScore { get; set; }
    public float FollowThroughScore { get; set; }
    public string[] DetectedIssues { get; set; } = [];
}
