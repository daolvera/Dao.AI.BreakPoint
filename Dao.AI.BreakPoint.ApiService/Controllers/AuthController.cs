using System.Security.Claims;
using Dao.AI.BreakPoint.ApiService.Configuration;
using Dao.AI.BreakPoint.ApiService.Utilities;
using Dao.AI.BreakPoint.Data.Models;
using Dao.AI.BreakPoint.Services;
using Dao.AI.BreakPoint.Services.DTOs;
using Dao.AI.BreakPoint.Services.Exceptions;
using Dao.AI.BreakPoint.Services.Repositories;
using Dao.AI.BreakPoint.Services.Requests;
using Dao.AI.BreakPoint.Services.Responses;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Protocols.Configuration;

namespace Dao.AI.BreakPoint.ApiService.Controllers;

[ApiController]
[Route("[controller]")]
public class AuthController(
    UserManager<AppUser> UserManager,
    ITokenService TokenService,
    IPlayerService PlayerService,
    IAppUserRepository AppUserRepository,
    IConfiguration Configuration,
    ILogger<AuthController> Logger
) : ControllerBase
{
    [Authorize]
    [HttpGet("me")]
    [ProducesResponseType(typeof(UserDto), 200)]
    [ProducesResponseType(401)]
    public async Task<IActionResult> GetUserDetails()
    {
        AppUser? user = await UserManager.FindByIdAsync(User.GetAppUserId());
        if (user == null)
        {
            return Unauthorized();
        }
        var player =
            await PlayerService.GetByAppUserIdAsync(user.Id)
            ?? throw new NotFoundException($"Player for {user.UserName}");
        return Ok(
            new UserDto
            {
                Id = user.Id,
                Email = user.Email,
                DisplayName = user.DisplayName,
                IsProfileComplete = user.IsProfileComplete,
                PlayerId = player.Id,
            }
        );
    }

    [Authorize]
    [HttpDelete("logout")]
    public IActionResult Logout()
    {
        Response.Cookies.Delete(CodeLookUps.AccessTokenCookieKey);
        Response.Cookies.Delete(CodeLookUps.RefreshTokenCookieKey);
        Response.Cookies.Delete(CodeLookUps.IsAuthenticatedCookieKey);

        return Ok();
    }

    [HttpGet("refresh")]
    [EnableRateLimiting(RateLimitingConfiguration.AnonymousPolicy)]
    public async Task<IActionResult> RefreshToken()
    {
        var refreshToken = Request.Cookies[CodeLookUps.RefreshTokenCookieKey];
        if (string.IsNullOrEmpty(refreshToken))
        {
            return Unauthorized();
        }
        AppUser? userFromRefresh = await AppUserRepository.GetByRefreshTokenAsync(refreshToken);
        if (userFromRefresh is null || userFromRefresh.RefreshTokenExpiry <= DateTime.UtcNow)
        {
            return Unauthorized();
        }
        SetSecureTokenCookies(await TokenService.GenerateTokenAsync(userFromRefresh));
        return Ok();
    }

    [Authorize]
    [HttpPost("complete")]
    public async Task<IActionResult> CompleteProfile(
        [FromBody] CompleteProfileRequest completeProfileRequest
    )
    {
        var wasSuccessfullyCompleted = await PlayerService.CompleteAsync(
            completeProfileRequest,
            User.GetAppUserId()
        );
        return wasSuccessfullyCompleted ? Ok() : BadRequest();
    }

    [HttpGet("challenge")]
    [EnableRateLimiting(RateLimitingConfiguration.AnonymousPolicy)]
    [EndpointDescription("Handle the external provider callback to complete authentication")]
    public async Task<IActionResult> HandleChallenge()
    {
        var result = await HttpContext.AuthenticateAsync(
            CookieAuthenticationDefaults.AuthenticationScheme
        );
        if (!result.Succeeded)
        {
            return BadRequest("Authentication failed");
        }

        var id = result.Principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var email = result.Principal.FindFirst(ClaimTypes.Email)?.Value;
        var name = result.Principal.FindFirst(ClaimTypes.Name)?.Value;

        string applicationBaseUrl =
            Configuration["BreakPointAppUrl"]
            ?? throw new InvalidConfigurationException("BreakPointAppUrl is not configured");
        if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(email) || string.IsNullOrEmpty(name))
        {
            return Redirect($"{applicationBaseUrl}/auth/error?message=missing_claims");
        }

        var user = await FindOrCreateUserFromProvider(email, name);

        if (user == null)
        {
            return Redirect($"{applicationBaseUrl}/auth/error?message=missing_user");
        }

        try
        {
            var tokens = await TokenService.GenerateTokenAsync(user);

            SetSecureTokenCookies(tokens);
            string redirectUrl = user.IsProfileComplete
                ? $"{applicationBaseUrl}"
                : $"{applicationBaseUrl}/auth/complete";
            return Redirect(redirectUrl);
        }
        catch (Exception ex)
        {
            Logger.LogError(
                ex,
                "Unexpected error during external provider login for email: {Email}",
                email
            );
            return Redirect($"{applicationBaseUrl}/auth/error?message=unexpected_error");
        }
    }

    #region Google Login
    [HttpGet("google")]
    [EnableRateLimiting(RateLimitingConfiguration.AnonymousPolicy)]
    [EndpointDescription("Begins the login using Google as the provided OAuth External Provider")]
    public ChallengeResult BeginGoogleLogin()
    {
        var properties = new AuthenticationProperties
        {
            RedirectUri = Url.Action(nameof(HandleChallenge)),
        };

        return Challenge(properties, GoogleDefaults.AuthenticationScheme);
    }
    #endregion

    private async Task<AppUser?> FindOrCreateUserFromProvider(string email, string name)
    {
        var user = await UserManager.FindByEmailAsync(email);
        if (user is null)
        {
            user = new AppUser
            {
                UserName = email,
                Email = email,
                CreatedAt = DateTime.UtcNow,
                IsProfileComplete = false,
                DisplayName = name,
                Player = new Player() { Name = name },
                EmailConfirmed = true, // External providers pre-verify emails
            };

            var createResult = await UserManager.CreateAsync(user);
            if (!createResult.Succeeded)
            {
                return null;
            }
        }

        return user;
    }

    private void SetSecureTokenCookies(TokenResponse tokens)
    {
        // Get shared domain for cross-subdomain cookies (e.g., .politedune-xxx.westus.azurecontainerapps.io)
        var cookieDomain = GetSharedCookieDomain();

        var cookieOptions = new CookieOptions
        {
            HttpOnly = true, // Prevents JavaScript access
            Secure = true, // HTTPS only
            SameSite = SameSiteMode.Lax, // Allows redirects from OAuth provider
            Expires = tokens.ExpiresAt,
            Domain = cookieDomain,
        };

        var refreshCookieOptions = new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Lax, // Allows redirects from OAuth provider
            Expires = DateTime.UtcNow.AddDays(7), // Refresh token expires in 7 days
            Domain = cookieDomain,
        };

        // Set access token cookie
        Response.Cookies.Append(
            CodeLookUps.AccessTokenCookieKey,
            tokens.AccessToken,
            cookieOptions
        );

        // Set refresh token cookie
        Response.Cookies.Append(
            CodeLookUps.RefreshTokenCookieKey,
            tokens.RefreshToken,
            refreshCookieOptions
        );

        Response.Cookies.Append(
            CodeLookUps.IsAuthenticatedCookieKey,
            "true",
            new CookieOptions
            {
                Secure = true,
                SameSite = SameSiteMode.Lax, // Allows redirects from OAuth provider
                Expires = tokens.ExpiresAt,
                Domain = cookieDomain,
            }
        );
    }

    private string? GetSharedCookieDomain()
    {
        var host = Request.Host.Host;
        // For localhost, don't set domain (cookies work on same origin)
        if (host == "localhost" || host == "127.0.0.1")
            return null;

        // Extract parent domain: breakpointapi.politedune-xxx.westus.azurecontainerapps.io
        // becomes .politedune-xxx.westus.azurecontainerapps.io
        var parts = host.Split('.');
        if (parts.Length > 1)
        {
            return "." + string.Join(".", parts.Skip(1));
        }
        return null;
    }
}
