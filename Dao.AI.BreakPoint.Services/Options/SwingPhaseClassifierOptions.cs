namespace Dao.AI.BreakPoint.Services.Options;

/// <summary>
/// Configuration options for the swing phase classifier model
/// </summary>
public class SwingPhaseClassifierOptions
{
    public const string SectionName = "SwingPhaseClassifier";

    /// <summary>
    /// Path to the trained ONNX model file.
    /// If null or file doesn't exist, heuristic classification is used.
    /// </summary>
    public required string ModelPath { get; set; }
}
