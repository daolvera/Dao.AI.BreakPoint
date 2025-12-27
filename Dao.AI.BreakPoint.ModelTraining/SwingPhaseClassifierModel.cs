using Tensorflow.Keras.Engine;
using static Tensorflow.KerasApi;

namespace Dao.AI.BreakPoint.ModelTraining;

/// <summary>
/// MLP classifier model for frame-level swing phase detection.
/// Takes pose keypoints, angles, velocities, and accelerations from current
/// and previous frames to classify the swing phase.
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

    // Feature counts per frame
    public const int KeypointFeatures = 17 * 3; // 17 joints × (x, y, confidence) = 51
    public const int AngleFeatures = 8; // 8 joint angles
    public const int VelocityFeatures = 12; // 12 key joint velocities
    public const int AccelerationFeatures = 12; // 12 key joint accelerations
    public const int HandednessFeature = 1; // isRightHanded as 0/1

    /// <summary>
    /// Features per single frame (without temporal context)
    /// </summary>
    public const int FeaturesPerFrame =
        KeypointFeatures + AngleFeatures + VelocityFeatures + AccelerationFeatures;

    /// <summary>
    /// Total features with 3-frame temporal window (current + 2 previous)
    /// Plus handedness which is constant across frames
    /// </summary>
    public const int TotalFeatures = (FeaturesPerFrame * 3) + HandednessFeature; // 84*3 + 1 = 253

    /// <summary>
    /// Build MLP classifier for swing phase detection.
    /// Uses a 3-frame sliding window for temporal context.
    ///
    /// Input: [batch, 253] features (3 frames × 84 features + 1 handedness)
    /// Output: [batch, 5] softmax probabilities for each phase
    /// </summary>
    public static IModel BuildModel()
    {
        var input = keras.Input(shape: TotalFeatures);

        // First hidden layer - learn feature combinations
        var dense1 = keras.layers.Dense(128, activation: "relu").Apply(input);
        var bn1 = keras.layers.BatchNormalization().Apply(dense1);
        var dropout1 = keras.layers.Dropout(0.3f).Apply(bn1);

        // Second hidden layer - higher-level patterns
        var dense2 = keras.layers.Dense(64, activation: "relu").Apply(dropout1);
        var bn2 = keras.layers.BatchNormalization().Apply(dense2);
        var dropout2 = keras.layers.Dropout(0.3f).Apply(bn2);

        // Third hidden layer - phase-specific patterns
        var dense3 = keras.layers.Dense(32, activation: "relu").Apply(dropout2);
        var dropout3 = keras.layers.Dropout(0.2f).Apply(dense3);

        // Output layer - 5-class softmax
        var output = keras.layers.Dense(NumClasses, activation: "softmax").Apply(dropout3);

        var model = keras.Model(inputs: input, outputs: output);
        return model;
    }

    /// <summary>
    /// Compile model with categorical crossentropy loss for multi-class classification
    /// </summary>
    public static void CompileModel(IModel model, float learningRate = 0.001f)
    {
        var optimizer = keras.optimizers.Adam(learning_rate: learningRate);

        model.compile(
            optimizer: optimizer,
            loss: keras.losses.CategoricalCrossentropy(),
            metrics: ["accuracy"]
        );
    }
}

/// <summary>
/// Training data structure for a single labeled frame
/// </summary>
public class LabeledFrameData
{
    /// <summary>
    /// The phase label (0-4)
    /// </summary>
    public required int PhaseLabel { get; set; }

    /// <summary>
    /// Flattened feature vector for this frame (84 features)
    /// </summary>
    public required float[] Features { get; set; }
}

/// <summary>
/// Training configuration specific to phase classifier
/// </summary>
public class PhaseClassifierTrainingConfiguration
{
    public string LabeledFramesDirectory { get; set; } = "data/labeled_frames";
    public string InputModelPath { get; set; } = "movenet/saved_model.pb";
    public string ModelOutputPath { get; set; } = "swing_phase_classifier";
    public int Epochs { get; set; } = 50;
    public int BatchSize { get; set; } = 64;
    public float ValidationSplit { get; set; } = 0.2f;
    public float LearningRate { get; set; } = 0.001f;
}
