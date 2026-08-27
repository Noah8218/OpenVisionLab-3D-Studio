using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Microsoft.Win32;
using OpenVisionLab;
using OpenVisionLab.Integration.Contracts;
using OpenVisionLab.ThreeD.Presentation.Commands;
using OpenVisionLab.ThreeD.Reporting.Integration;

namespace OpenVisionLab.ThreeD.Shell.ViewModels.Integration;

public sealed record ThreeDIntegrationTransactionItem(
    Guid TransactionId,
    DateTimeOffset CreatedAtUtc,
    string ProjectId,
    string SequenceId,
    string StepId,
    string CameraId,
    string State)
{
    public bool IsThreeDHeightMap { get; init; }
    public bool IsAccepted { get; init; }
    public bool HasResult { get; init; }
    public bool CanRunHeightMap => IsThreeDHeightMap && IsAccepted && !HasResult;

    public string Title => $"{ProjectId} | {State}";
    public string Detail => $"{SequenceId} / {StepId} | {CameraId} | {CreatedAtUtc.ToLocalTime():yyyy-MM-dd HH:mm:ss}";
}

public sealed class ThreeDIntegrationViewModel : INotifyPropertyChanged
{
    private readonly Func<string?> runRecordPathProvider;
    private readonly string settingsPath;
    private readonly Func<IntegrationApplicationIdentity> producerIdentityProvider;
    private readonly Func<
        string,
        Guid,
        IntegrationApplicationIdentity,
        CancellationToken,
        Task<IntegrationResultV2>> heightMapRunner;
    private string exchangeRoot = string.Empty;
    private string rejectionReason = string.Empty;
    private string statusText = L("Restore", "설정을 복원했습니다. 폴더를 스캔하거나 작업을 실행하지 않았습니다.", "Setup restored. No folder was scanned and no action was run.");
    private string selectedSummary = L("ChooseRefresh", "새로고침을 눌러 Machine Studio Handoff를 찾으세요.", "Choose Refresh to find Machine Studio handoffs.");
    private string currentRunRecordSummary = L("NoRunRecord", "선택한 Run Record가 없습니다.", "No Run Record is selected.");
    private ThreeDIntegrationTransactionItem? selectedTransaction;
    private bool isHeightMapRunning;
    private bool isHeightMapShutdownRequested;
    private CancellationTokenSource? heightMapCancellation;
    private Task? heightMapRunTask;
    private TaskCompletionSource<bool>? heightMapRunStarted;

    public ThreeDIntegrationViewModel(
        Func<string?> runRecordPathProvider,
        string? settingsPath = null,
        Func<IntegrationApplicationIdentity>? producerIdentityProvider = null,
        Func<string, Guid, IntegrationApplicationIdentity, CancellationToken, Task<IntegrationResultV2>>? heightMapRunner = null)
    {
        this.runRecordPathProvider = runRecordPathProvider ?? throw new ArgumentNullException(nameof(runRecordPathProvider));
        this.settingsPath = settingsPath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "OpenVisionLab",
            "ThreeDStudio",
            "machine-exchange.json");
        this.producerIdentityProvider = producerIdentityProvider ?? CreateProducerIdentity;
        this.heightMapRunner = heightMapRunner ?? ((root, transactionId, consumerBuild, cancellationToken) =>
            Task.Run(
                () => ThreeDIntegrationHeightMapRunner.RunAcceptedHandoffFromRecipe(
                    root,
                    transactionId,
                    consumerBuild,
                    cancellationToken),
                cancellationToken));
        var settings = ExchangeSettings.Load(this.settingsPath);
        exchangeRoot = settings.ExchangeRoot;
        BrowseExchangeRootCommand = new RelayCommand(_ => BrowseExchangeRoot());
        SaveSetupCommand = new RelayCommand(_ => SaveSetup());
        ResetSetupCommand = new RelayCommand(_ => ResetSetup());
        RefreshHandoffsCommand = new RelayCommand(_ => RefreshHandoffs());
        AcceptCommand = new RelayCommand(_ => AcceptSelected(), _ => SelectedTransaction is not null);
        RejectCommand = new RelayCommand(_ => RejectSelected(), _ => SelectedTransaction is not null);
        RunHeightMapCommand = new RelayCommand(
            _ => RunHeightMap(),
            _ => SelectedTransaction?.CanRunHeightMap == true
                && !isHeightMapRunning
                && !isHeightMapShutdownRequested);
        PublishResultCommand = new RelayCommand(_ => PublishResult(), _ => SelectedTransaction is not null);
        SyncRunRecord();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<ThreeDIntegrationTransactionItem> Transactions { get; } = [];
    public RelayCommand BrowseExchangeRootCommand { get; }
    public RelayCommand SaveSetupCommand { get; }
    public RelayCommand ResetSetupCommand { get; }
    public RelayCommand RefreshHandoffsCommand { get; }
    public RelayCommand AcceptCommand { get; }
    public RelayCommand RejectCommand { get; }
    public RelayCommand RunHeightMapCommand { get; }
    public RelayCommand PublishResultCommand { get; }

