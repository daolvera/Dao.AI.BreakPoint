using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Dao.AI.BreakPoint.Data.Enums;

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
///   "PrepScore": 90,
///   "BackswingScore": 85,
///   "ContactScore": 80,
///   "FollowThroughScore": 88,
///   "IsRightHanded": true
/// }
/// </example>
public class VideoLabel
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
    /// Overall quality score from 0-100 representing swing technique quality.
    /// This is the weighted average of phase scores or manually assigned.
    /// </summary>
    [Range(0, 100)]
    public double QualityScore { get; set; }

    /// <summary>
    /// Quality score for the Preparation phase (0-100).
    /// Evaluates ready position, split step, and initial movement.
    /// </summary>
    [Range(0, 100)]
    public int PrepScore { get; set; }

    /// <summary>
    /// Quality score for the Backswing phase (0-100).
    /// Evaluates racket takeback, shoulder turn, and unit turn.
    /// </summary>
    [Range(0, 100)]
    public int BackswingScore { get; set; }

    /// <summary>
    /// Quality score for the Contact phase (0-100).
    /// Evaluates contact point, racket face angle, and body position at impact.
    /// </summary>
    [Range(0, 100)]
    public int ContactScore { get; set; }

    /// <summary>
    /// Quality score for the Follow-Through phase (0-100).
    /// Evaluates extension, racket path, and recovery position.
    /// </summary>
    [Range(0, 100)]
    public int FollowThroughScore { get; set; }

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
