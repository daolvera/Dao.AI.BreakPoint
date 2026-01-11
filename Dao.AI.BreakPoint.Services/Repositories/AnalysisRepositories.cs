using Dao.AI.BreakPoint.Data;
using Dao.AI.BreakPoint.Data.Enums;
using Dao.AI.BreakPoint.Data.Models;
using Dao.AI.BreakPoint.Services.Requests;
using Microsoft.EntityFrameworkCore;

namespace Dao.AI.BreakPoint.Services.Repositories;

public class AnalysisRequestRepository
    : BaseRepository<AnalysisRequest, AnalysisRequestSearchRequest>,
        IAnalysisRequestRepository
{
    private BreakPointDbContext DbContext { get; init; }

    public AnalysisRequestRepository(BreakPointDbContext dbContext)
        : base(dbContext)
    {
        DbContext = dbContext;
    }

    public override IQueryable<AnalysisRequest> ApplySearchFilters(
        IQueryable<AnalysisRequest> query,
        AnalysisRequestSearchRequest searchParams
    )
    {
        if (searchParams.PlayerId.HasValue)
        {
            query = query.Where(a => a.PlayerId == searchParams.PlayerId.Value);
        }

        if (searchParams.RequestedId.HasValue)
        {
            query = query.Where(a => a.Id == searchParams.RequestedId.Value);
        }

        if (searchParams.Status.HasValue)
        {
            query = query.Where(a => a.Status == searchParams.Status.Value);
        }

        return query.OrderByDescending(a => a.CreatedAt);
    }

    public async Task<IEnumerable<AnalysisRequest>> GetPendingByPlayerIdAsync(int playerId)
    {
        return await DbContext
            .AnalysisRequests.Where(a =>
                a.PlayerId == playerId
                && a.Status != AnalysisStatus.Completed
                && a.Status != AnalysisStatus.Failed
            )
            .OrderByDescending(a => a.CreatedAt)
            .ToListAsync();
    }
}

public class AnalysisResultRepository
    : BaseRepository<AnalysisResult, AnalysisResultSearchRequest>,
        IAnalysisResultRepository
{
    private BreakPointDbContext DbContext { get; init; }

    public AnalysisResultRepository(BreakPointDbContext dbContext)
        : base(dbContext)
    {
        DbContext = dbContext;
    }

    public override IQueryable<AnalysisResult> ApplySearchFilters(
        IQueryable<AnalysisResult> query,
        AnalysisResultSearchRequest searchParams
    )
    {
        if (searchParams.PlayerId.HasValue)
        {
            query = query.Where(r => r.PlayerId == searchParams.PlayerId.Value);
        }

        return query.OrderByDescending(a => a.CreatedAt);
    }

    public async Task<AnalysisResult?> GetByRequestIdAsync(int requestId)
    {
        return await DbContext.AnalysisResults
            .Include(r => r.DrillRecommendations)
            .Include(r => r.PhaseDeviations)
            .Include(r => r.Player)
            .FirstOrDefaultAsync(r =>
                r.AnalysisRequestId == requestId
            );
    }

    public async Task<IEnumerable<AnalysisResult>> GetByPlayerIdAsync(
        int playerId,
        int page = 1,
        int pageSize = 10
    )
    {
        return await DbContext
            .AnalysisResults.Where(r => r.PlayerId == playerId)
            .OrderByDescending(r => r.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();
    }

    public async Task<int> GetCountByPlayerIdAsync(int playerId)
    {
        return await DbContext.AnalysisResults.CountAsync(r => r.PlayerId == playerId);
    }
}
