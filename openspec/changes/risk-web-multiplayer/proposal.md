# Proposal: risk-web-multiplayer — Networked Multiplayer (one player per device)

## Intent
Enable networked multiplayer where each player joins from their own device
(phone, laptop, tablet) with real-time sync, while preserving the existing
hot-seat mode (all players on one device) as the default. A game is either
*local* (hot-seat, current behavior, unchanged) or *networked* (one player
per device, synced via SignalR).

## Scope
- **Risk.Web only.** No changes to `Risk.Domain`, `Risk.Engine`, `Risk.AI`,
  `Risk.Tests`. All engine rules (turn rotation, phase gating, redacted
  `PlayerView`) are reused as-is.
- Blazor Server + SignalR (native fit, no new infrastructure).
- Host creates a local or networked game; others join via 6-char code/link.
- All existing game modes (Classic, SecretMission, TwoPlayer, Capital) work
  in both variants. AI seats work in both variants.

## Out of Scope (explicit)
- Dedicated game server / container orchestration / Azure SignalR Service.
- Public matchmaking lobby, voice/video chat, native mobile app.

## Success Criteria
1. Host creates a networked game and gets a 6-char join code.
2. 2-6 players join from separate devices; all see the same board/turn/phase.
3. Only the current player's client can dispatch; others see the result in
   real time (target <500ms on internet).
4. A disconnected player rejoins and restores seat, hand, and state.
5. Hot-seat mode unchanged: all existing tests pass unmodified.

## Open Questions (defaults applied unless the user overrides)
1. Join code format: 6-char alphanumeric (default) — alternatives deferred.
2. Spectators: allowed, read-only, no host approval (default).
3. Host migration: automatic to first joined human (default).
4. AI in networked games: host may add bots pre-start (default); mid-game
   addition deferred.
5. Save/Resume in networked games: host-only (default).
