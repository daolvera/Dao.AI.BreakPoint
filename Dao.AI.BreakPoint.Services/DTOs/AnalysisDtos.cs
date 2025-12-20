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
    public Dictionary<string, double> FeatureImportance { get; set; } = [];
    public List<string> CoachingTips { get; set; } = [];
    public string? SkeletonOverlayUrl { get; set; }
    public string? VideoBlobUrl { get; set; }
    public DateTime CreatedAt { get; set; }

    public static AnalysisResultDto FromModel(AnalysisResult model)
    {
        var featureImportance = !string.IsNullOrEmpty(model.FeatureImportanceJson)
            ? JsonSerializer.Deserialize<Dictionary<string, double>>(model.FeatureImportanceJson)
                ?? []
            : [];

        var coachingTips = !string.IsNullOrEmpty(model.CoachingTipsJson)
            ? JsonSerializer.Deserialize<List<string>>(model.CoachingTipsJson) ?? []
            : [];

        return new AnalysisResultDto
        {
            Id = model.Id,
            AnalysisRequestId = model.AnalysisRequestId,
            PlayerId = model.PlayerId,
            StrokeType = model.StrokeType,
            QualityScore = model.QualityScore,
            FeatureImportance = featureImportance,
            CoachingTips = coachingTips,
            SkeletonOverlayUrl = model.SkeletonOverlayUrl,
            VideoBlobUrl = model.VideoBlobUrl,
            CreatedAt = model.CreatedAt,
        };
    }
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
    public DateTime CreatedAt { get; set; }

    public static AnalysisResultSummaryDto FromModel(AnalysisResult model)
    {
        return new AnalysisResultSummaryDto
        {
            Id = model.Id,
            AnalysisRequestId = model.AnalysisRequestId,
            StrokeType = model.StrokeType,
            QualityScore = model.QualityScore,
            CreatedAt = model.CreatedAt,
        };
    }
}
