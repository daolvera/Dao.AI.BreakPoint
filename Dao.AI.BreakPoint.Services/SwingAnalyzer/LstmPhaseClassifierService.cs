using Dao.AI.BreakPoint.Data.Enums;
using Dao.AI.BreakPoint.Services.MoveNet;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace Dao.AI.BreakPoint.Services.SwingAnalyzer;

/// <summary>
/// LSTM-based swing phase classifier that processes variable-length frame sequences.
/// Uses 20 features per frame extracted by LstmFeatureExtractor.
/// Exports predictions for each frame in the sequence.
/// </summary>
public class LstmPhaseClassifierService : IDisposable
{
    private readonly InferenceSession _session;
    private bool _disposed;

    /// <summary>
    /// Number of swing phase classes (None, Preparation, Backswing, Contact, FollowThrough)
    /// </summary>
    public const int NumClasses = 5;

    /// <summary>
    /// Creates a new LSTM phase classifier service.
    /// </summary>
    /// <param name="modelPath">Path to the ONNX model file.</param>
    public LstmPhaseClassifierService(string modelPath)
    {
        if (string.IsNullOrEmpty(modelPath))
        {
            throw new ArgumentException("Model path is required.", nameof(modelPath));
        }

        if (!File.Exists(modelPath))
        {
            throw new FileNotFoundException(
                $"LSTM classifier model not found: {modelPath}",
                modelPath
            );
        }

        var sessionOptions = new SessionOptions
        {
            GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_EXTENDED,
        };
        _session = new InferenceSession(modelPath, sessionOptions);
    }

    /// <summary>
    /// Classify swing phases for an entire sequence of frames.
    /// </summary>
    /// <param name="frameSequence">List of frame data from MoveNet processing.</param>
    /// <param name="isRightHanded">Whether the player is right-handed.</param>
    /// <returns>Classification result for each frame in the sequence.</returns>
    public List<LstmPhaseClassificationResult> ClassifySequence(
        List<FrameData> frameSequence,
        bool isRightHanded
    )
    {
        if (frameSequence.Count == 0)
        {
            return [];
        }

        // Extract features for each frame
        var featureSequence = new List<float[]>();
        FrameData? prevFrame = null;

        foreach (var frame in frameSequence)
        {
            var features = LstmFeatureExtractor.ExtractFeatures(frame, prevFrame, isRightHanded);
            featureSequence.Add(features);
            prevFrame = frame;
        }

        // Create input tensor [1, seqLen, 20]
        var seqLen = featureSequence.Count;
        var inputTensor = new DenseTensor<float>([1, seqLen, LstmFeatureExtractor.FeatureCount]);

        for (int t = 0; t < seqLen; t++)
        {
            for (int f = 0; f < LstmFeatureExtractor.FeatureCount; f++)
            {
                inputTensor[0, t, f] = featureSequence[t][f];
            }
        }

        // Run inference
        var inputs = new List<NamedOnnxValue>
        {
            NamedOnnxValue.CreateFromTensor("input", inputTensor),
        };

        using var results = _session.Run(inputs);

        // Output shape: [1, seqLen, NumClasses]
        var output = results.First().AsTensor<float>();
        var classificationResults = new List<LstmPhaseClassificationResult>();

        for (int t = 0; t < seqLen; t++)
        {
            var probabilities = new float[NumClasses];
            float maxProb = float.MinValue;
            int maxIdx = 0;

            // Apply softmax to get probabilities
            float sum = 0;
            for (int c = 0; c < NumClasses; c++)
            {
                probabilities[c] = MathF.Exp(output[0, t, c]);
                sum += probabilities[c];
            }

            for (int c = 0; c < NumClasses; c++)
            {
                probabilities[c] /= sum;
                if (probabilities[c] > maxProb)
                {
                    maxProb = probabilities[c];
                    maxIdx = c;
                }
            }

            classificationResults.Add(
                new LstmPhaseClassificationResult
                {
                    FrameIndex = t,
                    Phase = IndexToPhase(maxIdx),
                    Confidence = maxProb,
                    Probabilities = probabilities,
                }
            );
        }

        return classificationResults;
    }

