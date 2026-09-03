# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this is

A from-scratch implementation of the board game **Risk** as a Blazor Server app on **.NET 8**.

`Risk.Domain` + `Risk.Engine` (+ `Risk.Tests`) is the headless rules engine, and `Risk.Web` is a playable Blazor Server UI built on top of it (hot-seat: all players share one browser tab, turn by turn). `Risk.AI` is **not implemented yet** — it was deliberately deferred so `Risk.Engine`'s public API (`IGameEngine.Observe`/`PlayerView`) stays a clean contract for a future human-equivalent client (including AI) with no access to hidden information; `Risk.Web` proves that contract works by consuming it exactly the way an AI client eventually would, never reading `GameState` directly.

## Commands

```bash
dotnet build Risk.sln          # build everything
dotnet test Risk.sln           # run the full xUnit suite
dotnet test --filter FullyQualifiedName~AttackCommandTests   # run one test class
dotnet test --filter FullyQualifiedName~AttackCommandTests.SpecificTestName  # run one test
```

There is no lint/format command configured. `Nullable` and `ImplicitUsings` are enabled on all three projects.

```bash
dotnet run --project src/Risk.Web   # run the playable UI
```

## Documentation

- `docs/casos-de-uso.md` — use cases for the engine's command surface, with flows and `GameErrorCode` mappings.
- `docs/historias-usuario.md` — user stories with acceptance criteria.
- `docs/diagrama-relacional.md` — entity-relationship diagram of the in-memory domain model (there is no database; `GameSessionService` holds everything in memory, scoped per Blazor circuit).

These three `docs/` files are in Spanish, matching this repo's existing convention for player/stakeholder-facing prose (see `README.md`); code, identifiers, and comments stay in English regardless.

## Architecture

Three projects, one dependency direction: `Risk.Domain` (no dependencies) ← `Risk.Engine` (depends only on `Risk.Domain`) ← `Risk.Tests` (references both).

- **`Risk.Domain`** (`src/Risk.Domain`) — dependency-free vocabulary and static seed data: `Territory`/`Continent`/`WorldMap` (the real 42-territory, 6-continent classic Risk map with a symmetric adjacency graph including sea routes — `WorldMap.AreAdjacent`/`NeighborsOf`), `Card`/`Deck` (44-card standard deck), `PlayerId`/`TerritoryId`/`ContinentId`, `IDiceRoller`, `GameError`/`GameErrorCode`. It holds no rules — see the note below on where validation lives.
- **`Risk.Engine`** (`src/Risk.Engine`) — owns every rule and the entire mutable-looking-but-immutable game loop:
  - `GameState` (`State/GameState.cs`) is one immutable record: territory ownership/troop map, per-player state, whose turn/phase it is, the remaining deck, an append-only event `Log`, and game status (`InProgress`/`Won`). State transitions are always `state with { ... }`, never in-place mutation.
  - `IGameEngine.Execute(GameState, GameCommand) -> CommandResult<GameState, GameEvent>` is the **single entry point** for every action. `GameEngine.Execute` (`GameEngine.cs`) runs a fixed validation pipeline before any command-specific logic: game-already-won check → actor-is-current-player check → pending-occupation gate (a conquest must be resolved via `OccupyCommand` before anything else) → mandatory-card-trade gate (splits into two rules sharing one flag, `TurnState.MandatoryTradeDown`: `Phase == Reinforce && count >= 5` at turn start, or the flag armed by an elimination pushing the eliminator to 6+ cards in `ExecuteAttack` and cleared once a trade in `ExecuteTradeCards` leaves ≤4 — both block all but `TradeCardsCommand`/`OccupyCommand`) → phase check for that command type. Callers never pre-validate; if you add a new command, wire its validation into this pipeline, don't scatter checks in the UI/AI layer later.
  - `CommandResult<TState, TEvent>` (`Results/CommandResult.cs`) is a sealed two-case record: `Ok(State, Events)` / `Rejected(GameError)`. Exceptions are reserved for programmer errors (unreachable switch arms), never for rule violations.
  - `GameCommand` (`Commands/`) is a closed record hierarchy: `PlaceTroopsCommand`, `TradeCardsCommand`, `AttackCommand`, `OccupyCommand`, `FortifyCommand`, `EndPhaseCommand`. `GameEvent` (`Events/`) mirrors this for things that happened (`TroopsPlaced`, `BattleResolved`, `TerritoryConquered`, `TerritoryOccupied`, `TroopsFortified`, `CardsTraded`, `PlayerEliminated`, `CardDrawn`, `GameWon`, `PhaseChanged`, `TerritoriesAssigned`) — both accumulate into `GameState.Log` and are also returned per-call so a future UI can animate just the delta.
  - Combat (`Combat/BattleResolver.cs`) rolls dice through the constructor-injected `IDiceRoller` — never a static/ambient random source — so combat is deterministic in tests. Setup's territory deal (`Setup/GameSetup.cs`) still uses `Random.Shared` and is a known testability gap (no seedable abstraction there yet).
  - Fortify validates connectivity with a BFS restricted to the acting player's owned territories (`Rules/ConnectivityRules.HasFriendlyPath`), not simple direct adjacency.
  - `IGameEngine.Observe(GameState, PlayerId) -> PlayerView` (`Views/PlayerView.cs`) returns a redacted view — other players' hands appear only as counts — so a future AI/UI client structurally cannot see hidden information, per the "no tricks" requirement for AI.
  - Turn rotation and phase advancement live at the bottom of `GameEngine.cs` (`ExecuteEndPhase`/`AdvancePhase`/`AdvanceFromAttackToFortify`/`AdvanceToNextPlayer`): Reinforce → Attack → Fortify → next player's Reinforce. The conquest card draw (if the departing player conquered a territory this turn and the deck isn't empty) happens at the Attack → Fortify transition, in `AdvanceFromAttackToFortify`. Per-turn flags (`ConqueredThisTurn`, `FortifyUsed`, `MandatoryTradeDown`) reset via a freshly constructed `TurnState` in `AdvanceToNextPlayer`/`AdvanceAfterSetupPlacement`.
