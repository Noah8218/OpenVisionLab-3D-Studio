extern alias OvlMessageDialogs;

using OpenVisionLab.Logging;
using OpenVisionLab.ThreeD.Shell.Verification.Smoke;
using OpenVisionLab.ThreeD.Shell.Dialogs;
using OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;
using OpenVisionLab.ThreeD.Shell.Views.Recipe;
using System.ComponentModel;
using System.IO;
using System.Threading;
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
    private readonly ShellMainWindowViewModel _viewModel;
    private readonly RecipeFileDialogService _recipeFileDialogs;
    private readonly ShellSourceFileDialogService _sourceFileDialogs;
    private readonly WorkbenchViewerTeachingCoordinator _workbenchViewerTeaching;
    private readonly ShellWorkbenchLifecycleCallbacks _callbacks;
    private RecipeManagerWindow? _recipeManagerWindow;
    private double? recipeManagerOwnerOpacityBeforeShow;
    private bool? recipeManagerOwnerEnabledBeforeShow;
    private readonly ShellWorkbenchSourceLoadCoordinator _sourceLoadCoordinator;
    private readonly ShellWorkbenchRecipeSourceCoordinator _recipeSourceCoordinator;
    private readonly ShellWorkbenchRequestOwner _recipeLifecycleRequestOwner;
    private int disposalState;

    public ShellWorkbenchLifecycleController(
        Window owner,
        ShellMainWindowViewModel viewModel,
        RecipeFileDialogService recipeFileDialogs,
        ShellSourceFileDialogService sourceFileDialogs,
        WorkbenchViewerTeachingCoordinator workbenchViewerTeaching,
        ShellWorkbenchSourceLoadCoordinator sourceLoadCoordinator,
        ShellWorkbenchLifecycleCallbacks callbacks)
    {
        _owner = owner;
        _viewModel = viewModel;
        _recipeFileDialogs = recipeFileDialogs;
        _sourceFileDialogs = sourceFileDialogs;
        _workbenchViewerTeaching = workbenchViewerTeaching;
        _callbacks = callbacks;
        _sourceLoadCoordinator = sourceLoadCoordinator ?? throw new ArgumentNullException(nameof(sourceLoadCoordinator));
        _recipeSourceCoordinator = new(
            _sourceLoadCoordinator,
            _viewModel.Workbench,
            _workbenchViewerTeaching,
            _viewModel.UpdateC3DSampleVisible);
        _recipeLifecycleRequestOwner = new(ReportRecipeLifecycleRequestFailure);
        _viewModel.Workbench.FirstRecipeSetupRequested += OnFirstRecipeSetupRequested;
    }

    public RecipeManagerWindow? RecipeManagerWindow => _recipeManagerWindow;

    public bool IsRecipeManagerVisible => _recipeManagerWindow?.IsVisible == true;

    internal bool IsDisposed => Volatile.Read(ref disposalState) != 0;

    public double LastWorkbenchSourceBindingMilliseconds =>
        _sourceLoadCoordinator.LastWorkbenchSourceBindingMilliseconds;

    public Window GetRecipeLifecycleDialogOwner() => IsRecipeManagerVisible ? _recipeManagerWindow! : _owner;

    public void ShowRecipeManagerWindow()
    {
        if (IsDisposed)
        {
            return;
        }

        if (_recipeManagerWindow is null)
        {
            _recipeManagerWindow = new RecipeManagerWindow
            {
                Owner = _owner,
                DataContext = _viewModel.Workbench
            };
            _recipeManagerWindow.Closed += OnRecipeManagerWindowClosed;
            _recipeManagerWindow.Closing += OnRecipeManagerWindowClosing;
        }

        if (!_recipeManagerWindow.IsVisible)
        {
            recipeManagerOwnerOpacityBeforeShow = _owner.Opacity;
            recipeManagerOwnerEnabledBeforeShow = _owner.IsEnabled;
            _owner.Opacity = 0.4;
            _owner.IsEnabled = false;
        }

        try
        {
            _viewModel.Workbench.SelectDefaultRecentRecipeIfNeeded();
            _recipeManagerWindow.Show();
            _recipeManagerWindow.Activate();
        }
        catch
        {
            if (!_recipeManagerWindow.IsVisible)
            {
                RestoreRecipeManagerOwnerState();
            }

            throw;
        }
    }

    public void CloseRecipeManager() => _recipeManagerWindow?.Close();

    private void OnFirstRecipeSetupRequested(object? sender, EventArgs args)
    {
        if (_owner.IsVisible)
        {
            ShowRecipeManagerWindow();
        }
    }

    public void HideRecipeManager()
    {
        if (_recipeManagerWindow is not { IsVisible: true } window)
        {
            return;
        }

        window.Hide();
        RestoreRecipeManagerOwnerState();
    }

    public Task<bool> LoadWorkbenchC3DSourceAsync(
        string path,
        bool showFailureDialog = true,
        bool bindToWorkbench = true,
        CancellationToken cancellationToken = default) =>
        _sourceLoadCoordinator.LoadWorkbenchC3DSourceAsync(
            path,
            showFailureDialog,
            bindToWorkbench,
            cancellationToken);

    public void CancelC3DSourceLoad() => _sourceLoadCoordinator.CancelC3DSourceLoad();

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

    public void LoadC3DSourceRequested(object? sender, EventArgs args) =>
        _sourceLoadCoordinator.LoadC3DSourceRequested(sender, args);
    public void UseSelectedInspectionInputForTeachingRequested(object? sender, InspectionQueueSelectionChangedEventArgs args) =>
        _sourceLoadCoordinator.UseSelectedInspectionInputForTeachingRequested(sender, args);

    public void Import3DDataRequested(object? sender, EventArgs args) =>
        _sourceLoadCoordinator.Import3DDataRequested(sender, args);

    public Task<bool> LoadViewerOnlySourceAsync(
        string path,
        bool showFailureDialog = true,
        CancellationToken cancellationToken = default) =>
        _sourceLoadCoordinator.LoadViewerOnlySourceAsync(path, showFailureDialog, cancellationToken);

    public Task<bool> ClickUnsavedRecipeDoNotSaveForSmokeAsync() =>
        ClickUnsavedRecipeDoNotSaveForSmokeAsync(default);

    public async Task<bool> ClickUnsavedRecipeDoNotSaveForSmokeAsync(CancellationToken cancellationToken)
    {
        var requestToken = cancellationToken.CanBeCanceled
            ? cancellationToken
            : _recipeLifecycleRequestOwner.Token;
        if (IsDisposed || requestToken.IsCancellationRequested)
        {
            return false;
        }

        var buttonText = _callbacks.DialogText(
            "ThreeD.Dialog.UnsavedRecipe.DoNotSave",
            "저장 안 함",
            "Don't Save");
        for (var attempt = 0; attempt < 40; attempt++)
        {
            try
            {
                await Task.Delay(100, requestToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (requestToken.IsCancellationRequested)
            {
                return false;
            }

            if (IsDisposed)
            {
                return false;
            }

            var clicked = await _owner.Dispatcher.InvokeAsync(() =>
            {
                var dialog = _owner.OwnedWindows
                    .OfType<WpfMessageDialogWindow>()
                    .Concat(_recipeManagerWindow?.OwnedWindows.OfType<WpfMessageDialogWindow>()
                        ?? Enumerable.Empty<WpfMessageDialogWindow>())
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

    public void NewTeachingRecipeRequested(object? sender, EventArgs args) =>
        _recipeLifecycleRequestOwner.TryStart(NewTeachingRecipeAsync);

    private void ReportRecipeLifecycleRequestFailure(Exception exception)
    {
        OVLog.Write(
            LogCategory.UI,
            LogLevel.Error,
            $"Workbench[New recipe] request failed: {exception}");
        if (IsDisposed)
        {
            return;
        }

        try
        {
            _callbacks.ShowFirstRecipeCreateFailure(exception.Message);
        }
        catch (Exception dialogException)
        {
            OVLog.Write(
                LogCategory.UI,
                LogLevel.Error,
                $"Workbench[New recipe] failure dialog failed: {dialogException}");
        }
    }

    private async Task NewTeachingRecipeAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!_viewModel.Workbench.TryGetFirstRecipeSetup(out var setup, out _)
            || !TryResolveWorkbenchChanges("creating a new recipe"))
        {
            return;
        }

        if (setup.IsCompatibleSourceVariant)
        {
            var sourcePreparation = await _recipeSourceCoordinator.PrepareNewRecipeSourceAsync(
                setup,
                cancellationToken);
            if (!sourcePreparation.IsReady)
            {
                if (sourcePreparation.FailureMessage is not null)
                {
                    _callbacks.ShowFirstRecipeCreateFailure(sourcePreparation.FailureMessage);
                }

                return;
            }

            cancellationToken.ThrowIfCancellationRequested();
            if (sourcePreparation.VariantBinding is not { } variantBinding)
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
            var variantSource = await _recipeSourceCoordinator.ApplyCreatedVariantSourceAsync(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            ActivateWorkbenchAfterRecipeLifecycle();
            if (!variantSource.IsReady && variantSource.FailureMessage is { } loadFailure)
            {
                _callbacks.ShowRecipeSourceLoadFailure(loadFailure);
            }
            return;
        }

        if (!_viewModel.Workbench.TryCreateEmptyTeachingRecipe(setup, out var createMessage))
        {
            _callbacks.ShowRecipeSaveFailure(createMessage);
            return;
        }

        _viewModel.ClearCurrentRunEvidenceForRecipeContext();
        _recipeSourceCoordinator.ApplyOpenedRecipeSource();
        if (!_viewModel.Workbench.CompleteFirstRecipeSetup(out var persistenceMessage))
        {
            _callbacks.ShowFirstRecipeSetupPersistenceFailure(persistenceMessage);
        }

        ActivateWorkbenchAfterRecipeLifecycle();
        if (string.IsNullOrWhiteSpace(setup.SourcePath))
        {
            return;
        }

        var inputPreparation = await _recipeSourceCoordinator.PrepareNewRecipeSourceAsync(
            setup,
            cancellationToken);
        if (!inputPreparation.IsReady)
        {
            if (inputPreparation.FailureMessage is not null)
            {
                _callbacks.ShowFirstRecipeCreateFailure(inputPreparation.FailureMessage);
            }

            return;
        }

        cancellationToken.ThrowIfCancellationRequested();
        SetWorkbenchC3DSourceFromViewer(setup.SourcePath);
        if (!_viewModel.Workbench.TryApplyFirstRecipeStarter(setup.StarterId, out var starterMessage))
        {
            _callbacks.ShowFirstRecipeCreateFailure(starterMessage);
        }
    }

    public void BrowseFirstRecipeFolderRequested(object? sender, EventArgs args)
    {
        var current = _viewModel.Workbench.FirstRecipeFolderPath.Trim();
        if (_recipeFileDialogs.TrySelectFirstRecipeFolderPath(current, out var path))
        {
            _viewModel.Workbench.FirstRecipeFolderPath = path;
        }
    }

    public void BrowseFirstRecipeSourceRequested(object? sender, EventArgs args)
    {
        var source = _viewModel.Workbench.FirstRecipeSourcePath.Trim();
        var folder = _viewModel.Workbench.FirstRecipeFolderPath.Trim();
        if (_sourceFileDialogs.TrySelectFirstRecipeSourcePath(source, folder, out var path))
        {
            _viewModel.Workbench.FirstRecipeSourcePath = path;
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
            OpenWorkbenchRecipe(args.Path, args.OpenForInspection);
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
        => OpenWorkbenchRecipe(path, openForInspection: false);

    private void OpenWorkbenchRecipe(string path, bool openForInspection)
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

        var sourceApplication = _recipeSourceCoordinator.ApplyOpenedRecipeSource();
        if (!sourceApplication.IsReady)
        {
            if (sourceApplication.FailureMessage is null)
            {
                _callbacks.ShowRecipeSourceNotReady();
            }
            else
            {
                _callbacks.ShowRecipeSourceLoadFailure(sourceApplication.FailureMessage);
            }
            return;
        }

        if (openForInspection && _viewModel.Workbench.RunTeachingRecipeCommand.CanExecute(null))
        {
            _viewModel.SelectWorkspaceCommand.Execute(ShellWorkspaceMode.Inspect);
        }
    }

    public bool TryResolveWorkbenchChanges(string reason)
    {
        if (_viewModel.Workbench.HasPendingOrderedRunRecordSave)
        {
            _callbacks.ShowRecipeSaveFailure(_callbacks.DialogText("ThreeD.Dialog.PendingRunRecord", "검사 기록 저장에 실패한 계산 결과가 남아 있습니다. '기록 저장 재시도'로 저장한 뒤 다시 진행하세요.", "An inspection result is waiting for its Run Record to be saved. Retry saving the record before continuing."));
            return false;
        }
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
        HideRecipeManager();
        _callbacks.ActivateWorkbench();
    }

    public bool IsViewerSourceAlreadyLoaded(string path) =>
        _sourceLoadCoordinator.IsViewerSourceAlreadyLoaded(path);

    public void SetWorkbenchC3DSourceFromViewer(string path, bool markDirty = true) =>
        _sourceLoadCoordinator.SetWorkbenchC3DSourceFromViewer(path, markDirty);

    public void SyncWorkbenchSourceFromViewer() =>
        _sourceLoadCoordinator.SyncWorkbenchSourceFromViewer();

    public void Dispose()
    {
        if (Interlocked.Exchange(ref disposalState, 1) != 0)
        {
            return;
        }

        _viewModel.Workbench.FirstRecipeSetupRequested -= OnFirstRecipeSetupRequested;
        _sourceLoadCoordinator.Dispose();
        _recipeLifecycleRequestOwner.Dispose();
        var window = _recipeManagerWindow;
        _recipeManagerWindow = null;
        RestoreRecipeManagerOwnerState();
        if (window is not null)
        {
            window.Closed -= OnRecipeManagerWindowClosed;
            window.Closing -= OnRecipeManagerWindowClosing;
            window.CloseForOwner();
        }

        GC.SuppressFinalize(this);
    }

    private void OnRecipeManagerWindowClosed(object? sender, EventArgs args)
    {
        if (sender is RecipeManagerWindow window)
        {
            window.Closed -= OnRecipeManagerWindowClosed;
            window.Closing -= OnRecipeManagerWindowClosing;
        }

        if (ReferenceEquals(_recipeManagerWindow, sender))
        {
            _recipeManagerWindow = null;
            RestoreRecipeManagerOwnerState();
        }
    }

    private void OnRecipeManagerWindowClosing(object? sender, CancelEventArgs args)
    {
        if (ReferenceEquals(_recipeManagerWindow, sender) && args.Cancel)
        {
            RestoreRecipeManagerOwnerState();
        }
    }

    private void RestoreRecipeManagerOwnerState()
    {
        if (recipeManagerOwnerOpacityBeforeShow is { } opacity)
        {
            _owner.Opacity = opacity;
            recipeManagerOwnerOpacityBeforeShow = null;
        }

        if (recipeManagerOwnerEnabledBeforeShow is { } isEnabled)
        {
            _owner.IsEnabled = isEnabled;
            recipeManagerOwnerEnabledBeforeShow = null;
        }
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
