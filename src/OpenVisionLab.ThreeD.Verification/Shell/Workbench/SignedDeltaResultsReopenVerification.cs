using System.Globalization;
using System.IO;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Reporting.RunRecords;
using OpenVisionLab.ThreeD.Shell;

namespace OpenVisionLab.ThreeD.Verification.Shell.Workbench;

internal static class SignedDeltaResultsReopenVerification
{
    public static bool Verify(string reportPath, out string summary)
    {
        var fullReportPath = Path.GetFullPath(reportPath);
        var reportDirectory = Path.GetDirectoryName(fullReportPath)
            ?? throw new InvalidOperationException(
                "Signed-delta Results verification report has no directory.");
        var lines = new List<string>
        {
            "OpenVisionLab 3D Studio signed-delta Results/reopen verification",
            $"Generated: {DateTimeOffset.Now:O}"
        };
        var total = 0;
        var passed = 0;
        void Check(string name, bool condition, string detail)
        {
            total++;
            lines.Add($"{(condition ? "PASS" : "FAIL")} | {name} | {detail}");
            if (condition)
            {
                passed++;
            }
        }

        try
        {
            Directory.CreateDirectory(reportDirectory);
            var root = Path.Combine(
                reportDirectory,
                "signed-delta-results-reopen",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            var recordPath = Path.Combine(root, "signed-delta-run.json");
            var currentHash = new string('A', 64);
            var currentRootHash = new string('B', 64);
            var backgroundHash = new string('C', 64);
            var backgroundRootHash = new string('D', 64);
            var values = new double[] { 0d, 1d, -2d, double.NaN };
            var artifact = C3DSignedHeightDeltaArtifact.Create(
                "step.signed.delta",
                "signed.output",
                "current.entity",
                currentHash,
                currentRootHash,
                32,
                "background.entity",
                backgroundHash,
                backgroundRootHash,
                32,
                "raw-height",
                "frame.signed",
                2,
                2,
                4,
                7,
                values,
                3,
                3,
                3,
                1,
                1,
                1,
                1,
                "Deterministic signed-delta Results fixture");
            var step = new InspectionRunStepResult(
                0,
                "step.signed.delta",
                C3DSignedHeightDeltaRecipePolicy.ToolId,
                "Signed Delta Background Subtraction",
                ["current.entity"],
                "signed.output",
                ResultStatus.Pass,
                "Signed delta evidence recorded.",
                1.25,
                [],
                [])
            {
                OutputContentSha256 = artifact.ContentSha256,
                SignedDeltaBackgroundSubtraction = artifact,
                SignedDeltaSidecar = new ToolRecipeSignedDeltaSidecarReference(
                    "artifacts/signed-output.sdelta.json",
                    artifact.StepId,
                    artifact.OutputEntityId,
                    artifact.ContentSha256,
                    artifact.OutputContentSha256,
                    artifact.CurrentSourceEntityId,
                    artifact.CurrentSourceContentSha256,
                    artifact.CurrentRootSourceSha256,
                    artifact.BackgroundEntityId,
                    artifact.BackgroundContentSha256,
                    artifact.BackgroundRootSourceSha256,
                    artifact.Width,
                    artifact.Height,
                    artifact.GridOriginColumn,
                    artifact.GridOriginRow,
                    artifact.Unit,
                    artifact.FrameId)
            };
            var record = new InspectionRunRecord(
                InspectionRunRecord.CurrentSchemaVersion,
                "run-signed-delta",
                new DateTimeOffset(2026, 9, 18, 8, 0, 0, TimeSpan.Zero),
                new InspectionRunRecipe(
                    "tool-recipe",
                    "1.10",
                    Path.Combine(root, "recipe.ov3d-recipe.json"),
                    new string('E', 64)),
                new InspectionRunSource(
                    "current.entity",
                    Path.Combine(root, "current.C3D"),
                    currentHash,
                    32,
                    "raw-height")
                {
                    FrameId = "frame.signed"
                },
                "Signed Delta Background Subtraction",
                ResultStatus.Pass,
                "Signed delta evidence recorded.",
                1.25,
                [],
                [],
                "SameOrderedGraphEngine",
                new InspectionRunArtifacts(
                    Path.Combine(root, "runner.txt"),
                    null,
                    null,
                    recordPath,
                    null,
                    null))
            {
                Steps = [step]
            };

            InspectionRunRecordJson.Write(recordPath, record);
            var persisted = InspectionRunRecordJson.Read(recordPath);
            var persistedArtifact = persisted.Steps?.Single().SignedDeltaBackgroundSubtraction;
            Check(
                "Run Record JSON accepts and restores finite-zero/missing values",
                persistedArtifact is not null
                && persistedArtifact.TryValidate(out _)
                && persistedArtifact.Values.Count == 4
                && persistedArtifact.Values[0] == 0d
                && double.IsNaN(persistedArtifact.Values[3]),
                persistedArtifact is null
                    ? "artifact missing"
                    : $"values={persistedArtifact.Values.Count};zero={persistedArtifact.ZeroDeltaSampleCount};missing={persistedArtifact.MissingEitherSampleCount}");
            Check(
                "Run Record keeps artifact and value identities",
                persistedArtifact is not null
                && persistedArtifact.ContentSha256 == artifact.ContentSha256
                && persistedArtifact.OutputContentSha256 == artifact.OutputContentSha256
                && persistedArtifact.CurrentSourceContentSha256 == currentHash
                && persistedArtifact.BackgroundContentSha256 == backgroundHash,
                persistedArtifact is null
                    ? "artifact missing"
                    : $"artifact={persistedArtifact.ContentSha256};values={persistedArtifact.OutputContentSha256}");
            var persistedSidecar = persisted.Steps?.Single().SignedDeltaSidecar;
            Check(
                "Run Record keeps the explicit sidecar reference bound to the artifact",
                persistedSidecar is not null
                && persistedSidecar.Path == "artifacts/signed-output.sdelta.json"
                && persistedSidecar.ContentSha256 == artifact.ContentSha256
                && persistedSidecar.OutputContentSha256 == artifact.OutputContentSha256
                && persistedSidecar.GridOriginColumn == artifact.GridOriginColumn
                && persistedSidecar.GridOriginRow == artifact.GridOriginRow
                && persistedSidecar.Unit == artifact.Unit
                && persistedSidecar.FrameId == artifact.FrameId,
                persistedSidecar is null
                    ? "sidecar reference missing"
                    : $"path={persistedSidecar.Path};artifact={persistedSidecar.ContentSha256};grid={persistedSidecar.GridOriginColumn},{persistedSidecar.GridOriginRow};unit={persistedSidecar.Unit};frame={persistedSidecar.FrameId}");

            using var shell = new ShellMainWindowViewModel(
                runRecordPath: recordPath,
                recentRunRecordsPath: Path.Combine(root, "recent-run-records.json"),
                orderedRunRecordRoot: root);
            var loaded = shell.LoadRunRecord(recordPath, out var loadMessage);
            var operatorSummary = shell.ResultsOperatorDecisionSummary;
            var validitySummary = shell.ResultsValueValiditySummary;
            var stepSummary = shell.InspectionSteps.SingleOrDefault()?.Evidence ?? string.Empty;
            Check(
                "Shell reopens the signed-delta Run Record",
                loaded,
                loadMessage);
            Check(
                "Results preserves signed-delta counts and evidence-only viewer state",
                operatorSummary.Contains("paired=3", StringComparison.Ordinal)
                && operatorSummary.Contains("missing=1", StringComparison.Ordinal)
                && operatorSummary.Contains("zero=1", StringComparison.Ordinal)
                && operatorSummary.Contains("Evidence-only", StringComparison.OrdinalIgnoreCase)
                && operatorSummary.Contains("C3D", StringComparison.OrdinalIgnoreCase),
                operatorSummary.Replace(Environment.NewLine, " | "));
            Check(
                "Results validity projection preserves provenance and no-recompute contract",
                validitySummary.Contains("SignedDeltaHeightField", StringComparison.Ordinal)
                && validitySummary.Contains(currentHash, StringComparison.Ordinal)
                && validitySummary.Contains(backgroundHash, StringComparison.Ordinal)
                && validitySummary.Contains(artifact.ContentSha256, StringComparison.Ordinal)
                && (validitySummary.Contains("finite zero", StringComparison.OrdinalIgnoreCase)
                    || validitySummary.Contains("finite zero는", StringComparison.Ordinal))
                && (validitySummary.Contains("not converted", StringComparison.OrdinalIgnoreCase)
                    || validitySummary.Contains("변환하지 않습니다", StringComparison.Ordinal)),
                validitySummary.Replace(Environment.NewLine, " | "));
            Check(
                "Inspection step projection exposes the persisted signed-delta distribution",
                stepSummary.Contains("signed delta", StringComparison.OrdinalIgnoreCase)
                && stepSummary.Contains("paired 3", StringComparison.Ordinal)
                && stepSummary.Contains("missing 1", StringComparison.Ordinal)
                && stepSummary.Contains("background.entity", StringComparison.Ordinal),
                stepSummary);

            File.WriteAllLines(fullReportPath, lines);
            var passedAll = passed == total;
            summary = string.Create(
                CultureInfo.InvariantCulture,
                $"SignedDeltaResultsReopen|{(passedAll ? "PASS" : "FAIL")}|checks={passed}/{total}|report={fullReportPath}");
            return passedAll;
        }
        catch (Exception exception)
        {
            lines.Add($"ERROR | exception | {exception.GetType().Name}: {exception.Message}");
            File.WriteAllLines(fullReportPath, lines);
            summary = string.Create(
                CultureInfo.InvariantCulture,
                $"SignedDeltaResultsReopen|FAIL|checks={passed}/{total}|report={fullReportPath}|error={exception.Message}");
            return false;
        }
    }
}
