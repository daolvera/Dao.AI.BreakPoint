using Dao.AI.BreakPoint.Services.MoveNet;
using Dao.AI.BreakPoint.Services.SwingAnalyzer;
using Tensorflow.NumPy;

namespace Dao.AI.BreakPoint.ModelTraining;

/// <summary>
/// Service for training the swing phase classifier model.
/// Handles data preprocessing, training, and model export.
/// </summary>
public class SwingPhaseClassifierTrainingService
{
    /// <summary>
    /// Train the swing phase classifier from labeled frame data
    /// </summary>
    public async Task<string> TrainAsync(
        List<LabeledFrameData> labeledFrames,
        PhaseClassifierTrainingConfiguration config
    )
    {
        ValidateTrainingInputs(labeledFrames, config);

        Console.WriteLine("Preprocessing labeled frame data...");
        var (inputData, targetData) = await PreprocessTrainingDataAsync(labeledFrames);

        Console.WriteLine($"Preprocessed {inputData.shape[0]} training samples");
        Console.WriteLine($"Input shape: {inputData.shape}");
        Console.WriteLine($"Target shape: {targetData.shape}");

        var model = SwingPhaseClassifierModel.BuildModel();
        SwingPhaseClassifierModel.CompileModel(model, config.LearningRate);

        Console.WriteLine("Training swing phase classifier...");
        Console.WriteLine(
            $"Architecture: {SwingPhaseClassifierModel.TotalFeatures} inputs → 5 classes"
        );

        try
        {
            var history = model.fit(
                inputData,
                targetData,
                batch_size: config.BatchSize,
                epochs: config.Epochs,
                validation_split: config.ValidationSplit,
                verbose: 1
            );

            // Ensure output directory exists
            var outputDir = Path.GetDirectoryName(config.ModelOutputPath);
            if (!string.IsNullOrEmpty(outputDir) && !Directory.Exists(outputDir))
            {
                Directory.CreateDirectory(outputDir);
            }

            model.save(config.ModelOutputPath);
            Console.WriteLine($"Model saved to {config.ModelOutputPath}");

            return config.ModelOutputPath;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Model training failed: {ex.Message}", ex);
        }
    }

    private static void ValidateTrainingInputs(
        List<LabeledFrameData> labeledFrames,
        PhaseClassifierTrainingConfiguration config
    )
    {
        if (labeledFrames.Count == 0)
        {
            throw new ArgumentException(
                "Labeled frames list cannot be empty.",
                nameof(labeledFrames)
            );
        }

        ArgumentNullException.ThrowIfNull(config);

        // Count samples per class
        var classCounts = new int[SwingPhaseClassifierModel.NumClasses];
        foreach (var frame in labeledFrames)
        {
            if (frame.PhaseLabel < 0 || frame.PhaseLabel >= SwingPhaseClassifierModel.NumClasses)
            {
                throw new ArgumentException($"Invalid phase label: {frame.PhaseLabel}");
            }
            classCounts[frame.PhaseLabel]++;
        }

        Console.WriteLine("Class distribution:");
        string[] classNames = ["None", "Preparation", "Backswing", "Swing", "FollowThrough"];
        for (int i = 0; i < SwingPhaseClassifierModel.NumClasses; i++)
        {
            Console.WriteLine($"  {classNames[i]}: {classCounts[i]} samples");
        }

        // Warn about class imbalance
        int minSamples = classCounts.Where(c => c > 0).Min();
        int maxSamples = classCounts.Max();
        if (maxSamples > minSamples * 5)
        {
            Console.WriteLine(
                "Warning: Significant class imbalance detected. Consider oversampling minority classes."
            );
        }

        if (labeledFrames.Count < config.BatchSize)
        {
            throw new ArgumentException(
                $"Not enough training data. Found {labeledFrames.Count} samples, but batch size is {config.BatchSize}."
            );
        }

        Console.WriteLine($"Validation passed: {labeledFrames.Count} total samples");
    }

    private static Task<(NDArray inputArray, NDArray targetArray)> PreprocessTrainingDataAsync(
        List<LabeledFrameData> labeledFrames
    )
    {
        return Task.Run(() =>
        {
            int numSamples = labeledFrames.Count;
            int numFeatures = SwingPhaseClassifierModel.TotalFeatures;
            int numClasses = SwingPhaseClassifierModel.NumClasses;

            // Input array: [numSamples, numFeatures]
            var inputData = new float[numSamples, numFeatures];

            // Target array: one-hot encoded [numSamples, numClasses]
            var targetData = new float[numSamples, numClasses];

            for (int i = 0; i < numSamples; i++)
            {
                var frame = labeledFrames[i];

                // Copy features (pad with zeros if needed)
                int featureCount = Math.Min(frame.Features.Length, numFeatures);
                for (int j = 0; j < featureCount; j++)
                {
                    inputData[i, j] = float.IsNaN(frame.Features[j]) ? 0f : frame.Features[j];
                }

                // One-hot encode the label
                targetData[i, frame.PhaseLabel] = 1.0f;
            }

            return (np.array(inputData), np.array(targetData));
        });
    }

    /// <summary>
    /// Extract features from a single frame for training or inference.
    /// This matches the input format expected by the classifier.
    /// </summary>
    public static float[] ExtractFrameFeatures(
        JointData[] keypoints,
        float[] angles,
        bool isRightHanded,
        FrameData? prevFrame = null,
        FrameData? prev2Frame = null
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

        // Joint angles (8 features)
        foreach (var angle in angles)
        {
            features.Add(angle / 180.0f); // Normalize to 0-1 range
        }

        // Velocities for key joints: shoulders, elbows, wrists, hips, knees, ankles (12 features)
        int[] velocityJoints = [5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16];
        foreach (var jointIdx in velocityJoints)
        {
            features.Add((keypoints[jointIdx].Speed ?? 0) / 1000.0f); // Normalize
        }

        // Accelerations for same joints (12 features)
        foreach (var jointIdx in velocityJoints)
        {
            features.Add((keypoints[jointIdx].Acceleration ?? 0) / 10000.0f); // Normalize
        }
    }

    private static void AddZeroFrameFeatures(List<float> features)
    {
        // 51 keypoint features + 8 angles + 12 velocities + 12 accelerations = 83 features
        for (int i = 0; i < SwingPhaseClassifierModel.FeaturesPerFrame; i++)
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
}
