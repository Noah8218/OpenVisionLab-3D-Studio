using System.Globalization;
using System.Windows.Input;
using OpenVisionLab.ThreeD.Viewer;

namespace OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;

/// <summary>
/// Composes the independent workspace selection and selected-tool projection
/// with the existing recipe, teaching, PropertyGrid, and artifact owners.
/// </summary>
public sealed partial class ToolWorkbenchViewModel
{
    private bool isSynchronizingInspectionWorkspace;
    private RelayCommand selectWorkspaceInputCommand = null!;
    private RelayCommand selectWorkspaceRegionCommand = null!;
    private RelayCommand selectWorkspaceOutputCommand = null!;
    private RelayCommand fitWorkspaceRegionCommand = null!;
    private RelayCommand showWorkspaceOutputCommand = null!;
    private RelayCommand pinWorkspaceOutputCommand = null!;
    private RelayCommand compareWorkspaceOutputCommand = null!;

    public InspectionWorkspaceSelectionSession WorkspaceSelection { get; }
    public SelectedToolWorkspaceViewModel SelectedToolWorkspace { get; }
    public ICommand SelectWorkspaceInputCommand => selectWorkspaceInputCommand;
    public ICommand SelectWorkspaceRegionCommand => selectWorkspaceRegionCommand;
    public ICommand SelectWorkspaceOutputCommand => selectWorkspaceOutputCommand;
    public ICommand FitWorkspaceRegionCommand => fitWorkspaceRegionCommand;
    public ICommand ShowWorkspaceOutputCommand => showWorkspaceOutputCommand;
    public ICommand PinWorkspaceOutputCommand => pinWorkspaceOutputCommand;
    public ICommand CompareWorkspaceOutputCommand => compareWorkspaceOutputCommand;
    public event EventHandler? FitWorkspaceRegionRequested;
    public event EventHandler? OutputComparePaneRequested;

    private void InitializeInspectionWorkspace()
    {
        selectWorkspaceInputCommand = new RelayCommand(
            parameter =>
            {
                if (parameter is SelectedToolInputItem input)
                {
                    WorkspaceSelection.SelectInput(input.EntityId);
                }
            },
            parameter => parameter is SelectedToolInputItem);
        selectWorkspaceRegionCommand = new RelayCommand(
            parameter =>
            {
                if (parameter is SelectedToolRegionItem region)
                {
                    WorkspaceSelection.SelectRegion(region.Role, region.SelectionId);
                }
            },
            parameter => parameter is SelectedToolRegionItem);
        selectWorkspaceOutputCommand = new RelayCommand(
            parameter =>
            {
                if (parameter is SelectedToolOutputItem output)
                {
                    WorkspaceSelection.SelectOutput(output.EntityId);
                }
            },
            parameter => parameter is SelectedToolOutputItem);
        fitWorkspaceRegionCommand = new RelayCommand(
            parameter =>
            {
                if (parameter is not SelectedToolRegionItem region)
                {
                    return;
                }

                WorkspaceSelection.SelectRegion(region.Role, region.SelectionId);
                FitWorkspaceRegionRequested?.Invoke(this, EventArgs.Empty);
            },
            parameter => parameter is SelectedToolRegionItem
            {
                Lifecycle: InspectionWorkspaceRegionLifecycleState.Review
                    or InspectionWorkspaceRegionLifecycleState.Applied
            });
        showWorkspaceOutputCommand = new RelayCommand(
            parameter => ShowWorkspaceOutput(parameter as SelectedToolOutputItem),
            parameter => ResolveDisplayedOutput(parameter as SelectedToolOutputItem) is { CanShowInViewer: true });
        pinWorkspaceOutputCommand = new RelayCommand(
            parameter => PinWorkspaceOutput(parameter as SelectedToolOutputItem),
            parameter => ResolveDisplayedOutput(parameter as SelectedToolOutputItem) is { CanPinToCompare: true });
        compareWorkspaceOutputCommand = new RelayCommand(
            parameter => CompareWorkspaceOutput(parameter as SelectedToolOutputItem),
            parameter => ResolveDisplayedOutput(parameter as SelectedToolOutputItem) is
                { IsPinnedToCompare: true } or { CanPinToCompare: true });
    }

