using System.Diagnostics;
using System.IO;
using OpenVisionLab.ThreeD.Shell;
using OpenVisionLab.ThreeD.Shell.Coordination;
using OpenVisionLab.ThreeD.Shell.Dialogs;
using OpenVisionLab.ThreeD.Shell.Verification.Smoke;
using OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;
using OpenVisionLab.ThreeD.Viewer.ViewModels;

namespace OpenVisionLab.ThreeD.Verification.Shell.Smoke;

internal static class ShellSmokeLifetimeVerification
{
    private const string Option = "--verify-shell-smoke-lifetime";

    public static bool TryRun(string[] arguments, out bool passed, out string summary)
    {
        var optionIndex = Array.FindIndex(
            arguments,
            argument => argument.Equals(Option, StringComparison.OrdinalIgnoreCase));
        if (optionIndex < 0)
        {
            passed = false;
            summary = string.Empty;
            return false;
        }

        if (optionIndex + 1 >= arguments.Length)
        {
            passed = false;
            summary = $"{Option} requires a report path.";
            return true;
        }

        var reportPath = Path.GetFullPath(arguments[optionIndex + 1]);
        var checks = new List<(string Name, bool Passed)>
        {
            ("CommandLineSnapshot", VerifyCommandLineSnapshot()),
            ("LifetimeDisposalCancelsDelay", VerifyLifetimeDisposalCancelsDelay()),
            ("PreviewWaiterCancellation", VerifyPreviewWaiterCancellation()),
            ("PreviewReady", VerifyPreviewReady()),
            ("IntegrationExchangeCancellationResult", VerifyIntegrationExchangeCancellationResult()),
            ("CurrentRecipeRunCancellationResult", VerifyCurrentRecipeRunCancellationResult()),
            ("SmokeExecutionGate", VerifySmokeExecutionGate()),
            ("SmokeOperationBoundary", VerifySmokeOperationBoundary()),
            ("ToolTeachingStartupNoOp", VerifyToolTeachingStartupNoOp()),
            ("SmokeScreenshotTargetSelection", VerifySmokeScreenshotTargetSelection()),
            ("SmokeScreenshotEvidenceAggregation", VerifySmokeScreenshotEvidenceAggregation()),
            ("RecipeMeasurementSmokeNoOp", VerifyRecipeMeasurementSmokeNoOp()),
            ("WorkbenchInteractionSmokeNoOp", VerifyWorkbenchInteractionSmokeNoOp()),
            ("MessageDialogPolicy", VerifyMessageDialogPolicy())
        };
        passed = checks.All(check => check.Passed);

        var lines = new List<string>
        {
            "OpenVisionLab 3D Shell Smoke lifetime verification"
        };
        lines.AddRange(checks.Select(check =>
            $"{check.Name}={(check.Passed ? "PASS" : "FAIL")}"));
        lines.Add($"Result={(passed ? "PASS" : "FAIL")}|{checks.Count(check => check.Passed)}/{checks.Count}");
        Directory.CreateDirectory(Path.GetDirectoryName(reportPath)!);
        File.WriteAllLines(reportPath, lines);
        summary = lines[^1];
        return true;
    }

    private static bool VerifyCommandLineSnapshot()
    {
        var commandLine = new ShellCommandLineArguments(
            ["shell.exe", "--SMOKE-FLAG", "--smoke-value", "1.25", "--smoke-count", "7"]);
        return commandLine.HasFlag("--smoke-flag")
            && commandLine.GetValue("--smoke-value") == "1.25"
            && commandLine.GetValueIgnoreCase("--SMOKE-VALUE") == "1.25"
            && commandLine.GetInvariantDouble("--smoke-value") == 1.25
            && commandLine.GetInvariantInt("--smoke-count") == 7
            && commandLine.GetValue("--missing") is null
            && commandLine.Values.Count == 6;
    }

    private static bool VerifyLifetimeDisposalCancelsDelay()
    {
        using var lifetime = new ShellSmokeLifetime();
        var delay = lifetime.DelayAsync(TimeSpan.FromSeconds(30));
        lifetime.Dispose();
        return delay.Wait(TimeSpan.FromSeconds(2))
            && !delay.Result
            && !lifetime.IsActive;
    }

    private static bool VerifyPreviewWaiterCancellation()
    {
        using var lifetime = new ShellSmokeLifetime();
        var waiter = new ShellNominalActualPreviewWaiter(
            () => NominalActualComparisonState.PreviewRunning);
        var wait = waiter.WaitAsync(TimeSpan.FromSeconds(30), lifetime.Token);
        lifetime.Dispose();
        return wait.Wait(TimeSpan.FromSeconds(2))
            && !wait.Result;
    }

