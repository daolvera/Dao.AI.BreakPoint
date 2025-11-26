namespace Dao.AI.BreakPoint.ModelTraining.VideoAnalysis;

/// <summary>
/// Individual video label file structure
/// </summary>
public class VideoLabel
{
    public string VideoFileName { get; set; } = string.Empty;
    public double UstaRating { get; set; } // 1.0 to 7.0 USTA rating
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Basic video metadata needed for processing
/// </summary>
public class VideoMetadata
{
    public int FrameRate { get; set; } = 30;
    public int Width { get; set; }
    public int Height { get; set; }
    public int TotalFrames { get; set; }
    public double DurationSeconds { get; set; }
}