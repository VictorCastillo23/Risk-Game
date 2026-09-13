# Spec: risk-web-multiplayer (delta)

## Capability: networked-game-mode (NEW)

### Requirement: game-variant selection at setup
`Setup.razor` MUST offer a `Local (hot-seat)` | `En red` selector.
Default MUST be `Local`. Selecting `En red` routes the host into the
networked lobby flow instead of starting a hot-seat game directly.

### Requirement: join code and lobby
- On creating a networked game the host MUST receive a 6-char uppercase
  alphanumeric join code (excluding ambiguous `I/O/1/0`) and a shareable
  link `/join/{code}`.
- Joiners at `/join/{code}` enter a name and wait in the lobby until the
  host starts (minimum 2 seated players, maximum 6).
- Joiners MAY join as spectators: read-only, no seat, no commands.

### Requirement: real-time state sync via SignalR
- One server-side session per game (`NetworkGameSession`), keyed by join
  code in `NetworkGameRegistry` — fan-out runs over the Blazor circuits
  the browsers already hold (see design Correction; no wire-level hub).
- The server MUST be authoritative: it owns the single `GameState` and runs
  every command through `IGameEngine.Execute`.
- Clients MUST receive the full `GameState` on join and the `GameEvent[]`
  delta after every accepted command, and render from their local copy
  with the existing components (zero component logic changes for display).

### Requirement: turn execution and validation
- Only the current player's client MAY dispatch commands; all other
  clients MUST render command UI disabled.
- The hub MUST reject (server-side) any command whose `Actor` is not
  `Turn.CurrentPlayer`; rejections go only to the sender.
- On `Ok` the server MUST broadcast the `GameEvent[]` delta to the group.

### Requirement: hidden information
- Full hands, missions, and headquarters MUST never be broadcast to other
  clients. Each connection receives ONLY its own `PlayerView` (via
  `Clients.Caller`), produced by the existing `engine.Observe` redaction.
- `CardDrawn` content stays redacted in the shared log, as today.

### Requirement: reconnection
- A player that closes the tab or loses connectivity MUST be able to
  reopen `/join/{code}` and automatically restore the same seat, hand,
  and state (seat ownership persisted in `sessionStorage`, reconciled
  server-side by connection id + seat index).
- Host disconnect MUST migrate lobby control to the first joined human
  automatically; the game itself continues uninterrupted (authority is
  server-side, not host-side).

## Capability: hot-seat-preservation (UNCHANGED)

### Requirement: local mode unaffected
- `Local` MUST behave exactly as today: scoped `GameSessionService` per
  circuit, no hub involved, no new UI steps.
- All 901 existing tests MUST pass without modification.
