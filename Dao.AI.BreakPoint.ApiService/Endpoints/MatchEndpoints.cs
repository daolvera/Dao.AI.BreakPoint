using Dao.AI.BreakPoint.Data;
using Dao.AI.BreakPoint.Data.Models;
using Microsoft.EntityFrameworkCore;

namespace Dao.AI.BreakPoint.ApiService.Endpoints;

public static class MatchEndpoints
{
    public static IEndpointRouteBuilder MapMatchEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/matches")
            .WithTags("Matches");

        group.MapGet("/", GetAllMatches)
            .WithName("GetAllMatches")
            .WithSummary("Get all matches")
            .Produces<List<Match>>();

        group.MapGet("/{id:int}", GetMatchById)
            .WithName("GetMatchById")
            .WithSummary("Get match by ID")
            .Produces<Match>()
            .Produces(404);

        group.MapPost("/", CreateMatch)
            .WithName("CreateMatch")
            .WithSummary("Create a new match")
            .Produces<Match>(201)
            .Produces(400);

        group.MapPut("/{id:int}", UpdateMatch)
            .WithName("UpdateMatch")
            .WithSummary("Update an existing match")
            .Produces<Match>()
            .Produces(404)
            .Produces(400);

        group.MapDelete("/{id:int}", DeleteMatch)
            .WithName("DeleteMatch")
            .WithSummary("Delete a match")
            .Produces(204)
            .Produces(404);

        group.MapGet("/player/{playerId:int}", GetMatchesByPlayer)
            .WithName("GetMatchesByPlayer")
            .WithSummary("Get matches for a specific player")
            .Produces<List<Match>>();

        return endpoints;
    }

    private static async Task<IResult> GetAllMatches(BreakPointDbContext context)
    {
        var matches = await context.Matches.ToListAsync();
        return Results.Ok(matches);
    }

    private static async Task<IResult> GetMatchById(int id, BreakPointDbContext context)
    {
        var match = await context.Matches.FindAsync(id);
        return match is not null ? Results.Ok(match) : Results.NotFound();
    }

    private static async Task<IResult> CreateMatch(Match match, BreakPointDbContext context)
    {
        if (string.IsNullOrWhiteSpace(match.Location) || string.IsNullOrWhiteSpace(match.Result))
        {
            return Results.BadRequest("Location and Result are required");
        }

        // Validate that Player1 exists
        var player1Exists = await context.Players.AnyAsync(p => p.Id == match.Player1Id);
        if (!player1Exists)
        {
            return Results.BadRequest("Player1Id does not exist");
        }

        match.CreatedAt = DateTime.UtcNow;
        match.UpdatedAt = DateTime.UtcNow;

        context.Matches.Add(match);
        await context.SaveChangesAsync();

        return Results.Created($"/api/matches/{match.Id}", match);
    }

    private static async Task<IResult> UpdateMatch(int id, Match updatedMatch, BreakPointDbContext context)
    {
        var match = await context.Matches.FindAsync(id);
        if (match is null)
        {
            return Results.NotFound();
        }

        if (string.IsNullOrWhiteSpace(updatedMatch.Location) || string.IsNullOrWhiteSpace(updatedMatch.Result))
        {
            return Results.BadRequest("Location and Result are required");
        }

        match.Player1Id = updatedMatch.Player1Id;
        match.Player2Id = updatedMatch.Player2Id;
        match.MatchDate = updatedMatch.MatchDate;
        match.Location = updatedMatch.Location;
        match.Result = updatedMatch.Result;
        match.Notes = updatedMatch.Notes;
        match.UpdatedAt = DateTime.UtcNow;

        await context.SaveChangesAsync();

        return Results.Ok(match);
    }

    private static async Task<IResult> DeleteMatch(int id, BreakPointDbContext context)
    {
        var match = await context.Matches.FindAsync(id);
        if (match is null)
        {
            return Results.NotFound();
        }

        context.Matches.Remove(match);
        await context.SaveChangesAsync();

        return Results.NoContent();
    }

    private static async Task<IResult> GetMatchesByPlayer(int playerId, BreakPointDbContext context)
    {
        var matches = await context.Matches
            .Where(m => m.Player1Id == playerId || m.Player2Id == playerId)
            .ToListAsync();

        return Results.Ok(matches);
    }
}