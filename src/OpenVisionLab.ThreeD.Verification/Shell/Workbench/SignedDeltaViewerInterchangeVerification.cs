using System.IO;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Data;
using OpenVisionLab.ThreeD.Shell;
using OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;

namespace OpenVisionLab.ThreeD.Verification.Shell.Workbench;

internal static class SignedDeltaViewerInterchangeVerification
{
    public static bool Verify(string reportPath, out string summary)
        => VerifyWithStageObserver(reportPath, out summary, null);

    internal static bool VerifyWithStageObserver(
        string reportPath, out string summary, Action<string>? stageCompleted)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reportPath);
        var fullReportPath = Path.GetFullPath(reportPath);
        var reportDirectory = Path.GetDirectoryName(fullReportPath)
            ?? throw new InvalidOperationException(
                "Signed-delta viewer interchange report has no directory.");
        Directory.CreateDirectory(reportDirectory);

        var lines = new List<string>
        {
            "OpenVisionLab 3D signed-delta viewer/interchange verification",
            $"Generated: {DateTimeOffset.Now:O}",
            $"Contract: {C3DSignedHeightDeltaArtifactStore.ContractVersion} | {C3DSignedHeightDeltaViewerContract.ContractName}",
            $"Missing: {C3DSignedHeightDeltaViewerContract.MissingValueEncoding}",
            $"Range: {C3DSignedHeightDeltaViewerContract.DisplayRangePolicy}",
            $"Pixels: {C3DSignedHeightDeltaViewerContract.PixelEncoding}"
        };
        var passed = 0;
        var total = 0;
        var root = Path.Combine(
            reportDirectory,
            "signed-delta-viewer-interchange",
            Guid.NewGuid().ToString("N"));

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
            Directory.CreateDirectory(root);
            var sidecarPath = Path.Combine(
                root,
                $"signed-output{C3DSignedHeightDeltaArtifactStore.FileExtension}");
            var artifact = CreateFixture();

            C3DSignedHeightDeltaArtifactStore.Save(sidecarPath, artifact);
            var json = File.ReadAllText(sidecarPath);
            var reopened = C3DSignedHeightDeltaArtifactStore.Load(sidecarPath);
            Check(
                "sidecar-roundtrip-preserves-zero-and-missing",
                reopened.TryValidate(out _)
                && reopened.ContentSha256 == artifact.ContentSha256
                && reopened.OutputContentSha256 == artifact.OutputContentSha256
                && reopened.Values[0] == 0d
                && !double.IsNaN(reopened.Values[0])
                && double.IsNaN(reopened.Values[3])
                && json.Contains("NaN", StringComparison.Ordinal),
                $"schema={reopened.SchemaVersion};artifact={reopened.ContentSha256};values={reopened.Values.Count};zero={reopened.ZeroDeltaSampleCount};missing={reopened.MissingEitherSampleCount};namedNaN={json.Contains("NaN", StringComparison.Ordinal)}");

            stageCompleted?.Invoke("signed-first-check");
            var frame = C3DHeightImageFrame.Create(reopened);
            var zeroCell = frame.TryGetCell(0, 0, out var zero)
                && zero.IsValid
                && zero.RawHeight == 0d
                && !double.IsNaN(zero.RawHeight);
            var missingCell = frame.TryGetCell(1, 1, out var missing)
                && !missing.IsValid
                && double.IsNaN(missing.RawHeight);
            Check(
                "height-image-frame-keeps-zero-valid-and-NaN-missing",
                frame.SourceEntityId == reopened.OutputEntityId
                && frame.SourceContentSha256 == reopened.ContentSha256
                && frame.Width == reopened.Width
                && frame.Height == reopened.Height
                && frame.ValidCount == reopened.PairedValidSampleCount
                && frame.MissingCount == reopened.MissingEitherSampleCount
                && zeroCell
                && missingCell
                && frame.InvalidCellMap.MissingCellCount == 1,
                $"entity={frame.SourceEntityId};identity={frame.SourceContentSha256};valid={frame.ValidCount};missing={frame.MissingCount};zeroValid={zeroCell};nanMissing={missingCell};mask={frame.InvalidCellMap.Sha256}");

            Check(
                "signed-delta-viewer-range-is-symmetric-around-zero",
                frame.DefaultDisplayMinimum == -2d
                && frame.DefaultDisplayMaximum == 2d
                && frame.DefaultDisplayFrame.Minimum == -2d
                && frame.DefaultDisplayFrame.Maximum == 2d,
                $"minimum={frame.DefaultDisplayMinimum:R};maximum={frame.DefaultDisplayMaximum:R};pixelSha={frame.PixelSha256}");

            var sourceRefreshPath = Path.Combine(root, "source-refresh.c3d");
            File.WriteAllBytes(sourceRefreshPath, [0x01]);
            using (var viewer = new HeightImageViewerViewModel(
                       ThreeDLocalization.Shared,
                       new SharedHeightCursorSession()))
            {
                var shown = viewer.ShowSignedDeltaArtifact(reopened);
                var sourceLoadCalls = 0;
                var sourceSnapshot = C3DHeightFieldSnapshot.CreateForVerification(
                    "source.viewer.refresh",
                    2,
                    2,
                    [2d, 5d, 2d, double.NaN]);
                var sourceLoad = viewer.EnsureSourceAsync(
                    sourceRefreshPath,
                    sourceSnapshot.EntityId,
                    sourceSnapshot.Unit,
                    sourceSnapshot.FrameId,
                    _ =>
                    {
                        sourceLoadCalls++;
                        return Task.FromResult(sourceSnapshot);
                    });
                sourceLoad.GetAwaiter().GetResult();
                Check(
                    "signed-delta-frame-survives-source-refresh",
                    shown
                    && sourceLoad.IsCompletedSuccessfully
                    && sourceLoadCalls == 0
                    && viewer.Frame?.SourceContentSha256 == reopened.ContentSha256
                    && viewer.Frame.DefaultDisplayMinimum == -2d
                    && viewer.Frame.DefaultDisplayMaximum == 2d,
                    $"shown={shown};loadCompleted={sourceLoad.IsCompletedSuccessfully};loaderCalls={sourceLoadCalls};sourceSha={viewer.Frame?.SourceContentSha256};range={viewer.Frame?.DefaultDisplayMinimum:R}..{viewer.Frame?.DefaultDisplayMaximum:R}");

                var selection = new InspectionWorkspaceSelectionSession();
                var workspace = new ViewerWorkspaceSession();
                var signedOutputAvailable = true;
                using var routing = new ViewerWorkspaceViewModel(
                    workspace,
                    selection,
                    ThreeDLocalization.Shared,
                    () => (false, sourceRefreshPath),
                    () => [new ToolWorkbenchRenderableC3DTarget(reopened.CurrentSourceEntityId, "Input", "HeightField", "Ready", sourceRefreshPath, "", true, true, null, null)],
                    () => (string.Empty, string.Empty, string.Empty),
                    id => signedOutputAvailable && string.Equals(id, reopened.OutputEntityId, StringComparison.OrdinalIgnoreCase) ? reopened : null,
                    viewer.ShowSignedDeltaArtifactIfChanged,
                    viewer.ClearSignedDeltaArtifact);
                selection.SynchronizeTool(reopened.StepId, reopened.CurrentSourceEntityId, InspectionWorkspaceRegionRole.None, null, reopened.OutputEntityId);
                routing.SetInspectionDataViewCommand.Execute(ViewerInspectionDataView.Output);
                Check(
                    "selected-signed-delta-output-uses-native-height-image-without-running-or-pinning",
                    routing.EffectiveViewerLayout == ViewerWorkspaceLayout.HeightImageOnly
                    && routing.EffectiveAuxiliaryViewerContentId == ViewerWorkspaceViewModel.HeightImageViewerContentId
                    && routing.HasEffectiveOutputGeometry
                    && viewer.Frame?.SourceEntityId == reopened.OutputEntityId
                    && workspace.MainContentId.Length == 0
                    && workspace.AuxiliaryContentId.Length == 0,
                    $"layout={routing.EffectiveViewerLayout};aux={routing.EffectiveAuxiliaryViewerContentId};frame={viewer.Frame?.SourceEntityId};pins={workspace.MainContentId}/{workspace.AuxiliaryContentId}");
                Check("signed-delta-pane-identifies-output-rather-than-the-input-file", routing.AuxiliaryPane.IsOutputSource
                    && !routing.AuxiliaryPane.IsInputSource && routing.AuxiliaryPane.SourceName == routing.ViewerWorkspaceCandidates.Single(candidate => candidate.Contract == "SignedDeltaHeightField").DisplayName,
                    $"name={routing.AuxiliaryPane.SourceName};input={routing.AuxiliaryPane.IsInputSource};output={routing.AuxiliaryPane.IsOutputSource}");
                routing.SetInspectionDataViewCommand.Execute(ViewerInspectionDataView.InputOutput);
                Check(
                    "signed-delta-input-output-keeps-distinct-source-and-image-identities",
                    routing.EffectiveViewerLayout == ViewerWorkspaceLayout.SplitVertical
                    && routing.EffectiveMainViewerContentId == reopened.CurrentSourceEntityId
                    && routing.EffectiveAuxiliaryViewerContentId == ViewerWorkspaceViewModel.HeightImageViewerContentId,
                    $"layout={routing.EffectiveViewerLayout};input={routing.EffectiveMainViewerContentId};output={routing.EffectiveAuxiliaryViewerContentId}");
                signedOutputAvailable = false;
                routing.RefreshInspectionDataView();
                Check(
                    "invalidated-signed-delta-clears-the-old-image",
                    !routing.HasEffectiveOutputGeometry && viewer.Frame is null,
                    $"available={routing.HasEffectiveOutputGeometry};frame={viewer.Frame?.SourceEntityId}");
                signedOutputAvailable = true;
                routing.RefreshInspectionDataView();
                var recoveredFrame = viewer.Frame?.SourceEntityId == reopened.OutputEntityId;
                routing.SetInspectionDataViewCommand.Execute(ViewerInspectionDataView.Input);
                Check(
                    "recovered-output-can-be-shown-and-input-return-clears-the-signed-image",
                    recoveredFrame && routing.IsInspectionInputView && viewer.Frame is null,
                    $"recovered={recoveredFrame};view={routing.InspectionDataView};frame={viewer.Frame?.SourceEntityId}");
                viewer.ShowSignedDeltaArtifactIfChanged(reopened);
                signedOutputAvailable = false;
                routing.RefreshInspectionDataView();
                Check(
                    "invalidated-manually-shown-image-does-not-survive-input-refresh",
                    viewer.Frame is null && routing.IsInspectionInputView,
                    $"view={routing.InspectionDataView};frame={viewer.Frame?.SourceEntityId}");
            }

            var signedOutputItem = new ToolWorkbenchArtifactItem(
                reopened.OutputEntityId,
                "Signed Delta",
                "SignedDeltaHeightField",
                "Published",
                reopened.CurrentSourceEntityId,
                $"{reopened.CurrentSourceEntityId}; {reopened.BackgroundEntityId}",
                reopened.Unit,
                reopened.FrameId,
                reopened.ContentSha256,
                "signed-delta viewer output",
                null,
                "SignedDeltaHeightField");
            var displayCommitCount = 0;
            var signedRequestReceived = false;
            var owner = new ToolWorkbenchDisplayedOutputsOwner(
                () => [signedOutputItem],
                (string? _) => (ToolWorkbenchRenderableC3DTarget?)null,
                id => string.Equals(id, reopened.OutputEntityId, StringComparison.OrdinalIgnoreCase)
                    ? reopened
                    : null,
                ThreeDLocalization.Shared,
                _ => string.Empty,
                () => true,
                _ => false,
                _ => displayCommitCount++,
                _ => { },
                () => { });
            owner.ViewerArtifactDisplayRequested += (_, request) =>
            {
                signedRequestReceived = request.SignedDelta is not null
                    && request.C3DPath.Length == 0;
                request.WasDisplayed = true;
            };
            owner.Rebuild();
            var displayed = owner.DisplayedOutputs.Single();
            owner.ShowDisplayedOutputInViewerCommand.Execute(displayed);
            Check(
                "displayed-outputs-routes-signed-delta-to-height-image-contract",
                displayed.CanShowInViewer
                && !displayed.CanPinToCompare
                && displayed.Availability == ThreeDLocalization.Shared.DisplayableHeightImageData
                && signedRequestReceived
                && displayed.IsShownInViewer
                && displayCommitCount == 1,
                $"canShow={displayed.CanShowInViewer};canPin={displayed.CanPinToCompare};availability={displayed.Availability};signedRequest={signedRequestReceived};shown={displayed.IsShownInViewer};commits={displayCommitCount}");

            var tamperedText = json.Replace(
                $"\"outputContentSha256\": \"{artifact.OutputContentSha256}\"",
                $"\"outputContentSha256\": \"{new string('F', 64)}\"",
                StringComparison.Ordinal);
            File.WriteAllText(sidecarPath, tamperedText);
            var tamperRejected = false;
            try
            {
                _ = C3DSignedHeightDeltaArtifactStore.Load(sidecarPath);
            }
            catch (InvalidDataException)
            {
                tamperRejected = true;
            }

            Check(
                "sidecar-tamper-fails-closed-on-value-hash",
                tamperRejected,
                $"tamperRejected={tamperRejected}");

            var source = C3DHeightFieldSnapshot.CreateForVerification(
                "signed-delta-c3d-guard",
                2,
                1,
                [2d, 3d]);
            var ordinaryC3DRejected = false;
            try
            {
                _ = source.CreateDerived(
                    "signed-delta-ordinary-c3d",
                    [0d, 1d],
                    "Signed-delta finite-zero guard");
            }
            catch (InvalidDataException)
            {
                ordinaryC3DRejected = true;
            }

            Check(
                "ordinary-c3d-route-does-not-accept-finite-zero",
                ordinaryC3DRejected,
                $"ordinaryC3DRejected={ordinaryC3DRejected}");
        }
        catch (Exception exception)
        {
            Check("exception", false, exception.ToString());
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }

        var succeeded = passed == total && total > 0;
        lines.Add($"Result: {(succeeded ? "Pass" : "Fail")} ({passed}/{total} checks)");
        File.WriteAllLines(fullReportPath, lines);
        summary = $"SignedDeltaViewerInterchange|{(succeeded ? "PASS" : "FAIL")}|checks={passed}/{total}|report={fullReportPath}";
        return succeeded;
    }

    private static C3DSignedHeightDeltaArtifact CreateFixture() =>
        C3DSignedHeightDeltaArtifact.Create(
            "step.signed.viewer",
            "signed.viewer.output",
            "current.viewer.entity",
            new string('A', 64),
            new string('B', 64),
            32,
            "background.viewer.entity",
            new string('C', 64),
            new string('D', 64),
            32,
            "raw-height",
            "frame.signed.viewer",
            2,
            2,
            11,
            7,
            [0d, 1d, -2d, double.NaN],
            3,
            3,
            3,
            1,
            1,
            1,
            1,
            "Signed-delta viewer/interchange fixture");
}
