using Dao.AI.BreakPoint.Data.Enums;

namespace Dao.AI.BreakPoint.Data.Models;

/// <summary>
/// Stores phase-specific deviation data from reference profiles.
/// Each AnalysisResult has one PhaseDeviation per swing phase (4 total).
/// </summary>
public class PhaseDeviation : BaseModel
{
    /// <summary>
    /// The analysis result this deviation belongs to
    /// </summary>
    public int AnalysisResultId { get; set; }
    public AnalysisResult AnalysisResult { get; set; } = null!;

    /// <summary>
    /// The swing phase this deviation represents
    /// </summary>
    public SwingPhase Phase { get; set; }

    /// <summary>
    /// Individual feature deviations within this phase
    /// </summary>
    public ICollection<FeatureDeviation> FeatureDeviations { get; set; } = [];
}

/// <summary>
/// Stores individual feature deviation from reference profile for a specific phase.
/// Uses the 20-feature LSTM feature set.
/// </summary>
public class FeatureDeviation : BaseModel
{
    /// <summary>
    /// The phase deviation this feature belongs to
    /// </summary>
    public int PhaseDeviationId { get; set; }
    public PhaseDeviation PhaseDeviation { get; set; } = null!;

    /// <summary>
    /// Index of the feature (0-19, matching LstmFeatureExtractor indices)
    /// </summary>
    public int FeatureIndex { get; set; }

    /// <summary>
    /// Human-readable name of the feature
    /// </summary>
    public string FeatureName { get; set; } = "";

    /// <summary>
    /// Z-score deviation from reference profile mean
    /// Positive = above reference, Negative = below reference
    /// </summary>
    public double ZScore { get; set; }

    /// <summary>
    /// The player's actual value for this feature
    /// </summary>
    public double ActualValue { get; set; }

    /// <summary>
    /// The reference profile mean for this feature
    /// </summary>
    public double ReferenceMean { get; set; }

    /// <summary>
    /// The reference profile standard deviation for this feature
    /// </summary>
    public double ReferenceStd { get; set; }
}
