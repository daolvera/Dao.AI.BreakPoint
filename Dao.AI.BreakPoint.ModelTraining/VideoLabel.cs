using Dao.AI.BreakPoint.Data.Enums;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Dao.AI.BreakPoint.ModelTraining;

/// <summary>
/// Label data for training videos.
/// Each video file should have a corresponding .json label file with this structure.
/// </summary>
/// <example>
/// {
///   "VideoFile": "forehand_pro.mp4",
///   "StrokeType": "ForehandGroundStroke",
///   "QualityScore": 85,
///   "IsRightHanded": true
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
    [JsonConverter(typeof(JsonStringEnumConverter))]
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
