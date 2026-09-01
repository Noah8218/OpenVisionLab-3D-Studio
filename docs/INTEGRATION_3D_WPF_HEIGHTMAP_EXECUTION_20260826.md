# WPF Accepted HeightMap Execution — 2026-08-26

Status: Incomplete

## Scope

The 3D Studio Machine Studio exchange panel now has an explicit execution path
for an accepted `ThreeD/HeightMap` Handoff. The path remains local,
file-first, recipe-owned, and deterministic:

1. Save the shared exchange folder.
2. Refresh and select a Machine Studio Handoff.
3. Accept the Handoff explicitly.
4. Run `Accepted HeightMap` only when the Handoff is a `ThreeD/HeightMap`, its
   acknowledgement is `Accepted`, and no Result already exists.
5. The existing recipe-owned adapter verifies the copied recipe and C3D
   identities, derives the ROI and acceptance limits, executes the HeightMap
   inspection, and writes the correlated Result and Run Record.

No recipe is loaded implicitly, and saving, restoring, refreshing, accepting,
or resetting the exchange setup does not execute inspection.

## Implementation

- `src/OpenVisionLab.ThreeD.Shell/ViewModels/Integration/ThreeDIntegrationViewModel.cs`
  now exposes `RunHeightMapCommand`, derives accepted/result state from the
  acknowledgement and transaction contents, and runs the recipe-owned adapter
  asynchronously. It owns the active task and cancellation source, rejects
  duplicate execution while work is in flight, and exposes a bounded
  `ShutdownAsync` contract that suppresses late UI refreshes.
- `src/OpenVisionLab.ThreeD.Shell/Views/Integration/ThreeDIntegrationExchangeView.xaml`
  adds the localized `Accepted HeightMap 실행` action with automation ID
  `RunAcceptedHeightMap` and explains its enablement contract.
- `src/OpenVisionLab.ThreeD.Shell/Verification/Integration/ThreeDIntegrationViewModelVerification.cs`
  adds an independent D-backed 3x3 HeightMap fixture and verifies the full
  acknowledgement-to-result state sequence.
- `src/OpenVisionLab.ThreeD.Shell/ViewModels/Shell/ShellMainWindowViewModel.cs`
  and `src/OpenVisionLab.ThreeD.Shell/MainWindow.xaml.cs` keep the production
  identity path fail-closed while allowing the automated WPF smoke to inject
  the built Shell identity for a dirty development checkout.
- `scripts/run-three-d-wpf-heightmap-click-smoke.ps1` publishes a D-backed
  Machine Studio `ThreeD/HeightMap` fixture, prepares the accepted state, and
  drives the visible `RunAcceptedHeightMap` button with a real pointer-down /
  pointer-up sequence.
- `src/OpenVisionLab.ThreeD.Shell/OpenVisionLab.ThreeD.Shell.csproj` pins the
  v2 integration contract package required by the WPF consumer.
- `src/OpenVisionLab.ThreeD.Reporting/Integration/ThreeDIntegrationHeightMapRunner.cs`
  remains the recipe-owned execution boundary; it is not duplicated in the
  WPF layer. Its optional `CancellationToken` is checked before and after the
  major read, evaluation, and publication boundaries, including before a
  Result is written.
- `src/OpenVisionLab.ThreeD.Shell/MainWindow.xaml.cs` now treats the first close
  request as cancellable shutdown: it waits at most two seconds for the active
  HeightMap task, logs a bounded-timeout warning if work remains, and then
  re-enters close. Repeated close requests during this transition are ignored;
  the existing unsaved-Workbench decision remains first.
- `src/OpenVisionLab.ThreeD.Shell/Verification/ShellVerificationCommandRouter.cs`
  runs the synchronous ViewModel verification off the WPF startup Dispatcher so
  its explicit async wait cannot deadlock the verification host.

## Acceptance evidence

The focused WPF-neutral verification report records `23/23` checks:

