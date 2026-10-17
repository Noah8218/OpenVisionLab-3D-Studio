using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows.Input;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Data;

namespace OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;

public sealed partial class ToolWorkbenchViewModel
{
    internal const string EmptyFirstRecipeStarterId = ToolWorkbenchFirstRecipeSetupOwner.EmptyStarterId;
    internal const string ThicknessFirstRecipeStarterId = ToolWorkbenchFirstRecipeSetupOwner.ThicknessStarterId;
    internal const string CompatibleVariantStarterId = ToolWorkbenchFirstRecipeSetupOwner.CompatibleVariantStarterId;

    private ToolWorkbenchFirstRecipeSetupOwner firstRecipeSetupOwner = null!;

    public event EventHandler? BrowseFirstRecipeFolderRequested;
    public event EventHandler? BrowseFirstRecipeSourceRequested;
    public event EventHandler? FirstRecipeSetupRequested;

    public ObservableCollection<ToolWorkbenchFirstRecipeStarterOption> FirstRecipeStarterOptions =>
        firstRecipeSetupOwner.FirstRecipeStarterOptions;

    public ICommand CreateFirstRecipeCommand => firstRecipeSetupOwner.CreateFirstRecipeCommand;
    public ICommand BeginCompatibleSourceVariantCommand => firstRecipeSetupOwner.BeginCompatibleSourceVariantCommand;
    public ICommand BrowseFirstRecipeFolderCommand => firstRecipeSetupOwner.BrowseFirstRecipeFolderCommand;
    public ICommand BrowseFirstRecipeSourceCommand => firstRecipeSetupOwner.BrowseFirstRecipeSourceCommand;
    public ICommand ResetFirstRecipeSetupCommand => firstRecipeSetupOwner.ResetFirstRecipeSetupCommand;
    public ICommand CancelFirstRecipeSetupCommand => firstRecipeSetupOwner.CancelFirstRecipeSetupCommand;

    public bool IsFirstRecipeSetupVisible => firstRecipeSetupOwner.IsFirstRecipeSetupVisible;
    public bool IsCompatibleVariantSetup => firstRecipeSetupOwner.IsCompatibleVariantSetup;
    public bool IsVariantRoiReuseConfirmed
    {
        get => firstRecipeSetupOwner.IsVariantRoiReuseConfirmed;
        set => firstRecipeSetupOwner.IsVariantRoiReuseConfirmed = value;
    }

    public string FirstRecipeName
    {
        get => firstRecipeSetupOwner.FirstRecipeName;
        set => firstRecipeSetupOwner.FirstRecipeName = value;
    }

    public string FirstRecipeCategory
    {
        get => firstRecipeSetupOwner.FirstRecipeCategory;
        set => firstRecipeSetupOwner.FirstRecipeCategory = value;
    }

    public string FirstRecipeInspectionTarget
    {
        get => firstRecipeSetupOwner.FirstRecipeInspectionTarget;
        set => firstRecipeSetupOwner.FirstRecipeInspectionTarget = value;
    }

    public string FirstRecipeDescription
    {
        get => firstRecipeSetupOwner.FirstRecipeDescription;
        set => firstRecipeSetupOwner.FirstRecipeDescription = value;
    }

    public string FirstRecipeFolderPath
    {
        get => firstRecipeSetupOwner.FirstRecipeFolderPath;
        set => firstRecipeSetupOwner.FirstRecipeFolderPath = value;
    }

    public string FirstRecipeSourcePath
    {
        get => firstRecipeSetupOwner.FirstRecipeSourcePath;
        set => firstRecipeSetupOwner.FirstRecipeSourcePath = value;
    }

    public ToolWorkbenchFirstRecipeStarterOption? SelectedFirstRecipeStarter
    {
        get => firstRecipeSetupOwner.SelectedFirstRecipeStarter;
        set => firstRecipeSetupOwner.SelectedFirstRecipeStarter = value;
    }

