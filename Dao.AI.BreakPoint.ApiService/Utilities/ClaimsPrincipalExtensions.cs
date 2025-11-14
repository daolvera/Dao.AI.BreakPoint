using System.Security.Claims;

namespace Dao.AI.BreakPoint.ApiService.Utilities;

public static class ClaimsPrincipalExtensions
{
    public static string GetAppUserId(this ClaimsPrincipal user)
    {
        // Look for the JWT claim
        var claim = user.Claims.FirstOrDefault(c => c.Type == "sub")?.Value;

        if (claim is null)
        {
            // Try the application cookie claim
            claim = user.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value;
        }

        if (claim is null)
        {
            throw new ArgumentException("User is not authenticated");
        }

        return claim;
    }
}
