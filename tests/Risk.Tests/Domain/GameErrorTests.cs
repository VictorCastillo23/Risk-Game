using Risk.Domain.Errors;

namespace Risk.Tests.Domain;

public class GameErrorTests
{
    [Fact]
    public void Constructs_with_code_and_message()
    {
        var error = new GameError(GameErrorCode.NotYourTurn, "It is not your turn.");

        Assert.Equal(GameErrorCode.NotYourTurn, error.Code);
        Assert.Equal("It is not your turn.", error.Message);
    }

    [Fact]
    public void Distinguishes_different_codes_with_value_equality()
    {
        var first = new GameError(GameErrorCode.NotAdjacent, "Territories are not adjacent.");
        var second = new GameError(GameErrorCode.NotAdjacent, "Territories are not adjacent.");
        var different = new GameError(GameErrorCode.WrongPhase, "Wrong phase.");

        Assert.Equal(first, second);
        Assert.NotEqual(first, different);
    }
}
