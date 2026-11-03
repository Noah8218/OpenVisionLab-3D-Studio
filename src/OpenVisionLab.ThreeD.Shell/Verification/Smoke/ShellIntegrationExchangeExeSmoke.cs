#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading.Tasks;
using OpenVisionLab.Integration.Contracts;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Reporting.Integration;
using OpenVisionLab.ThreeD.Reporting.RunRecords;
using OpenVisionLab.ThreeD.Shell;
using OpenVisionLab.ThreeD.Shell.Coordination;
using OpenVisionLab.ThreeD.Shell.ViewModels.Integration;

namespace OpenVisionLab.ThreeD.Shell.Verification.Smoke;

internal sealed record ShellIntegrationExchangeExeSmokeResult(
    bool Succeeded,
    string? Failure);

internal sealed class ShellIntegrationExchangeExeSmokeReport
{
    public string Schema { get; init; } = "1.0";
    public DateTimeOffset CapturedAtUtc { get; init; } = DateTimeOffset.UtcNow;
    public string Role { get; init; } = "consumer";
    public string? TransactionId { get; init; }
    public bool RestartBoundaryReached { get; init; }
    public bool RecoveredExistingTransaction { get; init; }
    public bool ReopenedExistingResult { get; init; }
    public bool CancellationExpected { get; init; }
    public bool CancelledResultObserved { get; init; }
    public bool ExecutionErrorExpected { get; init; }
    public bool ExecutionErrorObserved { get; init; }
    public string Status { get; init; } = string.Empty;
    public required IReadOnlyDictionary<string, bool> Checks { get; init; }
    public required IReadOnlyList<string> Failures { get; init; }
    public bool IsValid => Failures.Count == 0 && Checks.Count > 0 && Checks.Values.All(value => value);

    public void Save(string path)
    {
        var fullPath = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllText(
            fullPath,
            JsonSerializer.Serialize(this, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = true
            }));
    }
}

internal static class ShellIntegrationExchangeExeSmoke
{
    private const string SharedKeyEnvironmentVariable = "OPENVISIONLAB_TCP_SHARED_KEY";

    public static async Task<ShellIntegrationExchangeExeSmokeResult> RunAsync(
        string role,
        ShellMainWindowViewModel viewModel,
        Func<bool> isShellVisible,
        Func<Task> yieldUi,
        string? reportPath)
    {
        return await RunAsync(
            role,
            viewModel,
            isShellVisible,
            yieldUi,
            reportPath,
            ShellCommandLineArguments.Capture(),
            CancellationToken.None);
    }

    internal static async Task<ShellIntegrationExchangeExeSmokeResult> RunAsync(
        string role,
        ShellMainWindowViewModel viewModel,
        Func<bool> isShellVisible,
        Func<Task> yieldUi,
        string? reportPath,
        ShellCommandLineArguments commandLine)
    {
        return await RunAsync(
            role,
            viewModel,
            isShellVisible,
            yieldUi,
            reportPath,
            commandLine,
            CancellationToken.None);
    }

