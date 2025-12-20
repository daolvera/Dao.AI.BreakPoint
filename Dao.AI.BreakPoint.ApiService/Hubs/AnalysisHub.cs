using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Dao.AI.BreakPoint.ApiService.Hubs;

/// <summary>
/// SignalR hub for real-time analysis notifications
/// </summary>
[Authorize]
public class AnalysisHub : Hub<IAnalysisHubClient>
{
    /// <summary>
    /// Join a player-specific group to receive their analysis updates
    /// </summary>
    public async Task JoinPlayerGroup(int playerId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, GetPlayerGroupName(playerId));
    }

    /// <summary>
    /// Leave a player-specific group
    /// </summary>
    public async Task LeavePlayerGroup(int playerId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, GetPlayerGroupName(playerId));
    }

    /// <summary>
    /// Join a specific analysis event group for updates on that analysis
    /// </summary>
    public async Task JoinAnalysisGroup(int analysisId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, GetAnalysisGroupName(analysisId));
    }

    /// <summary>
    /// Leave an analysis-specific group
    /// </summary>
    public async Task LeaveAnalysisGroup(int analysisId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, GetAnalysisGroupName(analysisId));
    }

    public static string GetPlayerGroupName(int playerId) => $"player-{playerId}";

    public static string GetAnalysisGroupName(int analysisId) => $"analysis-{analysisId}";
}
