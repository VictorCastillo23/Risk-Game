namespace Risk.Engine.State;

/// <summary>
/// The phases a turn cycles through, in order. Claim (unclaimed-territory
/// pick) precedes Setup's initial troop placement, which happens once before
/// any player's first real turn. <see cref="SelectHeadquarters"/> is a
/// one-round gate entered only for <see cref="GameMode.Capital"/> games,
/// between Setup and Reinforce (every other mode goes straight from Setup to
/// Reinforce for player 0).
/// </summary>
public enum TurnPhase
{
    Claim,
    Setup,
    SelectHeadquarters,
    Reinforce,
    Attack,
    Fortify
}
