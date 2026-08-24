extern alias OvlMessageDialogs;

using Microsoft.Win32;
using OpenVisionLab.Logging;
using OpenVisionLab.ThreeD.Shell.Verification.Smoke;
using OpenVisionLab.ThreeD.Shell.Dialogs;
using OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;
using OpenVisionLab.ThreeD.Shell.Views.Recipe;
using OpenVisionLab.ThreeD.Shell.Views.Workbench;
using OpenVisionLab.ThreeD.Viewer;
using OpenVisionLab.ThreeD.Viewer.ViewModels;
using System.IO;
using System.Windows;

using WpfMessageDialogWindow = OvlMessageDialogs::OpenVisionLab.Wpf.MessageDialogs.WpfMessageDialogWindow;

namespace OpenVisionLab.ThreeD.Shell.Coordination;

internal enum ShellLifecycleDialogChoice
{
    Yes,
    No,
    Cancel
}

internal sealed record ShellWorkbenchLifecycleCallbacks
{
    public required Action<string> ShowLoadSourceFailure { get; init; }
    public required Action<string> ShowRecipeSaveFailure { get; init; }
    public required Action<string> ShowFirstRecipeCreateFailure { get; init; }
    public required Action<string> ShowFirstRecipeSetupPersistenceFailure { get; init; }
    public required Action<string> ShowRecipeFileUnavailable { get; init; }
    public required Action<string> ShowRecipeOpenFailure { get; init; }
    public required Action ShowRecipeSourceNotReady { get; init; }
    public required Action<string> ShowRecipeSourceLoadFailure { get; init; }
    public required Action<string> ShowParameterApplyFailure { get; init; }
    public required Func<ShellLifecycleDialogChoice> ConfirmUnsavedRecipeChanges { get; init; }
    public required Func<ShellLifecycleDialogChoice> ConfirmPendingParameterChanges { get; init; }
    public required Func<(bool Success, string Message)> CommitPendingParameterEdit { get; init; }
    public required Action DiscardPendingParameterChanges { get; init; }
    public required Action ActivateWorkbench { get; init; }
    public required Func<string, string, string, string> DialogText { get; init; }
}

internal sealed class ShellWorkbenchLifecycleController : IDisposable
{
    private readonly Window _owner;
    private readonly OpenVisionThreeDViewerControl _viewer;
    private readonly ShellMainWindowViewModel _viewModel;
    private readonly RecipeFileDialogService _recipeFileDialogs;
    private readonly WorkbenchViewerTeachingCoordinator _workbenchViewerTeaching;
    private readonly ShellWorkbenchLifecycleCallbacks _callbacks;
    private RecipeManagerWindow? _recipeManagerWindow;
    private CancellationTokenSource? _c3dSourceLoadCancellation;

    public ShellWorkbenchLifecycleController(
        Window owner,
        OpenVisionThreeDViewerControl viewer,
        ShellMainWindowViewModel viewModel,
        RecipeFileDialogService recipeFileDialogs,
        WorkbenchViewerTeachingCoordinator workbenchViewerTeaching,
        ShellWorkbenchLifecycleCallbacks callbacks)
    {
        _owner = owner;
        _viewer = viewer;
        _viewModel = viewModel;
        _recipeFileDialogs = recipeFileDialogs;
        _workbenchViewerTeaching = workbenchViewerTeaching;
        _callbacks = callbacks;
    }

    public RecipeManagerWindow? RecipeManagerWindow => _recipeManagerWindow;

    public bool IsRecipeManagerVisible => _recipeManagerWindow?.IsVisible == true;

    public double LastWorkbenchSourceBindingMilliseconds { get; private set; }

    public Window GetRecipeLifecycleDialogOwner() => IsRecipeManagerVisible ? _recipeManagerWindow! : _owner;

    public void ShowRecipeManagerWindow()
    {
        if (_recipeManagerWindow is null)
        {
            _recipeManagerWindow = new RecipeManagerWindow
            {
                Owner = _owner,
                DataContext = _viewModel.Workbench
            };
            _recipeManagerWindow.Closed += (_, _) => _recipeManagerWindow = null;
        }

        _recipeManagerWindow.Show();
        _recipeManagerWindow.Activate();
    }

    public void CloseRecipeManager() => _recipeManagerWindow?.Close();

    public void HideRecipeManager() => _recipeManagerWindow?.Hide();

