using Dao.AI.BreakPoint.Services.SwingAnalyzer;
using Tensorflow.NumPy;

namespace Dao.AI.BreakPoint.ModelTraining;

internal class SwingModelTrainingService()
{
    public async Task<string> TrainTensorFlowModelAsync(
        List<TrainingSwingVideo> processedSwingVideos,
        TrainingConfiguration config
    )
    {
        // Validate input parameters
        ValidateTrainingInputs(processedSwingVideos, config);

        Console.WriteLine("Starting data preprocessing...");
        var (inputData, targetData) = await PreprocessTrainingDataAsync(
            processedSwingVideos,
            config
        );

        Console.WriteLine($"Preprocessed {inputData.shape[0]} training samples");
        Console.WriteLine($"Input shape: {inputData.shape}");
        Console.WriteLine($"Target shape: {targetData.shape}");

        // Validate preprocessed data
        ValidatePreprocessedData(inputData, targetData, config);

        var model = SwingCnnModel.BuildModelWithAttention(
            config.SequenceLength,
            config.NumFeatures
        );
        SwingCnnModel.CompileModel(model, config.LearningRate);

        Console.WriteLine("Training CNN model for USTA rating prediction...");
        Console.WriteLine(
            $"Model architecture: {config.SequenceLength} timesteps × {config.NumFeatures} features → 6 outputs"
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
        List<TrainingSwingVideo> videos,
        TrainingConfiguration? config
    )
    {
        if (videos.Count == 0)
        {
            throw new ArgumentException("Training videos list cannot be empty.", nameof(videos));
        }

        ArgumentNullException.ThrowIfNull(config);

        // Validate video data
        var totalSwings = 0;
        var validVideos = 0;

        foreach (var trainingVideo in videos)
        {
            if (
                trainingVideo.TrainingLabel.QualityScore < 0
                || trainingVideo.TrainingLabel.QualityScore > 100
            )
            {
                Console.WriteLine(
                    $"Warning: Video has invalid quality score {trainingVideo.TrainingLabel.QualityScore}. Should be between 0 and 100."
                );
                continue;
            }

            if (
                trainingVideo.SwingVideo.Swings == null
                || trainingVideo.SwingVideo.Swings.Count == 0
            )
            {
                Console.WriteLine("Warning: Video contains no swing data.");
                continue;
            }

            foreach (var swing in trainingVideo.SwingVideo.Swings)
            {
                if (swing.Frames == null || swing.Frames.Count == 0)
                {
                    Console.WriteLine("Warning: Swing contains no frame data.");
                    continue;
                }

                // Check if swing has reasonable number of frames (at least 1 second)
                if (swing.Frames.Count < trainingVideo.SwingVideo.FrameRate)
                {
                    Console.WriteLine(
                        $"Warning: Swing has only {swing.Frames.Count} frames, expected at least {trainingVideo.SwingVideo.FrameRate}."
                    );
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
            throw new ArgumentException(
                $"Not enough training data. Found {totalSwings} swings, but batch size is {config.BatchSize}."
            );
        }

        Console.WriteLine($"Validation passed: {validVideos} videos, {totalSwings} total swings");
    }

    private static void ValidatePreprocessedData(
        NDArray inputData,
        NDArray targetData,
        TrainingConfiguration config
    )
    {
        if (inputData.shape.Length != 3)
        {
            throw new InvalidOperationException(
                $"Input data should have 3 dimensions (batch, sequence, features), got {inputData.shape.Length}"
            );
        }

        if (inputData.shape[1] != config.SequenceLength)
        {
            throw new InvalidOperationException(
                $"Input sequence length mismatch. Expected {config.SequenceLength}, got {inputData.shape[1]}"
            );
        }

        if (inputData.shape[2] != config.NumFeatures)
        {
            throw new InvalidOperationException(
                $"Input feature count mismatch. Expected {config.NumFeatures}, got {inputData.shape[2]}"
            );
        }

        if (targetData.shape.Length != 2)
        {
            throw new InvalidOperationException(
                $"Target data should have 2 dimensions (batch, outputs), got {targetData.shape.Length}"
            );
        }

        if (inputData.shape[0] != targetData.shape[0])
        {
            throw new InvalidOperationException(
                $"Batch size mismatch. Input: {inputData.shape[0]}, Target: {targetData.shape[0]}"
            );
        }

        if (targetData.shape[1] != 6)
        {
            throw new InvalidOperationException(
                $"Target should have 6 outputs, got {targetData.shape[1]}"
            );
        }

        Console.WriteLine("Data validation passed");
    }

    private static async Task<(
        NDArray inputArray,
        NDArray targetArray
    )> PreprocessTrainingDataAsync(
        List<TrainingSwingVideo> processedSwingVideos,
        TrainingConfiguration config
    )
    {
        var allInputSequences = new List<float[,]>();
        var allTargets = new List<float[]>();

        foreach (var video in processedSwingVideos)
        {
            foreach (var swing in video.SwingVideo.Swings)
            {
                float[,]? processedSequence;
                try
                {
                    processedSequence = await SwingPreprocessingService.PreprocessSwingAsync(
                        swing,
                        config.SequenceLength,
                        config.NumFeatures
                    );
                }
                catch
                {
                    continue;
                }
                if (processedSequence != null)
                {
                    allInputSequences.Add(processedSequence);
                    // Target: quality score (0-100) and sub-component scores derived from it
                    // TODO: Once attention mechanism is added, these sub-scores will come from attention weights
                    var qualityScore = (float)video.TrainingLabel.QualityScore;
                    var targets = new float[]
                    {
                        qualityScore, // Overall quality score
                        qualityScore * 0.9f, // Shoulder rotation score (placeholder)
                        qualityScore * 0.95f, // Contact point score (placeholder)
                        qualityScore * 0.85f, // Preparation score (placeholder)
                        qualityScore * 0.8f, // Balance score (placeholder)
                        qualityScore * 0.9f, // Follow-through score (placeholder)
                    };
                    allTargets.Add(targets);
                }
            }
        }

        if (allInputSequences.Count == 0)
        {
            throw new InvalidOperationException(
                "No valid training sequences found in the provided data."
            );
        }

        var inputArray = ConvertToNDArray(
            allInputSequences,
            config.SequenceLength,
            config.NumFeatures
        );
        var targetArray = ConvertTargetsToNDArray(allTargets);

        return (inputArray, targetArray);
    }

    private static NDArray ConvertToNDArray(
        List<float[,]> sequences,
        int sequenceLength,
        int numFeatures
    )
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

    private static NDArray ConvertTargetsToNDArray(List<float[]> targets)
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
