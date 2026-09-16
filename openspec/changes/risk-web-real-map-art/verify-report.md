```yaml
schema: gentle-ai.verify-result/v1
evidence_revision: sha256:f4a1d2a49fefc1e91cd2f852f6f93263066aede148153705175d4d1dbed55408
verdict: pass_with_warnings
blockers: 0
critical_findings: 0
requirements: 10/10
scenarios: 13/13
test_command: dotnet test Risk.sln
test_exit_code: 0
test_output_hash: sha256:555294e24cb88532ad202be0c97a64cf323cf3969bbc3a5a971547cc3b7cd56e
build_command: dotnet build Risk.sln --no-incremental
build_exit_code: 0
build_output_hash: sha256:2418931029ddf35b17082d8a627302954dca8504a3349c9b0e34907fe81ba61a
```

## Verification Report

**Change**: risk-web-real-map-art
**Version**: N/A (no openspec/specs/ prior baseline; new capability)
**Mode**: Strict TDD (repo-wide), Standard Mode for the markup-reconciliation unit (disclosed in apply-progress)

This is the THIRD verify pass on feature/risk-web-real-map-art-pr3-boardsvg, at HEAD 16b9b511df5b954977b2fc78c6c6aa161bbe921c (PR1 6a68487, PR2 faf1d83, PR3 6fedf84, remediation-1 0f5295e, remediation-2 16b9b51). Remediation-2 extended TerritoryLayout.LightOuterRingTerritories from 5 to 8 entries (added Congo, EasternAustralia, Argentina), closing the residual contrast risk (WARNING-4) flagged by the prior (second) verify pass. All evidence below was independently re-derived from source and fresh command execution in this session; the pixel-sampling numbers were re-computed from scratch with a new script, not copied from apply-progress or the prior report.

### Completeness
| Metric | Value |
|--------|-------|
| Tasks total | 14 |
| Tasks complete (checkbox) | 14 |
| Tasks genuinely verified complete | 14 |
| Tasks incomplete/unsubstantiated | 0 |

No tasks.md changes were needed for this remediation (all 14 tasks were already checked complete; the extension is documented via code comment and Engram apply-progress only, matching the instruction recorded in that apply-progress entry). Independently confirmed the code comment in TerritoryLayout.cs accurately narrates the two-phase history (original 5 = proven violation, new 3 = precautionary/borderline).

### Build and Tests Execution
Build: PASSED (clean rebuild, --no-incremental)
```text
dotnet build Risk.sln --no-incremental
Exit code: 0
Exactly 4 warnings (CS8524, non-exhaustive switch), all pre-existing, none in files touched by this change:
  src/Risk.Engine/Setup/GameSetup.cs(52,78)
  src/Risk.Engine/Modes/VictoryRules.cs(26,60)
  src/Risk.Web/Models/PhaseDisplay.cs(20,58)
  src/Risk.Web/Models/GameModeDisplay.cs(21,55)
Zero new warnings.
```

