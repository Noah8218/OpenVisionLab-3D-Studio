# Surface Model Symmetry Declaration Closure

Date: 2026-08-03

Status: Complete (`F-13`)

## Operator problem and bounded outcome

A saved SurfaceModel previously had no explicit way to say whether later pose
comparison should treat discrete rotations as equivalent. `F-13` adds that
model-owned declaration and validates it before persistence or use. It does
not change matching results.

Included scope:

- explicit `none` and discrete rotational symmetry declarations;
- model-local `x`, `y`, or `z` rotation axis and integer order;
- canonical content identity, JSON persistence, and save/load validation;
- exact compatibility for existing undeclared schema-`1.0` artifacts;
- preparation routing from the existing SurfaceModel request.

Excluded scope:

- symmetry-aware pose-equivalence arithmetic (`J-13`);
- reflection, continuous rotation, compound groups, or arbitrary-vector axes;
- inferred symmetry;
- UI or PropertyGrid authoring;
- any change to Preview, Publish, Run, Validation, or Viewer behavior.

## Saved contract

| Schema | Declaration | Valid shape | Identity behavior |
| --- | --- | --- | --- |
| `1.0` | undeclared (`null`, omitted from JSON) | no symmetry field | legacy canonical hash and JSON bytes are unchanged |
| `1.1` | `none` | `axis=none`, `order=1` | declaration participates in the canonical hash |
| `1.1` | `discrete-rotation` | `axis=x|y|z`, `order>=2` | declaration participates in the canonical hash |

Schema `1.1` rejects a missing declaration, unsupported kinds, malformed
`none`, invalid rotational axes or orders, and tampered content hashes. Schema
`1.0` rejects a declaration so its legacy meaning remains unambiguous.

Controlled hashes:

- legacy undeclared: `084EF0B6919673CB43817CA6ED50526BF20761B2D7FB0C609D8E35D28BB1A82B`;
- explicit none: `0C864385B49F90D6569C28F0838A879857F955A03CC9655AEB51ADD346965A0E`;
- discrete rotation `z`, order `4`:
  `7A65488BA280C4495F2F655D20986E7FF57C70EA1B1EE2F0082D4853BC2AA1BC`.

## Ownership and structural proof

| Responsibility | Owner after `F-13` | Evidence |
| --- | --- | --- |
| declaration vocabulary and artifact identity | Core `SurfaceModelArtifact` | typed declaration and schema-aware canonical hash |
| declaration validity | Core `SurfaceModelArtifactValidator` | fail-closed schema/kind/axis/order checks |
| JSON and atomic persistence | Data `SurfaceModelArtifactStore` | null omission preserves legacy JSON; current declarations round-trip |
| preparation routing | Tools `SurfaceModelPreparation` | optional declaration is passed into the existing artifact owner |
| matching arithmetic | vendored Library-Noah | unchanged; Studio does not interpret pose equivalence in this slice |

The execution path is now:

```text
SurfaceModelPreparationRequest.Symmetry
  -> SurfaceModelArtifact.Create
  -> schema-aware canonical identity
  -> SurfaceModelArtifactValidator
  -> SurfaceModelArtifactStore
```

The former artifact, validator, and store remain their existing owners. No
partial type, new service layer, duplicate numerical owner, or UI state owner
was introduced. Code-structure verification passes `29/29`, with zero Studio
numerical migration-debt files and `31` reviewed Studio boundaries.

### Refactor proof report

- Before: SurfaceModel identity, validation, preparation, and persistence had
  no symmetry data path.
- After: the existing Core artifact owns the typed declaration and identity;
  its validator owns declaration validity; Data persists it; Tools only routes
  the optional request value.
- Evidence: focused `34/34`, structure `29/29`, and source search show one
  canonical declaration type and no matching interpretation.
- Old call path: preparation request -> artifact creation without a symmetry
  value.
- New call path: preparation request -> optional declaration -> artifact
  creation -> canonical hash -> validator -> atomic store.
- Dependency direction: Tools -> Core contract; Data -> Core contract. Core
  remains WPF-neutral and has no dependency on Tools, Data, or Noah.
