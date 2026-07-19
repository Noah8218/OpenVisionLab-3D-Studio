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
    private object? selectedStepPropertyDraft;
    private string? selectedStepPropertyDraftStepId;
    private bool hasPendingStepParameterChanges;
    private string stepParameterEditStatus = "Select Filter, Height Difference Edge, or 3D Line Fit to teach typed parameters.";
    private ToolRecipeSource? openedSourceIdentity;
    private IReadOnlyList<string> sourceIdentityErrors = [];
    private readonly string recentRecipesPath;

    public event EventHandler<ToolWorkbenchRecipePathRequestEventArgs>? OpenRecentTeachingRecipeRequested;

    public ObservableCollection<ToolWorkbenchRecentRecipeItem> RecentRecipes { get; } = [];

    public object? SelectedStepPropertyDraft
    {
        get => selectedStepPropertyDraft;
        private set
        {
            if (ReferenceEquals(selectedStepPropertyDraft, value))
            {
                return;
            }

            selectedStepPropertyDraft = value;
            OnPropertyChanged();
        }
    }

    public bool IsSelectedStepPropertyGridSupported => SelectedStepPropertyDraft is not null;

    public bool HasPendingStepParameterChanges => hasPendingStepParameterChanges;

    public string StepParameterEditStatus => stepParameterEditStatus;

    public string SelectedStepAdapterStatus => SelectedPipelineStep switch
    {
        null => "No step selected",
        { ToolId: "filter" } step => FormatAdapterStatus(step, FilterStepProperties.MappedNames),
        { ToolId: "height-difference-edge" } step => FormatAdapterStatus(step, HeightDifferenceEdgeStepProperties.MappedNames),
        { ToolId: "three-d-line-fit" } step => FormatAdapterStatus(step, LineFitStepProperties.MappedNames),
        _ => "Partially supported - parameters are preserved read-only"
    };

    public int SupportedStepCount => PipelineSteps.Count(IsSupportedPropertyGridTool);

    public int UnsupportedStepCount => PipelineSteps.Count - SupportedStepCount;

    public string RecipeAdapterCoverageSummary => PipelineSteps.Count == 0
        ? "No inspection steps"
        : UnsupportedStepCount == 0
            ? $"{SupportedStepCount}/{PipelineSteps.Count} typed adapters ready"
            : $"{SupportedStepCount}/{PipelineSteps.Count} typed adapters ready | {UnsupportedStepCount} preserved read-only";

    public ICommand ApplySelectedStepParameterDraftCommand => applySelectedStepParameterDraftCommand;

    public ICommand DiscardSelectedStepParameterDraftCommand => discardSelectedStepParameterDraftCommand;

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
        applySelectedStepParameterDraftCommand = new RelayCommand(
            _ => TryApplySelectedStepParameterDraft(out var _),
            _ => IsSelectedStepPropertyGridSupported && HasPendingStepParameterChanges);
        discardSelectedStepParameterDraftCommand = new RelayCommand(
            _ => DiscardSelectedStepParameterDraft(),
            _ => IsSelectedStepPropertyGridSupported && HasPendingStepParameterChanges);
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

    public void MarkSelectedStepParameterDraftDirty()
    {
        if (!IsSelectedStepPropertyGridSupported)
        {
            return;
        }

        SetParameterDraftState(true, "Unapplied parameter changes. Apply or discard before changing recipe sessions.");
    }

    public void ReportParameterDraftCommitError(string message) => SetParameterDraftStatus(message);

    internal bool TryConfigureInvalidHeightDifferenceEdgeDraftForSmoke()
    {
        if (SelectedStepPropertyDraft is not HeightDifferenceEdgeStepProperties edge)
        {
            return false;
        }

        edge.ComparisonAxis = HeightDifferenceEdgeComparisonAxis.AcrossColumns;
        edge.Polarity = HeightDifferenceEdgePolarity.Rising;
        edge.MinimumDelta = 0;
        SelectedStepPropertyDraft = null;
        SelectedStepPropertyDraft = edge;
        MarkSelectedStepParameterDraftDirty();
        _ = TryApplySelectedStepParameterDraft(out _);
        return true;
    }

    public bool TryApplySelectedStepParameterDraft(out string message)
    {
        if (SelectedPipelineStep is not { } step
            || !string.Equals(step.Id, selectedStepPropertyDraftStepId, StringComparison.Ordinal))
        {
            message = "The selected step changed. Discard the draft and select the step again.";
            SetParameterDraftStatus(message);
            return false;
        }

        IReadOnlyDictionary<string, string> values;
        switch (SelectedStepPropertyDraft)
        {
            case FilterStepProperties filter:
                if (!filter.TryValidate(out message))
                {
                    SetParameterDraftStatus(message);
                    return false;
                }

                values = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["Method"] = filter.Method.ToString(),
                    ["KernelSize"] = filter.KernelSize.ToString(CultureInfo.InvariantCulture),
                    ["MissingValuePolicy"] = filter.MissingValuePolicy.ToString(),
                    ["BoundaryPolicy"] = filter.BoundaryPolicy.ToString()
                };
                break;
            case HeightDifferenceEdgeStepProperties edge:
                if (!edge.TryValidate(out message))
                {
                    SetParameterDraftStatus(message);
                    return false;
                }

                values = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["ComparisonAxis"] = edge.ComparisonAxis.ToString(),
                    ["Polarity"] = edge.Polarity.ToString(),
                    ["MinimumDelta"] = edge.MinimumDelta.ToString("G17", CultureInfo.InvariantCulture),
                    ["CandidatePolicy"] = edge.CandidatePolicy.ToString(),
                    ["PointPolicy"] = edge.PointPolicy.ToString(),
                    ["MissingValuePolicy"] = edge.MissingValuePolicy.ToString(),
                    ["BoundaryPolicy"] = edge.BoundaryPolicy.ToString()
                };
                break;
            case LineFitStepProperties lineFit:
                if (!lineFit.TryValidate(out message))
                {
                    SetParameterDraftStatus(message);
                    return false;
                }

                values = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["FitMethod"] = lineFit.FitMethod.ToString(),
                    ["MaximumOrthogonalResidual"] = lineFit.MaximumOrthogonalResidual.ToString("G17", CultureInfo.InvariantCulture),
                    ["MinimumInlierCount"] = lineFit.MinimumInlierCount.ToString(CultureInfo.InvariantCulture),
                    ["MinimumInlierRatio"] = lineFit.MinimumInlierRatio.ToString("G17", CultureInfo.InvariantCulture),
                    ["MinimumInlierScanlineSpan"] = lineFit.MinimumInlierScanlineSpan.ToString(CultureInfo.InvariantCulture),
                    ["HypothesisPolicy"] = lineFit.HypothesisPolicy.ToString(),
                    ["MaximumHypotheses"] = "256",
                    ["RefinementPolicy"] = lineFit.RefinementPolicy.ToString(),
                    ["DirectionPolicy"] = lineFit.DirectionPolicy.ToString(),
                    ["EndpointPolicy"] = lineFit.EndpointPolicy.ToString()
                };
                break;
            default:
                message = "This step has no typed parameter adapter.";
                SetParameterDraftStatus(message);
                return false;
        }

        var changed = false;
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
            MarkHeightDifferenceEdgePreviewStaleIfNeeded(step);
            MarkLineFitPreviewStaleIfNeeded();
            SetDirty(true);
            RefreshRecipeState();
        }

        message = changed
            ? "Parameters applied to the recipe. Preview and Publish were not run."
            : "No committed parameter value changed.";
        RefreshSelectedStepPropertyDraft(message);
        return true;
    }

    public void DiscardSelectedStepParameterDraft() =>
        RefreshSelectedStepPropertyDraft("Unapplied changes discarded. Recipe parameters were not changed.");

    private void RefreshSelectedStepPropertyDraft(string? status = null)
    {
        var step = SelectedPipelineStep;
        selectedStepPropertyDraftStepId = step?.Id;
        SelectedStepPropertyDraft = step?.ToolId switch
        {
            "filter" => FilterStepProperties.From(step),
            "height-difference-edge" => HeightDifferenceEdgeStepProperties.From(step),
            "three-d-line-fit" => LineFitStepProperties.From(step),
            _ => null
        };

        SetParameterDraftState(
            false,
            status ?? (SelectedStepPropertyDraft is null
                ? "This step is preserved, but no typed parameter editor is available yet."
                : "Parameters match the committed recipe. Editing does not run Preview or Publish."));
        OnPropertyChanged(nameof(IsSelectedStepPropertyGridSupported));
        OnPropertyChanged(nameof(SelectedStepAdapterStatus));
    }

    private void SetParameterDraftState(bool pending, string status)
    {
        hasPendingStepParameterChanges = pending;
        stepParameterEditStatus = status;
        OnPropertyChanged(nameof(HasPendingStepParameterChanges));
        OnPropertyChanged(nameof(HasUncommittedRecipeChanges));
        OnPropertyChanged(nameof(StepParameterEditStatus));
        applySelectedStepParameterDraftCommand?.RaiseCanExecuteChanged();
        discardSelectedStepParameterDraftCommand?.RaiseCanExecuteChanged();
        RefreshFilterCommands();
        RefreshHeightDifferenceEdgeCommands();
        RefreshLineFitCommands();
    }

    private void SetParameterDraftStatus(string status)
    {
        stepParameterEditStatus = status;
        OnPropertyChanged(nameof(StepParameterEditStatus));
    }

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

    private static bool IsSupportedPropertyGridTool(ToolWorkbenchPipelineStepItem step) =>
        step.ToolId is "filter" or "height-difference-edge" or "three-d-line-fit";

    private static string FormatAdapterStatus(
        ToolWorkbenchPipelineStepItem step,
        IReadOnlySet<string> mappedNames)
    {
        var unmappedCount = step.Parameters.Count(parameter => !mappedNames.Contains(parameter.Name));
        return unmappedCount == 0
            ? "Typed adapter ready"
            : $"Typed adapter ready | {unmappedCount} unmapped preserved";
    }

    internal static string GetParameter(ToolWorkbenchPipelineStepItem step, string name) =>
        step.Parameters.FirstOrDefault(parameter =>
            string.Equals(parameter.Name, name, StringComparison.Ordinal))?.Value ?? string.Empty;

    internal static string GetUnmappedParameters(
        ToolWorkbenchPipelineStepItem step,
        IReadOnlySet<string> mappedNames)
    {
        var values = step.Parameters
            .Where(parameter => !mappedNames.Contains(parameter.Name))
            .Select(parameter => $"{parameter.Name}={parameter.Value}")
            .ToArray();
        return values.Length == 0 ? "(none)" : string.Join("; ", values);
    }
}

