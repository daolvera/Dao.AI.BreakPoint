using Dao.AI.BreakPoint.Data.Models;
using Dao.AI.BreakPoint.Services.Responses;
using System.Security.Claims;

namespace Dao.AI.BreakPoint.Services;

public interface ITokenService
{
    Task<TokenResponse> GenerateTokenAsync(AppUser user);
    Task<TokenResponse> RefreshTokenAsync(string refreshToken);
    ClaimsPrincipal? GetPrincipalFromExpiredToken(string token);
}
