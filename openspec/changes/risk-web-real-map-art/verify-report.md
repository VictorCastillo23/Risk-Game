```yaml
schema: gentle-ai.verify-result/v1
evidence_revision: sha256:ed9a7df2a130b7f46bb95c68bb796cbd60cae954002af49aa5195c429e086c8f
verdict: pass_with_warnings
blockers: 0
critical_findings: 0
requirements: 10/10
scenarios: 13/13
test_command: dotnet test Risk.sln
test_exit_code: 0
test_output_hash: sha256:77c12233ebd7c3ae44f789a7890173c1a9b1b6bfaf6c4bbd96a00d3b9806d776
build_command: dotnet build Risk.sln --no-incremental
build_exit_code: 0
build_output_hash: sha256:df24acd3e4fc85f6de6ef2537572035e5ff288e27af19031167762c4fa7c5a26
```

## Verification Report

**Change**: risk-web-real-map-art
**Version**: N/A (no openspec/specs/ prior baseline; new capability)
**Mode**: Strict TDD (repo-wide), Standard Mode for the markup-reconciliation unit (disclosed in apply-progress)

This is a RE-verification after remediation commit 0f5295e on feature/risk-web-real-map-art-pr3-boardsvg (PR1 6a68487 to PR2 faf1d83 to PR3 6fedf84 to remediation 0f5295e), fixing the two CRITICAL findings from the prior verify pass (Engram sdd/risk-web-real-map-art/verify-report id 776; openspec/changes/risk-web-real-map-art/verify-report.md, previously FAIL). All evidence below was independently re-derived from source and fresh command execution in this session, not copied from the apply-progress or the prior report.

### Completeness
| Metric | Value |
|--------|-------|
| Tasks total | 14 |
| Tasks complete (checkbox) | 14 |
| Tasks genuinely verified complete | 14 |
| Tasks incomplete/unsubstantiated | 0 |

