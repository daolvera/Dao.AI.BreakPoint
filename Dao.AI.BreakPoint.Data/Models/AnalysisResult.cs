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
    /// Quality score from the ML model (0-100)
    /// </summary>
    [Range(0, 100)]
    public double QualityScore { get; set; }

    /// <summary>
    /// JSON string containing feature importance from attention weights
    /// Maps feature names to importance scores
    /// </summary>
    public string FeatureImportanceJson { get; set; } = "{}";

    /// <summary>
    /// JSON string containing coaching tips from the LLM
    /// </summary>
    public string CoachingTipsJson { get; set; } = "[]";

    /// <summary>
    /// URL to the generated skeleton overlay image
    /// </summary>
    public string? SkeletonOverlayUrl { get; set; }

    /// <summary>
    /// URL to the original video in blob storage
    /// </summary>
    public string? VideoBlobUrl { get; set; }
}
