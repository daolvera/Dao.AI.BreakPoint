using System.Text.Json;
using Dao.AI.BreakPoint.Data.Enums;
using Dao.AI.BreakPoint.Data.Models;

namespace Dao.AI.BreakPoint.Services.DTOs;

/// <summary>
/// DTO for an in-progress analysis request
/// </summary>
public class AnalysisRequestDto
{
    public int Id { get; set; }
    public int PlayerId { get; set; }
    public AnalysisStatus Status { get; set; }
    public SwingType StrokeType { get; set; }
    public string? VideoBlobUrl { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    /// <summary>
    /// The result ID if analysis is completed
    /// </summary>
    public int? ResultId { get; set; }

    public static AnalysisRequestDto FromModel(AnalysisRequest model)
    {
        return new AnalysisRequestDto
        {
            Id = model.Id,
            PlayerId = model.PlayerId,
            Status = model.Status,
            StrokeType = model.StrokeType,
            VideoBlobUrl = model.VideoBlobUrl,
            ErrorMessage = model.ErrorMessage,
            CreatedAt = model.CreatedAt,
            UpdatedAt = model.UpdatedAt,
            ResultId = model.Result?.Id,
        };
    }
}

/// <summary>
/// DTO for a completed analysis result (historical record)
/// </summary>
public class AnalysisResultDto
{
    public int Id { get; set; }
    public int AnalysisRequestId { get; set; }
    public int PlayerId { get; set; }
    public SwingType StrokeType { get; set; }
    public double QualityScore { get; set; }

    /// <summary>
    /// Phase-specific quality scores (0-100)
    /// </summary>
    public PhaseScoresDto PhaseScores { get; set; } = new();

    /// <summary>
    /// Phase-specific deviations from reference profiles
    /// </summary>
    public List<PhaseDeviationDto> PhaseDeviations { get; set; } = [];

    /// <summary>
    /// Drill recommendations for this analysis
    /// </summary>
    public List<DrillRecommendationDto> DrillRecommendations { get; set; } = [];

    public string? SkeletonOverlayUrl { get; set; }
    public string? SkeletonOverlayGifUrl { get; set; }
    public string? VideoBlobUrl { get; set; }
    public DateTime CreatedAt { get; set; }

    public static AnalysisResultDto FromModel(AnalysisResult model)
    {
        return new AnalysisResultDto
        {
            Id = model.Id,
            AnalysisRequestId = model.AnalysisRequestId,
            PlayerId = model.PlayerId,
            StrokeType = model.StrokeType,
            QualityScore = model.QualityScore,
            PhaseScores = new PhaseScoresDto
            {
                Backswing = model.BackswingScore,
                Contact = model.ContactScore,
                FollowThrough = model.FollowThroughScore,
            },
            PhaseDeviations = [.. model.PhaseDeviations.Select(PhaseDeviationDto.FromModel)],
            DrillRecommendations =
            [
                .. model
                    .DrillRecommendations.Where(d => d.IsActive)
                    .Select(DrillRecommendationDto.FromModel),
            ],
            SkeletonOverlayUrl = model.SkeletonOverlayUrl,
            SkeletonOverlayGifUrl = model.SkeletonOverlayGifUrl,
            VideoBlobUrl = model.VideoBlobUrl,
            CreatedAt = model.CreatedAt,
        };
    }
}

/// <summary>
/// Phase-specific quality scores
/// </summary>
public class PhaseScoresDto
{
    public int Backswing { get; set; }
    public int Contact { get; set; }
    public int FollowThrough { get; set; }
}

/// <summary>
/// DTO for phase deviation data
/// </summary>
public class PhaseDeviationDto
{
    public SwingPhase Phase { get; set; }
    public List<FeatureDeviationDto> FeatureDeviations { get; set; } = [];

    public static PhaseDeviationDto FromModel(PhaseDeviation model)
    {
        return new PhaseDeviationDto
        {
            Phase = model.Phase,
            FeatureDeviations = [.. model.FeatureDeviations.Select(FeatureDeviationDto.FromModel)],
        };
    }
}

/// <summary>
/// DTO for individual feature deviation
/// </summary>
public class FeatureDeviationDto
{
    public int FeatureIndex { get; set; }
    public string FeatureName { get; set; } = "";
    public double ZScore { get; set; }
    public double ActualValue { get; set; }
    public double ReferenceMean { get; set; }
    public double ReferenceStd { get; set; }

