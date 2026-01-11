using Dao.AI.BreakPoint.Data.Models;

namespace Dao.AI.BreakPoint.Services.Repositories;

/// <summary>
/// Repository for managing drill recommendations
/// </summary>
public interface IDrillRecommendationRepository
{
    /// <summary>
    /// Get recent drill recommendations for a player, ordered by most recent first
    /// </summary>
    /// <param name="playerId">The player ID</param>
    /// <param name="count">Maximum number of drills to retrieve</param>
    /// <returns>List of recent drill recommendations</returns>
    Task<List<DrillRecommendation>> GetRecentDrillsAsync(int playerId, int count);

    /// <summary>
    /// Get drills with player feedback (thumbs up/down) for a player
    /// </summary>
    /// <param name="playerId">The player ID</param>
    /// <param name="count">Maximum number of drills to retrieve</param>
    /// <returns>List of drills that have feedback</returns>
    Task<List<DrillRecommendation>> GetDrillsWithFeedbackAsync(int playerId, int count);

    /// <summary>
    /// Update drill feedback (thumbs up/down and optional text)
    /// </summary>
    Task<bool> UpdateDrillFeedbackAsync(int drillId, bool thumbsUp, string? feedbackText);

    /// <summary>
    /// Mark a drill as completed
    /// </summary>
    Task<bool> MarkDrillCompletedAsync(int drillId);
}