public enum FilterMethod
{
    Median
}

public enum FilterMissingValuePolicy
{
    PreserveMask
}

public enum FilterBoundaryPolicy
{
    AvailableNeighbors
}

[CategoryOrder("Filter", 0)]
[CategoryOrder("Compatibility", 1)]
public sealed class FilterStepProperties
{
    internal static readonly HashSet<string> MappedNames =
        ["Method", "KernelSize", "MissingValuePolicy", "BoundaryPolicy"];

    [Category("Filter")]
    [DisplayName("Method")]
    [Description("Filtering method. Recipe v1 supports Median only.")]
    [PropertyOrder(0)]
    public FilterMethod Method { get; set; }

    [Category("Filter")]
    [DisplayName("Kernel size")]
    [Description("Odd square neighborhood size. Supported values are 3, 5, and 7.")]
    [PropertyOrder(1)]
    [NumberRange(3, 7, 2)]
    public int KernelSize { get; set; }

    [Category("Filter")]
    [DisplayName("Missing values")]
    [Description("Keeps missing source cells missing.")]
    [PropertyOrder(2)]
    public FilterMissingValuePolicy MissingValuePolicy { get; set; }

    [Category("Filter")]
    [DisplayName("Boundary")]
    [Description("Uses only valid neighbors available inside the source boundary.")]
    [PropertyOrder(3)]
    public FilterBoundaryPolicy BoundaryPolicy { get; set; }

