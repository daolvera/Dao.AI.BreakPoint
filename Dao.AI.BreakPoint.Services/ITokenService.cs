using System.Security.Claims;
using Dao.AI.BreakPoint.Data.Models;
using Dao.AI.BreakPoint.Services.Responses;

namespace Dao.AI.BreakPoint.Services;

public interface ITokenService
{
    Task<TokenResponse> GenerateTokensAsync(AppUser user);
    Task<TokenResponse> RefreshTokenAsync(string refreshToken);
    ClaimsPrincipal? GetPrincipalFromExpiredToken(string token);
}
