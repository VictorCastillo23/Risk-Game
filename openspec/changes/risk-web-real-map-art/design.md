# Design: risk-web-real-map-art

## Technical Approach

SVG stays the interactive layer. `BoardSvg.razor` gains an `<image>` base
layer at the PNG's native 1340x876, and each `<polygon>` becomes an
owner-colored marker at a hand-placed pixel coordinate. `TerritoryLayout`
loses all derived geometry and becomes two hand-authored seed arrays (42
territory points, 6 continent label anchors). `HexGrid.cs` + tests are
deleted. `Risk.Domain`/`Engine`/`AI` untouched; `Game.razor`'s
`EventCallback<TerritoryId>` wiring is byte-identical.

## Architecture Decisions

| # | Choice | Rejected | Rationale |
|---|---|---|---|
| D1 | `TerritoryLayout` exposes `CanvasWidth=1340`, `CanvasHeight=876`, `MarkerRadius=17`, `RadiusOf(t)`, `Coordinates`, `ContinentOf`, new `ContinentLabelAnchors`. Delete `Polygons`, `PolygonPointsAttr`, `ContinentBounds`, `HexSize`, `ContinentOrigins`. | `ContinentBounds` re-derived from `Coordinates ± MarkerRadius` | With the halo `<rect>` dropped (Q4) the only consumer of a box is the label anchor. On real cartography continent bboxes **overlap** (EU/AF/AS), so a bbox top-left anchor lands on a neighbour's art. 6 hand anchors are authored in the same visual pass as the 42 points. See *Deviation from spec wording*. |
| D2 | `MarkerRadius = 17` viewBox units | 12 (=24 units wide) | WCAG 2.5.8's 24px floor is **CSS px after viewBox scaling**, not viewBox units. Reference desktop board column ≈ 1032 CSS px (`.game-shell` 1400 − 340 panel − 16 gap − 12 border) ⇒ scale ≈ 0.77. 34 units × 0.77 ≈ **26 CSS px** ≥ 24. r=12 would render ~18px and fail. |
| D3 | Seed keeps its `ContinentId` column | derive `ContinentOf` from `WorldMap` | Keeps the seed readable grouped per continent (authoring is continent-by-continent against the art) and keeps `ContinentOf_...MatchingItsRealContinent` a real cross-check instead of a tautology. |
| D4 | Marker's **outer** ring = constant dark ink; continent color moves to the "+bonus" label fill | continent-colored marker stroke (today's) | `ContinentPalette`'s muted mid-tones (`#7C8A4E` etc.) sit on that same continent's watercolor — no reliable 3:1 (SC 1.4.11). One constant ink ring clears 3:1 against every hue in the art and the ocean gradient, converting research's "validate 42 backgrounds" into one spot-check. Also keeps `ContinentPalette` production-alive. Fill contrast is a *separate* problem → **D8**. |
| D5 | `href="/images/world-map.png"` (root-relative) | `images/world-map.png` (relative, matching `app.css`) | Removes research's one unverified gap (SVG `<image href>` vs `<base>` resolution) by construction — no base resolution happens. `App.razor` already hardcodes `<base href="/" />` and `Program.cs` has no `UsePathBase`, so the two forms are identical today. |
| D6 | Filter sea routes at the call site: `BoardEdges.UniqueEdges().Where(e => BoardEdges.IsSeaRoute(e.A, e.B))` | new `BoardEdges.SeaRouteEdges()` | Proposal scopes `BoardEdges` as untouched; both members are already public and tested. Zero new API. |
| D7 | All 42 + 6 literals are **integral** pixel values (typed `double`) | fractional precision | Razor interpolates `double` with current culture; a Spanish culture would emit `123,4` and corrupt SVG attributes. Integral values format identically in every culture. (Today `PolygonPointsAttr` needed explicit `InvariantCulture` for exactly this reason — this removes the hazard instead of re-solving it.) |
| D8 | **Three-circle marker**: fill disc, then an *inner contrast ring* whose color is chosen by the fill's relative luminance, then D4's outer ink ring. New `Models/MarkerInk.cs` owns both ring colors + the luminance rule. | (a) "the ink ring satisfies 1.4.11, the interior is exempt"; (b) leave fill contrast to manual per-territory visual QA | **(a) fails on the numbers.** Measured against `--ink #2b2118`, 5 of the 9 possible fills are under 3:1 — blue `#1D4ED8` 2.27, purple `#7B2CBF` 2.17, black `#212529` **1.01**, unclaimed `#4B5563` 2.02, neutral `#6D4C41` 2.01 — and red 3.08 / green 3.01 are borderline. So even the generous reading (the fill's *literal* adjacent color is the ring, not the art) does not hold; asserting an interior exemption here would be asserting a failure. **(b) is not verifiable**: a marker's fill changes every time the territory changes hands, so per-territory screenshot review would owe 42 × 9 = 378 combinations, and no shipped checklist covers a fill that only appears mid-game. D8 instead makes the fill *never adjacent to map pixels at all* — the art touches the ink ring, the ink ring touches the contrast ring, the contrast ring touches the fill — and each of those pairs is a known-vs-known constant, computable in a unit test. |
| D9 | `RadiusOf(TerritoryId)` = `MarkerRadius` unless a named entry in `RadiusOverrides`; per-pair spacing test uses `RadiusOf(a) + RadiusOf(b) + 4` | one global radius with no escape hatch | Requirement 5 permits a smaller marker in dense clusters but the design had no mechanism to express one, so a placement conflict at apply time would have had no legal resolution. Bounds and the exception rule are fixed here rather than improvised later. |

### D8 rule (concrete)

`MarkerInk.InnerRingFor(fill)` returns `#f5ead0` (`--parchment-light`) when
the fill's WCAG relative luminance `< 0.18`, else `#2b2118` (`--ink`). At
that threshold the worst case is `(0.18+0.05)/0.0681 = 3.37:1` on the dark
side and `0.8783/(0.18+0.05) = 3.82:1` on the light side, so **any** sRGB
fill — not just today's nine — clears 3:1. Today's worst actual is red at
4.19:1.

Geometry for a marker of radius `R` (outer edge = `R`, so D2's 26 CSS px and
every spacing number are unchanged): fill disc `r = R−6`; inner ring
`circle r = R−4.5, stroke-width 3`; outer ink ring `circle r = R−1.5,
stroke-width 3`. Ring widths stay constant across overrides.

Both stroke colors are emitted from C# so one source of truth feeds the
markup and the test; the CSS keeps only widths. This is safe for
Requirement 7 because SVG presentation attributes map to author rules of
**specificity 0**, so `.board-territory-selected` still wins — and
selection must target the *outer* ring class only, leaving the contrast
ring (and therefore the fill guarantee) untouched; the existing cream
stroke + drop-shadow carry the highlight. The troop text keeps its current
`paint-order: stroke fill` black halo, so it stays legible where it
overhangs the rings (Requirement 4).

### D9 rule (concrete)

`RadiusOf(t) ∈ [13, 17]`, enforced by test. Bands:

- **16 ≤ R ≤ 17** — still ≥ 24 CSS px (24 / 0.770 = 31.2 units diameter ⇒ R ≥ 15.6). A smaller-but-compliant marker; **no WCAG exception is invoked**, no justification owed.
- **13 ≤ R < 16** — below the 24 px floor (R=13 ⇒ 20.0 CSS px). Each entry MUST carry an inline comment naming the neighbour it yields to and invoking SC 2.5.8 **"Essential"** for that territory specifically. This is the per-territory, documented exception Requirement 5 allows — never a blanket one.
- **R < 13** — forbidden. The two 3-unit rings would consume the disc and the troop count (a MUST, Requirement 4) would break before the target size did.

`RadiusOverrides` ships **empty**. It is populated only if hand-placement
proves the spacing test unsatisfiable for a specific pair. Note for
`sdd-tasks`: the spec's example ("Asia's 12") is the wrong worry — Asia's 12
sit on the largest landmass on the board. The real pressure is **Europe's 7**
(Iceland, Great Britain, Scandinavia, Northern/Western/Southern Europe,
Ukraine) plus the Central America / Caribbean pinch, and that is where the
placement budget belongs.

## Deviation from spec wording

Two spec sentences are satisfied by intent, not literally. Recorded here so
neither reads as an oversight:

1. **Continent bonus labels requirement / proposal default #4** — "near that
   continent's territory-coordinate bounding box". Bounding boxes of real
   continents **overlap**: Europe's box (Iceland → Ukraine → Southern
   Europe) intersects both Africa's and Asia's, so any bbox-derived anchor
   (corner *or* center) can land on a neighbour's painted art or inside
   another continent's box. The requirement's actual intent — *the label
   reads as belonging to that continent* — is met by 6 hand-placed anchors
   (D1), and is now **verified** rather than trusted, by the centroid test
   below. `specs.md` has been reworded to state this intent directly rather
   than the literal bbox wording.
