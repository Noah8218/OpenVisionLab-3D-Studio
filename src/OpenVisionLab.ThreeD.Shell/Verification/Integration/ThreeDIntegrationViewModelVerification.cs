using System.Security.Cryptography;
using System.IO;
using OpenVisionLab.Integration.Contracts;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Reporting.RunRecords;
using OpenVisionLab.ThreeD.Shell.ViewModels.Integration;

namespace OpenVisionLab.ThreeD.Shell.Verification.Integration;

internal static class ThreeDIntegrationViewModelVerification
{
    public static bool Verify(string reportPath, out string summary)
    {
        var lines = new List<string>
        {
            "OpenVisionLab 3D Integration ViewModel verification",
            $"Generated: {DateTimeOffset.UtcNow:O}"
        };
        var fixtureRoot = Path.Combine(
            Path.GetTempPath(),
            "OpenVisionLab-3D-Integration-UI",
            Guid.NewGuid().ToString("N"));
        var passed = 0;

        try
        {
            Directory.CreateDirectory(fixtureRoot);
            var exchangeRoot = Path.Combine(fixtureRoot, "exchange");
            var settingsPath = Path.Combine(fixtureRoot, "settings.json");
            var runRecordPath = Path.Combine(fixtureRoot, "run-record.json");
            Directory.CreateDirectory(exchangeRoot);
            var handoff = WriteHandoff(exchangeRoot);
            WriteRunRecord(runRecordPath);

            var setup = CreateViewModel(settingsPath, runRecordPath);
            setup.ExchangeRoot = exchangeRoot;
            setup.RefreshHandoffsCommand.Execute(null);
            Check(
                "unsaved refresh reports status without throwing",
                setup.Transactions.Count == 0 && !string.IsNullOrWhiteSpace(setup.StatusText),
                setup.StatusText);
            setup.SaveSetupCommand.Execute(null);
            Check("setup saved", File.Exists(settingsPath), setup.StatusText);
            Check("save does not scan", setup.Transactions.Count == 0, setup.Transactions.Count.ToString());
            Check("save does not acknowledge", !AcknowledgementExists(exchangeRoot, handoff.TransactionId), "acknowledgement absent");

            var restored = CreateViewModel(settingsPath, runRecordPath);
            Check("setup restored", restored.ExchangeRoot == Path.GetFullPath(exchangeRoot), restored.ExchangeRoot);
            Check("restore does not scan", restored.Transactions.Count == 0, restored.Transactions.Count.ToString());
            Check("restore does not acknowledge", !AcknowledgementExists(exchangeRoot, handoff.TransactionId), "acknowledgement absent");

            restored.RefreshHandoffsCommand.Execute(null);
            Check("explicit refresh finds handoff", restored.Transactions.Count == 1, restored.StatusText);
            Check("explicit refresh does not acknowledge", !AcknowledgementExists(exchangeRoot, handoff.TransactionId), "acknowledgement absent");
            restored.RefreshHandoffsCommand.Execute(null);
            Check("repeated refresh remains read-only and stable", restored.Transactions.Count == 1 && !AcknowledgementExists(exchangeRoot, handoff.TransactionId), restored.StatusText);

            restored.AcceptCommand.Execute(null);
            Check("explicit accept writes acknowledgement", AcknowledgementExists(exchangeRoot, handoff.TransactionId), restored.StatusText);
            Check("accept does not publish result", !ResultExists(exchangeRoot, handoff.TransactionId), "result absent");

            restored.PublishResultCommand.Execute(null);
            Check("explicit publish writes result", ResultExists(exchangeRoot, handoff.TransactionId), restored.StatusText);
            Check(
                "published state visible",
                restored.SelectedTransaction?.State.Contains("published", StringComparison.OrdinalIgnoreCase) == true
                || restored.SelectedTransaction?.State.Contains("게시", StringComparison.Ordinal) == true,
                restored.SelectedTransaction?.State ?? "none");

            restored.ResetSetupCommand.Execute(null);
            var reset = CreateViewModel(settingsPath, runRecordPath);
            Check("reset clears root", string.IsNullOrEmpty(reset.ExchangeRoot), reset.ExchangeRoot);
            Check("reset does not mutate transaction", ResultExists(exchangeRoot, handoff.TransactionId), "result preserved");
        }
        catch (Exception exception)
        {
            lines.Add($"FAIL|unhandled|{exception.GetType().Name}: {exception.Message}");
        }
        finally
        {
            try
            {
                if (Directory.Exists(fixtureRoot))
                {
                    Directory.Delete(fixtureRoot, recursive: true);
                }
            }
            catch
            {
            }
        }

        var failed = lines.Count(line => line.StartsWith("FAIL|", StringComparison.Ordinal));
        var directory = Path.GetDirectoryName(Path.GetFullPath(reportPath));
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }
        File.WriteAllLines(reportPath, lines);
        summary = failed == 0
            ? $"3D integration ViewModel verification PASS ({passed} checks)"
            : $"3D integration ViewModel verification FAIL ({passed} passed, {failed} failed)";
        return failed == 0;

