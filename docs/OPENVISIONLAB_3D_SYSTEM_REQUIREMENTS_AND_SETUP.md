# OpenVisionLab 3D Studio System Requirements and Setup

This document separates application runtime requirements from source-build and
verification utilities. A tool is not an application prerequisite merely
because repository automation uses it.

## Operator runtime

The supported distribution is the folder-based, self-contained Windows x64
package produced by `scripts/publish-windows-app.ps1`.

| Requirement | Status | Reason |
| --- | --- | --- |
| Windows 10 build 19041 or later, or Windows 11, x64 | Required | The Workbench uses WPF and the Windows 10.0.19041 contract. |
| OpenGL-compatible GPU and current vendor driver | Required | The Viewer renders through SharpGL/OpenGL. |
| Separate .NET installation | Not required by the self-contained package | The package carries the matching .NET runtime. |
| Git, Python, Windows SDK, Visual Studio, FFmpeg | Not required | These are source, verification, or evidence-production utilities. |

The package is intentionally folder-based rather than single-file. Native and
managed Viewer dependencies remain individually inspectable, and the generated
manifest records every payload file with its size and SHA-256.

Build the package:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\publish-windows-app.ps1
```

The default output is:

```text
artifacts\release\openvisionlab-3d-studio-win-x64
```

The package includes the application, the public Thickness Coupon and valid
mesh/point-cloud examples, tracked recipes, a package-specific `README.md`, the
user tutorial, `LICENSE`, `NOTICE`, this setup guide, and
`openvisionlab-3d-studio-manifest.json`. Deliberately corrupt loader fixtures
are not distributed in the operator package.

## Source-build and verification utilities

| Utility | Scope | Minimum/current contract | winget package ID |
| --- | --- | --- | --- |
| Git | Clone and source identity | A current supported Windows Git | `Git.Git` |
| Windows PowerShell | Repository automation | 5.1 | Built into supported Windows versions |
| .NET SDK | Restore, build, Runner, and Shell verification | 10.0.300; `global.json` allows a later compatible feature band | `Microsoft.DotNet.SDK.10` |
| Python | Full verification only: independent C3D and NuGet-health gates | 3.13, matching GitHub Actions | `Python.Python.3.13` |
| Windows Package Manager | Optional setup helper | Current App Installer/winget | Supplied by Microsoft App Installer |
| FFmpeg and FFprobe | Optional operator-video evidence only | No product runtime contract | Not installed by the setup script |

Open3D, VTK, oneMKL, Perl, Go, NASM, and the reviewed Visual C++ redistributable
belong to historical or deferred registration-engine distribution research.
They are not current OpenVisionLab 3D Studio product dependencies and must not
be installed or presented to operators as required utilities.

## Check or repair a restored development machine

Read-only source-build check:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\setup-development-environment.ps1 -CheckOnly -Scope Build
```

Read-only full-verification check, including Python 3.13:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\setup-development-environment.ps1 -CheckOnly -Scope FullVerification
```

Install only missing fixed packages for the selected scope:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\setup-development-environment.ps1 -InstallMissing -Scope Build
```

`FullVerification` remains the default scope when `-Scope` is omitted so
existing recovery commands keep their stricter behavior.

`-InstallMissing` is an explicit external-system action. The script:

1. checks Windows, .NET SDK, Git, PowerShell, winget, and Python when full
   verification is selected;
2. invokes only the fixed package IDs shown above from the official winget
   source;
3. refreshes the process PATH without discarding temporary process-only entries
   and checks the requirements again;
4. writes a reusable readiness report; and
5. never launches OpenVisionLab, opens a recipe, or executes Preview/Run.

After any installation, close and reopen existing terminals and Codex tasks so
they inherit the updated user PATH. On Windows, `py -3.13 --version` is the
most reliable immediate Python check; a newly opened terminal should also
resolve `python --version` to Python 3.13.

## Clone and package-cache paths

Use a short local checkout such as `C:\src\OpenVisionLab-3D-Studio`. Deep
checkout paths combined with a deep NuGet global-packages path can exceed
Windows path limits even when every required file is present.

If restore reports a package path or missing-file error under a long cache,
retry in the same terminal with a short cache:

```powershell
$env:NUGET_PACKAGES = 'C:\nuget'
dotnet restore OpenVisionLab.ThreeDStudio.sln
```

This environment variable affects only the current process and child
processes. It does not change the project or package contract.

## Why installation is not performed inside the Workbench

A framework-dependent application cannot repair a missing .NET runtime because
it cannot start without that runtime. Git and Python are developer utilities,
not inspection-operator requirements. Installing them from the inspection UI
would create an unrelated privileged workflow and incorrectly imply a product
dependency.

The supported design is therefore:

- ship operators a self-contained package with no separate .NET setup;
- keep developer setup explicit in the repository script;
- keep every install choice visible and auditable; and
- add an in-product installer only if a future operator-facing feature gains a
  genuine runtime dependency with an approved signed distribution contract.

## Recovery checklist

- [ ] `setup-development-environment.ps1 -CheckOnly -Scope Build` reports
      `Ready` for normal source work.
- [ ] `setup-development-environment.ps1 -CheckOnly -Scope FullVerification`
      reports `Ready` before running the full independent suite.
- [ ] `dotnet --version` is `10.0.300` or later in the .NET 10 line.
- [ ] `py -3.13 --version` reports Python 3.13.
- [ ] `python --version` works in a newly opened terminal.
- [ ] `dotnet restore OpenVisionLab.ThreeDStudio.slnx` succeeds.
- [ ] the Release solution build completes with zero errors.
- [ ] the self-contained package manifest identifies `selfContained: true` and
  `separateDotNetInstallationRequired: false`.
