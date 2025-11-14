namespace Dao.AI.BreakPoint.Services.Requests;

public abstract class PagedSearchRequest
{
    public string? FuzzySearch { get; set; }
    public int? RequestedId { get; set; }
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 15;
    public int? AppUserId { get; set; }
}
