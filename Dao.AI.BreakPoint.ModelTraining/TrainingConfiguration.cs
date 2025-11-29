using System.ComponentModel.DataAnnotations;

namespace Dao.AI.BreakPoint.ModelTraining;

public class TrainingConfiguration
{
    public string VideoDirectory { get; set; } = "data";
    public string InputModelPath { get; set; } = "movenet/saved_model.pb";
    public string ModelOutputPath { get; set; } = "swing_model";
    [Range(1, int.MaxValue, ErrorMessage = "The {0} field must be a positive integer.")]
    public int Epochs { get; set; } = 5;
    public bool OutputSwingVideos { get; set; } = true;
    [Range(1, int.MaxValue, ErrorMessage = "The {0} field must be a positive integer.")]
    public int BatchSize { get; set; } = 32;
    public float ValidationSplit { get; set; } = 0.2f;
    public float LearningRate { get; set; } = 0.001f;

    /// <summary>
    /// Number of features per frame (based on MoveNet pose features)
    /// 12 joints * 2 (velocity + acceleration) + 8 joint angles + 17 joints * 2 (x,y positions) = 66 features
    /// </summary>
    public int NumFeatures { get; set; } = 66;

    /// <summary>
    /// Maximum number of frames per swing sequence (standardized length for CNN input)
    /// Typically around 60-90 frames for a 2-3 second tennis swing at 30 FPS
    /// </summary>
    [Range(1, int.MaxValue, ErrorMessage = "The {0} field must be a positive integer.")]
    public int SequenceLength { get; set; } = 90;

    /// <summary>
    /// Calculate sequence length based on swing duration and frame rate
    /// </summary>
    /// <param name="swingDurationSeconds">Duration of swing in seconds (typically 2-4 seconds)</param>
    /// <param name="frameRate">Video frame rate (typically 30 FPS)</param>
    /// <returns>Calculated sequence length</returns>
    public static int CalculateSequenceLength(double swingDurationSeconds, int frameRate)
    {
        return (int)Math.Ceiling(swingDurationSeconds * frameRate);
    }
}
