# OpenVisionLab 3D EXE Recipe Authoring UX Study

Date: 2026-08-15
Status: Current product-direction evidence

`PL-0009` was subsequently completed on 2026-08-15. The ten recipe files
remain unchanged reproduction evidence; the current source and Release EXE
now prevent the reproduced incompatible Add path and expose direct legacy
route repair without execution.

`PL-0010` contextual dual-ROI setup and `PL-0011` recipe-health navigation
were subsequently completed on the same date. The original observation and
scores below remain the study baseline; the post-`PL-0011` reassessment and
completion records distinguish the current state.

## Outcome

The current Release EXE was used directly to save and reopen ten recipe files
from the bundled Thickness Coupon C3D. The set covers the current Thickness,
Filter, Warpage, Plane Flatness, Gap / Flush, Volume, Cross-section
Dimensions, Point Pair Dimensions, Completeness Grid, feature/affine,
Re-grid, and review tools.

This was an authoring and workflow study, not calibrated metrology or a claim
that every saved chain is executable. Only the bundled eight-pad Thickness
baseline reopened as a ready single-task recipe. The study deliberately
preserves the pending or incompatible states produced by the EXE because they
identify the highest-value product corrections.

## Scope And Evidence

- EXE:
  `src/OpenVisionLab.ThreeD.Shell/bin/Release/net10.0-windows10.0.19041/OpenVisionLab.ThreeD.Shell.exe`
- Sample:
  `3D/Samples/ThicknessCouponV1/thickness-coupon-v1.C3D`
- Sample identity: `1280 x 840`, `84.5%` valid, `15.5%` missing,
  `raw-height`, frame `frame.c3d-grid-index`.
- Physical artifact root:
  `D:/OpenVisionLab-TestData/OpenVisionLab-3D-Studio/artifacts/current/20260815-exe-recipe-authoring-study/recipes/`
- Monitor rule: the EXE was placed on leftmost `DISPLAY2`, bounds
  `[-1920,365,1920,1080]`, and the application rectangle intersected that
  monitor.
- The ten files all parse as JSON and were reopened through the EXE after
  saving. File structure verification found `10/10` JSON files, `90` total
  steps, and the exact tool chains listed below.

No source code, algorithm, recipe JSON, or test fixture was edited directly to
produce the recipes. All recipe creation, opening, step addition, Save, and
Save As operations were performed through the running EXE. Shell inspection
afterward was read-only.

## Recipe Set

| # | File | Saved chain | Selections | EXE result and purpose |
| --- | --- | --- | ---: | --- |
| 01 | `01-filter-thickness.ov3d-recipe.json` | Filter -> Thickness | 0 | Saved and reopened with two execution requirements. Thickness was automatically routed to the Filter output, but Reference/Measurement ROI capture was unavailable. This is the minimal first-use failure case. |
| 02 | `02-thickness-baseline.ov3d-recipe.json` | Thickness x 8 | 16 | Bundled eight-pad sample; reopened `Valid | Saved`. This is the positive baseline. |
| 03 | `03-thickness-warpage.ov3d-recipe.json` | Thickness x 8 -> Warpage | 16 | Saved and reopened. The new Warpage input was automatically routed to `derived.pad-thickness.08`, so two requirements remained. |
| 04 | `04-thickness-plane-flatness.ov3d-recipe.json` | Thickness x 8 -> Plane Flatness | 16 | Saved and reopened. The new step received the prior MeasurementResult instead of a HeightField. |
| 05 | `05-thickness-gap-flush.ov3d-recipe.json` | Thickness x 8 -> Gap / Flush | 16 | Saved and reopened with the same incompatible automatic route and missing dual-ROI setup. |
| 06 | `06-thickness-volume.ov3d-recipe.json` | Thickness x 8 -> Volume | 16 | Saved and reopened with the same incompatible automatic route and missing dual-ROI setup. |
| 07 | `07-thickness-cross-section.ov3d-recipe.json` | Thickness x 8 -> Cross-section Dimensions | 16 | Saved and reopened with an incompatible automatic input and missing line/profile teaching. |
| 08 | `08-thickness-point-pair.ov3d-recipe.json` | Thickness x 8 -> Point Pair Dimensions | 16 | Saved and reopened with an incompatible automatic input and missing point teaching. |
| 09 | `09-thickness-completeness-grid.ov3d-recipe.json` | Thickness x 8 -> Completeness Grid | 16 | Saved and reopened with an incompatible automatic input and missing grid/ROI teaching. |
| 10 | `10-affine-feature-measure-review-chain.ov3d-recipe.json` | Filter -> Edge -> Line Fit -> Edge -> Line Fit -> Line Intersection -> Edge -> Line Fit -> Edge -> Line Fit -> Line Intersection -> Landmark Correspondence -> XYZ Affine -> Re-grid -> Thickness -> Warpage -> Overlay Review | 2 | Seventeen-step template saved and reopened. It proves long-chain persistence and exposes Compact overview limits; most feature steps remain `Taught / pending`. |

