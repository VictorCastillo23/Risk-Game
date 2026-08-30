using Risk.Domain.Players;

namespace Risk.Engine.State;

/// <summary>Who owns a territory and how many troops occupy it. <c>Owner == null</c> means unclaimed.</summary>
public sealed record TerritoryState(PlayerId? Owner, int Troops);
