using Risk.Domain.Errors;
using Risk.Engine.Results;

namespace Risk.Tests.Engine;

public class CommandResultTests
{
    [Fact]
    public void Ok_carries_state_and_events()
    {
        CommandResult<int, string> result = new CommandResult<int, string>.Ok(42, ["troop-placed"]);

        var (state, events) = Match(result);

        Assert.Equal(42, state);
        Assert.Equal(["troop-placed"], events);
    }

    [Fact]
    public void Rejected_carries_the_game_error()
    {
        var error = new GameError(GameErrorCode.NotYourTurn, "It is not your turn.");
        CommandResult<int, string> result = new CommandResult<int, string>.Rejected(error);

        var rejection = MatchRejection(result);

        Assert.Equal(GameErrorCode.NotYourTurn, rejection.Code);
        Assert.Equal("It is not your turn.", rejection.Message);
    }

    private static (int State, IReadOnlyList<string> Events) Match(CommandResult<int, string> result) =>
        result switch
        {
            CommandResult<int, string>.Ok ok => (ok.State, ok.Events),
            CommandResult<int, string>.Rejected => throw new InvalidOperationException("Expected Ok."),
            _ => throw new InvalidOperationException("Unreachable.")
        };

    private static GameError MatchRejection(CommandResult<int, string> result) =>
        result switch
        {
            CommandResult<int, string>.Rejected rejected => rejected.Error,
            CommandResult<int, string>.Ok => throw new InvalidOperationException("Expected Rejected."),
            _ => throw new InvalidOperationException("Unreachable.")
        };
}
