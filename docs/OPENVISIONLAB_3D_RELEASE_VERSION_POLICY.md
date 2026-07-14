# OpenVisionLab 3D Release And Version Policy

Updated: 2026-07-13

## Scope

This policy covers the OpenVisionLab 3D Studio application, the separately hosted Viewer DLL bundle, the Viewer Host API, durable Run Record JSON, and recipe JSON contracts. The first Viewer bundle prerelease, `v0.1.0-rc.1`, is published at commit `ac57687`.

## Sources Of Truth

| Version | Source | Current value |
| --- | --- | --- |
| Product and assembly package version | `Directory.Build.props` / `OpenVisionLabProductVersion` | `0.1.0-rc.1` |
| Viewer Host API | `Directory.Build.props` / `OpenVisionLabViewerHostApiVersion` | `1.0` |
| Viewer bundle manifest schema | `scripts/build-viewer-dll.ps1` | `1.0` |
| Durable Run Record schema | `OpenVisionLab.ThreeD.Runner.RunRecordWriter` | `1.1` |
| Recipe schema | Each recipe's `Version` field and loader | Tool-specific, currently `1.0` |

Do not duplicate a product or Host API version in another source file. Generated assemblies, Viewer manifests, and Run Records consume the central MSBuild values.

Windows CI enforces that `Directory.Build.props`, the uploaded Viewer manifest, and the generated Run Record agree on product and Host API versions. It also reports the manifest and Run Record schema values in the Actions summary.

## Product Version

Use Semantic Versioning: `MAJOR.MINOR.PATCH[-prerelease]`.

- While the product is pre-1.0, increment `MINOR` for a meaningful inspection workflow or public Viewer capability and `PATCH` for compatible fixes.
- Use `-dev` for normal development and `-rc.N` for a release candidate.
- Remove the suffix only for a release that passes every required gate.
- Increment `MAJOR` after 1.0 only for an intentionally incompatible product release.

Example progression:

```text
0.1.0-dev -> 0.1.0-rc.1 -> 0.1.0
0.1.0 -> 0.1.1-dev
0.1.1 -> 0.2.0-dev
```

## Contract Versions

Contract versions change independently from the product version.

| Change | Product | Host API | Run Record | Manifest | Recipe |
| --- | --- | --- | --- | --- | --- |
| Internal compatible fix | PATCH | unchanged | unchanged | unchanged | unchanged |
| Add optional Host state/command | MINOR or PATCH | increment minor | unchanged | unchanged | unchanged |
| Remove/change Host API semantics | MAJOR | increment major | unchanged | unchanged | unchanged |
| Add optional Run Record fields | MINOR or PATCH | unchanged | increment minor | unchanged | unchanged |
| Incompatible Run Record shape | MAJOR | unchanged | increment major | unchanged | unchanged |
| Add optional bundle manifest fields | PATCH | unchanged | unchanged | increment minor | unchanged |
| Change recipe meaning or required fields | MINOR or MAJOR | unchanged | unchanged | unchanged | increment recipe version |

Rules:

- A Host API minor update must keep existing compiled hosts working.
- A Run Record minor update must remain readable by the current Shell when older minor fields are absent.
- A recipe version change requires controlled loader failure or an explicit migration path; never reinterpret old values silently.
- A manifest schema change describes the manifest format, not the Viewer assembly version.
- Product version changes never imply calibrated or metrology-grade capability.

## Release Candidate Checklist

1. Start from a clean tracked working tree and synchronized branch.
2. Set `OpenVisionLabProductVersion` and `Version` to the same `-rc.N` value in `Directory.Build.props`.
3. Change Host API, Run Record, manifest, or recipe versions only when the corresponding contract changed.
4. Update release notes, commit the intended RC changes, and confirm the tracked working tree is clean.
5. Build and verify the Viewer DLL boundary:

