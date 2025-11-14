using Dao.AI.BreakPoint.Data.Models;
using Dao.AI.BreakPoint.Services;
using Dao.AI.BreakPoint.Services.DTOs;
using Dao.AI.BreakPoint.Services.Exceptions;
using Dao.AI.BreakPoint.Services.Requests;
using Dao.AI.BreakPoint.Services.Responses;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Dao.AI.BreakPoint.ApiService.Controllers;

[ApiController]
[Route("[controller]")]
public class AuthController(
    UserManager<AppUser> UserManager,
    ITokenService TokenService
    ) : ControllerBase
{
    [HttpGet("google")]
    [EndpointDescription("Begins the login using Google as the provided OAuth External Provider")]
    public ChallengeResult BeginGoogleLogin()
    {
        var redirectUrl = $"{HttpContext.Request.Scheme}://{HttpContext.Request.Host}/Auth/google/callback";

        var properties = new AuthenticationProperties { RedirectUri = redirectUrl };

        return Challenge(
            properties,
            GoogleDefaults.AuthenticationScheme
        );
    }

    [HttpGet("google/callback")]
    [EndpointDescription("Handle the external provider callback to complete authentication")]
    public async Task<IResult> HandleGoogleCallback()
    {
        var result = await HttpContext.AuthenticateAsync(GoogleDefaults.AuthenticationScheme);
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

        var user = await FindOrCreateUserFromProvider(email);


        if (user == null)
        {
            return Results.BadRequest("Failed to create user");
        }

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

        // TODO: need to redirect to complete profile if not complete
        return Results.Ok(authResponse);
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
        string email
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
                Player = new Player(),
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
}
