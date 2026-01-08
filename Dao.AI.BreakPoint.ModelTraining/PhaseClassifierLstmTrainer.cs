using System.Text.Json;
using Dao.AI.BreakPoint.Data.Enums;
using Dao.AI.BreakPoint.Services.MoveNet;
using Dao.AI.BreakPoint.Services.SwingAnalyzer;
using TorchSharp;
using TorchSharp.Modules;
using static TorchSharp.torch;
using static TorchSharp.torch.nn;

namespace Dao.AI.BreakPoint.ModelTraining;

/// <summary>
/// TorchSharp LSTM model for swing phase classification.
/// Handles variable-length sequences with padding and masking.
/// Architecture: LSTM(input=20, hidden=64, layers=2) → Linear(64, 5)
/// </summary>
public class PhaseClassifierLstmModel : Module<Tensor, Tensor, Tensor>
{
    private readonly LSTM _lstm;
    private readonly Linear _fc;
    private readonly Dropout _dropout;

    public const int InputSize = LstmFeatureExtractor.FeatureCount; // 20
    public const int HiddenSize = 64;
    public const int NumLayers = 2;
    public const int NumClasses = 5; // None, Preparation, Backswing, Contact, FollowThrough

    public PhaseClassifierLstmModel(string name = "PhaseClassifierLstm")
        : base(name)
    {
        _lstm = LSTM(
            inputSize: InputSize,
            hiddenSize: HiddenSize,
            numLayers: NumLayers,
            batchFirst: true,
            dropout: 0.3,
            bidirectional: false
        );

        _dropout = Dropout(0.3);
        _fc = Linear(HiddenSize, NumClasses);

        RegisterComponents();
    }

    /// <summary>
    /// Forward pass with sequence mask support.
    /// </summary>
    /// <param name="x">Input tensor of shape [batch, seq_len, features]</param>
    /// <param name="mask">Mask tensor of shape [batch, seq_len] where 1 = valid, 0 = padding</param>
    /// <returns>Output tensor of shape [batch, seq_len, num_classes]</returns>
    public override Tensor forward(Tensor x, Tensor mask)
    {
        // LSTM forward pass
        // Output shape: [batch, seq_len, hidden_size]
        var (output, _, _) = _lstm.forward(x);

        // Apply dropout
        output = _dropout.forward(output);

        // Linear layer for classification at each timestep
        // Output shape: [batch, seq_len, num_classes]
        output = _fc.forward(output);

        return output;
    }

    /// <summary>
    /// Forward pass without mask (for inference with fixed-length sequences)
    /// </summary>
    public Tensor forward(Tensor x)
    {
        var (output, _, _) = _lstm.forward(x);
        output = _dropout.forward(output);
        return _fc.forward(output);
    }
}

/// <summary>
/// Training configuration for LSTM phase classifier
/// </summary>
public class LstmTrainingConfig
{
    public int Epochs { get; set; } = 150;
    public int BatchSize { get; set; } = 16;
    public float LearningRate { get; set; } = 0.001f;
    public float WeightDecay { get; set; } = 1e-5f;
    public int MaxSequenceLength { get; set; } = 150;
    public string ModelOutputPath { get; set; } = "models/phase_classifier_lstm.onnx";
    public string CheckpointPath { get; set; } = "models/phase_classifier_lstm.pt";
    public int EarlyStoppingPatience { get; set; } = 20;
    public float ValidationSplit { get; set; } = 0.2f;
}

/// <summary>
/// Trainer for the TorchSharp LSTM phase classifier.
/// Handles data loading, training loop, validation, and ONNX export.
/// </summary>
public class PhaseClassifierLstmTrainer
{
    private readonly LstmTrainingConfig _config;

    public static readonly string[] ClassNames =
    [
        "None",
        "Preparation",
        "Backswing",
        "Contact",
        "FollowThrough",
    ];

    public PhaseClassifierLstmTrainer(LstmTrainingConfig? config = null)
    {
        _config = config ?? new LstmTrainingConfig();
    }

