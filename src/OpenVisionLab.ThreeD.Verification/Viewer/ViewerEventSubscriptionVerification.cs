using System.ComponentModel;
using System.Globalization;
using System.IO;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Data;
using OpenVisionLab.ThreeD.Viewer.Hosting;
using OpenVisionLab.ThreeD.Viewer.Models;
using OpenVisionLab.ThreeD.Viewer.Localization;
using OpenVisionLab.ThreeD.Viewer.ViewModels;

namespace OpenVisionLab.ThreeD.Verification.Viewer;

internal static class ViewerEventSubscriptionVerification
{
    public static bool Verify(string reportPath, out string summary)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reportPath);
        var fullReportPath = Path.GetFullPath(reportPath);
        var lines = new List<string>
        {
            "OpenVisionLab 3D Viewer event-subscription verification",
            $"Generated: {DateTimeOffset.Now:O}"
        };
        var passed = 0;
        var total = 0;

        void Check(string name, bool condition, string detail)
        {
            total++;
            lines.Add($"{(condition ? "PASS" : "FAIL")} | {name} | {detail}");
            if (condition)
            {
                passed++;
            }
        }

        var viewModel = new MainWindowViewModel();
        var localization = new FakeLocalizationProvider();
        var propertyChanges = 0;
        var languageChanges = 0;
        var displayHeightChanges = 0;
        var profileNotifications = 0;
        var profileRequests = 0;
        C3DHeightGrid? profileSource = null;
        var publicProfile = ViewerHostProfileState.Empty;
        var hostState = new ViewerHostStateCoordinator(() => ViewerHostStateProjection.From(viewModel), () => false, args =>
        {
            if (args.PropertyName != nameof(ViewerHostState.Profile)) return;
            profileNotifications++;
            publicProfile = args.State.Profile;
        });
        var handlers = CreateHandlers(
            propertyChanged: (_, args) =>
            {
                propertyChanges++;
                hostState.Notify(args.PropertyName);
            },
            profileRequested: (_, _) =>
            {
                profileRequests++;
                viewModel.ProfileSourceCommand.Execute(profileSource);
                viewModel.ProfileActionCommand.Execute(ViewerHeightProfileAction.Begin);
            },
            languageChanged: (_, _) => languageChanges++,
            displayHeightChanged: (_, _) => displayHeightChanges++);
        using var subscription = new ViewerEventSubscription(viewModel, localization, handlers);

        Check("subscription starts detached", !subscription.IsAttached, "attached=False");

        subscription.Attach();
        Check("Attach subscribes once", subscription.IsAttached, "attached=True");

        var binding = new ToolRecipeSelectionSourceBinding("C3D", new string('A', 64), 4, 4);
        var request = new TeachingCaptureRequest(
            "subscription.roi",
            "Subscription ROI",
            ToolRecipeSelectionKinds.GridRectangle,
            2,
            "source.c3d.height-map",
            "frame.c3d-grid-index",
            binding);
        viewModel.BeginTeachingCapture(request, out _);
        viewModel.TryAddTeachingCapturePoint(
            new ToolRecipeSelectionPoint(
                new ToolRecipeGridCellLocator("grid-cell", 1, 1),
                new ToolRecipeXyz(0, 0, 0),
                1),
            out _);
        viewModel.TryAddTeachingCapturePoint(
            new ToolRecipeSelectionPoint(
                new ToolRecipeGridCellLocator("grid-cell", 2, 2),
                new ToolRecipeXyz(1, 0, 1),
                2),
            out _);
        viewModel.SetTeachingRoiDisplayHeightStep(10.0);
        viewModel.IncreaseTeachingRoiDisplayHeightCommand.Execute(null);
        Check(
            "attached display-height event reaches one handler",
            displayHeightChanges == 1,
            $"count={displayHeightChanges}");
        subscription.Detach();
        viewModel.IncreaseTeachingRoiDisplayHeightCommand.Execute(null);
        Check(
            "detached display-height event reaches no handler",
            displayHeightChanges == 1,
            $"count={displayHeightChanges}");
        subscription.Attach();

        localization.RaiseLanguageChanged();
        Check(
            "attached language provider notification reaches one handler",
            languageChanges == 1,
            $"delta={languageChanges}");

        var beforeFirstChange = propertyChanges;
        viewModel.SelectedEntity = "First selection";
        Check(
            "one property notification reaches one attached handler",
            propertyChanges - beforeFirstChange == 1,
            $"delta={propertyChanges - beforeFirstChange}");

        subscription.Attach();
        var beforeRepeatedAttach = propertyChanges;
        viewModel.SelectedEntity = "Second selection";
        Check(
            "repeated Attach does not duplicate handlers",
            propertyChanges - beforeRepeatedAttach == 1,
            $"delta={propertyChanges - beforeRepeatedAttach}");

        subscription.Detach();
        Check("Detach removes loaded presentation and execution callbacks", !subscription.IsAttached, "attached=False");
        localization.RaiseLanguageChanged();
        Check(
            "detached language provider notification reaches no handler",
            languageChanges == 1,
            $"count={languageChanges}");
        var beforeDetachChange = propertyChanges;
        viewModel.SelectedEntity = "Detached selection";
        Check(
            "unloaded retained Host still receives one property notification",
            propertyChanges - beforeDetachChange == 1,
            $"delta={propertyChanges - beforeDetachChange}");

        var profileSourcePath = Path.Combine(Path.GetDirectoryName(fullReportPath)!, "viewer-event-subscription-profile.c3d");
        Directory.CreateDirectory(Path.GetDirectoryName(profileSourcePath)!);
        using (var writer = new BinaryWriter(File.Create(profileSourcePath)))
        {
            writer.Write(5);
            writer.Write(1);
            for (var column = 0; column < 5; column++) writer.Write(10f + column);
        }
        profileSource = C3DHeightGrid.Load(profileSourcePath, maxRenderedPoints: 0);
        viewModel.CancelTeachingCapture();
        viewModel.C3DSampleVisible = true;
        viewModel.ProfileCommand.Execute(null);
        Check("unloaded retained Host begins a source-bound profile through its command",
            profileRequests == 1 && publicProfile.IsActive && publicProfile.IsPicking && publicProfile.SourceSha256 == profileSource.ContentSha256,
            $"requests={profileRequests}|picking={publicProfile.IsPicking}|source={publicProfile.SourceSha256}");
        viewModel.HeightProfile.Pick(profileSource.ReadPoint(0, 0));
        viewModel.HeightProfile.Pick(profileSource.ReadPoint(0, 4));
        viewModel.SetProfileLinkedCursor(0, 2, 12, 2, 5, 10, 14);
        Check("unloaded Host projects the exact source cursor and cell distance",
            publicProfile.LinkedCursorVisible && publicProfile.LinkedCursorRawHeight == 12 && publicProfile.LinkedCursorDistanceCells == 2,
            $"raw={publicProfile.LinkedCursorRawHeight:R}|distance={publicProfile.LinkedCursorDistanceCells:R}|notifications={profileNotifications}");

        subscription.Detach();
        viewModel.ProfileSourceCommand.Execute(null);
        viewModel.ProfileCommand.Execute(null);
        Check("profile request reconnects the restored source after repeated unload",
            profileRequests == 2 && publicProfile.IsActive && publicProfile.IsPicking && publicProfile.SourceSha256 == profileSource.ContentSha256,
            $"requests={profileRequests}|picking={publicProfile.IsPicking}|source={publicProfile.SourceSha256}");
        viewModel.HeightProfile.Pick(profileSource.ReadPoint(0, 0));
        viewModel.HeightProfile.Pick(profileSource.ReadPoint(0, 4));
        viewModel.SetProfileLinkedCursor(0, 3, 13, 3, 5, 10, 14);
        Check("repeated unload retains the next source cell without stale state",
            publicProfile.LinkedCursorVisible && publicProfile.LinkedCursorRawHeight == 13 && publicProfile.LinkedCursorDistanceCells == 3,
            $"raw={publicProfile.LinkedCursorRawHeight:R}|distance={publicProfile.LinkedCursorDistanceCells:R}");
        subscription.Detach();
        Check("repeated Detach is idempotent", !subscription.IsAttached, "attached=False");

        subscription.Attach();
        var beforeReattach = propertyChanges;
        viewModel.SelectedEntity = "Reattached selection";
        Check(
            "reattach restores one handler",
            propertyChanges - beforeReattach == 1,
            $"delta={propertyChanges - beforeReattach}");

        viewModel.ProfileCommand.Execute(null);
        Check("reattach does not duplicate the retained profile request", profileRequests == 3, $"requests={profileRequests}");
        viewModel.HeightProfile.Pick(profileSource.ReadPoint(0, 0));
        viewModel.HeightProfile.Pick(profileSource.ReadPoint(0, 4));
        viewModel.SetProfileLinkedCursor(0, 3, 13, 3, 5, 10, 14);

        subscription.Dispose();
        Check("Dispose detaches the subscription", !subscription.IsAttached, "attached=False");
        viewModel.ProfileCommand.Execute(null);
        Check("final disposal releases retained profile requests", profileRequests == 3, $"requests={profileRequests}");
        var beforeDisposedProfileChange = profileNotifications;
        viewModel.ClearProfileLinkedCursor();
        Check("final disposal releases retained Host property notifications",
            profileNotifications == beforeDisposedProfileChange && publicProfile.LinkedCursorVisible && publicProfile.LinkedCursorRawHeight == 13,
            $"notificationDelta={profileNotifications - beforeDisposedProfileChange}");
        var beforeDisposeChange = propertyChanges;
        viewModel.SelectedEntity = "Disposed selection";
        Check(
            "disposed subscription receives no notification",
            propertyChanges == beforeDisposeChange,
            $"delta={propertyChanges - beforeDisposeChange}");

        var attachAfterDisposeRejected = false;
        try
        {
            subscription.Attach();
        }
        catch (ObjectDisposedException)
        {
            attachAfterDisposeRejected = true;
        }

        Check(
            "Attach after Dispose is rejected",
            attachAfterDisposeRejected,
            $"rejected={attachAfterDisposeRejected}");

        var repeatedDisposeSucceeded = true;
        try
        {
            subscription.Dispose();
        }
        catch
        {
            repeatedDisposeSucceeded = false;
        }

        Check(
            "Dispose is idempotent",
            repeatedDisposeSucceeded,
            $"secondDispose={repeatedDisposeSucceeded}");

        var converter = new ViewerRuntimeTextConverter { Localization = localization };
        var converted = converter.Convert(
            "status",
            typeof(string),
            "Mode",
            CultureInfo.InvariantCulture);
        Check(
            "runtime converter uses the injected provider",
            converted is "custom:status:Mode",
            $"value={converted}");

        summary = $"Viewer event-subscription verification: {(passed == total ? "Pass" : "Fail")} ({passed}/{total} checks)";
        lines.Add(summary);
        Directory.CreateDirectory(Path.GetDirectoryName(fullReportPath)!);
        File.WriteAllLines(fullReportPath, lines);
        return passed == total;
    }

    private static ViewerEventHandlerSet CreateHandlers(
        PropertyChangedEventHandler propertyChanged,
        EventHandler profileRequested,
        EventHandler languageChanged,
        EventHandler<TeachingRoiDisplayHeightChangedEventArgs> displayHeightChanged) =>
        new()
        {
            FitAllRequested = NoOp,
            FitSelectionRequested = NoOp,
            FitRoiRequested = NoOp,
            TopViewRequested = NoOp,
            PerspectiveViewRequested = NoOp,
            ResetRequested = NoOp,
            OpenRecipeRequested = NoOp,
            SaveRecipeRequested = NoOp,
            ApplyRoiAlignmentRequested = NoOp,
            FitPlaneRequested = NoOp,
            PreviewThicknessRequested = NoOp,
            PreviewWarpageRequested = NoOp,
            PreviewPlaneFlatnessRequested = NoOp,
            PreviewPointPairDimensionsRequested = NoOp,
            PreviewGapFlushRequested = NoOp,
            PreviewVolumeRequested = NoOp,
            PreviewCrossSectionRequested = NoOp,
            ScreenshotRequested = NoOp,
            ProfileViewRequested = profileRequested,
            PublishPreviewResultRequested = NoOp,
            NominalActualPreviewRequested = NoOp,
            NominalActualPublishRequested = NoOp,
            ViewModelPropertyChanged = propertyChanged,
            NominalActualPropertyChanged = NoOp,
            CameraChanged = NoOp,
            TeachingRoiDisplayHeightChanged = displayHeightChanged,
            LanguageChanged = languageChanged
        };

    private static void NoOp(object? sender, EventArgs args)
    {
    }

    private static void NoOp(object? sender, NominalActualPreviewRequestedEventArgs args)
    {
    }

    private static void NoOp(object? sender, NominalActualPublishRequestedEventArgs args)
    {
    }

    private static void NoOp(object? sender, TeachingRoiDisplayHeightChangedEventArgs args)
    {
    }

    private sealed class FakeLocalizationProvider : IViewerLocalizationProvider
    {
        public int Revision { get; private set; }

        public event EventHandler? LanguageChanged;

        public string Resolve(string key, string korean, string english) => $"custom:{key}";

        public string LocalizeRuntimeText(object? value, string? mode = null) =>
            $"custom:{value}:{mode}";

        public void RaiseLanguageChanged()
        {
            Revision++;
            LanguageChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
