using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Windows.Input;
using OpenVisionLab;
using OpenVisionLab.ThreeD.Shell;
using OpenVisionLab.ThreeD.Viewer;

namespace OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;

public sealed partial class ToolWorkbenchViewModel
{
    internal const string EmptyFirstRecipeStarterId = "none";
    internal const string ThicknessFirstRecipeStarterId = "thickness";

    private bool isFirstRecipeSetupVisible;
    private string firstRecipeName = "new-inspection";
    private string firstRecipeFolderPath = string.Empty;
    private string firstRecipeSourcePath = string.Empty;
    private bool rememberFirstRecipeSetup;
    private ToolWorkbenchFirstRecipeStarterOption? selectedFirstRecipeStarter;
    private FirstRecipeSetupPreference? rememberedFirstRecipeSetup;
    private string firstRecipeSetupPath = string.Empty;
    private RelayCommand createFirstRecipeCommand = null!;

    public event EventHandler? BrowseFirstRecipeFolderRequested;
    public event EventHandler? BrowseFirstRecipeSourceRequested;

    public ObservableCollection<ToolWorkbenchFirstRecipeStarterOption> FirstRecipeStarterOptions { get; } = [];

    public ICommand CreateFirstRecipeCommand => createFirstRecipeCommand;
    public ICommand BrowseFirstRecipeFolderCommand { get; private set; } = null!;
    public ICommand BrowseFirstRecipeSourceCommand { get; private set; } = null!;
    public ICommand ResetFirstRecipeSetupCommand { get; private set; } = null!;
    public ICommand CancelFirstRecipeSetupCommand { get; private set; } = null!;

    public bool IsFirstRecipeSetupVisible
    {
        get => isFirstRecipeSetupVisible;
        private set => SetField(ref isFirstRecipeSetupVisible, value);
    }

    public string FirstRecipeName
    {
        get => firstRecipeName;
        set
        {
            if (SetField(ref firstRecipeName, value ?? string.Empty))
            {
                NotifyFirstRecipeSetupDraftChanged();
            }
        }
    }

    public string FirstRecipeFolderPath
    {
        get => firstRecipeFolderPath;
        set
        {
            if (SetField(ref firstRecipeFolderPath, value ?? string.Empty))
            {
                NotifyFirstRecipeSetupDraftChanged();
            }
        }
    }

    public string FirstRecipeSourcePath
    {
        get => firstRecipeSourcePath;
        set
        {
            if (SetField(ref firstRecipeSourcePath, value ?? string.Empty))
            {
                NotifyFirstRecipeSetupDraftChanged();
            }
        }
    }

    public ToolWorkbenchFirstRecipeStarterOption? SelectedFirstRecipeStarter
    {
        get => selectedFirstRecipeStarter;
        set
        {
            if (SetField(ref selectedFirstRecipeStarter, value))
            {
                OnPropertyChanged(nameof(FirstRecipeStarterDetail));
                NotifyFirstRecipeSetupDraftChanged();
            }
        }
    }

    public bool RememberFirstRecipeSetup
    {
        get => rememberFirstRecipeSetup;
        set => SetField(ref rememberFirstRecipeSetup, value);
    }

    public string FirstRecipeStarterDetail =>
        SelectedFirstRecipeStarter?.Detail ?? Localization.FirstRecipeNoStarterDetail;

    public string FirstRecipeTargetPath => TryBuildFirstRecipeTargetPath(out var path)
        ? path
        : Localization.FirstRecipeTargetUnavailable;

    public bool IsFirstRecipeSetupValid => GetFirstRecipeSetupValidationError() is null;

    public string FirstRecipeSetupStatus =>
        GetFirstRecipeSetupValidationError() ?? Localization.FirstRecipeReadyToCreate;

    public bool HasRecipeIdentity => !string.IsNullOrWhiteSpace(RecipePath);

    public string LocalizedSourceReadinessSummary => string.IsNullOrWhiteSpace(Source.Path)
        ? Localization.SourceNotSelected
        : !string.Equals(Source.Format, "C3D", StringComparison.OrdinalIgnoreCase)
            ? $"{Localization.SourceUnsupportedFormat} ({Source.Format})"
            : !File.Exists(Source.Path)
                ? Localization.SourceMissing
                : sourceIdentityErrors.Count > 0
                    ? Localization.SourceIdentityMismatch
                    : loadedSourceBinding is null
                        ? Localization.SourceUnreadable
                        : string.Format(
                            Localization.SourceReadyFormat,
                            loadedSourceBinding.GridWidth,
                            loadedSourceBinding.GridHeight);

