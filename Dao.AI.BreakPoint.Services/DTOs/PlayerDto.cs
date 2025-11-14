using Dao.AI.BreakPoint.Data.Enums;
using Dao.AI.BreakPoint.Data.Models;

namespace Dao.AI.BreakPoint.Services.DTOs;

public class PlayerDto : CreatePlayerDto
{
    public int Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public PlayerType EstimatedPlayerType { get; set; }
    public static PlayerDto FromModel(Player player)
        => new()
        {
            Id = player.Id,
            Name = player.Name,
            Email = player.AppUser?.Email,
            EstimatedPlayerType = player.EstimatedPlayerType,
            CreatedAt = player.CreatedAt,
            UpdatedAt = player.UpdatedAt
        };
}

public class CreatePlayerDto : IBaseDto<Player>
{
    public required string Name { get; set; }
    public string? Email { get; set; }
    // TODO: implement create flow
    public Player ToModel()
        => new()
        {
            Name = Name
        };
}

public class PlayerWithStatsDto : PlayerDto
{
    public int TotalMatches { get; set; }
    public int MatchesWon { get; set; }
    public int MatchesLost { get; set; }
    public double WinPercentage => MatchesWon + MatchesLost > 0 ?
        (double)(MatchesWon / (MatchesWon + MatchesLost)) * 100 :
        0;
    public IEnumerable<string> LatestCoachingTips { get; set; } = [];
    public double? EstimatedRating { get; set; }
    public double BigServerScore { get; set; }
    public double ServeAndVolleyerScore { get; set; }
    public double AllCourtPlayerScore { get; set; }
    public double AttackingBaselinerScore { get; set; }
    public double SolidBaselinerScore { get; set; }
    public double CounterPuncherScore { get; set; }
}