- **`Risk.Tests`** (`tests/Risk.Tests`) — xUnit, mirrors the `src/` folder structure loosely (`Domain/`, `Engine/`, `Rules/`). `Fakes/` holds test doubles: `QueuedDiceRoller`/`AlwaysAttackerWinsDiceRoller` (deterministic `IDiceRoller` fakes) and `GameStateBuilder` (test state construction helper). `FullGameIntegrationTests.cs` drives a complete game end-to-end through `IGameEngine` as a single test, proving the pieces compose.
- **`Risk.Web`** (`src/Risk.Web`) — the Blazor Server UI. `Services/GameSessionService.cs` is the single stateful seam between Razor components and `IGameEngine`: registered scoped (one instance per Blazor circuit = one hot-seat game), it wraps `GameSetup.Create`/`engine.Execute`, exposes `State`/`LastEvents`/a `Changed` event, and always reads through `engine.Observe` (`PlayerView`) rather than the raw `GameState`, so hidden information never reaches a component. One Razor component per turn phase under `Components/Game/` (`SetupPanel`, `ReinforcePanel`, `AttackPanel`, `FortifyPanel`, `OccupyPrompt`, `CardPanel`, `DicePanel`, `LogPanel`, `VictoryScreen`, `PhaseIndicator`), each with its own scoped `.razor.css`; `Components/Pages/Game.razor` composes them into a two-column shell (board + side panel). The board (`BoardSvg.razor`) renders coordinates from `Models/HexGrid.cs`/`TerritoryLayout.cs`, a hex-grid presentation model kept separate from `Risk.Domain`'s adjacency graph. Everything under `Models/` is presentation-only (layout, palettes, dice-option lists, staging state for in-progress UI selections) — like `Risk.Domain`, it must never contain rule validation; that stays in `Risk.Engine` per the convention below. `tests/Risk.Web.Tests` covers these models/services and mirrors `Risk.Tests`' full-game style via `Services/GameSessionServiceFullGameIntegrationTests.cs`.

## Conventions specific to this repo

- **Strict TDD is mandatory** for engine/domain work: write the failing xUnit test first, confirm red, then implement. This has been followed consistently in the existing code — don't break the pattern.
- Classic/official Risk values (continent bonuses, starting troop counts by player count, the progressive card-trade scale `4/6/8/10/12/15/+5`) were deliberately pinned to the real board game rather than invented — check `src/Risk.Domain/Map/Continents.cs`, `src/Risk.Engine/Setup/GameSetup.cs`, and `src/Risk.Engine/Rules/CardTradeBonus.cs` before changing any of these numbers.
- All rule validation belongs in `Risk.Engine`, never in `Risk.Domain`, `Risk.Web`, or (once it exists) `Risk.AI` — this was an explicit architecture decision resolving a tension between where rules "live" (Domain, per the project's description) and where validation "lives" (Engine, per its own architecture requirements).
- The implementation history was built across a chain of local feature branches off a `feature/risk-blazor-game` tracker branch rather than one flat commit; that chain has since been merged into `main` and pushed to `origin/main` — `git log --oneline --all --graph` shows the incremental story of how a piece was built.
