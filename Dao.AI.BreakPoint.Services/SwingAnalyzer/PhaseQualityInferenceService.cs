using System.Text.Json;
using System.Text.Json.Serialization;
using Dao.AI.BreakPoint.Data.Enums;
using Dao.AI.BreakPoint.Services.MoveNet;
using TorchSharp;
using TorchSharp.Modules;
using static TorchSharp.torch;
using static TorchSharp.torch.nn;

namespace Dao.AI.BreakPoint.Services.SwingAnalyzer;

/// <summary>
/// TorchSharp MLP model for phase-specific quality scoring (inference).
/// Architecture: MLP(60 → 128 → 64 → 1) with ReLU activations.
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

    public const int InputSize = 60; // 20 features × 3 stats
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
        x = functional.sigmoid(x) * 100;

        return x;
    }
}

/// <summary>
/// Phase-aware quality inference service using 4 separate TorchSharp MLP models
/// and reference profiles for z-score deviation analysis.
/// </summary>
public class PhaseQualityInferenceService : IDisposable
{
    private readonly Dictionary<SwingPhase, PhaseQualityMlpModel?> _phaseModels = [];
    private readonly Dictionary<SwingType, ReferenceProfileSet?> _referenceProfiles = [];
    private bool _disposed;

    /// <summary>
    /// Number of features per frame (from LstmFeatureExtractor)
    /// </summary>
    public const int FeaturesPerFrame = 20;

    /// <summary>
    /// Number of statistics per feature (mean, std, range)
    /// </summary>
    private const int StatsPerFeature = 3;

    /// <summary>
    /// Total aggregated feature count for each phase MLP
    /// </summary>
    public const int AggregatedFeatureCount = FeaturesPerFrame * StatsPerFeature; // 60

    private static readonly SwingPhase[] ScoredPhases =
    [
        SwingPhase.Backswing,
        SwingPhase.Contact,
        SwingPhase.FollowThrough,
    ];

    private static readonly Dictionary<SwingPhase, string> PhaseModelNames = new()
    {
        [SwingPhase.Backswing] = "backswing_quality",
        [SwingPhase.Contact] = "contact_quality",
        [SwingPhase.FollowThrough] = "followthrough_quality",
    };

    /// <summary>
    /// Feature names matching LstmFeatureExtractor
    /// </summary>
    public static readonly string[] FeatureNames =
    [
        "Right Wrist Speed",
        "Right Wrist Acceleration",
        "Left Wrist Speed",
        "Right Elbow Speed",
        "Right Shoulder Speed",
        "Hip Rotation Speed",
        "Right Elbow Angle",
        "Left Elbow Angle",
        "Right Shoulder Angle",
        "Left Shoulder Angle",
        "Right Hip Angle",
        "Right Knee Angle",
        "Right Wrist X (relative)",
        "Right Wrist Y (relative)",
        "Right Elbow X (relative)",
        "Right Elbow Y (relative)",
        "Wrist-to-Shoulder X",
        "Wrist-to-Shoulder Y",
        "Wrist Height Diff",
        "Handedness",
    ];

    /// <summary>
    /// Creates a new PhaseQualityInferenceService.
    /// </summary>
    /// <param name="modelsDirectory">Directory containing phase ONNX models</param>
    /// <param name="referenceProfilesPath">Path to reference_profiles.json</param>
    public PhaseQualityInferenceService(
        string modelsDirectory,
        string? referenceProfilesPath = null
    )
    {
        LoadPhaseModels(modelsDirectory);

        if (!string.IsNullOrEmpty(referenceProfilesPath))
        {
            LoadReferenceProfiles(referenceProfilesPath);
        }
    }

    private void LoadPhaseModels(string modelsDirectory)
    {
        foreach (var phase in ScoredPhases)
        {
            var modelName = PhaseModelNames[phase];
            var modelPath = Path.Combine(modelsDirectory, $"{modelName}.pt");

            if (File.Exists(modelPath))
            {
                try
                {
                    var model = new PhaseQualityMlpModel($"{modelName}_model");
                    model.load(modelPath);
                    model.eval(); // Set to evaluation mode
                    _phaseModels[phase] = model;
                    Console.WriteLine($"Loaded {phase} quality model from {modelPath}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Warning: Failed to load {phase} model: {ex.Message}");
                    _phaseModels[phase] = null;
                }
            }
            else
            {
                Console.WriteLine($"Warning: {phase} quality model not found at {modelPath}");
                _phaseModels[phase] = null;
            }
        }
    }