    [Category("Compatibility")]
    [DisplayName("Unmapped parameters")]
    [Description("Unknown parameters are retained unchanged when known parameters are applied.")]
    [PropertyOrder(10)]
    [ReadOnly(true)]
    public string UnmappedParameters { get; init; } = "(none)";

    internal static FilterStepProperties From(ToolWorkbenchPipelineStepItem step) => new()
    {
        Method = Enum.TryParse<FilterMethod>(ToolWorkbenchViewModel.GetParameter(step, "Method"), out var method) ? method : FilterMethod.Median,
        KernelSize = int.TryParse(ToolWorkbenchViewModel.GetParameter(step, "KernelSize"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var kernel) ? kernel : 0,
        MissingValuePolicy = Enum.TryParse<FilterMissingValuePolicy>(ToolWorkbenchViewModel.GetParameter(step, "MissingValuePolicy"), out var missing) ? missing : FilterMissingValuePolicy.PreserveMask,
        BoundaryPolicy = Enum.TryParse<FilterBoundaryPolicy>(ToolWorkbenchViewModel.GetParameter(step, "BoundaryPolicy"), out var boundary) ? boundary : FilterBoundaryPolicy.AvailableNeighbors,
        UnmappedParameters = ToolWorkbenchViewModel.GetUnmappedParameters(step, MappedNames)
    };

    internal bool TryValidate(out string message)
    {
        if (KernelSize is not (3 or 5 or 7))
        {
            message = "Kernel size must be 3, 5, or 7.";
            return false;
        }

        message = string.Empty;
        return true;
    }
}

public enum HeightDifferenceEdgeComparisonAxis
{
    Unspecified,
    AcrossColumns,
    AcrossRows
}

public enum HeightDifferenceEdgePolarity
{
    Unspecified,
    Rising,
    Falling,
    Absolute
}

public enum HeightDifferenceEdgeCandidatePolicy
{
    StrongestPerScanline
}

public enum HeightDifferenceEdgePointPolicy
{
    PairMidpoint
}

public enum HeightDifferenceEdgeMissingValuePolicy
{
    SkipPair
}

public enum HeightDifferenceEdgeBoundaryPolicy
{
    WithinSelection
}

[CategoryOrder("Edge", 0)]
[CategoryOrder("Policies", 1)]
[CategoryOrder("Compatibility", 2)]
public sealed class HeightDifferenceEdgeStepProperties
{
    internal static readonly HashSet<string> MappedNames =
    [
        "ComparisonAxis", "Polarity", "MinimumDelta", "CandidatePolicy",
        "PointPolicy", "MissingValuePolicy", "BoundaryPolicy"
    ];

