using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Dao.AI.BreakPoint.Data;

/// <summary>
/// Design-time factory for EF Core migrations tooling.
/// This is only used by `dotnet ef` commands and not at runtime.
/// </summary>
public class BreakPointDbContextFactory : IDesignTimeDbContextFactory<BreakPointDbContext>
{
    public BreakPointDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<BreakPointDbContext>();

        // This connection string is only used for migrations tooling.
        // At runtime, Aspire provides the real connection string.
        optionsBuilder.UseNpgsql(
            "Host=localhost;Database=breakpointdb;Username=postgres;Password=postgres"
        );

        return new BreakPointDbContext(optionsBuilder.Options);
    }
}
