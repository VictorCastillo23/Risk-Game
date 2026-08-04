using Risk.Domain.Map;

namespace Risk.Engine.State;

/// <summary>
/// Records a just-conquered territory awaiting the attacker's choice of how
/// many troops to move in. Set by a conquering <c>AttackCommand</c>, cleared
/// by the matching <c>OccupyCommand</c>. While set, the engine rejects every
/// command from the active player except <c>OccupyCommand</c>.
/// </summary>
/// <param name="From">The attacking territory troops will move out of.</param>
/// <param name="Conquered">The territory that was just conquered.</param>
/// <param name="MinimumTroops">
/// The minimum troops that must move in: the number of attacker dice used in
/// the winning battle round.
/// </param>
public sealed record PendingOccupation(TerritoryId From, TerritoryId Conquered, int MinimumTroops);
