using System.Text.Json;
using Dao.AI.BreakPoint.Data.Enums;
using Dao.AI.BreakPoint.Data.Models;
using Dao.AI.BreakPoint.Services.MoveNet;
using Dao.AI.BreakPoint.Services.Options;
using Dao.AI.BreakPoint.Services.VideoProcessing;
using Microsoft.Extensions.Options;

namespace Dao.AI.BreakPoint.Services.SwingAnalyzer;

public class SwingAnalyzerService(
    IVideoProcessingService VideoProcessingService,
    IOptions<MoveNetOptions> MoveNetOptions,
    IOptions<SwingQualityModelOptions> SwingQualityOptions
) : ISwingAnalyzerService
{
    private const int SequenceLength = 90;
    private const int NumFeatures = 66;

    public async Task AnalyzeSwingAsync(Stream videoStream, AnalysisRequest analysisRequest)
    {
        // save the stream to a temporary file
        string tempFilePath = Path.GetTempFileName();
        try
        {
            using (var fileStream = new FileStream(tempFilePath, FileMode.Create, FileAccess.Write))
            {
                await videoStream.CopyToAsync(fileStream);
            }
            var frameImages = VideoProcessingService.ExtractFrames(tempFilePath);
            if (frameImages.Count == 0)
            {
                throw new InvalidOperationException(
                    "No frames extracted from the provided video stream."
                );
            }
            var metadata = VideoProcessingService.GetVideoMetadata(tempFilePath);

            // Get player's handedness for swing analysis
            var isRightHanded = analysisRequest.Player?.Handedness != Handedness.LeftHanded;

            using var processor = new MoveNetVideoProcessor(MoveNetOptions.Value.ModelPath);
            var swingVideo = processor.ProcessVideoFrames(frameImages, metadata, isRightHanded);

            if (swingVideo.Swings.Count == 0)
            {
                throw new InvalidOperationException("No valid swings detected in the video.");
            }

            // Analyze each swing and aggregate results
            var swingResults = new List<SwingQualityResult>();
            var qualityOptions = SwingQualityOptions.Value;

            using var qualityService = new SwingQualityInferenceService(
                qualityOptions.ModelPath,
                qualityOptions.SequenceLength,
                qualityOptions.NumFeatures
            );

            foreach (var swing in swingVideo.Swings)
            {
                var preprocessed = await SwingPreprocessingService.PreprocessSwingAsync(
                    swing,
                    SequenceLength,
                    NumFeatures
                );

                var result = qualityService.RunInference(preprocessed);
                swingResults.Add(result);
            }

            // Aggregate results (use best swing for now, could average in future)
            var bestResult = swingResults.OrderByDescending(r => r.QualityScore).First();

            // Create or update the AnalysisResult
            if (analysisRequest.Result == null)
            {
                analysisRequest.Result = new AnalysisResult
                {
                    AnalysisRequestId = analysisRequest.Id,
                    PlayerId = analysisRequest.PlayerId,
                    StrokeType = analysisRequest.StrokeType,
                    VideoBlobUrl = analysisRequest.VideoBlobUrl,
                };
            }

            // Populate the result with quality score and feature importance
            analysisRequest.Result.QualityScore = bestResult.QualityScore;
            analysisRequest.Result.FeatureImportanceJson = JsonSerializer.Serialize(
                bestResult.FeatureImportance
            );

            // Generate coaching tips based on weak features
            var coachingTips = GenerateCoachingTips(bestResult, analysisRequest.StrokeType);
            analysisRequest.Result.CoachingTipsJson = JsonSerializer.Serialize(coachingTips);

            analysisRequest.Status = AnalysisStatus.Completed;
        }
        finally
        {
            // Clean up temp file
            if (File.Exists(tempFilePath))
            {
                try
                {
                    File.Delete(tempFilePath);
                }
                catch
                { /* Ignore cleanup errors */
                }
            }
        }
    }

    /// <summary>
    /// Generate coaching tips based on the swing analysis results
    /// </summary>
    private static List<string> GenerateCoachingTips(
        SwingQualityResult result,
        SwingType strokeType
    )
    {
        var tips = new List<string>();
        var weakFeatures = result.GetWeakFeatures(3);
        var strongFeatures = result.GetTopFeatures(2);

        // Add tips based on weak features
        foreach (var (featureName, _) in weakFeatures)
        {
            var tip = GetTipForFeature(featureName, strokeType, isStrength: false);
            if (!string.IsNullOrEmpty(tip))
            {
                tips.Add(tip);
            }
        }

        // Add encouragement for strong features
        foreach (var (featureName, _) in strongFeatures)
        {
            var tip = GetTipForFeature(featureName, strokeType, isStrength: true);
            if (!string.IsNullOrEmpty(tip))
            {
                tips.Add(tip);
            }
        }

        // Add general tip based on quality score
        if (result.QualityScore < 40)
        {
            tips.Add("Focus on the fundamentals: proper stance, grip, and early preparation.");
        }
        else if (result.QualityScore < 60)
        {
            tips.Add("Good foundation! Work on timing and body coordination for more power.");
        }
        else if (result.QualityScore < 80)
        {
            tips.Add("Strong technique! Fine-tune your follow-through for consistency.");
        }
        else
        {
            tips.Add("Excellent form! Maintain this technique and focus on shot placement.");
        }

        return tips.Take(5).ToList(); // Limit to 5 tips
    }

    /// <summary>
    /// Get a coaching tip for a specific feature
    /// </summary>
    private static string? GetTipForFeature(
        string featureName,
        SwingType strokeType,
        bool isStrength
    )
    {
        if (isStrength)
        {
            return featureName switch
            {
                string s when s.Contains("Wrist") && s.Contains("Velocity") =>
                    "Great wrist acceleration - this generates good racket head speed.",
                string s when s.Contains("Shoulder") && s.Contains("Angle") =>
                    "Good shoulder rotation - key for power generation.",
                string s when s.Contains("Hip") && s.Contains("Angle") =>
                    "Strong hip rotation - excellent kinetic chain usage.",
                _ => null,
            };
        }

        // Improvement tips
        return featureName switch
        {
            string s when s.Contains("Wrist") && s.Contains("Velocity") =>
                "Work on wrist snap through contact for more racket head speed.",
            string s when s.Contains("Wrist") && s.Contains("Acceleration") =>
                "Focus on accelerating through the ball, not slowing down at contact.",
            string s when s.Contains("Shoulder") && s.Contains("Velocity") =>
                "Rotate your shoulders more fully during the swing.",
            string s when s.Contains("Shoulder") && s.Contains("Angle") =>
                "Turn your shoulders earlier in the backswing preparation.",
            string s when s.Contains("Hip") && s.Contains("Angle") =>
                "Load your hips during preparation and rotate through the shot.",
            string s when s.Contains("Hip") && s.Contains("Velocity") =>
                "Start the forward swing with hip rotation before arm movement.",
            string s when s.Contains("Elbow") && s.Contains("Angle") => strokeType
            == SwingType.Serve
                ? "Keep your elbow high during the trophy position."
                : "Maintain a comfortable elbow angle through contact.",
            string s when s.Contains("Knee") =>
                "Bend your knees more for better balance and power.",
            _ => null,
        };
    }
}
