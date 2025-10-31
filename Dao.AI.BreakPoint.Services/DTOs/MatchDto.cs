namespace Dao.AI.BreakPoint.Services.DTOs;

public class MatchDto : CreateMatchDto
{
    public int Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class CreateMatchDto
{
    public int Player1Id { get; set; }
    public int? Player2Id { get; set; }
    public DateTime MatchDate { get; set; }
    public required string Location { get; set; }
    public required string Result { get; set; }
    public string? Notes { get; set; }
}