using Microsoft.ML.Data;

namespace Dao.AI.BreakPoint.ModelTraining;

/// <summary>
/// ML.NET data classes and constants for swing quality analysis.
/// Uses regression to predict quality scores from pose sequences.
///
/// Note: ML.NET doesn't support custom CNN architectures like TensorFlow,
/// but provides powerful regression trainers that work well for this task.
/// The model learns from aggregated sequence features to predict quality.
/// </summary>
public static class SwingCnnModel
{
    // Feature indices for interpretability
    public const int NumJoints = 12;
    public const int VelocityFeaturesStart = 0;
    public const int AccelerationFeaturesStart = 12;
    public const int AngleFeaturesStart = 24;
    public const int PositionFeaturesStart = 32;

    /// <summary>
    /// Number of features per frame in the sequence
    /// </summary>
    public const int FeaturesPerFrame = 66;

    /// <summary>
    /// Number of output scores (overall + 5 sub-components)
    /// </summary>
    public const int NumOutputs = 6;
}

/// <summary>
/// ML.NET input data class for swing quality regression.
/// Features are aggregated statistics from the swing sequence.
/// </summary>
public class SwingQualityInput
{
    /// <summary>
    /// Aggregated feature vector from the swing sequence.
    /// Contains statistics (mean, std, range) for each feature
    /// across the temporal dimension.
    /// 16 features × 3 statistics = 48 total
    /// </summary>
    [VectorType(48)] // 16 features × 3 statistics = 48
    public float[] Features { get; set; } = [];

    /// <summary>
    /// Target quality score (0-100)
    /// </summary>
    public float QualityScore { get; set; }
}

/// <summary>
/// ML.NET output prediction class for quality score
/// </summary>
public class SwingQualityPrediction
{
    /// <summary>
    /// Predicted quality score
    /// </summary>
    public float Score { get; set; }
}

/// <summary>
/// Helper class to interpret feature importance after inference.
/// Maps feature indices to human-readable names.
/// </summary>
public static class AttentionInterpreter
{
    /// <summary>
    /// Joint feature names for interpretable output
    /// </summary>
    public static readonly string[] JointNames =
    [
        "Left Shoulder",
        "Right Shoulder",
        "Left Elbow",
        "Right Elbow",
        "Left Wrist",
        "Right Wrist",
        "Left Hip",
        "Right Hip",
        "Left Knee",
        "Right Knee",
        "Left Ankle",
        "Right Ankle",
    ];

    /// <summary>
    /// Angle feature names
    /// </summary>
    public static readonly string[] AngleNames =
    [
        "Left Elbow Angle",
        "Right Elbow Angle",
        "Left Shoulder Angle",
        "Right Shoulder Angle",
        "Left Hip Angle",
        "Right Hip Angle",
        "Left Knee Angle",
        "Right Knee Angle",
    ];

    /// <summary>
    /// Get human-readable name for a feature index
    /// </summary>
    public static string GetFeatureName(int index)
    {
        // Velocity features: 0-11 (12 joints)
        if (index < 12)
        {
            return $"{JointNames[index]} Velocity";
        }
        // Acceleration features: 12-23
        if (index < 24)
        {
            return $"{JointNames[index - 12]} Acceleration";
        }
        // Angle features: 24-31
        if (index < 32)
        {
            return AngleNames[index - 24];
        }
        // Position features: 32-65 (17 joints x 2 coords)
        int posIndex = index - 32;
        int jointIndex = posIndex / 2;
        string coord = posIndex % 2 == 0 ? "X" : "Y";

        // Map to joint name (17 MoveNet keypoints)
        string[] allJointNames =
        [
            "Nose",
            "Left Eye",
            "Right Eye",
            "Left Ear",
            "Right Ear",
            "Left Shoulder",
            "Right Shoulder",
            "Left Elbow",
            "Right Elbow",
            "Left Wrist",
            "Right Wrist",
            "Left Hip",
            "Right Hip",
            "Left Knee",
            "Right Knee",
            "Left Ankle",
            "Right Ankle",
        ];

        if (jointIndex < allJointNames.Length)
        {
            return $"{allJointNames[jointIndex]} Position {coord}";
        }

        return $"Feature {index}";
    }

    /// <summary>
    /// Extract top N most important features from feature importance scores
    /// </summary>
    /// <param name="featureImportanceScores">Importance scores for each feature (length 66)</param>
    /// <param name="topN">Number of top features to return</param>
    /// <returns>List of (feature_name, importance_score) tuples</returns>
    public static List<(string FeatureName, float Importance)> GetTopImportantFeatures(
        float[] featureImportanceScores,
        int topN = 5
    )
    {
        var featureImportance = new List<(string Name, float Score, int Index)>();

        // Map weights to feature names
        for (int i = 0; i < featureImportanceScores.Length && i < 66; i++)
        {
            string featureName = GetFeatureName(i);
            featureImportance.Add((featureName, featureImportanceScores[i], i));
        }

        // Sort by importance and return top N
        return
        [
            .. featureImportance
                .OrderByDescending(f => f.Score)
                .Take(topN)
                .Select(f => (f.Name, f.Score)),
        ];
    }

    /// <summary>
    /// Get most negative features (areas needing improvement)
    /// </summary>
    public static List<(string FeatureName, float Importance)> GetWorstFeatures(
        float[] featureImportanceScores,
        int topN = 3
    )
    {
        var featureImportance = new List<(string Name, float Score, int Index)>();

        for (int i = 0; i < featureImportanceScores.Length && i < 66; i++)
        {
            string featureName = GetFeatureName(i);
            featureImportance.Add((featureName, featureImportanceScores[i], i));
        }

        // Sort by lowest importance (most negative contribution)
        return
        [
            .. featureImportance.OrderBy(f => f.Score).Take(topN).Select(f => (f.Name, f.Score)),
        ];
    }
}
