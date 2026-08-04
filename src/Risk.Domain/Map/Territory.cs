using Risk.Domain.Cards;

namespace Risk.Domain.Map;

/// <summary>
/// Identifies and describes a single territory: which continent it belongs
/// to and which card symbol it carries. The full 42-territory board data
/// (adjacency graph, sea routes) is seeded separately by <c>WorldMap</c>.
/// </summary>
public sealed record Territory(TerritoryId Id, ContinentId ContinentId, CardSymbol Symbol);
