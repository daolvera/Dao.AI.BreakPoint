using Dao.AI.BreakPoint.Data.Enums;
using Dao.AI.BreakPoint.Services.SwingAnalyzer;
using TorchSharp;
using TorchSharp.Modules;
using static TorchSharp.torch;
using static TorchSharp.torch.nn;

namespace Dao.AI.BreakPoint.ModelTraining;

/// <summary>
/// TorchSharp MLP model for phase-specific quality scoring.
/// Architecture: MLP(60 → 128 → 64 → 1) with ReLU activations and dropout.
/// Input: 20 features × 3 stats (mean, std, range) = 60 aggregated features per phase.
/// </summary>
public class PhaseQualityMlpModel : Module<Tensor, Tensor>
{
    private readonly Linear _fc1;
    private readonly Linear _fc2;
    private readonly Linear _fc3;
    private readonly Dropout _dropout1;
    private readonly Dropout _dropout2;
    private readonly BatchNorm1d _bn1;
    private readonly BatchNorm1d _bn2;

    public const int InputSize = LstmFeatureExtractor.FeatureCount * 3; // 20 × 3 = 60
    public const int Hidden1Size = 128;
    public const int Hidden2Size = 64;
    public const int OutputSize = 1;

    public PhaseQualityMlpModel(string name = "PhaseQualityMlp")
        : base(name)
    {
        _fc1 = Linear(InputSize, Hidden1Size);
        _bn1 = BatchNorm1d(Hidden1Size);
        _dropout1 = Dropout(0.3);

        _fc2 = Linear(Hidden1Size, Hidden2Size);
        _bn2 = BatchNorm1d(Hidden2Size);
        _dropout2 = Dropout(0.2);

        _fc3 = Linear(Hidden2Size, OutputSize);

        RegisterComponents();
    }

    public override Tensor forward(Tensor x)
    {
        x = _fc1.forward(x);
        x = _bn1.forward(x);
        x = functional.relu(x);
        x = _dropout1.forward(x);

        x = _fc2.forward(x);
        x = _bn2.forward(x);
        x = functional.relu(x);
        x = _dropout2.forward(x);

        x = _fc3.forward(x);
        // Output is 0-100 score, use sigmoid × 100
        x = functional.sigmoid(x) * 100;

        return x;
    }
}

/// <summary>
/// Training configuration for MLP quality models
/// </summary>
public class MlpTrainingConfig
{
    public int Epochs { get; set; } = 200;
    public int BatchSize { get; set; } = 32;
    public float LearningRate { get; set; } = 0.001f;
    public float WeightDecay { get; set; } = 1e-4f;
    public string OutputDirectory { get; set; } = "models";
    public int EarlyStoppingPatience { get; set; } = 30;
    public float ValidationSplit { get; set; } = 0.2f;
}

/// <summary>
/// Trainer for phase-specific quality MLP models.
/// Creates 4 separate models: prep_quality.onnx, backswing_quality.onnx, contact_quality.onnx, followthrough_quality.onnx
/// </summary>
public class PhaseQualityMlpTrainer
{
    private readonly MlpTrainingConfig _config;

    public static readonly SwingPhase[] TrainablePhases =
    [
        SwingPhase.Backswing,
        SwingPhase.Contact,
        SwingPhase.FollowThrough,
    ];

    public static readonly Dictionary<SwingPhase, string> PhaseModelNames = new()
    {
        [SwingPhase.Backswing] = "backswing_quality",
        [SwingPhase.Contact] = "contact_quality",
        [SwingPhase.FollowThrough] = "followthrough_quality",
    };

    public PhaseQualityMlpTrainer(MlpTrainingConfig? config = null)
    {
        _config = config ?? new MlpTrainingConfig();
    }

    /// <summary>
    /// Train all 4 phase quality models
    /// </summary>
    /// <param name="trainingData">Dictionary mapping phase to list of (aggregatedFeatures[60], score) tuples</param>
    public async Task<Dictionary<SwingPhase, string>> TrainAllPhasesAsync(
        Dictionary<SwingPhase, List<(float[] features, float score)>> trainingData
    )
    {
        var modelPaths = new Dictionary<SwingPhase, string>();

        foreach (var phase in TrainablePhases)
        {
            if (!trainingData.TryGetValue(phase, out var phaseData) || phaseData.Count < 10)
            {
                Console.WriteLine(
                    $"Warning: Insufficient data for {phase} phase ({phaseData?.Count ?? 0} samples). Skipping."
                );
                continue;
            }

            Console.WriteLine($"\n{'=', -60}");
            Console.WriteLine($"Training {phase} quality model with {phaseData.Count} samples");
            Console.WriteLine($"{'=', -60}");

            var modelPath = await TrainPhaseModelAsync(phase, phaseData);
            modelPaths[phase] = modelPath;
        }

        return modelPaths;
    }

