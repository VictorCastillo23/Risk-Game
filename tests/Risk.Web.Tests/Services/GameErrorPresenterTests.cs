using Risk.Domain.Errors;
using Risk.Web.Services;

namespace Risk.Web.Tests.Services;

public class GameErrorPresenterTests
{
    public static IEnumerable<object[]> AllErrorCodes =>
        Enum.GetValues<GameErrorCode>().Select(code => new object[] { code });

    [Theory]
    [MemberData(nameof(AllErrorCodes))]
    public void Describe_EveryGameErrorCode_ReturnsANonEmptySpanishMessageDistinctFromTheRawEngineMessage(
        GameErrorCode code)
    {
        var error = new GameError(code, "raw engine message");

        var described = GameErrorPresenter.Describe(error);

        Assert.False(string.IsNullOrWhiteSpace(described));
        Assert.NotEqual("raw engine message", described);
    }

    [Fact]
    public void Describe_AllSixteenCodes_ProduceDistinctMessages()
    {
        var messages = Enum.GetValues<GameErrorCode>()
            .Select(code => GameErrorPresenter.Describe(new GameError(code, "raw engine message")))
            .ToList();

        Assert.Equal(16, messages.Count);
        Assert.Equal(16, messages.Distinct().Count());
    }

    [Fact]
    public void Describe_UnmappedCode_FallsBackToTheRawEngineMessage()
    {
        var unmapped = (GameErrorCode)(-1);
        var error = new GameError(unmapped, "raw engine message");

        Assert.Equal("raw engine message", GameErrorPresenter.Describe(error));
    }
}
