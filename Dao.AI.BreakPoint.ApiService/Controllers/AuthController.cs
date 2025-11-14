using Dao.AI.BreakPoint.Data.Models;
using Dao.AI.BreakPoint.Services;
using Dao.AI.BreakPoint.Services.DTOs;
using Dao.AI.BreakPoint.Services.Exceptions;
using Dao.AI.BreakPoint.Services.Requests;
using Dao.AI.BreakPoint.Services.Responses;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Protocols.Configuration;
using System.Security.Claims;

namespace Dao.AI.BreakPoint.ApiService.Controllers;

[ApiController]
[Route("[controller]")]
public class AuthController(
    UserManager<AppUser> UserManager,
    ITokenService TokenService,
    IConfiguration Configuration,
    ILogger<AuthController> Logger
    ) : ControllerBase
{
    [HttpGet("google")]
    [EndpointDescription("Begins the login using Google as the provided OAuth External Provider")]
    public ChallengeResult BeginGoogleLogin()
    {
        var properties = new AuthenticationProperties { RedirectUri = Url.Action(nameof(HandleGoogleChallenge)) };

        return Challenge(
            properties,
            GoogleDefaults.AuthenticationScheme
        );
    }

    [HttpGet("google/challenge")]
    [EndpointDescription("Handle the external provider callback to complete authentication")]
    public async Task<IActionResult> HandleGoogleChallenge()
    {
        var result = await HttpContext.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        if (!result.Succeeded)
        {
            return BadRequest("Google authentication failed");
        }

        var googleId = result.Principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var email = result.Principal.FindFirst(ClaimTypes.Email)?.Value;
        var name = result.Principal.FindFirst(ClaimTypes.Name)?.Value;

        string applicationBaseUrl = Configuration["BreakPointAppUrl"] ??
            throw new InvalidConfigurationException("BreakPointAppUrl is not configured");
        if (string.IsNullOrEmpty(googleId) || string.IsNullOrEmpty(email))
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

            // Prepare response
            var authResponse = new AuthorizationResponse
            {
                AccessToken = tokens.AccessToken,
                RefreshToken = tokens.RefreshToken,
                ExpiresAt = tokens.ExpiresAt,
                User = new UserDto
                {
                    Id = user.Id,
                    Email = user.Email!,
                    Name = name ?? user.Email!,
                    IsProfileComplete = user.IsProfileComplete
                },
            };

            var redirectUrl = user.IsProfileComplete
                ? $"{applicationBaseUrl}/profile"
                : $"{applicationBaseUrl}/complete-profile";

            SetSecureTokenCookies(tokens);

            return Redirect(redirectUrl);
        }
        catch (EmailAlreadyExistsException ex)
        {
            Logger.LogWarning(ex, "Email already exists during external provider login for email: {Email}", email);
            return Redirect($"{applicationBaseUrl}/auth/error?message=email_exists");
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Unexpected error during external provider login for email: {Email}", email);
            return Redirect($"{applicationBaseUrl}/auth/error?message=unexpected_error");
        }
    }

    [HttpGet("google/callback")]
    [EndpointDescription("Handle the external provider callback to complete authentication")]
    public async Task<IActionResult> HandleGoogleCallback()
    {
        await Task.CompletedTask;
        return Ok();
    }

    //[HttpPost("refresh")]
    //public IActionResult RefreshToken([FromBody] RefreshTokenRequest refreshTokenRequest)
    //{

    //}

    [Authorize]
    [HttpPost("complete")]
    public IActionResult CompleteProfile([FromBody] CompleteProfileRequest completeProfileRequest)
    {
        // get the Player id from the User
        Player newPlayer = null!; // TODO: change to the user's player that was created previously during the handle back stage
        newPlayer.DisplayName = completeProfileRequest.Name;
        newPlayer.UstaRating = completeProfileRequest.UstaRating;
        // save those
        return Ok();
    }

    // Helper method for future provider support
    private async Task<AppUser?> FindOrCreateUserFromProvider(
        string email,
        string name
    )
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
                Player = new Player()
                {
                    DisplayName = name
                },
                EmailConfirmed = true, // External providers pre-verify emails
            };

            var createResult = await UserManager.CreateAsync(user);
            if (!createResult.Succeeded)
            {
                return null;
            }
        }
        else if (
            user is not null
        )
        {
            throw new EmailAlreadyExistsException();
        }

        return user;
    }

    private void SetSecureTokenCookies(TokenResponse tokens)
    {
        var cookieOptions = new CookieOptions
        {
            HttpOnly = true,          // Prevents JavaScript access
            Secure = true,            // HTTPS only
            SameSite = SameSiteMode.Strict,  // CSRF protection
            Expires = tokens.ExpiresAt
        };

        var refreshCookieOptions = new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            Expires = DateTime.UtcNow.AddDays(7) // Refresh token expires in 7 days
        };

        // Set access token cookie
        Response.Cookies.Append("access_token", tokens.AccessToken, cookieOptions);

        // Set refresh token cookie
        Response.Cookies.Append("refresh_token", tokens.RefreshToken, refreshCookieOptions);

        Response.Cookies.Append("user_authenticated", "true", new CookieOptions
        {
            Secure = true,
            SameSite = SameSiteMode.Strict,
            Expires = tokens.ExpiresAt
        });
    }
}
