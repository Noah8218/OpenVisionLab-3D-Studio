# Multiple Surface Match Result Collection

Date: 2026-08-03

Status: Complete for deterministic software scope

## Operator problem and product fit

A Surface Match step could previously retain and display only one pose. That
made a scene containing two valid copies look like a single-result inspection
and left no stable identity with which later workflow could navigate or review
individual occurrences.

The product principle is current-result clarity: keep the
configuration, selected result, Viewer evidence, and status visibly linked.
OpenVisionLab implements that principle with its own terminology and existing
Workbench/Viewer layout.

## Closed scope

- Library-Noah owns deterministic repeated pose search, per-result
  unique-nearest coverage, greedy disjoint scene-sample claiming, result
  ordering, and bounded termination in public sealed
  `DeterministicMultipleSurfaceMatchTool`.
- Studio owns source/unit/frame identity checks, authored acceptance, immutable
  schema-1 collection composition, stable IDs, atomic persistence, explicit
  lifecycle routing, evidence, and UI.
- The Workbench exposes a retained-match selector only when more than one
  result exists. The first result is selected initially.
- Selecting another retained match is presentation-only. It routes that exact
  stored execution and assessment to the Viewer without running matching,
  changing recipe state, or publishing a candidate.
- Full match identity remains in the artifact and selector tooltip; the compact
  visible label uses order, a four-character identity suffix, and decision.
- The selector is disabled while a parameter experiment candidate or execution
  owns the surface. Selection itself is intentionally not persisted.

Excluded from this slice are issue-by-issue previous/next commands, symmetry
handling, acquisition direction, calibrated metrology, production performance,
cross-hardware timing, and human-usability acceptance.

## Library-Noah provenance

| Item | Fixed value |
| --- | --- |
| Package | `Lib.ThreeD 2.8.9` |
| Source worktree | `C:\Git\Library-Noah-j12` |
| Source branch | `codex/j12-multiple-match` |
| Source commit | `4e301f481cac886f78425197314cd540b653473a` |
| Package SHA-256 | `A3B212E6D8AC487DF668F0FE557C17615845A161412AE7AF6BD7FE4FCC260278` |
| Studio vendor | `third_party/LibraryNoah/Lib.ThreeD.2.8.9.nupkg` |

The exact committed Noah source passes Release build `0/0` and full Smoke
`108/108`. Studio consumes the packed commit through the vendored package; it
does not use a checkout `ProjectReference` and contains no duplicate
multiple-match arithmetic.

## Controlled two-object result

The source-neutral asymmetric five-point model is placed twice in one
ten-point scene. Both occurrences use `Rz = 30 degrees` and the controlled
translations `(10, -4, 2)` and `(-12, 7, 1)` mm.

- evaluated candidates: `75`;
- retained matches: `2`;
- coverage: `5/5 = 1.0` for both;
- scene-sample claims shared between matches: `0`;
- collection ID:
  `collection.surface-match.E77390ADCD57DCA6695A563329046CACFD2E1996C952177DC5B895F084E4B65B`;
- first match ID:
  `match.surface.D01874961D6830C7D79DED766A84CD6A7658F5E0BA68A1C13584E54442A9286D`;
- second match ID:
  `match.surface.AB4C6D21F3D07351FF4BACF599FCD0586BB5973E3255466D6732E9F06200BC08`.

Save/load preserves the exact collection and match identities. Tampering with
schema, order, policy, result linkage, content hash, stable identity, or
disjoint claims fails validation.

## Structural proof

### Structural changes confirmed

- Before: Studio had a single Surface Match execution/assessment path and no
  typed retained-result collection or selection state.
- After: Core owns `SurfaceMatchCollectionArtifact`; Data owns its validated
  atomic store; Tools owns strict Noah adaptation; Runner owns deterministic
  contract verification; the Workbench owns only transient presentation
  selection and routes the selected stored result to the existing Viewer path.
- Evidence: the new owners are in
  `src/OpenVisionLab.ThreeD.Core/Contracts/Matching/SurfaceMatchCollectionArtifact.cs`,
  `src/OpenVisionLab.ThreeD.Data/SurfaceModels/SurfaceMatchCollectionArtifactStore.cs`,
  `src/OpenVisionLab.ThreeD.Tools/Matching/MultipleSurfaceMatchEvaluationExecutor.cs`,
  `src/OpenVisionLab.ThreeD.Runner/Verification/Matching/MultipleSurfaceMatchVerification.cs`,
  and
  `src/OpenVisionLab.ThreeD.Shell/ViewModels/Workbench/ToolWorkbenchViewModel.SurfaceMatchCollection.cs`.

### Call path

- Old path: one Studio `SurfaceMatchEvaluationExecutor` invocation produced one
  execution/assessment and Viewer overlay.
- New path: explicit multiple-match verification/load -> Studio identity
  validation -> `DeterministicMultipleSurfaceMatchTool.Execute(...)` -> strict
  execution/assessment mapping -> immutable collection/store -> explicit
  Workbench selection -> existing Viewer execution/assessment presentation.
- Evidence: Runner `14/14`, Workbench `6/6`, single-match parity `23/23`, and
  structure `29/29`.

### Responsibility split and dependency/state flow

