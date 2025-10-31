namespace Dao.AI.BreakPoint.Services.DTOs;

public class SwingAnalysisDto : CreateSwingAnalysisDto
{
    public int Id { get; set; }
    public required string PlayerName { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CreateSwingAnalysisDto
{
    public int PlayerId { get; set; }
    public double Rating { get; set; }
    public required string Summary { get; set; }
    public required string Recommendations { get; set; }
}