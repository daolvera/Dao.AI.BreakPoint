using Dao.AI.BreakPoint.Services.DTOs;

namespace Dao.AI.BreakPoint.ApiService.Hubs;

/// <summary>
/// Client methods that can be called from the server
/// </summary>
public interface IAnalysisHubClient
{
    /// <summary>
    /// Called when analysis request status changes (processing, etc.)
    /// </summary>
    Task AnalysisStatusChanged(AnalysisRequestDto request);

    /// <summary>
    /// Called when analysis completes with results
    /// </summary>
    Task AnalysisCompleted(AnalysisResultDto result);

    /// <summary>
    /// Called when analysis fails
    /// </summary>
    Task AnalysisFailed(int analysisRequestId, string errorMessage);
}
