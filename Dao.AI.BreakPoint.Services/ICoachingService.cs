using Dao.AI.BreakPoint.Data.Enums;

namespace Dao.AI.BreakPoint.Services;

/// <summary>
/// Service for generating coaching tips using Azure OpenAI
/// </summary>
public interface ICoachingService
{
    /// <summary>
    /// Generate coaching tips based on swing analysis results
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
}
