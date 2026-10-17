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
    private RelayCommand undoSelectedStepParametersCommand = null!;
    private readonly Dictionary<string, Stack<(ToolRecipeDocument Before, ToolRecipeDocument After, string Summary)>> appliedStepParameters = new(StringComparer.Ordinal);
    private RelayCommand configureSelectedStepAcceptanceLimitsCommand = null!;
    private RelayCommand clearRecentRecipeSearchCommand = null!;
    private RelayCommand discardSelectedStepParameterDraftCommand = null!;
    private RelayCommand markSelectedStepParameterDraftDirtyCommand = null!;
    private RelayCommand openRecentInspectionRecipeCommand = null!;
    private RelayCommand reportParameterDraftCommitErrorCommand = null!;
    private int stepParameterDraftRejectionVersion;
    private readonly ToolWorkbenchStepPropertySession stepPropertySession = new(ThreeDLocalization.Shared);
    private readonly string recentRecipesPath;
    private ToolWorkbenchRecentRecipeItem? selectedRecentRecipe;
    private ToolRecipeDocument? browsedRecipe;
    private ToolRecipeStep? selectedBrowsedStep;
    private string selectedRecipeBrowserSection = "overview";
    private string recipeSearchText = string.Empty;
    private string recipeCategoryFilter = string.Empty;
    private string recipeStatusFilter = string.Empty;
    private string recipeSortMode = "modified";
    private string recipeBrowseError = string.Empty;
    private RecipePreviewIdentityState recipePreviewIdentityState;
    private int recipePreviewRequestVersion;

    public event EventHandler<ToolWorkbenchRecipePathRequestEventArgs>? OpenRecentTeachingRecipeRequested;

    public ObservableCollection<ToolWorkbenchRecentRecipeItem> RecentRecipes { get; } = [];

    public string RecipeSearchText
    {
        get => recipeSearchText;
        set
        {
            recipeSearchText = value ?? string.Empty;
            OnPropertyChanged();
            OnPropertyChanged(nameof(FilteredRecentRecipes));
            OnPropertyChanged(nameof(HasRecentRecipeSearchText));
            OnPropertyChanged(nameof(HasRecentRecipeFilter));
            clearRecentRecipeSearchCommand?.RaiseCanExecuteChanged();
        }
    }

    public string RecipeCategoryFilter
    {
        get => recipeCategoryFilter;
        set
        {
            var next = value ?? string.Empty;
            if (string.Equals(recipeCategoryFilter, next, StringComparison.Ordinal))
            {
                return;
            }

            recipeCategoryFilter = next;
            OnPropertyChanged();
            OnPropertyChanged(nameof(FilteredRecentRecipes));
            OnPropertyChanged(nameof(HasRecentRecipeFilter));
            clearRecentRecipeSearchCommand?.RaiseCanExecuteChanged();
        }
    }

    public string RecipeStatusFilter
    {
        get => recipeStatusFilter;
        set
        {
            var next = value is "ready" or "draft" or "missing" ? value : string.Empty;
            if (string.Equals(recipeStatusFilter, next, StringComparison.Ordinal))
            {
                return;
            }

            recipeStatusFilter = next;
            OnPropertyChanged();
            OnPropertyChanged(nameof(FilteredRecentRecipes));
            OnPropertyChanged(nameof(HasRecentRecipeFilter));
            clearRecentRecipeSearchCommand?.RaiseCanExecuteChanged();
        }
    }

    public string RecipeSortMode
    {
        get => recipeSortMode;
        set
        {
            var next = string.Equals(value, "name", StringComparison.Ordinal) ? "name" : "modified";
            if (recipeSortMode == next)
            {
                return;
            }

            recipeSortMode = next;
            OnPropertyChanged();
            OnPropertyChanged(nameof(FilteredRecentRecipes));
        }
    }

    public bool HasRecentRecipeSearchText => !string.IsNullOrEmpty(RecipeSearchText);

    public bool HasRecentRecipeFilter => HasRecentRecipeSearchText
        || !string.IsNullOrWhiteSpace(RecipeCategoryFilter)
        || !string.IsNullOrWhiteSpace(RecipeStatusFilter);

    public IReadOnlyList<KeyValuePair<string, string>> RecipeCategoryOptions =>
        [new(string.Empty, Localization.AllRecipeCategories), .. RecentRecipes
            .Select(item => item.Category)
            .Where(category => !string.IsNullOrWhiteSpace(category))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(category => category, StringComparer.OrdinalIgnoreCase)
            .Select(category => new KeyValuePair<string, string>(category, category))];

    private void OnRecipeBrowserLocalizationChanged(object? sender, PropertyChangedEventArgs args) =>
        OnPropertyChanged(nameof(RecipeCategoryOptions));

    public IReadOnlyList<ToolWorkbenchRecentRecipeItem> FilteredRecentRecipes
    {
        get
        {
            var searchText = RecipeSearchText.Trim();
            var filtered = RecentRecipes.Where(item =>
                (searchText.Length == 0 || item.SearchContent.Contains(searchText, StringComparison.OrdinalIgnoreCase))
                && (RecipeCategoryFilter.Length == 0
                    || string.Equals(item.Category, RecipeCategoryFilter, StringComparison.OrdinalIgnoreCase))
                && (RecipeStatusFilter.Length == 0
                    || RecipeStatusFilter switch
                    {
                        "ready" => item.IsAvailable && item.IsInspectionReady,
                        "draft" => item.IsAvailable && !item.IsInspectionReady,
                        "missing" => !item.IsAvailable,
                        _ => true
                    }));
            return RecipeSortMode == "name"
                ? filtered.OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(item => item.Path, StringComparer.OrdinalIgnoreCase).ToArray()
                : filtered.OrderByDescending(item => item.LastModifiedUtc)
                    .ThenBy(item => item.Name, StringComparer.OrdinalIgnoreCase).ToArray();
        }
    }

    public ToolWorkbenchRecentRecipeItem? SelectedRecentRecipe
    {
        get => selectedRecentRecipe;
        set
        {
            selectedRecentRecipe = value;
            browsedRecipe = null;
            recipeBrowseError = string.Empty;
            if (value is not null)
            {
                try
                {
                    browsedRecipe = ResolveRelativeSourcePath(ToolRecipeDocumentStore.Load(value.Path), value.Path);
                }
                catch (Exception exception) when (exception is IOException or UnauthorizedAccessException
                    or ArgumentException or NotSupportedException or System.Text.Json.JsonException)
                {
                    recipeBrowseError = exception.Message;
                }
            }

            SelectedBrowsedStep = null;
            SelectedRecipeBrowserSection = "overview";
            StartBrowsedRecipeInputPreview();
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasSelectedRecentRecipe));
            OnPropertyChanged(nameof(BrowsedRecipe));
            OnPropertyChanged(nameof(RecipeBrowseError));
            OnPropertyChanged(nameof(IsRecipePreviewVisible));
            OnPropertyChanged(nameof(RecipePreviewIdentitySummary));
        }
    }

    public ToolRecipeDocument? BrowsedRecipe => browsedRecipe;

    public bool HasSelectedRecentRecipe => SelectedRecentRecipe is not null;

    internal bool SelectDefaultRecentRecipeIfNeeded()
    {
        if (SelectedRecentRecipe is not null)
        {
            return false;
        }

        var recipe = FilteredRecentRecipes.FirstOrDefault(item => item.IsAvailable);
        if (recipe is null)
        {
            return false;
        }

        SelectedRecentRecipe = recipe;
        return true;
    }

    public string SelectedRecipeBrowserSection
    {
        get => selectedRecipeBrowserSection;
        set
        {
            var next = value is "input" or "tools" or "regions" or "criteria" ? value : "overview";
            if (selectedRecipeBrowserSection == next)
            {
                return;
            }

            selectedRecipeBrowserSection = next;
            OnPropertyChanged();
        }
    }

    public bool IsRecipePreviewVisible => BrowsedRecipe is not null;

    public string RecipeBrowseError => recipeBrowseError;

    public string RecipePreviewIdentitySummary => recipePreviewIdentityState switch
    {
        RecipePreviewIdentityState.LoadingVerified => Localize("레시피 입력과 현재 C3D 파일을 확인하고 있습니다.", "Verifying the recipe input against the current C3D file."),
        RecipePreviewIdentityState.LoadingUnverified => Localize("저장된 입력 식별 정보가 없어 현재 파일을 미리 봅니다.", "Previewing the current file; the recipe has no complete input identity."),
        RecipePreviewIdentityState.Verified => Localize("현재 C3D 파일이 레시피의 저장 입력과 일치합니다.", "The current C3D file matches the recipe's saved input."),
        RecipePreviewIdentityState.Unverified => Localize("미리보기 전용 · 저장 입력과의 동일성은 확인되지 않았습니다.", "Preview only · the saved input identity could not be confirmed."),
        RecipePreviewIdentityState.Missing => Localize("저장된 입력 파일을 찾을 수 없습니다.", "The saved input file is unavailable."),
        RecipePreviewIdentityState.Unsupported => Localize("C3D 입력만 미리 볼 수 있습니다.", "Only C3D inputs can be previewed."),
        RecipePreviewIdentityState.Failed => Localize("입력 미리보기를 사용할 수 없습니다. 뷰어 오류를 확인하세요.", "Input preview is unavailable. Check the Viewer error."),
        _ => Localize("레시피를 선택하면 저장된 입력을 미리 봅니다.", "Select a recipe to preview its saved input.")
    };

    public ToolRecipeStep? SelectedBrowsedStep
    {
        get => selectedBrowsedStep;
        set
        {
            selectedBrowsedStep = value;
            OnPropertyChanged();
        }
    }

    public string? MostRecentAvailableRecipePath =>
        RecentRecipes.FirstOrDefault(recipe => recipe.IsAvailable)?.Path;

    public object? SelectedStepPropertyDraft => stepPropertySession.Draft;

    public bool IsSelectedStepPropertyGridSupported => stepPropertySession.IsSupported;

    public bool HasPendingStepParameterChanges => stepPropertySession.HasPendingChanges;

    public int StepParameterDraftRejectionVersion => stepParameterDraftRejectionVersion;

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

    public ICommand UndoSelectedStepParametersCommand => undoSelectedStepParametersCommand;
    public bool HasSelectedStepParameterHistory => SelectedPipelineStep is { } step && appliedStepParameters.TryGetValue(step.Id, out var history) && history.Count > 0 && CanRestoreAppliedStepChange(step, history.Peek().Before, history.Peek().After);
    public string SelectedStepLastAppliedChange => SelectedPipelineStep is { } step && appliedStepParameters.TryGetValue(step.Id, out var history) && history.Count > 0
        ? history.Peek().Summary : Localize("적용 이력 없음", "No applied changes");
    public ICommand ApplySelectedStepParameterDraftCommand => applySelectedStepParameterDraftCommand;
    public ICommand ConfigureSelectedStepAcceptanceLimitsCommand => configureSelectedStepAcceptanceLimitsCommand;
    public bool IsSelectedStepAcceptanceLimitsUnconfigured => SelectedPipelineStep?.AcceptanceLimitsConfigured == false;

    public ICommand DiscardSelectedStepParameterDraftCommand => discardSelectedStepParameterDraftCommand;

    public ICommand MarkSelectedStepParameterDraftDirtyCommand => markSelectedStepParameterDraftDirtyCommand;

    public ICommand ReportParameterDraftCommitErrorCommand => reportParameterDraftCommitErrorCommand;

    public ICommand OpenRecentTeachingRecipeCommand { get; private set; } = null!;

    public ICommand OpenRecentInspectionRecipeCommand => openRecentInspectionRecipeCommand;

    public ICommand RemoveRecentTeachingRecipeCommand { get; private set; } = null!;

    public ICommand ClearRecentRecipeSearchCommand => clearRecentRecipeSearchCommand;

    public bool HasUncommittedRecipeChanges =>
        IsDirty
        || IsValidationSetDefinitionDirty
        || HasPendingStepParameterChanges;

    public bool IsSourceReadyForRecipe => SourceSession.SourceIdentityErrors.Count == 0
        && SourceSession.SourceBinding is not null
        && File.Exists(Source.Path)
        && string.Equals(Source.Format, "C3D", StringComparison.OrdinalIgnoreCase);

    public string SourceReadinessSummary => string.IsNullOrWhiteSpace(Source.Path)
        ? "Source: not selected"
        : !string.Equals(Source.Format, "C3D", StringComparison.OrdinalIgnoreCase)
            ? $"Source: unsupported format ({Source.Format})"
            : !File.Exists(Source.Path)
                ? "Source: missing - relink required"
                : SourceSession.SourceIdentityErrors.Count > 0
                    ? "Source: identity mismatch - relink required"
                    : SourceSession.SourceBinding is null
                        ? "Source: unreadable"
                        : $"Source: ready | {SourceSession.SourceBinding.GridWidth} x {SourceSession.SourceBinding.GridHeight}";

    public string ViewerSourceSummary => IsSourceReadyForRecipe
        ? $"Source: {Source.Name}"
        : SourceReadinessSummary;

    private void StartBrowsedRecipeInputPreview()
    {
        var requestVersion = unchecked(++recipePreviewRequestVersion);
        RecipePreviewViewer.ClearSource();
        recipePreviewIdentityState = RecipePreviewIdentityState.None;

        if (browsedRecipe is not { } recipe)
        {
            return;
        }

        var source = recipe.Source;
        if (!string.Equals(source.Format, "C3D", StringComparison.OrdinalIgnoreCase))
        {
            recipePreviewIdentityState = RecipePreviewIdentityState.Unsupported;
            return;
        }

        if (string.IsNullOrWhiteSpace(source.Path) || !File.Exists(source.Path))
        {
            recipePreviewIdentityState = RecipePreviewIdentityState.Missing;
            return;
        }

        var hasCompleteIdentity = source.ByteLength.HasValue
            && !string.IsNullOrWhiteSpace(source.ContentSha256)
            && source.GridWidth.HasValue
            && source.GridHeight.HasValue;
        recipePreviewIdentityState = hasCompleteIdentity
            ? RecipePreviewIdentityState.LoadingVerified
            : RecipePreviewIdentityState.LoadingUnverified;
        OnPropertyChanged(nameof(RecipePreviewIdentitySummary));

        RecipePreviewViewer.StartObservedLoad(
            async () =>
            {
                await RecipePreviewViewer.EnsureSourceAsync(
                    source.Path,
                    source.Id,
                    source.Unit,
                    source.FrameId,
                    cancellationToken => Task.Run(
                        () => hasCompleteIdentity
                            ? C3DHeightFieldSnapshot.LoadVerified(
                                source.Path,
                                source.Id,
                                source.Unit,
                                source.FrameId,
                                source.ByteLength!.Value,
                                source.ContentSha256!,
                                source.GridWidth!.Value,
                                source.GridHeight!.Value,
                                cancellationToken)
                            : C3DHeightFieldSnapshot.LoadIdentified(
                                source.Path,
                                source.Id,
                                source.Unit,
                                source.FrameId,
                                cancellationToken),
                        cancellationToken));

                if (requestVersion != recipePreviewRequestVersion || IsDisposed)
                {
                    return;
                }

                recipePreviewIdentityState = RecipePreviewViewer.Frame is null
                    ? RecipePreviewIdentityState.Failed
                    : hasCompleteIdentity
                        ? RecipePreviewIdentityState.Verified
                        : RecipePreviewIdentityState.Unverified;
                OnPropertyChanged(nameof(RecipePreviewIdentitySummary));
            },
            _ =>
            {
                if (requestVersion == recipePreviewRequestVersion && !IsDisposed)
                {
                    recipePreviewIdentityState = RecipePreviewIdentityState.Failed;
                    OnPropertyChanged(nameof(RecipePreviewIdentitySummary));
                }
            });
    }

    private void InitializePropertyGridEditing()
    {
        clearRecentRecipeSearchCommand = new RelayCommand(
            _ =>
            {
                RecipeSearchText = string.Empty;
                RecipeCategoryFilter = string.Empty;
                RecipeStatusFilter = string.Empty;
            },
            _ => HasRecentRecipeFilter);
        configureSelectedStepAcceptanceLimitsCommand = new RelayCommand(_ =>
        {
            if (SelectedPipelineStep is not { } step) return;
            var before = CreateDocument();
            var count = appliedStepParameters.GetValueOrDefault(step.Id)?.Count ?? 0;
            if (!TryApplySelectedStepParameterDraft(out var message)) return;
            if ((appliedStepParameters.GetValueOrDefault(step.Id)?.Count ?? 0) > count) appliedStepParameters[step.Id].Pop();
            step.AcceptanceLimitsConfigured = true;
            RememberAppliedStepChange(step, before, Localize("값만 측정 → 판정 규격 적용", "Measure values only → apply acceptance limits"));
            NotifyStepParameterHistory();
            RefreshSelectedToolWorkspaceProjection();
        }, _ => IsSelectedStepAcceptanceLimitsUnconfigured && CanEditRecipe && IsSelectedStepPropertyGridSupported && !IsSelectedStepPreviewRunning);
        undoSelectedStepParametersCommand = new RelayCommand(_ => UndoSelectedStepParameters(),
            _ => !IsReadOnlySampleView && CanEditFlowConnection && !HasPendingFlowConnection && HasSelectedStepParameterHistory);
        applySelectedStepParameterDraftCommand = new RelayCommand(
            _ => TryApplySelectedStepParameterDraft(out var _),
            _ => IsSelectedStepPropertyGridSupported && HasPendingStepParameterChanges);
        discardSelectedStepParameterDraftCommand = new RelayCommand(
            _ => DiscardSelectedStepParameterDraft(),
            _ => IsSelectedStepPropertyGridSupported && HasPendingStepParameterChanges);
        markSelectedStepParameterDraftDirtyCommand = new RelayCommand(
            _ => MarkSelectedStepParameterDraftDirty(),
            _ => IsSelectedStepPropertyGridSupported);
        reportParameterDraftCommitErrorCommand = new RelayCommand(
            parameter =>
            {
                if (parameter is string message)
                {
                    ReportParameterDraftCommitError(message);
                }
            });
        OpenRecentTeachingRecipeCommand = new RelayCommand(
            parameter =>
            {
                if (parameter is ToolWorkbenchRecentRecipeItem item)
                {
                    OpenRecentTeachingRecipeRequested?.Invoke(this, new ToolWorkbenchRecipePathRequestEventArgs(item.Path));
                }
            }, parameter => parameter is ToolWorkbenchRecentRecipeItem item && item.IsAvailable && File.Exists(item.Path) && CanEditRecipe);
        openRecentInspectionRecipeCommand = new RelayCommand(
            parameter =>
            {
                if (parameter is ToolWorkbenchRecentRecipeItem item)
                {
                    OpenRecentTeachingRecipeRequested?.Invoke(this, new ToolWorkbenchRecipePathRequestEventArgs(item.Path, openForInspection: true));
                }
            }, parameter => parameter is ToolWorkbenchRecentRecipeItem item && item.IsInspectionReady && File.Exists(item.Path) && CanEditRecipe);
        RemoveRecentTeachingRecipeCommand = new RelayCommand(
            parameter =>
            {
                if (parameter is ToolWorkbenchRecentRecipeItem item)
                {
                    RecentRecipes.Remove(item);
                    if (SelectedRecentRecipe == item)
                    {
                        SelectedRecentRecipe = null;
                    }
                    SaveRecentRecipes();
                }
            });
        RecentRecipes.CollectionChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(FilteredRecentRecipes));
            OnPropertyChanged(nameof(RecipeCategoryOptions));
        };
        LoadRecentRecipes();
    }

    private void OnStepPropertySessionChanged(PropertyChangedEventArgs args)
    {
        switch (args.PropertyName)
        {
            case nameof(ToolWorkbenchStepPropertySession.Draft):
                SetStepParameterDraftRejected(false);
                OnPropertyChanged(nameof(SelectedStepPropertyDraft));
                break;
            case nameof(ToolWorkbenchStepPropertySession.IsSupported):
                OnPropertyChanged(nameof(IsSelectedStepPropertyGridSupported));
                break;
            case nameof(ToolWorkbenchStepPropertySession.HasPendingChanges):
                OnPropertyChanged(nameof(HasPendingStepParameterChanges));
                OnPropertyChanged(nameof(HasUncommittedRecipeChanges));
                if (!stepPropertySession.HasPendingChanges)
                {
                    SetStepParameterDraftRejected(false);
                    ResetPreparationPresetAssistant();
                }
                applySelectedStepParameterDraftCommand?.RaiseCanExecuteChanged();
                undoSelectedStepParametersCommand?.RaiseCanExecuteChanged();
                discardSelectedStepParameterDraftCommand?.RaiseCanExecuteChanged();
                RefreshValidationThresholdCorrectionCommands();
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

        SetStepParameterDraftRejected(false);
        stepPropertySession.MarkDirty();
    }

    public void ReportParameterDraftCommitError(string message)
    {
        stepPropertySession.SetStatus(message);
        SetStepParameterDraftRejected(true);
    }

    private void SetStepParameterDraftRejected(bool rejected)
    {
        // A repeated rejection must reveal hidden settings even without another edit.
        var version = rejected
            ? stepParameterDraftRejectionVersion == int.MaxValue ? 1 : stepParameterDraftRejectionVersion + 1
            : 0;
        if (stepParameterDraftRejectionVersion == version) return;
        stepParameterDraftRejectionVersion = version;
        OnPropertyChanged(nameof(StepParameterDraftRejectionVersion));
    }

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
            ReportParameterDraftCommitError(message);
            return false;
        }
        if (!stepPropertySession.TryCreateParameterValues(step, out var values, out message))
        {
            SetStepParameterDraftRejected(true);
            return false;
        }


        var before = CreateDocument();
        var previousParameters = before.Steps.First(item => item.Id == step.Id).Parameters;
        var changes = values.Where(pair => previousParameters.FirstOrDefault(parameter => parameter.Name == pair.Key)?.Value != pair.Value)
            .Select(pair => $"{ParameterChangeLabel(pair.Key)} {FormatParameterChangeValue(previousParameters.FirstOrDefault(parameter => parameter.Name == pair.Key)?.Value ?? Localize("미설정", "Unset"))} → {FormatParameterChangeValue(pair.Value)}").ToArray();
        var changed = ApplyStepParameterValues(step, values);
        if (changed) RememberAppliedStepChange(step, before, string.Join(Environment.NewLine, changes));
        message = changed
            ? "Parameters applied to the recipe. Preview and Publish were not run."
            : "No committed parameter value changed.";
        stepPropertySession.Refresh(step, message);
        NotifyStepParameterHistory();
        NotifyValidationThresholdDraftCommitted(step, changed);
        return true;
    }

    private bool ApplyStepParameterValues(ToolWorkbenchPipelineStepItem step, IReadOnlyDictionary<string, string> values, bool restoreFullSet = false)
    {
        var changed = false;
        if (restoreFullSet || step.ToolId == "re-grid-height-map")
        {
            for (var index = step.Parameters.Count - 1; index >= 0; index--)
            {
                if (!values.ContainsKey(step.Parameters[index].Name))
                {
                    recipePartEventCoordinator.UnsubscribeParameter(step.Parameters[index]);
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
                recipePartEventCoordinator.SubscribeParameter(parameter);
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
            MarkDomainMaskPreviewStaleIfNeeded(step);
            MarkLevelSurfacePreviewStaleIfNeeded(step);
            MarkHeightDifferenceEdgePreviewStaleIfNeeded(step);
            MarkTwoPointLinePreviewStaleIfNeeded(step);
            MarkThreePointPlanePreviewStaleIfNeeded(step);
            MarkDatumPlaneDeviationPreviewStaleIfNeeded(step);
            MarkLineFitPreviewStaleIfNeeded();
            MarkLineIntersectionPreviewStaleIfNeeded();
            MarkLandmarkCorrespondencePreviewStaleIfNeeded();
            MarkAffineSolvePreviewStaleIfNeeded();
            MarkSurfaceMatchExperimentCandidateStaleIfNeeded(step);
            SetDirty(true);
            RefreshRecipeState();
        }

        return changed;
    }

    private string ParameterChangeLabel(string name) => SelectedStepPropertyDraft is { } draft
        ? TypeDescriptor.GetProperties(OpenVisionLab.ThreeD.Shell.PropertyGrid.LocalizedPropertyGridObject.Create(draft))[name]?.DisplayName ?? name : name;

    private static string FormatParameterChangeValue(string value) => double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) && double.IsFinite(number)
        ? number.ToString("0.##", CultureInfo.InvariantCulture) : value;

    private string AppliedSelectionSummary(ToolRecipeSelection selection) => selection.GridRectangle is { } rectangle
        ? string.Format(Localization.SelectedToolGridRectangleFormat, rectangle.Column, rectangle.Row, rectangle.ColumnCount, rectangle.RowCount)
        : selection.GridCircle is { } circle
            ? Localize($"중심 열 {circle.CenterColumn}, 행 {circle.CenterRow}, 반지름 {circle.Radius:0.##} 셀", $"Centre column {circle.CenterColumn}, row {circle.CenterRow}, radius {circle.Radius:0.##} cells")
            : selection.GridPolygon is { } polygon
                ? Localize("다각형 꼭짓점 (열, 행): ", "Polygon vertices (column, row): ") + string.Join("; ", polygon.Vertices.Select(vertex => $"({vertex.Column:0.##}, {vertex.Row:0.##})"))
                : selection.Points is { } points
                    ? Localize("선택 점 (열, 행): ", "Selected points (column, row): ") + string.Join("; ", points.Select(point => $"({point.Locator.Column}, {point.Locator.Row})"))
                    : selection.Name;

    private void RememberAppliedStepChange(ToolWorkbenchPipelineStepItem step, ToolRecipeDocument before, string summary)
    {
        if (!appliedStepParameters.TryGetValue(step.Id, out var history)) appliedStepParameters.Add(step.Id, history = new());
        history.Push((before, CreateDocument(), summary));
        NotifyStepParameterHistory();
    }

    private static IEnumerable<string> ChangedSelectionIds(ToolRecipeDocument before, ToolRecipeDocument after) =>
        (before.Selections ?? []).Select(selection => selection.Id).Union((after.Selections ?? []).Select(selection => selection.Id), StringComparer.OrdinalIgnoreCase)
            .Where(id => !Equals(before.Selections?.FirstOrDefault(selection => selection.Id.Equals(id, StringComparison.OrdinalIgnoreCase)),
                after.Selections?.FirstOrDefault(selection => selection.Id.Equals(id, StringComparison.OrdinalIgnoreCase))));

    private static bool AppliedRouteChanged(ToolRecipeStep before, ToolRecipeStep after) =>
        !before.InputEntityIds.SequenceEqual(after.InputEntityIds) || before.DualRoiRouting != after.DualRoiRouting;

    private bool CanRestoreAppliedStepChange(ToolWorkbenchPipelineStepItem step, ToolRecipeDocument before, ToolRecipeDocument after)
    {
        // A session undo must never transplant ROI or links into another input or overwrite a later shared edit.
        if (before.Source.Id != Source.Id || before.Source.ContentSha256 != SourceSession.SourceBinding?.ContentSha256
            || before.Source.Unit != Source.Unit || before.Source.FrameId != Source.FrameId) return false;
        var expected = after.Steps.FirstOrDefault(item => item.Id == step.Id);
        if (expected is null || step.AcceptanceLimitsConfigured != expected.AcceptanceLimitsConfigured
            || !step.Parameters.Select(parameter => new ToolRecipeParameter(parameter.Name, parameter.Value)).SequenceEqual(expected.Parameters)) return false;
        foreach (var previous in before.Steps)
        {
            var next = after.Steps.FirstOrDefault(item => item.Id == previous.Id);
            if (next is null || !AppliedRouteChanged(previous, next)) continue;
            var current = PipelineSteps.FirstOrDefault(item => item.Id == previous.Id);
            if (current is null || !current.InputEntityIds.SequenceEqual(next.InputEntityIds) || current.DualRoiRouting != next.DualRoiRouting) return false;
        }
        return ChangedSelectionIds(before, after).All(id => Equals(teachingSelectionStoreOwner.Find(id),
            after.Selections?.FirstOrDefault(selection => selection.Id.Equals(id, StringComparison.OrdinalIgnoreCase))));
    }

    private void UndoSelectedStepParameters()
    {
        if (SelectedPipelineStep is not { } step || !HasSelectedStepParameterHistory || !CanEditFlowConnection || HasPendingFlowConnection || IsReadOnlySampleView) return;
        var change = appliedStepParameters[step.Id].Pop();
        var previousStep = change.Before.Steps.First(item => item.Id == step.Id);
        var changed = ApplyStepParameterValues(step, previousStep.Parameters.ToDictionary(parameter => parameter.Name, parameter => parameter.Value), restoreFullSet: true);
        if (step.AcceptanceLimitsConfigured != previousStep.AcceptanceLimitsConfigured) { step.AcceptanceLimitsConfigured = previousStep.AcceptanceLimitsConfigured; SetDirty(true); changed = true; }
        var selectionIds = ChangedSelectionIds(change.Before, change.After).ToArray();
        var routes = change.Before.Steps.Where(previous => change.After.Steps.FirstOrDefault(item => item.Id == previous.Id) is { } next && AppliedRouteChanged(previous, next)).ToArray();
        if (selectionIds.Length > 0 || routes.Length > 0)
        {
            MutateRecipe(() =>
            {
                foreach (var id in selectionIds)
                {
                    if (change.Before.Selections?.FirstOrDefault(selection => selection.Id.Equals(id, StringComparison.OrdinalIgnoreCase)) is { } previous)
                        teachingSelectionStoreOwner.Upsert(previous);
                    else if (teachingSelectionStoreOwner.Find(id) is { } added) teachingSelectionStoreOwner.Remove(added);
                }
                foreach (var route in routes)
                {
                    var target = PipelineSteps.First(item => item.Id == route.Id);
                    target.InputEntityIdsText = string.Join("; ", route.InputEntityIds);
                    target.DualRoiRouting = route.DualRoiRouting;
                }
            });
            MarkHeightDifferenceEdgePreviewStaleIfNeeded();
            MarkMeasurementPreviewStaleIfNeeded();
            RefreshTeachingSelectionContext();
            teachingSelectionStoreOwner.NotifyAppliedSelectionsChanged();
            changed = true;
        }
        stepPropertySession.Refresh(step, Localize("직전 적용값 복원 · 다시 실행 필요", "Previous applied values restored · Run again"));
        NotifyValidationThresholdDraftCommitted(step, changed);
        RefreshSelectedToolWorkspaceProjection();
        NotifyStepParameterHistory();
    }

    private void NotifyStepParameterHistory()
    {
        OnPropertyChanged(nameof(HasSelectedStepParameterHistory));
        OnPropertyChanged(nameof(SelectedStepLastAppliedChange));
        undoSelectedStepParametersCommand?.RaiseCanExecuteChanged();
    }

    private (bool Success, string Message) TryApplyFilterKernelPresetDraftForAssistant(
        ToolWorkbenchPipelineStepItem step,
        int kernelSize)
    {
        var success = stepPropertySession.TryApplyFilterKernelPresetDraft(
            step,
            kernelSize,
            out var message);
        if (success) SetStepParameterDraftRejected(false);
        return (success, message);
    }

    public void DiscardSelectedStepParameterDraft()
    {
        var stepId = SelectedPipelineStep?.Id;
        RefreshSelectedStepPropertyDraft(
            "Unapplied changes discarded. Recipe parameters were not changed.");
        NotifyValidationThresholdDraftDiscarded(stepId);
    }

    private void RefreshSelectedStepPropertyDraft(string? status = null)
    {
        ResetPreparationPresetAssistant();
        stepPropertySession.Refresh(SelectedPipelineStep, status);
        NotifyStepParameterHistory();
    }

    private void SetParameterDraftStatus(string status) =>
        stepPropertySession.SetStatus(status);

    private void RefreshAdapterCoverage()
    {
        OnPropertyChanged(nameof(SupportedStepCount));
        OnPropertyChanged(nameof(UnsupportedStepCount));
        OnPropertyChanged(nameof(RecipeAdapterCoverageSummary));
        OnPropertyChanged(nameof(SelectedStepAdapterStatus));
    }

    private void CaptureOpenedSourceIdentity(ToolRecipeSource source) => SourceSession.CaptureOpenedSourceIdentity(source);

    private void AcceptCurrentSourceIdentity() => SourceSession.AcceptCurrentSourceIdentity();

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
        else if (SourceSession.SourceBinding is null)
        {
            errors.Add("The recipe source exists but its C3D identity could not be read.");
        }
        else if (SourceSession.OpenedSourceIdentity is { } expected)
        {
            var actualLength = new FileInfo(Source.Path).Length;
            if (expected.ByteLength is { } expectedLength && expectedLength != actualLength)
            {
                errors.Add($"Source byte length mismatch: recipe {expectedLength}, actual {actualLength}.");
            }

            if (!string.IsNullOrWhiteSpace(expected.ContentSha256)
                && !string.Equals(expected.ContentSha256, SourceSession.SourceBinding.ContentSha256, StringComparison.OrdinalIgnoreCase))
            {
                errors.Add("Source SHA-256 does not match the recipe identity.");
            }

            if (expected.GridWidth is { } width && width != SourceSession.SourceBinding.GridWidth
                || expected.GridHeight is { } height && height != SourceSession.SourceBinding.GridHeight)
            {
                errors.Add($"Source grid mismatch: recipe {expected.GridWidth} x {expected.GridHeight}, actual {SourceSession.SourceBinding.GridWidth} x {SourceSession.SourceBinding.GridHeight}.");
            }
        }

        SourceSession.SetSourceIdentityErrors(errors);
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
        while (RecentRecipes.Count > RecipeRecentFileStore.MaximumRecipeEntries)
        {
            RecentRecipes.RemoveAt(RecentRecipes.Count - 1);
        }

        SaveRecentRecipes();
    }

    private void LoadRecentRecipes()
    {
        try
        {
            foreach (var path in RecipeRecentFileStore.Load(recentRecipesPath, RecipeRecentFileStore.MaximumRecipeEntries))
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
            RecipeRecentFileStore.Save(recentRecipesPath, RecentRecipes.Select(item => item.Path), RecipeRecentFileStore.MaximumRecipeEntries);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            AppendLog("Warning", $"Recent recipe list could not be saved: {exception.Message}");
        }
    }

    private enum RecipePreviewIdentityState
    {
        None,
        LoadingVerified,
        LoadingUnverified,
        Verified,
        Unverified,
        Missing,
        Unsupported,
        Failed
    }

}


