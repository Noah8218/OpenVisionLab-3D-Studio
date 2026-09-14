# OpenVisionLab 3D Refactor Batch 0.5.6-dev

Date: 2026-09-14
Status: Complete

## Versioned scope

- Product version: `0.5.6-dev`
- Target branch: `main`
- Original baseline: `2d3bb0ece99128aa707d40c7faf49109edb7c19a`
- Exact Dev export source: `9cc21e15c02d34f56b49e2e1e32833775b0df659`
- Public snapshot: 1,095 allowlisted product files
- Public project graph: 19 `.csproj` files; 17 solution entries plus the
  out-of-solution `Viewer.BinaryHost` and `ThreeDIntegrationConsumerSmoke`
  consumers
- Change type: one consolidated, compatible large-scale structure refactor
- Viewer Host API: `1.1` unchanged
- Public history: the single commit containing this document is the complete
  public batch after the baseline; private Dev history was not merged.

The batch preserves the file-first operator loop: load, review source quality,
teach, Preview, Publish, Run, inspect evidence, then save and reopen. It does
not add camera, PLC, robot, cloud, account, deployment, or production-line
control.

## Ownership and actual call paths

| Area | Former concentration | Current owner and call path | Mutable state / lifetime owner |
| --- | --- | --- | --- |
| Shell startup and WPF flow | `MainWindow` and broad event handlers | `MainWindow` composes `ShellStartupCoordinator`, request/dialog coordinators, and `ShellSmokeScenarioRunner` | The relevant coordinator owns the operation and subscription lifetime; the View retains WPF lifecycle plumbing. |
| Workbench authoring | large `ToolWorkbenchViewModel` sections | XAML command/property → Workbench facade → recipe, source-load, ordered-run, validation, teaching, and Viewer-workspace owners | Each responsibility owner writes its state; the facade preserves the existing binding contract. |
| Viewer loading and interaction | `OpenVisionThreeDViewerControl` partials | Host/editor surface → loading, rendering, recipe, interaction, and lifetime owners → narrow View/OpenGL adapter | Viewer ViewModels and named session/coordinator types own state; the control owns only WPF/OpenGL resources and disposal. |
| 3D integration | legacy exchange/context calls in the public HeightMap consumer | `ThreeDIntegrationHeightMapRunner` → `ThreeDIntegrationV2Exchange` → `ThreeDIntegrationRunRecordPublication`; paired 2D evidence is read by `ThreeDIntegrationTransactionSequenceReader` | The runner owns one accepted execution and temporary input; V2 exchange owns message order; Run Record publication owns copied evidence commit/rollback. |

XAML binding and command names remain compatible. The existing four-argument
`ThreeDIntegrationV2Exchange.PublishCompletedResult` overload remains public;
the five-argument overload adds optional projection evidence without changing
the prior caller. Viewer Host API `1.1`, recipe, Run Record, integration schema,
and persisted-storage contracts remain in place.

## Removed stale ownership paths

The public snapshot removes eight source files only after their responsibilities
moved to these current owners:

| Removed path | Current owner |
| --- | --- |
| `Shell/Verification/Data/SourceChannelAndNormalQualityVerification.cs` | `Verification/Data/SourceChannelAndNormalQualityVerification.cs` |
| `Shell/Verification/Logging/LoggingIntegrationVerification.cs` | `Verification/Logging/LoggingIntegrationVerification.cs` |
| `Shell/Verification/Workbench/ImportSurfaceViewModelVerification.cs` | `Verification/Workbench/ImportSurfaceViewModelVerification.cs` |
| `Shell/ViewModels/Workbench/ToolWorkbenchStepProperties.cs` | five responsibility-specific `ToolWorkbench*StepProperties.cs` files |
| `Viewer/Recipes/ViewerRecipeLoadPlanVerification.cs` | `Verification/Viewer/ViewerRecipeLoadPlanVerification.cs` |
| `Viewer/ViewModels/C3DHeightDistributionVerification.cs` | `Viewer/Verification/C3DHeightDistributionVerification.cs` |
| `Viewer/Views/OpenVisionThreeDViewerControl.WorkbenchAffineApply.cs` | `Viewer/Rendering/ViewerWorkbenchOverlayRenderer.cs` |
| `Viewer/Views/OpenVisionThreeDViewerControl.WorkbenchRegridHeightField.cs` | `Viewer/Rendering/ViewerWorkbenchOverlayRenderer.cs` |

## Shortest contributor reading route

1. Open `OpenVisionLab.ThreeDStudio.slnx`; use the equivalent `.sln` when the
   installed Visual Studio version does not support `.slnx`.
2. Set `src/OpenVisionLab.ThreeD.Shell/OpenVisionLab.ThreeD.Shell.csproj` as
   the startup project.
3. Read `MainWindow.xaml`/`.xaml.cs`, then the bound Shell or Workbench
   ViewModel, then search the command/property name once to reach its owner.
4. For integration, read `ThreeDIntegrationHeightMapRunner` →
   `ThreeDIntegrationV2Exchange` → `ThreeDIntegrationRunRecordPublication` →
   `ThreeDIntegrationTransactionSequenceReader`.
5. Read `OpenVisionLab.ThreeD.Reporting.Tests` for the focused public
   compatibility contract and `OpenVisionLab.ThreeD.Verification` for the
   wider application boundaries.

## DLL consumption boundary

The Viewer is detachable as the generated bundle, not as one isolated DLL.
`samples/OpenVisionLab.ThreeD.Viewer.BinaryHost` has no `ProjectReference` and
consumes the bundle by assembly path. The bundle must retain the Viewer, Core,
Data, Tools, SharpGL, LASzip, native OpenCV dependencies, manifest, licenses,
and attribution files together.

Source inspection and the prior unchanged Viewer-host verification establish
that boundary. This candidate did not distribute the bundle. The exact cvBlob
LGPL version and corresponding source/relink obligations remain unresolved, so
binary redistribution stays closed until that legal dependency record is
complete.

## Acceptance and verification

- The fail-closed exporter accepted Dev commit `9cc21e15` and emitted 1,095
  files; destination length and SHA-256 matched for every overlaid file.
- The 17-project solution restored and built in Debug and Release with zero
  warnings and zero errors.
- Reporting integration tests passed `13/13`; Data tests passed `9/9`.
- The public integration consumer built with zero warnings/errors and its
  argument-admission execution returned the expected usage exit code `2`.
- The exact Dev source passed structure `515/515` and command routing
  `2,213/2,213`; those private verification scripts are intentionally excluded
  from the public product allowlist.
- NuGet package health checked 17 projects with zero vulnerable and zero
  deprecated findings. Vision SDK, WPF PropertyGrid, and Integration Contracts
  package identity and SHA-256 verification passed.
- Version, JSON, Markdown links, secret/binary/large-file review, and staged
  whitespace checks passed before the public commit.

`소스 코드 기준 검토 완료 / 실제 Runtime UI 검증 필요`

No fresh WPF/GPU screen run was required for the Reporting-only compatibility
correction after the unchanged Viewer source had passed the Dev runtime gates.
Hosted CI remains a post-push observation, and 100–200% DPI/theme coverage,
GPU/driver qualification, maximum representative C3D limits, calibrated
metrology, and product-owner R0 remain separate gates.

## Rollback and publication boundary

No tag, release object, package publication, deployment, or Viewer binary
distribution is part of this batch. If a regression is found, revert the one
public `0.5.6-dev` batch commit; do not rewrite or force-move `main`.