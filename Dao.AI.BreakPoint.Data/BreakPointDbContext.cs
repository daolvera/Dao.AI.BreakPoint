using Dao.AI.BreakPoint.Data.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Dao.AI.BreakPoint.Data;

public class BreakPointDbContext : IdentityDbContext<AppUser>
{
    public DbSet<Player> Players { get; set; }
    public DbSet<SwingAnalysis> SwingAnalyses { get; set; }
    public DbSet<Match> Matches { get; set; }
    public DbSet<AnalysisRequest> AnalysisRequests { get; set; }
    public DbSet<AnalysisResult> AnalysisResults { get; set; }
    public DbSet<DrillRecommendation> DrillRecommendations { get; set; }
    public DbSet<PhaseDeviation> PhaseDeviations { get; set; }
    public DbSet<FeatureDeviation> FeatureDeviations { get; set; }

    public BreakPointDbContext(DbContextOptions<BreakPointDbContext> options)
        : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder
            .Entity<Player>()
            .HasMany<Match>(p => p.MyReportedMatches)
            .WithOne(nameof(Match.Player1));

        modelBuilder
            .Entity<Player>()
            .HasMany<Match>(p => p.MyParticipatedMatches)
            .WithOne(nameof(Match.Player2));

        modelBuilder
            .Entity<Player>()
            .HasOne(p => p.AppUser)
            .WithOne(u => u.Player)
            .OnDelete(DeleteBehavior.SetNull);

        // AnalysisRequest -> AnalysisResult (1:1, optional)
        modelBuilder
            .Entity<AnalysisRequest>()
            .HasOne(r => r.Result)
            .WithOne(r => r.AnalysisRequest)
            .HasForeignKey<AnalysisResult>(r => r.AnalysisRequestId)
            .OnDelete(DeleteBehavior.Cascade);

        // Player -> AnalysisResults (1:many)
        modelBuilder
            .Entity<Player>()
            .HasMany(p => p.AnalysisResults)
            .WithOne(r => r.Player)
            .HasForeignKey(r => r.PlayerId)
            .OnDelete(DeleteBehavior.Cascade);

        // Player -> AnalysisRequests (1:many)
        modelBuilder
            .Entity<Player>()
            .HasMany(p => p.AnalysisRequests)
            .WithOne(r => r.Player)
            .HasForeignKey(r => r.PlayerId)
            .OnDelete(DeleteBehavior.Cascade);

        // AnalysisResult -> DrillRecommendations (1:many)
        modelBuilder
            .Entity<AnalysisResult>()
            .HasMany(r => r.DrillRecommendations)
            .WithOne(d => d.AnalysisResult)
            .HasForeignKey(d => d.AnalysisResultId)
            .OnDelete(DeleteBehavior.Cascade);

        // Player -> DrillRecommendations (1:many, no cascade since AnalysisResult cascades)
        modelBuilder
            .Entity<Player>()
            .HasMany<DrillRecommendation>()
            .WithOne(d => d.Player)
            .HasForeignKey(d => d.PlayerId)
            .OnDelete(DeleteBehavior.ClientSetNull);

        // AnalysisResult -> PhaseDeviations (1:many)
        modelBuilder
            .Entity<AnalysisResult>()
            .HasMany(r => r.PhaseDeviations)
            .WithOne(p => p.AnalysisResult)
            .HasForeignKey(p => p.AnalysisResultId)
            .OnDelete(DeleteBehavior.Cascade);

        // PhaseDeviation -> FeatureDeviations (1:many)
        modelBuilder
            .Entity<PhaseDeviation>()
            .HasMany(p => p.FeatureDeviations)
            .WithOne(f => f.PhaseDeviation)
            .HasForeignKey(f => f.PhaseDeviationId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