    /// <summary>
    /// Classify a single frame with temporal context from previous frames.
    /// Uses a sliding window approach for real-time classification.
    /// </summary>
    /// <param name="currentFrame">Current frame data.</param>
    /// <param name="previousFrames">Previous frames for context (most recent first).</param>
    /// <param name="isRightHanded">Whether the player is right-handed.</param>
    /// <returns>Classification result for the current frame.</returns>
    public LstmPhaseClassificationResult ClassifyFrame(
        FrameData currentFrame,
        List<FrameData> previousFrames,
        bool isRightHanded
    )
    {
        // Build sequence with context window
        var sequence = new List<FrameData>();

        // Add previous frames (in chronological order)
        if (previousFrames.Count > 0)
        {
            // previousFrames is most recent first, reverse for chronological
            var contextFrames = previousFrames.Take(15).Reverse().ToList();
            sequence.AddRange(contextFrames);
        }

        sequence.Add(currentFrame);

        var results = ClassifySequence(sequence, isRightHanded);

        // Return result for the last frame (current)
        return results.Last();
    }

    /// <summary>
    /// Post-process classifications to smooth transitions and enforce temporal consistency.
    /// </summary>
    /// <param name="classifications">Raw classifications from LSTM.</param>
    /// <param name="minPhaseDuration">Minimum frames a phase should last.</param>
    /// <returns>Smoothed classifications.</returns>
    public static List<LstmPhaseClassificationResult> SmoothClassifications(
        List<LstmPhaseClassificationResult> classifications,
        int minPhaseDuration = 3
    )
    {
        if (classifications.Count == 0)
            return classifications;

        var smoothed = new List<LstmPhaseClassificationResult>(classifications);

        // Remove spurious single-frame transitions
        for (int i = 1; i < smoothed.Count - 1; i++)
        {
            var prev = smoothed[i - 1].Phase;
            var curr = smoothed[i].Phase;
            var next = smoothed[i + 1].Phase;

            // If current differs from both neighbors, and neighbors agree, correct it
            if (curr != prev && curr != next && prev == next)
            {
                smoothed[i] = smoothed[i] with { Phase = prev };
            }
        }

        // Enforce minimum phase duration
        int runStart = 0;
        var currentPhase = smoothed[0].Phase;

        for (int i = 1; i <= smoothed.Count; i++)
        {
            bool endOfRun = i == smoothed.Count || smoothed[i].Phase != currentPhase;

            if (endOfRun)
            {
                int runLength = i - runStart;

                if (runLength < minPhaseDuration && runStart > 0)
                {
                    // Short run - merge with previous phase
                    var prevPhase = smoothed[runStart - 1].Phase;
                    for (int j = runStart; j < i && j < smoothed.Count; j++)
                    {
                        smoothed[j] = smoothed[j] with { Phase = prevPhase };
                    }
                }

                if (i < smoothed.Count)
                {
                    runStart = i;
                    currentPhase = smoothed[i].Phase;
                }
            }
        }

        return smoothed;
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
/// Result from LSTM-based swing phase classification.
/// </summary>
public record LstmPhaseClassificationResult
{
    /// <summary>
    /// Index of the frame in the sequence.
    /// </summary>
    public required int FrameIndex { get; init; }

    /// <summary>
    /// The predicted swing phase.
    /// </summary>
    public required SwingPhase Phase { get; init; }

    /// <summary>
    /// Confidence score for the prediction (0-1).
    /// </summary>
    public required float Confidence { get; init; }

    /// <summary>
    /// Probability distribution across all classes [None, Prep, Back, Contact, Follow].
    /// </summary>
    public required float[] Probabilities { get; init; }
}
