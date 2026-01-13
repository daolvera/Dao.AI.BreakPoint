using Dao.AI.BreakPoint.ApiService.Services;
using Dao.AI.BreakPoint.Services;
using Dao.AI.BreakPoint.Services.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Dao.AI.BreakPoint.ApiService.Controllers;

/// <summary>
/// Internal controller for receiving analysis notifications from background services
/// (e.g., Azure Functions). This enables SignalR notifications to be sent to connected clients.
/// </summary>
[ApiController]
[Route("internal/[controller]")]
[AllowAnonymous] // Internal service-to-service communication; consider adding API key auth for production
public class AnalysisNotificationsController(
    IAnalysisNotificationService notificationService,
    IAnalysisService analysisService
) : ControllerBase
{
    /// <summary>
    /// Notify that an analysis has completed successfully
    /// </summary>
    [HttpPost("completed")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> NotifyCompleted(
        [FromBody] AnalysisCompletedNotification notification
    )
    {
        var result = await analysisService.GetResultByIdAsync(notification.AnalysisResultId);
        if (result is null)
        {
            return NotFound($"Analysis result with ID {notification.AnalysisResultId} not found");
        }

        await notificationService.NotifyCompletedAsync(result);
        return Ok();
    }

    /// <summary>
    /// Notify that an analysis has failed
    /// </summary>
    [HttpPost("failed")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> NotifyFailed(
        [FromBody] AnalysisFailedNotification notification
    )
    {
        await notificationService.NotifyFailedAsync(
            notification.AnalysisRequestId,
            notification.PlayerId,
            notification.ErrorMessage
        );
        return Ok();
    }

    /// <summary>
    /// Notify that an analysis status has changed
    /// </summary>
    [HttpPost("status-changed")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> NotifyStatusChanged(
        [FromBody] AnalysisStatusChangedNotification notification
    )
    {
        var request = await analysisService.GetRequestByIdAsync(notification.AnalysisRequestId);
        if (request is null)
        {
            return NotFound($"Analysis request with ID {notification.AnalysisRequestId} not found");
        }

        await notificationService.NotifyStatusChangedAsync(request);
        return Ok();
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