    private void LoadReferenceProfiles(string profilesPath)
    {
        if (!File.Exists(profilesPath))
        {
            Console.WriteLine($"Warning: Reference profiles not found at {profilesPath}");
            return;
        }

        try
        {
            var json = File.ReadAllText(profilesPath);
            var profiles = JsonSerializer.Deserialize<Dictionary<SwingType, ReferenceProfileSet>>(
                json,
                new JsonSerializerOptions { Converters = { new JsonStringEnumConverter() } }
            );

            if (profiles != null)
            {
                foreach (var (strokeType, profile) in profiles)
                {
                    _referenceProfiles[strokeType] = profile;
                }
                Console.WriteLine(
                    $"Loaded reference profiles for {_referenceProfiles.Count} stroke types"
                );
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Warning: Failed to load reference profiles: {ex.Message}");
        }
    }

    /// <summary>
    /// Whether all phase models are loaded
    /// </summary>
    public bool AllModelsLoaded => ScoredPhases.All(p => _phaseModels.GetValueOrDefault(p) != null);

    /// <summary>
    /// Run inference to get phase-specific quality scores and deviations
    /// </summary>
    public PhaseQualityResult RunInference(
        SwingData swing,
        SwingType strokeType,
        bool isRightHanded
    )
    {
        var result = new PhaseQualityResult
        {
            StrokeType = strokeType,
            IsRightHanded = isRightHanded,
        };

        if (swing.Frames.Count == 0)
        {
            return result;
        }

        // Group frames by phase
        var framesByPhase = swing
            .Frames.GroupBy(f => f.SwingPhase)
            .ToDictionary(g => g.Key, g => g.ToList());

        // Get reference profile for this stroke type
        _referenceProfiles.TryGetValue(strokeType, out var referenceProfile);

        // Score each phase
        foreach (var phase in ScoredPhases)
        {
            if (!framesByPhase.TryGetValue(phase, out var phaseFrames) || phaseFrames.Count == 0)
            {
                // Phase not detected in swing
                result.PhaseResults[phase] = new SinglePhaseResult
                {
                    Phase = phase,
                    FrameCount = 0,
                    Score = 0,
                    IsFromModel = false,
                    Deviations = [],
                };
                continue;
            }

            // Extract and aggregate features for this phase
            var aggregatedFeatures = AggregatePhaseFeatures(phaseFrames, isRightHanded);

            // Get score from model or heuristic
            float score;
            bool isFromModel;

            if (_phaseModels.TryGetValue(phase, out var model) && model != null)
            {
                score = RunModelInference(model, aggregatedFeatures);
                isFromModel = true;
            }
            else
            {
                score = ComputeHeuristicScore(aggregatedFeatures, phase);
                isFromModel = false;
            }

            // Compute deviations from reference profile
            var deviations = new List<FeatureDeviation>();
            if (
                referenceProfile != null
                && referenceProfile.PhaseProfiles.TryGetValue(phase, out var phaseProfile)
            )
            {
                deviations = ComputeDeviations(aggregatedFeatures, phaseProfile, isRightHanded);
            }

            result.PhaseResults[phase] = new SinglePhaseResult
            {
                Phase = phase,
                FrameCount = phaseFrames.Count,
                Score = score,
                IsFromModel = isFromModel,
                Deviations = deviations,
                AggregatedFeatures = aggregatedFeatures,
            };
        }

        // Compute overall score as weighted average of phase scores
        result.OverallScore = ComputeOverallScore(result.PhaseResults);

        return result;
    }

    /// <summary>
    /// Extract 20 features per frame using the LSTM feature extractor logic
    /// </summary>
    private static float[] ExtractFrameFeatures(FrameData frame, bool isRightHanded)
    {
        var features = new float[FeaturesPerFrame];

        // Determine dominant side
        int dominantWrist = isRightHanded ? 10 : 9; // RightWrist : LeftWrist
        int dominantElbow = isRightHanded ? 8 : 7; // RightElbow : LeftElbow
        int dominantShoulder = isRightHanded ? 6 : 5; // RightShoulder : LeftShoulder
        int nonDominantWrist = isRightHanded ? 9 : 10;
        int nonDominantElbow = isRightHanded ? 7 : 8;

        const int LeftHip = 11;
        const int RightHip = 12;
        const int LeftShoulder = 5;
        const int RightShoulder = 6;

        // Normalization
        var hipCenterX = (frame.Joints[LeftHip].X + frame.Joints[RightHip].X) / 2;
        var hipCenterY = (frame.Joints[LeftHip].Y + frame.Joints[RightHip].Y) / 2;
        var shoulderCenterY = (frame.Joints[LeftShoulder].Y + frame.Joints[RightShoulder].Y) / 2;
        var torsoHeight = Math.Max(0.1f, Math.Abs(hipCenterY - shoulderCenterY));

        // Velocities (0-5)
        features[0] = Sanitize((frame.Joints[dominantWrist].Speed ?? 0f) / 500f);
        features[1] = Sanitize((frame.Joints[dominantWrist].Acceleration ?? 0f) / 1000f);
        features[2] = Sanitize((frame.Joints[nonDominantWrist].Speed ?? 0f) / 500f);
        features[3] = Sanitize((frame.Joints[dominantElbow].Speed ?? 0f) / 500f);
        features[4] = Sanitize((frame.Joints[dominantShoulder].Speed ?? 0f) / 500f);
        features[5] = Sanitize(frame.HipRotationSpeed / 100f);

        // Angles (6-11)
        features[6] = Sanitize(
            (isRightHanded ? frame.RightElbowAngle : frame.LeftElbowAngle) / 180f
        );
        features[7] = Sanitize(
            (isRightHanded ? frame.LeftElbowAngle : frame.RightElbowAngle) / 180f
        );
        features[8] = Sanitize(
            (isRightHanded ? frame.RightShoulderAngle : frame.LeftShoulderAngle) / 180f
        );
        features[9] = Sanitize(
            (isRightHanded ? frame.LeftShoulderAngle : frame.RightShoulderAngle) / 180f
        );
        features[10] = Sanitize((isRightHanded ? frame.RightHipAngle : frame.LeftHipAngle) / 180f);
        features[11] = Sanitize(
            (isRightHanded ? frame.RightKneeAngle : frame.LeftKneeAngle) / 180f
        );

        // Relative positions (12-15)
        features[12] = Sanitize((frame.Joints[dominantWrist].X - hipCenterX) / torsoHeight);
        features[13] = Sanitize((frame.Joints[dominantWrist].Y - hipCenterY) / torsoHeight);
        features[14] = Sanitize((frame.Joints[dominantElbow].X - hipCenterX) / torsoHeight);
        features[15] = Sanitize((frame.Joints[dominantElbow].Y - hipCenterY) / torsoHeight);

        // Arm configuration (16-18)
        features[16] = Sanitize(
            (frame.Joints[dominantWrist].X - frame.Joints[dominantShoulder].X) / torsoHeight
        );
        features[17] = Sanitize(
            (frame.Joints[dominantWrist].Y - frame.Joints[dominantShoulder].Y) / torsoHeight
        );
        features[18] = Sanitize(
            (frame.Joints[dominantWrist].Y - frame.Joints[dominantShoulder].Y) / torsoHeight
        );

        // Handedness
        features[19] = isRightHanded ? 1.0f : 0.0f;

        return features;
    }

    /// <summary>
    /// Aggregate features from phase frames using mean, std, range
    /// </summary>
    private static float[] AggregatePhaseFeatures(List<FrameData> frames, bool isRightHanded)
    {
        var frameFeatures = frames.Select(f => ExtractFrameFeatures(f, isRightHanded)).ToList();
        var aggregated = new float[AggregatedFeatureCount];

        for (int f = 0; f < FeaturesPerFrame; f++)
        {
            var values = frameFeatures
                .Select(ff => ff[f])
                .Where(v => !float.IsNaN(v) && !float.IsInfinity(v))
                .ToList();

            int baseIdx = f * StatsPerFeature;

            if (values.Count == 0)
            {
                aggregated[baseIdx + 0] = 0f;
                aggregated[baseIdx + 1] = 0f;
                aggregated[baseIdx + 2] = 0f;
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

    private static float RunModelInference(PhaseQualityMlpModel model, float[] features)
    {
        using var noGrad = torch.no_grad();

        // Create input tensor [1, 60]
        var inputData = new float[PhaseQualityMlpModel.InputSize];
        for (int i = 0; i < Math.Min(features.Length, PhaseQualityMlpModel.InputSize); i++)
        {
            inputData[i] = features[i];
        }

        using var inputTensor = torch.tensor(inputData, dtype: torch.float32).reshape(1, -1);
        using var output = model.forward(inputTensor);

        var score = output.item<float>();
        return Math.Clamp(score, 0, 100);
    }

    private static float ComputeHeuristicScore(float[] aggregatedFeatures, SwingPhase phase)
    {
        // Heuristic based on key features for each phase
        float score = 50f;

        // Mean indices: 0, 3, 6, ... (every 3rd starting from 0)
        float wristSpeedMean = aggregatedFeatures.Length > 0 ? aggregatedFeatures[0] : 0;
        float shoulderSpeedMean = aggregatedFeatures.Length > 12 ? aggregatedFeatures[12] : 0;
        float elbowAngleMean = aggregatedFeatures.Length > 18 ? aggregatedFeatures[18] : 0;

        score += wristSpeedMean * 20;
        score += shoulderSpeedMean * 15;
        score += (1 - Math.Abs(elbowAngleMean - 0.5f)) * 10; // Optimal around 90 degrees

        return Math.Clamp(score, 0, 100);
    }

    private static List<FeatureDeviation> ComputeDeviations(
        float[] aggregatedFeatures,
        PhaseReferenceProfile reference,
        bool isRightHanded
    )
    {
        var deviations = new List<FeatureDeviation>();

        for (int f = 0; f < FeaturesPerFrame && f < reference.Means.Length; f++)
        {
            // Use mean value (index 0 in stats triplet)
            int meanIdx = f * StatsPerFeature;
            if (meanIdx >= aggregatedFeatures.Length)
                break;

            float actualMean = aggregatedFeatures[meanIdx];
            float refMean = reference.Means[f];
            float refStd = Math.Max(reference.StdDevs[f], 0.001f);

            float zScore = (actualMean - refMean) / refStd;

            if (Math.Abs(zScore) > 0.5f) // Only significant deviations
            {
                deviations.Add(
                    new FeatureDeviation
                    {
                        FeatureIndex = f,
                        FeatureName = f < FeatureNames.Length ? FeatureNames[f] : $"Feature {f}",
                        CoachingTerm = CoachingFeatureMapper.GetCoachingTerm(f, isRightHanded),
                        ActualValue = actualMean,
                        ReferenceValue = refMean,
                        ZScore = zScore,
                        Direction = zScore > 0 ? "too high" : "too low",
                    }
                );
            }
        }

        return deviations.OrderByDescending(d => Math.Abs(d.ZScore)).ToList();
    }

    private static double ComputeOverallScore(
        Dictionary<SwingPhase, SinglePhaseResult> phaseResults
    )
    {
        // Weighted average: Contact most important, then Backswing, then FollowThrough
        var weights = new Dictionary<SwingPhase, double>
        {
            [SwingPhase.Backswing] = 0.30,
            [SwingPhase.Contact] = 0.40,
            [SwingPhase.FollowThrough] = 0.30,
        };

        double weightedSum = 0;
        double totalWeight = 0;

        foreach (var (phase, result) in phaseResults)
        {
            if (result.FrameCount > 0 && weights.TryGetValue(phase, out var weight))
            {
                weightedSum += result.Score * weight;
                totalWeight += weight;
            }
        }

        return totalWeight > 0 ? weightedSum / totalWeight : 0;
    }

    private static float Sanitize(float value)
    {
        if (float.IsNaN(value) || float.IsInfinity(value))
            return 0f;
        return Math.Clamp(value, -10f, 10f);
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            foreach (var session in _phaseModels.Values)
            {
                session?.Dispose();
            }
            _phaseModels.Clear();
            _disposed = true;
        }
        GC.SuppressFinalize(this);
    }
}

/// <summary>
/// Complete quality result with phase-specific scores and deviations
/// </summary>
public class PhaseQualityResult
{
    public SwingType StrokeType { get; set; }
    public bool IsRightHanded { get; set; }
    public double OverallScore { get; set; }

    public Dictionary<SwingPhase, SinglePhaseResult> PhaseResults { get; set; } = [];

    public double PrepScore => PhaseResults.GetValueOrDefault(SwingPhase.Preparation)?.Score ?? 0;
    public double BackswingScore =>
        PhaseResults.GetValueOrDefault(SwingPhase.Backswing)?.Score ?? 0;
    public double ContactScore => PhaseResults.GetValueOrDefault(SwingPhase.Contact)?.Score ?? 0;
    public double FollowThroughScore =>
        PhaseResults.GetValueOrDefault(SwingPhase.FollowThrough)?.Score ?? 0;

    /// <summary>
    /// Get top N deviations across all phases for coaching focus
    /// </summary>
    public List<FeatureDeviation> GetTopDeviations(int topN = 5)
    {
        return PhaseResults
            .Values.SelectMany(r =>
                r.Deviations.Select(d => new FeatureDeviation
                {
                    FeatureIndex = d.FeatureIndex,
                    FeatureName = d.FeatureName,
                    CoachingTerm = d.CoachingTerm,
                    ActualValue = d.ActualValue,
                    ReferenceValue = d.ReferenceValue,
                    ZScore = d.ZScore,
                    Direction = d.Direction,
                    Phase = r.Phase,
                })
            )
            .OrderByDescending(d => Math.Abs(d.ZScore))
            .Take(topN)
            .ToList();
    }
}

/// <summary>
/// Result for a single phase
/// </summary>
public class SinglePhaseResult
{
    public SwingPhase Phase { get; set; }
    public int FrameCount { get; set; }
    public double Score { get; set; }
    public bool IsFromModel { get; set; }
    public List<FeatureDeviation> Deviations { get; set; } = [];
    public float[]? AggregatedFeatures { get; set; }
}

/// <summary>
/// Deviation from reference profile for a single feature
/// </summary>
public class FeatureDeviation
{
    public int FeatureIndex { get; set; }
    public string FeatureName { get; set; } = "";
    public string CoachingTerm { get; set; } = "";
    public float ActualValue { get; set; }
    public float ReferenceValue { get; set; }
    public float ZScore { get; set; }
    public string Direction { get; set; } = "";
    public SwingPhase Phase { get; set; }
}

/// <summary>
/// Reference profiles for ONNX inference (simplified structure)
/// </summary>
public class ReferenceProfileSet
{
    public SwingType StrokeType { get; set; }
    public DateTime GeneratedAt { get; set; }
    public int SourceVideoCount { get; set; }

    [JsonConverter(typeof(PhaseProfileDictionaryConverter))]
    public Dictionary<SwingPhase, PhaseReferenceProfile> PhaseProfiles { get; set; } = [];
}

public class PhaseReferenceProfile
{
    public SwingPhase Phase { get; set; }
    public int FrameCount { get; set; }
    public float[] Means { get; set; } = [];
    public float[] StdDevs { get; set; } = [];
}

/// <summary>
/// JSON converter for SwingPhase dictionary keys
/// </summary>
public class PhaseProfileDictionaryConverter
    : JsonConverter<Dictionary<SwingPhase, PhaseReferenceProfile>>
{
    public override Dictionary<SwingPhase, PhaseReferenceProfile>? Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options
    )
    {
        var result = new Dictionary<SwingPhase, PhaseReferenceProfile>();
        if (reader.TokenType != JsonTokenType.StartObject)
            return result;

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject)
                return result;
            if (reader.TokenType != JsonTokenType.PropertyName)
                continue;

            var keyString = reader.GetString();
            if (!Enum.TryParse<SwingPhase>(keyString, out var key))
                continue;

            reader.Read();
            var value = JsonSerializer.Deserialize<PhaseReferenceProfile>(ref reader, options);
            if (value != null)
                result[key] = value;
        }
        return result;
    }

    public override void Write(
        Utf8JsonWriter writer,
        Dictionary<SwingPhase, PhaseReferenceProfile> value,
        JsonSerializerOptions options
    )
    {
        writer.WriteStartObject();
        foreach (var kvp in value)
        {
            writer.WritePropertyName(kvp.Key.ToString());
            JsonSerializer.Serialize(writer, kvp.Value, options);
        }
        writer.WriteEndObject();
    }
}
