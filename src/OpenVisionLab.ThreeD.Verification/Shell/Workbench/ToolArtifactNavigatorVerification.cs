using System.IO;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Data;
using OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;

namespace OpenVisionLab.ThreeD.Verification.Shell.Workbench;

internal static class ToolArtifactNavigatorVerification
{
    public static bool Verify(string reportPath, out string summary)
    {
        var lines = new List<string> { "OpenVisionLab 3D Typed Artifact Registry + Recipe Navigator verification" };
        var passed = 0;
        var total = 0;
        var fixtureRoot = Path.Combine(Path.GetTempPath(), "OpenVisionLab.ThreeD", "ArtifactNavigator", Guid.NewGuid().ToString("N"));

        void Check(string name, bool condition, string detail)
        {
            total++;
            if (condition) passed++;
            lines.Add($"{(condition ? "PASS" : "FAIL")} | {name} | {detail}");
        }

        try
        {
            var viewerWorkspaceSession = new ViewerWorkspaceSession();
            viewerWorkspaceSession.ReconcileMainContent(["source", "output"], "source");
            viewerWorkspaceSession.ReconcileContents(["source", "output"], "output");
            viewerWorkspaceSession.PinMainContent("output");
            viewerWorkspaceSession.PinAuxiliaryContent("source");
            viewerWorkspaceSession.ReconcileMainContent(["source"], "source");
            viewerWorkspaceSession.ReconcileContents(["output"], "output");
            Check(
                "Viewer slots retain independent source/output pins across candidate refresh",
                viewerWorkspaceSession.MainContentId == "output"
                && viewerWorkspaceSession.AuxiliaryContentId == "source"
                && viewerWorkspaceSession.IsMainContentPinned
                && viewerWorkspaceSession.IsAuxiliaryContentPinned,
                $"main={viewerWorkspaceSession.MainContentId};aux={viewerWorkspaceSession.AuxiliaryContentId}");
            viewerWorkspaceSession.SetCameraLinked(true);
            Check(
                "Viewer camera link is an explicit session-only relation",
                viewerWorkspaceSession.IsCameraLinked,
                $"linked={viewerWorkspaceSession.IsCameraLinked}");
            viewerWorkspaceSession.ClearAuxiliaryContent();
            viewerWorkspaceSession.ReconcileContents(["output"], "output");
            Check(
                "Clearing a Viewer pin is explicit and does not silently select another candidate",
                viewerWorkspaceSession.AuxiliaryContentId.Length == 0
                && viewerWorkspaceSession.IsAuxiliaryContentExplicitlyCleared
                && !viewerWorkspaceSession.IsCameraLinked,
                $"aux={viewerWorkspaceSession.AuxiliaryContentId};cleared={viewerWorkspaceSession.IsAuxiliaryContentExplicitlyCleared};linked={viewerWorkspaceSession.IsCameraLinked}");
            Check(
                "An explicit split action can repopulate a cleared auxiliary slot",
                viewerWorkspaceSession.TrySetLayout(
                    ViewerWorkspaceLayout.SplitVertical,
                    ["output"],
                    "output")
                && viewerWorkspaceSession.AuxiliaryContentId == "output"
                && !viewerWorkspaceSession.IsAuxiliaryContentExplicitlyCleared,
                $"layout={viewerWorkspaceSession.Layout};aux={viewerWorkspaceSession.AuxiliaryContentId}");
            viewerWorkspaceSession.SetCameraLinked(true);
            viewerWorkspaceSession.ResetContentPins();
            Check(
                "Opening a new recipe context resets only session Viewer pins",
                viewerWorkspaceSession.MainContentId.Length == 0
                && viewerWorkspaceSession.AuxiliaryContentId.Length == 0
                && !viewerWorkspaceSession.IsMainContentExplicitlyCleared
                && !viewerWorkspaceSession.IsAuxiliaryContentExplicitlyCleared
                && !viewerWorkspaceSession.IsCameraLinked
                && viewerWorkspaceSession.Layout == ViewerWorkspaceLayout.SplitVertical,
                $"layout={viewerWorkspaceSession.Layout};main={viewerWorkspaceSession.MainContentId};aux={viewerWorkspaceSession.AuxiliaryContentId};linked={viewerWorkspaceSession.IsCameraLinked}");

            var compareSession = new ToolWorkbenchOutputCompareSession();
            var compareCandidate = new ToolWorkbenchCompareCandidateItem(
                "source.compare-session",
                "Compare session source",
                "SourceC3D",
                "Ready",
                "source.C3D",
                "Session-owned candidate",
                true);
            compareSession.ReplaceCandidates([compareCandidate], "No output pinned");
            compareSession.CompareSlotAArtifactId = compareCandidate.Id;
            compareSession.ReplaceCandidates(
                [compareCandidate with { State = "Published" }],
                "No output pinned");
            Check(
                "Output Compare session owns candidates, pins, and summary refresh",
                compareSession.CompareSlotAArtifactId == compareCandidate.Id
                && compareSession.CompareSlotASummary == "SourceC3D | Published | source.compare-session",
                $"slotA={compareSession.CompareSlotAArtifactId};summary={compareSession.CompareSlotASummary}");

            Directory.CreateDirectory(fixtureRoot);
            var source = C3DHeightFieldSnapshot.CreateForVerification(
                "source.navigator",
                4,
                4,
                [1, 1, 10, 10, 1, 1, 10, 10, 1, 1, 10, 10, 1, 1, 10, 10]);
            var sourcePath = Path.Combine(fixtureRoot, "source.c3d");
            source.SaveC3D(sourcePath);
            var selection = new ToolRecipeSelection(
                "selection.navigator.edge-band",
                "Navigator edge band",
                ToolRecipeSelectionKinds.GridRectangle,
                source.EntityId,
                source.FrameId,
                new ToolRecipeSelectionSourceBinding("C3D", source.ContentSha256, source.Width, source.Height),
                new ToolRecipeGridRectangle(0, 0, 4, 4),
                null,
                null);
            var document = new ToolRecipeDocument(
                ToolRecipeDocument.CurrentSchemaVersion,
                "Artifact Navigator Fixture",
                new ToolRecipeSource(source.EntityId, "Navigator source", "C3D", source.Unit, source.FrameId, sourcePath, source.ByteLength, source.ContentSha256, source.Width, source.Height),
                [],
                [
                    new ToolRecipeStep(
                        "step.filter.01", "filter", "Filter", 1, [source.EntityId], "derived.filtered.01",
                        [new("Method", "Median"), new("KernelSize", "3"), new("MissingValuePolicy", "PreserveMask"), new("BoundaryPolicy", "AvailableNeighbors")]),
                    new ToolRecipeStep(
                        "step.edge.01", "height-difference-edge", "Height Difference Edge", 1,
                        ["derived.filtered.01", selection.Id], "derived.edgepoints.01",
                        [new("ComparisonAxis", "AcrossColumns"), new("Polarity", "Rising"), new("MinimumDelta", "5"), new("CandidatePolicy", "StrongestPerScanline"), new("PointPolicy", "PairMidpoint"), new("MissingValuePolicy", "SkipPair"), new("BoundaryPolicy", "WithinSelection")])
                ],
                [selection]);
            var recipePath = Path.Combine(fixtureRoot, "artifact-navigator.ov3d-teach.json");
            ToolRecipeDocumentStore.Save(recipePath, document);

            var workbench = new ToolWorkbenchViewModel(Path.Combine(fixtureRoot, "recent.json"));
            Check("open typed recipe", workbench.TryOpenTeachingRecipe(recipePath, out var openMessage), openMessage);
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
            Check(
                "tree construction does not execute a tool",
                !workbench.HasCurrentFilterPreview && !workbench.HasCurrentEdgePreview,
                $"filterPreview={workbench.HasCurrentFilterPreview};edgePreview={workbench.HasCurrentEdgePreview}");
            Check(
                "registry records source, selection, and declared outputs",
                workbench.ArtifactRegistry.Count == 4
                && workbench.ArtifactRegistry.Single(item => item.Id == source.EntityId).State == "Ready"
                && workbench.ArtifactRegistry.Single(item => item.Id == selection.Id).State == "Current selection"
                && workbench.ArtifactRegistry.Single(item => item.Id == "derived.filtered.01").State == "Declared"
                && workbench.ArtifactRegistry.Single(item => item.Id == "derived.edgepoints.01").State == "Declared",
                workbench.ArtifactRegistrySummary);
            Check(
                "Main Viewer candidates are real typed 3D artifacts and the initial main pin is source-owned",
                workbench.MainViewerCandidates.All(candidate =>
                    candidate.Kind == ViewerWorkspaceCandidateKind.ThreeDArtifact
                    && File.Exists(candidate.SourcePath))
                && workbench.MainViewerCandidates.Any(candidate => candidate.IsSource)
                && workbench.ViewerWorkspace.MainContentId == source.EntityId
                && workbench.MainViewerSummary.Contains(workbench.Source.Name, StringComparison.Ordinal),
                $"main={workbench.ViewerWorkspace.MainContentId};candidates={string.Join(',', workbench.MainViewerCandidates.Select(item => item.Id))}");

            var initialSuggestionIds = workbench.CompatibleToolSuggestions.Select(item => item.Tool.Id).ToArray();
            Check(
                "compatible catalog scans ready source inputs without writing a route",
                initialSuggestionIds.Contains("filter", StringComparer.Ordinal)
                && initialSuggestionIds.Contains("roi-crop", StringComparer.Ordinal)
                && initialSuggestionIds.Contains("two-point-line", StringComparer.Ordinal)
                && initialSuggestionIds.Contains("three-point-plane", StringComparer.Ordinal)
                && !initialSuggestionIds.Contains("height-difference-edge", StringComparer.Ordinal)
                && !workbench.HasCurrentFilterPreview
                && !workbench.HasCurrentEdgePreview,
                string.Join(',', initialSuggestionIds));
            var readyFilterSuggestion = workbench.CompatibleToolSuggestions.Single(item => item.Tool.Id == "filter");
            Check(
                "valid Source Quality keeps the preparation suggestion enabled",
                readyFilterSuggestion.IsAvailable
                && readyFilterSuggestion.State == workbench.Localization.FlowPortReady
                && !readyFilterSuggestion.Detail.Contains("blocked", StringComparison.OrdinalIgnoreCase),
                readyFilterSuggestion.Detail);
            var readySourceQualityReport = workbench.SourceQuality.Report!;
            var noValidSamplesReport = readySourceQualityReport with
            {
                Coverage = readySourceQualityReport.Coverage with
                {
                    ValidSampleCount = 0,
                    MissingSampleCount = readySourceQualityReport.Coverage.SampleCount,
                    ValidRatio = 0d,
                    MissingRatio = 1d
                }
            };
            workbench.SourceQuality.SetReportForVerification(noValidSamplesReport);
            var blockedFilterSuggestion = workbench.CompatibleToolSuggestions.Single(item => item.Tool.Id == "filter");
            var pointToolSuggestion = workbench.CompatibleToolSuggestions.Single(item => item.Tool.Id == "two-point-line");
            var routeStepsBeforeQualityBlock = workbench.PipelineSteps.Count;
            workbench.SelectCompatibleToolCommand.Execute(blockedFilterSuggestion);
            Check(
                "invalid Source Quality blocks only the dependent preparation tool with an exact reason",
                !blockedFilterSuggestion.IsAvailable
                && blockedFilterSuggestion.State == workbench.Localization.CompatibleToolBlocked
                && blockedFilterSuggestion.BlockerReason.Contains("no valid height samples", StringComparison.Ordinal)
                && blockedFilterSuggestion.Detail.Contains("no valid height samples", StringComparison.Ordinal)
                && !workbench.AddCompatibleToolCommand.CanExecute(blockedFilterSuggestion)
                && !workbench.IsSelectedToolProposedRouteCompatible
                && workbench.SelectedToolProposedRouteDetail.Contains("no valid height samples", StringComparison.Ordinal),
                blockedFilterSuggestion.Detail);
            Check(
                "source-quality gate leaves point-selection routing available",
                pointToolSuggestion.IsAvailable
                && workbench.AddCompatibleToolCommand.CanExecute(pointToolSuggestion)
                && workbench.PipelineSteps.Count == routeStepsBeforeQualityBlock,
                pointToolSuggestion.Detail);
            workbench.SourceQuality.SetReportForVerification(readySourceQualityReport);
            Check(
                "restoring the current Source Quality report re-enables the dependent suggestion",
                workbench.CompatibleToolSuggestions.Single(item => item.Tool.Id == "filter").IsAvailable
                && workbench.CompatibleToolSuggestions.Single(item => item.Tool.Id == "filter").BlockerReason.Length == 0,
                workbench.CompatibleToolSuggestions.Single(item => item.Tool.Id == "filter").Detail);
            Check(
                "compatible catalog names the closest next missing typed input without routing or execution",
                workbench.HasCompatibleToolBlocker
                && workbench.CompatibleToolBlockerDetail.Contains("Height Difference Edge", StringComparison.Ordinal)
                && workbench.CompatibleToolBlockerDetail.Contains("Published FilteredHeightField + GridRectangle", StringComparison.Ordinal)
                && !workbench.HasCurrentFilterPreview
                && !workbench.HasCurrentEdgePreview,
                workbench.CompatibleToolBlockerDetail);
            var sourceSuggestion = workbench.CompatibleToolSuggestions.Single(item => item.Tool.Id == "two-point-line");
            var initialStepIds = workbench.PipelineSteps.Select(step => step.Id).ToArray();
            workbench.SelectCompatibleToolCommand.Execute(sourceSuggestion);
            Check(
                "compatible catalog selection changes only the Toolbox selection",
                workbench.SelectedTool?.Id == "two-point-line"
                && workbench.PipelineSteps.Select(step => step.Id).SequenceEqual(initialStepIds)
                && !workbench.HasCurrentFilterPreview
                && !workbench.HasCurrentEdgePreview,
                $"selected={workbench.SelectedTool?.Id};steps={workbench.PipelineSteps.Count}");

            var waitingForFilter = workbench.FlowPortDiagnostics.SingleOrDefault(item =>
                item.Step.ToolId == "height-difference-edge"
                && item.Port == "Input"
                && item.Kind == "WaitingForUpstream");
            Check(
                "Flow Map exposes declared upstream input as a read-only port problem",
                waitingForFilter is not null
                && workbench.PipelineSteps.Single(step => step.ToolId == "height-difference-edge").InputPortHasIssue
                && !workbench.HasCurrentFilterPreview
                && !workbench.HasCurrentEdgePreview,
                $"problems={workbench.FlowPortDiagnostics.Count};inputState={workbench.PipelineSteps.Single(step => step.ToolId == "height-difference-edge").InputPortState}");
            workbench.FocusFlowProblemStepCommand.Execute(waitingForFilter);
            Check(
                "Problems focus selects the authored step without routing or execution",
                workbench.SelectedPipelineStep?.ToolId == "height-difference-edge"
                && !workbench.HasCurrentFilterPreview
                && !workbench.HasCurrentEdgePreview,
                workbench.SelectedPipelineStepTitle);

            workbench.CompareSlotAArtifactId = source.EntityId;
            Check(
                "Output Compare exposes only the ready source before Preview",
                workbench.GetCompareCandidate(source.EntityId) is { IsSource: true, C3DPath: var sourceComparePath }
                && File.Exists(sourceComparePath)
                && workbench.GetCompareCandidate("derived.filtered.01") is null
                && workbench.CompareSlotASummary.Contains(source.EntityId, StringComparison.Ordinal),
                $"candidates={string.Join(',', workbench.CompareCandidates.Select(item => item.Id))};slotA={workbench.CompareSlotASummary}");

            var displayRequests = 0;
            workbench.ViewerArtifactDisplayRequested += (_, request) =>
            {
                displayRequests++;
                request.WasDisplayed = File.Exists(request.C3DPath);
            };
            var displayedSource = workbench.DisplayedOutputs.Single(item => item.Id == source.EntityId);
            workbench.ShowDisplayedOutputInViewerCommand.Execute(displayedSource);
            Check(
                "Displayed Outputs shows only an existing C3D source without execution",
                displayRequests == 1
                && displayedSource.IsRenderableInViewer
                && displayedSource.IsShownInViewer
                && !workbench.HasCurrentFilterPreview
                && !workbench.HasCurrentEdgePreview,
                $"requests={displayRequests};summary={workbench.CurrentViewerOutputSummary}");

            var pipelineRoot = workbench.NavigatorRoots.Single(item => item.NodeKind == "Pipeline");
            Check(
                "navigator preserves ordered input-output tree",
                pipelineRoot.Children.Count == 2
                && pipelineRoot.Children[0].Children.Any(item => item.Title.StartsWith("Input:", StringComparison.Ordinal))
                && pipelineRoot.Children[0].Children.Any(item => item.Title.StartsWith("Output:", StringComparison.Ordinal)),
                $"roots={workbench.NavigatorRoots.Count};steps={pipelineRoot.Children.Count}");

            var edgeStepNode = pipelineRoot.Children.Single(item => item.PipelineStep?.ToolId == "height-difference-edge");
            workbench.SelectNavigatorItemCommand.Execute(edgeStepNode);
            Check(
                "tree selection focuses the corresponding step without execution",
                workbench.SelectedPipelineStep?.ToolId == "height-difference-edge"
                && !workbench.HasCurrentFilterPreview
                && !workbench.HasCurrentEdgePreview,
                workbench.SelectedPipelineStepTitle);

            var filterStepNode = pipelineRoot.Children.Single(item => item.PipelineStep?.ToolId == "filter");
            workbench.SelectNavigatorItemCommand.Execute(filterStepNode);
            var declaredSelectedOutput = workbench.SelectedToolWorkspace.Outputs.Single();
            Check(
                "Selected Tool exposes declared output state and disabled inline actions without execution",
                declaredSelectedOutput.State == "Declared"
                && declaredSelectedOutput.Value == "\u2014"
                && !declaredSelectedOutput.CanShowInViewer
                && !declaredSelectedOutput.CanPinToCompare
                && !declaredSelectedOutput.CanCompare
                && !workbench.ShowWorkspaceOutputCommand.CanExecute(declaredSelectedOutput)
                && !workbench.PinWorkspaceOutputCommand.CanExecute(declaredSelectedOutput)
                && !workbench.CompareWorkspaceOutputCommand.CanExecute(declaredSelectedOutput)
                && !workbench.HasCurrentFilterPreview
                && !workbench.HasCurrentEdgePreview,
                $"state={declaredSelectedOutput.State};availability={declaredSelectedOutput.Availability}");
            var filterPreviewed = workbench.PreviewSelectedFilterAsync().GetAwaiter().GetResult();
            var filterArtifact = workbench.ArtifactRegistry.Single(item => item.Id == "derived.filtered.01");
            string FilterExecutionState() =>
                $"state={filterArtifact.State};current={workbench.HasCurrentFilterPreview};stale={workbench.IsFilterPreviewStale};published={workbench.IsFilterPreviewPublished};sourceLoading={workbench.SourceQuality.IsLoading};hasReport={workbench.SourceQuality.HasReport};selected={workbench.SelectedPipelineStep?.ToolId}";
            Check(
                "Filter Preview registers typed current output identity",
                filterPreviewed
                && filterArtifact.Contract == "FilteredHeightField"
                && filterArtifact.State == "Preview"
                && filterArtifact.RootSourceId == source.EntityId
                && filterArtifact.InputEntityIds == source.EntityId
                && filterArtifact.PreparationQualityDelta is
                {
                    ValidSampleDelta: 0,
                    MissingSampleDelta: 0,
                    DetectedOutlierCount: null,
                    SourceIdentityRetained: true
                }
                && filterArtifact.HasContentHash,
                $"{FilterExecutionState()};hash={filterArtifact.ContentSha256};input={filterArtifact.InputEntityIds};delta={filterArtifact.PreparationQualityDelta?.Summary}");
            Check("Filter Preview remains current after artifact projection", workbench.HasCurrentFilterPreview, FilterExecutionState());
            Check(
                "Flow Map keeps downstream waiting until explicit Filter Publish",
                workbench.FlowPortDiagnostics.Any(item =>
                    item.Step.ToolId == "height-difference-edge"
                    && item.Port == "Input"
                    && item.Kind == "WaitingForUpstream")
                && workbench.PipelineSteps.Single(step => step.ToolId == "height-difference-edge").InputPortHasIssue,
                $"problems={workbench.FlowPortDiagnostics.Count};inputState={workbench.PipelineSteps.Single(step => step.ToolId == "height-difference-edge").InputPortState}");

            var selectedFilterOutput = workbench.SelectedToolWorkspace.Outputs.Single();
            workbench.ShowWorkspaceOutputCommand.Execute(selectedFilterOutput);
            Check(
                "Selected Tool Show delegates an existing C3D output to the main Viewer without execution",
                displayRequests == 2
                && selectedFilterOutput.CanShowInViewer
                && workbench.DisplayedOutputs.Single(item => item.Id == filterArtifact.Id).IsShownInViewer
                && workbench.WorkspaceSelection.SelectedOutputEntityId == filterArtifact.Id
                && !workbench.HasCurrentEdgePreview,
                $"requests={displayRequests};selected={workbench.WorkspaceSelection.SelectedOutputEntityId}");

            selectedFilterOutput = workbench.SelectedToolWorkspace.Outputs.Single();
            workbench.PinWorkspaceOutputCommand.Execute(selectedFilterOutput);
            filterArtifact = workbench.ArtifactRegistry.Single(item => item.Id == "derived.filtered.01");
            Check("Filter Preview remains current after Selected Tool pin", workbench.HasCurrentFilterPreview, FilterExecutionState());
            Check(
                "Selected Tool Pin delegates to the first empty A/B/C slot without execution",
                string.Equals(workbench.CompareSlotBArtifactId, filterArtifact.Id, StringComparison.Ordinal)
                && workbench.DisplayedOutputs.Single(item => item.Id == filterArtifact.Id).IsPinnedToCompare
                && !workbench.HasCurrentEdgePreview,
                $"slotA={workbench.CompareSlotAArtifactId};slotB={workbench.CompareSlotBArtifactId}");

            var comparePaneRequests = 0;
            workbench.OutputComparePaneRequested += (_, _) => comparePaneRequests++;
            workbench.CompareSlotCArtifactId = source.EntityId;
            selectedFilterOutput = workbench.SelectedToolWorkspace.Outputs.Single();
            workbench.CompareWorkspaceOutputCommand.Execute(selectedFilterOutput);
            filterArtifact = workbench.ArtifactRegistry.Single(item => item.Id == "derived.filtered.01");
            Check("Filter Preview remains current after Selected Tool compare", workbench.HasCurrentFilterPreview, FilterExecutionState());
            Check(
                "Selected Tool Compare normalizes a preparation source/output comparison without execution",
                comparePaneRequests == 1
                && string.Equals(workbench.CompareSlotAArtifactId, source.EntityId, StringComparison.Ordinal)
                && string.Equals(workbench.CompareSlotBArtifactId, filterArtifact.Id, StringComparison.Ordinal)
                && workbench.CompareSlotCArtifactId.Length == 0
                && workbench.ViewerWorkspace.Layout == ViewerWorkspaceLayout.SplitVertical
                && string.Equals(workbench.MainViewerContentId, source.EntityId, StringComparison.Ordinal)
                && string.Equals(workbench.AuxiliaryViewerContentId, filterArtifact.Id, StringComparison.Ordinal)
                && workbench.HasPreparationQualityComparisonSummary
                && workbench.PreparationQualityComparisonSummary.Contains(
                    filterArtifact.DisplayName,
                    StringComparison.Ordinal)
                && workbench.PreparationQualityComparisonSummary.Contains(
                    workbench.CompareSlotBQualitySummary,
                    StringComparison.Ordinal)
                && workbench.PipelineSteps.Select(step => step.Id).SequenceEqual(initialStepIds)
                && !workbench.HasCurrentEdgePreview,
                $"requests={comparePaneRequests};slotA={workbench.CompareSlotAArtifactId};slotB={workbench.CompareSlotBArtifactId};slotC={workbench.CompareSlotCArtifactId};layout={workbench.ViewerWorkspace.Layout};main={workbench.MainViewerContentId};aux={workbench.AuxiliaryViewerContentId};quality={workbench.PreparationQualityComparisonSummary}");

            workbench.CompareSlotBArtifactId = filterArtifact.Id;
            filterArtifact = workbench.ArtifactRegistry.Single(item => item.Id == "derived.filtered.01");
            Check("Filter Preview remains current after compare slot update", workbench.HasCurrentFilterPreview, FilterExecutionState());
            Check(
                "Output Compare accepts current C3D source and Filter output without routing or running Edge",
                workbench.GetCompareCandidate(source.EntityId) is { IsSource: true, C3DPath: var sourcePathForCompare }
                && File.Exists(sourcePathForCompare)
                && workbench.GetCompareCandidate(filterArtifact.Id) is { IsSource: false, State: "Preview", C3DPath: var filterPathForCompare }
                && File.Exists(filterPathForCompare)
                && workbench.GetCompareCandidate("derived.edgepoints.01") is null
                && !workbench.HasCurrentEdgePreview
                && workbench.CompareSlotASummary.Contains(source.EntityId, StringComparison.Ordinal)
                && workbench.CompareSlotBSummary.Contains(filterArtifact.Id, StringComparison.Ordinal)
                && workbench.HasCompareSlotAQualitySummary
                && workbench.CompareSlotAQualitySummary == workbench.Localization.OutputCompareSourceBaseline
                && workbench.HasCompareSlotBQualitySummary
                && workbench.GetCompareCandidate(filterArtifact.Id)?.PreparationQualityDelta == filterArtifact.PreparationQualityDelta
                && workbench.CompareSlotBQualitySummary.Contains(
                    filterArtifact.PreparationQualityDelta!.BeforeValidSampleCount.ToString(),
                    StringComparison.Ordinal)
                && workbench.CompareSlotBQualitySummary.Contains(
                    filterArtifact.PreparationQualityDelta.AfterMissingSampleCount.ToString(),
                    StringComparison.Ordinal)
                && workbench.CompareSlotBQualitySummary.Contains(
                    workbench.Localization.OutputCompareOutliersNotEvaluated,
                    StringComparison.Ordinal)
                && workbench.CompareSlotBQualitySummary.Contains(
                    workbench.Localization.OutputCompareSourceIdentityRetained,
                    StringComparison.Ordinal),
                $"candidates={string.Join(',', workbench.CompareCandidates.Select(item => item.Id))};slotA={workbench.CompareSlotASummary};slotB={workbench.CompareSlotBSummary};sourceQuality={workbench.CompareSlotAQualitySummary};preparedQuality={workbench.CompareSlotBQualitySummary}");
            var sourceComparisonSlots = (
                workbench.CompareSlotAArtifactId,
                workbench.CompareSlotBArtifactId,
                workbench.CompareSlotCArtifactId);
            var displayedFilterForComparison = workbench.DisplayedOutputs.Single(item =>
                item.Id == filterArtifact.Id);
            var comparisonCandidates = workbench.CompareCandidates.ToArray();
            workbench.CompareCandidates.ReplaceAll(comparisonCandidates.Select(candidate =>
                string.Equals(candidate.Id, filterArtifact.Id, StringComparison.Ordinal)
                    ? candidate with { State = "Stale" }
                    : candidate));
            var staleComparisonRejected = !workbench.TryOpenPreparationQualityComparison(
                displayedFilterForComparison);
            workbench.CompareCandidates.ReplaceAll(comparisonCandidates.Select(candidate =>
                string.Equals(candidate.Id, filterArtifact.Id, StringComparison.Ordinal)
                    ? candidate with { C3DPath = Path.Combine(fixtureRoot, "missing-filter-output.c3d") }
                    : candidate));
            var missingComparisonRejected = !workbench.TryOpenPreparationQualityComparison(
                displayedFilterForComparison);
            workbench.CompareCandidates.ReplaceAll(comparisonCandidates.Select(candidate =>
                string.Equals(candidate.Id, filterArtifact.Id, StringComparison.Ordinal)
                    ? candidate with
                    {
                        PreparationQualityDelta = candidate.PreparationQualityDelta! with
                        {
                            DerivedContentSha256 = new string('0', 64)
                        }
                    }
                    : candidate));
            var mismatchedComparisonRejected = !workbench.TryOpenPreparationQualityComparison(
                displayedFilterForComparison);
            workbench.CompareCandidates.ReplaceAll(comparisonCandidates);
            var nonPreparationComparisonRejected = !workbench.TryOpenPreparationQualityComparison(
                workbench.DisplayedOutputs.Single(item => item.Id == source.EntityId));
            Check(
                "Stale, missing, mismatched, and non-preparation outputs cannot create a false preparation quality comparison",
                staleComparisonRejected
                && missingComparisonRejected
                && mismatchedComparisonRejected
                && nonPreparationComparisonRejected
                && sourceComparisonSlots == (
                    workbench.CompareSlotAArtifactId,
                    workbench.CompareSlotBArtifactId,
                    workbench.CompareSlotCArtifactId),
                $"stale={staleComparisonRejected};missing={missingComparisonRejected};mismatched={mismatchedComparisonRejected};nonPreparation={nonPreparationComparisonRejected};slotA={workbench.CompareSlotAArtifactId};slotB={workbench.CompareSlotBArtifactId};slotC={workbench.CompareSlotCArtifactId}");

            var viewerPinDirtyBaseline = workbench.IsDirty;
            workbench.SplitViewerVerticallyCommand.Execute(null);
            workbench.AuxiliaryViewerContentId = filterArtifact.Id;
            Check(
                "Viewer workspace pins a real Filter Preview beside the main Viewer without execution",
                workbench.ViewerWorkspace.Layout == ViewerWorkspaceLayout.SplitVertical
                && workbench.ViewerWorkspace.AuxiliaryContentId == filterArtifact.Id
                && workbench.ViewerWorkspace.IsAuxiliaryFocused
                && workbench.WorkspaceSelection.FocusedViewerSlotId == ViewerWorkspaceSession.AuxiliarySlotId
                && workbench.WorkspaceSelection.SelectedOutputEntityId == filterArtifact.Id
                && workbench.PipelineSteps.Select(step => step.Id).SequenceEqual(initialStepIds)
                && !workbench.HasCurrentEdgePreview,
                $"layout={workbench.ViewerWorkspace.Layout};pin={workbench.ViewerWorkspace.AuxiliaryContentId};focus={workbench.ViewerWorkspace.FocusedSlotId}");
            workbench.MainViewerContentId = filterArtifact.Id;
            workbench.AuxiliaryViewerContentId = source.EntityId;
            Check(
                "Main and Auxiliary Viewer pins can target different existing artifacts",
                workbench.ViewerWorkspace.MainContentId == filterArtifact.Id
                && workbench.ViewerWorkspace.AuxiliaryContentId == source.EntityId
                && workbench.MainViewerSummary.Contains(filterArtifact.DisplayName, StringComparison.Ordinal)
                && workbench.AuxiliaryViewerSummary.Contains(workbench.Source.Name, StringComparison.Ordinal)
                && workbench.IsDirty == viewerPinDirtyBaseline
                && !workbench.HasCurrentEdgePreview,
                $"main={workbench.ViewerWorkspace.MainContentId};aux={workbench.ViewerWorkspace.AuxiliaryContentId};dirty={workbench.IsDirty}");
            workbench.ClearAuxiliaryViewerPinCommand.Execute(null);
            Check(
                "Auxiliary clear leaves the main pin intact and reports no pinned auxiliary content",
                workbench.ViewerWorkspace.AuxiliaryContentId.Length == 0
                && workbench.ViewerWorkspace.IsAuxiliaryContentExplicitlyCleared
                && workbench.ViewerWorkspace.MainContentId == filterArtifact.Id
                && workbench.AuxiliaryViewerSummary == workbench.Localization.ViewerAuxiliaryNoOutput
                && workbench.IsDirty == viewerPinDirtyBaseline,
                $"main={workbench.ViewerWorkspace.MainContentId};aux={workbench.ViewerWorkspace.AuxiliaryContentId};summary={workbench.AuxiliaryViewerSummary};dirty={workbench.IsDirty};baseline={viewerPinDirtyBaseline}");
            workbench.AuxiliaryViewerContentId = filterArtifact.Id;
            Check(
                "Auxiliary replacement is explicit after clear",
                workbench.ViewerWorkspace.AuxiliaryContentId == filterArtifact.Id
                && !workbench.ViewerWorkspace.IsAuxiliaryContentExplicitlyCleared
                && workbench.IsDirty == viewerPinDirtyBaseline,
                $"aux={workbench.ViewerWorkspace.AuxiliaryContentId};cleared={workbench.ViewerWorkspace.IsAuxiliaryContentExplicitlyCleared};dirty={workbench.IsDirty};baseline={viewerPinDirtyBaseline}");
            workbench.SplitViewerHorizontallyCommand.Execute(null);
            workbench.PopOutViewerCommand.Execute(null);
            workbench.SetSingleViewerLayoutCommand.Execute(null);
            filterArtifact = workbench.ArtifactRegistry.Single(item => item.Id == "derived.filtered.01");
            Check("Filter Preview remains current after Viewer layout round-trip", workbench.HasCurrentFilterPreview, FilterExecutionState());
            Check(
                "Viewer layout round-trip retains the auxiliary pin and restores main focus",
                workbench.ViewerWorkspace.Layout == ViewerWorkspaceLayout.Single
                && workbench.ViewerWorkspace.AuxiliaryContentId == filterArtifact.Id
                && workbench.ViewerWorkspace.IsMainFocused
                && workbench.WorkspaceSelection.FocusedViewerSlotId == ViewerWorkspaceSession.MainSlotId
                && !workbench.HasCurrentEdgePreview,
                $"layout={workbench.ViewerWorkspace.Layout};pin={workbench.ViewerWorkspace.AuxiliaryContentId};focus={workbench.ViewerWorkspace.FocusedSlotId}");

            workbench.CompareSlotBArtifactId = string.Empty;
            var displayedFilter = workbench.DisplayedOutputs.Single(item => item.Id == filterArtifact.Id);
            workbench.PinDisplayedOutputToCompareCommand.Execute(displayedFilter);
            filterArtifact = workbench.ArtifactRegistry.Single(item => item.Id == "derived.filtered.01");
            Check("Filter Preview remains current after Displayed Outputs pin", workbench.HasCurrentFilterPreview, FilterExecutionState());
            Check(
                "Displayed Outputs pins an existing Filter C3D to Compare without routing or execution",
                string.Equals(workbench.CompareSlotBArtifactId, filterArtifact.Id, StringComparison.Ordinal)
                && displayedFilter.IsPinnedToCompare
                && !workbench.HasCurrentEdgePreview,
                $"slotB={workbench.CompareSlotBArtifactId};pins={displayedFilter.ComparePins}");

            var canPublishBeforeViewerPinPublish = workbench.PublishSelectedStepCommand.CanExecute(null);
            workbench.PublishSelectedStepCommand.Execute(null);
            filterArtifact = workbench.ArtifactRegistry.Single(item => item.Id == "derived.filtered.01");
            Check(
                "Filter Publish updates only artifact state",
                filterArtifact.State == "Published" && filterArtifact.HasContentHash,
                $"state={filterArtifact.State};hash={filterArtifact.HasContentHash};selected={workbench.SelectedPipelineStep?.ToolId};stepState={workbench.SelectedPipelineStep?.State};canBefore={canPublishBeforeViewerPinPublish};canPublish={workbench.PublishSelectedStepCommand.CanExecute(null)};current={workbench.HasCurrentFilterPreview};published={workbench.IsFilterPreviewPublished};stale={workbench.IsFilterPreviewStale};summary={workbench.FilterExecutionSummary};detail={filterArtifact.Detail}");
            Check(
                "Flow Map clears downstream waiting after explicit Filter Publish",
                !workbench.FlowPortDiagnostics.Any(item =>
                    item.Step.ToolId == "height-difference-edge"
                    && item.Port == "Input"
                    && item.Kind == "WaitingForUpstream")
                && !workbench.PipelineSteps.Single(step => step.ToolId == "height-difference-edge").InputPortHasIssue,
                $"problems={workbench.FlowPortDiagnostics.Count};inputState={workbench.PipelineSteps.Single(step => step.ToolId == "height-difference-edge").InputPortState}");
            var edgeSuggestion = workbench.CompatibleToolSuggestions.SingleOrDefault(item => item.Tool.Id == "height-difference-edge");
            Check(
                "compatible catalog exposes a published Filter plus current grid selection for Edge",
                edgeSuggestion is not null
                && edgeSuggestion.InputArtifactIds.Contains(filterArtifact.Id, StringComparison.Ordinal)
                && edgeSuggestion.InputArtifactIds.Contains(selection.Id, StringComparison.Ordinal)
                && !workbench.HasCurrentEdgePreview,
                edgeSuggestion?.Detail ?? "missing");
            workbench.SelectCompatibleToolCommand.Execute(edgeSuggestion);
            Check(
                "published-input compatible selection never auto-adds, connects, or executes Edge",
                workbench.SelectedTool?.Id == "height-difference-edge"
                && workbench.PipelineSteps.Select(step => step.Id).SequenceEqual(initialStepIds)
                && !workbench.HasCurrentEdgePreview,
                $"selected={workbench.SelectedTool?.Id};steps={workbench.PipelineSteps.Count}");

            // Publishing rebuilds the read-only navigator projection. Resolve the live
            // node as the WPF tree does, rather than interacting with a removed item.
            var currentEdgeStepNode = workbench.NavigatorRoots
                .Single(item => item.NodeKind == "Pipeline")
                .Children.Single(item => item.PipelineStep?.ToolId == "height-difference-edge");
            workbench.SelectNavigatorItemCommand.Execute(currentEdgeStepNode);
            var edgePreviewed = workbench.PreviewSelectedHeightDifferenceEdgeAsync().GetAwaiter().GetResult();
            var edgeArtifact = workbench.ArtifactRegistry.Single(item => item.Id == "derived.edgepoints.01");
            Check(
                "Edge Preview registers typed downstream identity",
                edgePreviewed
                && edgeArtifact.Contract == "EdgePointSet"
                && edgeArtifact.State == "Preview"
                && edgeArtifact.InputEntityIds == "derived.filtered.01"
                && edgeArtifact.RootSourceId == source.EntityId
                && edgeArtifact.HasContentHash,
                $"state={edgeArtifact.State};hash={edgeArtifact.ContentSha256};input={edgeArtifact.InputEntityIds}");

            var displayedEdge = workbench.DisplayedOutputs.Single(item => item.Id == edgeArtifact.Id);
            workbench.FocusDisplayedOutputStepCommand.Execute(displayedEdge);
            var selectedEdgeOutput = workbench.SelectedToolWorkspace.Outputs.Single();
            Check(
                "Displayed Outputs keeps feature output evidence-only and focuses its authored step",
                displayedEdge.IsEvidenceOnly
                && !displayedEdge.CanShowInViewer
                && !displayedEdge.CanPinToCompare
                && !selectedEdgeOutput.CanShowInViewer
                && !selectedEdgeOutput.CanPinToCompare
                && !selectedEdgeOutput.CanCompare
                && selectedEdgeOutput.Availability == workbench.Localization.EvidenceOnlyOutput
                && workbench.SelectedPipelineStep?.ToolId == "height-difference-edge",
                $"availability={displayedEdge.Availability};selected={workbench.SelectedPipelineStepTitle}");

            workbench.HeightDifferenceEdgeMinimumDelta = "20";
            edgeArtifact = workbench.ArtifactRegistry.Single(item => item.Id == "derived.edgepoints.01");
            Check(
                "parameter edits retain identity but mark output stale",
                edgeArtifact.State == "Stale" && edgeArtifact.HasContentHash && workbench.IsEdgePreviewStale
                && workbench.DisplayedOutputs.Single(item => item.Id == edgeArtifact.Id).HasNoCurrentOutput,
                $"state={edgeArtifact.State};hash={edgeArtifact.ContentSha256}");
            Check(
                "Problems reports a stale typed output without execution",
                workbench.FlowPortDiagnostics.Any(item =>
                    item.Step.ToolId == "height-difference-edge"
                    && item.Port == "Output"
                    && item.Kind == "Stale")
                && workbench.IsEdgePreviewStale,
                $"problems={workbench.FlowPortDiagnostics.Count};edgePreview={workbench.HasCurrentEdgePreview}");

            var compatibleAdd = workbench.CompatibleToolSuggestions.Single(item => item.Tool.Id == "two-point-line");
            var stepsBeforeCompatibleAdd = workbench.PipelineSteps.Count;
            var filterPreviewBeforeCompatibleAdd = workbench.HasCurrentFilterPreview;
            var edgePreviewBeforeCompatibleAdd = workbench.HasCurrentEdgePreview;
            workbench.AddCompatibleToolCommand.Execute(compatibleAdd);
            var addedCompatibleStep = workbench.PipelineSteps.Last();
            Check(
                "explicit compatible add creates one source-bound taught step without execution",
                workbench.PipelineSteps.Count == stepsBeforeCompatibleAdd + 1
                && addedCompatibleStep.ToolId == "two-point-line"
                && addedCompatibleStep.InputEntityIds.SequenceEqual([source.EntityId])
                && workbench.HasCurrentFilterPreview == filterPreviewBeforeCompatibleAdd
                && workbench.HasCurrentEdgePreview == edgePreviewBeforeCompatibleAdd,
                $"tool={addedCompatibleStep.ToolId};input={string.Join(';', addedCompatibleStep.InputEntityIds)};filterPreview={workbench.HasCurrentFilterPreview};edgePreview={workbench.HasCurrentEdgePreview}");

            var connectedRegionDocument = new ToolRecipeDocument(
                ToolRecipeDocument.CurrentSchemaVersion,
                "Connected Region Artifact Fixture",
                new ToolRecipeSource(
                    source.EntityId,
                    "Navigator source",
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
                        "step.connected-filter.01",
                        "filter",
                        "Connected Region Filter",
                        1,
                        [source.EntityId],
                        "derived.connected-filter.01",
                        [
                            new("Method", "Median"),
                            new("KernelSize", "3"),
                            new("MissingValuePolicy", "PreserveMask"),
                            new("BoundaryPolicy", "AvailableNeighbors")
                        ]),
                    new ToolRecipeStep(
                        "step.connected-region.01",
                        "connected-region",
                        "Connected Region",
                        2,
                        ["derived.connected-filter.01"],
                        "derived.connected-region.01",
                        [
                            new("Connectivity", "Four"),
                            new("OriginX", "0"),
                            new("OriginY", "0"),
                            new("ColumnPitch", "1"),
                            new("RowPitch", "1"),
                            new("AreaUnit", "mm2")
                        ])
                ],
                []);
            var connectedRegionRecipePath = Path.Combine(
                fixtureRoot,
                "connected-region-artifact.ov3d-teach.json");
            ToolRecipeDocumentStore.Save(
                connectedRegionRecipePath,
                connectedRegionDocument);
            var connectedRegionWorkbench = new ToolWorkbenchViewModel(
                Path.Combine(fixtureRoot, "connected-region-recent.json"));
            var connectedRegionOpened = connectedRegionWorkbench.TryOpenTeachingRecipe(
                connectedRegionRecipePath,
                out var connectedRegionOpenMessage);
            connectedRegionWorkbench.SourceQuality.Clear();
            var connectedRegionArtifact = connectedRegionWorkbench.ArtifactRegistry
                .SingleOrDefault(item => item.Id == "derived.connected-region.01");
            Check(
                "recipe and Artifact Registry preserve the ConnectedRegionArtifact declaration without execution",
                connectedRegionOpened
                && connectedRegionArtifact is not null
                && connectedRegionArtifact.Contract == "ConnectedRegionArtifact"
                && connectedRegionArtifact.State == "Declared"
                && connectedRegionArtifact.NodeKind == "DeclaredOutput"
                && !connectedRegionWorkbench.HasCurrentFilterPreview
                && connectedRegionWorkbench.PipelineSteps.Single(step => step.ToolId == "connected-region").OutputContract == "ConnectedRegionArtifact",
                $"opened={connectedRegionOpened};message={connectedRegionOpenMessage};contract={connectedRegionArtifact?.Contract};state={connectedRegionArtifact?.State};node={connectedRegionArtifact?.NodeKind}");

            workbench.Dispose();
            workbench.Dispose();
            Check(
                "Workbench disposal releases Filter Preview state idempotently",
                !workbench.IsFilterPreviewRunning
                && !workbench.HasCurrentFilterPreview
                && workbench.CurrentFilterPreviewPath is null,
                $"running={workbench.IsFilterPreviewRunning};current={workbench.HasCurrentFilterPreview};path={workbench.CurrentFilterPreviewPath ?? "(none)"}");
            connectedRegionWorkbench.Dispose();
        }
        catch (Exception exception)
        {
            total++;
            lines.Add($"FAIL | unexpected exception | {exception}");
        }
        finally
        {
            try
            {
                if (Directory.Exists(fixtureRoot)) Directory.Delete(fixtureRoot, recursive: true);
            }
            catch
            {
                // Functional checks remain valid if a temporary viewer lock delays cleanup.
            }
        }

        var success = total > 0 && passed == total;
        summary = $"ArtifactNavigator|pass={success}|checks={passed}/{total}|report={Path.GetFullPath(reportPath)}";
        lines.Insert(1, summary);
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(reportPath))!);
        File.WriteAllLines(reportPath, lines);
        return success;
    }
}
