using System.ComponentModel.DataAnnotations;

namespace Dao.AI.BreakPoint.Data.Models;

public class SwingAnalysis : BaseModel
{
    public int PlayerId { get; set; }
    [Range(1.0, 7.0)]
    public double Rating { get; set; }
    public string Summary { get; set; } = null!;
    public string Recommendations { get; set; } = null!;
    public Player Player { get; set; } = null!;
}
