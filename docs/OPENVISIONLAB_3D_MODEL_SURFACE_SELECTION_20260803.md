# OpenVisionLab 3D Model Surface Selection

Date: 2026-08-03

Status: Complete

Backlog item: `J-05 Remove internal/redundant/unobservable model surfaces`

## Outcome

SurfaceModel preparation can now preserve the complete imported mesh while
declaring a smaller active source-triangle domain for matching, model-edge
extraction, and transformed-model overlays.

The bounded version-1 policy is:

- remove exact-coordinate duplicate triangles deterministically, retaining the
  lowest non-excluded source-triangle index;
- remove operator-authored internal source-triangle indices;
- remove operator-authored unobservable source-triangle indices;
- keep every source point, triangle, normal, and original triangle locator in
  the identified SurfaceModel artifact;
- perform no tolerance-based, enclosure-based, or viewpoint-based inference.

The last boundary is deliberate. Hidden-surface removal in a projection is
viewpoint dependent, while the current product has no approved acquisition or
view direction (`B-12`). Therefore `unobservable` is an explicit authored role
in this slice, not an
automatic visibility claim. `K-04` remains blocked on `B-12`.

## Ownership and dependency direction

Library-Noah owns the reusable geometry operation through public sealed
`DeterministicModelSurfaceSelectionTool`. It accepts source-neutral points,
triangles, and typed options, and returns controlled retained indices plus
typed removal evidence.

Studio owns:

- imported-source identity and dense-normal admission;
- explicit authored internal/unobservable source-triangle locators;
- strict Noah adaptation;
- schema, canonical content identity, persistence, and validation;
- mapping Noah's retained compact domain back to original source-triangle
  locators;
- routing the same active domain to matching samples, model edges, and Viewer
  overlays.

Dependency direction remains:

`Core contracts <- Tools adapters -> vendored Library-Noah`

No numerical or duplicate-detection fallback was added to Studio.

## Structural proof report

### Structural changes confirmed

- Before: `SurfaceModelPreparation` sent every imported triangle directly to
  Noah sampling; SurfaceModel, model-edge extraction, and overlays treated the
  full triangle array as the only domain.
- After: Noah first returns one typed retained source-triangle domain; Studio
  persists that domain, maps sampled compact indices back to original source
  locators, and routes it to sampling, edges, matching, and overlays.
- Evidence: `SurfaceModelPreparation`, `SurfaceModelSurfaceDomain`,
  `ModelSurfaceEdgeExtractor`, `SurfaceMatchOverlayArtifact`, and focused J-05
  `15/15` active-domain evidence.

### Call path

- Old path: imported mesh -> Noah deterministic sampling -> all-triangle
  SurfaceModel -> all-triangle edge/overlay.
- New path: imported mesh -> Noah deterministic surface selection -> Noah
  deterministic sampling over retained triangles -> schema-1.2 SurfaceModel
  selection -> shared retained edge/overlay/matching domain.
- Evidence: structure guard `29/29`; package bridge `21/21`; controlled active
  domain `0,4,5`, samples `0,4,5`, nine edges, three overlay triangles, and
  matching coverage `3/3`.

### Responsibility split and dependency/state flow

- Moved responsibility: duplicate triangle identity, canonical exclusions,
  and retained ordering moved out of product adaptation into Library-Noah.
- New owner: public sealed `DeterministicModelSurfaceSelectionTool` at committed
  Noah source `55ea7a61bd1281294e91aa5366d2bafb509d3667`.
- Dependency direction now: Core contract <- Tools identity adapter -> vendored
  Noah; Shell and Viewer consume immutable evidence only.
- State/data owner now: SurfaceModel remains the immutable Studio artifact;
  `SurfaceModelSurfaceSelection` owns its identified active domain.
- Evidence: zero migration debt, `32` reviewed Studio boundaries, no selection
  or duplicate algorithm detected in Studio, and direct package provenance.

### Remaining structural work

- Current owner/coupling: none remains for the agreed J-05 active-domain
  contract.
- Intended owner/path: N/A within J-05. J-07 key points were completed later
  as a separate backlog boundary.
- Required change: none for J-05.
- Subsequent proof: J-07 demonstrates its own committed Noah owner and consumes
  the J-05 retained domain without reintroducing full-topology feature
  extraction.

### Not verified

- Automatic enclosure or viewpoint-based visibility: intentionally excluded
  because `B-12` acquisition/view direction is absent.
- Human usability: owner R0 remains external; automated `-ValidateOnly` is not
  a substitute.

## Artifact contract

SurfaceModel now has three compatible schema levels:

| Schema | Meaning |
| --- | --- |
| `1.0` | Existing undeclared symmetry and all source triangles active |
| `1.1` | Existing explicit symmetry declaration and all source triangles active |
| `1.2` | Explicit symmetry plus identified surface-selection evidence |

Schema `1.2` records:

- policy and original source-triangle count;
- canonical explicit internal and unobservable index lists;
- exact-duplicate option state;
- canonical retained source-triangle indices;
- ordered removed-surface entries with typed reason and, for a duplicate, the
  earlier retained source-triangle locator.