    [Category("Edge")]
    [DisplayName("Comparison axis")]
    [Description("Adjacent-height comparison direction in the source grid.")]
    [PropertyOrder(0)]
    public HeightDifferenceEdgeComparisonAxis ComparisonAxis { get; set; }

    [Category("Edge")]
    [DisplayName("Polarity")]
    [Description("Accepted sign of the adjacent raw-height difference.")]
    [PropertyOrder(1)]
    public HeightDifferenceEdgePolarity Polarity { get; set; }

    [Category("Edge")]
    [DisplayName("Minimum delta")]
    [Description("Finite raw-height difference threshold; must be greater than zero.")]
    [PropertyOrder(2)]
    [NumberRange(0, 1000000, 1, 3)]
    public double MinimumDelta { get; set; }

    [Category("Policies")]
    [DisplayName("Candidate")]
    [Description("Selects the strongest accepted pair in each scanline.")]
    [PropertyOrder(3)]
    public HeightDifferenceEdgeCandidatePolicy CandidatePolicy { get; set; }

    [Category("Policies")]
    [DisplayName("Point position")]
    [Description("Places the edge point at the adjacent pair midpoint.")]
    [PropertyOrder(4)]
    public HeightDifferenceEdgePointPolicy PointPolicy { get; set; }

    [Category("Policies")]
    [DisplayName("Missing values")]
    [Description("Skips adjacent pairs containing a missing sample.")]
    [PropertyOrder(5)]
    public HeightDifferenceEdgeMissingValuePolicy MissingValuePolicy { get; set; }

    [Category("Policies")]
    [DisplayName("Boundary")]
    [Description("Searches only within the recipe-owned GridRectangle.")]
    [PropertyOrder(6)]
    public HeightDifferenceEdgeBoundaryPolicy BoundaryPolicy { get; set; }

    [Category("Compatibility")]
    [DisplayName("Unmapped parameters")]
    [Description("Unknown parameters are retained unchanged when known parameters are applied.")]
    [PropertyOrder(10)]
    [ReadOnly(true)]
    public string UnmappedParameters { get; init; } = "(none)";

