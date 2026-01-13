using Dao.AI.BreakPoint.Data.Enums;
using Dao.AI.BreakPoint.Data.Models;
using Dao.AI.BreakPoint.Services.MoveNet;
using Dao.AI.BreakPoint.Services.Options;
using Dao.AI.BreakPoint.Services.Repositories;
using Dao.AI.BreakPoint.Services.VideoProcessing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Dao.AI.BreakPoint.Services.SwingAnalyzer;

public class SwingAnalyzerService(
    IVideoProcessingService VideoProcessingService,
    ISkeletonOverlayService SkeletonOverlayService,
    IBlobStorageService BlobStorageService,
    ICoachingService CoachingService,
    IPlayerRepository PlayerRepository,
    IAnalysisRequestRepository AnalysisRequestRepository,
    IDrillRecommendationRepository DrillRecommendationRepository,
    IAnalysisNotificationClient NotificationClient,
    ILogger<SwingAnalyzerService> Logger,
    IOptions<MoveNetOptions> MoveNetOptions,
    IOptions<SwingPhaseClassifierOptions> PhaseClassifierOptions,
    IOptions<SwingQualityModelOptions> SwingQualityOptions,
    IOptions<CoachingOptions> CoachingOpts
) : ISwingAnalyzerService
{
    public async Task AnalyzeSwingAsync(Stream videoStream, AnalysisRequest analysisRequest)
    {
        // save the stream to a temporary file
        string tempFilePath = Path.GetTempFileName();
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

        Player? player =
            analysisRequest.Player ?? await PlayerRepository.GetByIdAsync(analysisRequest.PlayerId);
        // Get player's handedness for swing analysis
        var isRightHanded = player?.Handedness != Handedness.LeftHanded;

        using var processor = new MoveNetVideoProcessor(
            MoveNetOptions.Value.ModelPath,
            PhaseClassifierOptions.Value.ModelPath
        );
        var swingVideo = processor.ProcessVideoFrames(frameImages, metadata, isRightHanded);

        if (swingVideo.Swings.Count == 0)
        {
            throw new InvalidOperationException("No valid swings detected in the video.");
        }

        // Analyze each swing using the phase-aware quality service
        var qualityOptions = SwingQualityOptions.Value;
        using var qualityService = new PhaseQualityInferenceService(
            qualityOptions.ModelsDirectory,
            qualityOptions.ReferenceProfilesPath
        );

        var swingResults = new List<PhaseQualityResult>();
        foreach (var swing in swingVideo.Swings)
        {
            var result = qualityService.RunInference(
                swing,
                analysisRequest.StrokeType,
                isRightHanded
            );
            swingResults.Add(result);
        }

        // Aggregate results (use best swing for now, could average in future)
        var bestResult = swingResults.OrderByDescending(r => r.OverallScore).First();
        var bestSwingIndex = swingResults.IndexOf(bestResult);
        var bestSwing = swingVideo.Swings[bestSwingIndex];
        analysisRequest.Result?.UpdatedAt = DateTime.UtcNow;
        analysisRequest.Result?.UpdatedByAppUserId = analysisRequest.CreatedByAppUserId;

        // Create or update the AnalysisResult
        analysisRequest.Result ??= new AnalysisResult
        {
            AnalysisRequestId = analysisRequest.Id,
            PlayerId = analysisRequest.PlayerId,
            StrokeType = analysisRequest.StrokeType,
            VideoBlobUrl = analysisRequest.VideoBlobUrl,
            CreatedAt = DateTime.UtcNow,
            CreatedByAppUserId = analysisRequest.CreatedByAppUserId,
        };

        // Populate the result with quality scores
        analysisRequest.Result.QualityScore = bestResult.OverallScore;

        // Populate phase scores (Preparation phase is not scored)
        if (bestResult.PhaseResults.TryGetValue(SwingPhase.Backswing, out var backswingResult))
            analysisRequest.Result.BackswingScore = (int)backswingResult.Score;
        if (bestResult.PhaseResults.TryGetValue(SwingPhase.Contact, out var contactResult))
            analysisRequest.Result.ContactScore = (int)contactResult.Score;
        if (bestResult.PhaseResults.TryGetValue(SwingPhase.FollowThrough, out var followResult))
            analysisRequest.Result.FollowThroughScore = (int)followResult.Score;

        // Build phase analyses from the quality results for drill recommendations
        var phaseAnalyses = bestResult
            .PhaseResults.Where(p => p.Value.FrameCount > 0)
            .Select(p => new PhaseAnalysisInput(
                Phase: p.Key,
                Score: (int)p.Value.Score,
                Deviations: p.Value.Deviations.ToDictionary(
                    d => d.FeatureName,
                    d => (double)d.ZScore
                )
            ))
            .ToList();

        // Fetch recent drills for context
        var coachingOptions = CoachingOpts.Value;
        var recentDrills = await DrillRecommendationRepository.GetRecentDrillsAsync(
            analysisRequest.PlayerId,
            coachingOptions.RecentDrillsCount
        );

        // Generate drill recommendations using AI coaching service
        var ustaRating = player?.UstaRating ?? 3.0; // Default to intermediate
        var drillInput = new DrillRecommendationInput(
            PlayerId: analysisRequest.PlayerId,
            StrokeType: analysisRequest.StrokeType,
            UstaRating: ustaRating,
            OverallQualityScore: (int)bestResult.OverallScore,
            PhaseAnalyses: phaseAnalyses,
            RecentDrills: recentDrills,
            PlayerHistorySummary: player?.TrainingHistorySummary,
            MaxDrills: coachingOptions.MaxDrillsPerAnalysis
        );

        var generatedDrills = await CoachingService.GenerateDrillRecommendationsAsync(drillInput);

        // Convert generated drills to DrillRecommendation entities
        foreach (var drill in generatedDrills)
        {
            analysisRequest.Result.DrillRecommendations.Add(
                new Data.Models.DrillRecommendation
                {
                    PlayerId = analysisRequest.PlayerId,
                    TargetPhase = drill.TargetPhase,
                    TargetFeature = drill.TargetFeature,
                    DrillName = drill.DrillName,
                    Description = drill.Description,
                    SuggestedDuration = drill.SuggestedDuration,
                    Priority = drill.Priority,
                    IsActive = true,
                }
            );
        }

        // Generate and update the player's training history summary
        if (player != null && generatedDrills.Count > 0)
        {
            var historyInput = new TrainingHistoryInput(
                PlayerName: player.Name,
                UstaRating: ustaRating,
                StrokeType: analysisRequest.StrokeType,
                OverallQualityScore: (int)bestResult.OverallScore,
                NewDrills: generatedDrills,
                PreviousHistorySummary: player.TrainingHistorySummary
            );

            var newSummary = await CoachingService.GenerateTrainingHistorySummaryAsync(
                historyInput
            );

            // Truncate if needed to keep within limits
            if (newSummary.Length > coachingOptions.MaxHistorySummaryLength)
            {
                newSummary = newSummary[..coachingOptions.MaxHistorySummaryLength];
            }

            player.TrainingHistorySummary = newSummary;
            await PlayerRepository.UpdateAsync(player, analysisRequest.CreatedByAppUserId);
        }

        // Generate skeleton overlay GIF for the best swing
        await GenerateAndUploadSkeletonOverlayAsync(
            frameImages,
            bestSwing,
            bestResult,
            metadata.Width,
            metadata.Height,
            analysisRequest
        );

        analysisRequest.Status = AnalysisStatus.Completed;
        await AnalysisRequestRepository.UpdateAsync(
            analysisRequest,
            analysisRequest.CreatedByAppUserId
        );

        // Send SignalR notification that analysis is complete
        if (analysisRequest.Result is not null)
        {
            Logger.LogInformation(
                "Sending completion notification for analysis request {RequestId}",
                analysisRequest.Id
            );
            await NotificationClient.NotifyCompletedAsync(analysisRequest.Result);
        }
    }

    /// <summary>
    /// Generate skeleton overlay GIF and key frame image, then upload to blob storage
    /// </summary>
    private async Task GenerateAndUploadSkeletonOverlayAsync(
        List<byte[]> allFrames,
        SwingData swing,
        PhaseQualityResult result,
        int videoWidth,
        int videoHeight,
        AnalysisRequest analysisRequest
    )
    {
        try
        {
            // Extract only the frames that correspond to this swing using actual frame indices
            var swingFrameImages = new List<byte[]>();
            foreach (var frameData in swing.Frames)
            {
                // Use the actual frame index from the FrameData
                int frameIndex = frameData.FrameIndex;
                if (frameIndex >= 0 && frameIndex < allFrames.Count)
                {
                    swingFrameImages.Add(allFrames[frameIndex]);
                }
            }

            if (swingFrameImages.Count == 0)
            {
                return; // No frames to process
            }

            // Map feature deviations to feature importance for overlay
            var allDeviations = result
                .PhaseResults.Values.SelectMany(p => p.Deviations)
                .OrderByDescending(d => Math.Abs(d.ZScore))
                .ToList();
            var featureImportance = CoachingFeatureMapper.MapDeviationsToFeatureImportance(
                allDeviations
            );

            // Find the worst frame for the static key frame image
            var worstFrameIndex = SkeletonOverlayService.FindWorstFrameIndex(
                swing,
                featureImportance
            );

            // Generate the static key frame image (PNG)
            if (
                worstFrameIndex >= 0
                && worstFrameIndex < swingFrameImages.Count
                && worstFrameIndex < swing.Frames.Count
            )
            {
                var keyFrameImage = swingFrameImages[worstFrameIndex];
                var keyFrameData = swing.Frames[worstFrameIndex];

                var imageBytes = SkeletonOverlayService.GenerateOverlayImage(
                    keyFrameImage,
                    keyFrameData,
                    featureImportance,
                    result.OverallScore,
                    videoWidth,
                    videoHeight
                );

                if (imageBytes.Length > 0)
                {
                    var imageFileName = $"skeleton-overlay-{analysisRequest.Id}.png";
                    using var imageStream = new MemoryStream(imageBytes);

                    var imageUrl = await BlobStorageService.UploadImageAsync(
                        imageStream,
                        imageFileName,
                        "image/png"
                    );

                    if (analysisRequest.Result != null)
                    {
                        analysisRequest.Result.SkeletonOverlayUrl = imageUrl;
                    }
                }
            }

            // Generate the skeleton overlay GIF
            var gifBytes = SkeletonOverlayService.GenerateOverlayGif(
                swingFrameImages,
                swing,
                featureImportance,
                result.OverallScore,
                videoWidth,
                videoHeight,
                frameDelayMs: 150 // ~6.7 FPS - slower for better analysis viewing
            );

            if (gifBytes.Length == 0)
            {
                return; // No GIF generated
            }

            // Upload to blob storage
            var fileName = $"skeleton-overlay-{analysisRequest.Id}.gif";
            using var gifStream = new MemoryStream(gifBytes);

            var gifUrl = await BlobStorageService.UploadImageAsync(
                gifStream,
                fileName,
                "image/gif"
            );

            // Store the URL in the result
            analysisRequest.Result?.SkeletonOverlayGifUrl = gifUrl;
        }
        catch (Exception ex)
        {
            // Log error but don't fail the analysis if overlay generation fails
            // The skeleton overlay is supplementary to the main analysis
            Logger.LogError(
                ex,
                "Failed to generate skeleton overlay for analysis request {RequestId}",
                analysisRequest.Id
            );
        }
    }
}
