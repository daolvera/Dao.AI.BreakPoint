using Dao.AI.BreakPoint.Data;
using Dao.AI.BreakPoint.Data.Models;
using Dao.AI.BreakPoint.Services.DTOs;
using Microsoft.EntityFrameworkCore;

namespace Dao.AI.BreakPoint.Services;

/// <summary>
/// Service for managing drill recommendations
/// </summary>
public class DrillRecommendationService(BreakPointDbContext dbContext) : IDrillRecommendationService
{
    public async Task<List<DrillRecommendationDto>> GetPlayerDrillsAsync(
        int playerId,
        bool activeOnly = true
    )
    {
        var query = dbContext.DrillRecommendations.Where(d => d.PlayerId == playerId);

        if (activeOnly)
        {
            query = query.Where(d => d.IsActive);
        }

        var drills = await query
            .OrderByDescending(d => d.CreatedAt)
            .ThenBy(d => d.Priority)
            .ToListAsync();

        return drills.Select(DrillRecommendationDto.FromModel).ToList();
    }

    public async Task<List<DrillRecommendationDto>> GetDrillsForAnalysisAsync(int analysisResultId)
    {
        var drills = await dbContext
            .DrillRecommendations.Where(d => d.AnalysisResultId == analysisResultId)
            .OrderBy(d => d.Priority)
            .ToListAsync();

        return drills.Select(DrillRecommendationDto.FromModel).ToList();
    }

    public async Task<DrillRecommendationDto?> GetDrillByIdAsync(int id)
    {
        var drill = await dbContext.DrillRecommendations.FindAsync(id);
        return drill is not null ? DrillRecommendationDto.FromModel(drill) : null;
    }

    public async Task<DrillRecommendationDto?> MarkDrillCompletedAsync(int id, string appUserId)
    {
        var drill = await dbContext.DrillRecommendations.FindAsync(id);
        if (drill is null)
            return null;

        drill.CompletedAt = DateTime.UtcNow;
        drill.UpdatedAt = DateTime.UtcNow;
        drill.UpdatedByAppUserId = appUserId;

        await dbContext.SaveChangesAsync();
        return DrillRecommendationDto.FromModel(drill);
    }

    public async Task<DrillRecommendationDto?> SubmitFeedbackAsync(
        int id,
        bool thumbsUp,
        string? feedbackText,
        string appUserId
    )
    {
        var drill = await dbContext.DrillRecommendations.FindAsync(id);
        if (drill is null)
            return null;

        drill.ThumbsUp = thumbsUp;
        drill.FeedbackText = feedbackText;
        drill.UpdatedAt = DateTime.UtcNow;
        drill.UpdatedByAppUserId = appUserId;

        // Also mark as completed if submitting feedback
        drill.CompletedAt ??= DateTime.UtcNow;

        await dbContext.SaveChangesAsync();
        return DrillRecommendationDto.FromModel(drill);
    }

    public async Task<bool> DismissDrillAsync(int id, string appUserId)
    {
        var drill = await dbContext.DrillRecommendations.FindAsync(id);
        if (drill is null)
            return false;

        drill.IsActive = false;
        drill.UpdatedAt = DateTime.UtcNow;
        drill.UpdatedByAppUserId = appUserId;

        await dbContext.SaveChangesAsync();
        return true;
    }

    public async Task<List<DrillRecommendationDto>> GetDrillHistoryWithFeedbackAsync(
        int playerId,
        int limit = 20
    )
    {
        var drills = await dbContext
            .DrillRecommendations.Where(d => d.PlayerId == playerId && d.ThumbsUp.HasValue)
            .OrderByDescending(d => d.UpdatedAt)
            .Take(limit)
            .ToListAsync();

        return drills.Select(DrillRecommendationDto.FromModel).ToList();
    }
}
