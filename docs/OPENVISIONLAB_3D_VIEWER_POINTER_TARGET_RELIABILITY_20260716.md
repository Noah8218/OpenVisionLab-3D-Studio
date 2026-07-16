# Viewer Pointer Target Reliability - 2026-07-16

Status: current-source local evidence for the deterministic WPF pointer-input smoke. This strengthens the fixed-scope Phase 1 regression baseline; it does not change the Phase 2 or Phase 3 decision.

## Decision

A valid WPF screenshot does not prove that native Windows input reached the Viewer. The smoke must prove the actual pointer target before each gesture, while preserving real OS pointer delivery rather than substituting synthetic ViewModel state or posted WPF events.

Windows can restrict foreground transfer. The smoke therefore uses the smallest native sequence that works in this desktop session: it asks Windows to show/raise the host, temporarily shares the foreground input queue with the Viewer UI thread, activates/focuses the host, then detaches the queue in `finally`. This behavior is reachable only from the private `--smoke-pointer-input-report` path. Normal Viewer and Shell interaction do not call it. Microsoft documents both the foreground restriction and the temporary input-queue contract in [SetForegroundWindow](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setforegroundwindow) and [AttachThreadInput](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-attachthreadinput).

## Implementation

- `WindowsPointerInput` normalizes both the hit-tested HWND and the Viewer host HWND to their `GA_ROOT` ancestors before comparing them.
- `OpenVisionThreeDViewerControl` prepares the pointer target before the click, orbit, pan, and wheel sequences. A three-attempt mismatch records requested/cursor coordinates, host bounds, visibility/enabled state, target/root HWNDs, and fails before counted input begins.
- The smoke-only preparation uses `SetWindowPos`, `BringWindowToTop`, `SetForegroundWindow`, `SetFocus`, and a short `AttachThreadInput`/detach scope. The actual acceptance input remains native `mouse_event` delivery.
- The change remains in the Viewer WPF/rendering boundary. ViewModels continue to own camera, selection, and presentation state.

## Evidence

1. The initial hosted Shell smoke recorded zero mouse events while its screenshot-quality report passed. This proved that a screenshot alone cannot prove routed input: `artifacts/viewer_reliability_reassessment_20260716/pointer/shell_pointer.txt`.
2. The diagnostic run recorded a visible/enabled Viewer whose requested point `(714,521)` lay inside host bounds `(104,104)..(1384,904)`, while `WindowFromPoint` resolved to the Chrome root. The early guard correctly failed closed instead of reporting a Viewer interaction failure as a pass: `artifacts/viewer_reliability_reassessment_20260716/pointer_zorder_request/viewer_pointer.txt`.
3. The final current-source build passes all ten repeated real-input runs: standalone Viewer `5/5` and hosted Shell `5/5`. Every report records `Result|pass=True`, routed events, cube pick, orbit, pan, zoom, and a first-attempt accepted screenshot: `artifacts/viewer_reliability_reassessment_20260716/pointer_attached_repeat`.
4. The final fixed data/loading/interaction/Shell matrix passes `128/128` with zero failures, including controlled missing/corrupt GLB/STL/LAZ paths: `artifacts/viewer_reliability_reassessment_20260716/matrix_after_attached_input/matrix_smoke_summary_after.txt`.
5. The current `0.1.1-dev` DLL-only BinaryHost passes `ProjectReference=0`, manifest `13/13`, required outputs `12/12`, Host API commands `3/3`, and C3D pick runtime exit `0`: `artifacts/viewer_reliability_reassessment_20260716/binary_host_after_attached_input/viewer-binary-host-report.txt`.

## Reproduction

```powershell
dotnet build OpenVisionLab.ThreeDStudio.slnx -c Debug --nologo
powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts\run-data-loading-matrix-smoke.ps1 -ArtifactDir artifacts\viewer_reliability_reassessment_20260716\matrix_after_attached_input -SkipBuild
powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts\verify-viewer-dll-host.ps1 -Configuration Debug -ArtifactDirectory artifacts\viewer_reliability_reassessment_20260716\binary_host_after_attached_input -NoRestore
```

## Claim Boundary

This proves repeatable real pointer behavior for the fixed Viewer and Shell smoke hosts in the current local desktop session, alongside the supported fixed file matrix and DLL Host boundary. It does not guarantee every third-party desktop, virtual-desktop, session, monitor/DPI arrangement, arbitrary geometry, geometric correctness, or physical/metrology accuracy.
