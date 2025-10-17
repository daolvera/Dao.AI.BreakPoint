using Dao.AI.BreakPoint.Data;
using Dao.AI.BreakPoint.Data.Models;
using Microsoft.EntityFrameworkCore;

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
            .Produces<List<Player>>();

        group.MapGet("/{id:int}", GetPlayerById)
            .WithName(nameof(GetPlayerById))
            .WithSummary("Get player by ID")
            .Produces<Player>()
            .Produces(404);

        group.MapPost("/", CreatePlayer)
            .WithName(nameof(CreatePlayer))
            .WithSummary("Create a new player")
            .Produces<Player>(201)
            .Produces(400);

        group.MapPut("/{id:int}", UpdatePlayer)
            .WithName(nameof(UpdatePlayer))
            .WithSummary("Update an existing player")
            .Produces<Player>()
            .Produces(404)
            .Produces(400);

        group.MapDelete("/{id:int}", DeletePlayer)
            .WithName(nameof(DeletePlayer))
            .WithSummary("Delete a player")
            .Produces(204)
            .Produces(404);

        return endpoints;
    }

    private static async Task<IResult> GetAllPlayers(BreakPointDbContext context)
    {
        var players = await context.Players.ToListAsync();
        return Results.Ok(players);
    }

    private static async Task<IResult> GetPlayerById(int id, BreakPointDbContext context)
    {
        var player = await context.Players.FindAsync(id);
        return player is not null ? Results.Ok(player) : Results.NotFound();
    }

    private static async Task<IResult> CreatePlayer(Player player, BreakPointDbContext context)
    {
        if (string.IsNullOrWhiteSpace(player.Email))
        {
            return Results.BadRequest("Email is required");
        }

        player.CreatedAt = DateTime.UtcNow;
        player.UpdatedAt = DateTime.UtcNow;

        context.Players.Add(player);
        await context.SaveChangesAsync();

        return Results.Created($"/api/players/{player.Id}", player);
    }

    private static async Task<IResult> UpdatePlayer(int id, Player updatedPlayer, BreakPointDbContext context)
    {
        var player = await context.Players.FindAsync(id);
        if (player is null)
        {
            return Results.NotFound();
        }

        if (string.IsNullOrWhiteSpace(updatedPlayer.Email))
        {
            return Results.BadRequest("Email is required");
        }

        player.Email = updatedPlayer.Email;
        player.UpdatedAt = DateTime.UtcNow;

        await context.SaveChangesAsync();

        return Results.Ok(player);
    }

    private static async Task<IResult> DeletePlayer(int id, BreakPointDbContext context)
    {
        var player = await context.Players.FindAsync(id);
        if (player is null)
        {
            return Results.NotFound();
        }

        context.Players.Remove(player);
        await context.SaveChangesAsync();

        return Results.NoContent();
    }
}