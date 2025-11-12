using Dao.AI.BreakPoint.Data.Models;
using Dao.AI.BreakPoint.Services;
using Dao.AI.BreakPoint.Services.DTOs;
using Dao.AI.BreakPoint.Services.Exceptions;
using Dao.AI.BreakPoint.Services.Requests;
using Dao.AI.BreakPoint.Services.Responses;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Dao.AI.BreakPoint.ApiService.Endpoints;

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/auth").WithTags("Authentication");

        // Google OAuth login
        group
            .MapGet("/google", GoogleLogin)
            .WithName("GoogleLogin")
            .WithSummary("Initiate Google OAuth login")
            .AllowAnonymous();

        group
            .MapGet("/google/callback", GoogleCallback)
            .WithName("GoogleCallback")
            .WithSummary("Handle Google OAuth callback")
            .AllowAnonymous();

        group
            .MapGet("/callback", GoogleCallback)
            .WithName("GoogleCallbackAlternate")
            .WithSummary("Handle Google OAuth callback (alternate route)")
            .AllowAnonymous();

        // Future: Microsoft OAuth (ready to implement)
        // group.MapGet("/microsoft", MicrosoftLogin).WithName("MicrosoftLogin").AllowAnonymous();
        // group.MapGet("/microsoft/callback", MicrosoftCallback).WithName("MicrosoftCallback").AllowAnonymous();

        // Token refresh
        group
            .MapPost("/refresh", RefreshToken)
            .WithName("RefreshToken")
            .WithSummary("Refresh access token")
            .AllowAnonymous();

        // Profile completion
        group
            .MapPost("/complete-profile", CompleteProfile)
            .WithName("CompleteProfile")
            .WithSummary("Complete user profile after OAuth")
            .RequireAuthorization();

        // Check authentication status
        group
            .MapGet("/me", GetCurrentUser)
            .WithName("GetCurrentUser")
            .WithSummary("Get current authenticated user")
            .RequireAuthorization();

        return endpoints;
    }

    private static IResult GoogleLogin(
        HttpContext context,
        string? returnUrl = null
        )
    {
        var redirectUrl = $"https://{context.Request.Host}/auth/callback";

        if (!string.IsNullOrEmpty(returnUrl))
        {
            redirectUrl += $"?returnUrl={Uri.EscapeDataString(returnUrl)}";
        }

        var properties = new AuthenticationProperties { RedirectUri = redirectUrl };

        return Results.Challenge(properties, [GoogleDefaults.AuthenticationScheme]);
    }

    private static async Task<IResult> GoogleCallback(
        HttpContext context,
        UserManager<AppUser> userManager,
        ITokenService tokenService,
        string? returnUrl = null
    )
    {
        var result = await context.AuthenticateAsync(GoogleDefaults.AuthenticationScheme);
        if (!result.Succeeded)
        {
            return Results.BadRequest("Google authentication failed");
        }

        var googleId = result.Principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var email = result.Principal.FindFirst(ClaimTypes.Email)?.Value;
        var name = result.Principal.FindFirst(ClaimTypes.Name)?.Value;

        if (string.IsNullOrEmpty(googleId) || string.IsNullOrEmpty(email))
        {
            return Results.BadRequest("Required claims not found");
        }

        var user = await FindOrCreateUserFromProvider(
            userManager,
            email,
            OAuthProvider.Google,
            googleId
        );

        if (user == null)
        {
            return Results.BadRequest("Failed to create user");
        }

        // Generate tokens
        var tokens = await tokenService.GenerateTokensAsync(user);

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
                IsProfileComplete = user.IsProfileComplete,
                ExternalProvider = user.ExternalProvider,
            },
        };

        // For SPA, we'll return JSON instead of redirecting
        // The frontend will handle the redirect based on profile completion status
        return Results.Ok(authResponse);
    }

    private static async Task<IResult> RefreshToken(
        [FromBody] RefreshTokenRequest request,
        [FromServices] ITokenService tokenService
    )
    {
        try
        {
            var tokens = await tokenService.RefreshTokenAsync(request.RefreshToken);
            return Results.Ok(
                new RefreshTokenResponse()
                {
                    AccessToken = tokens.AccessToken,
                    RefreshToken = tokens.RefreshToken,
                    ExpiresAt = tokens.ExpiresAt,
                }
            );
        }
        catch (Exception)
        {
            return Results.Unauthorized();
        }
    }

    private static async Task<IResult> CompleteProfile(
        [FromBody] CompleteProfileRequest request,
        [FromServices] IPlayerService playerService,
        [FromServices] UserManager<AppUser> userManager,
        [FromServices] ITokenService tokenService,
        ClaimsPrincipal user
    )
    {
        var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!int.TryParse(userId, out var userIdInt))
        {
            return Results.BadRequest("Invalid user ID");
        }

        var appUser = await userManager.FindByIdAsync(userId!);
        if (appUser == null)
        {
            return Results.NotFound("User not found");
        }

        if (appUser.IsProfileComplete)
        {
            return Results.BadRequest("Profile already completed");
        }

        // Create player profile
        var createPlayerDto = new CreatePlayerDto { Name = request.Name, Email = appUser.Email };

        var playerId = await playerService.CreateAsync(createPlayerDto, userIdInt);

        // Update user profile completion status
        appUser.IsProfileComplete = true;
        await userManager.UpdateAsync(appUser);

        // Generate new tokens with updated claims
        var tokens = await tokenService.GenerateTokensAsync(appUser);

        return Results.Ok(
            new
            {
                accessToken = tokens.AccessToken,
                refreshToken = tokens.RefreshToken,
                expiresAt = tokens.ExpiresAt,
                playerId = playerId,
            }
        );
    }

    private static async Task<IResult> GetCurrentUser(
        ClaimsPrincipal user,
        [FromServices] UserManager<AppUser> userManager
    )
    {
        var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
        {
            return Results.Unauthorized();
        }

        var appUser = await userManager.FindByIdAsync(userId);
        if (appUser == null)
        {
            return Results.NotFound();
        }

        var playerIdClaim = user.FindFirst("player_id")?.Value;
        int.TryParse(playerIdClaim, out var playerId);

        return Results.Ok(
            new UserDto
            {
                Id = appUser.Id,
                Email = appUser.Email!,
                Name = appUser.UserName!,
                IsProfileComplete = appUser.IsProfileComplete,
                PlayerId = playerId,
                ExternalProvider = appUser.ExternalProvider,
            }
        );
    }

    // Helper method for future provider support
    private static async Task<AppUser?> FindOrCreateUserFromProvider(
        UserManager<AppUser> userManager,
        string email,
        OAuthProvider provider,
        string providerId
    )
    {
        var user = await userManager.FindByEmailAsync(email);
        if (user is null)
        {
            user = new AppUser
            {
                UserName = email,
                Email = email,
                ExternalProvider = provider,
                ExternalProviderId = providerId,
                CreatedAt = DateTime.UtcNow,
                IsProfileComplete = false,
                Player = new Player(),
                EmailConfirmed = true, // External providers pre-verify emails
            };

            var createResult = await userManager.CreateAsync(user);
            if (!createResult.Succeeded)
            {
                return null;
            }
        }
        else if (
            user is not null &&
            user.ExternalProvider != provider
        )
        {
            throw new EmailAlreadyExistsException(user.ExternalProvider);
        }

        return user;
    }
}
