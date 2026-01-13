using Dao.AI.BreakPoint.Data.Enums;
using Dao.AI.BreakPoint.Services.MoveNet;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace Dao.AI.BreakPoint.Services.SwingAnalyzer;

/// <summary>
/// Inference service for AI-based swing phase classification.
/// Uses an ONNX model trained on labeled pose data to classify frames
/// into swing phases (None, Preparation, Backswing, Swing, FollowThrough).
/// </summary>
public class SwingPhaseClassifierService : IDisposable
{
    private readonly InferenceSession _session;
    private bool _disposed;

    // Feature dimensions (must match training - using pose-relative features)
    private const int AngleFeatures = 8;
    private const int RelativePositionFeatures = 12; // 6 key joints × 2 (relative x, y)
    private const int VelocityFeatures = 6;
    private const int ArmConfigFeatures = 4;
    private const int FeaturesPerFrame =
        AngleFeatures + RelativePositionFeatures + VelocityFeatures + ArmConfigFeatures; // 30
    private const int TotalFeatures = (FeaturesPerFrame * 3) + 1; // 3 frames + handedness = 91

    // Key joint indices
    private const int LeftShoulder = 5;
    private const int RightShoulder = 6;
    private const int LeftElbow = 7;
    private const int RightElbow = 8;
    private const int LeftWrist = 9;
    private const int RightWrist = 10;
    private const int LeftHip = 11;
    private const int RightHip = 12;

    /// <summary>
    /// Creates a new SwingPhaseClassifierService.
    /// </summary>
    /// <param name="modelPath">Path to the ONNX model file. Required.</param>
    /// <exception cref="ArgumentException">Thrown when modelPath is null or empty.</exception>
    /// <exception cref="FileNotFoundException">Thrown when model file doesn't exist.</exception>
    public SwingPhaseClassifierService(string modelPath)
    {
        if (string.IsNullOrEmpty(modelPath))
        {
            throw new ArgumentException(
                "Model path is required for swing phase classification.",
                nameof(modelPath)
            );
        }

        if (!File.Exists(modelPath))
        {
            throw new FileNotFoundException(
                $"Swing phase classifier model not found: {modelPath}",
                modelPath
            );
        }

        var sessionOptions = new SessionOptions();
        sessionOptions.GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_EXTENDED;
        _session = new InferenceSession(modelPath, sessionOptions);
    }

    /// <summary>
    /// Classify the swing phase for a frame using the AI model.
    /// </summary>
    public SwingPhaseClassificationResult ClassifyPhase(
        JointData[] keypoints,
        float[] angles,
        bool isRightHanded,
        FrameData? prevFrame = null,
        FrameData? prev2Frame = null
    )
    {
        var features = ExtractFeatures(keypoints, angles, isRightHanded, prevFrame, prev2Frame);
        var inputTensor = CreateInputTensor(features);

        // ML.NET exported ONNX requires both Features and Label inputs
        // Shape must be [1, 1] (batch_size, 1)
        var labelTensor = new DenseTensor<uint>(new uint[] { 0 }, [1, 1]);

        var inputs = new List<NamedOnnxValue>
        {
            NamedOnnxValue.CreateFromTensor("Features", inputTensor),
            NamedOnnxValue.CreateFromTensor("Label", labelTensor),
        };

        using var results = _session.Run(inputs);

        var predictedLabelOutput = results.FirstOrDefault(r => r.Name == "PredictedLabel.output");
        uint predictedLabel = predictedLabelOutput?.AsTensor<uint>()[0] ?? 0;

        var scoreOutput = results.FirstOrDefault(r => r.Name == "Score.output");
        float[] probabilities = scoreOutput?.AsTensor<float>().ToArray() ?? [];

        float confidence = probabilities.Max();

        return new SwingPhaseClassificationResult
        {
            Phase = IndexToPhase((int)predictedLabel),
            Confidence = confidence,
            Probabilities = probabilities,
        };
    }

    /// <summary>
    /// Extract features from frame data for model input.
    /// Uses pose-relative features (positions relative to torso) for better generalization.
    /// </summary>
    private static float[] ExtractFeatures(
        JointData[] keypoints,
        float[] angles,
        bool isRightHanded,
        FrameData? prevFrame,
        FrameData? prev2Frame
    )
    {
        var features = new List<float>();

        // Current frame features
        AddFrameFeatures(features, keypoints, angles, isRightHanded);

        // Previous frame features (or zeros if not available)
        if (prevFrame != null)
        {
            AddFrameFeatures(
                features,
                prevFrame.Joints,
                GetAnglesFromFrameData(prevFrame),
                isRightHanded
            );
        }
        else
        {
            AddZeroFrameFeatures(features);
        }

        // Previous-previous frame features (or zeros if not available)
        if (prev2Frame != null)
        {
            AddFrameFeatures(
                features,
                prev2Frame.Joints,
                GetAnglesFromFrameData(prev2Frame),
                isRightHanded
            );
        }
        else
        {
            AddZeroFrameFeatures(features);
        }

        // Handedness (constant across frames)
        features.Add(isRightHanded ? 1.0f : 0.0f);

        return [.. features];
    }

