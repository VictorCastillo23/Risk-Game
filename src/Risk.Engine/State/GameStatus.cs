using Risk.Domain.Players;

namespace Risk.Engine.State;

/// <summary>Whether the game is still being played or has been won.</summary>
public abstract record GameStatus
{
    private GameStatus()
    {
    }

    /// <summary>The game is still being played.</summary>
    public sealed record InProgress : GameStatus;

    /// <summary>The game has ended; <see cref="Winner"/> controls all 42 territories.</summary>
    public sealed record Won(PlayerId Winner) : GameStatus;
}
