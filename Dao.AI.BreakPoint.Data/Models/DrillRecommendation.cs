using Dao.AI.BreakPoint.Data.Enums;

namespace Dao.AI.BreakPoint.Data.Models;

/// <summary>
/// Represents a drill recommendation generated for a player based on analysis results.
/// Tracks completion status and player feedback for improving future recommendations.
/// </summary>
public class DrillRecommendation : UpdatableModel
{
    /// <summary>
    /// The analysis result that triggered this recommendation
    /// </summary>
    public int AnalysisResultId { get; set; }
    public AnalysisResult AnalysisResult { get; set; } = null!;

    /// <summary>
    /// The player this drill is recommended for
    /// </summary>
    public int PlayerId { get; set; }
    public Player? Player { get; set; }

    /// <summary>
    /// The swing phase this drill targets
    /// </summary>
    public SwingPhase TargetPhase { get; set; }

    /// <summary>
    /// The specific feature/aspect this drill aims to improve
    /// (e.g., "racket head speed", "shoulder rotation")
    /// </summary>
    public string TargetFeature { get; set; } = "";

    /// <summary>
    /// Name of the drill (e.g., "Shadow Swing Focus")
    /// </summary>
    public string DrillName { get; set; } = "";

    /// <summary>
    /// Detailed description of how to perform the drill
    /// </summary>
    public string Description { get; set; } = "";

    /// <summary>
    /// Suggested number of repetitions or duration
    /// </summary>
    public string? SuggestedDuration { get; set; }

    /// <summary>
    /// Priority/importance of this drill (1 = highest priority)
    /// </summary>
    public int Priority { get; set; } = 1;

    /// <summary>
    /// When the player marked this drill as completed
    /// </summary>
    public DateTime? CompletedAt { get; set; }

    /// <summary>
    /// Player's thumbs up/down feedback (true = helpful, false = not helpful, null = no feedback)
    /// </summary>
    public bool? ThumbsUp { get; set; }

    /// <summary>
    /// Optional text feedback from the player about the drill
    /// </summary>
    public string? FeedbackText { get; set; }

    /// <summary>
    /// Whether this drill is still active/relevant
    /// </summary>
    public bool IsActive { get; set; } = true;
}