    private void SynchronizeInspectionWorkspace()
    {
        var step = SelectedPipelineStep;
        var sameStep = string.Equals(
            WorkspaceSelection.SelectedStepId,
            step?.Id,
            StringComparison.OrdinalIgnoreCase);
        var inputId = sameStep
                      && step?.InputEntityIds.Contains(
                          WorkspaceSelection.SelectedInputEntityId ?? string.Empty,
                          StringComparer.OrdinalIgnoreCase) == true
            ? WorkspaceSelection.SelectedInputEntityId
            : step?.InputEntityIds.FirstOrDefault();
        var role = GetInspectionWorkspaceRegionRole();
        var regionId = SelectedStepTeachingSelection?.Id;
        var outputId = sameStep && !string.IsNullOrWhiteSpace(WorkspaceSelection.SelectedOutputEntityId)
            ? WorkspaceSelection.SelectedOutputEntityId
            : step?.OutputEntityId;

        isSynchronizingInspectionWorkspace = true;
        try
        {
            WorkspaceSelection.SynchronizeTool(
                step?.Id,
                inputId,
                role,
                regionId,
                outputId);
        }
        finally
        {
            isSynchronizingInspectionWorkspace = false;
        }

        RefreshSelectedToolWorkspaceProjection();
    }

    private void OnInspectionWorkspaceSelectionChanged(
        object? sender,
        InspectionWorkspaceSelectionChangedEventArgs args)
    {
        SynchronizeViewerWorkspaceFocus(args.Current.FocusedViewerSlotId);
        if (isSynchronizingInspectionWorkspace)
        {
            return;
        }

        if (IsSelectedStepDualRoiMeasurement
            && string.Equals(
                args.Current.SelectedStepId,
                SelectedPipelineStep?.Id,
                StringComparison.OrdinalIgnoreCase))
        {
            var selectsSecondRole =
                args.Current.ActiveRegionRole is InspectionWorkspaceRegionRole.Measurement
                    or InspectionWorkspaceRegionRole.Second;
            var selectsFirstRole =
                args.Current.ActiveRegionRole is InspectionWorkspaceRegionRole.Reference
                    or InspectionWorkspaceRegionRole.First;
            if ((selectsFirstRole || selectsSecondRole)
                && isPlaneFlatnessMeasurementRole != selectsSecondRole)
            {
                isPlaneFlatnessMeasurementRole = selectsSecondRole;
                NotifyPlaneFlatnessTeachingState();
                RefreshTeachingSelectionContext();
                return;
            }
        }

        RefreshSelectedToolWorkspaceProjection();
    }

    private void RefreshSelectedToolWorkspaceProjection()
    {
        SelectedToolWorkspace.Refresh(new SelectedToolWorkspaceProjection(
            SelectedPipelineStep,
            SelectedStepPropertyDraft,
            IsSelectedStepPropertyGridSupported,
            HasPendingStepParameterChanges,
            StepParameterEditStatus,
            ArtifactRegistry.ToArray(),
            DisplayedOutputs.ToArray(),
            CreateSelectedToolOutputEvidence(),
            SelectedStepSelectionRequirement,
            SelectedStepTeachingSelection,
            IsSelectedStepDualRoiMeasurement,
            IsSelectedStepGapFlush,
            PlaneFlatnessReferenceSelection,
            PlaneFlatnessMeasurementSelection,
            DualRoiFirstLabel,
            DualRoiSecondLabel,
            IsTeachingSelectionCaptureActive,
            CanApplyTeachingSelectionCapture,
            Localization));
        OnPropertyChanged(nameof(SelectedWorkspaceTitle));
        OnPropertyChanged(nameof(SelectedWorkspaceState));
        RefreshHeightImageRoiProjection();
        fitWorkspaceRegionCommand?.RaiseCanExecuteChanged();
        showWorkspaceOutputCommand?.RaiseCanExecuteChanged();
        pinWorkspaceOutputCommand?.RaiseCanExecuteChanged();
        compareWorkspaceOutputCommand?.RaiseCanExecuteChanged();
    }

