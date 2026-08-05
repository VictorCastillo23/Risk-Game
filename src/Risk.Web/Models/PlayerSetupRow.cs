namespace Risk.Web.Models;

/// <summary>
/// One row of the setup screen's player list before the game starts —
/// no <c>PlayerId</c> yet, since assignment only happens once
/// <c>GameSetup.Create</c> accepts the player count and
/// <see cref="Services.GameSessionService.Start"/> zips rows to
/// <c>PlayerId(0..N-1)</c> order.
/// </summary>
public sealed record PlayerSetupRow(string Name, string ColorHex, bool IsAi);
