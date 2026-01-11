using System.Text.Json;
using System.Text.Json.Serialization;
using Dao.AI.BreakPoint.Data.Enums;
using Dao.AI.BreakPoint.Services.SwingAnalyzer;

namespace Dao.AI.BreakPoint.ModelTraining;

/// <summary>
/// Generates reference profiles from high-quality (pro-level) swings.
/// Reference profiles contain mean and std for each feature per phase,
/// used to compute z-score deviations during inference.
/// </summary>
public class SwingReferenceProfileGenerator
{
    /// <summary>
    /// Minimum phase score to include a swing in reference profile calculation
    /// </summary>
    public const int MinPhaseScore = 90;

    /// <summary>
    /// Generate reference profiles from labeled video data.
    /// Only includes swings with all phase scores >= MinPhaseScore.
    /// </summary>
    public ReferenceProfiles GenerateProfiles(
        List<(VideoLabel label, List<SwingData> swings)> labeledSwings,
        SwingType strokeType
    )
    {
        Console.WriteLine($"Generating reference profiles for {strokeType}");

        // Filter to high-quality swings
        var highQualitySwings = labeledSwings
            .Where(ls =>
                ls.label.StrokeType == strokeType
                && ls.label.PrepScore >= MinPhaseScore
                && ls.label.BackswingScore >= MinPhaseScore
                && ls.label.ContactScore >= MinPhaseScore
                && ls.label.FollowThroughScore >= MinPhaseScore
            )
            .ToList();

        Console.WriteLine(
            $"Found {highQualitySwings.Count} high-quality videos (all phases >= {MinPhaseScore})"
        );

        if (highQualitySwings.Count < 3)
        {
            Console.WriteLine("Warning: Insufficient high-quality data. Lowering threshold to 80.");
            highQualitySwings =
            [
                .. labeledSwings.Where(ls =>
                    ls.label.StrokeType == strokeType
                    && ls.label.PrepScore >= 80
                    && ls.label.BackswingScore >= 80
                    && ls.label.ContactScore >= 80
                    && ls.label.FollowThroughScore >= 80
                ),
            ];
            Console.WriteLine($"Found {highQualitySwings.Count} videos with threshold 80");
        }

        var profiles = new ReferenceProfiles
        {
            StrokeType = strokeType,
            GeneratedAt = DateTime.UtcNow,
            SourceVideoCount = highQualitySwings.Count,
            FeatureNames = [.. LstmFeatureExtractor.FeatureNames],
        };

        // Collect features by phase
        var featuresByPhase = new Dictionary<SwingPhase, List<float[]>>();
        foreach (var phase in PhaseQualityMlpTrainer.TrainablePhases)
        {
            featuresByPhase[phase] = [];
        }

        foreach (var (label, swings) in highQualitySwings)
        {
            foreach (var swing in swings)
            {
                FrameData? prevFrame = null;
                foreach (var frame in swing.Frames)
                {
                    if (featuresByPhase.ContainsKey(frame.SwingPhase))
                    {
                        var features = LstmFeatureExtractor.ExtractFeatures(
                            frame,
                            prevFrame,
                            label.IsRightHanded
                        );
                        featuresByPhase[frame.SwingPhase].Add(features);
                    }
                    prevFrame = frame;
                }
            }
        }

        // Compute statistics for each phase
        foreach (var phase in PhaseQualityMlpTrainer.TrainablePhases)
        {
            var frames = featuresByPhase[phase];
            if (frames.Count == 0)
            {
                Console.WriteLine($"Warning: No frames for {phase} phase");
                continue;
            }

            var phaseProfile = ComputePhaseProfile(phase, frames);
            profiles.PhaseProfiles[phase] = phaseProfile;

            Console.WriteLine($"{phase}: {frames.Count} frames from reference swings");
        }

        return profiles;
    }

    /// <summary>
    /// Generate profiles for both forehand and backhand stroke types
    /// </summary>
    public Dictionary<SwingType, ReferenceProfiles> GenerateAllProfiles(
        List<(VideoLabel label, List<SwingData> swings)> labeledSwings
    )
    {
        var allProfiles = new Dictionary<SwingType, ReferenceProfiles>();

        allProfiles[SwingType.ForehandGroundStroke] = GenerateProfiles(
            labeledSwings,
            SwingType.ForehandGroundStroke
        );
        allProfiles[SwingType.BackhandGroundStroke] = GenerateProfiles(
            labeledSwings,
            SwingType.BackhandGroundStroke
        );

        return allProfiles;
    }

    /// <summary>
    /// Save reference profiles to JSON file
    /// </summary>
    public static async Task SaveProfilesAsync(
        Dictionary<SwingType, ReferenceProfiles> profiles,
        string outputPath
    )
    {
        var outputDir = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(outputDir) && !Directory.Exists(outputDir))
        {
            Directory.CreateDirectory(outputDir);
        }

        var json = JsonSerializer.Serialize(
            profiles,
            new JsonSerializerOptions
            {
                WriteIndented = true,
                Converters = { new JsonStringEnumConverter() },
            }
        );

