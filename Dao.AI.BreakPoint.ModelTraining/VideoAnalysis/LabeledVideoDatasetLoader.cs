using System.Text.Json;
using Dao.AI.BreakPoint.Services.MoveNet;
using Dao.AI.BreakPoint.Services.VideoProcessing;

namespace Dao.AI.BreakPoint.ModelTraining.VideoAnalysis;

internal class TrainingDatasetLoader(IVideoProcessingService VideoProcessingService)
    : ITrainingDatasetLoader
{
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
    };

    /// <summary>
    /// Load training dataset from individual video/label files in a directory
    /// Separate out the original video into swing segments
    /// </summary>
    public async Task<List<TrainingSwingVideo>> ProcessVideoDirectoryAsync(
        string videoDirectory,
        string moveNetModelPath,
        string phaseClassifierModelPath
    )
    {
        if (!Directory.Exists(videoDirectory))
        {
            throw new DirectoryNotFoundException($"Video directory not found: {videoDirectory}");
        }

        var processedTrainingSwingVideos = new List<TrainingSwingVideo>();
        var videoExtensions = new[] { ".mp4", ".avi", ".mov", ".mkv", ".webm" };

        var videoFiles = Directory
            .GetFiles(videoDirectory)
            .Where(f => videoExtensions.Contains(Path.GetExtension(f).ToLower()))
            .ToArray();

        using var processor = new MoveNetVideoProcessor(moveNetModelPath, phaseClassifierModelPath);

        foreach (var videoPath in videoFiles)
        {
            try
            {
                var labelPath = Path.ChangeExtension(videoPath, ".json");

                var label = await LoadVideoLabelAsync(labelPath);
                Console.WriteLine(
                    $"Processing video: {Path.GetFileName(videoPath)} (Stroke: {label.StrokeType}, Quality: {label.QualityScore}, RightHanded: {label.IsRightHanded})"
                );

                // Extract frames from video
                var frameImages = VideoProcessingService.ExtractFrames(videoPath);

                if (frameImages.Count == 0)
                {
                    Console.WriteLine($"No frames extracted from video: {videoPath}");
                    continue;
                }
                var metadata = VideoProcessingService.GetVideoMetadata(videoPath);

                // Split the videos in the different swings and analyze them
                // Pass the handedness from the label for proper arm detection
                var processedVideo = processor.ProcessVideoFrames(
                    frameImages,
                    metadata,
                    label.IsRightHanded
                );
                Console.WriteLine(
                    $"{processedVideo.Swings.Count} swings detected from this video."
                );
                processedTrainingSwingVideos.Add(
                    new() { TrainingLabel = label, SwingVideo = processedVideo }
                );
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error processing video {videoPath}: {ex.Message}");
                continue;
            }
        }

        Console.WriteLine(
            $"Successfully processed {processedTrainingSwingVideos.Count} videos from directory"
        );

        // Print dataset distribution metrics
        PrintDatasetDistribution(processedTrainingSwingVideos);

        return processedTrainingSwingVideos;
    }

    /// <summary>
    /// Print dataset distribution metrics to help identify data collection needs
    /// </summary>
    private static void PrintDatasetDistribution(List<TrainingSwingVideo> dataset)
    {
        if (dataset.Count == 0)
        {
            Console.WriteLine("No data to analyze.");
            return;
        }

        Console.WriteLine();
        Console.WriteLine("╔════════════════════════════════════════════════════════════════╗");
        Console.WriteLine("║                    DATASET DISTRIBUTION                        ║");
        Console.WriteLine("╚════════════════════════════════════════════════════════════════╝");

        int totalSwings = dataset.Sum(d => d.SwingVideo.Swings.Count);
        int totalVideos = dataset.Count;

        Console.WriteLine($"  Total Videos: {totalVideos}");
        Console.WriteLine($"  Total Swings: {totalSwings}");
        Console.WriteLine();

        // === HANDEDNESS DISTRIBUTION ===
        Console.WriteLine("┌────────────────────────────────────────────────────────────────┐");
        Console.WriteLine("│                      HANDEDNESS                                │");
        Console.WriteLine("├────────────────────────────────────────────────────────────────┤");

        var rightHandedVideos = dataset.Where(d => d.TrainingLabel.IsRightHanded).ToList();
        var leftHandedVideos = dataset.Where(d => !d.TrainingLabel.IsRightHanded).ToList();

        int rightSwings = rightHandedVideos.Sum(d => d.SwingVideo.Swings.Count);
        int leftSwings = leftHandedVideos.Sum(d => d.SwingVideo.Swings.Count);

        Console.WriteLine(
            $"  Right-handed: {rightHandedVideos.Count} videos, {rightSwings} swings ({(float)rightSwings / totalSwings:P1})"
        );
        Console.WriteLine(
            $"  Left-handed:  {leftHandedVideos.Count} videos, {leftSwings} swings ({(float)leftSwings / totalSwings:P1})"
        );
        Console.WriteLine();

        // === STROKE TYPE DISTRIBUTION ===
        Console.WriteLine("┌────────────────────────────────────────────────────────────────┐");
        Console.WriteLine("│                      STROKE TYPE                               │");
        Console.WriteLine("├────────────────────────────────────────────────────────────────┤");

        var byStrokeType = dataset.GroupBy(d => d.TrainingLabel.StrokeType).OrderBy(g => g.Key);

        foreach (var group in byStrokeType)
        {
            int videoCount = group.Count();
            int swingCount = group.Sum(d => d.SwingVideo.Swings.Count);
            Console.WriteLine(
                $"  {group.Key, -25} {videoCount, 3} videos, {swingCount, 4} swings ({(float)swingCount / totalSwings:P1})"
            );
        }

        Console.WriteLine();

        // === QUALITY SCORE DISTRIBUTION ===
        Console.WriteLine("┌────────────────────────────────────────────────────────────────┐");
        Console.WriteLine("│                    QUALITY DISTRIBUTION                        │");
        Console.WriteLine("├────────────────────────────────────────────────────────────────┤");

        var qualityBuckets = new[]
        {
            ("Beginner (0-30)", 0, 30),
            ("Developing (31-50)", 31, 50),
            ("Intermediate (51-70)", 51, 70),
            ("Advanced (71-85)", 71, 85),
            ("Pro (86-100)", 86, 100),
        };

        foreach (var (label, min, max) in qualityBuckets)
        {
            var inBucket = dataset.Where(d =>
                d.TrainingLabel.QualityScore >= min && d.TrainingLabel.QualityScore <= max
            );
            int videoCount = inBucket.Count();
            int swingCount = inBucket.Sum(d => d.SwingVideo.Swings.Count);
            Console.WriteLine(
                $"  {label, -25} {videoCount, 3} videos, {swingCount, 4} swings ({(float)swingCount / totalSwings:P1})"
            );
        }

        Console.WriteLine();

        // === STROKE TYPE × HANDEDNESS MATRIX ===
        Console.WriteLine("┌────────────────────────────────────────────────────────────────┐");
        Console.WriteLine("│                 STROKE TYPE × HANDEDNESS                       │");
        Console.WriteLine("├────────────────────────────────────────────────────────────────┤");
        Console.WriteLine($"  {"Stroke Type", -25} {"Right", 10} {"Left", 10}");
        Console.WriteLine($"  {new string('-', 45)}");

        foreach (var strokeType in byStrokeType.Select(g => g.Key))
        {
            int rightCount = dataset
                .Where(d =>
                    d.TrainingLabel.StrokeType == strokeType && d.TrainingLabel.IsRightHanded
                )
                .Sum(d => d.SwingVideo.Swings.Count);
            int leftCount = dataset
                .Where(d =>
                    d.TrainingLabel.StrokeType == strokeType && !d.TrainingLabel.IsRightHanded
                )
                .Sum(d => d.SwingVideo.Swings.Count);
            Console.WriteLine($"  {strokeType, -25} {rightCount, 10} {leftCount, 10}");
        }

        Console.WriteLine();

        // === QUALITY × STROKE TYPE MATRIX ===
        Console.WriteLine("┌────────────────────────────────────────────────────────────────┐");
        Console.WriteLine("│                 QUALITY × STROKE TYPE                          │");
        Console.WriteLine("├────────────────────────────────────────────────────────────────┤");

        var strokeTypes = byStrokeType.Select(g => g.Key).ToList();
        Console.Write($"  {"Quality", -20}");
        foreach (var st in strokeTypes)
        {
            Console.Write(
                $" {st.ToString().Replace("GroundStroke", "")[..Math.Min(st.ToString().Replace("GroundStroke", "").Length, 8)], 8}"
            );
        }
        Console.WriteLine();
        Console.WriteLine($"  {new string('-', 20 + strokeTypes.Count * 9)}");

        foreach (var (label, min, max) in qualityBuckets)
        {
            Console.Write($"  {label.Split(' ')[0], -20}");
            foreach (var strokeType in strokeTypes)
            {
                int count = dataset
                    .Where(d =>
                        d.TrainingLabel.StrokeType == strokeType
                        && d.TrainingLabel.QualityScore >= min
                        && d.TrainingLabel.QualityScore <= max
                    )
                    .Sum(d => d.SwingVideo.Swings.Count);
                Console.Write($" {count, 8}");
            }
            Console.WriteLine();
        }

        Console.WriteLine();

        // === PHASE SCORES SUMMARY ===
        Console.WriteLine("┌────────────────────────────────────────────────────────────────┐");
        Console.WriteLine("│                   PHASE SCORE AVERAGES                         │");
        Console.WriteLine("├────────────────────────────────────────────────────────────────┤");

        var avgPrep = dataset.Average(d => d.TrainingLabel.PrepScore);
        var avgBackswing = dataset.Average(d => d.TrainingLabel.BackswingScore);
        var avgContact = dataset.Average(d => d.TrainingLabel.ContactScore);
        var avgFollowThrough = dataset.Average(d => d.TrainingLabel.FollowThroughScore);

        Console.WriteLine($"  Preparation:    {avgPrep, 6:F1} avg");
        Console.WriteLine($"  Backswing:      {avgBackswing, 6:F1} avg");
        Console.WriteLine($"  Contact:        {avgContact, 6:F1} avg");
        Console.WriteLine($"  Follow-Through: {avgFollowThrough, 6:F1} avg");

        Console.WriteLine();
        Console.WriteLine("╔════════════════════════════════════════════════════════════════╗");
        Console.WriteLine("║                  DATA COLLECTION RECOMMENDATIONS               ║");
        Console.WriteLine("╚════════════════════════════════════════════════════════════════╝");

        // Identify gaps
        var recommendations = new List<string>();

        if ((float)leftSwings / totalSwings < 0.3f)
            recommendations.Add(
                "⚠️  Need more LEFT-HANDED samples (currently "
                    + $"{(float)leftSwings / totalSwings:P0})"
            );

        foreach (var group in byStrokeType)
        {
            int swingCount = group.Sum(d => d.SwingVideo.Swings.Count);
            if ((float)swingCount / totalSwings < 0.1f)
                recommendations.Add(
                    $"⚠️  Need more {group.Key} samples (currently {(float)swingCount / totalSwings:P0})"
                );
        }

        var beginnerCount = dataset
            .Where(d => d.TrainingLabel.QualityScore <= 30)
            .Sum(d => d.SwingVideo.Swings.Count);
        var proCount = dataset
            .Where(d => d.TrainingLabel.QualityScore >= 86)
            .Sum(d => d.SwingVideo.Swings.Count);

        if ((float)beginnerCount / totalSwings < 0.15f)
            recommendations.Add(
                $"⚠️  Need more BEGINNER (0-30) samples (currently {(float)beginnerCount / totalSwings:P0})"
            );
        if ((float)proCount / totalSwings < 0.15f)
            recommendations.Add(
                $"⚠️  Need more PRO (86-100) samples (currently {(float)proCount / totalSwings:P0})"
            );

        if (recommendations.Count == 0)
        {
            Console.WriteLine("  ✅ Dataset looks reasonably balanced!");
        }
        else
        {
            foreach (var rec in recommendations)
            {
                Console.WriteLine($"  {rec}");
            }
        }

        Console.WriteLine();
    }

    /// <summary>
    /// Load a single video label file
    /// </summary>
    public async Task<VideoLabel> LoadVideoLabelAsync(string labelPath)
    {
        if (!File.Exists(labelPath))
        {
            throw new FileNotFoundException($"Label file not found: {labelPath}");
        }

        var jsonContent = await File.ReadAllTextAsync(labelPath);
        var label =
            JsonSerializer.Deserialize<VideoLabel>(jsonContent, _jsonOptions)
            ?? throw new InvalidOperationException(
                $"Failed to deserialize label from: {labelPath}"
            );

        // Validate quality score range (0-100)
        if (label.QualityScore < 0 || label.QualityScore > 100)
        {
            throw new ArgumentException(
                $"Invalid quality score {label.QualityScore} in {labelPath}. Must be between 0 and 100"
            );
        }

        return label;
    }

    /// <summary>
    /// Save a video label file
    /// </summary>
    public void SaveVideoLabel(VideoLabel label, string labelPath)
    {
        var jsonContent = JsonSerializer.Serialize(label, _jsonOptions);
        File.WriteAllText(labelPath, jsonContent);
        Console.WriteLine($"Video label saved to: {labelPath}");
    }
}
