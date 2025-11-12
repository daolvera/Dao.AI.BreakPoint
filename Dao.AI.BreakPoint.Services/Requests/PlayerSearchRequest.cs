namespace Dao.AI.BreakPoint.Services.Requests;

public class PlayerSearchRequest : PagedSearchRequest
{
    public string? PlayerName { get; set; }
    public string? Email { get; set; }
}
