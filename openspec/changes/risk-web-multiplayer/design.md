# Design: risk-web-multiplayer

## Technical Approach

```
Blazor Server App
 GameHub (SignalR, group = join code)
   JoinGame / LeaveGame / DispatchCommand / RequestPlayerView / StartGame
 NetworkGameSessionService (singleton per game via keyed registry)
   owns GameState, PlayerSeat[], JoinCode; wraps IGameEngine.Execute
 Clients (one circuit each) -> local GameState copy
   existing Razor components render unchanged; command UI gated on
   State.Turn.CurrentPlayer == MyPlayerId
```

All behavior lands in `Risk.Web`. `Risk.Engine` stays the single rule
authority: the hub calls `Execute` exactly like `GameSessionService`
does today, then broadcasts the returned `GameEvent[]` delta.

## Correction (Phase 0, found during implementation)

**No raw SignalR `GameHub` + JS client.** Every browser tab already holds
a persistent SignalR *circuit* to the Blazor Server app — opening a second
JS `HubConnection` per device would be redundant transport. Instead the
"hub" is server-side fan-out: `NetworkGameSession` (singleton per game)
raises events, and each connected circuit re-renders via Blazor's own
`InvokeAsync(StateHasChanged)`. Same real-time guarantee, zero extra JS,
and Blazor's built-in circuit reconnect covers the transport layer —
reconnection work shrinks to seat restore only. A wire-level hub is
deferred until a non-Blazor client (e.g. native mobile app) needs one.

Consequences: `Services/GameHub.cs` and `wwwroot/js/signalr-reconnect.js`
are REMOVED from the file table; replaced by
`Services/NetworkGameSession.cs` (singleton, event fan-out) and
`Services/NetworkGameRegistry.cs` (code -> session). `Program.cs` needs
no SignalR wiring. The client Hub-event contract
(`ReceiveState`/`ReceiveEvents`/...) becomes server-side `Action`
subscriptions per circuit.

## Key Decisions (D1-D6)

- **D1 — SignalR groups per game.** Native to Blazor Server, no extra
  infra. Scales to a 6-player + spectators table trivially.
- **D2 — Server-authoritative singleton session.** Hot-seat's
  scoped-per-circuit service becomes a per-game singleton held in a
  `NetworkGameRegistry` (join code -> session). Scoped service is kept
  for `Local` mode; DI selects the implementation by game variant.
- **D3 — Delta broadcast, full state on join.** `GameState.Log` is
  already append-only, so `GameEvent[]` per accepted command is the
  natural wire delta. Join/reconnect pulls full `GameState` once.
- **D4 — Private PlayerView per connection.** `engine.Observe(state,
  playerId)` is called server-side and sent ONLY via `Clients.Caller`.
  The hub never sends one player's view to another connection: the
  hidden-information contract is a routing property, not a convention.
- **D5 — Components read local state (unchanged).** Board/panels keep
  reading a `GameState` parameter (design D5 of the hot-seat UI). The
  only new input is `MyPlayerId` for gating command UI.
- **D6 — Reconnection = idempotent seat restore.** `sessionStorage`
  holds `(code, seatIndex)`; `OnConnectedAsync` reconciles by live
  `ConnectionId` first, then stored seat index. Same seat replays
  full state + private view; no duplicate seats ever.

## Interface Sketches

```csharp
// Models/JoinCode.cs
public sealed record JoinCode(string Value)
{
    public static JoinCode Generate();                       // 6-char, no I/O/1/0
    public static bool TryParse(string? s, out JoinCode code);
}

// Models/PlayerSeat.cs
public sealed record PlayerSeat(
    PlayerConfig Config,
    string? ConnectionId,   // null = AI seat or not yet connected
    bool IsHost,
    bool IsConnected,
    bool IsSpectator);

// Services/IGameSession.cs — extracted seam
public interface IGameSession
{
    GameState? State { get; }
    IReadOnlyDictionary<PlayerId, PlayerConfig> Players { get; }
    CommandResult<GameState, GameEvent> Execute(GameCommand command);
    PlayerView Observe(PlayerId player);
    event Action? Changed;
}

// Services/NetworkGameSession.cs — singleton per game (see Correction
// above: server-side fan-out over Blazor circuits, no wire hub)
public sealed class NetworkGameSession
{
    void Join(PlayerSeat seat);          // claims a seat, binds circuit
    void Leave(PlayerId seat);           // frees binding, keeps seat
    CommandResult<GameState, GameEvent> Dispatch(GameCommand command);
    PlayerView Observe(PlayerId player); // private view, caller-side only
    event Action? Changed;               // fan-out: every circuit re-renders
}
```

Per-circuit subscriptions replace the wire Hub-event contract: each
circuit renders from the shared `State` on `Changed` and reads its own
`PlayerView` for the hand panel — exactly like hot-seat does today.

## File Changes

| File | Action |
|---|---|
| `Risk.Web.csproj` | Unchanged (server SignalR ships in the web SDK; no package needed) |
| `Program.cs` | Register `NetworkGameRegistry` singleton (no hub mapping) |
| `Services/NetworkGameSession.cs` | Create (singleton per game, event fan-out) |
| `Services/NetworkGameRegistry.cs` | Create (code -> session, collision retry) |
| `Services/IGameSession.cs` | Create (extract seam from `GameSessionService`) |
| `Services/NetworkGameSession.cs` | Create (singleton per game) |
| `Services/NetworkGameRegistry.cs` | Create (code -> session) |
| `Models/JoinCode.cs`, `Models/PlayerSeat.cs`, `Models/LobbyState.cs` | Create |
| `Components/Pages/Setup.razor` | Add Local/En red selector + lobby routing |
| `Components/Pages/Lobby.razor` | Create (host view) |
| `Components/Pages/Join.razor` | Create (joiner view) |
| `Components/Pages/Game.razor` | Circuit subscription + turn gating (`MyPlayerId`) |
| `tests/.../JoinCodeTests.cs`, `NetworkGameSessionTests.cs`, `MultiplayerIntegrationTests.cs` | Create |

## Testing Strategy
Strict TDD per repo convention. Unit: `JoinCode` generation/parse,
hub dispatch validation with mocked `IGameEngine`. Integration:
`WebApplicationFactory` + SignalR test client, 3 clients join and play
a full Classic game end-to-end. Regression: 901 existing tests green.
Manual: 3 real devices on LAN before merge.
