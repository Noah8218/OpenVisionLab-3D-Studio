using System.IO;
using System.Windows.Threading;
using OpenVisionLab;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Data;
using OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;

namespace OpenVisionLab.ThreeD.Verification.Shell.Workbench;

/// <summary>
/// Exercises Tool Recipe teaching from source selection through ordered
/// routing, JSON persistence, reopen, and invalid-route rejection. Execution
/// remains covered separately by the Filter adapter verification.
/// </summary>
internal static class ToolRecipeTeachingVerification
{
    public static bool Verify(string reportPath, out string summary)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reportPath);

        var lines = new List<string>
        {
            "OpenVisionLab 3D Tool Recipe teaching verification",
            $"Generated: {DateTimeOffset.Now:O}"
        };
        var passed = 0;
        var total = 0;
        var fixtureRoot = Path.Combine(
            Path.GetTempPath(),
            "OpenVisionLab.ThreeD",
            "ToolRecipeTeachingVerification",
            Guid.NewGuid().ToString("N"));

        void Check(string name, bool condition, string detail)
        {
            total++;
            lines.Add($"{(condition ? "PASS" : "FAIL")} | {name} | {detail}");
            if (condition)
            {
                passed++;
            }
        }

        try
        {
            Directory.CreateDirectory(fixtureRoot);
            var sourcePath = Path.Combine(fixtureRoot, "teaching-source.C3D");
            C3DHeightFieldSnapshot.CreateForVerification(
                "source.fixture",
                2,
                2,
                [1.0, 2.0, 3.0, 4.0]).SaveC3D(sourcePath);
            var recipePath = Path.Combine(fixtureRoot, "fixture.ov3d-teach.json");
            var emptyRecipePath = Path.Combine(fixtureRoot, "empty.ov3d-recipe.json");

            var routeWorkbench = new ToolWorkbenchViewModel(Path.Combine(fixtureRoot, "recent-route.json"));
            routeWorkbench.SetC3DSource(sourcePath, markDirty: false);
            routeWorkbench.SelectedTool = routeWorkbench.Tools.Single(tool => tool.Id == "thickness");
            routeWorkbench.ToolSearchText = "Thickness";
            var selectedStepSetupRequestCount = 0;
            routeWorkbench.SelectedStepSetupRequested += (_, _) => selectedStepSetupRequestCount++;
            Check(
                "proposed Add route exposes actual input and output types before insertion",
                routeWorkbench.IsSelectedToolProposedRouteCompatible
                && routeWorkbench.SelectedToolProposedRouteDetail.Contains(
                    "source.c3d.height-map [SourceC3D / RawHeightField]",
                    StringComparison.Ordinal)
                && routeWorkbench.SelectedToolProposedRouteDetail.Contains(
                    "MeasurementResult",
                    StringComparison.Ordinal),
                routeWorkbench.SelectedToolProposedRouteDetail);
            var routeActionLogCount = routeWorkbench.RunLog.Count(item =>
                item.Category is "Preview" or "Publish" or "Run");
            var routeThickness = AddTool(routeWorkbench, "Thickness");
            Check(
                "successful Add clears the Tool Library search without execution",
                string.IsNullOrEmpty(routeWorkbench.ToolSearchText)
                && routeWorkbench.RunLog.Count(item =>
                    item.Category is "Preview" or "Publish" or "Run") == routeActionLogCount,
                $"search='{routeWorkbench.ToolSearchText}'; actionLogs={routeActionLogCount}");
            Check(
                "Add requests the selected-step setup surface exactly once without execution",
                selectedStepSetupRequestCount == 1
                && ReferenceEquals(routeThickness, routeWorkbench.SelectedPipelineStep)
                && routeWorkbench.RunLog.All(item => item.Category is not ("Preview" or "Publish" or "Run")),
                $"requests={selectedStepSetupRequestCount}; selected={routeWorkbench.SelectedPipelineStep?.Id}");
            routeWorkbench.SelectedTool = routeWorkbench.Tools.Single(tool => tool.Id == "warpage");
            var routeWarpageProposal = routeWorkbench.SelectedToolProposedRouteDetail;
            var routeWarpage = AddTool(routeWorkbench, "Warpage");
            Check(
                "HeightField Add skips the last MeasurementResult and routes to the compatible source",
                routeWarpage.InputEntityIds.SequenceEqual([routeWorkbench.Source.Id])
                && routeWarpageProposal.Contains(routeWorkbench.Source.Id, StringComparison.Ordinal)
                && !routeWarpageProposal.Contains(routeThickness.OutputEntityId, StringComparison.Ordinal),
                $"proposal={routeWarpageProposal}; route={routeWarpage.InputSummary}");
            routeWorkbench.SelectedTool = routeWorkbench.Tools.Single(tool => tool.Id == "plane-flatness");
            routeWorkbench.ToolSearchText = "Plane Flatness";
            var rejectedAddStepCount = routeWorkbench.PipelineSteps.Count;
            var rejectedAddActionLogCount = routeWorkbench.RunLog.Count(item =>
                item.Category is "Preview" or "Publish" or "Run");
            Check(
                "Add is unavailable when no TransformedHeightField route exists",
                !routeWorkbench.AddSelectedToolCommand.CanExecute(routeWorkbench.SelectedTool)
                && !routeWorkbench.IsSelectedToolProposedRouteCompatible
                && routeWorkbench.SelectedToolProposedRouteDetail.Contains(
                    "TransformedHeightField",
                    StringComparison.Ordinal),
                routeWorkbench.SelectedToolProposedRouteDetail);
            routeWorkbench.AddSelectedToolCommand.Execute(routeWorkbench.SelectedTool);
            Check(
                "rejected Add retains the visible search and changes no recipe or execution state",
                routeWorkbench.ToolSearchText == "Plane Flatness"
                && routeWorkbench.PipelineSteps.Count == rejectedAddStepCount
                && routeWorkbench.RunLog.Count(item =>
                    item.Category is "Preview" or "Publish" or "Run") == rejectedAddActionLogCount,
                $"search='{routeWorkbench.ToolSearchText}'; steps={routeWorkbench.PipelineSteps.Count}; actionLogs={rejectedAddActionLogCount}");
            Check(
                "compatible Add does not invoke Preview, Publish, or Run",
                routeWorkbench.RunLog.Count(item =>
                    item.Category is "Preview" or "Publish" or "Run") == routeActionLogCount,
                $"actionLogs={routeActionLogCount}");

            var inputSummaryLanguage = OpenVisionLanguageService.CurrentLanguage;
            var inputSummaryWorkbench = new ToolWorkbenchViewModel(
                Path.Combine(fixtureRoot, "recent-input-summary.json"));
            try
            {
                var emptyInputStep = new ToolWorkbenchPipelineStepItem(
                    "verification.empty-input",
                    inputSummaryWorkbench.Tools.Single(tool => tool.Id == "thickness"),
                    string.Empty,
                    "verification.empty-output");
                inputSummaryWorkbench.PipelineSteps.Add(emptyInputStep);
                var inputSummaryPropertyChanges = new List<string>();
                emptyInputStep.PropertyChanged += (_, args) =>
                {
                    if (!string.IsNullOrWhiteSpace(args.PropertyName))
                    {
                        inputSummaryPropertyChanges.Add(args.PropertyName);
                    }
                };

                OpenVisionLanguageService.SetLanguage(OpenVisionLanguage.English, save: false);
                var englishInputSummary = emptyInputStep.InputSummary;
                OpenVisionLanguageService.SetLanguage(OpenVisionLanguage.Korean, save: false);
                var koreanInputSummary = emptyInputStep.InputSummary;
                Check(
                    "empty input summary localizes and refreshes without changing input identity or execution",
                    englishInputSummary == "Specify an input entity ID."
                    && koreanInputSummary == inputSummaryWorkbench.Localization.FlowPortNoInputDetail
                    && englishInputSummary != koreanInputSummary
                    && emptyInputStep.InputEntityIds.Count == 0
                    && inputSummaryPropertyChanges.Contains(nameof(ToolWorkbenchPipelineStepItem.InputSummary), StringComparer.Ordinal)
                    && inputSummaryWorkbench.RunLog.All(item => item.Category is not ("Preview" or "Publish" or "Run")),
                    $"en={englishInputSummary}; ko={koreanInputSummary}; ids={emptyInputStep.InputEntityIds.Count}; changes={string.Join(',', inputSummaryPropertyChanges)}");
            }
            finally
            {
                OpenVisionLanguageService.SetLanguage(inputSummaryLanguage, save: false);
                inputSummaryWorkbench.Dispose();
            }

            var circleSourcePath = Path.Combine(fixtureRoot, "grid-circle-source.C3D");
            C3DHeightFieldSnapshot.CreateForVerification(
                "source.circle",
                7,
                7,
                Enumerable.Range(0, 49).Select(value => (double)value).ToArray()).SaveC3D(circleSourcePath);
            var circleBinding = ToolRecipeSelectionSourceBindingVerifier.ReadIdentity(circleSourcePath);
            var circleSelection = new ToolRecipeSelection(
                "selection.grid-circle.01",
                "Circular region",
                ToolRecipeSelectionKinds.GridCircle,
                "source.circle",
                "frame.c3d-grid-index",
                circleBinding,
                null,
                null,
                null,
                GridCircle: new ToolRecipeGridCircle(3, 3, 2));
            var circleDocument = new ToolRecipeDocument(
                ToolRecipeDocument.CurrentSchemaVersion,
                "GridCircle teaching fixture",
                new ToolRecipeSource(
                    "source.circle",
                    "GridCircle source",
                    "C3D",
                    "raw-height",
                    "frame.c3d-grid-index",
                    circleSourcePath,
                    new FileInfo(circleSourcePath).Length,
                    circleBinding.ContentSha256,
                    circleBinding.GridWidth,
                    circleBinding.GridHeight),
                [],
                [
                    new ToolRecipeStep(
                        "step.grid-circle-authoring.01",
                        "grid-circle-authoring",
                        "Circular Region Authoring",
                        2,
                        ["source.circle", circleSelection.Id],
                        "derived.grid-circle-authoring.01",
                        [])
                ],
                [circleSelection]);
            var circleRecipePath = Path.Combine(fixtureRoot, "grid-circle.ov3d-recipe.json");
            ToolRecipeDocumentStore.Save(circleRecipePath, circleDocument);
            var circleWorkbench = new ToolWorkbenchViewModel(Path.Combine(fixtureRoot, "recent-circle.json"));
            var circleOpened = circleWorkbench.TryOpenTeachingRecipe(circleRecipePath, out var circleOpenMessage);
            Check(
                "GridCircle recipe opens with its numeric editor context",
                circleOpened
                && circleWorkbench.SelectPipelineStep("step.grid-circle-authoring.01")
                && circleWorkbench.SelectedStepTeachingSelection?.GridCircle == circleSelection.GridCircle
                && circleWorkbench.IsTeachingGridCircleEditorVisible,
                circleOpenMessage);

            ToolWorkbenchTeachingCaptureRequestEventArgs? circleRequest = null;
            circleWorkbench.BeginTeachingSelectionCaptureRequested += (_, args) => circleRequest = args;
            var circleActionLogsBefore = circleWorkbench.RunLog.Count(item =>
                item.Category is "Preview" or "Publish" or "Run");
            circleWorkbench.BeginTeachingSelectionCaptureCommand.Execute(null);
            circleWorkbench.UpdateTeachingSelectionCaptureState(true, 2, 2, true, "Circle ready");
            circleWorkbench.UpdateTeachingGridCircleDraft(circleSelection.GridCircle);
            ToolRecipeGridCircle? numericDraft = null;
            circleWorkbench.TeachingGridCircleDraftChanged += (_, args) => numericDraft = args.Circle;
            circleWorkbench.TeachingGridCircleCenterColumn = 4;
            circleWorkbench.TeachingGridCircleRadius = 1;
            Check(
                "GridCircle center-and-boundary capture and numeric draft stay transient",
                circleRequest is
                {
                    Kind: ToolRecipeSelectionKinds.GridCircle,
                    RequiredPointCount: 2,
                    ExistingSelection.GridCircle: not null
                }
                && numericDraft == new ToolRecipeGridCircle(3, 4, 1)
                && circleWorkbench.SelectedStepTeachingSelection?.GridCircle == new ToolRecipeGridCircle(3, 3, 2)
                && circleWorkbench.RunLog.Count(item =>
                    item.Category is "Preview" or "Publish" or "Run") == circleActionLogsBefore,
                $"draft={numericDraft};applied={circleWorkbench.SelectedStepTeachingSelection?.GridCircle}");

            var appliedCircle = circleSelection with { GridCircle = numericDraft };
            var circleApplied = circleWorkbench.TryApplyCapturedTeachingSelection(appliedCircle, out var circleApplyMessage);
            Check(
                "explicit Apply replaces the same GridCircle without inspection execution",
                circleApplied
                && circleWorkbench.SelectedStepTeachingSelection?.Id == circleSelection.Id
                && circleWorkbench.SelectedStepTeachingSelection.GridCircle == numericDraft
                && circleWorkbench.RunLog.Count(item =>
                    item.Category is "Preview" or "Publish" or "Run") == circleActionLogsBefore,
                circleApplyMessage);

            var circleSavedPath = Path.Combine(fixtureRoot, "grid-circle-saved.ov3d-recipe.json");
            var circleSaved = circleWorkbench.TrySaveTeachingRecipe(circleSavedPath, out var circleSaveMessage);
            var circleReopened = circleSaved ? ToolRecipeDocumentStore.Load(circleSavedPath) : null;
            Check(
                "Workbench GridCircle save and reopen preserve exact geometry and route",
                circleSaved
                && circleReopened?.SchemaVersion == ToolRecipeDocument.CurrentSchemaVersion
                && circleReopened.Selections is [var savedCircle]
                && savedCircle.Id == circleSelection.Id
                && savedCircle.GridCircle == numericDraft
                && circleReopened.Steps.Single().InputEntityIds.SequenceEqual(
                    ["source.circle", circleSelection.Id]),
                circleSaveMessage);

            var polygonSelection = new ToolRecipeSelection(
                "selection.grid-polygon.01",
                "Irregular region",
                ToolRecipeSelectionKinds.GridPolygon,
                "source.circle",
                "frame.c3d-grid-index",
                circleBinding,
                null,
                null,
                null,
                GridPolygon: new ToolRecipeGridPolygon(
                [
                    new ToolRecipeGridPolygonVertex(1, 1),
                    new ToolRecipeGridPolygonVertex(1, 2.5),
                    new ToolRecipeGridPolygonVertex(2, 2),
                    new ToolRecipeGridPolygonVertex(2, 1)
                ]));
            var polygonDocument = circleDocument with
            {
                Name = "GridPolygon teaching fixture",
                Steps =
                [
                    new ToolRecipeStep(
                        "step.grid-polygon-authoring.01",
                        "grid-polygon-authoring",
                        "Irregular Region Authoring",
                        2,
                        ["source.circle", polygonSelection.Id],
                        "derived.grid-polygon-authoring.01",
                        [])
                ],
                Selections = [polygonSelection]
            };
            var polygonRecipePath = Path.Combine(fixtureRoot, "grid-polygon.ov3d-recipe.json");
            ToolRecipeDocumentStore.Save(polygonRecipePath, polygonDocument);
            var polygonWorkbench = new ToolWorkbenchViewModel(Path.Combine(fixtureRoot, "recent-polygon.json"));
            var polygonOpened = polygonWorkbench.TryOpenTeachingRecipe(polygonRecipePath, out var polygonOpenMessage);
            Check(
                "GridPolygon recipe opens with ordered numeric editor context",
                polygonOpened
                && polygonWorkbench.SelectPipelineStep("step.grid-polygon-authoring.01")
                && polygonWorkbench.SelectedStepTeachingSelection?.GridPolygon?.Vertices.SequenceEqual(
                    polygonSelection.GridPolygon!.Vertices) == true
                && polygonWorkbench.IsTeachingGridPolygonEditorVisible
                && polygonWorkbench.TeachingGridPolygonVertices.Select(vertex => vertex.Order).SequenceEqual([1, 2, 3, 4]),
                polygonOpenMessage);

            ToolWorkbenchTeachingCaptureRequestEventArgs? polygonRequest = null;
            polygonWorkbench.BeginTeachingSelectionCaptureRequested += (_, args) => polygonRequest = args;
            var polygonActionLogsBefore = polygonWorkbench.RunLog.Count(item =>
                item.Category is "Preview" or "Publish" or "Run");
            polygonWorkbench.BeginTeachingSelectionCaptureCommand.Execute(null);
            polygonWorkbench.UpdateTeachingSelectionCaptureState(true, 4, 3, true, "Polygon ready");
            polygonWorkbench.UpdateTeachingGridPolygonDraft(polygonSelection.GridPolygon);
            ToolRecipeGridPolygon? numericPolygonDraft = null;
            polygonWorkbench.TeachingGridPolygonDraftChanged += (_, args) => numericPolygonDraft = args.Polygon;
            polygonWorkbench.TeachingGridPolygonVertices[1].Column = 2.75;
            var firstPolygonVertex = polygonWorkbench.TeachingGridPolygonVertices[0];
            polygonWorkbench.MoveTeachingGridPolygonVertexDownCommand.Execute(firstPolygonVertex);
            polygonWorkbench.MoveTeachingGridPolygonVertexUpCommand.Execute(firstPolygonVertex);
            Check(
                "GridPolygon order and numeric draft stay transient until Apply",
                polygonRequest is
                {
                    Kind: ToolRecipeSelectionKinds.GridPolygon,
                    RequiredPointCount: 3,
                    ExistingSelection.GridPolygon: not null
                }
                && numericPolygonDraft?.Vertices.SequenceEqual(
                    [
                        new ToolRecipeGridPolygonVertex(1, 1),
                        new ToolRecipeGridPolygonVertex(1, 2.75),
                        new ToolRecipeGridPolygonVertex(2, 2),
                        new ToolRecipeGridPolygonVertex(2, 1)
                    ]) == true
                && polygonWorkbench.SelectedStepTeachingSelection?.GridPolygon?.Vertices.SequenceEqual(
                    polygonSelection.GridPolygon!.Vertices) == true
                && polygonWorkbench.RunLog.Count(item =>
                    item.Category is "Preview" or "Publish" or "Run") == polygonActionLogsBefore,
                $"draft={numericPolygonDraft};applied={polygonWorkbench.SelectedStepTeachingSelection?.GridPolygon}");

            var appliedPolygon = polygonSelection with { GridPolygon = numericPolygonDraft };
            var polygonApplied = polygonWorkbench.TryApplyCapturedTeachingSelection(appliedPolygon, out var polygonApplyMessage);
            Check(
                "explicit Apply replaces the same GridPolygon without inspection execution",
                polygonApplied
                && polygonWorkbench.SelectedStepTeachingSelection?.GridPolygon?.Vertices.SequenceEqual(
                    appliedPolygon.GridPolygon!.Vertices) == true
                && polygonWorkbench.RunLog.Count(item =>
                    item.Category is "Preview" or "Publish" or "Run") == polygonActionLogsBefore,
                polygonApplyMessage);

            var polygonSavedPath = Path.Combine(fixtureRoot, "grid-polygon-saved.ov3d-recipe.json");
            var polygonSaved = polygonWorkbench.TrySaveTeachingRecipe(polygonSavedPath, out var polygonSaveMessage);
            var polygonReopened = polygonSaved ? ToolRecipeDocumentStore.Load(polygonSavedPath) : null;
            Check(
                "Workbench GridPolygon save and reopen preserve exact vertex order and route",
                polygonSaved
                && polygonReopened?.SchemaVersion == ToolRecipeDocument.CurrentSchemaVersion
                && polygonReopened.Selections is [var savedPolygon]
                && savedPolygon.GridPolygon?.Vertices.SequenceEqual(appliedPolygon.GridPolygon!.Vertices) == true
                && polygonReopened.Steps.Single().InputEntityIds.SequenceEqual(
                    ["source.circle", polygonSelection.Id]),
                polygonSaveMessage);

            routeWarpage.InputEntityIdsText = routeThickness.OutputEntityId;
            var legacyRecipePath = Path.Combine(fixtureRoot, "legacy-incompatible-route.ov3d-recipe.json");
            Check(
                "legacy incompatible route is diagnosed and remains loadable for repair",
                routeWorkbench.FlowPortDiagnostics.Any(item =>
                    ReferenceEquals(item.Step, routeWarpage)
                    && item.Status == routeWorkbench.Localization.FlowPortIncompatible)
                && routeWorkbench.ValidationMessages.Any(item => item.Message.Contains(
                    "is MeasurementResult; HeightField is required.",
                    StringComparison.Ordinal))
                && routeWorkbench.TrySaveTeachingRecipe(legacyRecipePath, out _),
                routeWorkbench.FlowProblemsSummary);
            var repairWorkbench = new ToolWorkbenchViewModel(Path.Combine(fixtureRoot, "recent-repair.json"));
            repairWorkbench.ToolSearchText = "Warpage";
            var legacyOpened = repairWorkbench.TryOpenTeachingRecipe(legacyRecipePath, out var legacyOpenMessage);
            var legacyProblem = repairWorkbench.FlowPortDiagnostics.FirstOrDefault(item =>
                item.Step.ToolId == "warpage"
                && item.Status == repairWorkbench.Localization.FlowPortIncompatible);
            repairWorkbench.SelectedPipelineStep = legacyProblem?.Step;
            var directRepairProblem = repairWorkbench.SelectedStepFlowProblem;
            repairWorkbench.FocusFlowProblemStepCommand.Execute(directRepairProblem);
            var sameRepairProblemIdentity = directRepairProblem is not null
                && legacyProblem is not null
                && string.Equals(directRepairProblem.Step.Id, legacyProblem.Step.Id, StringComparison.Ordinal)
                && string.Equals(directRepairProblem.Port, legacyProblem.Port, StringComparison.Ordinal)
                && string.Equals(directRepairProblem.Kind, legacyProblem.Kind, StringComparison.Ordinal)
                && string.Equals(directRepairProblem.Status, legacyProblem.Status, StringComparison.Ordinal)
                && string.Equals(directRepairProblem.EntityId, legacyProblem.EntityId, StringComparison.Ordinal);
            var repairHasNoExecution = repairWorkbench.RunLog.All(item =>
                item.Category is not ("Preview" or "Publish" or "Run"));
            Check(
                "selected legacy step exposes Repair route and opens input editing without execution",
                legacyOpened
                && legacyProblem is not null
                && repairWorkbench.HasSelectedStepFlowProblem
                && directRepairProblem is not null
                && sameRepairProblemIdentity
                && ReferenceEquals(repairWorkbench.SelectedPipelineStep, directRepairProblem.Step)
                && repairWorkbench.IsSelectedToolInputSectionExpanded
                && repairWorkbench.IsAdvancedInputRouteEditingExpanded
                && repairHasNoExecution,
                $"opened={legacyOpened}; legacyProblem={legacyProblem is not null}; "
                + $"selectedProblem={repairWorkbench.HasSelectedStepFlowProblem}; "
                + $"directProblem={directRepairProblem is not null}; "
                + $"sameProblemIdentity={sameRepairProblemIdentity}; "
                + $"sameStep={ReferenceEquals(repairWorkbench.SelectedPipelineStep, directRepairProblem?.Step)}; "
                + $"inputExpanded={repairWorkbench.IsSelectedToolInputSectionExpanded}; "
                + $"advancedExpanded={repairWorkbench.IsAdvancedInputRouteEditingExpanded}; "
                + $"noExecution={repairHasNoExecution}; message={legacyOpenMessage}");
            Check(
                "successful recipe open clears the Tool Library search without execution",
                string.IsNullOrEmpty(repairWorkbench.ToolSearchText)
                && repairWorkbench.RunLog.All(item => item.Category is not ("Preview" or "Publish" or "Run")),
                $"search='{repairWorkbench.ToolSearchText}'");
            repairWorkbench.SelectedPipelineStep!.InputEntityIdsText = repairWorkbench.Source.Id;
            var repairedRecipePath = Path.Combine(fixtureRoot, "repaired-valid-route.ov3d-recipe.json");
            var repairedSaved = repairWorkbench.TrySaveTeachingRecipe(repairedRecipePath, out var repairedSaveMessage);
            var reopenedRouteWorkbench = new ToolWorkbenchViewModel(Path.Combine(fixtureRoot, "recent-reopened-route.json"));
            var repairedOpened = reopenedRouteWorkbench.TryOpenTeachingRecipe(repairedRecipePath, out _);
            Check(
                "valid repaired route survives save and reopen",
                repairedSaved
                && repairedOpened
                && reopenedRouteWorkbench.PipelineSteps.Single(step => step.ToolId == "warpage")
                    .InputEntityIds.SequenceEqual([reopenedRouteWorkbench.Source.Id]),
                repairedSaveMessage);

            var alignmentLanguage = OpenVisionLanguageService.CurrentLanguage;
            try
            {
                OpenVisionLanguageService.SetLanguage(OpenVisionLanguage.English, save: false);
                var alignment = new ToolWorkbenchViewModel();
                Check(
                    "alignment summary reports no taught stage initially",
                    alignment.AlignmentStatusSummary == "Alignment not taught",
                    alignment.AlignmentStatusSummary);
                alignment.SetC3DSource(sourcePath, markDirty: false);
                var legacyTool = new ToolWorkbenchToolItem(
                    "Transform", "XYZ Affine Transform", "xyz-affine-transform", 1,
                    "Legacy input", "Legacy output", "Verification-only legacy recipe step.", []);
                var legacyStep = new ToolWorkbenchPipelineStepItem(
                    "step.legacy-affine", legacyTool, alignment.Source.Id, "legacy.affine");
                alignment.PipelineSteps.Add(legacyStep);
                Check(
                    "legacy alignment summary reports the legacy step state",
                    alignment.AlignmentStatusSummary == $"Legacy XYZ Affine Transform | {legacyStep.State}",
                    alignment.AlignmentStatusSummary);
                var alignmentSolve = AddTool(alignment, "XYZ Affine Solve");
                Check(
                    "A1 alignment summary supersedes the legacy stage",
                    alignment.AlignmentStatusSummary == $"A1 XYZ Affine Solve | {alignmentSolve.State}",
                    alignment.AlignmentStatusSummary);
                var alignmentApply = AddTool(alignment, "Apply XYZ Affine");
                Check(
                    "A2 alignment summary supersedes A1",
                    alignment.AlignmentStatusSummary == $"A2 Apply XYZ Affine | {alignmentApply.State}",
                    alignment.AlignmentStatusSummary);
                var alignmentRegrid = AddTool(alignment, "Re-grid Height Map");
                Check(
                    "A3 alignment summary supersedes A2",
                    alignment.AlignmentStatusSummary == $"A3 Re-grid Height Map | {alignmentRegrid.State}",
                    alignment.AlignmentStatusSummary);
                var alignmentSummaryNotifications = 0;
                alignment.PropertyChanged += (_, args) =>
                {
                    if (args.PropertyName == nameof(ToolWorkbenchViewModel.AlignmentStatusSummary))
                    {
                        alignmentSummaryNotifications++;
                    }
                };
                var actionLogCount = alignment.RunLog.Count(item => item.Category is "Preview" or "Publish" or "Run");
                alignmentRegrid.State = "Published";
                Check(
                    "alignment step state change refreshes the header summary",
                    alignmentSummaryNotifications == 1
                    && alignment.AlignmentStatusSummary == "A3 Re-grid Height Map | Published",
                    $"notifications={alignmentSummaryNotifications}; summary={alignment.AlignmentStatusSummary}");
                var adapterStatusNotifications = 0;
                alignment.PropertyChanged += (_, args) =>
                {
                    if (args.PropertyName == nameof(ToolWorkbenchViewModel.SelectedStepAdapterStatus))
                    {
                        adapterStatusNotifications++;
                    }
                };
                Check(
                    "selected step adapter status keeps the existing English ready label",
                    alignment.SelectedStepAdapterStatus == "Typed adapter ready",
                    alignment.SelectedStepAdapterStatus);
                Check(
                    "alignment status refresh causes no Preview, Publish, or Run action",
                    alignment.RunLog.Count(item => item.Category is "Preview" or "Publish" or "Run") == actionLogCount,
                    $"actionLogs={actionLogCount}");
                OpenVisionLanguageService.SetLanguage(OpenVisionLanguage.Korean, save: false);
                Check(
                    "alignment summary refreshes localized labels without changing state",
                    alignmentSummaryNotifications == 2
                    && alignment.AlignmentStatusSummary == "A3 \uB192\uC774 \uB9F5 \uC7AC\uACA9\uC790\uD654 | Published",
                    $"notifications={alignmentSummaryNotifications}; summary={alignment.AlignmentStatusSummary}");
                Check(
                    "selected step adapter status refreshes its localized label without changing state",
                    adapterStatusNotifications == 1
                    && alignment.SelectedStepAdapterStatus == "\uC815\uC2DD \uC5B4\uB311\uD130 \uC900\uBE44\uB428",
                    $"notifications={adapterStatusNotifications}; status={alignment.SelectedStepAdapterStatus}");
            }
            finally
            {
                OpenVisionLanguageService.SetLanguage(alignmentLanguage, save: false);
            }

            var workbench = new ToolWorkbenchViewModel();
            Check(
                "initial empty recipe is clean and unsaved",
                !workbench.IsDirty && string.IsNullOrWhiteSpace(workbench.RecipePath),
                workbench.RecipeStateSummary);
            var emptyWorkspaceLanguage = OpenVisionLanguageService.CurrentLanguage;
            try
            {
                OpenVisionLanguageService.SetLanguage(OpenVisionLanguage.English, save: false);
                var englishInitialWorkspace = new SelectedToolWorkspaceViewModel(
                    new InspectionWorkspaceSelectionSession());
                var englishInitialParameterStatus = englishInitialWorkspace.ParameterStatus;
                var englishInitialHelp = englishInitialWorkspace.Help;
                var englishInitialCommonState = englishInitialWorkspace.CommonState;
                var englishInitialOutputPolicy = englishInitialWorkspace.OutputPolicy;
                var englishInitialParameterStatusExpected = workbench.Localization.StepParameterSelectTypedTool;
                var englishInitialOutputPolicyExpected = workbench.Localization.OutputEnabled;
                OpenVisionLanguageService.SetLanguage(OpenVisionLanguage.Korean, save: false);
                var koreanInitialWorkspace = new SelectedToolWorkspaceViewModel(
                    new InspectionWorkspaceSelectionSession());
                var koreanInitialParameterStatus = koreanInitialWorkspace.ParameterStatus;
                var koreanInitialHelp = koreanInitialWorkspace.Help;
                var koreanInitialCommonState = koreanInitialWorkspace.CommonState;
                var koreanInitialOutputPolicy = koreanInitialWorkspace.OutputPolicy;
                Check(
                    "selected-tool workspace initial defaults localize without recipe state",
                    englishInitialParameterStatus == englishInitialParameterStatusExpected
                    && englishInitialHelp == "Select an inspection step to see its inputs, parameters, regions, outputs, and authoring guidance."
                    && englishInitialCommonState == "Empty"
                    && englishInitialOutputPolicy == englishInitialOutputPolicyExpected
                    && koreanInitialParameterStatus == workbench.Localization.StepParameterSelectTypedTool
                    && koreanInitialHelp != englishInitialHelp
                    && koreanInitialCommonState == workbench.Localization.StateLabel(InspectionStepState.Empty)
                    && koreanInitialOutputPolicy == workbench.Localization.OutputEnabled
                    && englishInitialParameterStatus != koreanInitialParameterStatus
                    && englishInitialCommonState != koreanInitialCommonState
                    && englishInitialOutputPolicy != koreanInitialOutputPolicy
                    && !workbench.IsDirty,
                    $"enParameter={englishInitialParameterStatus}; koParameter={koreanInitialParameterStatus}; enState={englishInitialCommonState}; koState={koreanInitialCommonState}; enPolicy={englishInitialOutputPolicy}; koPolicy={koreanInitialOutputPolicy}");
                OpenVisionLanguageService.SetLanguage(OpenVisionLanguage.English, save: false);
                var englishEmptyWorkspaceTitle = workbench.SelectedToolWorkspace.Title;
                var englishEmptyWorkspaceState = workbench.SelectedToolWorkspace.State;
                var englishEmptyWorkspaceExample = workbench.SelectedToolWorkspace.Example;
                var englishEmptyWorkspaceOverlay = workbench.SelectedToolWorkspace.ExpectedOverlay;
                OpenVisionLanguageService.SetLanguage(OpenVisionLanguage.Korean, save: false);
                var koreanEmptyWorkspaceTitle = workbench.SelectedToolWorkspace.Title;
                var koreanEmptyWorkspaceState = workbench.SelectedToolWorkspace.State;
                var koreanEmptyWorkspaceExample = workbench.SelectedToolWorkspace.Example;
                var koreanEmptyWorkspaceOverlay = workbench.SelectedToolWorkspace.ExpectedOverlay;
                Check(
                    "selected-tool workspace empty title and state localize without changing recipe state",
                    englishEmptyWorkspaceTitle == "No taught step selected"
                    && englishEmptyWorkspaceState == "No selection"
                    && koreanEmptyWorkspaceTitle == workbench.Localization.NoTaughtStepSelected
                    && koreanEmptyWorkspaceState == workbench.Localization.NoSelectedToolState
                    && englishEmptyWorkspaceExample == "Select an inspection step to see a concrete authoring example."
                    && englishEmptyWorkspaceOverlay == "Select an inspection step to see the expected review overlay."
                    && koreanEmptyWorkspaceExample == workbench.Localization.SelectedToolEmptyExample
                    && koreanEmptyWorkspaceOverlay == workbench.Localization.SelectedToolEmptyExpectedOverlay
                    && englishEmptyWorkspaceTitle != koreanEmptyWorkspaceTitle
                    && englishEmptyWorkspaceExample != koreanEmptyWorkspaceExample
                    && englishEmptyWorkspaceOverlay != koreanEmptyWorkspaceOverlay
                    && !workbench.IsDirty,
                    $"enTitle={englishEmptyWorkspaceTitle}; koTitle={koreanEmptyWorkspaceTitle}; enState={englishEmptyWorkspaceState}; koState={koreanEmptyWorkspaceState}; enExample={englishEmptyWorkspaceExample}; koExample={koreanEmptyWorkspaceExample}; enOverlay={englishEmptyWorkspaceOverlay}; koOverlay={koreanEmptyWorkspaceOverlay}; dirty={workbench.IsDirty}");
            }
            finally
            {
                OpenVisionLanguageService.SetLanguage(emptyWorkspaceLanguage, save: false);
            }
            var sourceLoadCancellationRequested = false;
            workbench.CancelC3DSourceLoadRequested += (_, _) => sourceLoadCancellationRequested = true;
            workbench.BeginC3DSourceLoad(sourcePath);
            workbench.ReportC3DSourceLoadProgress(42.0);
            workbench.CancelC3DSourceLoadCommand.Execute(null);
            Check(
                "C3D source load state exposes progress, disables Open, and routes Cancel",
                workbench.IsC3DSourceLoading
                && Math.Abs(workbench.C3DSourceLoadProgressPercent - 42.0) < 0.001
                && workbench.C3DSourceLoadStatus.Contains(Path.GetFileName(sourcePath), StringComparison.Ordinal)
                && !workbench.LoadC3DSourceCommand.CanExecute(null)
                && sourceLoadCancellationRequested,
                $"loading={workbench.IsC3DSourceLoading}; progress={workbench.C3DSourceLoadProgressPercent:F1}; cancel={sourceLoadCancellationRequested}");
            workbench.CancelC3DSourceLoad(elapsedMilliseconds: 1);
            Check(
                "cancelled C3D load restores Open without modifying the recipe",
                !workbench.IsC3DSourceLoading
                && workbench.LoadC3DSourceCommand.CanExecute(null)
                && !workbench.IsDirty
                && string.IsNullOrWhiteSpace(workbench.RecipePath),
                $"loading={workbench.IsC3DSourceLoading}; open={workbench.LoadC3DSourceCommand.CanExecute(null)}; dirty={workbench.IsDirty}");
            Check(
                "catalog covers intended 3D teaching chain",
                workbench.Tools.Any(tool => tool.Name == "Filter")
                && workbench.Tools.Any(tool => tool.Name == "Height Difference Edge")
                && workbench.Tools.Any(tool => tool.Name == "3D Line Fit")
                && workbench.Tools.Any(tool => tool.Name == "Line Intersection")
                && workbench.Tools.Any(tool => tool.Name == "XYZ Affine Solve")
                && workbench.Tools.Any(tool => tool.Name == "Re-grid Height Map")
                && workbench.Tools.Any(tool => tool.Name == "Thickness")
                && workbench.Tools.Any(tool => tool.Name == "Warpage"),
                string.Join(", ", workbench.Tools.Select(tool => tool.Name)));
            Check(
                "source-less empty recipe can save but cannot execute",
                workbench.CanSaveTeachingRecipe && !workbench.IsTeachingRecipeExecutionReady,
                workbench.ValidationSummary);
            var emptySaved = workbench.TrySaveTeachingRecipe(emptyRecipePath, out var emptySaveMessage);
            Check("source-less empty recipe saves", emptySaved && File.Exists(emptyRecipePath), emptySaveMessage);
            var emptyStored = ToolRecipeDocumentStore.Load(emptyRecipePath);
            Check(
                "source-less empty recipe storage contract preserves zero steps and empty source path",
                emptyStored.Steps.Count == 0
                && string.IsNullOrEmpty(emptyStored.Source.Path)
                && ToolRecipeValidator.ValidateForStorage(emptyStored).IsValid
                && !ToolRecipeValidator.Validate(emptyStored).IsValid,
                $"steps={emptyStored.Steps.Count}; source='{emptyStored.Source.Path}'");
            var emptyReopened = new ToolWorkbenchViewModel();
            var emptyOpened = emptyReopened.TryOpenTeachingRecipe(emptyRecipePath, out var emptyOpenMessage);
            Check(
                "source-less empty recipe reopens as a saved editable draft",
                emptyOpened
                && emptyReopened.PipelineSteps.Count == 0
                && string.IsNullOrEmpty(emptyReopened.Source.Path)
                && emptyReopened.CanSaveTeachingRecipe
                && !emptyReopened.IsTeachingRecipeExecutionReady
                && !emptyReopened.IsDirty,
                emptyOpenMessage);
            var newLifecycle = new ToolWorkbenchViewModel();
            newLifecycle.SetC3DSource(sourcePath, markDirty: false);
            Check("automatic startup source does not create an unsaved-change prompt", !newLifecycle.IsDirty, newLifecycle.RecipeStateSummary);
            var loadedGrid = C3DHeightGrid.Load(sourcePath, maxRenderedPoints: 4);
            var viewerBinding = ToolRecipeSelectionSourceBindingVerifier.FromHeightGrid(loadedGrid);
            var viewerBoundWorkbench = new ToolWorkbenchViewModel();
            viewerBoundWorkbench.SetC3DSourceFromLoadedViewer(sourcePath, viewerBinding, markDirty: false);
            Check(
                "loaded Viewer C3D identity can be reused without changing recipe semantics",
                viewerBoundWorkbench.IsSourceReadyForRecipe
                && !viewerBoundWorkbench.IsDirty
                && string.Equals(
                    viewerBoundWorkbench.Source.Path,
                    Path.GetFullPath(sourcePath),
                    StringComparison.OrdinalIgnoreCase),
                viewerBoundWorkbench.SourceReadinessSummary);
            var invalidViewerBindingRejected = false;
            try
            {
                viewerBoundWorkbench.SetC3DSourceFromLoadedViewer(
                    sourcePath,
                    viewerBinding with { Format = "PLY" },
                    markDirty: false);
            }
            catch (ArgumentException)
            {
                invalidViewerBindingRejected = true;
            }
            Check(
                "loaded Viewer source reuse rejects a non-C3D identity",
                invalidViewerBindingRejected,
                $"rejected={invalidViewerBindingRejected}");
            newLifecycle.RecipeName = "Discarded draft";
            newLifecycle.ToolSearchText = "Filter";
            newLifecycle.CreateNewTeachingRecipe("Created recipe");
            Check(
                "New resets to a named source-less clean zero-step draft",
                newLifecycle.RecipeName == "Created recipe"
                && newLifecycle.PipelineSteps.Count == 0
                && string.IsNullOrWhiteSpace(newLifecycle.Source.Path)
                && !newLifecycle.IsSourceReadyForRecipe
                && !newLifecycle.AddSelectedToolCommand.CanExecute(null)
                && string.IsNullOrEmpty(newLifecycle.ToolSearchText)
                && string.IsNullOrWhiteSpace(newLifecycle.RecipePath)
                && !newLifecycle.IsDirty,
                newLifecycle.RecipeStateSummary);

            var failedOpenWorkbench = new ToolWorkbenchViewModel();
            failedOpenWorkbench.ToolSearchText = "Keep on failure";
            var failedOpenActionLogCount = failedOpenWorkbench.RunLog.Count(item =>
                item.Category is "Preview" or "Publish" or "Run");
            var failedOpen = failedOpenWorkbench.TryOpenTeachingRecipe(
                Path.Combine(fixtureRoot, "missing.ov3d-recipe.json"),
                out _);
            Check(
                "failed recipe open retains the visible search and changes no execution state",
                !failedOpen
                && failedOpenWorkbench.ToolSearchText == "Keep on failure"
                && failedOpenWorkbench.RunLog.Count(item =>
                    item.Category is "Preview" or "Publish" or "Run") == failedOpenActionLogCount,
                $"search='{failedOpenWorkbench.ToolSearchText}'; actionLogs={failedOpenActionLogCount}");

            workbench.RecipeName = "Fixture XYZ Affine Inspection";
            workbench.SetC3DSource(sourcePath);
            var filterTool = workbench.Tools.Single(tool => tool.Name == "Filter");
            var unavailableFilterGate = SourceQualityToolGate.Evaluate(
                filterTool.Id,
                "SourceC3D / RawHeightField",
                report: null,
                expectedSourceEntityId: workbench.Source.Id,
                expectedSourceContentSha256: workbench.SourceSession.SourceBinding?.ContentSha256);
            Check(
                "Filter remains blocked while Source Quality is unavailable",
                !unavailableFilterGate.IsAllowed
                && unavailableFilterGate.Reason == SourceQualityToolGateReason.ReportUnavailable,
                $"allowed={unavailableFilterGate.IsAllowed};reason={unavailableFilterGate.Reason};detail={unavailableFilterGate.Detail}");
            var sourceQualityReport = WaitForSourceQuality(workbench.SourceQuality);
            Check(
                "Filter becomes addable only after current Source Quality is ready",
                sourceQualityReport is not null
                && !workbench.SourceQuality.HasError
                && sourceQualityReport.Source.ContentSha256
                    == workbench.SourceSession.SourceBinding?.ContentSha256
                && workbench.AddSelectedToolCommand.CanExecute(filterTool),
                $"report={sourceQualityReport is not null};error={workbench.SourceQuality.Error};source={sourceQualityReport?.Source.ContentSha256};binding={workbench.SourceSession.SourceBinding?.ContentSha256};canAdd={workbench.AddSelectedToolCommand.CanExecute(filterTool)}");
            var binding = ToolRecipeSelectionSourceBindingVerifier.ReadIdentity(sourcePath);
            var filter = AddTool(workbench, "Filter");
            filter.Parameters.Single(parameter => parameter.Name == "KernelSize").Value = "5";
            var selectedWorkspaceLanguage = OpenVisionLanguageService.CurrentLanguage;
            try
            {
                OpenVisionLanguageService.SetLanguage(OpenVisionLanguage.English, save: false);
                var englishSelectedWorkspaceTitle = workbench.SelectedToolWorkspace.Title;
                var englishSelectedWorkspaceTitleExpected = string.Format(
                    workbench.Localization.SelectedPipelineStepTitleFormat,
                    filter.Order,
                    filter.ToolName);
                OpenVisionLanguageService.SetLanguage(OpenVisionLanguage.Korean, save: false);
                var koreanSelectedWorkspaceTitle = workbench.SelectedToolWorkspace.Title;
                Check(
                    "selected-tool workspace selected title uses the shared step format",
                    englishSelectedWorkspaceTitle == englishSelectedWorkspaceTitleExpected
                    && koreanSelectedWorkspaceTitle == string.Format(
                        workbench.Localization.SelectedPipelineStepTitleFormat,
                        filter.Order,
                        filter.ToolName)
                    && englishSelectedWorkspaceTitle != koreanSelectedWorkspaceTitle
                    && workbench.SelectedToolWorkspace.State == filter.State,
                    $"en={englishSelectedWorkspaceTitle}; ko={koreanSelectedWorkspaceTitle}; state={workbench.SelectedToolWorkspace.State}");
            }
            finally
            {
                OpenVisionLanguageService.SetLanguage(selectedWorkspaceLanguage, save: false);
            }
            var outputFallbackLanguage = OpenVisionLanguageService.CurrentLanguage;
            try
            {
                var fallbackWorkspace = new SelectedToolWorkspaceViewModel(
                    new InspectionWorkspaceSelectionSession());
                OpenVisionLanguageService.SetLanguage(OpenVisionLanguage.English, save: false);
                fallbackWorkspace.Refresh(new SelectedToolWorkspaceProjection(
                    filter,
                    null,
                    false,
                    false,
                    string.Empty,
                    Array.Empty<ToolWorkbenchArtifactItem>(),
                    Array.Empty<ToolWorkbenchDisplayedOutputItem>(),
                    SelectedToolOutputEvidence.Empty,
                    null,
                    null,
                    false,
                    false,
                    null,
                    null,
                    string.Empty,
                    string.Empty,
                    false,
                    false,
                    workbench.Localization));
                var englishOutput = fallbackWorkspace.Outputs.Single();
                var englishOutputState = englishOutput.State;
                var englishOutputDetail = englishOutput.Detail;
                var englishOutputStateExpected = workbench.Localization.FlowPortDeclared;
                var englishOutputValueLabelExpected = workbench.Localization.SelectedToolOutputValueLabel;
                var englishOutputDetailExpected = string.Format(
                    workbench.Localization.FlowPortDeclaredDetailFormat,
                    filter.OutputEntityId);
                OpenVisionLanguageService.SetLanguage(OpenVisionLanguage.Korean, save: false);
                fallbackWorkspace.Refresh(new SelectedToolWorkspaceProjection(
                    filter,
                    null,
                    false,
                    false,
                    string.Empty,
                    Array.Empty<ToolWorkbenchArtifactItem>(),
                    Array.Empty<ToolWorkbenchDisplayedOutputItem>(),
                    SelectedToolOutputEvidence.Empty,
                    null,
                    null,
                    false,
                    false,
                    null,
                    null,
                    string.Empty,
                    string.Empty,
                    false,
                    false,
                    workbench.Localization));
                var koreanOutput = fallbackWorkspace.Outputs.Single();
                Check(
                    "selected-tool workspace output fallback localizes without changing output identity or display state",
                    englishOutputState == englishOutputStateExpected
                    && englishOutputDetail == englishOutputDetailExpected
                    && englishOutput.ValueLabel == englishOutputValueLabelExpected
                    && koreanOutput.State == workbench.Localization.FlowPortDeclared
                    && koreanOutput.Detail == string.Format(
                        workbench.Localization.FlowPortDeclaredDetailFormat,
                        filter.OutputEntityId)
                    && koreanOutput.ValueLabel == workbench.Localization.SelectedToolOutputValueLabel
                    && englishOutputState != koreanOutput.State
                    && englishOutputDetail != koreanOutput.Detail
                    && englishOutput.ValueLabel != koreanOutput.ValueLabel
                    && koreanOutput.EntityId == filter.OutputEntityId
                    && !koreanOutput.CanShowInViewer
                    && !koreanOutput.CanPinToCompare
                    && !koreanOutput.IsShownInViewer
                    && !koreanOutput.IsPinnedToCompare,
                    $"enState={englishOutputState}; koState={koreanOutput.State}; enDetail={englishOutputDetail}; koDetail={koreanOutput.Detail}; enValueLabel={englishOutput.ValueLabel}; koValueLabel={koreanOutput.ValueLabel}; entity={koreanOutput.EntityId}; show={koreanOutput.CanShowInViewer}; pin={koreanOutput.CanPinToCompare}");
            }
            finally
            {
                OpenVisionLanguageService.SetLanguage(outputFallbackLanguage, save: false);
            }
            var outputEvidenceLanguage = OpenVisionLanguageService.CurrentLanguage;
            try
            {
                var outputEvidenceWorkspace = new SelectedToolWorkspaceViewModel(
                    new InspectionWorkspaceSelectionSession());
                SelectedToolOutputItem ReadOutputEvidence(
                    IReadOnlyList<SelectedToolOutputEvidence> evidence)
                {
                    outputEvidenceWorkspace.Refresh(new SelectedToolWorkspaceProjection(
                        filter,
                        null,
                        false,
                        false,
                        string.Empty,
                        Array.Empty<ToolWorkbenchArtifactItem>(),
                        Array.Empty<ToolWorkbenchDisplayedOutputItem>(),
                        evidence[0],
                        null,
                        null,
                        false,
                        false,
                        null,
                        null,
                        string.Empty,
                        string.Empty,
                        false,
                        false,
                        workbench.Localization));
                    return outputEvidenceWorkspace.Outputs.Single();
                }

                OpenVisionLanguageService.SetLanguage(OpenVisionLanguage.English, save: false);
                var englishStatuses = new[]
                {
                    workbench.Localization.Preview,
                    workbench.Localization.RecipeHealthPublished,
                    workbench.Localization.Preview,
                    workbench.Localization.Preview
                };
                var englishEvidence = new[]
                {
                    new SelectedToolOutputEvidence(workbench.Localization.SelectedToolRemovedOutliersLabel, "2", "count", englishStatuses[0]),
                    new SelectedToolOutputEvidence(workbench.Localization.SelectedToolReferenceRmsLabel, "0.125", "mm", englishStatuses[1]),
                    new SelectedToolOutputEvidence(workbench.Localization.SelectedToolOutputMissingCellsLabel, "3", "count", englishStatuses[2]),
                    new SelectedToolOutputEvidence(workbench.Localization.SelectedToolOutputCellsLabel, "9", "count", englishStatuses[3])
                };
                var englishLabels = englishEvidence.Select(evidence => evidence.ValueLabel).ToArray();
                var englishOutputs = englishEvidence.Select(evidence => ReadOutputEvidence([evidence])).ToArray();
                OpenVisionLanguageService.SetLanguage(OpenVisionLanguage.Korean, save: false);
                var koreanStatuses = new[]
                {
                    workbench.Localization.Preview,
                    workbench.Localization.RecipeHealthPublished,
                    workbench.Localization.Preview,
                    workbench.Localization.Preview
                };
                var koreanEvidence = new[]
                {
                    new SelectedToolOutputEvidence(workbench.Localization.SelectedToolRemovedOutliersLabel, "2", "count", koreanStatuses[0]),
                    new SelectedToolOutputEvidence(workbench.Localization.SelectedToolReferenceRmsLabel, "0.125", "mm", koreanStatuses[1]),
                    new SelectedToolOutputEvidence(workbench.Localization.SelectedToolOutputMissingCellsLabel, "3", "count", koreanStatuses[2]),
                    new SelectedToolOutputEvidence(workbench.Localization.SelectedToolOutputCellsLabel, "9", "count", koreanStatuses[3])
                };
                var koreanLabels = koreanEvidence.Select(evidence => evidence.ValueLabel).ToArray();
                var koreanOutputs = koreanEvidence.Select(evidence => ReadOutputEvidence([evidence])).ToArray();
                Check(
                    "selected-tool output evidence labels localize without changing values, units, status, or identity",
                    englishOutputs.Length == 4
                    && koreanOutputs.Length == 4
                    && englishOutputs.Zip(koreanOutputs).All(pair =>
                        pair.First.EntityId == filter.OutputEntityId
                        && pair.Second.EntityId == filter.OutputEntityId
                        && pair.First.Value == pair.Second.Value
                        && pair.First.Unit == pair.Second.Unit
                        && !string.IsNullOrWhiteSpace(pair.First.ResultStatus)
                        && !string.IsNullOrWhiteSpace(pair.Second.ResultStatus)
                        && !pair.First.CanShowInViewer
                        && !pair.First.CanPinToCompare
                        && !pair.Second.CanShowInViewer
                        && !pair.Second.CanPinToCompare)
                    && englishOutputs.Select(item => item.ValueLabel).SequenceEqual(englishLabels)
                    && koreanOutputs.Select(item => item.ValueLabel).SequenceEqual(koreanLabels)
                    && !englishLabels.SequenceEqual(koreanLabels),
                    $"en={string.Join(",", englishOutputs.Select(item => item.ValueLabel))}; ko={string.Join(",", koreanOutputs.Select(item => item.ValueLabel))}; values={string.Join(",", koreanOutputs.Select(item => item.Value))}");
                Check(
                    "selected-tool output evidence statuses localize without changing values, units, or identity",
                    englishOutputs.Length == 4
                    && koreanOutputs.Length == 4
                    && englishOutputs.Select(item => item.ResultStatus).SequenceEqual(englishStatuses)
                    && koreanOutputs.Select(item => item.ResultStatus).SequenceEqual(koreanStatuses)
                    && !englishStatuses.SequenceEqual(koreanStatuses)
                    && englishOutputs.Zip(koreanOutputs).All(pair =>
                        pair.First.EntityId == pair.Second.EntityId
                        && pair.First.Value == pair.Second.Value
                        && pair.First.Unit == pair.Second.Unit),
                    $"en={string.Join(",", englishOutputs.Select(item => item.ResultStatus))}; ko={string.Join(",", koreanOutputs.Select(item => item.ResultStatus))}; values={string.Join(",", koreanOutputs.Select(item => item.Value))}");
            }
            finally
            {
                OpenVisionLanguageService.SetLanguage(outputEvidenceLanguage, save: false);
            }
            var resultStatusLanguage = OpenVisionLanguageService.CurrentLanguage;
            try
            {
                var resultStatuses = Enum.GetValues<ResultStatus>();
                OpenVisionLanguageService.SetLanguage(OpenVisionLanguage.English, save: false);
                var englishResultStatuses = resultStatuses
                    .Select(workbench.Localization.ResultStatusLabel)
                    .ToArray();
                OpenVisionLanguageService.SetLanguage(OpenVisionLanguage.Korean, save: false);
                var koreanResultStatuses = resultStatuses
                    .Select(workbench.Localization.ResultStatusLabel)
                    .ToArray();
                Check(
                    "selected-tool measurement output status formatter localizes every ResultStatus value",
                    englishResultStatuses.SequenceEqual(["Not run", "Pass", "Fail", "Warning", "Error"])
                    && koreanResultStatuses.SequenceEqual(["미실행", "통과", "실패", "경고", "오류"])
                    && !englishResultStatuses.SequenceEqual(koreanResultStatuses),
                    $"en={string.Join(",", englishResultStatuses)}; ko={string.Join(",", koreanResultStatuses)}");
            }
            finally
            {
                OpenVisionLanguageService.SetLanguage(resultStatusLanguage, save: false);
            }
            var outputStateLanguage = OpenVisionLanguageService.CurrentLanguage;
            try
            {
                var outputStates = new[] { "Preview", "Published", "Stale", "Ready", "Disabled", "CustomState" };
                OpenVisionLanguageService.SetLanguage(OpenVisionLanguage.English, save: false);
                var englishOutputStates = outputStates
                    .Select(workbench.Localization.SelectedToolOutputStateLabel)
                    .ToArray();
                OpenVisionLanguageService.SetLanguage(OpenVisionLanguage.Korean, save: false);
                var koreanOutputStates = outputStates
                    .Select(workbench.Localization.SelectedToolOutputStateLabel)
                    .ToArray();
                Check(
                    "selected-tool output state labels localize known states and preserve unknown identity",
                    englishOutputStates.SequenceEqual(["Preview", "Published", "Stale", "Ready", "Disabled", "CustomState"])
                    && koreanOutputStates.SequenceEqual([
                        workbench.Localization.Preview,
                        workbench.Localization.RecipeHealthPublished,
                        workbench.Localization.FlowPortStale,
                        workbench.Localization.FlowPortReady,
                        workbench.Localization.OutputDisabled,
                        "CustomState"])
                    && !englishOutputStates.SequenceEqual(koreanOutputStates),
                    $"en={string.Join(",", englishOutputStates)}; ko={string.Join(",", koreanOutputStates)}");
            }
            finally
            {
                OpenVisionLanguageService.SetLanguage(outputStateLanguage, save: false);
            }
            var regionSummaryLanguage = OpenVisionLanguageService.CurrentLanguage;
            try
            {
                var regionWorkspace = new SelectedToolWorkspaceViewModel(
                    new InspectionWorkspaceSelectionSession());
                var regionSelection = new ToolRecipeSelection(
                    "selection.fixture-grid-rectangle",
                    "Fixture grid rectangle",
                    ToolRecipeSelectionKinds.GridRectangle,
                    sourcePath,
                    binding.FrameId ?? workbench.Source.FrameId,
                    binding,
                    new ToolRecipeGridRectangle(2, 3, 4, 5),
                    null,
                    null);
                var regionRequirement = new ToolWorkbenchTeachingSelectionRequirement(
                    "Fixture ROI",
                    ToolRecipeSelectionKinds.GridRectangle,
                    0,
                    false,
                    "Fixture ROI for Selected Tool Workspace summary.");
                OpenVisionLanguageService.SetLanguage(OpenVisionLanguage.English, save: false);
                regionWorkspace.Refresh(new SelectedToolWorkspaceProjection(
                    filter,
                    null,
                    false,
                    false,
                    string.Empty,
                    Array.Empty<ToolWorkbenchArtifactItem>(),
                    Array.Empty<ToolWorkbenchDisplayedOutputItem>(),
                    SelectedToolOutputEvidence.Empty,
                    regionRequirement,
                    regionSelection,
                    false,
                    false,
                    null,
                    null,
                    string.Empty,
                    string.Empty,
                    false,
                    false,
                    workbench.Localization));
                var englishRegion = regionWorkspace.Regions.Single();
                var englishRegionDetailExpected = string.Format(
                    workbench.Localization.SelectedToolGridRectangleFormat,
                    regionSelection.GridRectangle!.Column,
                    regionSelection.GridRectangle.Row,
                    regionSelection.GridRectangle.ColumnCount,
                    regionSelection.GridRectangle.RowCount);
                OpenVisionLanguageService.SetLanguage(OpenVisionLanguage.Korean, save: false);
                regionWorkspace.Refresh(new SelectedToolWorkspaceProjection(
                    filter,
                    null,
                    false,
                    false,
                    string.Empty,
                    Array.Empty<ToolWorkbenchArtifactItem>(),
                    Array.Empty<ToolWorkbenchDisplayedOutputItem>(),
                    SelectedToolOutputEvidence.Empty,
                    regionRequirement,
                    regionSelection,
                    false,
                    false,
                    null,
                    null,
                    string.Empty,
                    string.Empty,
                    false,
                    false,
                    workbench.Localization));
                var koreanRegion = regionWorkspace.Regions.Single();
                Check(
                    "selected-tool workspace region summary localizes without changing selection identity or lifecycle",
                    englishRegion.Detail == englishRegionDetailExpected
                    && koreanRegion.Detail == string.Format(
                        workbench.Localization.SelectedToolGridRectangleFormat,
                        regionSelection.GridRectangle.Column,
                        regionSelection.GridRectangle.Row,
                        regionSelection.GridRectangle.ColumnCount,
                        regionSelection.GridRectangle.RowCount)
                    && englishRegion.Detail != koreanRegion.Detail
                    && englishRegion.SelectionId == koreanRegion.SelectionId
                    && englishRegion.Lifecycle == koreanRegion.Lifecycle
                    && englishRegion.State == "Applied"
                    && koreanRegion.State == workbench.Localization.RoiApplied,
                    $"en={englishRegion.Detail}; ko={koreanRegion.Detail}; id={koreanRegion.SelectionId}; lifecycle={koreanRegion.Lifecycle}; state={koreanRegion.State}");

                var regionCircleSelection = regionSelection with
                {
                    Id = "selection.fixture-grid-circle",
                    Name = "Fixture grid circle",
                    Kind = ToolRecipeSelectionKinds.GridCircle,
                    GridRectangle = null,
                    GridCircle = new ToolRecipeGridCircle(3, 3, 1)
                };
                var circleRequirement = regionRequirement with
                {
                    Name = "Fixture circle",
                    Kind = ToolRecipeSelectionKinds.GridCircle
                };
                OpenVisionLanguageService.SetLanguage(OpenVisionLanguage.English, save: false);
                regionWorkspace.Refresh(new SelectedToolWorkspaceProjection(
                    filter,
                    null,
                    false,
                    false,
                    string.Empty,
                    Array.Empty<ToolWorkbenchArtifactItem>(),
                    Array.Empty<ToolWorkbenchDisplayedOutputItem>(),
                    SelectedToolOutputEvidence.Empty,
                    circleRequirement,
                    regionCircleSelection,
                    false,
                    false,
                    null,
                    null,
                    string.Empty,
                    string.Empty,
                    false,
                    false,
                    workbench.Localization));
                var englishCircle = regionWorkspace.Regions.Single();
                OpenVisionLanguageService.SetLanguage(OpenVisionLanguage.Korean, save: false);
                regionWorkspace.Refresh(new SelectedToolWorkspaceProjection(
                    filter,
                    null,
                    false,
                    false,
                    string.Empty,
                    Array.Empty<ToolWorkbenchArtifactItem>(),
                    Array.Empty<ToolWorkbenchDisplayedOutputItem>(),
                    SelectedToolOutputEvidence.Empty,
                    circleRequirement,
                    regionCircleSelection,
                    false,
                    false,
                    null,
                    null,
                    string.Empty,
                    string.Empty,
                    false,
                    false,
                    workbench.Localization));
                var koreanCircle = regionWorkspace.Regions.Single();
                Check(
                    "selected-tool workspace localizes known non-rectangle selection kinds while preserving identity and lifecycle",
                    englishCircle.Detail == $"Grid circle selection | {regionCircleSelection.Id}"
                    && koreanCircle.Detail == $"격자 원 선택 | {regionCircleSelection.Id}"
                    && englishCircle.Detail != koreanCircle.Detail
                    && englishCircle.SelectionId == koreanCircle.SelectionId
                    && englishCircle.Lifecycle == koreanCircle.Lifecycle
                    && englishCircle.State == "Applied"
                    && koreanCircle.State == workbench.Localization.RoiApplied,
                    $"en={englishCircle.Detail}; ko={koreanCircle.Detail}; id={koreanCircle.SelectionId}; lifecycle={koreanCircle.Lifecycle}; state={koreanCircle.State}");

                var unknownSelection = regionCircleSelection with
                {
                    Id = "selection.fixture-custom",
                    Name = "Fixture custom",
                    Kind = "custom-kind",
                    GridCircle = null
                };
                var unknownRequirement = circleRequirement with
                {
                    Name = "Fixture custom",
                    Kind = "custom-kind"
                };
                regionWorkspace.Refresh(new SelectedToolWorkspaceProjection(
                    filter,
                    null,
                    false,
                    false,
                    string.Empty,
                    Array.Empty<ToolWorkbenchArtifactItem>(),
                    Array.Empty<ToolWorkbenchDisplayedOutputItem>(),
                    SelectedToolOutputEvidence.Empty,
                    unknownRequirement,
                    unknownSelection,
                    false,
                    false,
                    null,
                    null,
                    string.Empty,
                    string.Empty,
                    false,
                    false,
                    workbench.Localization));
                var unknownRegion = regionWorkspace.Regions.Single();
                Check(
                    "selected-tool workspace preserves unknown selection kind identity",
                    unknownRegion.Detail == $"custom-kind | {unknownSelection.Id}"
                    && unknownRegion.SelectionId == unknownSelection.Id
                    && unknownRegion.Lifecycle == InspectionWorkspaceRegionLifecycleState.Applied,
                    $"detail={unknownRegion.Detail}; id={unknownRegion.SelectionId}; lifecycle={unknownRegion.Lifecycle}");
            }
            finally
            {
                OpenVisionLanguageService.SetLanguage(regionSummaryLanguage, save: false);
            }
            var inputFallbackLanguage = OpenVisionLanguageService.CurrentLanguage;
            try
            {
                var inputFallbackWorkspace = new SelectedToolWorkspaceViewModel(
                    new InspectionWorkspaceSelectionSession());
                var inputFallbackTool = new ToolWorkbenchToolItem(
                    "Verification",
                    "Input fallback",
                    "input-fallback",
                    2,
                    "HeightField",
                    "MeasurementResult",
                    "Verification-only input fallback fixture.",
                    []);
                var inputFallbackStep = new ToolWorkbenchPipelineStepItem(
                    "step.input-fallback",
                    inputFallbackTool,
                    "source.fixture",
                    "derived.input-fallback");
                var inputFallbackArtifact = new ToolWorkbenchArtifactItem(
                    "source.fixture",
                    "Fixture source",
                    "HeightField",
                    "Published",
                    "source.fixture",
                    string.Empty,
                    "mm",
                    binding.FrameId ?? workbench.Source.FrameId,
                    string.Empty,
                    "Fixture input artifact.",
                    inputFallbackStep,
                    "Source");
                OpenVisionLanguageService.SetLanguage(OpenVisionLanguage.English, save: false);
                inputFallbackWorkspace.Refresh(new SelectedToolWorkspaceProjection(
                    inputFallbackStep,
                    null,
                    false,
                    false,
                    string.Empty,
                    [inputFallbackArtifact],
                    Array.Empty<ToolWorkbenchDisplayedOutputItem>(),
                    SelectedToolOutputEvidence.Empty,
                    null,
                    null,
                    false,
                    false,
                    null,
                    null,
                    string.Empty,
                    string.Empty,
                    false,
                    false,
                    workbench.Localization));
                var englishInputs = inputFallbackWorkspace.Inputs.ToArray();
                var englishInputContractExpected = string.Format(
                    workbench.Localization.SelectedToolInputFormat,
                    2);
                var englishInputNotAssignedExpected = workbench.Localization.SelectedToolInputNotAssigned;
                var englishInputMissingExpected = workbench.Localization.RoiMissing;
                var englishInputPublishedState = workbench.Localization.SelectedToolOutputStateLabel("Published");
                OpenVisionLanguageService.SetLanguage(OpenVisionLanguage.Korean, save: false);
                inputFallbackWorkspace.Refresh(new SelectedToolWorkspaceProjection(
                    inputFallbackStep,
                    null,
                    false,
                    false,
                    string.Empty,
                    [inputFallbackArtifact],
                    Array.Empty<ToolWorkbenchDisplayedOutputItem>(),
                    SelectedToolOutputEvidence.Empty,
                    null,
                    null,
                    false,
                    false,
                    null,
                    null,
                    string.Empty,
                    string.Empty,
                    false,
                    false,
                    workbench.Localization));
                var koreanInputs = inputFallbackWorkspace.Inputs.ToArray();
                var koreanInputPublishedState = workbench.Localization.SelectedToolOutputStateLabel("Published");
                Check(
                    "selected-tool workspace input fallbacks localize without changing entity or artifact state",
                    englishInputs.Length == 2
                    && koreanInputs.Length == 2
                    && englishInputs[0].EntityId == koreanInputs[0].EntityId
                    && englishInputs[0].DisplayName == "Fixture source"
                    && koreanInputs[0].DisplayName == "Fixture source"
                    && englishInputs[0].State == englishInputPublishedState
                    && koreanInputs[0].State == koreanInputPublishedState
                    && englishInputs[1].EntityId.Length == 0
                    && koreanInputs[1].EntityId.Length == 0
                    && englishInputs[1].RequiredContract == englishInputContractExpected
                    && koreanInputs[1].RequiredContract == string.Format(
                        workbench.Localization.SelectedToolInputFormat,
                        2)
                    && englishInputs[1].DisplayName == englishInputNotAssignedExpected
                    && koreanInputs[1].DisplayName == workbench.Localization.SelectedToolInputNotAssigned
                    && englishInputs[1].State == englishInputMissingExpected
                    && koreanInputs[1].State == workbench.Localization.RoiMissing
                    && englishInputs[1].RequiredContract != koreanInputs[1].RequiredContract
                    && englishInputs[1].DisplayName != koreanInputs[1].DisplayName,
                    $"enContract={englishInputs[1].RequiredContract}; koContract={koreanInputs[1].RequiredContract}; enName={englishInputs[1].DisplayName}; koName={koreanInputs[1].DisplayName}; enState={englishInputs[1].State}; koState={koreanInputs[1].State}; entity={koreanInputs[0].EntityId}; artifactState={koreanInputs[0].State}");

                var currentSelectionArtifact = inputFallbackArtifact with
                {
                    State = "Current selection",
                    Detail = "Verification-only current selection fixture."
                };
                OpenVisionLanguageService.SetLanguage(OpenVisionLanguage.English, save: false);
                inputFallbackWorkspace.Refresh(new SelectedToolWorkspaceProjection(
                    inputFallbackStep,
                    null,
                    false,
                    false,
                    string.Empty,
                    [currentSelectionArtifact],
                    Array.Empty<ToolWorkbenchDisplayedOutputItem>(),
                    SelectedToolOutputEvidence.Empty,
                    null,
                    null,
                    false,
                    false,
                    null,
                    null,
                    string.Empty,
                    string.Empty,
                    false,
                    false,
                    workbench.Localization));
                var englishCurrentSelectionInput = inputFallbackWorkspace.Inputs.First();
                OpenVisionLanguageService.SetLanguage(OpenVisionLanguage.Korean, save: false);
                inputFallbackWorkspace.Refresh(new SelectedToolWorkspaceProjection(
                    inputFallbackStep,
                    null,
                    false,
                    false,
                    string.Empty,
                    [currentSelectionArtifact],
                    Array.Empty<ToolWorkbenchDisplayedOutputItem>(),
                    SelectedToolOutputEvidence.Empty,
                    null,
                    null,
                    false,
                    false,
                    null,
                    null,
                    string.Empty,
                    string.Empty,
                    false,
                    false,
                    workbench.Localization));
                var koreanCurrentSelectionInput = inputFallbackWorkspace.Inputs.First();
                Check(
                    "selected-tool workspace input artifact state localizes without changing canonical identity",
                    englishCurrentSelectionInput.State == "Current selection"
                    && koreanCurrentSelectionInput.State == "현재 선택"
                    && englishCurrentSelectionInput.State != koreanCurrentSelectionInput.State
                    && englishCurrentSelectionInput.EntityId == currentSelectionArtifact.Id
                    && koreanCurrentSelectionInput.EntityId == currentSelectionArtifact.Id
                    && englishCurrentSelectionInput.DisplayName == currentSelectionArtifact.DisplayName
                    && koreanCurrentSelectionInput.DisplayName == currentSelectionArtifact.DisplayName
                    && englishCurrentSelectionInput.FrameId == (currentSelectionArtifact.FrameId ?? string.Empty)
                    && koreanCurrentSelectionInput.FrameId == (currentSelectionArtifact.FrameId ?? string.Empty)
                    && englishCurrentSelectionInput.Unit == currentSelectionArtifact.Unit
                    && koreanCurrentSelectionInput.Unit == currentSelectionArtifact.Unit,
                    $"enState={englishCurrentSelectionInput.State}; koState={koreanCurrentSelectionInput.State}; entity={koreanCurrentSelectionInput.EntityId}; frame={koreanCurrentSelectionInput.FrameId}; unit={koreanCurrentSelectionInput.Unit}");
            }
            finally
            {
                OpenVisionLanguageService.SetLanguage(inputFallbackLanguage, save: false);
            }
            var parameterStatusLanguage = OpenVisionLanguageService.CurrentLanguage;
            try
            {
                OpenVisionLanguageService.SetLanguage(OpenVisionLanguage.English, save: false);
                var englishParameterStatus = workbench.StepParameterEditStatus;
                OpenVisionLanguageService.SetLanguage(OpenVisionLanguage.Korean, save: false);
                var koreanParameterStatus = workbench.StepParameterEditStatus;
                workbench.MarkSelectedStepParameterDraftDirty();
                var koreanPendingParameterStatus = workbench.StepParameterEditStatus;
                Check(
                    "step parameter lifecycle status localizes and refreshes without executing inspection",
                    englishParameterStatus == "Parameters match the committed recipe. Editing does not run Preview or Publish."
                    && koreanParameterStatus == workbench.Localization.StepParameterMatchesRecipe
                    && koreanPendingParameterStatus == workbench.Localization.StepParameterPendingChanges
                    && englishParameterStatus != koreanParameterStatus
                    && workbench.HasPendingStepParameterChanges,
                    $"en={englishParameterStatus}; ko={koreanParameterStatus}; pending={koreanPendingParameterStatus}; pendingState={workbench.HasPendingStepParameterChanges}");
            }
            finally
            {
                workbench.DiscardSelectedStepParameterDraft();
                OpenVisionLanguageService.SetLanguage(parameterStatusLanguage, save: false);
            }
            Check(
                "typed Filter is ready for explicit Preview only",
                filter.State == "Ready"
                && workbench.PreviewSelectedStepCommand.CanExecute(null)
                && !workbench.PublishSelectedStepCommand.CanExecute(null),
                $"state={filter.State}; preview={workbench.PreviewSelectedStepCommand.CanExecute(null)}; publish={workbench.PublishSelectedStepCommand.CanExecute(null)}");
            string? requestedToolLabId = null;
            workbench.ToolLabRequested += (_, args) => requestedToolLabId = args.ToolId;
            Check(
                "selected Filter exposes its routed input/output and focused Tool Lab",
                workbench.SelectedRouteInputIds == workbench.Source.Id
                && workbench.SelectedRouteOutputId == filter.OutputEntityId
                && workbench.IsSelectedToolLabAvailable
                && workbench.OpenSelectedToolLabCommand.CanExecute(null),
                $"input={workbench.SelectedRouteInputIds}; output={workbench.SelectedRouteOutputId}; available={workbench.IsSelectedToolLabAvailable}");
            workbench.OpenSelectedToolLabCommand.Execute(null);
            Check("focused Tool Lab request preserves selected Filter", requestedToolLabId == "filter" && ReferenceEquals(workbench.SelectedPipelineStep, filter), requestedToolLabId ?? "(none)");

            var edge = AddTool(workbench, "Height Difference Edge");
            var edgeSelection = new ToolRecipeSelection(
                "selection.edge-search-roi",
                "Edge search ROI",
                ToolRecipeSelectionKinds.GridRectangle,
                workbench.Source.Id,
                workbench.Source.FrameId,
                binding,
                new ToolRecipeGridRectangle(0, 0, 2, 2),
                null,
                null);
            workbench.Selections.Add(edgeSelection);
            edge.InputEntityIdsText = $"{filter.OutputEntityId}; {edgeSelection.Id}";
            edge.Parameters.Single(parameter => parameter.Name == "ComparisonAxis").Value = "AcrossColumns";

            var firstLine = AddTool(workbench, "3D Line Fit");
            firstLine.InputEntityIdsText = edge.OutputEntityId;
            var secondLine = AddTool(workbench, "3D Line Fit");
            secondLine.InputEntityIdsText = edge.OutputEntityId;

            var corner = AddTool(workbench, "Line Intersection");
            corner.InputEntityIdsText = $"{firstLine.OutputEntityId}; {secondLine.OutputEntityId}";

            workbench.NewReferenceId = "reference.fixture-landmarks";
            workbench.NewReferenceName = "Fixture landmarks";
            workbench.NewReferenceKind = "Reference landmark set";
            workbench.AddReferenceCommand.Execute(null);

            var correspondence = AddTool(workbench, "Landmark Correspondence");
            var selectionSummaryLanguage = OpenVisionLanguageService.CurrentLanguage;
            try
            {
                OpenVisionLanguageService.SetLanguage(OpenVisionLanguage.English, save: false);
                var englishSelectionSummary = workbench.SelectedStepTeachingSelectionSummary;
                OpenVisionLanguageService.SetLanguage(OpenVisionLanguage.Korean, save: false);
                var koreanSelectionSummary = workbench.SelectedStepTeachingSelectionSummary;
                Check(
                    "selected-step teaching-selection fallback localizes without creating a selection",
                    englishSelectionSummary == "No recipe-owned selection is routed to this step."
                    && koreanSelectionSummary == "\uC774 \uB2E8\uACC4\uC5D0 \uB808\uC2DC\uD53C \uC18C\uC720 \uC120\uD0DD\uC774 \uC5F0\uACB0\uB418\uC9C0 \uC54A\uC74C"
                    && workbench.SelectedStepTeachingSelection is null,
                    $"en={englishSelectionSummary}; ko={koreanSelectionSummary}");
            }
            finally
            {
                OpenVisionLanguageService.SetLanguage(selectionSummaryLanguage, save: false);
            }
            var correspondenceSelection = new ToolRecipeSelection(
                "selection.fixture-correspondences",
                "Fixture correspondences",
                ToolRecipeSelectionKinds.LandmarkCorrespondenceSet,
                workbench.Source.Id,
                workbench.Source.FrameId,
                binding,
                null,
                null,
                [
                    new ToolRecipeLandmarkCorrespondence(edge.OutputEntityId, "fixture.p1", new ToolRecipeXyz(0, 0, 0), "frame.fixture"),
                    new ToolRecipeLandmarkCorrespondence(firstLine.OutputEntityId, "fixture.p2", new ToolRecipeXyz(1, 0, 0), "frame.fixture"),
                    new ToolRecipeLandmarkCorrespondence(secondLine.OutputEntityId, "fixture.p3", new ToolRecipeXyz(0, 1, 0), "frame.fixture"),
                    new ToolRecipeLandmarkCorrespondence(corner.OutputEntityId, "fixture.p4", new ToolRecipeXyz(0, 0, 1), "frame.fixture")
                ],
                new ToolRecipeLandmarkCorrespondenceDescriptor(
                    "frame.fixture",
                    "raw-height",
                    "Structural teaching fixture",
                    "R1",
                    "ExactlyFour",
                    "CurrentPublishedCornerAnchor",
                    "RequireNonDegenerateTetrahedra",
                    0.000001));
            workbench.Selections.Add(correspondenceSelection);
            correspondence.InputEntityIdsText = correspondenceSelection.Id;
            var affine = AddTool(workbench, "XYZ Affine Solve");
            affine.InputEntityIdsText = correspondence.OutputEntityId;
            var regrid = AddTool(workbench, "Re-grid Height Map");
            regrid.InputEntityIdsText = affine.OutputEntityId;
            var thicknessReferenceSelection = new OpenVisionLab.ThreeD.Core.ToolRecipeSelection(
                "selection.thickness-reference-roi", "Thickness Reference ROI", OpenVisionLab.ThreeD.Core.ToolRecipeSelectionKinds.GridRectangle,
                workbench.Source.Id, workbench.Source.FrameId, binding,
                new OpenVisionLab.ThreeD.Core.ToolRecipeGridRectangle(0, 0, 2, 2), null, null);
            var measurementSelection = new OpenVisionLab.ThreeD.Core.ToolRecipeSelection(
                "selection.measurement-roi", "Measurement ROI", OpenVisionLab.ThreeD.Core.ToolRecipeSelectionKinds.GridRectangle,
                workbench.Source.Id, workbench.Source.FrameId, binding,
                new OpenVisionLab.ThreeD.Core.ToolRecipeGridRectangle(0, 0, 2, 2), null, null);
            workbench.Selections.Add(thicknessReferenceSelection);
            workbench.Selections.Add(measurementSelection);
            var thickness = AddTool(workbench, "Thickness");
            thickness.ToolName = "Tab 1 Thickness";
            thickness.InputEntityIdsText = $"{workbench.Source.Id}; {thicknessReferenceSelection.Id}; {measurementSelection.Id}";
            thickness.Parameters.Single(parameter => parameter.Name == "MaximumThickness").Value = "120";
            var warpage = AddTool(workbench, "Warpage");
            warpage.InputEntityIdsText = $"{workbench.Source.Id}; {measurementSelection.Id}";
            warpage.Parameters.Single(parameter => parameter.Name == "MaximumPeakToValley").Value = "80";
            var review = AddTool(workbench, "Overlay / Control Review");
            review.InputEntityIdsText = warpage.OutputEntityId;

            workbench.ValidateTeachingRecipeCommand.Execute(null);
            Check(
                "whole Run is blocked while downstream rows have no adapters",
                !workbench.RunTeachingRecipeCommand.CanExecute(null),
                "Only the selected Filter Preview adapter exists in this 11-step fixture.");
            Check(
                "ordered entity routing validates",
                workbench.CanSaveTeachingRecipe && workbench.IsTeachingRecipeExecutionReady && workbench.PipelineSteps.Count == 11,
                workbench.ValidationSummary);
            Check(
                "affine solve keeps its exact numerical contract without implicit execution",
                affine.ToolId == "xyz-affine-solve"
                && affine.Parameters.Single(parameter => parameter.Name == "SolvePolicy").Value == "ExactFourPartialPivot"
                && affine.Parameters.Any(parameter => parameter.Name == "MaximumConditionEstimate")
                && affine.Parameters.Any(parameter => parameter.Name == "ArithmeticResidualWarning"),
                string.Join(" | ", affine.Parameters.Select(parameter => $"{parameter.Name}={parameter.Value}")));

            var saved = workbench.TrySaveTeachingRecipe(recipePath, out var saveMessage);
            Check("save teaching JSON", saved && File.Exists(recipePath), saveMessage);
            Check("save clears modified state", saved && !workbench.IsDirty, workbench.RecipeStateSummary);
            workbench.SetC3DSource(sourcePath);
            Check("reloading the same source path preserves saved state", !workbench.IsDirty, workbench.RecipeStateSummary);

            var stored = ToolRecipeDocumentStore.Load(recipePath);
            Check(
                "saved document preserves source, reference, steps, and parameters",
                stored.Name == "Fixture XYZ Affine Inspection"
                && stored.Source.Path == Path.GetFullPath(sourcePath)
                && stored.References.Single().Id == "reference.fixture-landmarks"
                && stored.Steps.Count == 11
                && stored.Source.ContentSha256?.Length == 64
                && stored.Steps.Single(step => step.ToolId == "filter").Parameters.Single(parameter => parameter.Name == "KernelSize").Value == "5",
                $"steps={stored.Steps.Count}; source={stored.Source.Path}");

            var reopened = new ToolWorkbenchViewModel();
            var opened = reopened.TryOpenTeachingRecipe(recipePath, out var openMessage);
            Check(
                "reopen restores editable teaching graph",
                opened
                && !reopened.IsDirty
                && reopened.CanSaveTeachingRecipe
                && reopened.PipelineSteps.Count == 11
                && reopened.References.Single().Id == "reference.fixture-landmarks"
                && reopened.SelectedPipelineStep?.ToolName == "Filter",
                openMessage);
            Check(
                "recipe session owns reopened identity, lifecycle, and validation state",
                reopened.RecipeSession.SchemaVersion == reopened.RecipeSchemaVersion
                && reopened.RecipeSession.Name == reopened.RecipeName
                && reopened.RecipeSession.Path == reopened.RecipePath
                && reopened.RecipeSession.IsDirty == reopened.IsDirty
                && reopened.RecipeSession.StorageValidation.IsValid == reopened.CanSaveTeachingRecipe
                && reopened.RecipeSession.SourceBindingErrors.Count == 0,
                $"schema={reopened.RecipeSession.SchemaVersion}; path={reopened.RecipeSession.Path}; dirty={reopened.RecipeSession.IsDirty}; storageValid={reopened.RecipeSession.StorageValidation.IsValid}");
            Check(
                "step instance name survives save and reopen",
                reopened.PipelineSteps.Single(step => step.ToolId == "thickness").ToolName == "Tab 1 Thickness",
                reopened.PipelineSteps.Single(step => step.ToolId == "thickness").ToolName);

            var restoredFilter = reopened.PipelineSteps.Single(step => step.ToolId == "filter");
            restoredFilter.InputEntityIdsText = "missing.entity";
            Check("editing reopened recipe marks it modified", reopened.IsDirty, reopened.RecipeStateSummary);
            Check(
                "invalid entity route is blocked",
                !reopened.CanSaveTeachingRecipe
                && reopened.ValidationMessages.Any(message => message.Level == "Error" && message.Message.Contains("missing.entity", StringComparison.OrdinalIgnoreCase)),
                reopened.ValidationSummary);

            restoredFilter.InputEntityIdsText = reopened.Source.Id;
            Check("route correction restores save eligibility", reopened.CanSaveTeachingRecipe, reopened.ValidationSummary);

            var templatePath = Path.Combine(
                Environment.CurrentDirectory,
                "recipes",
                "c3d-xyz-affine-teaching-template.ov3d-teach.json");
            var template = new ToolWorkbenchViewModel();
            var templateOpened = template.TryOpenTeachingRecipe(templatePath, out var templateMessage);
            Check(
                "shipped legacy affine scaffold opens as a repairable draft but cannot execute with missing selection roles",
                templateOpened
                && template.CanSaveTeachingRecipe
                && !template.IsTeachingRecipeExecutionReady
                && template.PipelineSteps.Count == 17
                && File.Exists(template.Source.Path)
                && template.ValidationMessages.Any(item => item.Message.Contains(
                    "search-region",
                    StringComparison.Ordinal))
                && template.ValidationMessages.Any(item => item.Message.Contains(
                    "correspondences",
                    StringComparison.Ordinal)),
                templateOpened ? $"{template.Source.Path} | {template.ValidationSummary}" : templateMessage);

            if (templateOpened)
            {
                var healthCountTotal = template.RecipeHealthReadyCount
                    + template.RecipeHealthNeedsInputCount
                    + template.RecipeHealthNeedsSelectionCount
                    + template.RecipeHealthNeedsParametersCount
                    + template.RecipeHealthStalePreviewCount
                    + template.RecipeHealthPublishedCount;
                Check(
                    "recipe health assigns every long-chain step to exactly one visible state",
                    template.RecipeHealthItems.Count == 17
                    && healthCountTotal == 17
                    && template.RecipeHealthItems.Select(item => item.Step.Id).Distinct(StringComparer.Ordinal).Count() == 17,
                    $"items={template.RecipeHealthItems.Count}; counts={healthCountTotal}; summary={template.RecipeHealthCountsPrimary} | {template.RecipeHealthCountsSecondary}");

                var templateFilter = template.PipelineSteps[0];
                var firstDependent = template.PipelineSteps[1];
                var originalFilterState = templateFilter.State;
                var originalDependentState = firstDependent.State;
                var kernel = templateFilter.Parameters.Single(parameter => parameter.Name == "KernelSize");
                var originalKernel = kernel.Value;
                ToolWorkbenchRecipeHealthCategory CategoryOf(ToolWorkbenchPipelineStepItem step) =>
                    template.RecipeHealthItems.Single(item => ReferenceEquals(item.Step, step)).Category;

                templateFilter.State = "Published";
                var publishedCategory = CategoryOf(templateFilter);
                templateFilter.State = "Preview stale";
                var staleCategory = CategoryOf(templateFilter);
                templateFilter.State = "Ready";
                var readyCategory = CategoryOf(templateFilter);
                firstDependent.State = originalDependentState;
                templateFilter.State = "Published";
                var dependentAfterPublished = CategoryOf(firstDependent);
                templateFilter.State = originalFilterState;
                var dependentWithoutPublishedInput = CategoryOf(firstDependent);
                kernel.Value = "2";
                templateFilter.State = "Ready";
                var parameterCategory = CategoryOf(templateFilter);
                kernel.Value = originalKernel;
                templateFilter.State = originalFilterState;
                firstDependent.State = originalDependentState;

                Check(
                    "recipe health distinguishes Ready, input, selection, parameters, stale Preview, and Published",
                    readyCategory == ToolWorkbenchRecipeHealthCategory.Ready
                    && dependentWithoutPublishedInput == ToolWorkbenchRecipeHealthCategory.NeedsInput
                    && dependentAfterPublished == ToolWorkbenchRecipeHealthCategory.NeedsSelection
                    && parameterCategory == ToolWorkbenchRecipeHealthCategory.NeedsParameters
                    && staleCategory == ToolWorkbenchRecipeHealthCategory.StalePreview
                    && publishedCategory == ToolWorkbenchRecipeHealthCategory.Published,
                    $"ready={readyCategory}; input={dependentWithoutPublishedInput}; selection={dependentAfterPublished}; parameters={parameterCategory}; stale={staleCategory}; published={publishedCategory}");

                var flowDiagnosticsLanguage = OpenVisionLanguageService.CurrentLanguage;
                try
                {
                    var dependentInputId = firstDependent.InputEntityIds.Single();
                    OpenVisionLanguageService.SetLanguage(OpenVisionLanguage.English, save: false);
                    templateFilter.State = "Published";
                    var originalFirstDependentToolName = firstDependent.ToolName;
                    firstDependent.ToolName = originalFirstDependentToolName + " [localization-test]";
                    firstDependent.ToolName = originalFirstDependentToolName;
                    var englishInputPortDetail = firstDependent.InputPortDetail;
                    OpenVisionLanguageService.SetLanguage(OpenVisionLanguage.Korean, save: false);
                    var koreanInputPortDetail = firstDependent.InputPortDetail;
                    Check(
                        "flow diagnostics artifact state detail localizes without changing route identity",
                        englishInputPortDetail == $"{dependentInputId} | Published"
                        && koreanInputPortDetail == $"{dependentInputId} | {template.Localization.RecipeHealthPublished}"
                        && englishInputPortDetail != koreanInputPortDetail
                        && firstDependent.InputPortState == template.Localization.FlowPortReady
                        && firstDependent.InputEntityIds.Single() == dependentInputId,
                        $"en={englishInputPortDetail}; ko={koreanInputPortDetail}; state={firstDependent.InputPortState}; input={firstDependent.InputEntityIds.Single()}");
                }
                finally
                {
                    templateFilter.State = originalFilterState;
                    OpenVisionLanguageService.SetLanguage(flowDiagnosticsLanguage, save: false);
                }

                var beforeNavigationRecipe = string.Join(
                    "|",
                    template.PipelineSteps.Select(step =>
                        $"{step.Id}:{step.State}:{step.InputEntityIdsText}:{string.Join(',', step.Parameters.Select(parameter => $"{parameter.Name}={parameter.Value}"))}"));
                var beforeNavigationSource = $"{template.Source.Id}|{template.Source.Path}|{template.Source.FrameId}|{template.Source.Unit}";
                var beforeNavigationDirty = template.IsDirty;
                var beforeNavigationActionCount = template.RunLog.Count(item =>
                    item.Category is "Preview" or "Publish" or "Run");
                var revealedRequirements = new List<string>();
                var navigationGuard = 0;
                while (template.NextRecipeHealthIssueCommand.CanExecute(null)
                       && navigationGuard++ < template.PipelineSteps.Count)
                {
                    template.NextRecipeHealthIssueCommand.Execute(null);
                    var revealed = template.SelectedRecipeHealthItem;
                    if (revealed is null
                        || !ReferenceEquals(revealed.Step, template.SelectedPipelineStep)
                        || string.IsNullOrWhiteSpace(revealed.Detail))
                    {
                        break;
                    }

                    revealedRequirements.Add(revealed.Step.Id);
                }

                var lastRequirementStep = template.SelectedPipelineStep;
                template.NextRecipeHealthIssueCommand.Execute(null);
                var nextDidNotWrap = ReferenceEquals(lastRequirementStep, template.SelectedPipelineStep);
                while (template.PreviousRecipeHealthIssueCommand.CanExecute(null)
                       && navigationGuard++ < template.PipelineSteps.Count * 2)
                {
                    template.PreviousRecipeHealthIssueCommand.Execute(null);
                }

                var firstRequirementStep = template.SelectedPipelineStep;
                template.PreviousRecipeHealthIssueCommand.Execute(null);
                var previousDidNotWrap = ReferenceEquals(firstRequirementStep, template.SelectedPipelineStep);
                var afterNavigationRecipe = string.Join(
                    "|",
                    template.PipelineSteps.Select(step =>
                        $"{step.Id}:{step.State}:{step.InputEntityIdsText}:{string.Join(',', step.Parameters.Select(parameter => $"{parameter.Name}={parameter.Value}"))}"));
                var afterNavigationSource = $"{template.Source.Id}|{template.Source.Path}|{template.Source.FrameId}|{template.Source.Unit}";
                var afterNavigationActionCount = template.RunLog.Count(item =>
                    item.Category is "Preview" or "Publish" or "Run");
                Check(
                    "requirement navigation is non-wrapping and reveals the owning step without execution or recipe mutation",
                    revealedRequirements.Count > 0
                    && revealedRequirements.Distinct(StringComparer.Ordinal).Count() == revealedRequirements.Count
                    && nextDidNotWrap
                    && previousDidNotWrap
                    && string.Equals(beforeNavigationRecipe, afterNavigationRecipe, StringComparison.Ordinal)
                    && string.Equals(beforeNavigationSource, afterNavigationSource, StringComparison.Ordinal)
                    && beforeNavigationDirty == template.IsDirty
                    && beforeNavigationActionCount == afterNavigationActionCount,
                    $"revealed={string.Join(',', revealedRequirements)}; nextNoWrap={nextDidNotWrap}; previousNoWrap={previousDidNotWrap}; dirty={beforeNavigationDirty}->{template.IsDirty}; actions={beforeNavigationActionCount}->{afterNavigationActionCount}");
            }
        }
        catch (Exception exception)
        {
            lines.Add($"FAIL | unexpected exception | {exception.GetType().Name}: {exception.Message}");
        }
        finally
        {
            try
            {
                if (Directory.Exists(fixtureRoot))
                {
                    Directory.Delete(fixtureRoot, recursive: true);
                }
            }
            catch (IOException exception)
            {
                lines.Add($"FAIL | fixture cleanup | {exception.Message}");
            }
        }

        var reportDirectory = Path.GetDirectoryName(reportPath);
        if (!string.IsNullOrWhiteSpace(reportDirectory))
        {
            Directory.CreateDirectory(reportDirectory);
        }

        var succeeded = passed == total && total > 0 && !lines.Any(line => line.StartsWith("FAIL | unexpected exception", StringComparison.Ordinal));
        lines.Add($"Result: {(succeeded ? "Pass" : "Fail")} ({passed}/{total} checks)");
        File.WriteAllLines(reportPath, lines);
        summary = $"Tool Recipe teaching verification: {(succeeded ? "Pass" : "Fail")} ({passed}/{total} checks)";
        return succeeded;
    }

    private static ToolWorkbenchPipelineStepItem AddTool(ToolWorkbenchViewModel workbench, string name)
    {
        var tool = workbench.Tools.Single(tool => tool.Name == name);
        if (!workbench.AddSelectedToolCommand.CanExecute(tool))
        {
            throw new InvalidOperationException($"The '{name}' teaching tool cannot be added.");
        }

        workbench.AddSelectedToolCommand.Execute(tool);
        return workbench.SelectedPipelineStep
            ?? throw new InvalidOperationException($"The '{name}' teaching tool was not focused after being added.");
    }

    private static SourceQualityReport? WaitForSourceQuality(
        SourceQualityWorkspaceViewModel workspace)
    {
        if (workspace.Report is not null || workspace.HasError)
        {
            return workspace.Report;
        }

        var deadline = DateTimeOffset.UtcNow + TimeSpan.FromSeconds(5);
        var frame = new DispatcherFrame();
        var timer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromMilliseconds(10)
        };
        timer.Tick += (_, _) =>
        {
            if (workspace.Report is not null
                || workspace.HasError
                || DateTimeOffset.UtcNow >= deadline)
            {
                frame.Continue = false;
            }
        };
        timer.Start();
        try
        {
            Dispatcher.PushFrame(frame);
        }
        finally
        {
            timer.Stop();
        }

        return workspace.Report;
    }
}
