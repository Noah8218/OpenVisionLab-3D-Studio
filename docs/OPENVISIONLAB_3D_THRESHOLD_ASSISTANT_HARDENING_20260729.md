# Threshold Assistant Evidence Hardening

Date: 2026-07-29

Status: Complete for `I-12`, `I-13`, and `I-15`

## Product-direction source

This slice is part of the approved OpenVisionLab development direction, not an
isolated UI enhancement. Good and Bad observations may suggest limits, but the
operator must see whether the sample evidence is credible and must explicitly
review and apply any change. The implementation preserves OpenVisionLab's
stronger Held-out separation and explicit execution lifecycle instead of
copying vendor-specific UI or automatic acquisition.

The complete video-derived direction remains:

1. source identity and quality;
2. linked Height Image and 3D teaching;
3. typed preparation and regions;
4. deterministic inspection and threshold evidence;
5. completeness/presence;
6. surface matching with explainable pose, score, and diagnostics;
7. durable Run Record and repeatable Runner evidence.

Camera control, stereo reconstruction, PLC/robot/fieldbus, cloud/plant
management, implicit execution, and unverified physical-metrology claims
remain out of scope.

## Completed scope

### I-12 evidence warnings

The shared threshold report contract is now `2.0`. Each warning stores:

- warning kind;
- optional step-metric scope;
- owner step ID and name;
- metric and unit;
- exact Good and Bad development-sample counts;
- exact development-sample identities;
- deterministic operator-facing message.

Supported warning kinds are:

- missing Good;
- missing Bad;
- insufficient Good;
- insufficient Bad;
- imbalanced Good/Bad counts;
- distributions that no supported Minimum, Maximum, or Range candidate can
  separate.

The repeat-evidence minimum is two Good and two Bad samples. Held-out samples
never enter warning counts, identities, candidate boundaries, ranking,
confusion counts, or decisions.

Warnings are emitted only for step metrics owned by the explicit
Thickness/Warpage threshold-assistant coverage. ROI raw-height distributions
remain visible evidence but do not produce misleading parameter-assistant
warnings.

The normal Workbench surface shows a concise warning summary. The complete
typed warning records remain available in the Runner JSON contract.

### I-13 first-tool coverage

The fail-closed mapping matrix is now a published shared contract:

| Tool | Metric | Candidate | Typed parameter |
| --- | --- | --- | --- |
| Thickness | Mean | Minimum | `MinimumThickness` |
| Thickness | Mean | Maximum | `MaximumThickness` |
| Thickness | Mean | Range | `MinimumThickness`, `MaximumThickness` |
| Warpage | PeakToValley | Maximum | `MaximumPeakToValley` |
| Warpage | Rms | Maximum | `MaximumRms` |

Scope, tool ID, metric name, and limit kind must match exactly. Display-name
or fuzzy matching is forbidden. Region metrics, Warpage minimum/range
candidates, and every undeclared tool/metric combination fail closed.

### I-15 explicit lifecycle guards

The focused verification proves:

- staging and role changes leave every sample `Pending`;
- role changes affect the validation-set sidecar, not the recipe graph;
- role changes do not create distributions or threshold candidates;
- candidate selection and Review are view-only;
- Review Cancel changes no recipe, PropertyGrid, or execution state;
- candidate Apply changes only the typed PropertyGrid draft;
- manual edits and ordinary PropertyGrid Apply do not run development or
  Held-out samples;
- development revalidation and Held-out replay remain separate explicit
  commands;
- reopen restores roles and evidence without executing inspection.

No new automatic Preview, Publish, Run, save, candidate Apply, development
replay, or Held-out replay path was introduced.

## Controlled evidence

The deterministic Thickness fixture covers:

- balanced: Good `2, 4`, Bad `-10, 20`, Held-out `3`;
- missing Bad: one Good plus one Held-out;
- imbalanced: two Good plus one Bad;
- inseparable: Good `2, 4`, Bad `3, 5`.

Results:

- balanced report: `48` candidates, `4` development, `1` Held-out excluded,
  `0` evidence warnings;
- missing-Bad report: `0` candidates, `1` development, `1` Held-out excluded,
  `3` typed warnings;
