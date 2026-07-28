using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Windows.Controls.WpfPropertyGrid;
using System.Windows.Input;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Data;
using OpenVisionLab.ThreeD.Viewer;

namespace OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;

public sealed partial class ToolWorkbenchViewModel
{
    private RelayCommand applySelectedStepParameterDraftCommand = null!;
    private RelayCommand discardSelectedStepParameterDraftCommand = null!;
    private RelayCommand markSelectedStepParameterDraftDirtyCommand = null!;
    private readonly ToolWorkbenchStepPropertySession stepPropertySession = new();
    private ToolRecipeSource? openedSourceIdentity;
    private IReadOnlyList<string> sourceIdentityErrors = [];
    private readonly string recentRecipesPath;

    public event EventHandler<ToolWorkbenchRecipePathRequestEventArgs>? OpenRecentTeachingRecipeRequested;

    public ObservableCollection<ToolWorkbenchRecentRecipeItem> RecentRecipes { get; } = [];

    public string? MostRecentAvailableRecipePath =>
        RecentRecipes.FirstOrDefault(recipe => recipe.IsAvailable)?.Path;

    public object? SelectedStepPropertyDraft => stepPropertySession.Draft;

    public bool IsSelectedStepPropertyGridSupported => stepPropertySession.IsSupported;

    public bool HasPendingStepParameterChanges => stepPropertySession.HasPendingChanges;

    public string StepParameterEditStatus => stepPropertySession.Status;

    public string SelectedStepAdapterStatus =>
        stepPropertySession.GetAdapterStatus(SelectedPipelineStep);

    public int SupportedStepCount => PipelineSteps.Count(ToolWorkbenchStepPropertySession.IsSupportedTool);

    public int UnsupportedStepCount => PipelineSteps.Count - SupportedStepCount;

    public string RecipeAdapterCoverageSummary => PipelineSteps.Count == 0
        ? "No inspection steps"
        : UnsupportedStepCount == 0
            ? $"{SupportedStepCount}/{PipelineSteps.Count} typed adapters ready"
            : $"{SupportedStepCount}/{PipelineSteps.Count} typed adapters ready | {UnsupportedStepCount} preserved read-only";

    public ICommand ApplySelectedStepParameterDraftCommand => applySelectedStepParameterDraftCommand;

    public ICommand DiscardSelectedStepParameterDraftCommand => discardSelectedStepParameterDraftCommand;

    public ICommand MarkSelectedStepParameterDraftDirtyCommand => markSelectedStepParameterDraftDirtyCommand;

    public ICommand OpenRecentTeachingRecipeCommand { get; private set; } = null!;

    public ICommand RemoveRecentTeachingRecipeCommand { get; private set; } = null!;

    public bool HasUncommittedRecipeChanges => IsDirty || HasPendingStepParameterChanges;

    public bool IsSourceReadyForRecipe => sourceIdentityErrors.Count == 0
        && loadedSourceBinding is not null
        && File.Exists(Source.Path)
        && string.Equals(Source.Format, "C3D", StringComparison.OrdinalIgnoreCase);

    public string SourceReadinessSummary => string.IsNullOrWhiteSpace(Source.Path)
        ? "Source: not selected"
        : !string.Equals(Source.Format, "C3D", StringComparison.OrdinalIgnoreCase)
            ? $"Source: unsupported format ({Source.Format})"
            : !File.Exists(Source.Path)
                ? "Source: missing - relink required"
                : sourceIdentityErrors.Count > 0
                    ? "Source: identity mismatch - relink required"
                    : loadedSourceBinding is null
                        ? "Source: unreadable"
                        : $"Source: ready | {loadedSourceBinding.GridWidth} x {loadedSourceBinding.GridHeight}";

    public string ViewerSourceSummary => IsSourceReadyForRecipe
        ? $"Source: {Source.Name}"
        : SourceReadinessSummary;

    private void InitializePropertyGridEditing()
    {
        stepPropertySession.PropertyChanged += OnStepPropertySessionChanged;
        applySelectedStepParameterDraftCommand = new RelayCommand(
            _ => TryApplySelectedStepParameterDraft(out var _),
            _ => IsSelectedStepPropertyGridSupported && HasPendingStepParameterChanges);
        discardSelectedStepParameterDraftCommand = new RelayCommand(
            _ => DiscardSelectedStepParameterDraft(),
            _ => IsSelectedStepPropertyGridSupported && HasPendingStepParameterChanges);
        markSelectedStepParameterDraftDirtyCommand = new RelayCommand(
            _ => MarkSelectedStepParameterDraftDirty(),
            _ => IsSelectedStepPropertyGridSupported);
        OpenRecentTeachingRecipeCommand = new RelayCommand(
            parameter =>
            {
                if (parameter is ToolWorkbenchRecentRecipeItem item)
                {
                    OpenRecentTeachingRecipeRequested?.Invoke(this, new ToolWorkbenchRecipePathRequestEventArgs(item.Path));
                }
            });
        RemoveRecentTeachingRecipeCommand = new RelayCommand(
            parameter =>
            {
                if (parameter is ToolWorkbenchRecentRecipeItem item)
                {
                    RecentRecipes.Remove(item);
                    SaveRecentRecipes();
                }
            });
        LoadRecentRecipes();
    }

