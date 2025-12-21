using System.ComponentModel.DataAnnotations;
using Dao.AI.BreakPoint.Data.Enums;

namespace Dao.AI.BreakPoint.ModelTraining;

/// <summary>
/// Label data for training videos.
/// Each video file should have a corresponding .json label file with this structure.
/// </summary>
/// <example>
/// {
///   "video_file": "forehand_pro.mp4",
///   "stroke_type": "ForehandGroundStroke",
///   "quality_score": 85,
///   "is_right_handed": true
/// }
/// </example>
internal class VideoLabel
{
    /// <summary>
    /// The video filename this label corresponds to
    /// </summary>
    public string VideoFile { get; set; } = string.Empty;

    /// <summary>
    /// The type of stroke in the video (forehand, backhand, serve, etc.)
    /// </summary>
    public SwingType StrokeType { get; set; }

    /// <summary>
    /// Quality score from 0-100 representing swing technique quality.
    /// All swings in the video get the same score.
    /// </summary>
    [Range(0, 100)]
    public double QualityScore { get; set; }

    /// <summary>
    /// Whether the player in the video is right-handed.
    /// Used for proper hitting arm detection during training.
    /// </summary>
    public bool IsRightHanded { get; set; } = true;

    /// <summary>
    /// When the label was created
    /// </summary>
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
}
