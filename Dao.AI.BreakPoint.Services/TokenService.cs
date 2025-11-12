using Dao.AI.BreakPoint.Data;
using Dao.AI.BreakPoint.Data.Models;
using Dao.AI.BreakPoint.Services.Responses;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace Dao.AI.BreakPoint.Services;

public class TokenService(
    IConfiguration configuration,
    UserManager<AppUser> userManager,
    BreakPointDbContext context
    ) : ITokenService
{
    public async Task<TokenResponse> GenerateTokensAsync(AppUser user)
    {
        var claims = await BuildClaimsAsync(user);
        var accessToken = GenerateAccessToken(claims);
        var refreshToken = GenerateRefreshToken();

        user.RefreshToken = refreshToken;
        user.RefreshTokenExpiryTime = DateTime.UtcNow.AddDays(7);
        await userManager.UpdateAsync(user);

        return new TokenResponse
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            ExpiresAt = DateTime.UtcNow.AddMinutes(15), // 15-minute expiry
        };
    }

    public async Task<TokenResponse> RefreshTokenAsync(string refreshToken)
    {
        var principal = GetPrincipalFromExpiredToken(refreshToken);
        if (principal?.Identity?.Name == null)
        {
            throw new SecurityTokenException("Invalid refresh token");
        }

        var user = await userManager.FindByNameAsync(principal.Identity.Name)
            ?? throw new SecurityTokenException("User not found");
        if (
            string.IsNullOrEmpty(user.RefreshToken)
            || user.RefreshToken != refreshToken
            || user.RefreshTokenExpiryTime <= DateTime.UtcNow
        )
        {
            throw new SecurityTokenException("Invalid or expired refresh token");
        }

        return await GenerateTokensAsync(user);
    }

    public ClaimsPrincipal? GetPrincipalFromExpiredToken(string token)
    {
        var tokenValidationParameters = new TokenValidationParameters
        {
            ValidateAudience = false,
            ValidateIssuer = false,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(configuration["Jwt:Key"]!)
            ),
            ValidateLifetime = false, // Don't validate lifetime for refresh token validation
        };

        var tokenHandler = new JwtSecurityTokenHandler();
        var principal = tokenHandler.ValidateToken(
            token,
            tokenValidationParameters,
            out SecurityToken securityToken
        );

        if (
            securityToken is not JwtSecurityToken jwtSecurityToken
            || !jwtSecurityToken.Header.Alg.Equals(
                SecurityAlgorithms.HmacSha256,
                StringComparison.InvariantCultureIgnoreCase
            )
        )
        {
            throw new SecurityTokenException("Invalid token");
        }

        return principal;
    }

    private async Task<List<Claim>> BuildClaimsAsync(AppUser user)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.UserName ?? string.Empty),
            new(ClaimTypes.Email, user.Email ?? string.Empty),
            new("profile_complete", user.IsProfileComplete.ToString()),
        };

        // Add Player ID if user has completed profile
        if (user.IsProfileComplete)
        {
            var player = await context.Players.FirstOrDefaultAsync(p => p.AppUserId == user.Id);
            if (player != null)
            {
                claims.Add(new Claim("player_id", player.Id.ToString()));
            }
        }

        // Add roles
        var roles = await userManager.GetRolesAsync(user);
        claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));

        return claims;
    }

    private string GenerateAccessToken(IEnumerable<Claim> claims)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(configuration["Jwt:Key"]!));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: configuration["Jwt:Issuer"],
            audience: configuration["Jwt:Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(15), // 15-minute expiry
            signingCredentials: creds
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static string GenerateRefreshToken()
    {
        var randomNumber = new byte[64];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(randomNumber);
        return Convert.ToBase64String(randomNumber);
    }
}
