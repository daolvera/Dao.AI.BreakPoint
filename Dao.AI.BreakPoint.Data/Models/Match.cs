namespace Dao.AI.BreakPoint.Data.Models;

public class Match
{
    public int Id { get; set; }
    public int Player1Id { get; set; }
    /// <summary>
    /// Does not require a second registered player
    /// </summary>
    public int? Player2Id { get; set; }
    public DateTime MatchDate { get; set; }
    public string Location { get; set; } = null!;
    public string Result { get; set; } = null!;
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
