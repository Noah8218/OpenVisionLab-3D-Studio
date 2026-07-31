# OpenVisionLab 3D Surface Match Overlay and Parity

Date: 2026-07-31

Status: Complete

Backlog scope: `J-10`, `J-16`

## Outcome

OpenVisionLab 3D Studio now turns one identified model-to-scene pose result
into linked, decision-free Viewer evidence. The same shared execution boundary
is used by the Runner verification path and the Workbench path, and the
known-pose fixture proves exact pose, coverage, overlay, and execution-hash
parity.

The Viewer shows:

- the Prepared Scene samples in a neutral evidence color;
- the complete transformed SurfaceModel wireframe in OpenVisionLab teal;
- model-to-scene correspondence lines in amber;
- coverage, RMSE, pose, and short model/overlay identities in one compact
  evidence panel;
- the complete model and overlay SHA-256 values through the panel tooltip;
- an explicit `View only · no Pass/Fail decision` boundary.

Clearing the Workbench evidence also clears the Viewer presentation. Showing
or clearing evidence does not edit the source, recipe, ROI, selected step, or
dirty state and does not execute Preview, Publish, Run, or Validation.

## Benchmark adaptation

Operator problem:

- raw pose and coverage files did not let an operator verify, in the same
  workspace, what model geometry was transformed and where it landed;
- separate Runner and Workbench execution paths could drift without an exact
  parity gate.

Abstract commercial lesson adapted:

- keep configuration, Viewer geometry, and numeric evidence linked;
- keep the dominant Viewer visually primary;
- expose evidence identity and the next interpretation boundary without
  adding another full control surface.

Independent OpenVisionLab result:

- the existing graphite/teal visual system, terminology, command row, dock
  layout, and responsive rail are preserved;
- no GoPxL theme, colors, proportions, topology, labels, assets, or icons were
  copied;
- the evidence panel is a small OpenVisionLab-specific scientific overlay,
  not a reconstruction of a competitor screen.

## Contract and ownership

### Core

`SurfaceMatchOverlayArtifact` schema `1.0`:

- semantics:
  `identified-transformed-surface-model-wireframe-v1`;
- links model, scene, and pose-result SHA-256 identities;
- retains the source SurfaceModel point order and triangle topology;
- transforms only point coordinates into the Prepared Scene frame;
- owns a canonical SHA-256.

`SurfaceMatchExecutionArtifact` schema `1.0`:

- semantics:
  `pose-coverage-identified-overlay-no-acceptance-v1`;
- contains the pose result and the identified overlay when matched;
- forbids an overlay for `NoMatch`;
- owns a canonical SHA-256 over the linked identities;
- contains no Pass/Fail policy.

`SurfaceMatchExecutionArtifactValidator` fails closed for schema, semantics,
identity, frame, unit, geometry, topology, pose, overlay, and hash
inconsistency.

### Data

`SurfaceMatchExecutionArtifactStore` validates before atomic JSON save and
after load. Malformed, unsupported, tampered, or inconsistent evidence is
rejected.

### Tools

`SurfaceMatchExecutor` is the single deterministic execution boundary:

```text
identified SurfaceModel
  + identified Prepared Scene
  + explicit bounded pose-search parameters
  -> pose result
  -> raw coverage evidence
  -> identified transformed-model overlay
  -> identified decision-free execution artifact
```

### Workbench and Viewer

The Workbench owns the selected execution evidence and raises explicit
display/clear requests. The display coordinator maps those presentation-only
requests to the Viewer. The Viewer owns display-frame mapping and rendering;
it does not calculate a second pose or coverage value.

When identified match evidence is visible, the empty-input first-use card is
suppressed so it cannot cover the scientific overlay. This is an adaptive
presentation rule only.

## Deterministic fixture evidence

Known pose:

- yaw: `30 degrees`;
- translation: `(10, -4, 2) mm`;
- coverage: `5/5 = 1.0`;
- inlier RMSE: `8.881784197001252E-16 mm`.

Identities:

```text
model     A7211C538FB96C0464D2268A5DBF753F6D46F1D1721B71D2E48530BCB2561727
scene     F8E713B2DC044AF5304225F53495EF0565B71C9F291756C0D4474A5EE8672C30
pose      BD0B428B72CAEAD91F3A993A6C6CDC2E91B5EE4BAF8C5D7FA250D93E104CEE0A
overlay   20D1712D4DDE764A1B7835CD919FB6615C25215CC1D50FBC8D9D49E53997627F
execution 0906A1D2F08D4F974756BD6C5E6A078B5B0CA43AABE20CC038D4966D070F96A5
```

