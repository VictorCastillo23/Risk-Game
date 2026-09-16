# Proposal: risk-web-real-map-art — Real map artwork replaces the hex-grid board

## Intent
The board renders as a stylized hex-grid mosaic because real cartography was a
confirmed non-goal (`sdd/risk-web-ui/design`, D6). The user wants the shipped
watercolor map (`mapa_risk_simplificado_v4_transparente.png`, 1340x876 RGBA,
transparent background, 6 continents color-coded to match `Risk.Domain`)
instead. This reverses D6 deliberately — and D6's own isolation rationale
holds: only 7 files reference `TerritoryLayout`/`HexGrid`.

## Scope / Affected Areas
Paths relative to `src/Risk.Web/` and `tests/Risk.Web.Tests/Models/`.

| Path | Impact | Change |
|---|---|---|
| `wwwroot/images/world-map.png` | New | PNG moved from repo root, no copy left behind |
| `Models/TerritoryLayout.cs` | Rewrite | 1340x876 viewBox; 42 hand-placed points; drop `Polygons`/`PolygonPointsAttr`/`ContinentBounds` |
| `Models/HexGrid.cs` | Removed | Dead once polygons go |
| `Components/Game/BoardSvg.razor(.css)` | Rewrite | `<image>` base layer + owner-colored marker per territory; sea routes and "+bonus" labels only |
| `TerritoryLayoutTests.cs` | Rewrite | Coordinate invariants only |
| `HexGridTests.cs` | Removed | — |
| `HexAdjacencyRegressionTests.cs` | Replaced | Minimum-distance collision check (Q8) |
| `CLAUDE.md` | Modified | The one hex-grid sentence |

### Out of Scope
- `Risk.Domain`, `Risk.Engine`, `Risk.AI`, `Risk.Tests` — presentation-only swap; rules stay in the engine.
- `Game.razor`, phase panels, `GameSessionService`, `BoardSelection`/`BoardColors`/`BoardEdges`, `ContinentPalette`/`ContinentDisplay`.
- Hand-traced hit-region polygons; PNG size/preload work; unrelated `CLAUDE.md` staleness.

## Capabilities
- **New** — `board-map-rendering`: ownership, troop counts, selection, continent bonuses and sea routes rendered over real map artwork.
- **Modified** — None (no `openspec/specs/` exists yet).

## Approach
SVG stays the interactive layer: `<image>` renders the PNG, and each territory
polygon becomes a fixed-radius owner-colored marker — ownership reads as
"army on the map," which raster art supports without per-territory shape
data. Asset referenced by relative path through the already-wired
`UseStaticFiles()` (`@Assets[]` is .NET 9+, unusable on this TFM).

## Risks
| Risk | Likelihood | Mitigation |
|---|---|---|
| 42 hand-placed coordinates: largest manual effort, error-prone | High | Own work unit in `sdd-tasks` + visual QA pass |
| Asia's 12 markers overlap or fall below 24x24 px | High | Iterate placement; invoke SC 2.5.8 "Essential" only where truly dense |
| Geometric adjacency invariant lost | Certain | Accepted; weaker collision check (Q8) |
| PNG alpha may be opaque, not transparent | Med | Check alpha extrema before relying on the ocean gradient |
| ~900KB eager load, no SVG lazy-load | Med | Deferred; flag to `sdd-design` |

## Rollback
Revert the change commits. No engine, schema or persisted state is touched,
so the revert restores `HexGrid.cs`, the hex layout and the deleted tests
intact.

## Dependencies
The PNG asset (present, untracked at repo root); `UseStaticFiles()` already
wired in `Program.cs`.

## Success Criteria
- [ ] Artwork renders at 1340x876 with all 42 markers visually inside their painted territory.
- [ ] Every phase still playable; `Game.razor` and its `EventCallback<TerritoryId>` wiring untouched.
- [ ] Owner color and troop count legible per marker; >=24x24 CSS px where layout allows, >=3:1 contrast against adjacent art.
- [ ] 5 sea-route lines, no land-adjacency lines, per-continent "+bonus" labels present.
- [ ] `dotnet test Risk.sln` green; no `HexGrid` reference left; `CLAUDE.md` sentence updated.

## Open Questions (defaults applied unless the user overrides)
1. Marker overlay, not traced per-territory polygons.
2. PNG-native 1340x876 viewBox (replaces synthetic 1200x760).
3. 42 hand-placed points by visual inspection — no automated shortcut; the art carries no metadata.
4. No continent halo `<rect>` (PNG already color-codes); "+bonus" anchored to each continent's coordinate bounding box.
5. Sea routes only; land borders are already painted in the artwork.
6. Asset at `wwwroot/images/world-map.png`.
7. 24x24 px target floor where the layout allows, "Essential" exception for dense clusters only, per-marker 3:1 contrast validated locally rather than globally.
8. Replace `HexAdjacencyRegressionTests` with a minimum-pixel-distance collision check rather than deleting it. Accepted tradeoff, stated explicitly: the hex-edge-sharing-vs-`WorldMap.AreAdjacent` invariant has no geometric equivalent once positions are bare points, so adjacency correctness now rests on manual visual QA.
