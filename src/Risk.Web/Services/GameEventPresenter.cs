using Risk.Domain.Players;
using Risk.Engine.Events;
using Risk.Web.Models;

namespace Risk.Web.Services;

/// <summary>
/// Maps every concrete <see cref="GameEvent"/> subtype to a short,
/// human-readable Spanish sentence (design D8) for <c>LogPanel</c>. Mirrors
/// <see cref="GameErrorPresenter"/>'s exhaustive-mapping pattern.
/// </summary>
/// <remarks>
/// Design D5's binding-redaction convention exists because <c>Observe()</c>
/// is the one seam that hides another player's hand. <see cref="CardDrawn"/>
/// is the one <see cref="GameEvent"/> that would otherwise defeat that seam
/// by name — it carries the exact <c>Card</c> a player drew, which is
/// private information until traded in. This presenter deliberately never
/// reads <see cref="CardDrawn.Card"/>, so the log cannot leak it even though
/// <c>GameState.Log</c> itself is read unredacted (design's "Read directly
/// off Session.State" convention). <see cref="CardsTraded"/> is safe to
/// describe in full because trading in a set is a voluntary, public reveal
/// (mirrors the real board game rule), unlike drawing.
/// </remarks>
public static class GameEventPresenter
{
    public static string Describe(GameEvent gameEvent) => gameEvent switch
    {
        TerritoriesAssigned => "Los territorios fueron repartidos entre los jugadores.",
        TerritoryClaimed e => $"{PlayerLabel(e.Player)} reclamó {e.Territory.Value}.",
        TroopsPlaced e => $"{PlayerLabel(e.Player)} colocó {e.Troops} tropas en {e.Territory.Value}.",
        CardsTraded e => e.BonusTerritory is { } bonusTerritory
            ? $"{PlayerLabel(e.Actor)} canjeó {e.Cards.Count} cartas por {e.Bonus} tropas y +2 tropas extra en {bonusTerritory.Value}."
            : $"{PlayerLabel(e.Actor)} canjeó {e.Cards.Count} cartas por {e.Bonus} tropas.",
        BattleResolved e =>
            $"{PlayerLabel(e.Attacker)} atacó {e.To.Value} desde {e.From.Value}: perdió {e.AttackerLosses} tropas, el defensor perdió {e.DefenderLosses}.",
        TerritoryConquered e => $"{PlayerLabel(e.Conqueror)} conquistó {e.Territory.Value}.",
        TerritoryOccupied e => $"{PlayerLabel(e.Player)} ocupó {e.Territory.Value} con {e.Troops} tropas.",
        TroopsFortified e => $"{PlayerLabel(e.Actor)} movió {e.Troops} tropas de {e.From.Value} a {e.To.Value}.",
        PlayerEliminated e => $"{PlayerLabel(e.Victim)} fue eliminado por {PlayerLabel(e.By)} y le cedió {e.CardsTransferred} cartas.",
        CardDrawn e => $"{PlayerLabel(e.Actor)} robó una carta.",
        PhaseChanged e => $"Cambio de fase: {PhaseDisplay.Label(e.To)} ({PlayerLabel(e.CurrentPlayer)}).",
        GameWon e => $"¡{PlayerLabel(e.Winner)} ganó la partida!",
        // Design D6: HeadquartersSelected carries no territory by design
        // (its own doc comment — GameState.Log is public/unredacted), so
        // this case physically cannot name one.
        HeadquartersSelected e => $"{PlayerLabel(e.Player)} eligió su Cuartel General.",
        HeadquartersRevealed => "Todos los jugadores revelaron sus Cuarteles Generales.",
        HeadquartersCaptured e => $"{PlayerLabel(e.Attacker)} capturó el Cuartel General de {PlayerLabel(e.OriginalOwner)} en {e.Territory.Value}.",
        _ => "Ocurrió un evento."
    };

    /// <summary>
    /// A stable, 1-based label ("Jugador N") keyed only by <see cref="PlayerId"/>
    /// — this presenter is pure and has no access to <c>PlayerConfig</c>
    /// names, matching <see cref="GameErrorPresenter"/>/<see cref="PhaseDisplay"/>'s
    /// single-argument static-mapping shape. Matches the 1-based numbering
    /// already used by <c>Setup.razor</c>'s default player names.
    /// </summary>
    private static string PlayerLabel(PlayerId id) => $"Jugador {id.Value + 1}";
}
