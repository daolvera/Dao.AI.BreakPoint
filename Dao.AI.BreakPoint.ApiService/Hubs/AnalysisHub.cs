using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace Dao.AI.BreakPoint.ApiService.Hubs;

/// <summary>
/// SignalR hub for real-time analysis notifications
/// </summary>
[Authorize]
public class AnalysisHub(ILogger<AnalysisHub> logger) : Hub<IAnalysisHubClient>
{
    /// <summary>
    /// Join a player-specific group to receive their analysis updates
    /// </summary>
    public async Task JoinPlayerGroup(int playerId)
    {
        var groupName = GetPlayerGroupName(playerId);
        logger.LogInformation(
            "Client {ConnectionId} joining player group {GroupName}",
            Context.ConnectionId,
            groupName
        );
        await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
    }

    /// <summary>
    /// Leave a player-specific group
    /// </summary>
    public async Task LeavePlayerGroup(int playerId)
    {
        var groupName = GetPlayerGroupName(playerId);
        logger.LogInformation(
            "Client {ConnectionId} leaving player group {GroupName}",
            Context.ConnectionId,
            groupName
        );
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);
    }

    /// <summary>
    /// Join a specific analysis event group for updates on that analysis
    /// </summary>
    public async Task JoinAnalysisGroup(int analysisId)
    {
        var groupName = GetAnalysisGroupName(analysisId);
        logger.LogInformation(
            "Client {ConnectionId} joining analysis group {GroupName}",
            Context.ConnectionId,
            groupName
        );
        await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
    }

    /// <summary>
    /// Leave an analysis-specific group
    /// </summary>
    public async Task LeaveAnalysisGroup(int analysisId)
    {
        var groupName = GetAnalysisGroupName(analysisId);
        logger.LogInformation(
            "Client {ConnectionId} leaving analysis group {GroupName}",
            Context.ConnectionId,
            groupName
        );
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);
    }

    public override async Task OnConnectedAsync()
    {
        logger.LogInformation(
            "Client {ConnectionId} connected to AnalysisHub",
            Context.ConnectionId
        );
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        logger.LogInformation(
            "Client {ConnectionId} disconnected from AnalysisHub. Exception: {Exception}",
            Context.ConnectionId,
            exception?.Message ?? "None"
        );
        await base.OnDisconnectedAsync(exception);
    }

    public static string GetPlayerGroupName(int playerId) => $"player-{playerId}";

    public static string GetAnalysisGroupName(int analysisId) => $"analysis-{analysisId}";
}