    internal static HeightDifferenceEdgeStepProperties From(ToolWorkbenchPipelineStepItem step) => new()
    {
        ComparisonAxis = Enum.TryParse<HeightDifferenceEdgeComparisonAxis>(ToolWorkbenchViewModel.GetParameter(step, "ComparisonAxis"), out var axis)
            ? axis
            : HeightDifferenceEdgeComparisonAxis.Unspecified,
        Polarity = Enum.TryParse<HeightDifferenceEdgePolarity>(ToolWorkbenchViewModel.GetParameter(step, "Polarity"), out var polarity)
            ? polarity
            : HeightDifferenceEdgePolarity.Unspecified,
        MinimumDelta = double.TryParse(ToolWorkbenchViewModel.GetParameter(step, "MinimumDelta"), NumberStyles.Float, CultureInfo.InvariantCulture, out var delta)
            ? delta
            : 0,
        CandidatePolicy = Enum.TryParse<HeightDifferenceEdgeCandidatePolicy>(ToolWorkbenchViewModel.GetParameter(step, "CandidatePolicy"), out var candidate)
            ? candidate
            : HeightDifferenceEdgeCandidatePolicy.StrongestPerScanline,
        PointPolicy = Enum.TryParse<HeightDifferenceEdgePointPolicy>(ToolWorkbenchViewModel.GetParameter(step, "PointPolicy"), out var point)
            ? point
            : HeightDifferenceEdgePointPolicy.PairMidpoint,
        MissingValuePolicy = Enum.TryParse<HeightDifferenceEdgeMissingValuePolicy>(ToolWorkbenchViewModel.GetParameter(step, "MissingValuePolicy"), out var missing)
            ? missing
            : HeightDifferenceEdgeMissingValuePolicy.SkipPair,
        BoundaryPolicy = Enum.TryParse<HeightDifferenceEdgeBoundaryPolicy>(ToolWorkbenchViewModel.GetParameter(step, "BoundaryPolicy"), out var boundary)
            ? boundary
            : HeightDifferenceEdgeBoundaryPolicy.WithinSelection,
        UnmappedParameters = ToolWorkbenchViewModel.GetUnmappedParameters(step, MappedNames)
    };

    internal bool TryValidate(out string message)
    {
        if (ComparisonAxis == HeightDifferenceEdgeComparisonAxis.Unspecified)
        {
            message = "Select AcrossColumns or AcrossRows.";
            return false;
        }

        if (Polarity == HeightDifferenceEdgePolarity.Unspecified)
        {
            message = "Select Rising, Falling, or Absolute polarity.";
            return false;
        }

        if (!double.IsFinite(MinimumDelta) || MinimumDelta <= 0)
        {
            message = "Minimum delta must be finite and greater than zero.";
            return false;
        }

        message = string.Empty;
        return true;
    }
}

public enum LineFitMethod
{
    DeterministicConsensusOrthogonalTls
}

public enum LineFitHypothesisPolicy
{
    Sha256PairSchedule
}

public enum LineFitRefinementPolicy
{
    OrthogonalTlsUntilStable10
}

public enum LineFitDirectionPolicy
{
    PositiveScanlineAxis
}

public enum LineFitEndpointPolicy
{
    InlierProjectionExtents
}

[CategoryOrder("Fit rule", 0)]
[CategoryOrder("Fixed v1 policy", 1)]
[CategoryOrder("Compatibility", 2)]
public sealed class LineFitStepProperties
{
    internal static readonly HashSet<string> MappedNames =
    [
        "FitMethod", "MaximumOrthogonalResidual", "MinimumInlierCount", "MinimumInlierRatio", "MinimumInlierScanlineSpan",
        "HypothesisPolicy", "MaximumHypotheses", "RefinementPolicy", "DirectionPolicy", "EndpointPolicy"
    ];

    [Category("Fit rule")]
    [DisplayName("Method")]
    [Description("Deterministic full-XYZ consensus followed by orthogonal TLS.")]
    [PropertyOrder(0)]
    [ReadOnly(true)]
    public LineFitMethod FitMethod { get; set; }

    [Category("Fit rule")]
    [DisplayName("Maximum residual")]
    [Description("Inclusive full-XYZ orthogonal residual in uncalibrated source coordinates.")]
    [PropertyOrder(1)]
    [NumberRange(0, 1000000, 1, 6)]
    public double MaximumOrthogonalResidual { get; set; }

    [Category("Fit rule")]
    [DisplayName("Minimum inliers")]
    [Description("At least three supporting EdgePointSet points are required.")]
    [PropertyOrder(2)]
    [NumberRange(0, 1000000, 1)]
    public int MinimumInlierCount { get; set; }

    [Category("Fit rule")]
    [DisplayName("Minimum ratio")]
    [Description("Required inlier ratio from greater than zero through one.")]
    [PropertyOrder(3)]
    [NumberRange(0, 1, 0.01, 4)]
    public double MinimumInlierRatio { get; set; }

