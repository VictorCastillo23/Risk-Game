using Risk.Domain.Players;

namespace Risk.Web.Models;

/// <summary>
/// UI-only player identity: display name, chosen color swatch, and
/// human/AI flag. Zipped against <c>GameSetup.Create</c>'s implicit
/// <c>PlayerId(0..N-1)</c> order in <see cref="Services.GameSessionService.Start"/>.
/// Never merged into the engine's <c>PlayerState</c> — it is cosmetic/config
/// data with no place in tested engine state.
/// </summary>
public sealed record PlayerConfig(PlayerId Id, string Name, string ColorHex, bool IsAi);
