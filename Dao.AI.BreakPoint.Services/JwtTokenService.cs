using Dao.AI.BreakPoint.Data.Models;
using Dao.AI.BreakPoint.Services.Options;
using Dao.AI.BreakPoint.Services.Repositories;
using Dao.AI.BreakPoint.Services.Responses;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace Dao.AI.BreakPoint.Services;

public class JwtTokenService(
    IAppUserRepository appUserRepository,
    IOptions<JwtOptions> jwtOptions
    ) : ITokenService
{
    public async Task<TokenResponse> GenerateTokenAsync(AppUser user)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id),
            new(ClaimTypes.Email, user.Email!),
            new(JwtRegisteredClaimNames.Sub, user.UserName ?? string.Empty),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };
        var key = GetSecurityKey(jwtOptions.Value.Key);
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var expires = DateTime.UtcNow.AddMinutes(jwtOptions.Value.AccessTokenExpiryMinutes);

        var token = new JwtSecurityToken(
            jwtOptions.Value.Issuer,
            jwtOptions.Value.Audience,
            claims,
            expires: expires,
            signingCredentials: creds
        );

        string accessToken = new JwtSecurityTokenHandler().WriteToken(token);

        var refreshTokenExpiry = DateTime.UtcNow.AddDays(jwtOptions.Value.RefreshTokenExpiryDays);
        string refreshToken = GenerateRefreshToken();
        await appUserRepository.SaveRefreshTokenAsync(user.Id, refreshToken, refreshTokenExpiry);

        return new TokenResponse
        {
            AccessToken = accessToken,
            ExpiresAt = expires,
            RefreshToken = refreshToken
        };
    }

    // TODO DAO: decide if I need
    public ClaimsPrincipal? GetPrincipalFromExpiredToken(string token)
    {
        throw new NotImplementedException();
    }

    public async Task<TokenResponse> RefreshTokenAsync(string refreshToken)
    {
        AppUser? user = await appUserRepository.GetByRefreshTokenAsync(refreshToken);
        // TODO DAO: make it easier to handle errors in controller with correct error codes
        if (user == null)
        {
            throw new UnauthorizedAccessException("Invalid refresh token.");
        }
        if (user.RefreshTokenExpiry == null || user.RefreshTokenExpiry < DateTime.Now)
        {
            throw new UnauthorizedAccessException("Refresh token has expired.");
        }

        var accessToken = await GenerateTokenAsync(user);

        return accessToken;
    }

    public static SymmetricSecurityKey GetSecurityKey(string jwtKey)
    {
        return new SymmetricSecurityKey(Encoding.ASCII.GetBytes(jwtKey));
    }

    private static string GenerateRefreshToken()
    {
        var randomBytes = new byte[64];
        using var rng = System.Security.Cryptography.RandomNumberGenerator.Create();
        rng.GetBytes(randomBytes);
        return Convert.ToBase64String(randomBytes);
    }
}