    [Category("Fit rule")]
    [DisplayName("Minimum support span")]
    [Description("Minimum inlier scanline span in source grid-index intervals; at least two.")]
    [PropertyOrder(4)]
    [NumberRange(0, 1000000, 1)]
    public int MinimumInlierScanlineSpan { get; set; }

    [Category("Fixed v1 policy")]
    [DisplayName("Hypotheses")]
    [Description("All pairs through 256 candidates; SHA-256-derived unique pairs above that count.")]
    [PropertyOrder(5)]
    [ReadOnly(true)]
    public LineFitHypothesisPolicy HypothesisPolicy { get; set; }

    [Category("Fixed v1 policy")]
    [DisplayName("Maximum hypotheses")]
    [Description("Fixed deterministic v1 candidate limit.")]
    [PropertyOrder(6)]
    [ReadOnly(true)]
    public int MaximumHypotheses { get; set; } = 256;

    [Category("Fixed v1 policy")]
    [DisplayName("Refinement")]
    [Description("Refit and reclassify until membership is stable, at most ten iterations.")]
    [PropertyOrder(7)]
    [ReadOnly(true)]
    public LineFitRefinementPolicy RefinementPolicy { get; set; }

    [Category("Fixed v1 policy")]
    [DisplayName("Direction")]
    [Description("Canonical positive source scanline axis: +Z AcrossColumns, +X AcrossRows.")]
    [PropertyOrder(8)]
    [ReadOnly(true)]
    public LineFitDirectionPolicy DirectionPolicy { get; set; }

    [Category("Fixed v1 policy")]
    [DisplayName("Segment")]
    [Description("Displays only final inlier projection extents, never an infinite line.")]
    [PropertyOrder(9)]
    [ReadOnly(true)]
    public LineFitEndpointPolicy EndpointPolicy { get; set; }

    [Category("Compatibility")]
    [DisplayName("Unmapped parameters")]
    [Description("Unknown parameters are preserved unchanged when known parameters are applied.")]
    [PropertyOrder(10)]
    [ReadOnly(true)]
    public string UnmappedParameters { get; init; } = "(none)";

    internal static LineFitStepProperties From(ToolWorkbenchPipelineStepItem step) => new()
    {
        FitMethod = LineFitMethod.DeterministicConsensusOrthogonalTls,
        MaximumOrthogonalResidual = double.TryParse(ToolWorkbenchViewModel.GetParameter(step, "MaximumOrthogonalResidual"), NumberStyles.Float, CultureInfo.InvariantCulture, out var residual) ? residual : 0,
        MinimumInlierCount = int.TryParse(ToolWorkbenchViewModel.GetParameter(step, "MinimumInlierCount"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var count) ? count : 0,
        MinimumInlierRatio = double.TryParse(ToolWorkbenchViewModel.GetParameter(step, "MinimumInlierRatio"), NumberStyles.Float, CultureInfo.InvariantCulture, out var ratio) ? ratio : 0,
        MinimumInlierScanlineSpan = int.TryParse(ToolWorkbenchViewModel.GetParameter(step, "MinimumInlierScanlineSpan"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var span) ? span : 0,
        HypothesisPolicy = LineFitHypothesisPolicy.Sha256PairSchedule,
        MaximumHypotheses = 256,
        RefinementPolicy = LineFitRefinementPolicy.OrthogonalTlsUntilStable10,
        DirectionPolicy = LineFitDirectionPolicy.PositiveScanlineAxis,
        EndpointPolicy = LineFitEndpointPolicy.InlierProjectionExtents,
        UnmappedParameters = ToolWorkbenchViewModel.GetUnmappedParameters(step, MappedNames)
    };

    internal bool TryValidate(out string message)
    {
        if (!double.IsFinite(MaximumOrthogonalResidual) || MaximumOrthogonalResidual <= 0)
        {
            message = "Maximum residual must be finite and greater than zero.";
            return false;
        }
        if (MinimumInlierCount < 3)
        {
            message = "Minimum inliers must be at least three.";
            return false;
        }
        if (!double.IsFinite(MinimumInlierRatio) || MinimumInlierRatio <= 0 || MinimumInlierRatio > 1)
        {
            message = "Minimum ratio must be greater than zero and no greater than one.";
            return false;
        }
        if (MinimumInlierScanlineSpan < 2)
        {
            message = "Minimum support span must be at least two grid-index intervals.";
            return false;
        }
        message = string.Empty;
        return true;
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