    public string ExchangeRoot
    {
        get => exchangeRoot;
        set => SetField(ref exchangeRoot, value ?? string.Empty);
    }

    public string RejectionReason
    {
        get => rejectionReason;
        set => SetField(ref rejectionReason, value ?? string.Empty);
    }

    public string StatusText
    {
        get => statusText;
        private set => SetField(ref statusText, value);
    }

    public string SelectedSummary
    {
        get => selectedSummary;
        private set => SetField(ref selectedSummary, value);
    }

    public string CurrentRunRecordSummary
    {
        get => currentRunRecordSummary;
        private set => SetField(ref currentRunRecordSummary, value);
    }

    public ThreeDIntegrationTransactionItem? SelectedTransaction
    {
        get => selectedTransaction;
        set
        {
            if (!SetField(ref selectedTransaction, value))
            {
                return;
            }
            SelectedSummary = value is null
                ? L("NoSelection", "선택한 Machine Studio Handoff가 없습니다.", "No Machine Studio handoff is selected.")
                : string.Format(L("SelectedFormat", "거래 {0:D} | {1} | {2}", "Transaction {0:D} | {1} | {2}"), value.TransactionId, value.ProjectId, value.State);
            AcceptCommand.RaiseCanExecuteChanged();
            RejectCommand.RaiseCanExecuteChanged();
            RunHeightMapCommand.RaiseCanExecuteChanged();
            PublishResultCommand.RaiseCanExecuteChanged();
        }
    }

    public void SyncRunRecord()
    {
        var path = runRecordPathProvider();
        CurrentRunRecordSummary = string.IsNullOrWhiteSpace(path)
            ? L("NoRunRecordDetail", "선택한 Run Record가 없습니다. 게시 전에 Run Record를 열거나 실행을 완료하세요.", "No Run Record is selected. Open or complete a Run Record before publishing.")
            : string.Format(L("RunRecordFormat", "선택한 Run Record: {0}", "Selected Run Record: {0}"), path);
    }

    private void BrowseExchangeRoot()
    {
        var dialog = new OpenFolderDialog
        {
            Title = L(
                "ChooseFolder",
                "Machine Studio와 3D Studio가 공유할 교환 폴더 선택",
                "Choose the shared Machine Studio / 3D Studio exchange folder"),
            InitialDirectory = Directory.Exists(ExchangeRoot) ? ExchangeRoot : null
        };
        if (dialog.ShowDialog() == true)
        {
            ExchangeRoot = dialog.FolderName;
            StatusText = L("FolderSelected", "교환 폴더를 선택했습니다. 설정 저장을 눌러 기억하세요.", "Exchange folder selected. Choose Save setup to remember it.");
        }
    }

    private void SaveSetup()
    {
        try
        {
            var root = Path.GetFullPath(Require(
                ExchangeRoot,
                L("ChooseFolderFirst", "교환 폴더를 선택하세요.", "Choose an exchange folder.")));
            Directory.CreateDirectory(root);
            new ExchangeSettings { ExchangeRoot = root }.Save(settingsPath);
            ExchangeRoot = root;
            StatusText = L("Saved", "설정을 저장했습니다. 새로고침과 검토는 별도의 명시적 작업입니다.", "Setup saved. Refresh and review remain separate explicit actions.");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            StatusText = exception.Message;
        }
    }