    internal static async Task<ShellIntegrationExchangeExeSmokeResult> RunAsync(
        string role,
        ShellMainWindowViewModel viewModel,
        Func<bool> isShellVisible,
        Func<Task> yieldUi,
        string? reportPath,
        ShellCommandLineArguments commandLine,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(role);
        ArgumentNullException.ThrowIfNull(viewModel);
        ArgumentNullException.ThrowIfNull(isShellVisible);
        ArgumentNullException.ThrowIfNull(yieldUi);
        ArgumentNullException.ThrowIfNull(commandLine);

        var checks = new Dictionary<string, bool>(StringComparer.Ordinal);
        var failures = new List<string>();
        var transactionId = (Guid?)null;
        var status = string.Empty;
        var reportTarget = string.IsNullOrWhiteSpace(reportPath)
            ? Path.Combine(Path.GetTempPath(), "OpenVisionLab-3D-integration-exe-smoke.json")
            : Path.GetFullPath(reportPath);
        var stopAfterAcknowledgement = commandLine.HasFlag(
            "--smoke-integration-exe-stop-after-ack");
        var cancellationExpected = commandLine.HasFlag(
            "--smoke-integration-exe-expect-cancelled");
        var executionErrorExpected = commandLine.HasFlag(
            "--smoke-integration-exe-expect-error");
        var preAcknowledgementDelayMilliseconds = ParseMilliseconds(
            commandLine.GetValueIgnoreCase("--smoke-integration-exe-pre-ack-delay-ms"),
            0,
            0,
            30000);
        var preRunDelayMilliseconds = ParseMilliseconds(
            commandLine.GetValueIgnoreCase("--smoke-integration-exe-pre-run-delay-ms"),
            0,
            0,
            30000);
        var readyFilePath = commandLine.GetValueIgnoreCase(
            "--smoke-integration-exe-ready-file") is { Length: > 0 } readyPath
            ? Path.GetFullPath(readyPath)
            : null;
        var listeningReadyFilePath = commandLine.GetValueIgnoreCase(
            "--smoke-integration-exe-listening-ready-file") is { Length: > 0 } listeningPath
            ? Path.GetFullPath(listeningPath)
            : null;
        var resultReadyFilePath = commandLine.GetValueIgnoreCase(
            "--smoke-integration-exe-result-ready-file") is { Length: > 0 } resultPath
            ? Path.GetFullPath(resultPath)
            : null;
            var recoveryTransactionId = ParseOptionalGuid(
            commandLine.GetValueIgnoreCase("--smoke-integration-exe-recover-transaction-id"),
            "--smoke-integration-exe-recover-transaction-id");
        var reopenExistingResultTransactionId = ParseOptionalGuid(
            commandLine.GetValueIgnoreCase("--smoke-integration-exe-reopen-result-transaction-id"),
            "--smoke-integration-exe-reopen-result-transaction-id");
        if ((stopAfterAcknowledgement || recoveryTransactionId.HasValue)
            && reopenExistingResultTransactionId.HasValue)
        {
            throw new ArgumentException(
                "The ACK-stop/recovery and read-only result-reopen modes cannot be combined.");
        }
        if (stopAfterAcknowledgement && recoveryTransactionId.HasValue)
        {
            throw new ArgumentException(
                "The ACK-stop and recovery transaction modes cannot be combined.");
        }
        var restartBoundaryReached = false;
        var recoveredExistingTransaction = recoveryTransactionId.HasValue;
        var reopenedExistingResult = reopenExistingResultTransactionId.HasValue;
        var cancelledResultObserved = false;
        var executionErrorObserved = false;

        void Check(string name, bool passed)
        {
            checks[name] = passed;
            if (!passed && !failures.Contains(name, StringComparer.Ordinal))
            {
                failures.Add(name);
            }
        }

        void SignalReadyFile()
        {
            if (readyFilePath is null || transactionId is not Guid readyTransactionId)
            {
                return;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(readyFilePath)!);
            File.WriteAllText(readyFilePath, readyTransactionId.ToString("D"));
            Check("cancellationBoundaryReadyFileWritten", true);
        }

        if (!string.Equals(role, "consumer", StringComparison.OrdinalIgnoreCase))
        {
            Check("supportedConsumerRole", false);
            failures.Add($"Unsupported 3D integration EXE role '{role}'.");
            SaveReport(reportTarget, transactionId, status, checks, failures);
            return new(false, failures[^1]);
        }

        var integration = viewModel.IntegrationExchange;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var root = RequireArgument(commandLine, "--smoke-integration-exchange-root");
            var listenPort = ParsePort(
                commandLine.GetValueIgnoreCase("--smoke-integration-listen-port"),
                45103);
            var peerPort = ParsePort(
                commandLine.GetValueIgnoreCase("--smoke-integration-peer-port"),
                45101);
            viewModel.IntegrationExchange.SmokePreInspectionDelayMilliseconds = ParseMilliseconds(
                commandLine.GetValueIgnoreCase("--smoke-integration-exe-pre-inspection-delay-ms"),
                0,
                0,
                30000);
            viewModel.IntegrationExchange.SmokePreInspectionReadyFilePath = readyFilePath;
            viewModel.IntegrationExchange.SmokeForceExecutionError = executionErrorExpected;
            Directory.CreateDirectory(root);

            integration.ExchangeRoot = root;
            integration.TcpListenAddress = "127.0.0.1";
            integration.TcpListenPortText = listenPort.ToString();
            integration.TcpPeerHost = "127.0.0.1";
            integration.TcpPeerPortText = peerPort.ToString();
            integration.SetSessionSharedKey(
                Environment.GetEnvironmentVariable(SharedKeyEnvironmentVariable)
                ?? string.Empty);
            Check("consumerSettingsCanBeSaved", integration.SaveSetupCommand.CanExecute(null));
            integration.SaveSetupCommand.Execute(null);
            await yieldUi();
            cancellationToken.ThrowIfCancellationRequested();
            Check("consumerSettingsSaved", integration.ExchangeRoot == root);
            Check("consumerWindowLoaded", isShellVisible());

            await integration.StartTcpListenerAsync();
            cancellationToken.ThrowIfCancellationRequested();
            Check("consumerListening", integration.IsTcpListening);
            if (listeningReadyFilePath is not null && integration.IsTcpListening)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(listeningReadyFilePath)!);
                File.WriteAllText(listeningReadyFilePath, "listening");
                Check("consumerListeningReadyFileWritten", true);
            }

            var machinePingAccepted = false;
            for (var attempt = 0; attempt < 40 && !machinePingAccepted; attempt++)
            {
                await integration.PingTcpPeerAsync();
                cancellationToken.ThrowIfCancellationRequested();
                machinePingAccepted = integration.LastTcpTransferText.Contains(
                    IntegrationApplicationIds.MachineStudio,
                    StringComparison.Ordinal);
                if (!machinePingAccepted)
                {
                    await Task.Delay(250, cancellationToken);
                }
            }
            Check("machinePingAccepted", machinePingAccepted);

            await WaitForAsync(
                () =>
                {
                    integration.RefreshHandoffsCommand.Execute(null);
                    if (!recoveryTransactionId.HasValue
                        && !reopenExistingResultTransactionId.HasValue)
                    {
                        var pending = integration.Transactions
                            .Where(item => !item.HasResult)
                            .OrderByDescending(item => item.CreatedAtUtc)
                            .FirstOrDefault();
                        if (pending is null)
                        {
                            return false;
                        }

                        integration.SelectedTransaction = pending;
                        return true;
                    }

                    var expectedTransactionId = recoveryTransactionId
                        ?? reopenExistingResultTransactionId
                        ?? throw new InvalidOperationException(
                            "A recovery or result-reopen transaction ID was required.");
                    var recovered = integration.Transactions.FirstOrDefault(
                        item => item.TransactionId == expectedTransactionId);
                    if (recovered is null)
                    {
                        return false;
                    }

                    integration.SelectedTransaction = recovered;
                    return true;
                },
                TimeSpan.FromSeconds(60),
                recoveryTransactionId.HasValue
                    ? "The 3D consumer did not rediscover the requested transaction after restart."
                    : "The 3D consumer did not discover the pushed Machine transaction.",
                cancellationToken);
            Check("handoffDiscovered", integration.Transactions.Count > 0);

            var selected = integration.SelectedTransaction
                ?? throw new InvalidOperationException(
                    "The 3D consumer did not select its discovered transaction.");
            transactionId = selected.TransactionId;
            Check("handoffTargetsThreeD", selected.CanInspectInThreeD);
            Check(
                "handoffSelectionModeValid",
                reopenExistingResultTransactionId.HasValue
                    || recoveryTransactionId.HasValue
                    || (!selected.HasAcknowledgement && !selected.HasResult));
            if (recoveryTransactionId is Guid recoveryId)
            {
                Check("recoveryTransactionIdentityMatches", selected.TransactionId == recoveryId);
                Check("recoveryAcknowledgementAlreadyPresent", selected.HasAcknowledgement);
                Check("recoveryResultStillPending", !selected.HasResult);
                Check(
                    "recoveryAcknowledgementAccepted",
                    selected.AcknowledgementStatus == IntegrationAcknowledgementStatus.Accepted);
            }
            if (reopenExistingResultTransactionId is Guid reopenSelectionId)
            {
                Check("reopenResultTransactionIdentityMatches", selected.TransactionId == reopenSelectionId);
                Check("reopenAcknowledgementPresent", selected.HasAcknowledgement);
                Check("reopenResultPresent", selected.HasResult);
                Check(
                    "reopenAcknowledgementAccepted",
                    selected.AcknowledgementStatus == IntegrationAcknowledgementStatus.Accepted);
            }
            else
            {
                Check(
                    "acceptCommandEnabled",
                    recoveryTransactionId.HasValue || integration.AcceptCommand.CanExecute(null));
            }
            await WaitForAsync(
                () => viewModel.Workbench.HeightImageViewer.HasImage,
                TimeSpan.FromSeconds(30),
                "The 3D integration viewer did not load the selected HeightMap source.",
                cancellationToken);
            Check("sourceImageViewerLoaded", viewModel.Workbench.HeightImageViewer.HasImage);
            Check(
                "sourceImageViewerDimensions",
                viewModel.Workbench.HeightImageViewer.Frame is { Width: > 0, Height: > 0 });

            if (reopenExistingResultTransactionId is Guid reopenId)
            {
                var reopenedResult = ThreeDIntegrationV2Exchange.ReadResult(root, reopenId);
                var reopenedResultPath = Path.Combine(
                    root,
                    IntegrationTransactionLayout.TransactionsDirectoryName,
                    reopenId.ToString("D"),
                    IntegrationTransactionLayout.ResultFileName);
                var resultHashBeforeHold = Convert.ToHexString(
                    SHA256.HashData(File.ReadAllBytes(reopenedResultPath)));
                var runRecordArtifact = reopenedResult.RunRecord;
                var runRecordPath = runRecordArtifact is null
                    ? null
                    : Path.Combine(
                        root,
                        IntegrationTransactionLayout.TransactionsDirectoryName,
                        reopenId.ToString("D"),
                        runRecordArtifact.RelativePath.Replace('/', Path.DirectorySeparatorChar));
                var runRecord = runRecordPath is null || !File.Exists(runRecordPath)
                    ? null
                    : InspectionRunRecordJson.Read(runRecordPath);
                var imagePreview = reopenedResult.Evidence.SingleOrDefault(evidence =>
                    string.Equals(
                        evidence.Role,
                        IntegrationArtifactRoles.ResultEvidence,
                        StringComparison.Ordinal));
                var imagePreviewPath = imagePreview is null
                    ? null
                    : Path.Combine(
                        root,
                        IntegrationTransactionLayout.TransactionsDirectoryName,
                        reopenId.ToString("D"),
                        imagePreview.RelativePath.Replace('/', Path.DirectorySeparatorChar));
                Check(
                    "reopenResultCompletedPass",
                    selected.ResultSummary.Contains("Completed", StringComparison.Ordinal)
                    && selected.ResultSummary.Contains("Pass", StringComparison.Ordinal)
                    && reopenedResult.Status == IntegrationResultStatus.Completed
                    && reopenedResult.Outcome == IntegrationInspectionOutcome.Pass);
                Check("reopenResultHasRunId", !string.IsNullOrWhiteSpace(reopenedResult.RunId));
                Check(
                    "reopenRunRecordReadable",
                    runRecord is not null
                    && runRecord.RunId == reopenedResult.RunId);
                Check(
                    "reopenResultImagePreviewReadable",
                    imagePreview is not null
                    && string.Equals(
                        imagePreview.ArtifactId,
                        ThreeDHeightImagePreviewArtifact.ResultEvidenceArtifactId,
                        StringComparison.Ordinal)
                    && imagePreviewPath is not null
                    && File.Exists(imagePreviewPath)
                    && HasPngSignature(imagePreviewPath));
                Check("reopenAcceptDisabled", !integration.AcceptCommand.CanExecute(null));
                Check("reopenRunDisabled", !integration.RunInspectionCommand.CanExecute(null));
                Check("reopenPublishDisabled", !integration.PublishResultCommand.CanExecute(null));
                var reopenHoldMilliseconds = ParseMilliseconds(
                    commandLine.GetValueIgnoreCase("--smoke-integration-exe-hold-ms"),
                    0,
                    0,
                    30000);
                if (resultReadyFilePath is not null)
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(resultReadyFilePath)!);
                    File.WriteAllText(resultReadyFilePath, reopenId.ToString("D"));
                    Check("consumerResultReadyFileWritten", true);
                }
                if (reopenHoldMilliseconds > 0)
                {
                    await Task.Delay(reopenHoldMilliseconds, cancellationToken);
                }
                var resultHashAfterHold = Convert.ToHexString(
                    SHA256.HashData(File.ReadAllBytes(reopenedResultPath)));
                Check("reopenResultFileUnchanged", resultHashBeforeHold == resultHashAfterHold);
                status = "Existing Result reopened read-only; no Accept, Run, Publish, or Push was executed.";
                return new(
                    failures.Count == 0 && checks.Count > 0 && checks.Values.All(value => value),
                    failures.Count == 0 ? null : string.Join("; ", failures));
            }

            if (!recoveryTransactionId.HasValue && preAcknowledgementDelayMilliseconds > 0)
            {
                SignalReadyFile();
                await Task.Delay(preAcknowledgementDelayMilliseconds, cancellationToken);
                Check("preAcknowledgementDelayApplied", true);
            }

            if (!recoveryTransactionId.HasValue)
            {
                integration.AcceptCommand.Execute(null);
                await yieldUi();
                cancellationToken.ThrowIfCancellationRequested();
                integration.RefreshHandoffsCommand.Execute(null);
                selected = integration.SelectedTransaction
                    ?? throw new InvalidOperationException("The 3D transaction disappeared after Accept.");
                Check("acknowledgementCreated", selected.HasAcknowledgement);
                Check(
                    "acknowledgementAccepted",
                    selected.AcknowledgementStatus == IntegrationAcknowledgementStatus.Accepted);
                Check("resultNotCreatedByAccept", !selected.HasResult);

                if (stopAfterAcknowledgement)
                {
                    await integration.PushSelectedTransactionAsync();
                    cancellationToken.ThrowIfCancellationRequested();
                    Check(
                        "acknowledgementPushedToMachine",
                        integration.LastTcpTransferText.Contains(
                            "push",
                            StringComparison.OrdinalIgnoreCase)
                        && integration.LastTcpTransferText.Contains(
                            IntegrationApplicationIds.MachineStudio,
                            StringComparison.Ordinal));
                    restartBoundaryReached = true;
                    status = "ACK published; consumer exited before inspection for peer-restart recovery.";
                    return new(
                        failures.Count == 0 && checks.Count > 0 && checks.Values.All(value => value),
                        failures.Count == 0 ? null : string.Join("; ", failures));
                }

                if (cancellationExpected)
                {
                    await integration.PushSelectedTransactionAsync();
                    cancellationToken.ThrowIfCancellationRequested();
                    Check(
                        "acknowledgementPushedToMachine",
                        integration.LastTcpTransferText.Contains(
                            "push",
                            StringComparison.OrdinalIgnoreCase)
                        && integration.LastTcpTransferText.Contains(
                            IntegrationApplicationIds.MachineStudio,
                            StringComparison.Ordinal));
                }
            }

            Check("runInspectionCommandEnabled", integration.RunInspectionCommand.CanExecute(null));
            if (preRunDelayMilliseconds > 0)
            {
                SignalReadyFile();
                await Task.Delay(preRunDelayMilliseconds, cancellationToken);
                Check("preRunDelayApplied", true);
            }

            integration.RunInspectionCommand.Execute(null);
            await yieldUi();
            cancellationToken.ThrowIfCancellationRequested();
            integration.RefreshHandoffsCommand.Execute(null);
            selected = integration.SelectedTransaction
                ?? throw new InvalidOperationException("The 3D transaction disappeared after Publish.");
            Check("resultCreated", selected.HasResult);
            var publishedResult = ThreeDIntegrationV2Exchange.ReadResult(
                root,
                selected.TransactionId);
            if (cancellationExpected)
            {
                cancelledResultObserved = selected.ResultSummary.Contains(
                        "Cancelled",
                        StringComparison.Ordinal)
                    && selected.ResultSummary.Contains(
                        "Indeterminate",
                        StringComparison.Ordinal)
                    && publishedResult.Status == IntegrationResultStatus.Cancelled
                    && publishedResult.Outcome == IntegrationInspectionOutcome.Indeterminate
                    && publishedResult.Error?.Code == IntegrationErrorCode.Cancelled;
                Check("cancelledResultObserved", cancelledResultObserved);
            }
            else if (executionErrorExpected)
            {
                executionErrorObserved = selected.ResultSummary.Contains(
                        "Failed",
                        StringComparison.Ordinal)
                    && selected.ResultSummary.Contains(
                        "ExecutionError",
                        StringComparison.Ordinal)
                    && publishedResult.Status == IntegrationResultStatus.Failed
                    && publishedResult.Outcome == IntegrationInspectionOutcome.ExecutionError
                    && publishedResult.RunId is null
                    && publishedResult.RunRecord is null
                    && publishedResult.Evidence.Count == 0
                    && publishedResult.Error?.Code == IntegrationErrorCode.ExecutionFailed;
                Check("executionErrorResultObserved", executionErrorObserved);
                Check("executionErrorHasNoRunRecord", publishedResult.RunRecord is null);
                Check("executionErrorHasNoEvidence", publishedResult.Evidence.Count == 0);
            }
            else
            {
                Check(
                    "resultCompletedPass",
                    selected.ResultSummary.Contains("Completed", StringComparison.Ordinal)
                    && selected.ResultSummary.Contains("Pass", StringComparison.Ordinal));
                Check(
                    "resultHasRunId",
                    selected.ResultSummary.Contains("Run ", StringComparison.Ordinal)
                    && !selected.ResultSummary.EndsWith("Run -", StringComparison.Ordinal));
                var runRecordArtifact = publishedResult.RunRecord;
                var runRecordPath = runRecordArtifact is null
                    ? null
                    : Path.Combine(
                        root,
                        IntegrationTransactionLayout.TransactionsDirectoryName,
                        selected.TransactionId.ToString("D"),
                        runRecordArtifact.RelativePath.Replace('/', Path.DirectorySeparatorChar));
                var runRecord = runRecordPath is null || !File.Exists(runRecordPath)
                    ? null
                    : InspectionRunRecordJson.Read(runRecordPath);
                Check(
                    "actualRunRecordPublished",
                    runRecord is not null
                    && runRecord.RunId == publishedResult.RunId
                    && runRecord.Status is ResultStatus.Pass or ResultStatus.Fail
                    && string.Equals(
                        runRecord.Source.Sha256,
                        publishedResult.Correlation.InputSha256,
                        StringComparison.OrdinalIgnoreCase)
                    && string.Equals(
                        runRecord.Recipe.Sha256,
                        publishedResult.Correlation.RecipeSha256,
                        StringComparison.OrdinalIgnoreCase));
                var imagePreview = publishedResult.Evidence.SingleOrDefault(evidence =>
                    string.Equals(
                        evidence.Role,
                        IntegrationArtifactRoles.ResultEvidence,
                        StringComparison.Ordinal));
                var imagePreviewPath = imagePreview is null
                    ? null
                    : Path.Combine(
                        root,
                        IntegrationTransactionLayout.TransactionsDirectoryName,
                        selected.TransactionId.ToString("D"),
                        imagePreview.RelativePath.Replace('/', Path.DirectorySeparatorChar));
                Check(
                    "resultImagePreviewPublished",
                    imagePreview is not null
                    && string.Equals(
                        imagePreview.ArtifactId,
                        ThreeDHeightImagePreviewArtifact.ResultEvidenceArtifactId,
                        StringComparison.Ordinal)
                    && imagePreviewPath is not null
                    && File.Exists(imagePreviewPath)
                    && HasPngSignature(imagePreviewPath));
            }

            await integration.PushSelectedTransactionAsync();
            cancellationToken.ThrowIfCancellationRequested();
            Check(
                "resultPushedToMachine",
                integration.LastTcpTransferText.Contains(
                    "push",
                    StringComparison.OrdinalIgnoreCase)
                && integration.LastTcpTransferText.Contains(
                    IntegrationApplicationIds.MachineStudio,
                    StringComparison.Ordinal));
            if (resultReadyFilePath is not null)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(resultReadyFilePath)!);
                File.WriteAllText(resultReadyFilePath, selected.TransactionId.ToString("D"));
                Check("consumerResultReadyFileWritten", true);
            }
            status = integration.StatusText;
            var holdMilliseconds = ParseMilliseconds(
                commandLine.GetValueIgnoreCase("--smoke-integration-exe-hold-ms"),
                0,
                0,
                30000);
            if (holdMilliseconds > 0)
            {
                await Task.Delay(holdMilliseconds, cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            Check("shellLifetimeNotCanceled", false);
            status = "Integration EXE Smoke canceled by Shell lifetime.";
        }
        catch (Exception exception)
        {
            failures.Add(exception.GetBaseException().Message);
            status = exception.GetBaseException().ToString();
        }
        finally
        {
            try
            {
                if (integration.IsTcpListening)
                {
                    await integration.StopTcpListenerAsync();
                }
            }
            catch (Exception exception)
            {
                failures.Add("TCP listener cleanup: " + exception.GetBaseException().Message);
            }

            SaveReport(
                reportTarget,
                transactionId,
                status,
                checks,
                failures,
                restartBoundaryReached,
                recoveredExistingTransaction,
                reopenedExistingResult,
                cancellationExpected,
                cancelledResultObserved,
                executionErrorExpected,
                executionErrorObserved);
        }

        return new(
            failures.Count == 0 && checks.Count > 0 && checks.Values.All(value => value),
            failures.Count == 0 ? null : string.Join("; ", failures));
    }

    private static bool HasPngSignature(string path)
    {
        var bytes = File.ReadAllBytes(path);
        return bytes.Length >= 8
            && bytes.AsSpan(0, 8).SequenceEqual(
                new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A });
    }

    private static void SaveReport(
        string path,
        Guid? transactionId,
        string status,
        IReadOnlyDictionary<string, bool> checks,
        IReadOnlyList<string> failures,
        bool restartBoundaryReached = false,
        bool recoveredExistingTransaction = false,
        bool reopenedExistingResult = false,
        bool cancellationExpected = false,
        bool cancelledResultObserved = false,
        bool executionErrorExpected = false,
        bool executionErrorObserved = false)
    {
        var report = new ShellIntegrationExchangeExeSmokeReport
        {
            TransactionId = transactionId?.ToString("D"),
            RestartBoundaryReached = restartBoundaryReached,
            RecoveredExistingTransaction = recoveredExistingTransaction,
            ReopenedExistingResult = reopenedExistingResult,
            CancellationExpected = cancellationExpected,
            CancelledResultObserved = cancelledResultObserved,
            ExecutionErrorExpected = executionErrorExpected,
            ExecutionErrorObserved = executionErrorObserved,
            Status = status,
            Checks = checks,
            Failures = failures
        };
        report.Save(path);
    }

    private static async Task WaitForAsync(
        Func<bool> condition,
        TimeSpan timeout,
        string failureMessage,
        CancellationToken cancellationToken)
    {
        var deadline = DateTimeOffset.UtcNow + timeout;
        while (DateTimeOffset.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (condition())
            {
                return;
            }

            await Task.Delay(100, cancellationToken);
        }

        cancellationToken.ThrowIfCancellationRequested();
        throw new TimeoutException(failureMessage);
    }

    private static int ParsePort(string? value, int defaultValue)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return defaultValue;
        }

        if (!int.TryParse(value, out var port) || port is < 1 or > 65535)
        {
            throw new ArgumentException("Integration smoke TCP port must be between 1 and 65535.");
        }

        return port;
    }

    private static int ParseMilliseconds(string? value, int defaultValue, int minimum, int maximum)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return defaultValue;
        }

        if (!int.TryParse(value, out var milliseconds)
            || milliseconds < minimum
            || milliseconds > maximum)
        {
            throw new ArgumentException(
                $"Integration smoke hold must be between {minimum} and {maximum} milliseconds.");
        }

        return milliseconds;
    }

    private static Guid? ParseOptionalGuid(string? value, string argumentName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (!Guid.TryParse(value, out var parsed) || parsed == Guid.Empty)
        {
            throw new ArgumentException(
                $"Argument '{argumentName}' must contain a non-empty transaction ID.");
        }

        return parsed;
    }

    private static string RequireArgument(ShellCommandLineArguments commandLine, string name) =>
        commandLine.GetValueIgnoreCase(name) is { Length: > 0 } value
            ? Path.GetFullPath(value)
            : throw new ArgumentException($"Missing required argument '{name}'.");
}
