using System.ComponentModel.DataAnnotations;
using Dao.AI.BreakPoint.Data.Enums;

namespace Dao.AI.BreakPoint.Data.Models;

/// <summary>
/// Represents an in-progress swing analysis request.
/// Tracks the state of the analysis from upload through completion.
/// </summary>
public class AnalysisRequest : UpdatableModel
{
    public int PlayerId { get; set; }
    public Player? Player { get; set; }

    [ConcurrencyCheck]
    public AnalysisStatus Status { get; set; }

    /// <summary>
    /// The type of stroke being analyzed (forehand, backhand, serve, etc.)
    /// </summary>
    public SwingType StrokeType { get; set; }

    /// <summary>
    /// URL to the uploaded video in blob storage
    /// </summary>
    public string? VideoBlobUrl { get; set; }

    /// <summary>
    /// Error message if analysis failed
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// The completed result, only populated when Status is Completed
    /// </summary>
    public AnalysisResult? Result { get; set; }
}
