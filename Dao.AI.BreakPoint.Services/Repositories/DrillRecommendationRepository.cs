using Dao.AI.BreakPoint.Data;
using Dao.AI.BreakPoint.Data.Models;
using Microsoft.EntityFrameworkCore;

namespace Dao.AI.BreakPoint.Services.Repositories;

public class DrillRecommendationRepository(BreakPointDbContext dbContext)
    : IDrillRecommendationRepository
{
    public async Task<List<DrillRecommendation>> GetRecentDrillsAsync(int playerId, int count)
    {
        return await dbContext
            .Set<DrillRecommendation>()
            .Where(d => d.PlayerId == playerId && d.IsActive)
            .OrderByDescending(d => d.CreatedAt)
            .Take(count)
            .ToListAsync();
    }

    public async Task<List<DrillRecommendation>> GetDrillsWithFeedbackAsync(int playerId, int count)
    {
        return await dbContext
            .Set<DrillRecommendation>()
            .Where(d => d.PlayerId == playerId && d.ThumbsUp.HasValue)
            .OrderByDescending(d => d.UpdatedAt)
            .Take(count)
            .ToListAsync();
    }

    public async Task<bool> UpdateDrillFeedbackAsync(
        int drillId,
        bool thumbsUp,
        string? feedbackText
    )
    {
        var drill = await dbContext.Set<DrillRecommendation>().FindAsync(drillId);
        if (drill == null)
            return false;

        drill.ThumbsUp = thumbsUp;
        drill.FeedbackText = feedbackText;
        drill.UpdatedAt = DateTime.UtcNow;

        await dbContext.SaveChangesAsync();
        return true;
    }

    public async Task<bool> MarkDrillCompletedAsync(int drillId)
    {
        var drill = await dbContext.Set<DrillRecommendation>().FindAsync(drillId);
        if (drill == null)
            return false;

        drill.CompletedAt = DateTime.UtcNow;
        drill.UpdatedAt = DateTime.UtcNow;

        await dbContext.SaveChangesAsync();
        return true;
    }
}