    public async Task<bool> LoadWorkbenchC3DSourceAsync(
        string path,
        bool showFailureDialog = true,
        bool bindToWorkbench = true)
    {
        var cancellation = new CancellationTokenSource();
        _c3dSourceLoadCancellation = cancellation;
        LastWorkbenchSourceBindingMilliseconds = 0.0;
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        _viewModel.Workbench.BeginC3DSourceLoad(path);
        var progress = new Progress<double>(_viewModel.Workbench.ReportC3DSourceLoadProgress);

        try
        {
            if (await _viewer.LoadC3DSourceAsync(path, cancellation.Token, progress)
                && _viewer.CurrentC3DSourcePath is { } sourcePath)
            {
                if (bindToWorkbench)
                {
                    SetWorkbenchC3DSourceFromViewer(sourcePath);
                }
                _viewer.ViewModel.HudDetailsVisible = false;
                _viewModel.Workbench.CompleteC3DSourceLoad(sourcePath, stopwatch.ElapsedMilliseconds);
                return true;
            }

            _viewModel.Workbench.FailC3DSourceLoad(path, stopwatch.ElapsedMilliseconds);
            if (showFailureDialog)
            {
                _callbacks.ShowLoadSourceFailure(_viewer.HostState.ViewerStatus);
            }
            return false;
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            _viewModel.Workbench.CancelC3DSourceLoad(stopwatch.ElapsedMilliseconds);
            return false;
        }
        finally
        {
            if (ReferenceEquals(_c3dSourceLoadCancellation, cancellation))
            {
                _c3dSourceLoadCancellation = null;
            }
            cancellation.Dispose();
        }
    }

    public void CancelC3DSourceLoad() => _c3dSourceLoadCancellation?.Cancel();

    public void ConfigureFirstRecipeSetupForSmoke(ShellSmokeCommandLineOptions smoke)
    {
        if (string.IsNullOrWhiteSpace(smoke.FirstRecipeSetupSmokeState))
        {
            return;
        }

        _viewModel.Workbench.BeginFirstRecipeSetup();
        _viewModel.Workbench.FirstRecipeName = smoke.FirstRecipeSetupName ?? "Thickness first inspection";
        _viewModel.Workbench.FirstRecipeFolderPath = smoke.FirstRecipeSetupFolderPath ?? string.Empty;
        _viewModel.Workbench.FirstRecipeSourcePath = smoke.FirstRecipeSetupSourcePath ?? string.Empty;
        var starterId = smoke.FirstRecipeSetupStarterId
            ?? (string.Equals(smoke.FirstRecipeSetupSmokeState, "valid", StringComparison.OrdinalIgnoreCase)
                ? ToolWorkbenchViewModel.ThicknessFirstRecipeStarterId
                : ToolWorkbenchViewModel.EmptyFirstRecipeStarterId);
        _viewModel.Workbench.SelectedFirstRecipeStarter = _viewModel.Workbench.FirstRecipeStarterOptions
            .First(option => string.Equals(option.Id, starterId, StringComparison.Ordinal));
        _viewModel.Workbench.RememberFirstRecipeSetup = smoke.FirstRecipeSetupRememberSmoke;
        if (smoke.FirstRecipeStarterPopupSmoke && _recipeManagerWindow is not null)
        {
            _recipeManagerWindow.UpdateLayout();
            var starter = FindVisualDescendants<System.Windows.Controls.ComboBox>(_recipeManagerWindow)
                .FirstOrDefault(comboBox =>
                    System.Windows.Automation.AutomationProperties.GetAutomationId(comboBox)
                    == "FirstRecipeStarter");
            if (starter is not null)
            {
                starter.Focus();
                starter.IsDropDownOpen = true;
            }
        }
    }

    public async void LoadC3DSourceRequested(object? sender, EventArgs args)
    {
        var dialog = new OpenFileDialog
        {
            Title = _callbacks.DialogText("ThreeD.FileDialog.LoadC3D.Title", "레시피 티칭용 C3D 입력 불러오기", "Load C3D Input for Recipe Teaching"),
            Filter = _callbacks.DialogText("ThreeD.FileDialog.LoadC3D.Filter", "C3D 높이 맵 (*.C3D)|*.C3D|모든 파일 (*.*)|*.*", "C3D height map (*.C3D)|*.C3D|All files (*.*)|*.*"),
            CheckFileExists = true,
            Multiselect = false
        };
        if (dialog.ShowDialog(_owner) != true)
        {
            return;
        }

        if (IsViewerSourceAlreadyLoaded(dialog.FileName))
        {
            SetWorkbenchC3DSourceFromViewer(Path.GetFullPath(dialog.FileName));
            _viewer.ViewModel.HudDetailsVisible = false;
            return;
        }

        await LoadWorkbenchC3DSourceAsync(dialog.FileName);
    }

