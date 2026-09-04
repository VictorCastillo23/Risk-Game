using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Risk.Web.Data;

/// <summary>
/// Design-time factory so <c>dotnet ef migrations add</c>/<c>database
/// update</c> never has to boot the full web host (and its DI container,
/// engine singletons, etc.) just to resolve a connection string. Reads
/// <c>ConnectionStrings__Default</c> directly from the environment.
///
/// Double underscore (<c>__</c>) is ASP.NET Core's standard environment-
/// variable-to-configuration-key convention: it binds to the exact same
/// <c>ConnectionStrings:Default</c> key that <c>Program.cs</c> reads at
/// runtime via <c>builder.Configuration.GetConnectionString("Default")</c>.
/// Using this convention (review fix, replacing the previous ad hoc
/// <c>RISK_DB_CONNECTION</c> name) means design-time and runtime genuinely
/// share one canonical connection-string name instead of two independently
/// maintained ones.
/// </summary>
public sealed class RiskDbContextFactory : IDesignTimeDbContextFactory<RiskDbContext>
{
    private const string ConnectionStringEnvVar = "ConnectionStrings__Default";

    public RiskDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable(ConnectionStringEnvVar);
        if (string.IsNullOrEmpty(connectionString))
        {
            throw new InvalidOperationException(
                $"{ConnectionStringEnvVar} environment variable is not set. " +
                "Design-time tools (dotnet ef migrations add/database update) need it " +
                "to resolve the provider's connection string.");
        }

        var optionsBuilder = new DbContextOptionsBuilder<RiskDbContext>();
        optionsBuilder.UseSqlServer(connectionString);

        return new RiskDbContext(optionsBuilder.Options);
    }
}
