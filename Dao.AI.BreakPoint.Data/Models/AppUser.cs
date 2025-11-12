using Microsoft.AspNetCore.Identity;

namespace Dao.AI.BreakPoint.Data.Models;

public class AppUser : IdentityUser<int>
{
    public DateTime CreatedAt { get; set; }
    public int? CreatedByAppUserId { get; set; }

    // External login provider information
    public required OAuthProvider ExternalProvider { get; set; }
    public required string ExternalProviderId { get; set; }

    // Profile completion tracking
    public bool IsProfileComplete { get; set; } = false;

    // Refresh token storage for security
    public string? RefreshToken { get; set; }
    public DateTime? RefreshTokenExpiryTime { get; set; }
    public required Player Player { get; set; }
}

public enum OAuthProvider
{
    Google,
    Microsoft,
}
