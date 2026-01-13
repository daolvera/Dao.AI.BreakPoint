namespace Dao.AI.BreakPoint.Services.Options;

/// <summary>
/// Configuration options for the phase quality models
/// </summary>
public class SwingQualityModelOptions
{
    public const string SectionName = "SwingQualityModel";

    /// <summary>
    /// Directory containing the phase quality TorchSharp models.
    /// Expected files: prep_quality.pt, backswing_quality.pt, contact_quality.pt, followthrough_quality.pt
    /// </summary>
    public string ModelsDirectory { get; set; } = "models";

    /// <summary>
    /// Path to the reference profiles JSON file for z-score deviation analysis.
    /// </summary>
    public string? ReferenceProfilesPath { get; set; } = "models/reference_profiles.json";
}
