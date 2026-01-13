using Dao.AI.BreakPoint.Services.DTOs;

namespace Dao.AI.BreakPoint.Services;

/// <summary>
/// Service for managing drill recommendations
/// </summary>
public interface IDrillRecommendationService
{
    /// <summary>
    /// Get all drills for a player
    /// </summary>
    Task<List<DrillRecommendationDto>> GetPlayerDrillsAsync(int playerId, bool activeOnly = true);

    /// <summary>
    /// Get drills for a specific analysis result
    /// </summary>
    Task<List<DrillRecommendationDto>> GetDrillsForAnalysisAsync(int analysisResultId);

    /// <summary>
    /// Get a specific drill by ID
    /// </summary>
    Task<DrillRecommendationDto?> GetDrillByIdAsync(int id);

    /// <summary>
    /// Mark a drill as completed
    /// </summary>
    Task<DrillRecommendationDto?> MarkDrillCompletedAsync(int id, string appUserId);

    /// <summary>
    /// Submit feedback for a drill
    /// </summary>
    Task<DrillRecommendationDto?> SubmitFeedbackAsync(
        int id,
        bool thumbsUp,
        string? feedbackText,
        string appUserId
    );

    /// <summary>
    /// Dismiss/deactivate a drill
    /// </summary>
    Task<bool> DismissDrillAsync(int id, string appUserId);

    /// <summary>
    /// Get drill history with feedback for a player (for coaching AI context)
    /// </summary>
    Task<List<DrillRecommendationDto>> GetDrillHistoryWithFeedbackAsync(
        int playerId,
        int limit = 20
    );
}