    private static void AddFrameFeatures(
        List<float> features,
        JointData[] keypoints,
        float[] angles,
        bool isRightHanded
    )
    {
        // 1. Joint angles (8 features) - these are pose-invariant
        foreach (var angle in angles)
        {
            features.Add(Sanitize(angle / 180.0f));
        }

        // Calculate hip center as reference point
        var hipCenterX = (keypoints[LeftHip].X + keypoints[RightHip].X) / 2;
        var hipCenterY = (keypoints[LeftHip].Y + keypoints[RightHip].Y) / 2;

        // Calculate torso scale (for normalization)
        var shoulderCenterY = (keypoints[LeftShoulder].Y + keypoints[RightShoulder].Y) / 2;
        var torsoHeight = Math.Max(0.1f, Math.Abs(hipCenterY - shoulderCenterY));

        // 2. Relative positions of key joints (12 features)
        int[] relativeJoints =
        [
            LeftWrist,
            RightWrist,
            LeftElbow,
            RightElbow,
            LeftShoulder,
            RightShoulder,
        ];
        foreach (var jointIdx in relativeJoints)
        {
            var relX = (keypoints[jointIdx].X - hipCenterX) / torsoHeight;
            var relY = (keypoints[jointIdx].Y - hipCenterY) / torsoHeight;
            features.Add(Sanitize(relX));
            features.Add(Sanitize(relY));
        }

        // 3. Key velocities (6 features)
        int[] velocityJoints =
        [
            LeftWrist,
            RightWrist,
            LeftElbow,
            RightElbow,
            LeftShoulder,
            RightShoulder,
        ];
        foreach (var jointIdx in velocityJoints)
        {
            var speed = keypoints[jointIdx].Speed ?? 0;
            features.Add(Sanitize(speed / 500.0f));
        }

        // 4. Arm configuration features (4 features)
        int dominantWrist = isRightHanded ? RightWrist : LeftWrist;
        int dominantElbow = isRightHanded ? RightElbow : LeftElbow;
        int dominantShoulder = isRightHanded ? RightShoulder : LeftShoulder;

        var wristToShoulderX = keypoints[dominantWrist].X - keypoints[dominantShoulder].X;
        var wristToShoulderY = keypoints[dominantWrist].Y - keypoints[dominantShoulder].Y;
        features.Add(Sanitize(wristToShoulderX / torsoHeight));
        features.Add(Sanitize(wristToShoulderY / torsoHeight));

        var elbowToHipX = keypoints[dominantElbow].X - hipCenterX;
        features.Add(Sanitize(elbowToHipX / torsoHeight));

        var wristHeightDiff = keypoints[dominantWrist].Y - keypoints[dominantShoulder].Y;
        features.Add(Sanitize(wristHeightDiff / torsoHeight));
    }

    private static void AddZeroFrameFeatures(List<float> features)
    {
        for (int i = 0; i < FeaturesPerFrame; i++)
        {
            features.Add(0.0f);
        }
    }

    private static float Sanitize(float value)
    {
        if (float.IsNaN(value) || float.IsInfinity(value))
            return 0f;
        return Math.Clamp(value, -10f, 10f);
    }

    private static float[] GetAnglesFromFrameData(FrameData frame)
    {
        return
        [
            frame.LeftElbowAngle,
            frame.RightElbowAngle,
            frame.LeftShoulderAngle,
            frame.RightShoulderAngle,
            frame.LeftHipAngle,
            frame.RightHipAngle,
            frame.LeftKneeAngle,
            frame.RightKneeAngle,
        ];
    }

    private static DenseTensor<float> CreateInputTensor(float[] features)
    {
        var tensor = new DenseTensor<float>([1, features.Length]);
        for (int i = 0; i < features.Length; i++)
        {
            var val = features[i];
            tensor[0, i] = (float.IsNaN(val) || float.IsInfinity(val)) ? 0f : val;
        }
        return tensor;
    }

    private static SwingPhase IndexToPhase(int index) =>
        index switch
        {
            0 => SwingPhase.None,
            1 => SwingPhase.Preparation,
            2 => SwingPhase.Backswing,
            3 => SwingPhase.Contact,
            4 => SwingPhase.FollowThrough,
            _ => SwingPhase.None,
        };

    public void Dispose()
    {
        if (!_disposed)
        {
            _session.Dispose();
            _disposed = true;
        }
        GC.SuppressFinalize(this);
    }
}

/// <summary>
/// Result from swing phase classification
/// </summary>
public class SwingPhaseClassificationResult
{
    /// <summary>
    /// The predicted swing phase
    /// </summary>
    public required SwingPhase Phase { get; set; }

    /// <summary>
    /// Confidence score for the prediction (0-1)
    /// </summary>
    public required float Confidence { get; set; }

    /// <summary>
    /// Probability distribution across all classes [None, Prep, Back, Swing, Follow]
    /// </summary>
    public required float[] Probabilities { get; set; }
}