public sealed class ToolWorkbenchRecipePathRequestEventArgs(string path, bool openForInspection = false) : EventArgs
{
    public string Path { get; } = path;
    public bool OpenForInspection { get; } = openForInspection;
}

public sealed record ToolWorkbenchRecentRecipeItem(string Path, string Name, bool IsAvailable)
{
    public string Category { get; init; } = string.Empty;

    public string InspectionTarget { get; init; } = string.Empty;

    public string Description { get; init; } = string.Empty;

    public bool IsInspectionReady { get; init; }

    public string SearchContent { get; init; } = string.Empty;

    public bool HasCategory => !string.IsNullOrWhiteSpace(Category);

    public bool HasInspectionTarget => !string.IsNullOrWhiteSpace(InspectionTarget);

    public string Availability => IsAvailable ? "Available" : "Unavailable";

    public DateTime LastModifiedUtc
    {
        get
        {
            try
            {
                return File.Exists(Path) ? File.GetLastWriteTimeUtc(Path) : DateTime.MinValue;
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException
                or ArgumentException or NotSupportedException)
            {
                return DateTime.MinValue;
            }
        }
    }

    public bool HasModifiedDate => LastModifiedUtc != DateTime.MinValue;

    public static ToolWorkbenchRecentRecipeItem From(string path)
    {
        var fileName = System.IO.Path.GetFileNameWithoutExtension(path);
        if (!File.Exists(path))
        {
            return new ToolWorkbenchRecentRecipeItem(path, fileName, false)
            {
                SearchContent = BuildSearchContent(path, fileName, null)
            };
        }

        try
        {
            var document = ToolRecipeDocumentStore.Load(path);
            return new ToolWorkbenchRecentRecipeItem(
                path,
                string.IsNullOrWhiteSpace(document.Name) ? fileName : document.Name,
                true)
            {
                Category = document.Category ?? string.Empty,
                InspectionTarget = document.InspectionTarget ?? string.Empty,
                Description = document.Description ?? string.Empty,
                IsInspectionReady = ToolRecipeValidator.Validate(document).IsValid,
                SearchContent = BuildSearchContent(path, fileName, document)
            };
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException
            or ArgumentException or NotSupportedException or System.Text.Json.JsonException or InvalidDataException)
        {
            return new ToolWorkbenchRecentRecipeItem(path, fileName, true)
            {
                SearchContent = BuildSearchContent(path, fileName, null)
            };
        }
    }

    private static string BuildSearchContent(string path, string fileName, ToolRecipeDocument? document)
    {
        IEnumerable<string?> Terms()
        {
            yield return fileName;
            yield return path;
            if (document is null)
            {
                yield break;
            }

            yield return document.Name;
            yield return document.Category;
            yield return document.InspectionTarget;
            yield return document.Description;
            yield return document.Source.Path;
            foreach (var reference in document.References ?? [])
            {
                yield return reference.Id;
                yield return reference.Name;
                yield return reference.Kind;
            }

            foreach (var step in document.Steps ?? [])
            {
                yield return step.Id;
                yield return step.ToolId;
                yield return step.ToolName;
                yield return step.OutputEntityId;
                foreach (var inputEntityId in step.InputEntityIds ?? [])
                {
                    yield return inputEntityId;
                }

                foreach (var parameter in step.Parameters ?? [])
                {
                    yield return parameter.Name;
                    yield return parameter.Value;
                }
            }
        }

        return string.Join(' ', Terms().Where(term => !string.IsNullOrWhiteSpace(term)));
    }
}
