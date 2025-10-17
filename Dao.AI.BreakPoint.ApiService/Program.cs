using Dao.AI.BreakPoint.Data;
using Dao.AI.BreakPoint.Data.Models;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add service defaults & Aspire client integrations.
builder.AddServiceDefaults();

// Add services to the container.
builder.Services.AddProblemDetails();

// Add CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAngularApp", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// Add DbContext with MySQL
builder.AddMySqlDbContext<BreakPointDbContext>("BreakPointDb");

var app = builder.Build();

// Configure the HTTP request pipeline.
app.UseExceptionHandler();

// Use CORS
app.UseCors("AllowAngularApp");

// Ensure database is created
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<BreakPointDbContext>();
    context.Database.EnsureCreated();
}

string[] summaries = ["Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"];

app.MapGet("/weatherforecast", () =>
{
    var forecast = Enumerable.Range(1, 5).Select(index =>
        new WeatherForecast
        (
            DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
            Random.Shared.Next(-20, 55),
            summaries[Random.Shared.Next(summaries.Length)]
        ))
        .ToArray();
    return forecast;
})
.WithName("GetWeatherForecast");

// Add endpoints for testing database connectivity
app.MapGet("/players", async (BreakPointDbContext context) =>
{
    return await context.Players.ToListAsync();
})
.WithName("GetPlayers");

app.MapPost("/players", async (Player player, BreakPointDbContext context) =>
{
    context.Players.Add(player);
    await context.SaveChangesAsync();
    return Results.Created($"/players/{player.Id}", player);
})
.WithName("CreatePlayer");

// Add endpoints for matches
app.MapGet("/matches", async (BreakPointDbContext context) =>
{
    return await context.Matches.ToListAsync();
})
.WithName("GetMatches");

app.MapPost("/matches", async (Match match, BreakPointDbContext context) =>
{
    context.Matches.Add(match);
    await context.SaveChangesAsync();
    return Results.Created($"/matches/{match.Id}", match);
})
.WithName("CreateMatch");

app.MapDefaultEndpoints();

app.Run();

record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}