- save/restore/refresh remain explicit and read-only;
- acknowledgement is absent before review;
- accept writes an acknowledgement but no Result;
- the existing Run Record publish path remains available;
- a HeightMap run stays disabled before acknowledgement;
- acknowledgement enables the HeightMap run;
- recipe-owned execution publishes a `Pass` Result and Run Record;
- the HeightMap task exposes an in-flight state, rejects a duplicate command,
  receives the shutdown cancellation request, and returns `false` when the
  injected operation intentionally exceeds the 50 ms test bound;
- late completion after bounded shutdown does not refresh the selected
  transaction or replace the shutdown-owned status; and
- the published transaction is visible and the HeightMap run is disabled for
  rerun; and
- reset clears only the saved exchange setup while preserving transaction
  evidence.

Evidence:

`D:\OpenVisionLab-TestData\OpenVisionLab-3D-Studio\heightmap-async-viewmodel-20260826.txt`

The current Release Shell build completed with zero warnings and zero errors:

`D:\OpenVisionLab-TestData\OpenVisionLab-3D-Studio\wpf-heightmap-run\shell-build-5.log`

The final Release build used by the visible-click smoke also completed with
zero warnings and zero errors.

## Accepted-transaction visible click evidence

The new D-backed WPF smoke now covers the complete visible action boundary in
both supported layout profiles used by this slice:

