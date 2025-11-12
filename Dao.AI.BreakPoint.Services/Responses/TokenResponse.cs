namespace Dao.AI.BreakPoint.Services.Responses;

public class TokenResponse
{
    public required string AccessToken { get; set; }
    public required string RefreshToken { get; set; }
    public DateTime ExpiresAt { get; set; }
}
