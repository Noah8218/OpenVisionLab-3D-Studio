using System.Collections.Concurrent;
using System.IO;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Data;
using OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;

namespace OpenVisionLab.ThreeD.Verification.Shell.Workbench;

internal static class ValidationSetWorkspaceViewModelVerification
{
    public static bool Verify(string reportPath, out string summary)
    {
        var result = Task.Run(() => VerifyAsync(reportPath)).GetAwaiter().GetResult();
        summary = result.Summary;
        return result.Passed;
    }

    private static async Task<(bool Passed, string Summary)> VerifyAsync(string reportPath)
    {
        var fullReportPath = Path.GetFullPath(reportPath);
        var fixtureRoot = Path.Combine(Path.GetDirectoryName(fullReportPath)!, "validation-workspace-fixture");
        Directory.CreateDirectory(fixtureRoot);
        var lines = new List<string> { "Validation Set workspace independent feature verification" };
        var passed = 0;
        var total = 0;

        void Check(string name, bool condition, string detail)
        {
            total++;
            if (condition) passed++;
            lines.Add($"{(condition ? "PASS" : "FAIL")} | {name} | {detail}");
        }

        try
        {
            var taughtPath = Path.Combine(fixtureRoot, "taught.C3D");
            var passPath = Path.Combine(fixtureRoot, "pass.C3D");
            var failPath = Path.Combine(fixtureRoot, "fail.C3D");
            C3DHeightFieldSnapshot.CreateForVerification(
                "source.validation-workspace", 4, 4,
                [10, 11, 12, 13, 11, 12, 13, 14, 12, 13, 14, 15, 13, 14, 15, 16]).SaveC3D(taughtPath);
            C3DHeightFieldSnapshot.CreateForVerification(
                "source.validation-workspace", 4, 4,
                [9, 10, 11, 12, 10, 11, 12, 13, 11, 12, 13, 14, 12, 13, 14, 15]).SaveC3D(passPath);
            C3DHeightFieldSnapshot.CreateForVerification(
                "source.validation-workspace", 4, 4,
                [10, 11, 12, 13, 11, 12, 13, 14, 32, 33, 34, 35, 33, 34, 35, 36]).SaveC3D(failPath);
            var document = CreateDocument(taughtPath);
            var sourceBefore = document.Source;
            var recipePath = Path.Combine(fixtureRoot, "workspace.ov3d-recipe.json");
            var english = true;
            var documentReads = 0;
            var log = new List<(string Category, string Message)>();

            ValidationSetWorkspaceViewModel CreateWorkspace() => new(
                () => { documentReads++; return document; },
                () => document.Name,
                () => recipePath,
                () => taughtPath,
                () => true,
                [],
                () => false,
                () => null,
                _ => false,
                new ToolWorkbenchStepPropertySession(),
                (category, message) => log.Add((category, message)),
                (korean, translated) => english ? translated : korean,
                status => english ? status.ToString() : status switch
                {
                    ResultStatus.Pass => "통과",
                    ResultStatus.Fail => "실패",
                    ResultStatus.Warning => "경고",
                    _ => "오류"
                });

            using var workspace = CreateWorkspace();
            var sourceRequests = 0;
            var catalogRequests = 0;
            var clearPinRequests = 0;
            var dirtyRequests = 0;
            var notificationNames = new ConcurrentQueue<string?>();
            var notificationSendersMatch = true;
            ValidationSetSampleRow? comparisonSample = null;
            workspace.PropertyChanged += (sender, args) =>
            {
                notificationSendersMatch &= ReferenceEquals(sender, workspace);
                notificationNames.Enqueue(args.PropertyName);
            };
            workspace.SelectValidationSetSourcesRequested += (_, _) => sourceRequests++;
            workspace.SamplesChanged += (_, _) => catalogRequests++;
            workspace.ComparePinsClearRequested += (_, _) => clearPinRequests++;
            workspace.DefinitionDirtyChanged += (_, _) => dirtyRequests++;
            workspace.ComparisonRequested += (_, sample) => comparisonSample = sample;
            Check("independent construction is idle and does not execute", documentReads == 1
                && workspace.ValidationSetAllCount == 0
                && !workspace.IsValidationSetRunning
                && log.Count == 0,
                $"documentReads={documentReads};samples={workspace.ValidationSetAllCount};logs={log.Count}");

            workspace.SetValidationSetSources([passPath, failPath, passPath.ToUpperInvariant()]);
            workspace.SelectedValidationSetSample = workspace.AllSamples[1];
            workspace.SetValidationSampleRoleCommand.Execute("Bad");
            Check("staging and role changes stay inside the feature without execution", workspace.ValidationSetAllCount == 2
                && workspace.ValidationSetGoodCount == 1
                && workspace.ValidationSetBadCount == 1
                && workspace.AllSamples.All(sample => sample.Status == "Pending")
                && workspace.IsValidationSetDefinitionDirty
                && !workspace.IsValidationSetRunning
                && documentReads == 1
                && sourceRequests == 0
                && dirtyRequests > 0
                && catalogRequests > 0,
                $"samples={workspace.ValidationSetAllCount};good={workspace.ValidationSetGoodCount};bad={workspace.ValidationSetBadCount};reads={documentReads};catalog={catalogRequests}");

            workspace.SelectValidationSetSourcesCommand.Execute(null);
            Check("source selection is an explicit host request", sourceRequests == 1
                && workspace.AllSamples.All(sample => sample.Status == "Pending")
                && document.Source == sourceBefore,
                $"requests={sourceRequests};sourceSame={document.Source == sourceBefore}");

            workspace.SaveValidationSetDefinition(recipePath);
            using var reopened = CreateWorkspace();
            reopened.LoadValidationSetDefinition(recipePath, document);
            Check("definition save and reopen preserve ordered roles without execution", !workspace.IsValidationSetDefinitionDirty
                && !reopened.IsValidationSetDefinitionDirty
                && reopened.AllSamples.Select(sample => (sample.SourcePath, sample.Role))
                    .SequenceEqual(workspace.AllSamples.Select(sample => (sample.SourcePath, sample.Role)))
                && reopened.AllSamples.All(sample => sample.Status == "Pending")
                && !reopened.IsValidationSetRunning,
                $"reopened={reopened.ValidationSetAllCount};dirty={reopened.IsValidationSetDefinitionDirty}");

            await workspace.RunValidationSetAsync();
            Check("explicit run projects results and selects the first issue", workspace.ValidationSetPassCount == 1
                && workspace.ValidationSetFailCount == 1
                && workspace.SelectedValidationSetSample?.Status == "Fail"
                && workspace.SelectedValidationSetStep is { Metrics.Count: > 0 }
                && workspace.HasValidationEvidence
                && !workspace.IsValidationSetRunning
                && document.Source == sourceBefore,
                $"pass={workspace.ValidationSetPassCount};fail={workspace.ValidationSetFailCount};selected={workspace.SelectedValidationSetSample?.Status};summary={workspace.ValidationSetSummary}");

            Check("failure correction captures selected evidence", workspace.BeginValidationFailureCorrectionContext()
                && workspace.ActiveValidationFailureCorrectionContext is { } correction
                && correction.SourcePath == failPath
                && correction.StepId == document.Steps[0].Id
                && !string.IsNullOrWhiteSpace(correction.Reason),
                $"context={workspace.ActiveValidationFailureCorrectionContext}");

            workspace.SetValidationSetFilterCommand.Execute("Fail");
            workspace.OpenValidationSetComparisonCommand.Execute(null);
            Check("review filtering preserves all samples and requests explicit comparison", workspace.ValidationSetSamples.Count == 1
                && workspace.AllSamples.Count == 2
                && comparisonSample?.SourcePath == failPath
                && document.Source == sourceBefore,
                $"visible={workspace.ValidationSetSamples.Count};all={workspace.AllSamples.Count};comparison={comparisonSample?.FileName}");

            var logsBeforeLocalization = log.Count;
            english = false;
            workspace.RefreshValidationSetLocalization();
            Check("localization updates review state without execution", workspace.ValidationSetSamples[0].StatusText == "실패"
                && workspace.ValidationSetSummary.Contains("완료", StringComparison.Ordinal)
                && log.Count == logsBeforeLocalization
                && !workspace.IsValidationSetRunning
                && document.Source == sourceBefore,
                $"status={workspace.ValidationSetSamples[0].StatusText};logs={log.Count};summary={workspace.ValidationSetSummary}");

            workspace.ClearValidationSetCommand.Execute(null);
            Check("clear owns review and evidence reset and requests pin cleanup", workspace.AllSamples.Count == 0
                && workspace.ValidationSetSamples.Count == 0
                && workspace.SelectedValidationSetSample is null
                && workspace.SelectedValidationSetStep is null
                && !workspace.HasValidationEvidence
                && !workspace.HasActiveValidationFailureCorrectionContext
                && clearPinRequests == 1,
                $"all={workspace.AllSamples.Count};evidence={workspace.HasValidationEvidence};pinRequests={clearPinRequests}");

            Check("feature notifications retain owning sender and count/selection names", notificationSendersMatch
                && notificationNames.Contains(nameof(ValidationSetWorkspaceViewModel.ValidationSetAllCount))
                && notificationNames.Contains(nameof(ValidationSetWorkspaceViewModel.SelectedValidationSetSample))
                && notificationNames.Contains(nameof(ValidationSetWorkspaceViewModel.IsValidationSetRunning)),
                $"ownerSender={notificationSendersMatch};notifications={notificationNames.Count}");

            workspace.Dispose();
            var notificationsBeforeDisposedRun = notificationNames.Count;
            await workspace.RunValidationSetAsync();
            workspace.Dispose();
            Check("dispose is idempotent and closes command and notification lifetime", workspace.IsDisposed
                && !workspace.RunValidationSetCommand.CanExecute(null)
                && !workspace.SelectValidationSetSourcesCommand.CanExecute(null)
                && !workspace.CancelValidationSetCommand.CanExecute(null)
                && notificationNames.Count == notificationsBeforeDisposedRun,
                $"disposed={workspace.IsDisposed};notifications={notificationNames.Count}");
        }
        catch (Exception exception)
        {
            Check("unexpected exception", false, exception.ToString());
        }

        var success = total > 0 && passed == total;
        var summary = $"ValidationSetWorkspace|pass={success}|checks={passed}/{total}|report={fullReportPath}";
        lines.Insert(1, summary);
        File.WriteAllLines(fullReportPath, lines);
        return (success, summary);
    }

