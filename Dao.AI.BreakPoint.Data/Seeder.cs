using Dao.AI.BreakPoint.Data.Enums;
using Dao.AI.BreakPoint.Data.Models;
using Microsoft.EntityFrameworkCore;

namespace Dao.AI.BreakPoint.Data;

public static class Seeder
{
    public static async Task SeedFakeData(BreakPointDbContext breakPointDb)
    {
        // Check if data already exists
        if (await breakPointDb.Players.AnyAsync())
            return;

        var now = DateTime.UtcNow;

        // Create App Users
        var appUsers = await CreateAppUsers(breakPointDb, now);

        // Create Players
        var players = await CreatePlayers(breakPointDb, appUsers, now);

        // Create Matches
        await CreateMatches(breakPointDb, players, now);

        // Create Swing Analyses
        await CreateSwingAnalyses(breakPointDb, players, now);

        Console.WriteLine("Database seeded successfully!");
    }

    private static async Task<List<AppUser>> CreateAppUsers(
        BreakPointDbContext context,
        DateTime now
    )
    {
        var appUsers = new List<AppUser>
        {
            new() {
                Email = "player1@tennis.com",
                CreatedAt = now,
                Player = new()
                {
                    UstaRating = 3.5,
                    Name = "Player One"
                }
            },
            new() {
                Email = "player2@tennis.com",
                CreatedAt = now,
                Player = new()
                {
                    UstaRating = 4.0,
                    Name = "Player Two"
                }
            },
            new() {
                Email = "player3@tennis.com",
                CreatedAt = now,
                Player = new()
                {
                    UstaRating = 4.5,
                    Name = "Player Three"
                }
            },
            new() {
                Email = "player4@tennis.com",
                CreatedAt = now,
                Player = new()
                {
                    UstaRating = 5.0,
                    Name = "Player Four"
                }
            },
            new() {
                Email = "player5@tennis.com",
                CreatedAt = now,
                Player = new()
                {
                    UstaRating = 5.5,
                    Name = "Player Five"
                }
            },
        };

        await context.AddRangeAsync(appUsers);
        await context.SaveChangesAsync();
        return appUsers;
    }

    private static async Task<List<Player>> CreatePlayers(
        BreakPointDbContext context,
        List<AppUser> appUsers,
        DateTime now
    )
    {
        var players = new List<Player>
        {
            new()
            {
                AppUserId = appUsers[0].Id,
                Name = "Alex Champion",
                UstaRating = 6.5,
                EstimatedPlayerType = PlayerType.AllCourtPlayer,
                BigServerScore = 0.7,
                ServeAndVolleyerScore = 0.8,
                AllCourtPlayerScore = 0.9,
                AttackingBaselinerScore = 0.8,
                SolidBaselinerScore = 0.85,
                CounterPuncherScore = 0.6,
                CreatedAt = now,
                UpdatedAt = now,
            },
            new()
            {
                AppUserId = appUsers[1].Id,
                Name = "Maria Defense",
                UstaRating = 6.0,
                EstimatedPlayerType = PlayerType.CounterPuncher,
                BigServerScore = 0.5,
                ServeAndVolleyerScore = 0.3,
                AllCourtPlayerScore = 0.7,
                AttackingBaselinerScore = 0.7,
                SolidBaselinerScore = 0.9,
                CounterPuncherScore = 0.95,
                CreatedAt = now,
                UpdatedAt = now,
            },
            new()
            {
                AppUserId = appUsers[2].Id,
                Name = "John Baseline",
                UstaRating = 5.5,
                EstimatedPlayerType = PlayerType.SolidBaseliner,
                BigServerScore = 0.6,
                ServeAndVolleyerScore = 0.4,
                AllCourtPlayerScore = 0.7,
                AttackingBaselinerScore = 0.8,
                SolidBaselinerScore = 0.9,
                CounterPuncherScore = 0.8,
                CreatedAt = now,
                UpdatedAt = now,
            },
            new()
            {
                AppUserId = appUsers[3].Id,
                Name = "Sarah Power",
                UstaRating = 5.0,
                EstimatedPlayerType = PlayerType.AttackingBaseliner,
                BigServerScore = 0.8,
                ServeAndVolleyerScore = 0.5,
                AllCourtPlayerScore = 0.7,
                AttackingBaselinerScore = 0.9,
                SolidBaselinerScore = 0.7,
                CounterPuncherScore = 0.5,
                CreatedAt = now,
                UpdatedAt = now,
            },
            new()
            {
                AppUserId = appUsers[4].Id,
                Name = "Mike Serve",
                UstaRating = 4.5,
                EstimatedPlayerType = PlayerType.BigServer,
                BigServerScore = 0.95,
                ServeAndVolleyerScore = 0.8,
                AllCourtPlayerScore = 0.6,
                AttackingBaselinerScore = 0.6,
                SolidBaselinerScore = 0.5,
                CounterPuncherScore = 0.3,
                CreatedAt = now,
                UpdatedAt = now,
            },
            // Additional club players without app users
            new()
            {
                Name = "Local Player One",
                UstaRating = 4.0,
                EstimatedPlayerType = PlayerType.SolidBaseliner,
                BigServerScore = 0.4,
                ServeAndVolleyerScore = 0.3,
                AllCourtPlayerScore = 0.5,
                AttackingBaselinerScore = 0.6,
                SolidBaselinerScore = 0.8,
                CounterPuncherScore = 0.7,
                CreatedAt = now,
                UpdatedAt = now,
            },
            new()
            {
                Name = "Local Player Two",
                UstaRating = 3.5,
                EstimatedPlayerType = PlayerType.AttackingBaseliner,
                BigServerScore = 0.5,
                ServeAndVolleyerScore = 0.2,
                AllCourtPlayerScore = 0.4,
                AttackingBaselinerScore = 0.8,
                SolidBaselinerScore = 0.6,
                CounterPuncherScore = 0.4,
                CreatedAt = now,
                UpdatedAt = now,
            },
        };

        await context.AddRangeAsync(players);
        await context.SaveChangesAsync();
        return players;
    }