    private void ResetSetup()
    {
        try
        {
            new ExchangeSettings().Save(settingsPath);
            ExchangeRoot = string.Empty;
            Transactions.Clear();
            SelectedTransaction = null;
            StatusText = L("Reset", "교환 설정을 초기화했습니다. Handoff 작업은 실행하지 않았습니다.", "Exchange setup reset. No handoff action was run.");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            StatusText = exception.Message;
        }
    }

    private void RefreshHandoffs(Guid? preferredTransactionId = null)
    {
        try
        {
            var root = RequireSavedRoot();
            var discovered = ThreeDIntegrationExchange.DiscoverHandoffs(root);
            Transactions.Clear();
            foreach (var transaction in discovered)
            {
                var acknowledgementStatus = transaction.HasAcknowledgement
                    ? ThreeDIntegrationExchange.ReadAcknowledgement(
                        root,
                        transaction.Handoff.TransactionId).Status
                    : (IntegrationAcknowledgementStatus?)null;
                var state = transaction.HasResult
                    ? L("StatePublished", "결과 게시됨", "Result published")
                    : acknowledgementStatus == IntegrationAcknowledgementStatus.Accepted
                        ? L("StateReviewed", "검토됨", "Reviewed")
                        : acknowledgementStatus == IntegrationAcknowledgementStatus.Rejected
                            ? L("StateRejected", "거절됨", "Rejected")
                        : L("StatePending", "검토 대기", "Pending review");
                Transactions.Add(new(
                    transaction.Handoff.TransactionId,
                    transaction.Handoff.CreatedAtUtc,
                    transaction.Handoff.Context.ProjectId,
                    transaction.Handoff.Context.SequenceId,
                    transaction.Handoff.Context.StepId,
                    transaction.Handoff.Context.CameraId,
                    state)
                {
                    IsThreeDHeightMap = transaction.Handoff.Context.Modality == IntegrationInspectionModality.ThreeD
                        && transaction.Handoff.Context.InputKind == IntegrationInspectionInputKind.HeightMap,
                    IsAccepted = acknowledgementStatus == IntegrationAcknowledgementStatus.Accepted,
                    HasResult = transaction.HasResult
                });
            }
            SelectedTransaction = Transactions.FirstOrDefault(item => item.TransactionId == preferredTransactionId)
                ?? Transactions.FirstOrDefault();
            StatusText = Transactions.Count == 0
                ? L("NoneFound", "Machine Studio Handoff를 찾지 못했습니다.", "No Machine Studio handoff was found.")
                : string.Format(L("FoundFormat", "Handoff {0}개를 찾았습니다. 활성 레시피에는 아무것도 불러오지 않았습니다.", "Found {0} handoff(s). Nothing was loaded into the active recipe."), Transactions.Count);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException or IntegrationContractException)
        {
            StatusText = exception.Message;
        }
    }

    private void AcceptSelected()
    {
        RunReviewAction(rejectionReason: null);
    }

    private void RejectSelected()
    {
        if (string.IsNullOrWhiteSpace(RejectionReason))
        {
            StatusText = L("ReasonRequired", "이 Handoff를 거절하기 전에 사유를 입력하세요.", "Enter a rejection reason before rejecting this handoff.");
            return;
        }
        RunReviewAction(RejectionReason.Trim());
    }

    private void RunReviewAction(string? rejectionReason)
    {
        try
        {
            var selected = SelectedTransaction
                ?? throw new InvalidOperationException("Choose a Machine Studio handoff first.");
            var root = RequireSavedRoot();
            var handoff = rejectionReason is null
                ? ThreeDIntegrationExchange.ReadHandoff(root, selected.TransactionId)
                : ThreeDIntegrationExchange.ReadHandoffEnvelope(root, selected.TransactionId);
            var acknowledgement = ThreeDIntegrationExchange.PublishAcknowledgement(
                root,
                handoff,
                producerIdentityProvider(),
                rejectionReason);
            RefreshHandoffs(selected.TransactionId);
            StatusText = acknowledgement.Status == IntegrationAcknowledgementStatus.Accepted
                ? L("Accepted", "검토를 위해 Handoff를 승인했습니다. 레시피를 불러오거나 검사를 실행하지 않았습니다.", "Handoff accepted for review. No recipe was loaded and no inspection was run.")
                : L("Rejected", "기록된 사유로 Handoff를 거절했습니다.", "Handoff rejected with the recorded reason.");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException or IntegrationContractException)
        {
            StatusText = exception.Message;
        }
    }

