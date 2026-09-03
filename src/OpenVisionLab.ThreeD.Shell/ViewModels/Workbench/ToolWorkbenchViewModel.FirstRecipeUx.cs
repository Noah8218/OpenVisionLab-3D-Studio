using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows.Input;
using OpenVisionLab.ThreeD.Core;

namespace OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;

public sealed partial class ToolWorkbenchViewModel
{
    internal const string EmptyFirstRecipeStarterId = ToolWorkbenchFirstRecipeSetupOwner.EmptyStarterId;
    internal const string ThicknessFirstRecipeStarterId = ToolWorkbenchFirstRecipeSetupOwner.ThicknessStarterId;
    internal const string CompatibleVariantStarterId = ToolWorkbenchFirstRecipeSetupOwner.CompatibleVariantStarterId;

    private ToolWorkbenchFirstRecipeSetupOwner firstRecipeSetupOwner = null!;

    public event EventHandler? BrowseFirstRecipeFolderRequested;
    public event EventHandler? BrowseFirstRecipeSourceRequested;

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

    public string FirstRecipeName
    {
        get => firstRecipeSetupOwner.FirstRecipeName;
        set => firstRecipeSetupOwner.FirstRecipeName = value;
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
            return $"{validationState} | {saveState}";
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
        firstRecipeSetupOwner.PropertyChanged += OnFirstRecipeSetupOwnerPropertyChanged;
        firstRecipeSetupOwner.CreateRequested += (_, _) =>
            NewTeachingRecipeRequested?.Invoke(this, EventArgs.Empty);
        firstRecipeSetupOwner.BrowseFirstRecipeFolderRequested += (_, _) =>
            BrowseFirstRecipeFolderRequested?.Invoke(this, EventArgs.Empty);
        firstRecipeSetupOwner.BrowseFirstRecipeSourceRequested += (_, _) =>
            BrowseFirstRecipeSourceRequested?.Invoke(this, EventArgs.Empty);
    }

    public void BeginFirstRecipeSetup() => firstRecipeSetupOwner.BeginFirstRecipeSetup();

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

        if (!firstRecipeSetupOwner.TryValidateCompatibleSourceVariant(
                setup.SourcePath,
                newBinding,
                out message))
        {
            return false;
        }

        SetC3DSourceFromLoadedViewer(setup.SourcePath, newBinding);
        MutateRecipe(() =>
        {
            RecipeName = setup.RecipeName;
            teachingSelectionStoreOwner.RebindAll(newBinding);
            RecipePath = null;
        });
        ClearValidationSet();
        SetValidationSetDefinitionDirty(false);
        teachingSelectionStoreOwner.NotifyAppliedSelectionsChanged();
        var saved = TrySaveTeachingRecipe(setup.RecipePath, out message);
        NotifyFirstRecipeUx();
        return saved;
    }

    private void OnFirstRecipeSetupOwnerPropertyChanged(object? sender, PropertyChangedEventArgs args) =>
        OnPropertyChanged(args.PropertyName);

    private void OnFirstRecipeLanguageChanged(object? sender, EventArgs args) =>
        NotifyFirstRecipeUx();

    private void NotifyFirstRecipeUx()
    {
        OnPropertyChanged(nameof(LocalizedSourceReadinessSummary));
        OnPropertyChanged(nameof(HasRecipeIdentity));
        OnPropertyChanged(nameof(LocalizedRecipePathSummary));
        OnPropertyChanged(nameof(LocalizedRecipeSaveBlocker));
        OnPropertyChanged(nameof(LocalizedRecipeStateSummary));
        firstRecipeSetupOwner?.RefreshPresentation();
    }
}

public sealed record ToolWorkbenchFirstRecipeSetup(
    string RecipeName,
    string FolderPath,
    string SourcePath,
    string StarterId,
    string RecipePath,
    bool IsCompatibleSourceVariant = false);

public sealed record ToolWorkbenchFirstRecipeStarterOption(
    string Id,
    string Name,
    string Detail);