    private static async Task CreateMatches(
        BreakPointDbContext context,
        List<Player> players,
        DateTime now
    )
    {
        var matches = new List<Match>
        {
            new()
            {
                Player1Id = players[0].Id, // Alex Champion
                Player2Id = players[1].Id, // Maria Defense
                MatchDate = now.AddDays(-30),
                Location = "Central Tennis Club",
                Result = "6-4, 3-6, 7-5",
                Player1Won = true,
                Notes = "Great three-set battle with excellent rallies",
                CreatedAt = now,
                UpdatedAt = now,
            },
            new()
            {
                Player1Id = players[1].Id, // Maria Defense
                Player2Id = players[2].Id, // John Baseline
                MatchDate = now.AddDays(-25),
                Location = "City Tennis Center",
                Result = "6-2, 6-4",
                Player1Won = true,
                Notes = "Maria's defensive skills frustrated John's baseline game",
                CreatedAt = now,
                UpdatedAt = now,
            },
            new()
            {
                Player1Id = players[2].Id, // John Baseline
                Player2Id = players[3].Id, // Sarah Power
                MatchDate = now.AddDays(-20),
                Location = "Riverside Courts",
                Result = "4-6, 6-3, 6-4",
                Player1Won = true,
                Notes = "John's consistency overcame Sarah's power in the end",
                CreatedAt = now,
                UpdatedAt = now,
            },
            new()
            {
                Player1Id = players[3].Id, // Sarah Power
                Player2Id = players[4].Id, // Mike Serve
                MatchDate = now.AddDays(-15),
                Location = "Downtown Tennis Courts",
                Result = "7-6, 6-3",
                Player1Won = true,
                Notes = "Sarah's attacking style neutralized Mike's serve advantage",
                CreatedAt = now,
                UpdatedAt = now,
            },
            new()
            {
                Player1Id = players[0].Id, // Alex Champion
                Player2Id = players[4].Id, // Mike Serve
                MatchDate = now.AddDays(-10),
                Location = "Club Championship Finals",
                Result = "6-3, 7-5",
                Player1Won = true,
                Notes = "Championship match - Alex's all-court game dominated",
                CreatedAt = now,
                UpdatedAt = now,
            },
            new()
            {
                Player1Id = players[5].Id, // Local Player One
                Player2Id = players[6].Id, // Local Player Two
                MatchDate = now.AddDays(-8),
                Location = "Community Center Courts",
                Result = "6-2, 6-4",
                Player1Won = true,
                Notes = "Local club match with good baseline exchanges",
                CreatedAt = now,
                UpdatedAt = now,
            },
            new()
            {
                Player1Id = players[1].Id, // Maria Defense
                Player2Id = players[5].Id, // Local Player One
                MatchDate = now.AddDays(-5),
                Location = "City Tournament",
                Result = "6-1, 6-3",
                Player1Won = true,
                Notes = "Experience showed as Maria dominated the match",
                CreatedAt = now,
                UpdatedAt = now,
            },
            new()
            {
                Player1Id = players[2].Id, // John Baseline
                Player2Id = players[6].Id, // Local Player Two
                MatchDate = now.AddDays(-3),
                Location = "Weekly Club Match",
                Result = "6-4, 6-2",
                Player1Won = true,
                Notes = "John's consistency proved too much for the local player",
                CreatedAt = now,
                UpdatedAt = now,
            },
        };

        await context.AddRangeAsync(matches);
        await context.SaveChangesAsync();
    }