- imbalanced report: candidates remain visible with explicit imbalance and
  insufficient-Bad warnings;
- inseparable report: explicit overlap warning;
- Runner report schema `1.1`: threshold contract `2.0`, Warning, one Good and
  one Bad, two exact SHA-256 identities, one Held-out excluded, and the same
  five-entry mapping matrix.

## Verification

Commands actually run:

```powershell
dotnet build "OpenVisionLab.ThreeDStudio.sln" -c Release -p:Platform="Any CPU"

OpenVisionLab.ThreeD.Shell.exe `
  --verify-validation-set `
  artifacts/current/20260729-threshold-assistant-hardening/validation-set.txt

OpenVisionLab.ThreeD.Runner.exe `
  --labeled-validation-recipe <controlled-recipe> `
  --report `
  artifacts/current/20260729-threshold-assistant-hardening/runner-insufficient-evidence.json
```

Results:

- Release build: `0` warnings, `0` errors;
- Validation Set focused verification: `72/72`;
- Inspection Workspace selection: `63/63`;
- Recipe Manager / typed PropertyGrid: `37/37`;
- Shell smoke command-line options: `24/24`;
- code structure: `17/17`;
- Runner: report schema `1.1`, threshold contract `2.0`, two typed
  insufficient-repeat warnings, two exact development identities, Held-out
  excluded, five mappings;
- current-Release Wide and Compact screenshot quality: accepted on attempt
  `1`;
- `git diff --check`: pass.

## UI evidence

- `before-insufficient-evidence.png`: fresh current-Release baseline captured
  before implementation; it shows only a generic no-Good/Bad-pair message.
- `after-insufficient-evidence-wide.png`: the same Good `1` / Bad `0` /
  Held-out `1` set now reports `3 evidence warning(s)` and names missing Bad
  plus insufficient Good evidence.
- `after-insufficient-evidence-compact.png`: the same warning count remains
  visible in the Compact layout.

All captures contain only the application window.

## Evidence

- `artifacts/current/20260729-threshold-assistant-hardening/validation-set.txt`;
- `artifacts/current/20260729-threshold-assistant-hardening/runner-insufficient-evidence.json`;
- `artifacts/current/20260729-threshold-assistant-hardening/runner-console.txt`;
- `artifacts/current/20260729-threshold-assistant-hardening/inspection-workspace.txt`;
- `artifacts/current/20260729-threshold-assistant-hardening/recipe-manager-wpg.txt`;
- `artifacts/current/20260729-threshold-assistant-hardening/shell-smoke-options.txt`;
- `artifacts/current/20260729-threshold-assistant-hardening/code-structure.txt`;
- `artifacts/current/20260729-threshold-assistant-hardening/before-insufficient-evidence.png`;
- `artifacts/current/20260729-threshold-assistant-hardening/after-insufficient-evidence-wide.png`;
- `artifacts/current/20260729-threshold-assistant-hardening/after-insufficient-evidence-compact.png`.

## Completion record

Status: Complete

Scope: `I-12` typed evidence warnings, `I-13` explicit
Thickness/Warpage mapping coverage, and `I-15` no-auto-run/no-auto-apply
guards.

Acceptance criteria: deterministic missing/balance/overlap warnings -> pass;
exact development counts/identities and Held-out exclusion -> pass; published
Thickness/Warpage coverage and unsupported fail-closed behavior -> pass;
Workbench/Runner shared contract -> pass; explicit lifecycle guards -> pass;
fresh before/after UI evidence -> pass.

Verification: Release build `0/0`; Validation Set `72/72`; Inspection
Workspace `63/63`; Recipe Manager/PropertyGrid `37/37`; Shell options
`24/24`; structure `17/17`; Runner schema `1.1`/threshold contract `2.0` and
UI captures pass.

Evidence:
`artifacts/current/20260729-threshold-assistant-hardening/`.

Boundary / next dependency: this does not approve production tolerances,
generic threshold mappings, physical calibration, traceability, uncertainty,
GR&R, or metrology. `L-11 threshold-correction evidence in Run Record` is the
next implementation priority. Owner R0 remains an external acceptance gate.