    public string LocalizedRecipePathSummary => string.IsNullOrWhiteSpace(RecipePath)
        ? Localization.NotSavedYet
        : RecipePath;

    public string LocalizedRecipeSaveBlocker => Localization.RecipeSaveBlockedCorrections;

    public string LocalizedRecipeStateSummary
    {
        get
        {
            var validationState = sourceIdentityErrors.Count > 0
                ? string.Format(Localization.SourceCorrectionsFormat, sourceIdentityErrors.Count)
                : sourceBindingErrors.Count > 0
                    ? string.Format(Localization.StaleSelectionsFormat, sourceBindingErrors.Count)
                    : validation.IsValid
                        ? validation.Warnings.Count == 0
                            ? Localization.Valid
                            : string.Format(Localization.ValidWarningsFormat, validation.Warnings.Count)
                        : storageValidation.IsValid
                            ? string.Format(Localization.ExecutionRequirementsFormat, validation.Errors.Count)
                            : string.Format(Localization.CorrectionsFormat, storageValidation.Errors.Count);
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
        firstRecipeSetupPath = Path.Combine(
            Path.GetDirectoryName(Path.GetFullPath(recentRecipesPath))!,
            "first-recipe-setup.json");
        rememberedFirstRecipeSetup = LoadFirstRecipeSetupPreference();
        RefreshFirstRecipeStarterOptions(EmptyFirstRecipeStarterId);
        createFirstRecipeCommand = new RelayCommand(
            _ => NewTeachingRecipeRequested?.Invoke(this, EventArgs.Empty),
            _ => IsFirstRecipeSetupValid);
        BrowseFirstRecipeFolderCommand = new RelayCommand(
            _ => BrowseFirstRecipeFolderRequested?.Invoke(this, EventArgs.Empty));
        BrowseFirstRecipeSourceCommand = new RelayCommand(
            _ => BrowseFirstRecipeSourceRequested?.Invoke(this, EventArgs.Empty));
        ResetFirstRecipeSetupCommand = new RelayCommand(_ => ResetFirstRecipeSetup());
        CancelFirstRecipeSetupCommand = new RelayCommand(_ => IsFirstRecipeSetupVisible = false);
        OpenVisionLanguageService.LanguageChanged += OnFirstRecipeLanguageChanged;
    }

    public void BeginFirstRecipeSetup()
    {
        var setup = rememberedFirstRecipeSetup;
        FirstRecipeName = setup?.RecipeName ?? "new-inspection";
        FirstRecipeFolderPath = setup?.FolderPath ?? string.Empty;
        FirstRecipeSourcePath = setup?.SourcePath ?? string.Empty;
        RememberFirstRecipeSetup = setup is not null;
        RefreshFirstRecipeStarterOptions(setup?.StarterId ?? EmptyFirstRecipeStarterId);
        IsFirstRecipeSetupVisible = true;
        NotifyFirstRecipeSetupDraftChanged();
    }

    public bool TryGetFirstRecipeSetup(
        out ToolWorkbenchFirstRecipeSetup setup,
        out string message)
    {
        var error = GetFirstRecipeSetupValidationError();
        if (error is not null || !TryBuildFirstRecipeTargetPath(out var targetPath))
        {
            setup = default!;
            message = error ?? Localization.FirstRecipeTargetUnavailable;
            return false;
        }

        setup = new ToolWorkbenchFirstRecipeSetup(
            FirstRecipeName.Trim(),
            Path.GetFullPath(FirstRecipeFolderPath.Trim()),
            Path.GetFullPath(FirstRecipeSourcePath.Trim()),
            SelectedFirstRecipeStarter?.Id ?? EmptyFirstRecipeStarterId,
            targetPath);
        message = Localization.FirstRecipeReadyToCreate;
        return true;
    }

    public bool TryApplyFirstRecipeStarter(string starterId, out string message)
    {
        if (string.Equals(starterId, EmptyFirstRecipeStarterId, StringComparison.Ordinal))
        {
            message = Localization.FirstRecipeNoStarterDetail;
            return true;
        }

        if (!string.Equals(starterId, ThicknessFirstRecipeStarterId, StringComparison.Ordinal))
        {
            message = Localization.FirstRecipeStarterUnavailable;
            return false;
        }

        var thickness = Tools.First(tool => tool.Id == "thickness");
        if (!CanAddTool(thickness))
        {
            message = GetProposedInputRoute(thickness).Detail;
            return false;
        }

        var stepCount = PipelineSteps.Count;
        AddToolToRecipe(thickness, explicitInputIds: null);
        var added = PipelineSteps.Count == stepCount + 1
            && SelectedPipelineStep?.ToolId == thickness.Id;
        message = added
            ? Localization.FirstRecipeThicknessStarterApplied
            : Localization.FirstRecipeStarterUnavailable;
        return added;
    }

    public bool CompleteFirstRecipeSetup(out string message)
    {
        var setup = new FirstRecipeSetupPreference(
            1,
            FirstRecipeName.Trim(),
            Path.GetFullPath(FirstRecipeFolderPath.Trim()),
            Path.GetFullPath(FirstRecipeSourcePath.Trim()),
            SelectedFirstRecipeStarter?.Id ?? EmptyFirstRecipeStarterId);
        var persisted = RememberFirstRecipeSetup
            ? TrySaveFirstRecipeSetupPreference(setup, out message)
            : TryDeleteFirstRecipeSetupPreference(out message);
        rememberedFirstRecipeSetup = RememberFirstRecipeSetup && persisted ? setup : null;
        IsFirstRecipeSetupVisible = false;
        return persisted;
    }

    private void ResetFirstRecipeSetup()
    {
        _ = TryDeleteFirstRecipeSetupPreference(out _);
        rememberedFirstRecipeSetup = null;
        FirstRecipeName = "new-inspection";
        FirstRecipeFolderPath = string.Empty;
        FirstRecipeSourcePath = string.Empty;
        RememberFirstRecipeSetup = false;
        RefreshFirstRecipeStarterOptions(EmptyFirstRecipeStarterId);
        NotifyFirstRecipeSetupDraftChanged();
    }

    private void OnFirstRecipeLanguageChanged(object? sender, EventArgs args)
    {
        var starterId = SelectedFirstRecipeStarter?.Id ?? EmptyFirstRecipeStarterId;
        RefreshFirstRecipeStarterOptions(starterId);
        NotifyFirstRecipeUx();
        NotifyFirstRecipeSetupDraftChanged();
        RefreshSurfaceMatchExperimentState();
    }

    private void RefreshFirstRecipeStarterOptions(string selectedId)
    {
        FirstRecipeStarterOptions.Clear();
        FirstRecipeStarterOptions.Add(new ToolWorkbenchFirstRecipeStarterOption(
            EmptyFirstRecipeStarterId,
            Localization.FirstRecipeNoStarter,
            Localization.FirstRecipeNoStarterDetail));
        FirstRecipeStarterOptions.Add(new ToolWorkbenchFirstRecipeStarterOption(
            ThicknessFirstRecipeStarterId,
            Localization.FirstRecipeThicknessStarter,
            Localization.FirstRecipeThicknessStarterDetail));
        SelectedFirstRecipeStarter = FirstRecipeStarterOptions.FirstOrDefault(option =>
            string.Equals(option.Id, selectedId, StringComparison.Ordinal))
            ?? FirstRecipeStarterOptions[0];
    }

    private string? GetFirstRecipeSetupValidationError()
    {
        var name = FirstRecipeName.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            return Localization.FirstRecipeNameRequired;
        }
        if (name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0
            || name.EndsWith('.')
            || name.EndsWith(' '))
        {
            return Localization.FirstRecipeNameInvalid;
        }
        if (string.IsNullOrWhiteSpace(FirstRecipeFolderPath))
        {
            return Localization.FirstRecipeFolderRequired;
        }
        if (!Directory.Exists(FirstRecipeFolderPath.Trim()))
        {
            return Localization.FirstRecipeFolderMissing;
        }
        if (string.IsNullOrWhiteSpace(FirstRecipeSourcePath))
        {
            return Localization.FirstRecipeSourceRequired;
        }
        if (!File.Exists(FirstRecipeSourcePath.Trim()))
        {
            return Localization.FirstRecipeSourceMissing;
        }
        if (!string.Equals(Path.GetExtension(FirstRecipeSourcePath.Trim()), ".C3D", StringComparison.OrdinalIgnoreCase))
        {
            return Localization.FirstRecipeSourceMustBeC3D;
        }
        if (SelectedFirstRecipeStarter is null)
        {
            return Localization.FirstRecipeStarterRequired;
        }
        if (TryBuildFirstRecipeTargetPath(out var targetPath) && File.Exists(targetPath))
        {
            return Localization.FirstRecipeAlreadyExists;
        }
        return null;
    }

