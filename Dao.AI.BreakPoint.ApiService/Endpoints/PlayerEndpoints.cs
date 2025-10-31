using Dao.AI.BreakPoint.Services;
using Dao.AI.BreakPoint.Services.DTOs;
using Microsoft.AspNetCore.Mvc;

namespace Dao.AI.BreakPoint.ApiService.Endpoints;

public static class PlayerEndpoints
{
    public static IEndpointRouteBuilder MapPlayerEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/players")
            .WithTags("Players");

        group.MapGet("/", GetAllPlayers)
            .WithName(nameof(GetAllPlayers))
            .WithSummary("Get all players")
            .Produces<ICollection<PlayerDto>>();

        group.MapGet("/{id:int}", GetPlayerById)
            .WithName(nameof(GetPlayerById))
            .WithSummary("Get player by ID")
            .Produces<PlayerDto>()
            .Produces(404);

        group.MapGet("/{id:int}/details", GetPlayerWithStatsById)
            .WithName(nameof(GetPlayerWithStatsById))
            .WithSummary("Get player by ID with enhanced stats")
            .Produces<PlayerWithStatsDto>()
            .Produces(404);

        group.MapPost("/", CreatePlayer)
            .WithName(nameof(CreatePlayer))
            .WithSummary("Create a new player")
            .Produces<int>(201)
            .Produces(400);

        group.MapPut("/{id:int}", UpdatePlayer)
            .WithName(nameof(UpdatePlayer))
            .WithSummary("Update an existing player")
            .Produces<bool>()
            .Produces(404)
            .Produces(400);

        group.MapDelete("/{id:int}", DeletePlayer)
            .WithName(nameof(DeletePlayer))
            .WithSummary("Delete a player")
            .Produces(204)
            .Produces(404);

        return endpoints;
    }

    private static async Task<IResult> GetAllPlayers(
        [FromServices] IPlayerService playerService
        )
    {
        return Results.Ok(await playerService.GetAllAsync());
    }

    private static async Task<IResult> GetPlayerById(
        [FromServices] IPlayerService playerService,
        [FromRoute] int id)
    {
        var player = await playerService.GetByIdAsync(id);
        return player is not null ? Results.Ok(player) : Results.NotFound();
    }

    private static async Task<IResult> GetPlayerWithStatsById(
        [FromServices] IPlayerService playerService,
        [FromRoute] int id)
    {
        PlayerWithStatsDto? player = await playerService.GetWithStatsAsync(id);
        return player is not null ? Results.Ok(player) : Results.NotFound();
    }

    private static async Task<IResult> CreatePlayer(
        [FromServices] IPlayerService playerService,
        [FromServices] IAuthenticationService authService,
        [FromBody] CreatePlayerDto createPlayerDto)
    {
        var playerId = await playerService.CreateAsync(createPlayerDto, await authService.GetAppUserId());

        return Results.Created($"/api/players/{playerId}", createPlayerDto);
    }

    private static async Task<IResult> UpdatePlayer(
        [FromServices] IPlayerService playerService,
        [FromServices] IAuthenticationService authService,
        [FromRoute] int id,
        [FromBody] CreatePlayerDto updatedPlayer)
    {
        return Results.Ok(await playerService.UpdateAsync(id, updatedPlayer, await authService.GetAppUserId()));
    }

    // TODO: implement role based access to do this
    private static async Task<IResult> DeletePlayer(
        [FromServices] IPlayerService playerService,
        [FromRoute] int id)
    {
        await playerService.DeleteAsync(id);

        return Results.NoContent();
    }
}