    private static bool VerifyPreviewReady()
    {
        var waiter = new ShellNominalActualPreviewWaiter(
            () => NominalActualComparisonState.PreviewReady);
        return waiter.WaitAsync(TimeSpan.Zero).GetAwaiter().GetResult();
    }

    private static bool VerifyIntegrationExchangeCancellationResult()
    {
        var canceled = new ShellIntegrationExchangeSmokeResult(
            PressedCaptureAutomationId: null,
            PressedCaptureScope: null,
            EvidenceLine: null,
            Failure: null)
        {
            IsCanceled = true
        };
        return canceled.IsCanceled && !canceled.Succeeded;
    }

    private static bool VerifyCurrentRecipeRunCancellationResult()
    {
        var canceled = new ShellCurrentRecipeRunSmokeResult(null)
        {
            IsCanceled = true
        };
        return canceled.IsCanceled && !canceled.Succeeded;
    }

    private static bool VerifySmokeExecutionGate()
    {
        var gate = new ShellSmokeExecutionGate();
        var admitted = 0;
        Parallel.For(
            0,
            32,
            _ =>
            {
                if (gate.TryEnter())
                {
                    Interlocked.Increment(ref admitted);
                }
            });
        return admitted == 1 && !gate.TryEnter();
    }

    private static bool VerifySmokeOperationBoundary()
    {
        using var operation = new ShellSmokeOperation();
        var admitted = 0;
        Parallel.For(
            0,
            32,
            _ =>
            {
                if (operation.TryEnter())
                {
                    Interlocked.Increment(ref admitted);
                }
            });
        if (admitted != 1)
        {
            return false;
        }

        var delay = operation.DelayAsync(TimeSpan.FromSeconds(30));
        operation.Dispose();
        operation.Dispose();
        return delay.Wait(TimeSpan.FromSeconds(2))
            && !delay.Result
            && !operation.IsActive
            && !operation.TryEnter();
    }

    private static bool VerifyToolTeachingStartupNoOp()
    {
        using var workbench = new ToolWorkbenchViewModel();
        var callbackCount = 0;
        var coordinator = new ShellToolTeachingStartupCoordinator(
            workbench,
            new ShellToolTeachingStartupCallbacks
            {
                ClearViewerSource = _ => callbackCount++,
                UpdateSampleVisible = _ => callbackCount++,
                ViewerSampleVisible = () => false,
                IsViewerSourceAlreadyLoaded = _ =>
                {
                    callbackCount++;
                    return false;
                },
                LoadViewerSource = _ =>
                {
                    callbackCount++;
                    return false;
                },
                CurrentViewerSourcePath = () => null,
                ViewerStatus = () => string.Empty,
                SetWorkbenchSourceFromViewer = _ => callbackCount++,
                IsWorkbenchWorkspaceSelected = () => false,
                HideWorkbenchHudDetails = () => callbackCount++
            });
        var result = coordinator.Configure(
            new ShellToolTeachingStartupRequest(null, null, null));
        return result.Succeeded && callbackCount == 0;
    }

    private static bool VerifySmokeScreenshotTargetSelection()
    {
        var defaultTarget = ShellSmokeScreenshotTargetSelector.Select(
            new ShellSmokeScreenshotTargetRequest());
        var importTarget = ShellSmokeScreenshotTargetSelector.Select(
            new ShellSmokeScreenshotTargetRequest
            {
                Import3DDataPressed = true,
                ViewerToolbarPressed = true
            });
        var recipeHealthTarget = ShellSmokeScreenshotTargetSelector.Select(
            new ShellSmokeScreenshotTargetRequest
            {
                RecipeHealthNavigationPressed = true
            });
        var integrationTarget = ShellSmokeScreenshotTargetSelector.Select(
            new ShellSmokeScreenshotTargetRequest
            {
                IntegrationExchangePressed = true,
                IntegrationExchangeAutomationId = "IntegrationCapture",
                IntegrationExchangeScope = "IntegrationPressed"
            });
        var presetTarget = ShellSmokeScreenshotTargetSelector.Select(
            new ShellSmokeScreenshotTargetRequest
            {
                PreparationPresetAssistantMode = "APPLY-PRESSED"
            });

        return defaultTarget is null
            && importTarget?.Kind == ShellSmokeScreenshotTargetKind.Button
            && importTarget.AutomationId == "Import3DData"
            && recipeHealthTarget?.Kind == ShellSmokeScreenshotTargetKind.RecipeHealthNavigation
            && integrationTarget?.AutomationId == "IntegrationCapture"
            && integrationTarget.Scope == "IntegrationPressed"
            && presetTarget?.AutomationId == "ApplyPreparationPresetDraft";
    }

