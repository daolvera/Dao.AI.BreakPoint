using Dao.AI.BreakPoint.Data.Enums;
using Dao.AI.BreakPoint.Data.Models;

namespace Dao.AI.BreakPoint.Services;

/// <summary>
/// Input model for phase-specific coaching analysis
/// </summary>
public record PhaseAnalysisInput(
    SwingPhase Phase,
    int Score,
    Dictionary<string, double> Deviations
);

/// <summary>
/// Input model for drill recommendation generation
/// </summary>
public record DrillRecommendationInput(
    int PlayerId,
    SwingType StrokeType,
    double UstaRating,
    int OverallQualityScore,
    List<PhaseAnalysisInput> PhaseAnalyses,
    List<DrillRecommendation> RecentDrills,
    string? PlayerHistorySummary = null,
    int MaxDrills = 3
);

/// <summary>
/// Input model for generating training history summary
/// </summary>
public record TrainingHistoryInput(
    string PlayerName,
    double UstaRating,
    SwingType StrokeType,
    int OverallQualityScore,
    List<GeneratedDrill> NewDrills,
    string? PreviousHistorySummary
);

/// <summary>
/// Output model for a generated drill recommendation
/// </summary>
public record GeneratedDrill(
    SwingPhase TargetPhase,
    string TargetFeature,
    string DrillName,
    string Description,
    string SuggestedDuration,
    int Priority
);

/// <summary>
/// Service for generating coaching tips and drill recommendations using Azure OpenAI
/// </summary>
public interface ICoachingService
{
    /// <summary>
    /// Generate targeted drill recommendations based on phase-specific analysis
    /// </summary>
    /// <param name="input">Input containing player info, phase analyses, and recent drill history</param>
    /// <returns>List of generated drill recommendations</returns>
    Task<List<GeneratedDrill>> GenerateDrillRecommendationsAsync(DrillRecommendationInput input);

    /// <summary>
    /// Generate an updated training history summary for the player.
    /// This summarizes their progress over time to personalize future coaching.
    /// </summary>
    /// <param name="input">Input containing current analysis and previous history</param>
    /// <returns>Updated training history summary</returns>
    Task<string> GenerateTrainingHistorySummaryAsync(TrainingHistoryInput input);
}
