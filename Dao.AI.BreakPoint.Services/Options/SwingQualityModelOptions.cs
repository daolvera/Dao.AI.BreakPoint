namespace Dao.AI.BreakPoint.Services.Options;

/// <summary>
/// Configuration options for the swing quality CNN model
/// </summary>
public class SwingQualityModelOptions
{
    public const string SectionName = "SwingQualityModel";

    /// <summary>
    /// Path to the trained ONNX model file.
    /// If null or file doesn't exist, heuristic scoring is used.
    /// </summary>
    public string? ModelPath { get; set; }

    /// <summary>
    /// Expected sequence length (number of frames) for the model.
    /// Default: 90 frames (~3 seconds at 30fps)
    /// </summary>
    public int SequenceLength { get; set; } = 90;

    /// <summary>
    /// Number of features per frame.
    /// Default: 66 (12 joints × 2 motion metrics + 8 angles + 17 joints × 2 positions)
    /// </summary>
    public int NumFeatures { get; set; } = 66;
}
