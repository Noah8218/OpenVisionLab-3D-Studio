# PL-0038 Coherent Proven-Decoder Import Surface

Status: Complete

## Scope and product boundary

OpenVisionLab 3D Studio now has one always-reachable Import command in the 3D
Viewer toolbar and empty state. Its native file dialog exposes exactly `C3D`,
`GLB`, `STL`, `LAS`, and `LAZ`.

- `C3D` retains the existing cancellable recipe-source load and binding path.
- `GLB`, `STL`, `LAS`, and `LAZ` decode outside the UI thread, apply only after
  success, and are marked `Viewer only` / `Viewer 전용` with the retained
  recipe-input boundary.
- Editing visibility or importing Viewer-only data does not invoke Preview,
  Publish, Run, or result creation.
- Failure or cancellation retains the last successful Viewer source and the
  recipe source, step count, and dirty state.
- `.gltf` external-resource files, OBJ, PCD, XYZ, TIFF, RAW, camera, PLC,
  cloud, and physical-metrology claims remain outside this closure.

This is the smallest coherent surface over the existing decoders, ViewModel
loading state, progress ribbon, cancellation command, and Viewer. It adds no
second import framework or algorithm owner.

## Root cause and implementation

The decoders existed, but operator access was split across C3D recipe loading,
Viewer sample paths, smoke paths, and fixed comparison paths. The Shell had no
single truthful action describing which files become recipe input and which
are display-only.

| Owner | Change |
| --- | --- |
| `ViewerWorkspaceView` | Always-reachable semantic import button, empty-state action, and visible Viewer-only source marker |
| `ToolWorkbenchViewModel.SourceLoading` | Separate general Import command, shared progress/cancel state, and retained successful Viewer-only summary |
| `ShellRequestCoordinator` / `ShellWorkbenchLifecycleController` | One exact native filter, C3D recipe routing, Viewer-only routing, and failure/cancel retention |
| `OpenVisionThreeDViewerControl.Data` | Async GLB/STL/LAS/LAZ decode and success-only scene application |
| `GlbMesh` / `StlMesh` | Compatible cancellable/progress overloads with bounded cancellation checks |
| `ThreeDLocalization` | Korean/English command, limitation, progress, cancellation, and Viewer-only text |

## Acceptance evidence

Evidence root:
`D:\OpenVisionLab-TestData\OpenVisionLab-3D-Studio\artifacts\current\20260823-pl0038-import-surface`

The actual EXE used the dynamically selected smaller left monitor. Runtime
reports record physical monitor bounds `-2400,456,0,1806`, working area
`-2400,456,0,1746`, and intersecting application windows. The available scale
was 125%.

### Decoder and state behavior

| Input | SHA-256 | Current evidence |
| --- | --- | --- |
| 4096 x 4096 synthetic C3D | `F3BFCA5730C2AE5F3DA66EA1BB1D38D00A12CA7E33778F5615A1F6B1675B4B22` | `runtime/c3d-medium-report.txt`: pass, 85 dispatcher ticks, 100%, recipe binding complete |
| BoxTextured GLB | `2D055D2D56A492D1B9302DE6E733046B625CF51E5F2A3090BD3A8C11ACC93C17` | `runtime/glb-final-report.txt`: success plus failure/cancel retention pass |
| 3DBenchy STL | `6AB57F1C3F8E86BC3CBD302C6FA6270ACF06277C6335454E922419C25D42E97E` | `runtime/stl-final-report.txt`: success plus failure/cancel retention pass |
| interesting LAS | `505E6A78E20B97CFD56ADE899686E1882C5C89CBA5598AAA75CB485147947130` | `runtime/las-final-report.txt`: success plus failure/cancel retention pass |
| xyzrgb manuscript LAZ | `255569B7AE9FCE1FA98E0FD55F7FA887EA402FBD4EC2EE7989E4384FD984B26F` | `runtime/laz-final-report.txt`: success plus failure/cancel retention pass |

Every Viewer-only report records unchanged recipe source, zero step-count
change, unchanged dirty state, `failurePreserved=True`,
`cancellationPreserved=True`, and no Preview/Publish/Run.

### UI runtime states

| State | Evidence |
| --- | --- |
| Compact English actual pointer-down | `ui/import-button-pressed-compact-en-final.png` and quality report; OS injection, held `IsPressed`, no fallback |
| Wide Korean active progress / Import disabled / Cancel visible | `ui/import-progress-disabled-wide-ko-final.png` and quality report |
| Wide Korean successful textured GLB | `ui/after-glb-wide-ko-final.png`; full Viewer-only marker and unchanged recipe text visible |
| Compact English successful LAZ | `ui/after-laz-compact-en-final.png`; full Viewer-only marker and unchanged recipe text visible |
| Compact Korean native dialog and open filter | `ui/import-dialog-compact-ko.png`, `ui/import-filter-popup-compact-ko.png`, and runtime report |
| Wide English native dialog and open filter | `ui/import-dialog-wide-en.png`, `ui/import-filter-popup-wide-en.png`, and runtime report |

The native filter popup contains five complete entries. Its combined entry
shows all five format names before the native dialog trims the redundant
appended wildcard suffix. The individual C3D, GLB, STL, and LAS/LAZ entries are
fully visible. Normal, actual pointer-down/pressed, disabled during progress,
cancel-enabled, loaded Viewer-only, and open-popup states were exercised.
Keyboard focus is provided by the native dialog and focused WPF button path;
mouse-leave recovery follows the already-qualified shared button style.

Only the workstation's available 125% scale was run. The code does not claim
runtime proof for 100%, 150%, 175%, or 200% on this machine.

## Focused verification already passed

- Source-channel/normal/import regression: `39/39`.
- Import-surface ViewModel contract: `8/8`.
- Shell smoke option parsing/routing: `46/46`.
- Focused Shell Release build: 0 warnings, 0 errors.

## Final repository gates

- `dotnet build OpenVisionLab.ThreeDStudio.slnx -c Release --no-restore`:
  0 warnings, 0 errors.
- `scripts/verify-code-structure.ps1`: `68/68`.
- `scripts/verify-vision-sdk-package.ps1`: package ID/version, source commit,
  SHA-256, and target framework pass.
- `git diff --check`: no whitespace errors; Git reported only the repository's
  existing LF-to-CRLF conversion warnings.

## Durable closure record

Status: Complete
Scope: PL-0038 exact C3D/GLB/STL/LAS/LAZ Import surface and truthful recipe/Viewer boundaries
Acceptance criteria: C1-C6 pass with evidence `PL-0038#E1` through `PL-0038#E5`
Verification: focused checks, actual five-format EXE and Wide/Compact runtime UI, full Release solution, structure 68/68, SDK package identity, and diff hygiene pass
Evidence: this document, `.proofline/issues/PL-0038.json`, and the D-backed evidence root
Boundary / next dependency: owner R0, representative maximum C3D qualification, and unadvertised decoder formats remain separate; runtime DPI proof on this workstation is limited to available 125%
