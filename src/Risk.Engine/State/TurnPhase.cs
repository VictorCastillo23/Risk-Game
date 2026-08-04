namespace Risk.Engine.State;

/// <summary>
/// The phases a turn cycles through, in order. Setup happens once, before
/// any player's first real turn, and is followed by Reinforce for player 0.
/// </summary>
public enum TurnPhase
{
    Setup,
    Reinforce,
    Attack,
    Fortify
}