The legacy examples under `recipes/*.recipe.json` document algorithms but are
not the same current Workbench recipe format. The current EXE file picker and
Save As workflow use `.ov3d-recipe.json`; therefore they were not presented as
ten directly runnable modern recipes.

## What Worked Well

1. The empty Viewer gives one clear `Open 3D Map` action and explains why a
   source is required.
2. Source identity, raw-height/frame, quality percentage, alignment state, and
   recipe state remain visible in the header.
3. Adding a step does not run inspection. Save/reopen also did not trigger
   Preview, Publish, or Run.
4. The bundled eight-pad Thickness recipe preserves all sixteen ROI
   selections and reopens without identity loss.
5. The Tools surface separates `Compatible next tools` from `All tools`, and
   tooltips state that Add does not execute inspection.
6. The Affine template preserves seventeen explicit, independently named
   steps and source/result identities.

These strengths fit the approved product principle: keep the operator's
current state and next action visible while preserving explicit execution and
evidence boundaries.

## Prioritized Findings

### P0 - Prevent incompatible step insertion and automatic routing

`All tools` allowed Warpage, Plane Flatness, Gap / Flush, Volume,
Cross-section Dimensions, Point Pair Dimensions, and Completeness Grid to be
added after the last Thickness result. Each new step was automatically given
`derived.pad-thickness.08`, a MeasurementResult, although the tool requires a
HeightField. The recipe could then be saved and reopened.

A related sequencing gap appeared in the from-scratch Filter -> Thickness
case: the typed route was addable, but ROI teaching could not start for the
not-yet-produced Filter output. The user sees an execution-requirement count
only after the step has already been inserted and after moving to another
panel.

Required correction:

- refuse Add when no compatible input route exists, or require the user to
  choose a valid route before insertion;
- show the proposed input and output types on the Add action;
- never silently route a HeightField consumer to a MeasurementResult;
- make a repair action jump directly to the invalid input; and
- preserve the no-auto-execution contract.

Durable issue: `PL-0009`.

Current correction, verified 2026-08-15:

- the Add boundary now resolves the newest compatible typed artifact instead
  of blindly using the last result;
- generic HeightField measurements fall back to the identified source rather
  than consuming a MeasurementResult;
- tools that require `TransformedHeightField` keep Add disabled until that
  contract exists;
- the proposed `input [contract] -> tool -> output [contract]` route is
  visible before Add;
- saved legacy mismatches remain loadable as repairable drafts and show a
  direct `Repair route` / `경로 수정` card on the selected step; and
- Add and repair do not invoke Preview, Publish, Run, or save.

Current evidence is under
`D:/OpenVisionLab-TestData/OpenVisionLab-3D-Studio/artifacts/current/20260815-pl0009-compatible-tool-routing/`.
Focused Tool Recipe teaching passes `42/42`, Height Measurement Workbench
passes `54/54`, affected regressions pass, and the full Release build has
zero warnings and zero errors. At that checkpoint the typed-routing defect
closed while the multi-pane setup effort remained `PL-0010`; the later
completion record below closes that follow-up.

### P1 - Keep add, configure, teach, and repair in one contextual path

The operator selects a tool in `Tools`, then must switch to `Selected`, expand
Inputs or ROI/Regions, sometimes open Advanced input routing, change the
Viewer to Top, scroll the narrow inspector, choose Draw ROI, teach the
selection, review it, and Apply. The missing requirement is not presented at
the Add location, and the relevant controls can be below the Compact fold.