    public async void Import3DDataRequested(object? sender, EventArgs args)
    {
        OVLog.Write(LogCategory.UI, LogLevel.Info, "Workbench[Import] Opening verified 3D data dialog.");
        var dialog = new OpenFileDialog
        {
            Title = _callbacks.DialogText("ThreeD.FileDialog.Import3D.Title", "3D 데이터 가져오기", "Import 3D Data"),
            Filter = _callbacks.DialogText(
                "ThreeD.FileDialog.Import3D.Filter",
                "3D: C3D/GLB/STL/LAS/LAZ|*.C3D;*.GLB;*.STL;*.LAS;*.LAZ|C3D 높이 맵|*.C3D|GLB 메시|*.GLB|STL 메시|*.STL|LAS/LAZ 포인트 클라우드|*.LAS;*.LAZ",
                "3D: C3D/GLB/STL/LAS/LAZ|*.C3D;*.GLB;*.STL;*.LAS;*.LAZ|C3D height map|*.C3D|GLB mesh|*.GLB|STL mesh|*.STL|LAS/LAZ point cloud|*.LAS;*.LAZ"),
            CheckFileExists = true,
            Multiselect = false
        };
        if (dialog.ShowDialog(_owner) != true)
        {
            OVLog.Write(LogCategory.UI, LogLevel.Info, "Workbench[Import] Dialog closed without a file selection.");
            return;
        }

        var extension = Path.GetExtension(dialog.FileName);
        if (string.Equals(extension, ".c3d", StringComparison.OrdinalIgnoreCase))
        {
            if (IsViewerSourceAlreadyLoaded(dialog.FileName))
            {
                SetWorkbenchC3DSourceFromViewer(Path.GetFullPath(dialog.FileName));
                _viewer.ViewModel.HudDetailsVisible = false;
                return;
            }

            await LoadWorkbenchC3DSourceAsync(dialog.FileName);
            return;
        }

        await LoadViewerOnlySourceAsync(dialog.FileName);
    }

    public async Task<bool> LoadViewerOnlySourceAsync(string path, bool showFailureDialog = true)
    {
        var extension = Path.GetExtension(path);
        var format = extension.TrimStart('.').ToUpperInvariant();
        if (format is not ("GLB" or "STL" or "LAS" or "LAZ"))
        {
            throw new NotSupportedException($"Viewer-only import does not support '{extension}'.");
        }

        var cancellation = new CancellationTokenSource();
        _c3dSourceLoadCancellation = cancellation;
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        _viewModel.Workbench.Begin3DDataImport(path, format);
        var progress = new Progress<double>(_viewModel.Workbench.ReportC3DSourceLoadProgress);

        try
        {
            if (await _viewer.LoadViewerOnlySourceAsync(path, cancellation.Token, progress))
            {
                _viewer.ViewModel.HudDetailsVisible = false;
                _viewModel.Workbench.CompleteViewerOnlyImport(path, format, stopwatch.ElapsedMilliseconds);
                return true;
            }

            _viewModel.Workbench.FailC3DSourceLoad(path, stopwatch.ElapsedMilliseconds);
            if (showFailureDialog)
            {
                _callbacks.ShowLoadSourceFailure(_viewer.HostState.ViewerStatus);
            }
            return false;
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            _viewModel.Workbench.CancelC3DSourceLoad(stopwatch.ElapsedMilliseconds);
            return false;
        }
        finally
        {
            if (ReferenceEquals(_c3dSourceLoadCancellation, cancellation))
            {
                _c3dSourceLoadCancellation = null;
            }
            cancellation.Dispose();
        }
    }

