using Microsoft.ML.Data;

namespace Dao.AI.BreakPoint.ModelTraining;

/// <summary>
/// ML.NET data classes and constants for frame-level swing phase classification.
/// Uses a multiclass classifier trained on pose features.
///
/// Output classes:
/// 0 = None (no person or not in tennis stance)
/// 1 = Preparation (ready position)
/// 2 = Backswing (racket going back)
/// 3 = Swing (forward motion through contact)
/// 4 = FollowThrough (after contact)
/// </summary>
public static class SwingPhaseClassifierModel
{
    public const int NumClasses = 5;

    // Feature counts per frame (using pose-relative features)
    public const int AngleFeatures = 8; // 8 joint angles
    public const int RelativePositionFeatures = 12; // 6 key joints × 2 (relative x, y)
    public const int VelocityFeatures = 6; // 6 key joint velocities
    public const int ArmConfigFeatures = 4; // Arm position relative to body
    public const int HandednessFeature = 1;

    /// <summary>
    /// Features per single frame (without temporal context)
    /// </summary>
    public const int FeaturesPerFrame =
        AngleFeatures + RelativePositionFeatures + VelocityFeatures + ArmConfigFeatures; // 30

    /// <summary>
    /// Total features with 3-frame temporal window (current + 2 previous)
    /// Plus handedness which is constant across frames
    /// </summary>
    public const int TotalFeatures = (FeaturesPerFrame * 3) + HandednessFeature; // 30×3 + 1 = 91

    /// <summary>
    /// Class names for output interpretation
    /// </summary>
    public static readonly string[] ClassNames =
    [
        "None",
        "Preparation",
        "Backswing",
        "Swing",
        "FollowThrough",
    ];
}

/// <summary>
/// ML.NET input data class for swing phase classification training.
/// Features is a fixed-size vector of pose data from 3 frames.
/// </summary>
public class PhaseClassifierInput
{
    /// <summary>
    /// Feature vector: 3 frames × 83 features + 1 handedness = 250 features
    /// </summary>
    [VectorType(SwingPhaseClassifierModel.TotalFeatures)]
    public float[] Features { get; set; } = [];

    /// <summary>
    /// Target phase label (0-4)
    /// </summary>
    public uint Label { get; set; }
}

/// <summary>
/// ML.NET output prediction class
/// </summary>
public class PhaseClassifierPrediction
{
    /// <summary>
    /// Predicted class label
    /// </summary>
    public uint PredictedLabel { get; set; }

    /// <summary>
    /// Probability scores for each class
    /// </summary>
    [VectorType(SwingPhaseClassifierModel.NumClasses)]
    public float[] Score { get; set; } = [];
}

/// <summary>
/// Training data structure for a single labeled frame (used for loading JSON data)
/// </summary>
public class LabeledFrameData
{
    /// <summary>
    /// The phase label (0-4)
    /// </summary>
    public required int PhaseLabel { get; set; }

    /// <summary>
    /// Flattened feature vector for this frame (250 features with temporal context)
    /// </summary>
    public required float[] Features { get; set; }
}

/// <summary>
/// Training configuration specific to phase classifier
/// </summary>
public class PhaseClassifierTrainingConfiguration
{
    public string LabeledFramesDirectory { get; set; } = "labeled_frames";
    public string ModelOutputPath { get; set; } = "swingphaseclassifier.onnx";
}
