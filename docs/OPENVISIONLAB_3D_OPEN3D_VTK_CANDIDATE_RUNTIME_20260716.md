# Open3D Runtime On Controlled VTK Candidate - 2026-07-16

## Scope And Decision

This is a local supply-chain compatibility checkpoint for the separate-process Open3D registration candidate. It does not add Open3D or VTK to the product, approve redistribution, prove historical VTK byte identity, establish physical measurement accuracy, or prove Viewer/Runner registration parity.

**Decision:** the same Open3D `0.19.0` source configuration can use the controlled VTK `9.1.0` Release candidate through `USE_SYSTEM_VTK=ON`. The resulting Open3D install, public DLL contract, DemoICP behavior, controlled invalid-input behavior, and robustness behavior match the independent clean Open3D build within the documented elapsed-time exclusion.

## Inputs And Configuration

| Input | Evidence |
| --- | --- |
| Open3D source | `v0.19.0`, commit `1e7b17438687a0b0c1e5a7187321ac7044afe275` |
| VTK candidate | Controlled no-patch VTK `9.1.0` Release candidate from `docs/OPENVISIONLAB_3D_VTK_CONTROLLED_REBUILD_20260716.md` |
| Generator | Visual Studio 17 2022, x64, v143, MSVC `19.44.35227.0` |
| Runtime model | Shared Open3D DLL, dynamic CRT, OpenMP enabled |
| Candidate switch | `USE_SYSTEM_VTK=ON`, `VTK_DIR=.../compare/candidate/vtk/lib/cmake/vtk-9.1` |

All non-VTK Open3D options match the independent clean non-GUI Release configuration: GUI, WebRTC, Python, examples, unit tests, benchmarks, CUDA, SYCL, ISPC, IPP, sensor integrations, TensorFlow, PyTorch, and bundled ML remain disabled.

## Build And Install Contract

`cmake --build ... --config Release --target install` completed with exit code `0`.

| Check | Candidate Result |
| --- | --- |
| Installed file paths | `873 / 873` match the clean Open3D install; no candidate-only or clean-only path |
| Exact shared file hashes | `870 / 873` |
| Expected hash differences | `bin/Open3D.dll`, `bin/tbb12.dll`, `CMake/Open3DTargets.cmake` |
| Candidate `Open3D.dll` | `58,220,032` bytes, SHA-256 `88AB8EE38F218C9BA02A929F07462554D555E0C131D771A5343777445EC2E19F` |
| Candidate `tbb12.dll` | `328,704` bytes, SHA-256 `2785A4688DCB2AA197A49B4BE7A94B471A1FB951BD8EFE46476F5BA95AD449E3` |
| Dynamic dependency contract | `29 / 29` exact ordered entries versus clean build |
| DLL export contract | `16,000 / 16,000` exact ordinal/name entries versus clean build |

The changed DLL hashes are expected from a fresh Open3D build linked with the controlled VTK candidate. The export and dependency contracts provide stronger compatibility evidence than file size alone.

## Registration Behavior

| Check | Result |
| --- | --- |
| Independent registration probe | Builds against the candidate `Open3DConfig.cmake` with exact version `0.19.0` |
| DemoICP `0 -> 1` | `3 / 3` candidate JSON results exactly match clean output after removing elapsed milliseconds |
| DemoICP metrics | `123,483` refined correspondences, fitness `0.621032514396`, RMSE `0.006565226689` |
| DemoICP `1 -> 2` | Controlled exit `1`, no output report, `Target point cloud contains a non-finite normal.` diagnostic |
| Robustness input matrix | `11` cases x `3` runs = `33 / 33` probe JSON results exactly match clean output after removing elapsed milliseconds |
| Robustness verification | `33 / 33` acceptance-verification JSON results exactly match clean output |

The predeclared acceptance policy intentionally has only `15` passing and `18` failing runs. The failures are the known baseline outcomes for `bad-initial`, `combined`, `noise-high`, and the three overlap cases; candidate behavior matches that baseline exactly. The check is parity, not an assertion that all predeclared registration expectations are valid.

## Evidence

Ignored generated evidence is under `artifacts/o3d-vtk-candidate-20260716`:

```text
b/CMakeCache.txt
i/
probe-build/Release/open3d-registration-probe.exe
install-contract-vs-clean.json
open3d-dll-contract-vs-clean.json
probe-results/demo-current/candidate-vs-clean-parity.json
probe-results/demo-current/candidate-1-to-2-controlled-failure.json
probe-results/robustness/candidate-vs-clean-robustness-parity.json
```

## Remaining Limits

1. The VTK candidate is Release-only and omits the legacy Debug payload.
2. VTK and Open3D DLL hashes differ from the historical packages; this is not historical byte reproduction.
3. A clean Windows host VC/OpenMP install, restart behavior, servicing, redistribution rights, notices, and owner/legal approval remain unproven.
4. No Open3D dependency, PCD loader, fixed sample, product adapter, or Viewer/Shell UI was added.
5. Viewer/Runner registration result mapping, arbitrary registration recovery, physical calibration, uncertainty, and metrology claims remain open.
