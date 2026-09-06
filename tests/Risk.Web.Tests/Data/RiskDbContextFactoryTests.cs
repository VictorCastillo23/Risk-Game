using Risk.Web.Data;

namespace Risk.Web.Tests.Data;

/// <summary>
/// Covers <see cref="RiskDbContextFactory"/> — previously untested (review
/// fix). Only exercises the "no live database needed" surface: env var
/// presence/absence. Mutates a process-wide environment variable, so each
/// test saves and restores the original value in a try/finally to avoid
/// bleeding into other tests.
/// </summary>
public sealed class RiskDbContextFactoryTests
{
    private const string ConnectionStringEnvVar = "ConnectionStrings__Default";

    [Fact]
    public void CreateDbContext_WhenConnectionStringEnvVarUnset_ThrowsInvalidOperationException()
    {
        var original = Environment.GetEnvironmentVariable(ConnectionStringEnvVar);
        try
        {
            Environment.SetEnvironmentVariable(ConnectionStringEnvVar, null);
            var factory = new RiskDbContextFactory();

            var exception = Assert.Throws<InvalidOperationException>(() => factory.CreateDbContext([]));

            Assert.Contains(ConnectionStringEnvVar, exception.Message);
        }
        finally
        {
            Environment.SetEnvironmentVariable(ConnectionStringEnvVar, original);
        }
    }

    [Fact]
    public void CreateDbContext_WhenConnectionStringEnvVarEmpty_ThrowsInvalidOperationException()
    {
        var original = Environment.GetEnvironmentVariable(ConnectionStringEnvVar);
        try
        {
            Environment.SetEnvironmentVariable(ConnectionStringEnvVar, string.Empty);
            var factory = new RiskDbContextFactory();

            Assert.Throws<InvalidOperationException>(() => factory.CreateDbContext([]));
        }
        finally
        {
            Environment.SetEnvironmentVariable(ConnectionStringEnvVar, original);
        }
    }

    [Fact]
    public void CreateDbContext_WhenConnectionStringEnvVarSet_ReturnsUsableContext()
    {
        var original = Environment.GetEnvironmentVariable(ConnectionStringEnvVar);
        try
        {
            Environment.SetEnvironmentVariable(
                ConnectionStringEnvVar,
                "Server=tcp:placeholder.database.windows.net,1433;Database=placeholder;User Id=placeholder;Password=placeholder;Encrypt=True;");
            var factory = new RiskDbContextFactory();

            using var context = factory.CreateDbContext([]);

            Assert.NotNull(context);
        }
        finally
        {
            Environment.SetEnvironmentVariable(ConnectionStringEnvVar, original);
        }
    }
}
