# Model Key-Point Artifact and Debug Overlay

Date: 2026-08-03

Status: Complete

Backlog item: `J-07 Model key-point artifact and debug overlay`

## Outcome

OpenVisionLab 3D Studio now has one deterministic, identified model key-point
contract built from the already prepared `SurfaceModel.Samples` domain.
Library-Noah owns the key-point selection calculation. Studio maps the
selected source-sample and source-triangle locators into a content-addressed
artifact, persists that artifact atomically, and creates a WPF-neutral,
display-only position/normal overlay artifact.

Key-point creation and overlay creation do not execute Preview, Publish, Run,
Validation, pose search, scoring, or acceptance, and do not mutate the
SurfaceModel. The key points are not yet consumed by the matching kernel.

## Bounded contract

`DeterministicModelKeyPointExtractionTool` uses the following schema-1 rule:

1. Validate unique non-negative source-sample orders, finite positions, unit
   normals, positive maximum count, and finite non-negative minimum
   separation.
2. Canonicalize candidates by source-sample order.
3. Select the lowest source-sample order as the first key point.
4. Repeatedly select the candidate whose distance to its nearest already
   selected point is greatest.
5. Break an exact distance tie by the lower source-sample order.
6. Stop at `MaximumKeyPointCount` or when the next distance is not strictly
   greater than `MinimumSeparation`.

The Studio artifact preserves:

- exact SurfaceModel content SHA-256, unit, and model frame;
- source sample count;
- extraction method and parameters;
- contiguous extraction order;
- stable identity `kp.sample.{SourceSampleOrder:D8}`;
- exact source-sample and source-triangle locators;
- source position and normal;
- nearest-selected distance at extraction time;
- canonical content SHA-256.

The debug overlay copies those identified positions, normals, locators, and
distances and links both the SurfaceModel and key-point artifact SHA-256. It
does not own WPF types, marker size, color, normal-vector display length, pose,
score, or decision.

## Product and commercial boundary

This slice adapts the commercial principle that model preparation and debug
evidence should be inspectable instead of hidden. It does not copy a
competitor UI, parameter set, algorithm, or screen layout.

The HALCON terminology is deliberately not claimed as equivalent. Its
official documentation says the surface model is created by sampling the 3D
object model, while `find_surface_model` selects key points from the sampled
scene during matching. J-07 is a bounded model-side representative-point
artifact for OpenVisionLab and is not a claim that HALCON's scene-key-point
algorithm was reproduced:

- <https://www.mvtec.com/doc/halcon/2511/en/create_surface_model.html>
- <https://www.mvtec.com/doc/halcon/2511/en/find_surface_model.html>

## Ownership and refactor proof

### Before

- Current owner/coupling: `SurfaceModel` had deterministic surface samples but
  no key-point owner, identified key-point artifact, persistence, or debug
  overlay contract.
- Why that was insufficient: a Viewer or future matcher could otherwise invent
  an untracked subset with no stable identity or audit boundary.

### After

- New owner:
  `Lib.ThreeD.FeatureExtraction.DeterministicModelKeyPointExtractionTool` at
  committed Library-Noah source
  `7ed50ea37b3d7cb711c2afe698d209f9073e9217`.
- Dependency direction:
  `Core artifact <- Tools identity adapter -> vendored Library-Noah`.
- State/data owner: `ModelKeyPointArtifact` owns immutable identified evidence;
  `ModelKeyPointArtifactStore` owns atomic JSON persistence;
  `ModelKeyPointDebugOverlayArtifact` owns display-only geometry.
- New call path:
  `J-05 retained SurfaceModel samples -> Noah Tool -> identified key-point artifact -> display-only debug overlay`.
- Removed ownership: no selection distance, farthest-point iteration, or tie
  calculation exists in Studio.
- Proof: focused `15/15`, code-structure `29/29`, and the Tool-only ledger has
  zero migration debt with `33` reviewed Studio boundaries.

### Remaining structural work

None for the agreed J-07 artifact/overlay contract. Connecting these key
points to pose search would be a separate matching-algorithm change and must
not be inferred from this closure.

## Controlled evidence

The controlled model reuses the J-05 six-triangle fixture. Explicit internal
triangle `1`, explicit unobservable triangle `2`, and exact duplicate triangle
`3` are removed from the active domain. Prepared samples retain source
triangles `0,4,5`.

With maximum count `2` and minimum separation `1 mm`, key points are:

| Order | Stable ID | Source sample | Source triangle | Nearest-selected distance |
| ---: | --- | ---: | ---: | ---: |
| 0 | `kp.sample.00000000` | 0 | 0 | `0 mm` |
| 1 | `kp.sample.00000002` | 2 | 5 | `8 mm` |

