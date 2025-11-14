using Microsoft.AspNetCore.Identity;

namespace Dao.AI.BreakPoint.Data.Models;

public class AppUser : IdentityUser
{
    public DateTime CreatedAt { get; set; }
    public int? CreatedByAppUserId { get; set; }

    // Profile completion tracking
    public bool IsProfileComplete { get; set; } = false;

    // Refresh token storage for security
    public string? RefreshToken { get; set; }
    public DateTime? RefreshTokenExpiry { get; set; }
    public string? DisplayName { get; set; }
    public Player Player { get; set; } = new();
}
