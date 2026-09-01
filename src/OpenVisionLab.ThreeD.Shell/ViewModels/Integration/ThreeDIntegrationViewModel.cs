using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Net;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Win32;
using OpenVisionLab;
using OpenVisionLab.Integration.Contracts;
using OpenVisionLab.Integration.Transport.Tcp;
using OpenVisionLab.ThreeD.Presentation.Commands;
using OpenVisionLab.ThreeD.Reporting.Integration;

namespace OpenVisionLab.ThreeD.Shell.ViewModels.Integration;

public sealed record ThreeDIntegrationTransactionItem(
    Guid TransactionId,
    string SchemaVersion,
    DateTimeOffset CreatedAtUtc,
    string ProjectId,
    string SequenceId,
    string StepId,
    string CameraId,
    string State,
    string ModalitySummary,
    string AcknowledgementSummary,
    string ResultSummary,
    bool CanInspectInThreeD,
    bool HasAcknowledgement,
    bool HasResult,
    IntegrationAcknowledgementStatus? AcknowledgementStatus)
{
    public string Title => $"{ProjectId} | {State}";
    public string Detail => $"schema {SchemaVersion} | {ModalitySummary} | {AcknowledgementSummary} | {ResultSummary} | {SequenceId} / {StepId} | {CameraId} | {CreatedAtUtc.ToLocalTime():yyyy-MM-dd HH:mm:ss}";
}

public sealed class ThreeDIntegrationViewModel : INotifyPropertyChanged, IAsyncDisposable
{
    private const string SharedKeyEnvironmentVariable = "OPENVISIONLAB_TCP_SHARED_KEY";
    private readonly Func<string?> runRecordPathProvider;
    private readonly string settingsPath;
    private readonly Func<IntegrationApplicationIdentity>? producerIdentityProvider;
    private ThreeDIntegrationTcpExchange? tcpListener;
    private CancellationTokenSource? tcpOperationCancellation;
    private byte[]? sessionSharedKey;
    private bool hasSessionSharedKeyInput;
    private bool disposed;
    private string exchangeRoot = string.Empty;
    private string tcpListenAddress = "127.0.0.1";
    private string tcpListenPortText = "45103";
    private string tcpPeerHost = "127.0.0.1";
    private string tcpPeerPortText = "45102";
    private bool isTcpListening;
    private bool isTcpBusy;
    private string tcpListenerStatusText = L("TcpStopped", "TCP 수신 중지됨", "TCP listener stopped");
    private string sharedKeyStatusText = string.Empty;
    private string lastTcpTransferText = L("NoTcpTransfer", "TCP 전송 기록이 없습니다.", "No TCP transfer has run.");
    private string rejectionReason = string.Empty;
    private string statusText = L("Restore", "설정을 복원했습니다. 폴더를 스캔하거나 작업을 실행하지 않았습니다.", "Setup restored. No folder was scanned and no action was run.");
    private string selectedSummary = L("ChooseRefresh", "새로고침을 눌러 Machine Studio Handoff를 찾으세요.", "Choose Refresh to find Machine Studio handoffs.");
    private string currentRunRecordSummary = L("NoRunRecord", "선택한 Run Record가 없습니다.", "No Run Record is selected.");
    private ThreeDIntegrationTransactionItem? selectedTransaction;