    private void OnStepPropertySessionChanged(object? sender, PropertyChangedEventArgs args)
    {
        switch (args.PropertyName)
        {
            case nameof(ToolWorkbenchStepPropertySession.Draft):
                OnPropertyChanged(nameof(SelectedStepPropertyDraft));
                break;
            case nameof(ToolWorkbenchStepPropertySession.IsSupported):
                OnPropertyChanged(nameof(IsSelectedStepPropertyGridSupported));
                break;
            case nameof(ToolWorkbenchStepPropertySession.HasPendingChanges):
                OnPropertyChanged(nameof(HasPendingStepParameterChanges));
                OnPropertyChanged(nameof(HasUncommittedRecipeChanges));
                applySelectedStepParameterDraftCommand?.RaiseCanExecuteChanged();
                discardSelectedStepParameterDraftCommand?.RaiseCanExecuteChanged();
                RefreshStepCommands();
                break;
            case nameof(ToolWorkbenchStepPropertySession.Status):
                OnPropertyChanged(nameof(StepParameterEditStatus));
                break;
            case ToolWorkbenchStepPropertySession.AdapterStatusPropertyName:
                OnPropertyChanged(nameof(SelectedStepAdapterStatus));
                break;
        }

        RefreshSelectedToolWorkspaceProjection();
    }

    public void MarkSelectedStepParameterDraftDirty()
    {
        if (!IsSelectedStepPropertyGridSupported)
        {
            return;
        }

        stepPropertySession.MarkDirty();
    }

    public void ReportParameterDraftCommitError(string message) => stepPropertySession.SetStatus(message);

    internal bool TryConfigureInvalidHeightDifferenceEdgeDraftForSmoke()
    {
        if (SelectedStepPropertyDraft is not HeightDifferenceEdgeStepProperties edge)
        {
            return false;
        }

        edge.ComparisonAxis = HeightDifferenceEdgeComparisonAxis.AcrossColumns;
        edge.Polarity = HeightDifferenceEdgePolarity.Rising;
        edge.MinimumDelta = 0;
        stepPropertySession.ResetDraftForSmoke(edge);
        MarkSelectedStepParameterDraftDirty();
        _ = TryApplySelectedStepParameterDraft(out _);
        return true;
    }

    public bool TryApplySelectedStepParameterDraft(out string message)
    {
        if (SelectedPipelineStep is not { } step)
        {
            message = "The selected step changed. Discard the draft and select the step again.";
            stepPropertySession.SetStatus(message);
            return false;
        }
        if (!stepPropertySession.TryCreateParameterValues(step, out var values, out message))
        {
            return false;
        }


        var changed = false;
        if (step.ToolId == "re-grid-height-map")
        {
            for (var index = step.Parameters.Count - 1; index >= 0; index--)
            {
                if (!values.ContainsKey(step.Parameters[index].Name))
                {
                    step.Parameters.RemoveAt(index);
                    changed = true;
                }
            }
        }
        foreach (var pair in values)
        {
            var parameter = step.Parameters.FirstOrDefault(item =>
                string.Equals(item.Name, pair.Key, StringComparison.Ordinal));
            if (parameter is null)
            {
                parameter = new ToolWorkbenchParameterItem(pair.Key, pair.Value);
                parameter.PropertyChanged += OnRecipePartChanged;
                step.Parameters.Add(parameter);
                changed = true;
                continue;
            }

            if (!string.Equals(parameter.Value, pair.Value, StringComparison.Ordinal))
            {
                parameter.Value = pair.Value;
                changed = true;
            }
        }

        if (changed)
        {
            MarkFilterPreviewStaleIfNeeded(step);
            MarkRemoveOutlierPreviewStaleIfNeeded(step);
            MarkHeightDifferenceEdgePreviewStaleIfNeeded(step);
            MarkTwoPointLinePreviewStaleIfNeeded(step);
            MarkThreePointPlanePreviewStaleIfNeeded(step);
            MarkDatumPlaneDeviationPreviewStaleIfNeeded(step);
            MarkLineFitPreviewStaleIfNeeded();
            MarkLineIntersectionPreviewStaleIfNeeded();
            MarkLandmarkCorrespondencePreviewStaleIfNeeded();
            MarkAffineSolvePreviewStaleIfNeeded();
            SetDirty(true);
            RefreshRecipeState();
        }

        message = changed
            ? "Parameters applied to the recipe. Preview and Publish were not run."
            : "No committed parameter value changed.";
        stepPropertySession.Refresh(step, message);
        return true;
    }