        void Check(string name, bool condition, string detail)
        {
            lines.Add($"{(condition ? "PASS" : "FAIL")}|{name}|{detail}");
            if (condition)
            {
                passed++;
            }
        }
    }

    private static ThreeDIntegrationViewModel CreateViewModel(string settingsPath, string runRecordPath) =>
        new(
            () => runRecordPath,
            settingsPath,
            () => new IntegrationApplicationIdentity(
                IntegrationApplicationIds.ThreeDStudio,
                "0.1.0",
                new string('2', 40),
                IntegrationSourceState.Clean));

    private static IntegrationHandoff WriteHandoff(string exchangeRoot)
    {
        var transactionId = Guid.NewGuid();
        var transactionDirectory = Path.Combine(exchangeRoot, "transactions", transactionId.ToString("D"));
        var artifactsDirectory = Path.Combine(transactionDirectory, "artifacts");
        Directory.CreateDirectory(artifactsDirectory);
        var projectPath = Path.Combine(artifactsDirectory, "machine-project.ovmachine");
        var sourcePath = Path.Combine(artifactsDirectory, "inspection-source.c3d");
        File.WriteAllText(projectPath, "{}");
        File.WriteAllBytes(sourcePath, [1, 2, 3, 4]);
        var handoff = new IntegrationHandoff(
            IntegrationContractSchema.Current,
            IntegrationMessageKind.Handoff,
            Guid.NewGuid(),
            transactionId,
            DateTimeOffset.UtcNow,
            new IntegrationApplicationIdentity(
                IntegrationApplicationIds.MachineStudio,
                "0.1.0",
                new string('1', 40),
                IntegrationSourceState.Clean),
            new MachineInspectionContext(
                "project-1",
                "1.11",
                "sequence-1",
                "step-1",
                "camera-1",
                "mm",
                "camera-1-frame",
                [
                    Artifact(IntegrationArtifactRoles.MachineProject, "project-1", projectPath, "artifacts/machine-project.ovmachine"),
                    Artifact(IntegrationArtifactRoles.InspectionSource, "source-1", sourcePath, "artifacts/inspection-source.c3d")
                ]));
        File.WriteAllBytes(
            Path.Combine(transactionDirectory, IntegrationTransactionLayout.HandoffFileName),
            IntegrationContractJson.Serialize(handoff));
        return handoff;
    }

    private static IntegrationArtifactReference Artifact(string role, string id, string path, string relativePath)
    {
        using var stream = File.OpenRead(path);
        return new(role, id, relativePath, stream.Length, Convert.ToHexString(SHA256.HashData(stream)));
    }

    private static void WriteRunRecord(string path) => InspectionRunRecordJson.Write(
        path,
        new InspectionRunRecord(
            "1.0",
            "run-1",
            DateTimeOffset.UtcNow,
            new InspectionRunRecipe("tool-recipe", "1.0", "recipe.json", new string('A', 64)),
            new InspectionRunSource("source-1", "source.c3d", new string('B', 64), 4, "mm"),
            "Integration Verification",
            ResultStatus.Pass,
            "Completed",
            1.0,
            [],
            [],
            "matched",
            new InspectionRunArtifacts("report.txt", null, null, path, null, null)));

    private static string TransactionDirectory(string root, Guid transactionId) =>
        Path.Combine(root, "transactions", transactionId.ToString("D"));

    private static bool AcknowledgementExists(string root, Guid transactionId) =>
        File.Exists(Path.Combine(TransactionDirectory(root, transactionId), IntegrationTransactionLayout.AcknowledgementFileName));

    private static bool ResultExists(string root, Guid transactionId) =>
        File.Exists(Path.Combine(TransactionDirectory(root, transactionId), IntegrationTransactionLayout.ResultFileName));
}
