using Microsoft.EntityFrameworkCore;
using Risk.Web.Data;

namespace Risk.Web.Tests.Data;

/// <summary>
/// Covers <see cref="RiskDbContext"/> — previously untested (review fix).
/// Only validates the EF Core model composition (provider + mappings), never
/// opens a real connection, so this needs no live database:
/// <see cref="RelationalDatabaseFacadeExtensions.GenerateCreateScript"/>
/// only needs the model to be valid against the configured provider.
/// </summary>
public sealed class RiskDbContextTests
{
    private static RiskDbContext CreateContext()
    {
        var optionsBuilder = new DbContextOptionsBuilder<RiskDbContext>();
        optionsBuilder.UseSqlServer(
            "Server=tcp:placeholder.database.windows.net,1433;Database=placeholder;User Id=placeholder;Password=placeholder;Encrypt=True;");
        return new RiskDbContext(optionsBuilder.Options);
    }

    [Fact]
    public void Model_IsValid_CanBeMaterialized()
    {
        using var context = CreateContext();

        var model = context.Model;

        Assert.NotNull(model);
        Assert.NotEmpty(model.GetEntityTypes());
    }

    [Fact]
    public void Database_GenerateCreateScript_SucceedsWithoutOpeningConnection()
    {
        using var context = CreateContext();

        var script = context.Database.GenerateCreateScript();

        Assert.False(string.IsNullOrWhiteSpace(script));
        Assert.Contains("AspNetUsers", script);
    }
}
