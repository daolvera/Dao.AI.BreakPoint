namespace Dao.AI.BreakPoint.Services.SwingAnalyzer;

public class ProcessedSwingVideo
{
    public List<SwingData> Swings { get; set; } = [];
    public int ImageHeight { get; set; }
    public int ImageWidth { get; set; }
    public int FrameRate { get; set; } = 30;
}
