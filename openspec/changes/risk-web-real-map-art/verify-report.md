```yaml
schema: gentle-ai.verify-result/v1
evidence_revision: sha256:39b079acd4dbd10b71bdc1105acbb7edeee24955d7b8132893042684c619a89f
verdict: fail
blockers: 1
critical_findings: 2
requirements: 9/10
scenarios: 12/13
test_command: dotnet test Risk.sln
test_exit_code: 0
test_output_hash: sha256:9550221839acb4782ca14a7b732c8faa374e36079979e5edfadffc5d603be634
build_command: dotnet build Risk.sln --no-incremental
build_exit_code: 0
build_output_hash: sha256:8d7697848406e538d4cb5ca38f0aff97ee896a7991b57da3cce80c6f6a7024de
```

## Verification Report

**Change**: risk-web-real-map-art
**Version**: N/A (no openspec/specs/ prior baseline; new capability)
**Mode**: Strict TDD (repo-wide), Standard Mode for the markup-reconciliation unit (PR3, disclosed in apply-progress)

This report verifies the cumulative final state of the whole change (PR1 6a68487 to PR2 faf1d83 to PR3 6fedf84) on feature/risk-web-real-map-art-pr3-boardsvg, against main. All evidence below was independently re-derived from source and from fresh command runs in this session, not copied from prior sdd-apply reports.

### Completeness
| Metric | Value |
|--------|-------|
| Tasks total | 14 |
| Tasks complete (checkbox) | 14 |
| Tasks genuinely verified complete | 13 |
| Tasks incomplete/unsubstantiated | 1 (5.3, second manual-QA item, see CRITICAL-2) |

### Build and Tests Execution
**Build**: PASSED (clean rebuild, --no-incremental)
```text
$ dotnet build Risk.sln --no-incremental
Exit code: 0
Exactly 4 warnings (CS8524, non-exhaustive switch), all pre-existing, none in files touched by this change:
  src/Risk.Engine/Setup/GameSetup.cs(52,78)
  src/Risk.Engine/Modes/VictoryRules.cs(26,60)
  src/Risk.Web/Models/PhaseDisplay.cs(20,58)
  src/Risk.Web/Models/GameModeDisplay.cs(21,55)
Zero new warnings introduced by this change. Confirms apply-progress claim.
```

**Tests**: 932 passed / 0 failed / 0 skipped (fresh run, this session)
```text
$ dotnet test Risk.sln
Exit code: 0
Risk.Tests:      384/384
Risk.AI.Tests:   206/206
Risk.Web.Tests:  342/342
Total:           932/932, 0 failed, 0 skipped
```
This independently reconfirms PR3 own reported final number (932/932). The earlier PR1 count of 963 is explained and consistent: PR2 deleted HexGridTests.cs and HexAdjacencyRegressionTests.cs and trimmed TerritoryLayoutTests.cs (net -31 tests: 963 to 932), an intentional test-suite restructuring documented in design.md Testing Strategy table, not an unexplained drop. No discrepancy found across the three reported counts.

Focused re-run of only the tests this change added or modified, isolated from the rest of the suite:
```text
$ dotnet test Risk.sln --filter "FullyQualifiedName~MarkerPlacementTests|FullyQualifiedName~MarkerInkTests|FullyQualifiedName~TerritoryLayoutTests"
Risk.Web.Tests: 20/20 passed (6 TerritoryLayoutTests + 4 MarkerPlacementTests + 10 MarkerInkTests)
```

**Coverage**: Not available, no coverage tool configured in this repo (coverlet.collector is referenced but no coverage run or threshold is wired into the build). Not a failure per the skill graceful-handling rule.

