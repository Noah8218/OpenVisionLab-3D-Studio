# OpenVisionLab 3D Standard Test Facade Closure

Date: 2026-08-23

Status: Complete

## Scope

`PL-0039` adds the smallest conventional `dotnet test` entry point that reuses
selected existing verification. One .NET 10 MTP/xUnit v3 project directly
calls:

- `C3DHeightProfileVerification.Verify(...)`;
- `ToolRecipeSelectionContractVerification.Verify(...)`.

The facade copies no verifier assertions, creates no shared fixture or helper
framework, and leaves the broader custom verification catalog unchanged.

## Implementation

- `global.json` selects `Microsoft.Testing.Platform` for .NET 10.
- `tests/OpenVisionLab.ThreeD.Data.Tests` contains one test project and one
  two-test facade class.
- Both classic and XML solution formats own the project.
- CI runs the already-built facade with `--no-build`, `--no-restore`, and
  `--minimum-expected-tests 2` before the existing custom gates.
- Detailed verifier reports use `Path.GetTempPath()`. Local `TEMP`/`TMP` and
  SDK `--artifacts-path` were routed to the project D-drive test root; hosted
  CI uses its available workspace and temporary-storage fallback.

## Acceptance Evidence

| Criterion | Evidence |
| --- | --- |
| Standard discovery | Release MTP run discovers and passes the two separately named tests (`2/2`) |
| Direct reuse | `ExistingVerificationFacadeTests.cs` contains only the two public `Verify(...)` calls plus report-path plumbing |
| Preserved reports | `c3d-height-profile.txt` and `tool-recipe-selection.txt` were generated under the D-backed process temp root |
| CI | one no-build/no-restore MTP step with a minimum expected count of two; existing custom commands are unchanged |
| Dependency health | 15 projects, vulnerable `0`, deprecated `0` |
| Regression | full Release build warning `0` / error `0`; structure `68/68`; Vision SDK package boundary passes |

## Verification

Commands actually run against the final package graph:

```powershell
dotnet restore tests\OpenVisionLab.ThreeD.Data.Tests\OpenVisionLab.ThreeD.Data.Tests.csproj --artifacts-path <D-backed-sdk-artifacts>
dotnet test --project tests\OpenVisionLab.ThreeD.Data.Tests\OpenVisionLab.ThreeD.Data.Tests.csproj -c Release --no-restore --artifacts-path <D-backed-sdk-artifacts> --results-directory <D-backed-results> --minimum-expected-tests 2 --report-xunit-trx
dotnet restore OpenVisionLab.ThreeDStudio.slnx --artifacts-path <D-backed-solution-artifacts>
dotnet build OpenVisionLab.ThreeDStudio.slnx -c Release --no-restore --artifacts-path <D-backed-solution-artifacts>
python scripts\verify-nuget-package-health.py --solution OpenVisionLab.ThreeDStudio.slnx --report <D-backed-report> --json-directory <D-backed-json>
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\verify-code-structure.ps1 -ReportPath <D-backed-report>
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\verify-vision-sdk-package.ps1 -ReportPath <D-backed-report>
```

Evidence root:

```text
D:\OpenVisionLab-TestData\OpenVisionLab-3D-Studio\artifacts\current\20260823-pl0039-standard-test-facade
```

## Boundary

This is a developer-verification facade. It does not launch the WPF EXE,
alter product behavior, qualify maximum inputs, satisfy human-owner R0, change
the product version, or create a release. The first attempted legacy xUnit v2
package graph failed the repository deprecation gate and was replaced before
closure by the one-package xUnit v3 MTP path; only the final passing graph is
the supported result.

## Durable Closure

Status: Complete

Scope: Two existing public Data verifiers are discoverable through one
solution-owned .NET 10 `dotnet test` project and one CI step.

Acceptance criteria: standard discovery `2/2`; direct reuse and report
preservation passed; D-backed local evidence passed; CI source gate added;
restore, Release build, NuGet, structure, package, and documentation gates
passed.

Verification: see the commands and D-backed reports above.

Evidence: this document, `.proofline/issues/PL-0039.json`, and the D-backed
evidence root.

Boundary / next dependency: no product or release claim; no additional
dependency-ready audit follow-up is selected. Human R0 remains owner-deferred,
and maximum-C3D qualification requires a representative input plus accepted
memory/load-time limits.
