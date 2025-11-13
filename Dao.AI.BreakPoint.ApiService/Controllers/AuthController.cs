using Dao.AI.BreakPoint.Data.Models;
using Dao.AI.BreakPoint.Services.Requests;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Dao.AI.BreakPoint.ApiService.Controllers;

[ApiController]
[Route("[controller]")]
public class AuthController(IConfiguration Configuration) : ControllerBase
{
    [HttpGet("google")]
    [EndpointDescription("Begins the login using Google as the provided OAuth External Provider")]
    public ChallengeResult BeginGoogleLogin()
    {
        var redirectUrl = $"{Configuration["BreakPointAppUrl"]}/auth/callback";

        var properties = new AuthenticationProperties { RedirectUri = redirectUrl };

        return Challenge(
            properties,
            GoogleDefaults.AuthenticationScheme
        );
    }

    //[HttpGet("{oauthProvider}/callback")]
    //[EndpointDescription("Handle the external provider callback to complete authentication")]
    //public IActionResult HandleCallback(OAuthProvider oauthProvider)
    //{

    //}

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
}