    /// <summary>
    /// Train a single phase quality model
    /// </summary>
    private async Task<string> TrainPhaseModelAsync(
        SwingPhase phase,
        List<(float[] features, float score)> data
    )
    {
        var modelName = PhaseModelNames[phase];
        var modelPath = Path.Combine(_config.OutputDirectory, $"{modelName}.pt");
        var checkpointPath = Path.Combine(_config.OutputDirectory, $"{modelName}_checkpoint.pt");

        // Split into train/validation
        var shuffled = data.OrderBy(_ => Random.Shared.Next()).ToList();
        int valCount = Math.Max(1, (int)(data.Count * _config.ValidationSplit));
        var valData = shuffled.Take(valCount).ToList();
        var trainData = shuffled.Skip(valCount).ToList();

        Console.WriteLine($"Train: {trainData.Count}, Validation: {valData.Count}");
        PrintScoreDistribution(trainData, "Training");

        // Create model
        var model = new PhaseQualityMlpModel(modelName);
        model.train();

        // Loss and optimizer
        var criterion = MSELoss();
        var optimizer = optim.AdamW(
            model.parameters(),
            lr: _config.LearningRate,
            weight_decay: _config.WeightDecay
        );
        var scheduler = optim.lr_scheduler.ReduceLROnPlateau(optimizer, factor: 0.5, patience: 15);

        float bestValLoss = float.MaxValue;
        int patienceCounter = 0;

        for (int epoch = 0; epoch < _config.Epochs; epoch++)
        {
            // Training
            model.train();
            float trainLoss = 0f;
            int trainBatches = 0;

            foreach (var batch in CreateBatches(trainData, _config.BatchSize))
            {
                optimizer.zero_grad();

                var output = model.forward(batch.features);
                var loss = criterion.forward(output.squeeze(-1), batch.scores);

                loss.backward();
                optimizer.step();

                trainLoss += loss.item<float>();
                trainBatches++;
            }

            float avgTrainLoss = trainLoss / Math.Max(trainBatches, 1);

            // Validation
            model.eval();
            float valLoss = 0f;
            float valMae = 0f;
            int valBatches = 0;

            using (no_grad())
            {
                foreach (var batch in CreateBatches(valData, _config.BatchSize))
                {
                    var output = model.forward(batch.features);
                    var loss = criterion.forward(output.squeeze(-1), batch.scores);

                    valLoss += loss.item<float>();
                    valMae += (output.squeeze(-1) - batch.scores).abs().mean().item<float>();
                    valBatches++;
                }
            }

            float avgValLoss = valLoss / Math.Max(valBatches, 1);
            float avgValMae = valMae / Math.Max(valBatches, 1);

            scheduler.step(avgValLoss);

            if ((epoch + 1) % 20 == 0 || epoch == 0)
            {
                Console.WriteLine(
                    $"Epoch {epoch + 1}/{_config.Epochs}: "
                        + $"Train MSE={avgTrainLoss:F4}, Val MSE={avgValLoss:F4}, Val MAE={avgValMae:F2}"
                );
            }

            // Early stopping
            if (avgValLoss < bestValLoss)
            {
                bestValLoss = avgValLoss;
                patienceCounter = 0;
                model.save(checkpointPath);
            }
            else
            {
                patienceCounter++;
                if (patienceCounter >= _config.EarlyStoppingPatience)
                {
                    Console.WriteLine($"Early stopping at epoch {epoch + 1}");
                    break;
                }
            }
        }

        // Load best model and save final
        model.load(checkpointPath);
        model.eval();

        Console.WriteLine($"Saving model: {modelPath}");
        await SaveModelAsync(model, modelPath);

        // Clean up checkpoint
        if (File.Exists(checkpointPath))
            File.Delete(checkpointPath);

        return modelPath;
    }

