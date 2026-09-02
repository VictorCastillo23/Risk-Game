using Risk.Domain.Errors;

namespace Risk.Web.Services;

/// <summary>
/// Maps every <see cref="GameErrorCode"/> to a short Spanish, user-facing
/// message (design D8) so panels never render the engine's raw
/// <see cref="GameError.Message"/> text. Falls back to
/// <see cref="GameError.Message"/> for any code without an explicit
/// mapping, so a future engine error code degrades gracefully instead of
/// throwing.
/// </summary>
public static class GameErrorPresenter
{
    public static string Describe(GameError error) => error.Code switch
    {
        GameErrorCode.NotYourTurn => "No es tu turno.",
        GameErrorCode.WrongPhase => "Esa acción no está disponible en esta fase.",
        GameErrorCode.NotOwner => "No sos dueño de ese territorio.",
        GameErrorCode.NotAdjacent => "Los territorios no son adyacentes.",
        GameErrorCode.InsufficientTroops => "No tenés suficientes tropas para esa acción.",
        GameErrorCode.InvalidDiceCount => "La cantidad de dados elegida no es válida.",
        GameErrorCode.InvalidCardSet => "Ese conjunto de cartas no se puede canjear.",
        GameErrorCode.MandatoryTradeRequired => "Tenés 5 cartas o más: primero debés canjear.",
        GameErrorCode.OccupationPending => "Primero tenés que ocupar el territorio conquistado.",
        GameErrorCode.FortifyAlreadyUsed => "Ya moviste tropas este turno.",
        GameErrorCode.NoFriendlyPath => "No hay un camino propio entre esos territorios.",
        GameErrorCode.GameOver => "La partida ya terminó.",
        GameErrorCode.InvalidPlayerCount => "La cantidad de jugadores no es válida para el modo de juego elegido.",
        GameErrorCode.InvalidTroopCount => "La cantidad de tropas indicada no es válida.",
        GameErrorCode.NoPendingOccupation => "No hay ninguna ocupación pendiente de confirmar.",
        GameErrorCode.ReinforcementIncomplete => "Todavía te quedan tropas de refuerzo por colocar.",
        GameErrorCode.ActorIsNeutral => "El ejército neutral no puede emitir órdenes.",
        _ => error.Message
    };
}