    private void PublishResult()
    {
        try
        {
            var selected = SelectedTransaction
                ?? throw new InvalidOperationException("Choose an accepted handoff first.");
            var runRecordPath = runRecordPathProvider();
            if (string.IsNullOrWhiteSpace(runRecordPath))
            {
                throw new InvalidOperationException("Select an existing completed Run Record before publishing a result.");
            }
            var result = ThreeDIntegrationExchange.PublishCompletedResult(
                RequireSavedRoot(),
                selected.TransactionId,
                producerIdentityProvider(),
                runRecordPath);
            RefreshHandoffs(selected.TransactionId);
            StatusText = string.Format(L("PublishedFormat", "결과 게시됨: {0} | Run {1}.", "Result published: {0} | Run {1}."), result.Outcome, result.RunId);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException or IntegrationContractException)
        {
            StatusText = exception.Message;
        }
    }

    public bool IsHeightMapRunning => isHeightMapRunning;

    internal async Task WaitForHeightMapRunAsync()
    {
        if (heightMapRunTask is null && !isHeightMapShutdownRequested)
        {
            if (heightMapRunStarted is { } runStarted)
            {
                await runStarted.Task;
            }
        }

        if (heightMapRunTask is { } activeTask)
        {
            await activeTask;
        }
    }

    /// <summary>
    /// Requests cooperative cancellation and waits at most <paramref name="timeout"/>
    /// for the active HeightMap operation. A false result means the work was
    /// detached after the bound; no UI state is applied after shutdown is requested.
    /// </summary>
    public async Task<bool> ShutdownAsync(TimeSpan timeout)
    {
        if (timeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(timeout), "Shutdown timeout must be positive.");
        }

        isHeightMapShutdownRequested = true;
        RunHeightMapCommand.RaiseCanExecuteChanged();
        heightMapCancellation?.Cancel();
        heightMapRunStarted?.TrySetResult(true);

        var activeTask = heightMapRunTask;
        if (activeTask is null || activeTask.IsCompleted)
        {
            return true;
        }

