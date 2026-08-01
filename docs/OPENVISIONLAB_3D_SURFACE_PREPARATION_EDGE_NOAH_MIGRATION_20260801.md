# Surface Preparation and Edge Library-Noah Migration

Date: 2026-08-01

Status: Complete

## Outcome

The active Surface Match preparation and edge execution path no longer owns
sampling, mesh-edge geometry, organized-grid height-step extraction, or edge
correspondence arithmetic in Studio. Those calculations are public,
source-neutral, deterministic Library-Noah Tools consumed from the committed
and vendored `Lib.ThreeD 2.8.1` package.

This is an ownership migration, not an algorithm change. Studio still owns
source/unit/frame/content identity, Source Quality admission, normal-quality
admission, canonical artifacts, authored acceptance, explicit execution,
evidence composition, persistence, and UI.

## Migrated Tool contracts

| Studio compatibility entry point | Library-Noah public Tool | Noah-owned calculation | Studio-retained responsibility |
| --- | --- | --- | --- |
| `SurfaceModelPreparation` | `DeterministicSurfaceModelPreparationTool` | even triangle schedule, centroid, declared-normal average | mesh admission, source preservation, identified SurfaceModel |
| `PreparedScenePreparation` | `DeterministicPreparedScenePreparationTool` | even finite-point schedule | Source Quality and scene identity, identified Prepared Scene |
| `ModelSurfaceEdgeExtractor` | `DeterministicModelSurfaceEdgeExtractionTool` | undirected topology ownership, boundary/crease angle, length, midpoint | model identity validation and model-edge artifact |
| `SceneSurfaceEdgeExtractor` | `DeterministicOrganizedSceneSurfaceEdgeExtractionTool` | row-major adjacency, inclusive height step, higher-endpoint anchor | complete-grid admission and scene-edge artifact |
| `SurfaceAndEdgeMatchScorer` | `DeterministicSurfaceEdgeCoverageTool` | one-way greedy unique-nearest edge coverage and RMSE | identity/frame validation, diagnostic evidence text, separate score artifact |

`DeterministicSurfaceEdgeCoverageTool` delegates to the existing shared
`DeterministicSurfaceCoverageTool`; it does not contain a second nearest-point
implementation. An empty scene-edge set is a valid zero-match result, matching
the established diagnostic contract.

## Immutable package provenance

| Item | Value |
| --- | --- |
| Library-Noah branch | `codex/surface-match-kernel` |
| Source commit | `46cfa0946bb4c23190b0dab75415ce2c637b4c41` |
| Package | `Lib.ThreeD 2.8.1` |
| Target | `netstandard2.0` |
| Vendored file | `third_party/LibraryNoah/Lib.ThreeD.2.8.1.nupkg` |
| Package SHA-256 | `3C908BB6671D2F89C7BC9DDEC601CD10A33A0905D78A8A24A276DA9BAAFF4445` |

The Noah source was committed before packing. Studio has no external
`ProjectReference` to a Noah checkout.

## Observable parity

The pre-migration Runner artifacts were captured before editing. After
installing the final package and rebuilding Studio, all 24 corresponding JSON
files were compared by file SHA-256 and remained exact (`24/24`). This covers
SurfaceModel, Prepared Scene, pose/execution, model edges, height and flat
scene edges, separate scores, overlays, assessments, and the retained
false-positive review.

Key unchanged artifact identities include:

- nominal SurfaceModel:
  `084EF0B6919673CB43817CA6ED50526BF20761B2D7FB0C609D8E35D28BB1A82B`;
- known-pose SurfaceModel:
  `A7211C538FB96C0464D2268A5DBF753F6D46F1D1721B71D2E48530BCB2561727`;
- full Prepared Scene:
  `F8E713B2DC044AF5304225F53495EF0565B71C9F291756C0D4474A5EE8672C30`;
- model-edge artifact:
  `47F5C3105B01E178D76EC60869BF3D4239F525D91E17588504F757E80A84F06C`;
- accepted separate score:
  `CDBD5B58DCE5949B08DBAEC88796E3B7242E5829AFDDA6B70B1F242598F43DF2`;
- flat-scene separate score:
  `EC491C5BBFEC0D703927E7688B82F176ABE67B9E317D0AE6083EF52222B809C0`.

## Verification

- Library-Noah Release build: `0` warnings, `0` errors.
- Library-Noah Smoke: `75/75`.
- Studio Release build: `0` warnings, `0` errors.
- package metadata/hash boundary: Pass.
- Studio Library-Noah package/bridge: `7/7`.
- SurfaceModel foundation: `22/22`.
- Surface matching foundation: `34/34`.
- Surface Match acceptance goldens: `14/14`.
- fixed-fixture performance budget: `18/18`.
- surface-edge matching: `21/21`.
- surface-edge diagnostics/review: `20/20`.
- Surface Match Workbench/Runner parity: `14/14`.
- surface-edge Workbench/Runner parity: `12/12`.
- surface-edge diagnostic/review Workbench parity: `13/13`.
- code structure and Noah ownership: `22/22`.
- exact before/after JSON files: `24/24`.

Evidence is under
`artifacts/current/20260801-noah-surface-preparation-edge-migration/`. The
repository path is a verified junction to
`D:\OpenVisionLab-TestData\OpenVisionLab-3D-Studio\artifacts\current\20260801-noah-surface-preparation-edge-migration`.

## Migration baseline change

The schema-1 decreasing baseline changes from `22` migration-debt files and
`6` reviewed Studio boundaries to `17` debt files and `11` reviewed
boundaries. The five migrated entry points now have zero numerical-signal
ceilings and explicit Noah Tool ownership. The structure guard adds a focused
check that these adapters keep calling the five Noah Tools and do not regain
their former arithmetic.

## Boundaries

- No UI, layout, theme, recipe, Viewer, or lifecycle behavior changed.
- No new acceptance rule or weighted surface/edge score was introduced.
- No acquisition direction, symmetry handling, multiple-match collection,
  metrology, or production-performance claim is included.
- Human-owner Wide/Compact R0 remains an external acceptance task and is not
  replaced by automated parity.
- The remaining `17` Studio calculation-debt files are not closed by this
  slice.

## Completion record

Status: Complete

Scope: Migrate the five active Surface Match preparation/edge calculation
owners to committed Library-Noah public Tools and retain Studio as a strict
product adapter/evidence boundary.

Acceptance criteria: exact Noah source is committed and vendored; Studio has
no duplicate execution arithmetic in the five entry points; package identity
passes; Runner and Workbench parity passes; pre/post persisted artifacts are
byte-identical; the decreasing migration baseline and structure guard are
updated.

Verification: Noah `0/0` and `75/75`; Studio `0/0`; focused Runner
`22/22`, `34/34`, `14/14`, `18/18`, `21/21`, `20/20`; Workbench parity
`14/14`, `12/12`, `13/13`; package `7/7`; structure `22/22`; exact artifacts
`24/24`.

Evidence: this document, the package/checksum pair, the schema-1 migration
baseline, Noah commit `46cfa0946bb4c23190b0dab75415ce2c637b4c41`, and
`artifacts/current/20260801-noah-surface-preparation-edge-migration/`.

Boundary / next dependency: continue the decreasing baseline with local-median
outlier filtering and leveling before introducing new `J-12` multiple-match
arithmetic. `J-12` must also begin in committed Noah source.
