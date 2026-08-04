using Risk.Domain.Players;

namespace Risk.Engine.Commands;

/// <summary>
/// Something a player can ask the engine to do. Every command carries the
/// acting player so the engine can validate it is their turn.
/// </summary>
public abstract record GameCommand(PlayerId Actor);
