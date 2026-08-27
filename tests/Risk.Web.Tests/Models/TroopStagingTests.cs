using Risk.Domain.Map;
using Risk.Web.Models;

namespace Risk.Web.Tests.Models;

public class TroopStagingTests
{
    private static readonly TerritoryId Alaska = new("alaska");
    private static readonly TerritoryId Iceland = new("iceland");

    [Fact]
    public void Add_ToEmptyTerritory_StagesOneTroop()
    {
        var staging = TroopStaging.Empty.Add(Alaska, remainingPool: 5);

        Assert.Equal(1, staging.For(Alaska));
        Assert.Equal(1, staging.TotalStaged);
    }

    [Fact]
    public void Add_PastRemainingPoolCap_IsNoOp()
    {
        var staging = TroopStaging.Empty.Add(Alaska, remainingPool: 1).Add(Alaska, remainingPool: 1);

        Assert.Equal(1, staging.For(Alaska));
        Assert.Equal(1, staging.TotalStaged);
    }

    [Fact]
    public void Add_ToSecondTerritory_AccumulatesIndependently()
    {
        var staging = TroopStaging.Empty
            .Add(Alaska, remainingPool: 5)
            .Add(Alaska, remainingPool: 5)
            .Add(Iceland, remainingPool: 5);

        Assert.Equal(2, staging.For(Alaska));
        Assert.Equal(1, staging.For(Iceland));
        Assert.Equal(3, staging.TotalStaged);
    }

    [Fact]
    public void Remove_DecrementsExistingEntry()
    {
        var staging = TroopStaging.Empty.Add(Alaska, remainingPool: 5).Add(Alaska, remainingPool: 5).Remove(Alaska);

        Assert.Equal(1, staging.For(Alaska));
    }

    [Fact]
    public void Remove_ToZero_RemovesEntryEntirely()
    {
        var staging = TroopStaging.Empty.Add(Alaska, remainingPool: 5).Remove(Alaska);

        Assert.Equal(0, staging.For(Alaska));
        Assert.Equal(0, staging.TotalStaged);
    }

    [Fact]
    public void Remove_TerritoryWithNoPendingEntry_IsNoOp()
    {
        var before = TroopStaging.Empty.Add(Iceland, remainingPool: 5);
        var after = before.Remove(Alaska);

        Assert.Equal(before, after);
    }

    [Fact]
    public void TotalStaged_SumsAcrossMultipleTerritories()
    {
        var staging = TroopStaging.Empty
            .Add(Alaska, remainingPool: 10)
            .Add(Alaska, remainingPool: 10)
            .Add(Alaska, remainingPool: 10)
            .Add(Iceland, remainingPool: 10);

        Assert.Equal(4, staging.TotalStaged);
    }

    [Fact]
    public void Clear_ReturnsToEmpty()
    {
        var staging = TroopStaging.Empty.Add(Alaska, remainingPool: 5).Clear();

        Assert.Equal(TroopStaging.Empty, staging);
        Assert.Equal(0, staging.TotalStaged);
    }
}
