using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Windows.Input;
using OpenVisionLab;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Shell;

namespace OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;

/// <summary>
/// Owns the first-use recipe setup draft and its workspace-scoped preference.
/// Recipe/source mutation remains an explicit callback on the Workbench facade.
/// </summary>
internal sealed class ToolWorkbenchFirstRecipeSetupOwner : INotifyPropertyChanged, IDisposable
{
    public const string EmptyStarterId = "none";
    public const string ThicknessStarterId = "thickness";
    public const string CompatibleVariantStarterId = "compatible-grid-variant";

    private readonly string setupPath;
    private readonly Func<string?> getCurrentRecipePath;
    private readonly Func<string> getCurrentRecipeName;
    private readonly Func<string> getCurrentSourcePath;
    private readonly Func<ToolRecipeSelectionSourceBinding?> getCurrentSourceBinding;
    private readonly Func<IEnumerable<ToolRecipeSelection>> getSelections;
    private readonly Func<string> getCurrentSourceId;
    private readonly Func<string, ToolRecipeSelectionSourceBinding?> readSourceBinding;
    private readonly Action refreshSurfaceMatchExperimentState;
    private readonly RelayCommand createFirstRecipeCommand;
    private readonly RelayCommand beginCompatibleSourceVariantCommand;
    private readonly RelayCommand browseFirstRecipeFolderCommand;
    private readonly RelayCommand browseFirstRecipeSourceCommand;
    private readonly RelayCommand resetFirstRecipeSetupCommand;
    private readonly RelayCommand cancelFirstRecipeSetupCommand;

    private bool isFirstRecipeSetupVisible;
    private bool isCompatibleVariantSetup;
    private string firstRecipeName = "new-inspection";
    private string firstRecipeFolderPath = string.Empty;
    private string firstRecipeSourcePath = string.Empty;
    private bool rememberFirstRecipeSetup;
    private ToolWorkbenchFirstRecipeStarterOption? selectedFirstRecipeStarter;
    private FirstRecipeSetupPreference? rememberedFirstRecipeSetup;
    private int disposalState;

    public ToolWorkbenchFirstRecipeSetupOwner(
        string recentRecipesPath,
        Func<string?> getCurrentRecipePath,
        Func<string> getCurrentRecipeName,
        Func<string> getCurrentSourcePath,
        Func<ToolRecipeSelectionSourceBinding?> getCurrentSourceBinding,
        Func<IEnumerable<ToolRecipeSelection>> getSelections,
        Func<string> getCurrentSourceId,
        Func<string, ToolRecipeSelectionSourceBinding?> readSourceBinding,
        Action refreshSurfaceMatchExperimentState)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(recentRecipesPath);
        ArgumentNullException.ThrowIfNull(getCurrentRecipePath);
        ArgumentNullException.ThrowIfNull(getCurrentRecipeName);
        ArgumentNullException.ThrowIfNull(getCurrentSourcePath);
        ArgumentNullException.ThrowIfNull(getCurrentSourceBinding);
        ArgumentNullException.ThrowIfNull(getSelections);
        ArgumentNullException.ThrowIfNull(getCurrentSourceId);
        ArgumentNullException.ThrowIfNull(readSourceBinding);
        ArgumentNullException.ThrowIfNull(refreshSurfaceMatchExperimentState);

        setupPath = Path.Combine(
            Path.GetDirectoryName(Path.GetFullPath(recentRecipesPath))!,
            "first-recipe-setup.json");
        this.getCurrentRecipePath = getCurrentRecipePath;
        this.getCurrentRecipeName = getCurrentRecipeName;
        this.getCurrentSourcePath = getCurrentSourcePath;
        this.getCurrentSourceBinding = getCurrentSourceBinding;
        this.getSelections = getSelections;
        this.getCurrentSourceId = getCurrentSourceId;
        this.readSourceBinding = readSourceBinding;
        this.refreshSurfaceMatchExperimentState = refreshSurfaceMatchExperimentState;

