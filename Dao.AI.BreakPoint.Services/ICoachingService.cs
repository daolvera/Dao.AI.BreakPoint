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
    int MaxDrills = 3
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
    /// Generate coaching tips based on swing analysis results (legacy method)
    /// </summary>
    /// <param name="strokeType">The type of stroke analyzed</param>
    /// <param name="qualityScore">The quality score (0-100)</param>
    /// <param name="featureImportance">Feature importance from the model's attention</param>
    /// <param name="ustaRating">The player's USTA NTRP rating (1.0-7.0)</param>
    /// <returns>List of coaching tips/drills</returns>
    Task<List<string>> GenerateCoachingTipsAsync(
        SwingType strokeType,
        double qualityScore,
        Dictionary<string, double> featureImportance,
        double ustaRating
    );

    /// <summary>
    /// Generate targeted drill recommendations based on phase-specific analysis
    /// </summary>
    /// <param name="input">Input containing player info, phase analyses, and recent drill history</param>
    /// <returns>List of generated drill recommendations</returns>
    Task<List<GeneratedDrill>> GenerateDrillRecommendationsAsync(DrillRecommendationInput input);
}
