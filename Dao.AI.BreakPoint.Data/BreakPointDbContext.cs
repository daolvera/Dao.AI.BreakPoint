using Dao.AI.BreakPoint.Data.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Dao.AI.BreakPoint.Data;

public class BreakPointDbContext : IdentityDbContext<AppUser, IdentityRole<int>, int>
{
    public DbSet<Player> Players { get; set; }
    public DbSet<SwingAnalysis> SwingAnalyses { get; set; }
    public DbSet<Match> Matches { get; set; }
    public DbSet<AnalysisEvent> AnalysisEvents { get; set; }

    public BreakPointDbContext(DbContextOptions<BreakPointDbContext> options)
        : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configure Player relationships
        modelBuilder
            .Entity<Player>()
            .HasMany<Match>(p => p.MyReportedMatches)
            .WithOne(nameof(Match.Player1));

        modelBuilder
            .Entity<Player>()
            .HasMany<Match>(p => p.MyParticipatedMatches)
            .WithOne(nameof(Match.Player2));

        // Configure AppUser -> Player relationship
        modelBuilder
            .Entity<Player>()
            .HasOne(p => p.AppUser)
            .WithOne(u => u.Player)
            .OnDelete(DeleteBehavior.SetNull);

        // Configure OAuthProvider enum as string in database
        modelBuilder.Entity<AppUser>().Property(e => e.ExternalProvider).HasConversion<string>();
    }
}
