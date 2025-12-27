using Dao.AI.BreakPoint.Data.Enums;
using Dao.AI.BreakPoint.Services.DTOs;
using Dao.AI.BreakPoint.Services.Exceptions;
using Dao.AI.BreakPoint.Services.Repositories;

namespace Dao.AI.BreakPoint.Services;

public class AnalysisService(
    IAnalysisRequestRepository analysisRequestRepository,
    IAnalysisResultRepository analysisResultRepository,
    IBlobStorageService blobStorageService,
    IPlayerRepository playerRepository
) : IAnalysisService
{
    public async Task<AnalysisRequestDto> UploadAndCreateAnalysisAsync(
        Stream videoStream,
        string fileName,
        string contentType,
        CreateAnalysisRequest request,
        string appUserId
    )
    {
        // Verify player exists
        var player =
            await playerRepository.GetByIdAsync(request.PlayerId)
            ?? throw new NotFoundException($"Player with ID {request.PlayerId}");

        // Create the analysis request first to get the ID
        var analysisRequest = request.ToModel();
        analysisRequest.Status = AnalysisStatus.Requested;

        var analysisRequestId = await analysisRequestRepository.AddAsync(
            analysisRequest,
            appUserId
        );

        try
        {
            // Upload video to blob storage using the analysis request ID as the filename
            // This allows the Azure Function to pick it up via blob trigger
            var blobFileName = $"{analysisRequestId}{Path.GetExtension(fileName)}";
            var blobUrl = await blobStorageService.UploadVideoAsync(
                videoStream,
                blobFileName,
                contentType
            );

            // Update the analysis request with the blob URL
            analysisRequest.VideoBlobUrl = blobUrl;
            await analysisRequestRepository.UpdateAsync(analysisRequest, appUserId);

            return AnalysisRequestDto.FromModel(analysisRequest);
        }
        catch (Exception ex)
        {
            // If blob upload fails, mark the analysis as failed
            analysisRequest.Status = AnalysisStatus.Failed;
            analysisRequest.ErrorMessage = $"Video upload failed: {ex.Message}";
            await analysisRequestRepository.UpdateAsync(analysisRequest, appUserId);
            throw;
        }
    }

    public async Task<AnalysisRequestDto?> GetRequestByIdAsync(int id)
    {
        var request = await analysisRequestRepository.GetByIdAsync(id);
        return request is null ? null : AnalysisRequestDto.FromModel(request);
    }

    public async Task<AnalysisResultDto?> GetResultByIdAsync(int id)
    {
        var result = await analysisResultRepository.GetByIdAsync(id);
        return result is null ? null : AnalysisResultDto.FromModel(result);
    }

    public async Task<AnalysisResultDto?> GetResultByRequestIdAsync(int requestId)
    {
        var result = await analysisResultRepository.GetByRequestIdAsync(requestId);
        return result is null ? null : AnalysisResultDto.FromModel(result);
    }

    public async Task<IEnumerable<AnalysisRequestDto>> GetPendingRequestsAsync(int playerId)
    {
        var requests = await analysisRequestRepository.GetPendingByPlayerIdAsync(playerId);
        return requests.Select(AnalysisRequestDto.FromModel);
    }

    public async Task<IEnumerable<AnalysisResultSummaryDto>> GetResultHistoryAsync(
        int playerId,
        int page = 1,
        int pageSize = 10
    )
    {
        var results = await analysisResultRepository.GetByPlayerIdAsync(playerId, page, pageSize);
        return results.Select(AnalysisResultSummaryDto.FromModel);
    }

    public async Task<bool> DeleteRequestAsync(int id, string appUserId)
    {
        var request =
            await analysisRequestRepository.GetByIdAsync(id)
            ?? throw new NotFoundException($"Analysis request with ID {id}");

        // Delete blob if it exists
        if (!string.IsNullOrEmpty(request.VideoBlobUrl))
        {
            try
            {
                await blobStorageService.DeleteAsync(request.VideoBlobUrl);
            }
            catch
            {
                // Log but don't fail if blob deletion fails
            }
        }

        // Delete result skeleton overlay if exists
        if (request.Result?.SkeletonOverlayUrl is not null)
        {
            try
            {
                await blobStorageService.DeleteAsync(request.Result.SkeletonOverlayUrl);
            }
            catch
            {
                // Log but don't fail if blob deletion fails
            }
        }

        return await analysisRequestRepository.DeleteItemAsync(id);
    }
}