    /// <summary>
    /// Prepare training data from video labels and processed swings.
    /// Segments swing frames by phase and computes aggregated features.
    /// </summary>
    public static Dictionary<SwingPhase, List<(float[] features, float score)>> PrepareTrainingData(
        List<(VideoLabel label, List<SwingData> swings)> labeledSwings
    )
    {
        var trainingData = new Dictionary<SwingPhase, List<(float[] features, float score)>>();

        foreach (var phase in TrainablePhases)
        {
            trainingData[phase] = [];
        }

        foreach (var (label, swings) in labeledSwings)
        {
            foreach (var swing in swings)
            {
                // Group frames by phase
                var framesByPhase = swing
                    .Frames.GroupBy(f => f.SwingPhase)
                    .ToDictionary(g => g.Key, g => g.ToList());

                // Extract aggregated features for each phase
                if (
                    framesByPhase.TryGetValue(SwingPhase.Preparation, out var prepFrames)
                    && prepFrames.Count > 0
                )
                {
                    var features = AggregatePhaseFeatures(prepFrames, label.IsRightHanded);
                    trainingData[SwingPhase.Preparation].Add((features, label.PrepScore));
                }

                if (
                    framesByPhase.TryGetValue(SwingPhase.Backswing, out var backswingFrames)
                    && backswingFrames.Count > 0
                )
                {
                    var features = AggregatePhaseFeatures(backswingFrames, label.IsRightHanded);
                    trainingData[SwingPhase.Backswing].Add((features, label.BackswingScore));
                }

                if (
                    framesByPhase.TryGetValue(SwingPhase.Contact, out var contactFrames)
                    && contactFrames.Count > 0
                )
                {
                    var features = AggregatePhaseFeatures(contactFrames, label.IsRightHanded);
                    trainingData[SwingPhase.Contact].Add((features, label.ContactScore));
                }

                if (
                    framesByPhase.TryGetValue(SwingPhase.FollowThrough, out var followFrames)
                    && followFrames.Count > 0
                )
                {
                    var features = AggregatePhaseFeatures(followFrames, label.IsRightHanded);
                    trainingData[SwingPhase.FollowThrough]
                        .Add((features, label.FollowThroughScore));
                }
            }
        }

        return trainingData;
    }

    /// <summary>
    /// Aggregate features from phase frames using mean, std, range statistics.
    /// </summary>
    public static float[] AggregatePhaseFeatures(List<FrameData> frames, bool isRightHanded)
    {
        const int numFeatures = LstmFeatureExtractor.FeatureCount;
        const int numStats = 3; // mean, std, range

        // Extract features for all frames with previous frame context
        var frameFeatures = new List<float[]>();
        FrameData? prevFrame = null;
        foreach (var frame in frames)
        {
            frameFeatures.Add(
                LstmFeatureExtractor.ExtractFeatures(frame, prevFrame, isRightHanded)
            );
            prevFrame = frame;
        }

        var aggregated = new float[numFeatures * numStats];

        for (int f = 0; f < numFeatures; f++)
        {
            var values = frameFeatures
                .Select(ff => ff[f])
                .Where(v => !float.IsNaN(v) && !float.IsInfinity(v))
                .ToList();

            int baseIdx = f * numStats;

            if (values.Count == 0)
            {
                aggregated[baseIdx + 0] = 0f; // mean
                aggregated[baseIdx + 1] = 0f; // std
                aggregated[baseIdx + 2] = 0f; // range
                continue;
            }

            float mean = values.Average();
            float variance = values.Sum(v => (v - mean) * (v - mean)) / values.Count;
            float std = MathF.Sqrt(variance);
            float range = values.Max() - values.Min();

            aggregated[baseIdx + 0] = Sanitize(mean);
            aggregated[baseIdx + 1] = Sanitize(std);
            aggregated[baseIdx + 2] = Sanitize(range);
        }

        return aggregated;
    }

    private static IEnumerable<(Tensor features, Tensor scores)> CreateBatches(
        List<(float[] features, float score)> data,
        int batchSize
    )
    {
        for (int i = 0; i < data.Count; i += batchSize)
        {
            var batch = data.Skip(i).Take(batchSize).ToList();
            int actualBatchSize = batch.Count;

            var featuresData = new float[actualBatchSize, PhaseQualityMlpModel.InputSize];
            var scoresData = new float[actualBatchSize];

            for (int b = 0; b < actualBatchSize; b++)
            {
                var (features, score) = batch[b];
                for (int f = 0; f < PhaseQualityMlpModel.InputSize && f < features.Length; f++)
                {
                    featuresData[b, f] = features[f];
                }
                scoresData[b] = score;
            }

            yield return (tensor(featuresData), tensor(scoresData));
        }
    }

    private static void PrintScoreDistribution(
        List<(float[] features, float score)> data,
        string setName
    )
    {
        var scores = data.Select(d => d.score).ToList();
        Console.WriteLine(
            $"{setName} score distribution: "
                + $"Min={scores.Min():F1}, Max={scores.Max():F1}, "
                + $"Mean={scores.Average():F1}, StdDev={StdDev(scores):F1}"
        );
    }

    private static float StdDev(List<float> values)
    {
        if (values.Count == 0)
            return 0;
        float mean = values.Average();
        float variance = values.Sum(v => (v - mean) * (v - mean)) / values.Count;
        return MathF.Sqrt(variance);
    }

    private static float Sanitize(float value)
    {
        if (float.IsNaN(value) || float.IsInfinity(value))
            return 0f;
        return Math.Clamp(value, -10f, 10f);
    }

    private Task SaveModelAsync(PhaseQualityMlpModel model, string outputPath)
    {
        return Task.Run(() =>
        {
            var outputDir = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(outputDir) && !Directory.Exists(outputDir))
            {
                Directory.CreateDirectory(outputDir);
            }

            model.eval();
            model.save(outputPath);
            Console.WriteLine($"Model saved: {outputPath}");
        });
    }
}
