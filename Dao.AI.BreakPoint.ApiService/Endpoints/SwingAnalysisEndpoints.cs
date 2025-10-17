using Dao.AI.BreakPoint.Data;
using Dao.AI.BreakPoint.Data.Models;
using Microsoft.EntityFrameworkCore;

namespace Dao.AI.BreakPoint.ApiService.Endpoints;

public static class SwingAnalysisEndpoints
{
    public static IEndpointRouteBuilder MapSwingAnalysisEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/swing-analyses")
            .WithTags("Swing Analyses");

        group.MapGet("/", GetAllSwingAnalyses)
            .WithName("GetAllSwingAnalyses")
            .WithSummary("Get all swing analyses")
            .Produces<List<SwingAnalysis>>();

        group.MapGet("/{id:int}", GetSwingAnalysisById)
            .WithName("GetSwingAnalysisById")
            .WithSummary("Get swing analysis by ID")
            .Produces<SwingAnalysis>()
            .Produces(404);

        group.MapPost("/", CreateSwingAnalysis)
            .WithName("CreateSwingAnalysis")
            .WithSummary("Create a new swing analysis")
            .Produces<SwingAnalysis>(201)
            .Produces(400);

        group.MapPut("/{id:int}", UpdateSwingAnalysis)
            .WithName("UpdateSwingAnalysis")
            .WithSummary("Update an existing swing analysis")
            .Produces<SwingAnalysis>()
            .Produces(404)
            .Produces(400);

        group.MapDelete("/{id:int}", DeleteSwingAnalysis)
            .WithName("DeleteSwingAnalysis")
            .WithSummary("Delete a swing analysis")
            .Produces(204)
            .Produces(404);

        group.MapGet("/player/{playerId:int}", GetSwingAnalysesByPlayer)
            .WithName("GetSwingAnalysesByPlayer")
            .WithSummary("Get swing analyses for a specific player")
            .Produces<List<SwingAnalysis>>();

        return endpoints;
    }

    private static async Task<IResult> GetAllSwingAnalyses(BreakPointDbContext context)
    {
        var analyses = await context.SwingAnalyses
            .Include(sa => sa.Player)
            .ToListAsync();
        return Results.Ok(analyses);
    }

    private static async Task<IResult> GetSwingAnalysisById(int id, BreakPointDbContext context)
    {
        var analysis = await context.SwingAnalyses
            .Include(sa => sa.Player)
            .FirstOrDefaultAsync(sa => sa.Id == id);

        return analysis is not null ? Results.Ok(analysis) : Results.NotFound();
    }

    private static async Task<IResult> CreateSwingAnalysis(SwingAnalysis analysis, BreakPointDbContext context)
    {
        if (string.IsNullOrWhiteSpace(analysis.Summary) || string.IsNullOrWhiteSpace(analysis.Recommendations))
        {
            return Results.BadRequest("Summary and Recommendations are required");
        }

        if (analysis.Rating < 1.0 || analysis.Rating > 7.0)
        {
            return Results.BadRequest("Rating must be between 1.0 and 7.0");
        }

        // Validate that Player exists
        var playerExists = await context.Players.AnyAsync(p => p.Id == analysis.PlayerId);
        if (!playerExists)
        {
            return Results.BadRequest("PlayerId does not exist");
        }

        analysis.CreatedAt = DateTime.UtcNow;

        context.SwingAnalyses.Add(analysis);
        await context.SaveChangesAsync();

        // Return the analysis with the player included
        var createdAnalysis = await context.SwingAnalyses
            .Include(sa => sa.Player)
            .FirstOrDefaultAsync(sa => sa.Id == analysis.Id);

        return Results.Created($"/api/swing-analyses/{analysis.Id}", createdAnalysis);
    }

    private static async Task<IResult> UpdateSwingAnalysis(int id, SwingAnalysis updatedAnalysis, BreakPointDbContext context)
    {
        var analysis = await context.SwingAnalyses.FindAsync(id);
        if (analysis is null)
        {
            return Results.NotFound();
        }

        if (string.IsNullOrWhiteSpace(updatedAnalysis.Summary) || string.IsNullOrWhiteSpace(updatedAnalysis.Recommendations))
        {
            return Results.BadRequest("Summary and Recommendations are required");
        }

        if (updatedAnalysis.Rating < 1.0 || updatedAnalysis.Rating > 7.0)
        {
            return Results.BadRequest("Rating must be between 1.0 and 7.0");
        }

        analysis.PlayerId = updatedAnalysis.PlayerId;
        analysis.Rating = updatedAnalysis.Rating;
        analysis.Summary = updatedAnalysis.Summary;
        analysis.Recommendations = updatedAnalysis.Recommendations;

        await context.SaveChangesAsync();

        // Return the updated analysis with the player included
        var result = await context.SwingAnalyses
            .Include(sa => sa.Player)
            .FirstOrDefaultAsync(sa => sa.Id == id);

        return Results.Ok(result);
    }

    private static async Task<IResult> DeleteSwingAnalysis(int id, BreakPointDbContext context)
    {
        var analysis = await context.SwingAnalyses.FindAsync(id);
        if (analysis is null)
        {
            return Results.NotFound();
        }

        context.SwingAnalyses.Remove(analysis);
        await context.SaveChangesAsync();

        return Results.NoContent();
    }

    private static async Task<IResult> GetSwingAnalysesByPlayer(int playerId, BreakPointDbContext context)
    {
        var analyses = await context.SwingAnalyses
            .Include(sa => sa.Player)
            .Where(sa => sa.PlayerId == playerId)
            .ToListAsync();

        return Results.Ok(analyses);
    }
}