### TDD Compliance
| Check | Result | Details |
|-------|--------|---------|
| TDD Evidence reported | Partial | apply-progress (Engram id 775) narrates RED/GREEN per task in prose and tasks.md itself labels each task RED or GREEN, but there is no standalone TDD Cycle Evidence table in the mandated column format (RED/GREEN/TRIANGULATE/SAFETY NET/REFACTOR). |
| All tasks have tests | Yes | Phases 1-3 (asset, MarkerInk, TerritoryLayout) each produced a paired test file (MarkerInkTests.cs, TerritoryLayoutTests.cs rewrite, MarkerPlacementTests.cs). Phase 4 (BoardSvg.razor rewrite) has no new test file, disclosed and reasoned in apply-progress as Standard Mode: it reconciles two already-TDD-covered files with their Razor consumer with no new pure-logic unit, and this repo has no bUnit or component-render harness for any Razor component (verified: zero BoardSvg references anywhere under tests/, and Risk.Web.Tests.csproj has no bUnit package). This is a pre-existing, repo-wide test-architecture boundary, not a gap this change introduced. |
| RED confirmed (tests exist) | Yes | MarkerInkTests.cs, TerritoryLayoutTests.cs, MarkerPlacementTests.cs all exist and were read directly. |
| GREEN confirmed (tests pass) | Yes | All 20 focused tests plus full 932/932 suite pass on fresh execution in this session. |
| Triangulation adequate | Yes | MarkerInkTests AllFills theory covers 9 distinct fills (6 PlayerPalette.Swatches plus UnclaimedColor plus UnknownOwnerColor plus NeutralColor) against one assertion, real variance, not single-case. MarkerPlacementTests has 4 independent facts covering 4 distinct geometric invariants. |
| Safety Net for modified files | Yes | TerritoryLayout.cs and TerritoryLayoutTests.cs were modified together (design.md testing-strategy table shows a fact-by-fact keep/delete/move decision), not blind-rewritten. |

**TDD Compliance**: 5/6 checks fully passed, 1 partial (missing table format, substance present in prose).

### Assertion Quality
Scanned MarkerInkTests.cs, TerritoryLayoutTests.cs, MarkerPlacementTests.cs (all new or modified by this change) for banned patterns (tautologies, ghost loops over possibly-empty collections, type-only assertions, smoke-test-only, mock-heavy). None found:
- All loops iterate fixed, always-populated collections (TerritoryLayout.Coordinates, 42 entries; WorldMap.Territories, 42 entries), not possibly-empty.
- Every assertion calls real production code (MarkerInk.ContrastRatio, TerritoryLayout.RadiusOf, distance/centroid math) and asserts a concrete numeric or structural outcome, not a type-only check.
- Zero mocks in any of the three files.

**Assertion quality**: All assertions verify real behavior

### Test Layer Distribution
| Layer | Tests | Files | Tools |
|-------|-------|-------|-------|
| Unit | 20 | 3 | xUnit |
| Integration | 0 | 0 | not installed (no bUnit in this repo) |
| E2E | 0 | 0 | not installed |
| Total | 20 | 3 | |

BoardSvg.razor own markup (image layer, click/right-click wiring, sea-route filtering, selection highlight, locked-pointer-events, continent labels) has zero direct component-render test coverage. This is true of every Razor component in this repo, before and after this change, not a regression. SUGGESTION: a future change could introduce bUnit for board-critical interaction contracts; out of scope to require here.