    private static ToolRecipeDocument CreateDocument(string sourcePath)
    {
        var binding = ToolRecipeSelectionSourceBindingVerifier.ReadIdentity(sourcePath);
        var source = new ToolRecipeSource(
            "source.validation-workspace", "Validation workspace source", "C3D", "model",
            "frame.c3d-grid-index", sourcePath, new FileInfo(sourcePath).Length,
            binding.ContentSha256, binding.GridWidth, binding.GridHeight);
        var reference = new ToolRecipeSelection(
            "selection.reference", "Reference ROI", ToolRecipeSelectionKinds.GridRectangle,
            source.Id, source.FrameId, binding, new ToolRecipeGridRectangle(0, 0, 2, 4), null, null);
        var measurement = new ToolRecipeSelection(
            "selection.measurement", "Measurement ROI", ToolRecipeSelectionKinds.GridRectangle,
            source.Id, source.FrameId, binding, new ToolRecipeGridRectangle(2, 0, 2, 4), null, null);
        var step = new ToolRecipeStep(
            "step.thickness", "thickness", "Dual-surface Thickness", 3,
            [source.Id, reference.Id, measurement.Id], "result.thickness",
            [
                new ToolRecipeParameter("MinimumThickness", "0"),
                new ToolRecipeParameter("MaximumThickness", "10"),
                new ToolRecipeParameter("MinimumValidSampleCount", "1")
            ]);
        return new ToolRecipeDocument(ToolRecipeDocument.CurrentSchemaVersion,
            "Validation workspace fixture", source, [], [step], [reference, measurement]);
    }
}
