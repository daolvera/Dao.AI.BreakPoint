using System.ComponentModel.DataAnnotations;

namespace Dao.AI.BreakPoint.ModelTraining;

internal class VideoLabel
{
    [Range(1, 7)]
    public double UstaRating { get; set; } // 1.0 to 7.0 USTA rating
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
}