Task 5.3's second manual-QA item (previously CRITICAL-2: checked complete with no evidence it was performed) now carries a remediation note in tasks.md naming the exact evidence (this verify lineage's own pixel-sampling, cross-checked by the orchestrator's independent second measurement) and the before/after contrast numbers. Independently confirmed genuine: the note's claimed final numbers (Kamchatka 10.81, Japan 4.62, Indonesia 6.38, NewGuinea 5.18, Madagascar 4.61) match my own independent re-measurement below within expected sampling variance.

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
Zero new warnings.
```

**Tests**: 945 passed / 0 failed / 0 skipped (fresh run, this session)
```text
$ dotnet test Risk.sln
Exit code: 0
Risk.Tests:      384/384
Risk.AI.Tests:   206/206
Risk.Web.Tests:  355/355
Total:           945/945, 0 failed, 0 skipped
```
Independently confirms apply-progress's claimed 945/945 (up from the prior pass's fresh-verified 932/932 by exactly 13 new cases: 5 OuterOnDarkArt_ReachesThreeToOneAgainstMeasuredDarkArt theory cases + 8 NeedsLightOuterRing_IsTrueOnlyForTheFiveDarkArtTerritories theory cases). No discrepancy.

Focused re-run of only this remediation's touched tests:
```text
$ dotnet test Risk.sln --filter "FullyQualifiedName~MarkerInkTests|FullyQualifiedName~TerritoryLayoutTests|FullyQualifiedName~MarkerPlacementTests"
Risk.Web.Tests: 33/33 passed (20 pre-existing + 5 new MarkerInk theory cases + 8 new TerritoryLayout theory cases)
```

**Coverage**: Not available, no coverage tool wired into the build (unchanged from the prior pass).

### TDD Compliance
| Check | Result | Details |
|-------|--------|---------|
| TDD Evidence reported | Yes | apply-progress (Engram id 775) includes a genuine RED/GREEN/REFACTOR table for this remediation's 2 code items + 1 markup item, an improvement over the PR1-3 apply-progress's prose-only evidence (still a carried-forward WARNING for that earlier apply-progress, see WARNING-1). |
| All tasks have tests | Yes for the 2 code changes (MarkerInk.OuterOnDarkArt, TerritoryLayout.NeedsLightOuterRing); N/A for the 1-line BoardSvg.razor conditional stroke change, disclosed as Standard Mode (no bUnit in this repo, pre-existing gap). |
| RED confirmed (tests exist) | Yes | MarkerInkTests.OuterOnDarkArt_ReachesThreeToOneAgainstMeasuredDarkArt and TerritoryLayoutTests.NeedsLightOuterRing_IsTrueOnlyForTheFiveDarkArtTerritories both exist and were read directly; apply-progress cites the exact CS0117 compile-red messages for both. |
| GREEN confirmed (tests pass) | Yes | Both theories pass in this session's fresh 33/33 focused run and the full 945/945 run. |
| Triangulation adequate | Yes | OuterOnDarkArt theory: 5 cases (one per flagged territory, distinct measured hex values). NeedsLightOuterRing theory: 8 cases with real variance (5 true + 3 false from different continents), not single-case. |
| Safety Net for modified files | Yes (disclosed) | MarkerInk.cs/TerritoryLayout.cs extended additively (existing tests re-run, still pass); BoardSvg.razor's one-line change has no safety net beyond full-suite regression and git diff, explicitly disclosed rather than glossed over. |

**TDD Compliance**: 6/6 checks passed.

### Assertion Quality
Scanned the 2 new test additions (MarkerInkTests.OuterOnDarkArt_ReachesThreeToOneAgainstMeasuredDarkArt, TerritoryLayoutTests.NeedsLightOuterRing_IsTrueOnlyForTheFiveDarkArtTerritories) for banned patterns. None found:
- Both call real production code (MarkerInk.ContrastRatio/OuterOnDarkArt, TerritoryLayout.NeedsLightOuterRing) and assert concrete outcomes, not type-only checks.
- No possibly-empty loops (fixed InlineData/MemberData case lists).
- No mocks.
- NeedsLightOuterRing theory has real true/false variance; OuterOnDarkArt theory asserts the same inequality direction across 5 cases with 5 distinct hex inputs, the same pattern already accepted for InnerRingFor_ReachesThreeToOneAgainstEveryFill in the original pass, consistent, not a new concern.

**Assertion quality**: All assertions verify real behavior.

### Independent Re-Measurement of Outer-Ring-vs-Artwork Contrast

Re-ran an independent pixel-sampling script against the shipped world-map.png (1340x876, confirmed unchanged), sampling at the outer ring's true adjacent radius (marker outer edge = MarkerRadius = 17 canvas units; the ring itself is centered at r-1.5=15.5 with stroke-width 3, so its outer painted edge sits at 15.5+1.5=17), opaque pixels only (alpha==255). Two passes were run: an initial single-radius pass at similar sample density to the prior verify pass's method, then a more robust 216-sample pass (3 radii x 72 angles) to reduce single-pixel noise, both against the current ink constants.

**The 5 remediated territories, against the NEW ink (MarkerInk.OuterOnDarkArt = #f5ead0)**, all clear 3:1 with comfortable margin, closely matching apply-progress's own claimed final numbers:

| Territory | My median (robust, 216-sample) | apply-progress claimed | Result |
|---|---|---|---|
| Kamchatka | 10.03:1 | 10.81:1 | PASS |
| Japan | 4.31:1 | 4.62:1 | PASS (worst single-sample 2.21:1, see WARNING-4) |
| Indonesia | 5.27:1 | 6.38:1 | PASS |
| NewGuinea | 5.31:1 | 5.18:1 | PASS |
| Madagascar | 4.77:1 | 4.61:1 | PASS |

CRITICAL-1 (original) is genuinely resolved: every previously-failing territory now measures well above 3:1 by median, independently reconfirmed, not merely trusted from the apply-progress claim.

**Scope check, 40+ territories spot-checked against the OLD default ink** to confirm the fix's 5-territory scope is neither under- nor over-inclusive. A full 42-territory sweep (see WARNING-4) found the territories with medians unambiguously below 3:1 in every sampling variant tried (Kamchatka 1.19-1.41, Indonesia 2.37-2.53, NewGuinea 2.48-2.54, Madagascar 2.77-2.99) are exactly 4 of the 5 fixed territories; Japan measured borderline (2.88-3.24 depending on sample count and radius) rather than unambiguously failing, but the fix does not hurt it. No sampled territory outside the fixed 5 was unambiguously below 3:1 in every run, but 3 territories (Congo, EasternAustralia, Argentina) sit within noise distance of the boundary and are flagged as a residual risk, not a proven failure. See WARNING-4 for the full data and reasoning.

### Spec Compliance Matrix
| Requirement | Scenario | Test / Evidence | Result |
|---|---|---|---|
| Map background layer | Board loads with real artwork | Unchanged from prior pass; static evidence, no component-render test in this repo | COMPLIANT |
| Territory marker placement | All markers render inside their territory | Unchanged from prior pass (count/bounds tested; painted-region placement is manual-QA-only by design) | COMPLIANT |
| Ownership color and contrast | Owned territory marker color | MarkerInkTests.InnerRingFor_ReachesThreeToOneAgainstEveryFill (9/9, inner ring) plus OuterOnDarkArt_ReachesThreeToOneAgainstMeasuredDarkArt (5/5, outer ring at the previously-failing positions) all pass; independent re-measurement in this session confirms all 5 previously-failing territories now clear 3:1 by median with comfortable margin | COMPLIANT (previously FAILING; fix independently confirmed. Residual boundary risk at 3 other territories tracked as WARNING-4, not held against this scenario's compliance) |
| Ownership color and contrast | Unclaimed territory marker color | Unchanged from prior pass | COMPLIANT |
| Troop count and reinforcement badge | Troop count visible | Unchanged from prior pass | COMPLIANT |
| Troop count and reinforcement badge | Pending reinforcement badge | Unchanged from prior pass | COMPLIANT |
| Marker size and density exception | Dense cluster marker | Unchanged from prior pass | COMPLIANT |
| Click and right-click interaction contract | Left click raises OnTerritoryClick | Unchanged from prior pass; git diff main...HEAD on the click/right-click block still byte-identical | COMPLIANT |
| Click and right-click interaction contract | Right click raises OnTerritoryRightClick | Unchanged from prior pass | COMPLIANT |
| Selection highlight | Selected territory highlighted | Unchanged from prior pass | COMPLIANT |
| Locked board during pending occupation | Board locked | Unchanged from prior pass | COMPLIANT |
| Sea-route lines only | Only sea routes render | Unchanged from prior pass | COMPLIANT |
| Continent bonus labels | Continent bonus label renders | Unchanged from prior pass | COMPLIANT |

**Compliance summary**: 13/13 scenarios compliant (up from 12/13; the one previously-FAILING scenario is now independently confirmed fixed).

### Correctness (Static Evidence): design.md D1-D9 plus remediation additions
| Decision | Status | Notes |
|---|---|---|
| D1-D7, D9 | Implemented | Unchanged from prior pass, re-confirmed present in TerritoryLayout.cs |
| D8 (three-circle marker, luminance-threshold inner ring) | Implemented; contrast guarantee now holds at the 5 previously-failing positions | MarkerInk.OuterOnDarkArt plus TerritoryLayout.NeedsLightOuterRing extend D8/D9's own per-territory-exception pattern (a new mechanism was not invented). Independently confirmed the outer ring stroke attribute in BoardSvg.razor is genuinely conditional: TerritoryLayout.NeedsLightOuterRing(id) selects MarkerInk.OuterOnDarkArt or MarkerInk.Outer, not a no-op, not always-true/false. |
| NeedsLightOuterRing exception set | Implemented, exactly 5 entries | LightOuterRingTerritories (private HashSet of TerritoryId) contains exactly Kamchatka, Japan, Indonesia, NewGuinea, Madagascar. Verified by full enumeration: TerritorySeed has exactly 42 entries (9 NA + 4 SA + 7 EU + 6 AF + 12 AS + 4 OC, matching classic Risk), TerritoryId is a readonly record struct(string Value) (value equality, so HashSet.Contains behaves correctly), and none of the other 37 territory names collide with the 5 literal strings in the set, so NeedsLightOuterRing returns true for exactly those 5 and false for all other 37 by construction, not by sampling. The theory test (InlineData, 5 true + 3 false cases) is a spot-check on top of this, not the only evidence. |
| MarkerInk.OuterOnDarkArt | Implemented, real and distinct-purpose | Value #f5ead0, same literal value as InnerOnDark (documented in both files as deliberate reuse of the project's one light-ink constant, not a dead/duplicate color), used only by the new outer-ring conditional in BoardSvg.razor. Not unused/dead. |

### Coherence (Design)
| Decision/Area | Followed? | Notes |
|---|---|---|
| Deviations 1-2, scope discipline, disclosed apply-time deviations | Yes | Unchanged from prior pass. |
| Deviation 2 (fill isolated from art; ink ring is the one manual check) | Design correct, execution now genuinely complete for the 5 previously-failing positions | The manual check the design calls for was performed and evidenced this time (CRITICAL-2 resolved); independent re-derivation with a more robust sampling method surfaces 3 additional borderline territories not covered by this remediation, see WARNING-4. |
| Testing Strategy table vs code block (5th vs 4th MarkerPlacementTests fact) | Genuine minor doc inconsistency, not missed coverage | Unchanged from prior pass; re-confirmed still present, still low-risk (SUGGESTION-2). |

### Issues Found

**CRITICAL**: None. Both CRITICAL findings from the prior pass are resolved:
1. (Resolved) Outer ink ring WCAG 1.4.11 failure at 5/42 territories, independently re-confirmed fixed above.
2. (Resolved) Task 5.3's contrast spot-check evidence gap, tasks.md now carries a specific, falsifiable remediation note naming the evidence and the measured numbers, cross-checked here.

**WARNING**:
1. (Carried forward, unchanged) No standalone TDD Cycle Evidence table in the original PR1-3 apply-progress (Engram id 775's PR1-3 portion), substance present in prose, table format only introduced for this remediation's own 3 items.
2. (Carried forward, unchanged) BoardSvg.razor markup-level requirements have zero automated component-render test coverage, pre-existing, repo-wide gap (no bUnit anywhere in this repo), not introduced or worsened by this change or its remediation.
3. (Carried forward, unchanged) design.md's Testing Strategy table names a 5th MarkerPlacementTests fact that the design's own code block and the implementation both omit, a design-doc-internal inconsistency, not a missed requirement.
4. (NEW) Independent broader re-sampling suggests the remediation's 5-territory scope may be marginally under-inclusive at 3 additional territories, though this is a boundary/ambiguous finding, not a proven failure like CRITICAL-1 was. Method: sampled 216 points per territory (3 radii bands x 72 angles, at the outer ring's true painted edge) against MarkerInk.Outer (#2b2118), opaque pixels only. Results for the 3 territories in question, versus the clearly-passing next tier and the clearly-failing fixed tier for context:

   | Territory | Median | 25th pct | Status |
   |---|---|---|---|
   | Congo | 3.00:1 | 2.51:1 | Right at the boundary |
   | EasternAustralia | 3.03:1 | 2.82:1 | Right at the boundary |
   | Argentina | 3.05:1 | 2.80:1 | Right at the boundary |
   | Madagascar (fixed, for context) | 2.77-2.99:1 across runs | n/a | Clearly below 3:1 in every run |
   | Peru (clearly passing, for context) | 3.60-3.65:1 across runs | n/a | Comfortably above |

   Across three different sampling parameterizations (single-radius at two different sample counts, then 216-sample/3-radii), these 3 territories' medians straddled 3:1 inconsistently (for example, Argentina ranged 2.59-3.09 across runs), unlike the 4 originally-fixed territories whose medians were below 3:1 in every variant tried (never above 2.99). This inconsistency is why this is reported as a WARNING (a genuine residual risk worth a decision) rather than a CRITICAL (a proven violation): the evidence is suggestive, not conclusive, and WCAG 1.4.11 against a hand-painted, non-uniform watercolor background is inherently a judgment call that this repo's own design.md already treats as an approximate "darker than mid-gray" walk-through rather than a rigorous statistic. Recommend a follow-up decision: either extend TerritoryLayout.NeedsLightOuterRing to include Congo, EasternAustralia, and Argentina pre-emptively, or accept the residual risk given the approximate nature of this check. Not blocking archive on its own.
5. (NEW, minor) Even after the fix, Japan's single worst-sampled pixel against the new OuterOnDarkArt ink measured as low as 2.21:1 in the robust pass (median 4.31:1, comfortably passing), the same "worst-case single pixel below 3:1 while median passes" pattern this whole verification lineage has consistently treated as acceptable noise (per the prior pass's own median-based methodology, and design.md's own coarse acceptance rule). Noted for completeness, not elevated.

**SUGGESTION**:
1. (Carried forward) Consider bUnit for BoardSvg.razor's interaction contract.
2. (Carried forward) Consider adding the missing ContinentLabelAnchors in-canvas-bounds assertion.
3. (NEW) If WARNING-4 is acted on, consider whether a live-render sampling script (composing the actual SVG output, not just the base PNG) would settle the boundary cases more definitively than base-PNG pixel sampling, since the ink ring itself is drawn by the browser, not baked into the artwork.

### Verdict
PASS WITH WARNINGS
Reason: 945/945 tests pass fresh, clean build with only the same 4 pre-existing warnings, all 14 tasks are now genuinely complete with real evidence, and both prior CRITICAL findings are independently confirmed resolved, the previously-failing 5 territories all now clear 3:1 against the real artwork with comfortable margin. This change is archive-ready. A new WARNING-level residual finding (3 additional territories sitting within noise distance of the 3:1 boundary against the default ink, not proven to fail) is recorded for a follow-up decision but does not block archive, consistent with an ambiguous/boundary finding rather than a demonstrated spec violation.
