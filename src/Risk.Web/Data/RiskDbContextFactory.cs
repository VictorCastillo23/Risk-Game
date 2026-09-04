using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Risk.Web.Data;

/// <summary>
/// Design-time factory so <c>dotnet ef migrations add</c>/<c>database
/// update</c> never has to boot the full web host (and its DI container,
/// engine singletons, etc.) just to resolve a connection string. Reads
/// <c>RISK_DB_CONNECTION</c> directly from the environment — the same
/// variable name the CI <c>migrate</c> job (design D6) sets from the
/// <c>RISK_DB_CONNECTION</c> GitHub secret, so migration generation and
/// migration application always agree on where the connection string comes
/// from.
/// </summary>
public sealed class RiskDbContextFactory : IDesignTimeDbContextFactory<RiskDbContext>
{
    public RiskDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("RISK_DB_CONNECTION")
            ?? throw new InvalidOperationException(
                "RISK_DB_CONNECTION environment variable is not set. " +
                "Design-time tools (dotnet ef migrations add/database update) need it " +
                "to resolve the provider's connection string.");

        var optionsBuilder = new DbContextOptionsBuilder<RiskDbContext>();
        optionsBuilder.UseSqlServer(connectionString);

        return new RiskDbContext(optionsBuilder.Options);
    }
}