    private SelectedToolOutputEvidence CreateSelectedToolOutputEvidence()
    {
        if (IsSelectedStepRemoveOutlierPixels
            && CurrentRemoveOutlierPreviewOutput is not null
            && CurrentRemoveOutlierMask is { } outlierMask)
        {
            return new SelectedToolOutputEvidence(
                "Removed outliers",
                outlierMask.OutlierCellCount.ToString(
                    CultureInfo.InvariantCulture),
                "count",
                IsRemoveOutlierPreviewRunning
                    ? "Preview running"
                    : IsRemoveOutlierPreviewPublished
                        ? "Published"
                        : "Preview");
        }

        if (IsSelectedStepLevelSurface
            && CurrentLevelSurfaceTransform is { } transform)
        {
            return new SelectedToolOutputEvidence(
                "Reference RMS",
                transform.ReferenceResidualRms.ToString(
                    "G6",
                    CultureInfo.InvariantCulture),
                transform.SourceUnit,
                IsLevelSurfacePreviewPublished
                    ? "Published"
                    : IsLevelSurfacePreviewRunning
                        ? "Preview running"
                        : "Preview");
        }

        if (SelectedPipelineStep is not { } step
            || measurementPreviewOutput is null
            || !string.Equals(
                measurementPreviewOutput.OutputEntityId,
                step.OutputEntityId,
                StringComparison.OrdinalIgnoreCase))
        {
            return SelectedToolOutputEvidence.Empty;
        }

        var primaryMetric = measurementPreviewOutput.Result.Metrics.FirstOrDefault(metric =>
            double.IsFinite(metric.Value)
            && !string.Equals(metric.Unit, "count", StringComparison.OrdinalIgnoreCase));
        return primaryMetric is null
            ? new SelectedToolOutputEvidence(
                "Value",
                "\u2014",
                measurementPreviewOutput.Unit,
                measurementPreviewOutput.Result.Status.ToString())
            : new SelectedToolOutputEvidence(
                primaryMetric.Name,
                primaryMetric.Value.ToString("G6", CultureInfo.InvariantCulture),
                primaryMetric.Unit,
                measurementPreviewOutput.Result.Status.ToString());
    }

    private ToolWorkbenchDisplayedOutputItem? ResolveDisplayedOutput(SelectedToolOutputItem? output) =>
        output is null
            ? null
            : DisplayedOutputs.FirstOrDefault(item =>
                string.Equals(item.Id, output.EntityId, StringComparison.OrdinalIgnoreCase));

    private void ShowWorkspaceOutput(SelectedToolOutputItem? output)
    {
        if (ResolveDisplayedOutput(output) is not { CanShowInViewer: true } displayed)
        {
            return;
        }

        RequestDisplayedOutputInViewer(displayed);
    }

    private void PinWorkspaceOutput(SelectedToolOutputItem? output)
    {
        if (ResolveDisplayedOutput(output) is not { CanPinToCompare: true } displayed)
        {
            return;
        }

        WorkspaceSelection.SelectOutput(displayed.Id);
        PinDisplayedOutputToCompare(displayed);
    }

    private void CompareWorkspaceOutput(SelectedToolOutputItem? output)
    {
        var displayed = ResolveDisplayedOutput(output);
        if (displayed is null)
        {
            return;
        }

        WorkspaceSelection.SelectOutput(displayed.Id);
        if (!displayed.IsPinnedToCompare)
        {
            PinDisplayedOutputToCompare(displayed);
        }

        if (displayed.IsPinnedToCompare)
        {
            OutputComparePaneRequested?.Invoke(this, EventArgs.Empty);
        }
    }

    private InspectionWorkspaceRegionRole GetInspectionWorkspaceRegionRole()
    {
        if (SelectedPipelineStep is null || SelectedStepSelectionRequirement is null)
        {
            return InspectionWorkspaceRegionRole.None;
        }

        if (!IsSelectedStepDualRoiMeasurement)
        {
            return InspectionWorkspaceRegionRole.Selection;
        }

        if (IsSelectedStepGapFlush)
        {
            return IsPlaneFlatnessMeasurementRoleActive
                ? InspectionWorkspaceRegionRole.Second
                : InspectionWorkspaceRegionRole.First;
        }

        return IsPlaneFlatnessMeasurementRoleActive
            ? InspectionWorkspaceRegionRole.Measurement
            : InspectionWorkspaceRegionRole.Reference;
    }
}