The observed first-use Filter -> Thickness route required at least fourteen
explicit actions before the operator reached the blocked ROI state. This is a
workflow count from the observed route, not instrumented telemetry.

Required correction: after Add, keep a compact step setup card visible with
the selected input, missing prerequisites, one primary next action, and a
direct return to the tool catalog. Persist only confirmed reusable setup at
recipe/project scope; restoration must remain visible, editable, and
non-executing.

Durable issue: `PL-0010`.

Implemented 2026-08-15: Add now opens `Selected Tool` in Compact, while a
single dual-ROI setup card keeps the compatible input, both ROI lifecycle
states, parameter state, the selected-step readiness reason, one primary next
action, and a direct `Tools` return together. Reference and Measurement ROI
teaching advances in the card through `Missing -> Drawing -> Review ->
Applied`; setup and navigation remain non-executing, while Preview stays an
explicit action. Existing recipe-scoped ROI and parameter persistence remains
authoritative; save/reopen restores it without execution, and New Recipe
returns to empty safe defaults. This closes `PL-0010` for the approved dual-ROI
scope; `PL-0011` owns recipe-wide problem navigation when another step blocks
the selected step.

### P1 - Add a recipe-level health summary and issue navigation

At Compact width the seventeen-step Affine recipe displayed only the first
ten rows. Several names and `Taught / pending` states were truncated, and the
operator had no single summary of which steps were Ready, Pending, invalidly
routed, or missing selections. The global badge reports a requirement count
but not its locations.

Required correction: add a non-executing recipe health summary with counts by
state and Previous/Next issue navigation. Selecting an issue should reveal
the owning step, input/parameter/ROI requirement, and Viewer evidence without
changing recipe or result state.

Durable issue: `PL-0011`.

Implemented 2026-08-15: Flow now classifies every step exactly once as Ready,
Needs input, Needs selection, Needs parameters, Stale Preview, or Published.
The localized health card shows exact counts and the selected owning step and
requirement. Non-wrapping Previous/Next selects and scrolls the exact row and
is disabled when execution or an in-progress draft makes navigation unsafe.
Focused regression proves that navigation does not change recipe, source,
result, dirty, execution-log, layer, or active-input state. Current Release
Wide and Compact English/Korean evidence proves that the seventeenth step and
actions are reachable without clipped required text and that the held
pointer-down state remains themed. This closes `PL-0011`.

### P2 - Remove stale Tool Library search context

The search term used for one algorithm remained after opening another recipe.
`Thickness`, `Warpage`, `Plane Flatness`, and later terms each hid the rest of
the catalog until the field was manually selected and replaced. The active
filter is easy to miss because the compatible list remains visible above it.

Required correction: clear the search on recipe open and after successful Add,
or make retained search an explicit, visible project-scoped preference with a
one-click clear action. Restoring it must not add or execute a tool.

Durable issue: `PL-0012`.

### P2 - Consolidate new-recipe identity and source setup

New Recipe opened Save As before the source or task was selected, used the
generic `new-inspection` filename, then closed Recipe Center and required a
second file dialog for C3D. The Recipe Center was `720 x 496`, while its owned
system dialog was about `950 x 571`, extending outside the parent. The output
folder was not the first useful context during the initial attempt.

Required correction: one first-use setup surface should collect recipe name,
recipe folder, sample/source, and optional task starter. The final Create
action may still perform the save, but merely restoring settings must not
load, add, Preview, Publish, or Run. Keep the selected values visible and
editable and provide Reset to defaults.

Durable issue: `PL-0013`.

## Useful Product Additions

1. Task starters for Thickness, Warpage, Plane Flatness, Gap / Flush, Volume,
   Cross-section, Point Pair, Completeness Grid, and Surface Match. A starter
   should create only compatible typed inputs and show its remaining teaching
   requirements before Create.
2. `Duplicate as variant` for a selected step or recipe. It should retain
   compatible source/ROI identity, assign new stable step/result identities,
   and require explicit review before Publish or Run.