    private bool TryBuildFirstRecipeTargetPath(out string path)
    {
        path = string.Empty;
        if (string.IsNullOrWhiteSpace(FirstRecipeName)
            || string.IsNullOrWhiteSpace(FirstRecipeFolderPath))
        {
            return false;
        }

        try
        {
            path = Path.Combine(
                Path.GetFullPath(FirstRecipeFolderPath.Trim()),
                $"{FirstRecipeName.Trim()}.ov3d-recipe.json");
            return true;
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return false;
        }
    }

    private FirstRecipeSetupPreference? LoadFirstRecipeSetupPreference()
    {
        try
        {
            if (!File.Exists(firstRecipeSetupPath))
            {
                return null;
            }

            var setup = JsonSerializer.Deserialize<FirstRecipeSetupPreference>(
                File.ReadAllBytes(firstRecipeSetupPath));
            return setup is { Version: 1 } ? setup : null;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException or ArgumentException or NotSupportedException)
        {
            return null;
        }
    }

    private bool TrySaveFirstRecipeSetupPreference(
        FirstRecipeSetupPreference setup,
        out string message)
    {
        var temporaryPath = $"{firstRecipeSetupPath}.tmp.{Guid.NewGuid():N}";
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(firstRecipeSetupPath)!);
            var bytes = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetBytes(
                JsonSerializer.Serialize(setup, new JsonSerializerOptions { WriteIndented = true }));
            using (var stream = new FileStream(
                       temporaryPath,
                       FileMode.CreateNew,
                       FileAccess.Write,
                       FileShare.None,
                       4096,
                       FileOptions.WriteThrough))
            {
                stream.Write(bytes);
                stream.Flush(flushToDisk: true);
            }
            File.Move(temporaryPath, firstRecipeSetupPath, overwrite: true);
            message = Localization.FirstRecipeSetupRemembered;
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            message = exception.Message;
            return false;
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    private bool TryDeleteFirstRecipeSetupPreference(out string message)
    {
        try
        {
            if (File.Exists(firstRecipeSetupPath))
            {
                File.Delete(firstRecipeSetupPath);
            }
            message = Localization.FirstRecipeSetupNotRemembered;
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            message = exception.Message;
            return false;
        }
    }