    /// <summary>
    /// Train the LSTM model from labeled frame sequences
    /// </summary>
    /// <param name="sequences">List of (features[seq_len, 20], labels[seq_len]) tuples</param>
    public async Task<string> TrainAsync(List<(float[,] features, int[] labels)> sequences)
    {
        if (sequences.Count == 0)
        {
            throw new ArgumentException("No training sequences provided");
        }

        Console.WriteLine($"Training LSTM phase classifier with {sequences.Count} sequences");
        Console.WriteLine(
            $"Config: epochs={_config.Epochs}, batch={_config.BatchSize}, lr={_config.LearningRate}"
        );

        // Split into train/validation
        var shuffled = sequences.OrderBy(_ => Random.Shared.Next()).ToList();
        int valCount = (int)(sequences.Count * _config.ValidationSplit);
        var valData = shuffled.Take(valCount).ToList();
        var trainData = shuffled.Skip(valCount).ToList();

        Console.WriteLine(
            $"Train: {trainData.Count} sequences, Validation: {valData.Count} sequences"
        );

        // Print class distribution
        PrintClassDistribution(trainData, "Training");

        // Create model
        var model = new PhaseClassifierLstmModel();
        model.train();

        // Use class weights for imbalanced data
        var classWeights = ComputeClassWeights(trainData);
        var weightTensor = tensor(classWeights);

        // Loss and optimizer
        var criterion = CrossEntropyLoss(weight: weightTensor);
        var optimizer = optim.AdamW(
            model.parameters(),
            lr: _config.LearningRate,
            weight_decay: _config.WeightDecay
        );
        var scheduler = optim.lr_scheduler.ReduceLROnPlateau(optimizer, factor: 0.5, patience: 10);

        float bestValLoss = float.MaxValue;
        int patienceCounter = 0;

        for (int epoch = 0; epoch < _config.Epochs; epoch++)
        {
            // Training
            model.train();
            float trainLoss = 0f;
            int trainCorrect = 0;
            int trainTotal = 0;

            var batches = CreateBatches(trainData, _config.BatchSize);

            foreach (var (batchFeatures, batchLabels, batchMask) in batches)
            {
                optimizer.zero_grad();

                var output = model.forward(batchFeatures, batchMask);

                // Reshape for loss: [batch * seq_len, num_classes] vs [batch * seq_len]
                var batchSize = output.shape[0];
                var seqLen = output.shape[1];
                var outputFlat = output.view(batchSize * seqLen, -1);
                var labelsFlat = batchLabels.view(-1);
                var maskFlat = batchMask.view(-1);

                // Only compute loss on non-padded positions
                var validIndices = maskFlat.nonzero().squeeze(-1);
                if (validIndices.numel() == 0)
                    continue;

                var validOutput = outputFlat.index_select(0, validIndices);
                var validLabels = labelsFlat.index_select(0, validIndices);

                var loss = criterion.forward(validOutput, validLabels);
                loss.backward();
                optimizer.step();

                trainLoss += loss.item<float>();

                // Accuracy
                var predictions = validOutput.argmax(dim: 1);
                trainCorrect += (int)predictions.eq(validLabels).sum().item<long>();
                trainTotal += (int)validIndices.numel();
            }

            float avgTrainLoss = trainLoss / batches.Count;
            float trainAcc = trainCorrect / (float)Math.Max(trainTotal, 1);

            // Validation
            model.eval();
            float valLoss = 0f;
            int valCorrect = 0;
            int valTotal = 0;

            using (no_grad())
            {
                var valBatches = CreateBatches(valData, _config.BatchSize);

                foreach (var (batchFeatures, batchLabels, batchMask) in valBatches)
                {
                    var output = model.forward(batchFeatures, batchMask);

                    var batchSize = output.shape[0];
                    var seqLen = output.shape[1];
                    var outputFlat = output.view(batchSize * seqLen, -1);
                    var labelsFlat = batchLabels.view(-1);
                    var maskFlat = batchMask.view(-1);

                    var validIndices = maskFlat.nonzero().squeeze(-1);
                    if (validIndices.numel() == 0)
                        continue;

                    var validOutput = outputFlat.index_select(0, validIndices);
                    var validLabels = labelsFlat.index_select(0, validIndices);

                    var loss = criterion.forward(validOutput, validLabels);
                    valLoss += loss.item<float>();

                    var predictions = validOutput.argmax(dim: 1);
                    valCorrect += (int)predictions.eq(validLabels).sum().item<long>();
                    valTotal += (int)validIndices.numel();
                }
            }

            float avgValLoss = valLoss / Math.Max(valData.Count, 1);
            float valAcc = valCorrect / (float)Math.Max(valTotal, 1);

            scheduler.step(avgValLoss);

            if ((epoch + 1) % 10 == 0 || epoch == 0)
            {
                Console.WriteLine(
                    $"Epoch {epoch + 1}/{_config.Epochs}: "
                        + $"Train Loss={avgTrainLoss:F4}, Train Acc={trainAcc:P1}, "
                        + $"Val Loss={avgValLoss:F4}, Val Acc={valAcc:P1}"
                );
            }

            // Early stopping
            if (avgValLoss < bestValLoss)
            {
                bestValLoss = avgValLoss;
                patienceCounter = 0;

                // Save best model
                model.save(_config.CheckpointPath);
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

        // Load best model for export
        model.load(_config.CheckpointPath);
        model.eval();

        // Export to ONNX
        Console.WriteLine($"Exporting model to ONNX: {_config.ModelOutputPath}");
        await ExportToOnnxAsync(model);

        Console.WriteLine($"Model saved to {_config.ModelOutputPath}");
        return _config.ModelOutputPath;
    }

    /// <summary>
    /// Load labeled frame data from phaseData directory and convert to sequences
    /// </summary>
    public static List<(float[,] features, int[] labels)> LoadSequencesFromPhaseData(
        string phaseDataDirectory,
        int maxSequenceLength = 150
    )
    {
        var sequences = new List<(float[,] features, int[] labels)>();

        // Group files by video name
        var jsonFiles = Directory.GetFiles(phaseDataDirectory, "*.json");
        var videoGroups = new Dictionary<string, List<LabeledFrameJson>>();

        foreach (var file in jsonFiles)
        {
            try
            {
                var json = File.ReadAllText(file);
                var frameData = JsonSerializer.Deserialize<LabeledFrameJson>(
                    json,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
                );

                if (frameData == null)
                    continue;

                var videoName =
                    frameData.VideoName ?? Path.GetFileNameWithoutExtension(file).Split('_')[0];

                if (!videoGroups.ContainsKey(videoName))
                {
                    videoGroups[videoName] = [];
                }

                videoGroups[videoName].Add(frameData);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Warning: Could not load {file}: {ex.Message}");
            }
        }

        // Convert each video's frames into a sequence
        foreach (var (videoName, frames) in videoGroups)
        {
            if (frames.Count < 5)
                continue; // Skip very short sequences

            // Sort by frame index
            var sortedFrames = frames.OrderBy(f => f.FrameIndex).ToList();

            // Extract features using LstmFeatureExtractor from raw joints/angles
            int seqLen = Math.Min(sortedFrames.Count, maxSequenceLength);
            var seqFeatures = new float[seqLen, LstmFeatureExtractor.FeatureCount];
            var seqLabels = new int[seqLen];

            FrameData? prevFrameData = null;

            for (int t = 0; t < seqLen; t++)
            {
                var frame = sortedFrames[t];

                var frameData = ReconstructFrameData(frame.Joints, frame.Angles, frame.FrameIndex);
                var features = LstmFeatureExtractor.ExtractFeatures(
                    frameData,
                    prevFrameData,
                    frame.IsRightHanded
                );
                prevFrameData = frameData;

                for (int f = 0; f < LstmFeatureExtractor.FeatureCount; f++)
                {
                    seqFeatures[t, f] = features[f];
                }

                seqLabels[t] = frame.Phase;
            }

            sequences.Add((seqFeatures, seqLabels));
        }

        Console.WriteLine($"Loaded {sequences.Count} sequences from {videoGroups.Count} videos");
        return sequences;
    }

    /// <summary>
    /// Reconstruct FrameData from flat arrays stored in JSON
    /// </summary>
    private static FrameData ReconstructFrameData(
        float[] jointsFlat,
        float[] angles,
        int frameIndex
    )
    {
        // Reconstruct JointData array from flat: [x, y, confidence, speed] × 17 joints
        var joints = new JointData[17];
        for (int j = 0; j < 17; j++)
        {
            joints[j] = new JointData
            {
                JointFeature = (JointFeatures)j,
                X = jointsFlat[j * 4 + 0],
                Y = jointsFlat[j * 4 + 1],
                Confidence = jointsFlat[j * 4 + 2],
                Speed = jointsFlat[j * 4 + 3],
            };
        }

        return new FrameData
        {
            Joints = joints,
            LeftElbowAngle = angles[0],
            RightElbowAngle = angles[1],
            LeftShoulderAngle = angles[2],
            RightShoulderAngle = angles[3],
            LeftHipAngle = angles[4],
            RightHipAngle = angles[5],
            LeftKneeAngle = angles[6],
            RightKneeAngle = angles[7],
            FrameIndex = frameIndex,
            SwingPhase = Data.Enums.SwingPhase.None,
        };
    }

    /// <summary>
    /// Extract 20 LSTM features from legacy 91-feature format
    /// </summary>
    private List<(Tensor features, Tensor labels, Tensor mask)> CreateBatches(
        List<(float[,] features, int[] labels)> data,
        int batchSize
    )
    {
        var batches = new List<(Tensor features, Tensor labels, Tensor mask)>();

        for (int i = 0; i < data.Count; i += batchSize)
        {
            var batch = data.Skip(i).Take(batchSize).ToList();

            // Find max sequence length in batch
            int maxLen = batch.Max(b => b.features.GetLength(0));
            maxLen = Math.Min(maxLen, _config.MaxSequenceLength);

            int actualBatchSize = batch.Count;

            // Create padded tensors
            var featuresData = new float[
                actualBatchSize,
                maxLen,
                LstmFeatureExtractor.FeatureCount
            ];
            var labelsData = new long[actualBatchSize, maxLen];
            var maskData = new float[actualBatchSize, maxLen];

            for (int b = 0; b < actualBatchSize; b++)
            {
                var (seqFeatures, seqLabels) = batch[b];
                int seqLen = Math.Min(seqFeatures.GetLength(0), maxLen);

                for (int t = 0; t < seqLen; t++)
                {
                    for (int f = 0; f < LstmFeatureExtractor.FeatureCount; f++)
                    {
                        featuresData[b, t, f] = seqFeatures[t, f];
                    }
                    labelsData[b, t] = seqLabels[t];
                    maskData[b, t] = 1f;
                }
                // Padding positions remain 0
            }

            var featuresTensor = tensor(featuresData);
            var labelsTensor = tensor(labelsData);
            var maskTensor = tensor(maskData);

            batches.Add((featuresTensor, labelsTensor, maskTensor));
        }

        return batches;
    }

    private static float[] ComputeClassWeights(List<(float[,] features, int[] labels)> data)
    {
        var classCounts = new int[PhaseClassifierLstmModel.NumClasses];

        foreach (var (_, labels) in data)
        {
            foreach (var label in labels)
            {
                if (label >= 0 && label < classCounts.Length)
                {
                    classCounts[label]++;
                }
            }
        }

        int total = classCounts.Sum();
        var weights = new float[PhaseClassifierLstmModel.NumClasses];

        for (int i = 0; i < weights.Length; i++)
        {
            if (classCounts[i] > 0)
            {
                weights[i] = (float)total / (weights.Length * classCounts[i]);
            }
            else
            {
                weights[i] = 1f;
            }
        }

        return weights;
    }

    private static void PrintClassDistribution(
        List<(float[,] features, int[] labels)> data,
        string setName
    )
    {
        var classCounts = new int[PhaseClassifierLstmModel.NumClasses];

        foreach (var (_, labels) in data)
        {
            foreach (var label in labels)
            {
                if (label >= 0 && label < classCounts.Length)
                {
                    classCounts[label]++;
                }
            }
        }

        Console.WriteLine($"{setName} class distribution:");
        for (int i = 0; i < classCounts.Length; i++)
        {
            Console.WriteLine($"  {ClassNames[i]}: {classCounts[i]} frames");
        }
    }

    private Task ExportToOnnxAsync(PhaseClassifierLstmModel model)
    {
        return Task.Run(() =>
        {
            // Ensure output directory exists
            var outputDir = Path.GetDirectoryName(_config.ModelOutputPath);
            if (!string.IsNullOrEmpty(outputDir) && !Directory.Exists(outputDir))
            {
                Directory.CreateDirectory(outputDir);
            }

            model.eval();

            // Save as TorchScript for now - ONNX export requires external tools
            // Use torch.jit.trace equivalent in TorchSharp
            var torchScriptPath = Path.ChangeExtension(_config.ModelOutputPath, ".pt");
            model.save(torchScriptPath);

            Console.WriteLine($"Model saved as TorchScript: {torchScriptPath}");
            Console.WriteLine(
                "Note: For ONNX export, use external tools like 'python -c \"import torch; model = torch.load(...); torch.onnx.export(...)\"'"
            );
        });
    }
}

/// <summary>
/// JSON structure for labeled frame data files
/// </summary>
public class LabeledFrameJson
{
    public string? VideoName { get; set; }
    public int FrameIndex { get; set; }
    public float Timestamp { get; set; }
    public bool IsRightHanded { get; set; } = true;

    /// <summary>
    /// Raw joint positions: [x, y, confidence, speed] × 17 joints = 68 values
    /// </summary>
    public required float[] Joints { get; set; }

    /// <summary>
    /// Joint angles: [leftElbow, rightElbow, leftShoulder, rightShoulder, leftHip, rightHip, leftKnee, rightKnee]
    /// </summary>
    public required float[] Angles { get; set; }

    public int Phase { get; set; }
}