2. **Ownership color and contrast requirement** — "against the locally
   adjacent map pixels". Under D8 a fill has no locally adjacent map pixels;
   the ink ring does. The requirement is met by construction (isolation),
   and the residual art-facing pair — ink ring vs artwork — is the one
   manual check, with a concrete threshold (below) instead of an eyeball.

## Data Flow

    wwwroot/images/world-map.png ──GET /images/world-map.png──▶ <image> (layer 0)
    TerritorySeed ──▶ Coordinates ─┬─▶ 3-circle markers (layer 3)
                     RadiusOf      ├─▶ troop / +pending <text>
                     ContinentOf   └─▶ 5 sea-route <line> endpoints (layer 2)
    ContinentLabelSeed ──▶ ContinentLabelAnchors ──▶ "+bonus" <text> (layer 1)
    GameState.Territories ──▶ BoardColors.OwnerColor ──▶ fill ──▶ MarkerInk.InnerRingFor ──▶ inner ring
    <g> click ──▶ EventCallback<TerritoryId> ──▶ Game.razor   (UNCHANGED)

## File Changes

| File | Action | Description |
|---|---|---|
| `src/Risk.Web/wwwroot/images/world-map.png` | Create | Move from repo root (`mapa_risk_simplificado_v4_transparente.png`), rename to English. `Sdk.Web` includes `wwwroot/**` by default — **no csproj change**. |
| `src/Risk.Web/Models/TerritoryLayout.cs` | Rewrite | Per D1/D3/D7/D9. |
| `src/Risk.Web/Models/MarkerInk.cs` | Create | D8: `Outer`, `InnerOnDark`, `InnerOnLight`, `RelativeLuminance`, `ContrastRatio`, `InnerRingFor`. A **new** file rather than an edit to `BoardColors`, which the proposal scopes as untouched. |
| `src/Risk.Web/Models/HexGrid.cs` | Delete | No consumer left. |
| `src/Risk.Web/Components/Game/BoardSvg.razor` | Rewrite markup | 4 layers (below). `@code` block unchanged. |
| `src/Risk.Web/Components/Game/BoardSvg.razor.css` | Modify | Drop `.board-continent-halo`; replace `.board-territory-marker` with `.board-marker-fill` / `.board-marker-ring-inner` / `.board-marker-ring-outer` (widths only, colors come from C#; drop `paint-order`); retarget `.board-territory-selected` and `:hover` at `.board-marker-ring-outer`; `.board-continent-label` gets continent fill + white `paint-order: stroke fill` halo; `.board-svg` gains `max-width: 1340px; margin: 0 auto` (never upscale past native). |
| `tests/.../TerritoryLayoutTests.cs` | Modify | See Testing Strategy. |
| `tests/.../MarkerInkTests.cs` | Create | D8 proof. |
| `tests/.../HexGridTests.cs` | Delete | — |
| `tests/.../HexAdjacencyRegressionTests.cs` | Delete → `MarkerPlacementTests.cs` | Rename is deliberate: it is no longer an adjacency test. |
| `CLAUDE.md` | Modify | One sentence (below). |

## Interfaces / Contracts

```csharp
public const double CanvasWidth = 1340;
public const double CanvasHeight = 876;
/// <summary>Default radius of a territory marker, in canvas units. 2*R scaled by the
/// reference board width (~1032 CSS px / 1340) clears WCAG 2.5.8's 24x24 px floor.</summary>
public const double MarkerRadius = 17;
public const double MinRadius = 13;   // ~20 CSS px — hard floor, see D9

/// <summary>Named exceptions only (D9). Empty unless hand-placement proves the
/// spacing test unsatisfiable; an entry below 16 owes an inline SC 2.5.8 justification.</summary>
private static readonly IReadOnlyDictionary<TerritoryId, double> RadiusOverrides = /* empty */;
public static double RadiusOf(TerritoryId t);   // override ?? MarkerRadius

private static readonly (string Name, string ContinentId, double X, double Y)[] TerritorySeed =
[
    ("Alaska", "NA", 96, 132), /* ...42 integral literals, grouped per continent... */
];
private static readonly (string ContinentId, double X, double Y)[] ContinentLabelSeed = [ /* 6 */ ];

public static IReadOnlyDictionary<TerritoryId, (double X, double Y)> Coordinates { get; }
public static IReadOnlyDictionary<TerritoryId, ContinentId> ContinentOf { get; }
public static IReadOnlyDictionary<ContinentId, (double X, double Y)> ContinentLabelAnchors { get; }
```

`BoardSvg.razor` layer order (document order = paint order):

```razor
<svg class="board-svg" viewBox="0 0 @TerritoryLayout.CanvasWidth @TerritoryLayout.CanvasHeight" ...>
  <image href="/images/world-map.png" x="0" y="0"
         width="@TerritoryLayout.CanvasWidth" height="@TerritoryLayout.CanvasHeight"
         aria-hidden="true" />                                  @* decorative: all state is in the markers *@
  <g class="board-continents">   @* label only — no <rect> *@
      <text class="board-continent-label" x="@a.X" y="@a.Y" text-anchor="middle" fill="@ContinentPalette.ColorOf(id)">
          @ContinentDisplay.Label(id) (+@continent.Bonus)</text>
  </g>
  <g class="board-edges">        @* 5 sea routes only *@ </g>
  <g class="board-territories">  @* per territory, click wiring unchanged *@
      <g transform="translate(@p.X, @p.Y)">
          <circle class="board-marker-fill"       r="@(r - 6)"   fill="@color" />
          <circle class="board-marker-ring-inner" r="@(r - 4.5)" fill="none" stroke="@MarkerInk.InnerRingFor(color)" />
          <circle class="board-marker-ring-outer" r="@(r - 1.5)" fill="none" stroke="@MarkerInk.Outer" />
          <text class="board-territory-troops" text-anchor="middle" dy="4">@t.Troops</text>
          <text class="board-territory-pending" text-anchor="middle" dy="30" ...>+@pending</text>
      </g>
  </g>
</svg>
```

Moving the circles inside the existing translate group (r-only, no
`cx`/`cy`) keeps one coordinate emission per territory. `r` is
`TerritoryLayout.RadiusOf(t)`, hoisted once per iteration.

**CLAUDE.md** — replace exactly:
> The board (`BoardSvg.razor`) renders coordinates from `Models/HexGrid.cs`/`TerritoryLayout.cs`, a hex-grid presentation model kept separate from `Risk.Domain`'s adjacency graph.

with:
> The board (`BoardSvg.razor`) renders the watercolor map artwork (`wwwroot/images/world-map.png`) as an SVG `<image>` layer with one owner-colored marker per territory, positioned from `Models/TerritoryLayout.cs` — hand-placed pixel coordinates in the artwork's own 1340x876 space, kept separate from `Risk.Domain`'s adjacency graph.

## Testing Strategy

**Count correction**: `TerritoryLayoutTests.cs` has **12** `[Fact]`s, not 13
(explore miscounted); **5**, not 6, are polygon/bounds-derived.

| Test (current) | Fate |
|---|---|
| `Coordinates_ContainsEveryWorldMapTerritory_ExactlyOnce` | keep as-is |
| `Coordinates_HasNoExtraEntriesBeyondWorldMapTerritories` | keep as-is |
| `Coordinates_HasNoDuplicatePositions` | keep as-is (subsumed by separation check, but free) |
| `Coordinates_HasNoNaNOrInfiniteValues` | keep as-is |
| `Coordinates_EveryAdjacencyPairHasBothEndpointsLaidOut` | keep as-is |
| `ContinentOf_...MatchingItsRealContinent` | keep as-is (real guard, per D3) |
| `Coordinates_AllFallWithinTheDeclaredCanvasBounds` | **move + tighten** → `MarkerPlacementTests`, inset by `RadiusOf(t)` |
| `Polygons_ContainsEveryWorldMapTerritory_ExactlyOnce` | delete |
| `Polygons_EveryTerritoryHasASixPointHexagon_...` | delete |
| `PolygonPointsAttr_ContainsEveryTerritory_AsANonEmptyString` | delete |
| `ContinentBounds_ContainsAllSixContinents_WithPositiveExtents` | delete → new `ContinentLabelAnchors_ContainsAllSixContinents_InsideTheCanvas` |
| `ContinentBounds_NoTwoContinentHalosOverlap` | delete — **real geography makes continent bboxes overlap**; asserting otherwise would now be wrong, not just dead |

New `tests/Risk.Web.Tests/Models/MarkerPlacementTests.cs` (RED first, per repo TDD convention):

```csharp
/// <summary>
/// PLACEMENT SANITY, NOT ADJACENCY CORRECTNESS. This replaces
/// HexAdjacencyRegressionTests, whose invariant ("two territories sharing a hex
/// edge must be WorldMap.AreAdjacent") has no geometric equivalent once positions
/// are bare points with no shape data. Whether a marker sits inside its painted
/// territory is now verified by manual visual QA only — an accepted reduction
/// (proposal Q8/Risks), recorded here so it is never mistaken for a guarantee.
/// </summary>
[Fact] public void Coordinates_NoTwoMarkersOverlap()
    // all 861 pairs: dist(a,b) >= RadiusOf(a) + RadiusOf(b) + RimGap  (38 at default radius)
[Fact] public void Coordinates_EveryMarkerFitsFullyInsideTheCanvas()   // inset by RadiusOf(t)
[Fact] public void RadiusOverrides_AreKnownTerritoriesWithinTheAllowedBand()  // D9: keys valid, value in [13, 17]
[Fact] public void ContinentLabelAnchors_EachSitsNearItsOwnContinent()        // continent bonus labels requirement, below
```

The separation threshold is expressed **in terms of the two markers' own
radii** so a D9 override buys real room instead of silently weakening the
test, and shrinking a radius can never loosen an unrelated pair. `RimGap =
4` is the visible gap (two markers never read as one blob). Failure mode is
intended: a too-close hand-placed pair fails the build and must be nudged,
or granted a documented override.

`ContinentLabelAnchors_EachSitsNearItsOwnContinent` — computed only from
`Coordinates` + `ContinentOf`, with no bbox-overlap logic:

- `centroid(c)` = arithmetic mean of that continent's territory coordinates; `spread(c)` = max distance from `centroid(c)` to any of its own territories.
- Assert the **nearest of the 6 centroids** to `ContinentLabelAnchors[c]` is `c`'s own — catches a swapped or typo'd anchor, which is the realistic failure.
- Assert `dist(anchor, centroid(c)) <= max(spread(c), 120)` — self-scaling, so Australia is held tight and Asia is not over-constrained.

If a legitimately ocean-side anchor ever trips the nearest-centroid assert,
the fix is to move the anchor, not to relax the test.

New `tests/Risk.Web.Tests/Models/MarkerInkTests.cs`:

```csharp
[Theory] // every PlayerPalette.Swatches entry + UnclaimedColor + UnknownOwnerColor + NeutralColor
public void InnerRingFor_ReachesThreeToOneAgainstEveryFill(string fill)
    => Assert.True(MarkerInk.ContrastRatio(fill, MarkerInk.InnerRingFor(fill)) >= 3.0);

[Fact] public void RingColors_StillMatchTheCssCustomProperties();  // reads src/Risk.Web/wwwroot/app.css
```

The first is not tautological: it pins the *outcome* for concrete palette
values, so adding a 7th swatch or nudging the 0.18 threshold fails the
build (the rejected alternative — asserting nothing and trusting the rule —
is exactly what left the contrast requirement open). Today's worst case is
red at 4.19:1. The second walks up from `AppContext.BaseDirectory` to the
directory holding `Risk.sln` and regex-reads `--ink` / `--parchment-light`,
so editing the CSS palette cannot silently invalidate the contrast proof.
Rejected for both: sampling the PNG's pixels in a test — it would add an
imaging dependency (ImageSharp/SkiaSharp) to `Risk.Web.Tests` and re-break
on every art revision.

**Manual QA** (tasks, not tests) — two items, both bounded:

1. All 42 markers visually inside their painted territory (proposal Q8, accepted).
2. **Outer ink ring vs artwork**, the one pair D8 cannot automate. `#2b2118`
   reaches 3:1 against any background lighter than ≈`#6D6D6D` in perceived
   luminance, so the acceptance rule is concrete: walk all 42 positions once
   and flag any marker whose surrounding art is darker than mid-gray
   (realistically only deep-ocean or a dark coastal wash). One constant
   color × 42 positions — it does **not** multiply by owner color, because
   D8 removed the fill from the art-facing boundary.

## Threat Matrix

N/A — no routing, shell, subprocess, VCS/PR automation, executable-file
classification, or process integration. The new asset is a static file
under the already-wired `UseStaticFiles()` with a constant path and no user
input.

## Migration / Rollout

No migration. Presentation-only; no schema, persisted state, or engine
contract touched. Revert = revert the commits.

## Open Questions

- [ ] **PNG alpha is unverified** — explore could not read raw alpha bytes
  (no shell). `sdd-apply` MUST check before assuming `.board-svg`'s
  radial-gradient ocean shows through (e.g. a throwaway PowerShell
  `System.Drawing` script reading the alpha plane, or an equivalent .NET
  check — no PIL available on this machine). If min alpha == 255 the
  background is opaque and the gradient is dead CSS — drop it or keep it
  only as a letterbox fill.
- [ ] **PNG dimensions unverified in this phase** (no shell in this phase).
  If the file is not exactly 1340x876, the viewBox constants must match the
  real size or the art distorts — check at apply time before authoring
  coordinates.
- [ ] **Small-viewport target size**: below ~1000 CSS px of board width the
  whole SVG scales down uniformly and markers drop under 24 CSS px (at the
  `max-width: 900px` single-column breakpoint, ~23px). Inherent to a
  responsive viewBox and unchanged from today's hex board; a responsive
  `MarkerRadius` or min-width scroll container is out of scope here. D9's
  overrides are authored against the reference 1032 px column, not the
  breakpoint.
- [ ] **~900KB eager load** deferred per proposal. Escape hatch if it bites:
  `<link rel="preload" as="image" href="/images/world-map.png">` in
  `App.razor` — SVG `<image>` has no `loading="lazy"`.
