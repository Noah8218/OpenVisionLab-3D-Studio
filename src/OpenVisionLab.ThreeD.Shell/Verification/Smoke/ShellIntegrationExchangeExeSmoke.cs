#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using OpenVisionLab.Integration.Contracts;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Reporting.Integration;
using OpenVisionLab.ThreeD.Reporting.RunRecords;
using OpenVisionLab.ThreeD.Shell;
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
    public string Status { get; init; } = string.Empty;
    public required IReadOnlyDictionary<string, bool> Checks { get; init; }
    public required IReadOnlyList<string> Failures { get; init; }
    public bool IsValid => Failures.Count == 0 && Checks.Values.All(value => value);

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
        ArgumentException.ThrowIfNullOrWhiteSpace(role);
        ArgumentNullException.ThrowIfNull(viewModel);
        ArgumentNullException.ThrowIfNull(isShellVisible);
        ArgumentNullException.ThrowIfNull(yieldUi);

        var checks = new Dictionary<string, bool>(StringComparer.Ordinal);
        var failures = new List<string>();
        var transactionId = (Guid?)null;
        var status = string.Empty;
        var reportTarget = string.IsNullOrWhiteSpace(reportPath)
            ? Path.Combine(Path.GetTempPath(), "OpenVisionLab-3D-integration-exe-smoke.json")
            : Path.GetFullPath(reportPath);

        void Check(string name, bool passed)
        {
            checks[name] = passed;
            if (!passed && !failures.Contains(name, StringComparer.Ordinal))
            {
                failures.Add(name);
            }
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
            var args = Environment.GetCommandLineArgs();
            var root = RequireArgument(args, "--smoke-integration-exchange-root");
            var listenPort = ParsePort(
                GetArgumentValue(args, "--smoke-integration-listen-port"),
                45103);
            var peerPort = ParsePort(
                GetArgumentValue(args, "--smoke-integration-peer-port"),
                45101);
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
            Check("consumerSettingsSaved", integration.ExchangeRoot == root);
            Check("consumerWindowLoaded", isShellVisible());

            await integration.StartTcpListenerAsync();
            Check("consumerListening", integration.IsTcpListening);

            var machinePingAccepted = false;
            for (var attempt = 0; attempt < 40 && !machinePingAccepted; attempt++)
            {
                await integration.PingTcpPeerAsync();
                machinePingAccepted = integration.LastTcpTransferText.Contains(
                    IntegrationApplicationIds.MachineStudio,
                    StringComparison.Ordinal);
                if (!machinePingAccepted)
                {
                    await Task.Delay(250);
                }
            }
            Check("machinePingAccepted", machinePingAccepted);

            await WaitForAsync(
                () =>
                {
                    integration.RefreshHandoffsCommand.Execute(null);
                    return integration.Transactions.Count > 0;
                },
                TimeSpan.FromSeconds(60),
                "The 3D consumer did not discover the pushed Machine transaction.");
            Check("handoffDiscovered", integration.Transactions.Count > 0);

            var selected = integration.SelectedTransaction
                ?? throw new InvalidOperationException(
                    "The 3D consumer did not select its discovered transaction.");
            transactionId = selected.TransactionId;
            Check("handoffTargetsThreeD", selected.CanInspectInThreeD);
            Check("handoffStartsWithoutAckOrResult", !selected.HasAcknowledgement && !selected.HasResult);
            Check("acceptCommandEnabled", integration.AcceptCommand.CanExecute(null));

            integration.AcceptCommand.Execute(null);
            await yieldUi();
            integration.RefreshHandoffsCommand.Execute(null);
            selected = integration.SelectedTransaction
                ?? throw new InvalidOperationException("The 3D transaction disappeared after Accept.");
            Check("acknowledgementCreated", selected.HasAcknowledgement);
            Check(
                "acknowledgementAccepted",
                selected.AcknowledgementStatus == IntegrationAcknowledgementStatus.Accepted);
            Check("resultNotCreatedByAccept", !selected.HasResult);

            var handoff = ThreeDIntegrationV2Exchange.ReadHandoff(root, selected.TransactionId);
            var sourceArtifact = handoff.Context.Artifacts.Single(artifact =>
                string.Equals(artifact.Role, IntegrationArtifactRoles.InspectionSource, StringComparison.Ordinal));
            var recipeArtifact = handoff.Context.Artifacts.Single(artifact =>
                string.Equals(artifact.Role, IntegrationArtifactRoles.InspectionRecipe, StringComparison.Ordinal));
            var runRecordPath = Path.Combine(root, "integration-exe-run-record.json");
            var runnerTextReportPath = Path.Combine(root, "integration-exe-runner.txt");
            File.WriteAllText(
                runnerTextReportPath,
                $"3D integration EXE smoke; transaction={selected.TransactionId:D}; outcome=Pass");
            InspectionRunRecordJson.Write(
                runRecordPath,
                new InspectionRunRecord(
                    "1.0",
                    "run-cross-repo-3d-smoke",
                    DateTimeOffset.UtcNow,
                    new InspectionRunRecipe(
                        "tool-recipe",
                        "1.0",
                        recipeArtifact.RelativePath,
                        handoff.Context.RecipeSha256),
                    new InspectionRunSource(
                        "integration-source",
                        sourceArtifact.RelativePath,
                        handoff.Context.InputSha256,
                        sourceArtifact.ByteLength,
                        handoff.Context.Unit),
                    "Integration EXE Smoke",
                    ResultStatus.Pass,
                    "Completed",
                    1.0,
                    [new InspectionRunMetric(
                        "height",
                        MetricKind.Length,
                        1.25,
                        handoff.Context.Unit,
                        ResultStatus.Pass)],
                    [],
                    "matched",
                    new InspectionRunArtifacts(
                        runnerTextReportPath,
                        null,
                        null,
                        runRecordPath,
                        null,
                        null)));
            Check("runRecordWritten", File.Exists(runRecordPath));
            Check(
                "runRecordLoadedByShell",
                viewModel.LoadRunRecord(runRecordPath, out _));
            integration.SyncRunRecord();
            Check("publishCommandEnabled", integration.PublishResultCommand.CanExecute(null));

            integration.PublishResultCommand.Execute(null);
            await yieldUi();
            integration.RefreshHandoffsCommand.Execute(null);
            selected = integration.SelectedTransaction
                ?? throw new InvalidOperationException("The 3D transaction disappeared after Publish.");
            Check("resultCreated", selected.HasResult);
            Check(
                "resultCompletedPass",
                selected.ResultSummary.Contains("Completed", StringComparison.Ordinal)
                && selected.ResultSummary.Contains("Pass", StringComparison.Ordinal));
            Check(
                "resultHasRunId",
                selected.ResultSummary.Contains("run-cross-repo-3d-smoke", StringComparison.Ordinal));

            await integration.PushSelectedTransactionAsync();
            Check(
                "resultPushedToMachine",
                integration.LastTcpTransferText.Contains(
                    "push",
                    StringComparison.OrdinalIgnoreCase)
                && integration.LastTcpTransferText.Contains(
                    IntegrationApplicationIds.MachineStudio,
                    StringComparison.Ordinal));
            status = integration.StatusText;
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

            SaveReport(reportTarget, transactionId, status, checks, failures);
        }

        return new(
            failures.Count == 0 && checks.Values.All(value => value),
            failures.Count == 0 ? null : string.Join("; ", failures));
    }

    private static void SaveReport(
        string path,
        Guid? transactionId,
        string status,
        IReadOnlyDictionary<string, bool> checks,
        IReadOnlyList<string> failures)
    {
        var report = new ShellIntegrationExchangeExeSmokeReport
        {
            TransactionId = transactionId?.ToString("D"),
            Status = status,
            Checks = checks,
            Failures = failures
        };
        report.Save(path);
    }

    private static async Task WaitForAsync(
        Func<bool> condition,
        TimeSpan timeout,
        string failureMessage)
    {
        var deadline = DateTimeOffset.UtcNow + timeout;
        while (DateTimeOffset.UtcNow < deadline)
        {
            if (condition())
            {
                return;
            }

            await Task.Delay(100);
        }

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

    private static string RequireArgument(IReadOnlyList<string> args, string name) =>
        GetArgumentValue(args, name) is { Length: > 0 } value
            ? Path.GetFullPath(value)
            : throw new ArgumentException($"Missing required argument '{name}'.");

    private static string? GetArgumentValue(IReadOnlyList<string> args, string name)
    {
        for (var index = 0; index < args.Count - 1; index++)
        {
            if (string.Equals(args[index], name, StringComparison.OrdinalIgnoreCase))
            {
                return args[index + 1];
            }
        }

        return null;
    }
}
