using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace TradeFlowGuardian.Infrastructure.Data;

/// <summary>
/// Allows `dotnet ef migrations add` to run from the Infrastructure project directory.
/// Uses Postgres__ConnectionString env var, falling back to a local dev string.
/// </summary>
public class TradeFlowDbContextFactory : IDesignTimeDbContextFactory<TradeFlowDbContext>
{
    public TradeFlowDbContext CreateDbContext(string[] args)
    {
        var cs = Environment.GetEnvironmentVariable("Postgres__ConnectionString")
                 ?? "Host=localhost;Database=tradeflow;Username=postgres;Password=postgres";
        var opts = new DbContextOptionsBuilder<TradeFlowDbContext>()
            .UseNpgsql(cs)
            .Options;
        return new TradeFlowDbContext(opts);
    }
}
