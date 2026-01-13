using System.Security.Claims;
using Dao.AI.BreakPoint.Services;
using Dao.AI.BreakPoint.Services.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Dao.AI.BreakPoint.ApiService.Controllers;

[ApiController]
[Authorize]
[Route("[controller]")]
public class DrillsController(IDrillRecommendationService drillService) : ControllerBase
{
    /// <summary>
    /// Get all drill recommendations for a player
    /// </summary>
    [HttpGet("player/{playerId:int}")]
    [ProducesResponseType(typeof(IEnumerable<DrillRecommendationDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPlayerDrills(
        int playerId,
        [FromQuery] bool activeOnly = true
    )
    {
        var drills = await drillService.GetPlayerDrillsAsync(playerId, activeOnly);
        return Ok(drills);
    }

    /// <summary>
    /// Get drill recommendations for a specific analysis result
    /// </summary>
    [HttpGet("analysis/{analysisResultId:int}")]
    [ProducesResponseType(typeof(IEnumerable<DrillRecommendationDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAnalysisDrills(int analysisResultId)
    {
        var drills = await drillService.GetDrillsForAnalysisAsync(analysisResultId);
        return Ok(drills);
    }

    /// <summary>
    /// Get a specific drill recommendation
    /// </summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(DrillRecommendationDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetDrill(int id)
    {
        var drill = await drillService.GetDrillByIdAsync(id);
        return drill is not null ? Ok(drill) : NotFound();
    }

    /// <summary>
    /// Mark a drill as completed
    /// </summary>
    [HttpPost("{id:int}/complete")]
    [ProducesResponseType(typeof(DrillRecommendationDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CompleteDrill(int id)
    {
        var appUserId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var drill = await drillService.MarkDrillCompletedAsync(id, appUserId);
        return drill is not null ? Ok(drill) : NotFound();
    }

    /// <summary>
    /// Submit feedback for a drill (thumbs up/down + optional text)
    /// </summary>
    [HttpPost("{id:int}/feedback")]
    [ProducesResponseType(typeof(DrillRecommendationDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SubmitFeedback(int id, [FromBody] DrillFeedbackRequest request)
    {
        var appUserId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var drill = await drillService.SubmitFeedbackAsync(
            id,
            request.ThumbsUp,
            request.FeedbackText,
            appUserId
        );
        return drill is not null ? Ok(drill) : NotFound();
    }

    /// <summary>
    /// Dismiss/deactivate a drill recommendation
    /// </summary>
    [HttpPost("{id:int}/dismiss")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DismissDrill(int id)
    {
        var appUserId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var success = await drillService.DismissDrillAsync(id, appUserId);
        return success ? NoContent() : NotFound();
    }

    /// <summary>
    /// Get drill history with feedback for a player (for coaching AI context)
    /// </summary>
    [HttpGet("player/{playerId:int}/history")]
    [ProducesResponseType(typeof(IEnumerable<DrillRecommendationDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetDrillHistory(int playerId, [FromQuery] int limit = 20)
    {
        var drills = await drillService.GetDrillHistoryWithFeedbackAsync(playerId, limit);
        return Ok(drills);
    }
}
