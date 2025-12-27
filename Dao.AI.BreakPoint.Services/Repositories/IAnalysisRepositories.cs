using Dao.AI.BreakPoint.Data.Models;
using Dao.AI.BreakPoint.Services.Requests;

namespace Dao.AI.BreakPoint.Services.Repositories;

public interface IAnalysisRequestRepository
{
    Task<int> AddAsync(AnalysisRequest entity, string? appUserId);
    Task<bool> UpdateAsync(AnalysisRequest entity, string? appUserId);
    Task<AnalysisRequest?> GetByIdAsync(int id);
    Task<bool> DeleteItemAsync(int id);
    Task<IEnumerable<AnalysisRequest>> GetValuesAsync(AnalysisRequestSearchRequest searchParams);
    Task<IEnumerable<AnalysisRequest>> GetPendingByPlayerIdAsync(int playerId);
}

public interface IAnalysisResultRepository
{
    Task<int> AddAsync(AnalysisResult entity, string? appUserId);
    Task<bool> UpdateAsync(AnalysisResult entity, string? appUserId);
    Task<AnalysisResult?> GetByIdAsync(int id);
    Task<AnalysisResult?> GetByRequestIdAsync(int requestId);
    Task<bool> DeleteItemAsync(int id);
    Task<IEnumerable<AnalysisResult>> GetByPlayerIdAsync(
        int playerId,
        int page = 1,
        int pageSize = 10
    );
    Task<int> GetCountByPlayerIdAsync(int playerId);
}
