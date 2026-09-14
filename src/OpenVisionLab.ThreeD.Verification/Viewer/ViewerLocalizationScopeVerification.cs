using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using OpenVisionLab.ThreeD.Viewer;
using OpenVisionLab.ThreeD.Viewer.Hosting;
using OpenVisionLab.ThreeD.Viewer.Localization;

namespace OpenVisionLab.ThreeD.Verification.Viewer;

internal static class ViewerLocalizationScopeVerification
{
    public static bool Verify(string reportPath, out string summary)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reportPath);
        var passed = false;
        var staSummary = string.Empty;
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                passed = VerifyOnSta(reportPath, out staSummary);
            }
            catch (Exception exception)
            {
                failure = exception;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (failure is not null)
        {
            throw new InvalidOperationException("Viewer localization-scope verification failed on the STA thread.", failure);
        }

        summary = staSummary;
        return passed;
    }

    private static bool VerifyOnSta(string reportPath, out string summary)
    {
        var lines = new List<string>
        {
            "OpenVisionLab 3D Viewer XAML localization-scope verification",
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

        var dispatcher = Dispatcher.CurrentDispatcher;
        var localization = new FakeLocalizationProvider();
        OpenVisionThreeDViewerControl? control = null;
        try
        {
            control = new OpenVisionThreeDViewerControl(
                loadDefaultSamples: false,
                recipeDialogHost: null,
                samplePathResolver: null,
                localizationProvider: localization);

            Check(
                "control carries the injected provider scope",
                ReferenceEquals(ViewerLocalizationScope.GetProvider(control), localization),
                $"sameProvider={ReferenceEquals(ViewerLocalizationScope.GetProvider(control), localization)}");

            var menu = control.FindName("ViewerViewMenuRoot") as MenuItem;
            Check(
                "compiled static ViewerText uses the injected provider",
                menu?.ToolTip is string tooltip && tooltip == "custom:ThreeD.Viewer.ViewCommands:rev0",
                $"tooltip={menu?.ToolTip}");

            localization.RaiseLanguageChanged();
            Drain(dispatcher);
            Check(
                "static ViewerText refreshes after provider language change",
                menu?.ToolTip is string refreshed && refreshed == "custom:ThreeD.Viewer.ViewCommands:rev1",
                $"tooltip={menu?.ToolTip}|revision={localization.Revision}");

            Check(
                "runtime converter retains the same provider",
                control.Resources["ViewerRuntimeTextConverter"] is ViewerRuntimeTextConverter converter
                    && ReferenceEquals(converter.Localization, localization),
                $"sameProvider={control.Resources["ViewerRuntimeTextConverter"] is ViewerRuntimeTextConverter runtime
                    && ReferenceEquals(runtime.Localization, localization)}");
        }
        finally
        {
            control?.Dispose();
        }

        Check(
            "control disposal detaches provider listeners and scope",
            localization.SubscriberCount == 0
                && control is not null
                && ViewerLocalizationScope.GetProvider(control) is null,
            $"subscribers={localization.SubscriberCount}|scopeCleared={control is not null && ViewerLocalizationScope.GetProvider(control) is null}");

        var fallbackExtension = new ViewerTextExtension
        {
            Key = "ThreeD.Viewer.UnscopedFallback",
            Korean = "Korean fallback",
            English = "English fallback"
        };
        var fallbackValue = fallbackExtension.ProvideValue(new EmptyServiceProvider());
        Check(
            "markup outside a Viewer scope keeps the shared fallback",
            fallbackValue is "Korean fallback" or "English fallback",
            $"value={fallbackValue}");

        summary = $"Viewer localization-scope verification: {(passed == total ? "Pass" : "Fail")} ({passed}/{total} checks)";
        lines.Add(summary);
        var fullPath = Path.GetFullPath(reportPath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllLines(fullPath, lines);
        return passed == total;
    }

    private static void Drain(Dispatcher dispatcher)
    {
        var frame = new DispatcherFrame();
        _ = dispatcher.BeginInvoke(
            DispatcherPriority.ApplicationIdle,
            new Action(() => frame.Continue = false));
        Dispatcher.PushFrame(frame);
    }

    private sealed class EmptyServiceProvider : IServiceProvider
    {
        public object? GetService(Type serviceType) => null;
    }

    private sealed class FakeLocalizationProvider : IViewerLocalizationProvider
    {
        private EventHandler? languageChanged;

        public int Revision { get; private set; }

        public int SubscriberCount { get; private set; }

        public event EventHandler? LanguageChanged
        {
            add
            {
                languageChanged += value;
                SubscriberCount++;
            }
            remove
            {
                languageChanged -= value;
                SubscriberCount--;
            }
        }

        public string Resolve(string key, string korean, string english) =>
            $"custom:{key}:rev{Revision}";

        public string LocalizeRuntimeText(object? value, string? mode = null) =>
            $"custom:{value}:{mode}:rev{Revision}";

        public void RaiseLanguageChanged()
        {
            Revision++;
            languageChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
