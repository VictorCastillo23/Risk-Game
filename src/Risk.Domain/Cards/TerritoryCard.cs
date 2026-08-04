using Risk.Domain.Map;

namespace Risk.Domain.Cards;

/// <summary>
/// A card tied to a specific territory, carrying one of the three symbols
/// used to validate trade-in sets.
/// </summary>
public sealed record TerritoryCard(TerritoryId Territory, CardSymbol Symbol) : Card;