    public async Task<bool> ClickUnsavedRecipeDoNotSaveForSmokeAsync()
    {
        var buttonText = _callbacks.DialogText(
            "ThreeD.Dialog.UnsavedRecipe.DoNotSave",
            "저장 안 함",
            "Don't Save");
        for (var attempt = 0; attempt < 40; attempt++)
        {
            await Task.Delay(100).ConfigureAwait(false);
            var clicked = await _owner.Dispatcher.InvokeAsync(() =>
            {
                var dialog = Application.Current.Windows
                    .OfType<WpfMessageDialogWindow>()
                    .FirstOrDefault(window => window.IsVisible);
                var button = dialog is null
                    ? null
                    : FindVisualDescendants<System.Windows.Controls.Button>(dialog)
                        .FirstOrDefault(candidate => string.Equals(candidate.Content?.ToString(), buttonText, StringComparison.Ordinal));
                if (button is null)
                {
                    return false;
                }
                button.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
                return true;
            });
            if (clicked)
            {
                return true;
            }
        }
        return false;
    }

    public void NewTeachingRecipeRequested(object? sender, EventArgs args) => _ = NewTeachingRecipeAsync();

    private async Task NewTeachingRecipeAsync()
    {
        if (!_viewModel.Workbench.TryGetFirstRecipeSetup(out var setup, out _)
            || !TryResolveWorkbenchChanges("creating a new recipe"))
        {
            return;
        }

        var sourceLoaded = IsViewerSourceAlreadyLoaded(setup.SourcePath)
            || await LoadWorkbenchC3DSourceAsync(setup.SourcePath, bindToWorkbench: false);
        if (!sourceLoaded)
        {
            return;
        }

        if (setup.IsCompatibleSourceVariant)
        {
            if (!_viewer.TryGetCurrentC3DSourceBinding(setup.SourcePath, out var variantBinding))
            {
                _callbacks.ShowFirstRecipeCreateFailure(_viewModel.Workbench.Localization.SourceUnreadable);
                return;
            }
            if (!_viewModel.Workbench.TryCreateCompatibleSourceVariant(setup, variantBinding, out var variantMessage))
            {
                _callbacks.ShowFirstRecipeCreateFailure(variantMessage);
                return;
            }
            _viewModel.ClearCurrentRunEvidenceForRecipeContext();
            _viewModel.Workbench.CompleteFirstRecipeSetup(out _);
            ActivateWorkbenchAfterRecipeLifecycle();
            return;
        }

        _viewModel.Workbench.CreateNewTeachingRecipe(setup.RecipeName);
        _viewModel.ClearCurrentRunEvidenceForRecipeContext();
        SetWorkbenchC3DSourceFromViewer(setup.SourcePath);
        if (!_viewModel.Workbench.TryApplyFirstRecipeStarter(setup.StarterId, out var starterMessage))
        {
            _callbacks.ShowFirstRecipeCreateFailure(starterMessage);
            return;
        }
        if (!_viewModel.Workbench.TrySaveTeachingRecipe(setup.RecipePath, out var message))
        {
            _callbacks.ShowRecipeSaveFailure(message);
            return;
        }

        if (!_viewModel.Workbench.CompleteFirstRecipeSetup(out var persistenceMessage))
        {
            _callbacks.ShowFirstRecipeSetupPersistenceFailure(persistenceMessage);
        }
        ActivateWorkbenchAfterRecipeLifecycle();
    }

    public void BrowseFirstRecipeFolderRequested(object? sender, EventArgs args)
    {
        var current = _viewModel.Workbench.FirstRecipeFolderPath.Trim();
        var dialog = new OpenFolderDialog
        {
            Title = _callbacks.DialogText("ThreeD.FileDialog.FirstRecipeFolder.Title", "새 레시피를 저장할 폴더 선택", "Select Folder for New Recipe"),
            Multiselect = false,
            InitialDirectory = Directory.Exists(current) ? current : null
        };
        if (dialog.ShowDialog(GetRecipeLifecycleDialogOwner()) == true)
        {
            _viewModel.Workbench.FirstRecipeFolderPath = dialog.FolderName;
        }
    }