    private void NotifyFirstRecipeSetupDraftChanged()
    {
        OnPropertyChanged(nameof(FirstRecipeTargetPath));
        OnPropertyChanged(nameof(IsFirstRecipeSetupValid));
        OnPropertyChanged(nameof(FirstRecipeSetupStatus));
        createFirstRecipeCommand?.RaiseCanExecuteChanged();
    }

    private void NotifyFirstRecipeUx()
    {
        OnPropertyChanged(nameof(LocalizedSourceReadinessSummary));
        OnPropertyChanged(nameof(HasRecipeIdentity));
        OnPropertyChanged(nameof(LocalizedRecipePathSummary));
        OnPropertyChanged(nameof(LocalizedRecipeSaveBlocker));
        OnPropertyChanged(nameof(LocalizedRecipeStateSummary));
        OnPropertyChanged(nameof(FirstRecipeStarterDetail));
    }

    private sealed record FirstRecipeSetupPreference(
        int Version,
        string RecipeName,
        string FolderPath,
        string SourcePath,
        string StarterId);
}

public sealed record ToolWorkbenchFirstRecipeSetup(
    string RecipeName,
    string FolderPath,
    string SourcePath,
    string StarterId,
    string RecipePath);

public sealed record ToolWorkbenchFirstRecipeStarterOption(
    string Id,
    string Name,
    string Detail);
