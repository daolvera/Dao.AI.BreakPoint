using Dao.AI.BreakPoint.Data.Models;
using Microsoft.EntityFrameworkCore;

namespace Dao.AI.BreakPoint.Data;

public class BreakPointDbContext : DbContext
{
    public BreakPointDbContext(DbContextOptions<BreakPointDbContext> options) : base(options)
    {
    }

    public DbSet<Player> Players { get; set; }
    public DbSet<SwingAnalysis> SwingAnalyses { get; set; }
    public DbSet<Match> Matches { get; set; }
}