        createFirstRecipeCommand = new RelayCommand(
            _ => CreateRequested?.Invoke(this, EventArgs.Empty),
            _ => IsFirstRecipeSetupValid);
        beginCompatibleSourceVariantCommand = new RelayCommand(
            _ => BeginCompatibleSourceVariantSetup(),
            _ => CanBeginCompatibleSourceVariant());
        browseFirstRecipeFolderCommand = new RelayCommand(
            _ => BrowseFirstRecipeFolderRequested?.Invoke(this, EventArgs.Empty));
        browseFirstRecipeSourceCommand = new RelayCommand(
            _ => BrowseFirstRecipeSourceRequested?.Invoke(this, EventArgs.Empty));
        resetFirstRecipeSetupCommand = new RelayCommand(_ => ResetFirstRecipeSetup());
        cancelFirstRecipeSetupCommand = new RelayCommand(_ => IsFirstRecipeSetupVisible = false);
        rememberedFirstRecipeSetup = LoadFirstRecipeSetupPreference();
        RefreshFirstRecipeStarterOptions(EmptyStarterId);
        OpenVisionLanguageService.LanguageChanged += OnFirstRecipeLanguageChanged;
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    public event EventHandler? CreateRequested;
    public event EventHandler? BrowseFirstRecipeFolderRequested;
    public event EventHandler? BrowseFirstRecipeSourceRequested;

    public void Dispose()
    {
        if (Interlocked.Exchange(ref disposalState, 1) != 0)
        {
            return;
        }

        OpenVisionLanguageService.LanguageChanged -= OnFirstRecipeLanguageChanged;
    }

    public ObservableCollection<ToolWorkbenchFirstRecipeStarterOption> FirstRecipeStarterOptions { get; } = [];

    public ICommand CreateFirstRecipeCommand => createFirstRecipeCommand;
    public ICommand BeginCompatibleSourceVariantCommand => beginCompatibleSourceVariantCommand;
    public ICommand BrowseFirstRecipeFolderCommand => browseFirstRecipeFolderCommand;
    public ICommand BrowseFirstRecipeSourceCommand => browseFirstRecipeSourceCommand;
    public ICommand ResetFirstRecipeSetupCommand => resetFirstRecipeSetupCommand;
    public ICommand CancelFirstRecipeSetupCommand => cancelFirstRecipeSetupCommand;

    public bool IsFirstRecipeSetupVisible
    {
        get => isFirstRecipeSetupVisible;
        private set => SetField(ref isFirstRecipeSetupVisible, value);
    }

