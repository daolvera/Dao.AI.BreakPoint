using Dao.AI.BreakPoint.Data;
using Dao.AI.BreakPoint.Data.Models;
using Dao.AI.BreakPoint.Services.DTOs;
using Dao.AI.BreakPoint.Services.SearchParams;
using Microsoft.EntityFrameworkCore;

namespace Dao.AI.BreakPoint.Services.Repositories;

public class PlayerRepository : BaseRepository<Player, PlayerSearchParameters>, IPlayerRepository
{
    private BreakPointDbContext DbContext { get; init; }
    public PlayerRepository(BreakPointDbContext dbContext) : base(dbContext)
    {
        DbContext = dbContext;
    }

    public override IQueryable<Player> ApplySearchFilters(IQueryable<Player> query, PlayerSearchParameters searchParams)
    {
        if (!string.IsNullOrWhiteSpace(searchParams.PlayerName))
        {
            query = query.Where(p => p.Name.Contains(searchParams.PlayerName));
        }

        if (!string.IsNullOrWhiteSpace(searchParams.Email))
        {
            query = query
                .Include(p => p.AppUser)
                .Where(p => p.AppUser != null &&
                    p.AppUser.Email.Equals(searchParams.Email, StringComparison.InvariantCultureIgnoreCase));
        }

        return query;
    }

    public async Task<PlayerWithStatsDto?> GetPlayerWithStatsAsync(int id)
    {
        var player = await DbContext.Players.Where(p => p.Id == id)
            .Include(o => o.AppUser)
            .Include(p => p.MyReportedMatches)
            .Include(p => p.MyParticipatedMatches)
            .SingleOrDefaultAsync();
        if (player is null)
        {
            return null;
        }
        int totalMatches = player.MyParticipatedMatches.Count + player.MyReportedMatches.Count;
        int matchesWon = player.MyReportedMatches.Count(m => m.Player1Won) +
                             player.MyParticipatedMatches.Count(m => !m.Player1Won);
        return new PlayerWithStatsDto()
        {
            Id = player.Id,
            Name = player.Name,
            Email = player.AppUser?.Email,
            TotalMatches = totalMatches,
            CreatedAt = player.CreatedAt,
            UpdatedAt = player.UpdatedAt,
            EstimatedPlayerType = player.EstimatedPlayerType,
            EstimatedRating = player.UstaRating,
            BigServerScore = player.BigServerScore,
            ServeAndVolleyerScore = player.ServeAndVolleyerScore,
            AllCourtPlayerScore = player.AllCourtPlayerScore,
            AttackingBaselinerScore = player.AttackingBaselinerScore,
            SolidBaselinerScore = player.SolidBaselinerScore,
            CounterPuncherScore = player.CounterPuncherScore,
            MatchesWon = matchesWon,
            MatchesLost = totalMatches - matchesWon,
        };
    }
}
