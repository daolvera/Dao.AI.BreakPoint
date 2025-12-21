using Tensorflow.Keras.Engine;
using static Tensorflow.KerasApi;

namespace Dao.AI.BreakPoint.ModelTraining;

/// <summary>
/// CNN model for swing quality analysis with attention-like mechanisms.
/// Outputs quality score plus feature importance weights for interpretability.
///
/// Note: TensorFlow.NET has limited layer support compared to Python TensorFlow.
/// This implementation uses a simplified attention mechanism that still provides
/// interpretable feature importance through learned dense weights.
/// </summary>
public static class SwingCnnModel
{
    // Joint groups for attention - indices into the 66 features
    // Features are: 12 joints * 2 (velocity + accel) + 8 angles + 17 joints * 2 (x,y)
    // These constants help interpret attention weights
    public const int NumJoints = 12;
    public const int VelocityFeaturesStart = 0;
    public const int AccelerationFeaturesStart = 12;
    public const int AngleFeaturesStart = 24;
    public const int PositionFeaturesStart = 32;

    /// <summary>
    /// Build 1D CNN model with attention-like feature importance for swing analysis.
    ///
    /// Architecture:
    /// - CNN backbone extracts spatial-temporal features
    /// - Global pooling provides frame-level aggregation
    /// - Dense layers with interpretable weights for feature importance
    /// - Output: quality score (0-100)
    ///
    /// Feature importance can be extracted post-training by analyzing gradient flow
    /// or by examining the learned weights of the first dense layer.
    /// </summary>
    /// <param name="sequenceLength">Number of frames in sequence (e.g., 90)</param>
    /// <param name="numFeatures">Number of features per frame (e.g., 66)</param>
    /// <returns>Model that outputs quality score</returns>
    public static IModel BuildModelWithAttention(int sequenceLength, int numFeatures)
    {
        var input = keras.Input(shape: (sequenceLength, numFeatures));

        // === CNN Feature Extraction Backbone ===
        // Extracts hierarchical features from pose sequences

        // First conv block - local patterns (3-frame window)
        var conv1 = keras.layers.Conv1D(64, 3, activation: "relu", padding: "same").Apply(input);
        var bn1 = keras.layers.BatchNormalization().Apply(conv1);
        var pool1 = keras.layers.MaxPooling1D(2).Apply(bn1);
        var dropout1 = keras.layers.Dropout(0.2f).Apply(pool1);

        // Second conv block - medium patterns (5-frame window)
        var conv2 = keras
            .layers.Conv1D(128, 5, activation: "relu", padding: "same")
            .Apply(dropout1);
        var bn2 = keras.layers.BatchNormalization().Apply(conv2);
        var pool2 = keras.layers.MaxPooling1D(2).Apply(bn2);
        var dropout2 = keras.layers.Dropout(0.2f).Apply(pool2);

        // Third conv block - wider patterns (7-frame window)
        var conv3 = keras
            .layers.Conv1D(256, 7, activation: "relu", padding: "same")
            .Apply(dropout2);
        var bn3 = keras.layers.BatchNormalization().Apply(conv3);
        var dropout3 = keras.layers.Dropout(0.3f).Apply(bn3);

        // === Temporal Aggregation ===
        // Global pooling aggregates across time, giving implicit temporal attention
        // Frames with stronger features naturally contribute more
        var globalPool = keras.layers.GlobalAveragePooling1D().Apply(dropout3);

        // === Feature Importance Layer ===
        // This dense layer learns which CNN features matter most
        // The weights can be analyzed post-training for interpretability
        var featureImportance = keras.layers.Dense(128, activation: "relu").Apply(globalPool);
        var dropoutFI = keras.layers.Dropout(0.4f).Apply(featureImportance);

        // Second importance layer for finer-grained feature selection
        var dense2 = keras.layers.Dense(64, activation: "relu").Apply(dropoutFI);
        var dropout4 = keras.layers.Dropout(0.3f).Apply(dense2);

        // === Output: Quality Score ===
        // Single output scaled to 0-100 range via sigmoid * 100 post-processing
        var qualityOutput = keras.layers.Dense(1, activation: "sigmoid").Apply(dropout4);

        var model = keras.Model(inputs: input, outputs: qualityOutput);
        return model;
    }

    /// <summary>
    /// Build simpler single-output model for backward compatibility.
    /// Output: [overall_rating, shoulder_score, contact_score, prep_score, balance_score, follow_score]
    /// </summary>
    public static IModel BuildSingleOutputModel(int sequenceLength, int numFeatures)
    {
        var input = keras.Input(shape: (sequenceLength, numFeatures));

        // Shared CNN layers for feature extraction
        var conv1 = keras.layers.Conv1D(64, 3, activation: "relu", padding: "same").Apply(input);
        var pool1 = keras.layers.MaxPooling1D(2).Apply(conv1);
        var dropout1 = keras.layers.Dropout(0.3f).Apply(pool1);

        var conv2 = keras
            .layers.Conv1D(128, 5, activation: "relu", padding: "same")
            .Apply(dropout1);
        var pool2 = keras.layers.MaxPooling1D(2).Apply(conv2);
        var dropout2 = keras.layers.Dropout(0.3f).Apply(pool2);

        var conv3 = keras
            .layers.Conv1D(256, 7, activation: "relu", padding: "same")
            .Apply(dropout2);
        var globalPool = keras.layers.GlobalAveragePooling1D().Apply(conv3);
        var dropout3 = keras.layers.Dropout(0.4f).Apply(globalPool);

        // Dense shared features
        var dense = keras.layers.Dense(512, activation: "relu").Apply(dropout3);
        var dropout4 = keras.layers.Dropout(0.4f).Apply(dense);

        // Output layer: 6 values for different aspects of swing technique
        // [overall_rating, shoulder_score, contact_score, prep_score, balance_score, follow_score]
        var totalOutputs = 6;
        var output = keras.layers.Dense(totalOutputs, activation: "linear").Apply(dropout4);

        var model = keras.Model(inputs: input, outputs: output);
        return model;
    }

    private static readonly string[] metrics = ["mae"];

    /// <summary>
    /// Compile model with appropriate loss functions and metrics
    /// </summary>
    public static void CompileModel(IModel model, float learningRate = 0.001f)
    {
        var optimizer = keras.optimizers.Adam(learning_rate: learningRate);

        model.compile(
            optimizer: optimizer,
            loss: keras.losses.MeanSquaredError(),
            metrics: metrics
        );
    }

    /// <summary>
    /// Compile model with attention outputs for training (same as CompileModel for now)
    /// </summary>
    public static void CompileModelWithAttention(IModel model, float learningRate = 0.001f)
    {
        CompileModel(model, learningRate);
    }
}

/// <summary>
/// Helper class to interpret feature importance after inference.
/// Since TensorFlow.NET doesn't support extracting attention weights directly,
/// we use gradient-based methods or analyze learned weights for interpretability.
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
        return featureImportance
            .OrderByDescending(f => f.Score)
            .Take(topN)
            .Select(f => (f.Name, f.Score))
            .ToList();
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
        return featureImportance
            .OrderBy(f => f.Score)
            .Take(topN)
            .Select(f => (f.Name, f.Score))
            .ToList();
    }
}