    public void DiscardSelectedStepParameterDraft() =>
        RefreshSelectedStepPropertyDraft("Unapplied changes discarded. Recipe parameters were not changed.");

    private void RefreshSelectedStepPropertyDraft(string? status = null) =>
        stepPropertySession.Refresh(SelectedPipelineStep, status);

    private void SetParameterDraftStatus(string status) =>
        stepPropertySession.SetStatus(status);

    private void RefreshAdapterCoverage()
    {
        OnPropertyChanged(nameof(SupportedStepCount));
        OnPropertyChanged(nameof(UnsupportedStepCount));
        OnPropertyChanged(nameof(RecipeAdapterCoverageSummary));
        OnPropertyChanged(nameof(SelectedStepAdapterStatus));
    }

    private void CaptureOpenedSourceIdentity(ToolRecipeSource source) => openedSourceIdentity = source;

    private void AcceptCurrentSourceIdentity() => openedSourceIdentity = null;

    private void RefreshSourceIdentityState()
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(Source.Path))
        {
            errors.Add("A C3D source must be selected.");
        }
        else if (!string.Equals(Source.Format, "C3D", StringComparison.OrdinalIgnoreCase))
        {
            errors.Add($"Source format '{Source.Format}' is unsupported. Recipe Manager v1 requires C3D.");
        }
        else if (!File.Exists(Source.Path))
        {
            errors.Add($"The recipe source file is missing: {Source.Path}");
        }
        else if (loadedSourceBinding is null)
        {
            errors.Add("The recipe source exists but its C3D identity could not be read.");
        }
        else if (openedSourceIdentity is { } expected)
        {
            var actualLength = new FileInfo(Source.Path).Length;
            if (expected.ByteLength is { } expectedLength && expectedLength != actualLength)
            {
                errors.Add($"Source byte length mismatch: recipe {expectedLength}, actual {actualLength}.");
            }

            if (!string.IsNullOrWhiteSpace(expected.ContentSha256)
                && !string.Equals(expected.ContentSha256, loadedSourceBinding.ContentSha256, StringComparison.OrdinalIgnoreCase))
            {
                errors.Add("Source SHA-256 does not match the recipe identity.");
            }

            if (expected.GridWidth is { } width && width != loadedSourceBinding.GridWidth
                || expected.GridHeight is { } height && height != loadedSourceBinding.GridHeight)
            {
                errors.Add($"Source grid mismatch: recipe {expected.GridWidth} x {expected.GridHeight}, actual {loadedSourceBinding.GridWidth} x {loadedSourceBinding.GridHeight}.");
            }
        }

        sourceIdentityErrors = errors;
        OnPropertyChanged(nameof(IsSourceReadyForRecipe));
        OnPropertyChanged(nameof(SourceReadinessSummary));
        OnPropertyChanged(nameof(ViewerSourceSummary));
        NotifyFirstRecipeUx();
    }

    private void RecordRecentRecipe(string path)
    {
        var fullPath = Path.GetFullPath(path);
        var existing = RecentRecipes.FirstOrDefault(item =>
            string.Equals(item.Path, fullPath, StringComparison.OrdinalIgnoreCase));
        if (existing is not null)
        {
            RecentRecipes.Remove(existing);
        }

        RecentRecipes.Insert(0, ToolWorkbenchRecentRecipeItem.From(fullPath));
        while (RecentRecipes.Count > RecipeRecentFileStore.MaximumEntries)
        {
            RecentRecipes.RemoveAt(RecentRecipes.Count - 1);
        }

        SaveRecentRecipes();
    }

    private void LoadRecentRecipes()
    {
        try
        {
            foreach (var path in RecipeRecentFileStore.Load(recentRecipesPath))
            {
                RecentRecipes.Add(ToolWorkbenchRecentRecipeItem.From(path));
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            AppendLog("Warning", $"Recent recipe list unavailable: {exception.Message}");
        }
    }

    private void SaveRecentRecipes()
    {
        try
        {
            RecipeRecentFileStore.Save(recentRecipesPath, RecentRecipes.Select(item => item.Path));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            AppendLog("Warning", $"Recent recipe list could not be saved: {exception.Message}");
        }
    }

}


public sealed class ToolWorkbenchRecipePathRequestEventArgs(string path) : EventArgs
{
    public string Path { get; } = path;
}

public sealed record ToolWorkbenchRecentRecipeItem(string Path, string Name, bool IsAvailable)
{
    public string Availability => IsAvailable ? "Available" : "Unavailable";

    public static ToolWorkbenchRecentRecipeItem From(string path) => new(
        path,
        System.IO.Path.GetFileNameWithoutExtension(path),
        File.Exists(path));
}
