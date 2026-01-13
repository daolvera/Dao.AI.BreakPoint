using Dao.AI.BreakPoint.ApiService.Hubs;
using Dao.AI.BreakPoint.Services.DTOs;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace Dao.AI.BreakPoint.ApiService.Services;

public class AnalysisNotificationService(
    IHubContext<AnalysisHub, IAnalysisHubClient> hubContext,
    ILogger<AnalysisNotificationService> logger
) : IAnalysisNotificationService
{
    public async Task NotifyStatusChangedAsync(AnalysisRequestDto request)
    {
        var playerGroup = AnalysisHub.GetPlayerGroupName(request.PlayerId);
        var analysisGroup = AnalysisHub.GetAnalysisGroupName(request.Id);

        logger.LogInformation(
            "Sending AnalysisStatusChanged to groups: {PlayerGroup}, {AnalysisGroup} for request {RequestId}",
            playerGroup,
            analysisGroup,
            request.Id
        );

        await Task.WhenAll(
            hubContext.Clients.Group(playerGroup).AnalysisStatusChanged(request),
            hubContext.Clients.Group(analysisGroup).AnalysisStatusChanged(request)
        );

        logger.LogInformation(
            "AnalysisStatusChanged sent successfully for request {RequestId}",
            request.Id
        );
    }

    public async Task NotifyCompletedAsync(AnalysisResultDto result)
    {
        var playerGroup = AnalysisHub.GetPlayerGroupName(result.PlayerId);
        var analysisGroup = AnalysisHub.GetAnalysisGroupName(result.AnalysisRequestId);

        logger.LogInformation(
            "Sending AnalysisCompleted to groups: {PlayerGroup}, {AnalysisGroup} for result {ResultId}",
            playerGroup,
            analysisGroup,
            result.Id
        );

        await Task.WhenAll(
            hubContext.Clients.Group(playerGroup).AnalysisCompleted(result),
            hubContext.Clients.Group(analysisGroup).AnalysisCompleted(result)
        );

        logger.LogInformation(
            "AnalysisCompleted sent successfully for result {ResultId}",
            result.Id
        );
    }

    public async Task NotifyFailedAsync(int analysisRequestId, int playerId, string errorMessage)
    {
        var playerGroup = AnalysisHub.GetPlayerGroupName(playerId);
        var analysisGroup = AnalysisHub.GetAnalysisGroupName(analysisRequestId);

        logger.LogInformation(
            "Sending AnalysisFailed to groups: {PlayerGroup}, {AnalysisGroup} for request {RequestId}",
            playerGroup,
            analysisGroup,
            analysisRequestId
        );

        await Task.WhenAll(
            hubContext.Clients.Group(playerGroup).AnalysisFailed(analysisRequestId, errorMessage),
            hubContext.Clients.Group(analysisGroup).AnalysisFailed(analysisRequestId, errorMessage)
        );

        logger.LogInformation(
            "AnalysisFailed sent successfully for request {RequestId}",
            analysisRequestId
        );
    }
}
