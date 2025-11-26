namespace Dao.AI.BreakPoint.ModelTraining.VideoAnalysis;

public class SimpleVideoLabel
{
    public string Path { get; set; } = string.Empty;
    public float Score { get; set; }
}

public class SimpleVideoDataset
{
    public List<SimpleVideoLabel> Videos { get; set; } = new();
}

// Keep the complex structures for advanced use cases if needed
public class VideoLabel
{
    public string VideoId { get; set; } = string.Empty;
    public string VideoPath { get; set; } = string.Empty;
    public float OverallScore { get; set; }
    public int ContactFrame { get; set; }
    public TechniqueScores TechniqueScores { get; set; } = new();
    public List<string> IssueCategories { get; set; } = new();
    public VideoMetadata Metadata { get; set; } = new();
}

public class TechniqueScores
{
    public float ShoulderRotationScore { get; set; }
    public float ContactPointScore { get; set; }
    public float PreparationTimingScore { get; set; }
    public float BalanceScore { get; set; }
    public float FollowThroughScore { get; set; }
}

public class VideoMetadata
{
    public int FrameRate { get; set; } = 30;
    public int Width { get; set; }
    public int Height { get; set; }
    public int TotalFrames { get; set; }
    public double DurationSeconds { get; set; }
    public string PlayerName { get; set; } = string.Empty;
    public string SkillLevel { get; set; } = string.Empty;
    public DateTime RecordingDate { get; set; }
    public string Court { get; set; } = string.Empty;
    public string Equipment { get; set; } = string.Empty;
}

public class LabeledVideoDataset
{
    public List<VideoLabel> Videos { get; set; } = new();
    public string DatasetVersion { get; set; } = string.Empty;
    public DateTime CreatedDate { get; set; }
    public string Description { get; set; } = string.Empty;
}