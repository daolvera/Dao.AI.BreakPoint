using System.ComponentModel.DataAnnotations;

namespace Dao.AI.BreakPoint.Services.MoveNet;

/// <summary>
/// Individual video label file structure
/// </summary>
public class VideoLabel
{
    public string VideoFileName { get; set; } = string.Empty;
    [Range(1, 7)]
    public double UstaRating { get; set; } // 1.0 to 7.0 USTA rating
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
}
