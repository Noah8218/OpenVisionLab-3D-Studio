using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Data;

namespace OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;

public sealed partial class ToolWorkbenchViewModel
{
    private void InitializeOrientedBox3DEditing()
    {
        OrientedBoxEditor.ApplyRequested += OnOrientedBoxApplyRequested;
        OrientedBoxEditor.DeleteRequested += OnOrientedBoxDeleteRequested;
        OrientedBoxEditor.DraftChanged += OnOrientedBoxDraftChanged;
    }

    private void OnOrientedBoxDraftChanged(
        object? sender,
        OrientedBox3DDraftChangedEventArgs args)
    {
        OnPropertyChanged(nameof(IsSelectionCandidateActive));
        OnPropertyChanged(nameof(IsPipelineReviewExpanded));
        OnPropertyChanged(nameof(TeachingSelectionCaptureTitle));
        OnPropertyChanged(nameof(TeachingSelectionCaptureProgress));
        OnPropertyChanged(nameof(TeachingSelectionCaptureInstruction));
        OnPropertyChanged(nameof(IsOrientedBoxEditorContextVisible));
        OnPropertyChanged(nameof(IsSelectedStepRegionSurfaceVisible));
        teachingSelectionCaptureOwner.RefreshCommandStates();
    }

    private void OnOrientedBoxApplyRequested(
        object? sender,
        OrientedBox3DApplyRequestedEventArgs args)
    {
        var selection = args.Selection;
        if (SourceSession.SourceBinding is null
            || selection.Kind != ToolRecipeSelectionKinds.OrientedBox3D
            || selection.OrientedBox3D is null
            || !string.Equals(selection.RootSourceId, Source.Id, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(selection.FrameId, Source.FrameId, StringComparison.Ordinal)
            || !ToolRecipeSelectionSourceBindingVerifier.BindingsEqual(
                selection.SourceBinding,
                SourceSession.SourceBinding)
            || ToolRecipeOrientedBox3DGeometry.Validate(selection.OrientedBox3D).Count > 0)
        {
            OrientedBoxEditor.SetStatus(
                "Apply rejected: source identity or OrientedBox3D geometry is invalid.");
            return;
        }

        MutateRecipe(() =>
        {
            PromoteRecipeSchemaForSelection();
            teachingSelectionStoreOwner.Upsert(selection);
        });

        OrientedBoxEditor.SelectedSelection = Selections.First(item =>
            string.Equals(item.Id, selection.Id, StringComparison.OrdinalIgnoreCase));
        OrientedBoxEditor.SetStatus(
            "OrientedBox3D applied to the recipe. Inspection was not run.");
        teachingSelectionStoreOwner.NotifyAppliedSelectionsChanged();
        AppendLog(
            "Teach",
            $"OrientedBox3D applied | selection={selection.Id} | frame={selection.FrameId} | recipeChanged=true | inspectionRun=false.");
    }

    private void OnOrientedBoxDeleteRequested(
        object? sender,
        OrientedBox3DDeleteRequestedEventArgs args)
    {
        var selection = Selections.FirstOrDefault(item =>
            item.Kind == ToolRecipeSelectionKinds.OrientedBox3D
            && string.Equals(item.Id, args.SelectionId, StringComparison.OrdinalIgnoreCase));
        if (selection is null)
        {
            OrientedBoxEditor.SetStatus("Delete rejected: the selected 3D box no longer exists.");
            return;
        }

        if (PipelineSteps.Any(step =>
                step.InputEntityIds.Contains(selection.Id, StringComparer.OrdinalIgnoreCase)))
        {
            OrientedBoxEditor.SetStatus(
                "Delete rejected: a recipe step consumes this 3D box.");
            return;
        }

        MutateRecipe(() => teachingSelectionStoreOwner.Remove(selection));
        OrientedBoxEditor.CompleteDelete();
        teachingSelectionStoreOwner.NotifyAppliedSelectionsChanged();
        AppendLog(
            "Teach",
            $"OrientedBox3D deleted | selection={selection.Id} | recipeChanged=true | inspectionRun=false.");
    }
}