3. A proposed-route preview on every Add action: `source/output -> tool ->
   result`, with incompatible alternatives explained instead of inserted.
4. Recipe health chips for `Ready`, `Needs input`, `Needs selection`, `Needs
   parameters`, `Preview stale`, and `Published`, plus jump-to-issue.
5. Project-scoped remembered recipe folder and last confirmed source/task
   starter. Values must be validated on reopen, shown to the user, and never
   shared silently across unrelated projects.

## Independent Product Evaluation

The approved task-centered direction remains correct at the principle level:
current-task clarity, linked configuration/Viewer/evidence, progressive
disclosure, purposeful icons, collapsible support panes, and explicit status
and next action. The study shows that the visual shell already expresses much
of that direction, but multi-tool authoring still exposes internal panel and
type boundaries to the operator.

The next improvement should extend the OpenVisionLab-specific typed authoring
contract: compatible route first, one visible next action, recipe health at a
glance, and explicit Preview/Publish/Run.

## Evidence-Based Maturity

| Area | Score | Evidence |
| --- | ---: | --- |
| Deterministic lifecycle and persistence | 8.0/10 | Explicit non-running Add/Save; 10/10 save/reopen; stable source and selection identities. |
| Single-task sample readiness | 7.5/10 | Eight-pad Thickness baseline is complete, visible, and reusable. |
| Multi-tool typed authoring | 7.0/10 | Current Add resolves a compatible typed artifact, exposes the proposed route, blocks unavailable transformed-only tools, and gives legacy mismatches a direct non-executing repair action. |
| First-use efficiency | 6.5/10 | Add now opens one contextual dual-ROI setup path, but recipe identity/source creation and save remain split. |
| Compact long-chain overview | 4.5/10 | Seventeen-step chain persists, but only part is visible and requirement locations are not summarized. |
| Current operator authoring readiness | 7.0/10 | Typed insertion and dual-ROI setup are coherent and non-executing; recipe-wide health, first-use setup, and long-chain navigation remain incomplete. |

The scores are an evidence-bounded product judgment, not instrumented
telemetry. They apply only to the observed file-first recipe-authoring
workflow. They do not evaluate calibrated physical metrology, production I/O,
camera, PLC, robot, cloud, account, or deployment capabilities; those remain
outside the product scope.

### Post-PL-0011 Reassessment

| Area | Current score | Change evidence |
| --- | ---: | --- |
| Compact long-chain overview | 7.5/10 | Exact six-state counts, direct owning-step/requirement detail, non-wrapping navigation, and automatic reveal of step 17 now pass in current Release Compact English/Korean. |
| Current operator authoring readiness | 7.4/10 | Compatible insertion, contextual dual-ROI setup, and recipe-wide health are coherent and non-executing; first-use creation, stale search context, and the language popup remain open. |

These current scores supersede only the corresponding `4.5/10` and `7.0/10`
study-baseline rows. They remain an evidence-bounded product judgment, not
telemetry or a release, usability, or metrology acceptance claim.

## Recommended Execution Order

1. `PL-0013` first-use recipe/source/task setup | Recommended model:
   `gpt-5.6-sol` | Reasoning effort: `medium`.
2. `PL-0012` Tool Library search reset/visibility | Recommended model:
   `gpt-5.6-terra` | Reasoning effort: `low`.
3. `PL-0014` Studio language-popup theme and bounds | Recommended model:
   `gpt-5.6-terra` | Reasoning effort: `low`.

Product-owner unaided Compact R0 remains an external prerequisite for
`A-01`, Workspace v3 `8/8`, and release/usability acceptance. The earlier Wide
run was user-reported as pass; Compact was interrupted by this study and is
not counted as passed.

## Verification