Tests: 948 passed / 0 failed / 0 skipped (fresh run, this session)
```text
dotnet test Risk.sln
Exit code: 0
Risk.Tests:      384/384
Risk.AI.Tests:   206/206
Risk.Web.Tests:  358/358
Total:           948/948, 0 failed, 0 skipped
```
Independently confirms apply-progress claimed 948/948 (up from the second pass's fresh-verified 945/945 by exactly 3 new cases: the 3 new InlineData rows for Congo, EasternAustralia, and Argentina added to the renamed TerritoryLayoutTests.NeedsLightOuterRing_IsTrueOnlyForTheEightBorderlineOrDarkArtTerritories theory). No discrepancy: counted this myself from a fresh dotnet test Risk.sln run in this session, not from apply-progress's claim alone.

Focused re-run of only the touched test class (dotnet test Risk.sln --filter FullyQualifiedName~TerritoryLayoutTests) confirms the theory now carries 11 cases (8 true plus 3 false), all passing.

Coverage: Not available, no coverage tool wired into the build (unchanged from prior passes).

### TDD Compliance
| Check | Result | Details |
|-------|--------|---------|
| TDD Evidence reported | Yes | apply-progress (Engram id 775) reports RED confirmed first, then GREEN (17/17) after the production edit. |
| All tasks have tests | Yes | The only production change (LightOuterRingTerritories extension) is covered by the extended theory; no other production code touched. |
| RED confirmed (tests exist) | Yes | The extended theory exists and was read directly; the 3 new InlineData rows are present exactly as apply-progress describes. |
| GREEN confirmed (tests pass) | Yes | The theory passes in this session's fresh 948/948 run and in a filtered re-run. |
| Triangulation adequate | Yes | 11 cases (8 true across 2 historical tiers, 3 false), real variance across continents. |
| Safety Net for modified files | Yes | TerritoryLayout.cs is a pre-existing, tested file; the extension is additive to an existing HashSet literal, and the full pre-existing suite was re-run and still passes, confirming no regression. |

TDD Compliance: 6/6 checks passed.

### Assertion Quality
Scanned the 3 new InlineData rows added to the NeedsLightOuterRing theory for banned patterns. None found:
- The theory calls real production code (TerritoryLayout.NeedsLightOuterRing) and asserts a concrete boolean per case, not a type-only check.
- No loops over collections (fixed InlineData list).
- No mocks.
- Real variance: 8 true cases (2 historical tiers) and 3 false cases from different continents, not degenerate/single-valued.

Assertion quality: All assertions verify real behavior.

### Independent Re-Measurement of Outer-Ring-vs-Artwork Contrast

Wrote a fresh, independent Python/Pillow pixel-sampling script against the shipped world-map.png (confirmed 1340x876 RGBA, same dimensions as all prior passes; canvas units map 1:1 to image pixels). Sampled at the outer ring's painted radii band (the ring is drawn at r = radius - 1.5 with stroke-width 3; MarkerRadius = 17 for every territory checked here, none carry a RadiusOverrides entry), opaque pixels only (alpha == 255). Two sweeps were run:

1. Full 42-territory sweep (216 samples per territory: 3 radii times 72 angles) applying the SAME ink logic BoardSvg.razor actually uses: MarkerInk.OuterOnDarkArt (#f5ead0) for the 8 LightOuterRingTerritories, MarkerInk.Outer (#2b2118) for the other 34, to confirm the current shipped configuration has zero territories below 3:1 by median.
2. A 4-variant cross-check on the 3 newly-added territories plus several neighbors, using 4 independent sampling parameterizations (single-radius/72-angle, single-radius/144-angle, 3-radii-band/48-angle, and a wider 7-radii-band/36-angle sweep) to test for the same kind of parameterization sensitivity that made the pre-fix Congo/EasternAustralia/Argentina result ambiguous in the prior pass.

Result 1, full sweep, current shipped ink assignment: 0 of 42 territories fail (median below 3:1). The 8 LightOuterRingTerritories and their medians:

| Territory | My median (216-sample) | apply-progress / prior-pass claimed | Result |
|---|---|---|---|
| Kamchatka | 10.62:1 | 10.81:1 | PASS |
| Japan | 4.13:1 | 4.62:1 | PASS |
| Indonesia | 5.31:1 | 6.38:1 | PASS |
| NewGuinea | 5.30:1 | 5.18:1 | PASS |
| Madagascar | 4.77:1 | 4.61:1 | PASS |
| Congo | 4.37:1 | N/A, new this pass | PASS |
| EasternAustralia | 4.37:1 | N/A, new this pass | PASS |
| Argentina | 4.27:1 | N/A, new this pass | PASS |

All 8 clear 3:1 with comfortable margin (lowest median 4.13:1, more than 1:1 above the floor). The 5 originally-fixed territories' numbers are consistent with both prior passes' independent measurements within expected sampling variance.

Result 2, 4-variant stability check on the 3 newly-added territories: unlike the pre-fix state, where these 3 territories' medians straddled 3:1 inconsistently across sampling variants per the prior pass's own WARNING-4 data, all 3 are now stable across every variant tried:

| Territory | single r=17, 72 angles | single r=17, 144 angles | band 16-18, 48 angles x3 | wide band 14-20, 36 angles x7 | Worst single sample any variant |
|---|---|---|---|---|---|
| Congo | 4.43 | 4.43 | 4.40 | 4.33 | 3.08 |
| EasternAustralia | 4.32 | 4.32 | 4.34 | 4.38 | 3.50 |
| Argentina | 4.27 | 4.31 | 4.27 | 4.26 | 3.48 |

Every median in every variant is at least 4.26:1, and even the single worst individual sample across all four variants for each territory stays at least 3.08:1, still above the 3:1 floor. This is qualitatively different from the pre-fix measurements (medians ranging 2.59 to 3.09 across variants, straddling and sometimes falling below the floor). WARNING-4 from the prior pass is genuinely resolved, not merely reasserted.

Scope check, spot-check of the remaining 34 territories for any newly-emergent borderline case: sampled all 34 (not just a handful) against the default MarkerInk.Outer, including territories near continent boundaries and coastlines not individually named in either prior pass's tables (EastAfrica, SouthAfrica, WesternAustralia, Peru, Venezuela, GreatBritain, Iceland, Greenland, Yakutsk, Irkutsk, Mongolia, Siam, MiddleEast, Egypt, and 20 others). Findings:

- 33 of 34 comfortably clear 3:1, medians at least 3.6:1 in the 216-sample sweep.
- EastAfrica is the tightest: median 3.21 to 3.26:1 across all 4 sampling variants (single r=17/72 angles: 3.24; single r=17/144 angles: 3.22; band 16-18/48 angles x3: 3.26; wide band 14-20/36 angles x7: 3.21). This is a genuinely new observation; neither prior pass named EastAfrica specifically. However, unlike the pre-fix Congo/EasternAustralia/Argentina, EastAfrica's median never drops below 3.0 in any of the 4 variants tried (range is 3.21 to 3.26, a 0.05 spread, versus the pre-fix territories' 2.59 to 3.09 spread that crossed the floor). This is closer in character to Peru (3.61 to 3.63, stable) and SouthAfrica (3.44 to 3.67, stable with more spread) than to the pre-fix ambiguous cases. Reported as SUGGESTION-3 below, worth awareness, not a proven or even ambiguous violation.
- No other territory outside the current 8-entry LightOuterRingTerritories set showed a median below 3.4:1 in any variant.

### Spec Compliance Matrix
| Requirement | Scenario | Test / Evidence | Result |
|---|---|---|---|
| Map background layer | Board loads with real artwork | Unchanged from prior passes | COMPLIANT |
| Territory marker placement | All markers render inside their territory | Unchanged from prior passes | COMPLIANT |
| Ownership color and contrast | Owned territory marker color | MarkerInkTests plus TerritoryLayoutTests theory all pass; independent re-measurement confirms all 8 exception-list territories clear 3:1 with stable margin | COMPLIANT |
| Ownership color and contrast | Unclaimed territory marker color | Unchanged from prior passes | COMPLIANT |
| Troop count and reinforcement badge | Troop count visible | Unchanged from prior passes | COMPLIANT |
| Troop count and reinforcement badge | Pending reinforcement badge | Unchanged from prior passes | COMPLIANT |
| Marker size and density exception | Dense cluster marker | Unchanged from prior passes | COMPLIANT |
| Click and right-click interaction contract | Left click raises OnTerritoryClick | Unchanged from prior passes | COMPLIANT |
| Click and right-click interaction contract | Right click raises OnTerritoryRightClick | Unchanged from prior passes | COMPLIANT |
| Selection highlight | Selected territory highlighted | Unchanged from prior passes | COMPLIANT |
| Locked board during pending occupation | Board locked | Unchanged from prior passes | COMPLIANT |
| Sea-route lines only | Only sea routes render | Unchanged from prior passes | COMPLIANT |
| Continent bonus labels | Continent bonus label renders | Unchanged from prior passes | COMPLIANT |

Compliance summary: 13 of 13 scenarios compliant, same count as the prior pass; the Ownership color and contrast scenario residual risk (WARNING-4) is now independently confirmed closed rather than merely disclosed.

### Correctness (Static Evidence): design.md D1-D9 plus both remediations
| Decision | Status | Notes |
|---|---|---|
| D1-D7, D9 | Implemented | Unchanged from prior passes, re-confirmed present in TerritoryLayout.cs |
| D8 (three-circle marker, luminance-threshold inner ring) | Implemented; contrast guarantee now holds at all 8 exception-list positions | MarkerInk.OuterOnDarkArt and TerritoryLayout.NeedsLightOuterRing are unchanged code from the prior pass; only the exception-set membership grew. Re-confirmed the outer-ring stroke attribute in BoardSvg.razor line 45 is genuinely conditional and unchanged: NeedsLightOuterRing selects OuterOnDarkArt or Outer. |
| LightOuterRingTerritories exception set | Implemented, exactly 8 entries, verified by direct source read | Kamchatka, Japan, Indonesia, NewGuinea, Madagascar, Congo, EasternAustralia, Argentina, read directly from source lines 133-143, byte for byte match against the required list, no extras, no omissions. Diff between the two remediation commits for these two files shows this commit touched exactly the doc comment, the HashSet literal, and the theory InlineData plus name, nothing else in either file. |
| MarkerInk.OuterOnDarkArt | Unchanged, still correct | Same value f5ead0 and same sole call site as the prior pass; not touched by this remediation. |

### Coherence (Design)
| Decision/Area | Followed? | Notes |
|---|---|---|
| Deviations 1-2, scope discipline, disclosed apply-time deviations | Yes | Unchanged from prior passes. |
| Diff scope against main | Clean | Same 17-file set as the prior pass re-verification: CLAUDE.md, 5 openspec change docs, BoardSvg.razor and its css, HexGrid.cs deletion, MarkerInk.cs, TerritoryLayout.cs, world-map.png, 5 test files. No file outside this set was touched by remediation-2; the only files with line-count deltas versus the prior pass own stat are TerritoryLayout.cs and TerritoryLayoutTests.cs, exactly as expected for a targeted exception-set extension. |
| Deviation 2, fill isolated from art, ink ring is the one manual check | Design correct, execution now complete for both the original 5 and the 3 precautionary additions | The extension follows the exact same named-exception mechanism design D9 already established for radius overrides, reused for ink color; no new mechanism invented. |
| Testing Strategy table vs code block, 5th vs 4th MarkerPlacementTests fact | Genuine minor doc inconsistency, not missed coverage | Unchanged from prior passes; re-confirmed still present, still low-risk (SUGGESTION-2). |

### Issues Found

CRITICAL: None.

WARNING:
1. (Carried forward, unchanged since pass 1) No standalone TDD Cycle Evidence table in the original PR1-3 apply-progress (Engram id 775 PR1-3 portion); substance is present in prose, table format was only introduced starting with remediation-1 apply-progress entry.
2. (Carried forward, unchanged since pass 1) BoardSvg.razor markup-level requirements (click and right-click wiring, selection highlight, locked-board pointer-events, sea-route line count, continent label positioning) have zero automated component-render test coverage, a pre-existing, repo-wide gap (no bUnit anywhere in this repo), not introduced or worsened by this change or either of its remediations. Several COMPLIANT rows in the Spec Compliance Matrix above rest on static or manual evidence rather than a runtime-executed test for this reason; disclosed consistently across all three verify passes.
3. (Carried forward, unchanged since pass 1) design.md Testing Strategy table names a 5th MarkerPlacementTests fact that the design own code block and the implementation both omit, a design-doc-internal inconsistency, not a missed requirement.

Resolved this pass (was WARNING-4 in the prior pass, no longer applicable): the residual contrast risk at Congo, EasternAustralia, and Argentina against the default outer ink is closed. TerritoryLayout.LightOuterRingTerritories now includes all 3, and independent re-measurement across 4 sampling parameterizations shows stable, comfortable margin (medians 4.26 to 4.43 to 1, worst single sample across any variant 3.08 to 1), a qualitatively different, stable result versus the pre-fix ambiguous 2.59 to 3.09 spread.

SUGGESTION:
1. (Carried forward) Consider bUnit for BoardSvg.razor interaction contract.
2. (Carried forward) Consider adding the missing ContinentLabelAnchors in-canvas-bounds assertion.
3. (NEW) EastAfrica shows the tightest contrast margin among the 34 territories still using the default MarkerInk.Outer ink: median 3.21 to 3.26 to 1 across 4 independent sampling variants, always comfortably at or above 3.0 to 1, unlike the pre-fix borderline set sub-3.0 excursions. Reported for awareness rather than as a finding requiring action. Worth revisiting only if a future retouch of the artwork darkens that region, or as a low-cost precautionary addition to LightOuterRingTerritories if the team wants a larger safety margin than 3.21 to 1 provides.
4. (NEW) The prior pass SUGGESTION-3 (consider live-render sampling if WARNING-4 is acted on) is effectively superseded: the team acted on WARNING-4 by extending the existing named-exception mechanism rather than commissioning a live-render sampling script, and this pass independent 4-variant static-image cross-check found that sufficient to demonstrate a stable, non-ambiguous result for all 3 added territories. No further action needed on this suggestion.

### Verdict
PASS WITH WARNINGS

Reason: 948 of 948 tests pass fresh (up from 945 of 945, plus 3 new cases exactly accounting for the 3 new InlineData rows), clean build with only the same 4 pre-existing warnings, all 14 tasks remain genuinely complete, and the specific residual risk this pass was commissioned to re-verify, the Congo/EasternAustralia/Argentina contrast boundary, is independently confirmed resolved with stable, comfortable margin across 4 different sampling methodologies (a materially stronger result than the pre-fix ambiguous, parameterization-sensitive measurements). Zero CRITICAL findings exist, and none of the 3 remaining WARNING items are new, related to this remediation, or spec-breaking: they are pre-existing, previously-disclosed, repo-wide or documentation-level notes that have been present and non-blocking since the first verify pass. This verdict is kept at PASS WITH WARNINGS rather than a bare PASS specifically because those 3 items remain factually true and unresolved; reporting a clean pass while still listing WARNING items would misrepresent the actual state. This does not change the archival recommendation: this change is archive-ready, with zero blockers.