        try
        {
            await activeTask.WaitAsync(timeout);
            return true;
        }
        catch (TimeoutException)
        {
            return false;
        }
    }

    private void RunHeightMap()
    {
        if (isHeightMapRunning || isHeightMapShutdownRequested)
        {
            return;
        }

        try
        {
            var selected = SelectedTransaction
                ?? throw new InvalidOperationException("Choose an accepted HeightMap handoff first.");
            if (!selected.CanRunHeightMap)
            {
                throw new InvalidOperationException(
                    L(
                        "HeightMapRunUnavailable",
                        "승인된 ThreeD/HeightMap Handoff만 실행할 수 있습니다.",
                        "Only an accepted ThreeD/HeightMap handoff can run."));
            }

            var root = RequireSavedRoot();
            var consumerBuild = producerIdentityProvider();
            var cancellation = new CancellationTokenSource();
            var runStarted = new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            isHeightMapRunning = true;
            heightMapCancellation = cancellation;
            heightMapRunStarted = runStarted;
            heightMapRunTask = null;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsHeightMapRunning)));
            RunHeightMapCommand.RaiseCanExecuteChanged();
            StatusText = L(
                "HeightMapRunning",
                "레시피 설정으로 HeightMap 검사를 실행하는 중입니다.",
                "Running the HeightMap inspection from the recipe settings.");

            heightMapRunTask = RunHeightMapAsync(
                root,
                selected.TransactionId,
                consumerBuild,
                cancellation);
            runStarted.TrySetResult(true);
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException
            or ArgumentException
            or InvalidDataException
            or InvalidOperationException
            or IntegrationContractException)
        {
            StatusText = exception.Message;
            heightMapRunStarted?.TrySetResult(true);
        }
    }

    private async Task RunHeightMapAsync(
        string root,
        Guid transactionId,
        IntegrationApplicationIdentity consumerBuild,
        CancellationTokenSource cancellation)
    {
        try
        {
            var result = await heightMapRunner(
                root,
                transactionId,
                consumerBuild,
                cancellation.Token);
            if (isHeightMapShutdownRequested || cancellation.IsCancellationRequested)
            {
                return;
            }

            RefreshHandoffs(transactionId);
            StatusText = string.Format(
                L(
                    "HeightMapRunCompleted",
                    "HeightMap 검사 완료: {0} | Run {1}.",
                    "HeightMap inspection completed: {0} | Run {1}."),
                result.Outcome,
                result.RunId);
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested || isHeightMapShutdownRequested)
        {
            // Shutdown owns the visible state; do not replace it with a late cancellation message.
        }
        catch (Exception exception)
        {
            if (!isHeightMapShutdownRequested)
            {
                StatusText = exception.Message;
            }
        }
        finally
        {
            if (ReferenceEquals(heightMapCancellation, cancellation))
            {
                heightMapCancellation = null;
            }

            if (isHeightMapRunning)
            {
                isHeightMapRunning = false;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsHeightMapRunning)));
            }

            if (!isHeightMapShutdownRequested)
            {
                RunHeightMapCommand.RaiseCanExecuteChanged();
            }

            cancellation.Dispose();
        }
    }

    private string RequireSavedRoot()
    {
        var root = Path.GetFullPath(Require(
            ExchangeRoot,
            L(
                "ChooseAndSaveFolder",
                "교환 폴더를 선택하고 저장하세요.",
                "Choose and save an exchange folder.")));
        var settings = ExchangeSettings.Load(settingsPath);
        if (!string.Equals(settings.ExchangeRoot, root, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(L(
                "SaveCurrentFolder",
                "연동 작업을 실행하기 전에 현재 교환 폴더를 저장하세요.",
                "Save the current exchange folder before running an integration action."));
        }
        if (!Directory.Exists(root))
        {
            throw new DirectoryNotFoundException(L(
                "SavedFolderUnavailable",
                "저장한 교환 폴더를 사용할 수 없습니다. 폴더를 다시 선택하거나 만든 뒤 설정을 저장하세요.",
                "The saved exchange folder is unavailable. Choose or recreate it, then save setup again."));
        }
        return root;
    }

    private static IntegrationApplicationIdentity CreateProducerIdentity()
    {
        var sourceState = IntegrationBuildIdentity.SourceState.ToLowerInvariant() switch
        {
            "clean" => IntegrationSourceState.Clean,
            "dirty" => IntegrationSourceState.Dirty,
            _ => IntegrationSourceState.Unknown
        };
        return new(
            IntegrationApplicationIds.ThreeDStudio,
            IntegrationBuildIdentity.Version,
            IntegrationBuildIdentity.SourceCommit,
            sourceState);
    }

    private static string Require(string value, string message) =>
        string.IsNullOrWhiteSpace(value) ? throw new ArgumentException(message) : value.Trim();

    private static string L(string key, string korean, string english) =>
        OpenVisionLanguageService.CurrentLanguage == OpenVisionLanguage.English
            ? english
            : korean;

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string propertyName = "")
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        return true;
    }

    private sealed class ExchangeSettings
    {
        public string ExchangeRoot { get; set; } = string.Empty;

        public static ExchangeSettings Load(string path)
        {
            try
            {
                return File.Exists(path)
                    ? JsonSerializer.Deserialize<ExchangeSettings>(File.ReadAllText(path)) ?? new()
                    : new();
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
            {
                return new();
            }
        }

        public void Save(string path)
        {
            var fullPath = Path.GetFullPath(path);
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
            var temporary = $"{fullPath}.{Guid.NewGuid():N}.tmp";
            try
            {
                File.WriteAllText(temporary, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
                File.Move(temporary, fullPath, overwrite: true);
            }
            finally
            {
                if (File.Exists(temporary))
                {
                    File.Delete(temporary);
                }
            }
        }
    }
}
