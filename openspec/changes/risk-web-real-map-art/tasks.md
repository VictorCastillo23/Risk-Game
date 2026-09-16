# Tasks: risk-web-real-map-art

## Review Workload Forecast

| Field | Value |
|---|---|
| Estimated changed lines | ~1100-1250 |
| 400-line budget risk | High |
| Chained PRs recommended | Yes |
| Suggested split | PR1 → PR2 → PR3, feature branch chain |
| Delivery strategy | auto-chain |
| Chain strategy | feature-branch-chain |

Decision needed before apply: No
Chained PRs recommended: Yes
Chain strategy: feature-branch-chain
400-line budget risk: High

`TerritoryLayout`/`BoardSvg.razor` are compile-coupled — deleting
`Polygons`/`ContinentBounds` breaks `BoardSvg.razor` until rewritten — so
stacked-to-main can't keep main green mid-chain. Feature-branch-chain
(tracker: `feature/risk-web-real-map-art`) matches this repo's own
`feature/risk-blazor-game` precedent.

### Suggested Work Units

| Unit | Goal | PR base | Focused test | Runtime harness | Rollback |
|---|---|---|---|---|---|
| 1 | Asset move+verify, `MarkerInk` | tracker | `--filter MarkerInkTests` | N/A, unused till PR3 | Revert PR1 |
| 2 | `TerritoryLayout`+tests, delete `HexGrid` | PR1 | `--filter TerritoryLayoutTests\|MarkerPlacementTests` | N/A, `BoardSvg` not yet updated | Revert PR2 |
| 3 | `BoardSvg`/css, docs, verify | PR2 → tracker → main | `dotnet test Risk.sln` | `dotnet run --project src/Risk.Web`, 42-marker QA | Revert PR3 |

PR2 alone exceeds 400 lines (42-point table is one indivisible unit) —
accept as `size:exception` inside the chain.

## Phase 1: Asset verification & move
- [x] 1.1 Verify `mapa_risk_simplificado_v4_transparente.png` real pixel size + alpha min/max: throwaway PowerShell `System.Drawing` script, `LockBits` over the alpha plane (no PIL available).
- [x] 1.2 `git mv mapa_risk_simplificado_v4_transparente.png src/Risk.Web/wwwroot/images/world-map.png`.
- [x] 1.3 If size ≠ 1340x876, use real values in Phase 3; if min alpha == 255, flag Phase 4's `.board-svg` gradient for removal.

## Phase 2: `MarkerInk` (TDD)
- [x] 2.1 RED `tests/Risk.Web.Tests/Models/MarkerInkTests.cs`: Theory over `PlayerPalette.Swatches` (read-only) and `BoardColors` (read-only) constants asserting `ContrastRatio(fill, InnerRingFor(fill)) >= 3.0`; plus a test reading `src/Risk.Web/wwwroot/app.css` (read-only) for `--ink`/`--parchment-light`. Confirm compile-red.
- [x] 2.2 GREEN `src/Risk.Web/Models/MarkerInk.cs` per design D8: `Outer`, `InnerOnDark`/`InnerOnLight`, threshold `0.18`, `RelativeLuminance`, `ContrastRatio`, `InnerRingFor`. Verify design's hand-computed worst cases empirically.

## Phase 3: `TerritoryLayout` rewrite
- [x] 3.1 RED rewrite `tests/Risk.Web.Tests/Models/TerritoryLayoutTests.cs` per design's Testing Strategy: keep the 6 `Coordinates_*`/`ContinentOf_*` facts, delete `Polygons_*`(2)/`PolygonPointsAttr_*`/`ContinentBounds_*`(2), remove `Coordinates_AllFallWithinTheDeclaredCanvasBounds` (→3.2). Confirm red.
- [x] 3.2 RED create `tests/Risk.Web.Tests/Models/MarkerPlacementTests.cs` (delete `HexAdjacencyRegressionTests.cs`): `Coordinates_NoTwoMarkersOverlap` (861 pairs, `RadiusOf(a)+RadiusOf(b)+4`), `Coordinates_EveryMarkerFitsFullyInsideTheCanvas`, `RadiusOverrides_AreKnownTerritoriesWithinTheAllowedBand` (`[13,17]`), `ContinentLabelAnchors_EachSitsNearItsOwnContinent`. Confirm red.
- [x] 3.3 GREEN rewrite `src/Risk.Web/Models/TerritoryLayout.cs` per design D1/D3/D7/D9: 42-entry `TerritorySeed`, 6-entry `ContinentLabelSeed`, `RadiusOf`/empty `RadiusOverrides`; delete `Polygons`/`PolygonPointsAttr`/`ContinentBounds`/`HexSize`/`ContinentOrigins`. Largest manual unit — author against `world-map.png`, iterate on 3.2; Europe's 7 + Central America/Caribbean are likely `RadiusOverrides` candidates (each <16 needs an inline SC 2.5.8 comment).
- [x] 3.4 Delete `src/Risk.Web/Models/HexGrid.cs` + `tests/Risk.Web.Tests/Models/HexGridTests.cs`. `dotnet test Risk.sln` green.

## Phase 4: `BoardSvg` rewrite
- [x] 4.1 Rewrite `src/Risk.Web/Components/Game/BoardSvg.razor` per design's layer-order block: `<image>` base layer, label-only continents `<g>`, sea-route-filtered edges `<g>`, 3-circle territory markers. `EventCallback<TerritoryId>`/`Locked` wiring unchanged.
- [x] 4.2 Update `src/Risk.Web/Components/Game/BoardSvg.razor.css` per D8: drop `.board-continent-halo`; new ring classes (widths only, colors from C#); retarget selected/hover at `.board-marker-ring-outer` ONLY; `max-width:1340px` on `.board-svg`; resolve gradient per 1.3.

## Phase 5: Docs & verification
- [x] 5.1 `CLAUDE.md`: replace the hex-grid sentence with design's artwork-based replacement text (design File Changes section, verbatim).
- [x] 5.2 `dotnet test Risk.sln` green, zero new warnings.
- [x] 5.3 Manual `dotnet run --project src/Risk.Web`: all 42 markers inside painted territory + legible; spot-check outer ink ring vs art at all 42 positions for backgrounds darker than mid-gray.
