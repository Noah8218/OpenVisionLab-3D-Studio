using System.ComponentModel;
using System.IO;
using OpenVisionLab.ThreeD.Shell;
using OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;

namespace OpenVisionLab.ThreeD.Verification.Shell.Workbench;

internal static class ViewerWorkspaceViewModelVerification
{
    public static bool Verify(string reportPath, out string summary)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reportPath);
        var fullReportPath = Path.GetFullPath(reportPath);
        var reportDirectory = Path.GetDirectoryName(fullReportPath)!;
        Directory.CreateDirectory(reportDirectory);
        var lines = new List<string> { "OpenVisionLab 3D Viewer workspace presentation verification" };
        var passed = 0;
        var total = 0;

        void Check(string name, bool condition)
        {
            total++;
            if (condition)
            {
                passed++;
            }
            lines.Add($"{(condition ? "PASS" : "FAIL")} | {name}");
        }

        try
        {
            var session = new ViewerWorkspaceSession();
            var selection = new InspectionWorkspaceSelectionSession();
            selection.SynchronizeTool("step.viewer", "source.viewer", InspectionWorkspaceRegionRole.Selection, "roi.viewer", "output.original");
            var initialSelection = selection.Current;
            var localization = ThreeDLocalization.Shared;
            var sourceReady = false;
            var sourcePath = Path.Combine(reportDirectory, "missing-native-grid.c3d");
            IReadOnlyList<ToolWorkbenchRenderableC3DTarget> targets = [];
            (string A, string B, string C) compareSlots = ("output.a", "output.b", "output.c");
            using var owner = new ViewerWorkspaceViewModel(
                session,
                selection,
                localization,
                () => (sourceReady, sourcePath),
                () => targets,
                () => compareSlots);
            var notifications = new List<string?>();
            owner.PropertyChanged += (_, args) => notifications.Add(args.PropertyName);
            var commandNotifications = 0;
            owner.SplitViewerVerticallyCommand.CanExecuteChanged += (_, _) => commandNotifications++;

            Check("construction leaves source, selection, and layout unchanged",
                selection.Current == initialSelection && session.Layout == ViewerWorkspaceLayout.Single
                && session.MainContentId.Length == 0 && session.AuxiliaryContentId.Length == 0);
            Check("empty inputs disable auxiliary, native image, and camera commands",
                !owner.SplitViewerVerticallyCommand.CanExecute(null)
                && !owner.SplitViewerHorizontallyCommand.CanExecute(null)
                && !owner.PopOutViewerCommand.CanExecute(null)
                && !owner.OpenHeightImageCommand.CanExecute(null)
                && !owner.ToggleViewerCameraLinkCommand.CanExecute(null));

            ToolWorkbenchRenderableC3DTarget Target(string id, bool isSource = false, bool isDisplayable = true) =>
                new(id, id, "HeightField / raw-height", "Ready", sourcePath, id, isSource, isDisplayable, null, null);

            var allTargets = new[]
            {
                Target("output.a"), Target("source.viewer", isSource: true),
                Target("output.b"), Target("output.c"), Target("output.hidden", isDisplayable: false)
            };
            targets = allTargets;
            owner.ReconcileViewerWorkspaceContents();
            Check("projection includes only displayable catalog targets without mutating them",
                owner.ViewerWorkspaceCandidates.Select(item => item.Id)
                    .SequenceEqual(new[] { "output.a", "source.viewer", "output.b", "output.c" })
                && targets.Count == 5 && !targets[4].IsDisplayable);
            Check("reconciliation prefers source in main and compare slot B in auxiliary",
                owner.MainViewerContentId == "source.viewer" && owner.AuxiliaryViewerContentId == "output.b"
                && selection.Current == initialSelection && commandNotifications > 0);
            Check("candidate lookup preserves case-insensitive identity",
                owner.GetMainViewerCandidate("OUTPUT.A")?.Id == "output.a"
                && owner.GetViewerWorkspaceCandidate("OUTPUT.B")?.Id == "output.b");

            sourceReady = true;
            Check("ready source with missing file does not expose native image", !owner.OpenHeightImageCommand.CanExecute(null));
            sourcePath = Path.Combine(reportDirectory, "native-grid-presence-only.fixture");
            File.WriteAllText(sourcePath, "Candidate availability fixture; not decoded or executed.");
            Check("native image is first auxiliary candidate and excluded from main candidates",
                owner.ViewerWorkspaceCandidates[0].Kind == ViewerWorkspaceCandidateKind.HeightImage
                && owner.ViewerWorkspaceCandidates[0].SourcePath == sourcePath
                && owner.MainViewerCandidates.All(item => item.Kind == ViewerWorkspaceCandidateKind.ThreeDArtifact)
                && owner.OpenHeightImageCommand.CanExecute(null));
            session.ResetContentPins();
            owner.SplitViewerVerticallyCommand.Execute(null);
            Check("opening split from single prefers native image without changing selected output",
                owner.IsSplitVerticalViewerLayout && owner.AuxiliaryViewerContentId == ViewerWorkspaceViewModel.HeightImageViewerContentId
                && selection.SelectedOutputEntityId == initialSelection.SelectedOutputEntityId
                && !owner.CanLinkViewerCameras && !owner.ToggleViewerCameraLinkCommand.CanExecute(null));

            owner.AuxiliaryViewerContentId = "OUTPUT.B";
            Check("pinning auxiliary output selects its stable identity and focuses auxiliary",
                owner.AuxiliaryViewerContentId == "output.b" && selection.SelectedOutputEntityId == "output.b"
                && session.FocusedSlotId == ViewerWorkspaceSession.AuxiliarySlotId
                && selection.FocusedViewerSlotId == ViewerWorkspaceSession.AuxiliarySlotId && owner.CanLinkViewerCameras);
            owner.MainViewerContentId = "output.a";
            owner.MainViewerContentId = ViewerWorkspaceViewModel.HeightImageViewerContentId;
            owner.AuxiliaryViewerContentId = "output.missing";
            Check("main accepts only 3D candidates and invalid pins leave selection unchanged",
                owner.MainViewerContentId == "output.a" && owner.AuxiliaryViewerContentId == "output.b"
                && selection.SelectedOutputEntityId == "output.b");
            owner.ToggleViewerCameraLinkCommand.Execute(null);
            Check("camera command links two 3D viewers and updates presentation",
                owner.IsViewerCameraLinked && owner.ViewerCameraLinkLabel == localization.ViewerCameraUnlink
                && owner.ViewerCameraLinkSummary == localization.ViewerCameraLinked);
            owner.ToggleViewerCameraLinkCommand.Execute(null);
            Check("camera command unlinks reversibly", !owner.IsViewerCameraLinked && owner.CanLinkViewerCameras);

            owner.FocusViewerWorkspaceSlotCommand.Execute(ViewerWorkspaceSession.MainSlotId);
            owner.FocusViewerWorkspaceSlotCommand.Execute("viewer.unknown");
            Check("focus command validates slot and preserves output selection",
                session.IsMainFocused && selection.FocusedViewerSlotId == ViewerWorkspaceSession.MainSlotId
                && selection.SelectedOutputEntityId == "output.b"
                && !owner.FocusViewerWorkspaceSlotCommand.CanExecute("viewer.unknown"));
            owner.SplitViewerHorizontallyCommand.Execute(null);
            Check("horizontal layout preserves pinned content", owner.IsSplitHorizontalViewerLayout && owner.AuxiliaryViewerContentId == "output.b");
            owner.PopOutViewerCommand.Execute(null);
            Check("popout layout preserves pinned content", owner.IsPopOutViewerLayout && owner.AuxiliaryViewerContentId == "output.b");
            owner.ToggleViewerCameraLinkCommand.Execute(null);
            owner.SetSingleViewerLayoutCommand.Execute(null);
            Check("single layout disables camera link and restores main focus",
                owner.IsSingleViewerLayout && !owner.IsViewerCameraLinked && session.IsMainFocused
                && selection.FocusedViewerSlotId == ViewerWorkspaceSession.MainSlotId
                && !owner.FocusViewerWorkspaceSlotCommand.CanExecute(ViewerWorkspaceSession.AuxiliarySlotId));

            owner.ClearMainViewerPinCommand.Execute(null);
            owner.ClearAuxiliaryViewerPinCommand.Execute(null);
            owner.ReconcileViewerWorkspaceContents();
            Check("explicit clears survive catalog reconciliation",
                owner.MainViewerContentId.Length == 0 && owner.AuxiliaryViewerContentId.Length == 0
                && session.IsMainContentExplicitlyCleared && session.IsAuxiliaryContentExplicitlyCleared
                && !owner.ClearMainViewerPinCommand.CanExecute(null) && !owner.ClearAuxiliaryViewerPinCommand.CanExecute(null));
            owner.OpenHeightImageCommand.Execute(null);
            Check("explicit native image command reopens auxiliary without repinning main",
                owner.IsSplitVerticalViewerLayout && owner.AuxiliaryViewerContentId == ViewerWorkspaceViewModel.HeightImageViewerContentId
                && owner.MainViewerContentId.Length == 0 && session.IsAuxiliaryFocused);
            owner.MainViewerContentId = "output.a";
            owner.AuxiliaryViewerContentId = "output.b";
            targets = allTargets.Where(item => item.Id is not "output.a" and not "output.b").ToArray();
            owner.ReconcileViewerWorkspaceContents();
            Check("removed candidates retain stale pins and show unavailability",
                owner.MainViewerContentId == "output.a" && owner.AuxiliaryViewerContentId == "output.b"
                && owner.MainViewerSummary == $"{localization.ViewerPinnedUnavailable} | output.a"
                && owner.AuxiliaryViewerSummary == $"{localization.ViewerPinnedUnavailable} | output.b");
            targets = allTargets;
            owner.ReconcileViewerWorkspaceContents();
            Check("restored candidates resolve existing pins without substitution",
                owner.MainViewerSummary == "output.a | HeightField / raw-height"
                && owner.AuxiliaryViewerSummary == "output.b | HeightField / raw-height");

            sourceReady = false;
            compareSlots = ("output.a", "missing", "output.c");
            session.ResetContentPins();
            owner.ReconcileViewerWorkspaceContents();
            Check("missing compare B falls back to compare A", owner.AuxiliaryViewerContentId == "output.a");
            compareSlots = ("missing", "missing", "output.c");
            session.ResetContentPins();
            owner.ReconcileViewerWorkspaceContents();
            Check("missing compare A and B fall back to compare C", owner.AuxiliaryViewerContentId == "output.c");
            compareSlots = ("missing", "missing", "missing");
            session.ResetContentPins();
            owner.ReconcileViewerWorkspaceContents();
            Check("missing compare slots fall back to first available artifact", owner.AuxiliaryViewerContentId == "output.a");

            notifications.Clear();
            var beforeLocalizationSelection = selection.Current;
            owner.RefreshLocalization(new PropertyChangedEventArgs(nameof(ThreeDLocalization.HeightImage)));
            Check("localization refresh notifies all translated presentation properties without selection changes",
                notifications.SequenceEqual(new[]
                {
                    nameof(owner.ViewerWorkspaceLayoutSummary), nameof(owner.MainViewerSummary), nameof(owner.AuxiliaryViewerSummary),
                    nameof(owner.MainViewerCandidates), nameof(owner.ViewerWorkspaceCandidates),
                    nameof(owner.ViewerCameraLinkLabel), nameof(owner.ViewerCameraLinkSummary)
                }) && selection.Current == beforeLocalizationSelection);
            notifications.Clear();
            owner.RefreshLocalization(new PropertyChangedEventArgs("UnrelatedCaption"));
            Check("unrelated localization properties do not refresh viewer bindings", notifications.Count == 0);
            owner.SynchronizeViewerWorkspaceFocus(ViewerWorkspaceSession.MainSlotId);
            Check("incoming focus synchronization changes only viewer focus", session.IsMainFocused && selection.Current == beforeLocalizationSelection);
            Check("all presentation commands preserve authored tool, input, and region identities",
                selection.SelectedStepId == initialSelection.SelectedStepId
                && selection.SelectedInputEntityId == initialSelection.SelectedInputEntityId
                && selection.SelectedRegionId == initialSelection.SelectedRegionId
                && selection.ActiveRegionRole == initialSelection.ActiveRegionRole);
            notifications.Clear();
            owner.Dispose();
            owner.Dispose();
            session.PinMainContent("output.after-dispose");
            session.SetLayout(ViewerWorkspaceLayout.PopOut);
            Check("owner disposal detaches session presentation notifications", notifications.Count == 0);
        }
        catch (Exception exception)
        {
            total++;
            lines.Add($"FAIL | unexpected exception | {exception}");
        }

        var success = total > 0 && passed == total;
        summary = $"ViewerWorkspaceViewModel|pass={success}|checks={passed}/{total}|report={fullReportPath}";
        lines.Insert(1, summary);
        File.WriteAllLines(fullReportPath, lines);
        return success;
    }
}