The complete source geometry remains immutable. Downstream operations obtain
the active domain through `SurfaceModelSurfaceDomain`.

Without a selection request, preparation continues to emit schema `1.0` or
`1.1` exactly as before. Five established J-13 SurfaceModel JSON artifacts are
byte-identical after this change.

## Library-Noah provenance

| Field | Value |
| --- | --- |
| Branch | `codex/j05-model-surface-selection` |
| Source commit | `55ea7a61bd1281294e91aa5366d2bafb509d3667` |
| Package | `Lib.ThreeD 2.8.11` |
| SHA-256 | `AC61E132938AD184F3E3A39622A5BC3C4E48F1419D7C4EC75AC604A8CD1F8A42` |
| Target | `netstandard2.0` |
| Studio vendor | `third_party/LibraryNoah/Lib.ThreeD.2.8.11.nupkg` |

The source was committed before packaging. Noah Release builds with zero
warnings/errors and Smoke passes `118/118`. Studio package provenance and
direct package bridge verification pass `21/21`.

## Controlled comparison

The J-05 fixture contains six valid source triangles:

- triangle `0`: retained outer surface;
- triangle `1`: explicitly internal;
- triangle `2`: explicitly unobservable;
- triangle `3`: exact-coordinate duplicate of triangle `0` using different
  point indices;
- triangles `4` and `5`: retained outer surfaces.

The resulting artifact preserves all `6` source triangles and all `18`
points, while the active domain is `0,4,5`. It has three samples, nine
boundary edges, three overlay triangles, and full `3/3` matching coverage.
Save/reopen preserves the exact identified selection; role overlap and a
rehashed inconsistent partition fail closed.

Focused verification passes `15/15`.

## Verification

| Gate | Result |
| --- | --- |
| Library-Noah Release / Smoke | `0/0`; `118/118` |
| Studio Release Rebuild | `0/0` |
| Package provenance / direct bridge | pass; `21/21` |
| J-05 controlled selection | `15/15` |
| SurfaceModel foundation / legacy byte parity | `34/34`; `5/5` |
| Single matching / acceptance / performance | `34/34`; `14/14`; `18/18` |
| Multiple-match / symmetry equivalence | `14/14`; `15/15` |
| Edge matching / diagnostic review | `21/21`; `20/20` |
| Multiple / single Workbench parity | `10/10`; `23/23` |
| Docking / Inspection Workspace / Validation Set | `82/82`; `64/64`; `84/84` |
| Command line / structure | `31/31`; `29/29` |
| R0 fixed inputs | Wide and Compact `-ValidateOnly` pass |

Evidence is physically stored at:

`D:\OpenVisionLab-TestData\OpenVisionLab-3D-Studio\artifacts\current\20260803-j05-model-surface-selection\`

No UI, visible text, recipe editor, layout, or navigation changed. A new UI
capture is therefore neither required nor claimed. Human-owner unaided
Wide/Compact R0 remains external and is not replaced by `-ValidateOnly`.

## Historical inventory and superseded next priority

`J-05` moves from `N` to `C`. Inventory is now
`135 C / 17 P / 58 N / 9 E / 16 O` (`235` total).

At this checkpoint the former next priority was `J-07 Model key-point artifact
and debug overlay`. It is now Complete in
`docs/OPENVISIONLAB_3D_MODEL_KEY_POINT_ARTIFACT_AND_DEBUG_OVERLAY_20260803.md`.

The current dependency-ready priority is `B-12 Acquisition/source provenance
text and limitation notes` | Recommended model: `gpt-5.6-sol` | Reasoning
effort: `high`. Human-owner `A-01` R0 remains external and needs no model-token
spend until the owner performs it. `K-04` remains blocked on `B-12`
acquisition/view-direction evidence.

## Completion record

Status: Complete

Scope: Immutable SurfaceModel source geometry plus an identified active
source-triangle domain for exact duplicate removal and explicit internal/
unobservable exclusions, consumed consistently by preparation, matching,
model-edge extraction, persistence, and transformed overlays.

Acceptance criteria: Committed public Noah Tool and exact vendored package ->
pass; controlled prepared-model comparison with typed removal evidence ->
pass; source geometry and original locators preserved -> pass; active domain
shared by samples, edges, matching, and overlay -> pass; save/reopen and
fail-closed invalid input -> pass; no-selection legacy bytes unchanged ->
pass; ownership and current R0 fixed-input gates -> pass.

Verification: Noah Release `0/0`, Smoke `118/118`; Studio Release Rebuild
`0/0`; package bridge `21/21`; J-05 `15/15`; focused and broad matrix above;
structure `29/29`; Wide/Compact R0 `-ValidateOnly` pass.

Evidence: This document and the D-backed
`20260803-j05-model-surface-selection` artifact folder.

Boundary / next dependency: Automatic removal is limited to exact-coordinate
duplicates. Internal and unobservable surfaces require explicit source-
triangle locators. This does not infer enclosure, near-duplicate tolerance,
or viewpoint visibility; it adds no UI or recipe authoring surface and proves
no human usability, metrology, acquisition, production, or cross-hardware
claim. The former next item J-07 is now Complete; current next is `B-12`, and
`K-04` remains blocked on it.