    public bool RememberFirstRecipeSetup
    {
        get => firstRecipeSetupOwner.RememberFirstRecipeSetup;
        set => firstRecipeSetupOwner.RememberFirstRecipeSetup = value;
    }

    public string FirstRecipeStarterDetail => firstRecipeSetupOwner.FirstRecipeStarterDetail;
    public string FirstRecipeSetupTitle => firstRecipeSetupOwner.FirstRecipeSetupTitle;
    public string FirstRecipeSetupDetailText => firstRecipeSetupOwner.FirstRecipeSetupDetailText;
    public string FirstRecipeCreateLabel => firstRecipeSetupOwner.FirstRecipeCreateLabel;
    public bool IsFirstRecipeStarterEditable => firstRecipeSetupOwner.IsFirstRecipeStarterEditable;
    public bool IsFirstRecipeSetupMemoryAvailable => firstRecipeSetupOwner.IsFirstRecipeSetupMemoryAvailable;
    public string FirstRecipeTargetPath => firstRecipeSetupOwner.FirstRecipeTargetPath;
    public bool IsFirstRecipeSetupValid => firstRecipeSetupOwner.IsFirstRecipeSetupValid;
    public string FirstRecipeSetupStatus => firstRecipeSetupOwner.FirstRecipeSetupStatus;

    public bool HasRecipeIdentity => !string.IsNullOrWhiteSpace(RecipePath);

    public string HeaderRecipeText => string.Equals(
        RecipeName,
        ToolWorkbenchRecipeSession.DefaultName,
        StringComparison.Ordinal)
        ? Localization.NewRecipe
        : RecipeName;

    public string LocalizedSourceReadinessSummary => string.IsNullOrWhiteSpace(Source.Path)
        ? Localization.SourceNotSelected
        : !string.Equals(Source.Format, "C3D", StringComparison.OrdinalIgnoreCase)
            ? $"{Localization.SourceUnsupportedFormat} ({Source.Format})"
            : !File.Exists(Source.Path)
                ? Localization.SourceMissing
                : SourceSession.SourceIdentityErrors.Count > 0
                    ? Localization.SourceIdentityMismatch
                    : SourceSession.SourceBinding is null
                        ? Localization.SourceUnreadable
                        : string.Format(
                            Localization.SourceReadyFormat,
                            SourceSession.SourceBinding.GridWidth,
                            SourceSession.SourceBinding.GridHeight);

    public string LocalizedRecipePathSummary => string.IsNullOrWhiteSpace(RecipePath)
        ? Localization.NotSavedYet
        : RecipePath;

    public string SourceContractSummary => !IsSourceReadyForRecipe
        ? Localization.SourceContractUnavailable
        : string.Format(Localization.SourceContractFormat,
            string.IsNullOrWhiteSpace(Source.Unit) ? Localization.SourceContractUnavailable : Source.Unit,
            string.IsNullOrWhiteSpace(Source.FrameId) ? Localization.SourceContractUnavailable : Source.FrameId);

    public string LocalizedRecipeSaveBlocker => Localization.RecipeSaveBlockedCorrections;

    public string LocalizedRecipeStateSummary
    {
        get
        {
            var validationState = SourceSession.SourceIdentityErrors.Count > 0
                ? string.Format(Localization.SourceCorrectionsFormat, SourceSession.SourceIdentityErrors.Count)
                : RecipeSession.SourceBindingErrors.Count > 0
                    ? string.Format(Localization.StaleSelectionsFormat, RecipeSession.SourceBindingErrors.Count)
                    : RecipeSession.Validation.IsValid
                        ? RecipeSession.Validation.Warnings.Count == 0
                            ? Localization.Valid
                            : string.Format(Localization.ValidWarningsFormat, RecipeSession.Validation.Warnings.Count)
                        : RecipeSession.StorageValidation.IsValid
                            ? string.Format(Localization.ExecutionRequirementsFormat, RecipeSession.Validation.Errors.Count)
                            : string.Format(Localization.CorrectionsFormat, RecipeSession.StorageValidation.Errors.Count);
            var saveState = IsDirty || IsValidationSetDefinitionDirty
                ? Localization.Modified
                : string.IsNullOrWhiteSpace(RecipePath)
                    ? Localization.Unsaved
                    : Localization.Saved;
            return $"{Localization.Resolve("ThreeD.Recipe.StatusLabel", "레시피", "Recipe")}: {validationState} | {saveState}";
        }
    }

