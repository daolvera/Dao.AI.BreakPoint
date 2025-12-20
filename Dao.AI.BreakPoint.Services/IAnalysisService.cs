using Dao.AI.BreakPoint.Services.DTOs;

namespace Dao.AI.BreakPoint.Services;

public interface IAnalysisService
{
    /// <summary>
    /// Uploads a video and creates an analysis request
    /// </summary>
    /// <param name="videoStream">The video file stream</param>
    /// <param name="fileName">Original file name</param>
    /// <param name="contentType">Content type of the video</param>
    /// <param name="request">Analysis request details</param>
    /// <param name="appUserId">The user making the request</param>
    /// <returns>The created analysis request</returns>
    Task<AnalysisRequestDto> UploadAndCreateAnalysisAsync(
        Stream videoStream,
        string fileName,
        string contentType,
        CreateAnalysisRequest request,
        string appUserId
    );

    /// <summary>
    /// Gets an analysis request by ID (for checking status)
    /// </summary>
    Task<AnalysisRequestDto?> GetRequestByIdAsync(int id);

    /// <summary>
    /// Gets an analysis result by ID (completed analysis)
    /// </summary>
    Task<AnalysisResultDto?> GetResultByIdAsync(int id);

    /// <summary>
    /// Gets an analysis result by the analysis request ID
    /// </summary>
    Task<AnalysisResultDto?> GetResultByRequestIdAsync(int requestId);

    /// <summary>
    /// Gets pending analysis requests for a player
    /// </summary>
    Task<IEnumerable<AnalysisRequestDto>> GetPendingRequestsAsync(int playerId);

    /// <summary>
    /// Gets paginated analysis result history for a player
    /// </summary>
    Task<IEnumerable<AnalysisResultSummaryDto>> GetResultHistoryAsync(
        int playerId,
        int page = 1,
        int pageSize = 10
    );

    /// <summary>
    /// Deletes an analysis request and its associated blob
    /// </summary>
    Task<bool> DeleteRequestAsync(int id, string appUserId);
}
