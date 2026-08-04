# OpenVisionLab 3D Symmetry-Aware Pose Equivalence

Date: 2026-08-03

Status: Complete for `J-13`

## Outcome

OpenVisionLab 3D Studio can now compare two identified model-to-scene rigid
poses under the saved SurfaceModel symmetry declaration. Undeclared schema
`1.0` and schema `1.1` `none` models use direct rigid-pose comparison. A
schema `1.1` discrete rotation about model axis `x`, `y`, or `z` uses the
declared cyclic order and reports the deterministic equivalent operation.

This slice does not alter the existing single-match or multiple-match search.
J-12 already prevents duplicate collected results by claiming scene samples
disjointly. J-13 supplies an independent typed equivalence evaluation for two
poses; it does not infer symmetry or change result ordering, identity, or
selection.

## Contract

Included:

- exact schema `1.0` undeclared/direct behavior;
- schema `1.1` `none`/direct behavior;
- declared discrete cyclic rotations about model `x`, `y`, or `z`;
- finite non-negative translation and rotation tolerances;
- deterministic lowest-operation-index tie breaking;
- typed model, symmetry, unit, source-frame, target-frame, limit, residual,
  operation, decision, and evidence fields;
- strict rejection of invalid models, non-rigid poses, identity mismatches,
  frame mismatches, unit mismatches, and invalid limits.

Excluded:

- reflection, continuous rotation, compound symmetry groups, or arbitrary
  axes;
- symmetry inference from geometry;
- changes to matching search, multiple-result collection, persistence, recipe
  execution, Viewer selection, Preview, Publish, Run, or Validation;
- physical calibration, uncertainty, metrology, or production claims;
- UI changes.

For a model-to-scene convention `scene = R model + t`, the Noah Tool tests the
declared cyclic candidates `R_reference * S(k)`. Translation residual is the
distance between the transformed model origins. Rotation residual is the
geodesic angle between candidate and symmetry-adjusted reference rotations.
Both authored limits are inclusive.

## Ownership and structural proof

Library-Noah owns all equivalence arithmetic through public sealed
`RigidPoseSymmetryEquivalenceTool` and typed source-neutral contracts. Studio
owns only WPF-neutral product evidence, model/pose/unit/frame/limit validation,
declaration mapping, and result composition.

```text
SurfaceModel + reference pose + candidate pose + limits
    -> Studio identity/unit/frame validation
    -> Lib.ThreeD RigidPoseSymmetryEquivalenceTool
       (reference rotation * declared cyclic operation)
    -> Studio typed evidence mapping
```

The Studio evaluator contains no relative-rotation, angle, trigonometric, or
symmetry-operation arithmetic. The decreasing migration ledger remains at
zero debt. The new strict evaluator is recorded as the 32nd reviewed Studio
boundary, and the structure guard rejects arithmetic moving back into it.

Before this slice, SurfaceModel symmetry was saved metadata but no product
owner interpreted it. After this slice, the new evaluator is the independent
owner for Studio validation and Noah adaptation. The existing single and
multiple matching executors remain unchanged and do not own the new behavior.

### Refactor proof report

- Before: Core owned a saved symmetry declaration; no Studio or Noah call path
  evaluated two poses under it.
- After: Core owns immutable evaluation evidence, Tools owns the strict product
  adapter, and committed Noah owns the new numerical operation.
- Evidence: `SurfaceMatchPoseEquivalenceEvaluation.cs`,
  `SurfaceMatchPoseEquivalenceEvaluator.cs`,
  `RigidPoseSymmetryEquivalenceTool.cs`, the package bridge report, and the
  `29/29` structure report.
- Old path: none; J-13 was absent and F-13 metadata was deliberately not
  interpreted.
- New path: caller -> Studio evaluator identity checks -> vendored Noah Tool ->
  immutable Studio evidence.
- Moved responsibility: N/A; this is a new numerical responsibility, created
  directly in its required Noah owner rather than temporarily in Studio.
- Dependency direction: Runner verification -> Tools -> vendored `Lib.ThreeD`;
  Core remains runtime-neutral and has no Noah dependency.
- State/data owner: SurfaceModel declaration and evaluation evidence remain
  immutable Core contracts; Noah receives source-neutral copied values and
  owns no recipe or UI state.
- Remaining structural work for J-13: none. Wiring equivalence into search or
  collection is excluded and not implied by this independent evaluator.
- Checks run: Release builds, Noah Smoke, direct package bridge, focused J-13,
  legacy byte parity, matching regressions, and the structure guard listed
  below.
- Not verified: human-owner usability, physical equivalence of a real part,
  calibration, metrology, or production robustness.

## Exact Library-Noah provenance