    private void InitializeFirstRecipeUx()
    {
        firstRecipeSetupOwner = new ToolWorkbenchFirstRecipeSetupOwner(
            recentRecipesPath,
            () => RecipePath,
            () => RecipeName,
            () => Source.Path,
            () => SourceSession.SourceBinding,
            () => Selections,
            () => Source.Id,
            TryReadSourceBinding,
            RefreshSurfaceMatchExperimentState);
    }

    private ToolWorkbenchFirstRecipeEventCoordinator CreateFirstRecipeEventCoordinator() =>
        new(
            firstRecipeSetupOwner,
            OnFirstRecipeSetupOwnerPropertyChanged,
            () => NewTeachingRecipeRequested?.Invoke(this, EventArgs.Empty),
            () => BrowseFirstRecipeFolderRequested?.Invoke(this, EventArgs.Empty),
            () => BrowseFirstRecipeSourceRequested?.Invoke(this, EventArgs.Empty));

    public void BeginFirstRecipeSetup()
    {
        firstRecipeSetupOwner.BeginFirstRecipeSetup();
        FirstRecipeSetupRequested?.Invoke(this, EventArgs.Empty);
    }

    public void BeginCompatibleSourceVariantSetup() =>
        firstRecipeSetupOwner.BeginCompatibleSourceVariantSetup();

    public bool TryGetFirstRecipeSetup(
        out ToolWorkbenchFirstRecipeSetup setup,
        out string message) =>
        firstRecipeSetupOwner.TryGetFirstRecipeSetup(out setup, out message);

    public bool TryApplyFirstRecipeStarter(string starterId, out string message) =>
        firstRecipeSetupOwner.TryApplyFirstRecipeStarter(
            starterId,
            () =>
            {
                var thickness = Tools.First(tool => tool.Id == "thickness");
                return CanAddTool(thickness);
            },
            () =>
            {
                var thickness = Tools.First(tool => tool.Id == "thickness");
                return GetProposedInputRoute(thickness).Detail;
            },
            () =>
            {
                var thickness = Tools.First(tool => tool.Id == "thickness");
                var stepCount = PipelineSteps.Count;
                AddToolToRecipe(thickness, explicitInputIds: null);
                return PipelineSteps.Count == stepCount + 1
                    && SelectedPipelineStep?.ToolId == thickness.Id;
            },
            out message);

    public bool CompleteFirstRecipeSetup(out string message) =>
        firstRecipeSetupOwner.CompleteFirstRecipeSetup(out message);