```text
Status: Complete
Scope: Create, save, reopen, and assess ten current-format recipes through the current Release EXE using the bundled Thickness Coupon C3D
Acceptance criteria: 10 physical recipe files -> pass; diverse tool coverage -> pass; EXE save/reopen -> pass 10/10; JSON parse and chain inspection -> pass 10/10; prioritized UX findings and useful additions -> pass; no automatic execution or source/result mutation claim -> pass
Verification: actual EXE authoring on DISPLAY2; EXE reopen 10/10; Get-ChildItem plus ConvertFrom-Json 10/10 and 90 total persisted steps; issue-ledger.js validate 14/14; git diff --check pass; current-authority stale-priority search pass
Evidence: D:/OpenVisionLab-TestData/OpenVisionLab-3D-Studio/artifacts/current/20260815-exe-recipe-authoring-study/recipes/; this document; .proofline/issues/PL-0009.json through PL-0014.json
Boundary / next dependency: only the bundled Thickness baseline is a ready single-task recipe; pending/incompatible chains are preserved as UX evidence, not presented as successful inspection runs; Compact owner R0 remains external
```

```text
Status: Complete
Scope: PL-0009 compatible tool insertion, proposed typed route, legacy mismatch repair entry, and explicit no-auto-execution behavior
Acceptance criteria: incompatible Add unavailable -> pass; HeightField consumer never auto-routed to MeasurementResult -> pass; proposed input/output contracts visible -> pass; valid route save/reopen -> pass; legacy mismatch load and direct repair -> pass; Add/repair cause no Preview/Publish/Run -> pass
Verification: Debug Shell build 0 warnings/0 errors; Tool Recipe teaching 42/42; Height Measurement Workbench 54/54; Recipe Manager + WPG 40/40; Tool Recipe selections 29/29; Artifact Navigator pass; full Release build 0 warnings/0 errors; actual Release EXE Wide/Compact English/Korean on DISPLAY2
Evidence: D:/OpenVisionLab-TestData/OpenVisionLab-3D-Studio/artifacts/current/20260815-pl0009-compatible-tool-routing/; .proofline/issues/PL-0009.json; this document
Boundary / next dependency: the original ten files remain reproduction evidence rather than repaired recipes; PL-0010 workflow consolidation is recorded below; product-owner Compact R0 remains external
```

```text
Status: Complete
Scope: PL-0010 contextual Add, dual-ROI setup/teaching, selected-step readiness, direct Tools return, persistence, and safe recipe reset
Acceptance criteria: Add opens Selected Tool -> pass; compatible input and all current setup requirements visible -> pass; exactly one primary next action -> pass; direct Tools return -> pass; complete Reference/Measurement teaching reachable with Viewer visible in Compact and Wide -> pass; save/reopen restores without execution -> pass; New Recipe reset returns empty defaults without execution -> pass
Verification: Debug build 0 warnings/0 errors; Tool Recipe teaching 43/43; Height Measurement Workbench 56/56; Workbench docking 83/83; final Release build 0 warnings/0 errors; actual current Release EXE on DISPLAY2 at physical Compact 1280x760 and Wide 1920x1032 with English/Korean and full two-ROI teaching
Evidence: D:/OpenVisionLab-TestData/OpenVisionLab-3D-Studio/artifacts/current/20260815-pl0010-contextual-step-setup/; .proofline/issues/PL-0010.json; this document
Boundary / next dependency: PL-0011 recipe-wide health navigation is completed below; PL-0014 owns the language selector popup theme leak; product-owner Compact R0 remains external
```

```text
Status: Complete
Scope: PL-0011 exact recipe-health projection, localized non-wrapping requirement navigation, Flow reveal, and presentation-only safety
Acceptance criteria: six exact mutually exclusive counts -> pass; Previous/Next reveals exact owner and requirement without wrapping or mutation -> pass; seventeen-step Wide/Compact review has reachable actions and no clipped required text -> pass
Verification: Debug and Release builds 0 warnings/0 errors; Tool Recipe teaching 46/46; Workbench docking 84/84; Shell smoke options 37/37; current Release EXE Wide/Compact English/Korean on DISPLAY2; last-requirement and held pointer-down captures accepted; fixed Wide/Compact -ValidateOnly pass
Evidence: docs/OPENVISIONLAB_3D_RECIPE_HEALTH_NAVIGATION_20260815.md; .proofline/issues/PL-0011.json; D:/OpenVisionLab-TestData/OpenVisionLab-3D-Studio/artifacts/current/20260815-pl0011-recipe-health-navigation/
Boundary / next dependency: product-owner unaided Wide/Compact R0 remains external; PL-0013 is the next deterministic software priority
```
