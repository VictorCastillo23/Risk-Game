using Risk.Domain.Players;

namespace Risk.Engine.State;

/// <summary>Who owns a territory and how many troops occupy it.</summary>
public sealed record TerritoryState(PlayerId Owner, int Troops);