- State/data owner: the saved SurfaceModel artifact, not the Workbench, Viewer,
  or matching executor.
- Remaining structural work for `F-13`: N/A; every included owner and path is
  active. `J-13` is a separate numerical scope, not unfinished F-13 work.
- Not verified: symmetry-aware pose equivalence and human-owner operation,
  because both are outside this bounded contract slice.

## Library-Noah boundary

The vendored `Lib.ThreeD 2.8.9` API and committed Noah source were inspected.
No symmetry-equivalence Tool exists, and `F-13` needs no numerical algorithm:
it is a Studio model identity/validation/persistence contract. Therefore the
package was not changed. `J-13` must add any pose-equivalence arithmetic as a
public sealed Library-Noah Tool in committed source, pack that exact commit,
vendor it, and then adapt Studio.

## Verification

Physical evidence root:

`D:\OpenVisionLab-TestData\OpenVisionLab-3D-Studio\artifacts\current\20260803-f13-surface-model-symmetry-declaration\verification\`

| Gate | Result |
| --- | --- |
| Release solution build | Pass, `0` warnings / `0` errors |
| focused SurfaceModel contract | Pass, `34/34` |
| legacy schema-1.0 byte parity | Pass, `5/5`, zero differences |
| Library-Noah package bridge | Pass, `19/19` |
| existing Surface Match foundation | Pass, `34/34` |
| acceptance and fixed performance | Pass, `14/14` and `18/18` |
| multiple-match Runner and Workbench | Pass, `14/14` and `10/10` |
| surface-edge and review | Pass, `21/21` and `20/20` |
| accepted-input Workbench/Runner parity | Pass, `23/23` |
| docking and Inspection Workspace | Pass, `82/82` and `64/64` |
| Validation Set and command line | Pass, `84/84` and `31/31` |
| code structure | Pass, `29/29` |
| Wide/Compact R0 `-ValidateOnly` | Pass at `1920 x 1040` and `1280 x 760` |
| `git diff --check` | Pass |

The parity verifier requires the assessment-linked
`accepted-known-pose.surface-match-execution.json`. Diagnostic reports from
three deliberately corrected runs that mixed the raw `known-pose` execution
with its separate assessment are retained under `verification\diagnostics`;
the correctly linked final report passes `23/23`.

No UI, visible text, layout, navigation, theme, or responsive behavior changed,
so new Wide/Compact screenshots were not required. The fixed Release binary
set was refreshed and both non-launching R0 validation modes passed.

## Inventory and next dependency

`F-13` moves from `N` to `C`. Inventory is now
`133 C / 17 P / 60 N / 9 E / 16 O` (`235` total).

1. `J-13 Symmetry-aware pose equivalence` | Recommended model: `gpt-5.6-sol` | Reasoning effort: `high`

Human-owner unaided Wide/Compact R0 remains external and is not replaced by
automated checks.

## Durable completion record

Status: Complete
Scope: Saved and validated SurfaceModel declarations for none or discrete model-axis rotation, with legacy schema-1.0 compatibility and no matching-behavior change.
Acceptance criteria: Schema contract, fail-closed validation, canonical identity, save/load round trip, legacy byte compatibility, and unchanged matching regressions all pass.
Verification: Release `0/0`; SurfaceModel `34/34`; legacy parity `5/5`; bridge `19/19`; matching `34/34`; acceptance `14/14`; performance `18/18`; multiple Runner/Workbench `14/14` and `10/10`; edge/review `21/21` and `20/20`; single parity `23/23`; docking `82/82`; workspace `64/64`; Validation Set `84/84`; command line `31/31`; structure `29/29`; Wide/Compact R0 `-ValidateOnly` pass.
Evidence: `docs/OPENVISIONLAB_3D_SURFACE_MODEL_SYMMETRY_DECLARATION_20260803.md` and the D-backed `20260803-f13-surface-model-symmetry-declaration` verification folder.
Boundary / next dependency: This F-13 slice does not itself implement or prove symmetry-aware pose equivalence. Its former `J-13` dependency is now Complete in `docs/OPENVISIONLAB_3D_SYMMETRY_AWARE_POSE_EQUIVALENCE_20260803.md`; human-owner R0 remains external.
