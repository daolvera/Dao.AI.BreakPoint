using Dao.AI.BreakPoint.ApiService.Hubs;
using Dao.AI.BreakPoint.Services.DTOs;
using Microsoft.AspNetCore.SignalR;

namespace Dao.AI.BreakPoint.ApiService.Services;

public class AnalysisNotificationService(IHubContext<AnalysisHub, IAnalysisHubClient> hubContext)
    : IAnalysisNotificationService
{
    public async Task NotifyStatusChangedAsync(AnalysisRequestDto request)
    {
        var playerGroup = AnalysisHub.GetPlayerGroupName(request.PlayerId);
        var analysisGroup = AnalysisHub.GetAnalysisGroupName(request.Id);

        await Task.WhenAll(
            hubContext.Clients.Group(playerGroup).AnalysisStatusChanged(request),
            hubContext.Clients.Group(analysisGroup).AnalysisStatusChanged(request)
        );
    }

    public async Task NotifyCompletedAsync(AnalysisResultDto result)
    {
        var playerGroup = AnalysisHub.GetPlayerGroupName(result.PlayerId);
        var analysisGroup = AnalysisHub.GetAnalysisGroupName(result.AnalysisRequestId);

        await Task.WhenAll(
            hubContext.Clients.Group(playerGroup).AnalysisCompleted(result),
            hubContext.Clients.Group(analysisGroup).AnalysisCompleted(result)
        );
    }

    public async Task NotifyFailedAsync(int analysisRequestId, int playerId, string errorMessage)
    {
        var playerGroup = AnalysisHub.GetPlayerGroupName(playerId);
        var analysisGroup = AnalysisHub.GetAnalysisGroupName(analysisRequestId);

        await Task.WhenAll(
            hubContext.Clients.Group(playerGroup).AnalysisFailed(analysisRequestId, errorMessage),
            hubContext.Clients.Group(analysisGroup).AnalysisFailed(analysisRequestId, errorMessage)
        );
    }
}