    private static bool VerifySmokeScreenshotEvidenceAggregation()
    {
        var events = new List<string>();
        var coordinator = new ShellSmokeScreenshotEvidenceCoordinator(
            new ShellSmokeScreenshotEvidenceCallbacks
            {
                AppendWindowMonitorEvidence = path =>
                    events.Add($"monitor:{path ?? "<null>"}"),
                AppendValidationThresholdEvidence = path =>
                    events.Add($"threshold:{path}"),
                AppendPreparationPresetEvidence = (state, path) =>
                    events.Add($"preparation:{state}:{path ?? "<null>"}")
            },
            (path, lines) => events.AddRange(
                lines.Select(line => $"line:{path}:{line}")));

        var reportPath = Path.GetFullPath("shell-smoke-evidence.txt");
        coordinator.Append(
            new ShellSmokeScreenshotEvidenceRequest
            {
                QualityReportPath = reportPath,
                ViewerPresentationCameraLinkSummary = "camera-summary",
                AppendValidationThresholdEvidence = true,
                IntegrationExchangeEvidenceLine = "integration-evidence",
                PreparationPresetAssistantMode = "review"
            });
        coordinator.Append(
            new ShellSmokeScreenshotEvidenceRequest
            {
                PreparationPresetAssistantMode = "dropdown"
            });

        return events.SequenceEqual(
        [
            $"line:{reportPath}:camera-summary",
            $"monitor:{reportPath}",
            $"threshold:{reportPath}",
            $"line:{reportPath}:integration-evidence",
            $"preparation:review:{reportPath}",
            "monitor:<null>",
            "preparation:dropdown:<null>"
        ]);
    }

    private static bool VerifyRecipeMeasurementSmokeNoOp()
    {
        using var workbench = new ToolWorkbenchViewModel();
        var viewCallbackCount = 0;
        var coordinator = new ShellRecipeMeasurementSmokeCoordinator(workbench);
        var failure = coordinator.RunAsync(
                new ShellRecipeMeasurementSmokeRequest(),
                () => viewCallbackCount++,
                () => Task.CompletedTask)
            .GetAwaiter()
            .GetResult();
        return failure is null && viewCallbackCount == 0;
    }

    private static bool VerifyWorkbenchInteractionSmokeNoOp()
    {
        using var workbench = new ToolWorkbenchViewModel();
        var layoutCallbackCount = 0;
        var renderCallbackCount = 0;
        var interactionCallbackCount = 0;
        var coordinator = new ShellWorkbenchInteractionSmokeCoordinator(workbench);
        var failure = coordinator.RunAsync(
                new ShellWorkbenchInteractionSmokeRequest(),
                () => layoutCallbackCount++,
                () =>
                {
                    renderCallbackCount++;
                    return Task.CompletedTask;
                },
                () =>
                {
                    interactionCallbackCount++;
                    return Task.FromResult(new ShellSurfaceMatchInteractionSmokeResult(null));
                },
                () => Array.Empty<string>())
            .GetAwaiter()
            .GetResult();
        return failure is null
            && layoutCallbackCount == 1
            && renderCallbackCount == 1
            && interactionCallbackCount == 0;
    }

    private static bool VerifyMessageDialogPolicy()
    {
        using var shell = new ShellMainWindowViewModel();
        var localizedKeys = new List<string>();
        var controller = new ShellMessageDialogController(
            () => throw new InvalidOperationException("Dialog owner must not be resolved while building options."),
            shell,
            (key, korean, english) =>
            {
                localizedKeys.Add(key);
                return english;
            });
        var noOrphanOptions = controller.CreateRecipeStepRemovalDialogOptions(
            new ToolWorkbenchStepRemovalRequestEventArgs(
                "step-1",
                "Height Measurement",
                Array.Empty<string>()));
        var orphanOptions = controller.CreateRecipeStepRemovalDialogOptions(
            new ToolWorkbenchStepRemovalRequestEventArgs(
                "step-2",
                "Filter",
                ["Selection A", "Selection B"]));

        return noOrphanOptions.Buttons.ToString() == "YesNo"
            && noOrphanOptions.DefaultResult.ToString() == "No"
            && noOrphanOptions.Message.Contains("Height Measurement", StringComparison.Ordinal)
            && noOrphanOptions.Message.Contains("No teaching selections", StringComparison.Ordinal)
            && orphanOptions.Message.Contains("2 teaching selection(s)", StringComparison.Ordinal)
            && localizedKeys.Contains("ThreeD.Dialog.RemoveStep.Title")
            && localizedKeys.Contains("ThreeD.Dialog.RemoveStep.OrphanSelections");
    }
}