    public ThreeDIntegrationViewModel(
        Func<string?> runRecordPathProvider,
        string? settingsPath = null)
    {
        this.runRecordPathProvider = runRecordPathProvider ?? throw new ArgumentNullException(nameof(runRecordPathProvider));
        this.settingsPath = settingsPath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "OpenVisionLab",
            "ThreeDStudio",
            "machine-exchange.json");
        var settings = ExchangeSettings.Load(this.settingsPath);
        exchangeRoot = settings.ExchangeRoot;
        tcpListenAddress = settings.TcpListenAddress;
        tcpListenPortText = settings.TcpListenPort.ToString(CultureInfo.InvariantCulture);
        tcpPeerHost = settings.TcpPeerHost;
        tcpPeerPortText = settings.TcpPeerPort.ToString(CultureInfo.InvariantCulture);
        sharedKeyStatusText = DescribeSharedKeyStatus();
        BrowseExchangeRootCommand = new RelayCommand(_ => BrowseExchangeRoot(), _ => CanEditTcpSetup);
        SaveSetupCommand = new RelayCommand(_ => SaveSetup(), _ => CanEditTcpSetup);
        ResetSetupCommand = new RelayCommand(_ => ResetSetup(), _ => CanEditTcpSetup);
        RefreshHandoffsCommand = new RelayCommand(_ => RefreshHandoffs(), _ => !IsTcpBusy);
        AcceptCommand = new RelayCommand(_ => AcceptSelected(), _ => CanReviewSelected);
        RejectCommand = new RelayCommand(_ => RejectSelected(), _ => CanReviewSelected);
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
        SyncRunRecord();
    }

    internal ThreeDIntegrationViewModel(
        Func<string?> runRecordPathProvider,
        string settingsPath,
        Func<IntegrationApplicationIdentity> producerIdentityProvider)
        : this(runRecordPathProvider, settingsPath)
    {
        this.producerIdentityProvider = producerIdentityProvider
            ?? throw new ArgumentNullException(nameof(producerIdentityProvider));
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<ThreeDIntegrationTransactionItem> Transactions { get; } = [];
    public RelayCommand BrowseExchangeRootCommand { get; }
    public RelayCommand SaveSetupCommand { get; }
    public RelayCommand ResetSetupCommand { get; }
    public RelayCommand RefreshHandoffsCommand { get; }
    public RelayCommand AcceptCommand { get; }
    public RelayCommand RejectCommand { get; }
    public RelayCommand PublishResultCommand { get; }
    public RelayCommand StartTcpListenerCommand { get; }
    public RelayCommand StopTcpListenerCommand { get; }
    public RelayCommand PingTcpPeerCommand { get; }
    public RelayCommand PushSelectedTransactionCommand { get; }
    public RelayCommand PullSelectedTransactionCommand { get; }

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
                : string.Format(
                    L(
                        "SelectedFormat",
                        "거래 {0:D} | {1} | {2} | {3} | {4}",
                        "Transaction {0:D} | {1} | {2} | {3} | {4}"),
                    value.TransactionId,
                    value.ProjectId,
                    value.State,
                    value.AcknowledgementSummary,
                    value.ResultSummary);
            AcceptCommand.RaiseCanExecuteChanged();
            RejectCommand.RaiseCanExecuteChanged();
            PublishResultCommand.RaiseCanExecuteChanged();
            PushSelectedTransactionCommand.RaiseCanExecuteChanged();
            PullSelectedTransactionCommand.RaiseCanExecuteChanged();
        }
    }

    private bool CanReviewSelected =>
        !IsTcpBusy
        && SelectedTransaction is { CanInspectInThreeD: true, HasAcknowledgement: false, HasResult: false };

    private bool CanPublishSelectedResult =>
        !IsTcpBusy
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
            ? L("NoRunRecordDetail", "선택한 Run Record가 없습니다. 게시 전에 Run Record를 열거나 실행을 완료하세요.", "No Run Record is selected. Open or complete a Run Record before publishing.")
            : string.Format(L("RunRecordFormat", "선택한 Run Record: {0}", "Selected Run Record: {0}"), path);
    }

    public void SetSessionSharedKey(string? encodedKey)
    {
        if (sessionSharedKey is not null)
        {
            CryptographicOperations.ZeroMemory(sessionSharedKey);
        }
        sessionSharedKey = null;
        hasSessionSharedKeyInput = !string.IsNullOrWhiteSpace(encodedKey);
        if (!hasSessionSharedKeyInput)
        {
            SharedKeyStatusText = DescribeSharedKeyStatus();
            return;
        }

        try
        {
            var parsed = Convert.FromBase64String(encodedKey!.Trim());
            if (parsed.Length < 32)
            {
                CryptographicOperations.ZeroMemory(parsed);
                SharedKeyStatusText = L(
                    "TcpKeyTooShort",
                    "세션 공유 키는 Base64로 인코딩한 32바이트 이상이어야 합니다.",
                    "The session shared key must be Base64-encoded and contain at least 32 bytes.");
                return;
            }

            sessionSharedKey = parsed;
            SharedKeyStatusText = L(
                "TcpSessionKeyReady",
                "세션 공유 키 준비됨(저장되지 않음)",
                "Session shared key ready (not saved)");
        }
        catch (FormatException)
        {
            SharedKeyStatusText = L(
                "TcpKeyInvalidBase64",
                "세션 공유 키가 올바른 Base64가 아닙니다.",
                "The session shared key is not valid Base64.");
        }
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
            var tcp = ResolveCurrentTcpSettings(root);
            Directory.CreateDirectory(root);
            new ExchangeSettings
            {
                ExchangeRoot = root,
                TcpListenAddress = tcp.ListenAddress.ToString(),
                TcpListenPort = tcp.ListenPort,
                TcpPeerHost = tcp.PeerHost,
                TcpPeerPort = tcp.PeerPort
            }.Save(settingsPath);
            ExchangeRoot = root;
            TcpListenAddress = tcp.ListenAddress.ToString();
            TcpListenPortText = tcp.ListenPort.ToString(CultureInfo.InvariantCulture);
            TcpPeerHost = tcp.PeerHost;
            TcpPeerPortText = tcp.PeerPort.ToString(CultureInfo.InvariantCulture);
            StatusText = L(
                "Saved",
                "교환 폴더와 TCP 주소를 저장했습니다. 공유 키는 저장하지 않으며 수신 시작, 새로고침, 검토는 각각 별도의 명시적 작업입니다.",
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
            new ExchangeSettings().Save(settingsPath);
            ExchangeRoot = string.Empty;
            TcpListenAddress = "127.0.0.1";
            TcpListenPortText = "45103";
            TcpPeerHost = "127.0.0.1";
            TcpPeerPortText = "45102";
            SetSessionSharedKey(null);
            Transactions.Clear();
            SelectedTransaction = null;
            LastTcpTransferText = L("NoTcpTransfer", "TCP 전송 기록이 없습니다.", "No TCP transfer has run.");
            StatusText = L("Reset", "교환 설정을 초기화했습니다. 네트워크 또는 Handoff 작업은 실행하지 않았습니다.", "Exchange setup reset. No network or handoff action was run.");
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
            var root = RequireSavedRoot();
            var discovered = ThreeDIntegrationExchange.DiscoverHandoffs(root);
            var items = new List<ThreeDIntegrationTransactionItem>();
            Transactions.Clear();
            foreach (var transaction in discovered)
            {
                ThreeDIntegrationTcpSequence sequence;
                try
                {
                    sequence = ThreeDIntegrationTcpExchange.ReadValidatedV2Sequence(
                        root,
                        transaction.Handoff.TransactionId);
                }
                catch (Exception exception) when (
                    exception is IOException
                    or UnauthorizedAccessException
                    or InvalidDataException
                    or JsonException
                    or IntegrationContractException)
                {
                    sequence = new(
                        transaction.Handoff,
                        null,
                        null);
                }

                var acknowledgementPresent = sequence.Acknowledgement is not null
                    || transaction.HasAcknowledgement;
                var resultPresent = sequence.Result is not null
                    || transaction.HasResult;
                var state = resultPresent
                    ? L("StatePublished", "결과 게시됨", "Result published")
                    : acknowledgementPresent
                        ? L("StateReviewed", "검토됨", "Reviewed")
                        : L("StatePending", "검토 대기", "Pending review");
                items.Add(new(
                    transaction.Handoff.TransactionId,
                    transaction.Handoff.SchemaVersion,
                    transaction.Handoff.CreatedAtUtc,
                    transaction.Handoff.Context.ProjectId,
                    transaction.Handoff.Context.SequenceId,
                    transaction.Handoff.Context.StepId,
                    transaction.Handoff.Context.CameraId,
                    state,
                    $"{transaction.Handoff.Context.Modality}/{transaction.Handoff.Context.InputKind}",
                    sequence.Acknowledgement is not null
                        ? $"ACK {sequence.Acknowledgement.Status}"
                        : acknowledgementPresent
                            ? L("AckPresent", "ACK 있음", "ACK present")
                            : L("AckAbsent", "ACK 없음", "ACK absent"),
                    sequence.Result is not null
                        ? $"Result {sequence.Result.Status}/{sequence.Result.Outcome}/Run {sequence.Result.RunId ?? "-"}"
                        : resultPresent
                            ? L("ResultPresent", "Result 있음", "Result present")
                            : L("ResultAbsent", "Result 없음", "Result absent"),
                    transaction.Handoff.Context.Modality == IntegrationInspectionModality.ThreeD
                    && transaction.Handoff.Context.InputKind == IntegrationInspectionInputKind.HeightMap
                    && string.Equals(
                        transaction.Handoff.Context.ConsumerBuild.ApplicationId,
                        IntegrationApplicationIds.ThreeDStudio,
                        StringComparison.Ordinal),
                    acknowledgementPresent,
                    resultPresent,
                    sequence.Acknowledgement?.Status));
            }
            foreach (var item in items.OrderByDescending(candidate => candidate.CreatedAtUtc))
            {
                Transactions.Add(item);
            }
            SelectedTransaction = Transactions.FirstOrDefault(item => item.TransactionId == preferredTransactionId)
                ?? Transactions.FirstOrDefault();
            StatusText = Transactions.Count == 0
                ? L("NoneFound", "Machine Studio Handoff를 찾지 못했습니다.", "No Machine Studio handoff was found.")
                : string.Format(L("FoundFormat", "Handoff {0}개를 찾았습니다. 활성 레시피에는 아무것도 불러오지 않았습니다.", "Found {0} handoff(s). Nothing was loaded into the active recipe."), Transactions.Count);
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
                ResolveProducerIdentity(handoff.Context.ConsumerBuild),
                rejectionReason);
            RefreshHandoffs(selected.TransactionId);
            StatusText = acknowledgement.Status == IntegrationAcknowledgementStatus.Accepted
                ? L("Accepted", "ACK를 로컬 거래에 기록했습니다. 레시피를 불러오거나 검사를 실행하지 않았습니다. TCP 상대에게 돌려보내려면 선택 거래 보내기를 누르세요.", "ACK recorded in the local transaction. No recipe was loaded and no inspection was run. Choose Push selected transaction to return it to the TCP peer.")
                : L("Rejected", "거절 ACK를 로컬 거래에 기록했습니다. TCP 상대에게 돌려보내려면 선택 거래 보내기를 누르세요.", "Rejected ACK recorded in the local transaction. Choose Push selected transaction to return it to the TCP peer.");
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
            var root = RequireSavedRoot();
            var handoff = ThreeDIntegrationExchange.ReadHandoff(
                root,
                selected.TransactionId);
            var result = ThreeDIntegrationExchange.PublishCompletedResult(
                root,
                selected.TransactionId,
                ResolveProducerIdentity(handoff.Context.ConsumerBuild),
                runRecordPath);
            RefreshHandoffs(selected.TransactionId);
            StatusText = string.Format(
                L(
                    "PublishedFormat",
                    "결과 준비됨: {0} | Run {1}. TCP 상대에게 돌려보내려면 선택 거래 보내기를 누르세요.",
                    "Result prepared: {0} | Run {1}. Choose Push selected transaction to return it to the TCP peer."),
                result.Outcome,
                result.RunId);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException or IntegrationContractException)
        {
            StatusText = exception.Message;
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
            ThreeDIntegrationTcpExchange? listener = null;
            try
            {
                listener = new ThreeDIntegrationTcpExchange(settings.ExchangeRoot, key);
                var endpoint = await listener.StartListeningAsync(
                    settings.ListenAddress,
                    settings.ListenPort,
                    cancellationToken);
                tcpListener = listener;
                listener = null;
                IsTcpListening = true;
                TcpListenerStatusText = string.Format(
                    L("TcpListeningFormat", "TCP 수신 중: {0}", "TCP listening: {0}"),
                    endpoint);
                StatusText = L(
                    "TcpStarted",
                    "TCP 수신을 시작했습니다. 수신만으로 ACK, 레시피 로드, Preview, Publish, Run 또는 Result를 실행하지 않습니다.",
                    "TCP listening started. Receipt alone never ACKs, loads a recipe, Previews, Publishes, Runs, or creates a Result.");
            }
            finally
            {
                CryptographicOperations.ZeroMemory(key);
                if (listener is not null)
                {
                    await listener.DisposeAsync();
                }
            }
        });

    internal Task StopTcpListenerAsync() => RunTcpOperationAsync(
        L("TcpStopping", "TCP 수신을 중지하는 중입니다.", "Stopping TCP listener."),
        async _ =>
        {
            var listener = tcpListener;
            tcpListener = null;
            try
            {
                if (listener is not null)
                {
                    await listener.DisposeAsync();
                }
            }
            finally
            {
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
                "보낼 거래를 먼저 선택하세요.",
                "Choose a transaction to push first.");
            return Task.CompletedTask;
        }
        return RunTcpTransferAsync(
            L("TcpPushing", "선택 거래를 보내는 중입니다.", "Pushing selected transaction."),
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
                "받을 거래를 먼저 선택하세요.",
                "Choose a transaction to pull first.");
            return Task.CompletedTask;
        }
        return RunTcpTransferAsync(
            L("TcpPulling", "선택 거래를 받는 중입니다.", "Pulling selected transaction."),
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
            ThreeDIntegrationTcpExchange,
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
                    await using var exchange = new ThreeDIntegrationTcpExchange(
                        settings.ExchangeRoot,
                        key);
                    var receipt = await operation(
                        exchange,
                        new TcpIntegrationEndpoint(settings.PeerHost, settings.PeerPort),
                        cancellationToken);
                    LastTcpTransferText = string.Format(
                        CultureInfo.CurrentCulture,
                        L(
                            "TcpTransferFormat",
                            "{0} 완료 | 상대 {1} | 거래 {2} | 파일 {3} | 바이트 {4:N0} | 멱등 {5}",
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
                            return;
                        }
                    }
                    StatusText = LastTcpTransferText;
                }
                finally
                {
                    CryptographicOperations.ZeroMemory(key);
                }
            });

    private async Task RunTcpOperationAsync(
        string busyStatus,
        Func<CancellationToken, Task> operation)
    {
        if (disposed)
        {
            StatusText = L(
                "TcpDisposed",
                "종료 중인 연동 화면에서는 TCP 작업을 시작할 수 없습니다.",
                "A TCP action cannot start while the integration workspace is closing.");
            return;
        }
        if (IsTcpBusy)
        {
            return;
        }

        IsTcpBusy = true;
        StatusText = busyStatus;
        using var cancellation = new CancellationTokenSource();
        tcpOperationCancellation = cancellation;
        try
        {
            await operation(cancellation.Token);
        }
        catch (OperationCanceledException)
        {
            StatusText = L(
                "TcpCancelled",
                "TCP 작업을 취소했습니다.",
                "TCP action cancelled.");
        }
        catch (Exception exception)
        {
            StatusText = exception.Message;
        }
        finally
        {
            if (ReferenceEquals(tcpOperationCancellation, cancellation))
            {
                tcpOperationCancellation = null;
            }
            IsTcpBusy = false;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        tcpOperationCancellation?.Cancel();
        var listener = tcpListener;
        tcpListener = null;
        IsTcpListening = false;
        TcpListenerStatusText = L("TcpStopped", "TCP 수신 중지됨", "TCP listener stopped");
        if (sessionSharedKey is not null)
        {
            CryptographicOperations.ZeroMemory(sessionSharedKey);
            sessionSharedKey = null;
        }
        hasSessionSharedKeyInput = false;
        if (listener is not null)
        {
            await listener.DisposeAsync().ConfigureAwait(false);
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

    private ResolvedTcpSettings RequireSavedTcpSettings()
    {
        var root = RequireSavedRoot();
        var current = ResolveCurrentTcpSettings(root);
        var saved = ExchangeSettings.Load(settingsPath);
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
        if (hasSessionSharedKeyInput)
        {
            if (sessionSharedKey is null)
            {
                throw new InvalidOperationException(SharedKeyStatusText);
            }
            return sessionSharedKey.ToArray();
        }

        var encoded = Environment.GetEnvironmentVariable(SharedKeyEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(encoded))
        {
            throw new InvalidOperationException(string.Format(
                L(
                    "TcpKeyRequired",
                    "세션 공유 키를 입력하거나 환경 변수 {0}에 Base64 키를 설정하세요.",
                    "Enter a session shared key or set environment variable {0} to a Base64 key."),
                SharedKeyEnvironmentVariable));
        }
        try
        {
            var key = Convert.FromBase64String(encoded.Trim());
            if (key.Length >= 32)
            {
                return key;
            }
            CryptographicOperations.ZeroMemory(key);
        }
        catch (FormatException)
        {
            // The actionable message below owns both malformed and short values.
        }

        throw new InvalidOperationException(string.Format(
            L(
                "TcpEnvironmentKeyInvalid",
                "환경 변수 {0}에는 Base64로 인코딩한 32바이트 이상의 키가 필요합니다.",
                "Environment variable {0} must contain a Base64 key of at least 32 bytes."),
            SharedKeyEnvironmentVariable));
    }

    private string DescribeSharedKeyStatus()
    {
        var encoded = Environment.GetEnvironmentVariable(SharedKeyEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(encoded))
        {
            return string.Format(
                L(
                    "TcpEnvironmentKeyMissing",
                    "공유 키 없음: 세션 입력 또는 환경 변수 {0} 필요",
                    "No shared key: session input or environment variable {0} required"),
                SharedKeyEnvironmentVariable);
        }

        try
        {
            var key = Convert.FromBase64String(encoded.Trim());
            var valid = key.Length >= 32;
            CryptographicOperations.ZeroMemory(key);
            return valid
                ? string.Format(
                    L(
                        "TcpEnvironmentKeyReady",
                        "환경 변수 {0}의 공유 키 준비됨",
                        "Shared key ready from environment variable {0}"),
                    SharedKeyEnvironmentVariable)
                : string.Format(
                    L(
                        "TcpEnvironmentKeyShort",
                        "환경 변수 {0}의 공유 키가 32바이트보다 짧습니다.",
                        "Shared key in environment variable {0} is shorter than 32 bytes."),
                    SharedKeyEnvironmentVariable);
        }
        catch (FormatException)
        {
            return string.Format(
                L(
                    "TcpEnvironmentKeyMalformed",
                    "환경 변수 {0}의 공유 키가 올바른 Base64가 아닙니다.",
                    "Shared key in environment variable {0} is not valid Base64."),
                SharedKeyEnvironmentVariable);
        }
    }

    private void RaiseTcpCanExecuteChanged()
    {
        BrowseExchangeRootCommand.RaiseCanExecuteChanged();
        SaveSetupCommand.RaiseCanExecuteChanged();
        ResetSetupCommand.RaiseCanExecuteChanged();
        RefreshHandoffsCommand.RaiseCanExecuteChanged();
        AcceptCommand.RaiseCanExecuteChanged();
        RejectCommand.RaiseCanExecuteChanged();
        PublishResultCommand.RaiseCanExecuteChanged();
        StartTcpListenerCommand.RaiseCanExecuteChanged();
        StopTcpListenerCommand.RaiseCanExecuteChanged();
        PingTcpPeerCommand.RaiseCanExecuteChanged();
        PushSelectedTransactionCommand.RaiseCanExecuteChanged();
        PullSelectedTransactionCommand.RaiseCanExecuteChanged();
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

    private sealed record ResolvedTcpSettings(
        string ExchangeRoot,
        IPAddress ListenAddress,
        int ListenPort,
        string PeerHost,
        int PeerPort);

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
        public string TcpListenAddress { get; set; } = "127.0.0.1";
        public int TcpListenPort { get; set; } = 45103;
        public string TcpPeerHost { get; set; } = "127.0.0.1";
        public int TcpPeerPort { get; set; } = 45102;

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
