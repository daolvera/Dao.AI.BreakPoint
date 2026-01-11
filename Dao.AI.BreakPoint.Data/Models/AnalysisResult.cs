using System.ComponentModel.DataAnnotations;
using Dao.AI.BreakPoint.Data.Enums;

namespace Dao.AI.BreakPoint.Data.Models;

/// <summary>
/// Represents a completed swing analysis result.
/// Only created when analysis successfully completes.
/// </summary>
public class AnalysisResult : UpdatableModel
{
    /// <summary>
    /// The request that produced this result
    /// </summary>
    public int AnalysisRequestId { get; set; }
    public AnalysisRequest AnalysisRequest { get; set; } = null!;

    /// <summary>
    /// The player who owns this result (denormalized for easier querying)
    /// </summary>
    public int PlayerId { get; set; }
    public Player? Player { get; set; }

    /// <summary>
    /// The type of stroke analyzed
    /// </summary>
    public SwingType StrokeType { get; set; }

    /// <summary>
    /// Overall quality score from the ML model (0-100)
    /// Computed as weighted average of phase scores
    /// </summary>
    [Range(0, 100)]
    public double QualityScore { get; set; }

    /// <summary>
    /// Quality score for the Backswing phase (0-100)
    /// </summary>
    [Range(0, 100)]
    public int BackswingScore { get; set; }

    /// <summary>
    /// Quality score for the Contact phase (0-100)
    /// </summary>
    [Range(0, 100)]
    public int ContactScore { get; set; }

    /// <summary>
    /// Quality score for the Follow-Through phase (0-100)
    /// </summary>
    [Range(0, 100)]
    public int FollowThroughScore { get; set; }

    /// <summary>
    /// Phase-specific deviations from reference profiles
    /// </summary>
    public ICollection<PhaseDeviation> PhaseDeviations { get; set; } = [];

    /// <summary>
    /// JSON string containing coaching tips from the LLM
    /// </summary>
    public string CoachingTipsJson { get; set; } = "[]";

    /// <summary>
    /// URL to the generated skeleton overlay image (PNG)
    /// Shows worst frame with annotated skeleton
    /// </summary>
    public string? SkeletonOverlayUrl { get; set; }

    /// <summary>
    /// URL to the generated skeleton overlay animation (GIF)
    /// Shows the full swing with skeleton overlay and problem joint highlighting
    /// </summary>
    public string? SkeletonOverlayGifUrl { get; set; }

    /// <summary>
    /// URL to the original video in blob storage
    /// </summary>
    public string? VideoBlobUrl { get; set; }

    /// <summary>
    /// Drill recommendations generated for this analysis
    /// </summary>
    public ICollection<DrillRecommendation> DrillRecommendations { get; set; } = [];
}
