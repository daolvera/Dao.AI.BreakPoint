using Dao.AI.BreakPoint.Services;
using Dao.AI.BreakPoint.Services.MoveNet;
using Dao.AI.BreakPoint.Services.SwingAnalyzer;
using System.Numerics;
using Tensorflow.NumPy;

namespace Dao.AI.BreakPoint.ModelTraining;

/// <summary>
/// Service for training a 1D CNN model to analyze tennis swing technique and predict USTA ratings.
/// 
/// Training Process:
/// 1. Takes List<ProcessedSwingVideo> containing:
///    - Multiple swings per video with pose keypoint data from MoveNet
///    - USTA rating (1.0-7.0) for the player in the video
///    - Frame rate and image dimensions
/// 
/// 2. For each swing, extracts 66 features per frame:
///    - 24 velocity/acceleration values (12 joints × 2)
///    - 8 joint angles (elbows, shoulders, hips, knees)
///    - 34 position coordinates (17 keypoints × 2)
/// 
/// 3. Normalizes sequences to fixed length (default 90 frames = 3 seconds at 30 FPS)
/// 
/// 4. Trains 1D CNN to output 6 values:
///    - Overall rating
///    - Shoulder technique score
///    - Contact point technique score  
///    - Preparation technique score
///    - Balance technique score
///    - Follow-through technique score
/// 
/// Usage:
///   var trainingService = new SwingModelTrainingService(poseFeatureExtractor);
///   var modelPath = await trainingService.TrainTensorFlowModelAsync(videos, config);
/// </summary>
internal class SwingModelTrainingService(IPoseFeatureExtractorService PoseFeatureExtractorService)
{
    private const float MIN_CONFIDENCE = 0.2f;