| Item | Value |
| --- | --- |
| Source worktree | `C:\Git\Library-Noah-j12` |
| Source commit | `f225fd2709de1dd1d0ecfe19b37315cb1f019ee4` |
| Commit subject | `Add rigid pose symmetry equivalence tool` |
| Package | `Lib.ThreeD 2.8.10` |
| Target | `netstandard2.0` |
| SHA-256 | `535CD75D33BE5EC015B1B36215FF3DBDD7E8AEC1A5F2B8FFE1FCCBA18B7877C7` |
| Studio vendor | `third_party/LibraryNoah/Lib.ThreeD.2.8.10.nupkg` |

The package was produced from clean committed source. Noah Release passes
with zero warnings/errors and its Smoke suite passes `113/113`. Studio package
metadata, source commit, checksum, license, target assembly, and direct bridge
verification pass.

## Controlled evidence

The focused Studio verifier passes `15/15`:

- non-commutative `Rx(30) * Rz(90)` proves model-axis post-multiplication for
  declared `z/4`, with operation `1`, angle `90`, and zero residuals;
- a `45` degree half-step remains non-equivalent;
- independent translation tolerance rejects a translated pose;
- undeclared and declared-none models use direct comparison and retain a
  `90` degree residual;
- `x/2` and `y/3` map to `180` and `120` degree equivalent operations;
- unit, model-source-frame, common-target-frame, invalid-limit, non-rigid, and
  tampered-model contracts fail closed;
- evaluation does not mutate either pose or execute matching.

Five established schema-`1.0` SurfaceModel, single-match, and multiple-match
artifacts retain exact bytes and SHA-256 hashes. This proves that enabling the
new independent evaluator did not rewrite existing persisted identities or
matching outputs.

## Verification

| Gate | Result |
| --- | --- |
| Library-Noah Release / Smoke | `0/0`; `113/113` |
| Studio Release | `0/0` |
| Package provenance / Noah bridge | pass; `20/20` |
| J-13 focused equivalence | `15/15` |
| SurfaceModel / legacy byte parity | `34/34`; `5/5` |
| Single matching / acceptance / performance | `34/34`; `14/14`; `18/18` |
| Multiple-match Runner / Workbench | `14/14`; `10/10` |
| Edge diagnostic / review | `21/21`; `20/20` |
| Workbench/Runner accepted-input parity | `23/23` |
| Docking / Inspection Workspace / Validation Set | `82/82`; `64/64`; `84/84` |
| Command line / structure | `31/31`; `29/29` |
| R0 fixed inputs | Wide and Compact `-ValidateOnly` pass |

Evidence is physically stored at:

`D:\OpenVisionLab-TestData\OpenVisionLab-3D-Studio\artifacts\current\20260803-j13-symmetry-aware-pose-equivalence\`

No UI, visible text, layout, navigation, or interaction changed, so a new UI
capture is neither required nor claimed. Human-owner unaided Wide/Compact R0
remains external and is not replaced by `-ValidateOnly`.

## Inventory and next priority

`J-13` moves from `N` to `C`. Inventory is now
`134 C / 17 P / 59 N / 9 E / 16 O` (`235` total).

Historical next item: `J-05 Remove internal/redundant/unobservable model
surfaces`. It is superseded by the completed
`OPENVISIONLAB_3D_MODEL_SURFACE_SELECTION_20260803.md` closure.

`J-07` is now the next dependency-ready matching item in the master backlog.
Its feature-extraction arithmetic must be implemented as a committed public
Library-Noah Tool before Studio adaptation. `K-04` remains blocked on `B-12` acquisition/view
direction evidence. Human-owner `A-01` R0 remains an external acceptance task
and needs no model-token spend until the owner performs it.

## Completion record

Status: Complete

Scope: Deterministic independent pose-equivalence evaluation for undeclared,
none, and declared model-axis cyclic SurfaceModel symmetry, with strict Studio
identity validation and Noah-owned arithmetic.

Acceptance criteria: Public committed Noah Tool, exact vendored package,
typed Studio evidence, direct and cyclic controlled fixtures, unchanged
matching/persistence bytes, strict ownership guard, and current R0 fixed-input
validation all pass.

Verification: Noah Release `0/0` and Smoke `113/113`; Studio Release `0/0`;
bridge `20/20`; J-13 `15/15`; focused and broad regression matrix above;
structure `29/29`; Wide/Compact R0 `-ValidateOnly` pass.

Evidence: This document and the D-backed
`20260803-j13-symmetry-aware-pose-equivalence` artifact folder.

Boundary / next dependency: This does not change or deduplicate J-12 matching
results, infer symmetry, add UI, or prove human usability/metrology. J-05 is
now Complete; current next is J-07. Human-owner R0 remains external.
