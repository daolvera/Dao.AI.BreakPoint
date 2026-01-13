using Dao.AI.BreakPoint.Data.Models;

namespace Dao.AI.BreakPoint.Services;

/// <summary>
/// Client interface for sending analysis notifications to the API service.
/// Used by background services (e.g., Azure Functions) to notify the API
/// when analysis completes, allowing SignalR notifications to be sent.
/// </summary>
public interface IAnalysisNotificationClient
{
    /// <summary>
    /// Notify that an analysis has completed successfully
    /// </summary>
    Task NotifyCompletedAsync(AnalysisResult result, CancellationToken cancellationToken = default);

    /// <summary>
    /// Notify that an analysis has failed
    /// </summary>
    Task NotifyFailedAsync(int analysisRequestId, int playerId, string errorMessage, CancellationToken cancellationToken = default);

    /// <summary>
    /// Notify that an analysis status has changed
    /// </summary>
    Task NotifyStatusChangedAsync(AnalysisRequest request, CancellationToken cancellationToken = default);
}
