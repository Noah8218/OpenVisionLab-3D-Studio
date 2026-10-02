using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Net;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Threading;
using OpenVisionLab;
using OpenVisionLab.Integration.Contracts;
using OpenVisionLab.Integration.Transport.Tcp;
using OpenVisionLab.ThreeD.Presentation.Commands;
using OpenVisionLab.ThreeD.Reporting.Integration;
using OpenVisionLab.ThreeD.Shell.Dialogs;

namespace OpenVisionLab.ThreeD.Shell.ViewModels.Integration;

public enum ThreeDIntegrationTcpOperationState
{
    Idle,
    Running,
    Cancelling,
    Succeeded,
    Cancelled,
    TimedOut,
    Failed,
}

public sealed class ThreeDIntegrationViewModel : INotifyPropertyChanged, IAsyncDisposable
{
    private readonly Func<string?> runRecordPathProvider;
    private readonly ThreeDIntegrationSettingsStore settingsStore;
    private readonly ThreeDIntegrationSharedKeySession sharedKeySession = new();
    private readonly ThreeDIntegrationTransactionWorkflow transactionWorkflow;
    private readonly Func<IntegrationApplicationIdentity>? producerIdentityProvider;
    private readonly TcpIntegrationOptions? tcpOptions;
    private readonly object tcpOperationGate = new();
    private ThreeDIntegrationTcpTransport? tcpListener;
    private ThreeDIntegrationCancellationCoordinator? tcpCancellationCoordinator;
    private CancellationTokenSource? tcpOperationCancellation;
    private Task? tcpOperationTask;
    private Task? disposalTask;
    private IThreeDIntegrationDialogHost? dialogHost;
    private int disposedState;
    private string exchangeRoot = string.Empty;
    private string tcpListenAddress = "127.0.0.1";
    private string tcpListenPortText = "45103";
    private string tcpPeerHost = "127.0.0.1";
    private string tcpPeerPortText = "45102";
    private bool isTcpListening;
    private bool isTcpBusy;
    private ThreeDIntegrationTcpOperationState tcpOperationState;
    private string tcpListenerStatusText = L("TcpStopped", "TCP 수신 중지됨", "TCP listener stopped");
    private string sharedKeyStatusText = string.Empty;
    private string lastTcpTransferText = L("NoTcpTransfer", "TCP 전송 기록이 없습니다.", "No TCP transfer has run.");
    private string rejectionReason = string.Empty;
    private string statusText = L("Restore", "설정을 복원했습니다. 폴더를 스캔하거나 작업을 실행하지 않았습니다.", "Setup restored. No folder was scanned and no action was run.");
    private string selectedSummary = L("ChooseRefresh", "새로고침을 눌러 Machine Studio 검사 요청을 찾으세요.", "Choose Refresh to find Machine Studio handoffs.");
    private string currentRunRecordSummary = L("NoRunRecord", "선택한 검사 기록이 없습니다.", "No Run Record is selected.");
    private string selectedInputArtifactPath = string.Empty;
    private string selectedInputArtifactId = string.Empty;
    private string selectedInputUnit = string.Empty;
    private string selectedInputFrameId = string.Empty;
    private ThreeDIntegrationTransactionItem? selectedTransaction;
    private bool isInspectionBusy;