    public async Task<string> TrainTensorFlowModelAsync(
        List<ProcessedSwingVideo> processedSwingVideos,
        TrainingConfiguration config
    )
    {
        // Validate input parameters
        ValidateTrainingInputs(processedSwingVideos, config);

        Console.WriteLine("Starting data preprocessing...");
        var (inputData, targetData) = await PreprocessTrainingDataAsync(processedSwingVideos, config);

        Console.WriteLine($"Preprocessed {inputData.shape[0]} training samples");
        Console.WriteLine($"Input shape: {inputData.shape}");
        Console.WriteLine($"Target shape: {targetData.shape}");

        // Validate preprocessed data
        ValidatePreprocessedData(inputData, targetData, config);

        var model = SwingCnnModel.BuildSingleOutputModel(
            config.SequenceLength,
            config.NumFeatures
        );
        SwingCnnModel.CompileModel(model, config.LearningRate);

        Console.WriteLine("Training CNN model for USTA rating prediction...");
        Console.WriteLine($"Model architecture: {config.SequenceLength} timesteps × {config.NumFeatures} features → 6 outputs");

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

    private static void ValidateTrainingInputs(List<ProcessedSwingVideo> videos, TrainingConfiguration? config)
    {
        if (videos.Count == 0)
        {
            throw new ArgumentException("Training videos list cannot be empty.", nameof(videos));
        }

        ArgumentNullException.ThrowIfNull(config);

        // Validate video data
        var totalSwings = 0;
        var validVideos = 0;

        foreach (var video in videos)
        {
            if (video.UstaRating < 1.0 || video.UstaRating > 7.0)
            {
                Console.WriteLine($"Warning: Video has invalid USTA rating {video.UstaRating}. Should be between 1.0 and 7.0.");
                continue;
            }

            if (video.Swings == null || video.Swings.Count == 0)
            {
                Console.WriteLine("Warning: Video contains no swing data.");
                continue;
            }

            foreach (var swing in video.Swings)
            {
                if (swing.Frames == null || swing.Frames.Count == 0)
                {
                    Console.WriteLine("Warning: Swing contains no frame data.");
                    continue;
                }

                // Check if swing has reasonable number of frames (at least 1 second)
                if (swing.Frames.Count < video.FrameRate)
                {
                    Console.WriteLine($"Warning: Swing has only {swing.Frames.Count} frames, expected at least {video.FrameRate}.");
                }

                totalSwings++;
            }

            validVideos++;
        }

        if (validVideos == 0)
        {
            throw new ArgumentException("No valid training videos found.");
        }

        if (totalSwings < config.BatchSize)
        {
            throw new ArgumentException($"Not enough training data. Found {totalSwings} swings, but batch size is {config.BatchSize}.");
        }

        Console.WriteLine($"Validation passed: {validVideos} videos, {totalSwings} total swings");
    }

    private static void ValidatePreprocessedData(NDArray inputData, NDArray targetData, TrainingConfiguration config)
    {
        if (inputData.shape.Length != 3)
        {
            throw new InvalidOperationException($"Input data should have 3 dimensions (batch, sequence, features), got {inputData.shape.Length}");
        }

        if (inputData.shape[1] != config.SequenceLength)
        {
            throw new InvalidOperationException($"Input sequence length mismatch. Expected {config.SequenceLength}, got {inputData.shape[1]}");
        }

        if (inputData.shape[2] != config.NumFeatures)
        {
            throw new InvalidOperationException($"Input feature count mismatch. Expected {config.NumFeatures}, got {inputData.shape[2]}");
        }

        if (targetData.shape.Length != 2)
        {
            throw new InvalidOperationException($"Target data should have 2 dimensions (batch, outputs), got {targetData.shape.Length}");
        }

        if (inputData.shape[0] != targetData.shape[0])
        {
            throw new InvalidOperationException($"Batch size mismatch. Input: {inputData.shape[0]}, Target: {targetData.shape[0]}");
        }

        if (targetData.shape[1] != 6)
        {
            throw new InvalidOperationException($"Target should have 6 outputs, got {targetData.shape[1]}");
        }

        Console.WriteLine("Data validation passed");
    }

    private async Task<(NDArray inputArray, NDArray targetArray)> PreprocessTrainingDataAsync(
        List<ProcessedSwingVideo> processedSwingVideos,
        TrainingConfiguration config)
    {
        var allInputSequences = new List<float[,]>();
        var allTargets = new List<float[]>();

        foreach (var video in processedSwingVideos)
        {
            foreach (var swing in video.Swings)
            {
                var processedSequence = await ProcessSwingSequenceAsync(swing, video, config);
                if (processedSequence != null)
                {
                    allInputSequences.Add(processedSequence);

                    // Create target vector: [overall_rating, shoulder_score, contact_score, prep_score, balance_score, follow_score]
                    var targets = new float[]
                    {
                        (float)video.UstaRating,  // Overall rating
                        (float)video.UstaRating * 0.9f,  // Shoulder technique (slightly lower)
                        (float)video.UstaRating * 0.95f, // Contact technique
                        (float)video.UstaRating * 0.85f, // Preparation technique
                        (float)video.UstaRating * 0.8f,  // Balance technique
                        (float)video.UstaRating * 0.9f   // Follow-through technique
                    };
                    allTargets.Add(targets);
                }
            }
        }

        if (allInputSequences.Count == 0)
        {
            throw new InvalidOperationException("No valid training sequences found in the provided data.");
        }

        // Convert to NDArrays
        var inputArray = ConvertToNDArray(allInputSequences, config.SequenceLength, config.NumFeatures);
        var targetArray = ConvertTargetsToNDArray(allTargets);

        return (inputArray, targetArray);
    }

    private async Task<float[,]?> ProcessSwingSequenceAsync(
        SwingData swing,
        ProcessedSwingVideo video,
        TrainingConfiguration config)
    {
        try
        {
            var frameFeatures = new List<float[]>();
            Vector2[]? prev2Positions = null;
            Vector2[]? prevPositions = null;

            foreach (var frame in swing.Frames)
            {
                var (currentPositions, confidences) = MoveNetPoseFeatureExtractorService.KeypointsToPixels(
                    frame, video.ImageHeight, video.ImageWidth);

                // Check if pose detection is reasonable
                var validKeypoints = confidences.Count(c => c > MIN_CONFIDENCE);
                if (validKeypoints < 8) // Need at least 8 keypoints with good confidence
                {
                    Console.WriteLine($"Warning: Frame has only {validKeypoints} valid keypoints out of 17");
                    // Still process the frame but with degraded quality
                }

                // Build frame features using the pose feature extractor
                var features = PoseFeatureExtractorService.BuildFrameFeatures(
                    prev2Positions,
                    prevPositions,
                    currentPositions,
                    confidences,
                    1.0f / video.FrameRate);

                // Validate features
                if (features == null || features.Length != config.NumFeatures)
                {
                    Console.WriteLine($"Warning: Feature extraction produced {features?.Length ?? 0} features, expected {config.NumFeatures}");
                    continue;
                }

                frameFeatures.Add(features);

                // Update position history
                prev2Positions = prevPositions;
                prevPositions = currentPositions;
            }

            if (frameFeatures.Count == 0)
            {
                Console.WriteLine("Warning: No valid frame features extracted from swing");
                return null;
            }

            if (frameFeatures.Count < config.SequenceLength / 3)
            {
                Console.WriteLine($"Warning: Swing has only {frameFeatures.Count} frames, might be too short for reliable analysis");
                // Still process but flag as potentially problematic
            }

            return NormalizeAndPadSequence(frameFeatures, config);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error processing swing sequence: {ex.Message}");
            return null;
        }
    }

    private float[,] NormalizeAndPadSequence(List<float[]> frameFeatures, TrainingConfiguration config)
    {
        var sequenceLength = config.SequenceLength;
        var numFeatures = config.NumFeatures;
        var normalizedSequence = new float[sequenceLength, numFeatures];

        // Pad or truncate to target sequence length
        var actualLength = Math.Min(frameFeatures.Count, sequenceLength);

        for (int frameIdx = 0; frameIdx < actualLength; frameIdx++)
        {
            var features = frameFeatures[frameIdx];
            for (int featIdx = 0; featIdx < Math.Min(features.Length, numFeatures); featIdx++)
            {
                var value = features[featIdx];
                // Handle NaN values by setting them to 0, and apply basic normalization
                if (float.IsNaN(value) || float.IsInfinity(value))
                {
                    normalizedSequence[frameIdx, featIdx] = 0.0f;
                }
                else
                {
                    // Simple clipping to prevent extreme values
                    normalizedSequence[frameIdx, featIdx] = Math.Max(-1000.0f, Math.Min(1000.0f, value));
                }
            }
        }

        // If sequence is shorter than required, the rest will remain 0 (padding)
        // If sequence is longer, it gets truncated

        return normalizedSequence;
    }

    private NDArray ConvertToNDArray(List<float[,]> sequences, int sequenceLength, int numFeatures)
    {
        var batchSize = sequences.Count;
        var data = new float[batchSize, sequenceLength, numFeatures];

        for (int i = 0; i < batchSize; i++)
        {
            var sequence = sequences[i];
            for (int j = 0; j < sequenceLength; j++)
            {
                for (int k = 0; k < numFeatures; k++)
                {
                    data[i, j, k] = sequence[j, k];
                }
            }
        }

        return np.array(data);
    }

    private NDArray ConvertTargetsToNDArray(List<float[]> targets)
    {
        var batchSize = targets.Count;
        var numOutputs = targets[0].Length;
        var data = new float[batchSize, numOutputs];

        for (int i = 0; i < batchSize; i++)
        {
            for (int j = 0; j < numOutputs; j++)
            {
                data[i, j] = targets[i][j];
            }
        }

        return np.array(data);
    }
}
