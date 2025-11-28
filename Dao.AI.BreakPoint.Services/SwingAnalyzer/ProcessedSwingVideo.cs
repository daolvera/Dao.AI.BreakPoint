using System.ComponentModel.DataAnnotations;

namespace Dao.AI.BreakPoint.Services.SwingAnalyzer;

public class ProcessedSwingVideo
{
    public List<SwingData> Swings { get; set; } = [];
    [Range(1, 7)]
    public double UstaRating { get; set; }
    public int ImageHeight { get; set; }
    public int ImageWidth { get; set; }
    public int FrameRate { get; set; } = 30;
}