Repeated extraction has the same key-point array and content SHA-256
`BC68FBAB2ADFBAE4DC4480CD30134A702C19D9FAE828484C2302A8CF622B56B3`.
The debug overlay has two markers and SHA-256
`803D308502901E66EFCFF4E28D63634BE6CB27ECEDBCB32F3B94CA9EC3F61E31`.
Save/reopen preserves the artifact. Tampering and an unsupported method fail
closed.

## Library-Noah provenance

| Field | Value |
| --- | --- |
| Branch | `codex/j07-model-keypoints` |
| Source commit | `7ed50ea37b3d7cb711c2afe698d209f9073e9217` |
| Package | `Lib.ThreeD 2.8.12` |
| SHA-256 | `7E5DAF887851CB16C45279CD957260C2546AD0EDBB92B9F4903E23E529BADFE3` |
| Target | `netstandard2.0` |
| Studio vendor | `third_party/LibraryNoah/Lib.ThreeD.2.8.12.nupkg` |

The source was committed before packaging. Noah Release builds with zero
warnings/errors and Smoke passes `122/122`. Studio package provenance and the
direct package bridge pass `21/21`.

## Verification

| Gate | Result |
| --- | --- |
| Library-Noah Release / Smoke | `0/0`; `122/122` |
| Studio Release Rebuild | `0/0` |
| Package provenance / direct bridge | pass; `21/21` |
| J-07 controlled key points and overlay | `15/15` |
| J-05 retained-domain regression | `15/15` |
| SurfaceModel foundation / legacy byte parity | `34/34`; `5/5` |
| Single matching / acceptance / performance | `34/34`; `14/14`; `18/18` |
| Multiple-match / symmetry equivalence | `14/14`; `15/15` |
| Edge matching / diagnostic review | `21/21`; `20/20` |
| Multiple / single Workbench parity | `10/10`; `23/23` |
| Docking / Inspection Workspace / Validation Set | `82/82`; `64/64`; `84/84` |
| Command line / structure | `31/31`; `29/29` |
| CI regression registration | `--verify-model-key-points` dedicated gate present |
| R0 fixed inputs | Wide and Compact `-ValidateOnly` pass |

Evidence is physically stored at:

`D:\OpenVisionLab-TestData\OpenVisionLab-3D-Studio\artifacts\current\20260803-j07-model-key-points\`

No UI, visible text, navigation, layout, Viewer renderer, recipe editor, or
PropertyGrid changed. Fresh UI screenshots are therefore neither required nor
claimed. Human-owner unaided Wide/Compact R0 remains external and is not
replaced by `-ValidateOnly`.

## Inventory and next priority

`J-07` moves from `N` to `C`. Inventory is now
`136 C / 17 P / 57 N / 9 E / 16 O` (`235` total).

1. `B-12 Acquisition/source provenance text and limitation notes` | Recommended model: `gpt-5.6-sol` | Reasoning effort: `high`
2. `K-04 Acquisition viewpoint/direction metadata for edge orientation` | Prerequisite: complete `B-12` first | Recommended model: none until prerequisite passes | Reasoning effort: none
3. `L-13 Surface-match pose/score component export` | Recommended model: `gpt-5.6-terra` | Reasoning effort: `medium`

`B-12` is the shortest dependency-closing path for the remaining matching
diagnostic item `K-04`. It must persist explicit available/unavailable
provenance without execution and must not infer a camera or viewpoint.
`L-13` is independently dependency-ready but follows the diagnostic blocker
in the current matching stream. Human-owner `A-01` R0 remains external and
needs no model-token spend until the owner performs it.

## Completion record

Status: Complete

Scope: Deterministic Noah-owned model key-point extraction from the J-05
retained SurfaceModel sample domain, plus a persisted identified Studio
artifact and WPF-neutral display-only debug overlay artifact.

Acceptance criteria: committed public Noah Tool and exact vendored package ->
pass; stable key-point count/identity and deterministic tie order -> pass;
J-05 retained source-sample/source-triangle locators -> pass; exact
save/reopen and fail-closed tamper/method handling -> pass; display-only
overlay linkage and source immutability -> pass; matching remains unchanged ->
pass; ownership and fixed-input gates -> pass.
The dedicated CI command is registered so the focused contract is replayed on
future pushes and pull requests.

Verification: Noah Release `0/0`, Smoke `122/122`; Studio Release Rebuild
`0/0`; package bridge `21/21`; J-07 `15/15`; focused and broad matrix above;
structure `29/29`; Wide/Compact R0 `-ValidateOnly` pass.

Evidence: This document and the D-backed
`20260803-j07-model-key-points` artifact folder.

Boundary / next dependency: This is a model-side representative-point and
debug-evidence contract. It does not use key points in matching, add a Viewer
renderer or UI, infer acquisition/view direction, reproduce HALCON scene key
points, or prove human usability, real-part robustness, metrology,
cross-hardware performance, or production readiness. Next is `B-12`, which
unblocks `K-04`; `L-13` remains independently dependency-ready.
