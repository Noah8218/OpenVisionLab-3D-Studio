# OpenVisionLab 3D Validation Set v1

Date: 2026-07-23

Historical note: this v1 raw-measurement-only boundary is superseded by
`docs/OPENVISIONLAB_3D_ORDERED_GRAPH_VALIDATION_20260723.md`. Keep this document
as the first Validation Set UI/lifecycle evidence, not as current execution
coverage.

## Decision

`Validation Set` is the dockable repeat-inspection workspace that follows
teaching. It is not a Thickness or Warpage workspace and it does not create a
second recipe lifecycle.

The operator workflow is:

1. open or teach one Inspection Recipe;
2. add an explicit ordered list of C3D validation samples;
3. choose `Run all`;
4. review one row per sample;
5. select a failed/error row and inspect its ordered step evidence.

Adding or selecting a validation sample never changes the authored recipe or
the current 3D Viewer input. Execution is explicit and sequential. A failed
sample does not discard later sample evidence.

## Current executable boundary

`ToolRecipeValidationSetExecution` owns the fail-closed execution contract.
For every selected sample it:

- verifies that the file exists;
- reads the current C3D byte/grid identity;
- requires the taught and validation samples to have the same grid size;
- creates an ephemeral recipe copy with the new source byte identity;
- rebinds only source-owned C3D GridRectangle selections;
- invokes the existing typed measurement adapter in authored order;
- preserves `Pass`, `Fail`, and `Error` independently for every sample;
- leaves the original `ToolRecipeDocument` unchanged.

Validation Set v1 currently executes only recipe steps that consume the
verified raw C3D source directly and are already covered by the raw-height
measurement adapter. The visible workspace remains algorithm-neutral, but the
current executable adapter coverage is the raw-source `thickness` and
`warpage` tool IDs.

It deliberately rejects Filter, feature extraction, line fitting,
intersection, correspondence, affine, Re-grid, and A3-owned measurement graphs
for per-sample replay. The product has typed adapters for those authored
slices, but it does not yet have one general fresh-sample whole-graph Runner.
The UI shows this as execution coverage instead of fabricating a result.

## UI contract

- The workspace is the `Validation Set` tab inside the dockable
  `Pipeline / Validation` pane.
- `Add samples`, `Run all`, and `Clear list` use familiar WPF UI icons, visible
  text, tooltips, and accessible names.
- The left table shows sample order, file, localized status, duration, and
  summary evidence.
- Selecting a row shows ordered tool evidence on the right.
- After execution, the first `Fail` or `Error` row is selected automatically.
- Korean and English labels use the shared OpenVisionLab language service.
- The file picker is the only View code-behind bridge.

## Verification

Current Debug commands:

```powershell
dotnet build "OpenVisionLab.ThreeDStudio.slnx" -c Debug

dotnet run --no-build `
  --project src\OpenVisionLab.ThreeD.Shell\OpenVisionLab.ThreeD.Shell.csproj `
  -c Debug -- `
  --verify-validation-set `
  artifacts\current\20260723-validation-set-v1\validation-set-verification.txt

dotnet run --no-build `
  --project src\OpenVisionLab.ThreeD.Shell\OpenVisionLab.ThreeD.Shell.csproj `
  -c Debug -- `
  --verify-tool-recipe-teaching `
  artifacts\current\20260723-validation-set-v1\recipe-teaching-regression.txt

dotnet run --no-build `
  --project src\OpenVisionLab.ThreeD.Shell\OpenVisionLab.ThreeD.Shell.csproj `
  -c Debug -- `
  --verify-workbench-docking `
  artifacts\current\20260723-validation-set-v1\docking-regression.txt
```

Results:

- solution build: `0 warnings / 0 errors`;
- Validation Set: `10/10`;
- recipe teaching regression: `25/25`;
- docking regression: `26/26`;
- actual current EXE Korean screenshot quality: pass, attempt 1;
- actual current EXE English screenshot quality: pass, attempt 1.

The deterministic fixture contains:

- one same-grid passing sample;
- one same-grid out-of-tolerance sample;
- one grid-mismatch error sample.

The evidence proves that all three complete, later evidence survives the
failure, the aggregate preserves the error, and the authored source path/hash
remain unchanged.

## Evidence

- `artifacts/current/20260723-validation-set-v1/before-ko.png`
- `artifacts/current/20260723-validation-set-v1/after-ko.png`
- `artifacts/current/20260723-validation-set-v1/after-en.png`
- `artifacts/current/20260723-validation-set-v1/validation-set-verification.txt`
- `artifacts/current/20260723-validation-set-v1/recipe-teaching-regression.txt`
- `artifacts/current/20260723-validation-set-v1/docking-regression.txt`
- `artifacts/current/20260723-validation-set-v1/validation-set-fixture/`

## Claim boundary

This is bounded local software evidence. It is not an arbitrary recipe graph
Runner, parallel/batch production infrastructure, folder watching, SPC,
physical calibration, sensor fidelity, Gauge R&R, or metrology evidence.

The next implementation gate is a general per-sample ordered recipe executor
that resolves fresh sample outputs through Filter -> feature/datum -> full XYZ
affine -> A3 -> measurement without weakening typed input/output identity,
explicit authoring Preview/Publish, or Run Record evidence.

## Durable completion record

```text
Status: Complete
Scope: Dockable bilingual Validation Set v1 with explicit sample selection, sequential execution, per-sample Pass/Fail/Error rows, selected-step evidence, and fail-closed same-grid raw-source adapter coverage.
Acceptance criteria: Explicit sample list -> pass; explicit Run all -> pass; per-sample result/evidence -> pass; later evidence after Fail -> pass; original recipe unchanged -> pass; unsupported graph rejected -> pass; Korean/English current-EXE UI -> pass.
Verification: Debug solution build 0/0; Validation Set 10/10; recipe teaching 25/25; docking 26/26; Korean/English screenshot quality attempt 1.
Evidence: docs/OPENVISIONLAB_3D_VALIDATION_SET_V1_20260723.md and artifacts/current/20260723-validation-set-v1/.
Boundary / next dependency: General fresh-sample whole-graph replay and real calibrated multi-piece evidence are not implemented or proven.
```
