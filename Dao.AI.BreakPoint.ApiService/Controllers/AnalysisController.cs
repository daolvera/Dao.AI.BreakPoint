using System.Security.Claims;
using Dao.AI.BreakPoint.Data.Enums;
using Dao.AI.BreakPoint.Services;
using Dao.AI.BreakPoint.Services.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Dao.AI.BreakPoint.ApiService.Controllers;

[ApiController]
[Authorize]
[Route("[controller]")]
public class AnalysisController(IAnalysisService analysisService) : ControllerBase
{
    private const long MaxVideoSizeBytes = 100 * 1024 * 1024; // 100MB
    private static readonly string[] AllowedVideoTypes =
    [
        "video/mp4",
        "video/quicktime",
        "video/x-msvideo",
    ];
    private static readonly string[] AllowedExtensions = [".mp4", ".mov", ".avi"];

    /// <summary>
    /// Upload a video for analysis
    /// </summary>
    [HttpPost("upload")]
    [ProducesResponseType(typeof(AnalysisRequestDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [RequestSizeLimit(MaxVideoSizeBytes)]
    public async Task<IActionResult> UploadVideo(
        IFormFile video,
        [FromQuery] int playerId,
        [FromQuery] SwingType strokeType
    )
    {
        // Validate file
        if (video is null || video.Length == 0)
        {
            return BadRequest("No video file provided");
        }

        if (video.Length > MaxVideoSizeBytes)
        {
            return BadRequest(
                $"Video file too large. Maximum size is {MaxVideoSizeBytes / 1024 / 1024}MB"
            );
        }

        var extension = Path.GetExtension(video.FileName).ToLowerInvariant();
        if (!AllowedExtensions.Contains(extension))
        {
            return BadRequest(
                $"Invalid file type. Allowed types: {string.Join(", ", AllowedExtensions)}"
            );
        }

        if (!AllowedVideoTypes.Contains(video.ContentType.ToLowerInvariant()))
        {
            return BadRequest(
                $"Invalid content type. Allowed types: {string.Join(", ", AllowedVideoTypes)}"
            );
        }

        var appUserId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;

        var createRequest = new CreateAnalysisRequest
        {
            PlayerId = playerId,
            StrokeType = strokeType,
        };

        await using var stream = video.OpenReadStream();
        var result = await analysisService.UploadAndCreateAnalysisAsync(
            stream,
            video.FileName,
            video.ContentType,
            createRequest,
            appUserId
        );

        return CreatedAtAction(nameof(GetRequestById), new { id = result.Id }, result);
    }

    /// <summary>
    /// Get analysis request by ID
    /// </summary>
    [HttpGet("request/{id:int}")]
    [ProducesResponseType(typeof(AnalysisRequestDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetRequestById(int id)
    {
        var request = await analysisService.GetRequestByIdAsync(id);
        return request is not null ? Ok(request) : NotFound();
    }

    /// <summary>
    /// Get analysis result by request ID
    /// </summary>
    [HttpGet("result/{requestId:int}")]
    [ProducesResponseType(typeof(AnalysisResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetResultByRequestId(int requestId)
    {
        var result = await analysisService.GetResultByRequestIdAsync(requestId);
        return result is not null ? Ok(result) : NotFound();
    }

    /// <summary>
    /// Get pending analysis requests for a player
    /// </summary>
    [HttpGet("player/{playerId:int}/pending")]
    [ProducesResponseType(typeof(IEnumerable<AnalysisRequestDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPendingRequests(int playerId)
    {
        var requests = await analysisService.GetPendingRequestsAsync(playerId);
        return Ok(requests);
    }

    /// <summary>
    /// Get completed analysis results for a player
    /// </summary>
    [HttpGet("player/{playerId:int}/history")]
    [ProducesResponseType(typeof(IEnumerable<AnalysisResultSummaryDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetResultHistory(
        int playerId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10
    )
    {
        var results = await analysisService.GetResultHistoryAsync(playerId, page, pageSize);
        return Ok(results);
    }

    /// <summary>
    /// Delete an analysis request and its result
    /// </summary>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteAnalysis(int id)
    {
        var appUserId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        await analysisService.DeleteRequestAsync(id, appUserId);
        return NoContent();
    }
}