### Spec Compliance Matrix
| Requirement | Scenario | Test / Evidence | Result |
|---|---|---|---|
| Map background layer | Board loads with real artwork | Static: BoardSvg.razor image tag uses TerritoryLayout.CanvasWidth/Height (1340/876), svg viewBox is 0 0 1340 876; PNG measured 1340x876 via System.Drawing in this session | COMPLIANT (static evidence; no component-render test exists in this repo) |
| Territory marker placement | All markers render inside their territory | TerritoryLayoutTests.Coordinates_ContainsEveryWorldMapTerritory_ExactlyOnce (42/42) plus MarkerPlacementTests.Coordinates_EveryMarkerFitsFullyInsideTheCanvas, both pass. Falls-within-painted-region is manual-QA only per design.md, claimed done during PR2 authoring, not independently re-verifiable by source inspection | COMPLIANT (count/bounds tested; painted-region placement is manual-QA-only by design, unverified by me) |
| Ownership color and contrast - Owned territory marker color | Fill matches owner color AND at least 3:1 contrast vs adjacent art | MarkerInkTests.InnerRingFor_ReachesThreeToOneAgainstEveryFill passes (9/9 fills, inner-ring-vs-fill only). Independent pixel sampling of world-map.png in this session (median contrast, 24 angular samples per territory at the ink ring true adjacent radius, opaque samples only) found the outer ink ring #2b2118, the ring design D8 states is art-facing, falls below 3:1 at 5 of 42 territories: Kamchatka (median 1.33:1), Japan (2.70:1), Indonesia (2.80:1), New Guinea (2.56:1), Madagascar (2.73:1); worst single-sample as low as 1.16:1 (Kamchatka) | FAILING, see CRITICAL-1 |
| Ownership color and contrast - Unclaimed territory marker color | Unclaimed renders in a distinct non-player color | BoardSvg.razor: territory.Owner conditional selects BoardColors.OwnerColor or BoardColors.UnclaimedColor, a distinct constant | COMPLIANT (the contrast defect above is shared by the ink ring regardless of ownership, but this scenario own literal text only requires color distinctness, which holds) |
| Troop count and reinforcement badge | Troop count visible | troop-count text element renders territory.Troops with paint-order stroke fill black halo for legibility (CSS unchanged) | COMPLIANT (static evidence) |
| Troop count and reinforcement badge | Pending reinforcement badge | pending-badge text element toggles display:none based on pending count, distinct green fill/class | COMPLIANT (static evidence) |
| Marker size and density exception | Dense cluster marker | RadiusOverrides ships empty (all 42 at default MarkerRadius=17, about 26 CSS px, clears the 24px floor); MarkerPlacementTests.RadiusOverrides_AreKnownTerritoriesWithinTheAllowedBand plus Coordinates_NoTwoMarkersOverlap (861 pairs) both pass, so no territory needed the exception | COMPLIANT (exception mechanism exists and is tested; unused because unneeded, per design D9 own prediction) |
| Click and right-click interaction contract | Left/right click raise callbacks | git diff main...HEAD on BoardSvg.razor shows the onclick/oncontextmenu InvokeAsync lines are byte-identical, confirmed by diff (no changed lines in that block) | COMPLIANT (diff-verified identical; no component-render test exists in this repo) |
| Selection highlight | Selected territory highlighted | CSS rule targets .board-territory-selected .board-marker-ring-outer only (stroke color, width, drop-shadow), per design D8 own warning that inner ring/fill stay untouched | COMPLIANT (static evidence) |
| Locked board during pending occupation | Board locked | pointer-events:none style on the svg root is unchanged by diff | COMPLIANT (diff-verified identical) |
| Sea-route lines only | Only sea routes render | BoardEdges.UniqueEdges filtered by IsSeaRoute; SeaRoutes hardcoded set has exactly 5 entries (Alaska-Kamchatka, Greenland-Iceland, Brazil-NorthAfrica, WesternEurope-NorthAfrica, Siam-Indonesia); BoardEdges.cs untouched by this change | COMPLIANT (static evidence) |
| Continent bonus labels | Continent bonus label renders | continent label group wraps a bare g element around a text element with no rect in the continents group; anchor comes from TerritoryLayout.ContinentLabelAnchors; MarkerPlacementTests.ContinentLabelAnchors_EachSitsNearItsOwnContinent passes (nearest-centroid plus spread check, 6/6) | COMPLIANT (test plus static evidence) |

**Compliance summary**: 12/13 scenarios compliant, 1 FAILING (confirmed by independent measurement, not merely untested)

