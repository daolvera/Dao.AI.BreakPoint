using System.Net.Http.Json;
using Dao.AI.BreakPoint.Data.Models;
using Microsoft.Extensions.Logging;

namespace Dao.AI.BreakPoint.Services;

/// <summary>
/// HTTP client for sending analysis notifications to the API service.
/// Used by background services (e.g., Azure Functions) to trigger SignalR notifications.
/// </summary>
public class AnalysisNotificationClient(
    HttpClient httpClient,
    ILogger<AnalysisNotificationClient> logger
) : IAnalysisNotificationClient
{
    public async Task NotifyCompletedAsync(AnalysisResult result, CancellationToken cancellationToken = default)
    {
        try
        {
            var notification = new AnalysisCompletedNotification(result.Id);
            var response = await httpClient.PostAsJsonAsync(
                "internal/AnalysisNotifications/completed",
                notification,
                cancellationToken
            );

            if (!response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync(cancellationToken);
                logger.LogWarning(
                    "Failed to send completion notification for result {ResultId}. Status: {StatusCode}, Response: {Response}",
                    result.Id,
                    response.StatusCode,
                    content
                );
            }
            else
            {
                logger.LogInformation(
                    "Successfully sent completion notification for result {ResultId}",
                    result.Id
                );
            }
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Error sending completion notification for result {ResultId}",
                result.Id
            );
            // Don't throw - notification failure shouldn't fail the analysis
        }
    }

    public async Task NotifyFailedAsync(
        int analysisRequestId,
        int playerId,
        string errorMessage,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            var notification = new AnalysisFailedNotification(analysisRequestId, playerId, errorMessage);
            var response = await httpClient.PostAsJsonAsync(
                "internal/AnalysisNotifications/failed",
                notification,
                cancellationToken
            );

            if (!response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync(cancellationToken);
                logger.LogWarning(
                    "Failed to send failure notification for request {RequestId}. Status: {StatusCode}, Response: {Response}",
                    analysisRequestId,
                    response.StatusCode,
                    content
                );
            }
            else
            {
                logger.LogInformation(
                    "Successfully sent failure notification for request {RequestId}",
                    analysisRequestId
                );
            }
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Error sending failure notification for request {RequestId}",
                analysisRequestId
            );
            // Don't throw - notification failure shouldn't fail the analysis
        }
    }

    public async Task NotifyStatusChangedAsync(AnalysisRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            var notification = new AnalysisStatusChangedNotification(request.Id);
            var response = await httpClient.PostAsJsonAsync(
                "internal/AnalysisNotifications/status-changed",
                notification,
                cancellationToken
            );

            if (!response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync(cancellationToken);
                logger.LogWarning(
                    "Failed to send status change notification for request {RequestId}. Status: {StatusCode}, Response: {Response}",
                    request.Id,
                    response.StatusCode,
                    content
                );
            }
            else
            {
                logger.LogInformation(
                    "Successfully sent status change notification for request {RequestId}",
                    request.Id
                );
            }
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Error sending status change notification for request {RequestId}",
                request.Id
            );
            // Don't throw - notification failure shouldn't fail the analysis
        }
    }
}

/// <summary>
/// Notification payload for completed analysis
/// </summary>
public record AnalysisCompletedNotification(int AnalysisResultId);

/// <summary>
/// Notification payload for failed analysis
/// </summary>
public record AnalysisFailedNotification(int AnalysisRequestId, int PlayerId, string ErrorMessage);

/// <summary>
/// Notification payload for status change
/// </summary>
public record AnalysisStatusChangedNotification(int AnalysisRequestId);