    public bool TryCreateCompatibleSourceVariant(
        ToolWorkbenchFirstRecipeSetup setup,
        ToolRecipeSelectionSourceBinding newBinding,
        out string message)
    {
        ArgumentNullException.ThrowIfNull(setup);
        ArgumentNullException.ThrowIfNull(newBinding);

        if (IsDisposed || IsRecipeMutationBlocked || HasPendingStepParameterChanges)
        {
            message = HasPendingStepParameterChanges ? Localization.CompatibleVariantPendingDraft : Localization.CompatibleVariantBusy;
            return false;
        }

        // Recheck at the execution boundary; a setup snapshot is not overwrite authority.
        if (File.Exists(setup.RecipePath) || Directory.Exists(setup.RecipePath)
            || string.Equals(Path.GetFullPath(setup.RecipePath), Path.GetFullPath(RecipePath ?? setup.RecipePath), StringComparison.OrdinalIgnoreCase))
        {
            message = Localization.FirstRecipeAlreadyExists;
            return false;
        }

        if (!firstRecipeSetupOwner.TryValidateCompatibleSourceVariant(
                setup.SourcePath,
                newBinding,
                out message))
        {
            return false;
        }

        ToolRecipeDocument copy;
        string fullPath;
        try
        {
            fullPath = Path.GetFullPath(setup.RecipePath);
            var sourcePath = Path.GetFullPath(setup.SourcePath);
            var current = CreateDocument();
            copy = current with
            {
                Name = setup.RecipeName,
                Source = current.Source with
                {
                    Name = Path.GetFileNameWithoutExtension(sourcePath),
                    Path = sourcePath,
                    ByteLength = new FileInfo(sourcePath).Length,
                    ContentSha256 = newBinding.ContentSha256,
                    GridWidth = newBinding.GridWidth,
                    GridHeight = newBinding.GridHeight,
                    AcquisitionProvenance = CreateUnavailableSourceAcquisitionProvenance(),
                    MeasurementEvidence = null,
                    SensorId = null
                },
                Steps = current.Steps.Select(step => step with { SignedDeltaSidecar = null }).ToArray(),
                Selections = current.Selections?.Select(selection => selection with { SourceBinding = newBinding }).ToArray()
            };
            // Commit the new file before changing the editor or publishing any source-change event.
            ToolRecipeDocumentStore.SaveNew(fullPath, copy with
            {
                Source = copy.Source with { Path = PersistSourcePath(fullPath, sourcePath) }
            });
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException or ArgumentException or NotSupportedException)
        {
            message = exception.Message;
            AppendLog("Error", $"Teaching recipe copy failed: {message}");
            return false;
        }

        var selectedStepId = SelectedPipelineStep?.Id;
        SetC3DSourceCore(copy.Source.Path, newBinding, markDirty: false);
        ApplyDocumentCore(copy, markDirty: false, selectedStepId, "Source changed; Preview is required.", captureOpenedSourceIdentity: true);
        ViewerWorkspace.ResetContentPins();
        ClearValidationSet();
        SetValidationSetDefinitionDirty(false);
        RecipePath = fullPath;
        SetDirty(false);
        RecordRecentRecipe(fullPath);
        teachingSelectionStoreOwner.NotifyAppliedSelectionsChanged();
        message = $"Teaching recipe saved: {Path.GetFileName(fullPath)}";
        AppendLog("Teach", message);
        NotifyFirstRecipeUx();
        return true;
    }

    private void OnFirstRecipeSetupOwnerPropertyChanged(PropertyChangedEventArgs args) =>
        OnPropertyChanged(args.PropertyName);

    private void OnFirstRecipeLanguageChanged(object? sender, EventArgs args)
    {
        foreach (var tool in Tools)
        {
            tool.NotifyLocalizedPropertiesChanged();
        }

        RefreshFilteredTools();
        OnPropertyChanged(nameof(SelectedToolTitle));
        NotifyFirstRecipeUx();
        OnPropertyChanged(nameof(RecipePreviewIdentitySummary));
    }

    private void NotifyFirstRecipeUx()
    {
        OnPropertyChanged(nameof(LocalizedSourceReadinessSummary));
        OnPropertyChanged(nameof(SourceContractSummary));
        OnPropertyChanged(nameof(HasRecipeIdentity));
        OnPropertyChanged(nameof(LocalizedRecipePathSummary));
        OnPropertyChanged(nameof(LocalizedRecipeSaveBlocker));
        OnPropertyChanged(nameof(LocalizedRecipeStateSummary));
        OnPropertyChanged(nameof(HeaderRecipeText));
        NotifyHeaderRecipeState();
        firstRecipeSetupOwner?.RefreshPresentation();
    }
}

public sealed record ToolWorkbenchFirstRecipeSetup(
    string RecipeName,
    string FolderPath,
    string SourcePath,
    string StarterId,
    string RecipePath,
    bool IsCompatibleSourceVariant = false,
    string? Category = null,
    string? InspectionTarget = null,
    string? Description = null);

public sealed record ToolWorkbenchFirstRecipeStarterOption(
    string Id,
    string Name,
    string Detail);