### Correctness (Static Evidence) - design.md D1-D9
| Decision | Status | Notes |
|---|---|---|
| D1 (TerritoryLayout API surface) | Implemented | CanvasWidth=1340, CanvasHeight=876, MarkerRadius=17, RadiusOf, Coordinates (42), ContinentOf, ContinentLabelAnchors (6) all present; Polygons, PolygonPointsAttr, ContinentBounds, HexSize, ContinentOrigins confirmed absent repo-wide (grep, zero matches) |
| D2 (MarkerRadius=17) | Implemented | Exact constant confirmed in TerritoryLayout.cs |
| D3 (seed keeps ContinentId column) | Implemented | TerritorySeed is a (Name, ContinentId, X, Y) array, grouped per continent in source |
| D4 (constant outer ink, continent color moves to label) | Implemented | MarkerInk.Outer is the constant #2b2118; continent label text fill comes from ContinentPalette.ColorOf |
| D5 (href is root-relative /images/world-map.png) | Implemented | Exact match in BoardSvg.razor |
| D6 (filter sea routes at call site, no new BoardEdges API) | Implemented | BoardEdges.cs has zero diff vs main; filtering done via a Where clause at the call site |
| D7 (integral pixel literals) | Implemented | All 42 TerritorySeed plus 6 ContinentLabelSeed coordinates are integer literals |
| D8 (three-circle marker, luminance-threshold inner ring) | Implemented but contrast guarantee empirically fails at 5/42 positions | MarkerInk.cs and MarkerInkTests.cs faithfully implement the 0.18-threshold rule and pass for the inner-ring-vs-fill pair; the outer-ring-vs-artwork pair (the one D8 own text calls the one manual check) was independently measured in this session and fails at Kamchatka, Japan, Indonesia, New Guinea, Madagascar. See CRITICAL-1. |
| D9 (RadiusOf/RadiusOverrides, band 13 to 17) | Implemented | MinRadius=13 constant present; RadiusOverrides ships empty as specified; RadiusOverrides_AreKnownTerritoriesWithinTheAllowedBand passes |

