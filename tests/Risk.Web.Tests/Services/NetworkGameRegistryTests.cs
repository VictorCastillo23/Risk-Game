using Risk.Domain.Dice;
using Risk.Engine;
using Risk.Web.Models;
using Risk.Web.Services;
using Risk.Web.Tests.Fakes;

namespace Risk.Web.Tests.Services;

public class NetworkGameRegistryTests
{
    private static NetworkGameRegistry NewRegistry(IGameEngine engine, IDiceRoller dice) =>
        new(engine, dice);

    [Fact]
    public void Create_ReturnsSessionWithUniqueRegisteredCode()
    {
        var registry = NewRegistry(new FakeGameEngine(), new QueuedDiceRoller());

        var first = registry.Create();
        var second = registry.Create();

        Assert.NotEqual(first.Code, second.Code);
        Assert.True(registry.TryGet(first.Code, out var foundFirst));
        Assert.Same(first, foundFirst);
        Assert.True(registry.TryGet(second.Code, out var foundSecond));
        Assert.Same(second, foundSecond);
    }

    [Fact]
    public void TryParse_UnknownCode_ReturnsFalse()
    {
        var registry = NewRegistry(new FakeGameEngine(), new QueuedDiceRoller());

        Assert.False(registry.TryGet(new JoinCode("ZZZZZZ"), out _));
    }
}