- Wide `1920x1040`: pressed and Result captures plus the quality report are
  under
  `D:\OpenVisionLab-TestData\OpenVisionLab-3D-Studio\wpf-heightmap-click-smoke\run-20260826-192019-4b7e0dca6238418a908ec9e36d9b19f2\`.
- Compact `1280x760`: pressed and Result captures plus the quality report are
  under
  `D:\OpenVisionLab-TestData\OpenVisionLab-3D-Studio\wpf-heightmap-click-smoke\run-20260826-192035-4a4c754ff78d4803995731a110f3e598\`.

Both quality reports record `PointerDown|scope=IntegrationExchangeHeightMapRunPressed|state=held`,
`visibleClick=true`, `resultPublished=true`, `rerunDisabled=true`, acceptable
pressed/Result screenshots, left-monitor intersection, and 125% runtime DPI.
The fixture's Handoff was targeted to the built Shell assembly identity
(`0.1.1` plus the current 40-character commit); this is test setup only and
does not change the production identity validation.

## Bounded asynchronous cancellation and shutdown

The execution contract is now:

1. The command captures the saved exchange root, transaction ID, and consumer
   identity on the UI thread, then delegates the synchronous recipe-owned
   runner to a background task.
2. `RunHeightMapCommand.CanExecute` becomes false before the task starts and
   remains false until completion. A second command invocation cannot create a
   second runner.
3. `ShutdownAsync(timeout)` marks the ViewModel as shutdown-owned, requests
   cancellation, and awaits the active task only for the supplied bound. It
   returns `true` when the task completes in time and `false` when the task is
   detached after the bound.
4. A completed or cancelled task cannot refresh Handoff state or publish a late
   completion status after shutdown has been requested.
5. `MainWindow.OnClosing` preserves the existing Workbench confirmation, then
   performs a two-second bounded shutdown before allowing the close to recur.

The runner's cancellation is cooperative. The current C3D parser, artifact
hashing, and fixed Vision SDK call are synchronous and cannot be interrupted in
the middle of those calls; the shutdown bound still prevents the UI close path
from waiting indefinitely, and the checkpoints prevent a cancelled operation
from starting or publishing a Result after cancellation is observed.

## Runtime UI evidence

The existing integration interaction-matrix smoke was run against the current
Release Shell in both requested layouts:

- Wide `1920x1040`: screenshot and quality report under
  `D:\OpenVisionLab-TestData\OpenVisionLab-3D-Studio\wpf-heightmap-run\integration-wide.png`
  and `integration-wide-quality.txt`.
- Compact `1280x760`: screenshot and quality report under
  `D:\OpenVisionLab-TestData\OpenVisionLab-3D-Studio\wpf-heightmap-run\integration-compact.png`
  and `integration-compact-quality.txt`.

Both reports record `acceptable=True` screenshot quality and
`focus=true|hover=true|mouseLeave=true|disabled=true|canExecute=true|tabTraversal=true`.
The runtime-selected smaller left monitor was recorded as
`-2400,456,0,1806`; both actual window rectangles intersected it. Runtime DPI
was 125% (`scaleX=1.25`, `scaleY=1.25`). The captures were visually inspected
for the new section, disabled state, required text, and panel bounds.

## Verification commands

```powershell
dotnet build src\OpenVisionLab.ThreeD.Shell\OpenVisionLab.ThreeD.Shell.csproj -c Release --no-restore
dotnet run --project src\OpenVisionLab.ThreeD.Shell\OpenVisionLab.ThreeD.Shell.csproj -c Release --no-build -- --verify-integration-view-model D:\OpenVisionLab-TestData\OpenVisionLab-3D-Studio\heightmap-async-viewmodel-final-20260826.txt
dotnet run --project src\OpenVisionLab.ThreeD.Shell\OpenVisionLab.ThreeD.Shell.csproj -c Release --no-build -- --shell-workspace Exchange --smoke-integration-exchange-state interaction-matrix --shell-smoke-width 1920 --shell-smoke-height 1040 --shell-smoke-leftmost --shell-smoke-screenshot D:\OpenVisionLab-TestData\OpenVisionLab-3D-Studio\wpf-heightmap-run\integration-wide.png --shell-screenshot-quality-report D:\OpenVisionLab-TestData\OpenVisionLab-3D-Studio\wpf-heightmap-run\integration-wide-quality.txt
dotnet run --project src\OpenVisionLab.ThreeD.Shell\OpenVisionLab.ThreeD.Shell.csproj -c Release --no-build -- --shell-workspace Exchange --smoke-integration-exchange-state interaction-matrix --shell-smoke-width 1280 --shell-smoke-height 760 --shell-smoke-leftmost --shell-smoke-screenshot D:\OpenVisionLab-TestData\OpenVisionLab-3D-Studio\wpf-heightmap-run\integration-compact.png --shell-screenshot-quality-report D:\OpenVisionLab-TestData\OpenVisionLab-3D-Studio\wpf-heightmap-run\integration-compact-quality.txt
dotnet test tests\OpenVisionLab.ThreeD.Reporting.Tests\OpenVisionLab.ThreeD.Reporting.Tests.csproj -c Release --no-restore
dotnet src\OpenVisionLab.ThreeD.Shell\bin\Release\net10.0-windows10.0.19041\OpenVisionLab.ThreeD.Shell.dll --verify-shell-smoke-command-line D:\OpenVisionLab-TestData\OpenVisionLab-3D-Studio\shell-options-async-shutdown-dll-20260826.txt
.\scripts\run-three-d-wpf-heightmap-click-smoke.ps1 -Layout Wide -Configuration Release -SkipBuild
.\scripts\run-three-d-wpf-heightmap-click-smoke.ps1 -Layout Compact -Configuration Release -SkipBuild
git diff --check
```

## Remaining boundary

- Accepted-transaction execution is verified through both the real WPF Shell
  ViewModel path and the visible Wide/Compact button-click smoke. The smoke
  prepares the accepted external transaction before the click, then verifies
  the published Result and disabled rerun state.
- The user explicitly deferred performance work. Startup/interaction latency
  and the repository UI performance baseline were not run for this slice.
- Only the available 125% DPI runtime was exercised; 100%, 150%, 175%, and
  200% remain unverified.
- The focused VM verification proves the timeout, cancellation request, late
  refresh suppression, and duplicate-command contract with an injected slow
  runner. The actual desktop close path is source/build verified and the
  visible-click smoke exercises the normal post-completion close, but an
  in-flight close against a deliberately slow real desktop run has not been
  captured.
- Cancellation remains cooperative around synchronous C3D parsing, artifact
  hashing, and the fixed SDK call; a timeout may therefore detach background
  work rather than interrupting those calls in-place.
- This does not add camera SDK, calibration, physical metrology, PLC, cloud,
  release, package, version, commit, push, or deployment behavior. No PC
  restart occurred.