    public bool IsCompatibleVariantSetup
    {
        get => isCompatibleVariantSetup;
        private set
        {
            if (!SetField(ref isCompatibleVariantSetup, value))
            {
                return;
            }

            OnPropertyChanged(nameof(FirstRecipeSetupTitle));
            OnPropertyChanged(nameof(FirstRecipeSetupDetailText));
            OnPropertyChanged(nameof(FirstRecipeCreateLabel));
            OnPropertyChanged(nameof(IsFirstRecipeStarterEditable));
            OnPropertyChanged(nameof(IsFirstRecipeSetupMemoryAvailable));
        }
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
            if (!SetField(ref selectedFirstRecipeStarter, value))
            {
                return;
            }

            OnPropertyChanged(nameof(FirstRecipeStarterDetail));
            NotifyFirstRecipeSetupDraftChanged();
        }
    }

    public bool RememberFirstRecipeSetup
    {
        get => rememberFirstRecipeSetup;
        set => SetField(ref rememberFirstRecipeSetup, value);
    }

    public string FirstRecipeStarterDetail =>
        SelectedFirstRecipeStarter?.Detail ?? ThreeDLocalization.Shared.FirstRecipeNoStarterDetail;

    public string FirstRecipeSetupTitle => IsCompatibleVariantSetup
        ? ThreeDLocalization.Shared.CompatibleVariantSetup
        : ThreeDLocalization.Shared.FirstRecipeSetup;

    public string FirstRecipeSetupDetailText => IsCompatibleVariantSetup
        ? ThreeDLocalization.Shared.CompatibleVariantSetupDetail
        : ThreeDLocalization.Shared.FirstRecipeSetupDetail;

    public string FirstRecipeCreateLabel => IsCompatibleVariantSetup
        ? ThreeDLocalization.Shared.CompatibleVariantCreate
        : ThreeDLocalization.Shared.FirstRecipeCreate;

    public bool IsFirstRecipeStarterEditable => !IsCompatibleVariantSetup;
    public bool IsFirstRecipeSetupMemoryAvailable => !IsCompatibleVariantSetup;

    public string FirstRecipeTargetPath => TryBuildFirstRecipeTargetPath(out var path)
        ? path
        : ThreeDLocalization.Shared.FirstRecipeTargetUnavailable;

    public bool IsFirstRecipeSetupValid => GetFirstRecipeSetupValidationError() is null;

    public string FirstRecipeSetupStatus =>
        GetFirstRecipeSetupValidationError()
        ?? (IsCompatibleVariantSetup
            ? ThreeDLocalization.Shared.CompatibleVariantReady
            : ThreeDLocalization.Shared.FirstRecipeReadyToCreate);

    public void BeginFirstRecipeSetup()
    {
        IsCompatibleVariantSetup = false;
        var setup = rememberedFirstRecipeSetup;
        FirstRecipeName = setup?.RecipeName ?? "new-inspection";
        FirstRecipeFolderPath = setup?.FolderPath ?? string.Empty;
        FirstRecipeSourcePath = setup?.SourcePath ?? string.Empty;
        RememberFirstRecipeSetup = setup is not null;
        RefreshFirstRecipeStarterOptions(setup?.StarterId ?? EmptyStarterId);
        IsFirstRecipeSetupVisible = true;
        NotifyFirstRecipeSetupDraftChanged();
    }

    public void BeginCompatibleSourceVariantSetup()
    {
        if (!CanBeginCompatibleSourceVariant())
        {
            return;
        }

        IsCompatibleVariantSetup = true;
        FirstRecipeName = $"{getCurrentRecipeName().Trim()}-variant";
        FirstRecipeFolderPath = Path.GetDirectoryName(Path.GetFullPath(getCurrentRecipePath()!))!;
        FirstRecipeSourcePath = getCurrentSourcePath();
        RememberFirstRecipeSetup = false;
        RefreshFirstRecipeStarterOptions(CompatibleVariantStarterId);
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
            message = error ?? ThreeDLocalization.Shared.FirstRecipeTargetUnavailable;
            return false;
        }

        setup = new ToolWorkbenchFirstRecipeSetup(
            FirstRecipeName.Trim(),
            Path.GetFullPath(FirstRecipeFolderPath.Trim()),
            Path.GetFullPath(FirstRecipeSourcePath.Trim()),
            IsCompatibleVariantSetup
                ? CompatibleVariantStarterId
                : SelectedFirstRecipeStarter?.Id ?? EmptyStarterId,
            targetPath,
            IsCompatibleVariantSetup);
        message = ThreeDLocalization.Shared.FirstRecipeReadyToCreate;
        return true;
    }

    public bool TryApplyFirstRecipeStarter(
        string starterId,
        Func<bool> canAddThickness,
        Func<string> getThicknessRouteDetail,
        Func<bool> addThickness,
        out string message)
    {
        ArgumentNullException.ThrowIfNull(canAddThickness);
        ArgumentNullException.ThrowIfNull(getThicknessRouteDetail);
        ArgumentNullException.ThrowIfNull(addThickness);

        if (string.Equals(starterId, EmptyStarterId, StringComparison.Ordinal))
        {
            message = ThreeDLocalization.Shared.FirstRecipeNoStarterDetail;
            return true;
        }

        if (!string.Equals(starterId, ThicknessStarterId, StringComparison.Ordinal))
        {
            message = ThreeDLocalization.Shared.FirstRecipeStarterUnavailable;
            return false;
        }

        if (!canAddThickness())
        {
            message = getThicknessRouteDetail();
            return false;
        }

        var added = addThickness();
        message = added
            ? ThreeDLocalization.Shared.FirstRecipeThicknessStarterApplied
            : ThreeDLocalization.Shared.FirstRecipeStarterUnavailable;
        return added;
    }

    public bool CompleteFirstRecipeSetup(out string message)
    {
        if (IsCompatibleVariantSetup)
        {
            IsFirstRecipeSetupVisible = false;
            IsCompatibleVariantSetup = false;
            message = ThreeDLocalization.Shared.FirstRecipeSetupNotRemembered;
            return true;
        }

        var setup = new FirstRecipeSetupPreference(
            1,
            FirstRecipeName.Trim(),
            Path.GetFullPath(FirstRecipeFolderPath.Trim()),
            Path.GetFullPath(FirstRecipeSourcePath.Trim()),
            SelectedFirstRecipeStarter?.Id ?? EmptyStarterId);
        var persisted = RememberFirstRecipeSetup
            ? TrySaveFirstRecipeSetupPreference(setup, out message)
            : TryDeleteFirstRecipeSetupPreference(out message);
        rememberedFirstRecipeSetup = RememberFirstRecipeSetup && persisted ? setup : null;
        IsFirstRecipeSetupVisible = false;
        return persisted;
    }

    public bool TryValidateCompatibleSourceVariant(
        string sourcePath,
        ToolRecipeSelectionSourceBinding newBinding,
        out string message)
    {
        var error = GetCompatibleVariantValidationError(sourcePath, newBinding);
        message = error ?? string.Empty;
        return error is null;
    }

    public void RefreshPresentation() => NotifyFirstRecipeUx();

    public bool CanBeginCompatibleSourceVariant() =>
        !string.IsNullOrWhiteSpace(getCurrentRecipePath())
        && getCurrentSourceBinding() is not null
        && AreSelectionsCompatibleWithSourceVariant();

    private void ResetFirstRecipeSetup()
    {
        IsCompatibleVariantSetup = false;
        _ = TryDeleteFirstRecipeSetupPreference(out _);
        rememberedFirstRecipeSetup = null;
        FirstRecipeName = "new-inspection";
        FirstRecipeFolderPath = string.Empty;
        FirstRecipeSourcePath = string.Empty;
        RememberFirstRecipeSetup = false;
        RefreshFirstRecipeStarterOptions(EmptyStarterId);
        NotifyFirstRecipeSetupDraftChanged();
    }

    private void OnFirstRecipeLanguageChanged(object? sender, EventArgs args)
    {
        var starterId = SelectedFirstRecipeStarter?.Id ?? EmptyStarterId;
        RefreshFirstRecipeStarterOptions(starterId);
        NotifyFirstRecipeUx();
        NotifyFirstRecipeSetupDraftChanged();
        refreshSurfaceMatchExperimentState();
    }

    private void RefreshFirstRecipeStarterOptions(string selectedId)
    {
        FirstRecipeStarterOptions.Clear();
        if (string.Equals(selectedId, CompatibleVariantStarterId, StringComparison.Ordinal))
        {
            FirstRecipeStarterOptions.Add(new ToolWorkbenchFirstRecipeStarterOption(
                CompatibleVariantStarterId,
                ThreeDLocalization.Shared.CompatibleVariantStarter,
                ThreeDLocalization.Shared.CompatibleVariantStarterDetail));
            SelectedFirstRecipeStarter = FirstRecipeStarterOptions[0];
            return;
        }

        FirstRecipeStarterOptions.Add(new ToolWorkbenchFirstRecipeStarterOption(
            EmptyStarterId,
            ThreeDLocalization.Shared.FirstRecipeNoStarter,
            ThreeDLocalization.Shared.FirstRecipeNoStarterDetail));
        FirstRecipeStarterOptions.Add(new ToolWorkbenchFirstRecipeStarterOption(
            ThicknessStarterId,
            ThreeDLocalization.Shared.FirstRecipeThicknessStarter,
            ThreeDLocalization.Shared.FirstRecipeThicknessStarterDetail));
        SelectedFirstRecipeStarter = FirstRecipeStarterOptions.FirstOrDefault(option =>
            string.Equals(option.Id, selectedId, StringComparison.Ordinal))
            ?? FirstRecipeStarterOptions[0];
    }

    private string? GetFirstRecipeSetupValidationError()
    {
        var name = FirstRecipeName.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            return ThreeDLocalization.Shared.FirstRecipeNameRequired;
        }
        if (name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0
            || name.EndsWith('.')
            || name.EndsWith(' '))
        {
            return ThreeDLocalization.Shared.FirstRecipeNameInvalid;
        }
        if (string.IsNullOrWhiteSpace(FirstRecipeFolderPath))
        {
            return ThreeDLocalization.Shared.FirstRecipeFolderRequired;
        }
        if (!Directory.Exists(FirstRecipeFolderPath.Trim()))
        {
            return ThreeDLocalization.Shared.FirstRecipeFolderMissing;
        }
        if (string.IsNullOrWhiteSpace(FirstRecipeSourcePath))
        {
            return ThreeDLocalization.Shared.FirstRecipeSourceRequired;
        }
        if (!File.Exists(FirstRecipeSourcePath.Trim()))
        {
            return ThreeDLocalization.Shared.FirstRecipeSourceMissing;
        }
        if (!string.Equals(Path.GetExtension(FirstRecipeSourcePath.Trim()), ".C3D", StringComparison.OrdinalIgnoreCase))
        {
            return ThreeDLocalization.Shared.FirstRecipeSourceMustBeC3D;
        }
        if (IsCompatibleVariantSetup)
        {
            var binding = readSourceBinding(FirstRecipeSourcePath.Trim());
            var variantError = binding is null
                ? ThreeDLocalization.Shared.SourceUnreadable
                : GetCompatibleVariantValidationError(FirstRecipeSourcePath.Trim(), binding);
            if (variantError is not null)
            {
                return variantError;
            }
        }
        if (SelectedFirstRecipeStarter is null)
        {
            return ThreeDLocalization.Shared.FirstRecipeStarterRequired;
        }
        if (TryBuildFirstRecipeTargetPath(out var targetPath) && File.Exists(targetPath))
        {
            return ThreeDLocalization.Shared.FirstRecipeAlreadyExists;
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
            if (!File.Exists(setupPath))
            {
                return null;
            }

            var setup = JsonSerializer.Deserialize<FirstRecipeSetupPreference>(
                File.ReadAllBytes(setupPath));
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
        var temporaryPath = $"{setupPath}.tmp.{Guid.NewGuid():N}";
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(setupPath)!);
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
            File.Move(temporaryPath, setupPath, overwrite: true);
            message = ThreeDLocalization.Shared.FirstRecipeSetupRemembered;
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
            if (File.Exists(setupPath))
            {
                File.Delete(setupPath);
            }
            message = ThreeDLocalization.Shared.FirstRecipeSetupNotRemembered;
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
        createFirstRecipeCommand.RaiseCanExecuteChanged();
    }

    private string? GetCompatibleVariantValidationError(
        string sourcePath,
        ToolRecipeSelectionSourceBinding newBinding)
    {
        if (!CanBeginCompatibleSourceVariant())
        {
            return getCurrentSourceBinding() is null || string.IsNullOrWhiteSpace(getCurrentRecipePath())
                ? ThreeDLocalization.Shared.CompatibleVariantCurrentRecipeRequired
                : ThreeDLocalization.Shared.CompatibleVariantSelectionUnsupported;
        }

        try
        {
            if (string.Equals(
                    Path.GetFullPath(sourcePath),
                    Path.GetFullPath(getCurrentSourcePath()),
                    StringComparison.OrdinalIgnoreCase))
            {
                return ThreeDLocalization.Shared.CompatibleVariantSourceMustDiffer;
            }
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return ThreeDLocalization.Shared.CompatibleVariantSourceMustDiffer;
        }

        var currentBinding = getCurrentSourceBinding()!;
        if (newBinding.GridWidth != currentBinding.GridWidth
            || newBinding.GridHeight != currentBinding.GridHeight)
        {
            return string.Format(
                ThreeDLocalization.Shared.CompatibleVariantGridMismatchFormat,
                currentBinding.GridWidth,
                currentBinding.GridHeight,
                newBinding.GridWidth,
                newBinding.GridHeight);
        }
        return null;
    }

    private bool AreSelectionsCompatibleWithSourceVariant() => getSelections().All(selection =>
        string.Equals(selection.Kind, ToolRecipeSelectionKinds.GridRectangle, StringComparison.Ordinal)
        && selection.GridRectangle is not null
        && string.Equals(selection.RootSourceId, getCurrentSourceId(), StringComparison.Ordinal)
        && string.Equals(selection.SourceBinding.Format, "C3D", StringComparison.OrdinalIgnoreCase)
        && string.IsNullOrWhiteSpace(selection.SourceBinding.OwnerEntityId)
        && selection.Points is null
        && selection.Rows is null
        && selection.OrientedBox3D is null
        && selection.GridCircle is null);

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    private void NotifyFirstRecipeUx()
    {
        OnPropertyChanged(nameof(FirstRecipeStarterDetail));
        OnPropertyChanged(nameof(FirstRecipeSetupTitle));
        OnPropertyChanged(nameof(FirstRecipeSetupDetailText));
        OnPropertyChanged(nameof(FirstRecipeCreateLabel));
        OnPropertyChanged(nameof(IsFirstRecipeStarterEditable));
        OnPropertyChanged(nameof(IsFirstRecipeSetupMemoryAvailable));
        beginCompatibleSourceVariantCommand.RaiseCanExecuteChanged();
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    private sealed record FirstRecipeSetupPreference(
        int Version,
        string RecipeName,
        string FolderPath,
        string SourcePath,
        string StarterId);
}
