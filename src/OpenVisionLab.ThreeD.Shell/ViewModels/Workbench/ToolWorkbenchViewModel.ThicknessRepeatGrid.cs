using System.ComponentModel;
using System.Text.RegularExpressions;
using System.Windows.Input;
using OpenVisionLab.Logging;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Tools.Authoring;
using OpenVisionLab.ThreeD.Viewer;

namespace OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;

/// <summary>
/// Composes the pure repeat-grid authoring session with the current recipe.
/// Candidate geometry is display-only until Apply is invoked explicitly.
/// </summary>
public sealed partial class ToolWorkbenchViewModel
{
    private static readonly Regex NumberedThicknessName = new(
        @"^.+\s+\d+\s+Thickness$",
        RegexOptions.CultureInvariant);
    private RelayCommand beginThicknessRepeatGridCommand = null!;
    private RelayCommand applyThicknessRepeatGridCommand = null!;
    private RelayCommand cancelThicknessRepeatGridCommand = null!;

    public ThicknessRepeatGridAuthoringSession ThicknessRepeatGrid { get; }
    public ICommand BeginThicknessRepeatGridCommand => beginThicknessRepeatGridCommand;
    public ICommand ApplyThicknessRepeatGridCommand => applyThicknessRepeatGridCommand;
    public ICommand CancelThicknessRepeatGridCommand => cancelThicknessRepeatGridCommand;
    public event EventHandler<ThicknessRepeatGridPreviewChangedEventArgs>?
        ThicknessRepeatGridPreviewChanged;

    public bool CanStartThicknessRepeatGrid =>
        !ThicknessRepeatGrid.IsActive
        && !HasPendingStepParameterChanges
        && !IsTeachingSelectionCaptureActive
        && SelectedPipelineStep is { ToolId: "thickness", InputEntityIds.Count: >= 3 }
        && PlaneFlatnessReferenceSelection?.GridRectangle is not null
        && PlaneFlatnessMeasurementSelection?.GridRectangle is not null;

    public string ThicknessRepeatGridAvailabilitySummary => CanStartThicknessRepeatGrid
        ? Localization.ThicknessRepeatReady
        : Localization.ThicknessRepeatUnavailable;

    public int ThicknessRepeatColumns
    {
        get => ThicknessRepeatGrid.Columns;
        set => ThicknessRepeatGrid.Columns = value;
    }

    public int ThicknessRepeatRows
    {
        get => ThicknessRepeatGrid.Rows;
        set => ThicknessRepeatGrid.Rows = value;
    }

    public int ThicknessRepeatColumnPitch
    {
        get => ThicknessRepeatGrid.ColumnPitch;
        set => ThicknessRepeatGrid.ColumnPitch = value;
    }

    public int ThicknessRepeatRowPitch
    {
        get => ThicknessRepeatGrid.RowPitch;
        set => ThicknessRepeatGrid.RowPitch = value;
    }

    public string ThicknessRepeatNamePattern
    {
        get => ThicknessRepeatGrid.NamePattern;
        set => ThicknessRepeatGrid.NamePattern = value;
    }

    public string ThicknessRepeatGridReviewSummary => string.Format(
        System.Globalization.CultureInfo.CurrentCulture,
        Localization.ThicknessRepeatReviewFormat,
        ThicknessRepeatGrid.Candidates.Count,
        ThicknessRepeatGrid.Candidates.Count * 2);

    public string ThicknessRepeatGridValidationSummary =>
        ThicknessRepeatGrid.ValidationSummary;

    public IReadOnlyList<ThicknessRepeatGridCandidate> ThicknessRepeatGridCandidates =>
        ThicknessRepeatGrid.Candidates;

    public bool HasThicknessRepeatGroup => GetThicknessRepeatGroupCount() >= 2;

    public string ThicknessRepeatGroupSummary => string.Format(
        System.Globalization.CultureInfo.CurrentCulture,
        Localization.ThicknessGroupFormat,
        GetThicknessRepeatGroupCount());

    public string ThicknessRepeatGroupMembers => string.Join(
        " · ",
        PipelineSteps
            .Where(step => IsNumberedThicknessInstance(step.ToolName))
            .Select(step => step.ToolName));

    private void InitializeThicknessRepeatGrid()
    {
        beginThicknessRepeatGridCommand = new RelayCommand(
            _ => BeginThicknessRepeatGrid(),
            _ => CanStartThicknessRepeatGrid);
        applyThicknessRepeatGridCommand = new RelayCommand(
            _ => ApplyThicknessRepeatGrid(),
            _ => ThicknessRepeatGrid.IsActive && ThicknessRepeatGrid.IsValid);
        cancelThicknessRepeatGridCommand = new RelayCommand(
            _ => CancelThicknessRepeatGrid("Operator cancelled repeat-grid review."),
            _ => ThicknessRepeatGrid.IsActive);
        ThicknessRepeatGrid.PropertyChanged += OnThicknessRepeatGridPropertyChanged;
    }

    private void BeginThicknessRepeatGrid()
    {
        if (!CanStartThicknessRepeatGrid || SelectedPipelineStep is null)
        {
            return;
        }

        ThicknessRepeatGrid.Begin(CreateDocument(), SelectedPipelineStep.Id);
        RaiseThicknessRepeatGridPresentation();
        AppendLog(
            "Teach",
            $"Thickness repeat-grid review opened | sourceStep={SelectedPipelineStep.Id} | "
            + $"instances={ThicknessRepeatGrid.Candidates.Count} | viewOnly=true | "
            + "recipeChanged=false | inspectionRun=false.");
    }

