namespace Risk.Domain.Errors;

/// <summary>
/// Enumerates every rule violation the engine can reject a command with.
/// </summary>
public enum GameErrorCode
{
    NotYourTurn,
    WrongPhase,
    NotOwner,
    NotAdjacent,
    InsufficientTroops,
    InvalidDiceCount,
    InvalidCardSet,
    MandatoryTradeRequired,
    OccupationPending,
    FortifyAlreadyUsed,
    NoFriendlyPath,
    GameOver,
    InvalidPlayerCount,
    InvalidTroopCount,
    NoPendingOccupation,
    ReinforcementIncomplete,
    ActorIsNeutral
}