    private static async Task CreateSwingAnalyses(
        BreakPointDbContext context,
        List<Player> players,
        DateTime now
    )
    {
        var swingAnalyses = new List<SwingAnalysis>
        {
            // Alex Champion analyses
            new()
            {
                PlayerId = players[0].Id,
                Rating = 6.2,
                Summary =
                    "Excellent all-court movement with strong forehand and backhand. Net game is particularly impressive.",
                Recommendations =
                    "Continue working on serve placement variety. Consider adding more aggressive return positions.",
                CreatedAt = now.AddDays(-20),
            },
            new()
            {
                PlayerId = players[0].Id,
                Rating = 6.4,
                Summary =
                    "Improved consistency in longer rallies. Tactical awareness during points has enhanced significantly.",
                Recommendations =
                    "Focus on maintaining intensity in final sets. Work on drop shot execution.",
                CreatedAt = now.AddDays(-10),
            },
            // Maria Defense analyses
            new()
            {
                PlayerId = players[1].Id,
                Rating = 5.8,
                Summary =
                    "Outstanding defensive positioning and court coverage. Ability to turn defense into attack is excellent.",
                Recommendations =
                    "Develop more offensive weapons for shorter points. Improve first serve percentage.",
                CreatedAt = now.AddDays(-18),
            },
            new()
            {
                PlayerId = players[1].Id,
                Rating = 6.0,
                Summary =
                    "Mental toughness in tight situations is exceptional. Fitness level allows for extended matches.",
                Recommendations =
                    "Add more variety to shot selection. Consider net play on shorter balls.",
                CreatedAt = now.AddDays(-8),
            },
            // John Baseline analyses
            new()
            {
                PlayerId = players[2].Id,
                Rating = 5.3,
                Summary =
                    "Solid baseline strokes with good consistency. Footwork has improved over recent months.",
                Recommendations =
                    "Work on approach shots to set up net opportunities. Improve serve and volley technique.",
                CreatedAt = now.AddDays(-15),
            },
            // Sarah Power analyses
            new()
            {
                PlayerId = players[3].Id,
                Rating = 4.8,
                Summary =
                    "Powerful groundstrokes that can dictate play. Aggressive mindset creates many winners.",
                Recommendations =
                    "Focus on shot selection and patience. Reduce unforced errors in crucial moments.",
                CreatedAt = now.AddDays(-12),
            },
            // Mike Serve analyses
            new()
            {
                PlayerId = players[4].Id,
                Rating = 4.3,
                Summary =
                    "Big serve is a major weapon with good placement. Volley technique shows promise.",
                Recommendations =
                    "Improve movement between serve and volley. Work on baseline consistency.",
                CreatedAt = now.AddDays(-7),
            },
            // Local Player analyses
            new()
            {
                PlayerId = players[5].Id,
                Rating = 3.8,
                Summary =
                    "Steady baseline game with good court sense. Shows improvement in match tactics.",
                Recommendations = "Continue developing shot power. Work on closing out tight sets.",
                CreatedAt = now.AddDays(-5),
            },
            new()
            {
                PlayerId = players[6].Id,
                Rating = 3.5,
                Summary =
                    "Improving stroke technique and court positioning. Good fighting spirit in matches.",
                Recommendations =
                    "Focus on consistency over power. Develop a more reliable second serve.",
                CreatedAt = now.AddDays(-3),
            },
        };

        await context.AddRangeAsync(swingAnalyses);
        await context.SaveChangesAsync();
    }
}
