using Dao.AI.BreakPoint.Data.Enums;

namespace Dao.AI.BreakPoint.Services.Requests;

public class AnalysisRequestSearchRequest : PagedSearchRequest
{
    public int? PlayerId { get; set; }
    public AnalysisStatus? Status { get; set; }
}
