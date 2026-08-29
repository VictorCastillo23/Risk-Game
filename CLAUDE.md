# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this is

A from-scratch implementation of the board game **Risk** as a Blazor Server app on **.NET 8**. The full game design (map, turn structure, combat math, card economy, AI heuristics, UI requirements) is specified in `prompt.md` at the repo root — read it before making rules or architecture decisions, it is the source of truth, not this file.

Only the headless rules engine exists so far: `Risk.Domain` + `Risk.Engine` + `Risk.Tests`. `Risk.AI` and `Risk.Web` (the Blazor UI) are **not implemented yet** — they are out of scope for the current code and were deliberately deferred so `Risk.Engine`'s public API stays a clean contract for a future human-equivalent client (including AI) with no access to hidden information.

## Commands

```bash
dotnet build Risk.sln          # build everything
dotnet test Risk.sln           # run the full xUnit suite
dotnet test --filter FullyQualifiedName~AttackCommandTests   # run one test class
dotnet test --filter FullyQualifiedName~AttackCommandTests.SpecificTestName  # run one test
```

There is no lint/format command configured. `Nullable` and `ImplicitUsings` are enabled on all three projects.

## Architecture

Three projects, one dependency direction: `Risk.Domain` (no dependencies) ← `Risk.Engine` (depends only on `Risk.Domain`) ← `Risk.Tests` (references both).

- **`Risk.Domain`** (`src/Risk.Domain`) — dependency-free vocabulary and static seed data: `Territory`/`Continent`/`WorldMap` (the real 42-territory, 6-continent classic Risk map with a symmetric adjacency graph including sea routes — `WorldMap.AreAdjacent`/`NeighborsOf`), `Card`/`Deck` (44-card standard deck), `PlayerId`/`TerritoryId`/`ContinentId`, `IDiceRoller`, `GameError`/`GameErrorCode`. It holds no rules — see the note below on where validation lives.
- **`Risk.Engine`** (`src/Risk.Engine`) — owns every rule and the entire mutable-looking-but-immutable game loop:
  - `GameState` (`State/GameState.cs`) is one immutable record: territory ownership/troop map, per-player state, whose turn/phase it is, the remaining deck, an append-only event `Log`, and game status (`InProgress`/`Won`). State transitions are always `state with { ... }`, never in-place mutation.
  - `IGameEngine.Execute(GameState, GameCommand) -> CommandResult<GameState, GameEvent>` is the **single entry point** for every action. `GameEngine.Execute` (`GameEngine.cs`) runs a fixed validation pipeline before any command-specific logic: game-already-won check → actor-is-current-player check → pending-occupation gate (a conquest must be resolved via `OccupyCommand` before anything else) → mandatory-card-trade gate (≥5 cards blocks all but `TradeCardsCommand`/`OccupyCommand`) → phase check for that command type. Callers never pre-validate; if you add a new command, wire its validation into this pipeline, don't scatter checks in the UI/AI layer later.
  - `CommandResult<TState, TEvent>` (`Results/CommandResult.cs`) is a sealed two-case record: `Ok(State, Events)` / `Rejected(GameError)`. Exceptions are reserved for programmer errors (unreachable switch arms), never for rule violations.
  - `GameCommand` (`Commands/`) is a closed record hierarchy: `PlaceTroopsCommand`, `TradeCardsCommand`, `AttackCommand`, `OccupyCommand`, `FortifyCommand`, `EndPhaseCommand`. `GameEvent` (`Events/`) mirrors this for things that happened (`TroopsPlaced`, `BattleResolved`, `TerritoryConquered`, `TerritoryOccupied`, `TroopsFortified`, `CardsTraded`, `PlayerEliminated`, `CardDrawn`, `GameWon`, `PhaseChanged`, `TerritoriesAssigned`) — both accumulate into `GameState.Log` and are also returned per-call so a future UI can animate just the delta.
  - Combat (`Combat/BattleResolver.cs`) rolls dice through the constructor-injected `IDiceRoller` — never a static/ambient random source — so combat is deterministic in tests. Setup's territory deal (`Setup/GameSetup.cs`) still uses `Random.Shared` and is a known testability gap (no seedable abstraction there yet).
  - Fortify validates connectivity with a BFS restricted to the acting player's owned territories (`GameEngine.HasFriendlyPath`), not simple direct adjacency.
  - `IGameEngine.Observe(GameState, PlayerId) -> PlayerView` (`Views/PlayerView.cs`) returns a redacted view — other players' hands appear only as counts — so a future AI/UI client structurally cannot see hidden information, per `prompt.md`'s "no tricks" requirement for AI.
  - Turn rotation and phase advancement live at the bottom of `GameEngine.cs` (`ExecuteEndPhase`/`AdvancePhase`/`AdvanceToNextPlayer`): Reinforce → Attack → Fortify → next player's Reinforce, with the end-of-turn card draw (if the departing player conquered a territory this turn and the deck isn't empty) and per-turn flag resets happening in `AdvanceToNextPlayer`.
- **`Risk.Tests`** (`tests/Risk.Tests`) — xUnit, mirrors the `src/` folder structure loosely (`Domain/`, `Engine/`, `Rules/`). `Fakes/` holds test doubles: `QueuedDiceRoller`/`AlwaysAttackerWinsDiceRoller` (deterministic `IDiceRoller` fakes) and `GameStateBuilder` (test state construction helper). `FullGameIntegrationTests.cs` drives a complete game end-to-end through `IGameEngine` as a single test, proving the pieces compose.

## Conventions specific to this repo

- **Strict TDD is mandatory** for engine/domain work: write the failing xUnit test first, confirm red, then implement. This has been followed consistently in the existing code — don't break the pattern.
- Classic/official Risk values (continent bonuses, starting troop counts by player count, the progressive card-trade scale `4/6/8/10/12/15/+5`) were deliberately pinned to the real board game rather than invented — check `src/Risk.Domain/Map/Continents.cs`, `src/Risk.Engine/Setup/GameSetup.cs`, and `src/Risk.Engine/Rules/CardTradeBonus.cs` before changing any of these numbers.
- All rule validation belongs in `Risk.Engine`, never in `Risk.Domain` or (once it exists) `Risk.Web`/`Risk.AI` — this was an explicit architecture decision resolving a tension in `prompt.md` between where rules "live" (Domain, per the doc's project description) and where validation "lives" (Engine, per the doc's own architecture requirements section).
- The implementation history lives across a chain of local feature branches off a `feature/risk-blazor-game` tracker branch (not merged or pushed as of this writing) rather than one flat commit — `git log --oneline --all --graph` shows the sequence if you need the incremental story of how a piece was built.