    public void BrowseFirstRecipeSourceRequested(object? sender, EventArgs args)
    {
        var source = _viewModel.Workbench.FirstRecipeSourcePath.Trim();
        var folder = _viewModel.Workbench.FirstRecipeFolderPath.Trim();
        var dialog = new OpenFileDialog
        {
            Title = _callbacks.DialogText("ThreeD.FileDialog.FirstRecipeSource.Title", "새 레시피의 C3D 입력 선택", "Select C3D Input for New Recipe"),
            Filter = _callbacks.DialogText("ThreeD.FileDialog.LoadC3D.Filter", "C3D 높이 맵 (*.C3D)|*.C3D|모든 파일 (*.*)|*.*", "C3D height map (*.C3D)|*.C3D|All files (*.*)|*.*"),
            CheckFileExists = true,
            Multiselect = false,
            InitialDirectory = File.Exists(source) ? Path.GetDirectoryName(source) : Directory.Exists(folder) ? folder : null
        };
        if (dialog.ShowDialog(GetRecipeLifecycleDialogOwner()) == true)
        {
            _viewModel.Workbench.FirstRecipeSourcePath = dialog.FileName;
        }
    }

    public void SaveTeachingRecipeRequested(object? sender, EventArgs args)
    {
        if (TryResolveParameterDraft())
        {
            SaveWorkbenchRecipe(forceDialog: false);
        }
    }

    public void SaveTeachingRecipeAsRequested(object? sender, EventArgs args)
    {
        if (TryResolveParameterDraft())
        {
            SaveWorkbenchRecipe(forceDialog: true);
        }
    }

    public void OpenTeachingRecipeRequested(object? sender, EventArgs args)
    {
        if (!TryResolveWorkbenchChanges("opening another recipe")
            || !_recipeFileDialogs.TrySelectOpenPath(out var path))
        {
            return;
        }
        OpenWorkbenchRecipe(path);
    }

    public void OpenRecentTeachingRecipeRequested(object? sender, ToolWorkbenchRecipePathRequestEventArgs args)
    {
        if (TryResolveWorkbenchChanges("opening a recent recipe"))
        {
            OpenWorkbenchRecipe(args.Path);
        }
    }

    public void RestoreMostRecentWorkbenchRecipe()
    {
        if (!string.IsNullOrWhiteSpace(_viewModel.Workbench.RecipePath))
        {
            return;
        }

        var path = _viewModel.Workbench.MostRecentAvailableRecipePath;
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        OVLog.Write(
            LogCategory.UI,
            LogLevel.Info,
            $"Workbench[Open] Restoring most recent recipe | path={path} | preview=false | run=false | publish=false.");
        OpenWorkbenchRecipe(path);
    }

    public void OpenWorkbenchRecipe(string path)
    {
        if (!File.Exists(path))
        {
            _callbacks.ShowRecipeFileUnavailable(path);
            return;
        }

        if (!_viewModel.Workbench.TryOpenTeachingRecipe(path, out var message))
        {
            _callbacks.ShowRecipeOpenFailure(message);
            return;
        }

        _viewModel.ClearCurrentRunEvidenceForRecipeContext();
        ActivateWorkbenchAfterRecipeLifecycle();

        var source = _viewModel.Workbench.Source;
        if (!_viewModel.Workbench.IsSourceReadyForRecipe)
        {
            _viewer.ClearC3DTeachingSource(_viewModel.Workbench.SourceReadinessSummary);
            _viewModel.UpdateC3DSampleVisible(false);
            _callbacks.ShowRecipeSourceNotReady();
            return;
        }

        if (IsViewerSourceAlreadyLoaded(source.Path))
        {
            _workbenchViewerTeaching.SyncAppliedSelections();
            return;
        }

        if (!_viewer.LoadC3DSource(source.Path))
        {
            var loadFailure = _viewer.HostState.ViewerStatus;
            _viewer.ClearC3DTeachingSource("Recipe source could not be loaded. Relink a valid C3D source.");
            _viewModel.UpdateC3DSampleVisible(false);
            _callbacks.ShowRecipeSourceLoadFailure(loadFailure);
            return;
        }

        if (_viewer.CurrentC3DSourcePath is { } loadedSourcePath)
        {
            SetWorkbenchC3DSourceFromViewer(loadedSourcePath);
            _workbenchViewerTeaching.SyncAppliedSelections();
        }
    }

    public bool TryResolveWorkbenchChanges(string reason)
    {
        if (!TryResolveParameterDraft()
            || (!_viewModel.Workbench.IsDirty && !_viewModel.Workbench.IsValidationSetDefinitionDirty))
        {
            return !_viewModel.Workbench.HasPendingStepParameterChanges;
        }

        return _callbacks.ConfirmUnsavedRecipeChanges() switch
        {
            ShellLifecycleDialogChoice.Yes => SaveWorkbenchRecipe(forceDialog: false),
            ShellLifecycleDialogChoice.No => true,
            _ => false
        };
    }

