using System.Security.Cryptography;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using OpenVisionLab.Integration.Contracts;
using OpenVisionLab.Integration.Transport.Tcp;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Data;
using OpenVisionLab.ThreeD.Reporting.Integration;
using OpenVisionLab.ThreeD.Reporting.RunRecords;
using OpenVisionLab.ThreeD.Shell;
using OpenVisionLab.ThreeD.Shell.Dialogs;
using OpenVisionLab.ThreeD.Shell.ViewModels.Integration;

namespace OpenVisionLab.ThreeD.Verification.Integration;

internal static class ThreeDIntegrationViewModelVerification
{
    public static bool VerifyTcpOperationLifecycle(string reportPath, out string summary)
    {
        var lines = new List<string>
        {
            "OpenVisionLab 3D Integration TCP operation lifecycle verification",
            $"Generated: {DateTimeOffset.UtcNow:O}"
        };
        var fixtureRoot = Path.Combine(
            Path.GetTempPath(),
            "OpenVisionLab-3D-Integration-TCP-Lifecycle",
            Guid.NewGuid().ToString("N"));
        var passed = 0;

        try
        {
            Directory.CreateDirectory(fixtureRoot);
            Task.Run(() => VerifyTcpExchangeAsync(fixtureRoot, Check))
                .GetAwaiter()
                .GetResult();
            Task.Run(() => VerifyTcpOperationLifecycleAsync(fixtureRoot, Check))
                .GetAwaiter()
                .GetResult();
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
            ? $"3D integration TCP lifecycle verification PASS ({passed} checks)"
            : $"3D integration TCP lifecycle verification FAIL ({passed} passed, {failed} failed)";
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
            VerifyBuildIdentity(fixtureRoot, Check);
            var exchangeRoot = Path.Combine(fixtureRoot, "exchange");
            var legacyExchangeRoot = Path.Combine(fixtureRoot, "legacy-exchange");
            var workflowRoot = Path.Combine(fixtureRoot, "workflow-direct");
            var settingsPath = Path.Combine(fixtureRoot, "settings.json");
            var runRecordPath = Path.Combine(fixtureRoot, "run-record.json");
            var workflowRunRecordPath = Path.Combine(workflowRoot, "run-record.json");
            var sourceMismatchRunRecordPath = Path.Combine(
                fixtureRoot,
                "source-mismatch-run-record.json");
            var recipeMismatchRunRecordPath = Path.Combine(
                fixtureRoot,
                "recipe-mismatch-run-record.json");
            Directory.CreateDirectory(exchangeRoot);
            Directory.CreateDirectory(legacyExchangeRoot);
            Directory.CreateDirectory(workflowRoot);
            var handoff = WriteV2Handoff(exchangeRoot);
            WriteRunRecord(
                runRecordPath,
                handoff.Context.InputSha256,
                handoff.Context.RecipeSha256);
            WriteRunRecord(
                sourceMismatchRunRecordPath,
                new string('C', 64),
                handoff.Context.RecipeSha256);
            WriteRunRecord(
                recipeMismatchRunRecordPath,
                handoff.Context.InputSha256,
                new string('D', 64));

            var uncommittedPublicationDirectory = Path.Combine(
                fixtureRoot,
                "run-record-publication-uncommitted");
            string uncommittedArtifactPath;
            using (var publication = ThreeDIntegrationRunRecordPublication.Create(
                       runRecordPath,
                       uncommittedPublicationDirectory,
                       handoff.Context))
            {
                uncommittedArtifactPath = publication.ArtifactPath;
                Check(
                    "run record publication projects the existing record",
                    publication.RunId == "run-1"
                    && publication.Outcome == IntegrationInspectionOutcome.Pass
                    && publication.Disposition == IntegrationInspectionDisposition.Pass
                    && publication.Metrics.Count == 2
                    && File.Exists(publication.ArtifactPath),
                    $"run={publication.RunId}; outcome={publication.Outcome}; metrics={publication.Metrics.Count}");
            }
            Check(
                "uncommitted run record publication cleans the copied artifact",
                !File.Exists(uncommittedArtifactPath),
                uncommittedArtifactPath);
            Check(
                "uncommitted run record publication cleans temporary siblings",
                !Directory.GetFiles(
                    Path.GetDirectoryName(uncommittedArtifactPath)!,
                    "*.tmp.*").Any(),
                uncommittedArtifactPath);

            var committedPublicationDirectory = Path.Combine(
                fixtureRoot,
                "run-record-publication-committed");
            string committedArtifactPath;
            using (var publication = ThreeDIntegrationRunRecordPublication.Create(
                       runRecordPath,
                       committedPublicationDirectory,
                       handoff.Context))
            {
                committedArtifactPath = publication.ArtifactPath;
                publication.Commit();
            }
            Check(
                "committed run record publication preserves the copied artifact",
                File.Exists(committedArtifactPath),
                committedArtifactPath);
            Check(
                "committed run record publication preserves source bytes",
                File.ReadAllBytes(committedArtifactPath).AsSpan().SequenceEqual(
                    File.ReadAllBytes(runRecordPath)),
                committedArtifactPath);
            Check(
                "committed run record publication cleans temporary siblings",
                !Directory.GetFiles(
                    Path.GetDirectoryName(committedArtifactPath)!,
                    "*.tmp.*").Any(),
                committedArtifactPath);

            var existingArtifactDirectory = Path.Combine(
                fixtureRoot,
                "run-record-publication-existing",
                IntegrationTransactionLayout.ArtifactsDirectoryName);
            Directory.CreateDirectory(existingArtifactDirectory);
            var existingArtifactPath = Path.Combine(
                existingArtifactDirectory,
                "3d-run-record.json");
            var existingArtifactSentinel = "pre-existing-run-record-artifact";
            File.WriteAllText(existingArtifactPath, existingArtifactSentinel);
            var existingArtifactRejected = false;
            try
            {
                using var unusedPublication = ThreeDIntegrationRunRecordPublication.Create(
                    runRecordPath,
                    Path.Combine(fixtureRoot, "run-record-publication-existing"),
                    handoff.Context);
            }
            catch (IOException)
            {
                existingArtifactRejected = true;
            }

            Check(
                "existing run record artifact rejects overwrite",
                existingArtifactRejected,
                existingArtifactPath);
            Check(
                "existing run record artifact remains intact after rejection",
                File.ReadAllText(existingArtifactPath) == existingArtifactSentinel,
                existingArtifactPath);
            Check(
                "existing run record artifact rejection cleans temporary siblings",
                !Directory.GetFiles(existingArtifactDirectory, "*.tmp.*").Any(),
                existingArtifactPath);

            var workflowHandoff = WriteV2Handoff(workflowRoot);
            WriteRunRecord(
                workflowRunRecordPath,
                workflowHandoff.Context.InputSha256,
                workflowHandoff.Context.RecipeSha256);
            var transactionWorkflow = new ThreeDIntegrationTransactionWorkflow(
                _ => CreateConsumerIdentity(),
                (_, _, english) => english);
            var workflowItems = transactionWorkflow.Discover(workflowRoot);
            Check(
                "direct transaction workflow discovery is read-only",
                workflowItems.Count == 1
                && !AcknowledgementExists(workflowRoot, workflowHandoff.TransactionId)
                && !ResultExists(workflowRoot, workflowHandoff.TransactionId),
                $"items={workflowItems.Count}; ack={AcknowledgementExists(workflowRoot, workflowHandoff.TransactionId)}; result={ResultExists(workflowRoot, workflowHandoff.TransactionId)}");
            var workflowStatus = transactionWorkflow.Review(workflowRoot, workflowItems[0], rejectionReason: null);
            var workflowPublished = transactionWorkflow.PublishResult(
                workflowRoot,
                workflowItems[0],
                workflowRunRecordPath);
            Check(
                "direct transaction workflow preserves review and publish",
                workflowStatus == IntegrationAcknowledgementStatus.Accepted
                && ResultExists(workflowRoot, workflowHandoff.TransactionId)
                && workflowPublished.Outcome == IntegrationInspectionOutcome.Pass.ToString()
                && workflowPublished.RunId == "run-1",
                $"ack={workflowStatus}; outcome={workflowPublished.Outcome}; run={workflowPublished.RunId}");
            var workflowLegacyRoot = Path.Combine(fixtureRoot, "workflow-legacy");
            Directory.CreateDirectory(workflowLegacyRoot);
            var workflowLegacyHandoff = WriteLegacyHandoff(workflowLegacyRoot);
            var workflowLegacyItems = transactionWorkflow.Discover(workflowLegacyRoot);
            var workflowLegacyStatus = transactionWorkflow.Review(
                workflowLegacyRoot,
                workflowLegacyItems[0],
                rejectionReason: null);
            var workflowLegacyPublished = transactionWorkflow.PublishResult(
                workflowLegacyRoot,
                workflowLegacyItems[0],
                runRecordPath);
            Check(
                "direct transaction workflow preserves legacy review and publish",
                workflowLegacyItems.Count == 1
                && workflowLegacyStatus == IntegrationAcknowledgementStatus.Accepted
                && ResultExists(workflowLegacyRoot, workflowLegacyHandoff.TransactionId)
                && workflowLegacyPublished.Outcome == IntegrationInspectionDisposition.Pass.ToString()
                && workflowLegacyPublished.RunId == "run-1",
                $"items={workflowLegacyItems.Count}; ack={workflowLegacyStatus}; outcome={workflowLegacyPublished.Outcome}; run={workflowLegacyPublished.RunId}");

            var recipeRunRoot = Path.Combine(fixtureRoot, "workflow-recipe-run");
            var recipeRunSettingsPath = Path.Combine(fixtureRoot, "workflow-recipe-run-settings.json");
            var recipeRunRecordPath = Path.Combine(fixtureRoot, "workflow-recipe-run-record.json");
            Directory.CreateDirectory(recipeRunRoot);
            var recipeRunHandoff = WriteValidHeightMapHandoff(recipeRunRoot);
            var recipeRunViewModel = CreateViewModel(recipeRunSettingsPath, recipeRunRecordPath);
            try
            {
                recipeRunViewModel.ExchangeRoot = recipeRunRoot;
                recipeRunViewModel.SaveSetupCommand.Execute(null);
                recipeRunViewModel.RefreshHandoffsCommand.Execute(null);
                Check(
                    "recipe execution command discovers an actionable 3D handoff",
                    recipeRunViewModel.Transactions.Count == 1
                    && recipeRunViewModel.SelectedTransaction?.TransactionId == recipeRunHandoff.TransactionId
                    && recipeRunViewModel.SelectedTransaction.CanInspectInThreeD
                    && !recipeRunViewModel.SelectedTransaction.HasAcknowledgement,
                    recipeRunViewModel.StatusText);

                recipeRunViewModel.AcceptCommand.Execute(null);
                recipeRunViewModel.RefreshHandoffsCommand.Execute(null);
                Check(
                    "recipe execution command becomes available after explicit review",
                    recipeRunViewModel.SelectedTransaction?.AcknowledgementStatus == IntegrationAcknowledgementStatus.Accepted
                    && recipeRunViewModel.RunInspectionCommand.CanExecute(null),
                    recipeRunViewModel.SelectedTransaction?.Detail ?? "no selection");

                recipeRunViewModel.RunInspectionCommand.Execute(null);
                recipeRunViewModel.RefreshHandoffsCommand.Execute(null);
                var recipeRunResult = ThreeDIntegrationV2Exchange.ReadResult(
                    recipeRunRoot,
                    recipeRunHandoff.TransactionId);
                var publishedRecordPath = Path.Combine(
                    recipeRunRoot,
                    IntegrationTransactionLayout.TransactionsDirectoryName,
                    recipeRunHandoff.TransactionId.ToString("D"),
                    IntegrationTransactionLayout.ArtifactsDirectoryName,
                    "3d-run-record.json");
                Check(
                    "RunInspectionCommand executes the copied recipe and publishes a real Run Record",
                    recipeRunResult.Status == IntegrationResultStatus.Completed
                    && recipeRunResult.Outcome == IntegrationInspectionOutcome.Pass
                    && !string.IsNullOrWhiteSpace(recipeRunResult.RunId)
                    && File.Exists(publishedRecordPath)
                    && recipeRunResult.Correlation.InputSha256 == recipeRunHandoff.Context.InputSha256
                    && recipeRunResult.Correlation.RecipeSha256 == recipeRunHandoff.Context.RecipeSha256,
                    $"status={recipeRunResult.Status}; outcome={recipeRunResult.Outcome}; run={recipeRunResult.RunId}; record={File.Exists(publishedRecordPath)}");
                Check(
                    "RunInspectionCommand is disabled after the result is published",
                    !recipeRunViewModel.RunInspectionCommand.CanExecute(null)
                    && recipeRunViewModel.SelectedTransaction?.HasResult == true,
                    recipeRunViewModel.SelectedTransaction?.Detail ?? "no selection");
            }
            finally
            {
                recipeRunViewModel.DisposeAsync().AsTask().GetAwaiter().GetResult();
            }

            var failedRecipeRoot = Path.Combine(fixtureRoot, "workflow-failed-recipe");
            var failedRecipeSettingsPath = Path.Combine(fixtureRoot, "workflow-failed-recipe-settings.json");
            Directory.CreateDirectory(failedRecipeRoot);
            var failedRecipeHandoff = WriteValidHeightMapHandoff(failedRecipeRoot);
            var failedRecipePath = Path.Combine(
                TransactionDirectory(failedRecipeRoot, failedRecipeHandoff.TransactionId),
                IntegrationTransactionLayout.ArtifactsDirectoryName,
                "inspection-recipe.json");
            File.WriteAllText(failedRecipePath, "{", new UTF8Encoding(false));
            var failedRecipeArtifact = Artifact(
                IntegrationArtifactRoles.InspectionRecipe,
                "recipe-recipe-run",
                failedRecipePath,
                "artifacts/inspection-recipe.json");
            var failedRecipeContext = new IntegrationInspectionContextV2(
                failedRecipeHandoff.Context.ProjectId,
                failedRecipeHandoff.Context.ProjectSchema,
                failedRecipeHandoff.Context.SequenceId,
                failedRecipeHandoff.Context.StepId,
                failedRecipeHandoff.Context.CameraId,
                failedRecipeHandoff.Context.AcquisitionId,
                failedRecipeHandoff.Context.FrameId,
                failedRecipeHandoff.Context.Unit,
                failedRecipeHandoff.Context.Modality,
                failedRecipeHandoff.Context.InputKind,
                failedRecipeHandoff.Context.InputSha256,
                failedRecipeArtifact.Sha256,
                failedRecipeHandoff.Context.ConsumerBuild,
                failedRecipeHandoff.Context.Artifacts
                    .Select(artifact => string.Equals(
                            artifact.Role,
                            IntegrationArtifactRoles.InspectionRecipe,
                            StringComparison.Ordinal)
                        ? failedRecipeArtifact
                        : artifact)
                    .ToArray());
            var failedRecipePersistedHandoff = failedRecipeHandoff with
            {
                Context = failedRecipeContext
            };
            File.WriteAllBytes(
                Path.Combine(
                    TransactionDirectory(failedRecipeRoot, failedRecipeHandoff.TransactionId),
                    IntegrationTransactionLayout.HandoffFileName),
                IntegrationContractJson.SerializeCanonical(failedRecipePersistedHandoff));
            var failedRecipeViewModel = CreateViewModel(
                failedRecipeSettingsPath,
                workflowRunRecordPath);
            try
            {
                failedRecipeViewModel.ExchangeRoot = failedRecipeRoot;
                failedRecipeViewModel.SaveSetupCommand.Execute(null);
                failedRecipeViewModel.RefreshHandoffsCommand.Execute(null);
                failedRecipeViewModel.AcceptCommand.Execute(null);
                failedRecipeViewModel.RefreshHandoffsCommand.Execute(null);
                failedRecipeViewModel.RunInspectionCommand.Execute(null);
                var failedRecipeResult = ThreeDIntegrationV2Exchange.ReadResult(
                    failedRecipeRoot,
                    failedRecipeHandoff.TransactionId);
                Check(
                    "RunInspectionCommand publishes a terminal execution-error Result without a Run Record",
                    failedRecipeResult.Status == IntegrationResultStatus.Failed
                    && failedRecipeResult.Outcome == IntegrationInspectionOutcome.ExecutionError
                    && failedRecipeResult.RunId is null
                    && failedRecipeResult.RunRecord is null
                    && failedRecipeResult.Error?.Code == IntegrationErrorCode.ExecutionFailed
                    && failedRecipeViewModel.SelectedTransaction?.HasResult == true
                    && !failedRecipeViewModel.RunInspectionCommand.CanExecute(null),
                    $"status={failedRecipeResult.Status}; outcome={failedRecipeResult.Outcome}; error={failedRecipeResult.Error?.Code}; run={failedRecipeResult.RunId}");
                Check(
                    "execution-error status keeps the explicit push boundary visible",
                    failedRecipeViewModel.StatusText.Contains("오류", StringComparison.Ordinal)
                    || failedRecipeViewModel.StatusText.Contains("error", StringComparison.OrdinalIgnoreCase),
                    failedRecipeViewModel.StatusText);
            }
            finally
            {
                failedRecipeViewModel.DisposeAsync().AsTask().GetAwaiter().GetResult();
            }

            var setup = CreateViewModel(settingsPath, runRecordPath);
            Check(
                "3D default endpoints align with local 2D peer",
                setup.TcpListenAddress == "127.0.0.1"
                && setup.TcpListenPortText == "45103"
                && setup.TcpPeerHost == "127.0.0.1"
                && setup.TcpPeerPortText == "45102",
                $"listen={setup.TcpListenAddress}:{setup.TcpListenPortText}; peer={setup.TcpPeerHost}:{setup.TcpPeerPortText}");
            setup.ExchangeRoot = exchangeRoot;
            var selectedExchangeRoot = Path.Combine(fixtureRoot, "selected-exchange");
            var dialogHost = new RecordingIntegrationDialogHost(selectedExchangeRoot);
            setup.SetDialogHost(dialogHost);
            setup.BrowseExchangeRootCommand.Execute(null);
            Check(
                "folder selection applies only the dialog result",
                setup.ExchangeRoot == selectedExchangeRoot
                && dialogHost.InitialDirectory == exchangeRoot
                && !string.IsNullOrWhiteSpace(dialogHost.Title),
                $"root={setup.ExchangeRoot}; initial={dialogHost.InitialDirectory}; title={dialogHost.Title}");
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

            var rejectedExchangeRoot = Path.Combine(fixtureRoot, "rejected-exchange");
            var rejectedSettingsPath = Path.Combine(fixtureRoot, "rejected-settings.json");
            Directory.CreateDirectory(rejectedExchangeRoot);
            var rejectedHandoff = WriteV2Handoff(rejectedExchangeRoot);
            var rejector = CreateViewModel(rejectedSettingsPath, runRecordPath);
            rejector.ExchangeRoot = rejectedExchangeRoot;
            rejector.SaveSetupCommand.Execute(null);
            rejector.RefreshHandoffsCommand.Execute(null);
            rejector.RejectionReason = "Q6 negative qualification: consumer policy rejected the Handoff.";
            rejector.RejectCommand.Execute(null);
            var rejectedAcknowledgement = ThreeDIntegrationV2Exchange.ReadAcknowledgement(
                rejectedExchangeRoot,
                rejectedHandoff.TransactionId);
            Check(
                "explicit reject writes rejected acknowledgement",
                rejectedAcknowledgement.Status == IntegrationAcknowledgementStatus.Rejected
                && rejectedAcknowledgement.Error?.Code == IntegrationErrorCode.RequestRejected
                && (rejector.StatusText.Contains("거절", StringComparison.Ordinal)
                    || rejector.StatusText.Contains("Rejected", StringComparison.OrdinalIgnoreCase)),
                $"status={rejectedAcknowledgement.Status}; error={rejectedAcknowledgement.Error?.Code}; text={rejector.StatusText}");
            Check(
                "rejected handoff never publishes a result",
                !ResultExists(rejectedExchangeRoot, rejectedHandoff.TransactionId)
                && !rejector.PublishResultCommand.CanExecute(null),
                "result absent; publish disabled");

            restored.AcceptCommand.Execute(null);
            Check("explicit accept writes acknowledgement", AcknowledgementExists(exchangeRoot, handoff.TransactionId), restored.StatusText);
            Check("accept does not publish result", !ResultExists(exchangeRoot, handoff.TransactionId), "result absent");

            Check(
                "v2 result rejects mismatched source SHA",
                RejectsCorrelationMismatch(() =>
                    ThreeDIntegrationV2Exchange.PublishCompletedResult(
                        exchangeRoot,
                        handoff.TransactionId,
                        CreateConsumerIdentity(),
                        sourceMismatchRunRecordPath))
                && !ResultExists(exchangeRoot, handoff.TransactionId)
                && !PublishedRunRecordExists(exchangeRoot, handoff.TransactionId),
                "correlation mismatch; result and copied Run Record absent");
            Check(
                "v2 result rejects mismatched recipe SHA",
                RejectsCorrelationMismatch(() =>
                    ThreeDIntegrationV2Exchange.PublishCompletedResult(
                        exchangeRoot,
                        handoff.TransactionId,
                        CreateConsumerIdentity(),
                        recipeMismatchRunRecordPath))
                && !ResultExists(exchangeRoot, handoff.TransactionId)
                && !PublishedRunRecordExists(exchangeRoot, handoff.TransactionId),
                "correlation mismatch; result and copied Run Record absent");

            restored.PublishResultCommand.Execute(null);
            Check("explicit publish writes result", ResultExists(exchangeRoot, handoff.TransactionId), restored.StatusText);
            var result = ThreeDIntegrationV2Exchange.ReadResult(
                exchangeRoot,
                handoff.TransactionId);
            Check(
                "v2 result correlation and finite metrics",
                result.Status == IntegrationResultStatus.Completed
                && result.Outcome == IntegrationInspectionOutcome.Pass
                && result.Correlation == IntegrationRunCorrelation.FromContext(handoff.Context)
                && result.Metrics.Count == 2
                && result.Metrics.All(metric => double.IsFinite(metric.Value)),
                $"status={result.Status}; outcome={result.Outcome}; metrics={result.Metrics.Count}");
            Check(
                "published outcome and run are visible",
                restored.StatusText.Contains("Pass", StringComparison.OrdinalIgnoreCase)
                && restored.StatusText.Contains("run-1", StringComparison.Ordinal),
                restored.StatusText);
            Check(
                "published state visible",
                restored.SelectedTransaction?.State.Contains("published", StringComparison.OrdinalIgnoreCase) == true
                || restored.SelectedTransaction?.State.Contains("게시", StringComparison.Ordinal) == true,
                restored.SelectedTransaction?.State ?? "none");

            var legacyHandoff = WriteLegacyHandoff(legacyExchangeRoot);
            var legacyRead = ThreeDIntegrationExchange.ReadHandoff(
                legacyExchangeRoot,
                legacyHandoff.TransactionId);
            Check(
                "legacy v1 handoff remains readable",
                legacyRead.SchemaVersion == IntegrationContractSchema.Legacy,
                legacyRead.SchemaVersion);
            var legacyAcknowledgement = ThreeDIntegrationExchange.PublishAcknowledgement(
                legacyExchangeRoot,
                legacyRead,
                CreateConsumerIdentity());
            var legacyResult = ThreeDIntegrationExchange.PublishCompletedResult(
                legacyExchangeRoot,
                legacyHandoff.TransactionId,
                CreateConsumerIdentity(),
                runRecordPath);
            Check(
                "legacy v1 acknowledgement and result remain writable",
                legacyAcknowledgement.Status == IntegrationAcknowledgementStatus.Accepted
                && legacyResult.Status == IntegrationResultStatus.Completed
                && legacyResult.Disposition == IntegrationInspectionDisposition.Pass,
                $"ack={legacyAcknowledgement.Status}; result={legacyResult.Status}/{legacyResult.Disposition}");

            Task.Run(() => VerifyTcpExchangeAsync(fixtureRoot, Check))
                .GetAwaiter()
                .GetResult();
            Task.Run(() => VerifyTcpViewModelAsync(fixtureRoot, Check))
                .GetAwaiter()
                .GetResult();
            Task.Run(() => VerifyTcpOperationLifecycleAsync(fixtureRoot, Check))
                .GetAwaiter()
                .GetResult();

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

    private static void VerifyBuildIdentity(
        string fixtureRoot,
        Action<string, bool, string> check)
    {
        var missingPath = Path.Combine(fixtureRoot, "missing-runtime.json");
        check(
            "runtime identity rejects a missing manifest",
            RejectsRuntimeIdentity(
                () => IntegrationBuildIdentity.LoadQualifiedIdentity(missingPath),
                IntegrationErrorCode.ArtifactMissing),
            "missing manifest rejected");

        var applicationAssembly = typeof(IntegrationBuildIdentity).Assembly;
        var applicationPath = applicationAssembly.Location;
        using var applicationStream = File.OpenRead(applicationPath);
        var applicationHash = Convert.ToHexString(SHA256.HashData(applicationStream));
        var sourceState = IntegrationBuildIdentity.SourceState.ToLowerInvariant() switch
        {
            "clean" => IntegrationSourceState.Clean,
            "dirty" => IntegrationSourceState.Dirty,
            _ => IntegrationSourceState.Unknown
        };
        var identity = new IntegrationApplicationIdentity(
            IntegrationApplicationIds.ThreeDStudio,
            IntegrationBuildIdentity.Version,
            IntegrationBuildIdentity.SourceCommit,
            sourceState);
        var manifest = new IntegrationRuntimeBuildManifest(
            IntegrationRuntimeBuildManifestContract.SchemaVersion,
            identity,
            new IntegrationRuntimeBinary(
                Path.GetFileName(applicationPath),
                applicationStream.Length,
                applicationHash));
        var validPath = Path.Combine(fixtureRoot, "valid-runtime.json");
        File.WriteAllBytes(
            validPath,
            IntegrationContractJson.SerializeCanonical(manifest));

        var tamperedHash = (applicationHash[0] == '0' ? "1" : "0")
            + applicationHash[1..];
        var tamperedPath = Path.Combine(fixtureRoot, "tampered-runtime.json");
        File.WriteAllBytes(
            tamperedPath,
            IntegrationContractJson.SerializeCanonical(
                manifest with
                {
                    EntryAssembly = manifest.EntryAssembly with
                    {
                        Sha256 = tamperedHash
                    }
                }));
        check(
            "runtime identity rejects a tampered entry assembly hash",
            RejectsRuntimeIdentity(
                () => IntegrationBuildIdentity.LoadQualifiedIdentity(tamperedPath),
                IntegrationErrorCode.ArtifactHashMismatch),
            "tampered SHA-256 rejected");

        check(
            "runtime identity rejects a mismatched target application",
            RejectsRuntimeIdentity(
                () => IntegrationBuildIdentity.LoadQualifiedTargetIdentity(
                    identity with { ApplicationId = IntegrationApplicationIds.TwoDStudio },
                    validPath),
                IntegrationErrorCode.InvalidIdentity),
            "mismatched target application rejected");
    }

    private static ThreeDIntegrationViewModel CreateViewModel(
        string settingsPath,
        string runRecordPath,
        TcpIntegrationOptions? tcpOptions = null) =>
        new(
            () => runRecordPath,
            settingsPath,
            CreateConsumerIdentity,
            tcpOptions);

    private static IntegrationApplicationIdentity CreateConsumerIdentity() =>
        new(
            IntegrationApplicationIds.ThreeDStudio,
            "0.2.0-alpha.1",
            new string('2', 40),
            IntegrationSourceState.Clean);

    private static async Task VerifyTcpExchangeAsync(
        string fixtureRoot,
        Action<string, bool, string> check)
    {
        var sourceRoot = Path.Combine(fixtureRoot, "tcp-source");
        var receiverRoot = Path.Combine(fixtureRoot, "tcp-receiver");
        Directory.CreateDirectory(sourceRoot);
        Directory.CreateDirectory(receiverRoot);
        var handoff = WriteV2Handoff(sourceRoot);
        var sharedKey = RandomNumberGenerator.GetBytes(32);
        var options = new TcpIntegrationOptions
        {
            MaxAttempts = 1,
            ConnectTimeout = TimeSpan.FromSeconds(5),
            IdleTimeout = TimeSpan.FromSeconds(5)
        };

        try
        {
            await using var receiver = new ThreeDIntegrationTcpTransport(
                receiverRoot,
                sharedKey,
                options);
            await using var sender = new ThreeDIntegrationTcpTransport(
                sourceRoot,
                sharedKey,
                options);
            var listenEndpoint = await receiver.StartListeningAsync(
                IPAddress.Loopback,
                0);
            var peer = new TcpIntegrationEndpoint(
                IPAddress.Loopback.ToString(),
                listenEndpoint.Port);

            var ping = await sender.PingAsync(peer);
            check(
                "tcp ping identifies 3D peer",
                ping.PeerApplicationId == IntegrationApplicationIds.ThreeDStudio,
                ping.PeerApplicationId);

            await using (var compatibilityFacade = new ThreeDIntegrationTcpExchange(
                           sourceRoot,
                           sharedKey,
                           options))
            {
                var compatibilityPing = await compatibilityFacade.PingAsync(peer);
                check(
                    "public TCP exchange facade forwards transport operations",
                    compatibilityPing.PeerApplicationId == IntegrationApplicationIds.ThreeDStudio
                    && compatibilityFacade.ExchangeRoot == Path.GetFullPath(sourceRoot),
                    compatibilityPing.PeerApplicationId);
            }

            var pushed = await sender.PushTransactionAsync(peer, handoff.TransactionId);
            check(
                "tcp push publishes immutable transaction",
                pushed.Operation == "push"
                && pushed.TransactionId == handoff.TransactionId
                && pushed.FilesTransferred == 4,
                $"operation={pushed.Operation}; files={pushed.FilesTransferred}");
            var repeatedPush = await sender.PushTransactionAsync(peer, handoff.TransactionId);
            check(
                "tcp repeated push is idempotent",
                repeatedPush.Idempotent
                && repeatedPush.TransactionId == handoff.TransactionId
                && ThreeDIntegrationV2Exchange.DiscoverHandoffs(receiverRoot).Count == 1,
                $"idempotent={repeatedPush.Idempotent}; transaction={repeatedPush.TransactionId}");
            var discovered = ThreeDIntegrationV2Exchange.DiscoverHandoffs(receiverRoot);
            check(
                "tcp receive is discoverable through existing v2 adapter",
                discovered.Count == 1
                && discovered[0].Handoff.TransactionId == handoff.TransactionId,
                $"transactions={discovered.Count}");
            check(
                "tcp receive does not acknowledge or run",
                !AcknowledgementExists(receiverRoot, handoff.TransactionId)
                && !ResultExists(receiverRoot, handoff.TransactionId)
                && !Directory.EnumerateFiles(
                        TransactionDirectory(receiverRoot, handoff.TransactionId),
                        "*run-record*",
                        SearchOption.AllDirectories)
                    .Any(),
                "acknowledgement, result, and Run Record absent");

            var receivedHandoff = ThreeDIntegrationV2Exchange.ReadHandoff(
                receiverRoot,
                handoff.TransactionId);
            ThreeDIntegrationV2Exchange.PublishAcknowledgement(
                receiverRoot,
                receivedHandoff,
                CreateConsumerIdentity());
            var pulled = await sender.PullTransactionAsync(peer, handoff.TransactionId);
            var acknowledgement = ThreeDIntegrationV2Exchange.ReadAcknowledgement(
                sourceRoot,
                handoff.TransactionId);
            check(
                "tcp pull returns explicit acknowledgement",
                pulled.Operation == "pull"
                && acknowledgement.Status == IntegrationAcknowledgementStatus.Accepted
                && !ResultExists(sourceRoot, handoff.TransactionId),
                $"operation={pulled.Operation}; acknowledgement={acknowledgement.Status}");
        }
        finally
        {
            CryptographicOperations.ZeroMemory(sharedKey);
        }
    }

    private static async Task VerifyTcpViewModelAsync(
        string fixtureRoot,
        Action<string, bool, string> check)
    {
        var localRoot = Path.Combine(fixtureRoot, "tcp-view-model-local");
        var peerRoot = Path.Combine(fixtureRoot, "tcp-view-model-peer");
        var settingsPath = Path.Combine(fixtureRoot, "tcp-view-model-settings.json");
        var unusedRunRecordPath = Path.Combine(fixtureRoot, "tcp-view-model-unused-run.json");
        Directory.CreateDirectory(localRoot);
        Directory.CreateDirectory(peerRoot);
        var handoff = WriteTwoDHandoff(localRoot);
        var sharedKey = RandomNumberGenerator.GetBytes(32);
        var encodedKey = Convert.ToBase64String(sharedKey);
        var listenPort = ReserveLoopbackPort();
        var options = new TcpIntegrationOptions
        {
            MaxAttempts = 1,
            ConnectTimeout = TimeSpan.FromSeconds(5),
            IdleTimeout = TimeSpan.FromSeconds(5)
        };

        try
        {
            await using var peer = new TcpIntegrationServer(
                IntegrationApplicationIds.TwoDStudio,
                peerRoot,
                IPAddress.Loopback,
                0,
                sharedKey,
                options);
            await peer.StartAsync();
            var peerEndpoint = peer.LocalEndpoint
                ?? throw new InvalidOperationException("The 2D verification listener has no endpoint.");

            await using var setup = CreateViewModel(settingsPath, unusedRunRecordPath);
            setup.ExchangeRoot = localRoot;
            setup.TcpListenAddress = IPAddress.Loopback.ToString();
            setup.TcpListenPortText = listenPort.ToString();
            setup.TcpPeerHost = IPAddress.Loopback.ToString();
            setup.TcpPeerPortText = peerEndpoint.Port.ToString();
            setup.SetSessionSharedKey(encodedKey);
            setup.SaveSetupCommand.Execute(null);
            check(
                "tcp endpoint setup saves without plaintext shared key",
                File.Exists(settingsPath)
                && !File.ReadAllText(settingsPath).Contains(encodedKey, StringComparison.Ordinal),
                setup.StatusText);

            var activeStart = setup.StartTcpListenerAsync();
            var activeDispose = setup.DisposeAsync().AsTask();
            await Task.WhenAll(activeStart, activeDispose);
            check(
                "dispose awaits active listener operation",
                setup.IsDisposed
                && !setup.IsTcpBusy
                && !setup.IsTcpListening,
                $"disposed={setup.IsDisposed}; busy={setup.IsTcpBusy}; listening={setup.IsTcpListening}");
            await setup.StartTcpListenerAsync();
            check(
                "disposed ViewModel rejects a new listener operation",
                setup.IsDisposed
                && !setup.IsTcpBusy
                && !setup.IsTcpListening,
                setup.StatusText);

            await using var restored = CreateViewModel(settingsPath, unusedRunRecordPath);
            restored.SetSessionSharedKey(encodedKey);
            check(
                "tcp endpoint restore is passive",
                restored.TcpListenAddress == IPAddress.Loopback.ToString()
                && restored.TcpListenPortText == listenPort.ToString()
                && restored.TcpPeerPortText == peerEndpoint.Port.ToString()
                && !restored.IsTcpListening
                && restored.Transactions.Count == 0,
                $"listen={restored.TcpListenAddress}:{restored.TcpListenPortText}; peer={restored.TcpPeerHost}:{restored.TcpPeerPortText}; listening={restored.IsTcpListening}");

            restored.RefreshHandoffsCommand.Execute(null);
            var selected = restored.SelectedTransaction;
            check(
                "3D discovers schema-v2 2D/Image transaction for transport",
                selected?.TransactionId == handoff.TransactionId
                && selected.ModalitySummary.Contains("TwoD/Image", StringComparison.Ordinal)
                && !selected.CanInspectInThreeD,
                selected?.Detail ?? "none");
            check(
                "3D inspection actions fail closed for a 2D transaction",
                !restored.AcceptCommand.CanExecute(null)
                && !restored.RejectCommand.CanExecute(null)
                && !restored.PublishResultCommand.CanExecute(null),
                $"accept={restored.AcceptCommand.CanExecute(null)}; reject={restored.RejectCommand.CanExecute(null)}; publish={restored.PublishResultCommand.CanExecute(null)}");

            var firstStart = restored.StartTcpListenerAsync();
            var duplicateStart = restored.StartTcpListenerAsync();
            await Task.WhenAll(firstStart, duplicateStart);
            check(
                "explicit listener start is guarded against repeated clicks",
                restored.IsTcpListening
                && !restored.StartTcpListenerCommand.CanExecute(null)
                && restored.StopTcpListenerCommand.CanExecute(null),
                restored.TcpListenerStatusText);

            await restored.PingTcpPeerAsync();
            check(
                "ViewModel ping identifies 2D peer",
                restored.LastTcpTransferText.Contains(
                    IntegrationApplicationIds.TwoDStudio,
                    StringComparison.Ordinal),
                restored.LastTcpTransferText);

            await restored.PushSelectedTransactionAsync();
            var peerSequence = ThreeDIntegrationTransactionSequenceReader.ReadValidatedV2Sequence(
                peerRoot,
                handoff.TransactionId);
            var compatibilitySequence = ThreeDIntegrationTcpExchange.ReadValidatedV2Sequence(
                peerRoot,
                handoff.TransactionId);
            check(
                "public TCP exchange facade forwards sequence validation",
                compatibilitySequence.Handoff.TransactionId == peerSequence.Handoff.TransactionId
                && compatibilitySequence.Result?.RunId == peerSequence.Result?.RunId,
                compatibilitySequence.Result?.RunId ?? "no-result");
            check(
                "ViewModel push transfers selected 2D transaction",
                peerSequence.Handoff.TransactionId == handoff.TransactionId
                && restored.LastTcpTransferText.Contains("push", StringComparison.OrdinalIgnoreCase),
                restored.LastTcpTransferText);

            WriteTwoDAcknowledgementAndResult(peerRoot, peerSequence.Handoff);
            await restored.PullSelectedTransactionAsync();
            selected = restored.SelectedTransaction;
            check(
                "ViewModel pull displays validated ACK and Result outcome/run",
                selected?.AcknowledgementSummary.Contains("Accepted", StringComparison.Ordinal) == true
                && selected.ResultSummary.Contains("Completed", StringComparison.Ordinal)
                && selected.ResultSummary.Contains("Pass", StringComparison.Ordinal)
                && selected.ResultSummary.Contains("2d-run-1", StringComparison.Ordinal),
                selected?.Detail ?? "none");
            check(
                "2D transaction remains transport-only after result pull",
                selected?.CanInspectInThreeD == false
                && !restored.AcceptCommand.CanExecute(null)
                && !restored.PublishResultCommand.CanExecute(null),
                selected?.Detail ?? "none");

            var peerTransactionDirectory = TransactionDirectory(peerRoot, handoff.TransactionId);
            var localTransactionDirectory = TransactionDirectory(localRoot, handoff.TransactionId);
            File.AppendAllText(
                Path.Combine(
                    peerTransactionDirectory,
                    IntegrationTransactionLayout.ArtifactsDirectoryName,
                    "2d-run-record.json"),
                "corrupted");
            File.Delete(Path.Combine(
                localTransactionDirectory,
                IntegrationTransactionLayout.AcknowledgementFileName));
            File.Delete(Path.Combine(
                localTransactionDirectory,
                IntegrationTransactionLayout.ResultFileName));
            File.Delete(Path.Combine(
                localTransactionDirectory,
                IntegrationTransactionLayout.ArtifactsDirectoryName,
                "2d-run-record.json"));
            await restored.PullSelectedTransactionAsync();
            check(
                "invalid pulled result is not reported as transfer success",
                !string.Equals(restored.StatusText, restored.LastTcpTransferText, StringComparison.Ordinal)
                && !restored.StatusText.Contains("complete", StringComparison.OrdinalIgnoreCase)
                && !restored.StatusText.Contains("완료", StringComparison.Ordinal),
                restored.StatusText);
            check(
                "invalid pulled result records a failed operation state",
                restored.TcpOperationState == ThreeDIntegrationTcpOperationState.Failed
                && !restored.IsTcpBusy
                && restored.PullSelectedTransactionCommand.CanExecute(null),
                $"state={restored.TcpOperationState}; busy={restored.IsTcpBusy}");

            await restored.StopTcpListenerAsync();
            check(
                "explicit listener stop disposes the listener",
                !restored.IsTcpListening
                && restored.StartTcpListenerCommand.CanExecute(null)
                && !restored.StopTcpListenerCommand.CanExecute(null),
                restored.TcpListenerStatusText);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(sharedKey);
        }
    }

    private static async Task VerifyTcpOperationLifecycleAsync(
        string fixtureRoot,
        Action<string, bool, string> check)
    {
        var localRoot = Path.Combine(fixtureRoot, "tcp-operation-lifecycle-local");
        var peerRoot = Path.Combine(fixtureRoot, "tcp-operation-lifecycle-peer");
        var settingsPath = Path.Combine(fixtureRoot, "tcp-operation-lifecycle-settings.json");
        var unusedRunRecordPath = Path.Combine(fixtureRoot, "tcp-operation-lifecycle-unused-run.json");
        Directory.CreateDirectory(localRoot);
        Directory.CreateDirectory(peerRoot);
        var sharedKey = RandomNumberGenerator.GetBytes(32);
        var encodedKey = Convert.ToBase64String(sharedKey);
        var options = new TcpIntegrationOptions
        {
            MaxAttempts = 1,
            ConnectTimeout = TimeSpan.FromSeconds(1),
            IdleTimeout = TimeSpan.FromMilliseconds(250)
        };

        try
        {
            using (var cancelListener = new TcpListener(IPAddress.Loopback, 0))
            {
                cancelListener.Start();
                var cancelPort = ((IPEndPoint)cancelListener.LocalEndpoint).Port;
                await using var cancellable = CreateViewModel(settingsPath, unusedRunRecordPath, options);
                ConfigureTcpViewModel(cancellable, localRoot, cancelPort, encodedKey);

                var cancelOperation = cancellable.PingTcpPeerAsync();
                using var accepted = await cancelListener.AcceptTcpClientAsync()
                    .WaitAsync(TimeSpan.FromSeconds(3));
                await Task.Delay(50);
                check(
                    "active TCP operation exposes running state and cancel command",
                    cancellable.TcpOperationState == ThreeDIntegrationTcpOperationState.Running
                    && cancellable.IsTcpBusy
                    && cancellable.CancelTcpOperationCommand.CanExecute(null)
                    && !cancellable.PingTcpPeerCommand.CanExecute(null),
                    $"state={cancellable.TcpOperationState}; busy={cancellable.IsTcpBusy}; cancel={cancellable.CancelTcpOperationCommand.CanExecute(null)}");
                await cancellable.PingTcpPeerAsync();
                check(
                    "duplicate TCP operation is rejected without replacing active state",
                    cancellable.TcpOperationState == ThreeDIntegrationTcpOperationState.Running
                    && cancellable.IsTcpBusy
                    && cancellable.CancelTcpOperationCommand.CanExecute(null),
                    $"state={cancellable.TcpOperationState}; busy={cancellable.IsTcpBusy}");

                cancellable.CancelTcpOperationCommand.Execute(null);
                await cancelOperation.WaitAsync(TimeSpan.FromSeconds(3));
                check(
                    "operator cancel reaches a recoverable terminal state",
                    cancellable.TcpOperationState == ThreeDIntegrationTcpOperationState.Cancelled
                    && !cancellable.IsTcpBusy
                    && !cancellable.CancelTcpOperationCommand.CanExecute(null)
                    && cancellable.PingTcpPeerCommand.CanExecute(null),
                    $"state={cancellable.TcpOperationState}; busy={cancellable.IsTcpBusy}; status={cancellable.StatusText}");
            }

            using (var timeoutListener = new TcpListener(IPAddress.Loopback, 0))
            {
                timeoutListener.Start();
                var timeoutPort = ((IPEndPoint)timeoutListener.LocalEndpoint).Port;
                await using var recoverable = CreateViewModel(settingsPath, unusedRunRecordPath, options);
                ConfigureTcpViewModel(recoverable, localRoot, timeoutPort, encodedKey);

                var timeoutOperation = recoverable.PingTcpPeerAsync();
                using var accepted = await timeoutListener.AcceptTcpClientAsync()
                    .WaitAsync(TimeSpan.FromSeconds(3));
                await timeoutOperation.WaitAsync(TimeSpan.FromSeconds(3));
                check(
                    "transport idle timeout is classified without leaving a busy action",
                    recoverable.TcpOperationState == ThreeDIntegrationTcpOperationState.TimedOut
                    && !recoverable.IsTcpBusy
                    && recoverable.PingTcpPeerCommand.CanExecute(null),
                    $"state={recoverable.TcpOperationState}; busy={recoverable.IsTcpBusy}; status={recoverable.StatusText}");

                recoverable.TcpPeerPortText = "0";
                await recoverable.PingTcpPeerAsync();
                check(
                    "invalid endpoint records failure and keeps explicit retry available",
                    recoverable.TcpOperationState == ThreeDIntegrationTcpOperationState.Failed
                    && !recoverable.IsTcpBusy
                    && recoverable.PingTcpPeerCommand.CanExecute(null),
                    $"state={recoverable.TcpOperationState}; busy={recoverable.IsTcpBusy}; status={recoverable.StatusText}");

                await using var peer = new TcpIntegrationServer(
                    IntegrationApplicationIds.TwoDStudio,
                    peerRoot,
                    IPAddress.Loopback,
                    0,
                    sharedKey,
                    options);
                await peer.StartAsync();
                var peerEndpoint = peer.LocalEndpoint
                    ?? throw new InvalidOperationException("The retry verification listener has no endpoint.");
                ConfigureTcpViewModel(recoverable, localRoot, peerEndpoint.Port, encodedKey);
                await recoverable.PingTcpPeerAsync();
                check(
                    "explicit retry succeeds after timeout",
                    recoverable.TcpOperationState == ThreeDIntegrationTcpOperationState.Succeeded
                    && !recoverable.IsTcpBusy
                    && recoverable.LastTcpTransferText.Contains(
                        IntegrationApplicationIds.TwoDStudio,
                        StringComparison.Ordinal),
                    $"state={recoverable.TcpOperationState}; transfer={recoverable.LastTcpTransferText}");
            }

            await using var reopened = CreateViewModel(settingsPath, unusedRunRecordPath, options);
            reopened.SetSessionSharedKey(encodedKey);
            check(
                "saved endpoint reopen remains passive and starts idle",
                reopened.TcpOperationState == ThreeDIntegrationTcpOperationState.Idle
                && !reopened.IsTcpBusy
                && !reopened.IsTcpListening
                && reopened.Transactions.Count == 0
                && !reopened.CancelTcpOperationCommand.CanExecute(null),
                $"state={reopened.TcpOperationState}; busy={reopened.IsTcpBusy}; listening={reopened.IsTcpListening}; transactions={reopened.Transactions.Count}");

            using var disposeListener = new TcpListener(IPAddress.Loopback, 0);
            disposeListener.Start();
            var disposePort = ((IPEndPoint)disposeListener.LocalEndpoint).Port;
            await using var disposing = CreateViewModel(
                Path.Combine(fixtureRoot, "tcp-operation-dispose-settings.json"),
                unusedRunRecordPath,
                options);
            ConfigureTcpViewModel(disposing, localRoot, disposePort, encodedKey);
            var disposeOperation = disposing.PingTcpPeerAsync();
            using var disposeAccepted = await disposeListener.AcceptTcpClientAsync()
                .WaitAsync(TimeSpan.FromSeconds(3));
            var disposeTask = disposing.DisposeAsync().AsTask();
            await Task.WhenAll(disposeOperation, disposeTask).WaitAsync(TimeSpan.FromSeconds(3));
            check(
                "dispose cancels and awaits the admitted operation without stale busy state",
                disposing.IsDisposed
                && disposing.TcpOperationState == ThreeDIntegrationTcpOperationState.Cancelled
                && !disposing.IsTcpBusy
                && !disposing.IsTcpListening
                && !disposing.CancelTcpOperationCommand.CanExecute(null),
                $"disposed={disposing.IsDisposed}; state={disposing.TcpOperationState}; busy={disposing.IsTcpBusy}; listening={disposing.IsTcpListening}");
        }
        finally
        {
            CryptographicOperations.ZeroMemory(sharedKey);
        }
    }

    private static void ConfigureTcpViewModel(
        ThreeDIntegrationViewModel viewModel,
        string exchangeRoot,
        int peerPort,
        string encodedKey)
    {
        viewModel.ExchangeRoot = exchangeRoot;
        viewModel.TcpListenAddress = IPAddress.Loopback.ToString();
        viewModel.TcpListenPortText = ReserveLoopbackPort().ToString();
        viewModel.TcpPeerHost = IPAddress.Loopback.ToString();
        viewModel.TcpPeerPortText = peerPort.ToString();
        viewModel.SetSessionSharedKey(encodedKey);
        viewModel.SaveSetupCommand.Execute(null);
    }

    private static IntegrationHandoffV2 WriteValidHeightMapHandoff(string exchangeRoot)
    {
        var transactionId = Guid.NewGuid();
        var transactionDirectory = Path.Combine(
            exchangeRoot,
            IntegrationTransactionLayout.TransactionsDirectoryName,
            transactionId.ToString("D"));
        var artifactsDirectory = Path.Combine(
            transactionDirectory,
            IntegrationTransactionLayout.ArtifactsDirectoryName);
        Directory.CreateDirectory(artifactsDirectory);

        var frameId = "frame-recipe-run";
        var sourcePath = Path.Combine(artifactsDirectory, "inspection-source.c3d");
        C3DHeightFieldSnapshot.CreateForVerification(
                "source-recipe-run",
                3,
                3,
                [
                    1.00, 1.05, 1.02,
                    1.04, 1.01, 1.03,
                    1.02, 1.06, 1.01
                ],
                "mm",
                frameId)
            .SaveC3D(sourcePath);

        var recipePath = Path.Combine(artifactsDirectory, "inspection-recipe.json");
        File.WriteAllText(
            recipePath,
            $$"""
            {
              "recipeType": "c3d-warpage",
              "version": "1.0",
              "source": {
                "entityId": "source.recipe-run",
                "name": "Verification source",
                "path": "inspection-source.c3d",
                "unit": "mm"
              },
              "step": {
                "id": "step.recipe-run",
                "sourceEntityId": "source.recipe-run",
                "referenceId": "reference.recipe-run",
                "referenceMode": "BestFitInspectionRoi",
                "roi": {
                  "row": 0,
                  "column": 0,
                  "rowCount": 3,
                  "columnCount": 3
                },
                "acceptance": {
                  "maximumPeakToValley": 0.5,
                  "maximumRms": null
                },
                "unit": "mm",
                "frameId": "{{frameId}}",
                "minimumValidSamples": 3,
                "enabled": true
              }
            }
            """,
            new UTF8Encoding(false));

        var projectPath = Path.Combine(artifactsDirectory, "machine-project.ovmachine");
        File.WriteAllText(
            projectPath,
            "{\"schema\":\"machine-project/1.0\"}",
            new UTF8Encoding(false));

        var source = Artifact(
            IntegrationArtifactRoles.InspectionSource,
            "source-recipe-run",
            sourcePath,
            "artifacts/inspection-source.c3d");
        var recipe = Artifact(
            IntegrationArtifactRoles.InspectionRecipe,
            "recipe-recipe-run",
            recipePath,
            "artifacts/inspection-recipe.json");
        var project = Artifact(
            IntegrationArtifactRoles.MachineProject,
            "project-recipe-run",
            projectPath,
            "artifacts/machine-project.ovmachine");
        var handoff = new IntegrationHandoffV2(
            IntegrationContractSchema.V2,
            IntegrationMessageKind.Handoff,
            Guid.NewGuid(),
            transactionId,
            DateTimeOffset.UtcNow,
            new IntegrationApplicationIdentity(
                IntegrationApplicationIds.MachineStudio,
                "1.4.0",
                new string('4', 40),
                IntegrationSourceState.Clean),
            new IntegrationInspectionContextV2(
                "machine-project",
                "machine-project/1.0",
                "sequence-recipe-run",
                "inspect-heightmap",
                "camera-virtual",
                "acquisition-recipe-run",
                frameId,
                "mm",
                IntegrationInspectionModality.ThreeD,
                IntegrationInspectionInputKind.HeightMap,
                source.Sha256,
                recipe.Sha256,
                CreateConsumerIdentity(),
                [project, source, recipe]));
        File.WriteAllBytes(
            Path.Combine(transactionDirectory, IntegrationTransactionLayout.HandoffFileName),
            IntegrationContractJson.SerializeCanonical(handoff));
        return handoff;
    }

    private static IntegrationHandoffV2 WriteV2Handoff(string exchangeRoot)
    {
        var transactionId = Guid.NewGuid();
        var transactionDirectory = Path.Combine(exchangeRoot, "transactions", transactionId.ToString("D"));
        var artifactsDirectory = Path.Combine(transactionDirectory, "artifacts");
        Directory.CreateDirectory(artifactsDirectory);
        var projectPath = Path.Combine(artifactsDirectory, "machine-project.ovmachine");
        var sourcePath = Path.Combine(artifactsDirectory, "inspection-source.c3d");
        var recipePath = Path.Combine(artifactsDirectory, "inspection-recipe.json");
        File.WriteAllText(projectPath, "{}");
        C3DHeightFieldSnapshot.CreateForVerification(
                "source-1",
                2,
                2,
                [1.00, 1.01, 1.02, 1.03],
                "mm",
                "camera-1-frame")
            .SaveC3D(sourcePath);
        File.WriteAllText(recipePath, "{}");
        var project = Artifact(
            IntegrationArtifactRoles.MachineProject,
            "project-1",
            projectPath,
            "artifacts/machine-project.ovmachine");
        var source = Artifact(
            IntegrationArtifactRoles.InspectionSource,
            "source-1",
            sourcePath,
            "artifacts/inspection-source.c3d");
        var recipe = Artifact(
            IntegrationArtifactRoles.InspectionRecipe,
            "recipe-1",
            recipePath,
            "artifacts/inspection-recipe.json");
        var handoff = new IntegrationHandoffV2(
            IntegrationContractSchema.V2,
            IntegrationMessageKind.Handoff,
            Guid.NewGuid(),
            transactionId,
            DateTimeOffset.UtcNow,
            new IntegrationApplicationIdentity(
                IntegrationApplicationIds.MachineStudio,
                "0.1.0",
                new string('1', 40),
                IntegrationSourceState.Clean),
            new IntegrationInspectionContextV2(
                "project-1",
                "1.11",
                "sequence-1",
                "step-1",
                "camera-1",
                "acquisition-1",
                "camera-1-frame",
                "mm",
                IntegrationInspectionModality.ThreeD,
                IntegrationInspectionInputKind.HeightMap,
                source.Sha256,
                recipe.Sha256,
                CreateConsumerIdentity(),
                [project, source, recipe]));
        File.WriteAllBytes(
            Path.Combine(transactionDirectory, IntegrationTransactionLayout.HandoffFileName),
            IntegrationContractJson.SerializeCanonical(handoff));
        return handoff;
    }

    private static IntegrationHandoffV2 WriteTwoDHandoff(string exchangeRoot)
    {
        var transactionId = Guid.NewGuid();
        var transactionDirectory = TransactionDirectory(exchangeRoot, transactionId);
        var artifactsDirectory = Path.Combine(
            transactionDirectory,
            IntegrationTransactionLayout.ArtifactsDirectoryName);
        Directory.CreateDirectory(artifactsDirectory);
        var projectPath = Path.Combine(artifactsDirectory, "machine-project.ovmachine");
        var sourcePath = Path.Combine(artifactsDirectory, "inspection-source.png");
        var recipePath = Path.Combine(artifactsDirectory, "inspection-recipe.json");
        File.WriteAllText(projectPath, "{}");
        File.WriteAllBytes(sourcePath, [137, 80, 78, 71, 13, 10, 26, 10]);
        File.WriteAllText(recipePath, "{}");
        var project = Artifact(
            IntegrationArtifactRoles.MachineProject,
            "project-2d",
            projectPath,
            "artifacts/machine-project.ovmachine");
        var source = Artifact(
            IntegrationArtifactRoles.InspectionSource,
            "source-2d",
            sourcePath,
            "artifacts/inspection-source.png");
        var recipe = Artifact(
            IntegrationArtifactRoles.InspectionRecipe,
            "recipe-2d",
            recipePath,
            "artifacts/inspection-recipe.json");
        var handoff = new IntegrationHandoffV2(
            IntegrationContractSchema.V2,
            IntegrationMessageKind.Handoff,
            Guid.NewGuid(),
            transactionId,
            DateTimeOffset.UtcNow,
            new IntegrationApplicationIdentity(
                IntegrationApplicationIds.MachineStudio,
                "0.2.0-alpha.1",
                new string('1', 40),
                IntegrationSourceState.Clean),
            new IntegrationInspectionContextV2(
                "project-2d",
                "1.11",
                "sequence-2d",
                "step-2d",
                "camera-2d",
                "acquisition-2d",
                "camera-2d-frame",
                "px",
                IntegrationInspectionModality.TwoD,
                IntegrationInspectionInputKind.Image,
                source.Sha256,
                recipe.Sha256,
                CreateTwoDIdentity(),
                [project, source, recipe]));
        File.WriteAllBytes(
            Path.Combine(transactionDirectory, IntegrationTransactionLayout.HandoffFileName),
            IntegrationContractJson.SerializeCanonical(handoff));
        return handoff;
    }

    private static void WriteTwoDAcknowledgementAndResult(
        string exchangeRoot,
        IntegrationHandoffV2 handoff)
    {
        var transactionDirectory = TransactionDirectory(exchangeRoot, handoff.TransactionId);
        var acknowledgement = new IntegrationAcknowledgementV2(
            IntegrationContractSchema.V2,
            IntegrationMessageKind.Acknowledgement,
            Guid.NewGuid(),
            handoff.TransactionId,
            handoff.MessageId,
            handoff.CreatedAtUtc.AddTicks(1),
            CreateTwoDIdentity(),
            IntegrationAcknowledgementStatus.Accepted,
            null);
        var runRecordPath = Path.Combine(
            transactionDirectory,
            IntegrationTransactionLayout.ArtifactsDirectoryName,
            "2d-run-record.json");
        File.WriteAllText(runRecordPath, "{\"runId\":\"2d-run-1\"}");
        var runRecord = Artifact(
            IntegrationArtifactRoles.RunRecord,
            "2d-run-1",
            runRecordPath,
            "artifacts/2d-run-record.json");
        var result = new IntegrationResultV2(
            IntegrationContractSchema.V2,
            IntegrationMessageKind.Result,
            Guid.NewGuid(),
            handoff.TransactionId,
            handoff.MessageId,
            acknowledgement.MessageId,
            acknowledgement.CreatedAtUtc.AddTicks(1),
            CreateTwoDIdentity(),
            IntegrationResultStatus.Completed,
            IntegrationInspectionOutcome.Pass,
            "2d-run-1",
            runRecord,
            IntegrationRunCorrelation.FromContext(handoff.Context),
            [],
            [],
            null);
        var validation = IntegrationContractValidator.ValidateV2Sequence(
            handoff,
            acknowledgement,
            result);
        if (!validation.IsValid)
        {
            throw new InvalidOperationException(
                $"The 2D verification sequence is invalid: {validation.Issues[0].Message}");
        }
        File.WriteAllBytes(
            Path.Combine(
                transactionDirectory,
                IntegrationTransactionLayout.AcknowledgementFileName),
            IntegrationContractJson.SerializeCanonical(acknowledgement));
        File.WriteAllBytes(
            Path.Combine(transactionDirectory, IntegrationTransactionLayout.ResultFileName),
            IntegrationContractJson.SerializeCanonical(result));
    }

    private static IntegrationApplicationIdentity CreateTwoDIdentity() =>
        new(
            IntegrationApplicationIds.TwoDStudio,
            "0.2.0-alpha.1",
            new string('3', 40),
            IntegrationSourceState.Clean);

    private static int ReserveLoopbackPort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }

    private static IntegrationHandoff WriteLegacyHandoff(string exchangeRoot)
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
            IntegrationContractSchema.Legacy,
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
                "legacy-project-1",
                "1.11",
                "sequence-1",
                "step-1",
                "camera-1",
                "mm",
                "camera-1-frame",
                [
                    Artifact(IntegrationArtifactRoles.MachineProject, "legacy-project-1", projectPath, "artifacts/machine-project.ovmachine"),
                    Artifact(IntegrationArtifactRoles.InspectionSource, "legacy-source-1", sourcePath, "artifacts/inspection-source.c3d")
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

    private static void WriteRunRecord(
        string path,
        string sourceSha256,
        string recipeSha256) => InspectionRunRecordJson.Write(
        path,
        new InspectionRunRecord(
            "1.0",
            "run-1",
            DateTimeOffset.UtcNow,
            new InspectionRunRecipe("tool-recipe", "1.0", "recipe.json", recipeSha256),
            new InspectionRunSource("source-1", "source.c3d", sourceSha256, 4, "mm"),
            "Integration Verification",
            ResultStatus.Pass,
            "Completed",
            1.0,
            [new InspectionRunMetric("height", MetricKind.Length, 1.25, "mm", ResultStatus.Pass)],
            [],
            "matched",
            new InspectionRunArtifacts("report.txt", null, null, path, null, null))
        {
            Step = new InspectionRunStep("step-1", "source-1", [], [])
        });

    private sealed class RecordingIntegrationDialogHost(string selectedPath) : IThreeDIntegrationDialogHost
    {
        public string? InitialDirectory { get; private set; }

        public string? Title { get; private set; }

        public bool TrySelectExchangeRoot(string? initialDirectory, string title, out string path)
        {
            InitialDirectory = initialDirectory;
            Title = title;
            path = selectedPath;
            return true;
        }
    }

    private static bool RejectsCorrelationMismatch(Action operation)
    {
        try
        {
            operation();
            return false;
        }
        catch (IntegrationContractException exception)
            when (exception.ErrorCode == IntegrationErrorCode.CorrelationMismatch)
        {
            return true;
        }
    }

    private static bool RejectsRuntimeIdentity(
        Action operation,
        IntegrationErrorCode expectedError)
    {
        try
        {
            operation();
            return false;
        }
        catch (IntegrationContractException exception)
            when (exception.ErrorCode == expectedError)
        {
            return true;
        }
    }

    private static string TransactionDirectory(string root, Guid transactionId) =>
        Path.Combine(root, "transactions", transactionId.ToString("D"));

    private static bool AcknowledgementExists(string root, Guid transactionId) =>
        File.Exists(Path.Combine(TransactionDirectory(root, transactionId), IntegrationTransactionLayout.AcknowledgementFileName));

    private static bool ResultExists(string root, Guid transactionId) =>
        File.Exists(Path.Combine(TransactionDirectory(root, transactionId), IntegrationTransactionLayout.ResultFileName));

    private static bool PublishedRunRecordExists(string root, Guid transactionId) =>
        File.Exists(Path.Combine(
            TransactionDirectory(root, transactionId),
            IntegrationTransactionLayout.ArtifactsDirectoryName,
            "3d-run-record.json"));
}