Runner and Workbench independently call the shared executor with the same
identified inputs and parameters. Exact equality is required for pose SHA,
coverage count/ratio/RMSE, overlay SHA, and execution SHA.

## UI layout integrity

Checked current Release states:

| Size | State | Result |
| --- | --- | --- |
| Wide `1920 x 1040` | Teach, identified surface-match evidence | Pass |
| Compact `1280 x 760` | Teach, identified surface-match evidence | Pass |

Visual review explicitly checked:

- no overlapping controls;
- no clipped required task, status, evidence, or command text;
- no control outside its pane;
- no unreachable required control;
- no unintended horizontal or nested scroll bar;
- all five transformed fixture geometries remain inside the Viewer;
- the evidence panel does not cover the model and has enough space for every
  required label;
- short hashes remain secondary and their full values are available through
  the panel tooltip;
- no empty-input card covers identified evidence.

Before:

- `artifacts/current/20260731-surface-match-overlay-parity/before/wide-teach-before.png`;
- `artifacts/current/20260731-surface-match-overlay-parity/before/compact-teach-before.png`.

After:

- `artifacts/current/20260731-surface-match-overlay-parity/after/wide-teach-after.png`;
- `artifacts/current/20260731-surface-match-overlay-parity/after/compact-teach-after.png`.

Both final screenshot quality reports accepted attempt `1`.

## Verification

Commands and results:

| Check | Result |
| --- | --- |
| Release solution build | Pass, `0` warnings / `0` errors |
| Surface matching and overlay verification | Pass, `34/34` |
| Workbench/Runner parity | Pass, `10/10` |
| SurfaceModel regression | Pass, `22/22` |
| Source-channel and dense-normal regression | Pass, `26/26` |
| Source Quality regression | Pass, `18/18` |
| Workbench docking/layout regression | Pass, `76/76` |
| Inspection Workspace regression | Pass, `63/63` |
| Height distribution regression | Pass, `25/25` |
| Code structure | Pass, `17/17` |
| Wide R0 package `-ValidateOnly` | Pass; no application launched |
| Compact R0 package `-ValidateOnly` | Pass; no application launched |

Reusable evidence:

- `artifacts/current/20260731-surface-match-overlay-parity/`;
- `surface-match-runner-verification.txt`;
- `surface-match-workbench-parity.txt`;
- `known-pose.surface-match-execution.json`;
- regression reports, current-build before/after captures, quality reports,
  and R0 validation-only logs in the same folder.

## Boundaries

This closure does not prove or add:

- match Pass/Fail acceptance limits;
- authored search-domain UI;
- runtime budgets or production performance;
- multiple-match or symmetry handling;
- physical calibration, uncertainty, traceability, or metrology;
- camera, reconstruction, PLC, robot, cloud, or production-line integration;
- human-owner Wide/Compact R0 acceptance.

Automated `-ValidateOnly` protects the fixed package but does not replace the
product owner's unaided operation.

## Completion record

Status: Complete

Scope: Identified transformed-model overlay, fail-closed execution artifact
and persistence, shared Runner/Workbench execution boundary, Viewer-linked
pose/coverage/hash evidence, presentation-only clear routing, and supported
Wide/Compact layout evidence.

Acceptance criteria: transformed complete model linked to model/scene/pose
identities -> Pass; exact Workbench/Runner pose/coverage/overlay/execution
parity -> Pass; display and clear preserve recipe/source/ROI/execution
contracts -> Pass; Wide/Compact overlap and required-text clipping review ->
Pass; affected regression and fixed-package gates -> Pass.

Verification: Release `0/0`; matching `34/34`; parity `10/10`; SurfaceModel
`22/22`; source/normal `26/26`; Source Quality `18/18`; docking `76/76`;
Inspection Workspace `63/63`; height distribution `25/25`; structure
`17/17`; final Wide/Compact current-build captures accepted on attempt `1`;
both R0 `-ValidateOnly` modes passed.

Evidence:
`docs/OPENVISIONLAB_3D_SURFACE_MATCH_OVERLAY_AND_PARITY_20260731.md` and
`artifacts/current/20260731-surface-match-overlay-parity/`.

Boundary / next dependency: `J-11/J-14/J-15/M-16` must separately define
authored acceptance/search/rejection/timing/golden evidence. Human-owner R0
remains external for `A-01`.
