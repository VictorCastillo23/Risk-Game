namespace Risk.Domain.Cards;

/// <summary>
/// A card held by a player: either a <see cref="TerritoryCard"/> or a
/// <see cref="WildCard"/>. Closed hierarchy so consumers can exhaustively
/// pattern-match on the two variants.
/// </summary>
public abstract record Card;