### Coherence (Design)
| Decision/Area | Followed? | Notes |
|---|---|---|
| Deviation 1 (continent label anchors, not bbox) | Yes | ContinentLabelAnchors_EachSitsNearItsOwnContinent passes; spec.md own wording already reflects this deviation, correctly |
| Deviation 2 (fill isolated from art; ink ring is the one manual check) | Design correct, execution incomplete | The design own reasoning is sound; the manual check it calls for was apparently not actually performed (see CRITICAL-2), and when performed independently here, it fails |
| Testing Strategy table vs code block (5th vs 4th MarkerPlacementTests fact) | Genuine minor doc inconsistency, not missed coverage | design.md table row mentions a 5th fact, ContinentLabelAnchors_ContainsAllSixContinents_InsideTheCanvas, that design own concrete code block (4 facts) and the actual implementation both omit. ContinentLabelAnchors_EachSitsNearItsOwnContinent implicitly covers contains-all-six (an indexer miss on any of the 6 continent ids would throw KeyNotFoundException and fail the test), but does not assert the anchor lies within canvas bounds, a real if low-risk coverage gap (all 6 hardcoded anchor values are comfortably in-bounds by inspection, so this is a SUGGESTION, not CRITICAL) |
| Two disclosed apply-time deviations (RZ1023 g wrapper; empty .board-marker-fill CSS rule) | Confirmed non-functional | Both independently re-verified: the bare g wrapper is empty markup with no visual or semantic effect; .board-marker-fill carries no CSS-settable property (fill comes from the C# fill attribute only, no hover/selection target on it), and no rule is missing that any other class provides |
| Scope discipline (proposal explicit out-of-scope list) | Yes | git diff main...HEAD --stat touches only: CLAUDE.md, openspec/changes/risk-web-real-map-art/*, BoardSvg.razor(.css), HexGrid.cs (deleted), MarkerInk.cs (new), TerritoryLayout.cs, world-map.png, and the 5 test files in tests/Risk.Web.Tests/Models/. Zero touches to Risk.Domain, Risk.Engine, Risk.AI, Risk.Tests, Game.razor, the turn-phase panels, GameSessionService.cs, BoardSelection.cs, BoardColors.cs, BoardEdges.cs, ContinentPalette.cs, ContinentDisplay.cs |

### Issues Found

**CRITICAL**:
1. Outer ink ring fails WCAG 1.4.11 (at least 3:1) against the real artwork at 5 of 42 marker positions, violating spec Requirement "Ownership color and contrast" (a MUST clause) and contradicting design D8 own stated guarantee. Independently measured in this session by sampling world-map.png pixels at the ink ring true adjacent radius (24 angular samples per territory, opaque-only, median-based to avoid single-pixel noise):
   - Kamchatka: median 1.33:1 (worst 1.16:1)
   - Japan: median 2.70:1 (worst 1.18:1)
   - Indonesia: median 2.80:1 (worst 1.75:1)
   - New Guinea: median 2.56:1 (worst 1.78:1)
   - Madagascar: median 2.73:1 (worst 1.95:1)
   All five are exactly the category design.md own task 5.3 anticipated as the risk case (deep-ocean or dark coastal wash) - small islands or peninsulas surrounded by dark coastal/ocean shading. This affects the constant outer ring only (shared by every marker regardless of owner), not the owner-color fill itself, so ownership stays legible; the defect is narrowly scoped to WCAG non-text-contrast at these 5 positions, not a functional or gameplay break. Recommend: either move or enlarge markers at these 5 positions away from the dark art, or extend the RadiusOverrides-style mechanism to allow a per-territory outer-ring color override, then re-run this exact contrast check.
2. Task 5.3 second manual-QA item (spot-check outer ink ring vs art at all 42 positions for backgrounds darker than mid-gray) is marked done in tasks.md but the available evidence does not show it was actually performed. apply-progress (Engram id 775) explicitly attributes only the first manual-QA item (marker-inside-territory placement) to PR2 authoring pass (crop and overlay and re-crop against the actual artwork); it never claims the ink-ring-vs-art contrast spot-check was done, in PR2 or PR3, and explicitly disclaims visual re-verification for PR3 (no browser or screenshot capability). Independent execution of that exact check in this session (see CRITICAL-1) found real failures, which a genuine spot-check should have caught before the box was checked. This is a task-completeness misrepresentation, not merely a missing nice-to-have.

**WARNING**:
1. No standalone TDD Cycle Evidence table (RED/GREEN/TRIANGULATE/SAFETY NET/REFACTOR columns) exists in apply-progress, though the substance (RED-then-GREEN per task, focused-then-full test runs) is present in prose and independently confirmed by re-running the tests in this session.
2. BoardSvg.razor markup-level requirements (image background, click/right-click wiring, selection highlight, locked pointer-events, sea-route rendering, continent labels - 8 of 13 scenarios) have zero automated runtime/component-render test coverage. This is a pre-existing, repo-wide gap (no bUnit anywhere in Risk.Web.Tests, before or after this change) rather than something this change introduced or regressed; verified correct here only via source inspection and git-diff comparison against the prior committed version, not via a passing test.
3. design.md Testing Strategy table names a 5th MarkerPlacementTests fact (ContinentLabelAnchors_ContainsAllSixContinents_InsideTheCanvas) that design own concrete code block and the actual implementation both omit (4 facts, not 5), a genuine design-doc internal inconsistency (table vs code block), not a missed spec requirement. Real coverage gap: no test explicitly asserts continent label anchors fall within canvas bounds (values are correct by inspection, just unasserted).

**SUGGESTION**:
1. Consider bUnit (or equivalent) for BoardSvg.razor interaction contract (click/right-click callbacks, locked state, selection class) given how central this component is to every play mode; would close WARNING-2 durably rather than relying on git-diff comparison at each future change.
2. Consider adding the missing ContinentLabelAnchors in-canvas-bounds assertion to MarkerPlacementTests.cs to resolve the design-doc table/code-block inconsistency with an actual test rather than only a doc fix.

### Verdict
FAIL
Reason: 932/932 tests pass, build is clean, scope discipline holds, and 8 of 9 architecture decisions (D1-D9) are implemented exactly as designed, but independent pixel-level measurement of the shipped artwork found the outer marker ring genuinely fails the spec MUST-level at-least-3:1 WCAG 1.4.11 contrast requirement at 5 of 42 territories (Kamchatka, Japan, Indonesia, New Guinea, Madagascar), and the task list own manual-QA checkbox meant to catch exactly this appears to have been marked complete without actually being performed. This blocks archive-readiness; recommend routing back to sdd-apply for a targeted fix at these 5 positions plus a genuine execution (or automation) of task 5.3 contrast spot-check, then re-verify.