    private void ApplyThicknessRepeatGrid()
    {
        var draft = ThicknessRepeatGrid.Draft;
        if (draft is null)
        {
            return;
        }

        var generatedCount = draft.Candidates.Count;
        var generatedSelectionCount = generatedCount * 2;
        ThicknessRepeatGrid.Cancel();
        RaiseThicknessRepeatGridPresentation();
        ApplyAuthoredDocument(
            draft.CandidateDocument,
            draft.FirstGeneratedStepId,
            "Thickness repeat grid applied; Preview and Run remain explicit.");
        teachingSelectionStoreOwner.NotifyAppliedSelectionsChanged();
        AppendLog(
            "Teach",
            $"Thickness repeat-grid applied | sourceStep={draft.SourceStepId} | "
            + $"steps={generatedCount} | selections={generatedSelectionCount} | "
            + "recipeChanged=true | inspectionRun=false.");
    }

    private void CancelThicknessRepeatGrid(string reason)
    {
        if (!ThicknessRepeatGrid.IsActive)
        {
            return;
        }

        ThicknessRepeatGrid.Cancel();
        RaiseThicknessRepeatGridPresentation();
        AppendLog(
            "Teach",
            $"Thickness repeat-grid review cancelled | reason={reason} | "
            + "recipeChanged=false | inspectionRun=false.");
    }

    private void CancelThicknessRepeatGridForSelectionChange()
    {
        if (ThicknessRepeatGrid.IsActive)
        {
            CancelThicknessRepeatGrid("Selected recipe step changed.");
        }
    }

    private void OnThicknessRepeatGridPropertyChanged(
        object? sender,
        PropertyChangedEventArgs args) =>
        RaiseThicknessRepeatGridPresentation();

    private void OnThicknessRepeatGridLocalizationChanged(
        object? sender,
        PropertyChangedEventArgs args)
    {
        if (string.IsNullOrEmpty(args.PropertyName)
            || args.PropertyName is nameof(ThreeDLocalization.ThicknessRepeatReady)
            or nameof(ThreeDLocalization.ThicknessRepeatUnavailable)
            or nameof(ThreeDLocalization.ThicknessRepeatReviewFormat)
            or nameof(ThreeDLocalization.ThicknessGroupFormat))
        {
            RaiseThicknessRepeatGridPresentation();
        }
    }

    private void RaiseThicknessRepeatGridPresentation()
    {
        OnPropertyChanged(nameof(CanStartThicknessRepeatGrid));
        OnPropertyChanged(nameof(ThicknessRepeatGridAvailabilitySummary));
        OnPropertyChanged(nameof(ThicknessRepeatColumns));
        OnPropertyChanged(nameof(ThicknessRepeatRows));
        OnPropertyChanged(nameof(ThicknessRepeatColumnPitch));
        OnPropertyChanged(nameof(ThicknessRepeatRowPitch));
        OnPropertyChanged(nameof(ThicknessRepeatNamePattern));
        OnPropertyChanged(nameof(ThicknessRepeatGridReviewSummary));
        OnPropertyChanged(nameof(ThicknessRepeatGridValidationSummary));
        OnPropertyChanged(nameof(ThicknessRepeatGridCandidates));
        OnPropertyChanged(nameof(HasThicknessRepeatGroup));
        OnPropertyChanged(nameof(ThicknessRepeatGroupSummary));
        OnPropertyChanged(nameof(ThicknessRepeatGroupMembers));
        beginThicknessRepeatGridCommand?.RaiseCanExecuteChanged();
        applyThicknessRepeatGridCommand?.RaiseCanExecuteChanged();
        cancelThicknessRepeatGridCommand?.RaiseCanExecuteChanged();
        ThicknessRepeatGridPreviewChanged?.Invoke(
            this,
            new ThicknessRepeatGridPreviewChangedEventArgs(
                ThicknessRepeatGrid.IsActive
                    ? ThicknessRepeatGrid.Candidates
                        .SelectMany(candidate =>
                            new[]
                            {
                                candidate.ReferenceSelection,
                                candidate.MeasurementSelection
                            })
                        .ToArray()
                    : []));
    }

    private void RefreshThicknessRepeatGroupPresentation()
    {
        OnPropertyChanged(nameof(HasThicknessRepeatGroup));
        OnPropertyChanged(nameof(ThicknessRepeatGroupSummary));
        OnPropertyChanged(nameof(ThicknessRepeatGroupMembers));
        OnPropertyChanged(nameof(CanStartThicknessRepeatGrid));
        OnPropertyChanged(nameof(ThicknessRepeatGridAvailabilitySummary));
        beginThicknessRepeatGridCommand?.RaiseCanExecuteChanged();
    }

    private int GetThicknessRepeatGroupCount() =>
        PipelineSteps.Count(step => IsNumberedThicknessInstance(step.ToolName));

    private static bool IsNumberedThicknessInstance(string toolName) =>
        NumberedThicknessName.IsMatch(toolName);
}

public sealed record ThicknessRepeatGridPreviewChangedEventArgs(
    IReadOnlyList<ToolRecipeSelection> Selections);