    public ThreeDIntegrationViewModel(
        Func<string?> runRecordPathProvider,
        string? settingsPath = null)
    {
        this.runRecordPathProvider = runRecordPathProvider ?? throw new ArgumentNullException(nameof(runRecordPathProvider));
        var resolvedSettingsPath = settingsPath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "OpenVisionLab",
            "ThreeDStudio",
            "machine-exchange.json");
        settingsStore = new ThreeDIntegrationSettingsStore(resolvedSettingsPath);
        var settings = settingsStore.Load();
        exchangeRoot = settings.ExchangeRoot;
        tcpListenAddress = settings.TcpListenAddress;
        tcpListenPortText = settings.TcpListenPort.ToString(CultureInfo.InvariantCulture);
        tcpPeerHost = settings.TcpPeerHost;
        tcpPeerPortText = settings.TcpPeerPort.ToString(CultureInfo.InvariantCulture);
        transactionWorkflow = new(
            ResolveProducerIdentity,
            L);
        sharedKeyStatusText = DescribeSharedKeyStatus();
        BrowseExchangeRootCommand = new RelayCommand(_ => BrowseExchangeRoot(), _ => CanEditTcpSetup);
        SaveSetupCommand = new RelayCommand(_ => SaveSetup(), _ => CanEditTcpSetup);
        ResetSetupCommand = new RelayCommand(_ => ResetSetup(), _ => CanEditTcpSetup);
        SetSessionSharedKeyCommand = new RelayCommand(parameter => SetSessionSharedKey(parameter as string));
        RefreshHandoffsCommand = new RelayCommand(_ => RefreshHandoffs(), _ => !IsTcpBusy);
        AcceptCommand = new RelayCommand(_ => AcceptSelected(), _ => CanReviewSelected);
        RejectCommand = new RelayCommand(_ => RejectSelected(), _ => CanReviewSelected);
        RunInspectionCommand = new RelayCommand(_ => RunInspection(), _ => CanRunInspection);
        PublishResultCommand = new RelayCommand(_ => PublishResult(), _ => CanPublishSelectedResult);
        StartTcpListenerCommand = new RelayCommand(
            async _ => await StartTcpListenerAsync(),
            _ => !IsTcpBusy && !IsTcpListening);
        StopTcpListenerCommand = new RelayCommand(
            async _ => await StopTcpListenerAsync(),
            _ => !IsTcpBusy && IsTcpListening);
        PingTcpPeerCommand = new RelayCommand(
            async _ => await PingTcpPeerAsync(),
            _ => !IsTcpBusy);
        PushSelectedTransactionCommand = new RelayCommand(
            async _ => await PushSelectedTransactionAsync(),
            _ => !IsTcpBusy && SelectedTransaction is not null);
        PullSelectedTransactionCommand = new RelayCommand(
            async _ => await PullSelectedTransactionAsync(),
            _ => !IsTcpBusy && SelectedTransaction is not null);
        CancelTcpOperationCommand = new RelayCommand(
            _ => CancelTcpOperation(),
            _ => CanCancelTcpOperation);
        SyncRunRecord();
    }

    internal ThreeDIntegrationViewModel(
        Func<string?> runRecordPathProvider,
        string settingsPath,
        Func<IntegrationApplicationIdentity> producerIdentityProvider,
        TcpIntegrationOptions? tcpOptions = null)
        : this(runRecordPathProvider, settingsPath)
    {
        this.producerIdentityProvider = producerIdentityProvider
            ?? throw new ArgumentNullException(nameof(producerIdentityProvider));
        this.tcpOptions = tcpOptions;
    }

    internal void SetDialogHost(IThreeDIntegrationDialogHost host) =>
        dialogHost = host ?? throw new ArgumentNullException(nameof(host));

    public event PropertyChangedEventHandler? PropertyChanged;

    internal bool IsDisposed => Volatile.Read(ref disposedState) != 0;

    // Verification-only delay used by the disposable cross-process smoke host.
    // It keeps the cancellation coordinator's active-run window open without
    // changing the normal inspection workflow.
    internal int SmokePreInspectionDelayMilliseconds { get; set; }
    internal string? SmokePreInspectionReadyFilePath { get; set; }
    internal bool SmokeForceExecutionError { get; set; }

    public ObservableCollection<ThreeDIntegrationTransactionItem> Transactions { get; } = [];
    public RelayCommand BrowseExchangeRootCommand { get; }
    public RelayCommand SaveSetupCommand { get; }
    public RelayCommand ResetSetupCommand { get; }
    public RelayCommand SetSessionSharedKeyCommand { get; }
    public RelayCommand RefreshHandoffsCommand { get; }
    public RelayCommand AcceptCommand { get; }
    public RelayCommand RejectCommand { get; }
    public RelayCommand RunInspectionCommand { get; }
    public RelayCommand PublishResultCommand { get; }
    public RelayCommand StartTcpListenerCommand { get; }
    public RelayCommand StopTcpListenerCommand { get; }
    public RelayCommand PingTcpPeerCommand { get; }
    public RelayCommand PushSelectedTransactionCommand { get; }
    public RelayCommand PullSelectedTransactionCommand { get; }
    public RelayCommand CancelTcpOperationCommand { get; }

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

    public string TcpListenAddress
    {
        get => tcpListenAddress;
        set => SetField(ref tcpListenAddress, value ?? string.Empty);
    }

    public string TcpListenPortText
    {
        get => tcpListenPortText;
        set => SetField(ref tcpListenPortText, value ?? string.Empty);
    }

    public string TcpPeerHost
    {
        get => tcpPeerHost;
        set => SetField(ref tcpPeerHost, value ?? string.Empty);
    }

    public string TcpPeerPortText
    {
        get => tcpPeerPortText;
        set => SetField(ref tcpPeerPortText, value ?? string.Empty);
    }

    public bool IsTcpListening
    {
        get => isTcpListening;
        private set
        {
            if (SetField(ref isTcpListening, value))
            {
                RaiseTcpCanExecuteChanged();
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CanEditTcpSetup)));
            }
        }
    }

    public bool IsTcpBusy
    {
        get => isTcpBusy;
        private set
        {
            if (SetField(ref isTcpBusy, value))
            {
                RaiseTcpCanExecuteChanged();
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CanEditTcpSetup)));
            }
        }
    }

    public bool CanEditTcpSetup => !IsTcpListening && !IsTcpBusy;

    public ThreeDIntegrationTcpOperationState TcpOperationState
    {
        get => tcpOperationState;
        private set
        {
            if (SetField(ref tcpOperationState, value))
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(TcpOperationStateText)));
            }
        }
    }

    public string TcpOperationStateText => TcpOperationState switch
    {
        ThreeDIntegrationTcpOperationState.Idle => L("TcpStateIdle", "대기", "Idle"),
        ThreeDIntegrationTcpOperationState.Running => L("TcpStateRunning", "실행 중", "Running"),
        ThreeDIntegrationTcpOperationState.Cancelling => L("TcpStateCancelling", "취소 중", "Cancelling"),
        ThreeDIntegrationTcpOperationState.Succeeded => L("TcpStateSucceeded", "완료", "Succeeded"),
        ThreeDIntegrationTcpOperationState.Cancelled => L("TcpStateCancelled", "취소됨", "Cancelled"),
        ThreeDIntegrationTcpOperationState.TimedOut => L("TcpStateTimedOut", "시간 초과", "Timed out"),
        ThreeDIntegrationTcpOperationState.Failed => L("TcpStateFailed", "실패", "Failed"),
        _ => TcpOperationState.ToString(),
    };

    public string TcpListenerStatusText
    {
        get => tcpListenerStatusText;
        private set => SetField(ref tcpListenerStatusText, value);
    }

    public string SharedKeyStatusText
    {
        get => sharedKeyStatusText;
        private set => SetField(ref sharedKeyStatusText, value);
    }

    public string LastTcpTransferText
    {
        get => lastTcpTransferText;
        private set => SetField(ref lastTcpTransferText, value);
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

    public string SelectedInputArtifactPath
    {
        get => selectedInputArtifactPath;
        private set => SetField(ref selectedInputArtifactPath, value ?? string.Empty);
    }

    public string SelectedInputArtifactId
    {
        get => selectedInputArtifactId;
        private set => SetField(ref selectedInputArtifactId, value ?? string.Empty);
    }

    public string SelectedInputUnit
    {
        get => selectedInputUnit;
        private set => SetField(ref selectedInputUnit, value ?? string.Empty);
    }

    public string SelectedInputFrameId
    {
        get => selectedInputFrameId;
        private set => SetField(ref selectedInputFrameId, value ?? string.Empty);
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
            RefreshSelectedInputArtifact();
            SelectedSummary = value is null
                ? L("NoSelection", "선택한 Machine Studio 검사 요청이 없습니다.", "No Machine Studio handoff is selected.")
                : string.Format(
                    L(
                        "SelectedFormat",
                        "항목 {0:D} | {1} | {2} | {3} | {4}",
                        "Transaction {0:D} | {1} | {2} | {3} | {4}"),
                    value.TransactionId,
                    value.ProjectId,
                    value.State,
                    value.AcknowledgementSummary,
                    value.ResultSummary);
            AcceptCommand.RaiseCanExecuteChanged();
            RejectCommand.RaiseCanExecuteChanged();
            RunInspectionCommand.RaiseCanExecuteChanged();
            PublishResultCommand.RaiseCanExecuteChanged();
            PushSelectedTransactionCommand.RaiseCanExecuteChanged();
            PullSelectedTransactionCommand.RaiseCanExecuteChanged();
        }
    }

    private bool CanReviewSelected =>
        !IsTcpBusy
        && SelectedTransaction is { CanInspectInThreeD: true, HasAcknowledgement: false, HasResult: false };

    private void RefreshSelectedInputArtifact()
    {
        // Clear the path first so the host cancels an in-flight load before any
        // new metadata is published. The integration viewer never mutates the
        // active recipe/source state; it only displays the selected transaction.
        SelectedInputArtifactPath = string.Empty;
        SelectedInputArtifactId = string.Empty;
        SelectedInputUnit = string.Empty;
        SelectedInputFrameId = string.Empty;

        if (SelectedTransaction is not { CanInspectInThreeD: true }
            || SelectedTransaction.SchemaVersion != IntegrationContractSchema.V2
            || string.IsNullOrWhiteSpace(ExchangeRoot))
        {
            return;
        }

        try
        {
            var root = Path.GetFullPath(ExchangeRoot);
            var handoff = ThreeDIntegrationV2Exchange.ReadHandoff(
                root,
                SelectedTransaction.TransactionId);
            var source = handoff.Context.Artifacts.SingleOrDefault(artifact =>
                string.Equals(
                    artifact.Role,
                    IntegrationArtifactRoles.InspectionSource,
                    StringComparison.Ordinal));
            if (source is null)
            {
                return;
            }

            var transactionRoot = Path.Combine(
                root,
                IntegrationTransactionLayout.TransactionsDirectoryName,
                SelectedTransaction.TransactionId.ToString("D"));
            var fullRoot = Path.GetFullPath(transactionRoot)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                + Path.DirectorySeparatorChar;
            var fullPath = Path.GetFullPath(Path.Combine(
                transactionRoot,
                source.RelativePath.Replace('/', Path.DirectorySeparatorChar)));
            if (!fullPath.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase)
                || !File.Exists(fullPath))
            {
                return;
            }

            SelectedInputArtifactId = string.IsNullOrWhiteSpace(source.ArtifactId)
                ? "integration-source"
                : source.ArtifactId;
            SelectedInputUnit = handoff.Context.Unit;
            SelectedInputFrameId = handoff.Context.FrameId;
            // Publish the path last so a consumer observing this property sees
            // a complete metadata tuple for one transaction.
            SelectedInputArtifactPath = fullPath;
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException
            or ArgumentException
            or InvalidOperationException
            or IntegrationContractException)
        {
            // Discovery and selection must remain non-destructive. The existing
            // status projection reports malformed/unavailable transactions.
        }
    }

    private bool CanPublishSelectedResult =>
        !IsTcpBusy
        && SelectedTransaction is
        {
            CanInspectInThreeD: true,
            HasAcknowledgement: true,
            HasResult: false,
            AcknowledgementStatus: IntegrationAcknowledgementStatus.Accepted
        };

    private bool CanRunInspection =>
        !IsTcpBusy
        && !isInspectionBusy
        && SelectedTransaction is
        {
            CanInspectInThreeD: true,
            HasAcknowledgement: true,
            HasResult: false,
            AcknowledgementStatus: IntegrationAcknowledgementStatus.Accepted
        };

    public void SyncRunRecord()
    {
        var path = runRecordPathProvider();
        CurrentRunRecordSummary = string.IsNullOrWhiteSpace(path)
            ? L("NoRunRecordDetail", "선택한 검사 기록이 없습니다. 전송할 결과를 준비하려면 검사 기록을 열거나 검사를 완료하세요.", "No Run Record is selected. Open or complete a Run Record before publishing.")
            : string.Format(L("RunRecordFormat", "선택한 검사 기록: {0}", "Selected Run Record: {0}"), path);
    }

    public void SetSessionSharedKey(string? encodedKey)
    {
        var status = sharedKeySession.SetSession(encodedKey);
        if (status == ThreeDIntegrationSharedKeyInputStatus.NotConfigured)
        {
            SharedKeyStatusText = DescribeSharedKeyStatus();
            return;
        }

        SharedKeyStatusText = status switch
        {
            ThreeDIntegrationSharedKeyInputStatus.Ready => L(
                "TcpSessionKeyReady",
                "세션 공유 키 준비됨(저장되지 않음)",
                "Session shared key ready (not saved)"),
            ThreeDIntegrationSharedKeyInputStatus.TooShort => L(
                "TcpKeyTooShort",
                "세션 공유 키는 Base64로 인코딩한 32바이트 이상이어야 합니다.",
                "The session shared key must be Base64-encoded and contain at least 32 bytes."),
            ThreeDIntegrationSharedKeyInputStatus.InvalidBase64 => L(
                "TcpKeyInvalidBase64",
                "세션 공유 키가 올바른 Base64가 아닙니다.",
                "The session shared key is not valid Base64."),
            ThreeDIntegrationSharedKeyInputStatus.Disposed => L(
                "TcpDisposed",
                "종료 중인 연동 화면에서는 TCP 작업을 시작할 수 없습니다.",
                "A TCP action cannot start while the integration workspace is closing."),
            _ => DescribeSharedKeyStatus()
        };
    }

    private void BrowseExchangeRoot()
    {
        if (dialogHost is null)
        {
            StatusText = L(
                "FolderPickerUnavailable",
                "폴더 선택 창을 사용할 수 없습니다.",
                "The folder picker is unavailable.");
            return;
        }

        var title = L(
            "ChooseFolder",
            "Machine Studio와 3D Studio가 공유할 교환 폴더 선택",
            "Choose the shared Machine Studio / 3D Studio exchange folder");
        var initialDirectory = Directory.Exists(ExchangeRoot) ? ExchangeRoot : null;
        if (dialogHost.TrySelectExchangeRoot(initialDirectory, title, out var path))
        {
            ExchangeRoot = path;
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
            var tcp = ResolveCurrentTcpSettings(root);
            Directory.CreateDirectory(root);
            settingsStore.Save(new ThreeDIntegrationSettings
            {
                ExchangeRoot = root,
                TcpListenAddress = tcp.ListenAddress.ToString(),
                TcpListenPort = tcp.ListenPort,
                TcpPeerHost = tcp.PeerHost,
                TcpPeerPort = tcp.PeerPort
            });
            ExchangeRoot = root;
            TcpListenAddress = tcp.ListenAddress.ToString();
            TcpListenPortText = tcp.ListenPort.ToString(CultureInfo.InvariantCulture);
            TcpPeerHost = tcp.PeerHost;
            TcpPeerPortText = tcp.PeerPort.ToString(CultureInfo.InvariantCulture);
            StatusText = L(
                "Saved",
                "교환 폴더와 TCP 주소를 저장했습니다. 공유 키는 저장하지 않으며 수신 시작, 새로고침, 검토는 각각 별도의 작업입니다.",
                "Exchange folder and TCP endpoints saved. The shared key is not saved; listening, refresh, and review remain separate explicit actions.");
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
            settingsStore.Save(new ThreeDIntegrationSettings());
            ExchangeRoot = string.Empty;
            TcpListenAddress = "127.0.0.1";
            TcpListenPortText = "45103";
            TcpPeerHost = "127.0.0.1";
            TcpPeerPortText = "45102";
            SetSessionSharedKey(null);
            Transactions.Clear();
            SelectedTransaction = null;
            LastTcpTransferText = L("NoTcpTransfer", "TCP 전송 기록이 없습니다.", "No TCP transfer has run.");
            StatusText = L("Reset", "교환 설정을 초기화했습니다. 네트워크 또는 검사 요청 작업은 실행하지 않았습니다.", "Exchange setup reset. No network or handoff action was run.");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            StatusText = exception.Message;
        }
    }

    private bool RefreshHandoffs(Guid? preferredTransactionId = null)
    {
        try
        {
            var items = transactionWorkflow.Discover(RequireSavedRoot());
            Transactions.Clear();
            foreach (var item in items)
            {
                Transactions.Add(item);
            }
            SelectedTransaction = Transactions.FirstOrDefault(item => item.TransactionId == preferredTransactionId)
                ?? Transactions.FirstOrDefault();
            StatusText = Transactions.Count == 0
                ? L("NoneFound", "Machine Studio 검사 요청을 찾지 못했습니다.", "No Machine Studio handoff was found.")
                : string.Format(L("FoundFormat", "검사 요청 {0}개를 찾았습니다. 활성 레시피에는 아무것도 불러오지 않았습니다.", "Found {0} handoff(s). Nothing was loaded into the active recipe."), Transactions.Count);
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException or IntegrationContractException)
        {
            StatusText = exception.Message;
            return false;
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
            StatusText = L("ReasonRequired", "이 검사 요청을 거절하기 전에 사유를 입력하세요.", "Enter a rejection reason before rejecting this handoff.");
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
            var status = transactionWorkflow.Review(root, selected, rejectionReason);
            RefreshHandoffs(selected.TransactionId);
            StatusText = status == IntegrationAcknowledgementStatus.Accepted
                ? L("Accepted", "승인 응답을 저장했습니다. 레시피를 불러오거나 검사를 실행하지 않았습니다. 상대에게 보내려면 선택 항목 전송을 누르세요.", "ACK recorded in the local transaction. No recipe was loaded and no inspection was run. Choose Push selected transaction to return it to the TCP peer.")
                : L("Rejected", "거부 응답을 저장했습니다. 상대에게 보내려면 선택 항목 전송을 누르세요.", "Rejected ACK recorded in the local transaction. Choose Push selected transaction to return it to the TCP peer.");
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
            var published = transactionWorkflow.PublishResult(RequireSavedRoot(), selected, runRecordPath);
            RefreshHandoffs(selected.TransactionId);
            StatusText = string.Format(
                L(
                    "PublishedFormat",
                    "결과 준비됨: {0} | 검사 {1}. 상대에게 보내려면 선택 항목 전송을 누르세요.",
                    "Result prepared: {0} | Run {1}. Choose Push selected transaction to return it to the TCP peer."),
                published.Outcome,
                published.RunId);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException or IntegrationContractException)
        {
            StatusText = exception.Message;
        }
    }

    private void RunInspection()
    {
        if (!CanRunInspection)
        {
            return;
        }

        isInspectionBusy = true;
        RunInspectionCommand.RaiseCanExecuteChanged();
        try
        {
            var selected = SelectedTransaction
                ?? throw new InvalidOperationException("Choose an accepted ThreeD handoff first.");
            var root = RequireSavedRoot();
            var cancellationCoordinator = tcpCancellationCoordinator;
            var published = cancellationCoordinator is null
                ? transactionWorkflow.RunInspection(
                    root,
                    selected,
                    CancellationToken.None,
                    SmokeForceExecutionError)
                : cancellationCoordinator.RunWithCancellation(
                    selected.TransactionId,
                    cancellationToken =>
                    {
                        if (SmokePreInspectionDelayMilliseconds > 0)
                        {
                            if (SmokePreInspectionReadyFilePath is { Length: > 0 } readyPath)
                            {
                                var fullReadyPath = Path.GetFullPath(readyPath);
                                Directory.CreateDirectory(Path.GetDirectoryName(fullReadyPath)!);
                                File.WriteAllText(fullReadyPath, selected.TransactionId.ToString("D"));
                            }

                            Task.Delay(
                                    SmokePreInspectionDelayMilliseconds,
                                    cancellationToken)
                                .GetAwaiter()
                                .GetResult();
                        }

                        return transactionWorkflow.RunInspection(
                            root,
                            selected,
                            cancellationToken,
                            SmokeForceExecutionError);
                    },
                    cancelledResult => new ThreeDIntegrationPublishedResult(
                        cancelledResult.Outcome.ToString(),
                        cancelledResult.RunId));
            RefreshHandoffs(selected.TransactionId);
            StatusText = string.Equals(
                    published.Outcome,
                    IntegrationInspectionOutcome.ExecutionError.ToString(),
                    StringComparison.Ordinal)
                ? L(
                    "InspectionFailed",
                    "3D 검사 실행 오류가 기록되었습니다. Machine Studio에 오류 결과를 보내려면 선택 항목 전송을 누르세요.",
                    "A 3D inspection execution error was recorded. Push the Failed/ExecutionError Result to Machine Studio from the selected transaction.")
                : published.RunId is null
                ? L(
                    "InspectionCancelled",
                    "3D 검사가 취소되었습니다. Machine Studio에 취소 결과를 보내려면 선택 항목 전송을 누르세요.",
                    "3D inspection was cancelled. Push the selected transaction to return the cancelled Result to Machine Studio.")
                : string.Format(
                    L(
                        "InspectionCompletedFormat",
                        "3D 검사 완료: {0} | 검사 {1}. Machine Studio에 보내려면 선택 항목 전송을 누르세요.",
                        "3D inspection completed: {0} | Run {1}. Choose Push selected transaction to return the result to Machine Studio."),
                    published.Outcome,
                    published.RunId);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException or IntegrationContractException)
        {
            StatusText = exception.Message;
        }
        finally
        {
            isInspectionBusy = false;
            RunInspectionCommand.RaiseCanExecuteChanged();
            PublishResultCommand.RaiseCanExecuteChanged();
        }
    }

    internal Task StartTcpListenerAsync() => RunTcpOperationAsync(
        L("TcpStarting", "TCP 수신을 시작하는 중입니다.", "Starting TCP listener."),
        async cancellationToken =>
        {
            if (tcpListener is not null)
            {
                throw new InvalidOperationException(L(
                    "TcpAlreadyStarted",
                    "TCP 수신기가 이미 실행 중입니다.",
                    "The TCP listener is already running."));
            }

            var settings = RequireSavedTcpSettings();
            var key = AcquireSharedKey();
            ThreeDIntegrationTcpTransport? listener = null;
            ThreeDIntegrationCancellationCoordinator? cancellationCoordinator = null;
            try
            {
                listener = new ThreeDIntegrationTcpTransport(settings.ExchangeRoot, key, tcpOptions);
                cancellationCoordinator = new ThreeDIntegrationCancellationCoordinator(
                    settings.ExchangeRoot,
                    ResolveProducerIdentity());
                listener.CancellationRequested += cancellationCoordinator.HandleCancellationRequestedAsync;
                var endpoint = await listener.StartListeningAsync(
                    settings.ListenAddress,
                    settings.ListenPort,
                    cancellationToken);
                lock (tcpOperationGate)
                {
                    if (IsDisposed || cancellationToken.IsCancellationRequested)
                    {
                        return;
                    }

                    tcpListener = listener;
                    listener = null;
                    tcpCancellationCoordinator = cancellationCoordinator;
                    cancellationCoordinator = null;
                    IsTcpListening = true;
                    TcpListenerStatusText = string.Format(
                        L("TcpListeningFormat", "TCP 수신 중: {0}", "TCP listening: {0}"),
                        endpoint);
                    StatusText = L(
                        "TcpStarted",
                        "TCP 수신을 시작했습니다. 요청을 받아도 검토 응답, 레시피 열기, 미리보기, 결과 적용, 검사 또는 결과 저장은 자동으로 실행되지 않습니다.",
                        "TCP listening started. Receipt alone never ACKs, loads a recipe, Previews, Publishes, Runs, or creates a Result.");
                }
            }
            finally
            {
                CryptographicOperations.ZeroMemory(key);
                if (listener is not null)
                {
                    await listener.DisposeAsync();
                }
                cancellationCoordinator?.Dispose();
            }
        });

    internal Task StopTcpListenerAsync() => RunTcpOperationAsync(
        L("TcpStopping", "TCP 수신을 중지하는 중입니다.", "Stopping TCP listener."),
        async _ =>
        {
            var listener = tcpListener;
            tcpListener = null;
            var cancellationCoordinator = tcpCancellationCoordinator;
            tcpCancellationCoordinator = null;
            try
            {
                if (listener is not null)
                {
                    await listener.DisposeAsync();
                }
            }
            finally
            {
                cancellationCoordinator?.Dispose();
                IsTcpListening = false;
                TcpListenerStatusText = L(
                    "TcpStopped",
                    "TCP 수신 중지됨",
                    "TCP listener stopped");
            }
            StatusText = L(
                "TcpStoppedStatus",
                "TCP 수신을 중지했습니다.",
                "TCP listening stopped.");
        });

    internal Task PingTcpPeerAsync() => RunTcpTransferAsync(
        L("TcpPinging", "TCP 상대를 확인하는 중입니다.", "Pinging TCP peer."),
        (exchange, endpoint, cancellationToken) =>
            exchange.PingAsync(endpoint, cancellationToken),
        refreshTransactionId: null);

    internal Task PushSelectedTransactionAsync()
    {
        var selected = SelectedTransaction;
        if (selected is null)
        {
            StatusText = L(
                "ChooseTransferTransaction",
                "보낼 항목을 먼저 선택하세요.",
                "Choose a transaction to push first.");
            return Task.CompletedTask;
        }
        return RunTcpTransferAsync(
            L("TcpPushing", "선택 항목을 보내는 중입니다.", "Pushing selected transaction."),
            (exchange, endpoint, cancellationToken) =>
                exchange.PushTransactionAsync(
                    endpoint,
                    selected.TransactionId,
                    cancellationToken),
            refreshTransactionId: null);
    }

    internal Task PullSelectedTransactionAsync()
    {
        var selected = SelectedTransaction;
        if (selected is null)
        {
            StatusText = L(
                "ChooseTransferTransaction",
                "받을 항목을 먼저 선택하세요.",
                "Choose a transaction to pull first.");
            return Task.CompletedTask;
        }
        return RunTcpTransferAsync(
            L("TcpPulling", "선택 항목을 받는 중입니다.", "Pulling selected transaction."),
            (exchange, endpoint, cancellationToken) =>
                exchange.PullTransactionAsync(
                    endpoint,
                    selected.TransactionId,
                    cancellationToken),
            selected.TransactionId);
    }

    private Task RunTcpTransferAsync(
        string busyStatus,
        Func<
            ThreeDIntegrationTcpTransport,
            TcpIntegrationEndpoint,
            CancellationToken,
            Task<TcpIntegrationTransferReceipt>> operation,
        Guid? refreshTransactionId) =>
        RunTcpOperationAsync(
            busyStatus,
            async cancellationToken =>
            {
                var settings = RequireSavedTcpSettings();
                var key = AcquireSharedKey();
                try
                {
                    await using var exchange = new ThreeDIntegrationTcpTransport(
                        settings.ExchangeRoot,
                        key,
                        tcpOptions);
                    var receipt = await operation(
                        exchange,
                        new TcpIntegrationEndpoint(settings.PeerHost, settings.PeerPort),
                        cancellationToken);
                    LastTcpTransferText = string.Format(
                        CultureInfo.CurrentCulture,
                        L(
                            "TcpTransferFormat",
                            "{0} 완료 | 상대 {1} | 항목 {2} | 파일 {3} | 바이트 {4:N0} | 중복 전송 {5}",
                            "{0} complete | peer {1} | transaction {2} | files {3} | bytes {4:N0} | idempotent {5}"),
                        receipt.Operation,
                        receipt.PeerApplicationId,
                        receipt.TransactionId?.ToString("D") ?? "-",
                        receipt.FilesTransferred,
                        receipt.BytesTransferred,
                        receipt.Idempotent);
                    if (refreshTransactionId is Guid transactionId)
                    {
                        if (!RefreshHandoffs(transactionId))
                        {
                            throw new InvalidDataException(StatusText);
                        }
                    }
                    StatusText = LastTcpTransferText;
                }
                finally
                {
                    CryptographicOperations.ZeroMemory(key);
                }
            });

    private Task RunTcpOperationAsync(
        string busyStatus,
        Func<CancellationToken, Task> operation)
    {
        CancellationTokenSource cancellation;
        TaskCompletionSource<object?> completion;
        lock (tcpOperationGate)
        {
            if (IsDisposed)
            {
                StatusText = L(
                    "TcpDisposed",
                    "종료 중인 연동 화면에서는 TCP 작업을 시작할 수 없습니다.",
                    "A TCP action cannot start while the integration workspace is closing.");
                return Task.CompletedTask;
            }
            if (tcpOperationTask is not null || IsTcpBusy)
            {
                return Task.CompletedTask;
            }

            cancellation = new CancellationTokenSource();
            completion = new TaskCompletionSource<object?>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            tcpOperationCancellation = cancellation;
            tcpOperationTask = completion.Task;
            TcpOperationState = ThreeDIntegrationTcpOperationState.Running;
            StatusText = busyStatus;
            IsTcpBusy = true;
        }

        return RunTcpOperationAndCompleteAsync(operation, cancellation, completion);
    }

    private async Task RunTcpOperationAndCompleteAsync(
        Func<CancellationToken, Task> operation,
        CancellationTokenSource cancellation,
        TaskCompletionSource<object?> completion)
    {
        try
        {
            await RunTcpOperationCoreAsync(operation, cancellation, completion).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            completion.TrySetException(exception);
        }

        await completion.Task.ConfigureAwait(false);
    }

    private async Task RunTcpOperationCoreAsync(
        Func<CancellationToken, Task> operation,
        CancellationTokenSource cancellation,
        TaskCompletionSource<object?> completion)
    {
        try
        {
            await operation(cancellation.Token);
            TcpOperationState = ThreeDIntegrationTcpOperationState.Succeeded;
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            TcpOperationState = ThreeDIntegrationTcpOperationState.Cancelled;
            StatusText = L(
                "TcpCancelled",
                "TCP 작업을 취소했습니다.",
                "TCP action cancelled.");
        }
        catch (OperationCanceledException)
        {
            TcpOperationState = ThreeDIntegrationTcpOperationState.TimedOut;
            StatusText = L(
                "TcpTimedOut",
                "TCP 작업 시간이 초과되었습니다. 주소와 상대 상태를 확인한 뒤 다시 시도하세요.",
                "TCP action timed out. Check the endpoint and peer, then try again.");
        }
        catch (TimeoutException)
        {
            TcpOperationState = ThreeDIntegrationTcpOperationState.TimedOut;
            StatusText = L(
                "TcpTimedOut",
                "TCP 작업 시간이 초과되었습니다. 주소와 상대 상태를 확인한 뒤 다시 시도하세요.",
                "TCP action timed out. Check the endpoint and peer, then try again.");
        }
        catch (Exception exception)
        {
            TcpOperationState = ThreeDIntegrationTcpOperationState.Failed;
            StatusText = string.Format(
                CultureInfo.CurrentCulture,
                L(
                    "TcpFailedFormat",
                    "TCP 작업 실패: {0} 주소와 설정을 확인한 뒤 다시 시도하세요.",
                    "TCP action failed: {0} Check the endpoint and setup, then try again."),
                exception.Message);
        }
        finally
        {
            lock (tcpOperationGate)
            {
                if (ReferenceEquals(tcpOperationCancellation, cancellation))
                {
                    tcpOperationCancellation = null;
                    tcpOperationTask = null;
                    IsTcpBusy = false;
                }
            }
            cancellation.Dispose();
            completion.TrySetResult(null);
        }
    }

    private bool CanCancelTcpOperation
    {
        get
        {
            lock (tcpOperationGate)
            {
                return !IsDisposed
                    && IsTcpBusy
                    && tcpOperationCancellation is { IsCancellationRequested: false };
            }
        }
    }

    private void CancelTcpOperation()
    {
        lock (tcpOperationGate)
        {
            if (!CanCancelTcpOperation || tcpOperationCancellation is null)
            {
                return;
            }

            TcpOperationState = ThreeDIntegrationTcpOperationState.Cancelling;
            StatusText = L(
                "TcpCancelling",
                "현재 TCP 작업을 취소하는 중입니다.",
                "Cancelling the current TCP action.");
            tcpOperationCancellation.Cancel();
            CancelTcpOperationCommand.RaiseCanExecuteChanged();
        }
    }

    public async ValueTask DisposeAsync()
    {
        Task cleanupTask;
        TaskCompletionSource<object?>? completion = null;
        lock (tcpOperationGate)
        {
            if (disposalTask is null)
            {
                completion = new TaskCompletionSource<object?>(
                    TaskCreationOptions.RunContinuationsAsynchronously);
                disposalTask = completion.Task;
            }

            cleanupTask = disposalTask;
        }

        if (completion is not null)
        {
            await DisposeAndCompleteAsync(completion).ConfigureAwait(false);
            return;
        }

        await cleanupTask.ConfigureAwait(false);
    }

    private async Task DisposeAndCompleteAsync(TaskCompletionSource<object?> completion)
    {
        try
        {
            await DisposeCoreAsync(completion).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            completion.TrySetException(exception);
        }

        await completion.Task.ConfigureAwait(false);
    }

    private async Task DisposeCoreAsync(TaskCompletionSource<object?> completion)
    {
        try
        {
            Task? activeOperation;
            CancellationTokenSource? cancellation;
            lock (tcpOperationGate)
            {
                if (Interlocked.Exchange(ref disposedState, 1) != 0)
                {
                    completion.TrySetResult(null);
                    return;
                }

                cancellation = tcpOperationCancellation;
                activeOperation = tcpOperationTask;
                IsTcpListening = false;
                TcpListenerStatusText = L("TcpStopped", "TCP 수신 중지됨", "TCP listener stopped");
                sharedKeySession.Dispose();
            }

            cancellation?.Cancel();
            if (activeOperation is not null)
            {
                await activeOperation.ConfigureAwait(false);
            }

            ThreeDIntegrationTcpTransport? listener;
            ThreeDIntegrationCancellationCoordinator? cancellationCoordinator;
            lock (tcpOperationGate)
            {
                listener = tcpListener;
                tcpListener = null;
                cancellationCoordinator = tcpCancellationCoordinator;
                tcpCancellationCoordinator = null;
            }
            cancellationCoordinator?.Dispose();
            if (listener is not null)
            {
                await listener.DisposeAsync().ConfigureAwait(false);
            }

            completion.TrySetResult(null);
        }
        catch (Exception exception)
        {
            completion.TrySetException(exception);
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
        var settings = settingsStore.Load();
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

    private ResolvedTcpSettings RequireSavedTcpSettings()
    {
        var root = RequireSavedRoot();
        var current = ResolveCurrentTcpSettings(root);
        var saved = settingsStore.Load();
        if (!string.Equals(
                saved.TcpListenAddress,
                current.ListenAddress.ToString(),
                StringComparison.OrdinalIgnoreCase)
            || saved.TcpListenPort != current.ListenPort
            || !string.Equals(saved.TcpPeerHost, current.PeerHost, StringComparison.OrdinalIgnoreCase)
            || saved.TcpPeerPort != current.PeerPort)
        {
            throw new InvalidOperationException(L(
                "SaveCurrentTcpSetup",
                "TCP 작업 전에 현재 수신 주소와 상대 주소를 설정 저장하세요.",
                "Save the current listen and peer endpoints before running a TCP action."));
        }
        return current;
    }

    private ResolvedTcpSettings ResolveCurrentTcpSettings(string exchangeRoot)
    {
        var listenAddressText = Require(
            TcpListenAddress,
            L("TcpListenAddressRequired", "TCP 수신 주소를 입력하세요.", "Enter a TCP listen address."));
        if (!IPAddress.TryParse(listenAddressText, out var listenAddress))
        {
            throw new ArgumentException(L(
                "TcpListenAddressInvalid",
                "TCP 수신 주소는 이 PC의 올바른 IP 주소여야 합니다.",
                "The TCP listen address must be a valid IP address on this PC."));
        }
        return new(
            exchangeRoot,
            listenAddress,
            ParsePort(TcpListenPortText, L("TcpListenPort", "수신 포트", "listen port")),
            Require(TcpPeerHost, L("TcpPeerRequired", "TCP 상대 주소를 입력하세요.", "Enter a TCP peer host.")),
            ParsePort(TcpPeerPortText, L("TcpPeerPort", "상대 포트", "peer port")));
    }

    private byte[] AcquireSharedKey()
    {
        if (sharedKeySession.TryAcquire(out var key, out var failure))
        {
            return key;
        }

        throw new InvalidOperationException(failure switch
        {
            ThreeDIntegrationSharedKeyAcquireFailure.SessionUnavailable => SharedKeyStatusText,
            ThreeDIntegrationSharedKeyAcquireFailure.EnvironmentMissing => string.Format(
                L(
                    "TcpKeyRequired",
                    "세션 공유 키를 입력하거나 환경 변수 {0}에 Base64 키를 설정하세요.",
                    "Enter a session shared key or set environment variable {0} to a Base64 key."),
                ThreeDIntegrationSharedKeySession.EnvironmentVariableName),
            ThreeDIntegrationSharedKeyAcquireFailure.Disposed => L(
                "TcpDisposed",
                "종료 중인 연동 화면에서는 TCP 작업을 시작할 수 없습니다.",
                "A TCP action cannot start while the integration workspace is closing."),
            _ => string.Format(
                L(
                    "TcpEnvironmentKeyInvalid",
                    "환경 변수 {0}에는 Base64로 인코딩한 32바이트 이상의 키가 필요합니다.",
                    "Environment variable {0} must contain a Base64 key of at least 32 bytes."),
                ThreeDIntegrationSharedKeySession.EnvironmentVariableName)
        });
    }

    private string DescribeSharedKeyStatus()
    {
        return sharedKeySession.DescribeEnvironment() switch
        {
            ThreeDIntegrationSharedKeyEnvironmentStatus.Ready => string.Format(
                L(
                    "TcpEnvironmentKeyReady",
                    "환경 변수 {0}의 공유 키 준비됨",
                    "Shared key ready from environment variable {0}"),
                ThreeDIntegrationSharedKeySession.EnvironmentVariableName),
            ThreeDIntegrationSharedKeyEnvironmentStatus.TooShort => string.Format(
                L(
                    "TcpEnvironmentKeyShort",
                    "환경 변수 {0}의 공유 키가 32바이트보다 짧습니다.",
                    "Shared key in environment variable {0} is shorter than 32 bytes."),
                ThreeDIntegrationSharedKeySession.EnvironmentVariableName),
            ThreeDIntegrationSharedKeyEnvironmentStatus.Invalid => string.Format(
                L(
                    "TcpEnvironmentKeyMalformed",
                    "환경 변수 {0}의 공유 키가 올바른 Base64가 아닙니다.",
                    "Shared key in environment variable {0} is not valid Base64."),
                ThreeDIntegrationSharedKeySession.EnvironmentVariableName),
            ThreeDIntegrationSharedKeyEnvironmentStatus.Disposed => L(
                "TcpDisposed",
                "종료 중인 연동 화면에서는 TCP 작업을 시작할 수 없습니다.",
                "A TCP action cannot start while the integration workspace is closing."),
            _ => string.Format(
                L(
                    "TcpEnvironmentKeyMissing",
                    "공유 키 없음: 세션 입력 또는 환경 변수 {0} 필요",
                    "No shared key: session input or environment variable {0} required"),
                ThreeDIntegrationSharedKeySession.EnvironmentVariableName)
        };
    }

    private void RaiseTcpCanExecuteChanged()
    {
        BrowseExchangeRootCommand.RaiseCanExecuteChanged();
        SaveSetupCommand.RaiseCanExecuteChanged();
        ResetSetupCommand.RaiseCanExecuteChanged();
        RefreshHandoffsCommand.RaiseCanExecuteChanged();
        AcceptCommand.RaiseCanExecuteChanged();
        RejectCommand.RaiseCanExecuteChanged();
        RunInspectionCommand.RaiseCanExecuteChanged();
        PublishResultCommand.RaiseCanExecuteChanged();
        StartTcpListenerCommand.RaiseCanExecuteChanged();
        StopTcpListenerCommand.RaiseCanExecuteChanged();
        PingTcpPeerCommand.RaiseCanExecuteChanged();
        PushSelectedTransactionCommand.RaiseCanExecuteChanged();
        PullSelectedTransactionCommand.RaiseCanExecuteChanged();
        CancelTcpOperationCommand.RaiseCanExecuteChanged();
    }

    private static int ParsePort(string value, string name)
    {
        if (!int.TryParse(
                value,
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out var port)
            || port is < 1 or > IPEndPoint.MaxPort)
        {
            throw new ArgumentException($"{name} must be between 1 and {IPEndPoint.MaxPort}.");
        }
        return port;
    }

    private IntegrationApplicationIdentity ResolveProducerIdentity(
        IntegrationApplicationIdentity? expectedTarget = null) =>
        producerIdentityProvider?.Invoke()
        ?? (expectedTarget is null
            ? IntegrationBuildIdentity.LoadQualifiedIdentity()
            : IntegrationBuildIdentity.LoadQualifiedTargetIdentity(expectedTarget));

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

    private sealed record ResolvedTcpSettings(
        string ExchangeRoot,
        IPAddress ListenAddress,
        int ListenPort,
        string PeerHost,
        int PeerPort);

}
