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
}