- Moved responsibility: repeated pose search, per-match scoring, disjoint scene
  claiming, ordering, and bounded stop policy.
- New owner: committed Library-Noah `DeterministicMultipleSurfaceMatchTool`.
- Dependency direction: Shell -> Studio Core/Data/Tools -> vendored
  `Lib.ThreeD 2.8.9`; no Noah checkout reference and no WPF/UI dependency in
  Core, Data, Tools, or Noah.
- State owner: the immutable collection owns retained result evidence; the
  Workbench owns only the current in-memory selection. Selection is not recipe
  or collection persistence and cannot execute matching.
- Evidence: package provenance pass, bridge `19/19`, schema-1 baseline with
  zero debt and `31` reviewed boundaries, and the structure ownership check.

### Remaining structural work

No remaining structural work is required by the J-12 acceptance criteria.
`K-09` was subsequently completed over this closed typed collection while
preserving the same presentation-only state boundary. See
`docs/OPENVISIONLAB_3D_MULTIPLE_MATCH_ISSUE_NAVIGATION_20260803.md`.

## Verification

All Studio checks below use the current Release source unless named otherwise.

| Check | Result |
| --- | ---: |
| Studio Release solution build | `0 warnings / 0 errors` |
| Library-Noah package provenance/integrity | Pass |
| Studio Library-Noah bridge | `19/19` |
| Multiple Surface Match Runner | `14/14` |
| Multiple Surface Match Workbench | `6/6` |
| Existing Surface Match foundation | `34/34` |
| Acceptance | `14/14` |
| Fixed performance regression | `18/18` |
| SurfaceModel | `22/22` |
| Surface edge | `21/21` |
| Surface edge diagnostic/review | `20/20` |
| Single-match Workbench/Runner parity | `23/23` |
| Docking | `82/82` |
| Inspection Workspace | `64/64` |
| Validation Set | `84/84` |
| Shell command line | `30/30` |
| Structure/Noah ownership | `29/29`, `0` debt, `31` reviewed boundaries |
| Human-owner R0 Wide `-ValidateOnly` | Pass |
| Human-owner R0 Compact `-ValidateOnly` | Pass |

The acceptance and edge-review verifiers were finally run sequentially because
parallel invocations intentionally write the same controlled foundation files.
Their final sequential reports pass; the earlier file-write collisions were
not product failures.

## UI evidence

Physical evidence root:

`D:\OpenVisionLab-TestData\OpenVisionLab-3D-Studio\artifacts\current\20260803-j12-multiple-match\`

True pre-edit baselines:

- `before\wide-surface-match-before.png`;
- `before\compact-surface-match-before.png`.

Final current-Release evidence:

- `after\wide-second-match-final.png`;
- `after\compact-korean-popup-context-final.png`;
- `after\compact-korean-popup-child-final.png`;
- `after\compact-disabled-selector.png`;
- `after\final-release-window-monitor-placement.txt`.

Wide `1920 x 1040` and Compact `1280 x 760` were placed on the dynamically
selected leftmost monitor `\\.\DISPLAY1`, bounds `(0, 0)-(1920, 1080)`. The
final captures contain no unexplained overlap, required-text clipping,
out-of-pane controls, or unintended scroll bars. Normal, selected, pointer
hover, keyboard focus, open-popup, and disabled states use the existing
semantic ComboBox/theme resources. The popup child capture is application-only
and shows both complete compact result labels.

## Completion record

Status: Complete

Scope: Deterministic, stable-identified, disjoint multiple Surface Match result
collection in committed Library-Noah; strict Studio adaptation, immutable
artifact persistence, explicit presentation-only selection, Viewer routing,
current-layout selector, and deterministic Runner/Workbench evidence.

Acceptance criteria: Known two-object fixture returns exactly two ordered,
stable, disjoint matches -> pass; save/load and tamper rejection -> pass;
selecting a retained match updates only presentation -> pass; existing
single-match, acceptance, performance, edge, workspace, validation, docking,
and package boundaries remain green -> pass; current Release Wide/Compact and
theme-state evidence contains no unexplained layout defect -> pass.

Verification: Noah Release `0/0` and Smoke `108/108`; Studio Release `0/0`;
package pass; bridge `19/19`; multiple-match Runner `14/14`; Workbench `6/6`;
focused regressions `34/34`, `14/14`, `18/18`, `22/22`, `21/21`, `20/20`, and
`23/23`; docking `82/82`; Inspection Workspace `64/64`; Validation Set
`84/84`; command line `30/30`; structure `29/29`; Wide/Compact R0
`-ValidateOnly` pass.

Evidence: This document, the exact Noah commit and vendored package above, and
the D-backed `20260803-j12-multiple-match` evidence root.

Boundary / next dependency: This proves deterministic software behavior for
the controlled fixture only. It does not prove human usability, real-part
robustness, symmetry-aware pose equivalence, acquisition direction, calibrated
metrology, cross-hardware performance, or production readiness. Human-owner R0 remains
external. `K-09`, `F-13`, `J-13`, and `J-05` are now Complete. The next
dependency-ready item is `J-07 Model key-point artifact and debug overlay`,
implemented in committed Library-Noah first.
