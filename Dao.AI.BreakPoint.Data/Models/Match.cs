namespace Dao.AI.BreakPoint.Data.Models;

/// <summary>
/// An occurence of a singles matche in tennis
/// </summary>
public class Match
{
    public int Id { get; set; }
    public int Player1Id { get; set; }
    public Player Player1 { get; set; } = null!;
    public int Player2Id { get; set; }
    public Player Player2 { get; set; } = null!;
    public DateTime MatchDate { get; set; }
    public string Location { get; set; } = null!;
    public string Result { get; set; } = null!;
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public bool Player1Won { get; set; }
}
