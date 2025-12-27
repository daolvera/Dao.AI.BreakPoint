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

    // Feature dimensions (must match training)
    private const int KeypointFeatures = 17 * 3; // 17 joints × (x, y, confidence) = 51
    private const int AngleFeatures = 8;
    private const int VelocityFeatures = 12;
    private const int AccelerationFeatures = 12;
    private const int FeaturesPerFrame =
        KeypointFeatures + AngleFeatures + VelocityFeatures + AccelerationFeatures; // 83
    private const int TotalFeatures = (FeaturesPerFrame * 3) + 1; // 3 frames + handedness = 250

    // Velocity joints: shoulders(5,6), elbows(7,8), wrists(9,10), hips(11,12), knees(13,14), ankles(15,16)
    private static readonly int[] VelocityJoints = [5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16];

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

        var inputs = new List<NamedOnnxValue>
        {
            NamedOnnxValue.CreateFromTensor("input_1", inputTensor),
        };

        using var results = _session.Run(inputs);
        var outputTensor = results.First().AsTensor<float>();

        // Get probabilities for each class
        float[] probabilities = outputTensor.ToArray();

        // Find class with highest probability
        int predictedClass = 0;
        float maxProb = probabilities[0];
        for (int i = 1; i < probabilities.Length; i++)
        {
            if (probabilities[i] > maxProb)
            {
                maxProb = probabilities[i];
                predictedClass = i;
            }
        }

        return new SwingPhaseClassificationResult
        {
            Phase = IndexToPhase(predictedClass),
            Confidence = maxProb,
            Probabilities = probabilities,
        };
    }

    /// <summary>
    /// Extract features from frame data for model input.
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
        AddFrameFeatures(features, keypoints, angles);

        // Previous frame features (or zeros if not available)
        if (prevFrame != null)
        {
            AddFrameFeatures(features, prevFrame.Joints, GetAnglesFromFrameData(prevFrame));
        }
        else
        {
            AddZeroFrameFeatures(features);
        }

        // Previous-previous frame features (or zeros if not available)
        if (prev2Frame != null)
        {
            AddFrameFeatures(features, prev2Frame.Joints, GetAnglesFromFrameData(prev2Frame));
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
        float[] angles
    )
    {
        // Keypoint features: x, y, confidence for all 17 joints (51 features)
        for (int i = 0; i < MoveNetVideoProcessor.NumKeyPoints; i++)
        {
            features.Add(keypoints[i].X);
            features.Add(keypoints[i].Y);
            features.Add(keypoints[i].Confidence);
        }

        // Joint angles (8 features), normalized
        foreach (var angle in angles)
        {
            features.Add(angle / 180.0f);
        }

        // Velocities for key joints (12 features), normalized
        foreach (var jointIdx in VelocityJoints)
        {
            features.Add((keypoints[jointIdx].Speed ?? 0) / 1000.0f);
        }

        // Accelerations for same joints (12 features), normalized
        foreach (var jointIdx in VelocityJoints)
        {
            features.Add((keypoints[jointIdx].Acceleration ?? 0) / 10000.0f);
        }
    }

    private static void AddZeroFrameFeatures(List<float> features)
    {
        for (int i = 0; i < FeaturesPerFrame; i++)
        {
            features.Add(0.0f);
        }
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
            tensor[0, i] = float.IsNaN(features[i]) ? 0f : features[i];
        }
        return tensor;
    }

    private static SwingPhase IndexToPhase(int index) =>
        index switch
        {
            0 => SwingPhase.None,
            1 => SwingPhase.Preparation,
            2 => SwingPhase.Backswing,
            3 => SwingPhase.Swing,
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