    private bool TryResolveParameterDraft()
    {
        if (!_viewModel.Workbench.HasPendingStepParameterChanges)
        {
            return true;
        }

        var result = _callbacks.ConfirmPendingParameterChanges();
        if (result == ShellLifecycleDialogChoice.Cancel)
        {
            return false;
        }
        if (result == ShellLifecycleDialogChoice.No)
        {
            _callbacks.DiscardPendingParameterChanges();
            return true;
        }

        var commit = _callbacks.CommitPendingParameterEdit();
        var message = commit.Message;
        if (!commit.Success)
        {
            _viewModel.Workbench.ReportParameterDraftCommitError(message);
            _callbacks.ShowParameterApplyFailure(message);
            return false;
        }
        if (!_viewModel.Workbench.TryApplySelectedStepParameterDraft(out var draftMessage))
        {
            message = string.IsNullOrWhiteSpace(draftMessage) ? commit.Message : draftMessage;
            _viewModel.Workbench.ReportParameterDraftCommitError(message);
            _callbacks.ShowParameterApplyFailure(message);
            return false;
        }
        return true;
    }

    public bool TrySaveWorkbenchRecipe(bool forceDialog) => SaveWorkbenchRecipe(forceDialog);

    private bool SaveWorkbenchRecipe(bool forceDialog)
    {
        var path = _viewModel.Workbench.RecipePath;
        if ((forceDialog || string.IsNullOrWhiteSpace(path))
            && !_recipeFileDialogs.TrySelectSavePath(path, forceDialog, out path))
        {
            return false;
        }

        if (_viewModel.Workbench.TrySaveTeachingRecipe(path, out var message))
        {
            return true;
        }

        _callbacks.ShowRecipeSaveFailure(message);
        return false;
    }

    private void ActivateWorkbenchAfterRecipeLifecycle()
    {
        _recipeManagerWindow?.Hide();
        _callbacks.ActivateWorkbench();
    }

    public bool IsViewerSourceAlreadyLoaded(string path)
    {
        if (_viewer.CurrentC3DSourcePath is not { } currentPath)
        {
            return false;
        }
        return string.Equals(Path.GetFullPath(currentPath), Path.GetFullPath(path), StringComparison.OrdinalIgnoreCase);
    }

    public void SetWorkbenchC3DSourceFromViewer(string path, bool markDirty = true)
    {
        var sourceBindingStart = System.Diagnostics.Stopwatch.GetTimestamp();
        if (!_viewer.TryGetCurrentC3DSourceBinding(path, out var sourceBinding))
        {
            throw new InvalidOperationException("The Viewer source identity is unavailable or does not match the requested C3D path.");
        }

        _viewModel.Workbench.SetC3DSourceFromLoadedViewer(path, sourceBinding, markDirty);
        if (markDirty)
        {
            _viewModel.ClearCurrentRunEvidenceForRecipeContext();
        }
        LastWorkbenchSourceBindingMilliseconds = System.Diagnostics.Stopwatch.GetElapsedTime(sourceBindingStart).TotalMilliseconds;
    }

    public void SyncWorkbenchSourceFromViewer()
    {
        if (_viewer.CurrentC3DSourcePath is { } sourcePath
            && string.IsNullOrWhiteSpace(_viewModel.Workbench.Source.Path))
        {
            SetWorkbenchC3DSourceFromViewer(sourcePath, markDirty: false);
        }
    }

    public void Dispose()
    {
        _c3dSourceLoadCancellation?.Cancel();
        _c3dSourceLoadCancellation?.Dispose();
        _c3dSourceLoadCancellation = null;
        _recipeManagerWindow?.Close();
        _recipeManagerWindow = null;
    }

    private static IEnumerable<T> FindVisualDescendants<T>(DependencyObject root)
        where T : DependencyObject
    {
        for (var index = 0; index < System.Windows.Media.VisualTreeHelper.GetChildrenCount(root); index++)
        {
            var child = System.Windows.Media.VisualTreeHelper.GetChild(root, index);
            if (child is T match)
            {
                yield return match;
            }
            foreach (var descendant in FindVisualDescendants<T>(child))
            {
                yield return descendant;
            }
        }
    }
}