```powershell
dotnet restore OpenVisionLab.ThreeDStudio.slnx
dotnet build OpenVisionLab.ThreeDStudio.slnx -c Release --no-restore
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\verify-viewer-dll-host.ps1 -Configuration Release -ArtifactDirectory artifacts\release\viewer-dll-host
```

6. Run the fixed data-loading matrix and relevant algorithm golden suites.
7. Generate one schema `1.1` Run Record and confirm product, Host API, Git, .NET, OS, and architecture identity are not `unknown`; Git tree state must be `clean`.
8. Check direct and transitive NuGet packages:

```powershell
python scripts\verify-nuget-package-health.py --self-test
python scripts\verify-nuget-package-health.py --solution OpenVisionLab.ThreeDStudio.slnx --report artifacts\dependency_audit\nuget_package_health.txt --json-directory artifacts\dependency_audit\nuget-package-health
```

The verifier runs both `--vulnerable --include-transitive` and `--deprecated --include-transitive` in JSON mode, preserves each raw response, and returns exit code `1` when either direct or transitive findings exist or when the JSON version, required parameters, or project sets are incomplete or inconsistent.

9. Push only after explicit user approval and require Windows CI success.
10. Inspect the uploaded CI Viewer/Shell PNG, quality report, contract, golden reports, and Run Record.
11. Create the release tag only after the pushed commit and uploaded evidence agree.

## Release Gate

Do not create a release tag when any condition below is true:

- Build, Runner, golden, map-fidelity, BinaryHost, Viewer screenshot-quality, or Shell screenshot-quality CI failed.
- Viewer bundle required files or SHA-256 manifest entries are missing.
- Run Record or bundle identity contains `unknown`.
- The release evidence reports a dirty Git working tree.
- Viewer/Runner comparison is not `Matched` for the selected release recipe.
- A changed public contract kept the old contract version without an explicit compatibility justification.
- Documentation claims calibrated physical units without calibration provenance and independent evidence.

## Tag And Artifact Convention

- Product tag: `vMAJOR.MINOR.PATCH`, for example `v0.1.0`.
- Release candidate tag: `vMAJOR.MINOR.PATCH-rc.N`.
- Viewer bundle archive when packaging is introduced: `OpenVisionLab.ThreeD.Viewer-<version>-windows.zip`.
- Keep generated bundles and release evidence under `artifacts\release\`; do not commit generated binaries.
- A tag must point to the exact clean commit recorded in the release manifest and Run Record.

## Release Evidence Template

```text
Product version:
Git commit / tree state:
Viewer assembly version:
Viewer Host API version:
Viewer manifest schema:
Run Record schema:
Recipe type / version:
Build result:
BinaryHost result:
Viewer and Shell screenshot quality result:
Viewer/Runner comparison:
Golden and C3D map-fidelity results:
NuGet vulnerable/deprecated result:
CI run URL:
Artifact name / SHA-256:
Known limitations:
```

## Current Decision

- `v0.1.0-rc.1` is published as a GitHub prerelease at commit `ac57687`; Windows Actions run `29198517611` and the public archive manifest record the same clean commit and `Matched` Viewer/Runner state.
- The public Viewer ZIP SHA-256 is `b9a9b6d002f507da63da32934d93bf6e8deaff2d7c1b00ff70a6f36d6b784a83`.
- Post-release host verification was hardened at commit `c50d196`; Windows Actions run `29215566528` passed the BinaryHost, screenshot-quality, Runner/golden/map, C3D roundtrip, independent Python, and artifact-upload gates.
- Host API v1.0 consumer evidence was added at commit `95dd8da`; Windows Actions run `29216983045` passed the BinaryHost state/event/command/recipe gate and all existing release regressions.
- Do not replace prerelease assets or promote `0.1.0-rc.1` to stable `0.1.0` without explicit owner approval and a new complete release gate.
- Do not create an installer, NuGet package, tag, or GitHub Release merely because the Viewer DLL bundle builds.
- The first release candidate must preserve the current unitless/raw-height limitations and must not advertise physical calibration.
