using Dao.AI.BreakPoint.Services.DTOs;

namespace Dao.AI.BreakPoint.ApiService.Services;

public interface IAnalysisNotificationService
{
    Task NotifyCompletedAsync(AnalysisResultDto result);
    Task NotifyFailedAsync(int analysisRequestId, int playerId, string errorMessage);
    Task NotifyStatusChangedAsync(AnalysisRequestDto request);
}