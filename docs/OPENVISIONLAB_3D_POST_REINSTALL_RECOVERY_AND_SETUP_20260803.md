# Post-reinstall Recovery and Setup Closure - 2026-08-03

Status: Complete

## Scope

Recover the current development/verification prerequisites, repair the tracked
public-sample recipe contracts exposed by the post-reinstall audit, document
the operator/developer utility boundary, and prove a self-contained Windows
operator package.

No Library-Noah algorithm, Studio execution arithmetic, Viewer rendering
policy, or explicit Preview/Publish/Run contract changed.

## Completed work

- Installed Python `3.13.14` from the fixed official winget package ID
  `Python.Python.3.13`.
- Added `scripts/setup-development-environment.ps1` with explicit `-CheckOnly`
  and `-InstallMissing` modes. Both modes report `Ready`, `5/5` required
  checks, on the restored workstation.
- Split operator runtime requirements from source-build and verification tools
  in `README.md` and
  `docs/OPENVISIONLAB_3D_SYSTEM_REQUIREMENTS_AND_SETUP.md`.
- Added `scripts/publish-windows-app.ps1`. It produces a folder-based
  `win-x64` self-contained Shell package with the matching .NET runtime, valid
  public examples, tracked recipes, license/notice, setup documentation, and a
  per-file SHA-256 manifest.
- Recovered the six tracked recipe expectations against public Thickness
  Coupon SHA-256
  `D879FC9E40678762214E8C3FBEA01F5C9A309701DAAEAD448067E563C5B502F8`.
  Preserve the detailed root-cause record in
  `OPENVISIONLAB_3D_PUBLIC_SAMPLE_RECIPE_CONTRACT_RECOVERY_20260803.md`.
- Recovered all ten historical `artifacts/current` Junctions after confirming
  that the restored data volume moved from `D:` to `E:`. The repository links
  now target the existing directories under
  `E:\OpenVisionLab-TestData\OpenVisionLab-3D-Studio\artifacts\current`;
  no historical artifact was regenerated or copied.

## Acceptance criteria and verification

| Criterion | Result |
| --- | --- |
| Restored development environment | Pass: Windows, .NET SDK, Git, Python 3.13, and PowerShell `5/5`; winget ready |
| Current-source Release solution | Pass: `0` warnings, `0` errors |
| Six tracked expected-status recipe smokes | Pass: Height Deviation/Plane Flatness deliberate Fail; Point Pair/Gap-Flush/Volume/Cross-section Pass |
| Focused Runner goldens | Pass: Plane Flatness, Point Pair, Gap/Flush, Volume, Cross-section, map fidelity, Library-Noah bridge |
| Shell regression | Pass: height-measurement `54/54`, Validation Set `84/84`, docking `78/78`, command-line `28/28` |
| Vendored package and structure gates | Pass: Library-Noah, WPG, structure `29/29` |
| Python 3.13 gates | Pass: verifier self-test `4/4`, NuGet `12` projects with `0` vulnerable/deprecated, independent C3D PLY and signature |
| Self-contained package | Pass: `501` manifest payload files, `0` hash/size failures, `11` recipe source references with `0` missing, `0` corrupt fixtures, `230.46 MiB` |
| Self-contained launch without system .NET root | Pass: invalid `DOTNET_ROOT`, multilevel lookup disabled, Shell window ready |
| Current package Wide/Compact | Pass: `1920 x 1040` and `1280 x 760` accepted; no unexplained overlap or required-text clipping |
| Active leftmost monitor placement | Pass: `DISPLAY1`, bounds `0,0,1920,1080`, window `0,0,1280,760`, intersects true |
| Historical evidence Junction recovery | Pass: `10/10` Junctions target `E:`, `1,018` files and `30,045,009` bytes are accessible, and all `10/10` post-relink SHA-256 directory manifests match the pre-relink source manifests |

The current Cross-section Viewer and Runner record are `Pass`, preserve five
metrics and three overlays, and report `ViewerRunnerMatchState=Matched`.

## Evidence

```text
D:\OpenVisionLab-TestData\OpenVisionLab-3D-Studio\artifacts\current\20260803-recovery-prerequisites-and-recipe-contracts
```

Principal evidence folders:

- `recipe-contracts` and `focused-runner`;
- `focused-shell` and `cross-section-parity`;
- `python-3.13-gates`, `structure-and-packages`, and `setup`;
- `release/openvisionlab-3d-studio-win-x64`;
- `self-contained-smoke`;
- `restore-release.txt` and `build-release.txt`.

The recovered historical evidence is physically retained under:

```text
E:\OpenVisionLab-TestData\OpenVisionLab-3D-Studio\artifacts\current
```

Only the ten repository-local Junction entries were replaced. Each verified
target is an ordinary E-drive directory, and the pre/post manifest comparison
covered relative file path, length, and SHA-256 for every file.

## Boundary / next dependency

The ten historical `artifacts/current` targets are restored locally from the
existing E-drive data. This repair depends on the current workstation volume
letter; if the volume letter changes again, validate the exact physical target
and its manifest before replacing the repository Junctions.

Human-owner unaided Wide/Compact R0 remains external and is not replaced by the
automated package checks. With the recovery slice closed, the owner-selected
project priority returns to the bounded `A-12` layout stream; `J-12` remains the
next deferred numerical backlog item.

The package generated during this closure is verification evidence from an
uncommitted working tree; its manifest reports that state. Rebuild the package
from the final clean commit before calling it a distributable release.
