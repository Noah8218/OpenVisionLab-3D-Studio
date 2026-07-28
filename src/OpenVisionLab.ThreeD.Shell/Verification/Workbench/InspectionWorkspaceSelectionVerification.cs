using System.IO;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Data;
using OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;

namespace OpenVisionLab.ThreeD.Shell;

internal static class InspectionWorkspaceSelectionVerification
{
    public static bool Verify(string reportPath, out string summary)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reportPath);
        var lines = new List<string>
        {
            "Inspection Workspace selection boundary verification",
            $"Generated: {DateTimeOffset.Now:O}"
        };
        var passed = 0;
        var total = 0;
        var root = Path.Combine(
            Path.GetTempPath(),
            "OpenVisionLab.ThreeD",
            nameof(InspectionWorkspaceSelectionVerification),
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
            var session = new InspectionWorkspaceSelectionSession();
            var changes = new List<InspectionWorkspaceSelectionChangedEventArgs>();
            session.SelectionChanged += (_, args) => changes.Add(args);
            Check(
                "empty selection starts on the reusable main Viewer slot",
                session.Current == InspectionWorkspaceSelectionSnapshot.Empty
                && session.FocusedViewerSlotId == InspectionWorkspaceSelectionSession.MainViewerSlotId,
                session.FocusedViewerSlotId);

            session.SynchronizeTool(
                " step.thickness.01 ",
                " source.c3d.height-map ",
                InspectionWorkspaceRegionRole.Reference,
                " selection.reference ",
                " result.thickness.01 ");
            Check(
                "tool synchronization publishes one atomic selection change",
                changes.Count == 1
                && session.SelectedStepId == "step.thickness.01"
                && session.SelectedInputEntityId == "source.c3d.height-map"
                && session.ActiveRegionRole == InspectionWorkspaceRegionRole.Reference
                && session.SelectedRegionId == "selection.reference"
                && session.SelectedOutputEntityId == "result.thickness.01",
                $"changes={changes.Count}; step={session.SelectedStepId}; role={session.ActiveRegionRole}");

            session.SynchronizeTool(
                "STEP.THICKNESS.01",
                "SOURCE.C3D.HEIGHT-MAP",
                InspectionWorkspaceRegionRole.Reference,
                "SELECTION.REFERENCE",
                "RESULT.THICKNESS.01");
            Check(
                "identity casing does not create a duplicate selection event",
                changes.Count == 1,
                $"changes={changes.Count}");

            session.SelectRegion(InspectionWorkspaceRegionRole.Measurement, "selection.measurement");
            session.FocusViewerSlot("viewer.split.02");
            Check(
                "region and Viewer focus change without losing the selected tool",
                changes.Count == 3
                && session.SelectedStepId == "step.thickness.01"
                && session.ActiveRegionRole == InspectionWorkspaceRegionRole.Measurement
                && session.SelectedRegionId == "selection.measurement"
                && session.FocusedViewerSlotId == "viewer.split.02",
                $"changes={changes.Count}; role={session.ActiveRegionRole}; viewer={session.FocusedViewerSlotId}");

            session.ClearRecipeSelection();
            Check(
                "clearing a recipe selection preserves the focused Viewer slot",
                session.SelectedStepId is null
                && session.SelectedInputEntityId is null
                && session.ActiveRegionRole == InspectionWorkspaceRegionRole.None
                && session.SelectedRegionId is null
                && session.SelectedOutputEntityId is null
                && session.FocusedViewerSlotId == "viewer.split.02",
                $"step={session.SelectedStepId ?? "(none)"}; viewer={session.FocusedViewerSlotId}");

            Directory.CreateDirectory(root);
            var sourcePath = Path.Combine(root, "workspace-selection.C3D");
            C3DHeightFieldSnapshot.CreateForVerification(
                "source.workspace-selection",
                4,
                4,
                [10, 11, 12, 13, 11, 12, 13, 14, 17, 18, 19, 20, 18, 19, 20, 21])
                .SaveC3D(sourcePath);

            var workbench = new ToolWorkbenchViewModel(Path.Combine(root, "recent.json"));
            workbench.SetC3DSource(sourcePath, markDirty: false);
            var thicknessTool = workbench.Tools.Single(tool => tool.Id == "thickness");
            workbench.AddSelectedToolCommand.Execute(thicknessTool);
            var thickness = workbench.SelectedPipelineStep
                ?? throw new InvalidOperationException("Thickness was not selected after Add.");

            Check(
                "root ViewModel synchronizes a newly added Thickness step",
                workbench.WorkspaceSelection.SelectedStepId == thickness.Id
                && workbench.WorkspaceSelection.SelectedInputEntityId == workbench.Source.Id
                && workbench.WorkspaceSelection.ActiveRegionRole == InspectionWorkspaceRegionRole.Reference
                && workbench.WorkspaceSelection.SelectedOutputEntityId == thickness.OutputEntityId,
                $"step={workbench.WorkspaceSelection.SelectedStepId}; input={workbench.WorkspaceSelection.SelectedInputEntityId}; role={workbench.WorkspaceSelection.ActiveRegionRole}");

            Check(
                "selected-tool facade projects three inputs, two ROI roles, and one output",
                workbench.SelectedToolWorkspace.Inputs.Count == 3
                && workbench.SelectedToolWorkspace.Inputs[0].State == "Ready"
                && workbench.SelectedToolWorkspace.Inputs.Skip(1).All(item => item.State == "Missing")
                 && workbench.SelectedToolWorkspace.Regions.Count == 2
                 && workbench.SelectedToolWorkspace.Regions[0].Role == InspectionWorkspaceRegionRole.Reference
                 && workbench.SelectedToolWorkspace.Regions[0].Lifecycle == InspectionWorkspaceRegionLifecycleState.Missing
                 && workbench.SelectedToolWorkspace.Regions[1].Role == InspectionWorkspaceRegionRole.Measurement
                 && workbench.SelectedToolWorkspace.Regions[1].Lifecycle == InspectionWorkspaceRegionLifecycleState.Missing
                 && workbench.SelectedToolWorkspace.Outputs.Single().EntityId == thickness.OutputEntityId,
                $"inputs={workbench.SelectedToolWorkspace.Inputs.Count}; regions={workbench.SelectedToolWorkspace.Regions.Count}; outputs={workbench.SelectedToolWorkspace.Outputs.Count}");

            Check(
                "selected-tool Parameters reuses the existing typed PropertyGrid draft",
                ReferenceEquals(
                    workbench.SelectedToolWorkspace.ParameterDraft,
                    workbench.SelectedStepPropertyDraft)
                && workbench.SelectedToolWorkspace.IsParameterEditorSupported,
                workbench.SelectedStepAdapterStatus);

            var routeBeforeTransientCapture = thickness.InputEntityIdsText;
            var stateBeforeTransientCapture = thickness.State;
            workbench.CapturePlaneFlatnessReferenceRoiCommand.Execute(null);
            Check(
                "Reference ROI enters Drawing without changing authored or execution state",
                workbench.IsTeachingSelectionCaptureActive
                && workbench.SelectedToolWorkspace.ActiveRegion?.Role == InspectionWorkspaceRegionRole.Reference
                && workbench.SelectedToolWorkspace.ActiveRegion.Lifecycle == InspectionWorkspaceRegionLifecycleState.Drawing
                && thickness.InputEntityIdsText == routeBeforeTransientCapture
                && thickness.State == stateBeforeTransientCapture
                && workbench.CurrentMeasurementOutput is null,
                $"lifecycle={workbench.SelectedToolWorkspace.ActiveRegion?.Lifecycle}; progress={workbench.TeachingSelectionCaptureProgress}");

            workbench.UpdateTeachingSelectionCaptureState(true, 1, 2, false, "First corner selected.");
            Check(
                "one corner remains Drawing and Apply stays disabled",
                workbench.SelectedToolWorkspace.ActiveRegion?.Lifecycle == InspectionWorkspaceRegionLifecycleState.Drawing
                && workbench.TeachingSelectionCapturedPointCount == 1
                && !workbench.ApplyTeachingSelectionCaptureCommand.CanExecute(null),
                $"lifecycle={workbench.SelectedToolWorkspace.ActiveRegion?.Lifecycle}; progress={workbench.TeachingSelectionCaptureProgress}");

            workbench.UpdateTeachingSelectionCaptureState(true, 2, 2, true, "Candidate ready.");
            workbench.UpdateTeachingGridRectangleDraft(new ToolRecipeGridRectangle(0, 0, 2, 2));
            Check(
                "two corners enter Review and reject another draw command",
                workbench.SelectedToolWorkspace.ActiveRegion?.Lifecycle == InspectionWorkspaceRegionLifecycleState.Review
                && workbench.ApplyTeachingSelectionCaptureCommand.CanExecute(null)
                && !workbench.CapturePlaneFlatnessReferenceRoiCommand.CanExecute(null),
                $"lifecycle={workbench.SelectedToolWorkspace.ActiveRegion?.Lifecycle}; apply={workbench.ApplyTeachingSelectionCaptureCommand.CanExecute(null)}");

            workbench.CancelTeachingSelectionCaptureCommand.Execute(null);
            Check(
                "Cancel returns a new Reference ROI to Missing without mutation",
                !workbench.IsTeachingSelectionCaptureActive
                && workbench.SelectedToolWorkspace.Regions[0].Lifecycle == InspectionWorkspaceRegionLifecycleState.Missing
                && thickness.InputEntityIdsText == routeBeforeTransientCapture
                && thickness.State == stateBeforeTransientCapture
                && workbench.CurrentMeasurementOutput is null,
                $"lifecycle={workbench.SelectedToolWorkspace.Regions[0].Lifecycle}; route={thickness.InputEntityIdsText}");

            var draftPath = Path.Combine(root, "workspace-selection.ov3d-recipe.json");
            var saved = workbench.TrySaveTeachingRecipe(draftPath, out var saveMessage);
            var stateBeforeSelectionOnlyChanges = thickness.State;
            var routeBeforeSelectionOnlyChanges = thickness.InputEntityIdsText;
            workbench.WorkspaceSelection.SelectInput("presentation-only.input");
            workbench.WorkspaceSelection.FocusViewerSlot("viewer.split.03");
            Check(
                "selection-only changes do not dirty, reroute, or execute the recipe",
                saved
                && !workbench.IsDirty
                && thickness.InputEntityIdsText == routeBeforeSelectionOnlyChanges
                && thickness.State == stateBeforeSelectionOnlyChanges
                && !workbench.HasCurrentMeasurementPreview
                && workbench.CurrentMeasurementOutput is null,
                saved ? $"dirty={workbench.IsDirty}; state={thickness.State}" : saveMessage);

            Check(
                "Height Image is a first-class native-grid auxiliary candidate",
                workbench.GetViewerWorkspaceCandidate(ToolWorkbenchViewModel.HeightImageViewerContentId) is
                {
                    Kind: ViewerWorkspaceCandidateKind.HeightImage,
                    SourcePath: var heightImageSourcePath
                }
                && string.Equals(heightImageSourcePath, sourcePath, StringComparison.OrdinalIgnoreCase),
                string.Join(
                    ',',
                    workbench.ViewerWorkspaceCandidates.Select(candidate => $"{candidate.Id}:{candidate.Kind}")));

            Task.Run(() => workbench.HeightImageViewer.EnsureSourceAsync(
                    workbench.Source.Path,
                    workbench.Source.Id,
                    workbench.Source.Unit,
                    workbench.Source.FrameId))
                .GetAwaiter()
                .GetResult();
            Check(
                "Height Image ViewModel loads every native source cell without WPF ownership",
                workbench.HeightImageViewer.Frame is
                {
                    Width: 4,
                    Height: 4,
                    ValidCount: 16,
                    MissingCount: 0
                } heightImageFrame
                && heightImageFrame.Bgra32Pixels.Length == 4 * 4 * 4
                && heightImageFrame.TryGetCell(3, 2, out var heightImageCell)
                && heightImageCell.Row == 2
                && heightImageCell.Column == 3
                && heightImageCell.RawHeight == 20.0,
                workbench.HeightImageViewer.ImageSummary);

            workbench.HeightImageViewer.UpdateHover(3, 2);
            Check(
                "Height Image hover reports the exact source column row and raw height",
                workbench.HeightImageViewer.HoverSummary.Contains("column 3", StringComparison.Ordinal)
                && workbench.HeightImageViewer.HoverSummary.Contains("row 2", StringComparison.Ordinal)
                && workbench.HeightImageViewer.HoverSummary.Contains("H 20", StringComparison.Ordinal),
                workbench.HeightImageViewer.HoverSummary);

            Check(
                "Height Image hover publishes one coordinate-true shared cursor",
                workbench.SharedHeightCursor.Cursor is
                {
                    Origin: SharedHeightCursorOrigin.HeightImage,
                    Row: 2,
                    Column: 3,
                    RawHeight: 20.0,
                    IsValid: true
                } heightCursor
                && heightCursor.SourceContentSha256
                    == workbench.HeightImageViewer.Frame?.SourceContentSha256
                && workbench.HeightImageViewer.HasLinkedCursor
                && workbench.HeightImageViewer.LinkedCursorRow == 2
                && workbench.HeightImageViewer.LinkedCursorColumn == 3,
                workbench.HeightImageViewer.HoverSummary);

            var sharedCursorRevision = workbench.SharedHeightCursor.Revision;
            workbench.SharedHeightCursor.Update(
                SharedHeightCursorOrigin.ThreeDViewer,
                workbench.HeightImageViewer.Frame!.SourceContentSha256,
                row: 1,
                column: 2,
                rawHeight: 13.0,
                isValid: true);
            Check(
                "3D Viewer hover is projected into the Height Image at the same native cell",
                workbench.SharedHeightCursor.Revision == sharedCursorRevision + 1
                && workbench.HeightImageViewer.HasLinkedCursor
                && workbench.HeightImageViewer.LinkedCursorRow == 1
                && workbench.HeightImageViewer.LinkedCursorColumn == 2
                && workbench.HeightImageViewer.LinkedCursorIsValid
                && workbench.HeightImageViewer.HoverSummary.Contains("3D", StringComparison.Ordinal)
                && workbench.HeightImageViewer.HoverSummary.Contains("H 13", StringComparison.Ordinal),
                workbench.HeightImageViewer.HoverSummary);

            workbench.HeightImageViewer.ClearHover();
            Check(
                "a stale Height Image leave cannot clear a newer 3D Viewer cursor",
                workbench.SharedHeightCursor.Cursor is
                {
                    Origin: SharedHeightCursorOrigin.ThreeDViewer,
                    Row: 1,
                    Column: 2
                }
                && workbench.HeightImageViewer.HasLinkedCursor,
                $"cursor={workbench.SharedHeightCursor.Cursor}; linked={workbench.HeightImageViewer.HasLinkedCursor}");

            workbench.SharedHeightCursor.Update(
                SharedHeightCursorOrigin.ThreeDViewer,
                workbench.HeightImageViewer.Frame.SourceContentSha256,
                row: 0,
                column: 0,
                rawHeight: double.NaN,
                isValid: false);
            Check(
                "shared cursor preserves a native missing-cell state without fabricating height",
                workbench.HeightImageViewer.HasLinkedCursor
                && !workbench.HeightImageViewer.LinkedCursorIsValid
                && workbench.SharedHeightCursor.Cursor is
                {
                    RawHeight: var missingHeight,
                    IsValid: false
                }
                && double.IsNaN(missingHeight)
                && workbench.HeightImageViewer.HoverSummary.Contains(
                    workbench.Localization.HeightImageMissingValue,
                    StringComparison.Ordinal),
                workbench.HeightImageViewer.HoverSummary);

            workbench.SharedHeightCursor.Clear(SharedHeightCursorOrigin.ThreeDViewer);
            Check(
                "origin-aware clear removes the owning cursor from both views",
                !workbench.SharedHeightCursor.HasCursor
                && !workbench.HeightImageViewer.HasLinkedCursor,
                $"cursor={workbench.SharedHeightCursor.Cursor}; linked={workbench.HeightImageViewer.HasLinkedCursor}");

            Check(
                "shared coordinate hover does not dirty reroute or execute the recipe",
                !workbench.IsDirty
                && thickness.InputEntityIdsText == routeBeforeSelectionOnlyChanges
                && thickness.State == stateBeforeSelectionOnlyChanges
                && workbench.CurrentMeasurementOutput is null,
                $"dirty={workbench.IsDirty}; state={thickness.State}");

            var heightImageDisplayRequests = 0;
            workbench.HeightImageViewer.DisplayRequest += (_, _) => heightImageDisplayRequests++;
            workbench.HeightImageViewer.FitCommand.Execute(null);
            workbench.HeightImageViewer.ActualPixelsCommand.Execute(null);
            Check(
                "Height Image presentation commands do not dirty reroute or execute the recipe",
                heightImageDisplayRequests == 2
                && !workbench.IsDirty
                && thickness.InputEntityIdsText == routeBeforeSelectionOnlyChanges
                && thickness.State == stateBeforeSelectionOnlyChanges
                && workbench.CurrentMeasurementOutput is null,
                $"requests={heightImageDisplayRequests};dirty={workbench.IsDirty};state={thickness.State}");

            var autoHeightImageSha = workbench.HeightImageViewer.DisplayPixelSha256;
            Check(
                "Height Image shows the coordinate-true invalid-cell overlay by default",
                workbench.HeightImageViewer.ShowInvalidCells
                && workbench.HeightImageViewer.DisplayFrame?.InvalidOverlayMode
                    == C3DHeightImageInvalidOverlayMode.Visible
                && workbench.HeightImageViewer.InvalidOverlayPixelCount
                    == workbench.HeightImageViewer.Frame?.MissingCount
                && workbench.HeightImageViewer.DisplayFrame?.InvalidCellMapSha256
                    == workbench.HeightImageViewer.Frame?.InvalidCellMap.Sha256,
                $"visible={workbench.HeightImageViewer.ShowInvalidCells};pixels={workbench.HeightImageViewer.InvalidOverlayPixelCount};sha={workbench.HeightImageViewer.DisplayFrame?.InvalidCellMapSha256}");
            workbench.HeightImageViewer.ShowInvalidCells = false;
            Check(
                "Height Image can hide the invalid overlay without changing the source",
                workbench.HeightImageViewer.DisplayFrame?.InvalidOverlayMode
                    == C3DHeightImageInvalidOverlayMode.Hidden
                && workbench.HeightImageViewer.InvalidOverlayPixelCount == 0
                && workbench.HeightImageViewer.DisplayPixelSha256
                    == workbench.HeightImageViewer.Frame?.PixelSha256,
                $"visible={workbench.HeightImageViewer.ShowInvalidCells};pixels={workbench.HeightImageViewer.InvalidOverlayPixelCount};display={workbench.HeightImageViewer.DisplayPixelSha256}");
            workbench.HeightImageViewer.ShowInvalidCells = true;
            Check(
                "Height Image invalid overlay replay is deterministic and view only",
                workbench.HeightImageViewer.DisplayPixelSha256 == autoHeightImageSha
                && workbench.HeightImageViewer.InvalidOverlayPixelCount
                    == workbench.HeightImageViewer.Frame?.MissingCount
                && !workbench.IsDirty
                && thickness.InputEntityIdsText == routeBeforeSelectionOnlyChanges
                && thickness.State == stateBeforeSelectionOnlyChanges
                && workbench.CurrentMeasurementOutput is null,
                $"display={workbench.HeightImageViewer.DisplayPixelSha256};dirty={workbench.IsDirty};state={thickness.State}");
            workbench.HeightImageViewer.RangeMinimumText = "40";
            workbench.HeightImageViewer.RangeMaximumText = "20";
            Check(
                "Height Image rejects an inverted manual display range without rerendering",
                workbench.HeightImageViewer.HasRangeError
                && !workbench.HeightImageViewer.ApplyManualRangeCommand.CanExecute(null)
                && workbench.HeightImageViewer.IsAutoRange
                && workbench.HeightImageViewer.DisplayPixelSha256 == autoHeightImageSha,
                $"error={workbench.HeightImageViewer.RangeError};auto={workbench.HeightImageViewer.IsAutoRange};sha={workbench.HeightImageViewer.DisplayPixelSha256}");

            var manualRangeApplied = workbench.HeightImageViewer.TryApplyManualRange(12.0, 32.0);
            Check(
                "Height Image applies a valid manual range as independent view state",
                manualRangeApplied
                && !workbench.HeightImageViewer.IsAutoRange
                && workbench.HeightImageViewer.DisplayFrame is
                {
                    Minimum: 12.0,
                    Maximum: 32.0,
                    Palette: C3DHeightImagePalette.Height
                }
                && workbench.HeightImageViewer.DisplayPixelSha256 != autoHeightImageSha,
                workbench.HeightImageViewer.DisplayRangeSummary);

            var manualHeightSha = workbench.HeightImageViewer.DisplayPixelSha256;
            workbench.HeightImageViewer.SelectedPalette = C3DHeightImagePalette.Thermal;
            Check(
                "Height Image palette selection rerenders only presentation pixels",
                workbench.HeightImageViewer.DisplayFrame is
                {
                    Minimum: 12.0,
                    Maximum: 32.0,
                    Palette: C3DHeightImagePalette.Thermal
                }
                && workbench.HeightImageViewer.DisplayPixelSha256 != manualHeightSha
                && workbench.HeightImageViewer.Frame?.TryGetCell(3, 2, out var paletteInvariantCell) == true
                && paletteInvariantCell.RawHeight == 20.0,
                $"palette={workbench.HeightImageViewer.SelectedPalette};sha={workbench.HeightImageViewer.DisplayPixelSha256}");

            workbench.HeightImageViewer.AutoRangeCommand.Execute(null);
            Check(
                "Height Image Auto range restores full-source limits and retains the palette",
                workbench.HeightImageViewer.IsAutoRange
                && workbench.HeightImageViewer.SelectedPalette == C3DHeightImagePalette.Thermal
                && workbench.HeightImageViewer.DisplayFrame is
                {
                    Palette: C3DHeightImagePalette.Thermal
                } restoredAutoDisplay
                && restoredAutoDisplay.Minimum
                    == workbench.HeightImageViewer.Frame?.Minimum
                && restoredAutoDisplay.Maximum
                    == workbench.HeightImageViewer.Frame?.Maximum,
                workbench.HeightImageViewer.DisplayRangeSummary);

            workbench.HeightImageViewer.SelectedPalette = C3DHeightImagePalette.Height;
            Check(
                "Height Image default palette and Auto range restore the visible-overlay pixel identity",
                workbench.HeightImageViewer.IsAutoRange
                && workbench.HeightImageViewer.DisplayPixelSha256 == autoHeightImageSha
                && workbench.HeightImageViewer.DisplayFrame?.InvalidOverlayMode
                    == C3DHeightImageInvalidOverlayMode.Visible
                && workbench.HeightImageViewer.InvalidOverlayPixelCount
                    == workbench.HeightImageViewer.Frame?.MissingCount,
                $"display={workbench.HeightImageViewer.DisplayPixelSha256};native={workbench.HeightImageViewer.Frame?.PixelSha256}");

            Check(
                "Height Image range palette and invalid-overlay changes do not dirty reroute or execute the recipe",
                heightImageDisplayRequests == 2
                && !workbench.IsDirty
                && thickness.InputEntityIdsText == routeBeforeSelectionOnlyChanges
                && thickness.State == stateBeforeSelectionOnlyChanges
                && workbench.CurrentMeasurementOutput is null,
                $"requests={heightImageDisplayRequests};dirty={workbench.IsDirty};state={thickness.State}");

            Check(
                "auxiliary Viewer layout commands require a real current C3D candidate",
                workbench.SplitViewerVerticallyCommand.CanExecute(null)
                && workbench.SplitViewerHorizontallyCommand.CanExecute(null)
                && workbench.PopOutViewerCommand.CanExecute(null),
                $"candidates={workbench.ViewerWorkspaceCandidates.Count}");
            workbench.SplitViewerVerticallyCommand.Execute(null);
            Check(
                "side-by-side layout defaults to the coordinate-true Height Image without recipe mutation",
                workbench.ViewerWorkspace.Layout == ViewerWorkspaceLayout.SplitVertical
                && workbench.ViewerWorkspace.AuxiliaryContentId == ToolWorkbenchViewModel.HeightImageViewerContentId
                && !workbench.IsDirty
                && thickness.InputEntityIdsText == routeBeforeSelectionOnlyChanges
                && thickness.State == stateBeforeSelectionOnlyChanges
                && workbench.CurrentMeasurementOutput is null,
                $"layout={workbench.ViewerWorkspace.Layout}; pin={workbench.ViewerWorkspace.AuxiliaryContentId}; dirty={workbench.IsDirty}");
            workbench.FocusViewerWorkspaceSlotCommand.Execute(ViewerWorkspaceSession.AuxiliarySlotId);
            Check(
                "auxiliary Viewer focus synchronizes with the one workspace selection",
                workbench.ViewerWorkspace.IsAuxiliaryFocused
                && workbench.WorkspaceSelection.FocusedViewerSlotId == ViewerWorkspaceSession.AuxiliarySlotId
                && workbench.WorkspaceSelection.SelectedStepId == thickness.Id,
                $"session={workbench.ViewerWorkspace.FocusedSlotId}; selection={workbench.WorkspaceSelection.FocusedViewerSlotId}");
            workbench.SplitViewerHorizontallyCommand.Execute(null);
            workbench.PopOutViewerCommand.Execute(null);
            Check(
                "stacked and pop-out commands reuse the same auxiliary pin",
                workbench.ViewerWorkspace.Layout == ViewerWorkspaceLayout.PopOut
                && workbench.ViewerWorkspace.AuxiliaryContentId == ToolWorkbenchViewModel.HeightImageViewerContentId
                && !workbench.IsDirty
                && workbench.CurrentMeasurementOutput is null,
                $"layout={workbench.ViewerWorkspace.Layout}; pin={workbench.ViewerWorkspace.AuxiliaryContentId}");
            workbench.SetSingleViewerLayoutCommand.Execute(null);
            Check(
                "single layout restores main focus and retains the reusable auxiliary pin",
                workbench.ViewerWorkspace.Layout == ViewerWorkspaceLayout.Single
                && workbench.ViewerWorkspace.IsMainFocused
                && workbench.WorkspaceSelection.FocusedViewerSlotId == ViewerWorkspaceSession.MainSlotId
                && workbench.ViewerWorkspace.AuxiliaryContentId == ToolWorkbenchViewModel.HeightImageViewerContentId
                && !workbench.IsDirty,
                $"layout={workbench.ViewerWorkspace.Layout}; focus={workbench.ViewerWorkspace.FocusedSlotId}; pin={workbench.ViewerWorkspace.AuxiliaryContentId}");

            var binding = ToolRecipeSelectionSourceBindingVerifier.ReadIdentity(sourcePath);
            var reference = new ToolRecipeSelection(
                "selection.reference",
                "Reference ROI",
                ToolRecipeSelectionKinds.GridRectangle,
                workbench.Source.Id,
                workbench.Source.FrameId,
                binding,
                new ToolRecipeGridRectangle(0, 0, 2, 4),
                null,
                null);
            workbench.Selections.Add(reference);
            workbench.SelectedCompatibleSelection = reference;
            workbench.ReusePlaneFlatnessReferenceRoiCommand.Execute(null);
            Check(
                "Reference ROI routing advances the synchronized active role to Measurement",
                thickness.InputEntityIds.Count == 2
                && thickness.InputEntityIds[1] == reference.Id
                 && workbench.WorkspaceSelection.ActiveRegionRole == InspectionWorkspaceRegionRole.Measurement
                 && workbench.WorkspaceSelection.SelectedRegionId is null
                 && workbench.SelectedToolWorkspace.Regions[0].Lifecycle == InspectionWorkspaceRegionLifecycleState.Applied
                 && workbench.SelectedToolWorkspace.Regions[1].Lifecycle == InspectionWorkspaceRegionLifecycleState.Missing
                 && workbench.SelectedToolWorkspace.Regions[1].IsActive,
                $"role={workbench.WorkspaceSelection.ActiveRegionRole}; region={workbench.WorkspaceSelection.SelectedRegionId ?? "(none)"}");

            workbench.WorkspaceSelection.SelectRegion(
                InspectionWorkspaceRegionRole.Reference,
                reference.Id);
            Check(
                "workspace ROI role selection updates the existing teaching owner",
                workbench.IsPlaneFlatnessReferenceRoleActive
                && !workbench.IsPlaneFlatnessMeasurementRoleActive
                && workbench.SelectedStepTeachingSelection?.Id == reference.Id
                && workbench.SelectedToolWorkspace.Regions[0].IsActive
                && !workbench.HasCurrentMeasurementPreview,
                $"reference={workbench.IsPlaneFlatnessReferenceRoleActive}; region={workbench.SelectedStepTeachingSelection?.Id}");

            var selectedReference = workbench.SelectPipelineStepForSelection(reference.Id);
            Check(
                "Viewer ROI selection synchronizes the same step and Reference role",
                selectedReference
                && workbench.WorkspaceSelection.SelectedStepId == thickness.Id
                && workbench.WorkspaceSelection.ActiveRegionRole == InspectionWorkspaceRegionRole.Reference
                && workbench.WorkspaceSelection.SelectedRegionId == reference.Id
                && workbench.SelectedToolWorkspace.Regions[0].IsActive,
                $"selected={selectedReference}; role={workbench.WorkspaceSelection.ActiveRegionRole}; region={workbench.WorkspaceSelection.SelectedRegionId}");

            var fitRequests = 0;
            workbench.FitWorkspaceRegionRequested += (_, _) => fitRequests++;
            var routeBeforeFit = thickness.InputEntityIdsText;
            var appliedReferenceRow = workbench.SelectedToolWorkspace.Regions[0];
            Check(
                "Applied Reference exposes presentation-only Fit ROI",
                workbench.FitWorkspaceRegionCommand.CanExecute(appliedReferenceRow)
                && !workbench.FitWorkspaceRegionCommand.CanExecute(workbench.SelectedToolWorkspace.Regions[1]),
                $"reference={appliedReferenceRow.Lifecycle}; measurement={workbench.SelectedToolWorkspace.Regions[1].Lifecycle}");
            workbench.FitWorkspaceRegionCommand.Execute(appliedReferenceRow);
            Check(
                "Fit ROI raises one Viewer request without recipe or execution mutation",
                fitRequests == 1
                && thickness.InputEntityIdsText == routeBeforeFit
                && workbench.CurrentMeasurementOutput is null
                && workbench.WorkspaceSelection.ActiveRegionRole == InspectionWorkspaceRegionRole.Reference,
                $"requests={fitRequests}; route={thickness.InputEntityIdsText}");

            workbench.CapturePlaneFlatnessReferenceRoiCommand.Execute(null);
            workbench.UpdateTeachingSelectionCaptureState(true, 2, 2, true, "Replacement ready.");
            Check(
                "an authored Reference replacement enters Review with the same selection identity",
                workbench.SelectedToolWorkspace.Regions[0].Lifecycle == InspectionWorkspaceRegionLifecycleState.Review
                && workbench.SelectedToolWorkspace.Regions[0].SelectionId == reference.Id
                && workbench.FitWorkspaceRegionCommand.CanExecute(workbench.SelectedToolWorkspace.Regions[0]),
                $"lifecycle={workbench.SelectedToolWorkspace.Regions[0].Lifecycle}; selection={workbench.SelectedToolWorkspace.Regions[0].SelectionId}");
            workbench.CancelTeachingSelectionCaptureCommand.Execute(null);
            Check(
                "Cancel returns a replacement candidate to its prior Applied state",
                workbench.SelectedToolWorkspace.Regions[0].Lifecycle == InspectionWorkspaceRegionLifecycleState.Applied
                && thickness.InputEntityIdsText == routeBeforeFit
                && workbench.Selections.Single(item => item.Id == reference.Id).GridRectangle == reference.GridRectangle,
                $"lifecycle={workbench.SelectedToolWorkspace.Regions[0].Lifecycle}; selection={reference.Id}");

            workbench.CapturePlaneFlatnessReferenceRoiCommand.Execute(null);
            workbench.UpdateTeachingSelectionCaptureState(true, 2, 2, true, "Replacement ready.");
            var editedReference = reference with
            {
                GridRectangle = new ToolRecipeGridRectangle(0, 1, 2, 3)
            };
            var appliedReplacement = workbench.TryApplyCapturedTeachingSelection(
                editedReference,
                out var replacementMessage);
            Check(
                "Apply preserves Reference identity, returns it to Applied, and advances the active role",
                appliedReplacement
                && workbench.Selections.Single(item => item.Id == reference.Id).GridRectangle == editedReference.GridRectangle
                && workbench.SelectedToolWorkspace.Regions[0].Lifecycle == InspectionWorkspaceRegionLifecycleState.Applied
                && workbench.SelectedToolWorkspace.Regions[1].Lifecycle == InspectionWorkspaceRegionLifecycleState.Missing
                && workbench.SelectedToolWorkspace.ActiveRegion?.Role == InspectionWorkspaceRegionRole.Measurement
                && workbench.CurrentMeasurementOutput is null,
                appliedReplacement
                    ? $"reference={workbench.SelectedToolWorkspace.Regions[0].Lifecycle}; active={workbench.SelectedToolWorkspace.ActiveRegion?.Role}"
                    : replacementMessage);

            var roiWorkbench = new ToolWorkbenchViewModel(Path.Combine(root, "roi-recent.json"));
            roiWorkbench.SetC3DSource(sourcePath, markDirty: false);
            roiWorkbench.AddSelectedToolCommand.Execute(
                roiWorkbench.Tools.Single(tool => tool.Id == "thickness"));
            var roiStep = roiWorkbench.SelectedPipelineStep
                ?? throw new InvalidOperationException("ROI verification Thickness step was not selected.");
            var roiBinding = ToolRecipeSelectionSourceBindingVerifier.ReadIdentity(sourcePath);
            ToolWorkbenchTeachingCaptureRequestEventArgs? freshHeightImageRequest = null;
            var freshHeightImageApplySucceeded = false;
            EventHandler<ToolWorkbenchTeachingCaptureRequestEventArgs> freshHeightImageBeginHandler = (_, args) =>
                freshHeightImageRequest = args;
            EventHandler freshHeightImageApplyHandler = (_, _) =>
            {
                if (freshHeightImageRequest is not { } request
                    || roiWorkbench.HeightImageViewer.RoiWorkspace.Candidate is not { } candidate)
                {
                    return;
                }

                freshHeightImageApplySucceeded = roiWorkbench.TryApplyCapturedTeachingSelection(
                    new ToolRecipeSelection(
                        request.SelectionId,
                        request.SelectionName,
                        request.Kind,
                        request.RootSourceId,
                        request.FrameId,
                        request.SourceBinding,
                        candidate,
                        null,
                        null),
                    out _);
            };
            roiWorkbench.BeginTeachingSelectionCaptureRequested += freshHeightImageBeginHandler;
            roiWorkbench.ApplyTeachingSelectionCaptureRequested += freshHeightImageApplyHandler;
            roiWorkbench.CapturePlaneFlatnessReferenceRoiCommand.Execute(null);
            var freshHeightImageDrawBegan =
                roiWorkbench.HeightImageViewer.RoiWorkspace.TryBeginPointer(
                    row: 0,
                    column: 0,
                    rowTolerance: 0,
                    columnTolerance: 0);
            roiWorkbench.HeightImageViewer.RoiWorkspace.TryUpdatePointer(row: 1, column: 1);
            roiWorkbench.HeightImageViewer.RoiWorkspace.EndPointer();
            roiWorkbench.HeightImageViewer.RoiWorkspace.ApplyCommand.Execute(null);
            Check(
                "fresh Height Image Reference Apply immediately enables Measurement Draw",
                freshHeightImageDrawBegan
                && freshHeightImageApplySucceeded
                && !roiWorkbench.IsTeachingSelectionCaptureActive
                && roiWorkbench.IsPlaneFlatnessMeasurementRoleActive
                && roiWorkbench.CapturePlaneFlatnessMeasurementRoiCommand.CanExecute(null)
                && roiWorkbench.CurrentMeasurementOutput is null,
                $"applied={freshHeightImageApplySucceeded}; active={roiWorkbench.SelectedToolWorkspace.ActiveRegion?.Role}; measurementCanExecute={roiWorkbench.CapturePlaneFlatnessMeasurementRoiCommand.CanExecute(null)}");
            roiWorkbench.RemovePlaneFlatnessReferenceRoiCommand.Execute(null);
            roiWorkbench.BeginTeachingSelectionCaptureRequested -= freshHeightImageBeginHandler;
            roiWorkbench.ApplyTeachingSelectionCaptureRequested -= freshHeightImageApplyHandler;

            var roiReference = new ToolRecipeSelection(
                "selection.height-image.reference",
                "Height Image Reference ROI",
                ToolRecipeSelectionKinds.GridRectangle,
                roiWorkbench.Source.Id,
                roiWorkbench.Source.FrameId,
                roiBinding,
                new ToolRecipeGridRectangle(0, 0, 2, 2),
                null,
                null);
            var roiMeasurement = new ToolRecipeSelection(
                "selection.height-image.measurement",
                "Height Image Measurement ROI",
                ToolRecipeSelectionKinds.GridRectangle,
                roiWorkbench.Source.Id,
                roiWorkbench.Source.FrameId,
                roiBinding,
                new ToolRecipeGridRectangle(2, 2, 2, 2),
                null,
                null);
            roiWorkbench.Selections.Add(roiReference);
            roiWorkbench.SelectedCompatibleSelection = roiReference;
            roiWorkbench.ReusePlaneFlatnessReferenceRoiCommand.Execute(null);
            roiWorkbench.Selections.Add(roiMeasurement);
            roiWorkbench.SelectedCompatibleSelection = roiMeasurement;
            roiWorkbench.ReusePlaneFlatnessMeasurementRoiCommand.Execute(null);

            Check(
                "Height Image projects both applied ROI identities and native-grid geometry",
                roiWorkbench.HeightImageViewer.RoiWorkspace.Overlays.Count == 2
                && roiWorkbench.HeightImageViewer.RoiWorkspace.Overlays.Any(item =>
                    item.SelectionId == roiReference.Id
                    && item.Role == InspectionWorkspaceRegionRole.Reference
                    && item.Rectangle == roiReference.GridRectangle)
                && roiWorkbench.HeightImageViewer.RoiWorkspace.Overlays.Any(item =>
                    item.SelectionId == roiMeasurement.Id
                    && item.Role == InspectionWorkspaceRegionRole.Measurement
                    && item.Rectangle == roiMeasurement.GridRectangle),
                string.Join(
                    ';',
                    roiWorkbench.HeightImageViewer.RoiWorkspace.Overlays.Select(item =>
                        $"{item.Role}:{item.SelectionId}:{item.Rectangle}")));

            roiWorkbench.HeightImageViewer.RoiWorkspace.TryBeginPointer(
                row: 2,
                column: 2,
                rowTolerance: 0,
                columnTolerance: 0);
            Check(
                "clicking an applied Height Image ROI selects the same recipe role and identity",
                roiWorkbench.WorkspaceSelection.ActiveRegionRole
                    == InspectionWorkspaceRegionRole.Measurement
                && roiWorkbench.SelectedStepTeachingSelection?.Id == roiMeasurement.Id,
                $"role={roiWorkbench.WorkspaceSelection.ActiveRegionRole}; selection={roiWorkbench.SelectedStepTeachingSelection?.Id}");

            var geometryBeforeHeightImageEdit =
                roiWorkbench.Selections.Single(item => item.Id == roiMeasurement.Id).GridRectangle;
            var routeBeforeHeightImageEdit = roiStep.InputEntityIdsText;
            var dirtyBeforeHeightImageEdit = roiWorkbench.IsDirty;
            var heightImageDraftEvents = new List<ToolRecipeGridRectangle>();
            roiWorkbench.TeachingGridRectangleDraftChanged += (_, args) =>
                heightImageDraftEvents.Add(args.Rectangle);
            roiWorkbench.CapturePlaneFlatnessMeasurementRoiCommand.Execute(null);
            var beganHeightImageResize =
                roiWorkbench.HeightImageViewer.RoiWorkspace.TryBeginPointer(
                    row: 2,
                    column: 2,
                    rowTolerance: 0,
                    columnTolerance: 0);
            roiWorkbench.HeightImageViewer.RoiWorkspace.TryUpdatePointer(row: 1, column: 1);
            roiWorkbench.HeightImageViewer.RoiWorkspace.EndPointer();
            var reviewCandidate =
                roiWorkbench.HeightImageViewer.RoiWorkspace.Candidate;
            Check(
                "Height Image corner drag enters Review and synchronizes one 3D Viewer draft",
                beganHeightImageResize
                && reviewCandidate == new ToolRecipeGridRectangle(1, 1, 3, 3)
                && roiWorkbench.HeightImageViewer.RoiWorkspace.Lifecycle
                    == InspectionWorkspaceRegionLifecycleState.Review
                && heightImageDraftEvents.LastOrDefault() == reviewCandidate
                && roiWorkbench.ApplyTeachingSelectionCaptureCommand.CanExecute(null),
                $"candidate={reviewCandidate}; lifecycle={roiWorkbench.HeightImageViewer.RoiWorkspace.Lifecycle}; events={heightImageDraftEvents.Count}");

            Check(
                "Height Image Review remains transient before explicit Apply",
                roiWorkbench.Selections.Single(item => item.Id == roiMeasurement.Id).GridRectangle
                    == geometryBeforeHeightImageEdit
                && roiStep.InputEntityIdsText == routeBeforeHeightImageEdit
                && roiWorkbench.IsDirty == dirtyBeforeHeightImageEdit
                && roiWorkbench.CurrentMeasurementOutput is null,
                $"dirty={roiWorkbench.IsDirty}; route={roiStep.InputEntityIdsText}; geometry={roiWorkbench.Selections.Single(item => item.Id == roiMeasurement.Id).GridRectangle}");

            roiWorkbench.HeightImageViewer.RoiWorkspace.CancelCommand.Execute(null);
            Check(
                "Height Image Cancel restores Applied geometry without recipe mutation",
                !roiWorkbench.IsTeachingSelectionCaptureActive
                && roiWorkbench.HeightImageViewer.RoiWorkspace.Lifecycle
                    == InspectionWorkspaceRegionLifecycleState.Applied
                && roiWorkbench.Selections.Single(item => item.Id == roiMeasurement.Id).GridRectangle
                    == geometryBeforeHeightImageEdit
                && roiWorkbench.IsDirty == dirtyBeforeHeightImageEdit
                && roiWorkbench.CurrentMeasurementOutput is null,
                $"lifecycle={roiWorkbench.HeightImageViewer.RoiWorkspace.Lifecycle}; geometry={roiWorkbench.Selections.Single(item => item.Id == roiMeasurement.Id).GridRectangle}");

            ToolWorkbenchTeachingCaptureRequestEventArgs? heightImageApplyRequest = null;
            var heightImageApplySucceeded = false;
            roiWorkbench.BeginTeachingSelectionCaptureRequested += (_, args) =>
                heightImageApplyRequest = args;
            roiWorkbench.ApplyTeachingSelectionCaptureRequested += (_, _) =>
            {
                if (heightImageApplyRequest?.ExistingSelection is not { } existing
                    || roiWorkbench.HeightImageViewer.RoiWorkspace.Candidate is not { } appliedCandidate)
                {
                    return;
                }

                heightImageApplySucceeded = roiWorkbench.TryApplyCapturedTeachingSelection(
                    existing with { GridRectangle = appliedCandidate },
                    out _);
            };
            roiWorkbench.CapturePlaneFlatnessMeasurementRoiCommand.Execute(null);
            roiWorkbench.HeightImageViewer.RoiWorkspace.TryBeginPointer(
                row: 2,
                column: 2,
                rowTolerance: 0,
                columnTolerance: 0);
            roiWorkbench.HeightImageViewer.RoiWorkspace.TryUpdatePointer(row: 1, column: 1);
            roiWorkbench.HeightImageViewer.RoiWorkspace.EndPointer();
            roiWorkbench.HeightImageViewer.RoiWorkspace.ApplyCommand.Execute(null);
            var appliedHeightImageGeometry =
                roiWorkbench.Selections.Single(item => item.Id == roiMeasurement.Id).GridRectangle;
            Check(
                "Height Image Apply preserves selection identity and changes only authored geometry",
                heightImageApplySucceeded
                && appliedHeightImageGeometry == new ToolRecipeGridRectangle(1, 1, 3, 3)
                && roiWorkbench.Selections.Count(item => item.Id == roiMeasurement.Id) == 1
                && !roiWorkbench.IsTeachingSelectionCaptureActive
                && roiWorkbench.CurrentMeasurementOutput is null,
                $"applied={heightImageApplySucceeded}; id={roiMeasurement.Id}; geometry={appliedHeightImageGeometry}");

            var roiRecipePath = Path.Combine(root, "height-image-roi.ov3d-recipe.json");
            var roiSaved = roiWorkbench.TrySaveTeachingRecipe(
                roiRecipePath,
                out var roiSaveMessage);
            var reopenedRoiWorkbench = new ToolWorkbenchViewModel(
                Path.Combine(root, "roi-reopen-recent.json"));
            var roiReopened = reopenedRoiWorkbench.TryOpenTeachingRecipe(
                roiRecipePath,
                out var roiReopenMessage);
            reopenedRoiWorkbench.SelectPipelineStep(roiStep.Id);
            Check(
                "save and reopen preserve both synchronized Height Image ROI identities and geometry",
                roiSaved
                && roiReopened
                && reopenedRoiWorkbench.HeightImageViewer.RoiWorkspace.Overlays.Count == 2
                && reopenedRoiWorkbench.HeightImageViewer.RoiWorkspace.Overlays.Any(item =>
                    item.SelectionId == roiReference.Id
                    && item.Rectangle == roiReference.GridRectangle)
                && reopenedRoiWorkbench.HeightImageViewer.RoiWorkspace.Overlays.Any(item =>
                    item.SelectionId == roiMeasurement.Id
                    && item.Rectangle == appliedHeightImageGeometry),
                roiSaved && roiReopened
                    ? string.Join(
                        ';',
                        reopenedRoiWorkbench.HeightImageViewer.RoiWorkspace.Overlays.Select(item =>
                            $"{item.SelectionId}:{item.Rectangle}"))
                    : $"{roiSaveMessage} | {roiReopenMessage}");

            reopenedRoiWorkbench.HeightImageViewer.RoiWorkspace.TryBeginPointer(
                row: 3,
                column: 3,
                rowTolerance: 0,
                columnTolerance: 0);
            reopenedRoiWorkbench.HeightImageViewer.RoiWorkspace.DeleteCommand.Execute(null);
            Check(
                "Height Image Delete removes only the selected applied ROI without inspection",
                reopenedRoiWorkbench.Selections.All(item => item.Id != roiMeasurement.Id)
                && reopenedRoiWorkbench.Selections.Any(item => item.Id == roiReference.Id)
                && reopenedRoiWorkbench.CurrentMeasurementOutput is null,
                $"remaining={string.Join(';', reopenedRoiWorkbench.Selections.Select(item => item.Id))}; output={reopenedRoiWorkbench.CurrentMeasurementOutput?.OutputEntityId ?? "(none)"}");

            var boxWorkbench = new ToolWorkbenchViewModel(
                Path.Combine(root, "box-recent.json"));
            boxWorkbench.SetC3DSource(sourcePath, markDirty: false);
            Check(
                "OrientedBox3D numeric editor activates only from an identified source",
                boxWorkbench.OrientedBoxEditor.HasSourceContext
                && boxWorkbench.OrientedBoxEditor.NewCommand.CanExecute(null)
                && !boxWorkbench.IsDirty,
                $"source={boxWorkbench.OrientedBoxEditor.SourceFrameSummary};dirty={boxWorkbench.IsDirty}");

            boxWorkbench.OrientedBoxEditor.NewCommand.Execute(null);
            Check(
                "new OrientedBox3D opens a valid identity-axis draft without recipe mutation",
                boxWorkbench.OrientedBoxEditor.IsDraftOpen
                && boxWorkbench.OrientedBoxEditor.IsDraftValid
                && boxWorkbench.OrientedBoxEditor.ApplyCommand.CanExecute(null)
                && boxWorkbench.Selections.Count == 0
                && !boxWorkbench.IsDirty
                && boxWorkbench.CurrentMeasurementOutput is null,
                $"valid={boxWorkbench.OrientedBoxEditor.IsDraftValid};selections={boxWorkbench.Selections.Count};dirty={boxWorkbench.IsDirty}");

            boxWorkbench.OrientedBoxEditor.AxisYX = 1;
            boxWorkbench.OrientedBoxEditor.AxisYY = 0;
            Check(
                "numeric editor rejects a non-orthogonal axis before Apply",
                !boxWorkbench.OrientedBoxEditor.IsDraftValid
                && !boxWorkbench.OrientedBoxEditor.ApplyCommand.CanExecute(null)
                && boxWorkbench.OrientedBoxEditor.ValidationSummary.Contains(
                    "orthogonal",
                    StringComparison.OrdinalIgnoreCase)
                && boxWorkbench.Selections.Count == 0
                && !boxWorkbench.IsDirty,
                boxWorkbench.OrientedBoxEditor.ValidationSummary);

            boxWorkbench.OrientedBoxEditor.AxisYX = 0;
            boxWorkbench.OrientedBoxEditor.AxisYY = 1;
            boxWorkbench.OrientedBoxEditor.CenterY = 25;
            boxWorkbench.OrientedBoxEditor.HalfExtentY = 4;
            boxWorkbench.OrientedBoxEditor.ApplyCommand.Execute(null);
            var appliedBox = boxWorkbench.Selections.SingleOrDefault(selection =>
                selection.Kind == ToolRecipeSelectionKinds.OrientedBox3D);
            Check(
                "explicit numeric Apply adds one current-schema OrientedBox3D without inspection",
                appliedBox?.OrientedBox3D is
                {
                    Center.Y: 25,
                    HalfExtents.Y: 4
                }
                && boxWorkbench.RecipeSchemaVersion == ToolRecipeDocument.CurrentSchemaVersion
                && boxWorkbench.IsDirty
                && boxWorkbench.PipelineSteps.Count == 0
                && boxWorkbench.CurrentMeasurementOutput is null,
                $"schema={boxWorkbench.RecipeSchemaVersion};selection={appliedBox?.Id};dirty={boxWorkbench.IsDirty}");

            var boxPath = Path.Combine(root, "oriented-box.ov3d-recipe.json");
            var boxSaved = boxWorkbench.TrySaveTeachingRecipe(boxPath, out var boxSaveMessage);
            var reopenedBoxWorkbench = new ToolWorkbenchViewModel(
                Path.Combine(root, "box-reopen-recent.json"));
            var boxReopened = reopenedBoxWorkbench.TryOpenTeachingRecipe(
                boxPath,
                out var boxReopenMessage);
            var reopenedBox = reopenedBoxWorkbench.Selections.SingleOrDefault(selection =>
                selection.Kind == ToolRecipeSelectionKinds.OrientedBox3D);
            Check(
                "OrientedBox3D numeric geometry survives Workbench save and reopen",
                boxSaved
                && boxReopened
                && reopenedBox?.Id == appliedBox?.Id
                && reopenedBox?.OrientedBox3D == appliedBox?.OrientedBox3D
                && reopenedBoxWorkbench.OrientedBoxEditor.Selections.Count == 1
                && !reopenedBoxWorkbench.IsDirty,
                boxSaved && boxReopened
                    ? $"selection={reopenedBox?.Id};box={reopenedBox?.OrientedBox3D}"
                    : $"{boxSaveMessage} | {boxReopenMessage}");

            reopenedBoxWorkbench.OrientedBoxEditor.SelectedSelection = reopenedBox;
            reopenedBoxWorkbench.OrientedBoxEditor.CenterY = 30;
            reopenedBoxWorkbench.OrientedBoxEditor.ApplyCommand.Execute(null);
            Check(
                "numeric reapply preserves OrientedBox3D identity",
                reopenedBoxWorkbench.Selections.Count(selection =>
                    selection.Id == reopenedBox?.Id) == 1
                && reopenedBoxWorkbench.Selections.Single(selection =>
                    selection.Id == reopenedBox?.Id).OrientedBox3D?.Center.Y == 30
                && reopenedBoxWorkbench.IsDirty
                && reopenedBoxWorkbench.CurrentMeasurementOutput is null,
                $"selection={reopenedBox?.Id};centerY={reopenedBoxWorkbench.Selections.Single(selection => selection.Id == reopenedBox?.Id).OrientedBox3D?.Center.Y}");

            reopenedBoxWorkbench.OrientedBoxEditor.DeleteCommand.Execute(null);
            Check(
                "OrientedBox3D Delete removes only the unconsumed typed region without inspection",
                reopenedBoxWorkbench.Selections.All(selection =>
                    selection.Kind != ToolRecipeSelectionKinds.OrientedBox3D)
                && reopenedBoxWorkbench.CurrentMeasurementOutput is null,
                $"selections={reopenedBoxWorkbench.Selections.Count};output={reopenedBoxWorkbench.CurrentMeasurementOutput?.OutputEntityId ?? "(none)"}");
        }
        catch (Exception exception)
        {
            lines.Add($"FAIL | unexpected exception | {exception.GetType().Name}: {exception.Message}");
        }
        finally
        {
            try
            {
                if (Directory.Exists(root))
                {
                    Directory.Delete(root, recursive: true);
                }
            }
            catch (IOException exception)
            {
                lines.Add($"FAIL | fixture cleanup | {exception.Message}");
            }
        }

        var reportDirectory = Path.GetDirectoryName(Path.GetFullPath(reportPath));
        if (!string.IsNullOrWhiteSpace(reportDirectory))
        {
            Directory.CreateDirectory(reportDirectory);
        }

        var succeeded = passed == total
                        && total > 0
                        && !lines.Any(line => line.StartsWith("FAIL | unexpected", StringComparison.Ordinal));
        lines.Add($"Result: {(succeeded ? "Pass" : "Fail")} ({passed}/{total} checks)");
        File.WriteAllLines(reportPath, lines);
        summary = $"Inspection Workspace selection verification: {(succeeded ? "Pass" : "Fail")} ({passed}/{total} checks)";
        return succeeded;
    }
}
