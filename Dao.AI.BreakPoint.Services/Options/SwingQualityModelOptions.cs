namespace Dao.AI.BreakPoint.Services.Options;

/// <summary>
/// Configuration options for the swing quality model
/// </summary>
public class SwingQualityModelOptions
{
    public const string SectionName = "SwingQualityModel";

    /// <summary>
    /// Path to the trained ONNX model file.
    /// If null or file doesn't exist, heuristic scoring is used.
    /// </summary>
    public string? ModelPath { get; set; }
}
