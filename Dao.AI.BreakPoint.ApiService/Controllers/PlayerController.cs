using Dao.AI.BreakPoint.Services;
using Dao.AI.BreakPoint.Services.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Dao.AI.BreakPoint.ApiService.Controllers;

[ApiController]
[Authorize]
[Route("api/players")]
public class PlayerController(IPlayerService playerService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(ICollection<PlayerDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAllPlayers()
    {
        var players = await playerService.GetAllAsync();
        return Ok(players);
    }

    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(PlayerDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetPlayerById(int id)
    {
        var player = await playerService.GetByIdAsync(id);
        return player is not null ? Ok(player) : NotFound();
    }

    [HttpGet("{id:int}/details")]
    [ProducesResponseType(typeof(PlayerWithStatsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetPlayerWithStatsById(int id)
    {
        PlayerWithStatsDto? player = await playerService.GetWithStatsAsync(id);
        return player is not null ? Ok(player) : NotFound();
    }

    [HttpPost]
    [ProducesResponseType(typeof(int), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreatePlayer(CreatePlayerDto createPlayerDto)
    {
        var playerId = await playerService.CreateAsync(createPlayerDto, 1);
        return CreatedAtAction(nameof(GetPlayerById), new { id = playerId }, playerId);
    }

    [HttpPut("{id:int}")]
    [ProducesResponseType(typeof(bool), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpdatePlayer(int id, CreatePlayerDto updatedPlayer)
    {
        var result = await playerService.UpdateAsync(id, updatedPlayer, 1);
        return Ok(result);
    }

    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeletePlayer(int id)
    {
        // TODO: implement role based access to do this
        await playerService.DeleteAsync(id);
        return NoContent();
    }
}