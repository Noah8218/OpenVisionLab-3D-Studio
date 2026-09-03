using System.Globalization;
using System.IO;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Data;
using OpenVisionLab.ThreeD.Reporting.RunRecords;
using OpenVisionLab.ThreeD.Shell;
using OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;
using OpenVisionLab.ThreeD.Tools;

namespace OpenVisionLab.ThreeD.Verification.Shell.Workbench;

internal static class LevelSurfaceWorkbenchVerification
{
    public static bool Verify(string reportPath, out string summary)
    {
        var lines = new List<string>
        {
            "OpenVisionLab 3D Level Surface Workbench verification"
        };
        var passed = 0;
        var total = 0;
        var configuredTestRoot = Environment.GetEnvironmentVariable("OPENVISIONLAB_3D_TEST_ARTIFACT_ROOT");
        var rootDirectory = Path.Combine(
            string.IsNullOrWhiteSpace(configuredTestRoot)
                ? Path.Combine(Path.GetTempPath(), "OpenVisionLab.ThreeD")
                : Path.GetFullPath(configuredTestRoot),
            "LevelSurfaceWorkbench",
            Guid.NewGuid().ToString("N"));

        void Check(string name, bool condition, string detail)
        {
            total++;
            if (condition)
            {
                passed++;
            }

            lines.Add($"{(condition ? "PASS" : "FAIL")} | {name} | {detail}");
        }

        try
        {
            Directory.CreateDirectory(rootDirectory);
            var sourcePath = Path.Combine(rootDirectory, "tilted-source.c3d");
            CreateSource().SaveC3D(sourcePath);
            var source = C3DHeightFieldSnapshot.LoadIdentified(
                sourcePath,
                "source.level-workbench",
                "raw-height",
                "frame.c3d-grid-index");
            var document = CreateDocument(source, Path.GetFileName(sourcePath));
            var recipePath = Path.Combine(rootDirectory, "level-surface.ov3d-recipe.json");
            ToolRecipeDocumentStore.Save(recipePath, document);

            var workbench = new ToolWorkbenchViewModel();
            Check(
                "catalog exposes typed preparation tool",
                workbench.Tools.Any(
                    tool => tool.Id == "level-surface"
                            && tool.Category == "Prepare"
                            && tool.OutputContract.Contains(
                                "LevelingTransform",
                                StringComparison.Ordinal)),
                "Prepare -> Level Surface");
            Check(
                "open typed recipe",
                workbench.TryOpenTeachingRecipe(recipePath, out var openMessage),
                openMessage);
            workbench.SourceQuality.EnsureSourceAsync(
                sourcePath,
                workbench.Source.Id,
                workbench.Source.Unit,
                workbench.Source.FrameId,
                cancellationToken => workbench.SourceSession.GetOrLoadDecodedSourceAsync(
                    workbench.Source.Path,
                    workbench.Source.Id,
                    workbench.Source.Unit,
                    workbench.Source.FrameId,
                    cancellationToken)).GetAwaiter().GetResult();
            workbench.SelectPipelineStep("step.level-surface.01");
            Check(
                "typed PropertyGrid and two explicit references",
                workbench.SelectedStepPropertyDraft is LevelSurfaceStepProperties
                    {
                        MinimumValidSampleCount: 12,
                        MaximumReferenceRmsResidual: 0.1
                    }
                && workbench.SelectedPipelineStep?.InputEntityIds.Count == 3
                && workbench.Selections.Count == 2,
                $"{workbench.SelectedStepAdapterStatus};{workbench.LevelSurfaceReferenceSummary}");
            Check(
                "Preview enabled without implicit execution",
                workbench.PreviewSelectedStepCommand.CanExecute(null)
                && !workbench.HasCurrentLevelSurfacePreview,
                $"state={workbench.SelectedPipelineStep?.State}");
            ToolWorkbenchTeachingCaptureRequestEventArgs? additionalCapture = null;
            workbench.BeginTeachingSelectionCaptureRequested += (_, args) =>
                additionalCapture = args;
            workbench.BeginAdditionalLevelSurfaceReferenceCommand.Execute(null);
            Check(
                "additional reference ROI starts a new Viewer candidate",
                workbench.IsTeachingSelectionCaptureActive
                && additionalCapture is
                {
                    ExistingSelection: null,
                    Kind: ToolRecipeSelectionKinds.GridRectangle
                }
                && !workbench.Selections.Any(selection => string.Equals(
                    selection.Id,
                    additionalCapture.SelectionId,
                    StringComparison.OrdinalIgnoreCase)),
                $"candidate={additionalCapture?.SelectionId};existing={additionalCapture?.ExistingSelection?.Id ?? "(none)"}");
            workbench.CancelTeachingSelectionCaptureCommand.Execute(null);

            string? displayPath = null;
            string? displayHash = null;
            string? displayLabel = null;
            workbench.FilterDisplayRequested += (_, args) =>
            {
                displayPath = args.C3DPath;
                displayHash = args.ContentSha256;
                displayLabel = args.DisplayLabel;
            };
            var preview = workbench.PreviewSelectedLevelSurfaceAsync()
                .GetAwaiter()
                .GetResult();
            Check(
                "explicit Preview creates output and typed transform",
                preview
                && workbench.CurrentLevelSurfacePreviewOutput is not null
                && workbench.CurrentLevelSurfaceTransform?.ReferenceRegions.Count == 2
                && workbench.CurrentLevelSurfaceLevelFrame is not null
                && workbench.CurrentLevelSurfaceFrameChain is
                {
                    Source.Role: C3DCoordinateFrameRole.Source,
                    Reference.Role: C3DCoordinateFrameRole.Reference,
                    Result.Role: C3DCoordinateFrameRole.Result,
                    Level.Role: C3DCoordinateFrameRole.Level,
                    Links.Count: 3
                }
                && workbench.CurrentLevelSurfaceQualityEvidence is
                {
                    State: C3DLevelFrameQualityState.Accepted,
                    ReferenceCoverage.Count: 2
                },
                workbench.LevelSurfaceExecutionSummary);
            Check(
                "reference slope is removed",
                workbench.CurrentLevelSurfaceTransform is { } transform
                && Math.Abs(transform.FittedSlopeX - 0.8) < 0.01
                && Math.Abs(workbench.CurrentLevelSurfaceOutputSlopeX) < 0.00001
                && Math.Abs(workbench.CurrentLevelSurfaceOutputSlopeZ) < 0.00001,
                workbench.LevelSurfaceResidualSummary);
            Check(
                "Viewer request uses exact Preview bytes",
                displayPath == workbench.CurrentLevelSurfacePreviewPath
                && displayHash == workbench.CurrentLevelSurfacePreviewOutput?.ContentSha256
                && displayLabel == "Level Surface Preview"
                && File.Exists(displayPath),
                $"label={displayLabel};path={displayPath};hash={displayHash}");

            var direct = ToolRecipeLevelSurfaceExecution.Execute(
                document,
                "step.level-surface.01",
                rootDirectory);
            Check(
                "Workbench and Tools output parity",
                workbench.CurrentLevelSurfacePreviewOutput?.ContentSha256
                    == direct.Output?.ContentSha256,
                $"workbench={workbench.CurrentLevelSurfacePreviewOutput?.ContentSha256};tools={direct.Output?.ContentSha256}");
            Check(
                "Workbench and Tools transform parity",
                workbench.CurrentLevelSurfaceTransform?.ContentSha256
                    == direct.Transform?.ContentSha256,
                $"workbench={workbench.CurrentLevelSurfaceTransform?.ContentSha256};tools={direct.Transform?.ContentSha256}");
            Check(
                "Workbench and Tools Level Frame parity",
                workbench.CurrentLevelSurfaceLevelFrame?.ContentSha256
                    == direct.LevelFrame?.ContentSha256
                && workbench.CurrentLevelSurfaceLevelFrame?.LevelingTransformContentSha256
                    == workbench.CurrentLevelSurfaceTransform?.ContentSha256,
                $"workbench={workbench.CurrentLevelSurfaceLevelFrame?.ContentSha256};tools={direct.LevelFrame?.ContentSha256};transformLink={workbench.CurrentLevelSurfaceLevelFrame?.LevelingTransformContentSha256}");
            Check(
                "Workbench and Tools quality-evidence parity",
                workbench.CurrentLevelSurfaceQualityEvidence?.ContentSha256
                    == direct.QualityEvidence?.ContentSha256
                && workbench.CurrentLevelSurfaceQualityEvidence?.LevelFrameContentSha256
                    == workbench.CurrentLevelSurfaceLevelFrame?.ContentSha256
                && workbench.CurrentLevelSurfaceQualityEvidence?.LevelingTransformContentSha256
                    == workbench.CurrentLevelSurfaceTransform?.ContentSha256
                && workbench.CurrentLevelSurfaceQualityEvidence?.State
                    == C3DLevelFrameQualityState.Accepted,
                $"workbench={workbench.CurrentLevelSurfaceQualityEvidence?.ContentSha256};tools={direct.QualityEvidence?.ContentSha256};state={workbench.CurrentLevelSurfaceQualityEvidence?.State}");
            Check(
                "Workbench and Tools named frame-chain parity",
                workbench.CurrentLevelSurfaceFrameChain?.ContentSha256
                    == direct.FrameChain?.ContentSha256
                && workbench.LevelSurfaceFrameChainSummary.Contains("Source:", StringComparison.Ordinal)
                && workbench.LevelSurfaceFrameChainSummary.Contains("Reference:", StringComparison.Ordinal)
                && workbench.LevelSurfaceFrameChainSummary.Contains("Result:", StringComparison.Ordinal)
                && workbench.LevelSurfaceFrameChainSummary.Contains("Level:", StringComparison.Ordinal),
                $"workbench={workbench.CurrentLevelSurfaceFrameChain?.ContentSha256};tools={direct.FrameChain?.ContentSha256};summary={workbench.LevelSurfaceFrameChainSummary}");
            Check(
                "source grid and missing mask remain immutable",
                workbench.CurrentLevelSurfacePreviewOutput is { } output
                && output.RootSourceSha256 == source.ContentSha256
                && output.Width == source.Width
                && output.Height == source.Height
                && output.ValidCount == source.ValidCount
                && output.MissingCount == source.MissingCount,
                $"source={source.ContentSha256};root={workbench.CurrentLevelSurfacePreviewOutput?.RootSourceSha256}");
            var artifact = workbench.ArtifactRegistry.FirstOrDefault(
                item => item.Id == "derived.leveled-height.01");
            Check(
                "artifact exposes residual and transform evidence",
                artifact?.Contract == "LeveledHeightField + LevelingTransform + LevelFrame"
                && artifact.PreparationQualityDelta is
                {
                    BeforeValidSampleCount: 191,
                    BeforeMissingSampleCount: 1,
                    DetectedOutlierCount: null,
                    SourceIdentityRetained: true
                }
                && artifact.PreparationQualityDelta.AfterValidSampleCount
                    == workbench.CurrentLevelSurfacePreviewOutput?.ValidCount
                && artifact.PreparationQualityDelta.AfterMissingSampleCount
                    == workbench.CurrentLevelSurfacePreviewOutput?.MissingCount
                && artifact.Detail.Contains("reference RMS", StringComparison.Ordinal)
                && artifact.Detail.Contains(
                    workbench.CurrentLevelSurfaceTransform!.ContentSha256,
                    StringComparison.Ordinal)
                && artifact.Detail.Contains(
                    workbench.CurrentLevelSurfaceLevelFrame!.ContentSha256,
                    StringComparison.Ordinal)
                && artifact.Detail.Contains(
                    workbench.CurrentLevelSurfaceQualityEvidence!.ContentSha256,
                    StringComparison.Ordinal)
                && artifact.Detail.Contains(
                    workbench.CurrentLevelSurfaceFrameChain!.ContentSha256,
                    StringComparison.Ordinal),
                artifact?.Detail ?? "missing artifact");
            Check(
                "output is Viewer and compare renderable",
                workbench.CompareCandidates.Any(
                    candidate =>
                        candidate.Id == "derived.leveled-height.01"
                        && candidate.C3DPath == workbench.CurrentLevelSurfacePreviewPath),
                $"candidate={workbench.CurrentLevelSurfacePreviewPath}");

            workbench.PublishSelectedStepCommand.Execute(null);
            var publishOutputMatches = workbench.CurrentLevelSurfacePreviewOutput?.ContentSha256
                == direct.Output?.ContentSha256;
            Check(
                "Publish reuses Preview without rerun",
                workbench.IsLevelSurfacePreviewPublished
                && publishOutputMatches,
                $"published={workbench.IsLevelSurfacePreviewPublished};stale={workbench.IsLevelSurfacePreviewStale};outputMatches={publishOutputMatches};{workbench.LevelSurfaceExecutionSummary}");

            var savePath = Path.Combine(rootDirectory, "saved-level-surface.ov3d-recipe.json");
            var saved = workbench.TrySaveTeachingRecipe(
                savePath,
                out var saveMessage);
            var sidecarPath = Path.Combine(
                rootDirectory,
                "saved-level-surface.ov3d-recipe.level-surface.derived_leveled-height_01.json");
            Check(
                "save writes the Level Frame sidecar",
                saved
                && File.Exists(sidecarPath)
                && File.ReadAllText(sidecarPath).Contains(
                    workbench.CurrentLevelSurfaceQualityEvidence!.ContentSha256,
                    StringComparison.Ordinal)
                && File.ReadAllText(sidecarPath).Contains(
                    workbench.CurrentLevelSurfaceFrameChain!.ContentSha256,
                    StringComparison.Ordinal),
                saved ? sidecarPath : saveMessage);
            var reopened = saved
                ? ToolRecipeDocumentStore.Load(savePath)
                : null;
            var reopenParity = reopened is not null
                && reopened.Selections?.Count == 2
                && reopened.Steps.Single().InputEntityIds.Count == 3
                && ToolRecipeLevelSurfaceExecution.Execute(
                    reopened!,
                    "step.level-surface.01",
                    rootDirectory).Transform?.ContentSha256
                    == direct.Transform?.ContentSha256;
            Check(
                "save and reopen preserve reference routing",
                reopenParity,
                saveMessage);

            var restoredWorkbench = new ToolWorkbenchViewModel();
            var restored = restoredWorkbench.TryOpenTeachingRecipe(
                savePath,
                out var restoreMessage);
            Check(
                "reopen restores published Level Frame without executing",
                restored
                && restoredWorkbench.IsLevelSurfacePreviewPublished
                && restoredWorkbench.CurrentLevelSurfacePreviewOutput?.ContentSha256
                    == workbench.CurrentLevelSurfacePreviewOutput?.ContentSha256
                && restoredWorkbench.CurrentLevelSurfaceTransform?.ContentSha256
                    == workbench.CurrentLevelSurfaceTransform?.ContentSha256
                && restoredWorkbench.CurrentLevelSurfaceLevelFrame?.ContentSha256
                    == workbench.CurrentLevelSurfaceLevelFrame?.ContentSha256
                && restoredWorkbench.CurrentLevelSurfaceQualityEvidence?.ContentSha256
                    == workbench.CurrentLevelSurfaceQualityEvidence?.ContentSha256
                && restoredWorkbench.CurrentLevelSurfaceQualityEvidence?.State
                    == workbench.CurrentLevelSurfaceQualityEvidence?.State
                && restoredWorkbench.CurrentLevelSurfaceFrameChain?.ContentSha256
                    == workbench.CurrentLevelSurfaceFrameChain?.ContentSha256
                && restoredWorkbench.LevelSurfaceExecutionSummary.Contains(
                    "without executing",
                    StringComparison.OrdinalIgnoreCase),
                restored ? restoredWorkbench.LevelSurfaceExecutionSummary : restoreMessage);

            var sidecarJson = File.ReadAllText(sidecarPath);
            var tamperedQualityHash = new string('0', 64);
            File.WriteAllText(
                sidecarPath,
                sidecarJson.Replace(
                    workbench.CurrentLevelSurfaceQualityEvidence!.ContentSha256,
                    tamperedQualityHash,
                    StringComparison.Ordinal));
            var tamperedWorkbench = new ToolWorkbenchViewModel();
            var tamperedOpen = tamperedWorkbench.TryOpenTeachingRecipe(
                savePath,
                out var tamperedMessage);
            Check(
                "tampered quality sidecar is rejected without execution",
                tamperedOpen
                && !tamperedWorkbench.IsLevelSurfacePreviewPublished
                && tamperedWorkbench.LevelSurfaceExecutionSummary.Contains(
                    "not restored",
                    StringComparison.OrdinalIgnoreCase),
                tamperedOpen ? tamperedWorkbench.LevelSurfaceExecutionSummary : tamperedMessage);
            File.WriteAllText(sidecarPath, sidecarJson);

            var tamperedFrameChainHash = new string('1', 64);
            File.WriteAllText(
                sidecarPath,
                sidecarJson.Replace(
                    workbench.CurrentLevelSurfaceFrameChain!.ContentSha256,
                    tamperedFrameChainHash,
                    StringComparison.Ordinal));
            var tamperedFrameChainWorkbench = new ToolWorkbenchViewModel();
            var tamperedFrameChainOpen = tamperedFrameChainWorkbench.TryOpenTeachingRecipe(
                savePath,
                out var tamperedFrameChainMessage);
            Check(
                "tampered frame-chain sidecar is rejected without execution",
                tamperedFrameChainOpen
                && !tamperedFrameChainWorkbench.IsLevelSurfacePreviewPublished
                && tamperedFrameChainWorkbench.LevelSurfaceExecutionSummary.Contains(
                    "not restored",
                    StringComparison.OrdinalIgnoreCase),
                tamperedFrameChainOpen ? tamperedFrameChainWorkbench.LevelSurfaceExecutionSummary : tamperedFrameChainMessage);
            File.WriteAllText(sidecarPath, sidecarJson);

            var orderedRunRoot = Path.Combine(rootDirectory, "ordered-runs");
            var shell = new ShellMainWindowViewModel(
                recentRunRecordsPath: Path.Combine(rootDirectory, "recent-runs.json"),
                recentRecipesPath: Path.Combine(rootDirectory, "recent-recipes.json"),
                orderedRunRecordRoot: orderedRunRoot);
            var orderedRunCount = 0;
            ToolRecipeOrderedGraphExecutionResult? orderedExecution = null;
            shell.Workbench.OrderedRunCompleted += (_, args) =>
            {
                orderedRunCount++;
                orderedExecution = args.Execution;
            };
            var orderedOpened = shell.Workbench.TryOpenTeachingRecipe(
                savePath,
                out var orderedOpenMessage);
            var orderedSourceQuality = WaitForSourceQuality(shell.Workbench.SourceQuality);
            var orderedCanRun = shell.Workbench.RunTeachingRecipeCommand.CanExecute(null);
            var sourceBytesBeforeOrderedRun = File.ReadAllBytes(sourcePath);
            var orderedRunCompleted = shell.Workbench.RunTeachingRecipeAsync()
                .GetAwaiter()
                .GetResult();
            var orderedRecordPath = shell.Workbench.CurrentOrderedRunRecordPath;
            var orderedRecord = ReadRunRecord(orderedRecordPath);
            var orderedRecordStep = orderedRecord?.Steps?.SingleOrDefault();
            var sourceBytesAfterOrderedRun = File.ReadAllBytes(sourcePath);
            var sourceUnchangedAfterOrderedRun = sourceBytesBeforeOrderedRun.SequenceEqual(
                sourceBytesAfterOrderedRun);
            var transformOverlayLabel = direct.Transform is { } directTransform
                ? $"Leveling transform {directTransform.ContentSha256[..12]}"
                : null;
            var levelFrameOverlayLabel = direct.LevelFrame is { } directFrame
                ? $"Level Frame {directFrame.ContentSha256[..12]}"
                : null;
            Check(
                "saved Level Surface recipe runs through the current-recipe ordered graph",
                orderedOpened
                && orderedSourceQuality is not null
                && orderedCanRun
                && orderedRunCompleted
                && orderedRunCount == 1
                && orderedExecution?.Status == ResultStatus.Pass
                && orderedRecord is not null
                && orderedRecord.SchemaVersion == "1.9"
                && orderedRecord.Status == ResultStatus.Pass
                && orderedRecord.Source.Sha256 == source.ContentSha256
                && orderedRecordStep is not null
                && orderedRecordStep.ToolId == "level-surface"
                && orderedRecordStep.OutputContentSha256 == direct.Output?.ContentSha256
                && orderedRecordStep.Overlays.Any(overlay =>
                    string.Equals(overlay.Label, transformOverlayLabel, StringComparison.Ordinal))
                && orderedExecution?.Steps.SingleOrDefault()?.LevelFrameContentSha256
                    == direct.LevelFrame?.ContentSha256
                && orderedExecution?.Steps.SingleOrDefault()?.LevelFrameQualityContentSha256
                    == direct.QualityEvidence?.ContentSha256
                && orderedExecution?.Steps.SingleOrDefault()?.FrameChainContentSha256
                    == direct.FrameChain?.ContentSha256
                && orderedRecordStep.LevelFrameQualityContentSha256
                    == direct.QualityEvidence?.ContentSha256
                && orderedRecordStep.FrameChainContentSha256
                    == direct.FrameChain?.ContentSha256
                && orderedRecordStep.Overlays.Any(overlay =>
                    string.Equals(overlay.Label, levelFrameOverlayLabel, StringComparison.Ordinal))
                && sourceUnchangedAfterOrderedRun,
                $"opened={orderedOpened};canRun={orderedCanRun};run={orderedRunCompleted};count={orderedRunCount};execution={orderedExecution?.Status};record={orderedRecordPath};schema={orderedRecord?.SchemaVersion};status={orderedRecord?.Status};output={orderedRecordStep?.OutputContentSha256};transformOverlay={transformOverlayLabel};levelFrame={levelFrameOverlayLabel};sourceUnchanged={sourceUnchangedAfterOrderedRun};open={orderedOpenMessage}");

            var draft = (LevelSurfaceStepProperties)workbench.SelectedStepPropertyDraft!;
            draft.MaximumReferenceRmsResidual = 0.2;
            workbench.MarkSelectedStepParameterDraftDirty();
            Check(
                "draft edit alone does not stale or run",
                workbench.HasPendingStepParameterChanges
                && !workbench.IsLevelSurfacePreviewStale
                && workbench.IsLevelSurfacePreviewPublished,
                $"pending={workbench.HasPendingStepParameterChanges};published={workbench.IsLevelSurfacePreviewPublished};stale={workbench.IsLevelSurfacePreviewStale};{workbench.StepParameterEditStatus}");
            Check(
                "explicit parameter Apply marks Preview stale",
                workbench.TryApplySelectedStepParameterDraft(out var applyMessage)
                && workbench.IsLevelSurfacePreviewStale
                && !workbench.IsLevelSurfacePreviewPublished,
                applyMessage);

            var hadLevelSurfacePreviewBeforeDispose =
                workbench.CurrentLevelSurfacePreviewOutput is not null;
            workbench.Dispose();
            workbench.Dispose();
            Check(
                "Level Surface disposal clears Preview state idempotently",
                hadLevelSurfacePreviewBeforeDispose
                && !workbench.IsLevelSurfacePreviewRunning
                && !workbench.HasCurrentLevelSurfacePreview
                && !workbench.IsLevelSurfacePreviewPublished
                && workbench.CurrentLevelSurfacePreviewOutput is null
                && workbench.CurrentLevelSurfaceTransform is null
                && workbench.CurrentLevelSurfaceLevelFrame is null
                && workbench.CurrentLevelSurfaceFrameChain is null
                && !workbench.PreviewSelectedStepCommand.CanExecute(null)
                && !workbench.PublishSelectedStepCommand.CanExecute(null)
                && !workbench.CancelSelectedPreviewCommand.CanExecute(null),
                $"before={hadLevelSurfacePreviewBeforeDispose};running={workbench.IsLevelSurfacePreviewRunning};current={workbench.HasCurrentLevelSurfacePreview};published={workbench.IsLevelSurfacePreviewPublished};preview={workbench.PreviewSelectedStepCommand.CanExecute(null)};publish={workbench.PublishSelectedStepCommand.CanExecute(null)};cancel={workbench.CancelSelectedPreviewCommand.CanExecute(null)}");
        }
        catch (Exception exception)
        {
            lines.Add($"FAIL | unexpected exception | {exception}");
            total++;
        }
        finally
        {
            if (Directory.Exists(rootDirectory))
            {
                Directory.Delete(rootDirectory, true);
            }
        }

        summary =
            $"Level Surface Workbench verification: {(passed == total ? "PASS" : "FAIL")} ({passed}/{total})";
        lines.Insert(1, summary);
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(reportPath))!);
        File.WriteAllLines(reportPath, lines);
        return passed == total;
    }

    private static C3DHeightFieldSnapshot CreateSource()
    {
        const int width = 16;
        const int height = 12;
        var values = new double[width * height];
        for (var row = 0; row < height; row++)
        {
            for (var column = 0; column < width; column++)
            {
                var residual = ((row * 7 + column * 11) % 5 - 2) * 0.01;
                values[row * width + column] =
                    100 + 0.8 * column - 0.4 * row + residual;
            }
        }

        values[6 * width + 8] = double.NaN;
        return C3DHeightFieldSnapshot.CreateForVerification(
            "source.level-workbench",
            width,
            height,
            values);
    }

    private static ToolRecipeDocument CreateDocument(
        C3DHeightFieldSnapshot source,
        string sourcePath)
    {
        var selections = new[]
        {
            Selection("selection.level.left", "Left datum", 0, 0, 8, 6, source),
            Selection("selection.level.right", "Right datum", 4, 10, 8, 6, source)
        };
        return new ToolRecipeDocument(
            ToolRecipeDocument.CurrentSchemaVersion,
            "Level Surface Workbench",
            new ToolRecipeSource(
                source.EntityId,
                "Tilted Source",
                "C3D",
                source.Unit,
                source.FrameId,
                sourcePath,
                source.ByteLength,
                source.ContentSha256,
                source.Width,
                source.Height),
            [],
            [
                new ToolRecipeStep(
                    "step.level-surface.01",
                    "level-surface",
                    "Level Surface",
                    2,
                    [source.EntityId, .. selections.Select(item => item.Id)],
                    "derived.leveled-height.01",
                    [
                        new("ReferenceFitPolicy", C3DLevelingTransform.ReferenceFitPolicy),
                        new("LevelingPolicy", C3DLevelingTransform.LevelingPolicy),
                        new("MissingValuePolicy", C3DLevelingTransform.MissingValuePolicy),
                        new("GridPolicy", C3DLevelingTransform.GridPolicy),
                        new("MinimumValidSampleCount", "12"),
                        new("MaximumReferenceRmsResidual", 0.1.ToString("G17", CultureInfo.InvariantCulture))
                    ])
            ],
            selections);
    }

    private static ToolRecipeSelection Selection(
        string id,
        string name,
        int row,
        int column,
        int rowCount,
        int columnCount,
        C3DHeightFieldSnapshot source) =>
        new(
            id,
            name,
            ToolRecipeSelectionKinds.GridRectangle,
            source.EntityId,
            source.FrameId,
            new ToolRecipeSelectionSourceBinding(
                "C3D",
                source.RootSourceSha256,
                source.Width,
                source.Height),
            new ToolRecipeGridRectangle(
                row,
                column,
                rowCount,
                columnCount),
            null,
            null);

    private static SourceQualityReport? WaitForSourceQuality(
        SourceQualityWorkspaceViewModel workspace)
    {
        var deadline = DateTimeOffset.UtcNow + TimeSpan.FromSeconds(5);
        while (workspace.Report is null
               && !workspace.HasError
               && DateTimeOffset.UtcNow < deadline)
        {
            Thread.Sleep(10);
        }

        return workspace.Report;
    }

    private static InspectionRunRecord? ReadRunRecord(string? path) =>
        string.IsNullOrWhiteSpace(path) || !File.Exists(path)
            ? null
            : InspectionRunRecordJson.Read(path);
}