        await File.WriteAllTextAsync(outputPath, json);
        Console.WriteLine($"Reference profiles saved to {outputPath}");
    }

    /// <summary>
    /// Load reference profiles from JSON file
    /// </summary>
    public static async Task<Dictionary<SwingType, ReferenceProfiles>> LoadProfilesAsync(
        string inputPath
    )
    {
        if (!File.Exists(inputPath))
        {
            throw new FileNotFoundException($"Reference profiles not found: {inputPath}");
        }

        var json = await File.ReadAllTextAsync(inputPath);
        return JsonSerializer.Deserialize<Dictionary<SwingType, ReferenceProfiles>>(
                json,
                new JsonSerializerOptions { Converters = { new JsonStringEnumConverter() } }
            ) ?? [];
    }

    private static PhaseProfile ComputePhaseProfile(SwingPhase phase, List<float[]> frames)
    {
        int numFeatures = LstmFeatureExtractor.FeatureCount;
        var profile = new PhaseProfile
        {
            Phase = phase,
            FrameCount = frames.Count,
            Means = new float[numFeatures],
            StdDevs = new float[numFeatures],
            Mins = new float[numFeatures],
            Maxs = new float[numFeatures],
        };

        for (int f = 0; f < numFeatures; f++)
        {
            var values = frames
                .Select(frame => frame[f])
                .Where(v => !float.IsNaN(v) && !float.IsInfinity(v))
                .ToList();

            if (values.Count == 0)
            {
                profile.Means[f] = 0;
                profile.StdDevs[f] = 1; // Default to 1 to avoid division by zero
                profile.Mins[f] = 0;
                profile.Maxs[f] = 0;
                continue;
            }

            float mean = values.Average();
            float variance = values.Sum(v => (v - mean) * (v - mean)) / values.Count;
            float std = MathF.Sqrt(variance);

            profile.Means[f] = mean;
            profile.StdDevs[f] = std > 0.001f ? std : 0.001f; // Minimum std to avoid division issues
            profile.Mins[f] = values.Min();
            profile.Maxs[f] = values.Max();
        }

        return profile;
    }
}

/// <summary>
/// Reference profiles for a stroke type, containing per-phase statistics
/// </summary>
public class ReferenceProfiles
{
    public SwingType StrokeType { get; set; }
    public DateTime GeneratedAt { get; set; }
    public int SourceVideoCount { get; set; }
    public List<string> FeatureNames { get; set; } = [];

    [JsonConverter(typeof(SwingPhaseDictionaryConverter))]
    public Dictionary<SwingPhase, PhaseProfile> PhaseProfiles { get; set; } = [];
}

/// <summary>
/// Statistics for a single phase from reference swings
/// </summary>
public class PhaseProfile
{
    public SwingPhase Phase { get; set; }
    public int FrameCount { get; set; }

    /// <summary>
    /// Mean value for each of the 20 features
    /// </summary>
    public float[] Means { get; set; } = [];

    /// <summary>
    /// Standard deviation for each feature
    /// </summary>
    public float[] StdDevs { get; set; } = [];

    /// <summary>
    /// Minimum observed values (for normalization bounds)
    /// </summary>
    public float[] Mins { get; set; } = [];

    /// <summary>
    /// Maximum observed values (for normalization bounds)
    /// </summary>
    public float[] Maxs { get; set; } = [];

    /// <summary>
    /// Compute z-score deviation from reference for a given feature value
    /// </summary>
    public float ComputeZScore(int featureIndex, float value)
    {
        if (featureIndex < 0 || featureIndex >= Means.Length)
            return 0;

        float mean = Means[featureIndex];
        float std = StdDevs[featureIndex];

        if (std < 0.001f)
            return 0; // No variation in reference

        return (value - mean) / std;
    }

    /// <summary>
    /// Compute z-score deviations for all features
    /// </summary>
    public float[] ComputeZScores(float[] features)
    {
        var zScores = new float[features.Length];
        for (int i = 0; i < features.Length && i < Means.Length; i++)
        {
            zScores[i] = ComputeZScore(i, features[i]);
        }
        return zScores;
    }
}

/// <summary>
/// Custom JSON converter for Dictionary with SwingPhase keys
/// </summary>
public class SwingPhaseDictionaryConverter : JsonConverter<Dictionary<SwingPhase, PhaseProfile>>
{
    public override Dictionary<SwingPhase, PhaseProfile>? Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options
    )
    {
        var result = new Dictionary<SwingPhase, PhaseProfile>();

        if (reader.TokenType != JsonTokenType.StartObject)
            throw new JsonException();

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject)
                return result;

            if (reader.TokenType != JsonTokenType.PropertyName)
                throw new JsonException();

            var keyString = reader.GetString();
            if (!Enum.TryParse<SwingPhase>(keyString, out var key))
                throw new JsonException($"Invalid SwingPhase: {keyString}");

            reader.Read();
            var value = JsonSerializer.Deserialize<PhaseProfile>(ref reader, options);

            if (value != null)
                result[key] = value;
        }

        throw new JsonException();
    }

    public override void Write(
        Utf8JsonWriter writer,
        Dictionary<SwingPhase, PhaseProfile> value,
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
