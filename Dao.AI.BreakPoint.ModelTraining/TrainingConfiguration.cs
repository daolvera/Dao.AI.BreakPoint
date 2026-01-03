using System.ComponentModel.DataAnnotations;

namespace Dao.AI.BreakPoint.ModelTraining;

public class TrainingConfiguration
{
    public string VideoDirectory { get; set; } = "data";
    public string InputModelPath { get; set; } = "movenet/saved_model.pb";
    public string PhaseClassifierModelPath { get; set; } =
        "phase_classifier/swing_phase_classifier.onnx";
    public string ModelOutputPath { get; set; } = "swing_model";

    [Range(1, int.MaxValue, ErrorMessage = "The {0} field must be a positive integer.")]
    public int Epochs { get; set; } = 5;
    public bool OutputSwingVideos { get; set; } = true;

    [Range(1, int.MaxValue, ErrorMessage = "The {0} field must be a positive integer.")]
    public int BatchSize { get; set; } = 32;
    public float ValidationSplit { get; set; } = 0.2f;

    /// <summary>
    /// Learning rate for FastTree gradient boosting.
    /// Higher values (0.05-0.1) work better with fewer trees.
    /// Lower values (0.001-0.01) need more trees to converge.
    /// </summary>
    public float LearningRate { get; set; } = 0.1f;
    public string? ImageDirectory { get; set; }
    public bool IsTestingHeuristicFeatures => ImageDirectory is not null;

    /// <summary>
    /// Number of features per frame (focused on key tennis swing features)
    /// 6 key joints (wrist, elbow, shoulder) × 2 (velocity + acceleration) = 12 motion features
    /// + 4 key angles (elbow, shoulder) = 16 total features per frame
    /// </summary>
    public int NumFeatures { get; set; } = 16;

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
