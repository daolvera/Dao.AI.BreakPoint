using Dao.AI.BreakPoint.Data.Enums;
using Dao.AI.BreakPoint.Data.Models;
using Dao.AI.BreakPoint.Services.MoveNet;
using Dao.AI.BreakPoint.Services.Options;
using Dao.AI.BreakPoint.Services.Repositories;
using Dao.AI.BreakPoint.Services.VideoProcessing;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace Dao.AI.BreakPoint.Services.SwingAnalyzer;

public class SwingAnalyzerService(
    IVideoProcessingService VideoProcessingService,
    ISkeletonOverlayService SkeletonOverlayService,
    IBlobStorageService BlobStorageService,
    ICoachingService CoachingService,
    IPlayerRepository PlayerRepository,
    IAnalysisRequestRepository AnalysisRequestRepository,
    IOptions<MoveNetOptions> MoveNetOptions,
    IOptions<SwingPhaseClassifierOptions> PhaseClassifierOptions,
    IOptions<SwingQualityModelOptions> SwingQualityOptions
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

        // Analyze each swing and aggregate results
        var swingResults = new List<SwingQualityResult>();
        var qualityOptions = SwingQualityOptions.Value;

        using var qualityService = new SwingQualityInferenceService(qualityOptions.ModelPath);

        foreach (var swing in swingVideo.Swings)
        {
            // Pass SwingData directly - no preprocessing needed
            var result = qualityService.RunInference(swing);
            swingResults.Add(result);
        }

        // Aggregate results (use best swing for now, could average in future)
        var bestResult = swingResults.OrderByDescending(r => r.QualityScore).First();
        var bestSwingIndex = swingResults.IndexOf(bestResult);
        var bestSwing = swingVideo.Swings[bestSwingIndex];

        // Create or update the AnalysisResult
        analysisRequest.Result ??= new AnalysisResult
        {
            AnalysisRequestId = analysisRequest.Id,
            PlayerId = analysisRequest.PlayerId,
            StrokeType = analysisRequest.StrokeType,
            VideoBlobUrl = analysisRequest.VideoBlobUrl,
        };

        // Populate the result with quality score and feature importance
        analysisRequest.Result.QualityScore = bestResult.QualityScore;
        analysisRequest.Result.FeatureImportanceJson = JsonSerializer.Serialize(
            bestResult.FeatureImportance
        );

        // Generate coaching tips using AI coaching service
        var ustaRating = player?.UstaRating ?? 3.0; // Default to intermediate
        var coachingTips = await CoachingService.GenerateCoachingTipsAsync(
            analysisRequest.StrokeType,
            bestResult.QualityScore,
            bestResult.FeatureImportance,
            ustaRating
        );
        analysisRequest.Result.CoachingTipsJson = JsonSerializer.Serialize(coachingTips);

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
        await AnalysisRequestRepository.UpdateAsync(analysisRequest, analysisRequest.CreatedByAppUserId);
    }

    /// <summary>
    /// Generate skeleton overlay GIF and upload to blob storage
    /// </summary>
    private async Task GenerateAndUploadSkeletonOverlayAsync(
        List<byte[]> allFrames,
        SwingData swing,
        SwingQualityResult result,
        int videoWidth,
        int videoHeight,
        AnalysisRequest analysisRequest
    )
    {
        try
        {
            // Extract only the frames that correspond to this swing
            var swingFrameImages = new List<byte[]>();
            for (int i = 0; i < swing.Frames.Count && i < allFrames.Count; i++)
            {
                // Assuming swing frames align with video frames
                // In practice, you'd use frame indices from SwingData
                swingFrameImages.Add(allFrames[Math.Min(i, allFrames.Count - 1)]);
            }

            if (swingFrameImages.Count == 0)
            {
                return; // No frames to process
            }

            // Generate the skeleton overlay GIF
            var gifBytes = SkeletonOverlayService.GenerateOverlayGif(
                swingFrameImages,
                swing,
                result.FeatureImportance,
                result.QualityScore,
                videoWidth,
                videoHeight,
                frameDelayMs: 50 // ~20 FPS
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
            if (analysisRequest.Result != null)
            {
                analysisRequest.Result.SkeletonOverlayGifUrl = gifUrl;
            }
        }
        catch (Exception)
        {
            // Log error but don't fail the analysis if overlay generation fails
            // The skeleton overlay is supplementary to the main analysis
        }
    }
}
