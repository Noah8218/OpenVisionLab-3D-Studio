using System.Diagnostics;
using System.IO;
using OpenVisionLab.ThreeD.Shell;
using OpenVisionLab.ThreeD.Shell.Coordination;
using OpenVisionLab.ThreeD.Shell.Dialogs;
using OpenVisionLab.ThreeD.Shell.Verification.Smoke;
using OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;
using OpenVisionLab.ThreeD.Viewer.Hosting;

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
            ("SmokeScreenshotCapturePolicy", VerifySmokeScreenshotCapturePolicy()),
            ("AuxiliaryWindowScreenshotPolicy", VerifyAuxiliaryWindowScreenshotPolicy()),
            ("SmokePublishOrdering", VerifySmokePublishOrdering()),
            ("ViewerPointerSmokeOrdering", VerifyViewerPointerSmokeOrdering()),
            ("TeachingSmokeOrdering", VerifyTeachingSmokeOrdering()),
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
            () => ViewerNominalActualState.PreviewRunning);
        var wait = waiter.WaitAsync(TimeSpan.FromSeconds(30), lifetime.Token);
        lifetime.Dispose();
        return wait.Wait(TimeSpan.FromSeconds(2))
            && !wait.Result;
    }

    private static bool VerifyPreviewReady()
    {
        var waiter = new ShellNominalActualPreviewWaiter(
            () => ViewerNominalActualState.PreviewReady);
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

    private static bool VerifySmokeScreenshotCapturePolicy()
    {
        var events = new List<string>();
        var captureSucceeds = true;
        var coordinator = new ShellSmokeScreenshotCaptureCoordinator(
            new ShellSmokeScreenshotCaptureCallbacks
            {
                CaptureButtonPressed = (automationId, path, qualityPath, scope) =>
                {
                    events.Add($"button:{automationId}:{path}:{qualityPath}:{scope}");
                    return Task.FromResult(captureSucceeds);
                },
                CaptureRecipeHealthNavigation = (path, qualityPath) =>
                {
                    events.Add($"recipe-health:{path}:{qualityPath}");
                    return Task.FromResult(captureSucceeds);
                },
                CaptureWindow = (path, qualityPath, scope) =>
                {
                    events.Add($"window:{path}:{qualityPath}:{scope}");
                    return Task.FromResult(captureSucceeds);
                },
                AppendEvidence = request => events.Add(
                    $"evidence:{request.QualityReportPath}:{request.IntegrationExchangeEvidenceLine}")
            });

        var defaultCapture = coordinator.CaptureAsync(
                new ShellSmokeScreenshotCaptureRequest
                {
                    ScreenshotPath = "shell.png",
                    QualityReportPath = "quality.txt",
                    ViewerPresentationCameraLinkSummary = "camera",
                    IntegrationExchangeEvidenceLine = "integration"
                })
            .GetAwaiter()
            .GetResult();
        if (!defaultCapture.Succeeded
            || defaultCapture.Failure is not null
            || !events.SequenceEqual(
                ["window:shell.png:quality.txt:Shell", "evidence:quality.txt:integration"]))
        {
            return false;
        }

        events.Clear();
        var buttonCapture = coordinator.CaptureAsync(
                new ShellSmokeScreenshotCaptureRequest
                {
                    ScreenshotPath = "button.png",
                    Target = new ShellSmokeScreenshotTargetRequest
                    {
                        Import3DDataPressed = true
                    }
                })
            .GetAwaiter()
            .GetResult();
        if (!buttonCapture.Succeeded
            || !events.SequenceEqual(["button:Import3DData:button.png::Import3DDataPressed", "evidence::"]))
        {
            return false;
        }

        events.Clear();
        var recipeHealthCapture = coordinator.CaptureAsync(
                new ShellSmokeScreenshotCaptureRequest
                {
                    ScreenshotPath = "health.png",
                    QualityReportPath = "health.txt",
                    Target = new ShellSmokeScreenshotTargetRequest
                    {
                        RecipeHealthNavigationPressed = true
                    }
                })
            .GetAwaiter()
            .GetResult();
        if (!recipeHealthCapture.Succeeded
            || !events.SequenceEqual(["recipe-health:health.png:health.txt", "evidence:health.txt:"]))
        {
            return false;
        }

        events.Clear();
        captureSucceeds = false;
        var failedCapture = coordinator.CaptureAsync(
                new ShellSmokeScreenshotCaptureRequest
                {
                    ScreenshotPath = "failed.png"
                })
            .GetAwaiter()
            .GetResult();
        if (failedCapture.Succeeded
            || failedCapture.Failure != "Shell screenshot remained blank or invalid after 3 attempts."
            || !events.SequenceEqual(["window:failed.png::Shell"]))
        {
            return false;
        }

        events.Clear();
        var noOp = coordinator.CaptureAsync(
                new ShellSmokeScreenshotCaptureRequest())
            .GetAwaiter()
            .GetResult();
        return noOp.Succeeded
            && noOp.Failure is null
            && events.Count == 0;
    }

    private static bool VerifyAuxiliaryWindowScreenshotPolicy()
    {
        var events = new List<string>();
        var captureSucceeds = true;
        var coordinator = new ShellAuxiliaryWindowScreenshotCoordinator(
            new ShellAuxiliaryWindowScreenshotCallbacks
            {
                CaptureViewerPopout = (path, qualityPath) =>
                {
                    events.Add($"viewer:{path}:{qualityPath}");
                    return Task.FromResult(captureSucceeds);
                },
                CaptureRecipeManager = (path, qualityPath, firstRecipeCreatePressed) =>
                {
                    events.Add($"recipe:{path}:{qualityPath}:{firstRecipeCreatePressed}");
                    return Task.FromResult(captureSucceeds);
                },
                AppendRecipeManagerMonitorEvidence = qualityPath =>
                    events.Add($"monitor:{qualityPath}"),
                CaptureMessageDialog = (path, qualityPath, primaryPressed) =>
                {
                    events.Add($"dialog:{path}:{qualityPath}:{primaryPressed}");
                    return Task.FromResult(captureSucceeds);
                }
            });

        var success = coordinator.CaptureAsync(
                new ShellAuxiliaryWindowScreenshotRequest
                {
                    ViewerPopoutScreenshotPath = "viewer.png",
                    ViewerPopoutQualityReportPath = "viewer.txt",
                    RecipeManagerScreenshotPath = "recipe.png",
                    RecipeManagerQualityReportPath = "recipe.txt",
                    FirstRecipeCreatePressed = true,
                    MessageDialogScreenshotPath = "dialog.png",
                    MessageDialogQualityReportPath = "dialog.txt",
                    MessageDialogPrimaryPressed = true
                })
            .GetAwaiter()
            .GetResult();
        if (success is not null
            || !events.SequenceEqual(
                [
                    "viewer:viewer.png:viewer.txt",
                    "recipe:recipe.png:recipe.txt:True",
                    "monitor:recipe.txt",
                    "dialog:dialog.png:dialog.txt:True"
                ]))
        {
            return false;
        }

        events.Clear();
        captureSucceeds = false;
        var viewerFailure = coordinator.CaptureAsync(
                new ShellAuxiliaryWindowScreenshotRequest
                {
                    ViewerPopoutScreenshotPath = "viewer.png"
                })
            .GetAwaiter()
            .GetResult();
        if (viewerFailure != "Viewer pop-out screenshot remained unavailable, blank, or invalid after 3 attempts."
            || !events.SequenceEqual(["viewer:viewer.png:"]))
        {
            return false;
        }

        events.Clear();
        captureSucceeds = false;
        var recipeFailure = coordinator.CaptureAsync(
                new ShellAuxiliaryWindowScreenshotRequest
                {
                    RecipeManagerScreenshotPath = "recipe.png",
                    MessageDialogScreenshotPath = "dialog.png"
                })
            .GetAwaiter()
            .GetResult();
        if (recipeFailure != "Recipe Manager screenshot remained blank or invalid after 3 attempts."
            || !events.SequenceEqual(["recipe:recipe.png::False"]))
        {
            return false;
        }

        events.Clear();
        captureSucceeds = false;
        var dialogFailure = coordinator.CaptureAsync(
                new ShellAuxiliaryWindowScreenshotRequest
                {
                    MessageDialogScreenshotPath = "dialog.png"
                })
            .GetAwaiter()
            .GetResult();
        if (dialogFailure != "Message dialog screenshot remained blank or invalid after 3 attempts."
            || !events.SequenceEqual(["dialog:dialog.png::False"]))
        {
            return false;
        }

        events.Clear();
        var noOp = coordinator.CaptureAsync(
                new ShellAuxiliaryWindowScreenshotRequest())
            .GetAwaiter()
            .GetResult();
        return noOp is null && events.Count == 0;
    }

    private static bool VerifySmokePublishOrdering()
    {
        var events = new List<string>();
        var publishSucceeds = true;
        var saveSucceeds = true;
        var coordinator = new ShellSmokePublishCoordinator(
            new ShellSmokePublishCallbacks
            {
                PublishCurrentPreview = () =>
                {
                    events.Add("publish");
                    return publishSucceeds;
                },
                ShowReviewWorkspace = () => events.Add("review"),
                SaveCurrentRecipe = _ =>
                {
                    events.Add("save");
                    return saveSucceeds;
                }
            });

        var noPublishSave = coordinator.TryPublishAndSave(
            publish: false,
            saveRecipePath: "no-publish.json",
            out var noPublishFailure);
        if (!noPublishSave
            || noPublishFailure is not null
            || !events.SequenceEqual(["save"]))
        {
            return false;
        }

        events.Clear();
        var publishedAndSaved = coordinator.TryPublishAndSave(
            publish: true,
            saveRecipePath: "published.json",
            out var publishedFailure);
        if (!publishedAndSaved
            || publishedFailure is not null
            || !events.SequenceEqual(["publish", "review", "save"]))
        {
            return false;
        }

        events.Clear();
        publishSucceeds = false;
        var publishRejected = coordinator.TryPublishAndSave(
            publish: true,
            saveRecipePath: "rejected.json",
            out var publishFailure);
        if (publishRejected
            || publishFailure != "Viewer Publish failed because current Preview evidence was unavailable."
            || !events.SequenceEqual(["publish"]))
        {
            return false;
        }

        events.Clear();
        publishSucceeds = true;
        saveSucceeds = false;
        var saveRejected = coordinator.TryPublishAndSave(
            publish: true,
            saveRecipePath: "save-failure.json",
            out var saveFailure);
        return !saveRejected
            && saveFailure is null
            && events.SequenceEqual(["publish", "review", "save"]);
    }

    private static bool VerifyViewerPointerSmokeOrdering()
    {
        var events = new List<string>();
        var densitySucceeds = true;
        var pickSucceeds = true;
        var pointerSucceeds = true;
        var profileSucceeds = true;
        var orientedSucceeds = true;
        var coordinator = new ShellViewerPointerSmokeCoordinator(
            new ShellViewerPointerSmokeCallbacks
            {
                ApplyConfiguredNextDensity = () =>
                {
                    events.Add("density");
                    return Task.FromResult(densitySucceeds);
                },
                ApplyConfiguredPick = () =>
                {
                    events.Add("pick");
                    return pickSucceeds;
                },
                RunConfiguredPointerInputRegression = () =>
                {
                    events.Add("pointer");
                    return Task.FromResult(pointerSucceeds);
                },
                RunProfilePointerSmoke = _ =>
                {
                    events.Add("profile");
                    return Task.FromResult(profileSucceeds);
                },
                RunTeachingOrientedBoxPointerSmoke = _ =>
                {
                    events.Add("oriented");
                    return Task.FromResult(orientedSucceeds);
                },
                ViewerStatus = () => "viewer-status"
            });

        var success = coordinator.RunAsync("profile.txt", "oriented.txt")
            .GetAwaiter()
            .GetResult();
        if (success is not null
            || !events.SequenceEqual(["density", "pick", "pointer", "profile", "oriented"]))
        {
            return false;
        }

        events.Clear();
        pointerSucceeds = false;
        var continueFailure = coordinator.RunAsync(null, null)
            .GetAwaiter()
            .GetResult();
        if (continueFailure?.Message != "viewer-status"
            || continueFailure.Abort
            || !events.SequenceEqual(["density", "pick", "pointer"]))
        {
            return false;
        }

        events.Clear();
        pointerSucceeds = true;
        profileSucceeds = false;
        var abortFailure = coordinator.RunAsync("profile.txt", null)
            .GetAwaiter()
            .GetResult();
        return abortFailure?.Message == "Interactive height-profile pointer smoke failed."
            && abortFailure.Abort
            && events.SequenceEqual(["density", "pick", "pointer", "profile"]);
    }

    private static bool VerifyTeachingSmokeOrdering()
    {
        var events = new List<string>();
        var selectionSucceeds = true;
        var planeSucceeds = true;
        var saveSucceeds = true;
        var coordinator = new ShellTeachingSmokeCoordinator(
            new ShellTeachingSmokeCallbacks
            {
                RunTeachingSelection = (_, _) =>
                {
                    events.Add("selection");
                    return Task.FromResult(selectionSucceeds);
                },
                RunPlaneFlatnessLiveA3 = (_, _) =>
                {
                    events.Add("plane");
                    return Task.FromResult(planeSucceeds);
                },
                SaveTeachingRecipe = _ =>
                {
                    events.Add("save");
                    return (saveSucceeds, saveSucceeds ? null : "save-failure");
                }
            });

        var success = coordinator.RunAsync(
                new ShellTeachingSmokeRequest(
                    "capturing",
                    "selection.txt",
                    PlaneFlatnessLiveA3: true,
                    "plane.txt",
                    "plane.json",
                    "recipe.json"))
            .GetAwaiter()
            .GetResult();
        if (!success.Succeeded
            || success.Failure is not null
            || !events.SequenceEqual(["selection", "plane", "save"]))
        {
            return false;
        }

        events.Clear();
        selectionSucceeds = false;
        var selectionFailure = coordinator.RunAsync(
                new ShellTeachingSmokeRequest(
                    "capturing",
                    "selection.txt",
                    PlaneFlatnessLiveA3: true,
                    "plane.txt",
                    "plane.json",
                    "recipe.json"))
            .GetAwaiter()
            .GetResult();
        if (selectionFailure.Succeeded
            || selectionFailure.Failure is not null
            || !events.SequenceEqual(["selection"]))
        {
            return false;
        }

        events.Clear();
        selectionSucceeds = true;
        planeSucceeds = false;
        var planeFailure = coordinator.RunAsync(
                new ShellTeachingSmokeRequest(
                    null,
                    null,
                    PlaneFlatnessLiveA3: true,
                    "plane.txt",
                    "plane.json",
                    "recipe.json"))
            .GetAwaiter()
            .GetResult();
        if (planeFailure.Succeeded
            || planeFailure.Failure is not null
            || !events.SequenceEqual(["plane"]))
        {
            return false;
        }

        events.Clear();
        planeSucceeds = true;
        saveSucceeds = false;
        var saveFailure = coordinator.RunAsync(
                new ShellTeachingSmokeRequest(
                    null,
                    null,
                    PlaneFlatnessLiveA3: false,
                    null,
                    null,
                    "recipe.json"))
            .GetAwaiter()
            .GetResult();
        return !saveFailure.Succeeded
            && saveFailure.Failure == "save-failure"
            && events.SequenceEqual(["save"]);
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
