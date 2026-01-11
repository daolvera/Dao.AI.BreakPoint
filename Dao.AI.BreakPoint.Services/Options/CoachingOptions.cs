namespace Dao.AI.BreakPoint.Services.Options;

/// <summary>
/// Configuration options for the coaching and drill recommendation service
/// </summary>
public class CoachingOptions
{
    public const string SectionName = "Coaching";

    /// <summary>
    /// Maximum number of recent drills to include in recommendations context.
    /// These drills will be used to personalize future recommendations.
    /// </summary>
    public int RecentDrillsCount { get; set; } = 6;

    /// <summary>
    /// Maximum number of drills to generate per analysis
    /// </summary>
    public int MaxDrillsPerAnalysis { get; set; } = 3;

    /// <summary>
    /// Maximum length of the training history summary (in characters)
    /// </summary>
    public int MaxHistorySummaryLength { get; set; } = 2000;
}