    /// <summary>
    /// Human-readable severity description
    /// </summary>
    public string Severity =>
        Math.Abs(ZScore) switch
        {
            >= 2.0 => "significant",
            >= 1.5 => "moderate",
            >= 1.0 => "slight",
            _ => "normal",
        };

    /// <summary>
    /// Direction of deviation (above/below reference)
    /// </summary>
    public string Direction => ZScore > 0 ? "above" : "below";

    public static FeatureDeviationDto FromModel(FeatureDeviation model)
    {
        return new FeatureDeviationDto
        {
            FeatureIndex = model.FeatureIndex,
            FeatureName = model.FeatureName,
            ZScore = model.ZScore,
            ActualValue = model.ActualValue,
            ReferenceMean = model.ReferenceMean,
            ReferenceStd = model.ReferenceStd,
        };
    }
}

/// <summary>
/// DTO for drill recommendation
/// </summary>
public class DrillRecommendationDto
{
    public int Id { get; set; }
    public int AnalysisResultId { get; set; }
    public int PlayerId { get; set; }
    public SwingPhase TargetPhase { get; set; }
    public string TargetFeature { get; set; } = "";
    public string DrillName { get; set; } = "";
    public string Description { get; set; } = "";
    public string? SuggestedDuration { get; set; }
    public int Priority { get; set; }
    public DateTime? CompletedAt { get; set; }
    public bool? ThumbsUp { get; set; }
    public string? FeedbackText { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }

    public static DrillRecommendationDto FromModel(DrillRecommendation model)
    {
        return new DrillRecommendationDto
        {
            Id = model.Id,
            AnalysisResultId = model.AnalysisResultId,
            PlayerId = model.PlayerId,
            TargetPhase = model.TargetPhase,
            TargetFeature = model.TargetFeature,
            DrillName = model.DrillName,
            Description = model.Description,
            SuggestedDuration = model.SuggestedDuration,
            Priority = model.Priority,
            CompletedAt = model.CompletedAt,
            ThumbsUp = model.ThumbsUp,
            FeedbackText = model.FeedbackText,
            IsActive = model.IsActive,
            CreatedAt = model.CreatedAt,
        };
    }
}

/// <summary>
/// Request to provide feedback on a drill
/// </summary>
public class DrillFeedbackRequest
{
    public bool ThumbsUp { get; set; }
    public string? FeedbackText { get; set; }
}

/// <summary>
/// Request to create a new analysis (video upload)
/// </summary>
public class CreateAnalysisRequest : IBaseDto<AnalysisRequest>
{
    public int PlayerId { get; set; }
    public SwingType StrokeType { get; set; }

    public AnalysisRequest ToModel() =>
        new()
        {
            PlayerId = PlayerId,
            StrokeType = StrokeType,
            Status = AnalysisStatus.Requested,
        };
}

/// <summary>
/// Summary view of analysis result for lists/dashboard
/// </summary>
public class AnalysisResultSummaryDto
{
    public int Id { get; set; }
    public int AnalysisRequestId { get; set; }
    public SwingType StrokeType { get; set; }
    public double QualityScore { get; set; }
    public PhaseScoresDto PhaseScores { get; set; } = new();
    public DateTime CreatedAt { get; set; }

    public static AnalysisResultSummaryDto FromModel(AnalysisResult model)
    {
        return new AnalysisResultSummaryDto
        {
            Id = model.Id,
            AnalysisRequestId = model.AnalysisRequestId,
            StrokeType = model.StrokeType,
            QualityScore = model.QualityScore,
            PhaseScores = new PhaseScoresDto
            {
                Backswing = model.BackswingScore,
                Contact = model.ContactScore,
                FollowThrough = model.FollowThroughScore,
            },
            CreatedAt = model.CreatedAt,
        };
    }
}
