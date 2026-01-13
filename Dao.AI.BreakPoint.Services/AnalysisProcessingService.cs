using System.Text.Json;
using Dao.AI.BreakPoint.Data.Enums;
using Dao.AI.BreakPoint.Data.Models;
using Dao.AI.BreakPoint.Services.Exceptions;
using Dao.AI.BreakPoint.Services.Repositories;

namespace Dao.AI.BreakPoint.Services;

/// <summary>
/// Service for processing analysis requests (used by Azure Function)
/// </summary>
public class AnalysisProcessingService(
    IAnalysisRequestRepository analysisRequestRepository,
    IAnalysisResultRepository analysisResultRepository
) : IAnalysisProcessingService
{
    public async Task<AnalysisRequest?> GetRequestAsync(int analysisRequestId)
    {
        return await analysisRequestRepository.GetByIdAsync(analysisRequestId);
    }

    public async Task UpdateStatusAsync(
        int analysisRequestId,
        AnalysisStatus status,
        string? errorMessage = null
    )
    {
        var request =
            await analysisRequestRepository.GetByIdAsync(analysisRequestId)
            ?? throw new NotFoundException($"Analysis request with ID {analysisRequestId}");

        request.Status = status;
        if (!string.IsNullOrEmpty(errorMessage))
        {
            request.ErrorMessage = errorMessage;
        }

        await analysisRequestRepository.UpdateAsync(request, appUserId: null);
    }

    public async Task<AnalysisResult> CreateResultAsync(
        int analysisRequestId,
        double qualityScore,
        Dictionary<string, double> featureImportance,
        string? skeletonOverlayUrl
    )
    {
        var request =
            await analysisRequestRepository.GetByIdAsync(analysisRequestId)
            ?? throw new NotFoundException($"Analysis request with ID {analysisRequestId}");

        // Create the result
        var result = new AnalysisResult
        {
            AnalysisRequestId = analysisRequestId,
            PlayerId = request.PlayerId,
            StrokeType = request.StrokeType,
            QualityScore = qualityScore,
            // Phase scores and deviations are populated separately by PhaseQualityInferenceService
            SkeletonOverlayUrl = skeletonOverlayUrl,
            VideoBlobUrl = request.VideoBlobUrl,
        };

        await analysisResultRepository.AddAsync(result, appUserId: null);

        // Mark the request as completed
        request.Status = AnalysisStatus.Completed;
        await analysisRequestRepository.UpdateAsync(request, appUserId: null);

        return result;
    }
}
