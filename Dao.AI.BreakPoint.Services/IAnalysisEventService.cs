using Dao.AI.BreakPoint.Data.Models;

namespace Dao.AI.BreakPoint.Services;

public interface IAnalysisEventService
{
    Task<AnalysisEvent?> GetAnalysisEventAsync(string analysisEventId);
}
