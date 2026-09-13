# Tasks: risk-web-multiplayer

## Review Workload Forecast

| Field | Value |
|---|---|
| Estimated changed lines | ~900-1200 (incl. ~300 test lines) |
| 400-line budget risk | High |
| Chained PRs recommended | Yes |
| Suggested split | PR1 Phases 0-1 (hub + session), PR2 Phases 2-3 (UI), PR3 Phases 4-6 (reconnect, AI, polish) |
| Decision needed before apply | No (user approved single SDD flow; slicing decided at PR time) |

## Phase 0 — Foundation (infrastructure, no behavior change)
- [x] 0.1 No SignalR package/wiring needed (server SignalR ships in web SDK; fan-out is server-side over Blazor circuits — see design Correction)
- [x] 0.2 `Models/JoinCode.cs`: crypto-random 6-char generation (no `I/O/1/0`), `TryParse` — 12 unit tests green
- [x] 0.3 `Models/PlayerSeat.cs` + `Models/LobbyState.cs` records (trivial, exercised via Phase 1 session tests)
- [x] 0.4 Verify: build + 913 tests green (901 + 12 new), no UI change

## Phase 1 — Singleton session (core, TDD) — DONE (923/923 green)
- [x] 1.1 `IGameSession` extraction SKIPPED by design: nothing consumes both session types polymorphically yet; revisit in Phase 3 if needed
- [x] 1.2 `NetworkGameSession` + `NetworkGameRegistry` (collision retry) — 10 tests green
- [x] 1.3 `ClaimSeat`/`Join`/`Leave` + `Changed` fan-out (real `GameSetup`, fake engine)
- [x] 1.4 `Dispatch`: server-side `NotYourTurn` gate without touching engine + `Ok` path
- [x] 1.5 `Observe(playerId)` per-seat routing
- [x] 1.6 Registry singleton in `Program.cs` (hot-seat service untouched)

## Phase 2 — Setup + lobby UI (build-verified, no bUnit in repo) — DONE
- [x] 2.1 `Setup.razor`: Local (default, unchanged flow) / En red selector; host name+color + "Crear partida en red" → `/lobby/{code}`
- [x] 2.2 `Lobby.razor`: code + share link + seats list; host mode select + "Iniciar" (2-6); joiners see "Esperando…"; live update via `Changed`; started state shown (board routing = Phase 3)
- [x] 2.3 `Join.razor` (`/join`, `/join/{code?}`): code+name+spectator; full-table / started-game / bad-code errors; auto color; context set → lobby
- [x] 2.4 Plumbing: `NetworkedSeatContext` (scoped per circuit) + `NetworkCircuitHandler` (no-JS circuit id); spectator negative-id scheme (player zip stable)
- [x] 2.5 Suite **921/921** green (382 + 204 + 335; Web = 312 base + 12 JoinCode + 11 session/registry), zero new warnings

## Phase 3 — Game page integration (integration tests) — DONE (928/928 green)
- [x] 3.1 Proxy seam in `GameSessionService` (optional registry+context params; hot-seat constructions untouched): State/Players/LastEvents proxy, Execute→Dispatch, ObserveCurrentPlayer→own seat view (throws for seatless/spectator), WinnerMission routed, Reset drops circuit context; 6 proxy tests
- [x] 3.2 `Game.razor` gating: `IsMyTurn`/`CanSeeHand`; board locked + clicks ignored off-turn; waiting banner with current name; command panels only on own turn; own-hand CardPanel per device; MissionPanel on own turn; SavePanel hidden while networked (Phase 4 wires host save)
- [x] 3.3 Integration test: 3 seats, real engine, Classic — one claim each in engine order, ownership + 3 fan-outs asserted
- [x] 3.4 Eager `Changed` forwarding via explicit event accessor (relay gap found by failing test, then fixed)
- [x] 3.5 Full suite **928/928** (382 + 204 + 342), zero regressions

## Phase 4 — Reconnection + persistence — DONE (932/932 green, 0 warnings)
- [x] 4.1 `NetworkSeatBookmark` + persist in Lobby first-render + rebind in Lobby/Game first-render (interop-safe `OnAfterRenderAsync` only; best-effort try/catch); "Ir al tablero" button when started
- [x] 4.2 Host migration: `Leave` drops role + promotes first connected human; vacant role claimed by next human `Join`; `OnCircuitClosedAsync` frees bindings; first-spectator never hosts
- [x] 4.3 Host save (Lobby button → same `SaveAsync` path over proxied snapshot) + "Abrir sala en red" (Saved → fresh code + `Restore`, old bindings dropped)
- [x] 4.4 Session TDD: host-promote, vacant-promote, spectator-host rules, `Restore` (4 new tests)
- [x] 4.5 Double-host bug found by failing test and fixed (leaver must drop the role with the circuit)

## Phase 5 — AI seats in networked games — DONE (935/935 green, 0 warnings)
- [x] 5.1 Lobby host "Añadir IA" (pre-start, first-free color, no circuit binding) + `ClaimSeat(isAi)` path
- [x] 5.2 Server-side `AdvanceAiTurns` after every accepted human turn + once at `Start` (bot may open); per-turn `Changed` fan-out; `LastEvents` scoped to each bot turn's delta
- [x] 5.3 `AiTurnFailure` (Rejected/BudgetExhausted) + `AiFailure` on session + proxy passthrough + `game-error` banner in Game.razor
- [x] 5.4 `MarkSeenByAllBots` backfill (TwoPlayer Setup-B fix, same as hot-seat AI seats) + `RebuildBotRegistry` on Start/Restore
- [x] 5.5 Session TDD: real-bot Claim handoff, stub hard-fail (state untouched, no retry), Restore rebuilds registry (3 tests + `RejectingBotStub`)
- [x] 5.6 Wiring: Risk.Web→Risk.AI reference; `InternalsVisibleTo(Risk.Web)` for the tuned `RunTurn` budget default (same rationale as hot-seat AI seats); injectable `botFactory` seam for tests

## Phase 6 — Verification + polish — DONE (935/935 green, manual OK)
- [x] 6.1 Full suite green with zero regressions (382 + 204 + 349), 0 warnings
- [x] 6.2 Polish in place: "Esperando a X…" turn banner, reconnection via bookmark, `(desconectado)` lobby flags, AiFailure `game-error` banner
- [x] 6.3 Manual: user ran the app (`http://localhost:5162`) and played the networked flow — works perfectly
