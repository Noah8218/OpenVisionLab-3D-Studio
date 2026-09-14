using System.Runtime.CompilerServices;
using System.Threading;
using System.Windows;
using System.Windows.Threading;
using OpenVisionLab.ThreeD.Viewer.Hosting;

namespace OpenVisionLab.ThreeD.Viewer.Localization;

/// <summary>
/// Carries one host-owned localization provider through the Viewer WPF tree.
/// The provider remains the translation owner; this scope only bridges its
/// language-change event to an inherited revision binding for static XAML text.
/// </summary>
public static class ViewerLocalizationScope
{
    public static readonly DependencyProperty ProviderProperty =
        DependencyProperty.RegisterAttached(
            "Provider",
            typeof(IViewerLocalizationProvider),
            typeof(ViewerLocalizationScope),
            new FrameworkPropertyMetadata(
                default(IViewerLocalizationProvider),
                FrameworkPropertyMetadataOptions.Inherits));

    public static readonly DependencyProperty RevisionProperty =
        DependencyProperty.RegisterAttached(
            "Revision",
            typeof(int),
            typeof(ViewerLocalizationScope),
            new FrameworkPropertyMetadata(
                0,
                FrameworkPropertyMetadataOptions.Inherits));

    private static readonly ConditionalWeakTable<DependencyObject, Subscription> Subscriptions = new();

    public static IViewerLocalizationProvider? GetProvider(DependencyObject element) =>
        (IViewerLocalizationProvider?)element.GetValue(ProviderProperty);

    public static void SetProvider(DependencyObject element, IViewerLocalizationProvider? provider) =>
        element.SetValue(ProviderProperty, provider);

    public static int GetRevision(DependencyObject element) =>
        (int)element.GetValue(RevisionProperty);

    internal static void Attach(DependencyObject scope, IViewerLocalizationProvider provider)
    {
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(provider);

        Detach(scope);
        scope.SetValue(ProviderProperty, provider);
        scope.SetValue(RevisionProperty, provider.Revision);
        Subscriptions.Add(scope, new Subscription(scope, provider));
    }

    internal static void Detach(DependencyObject scope)
    {
        ArgumentNullException.ThrowIfNull(scope);

        if (Subscriptions.TryGetValue(scope, out var subscription))
        {
            subscription.Dispose();
            Subscriptions.Remove(scope);
        }

        scope.ClearValue(ProviderProperty);
        scope.ClearValue(RevisionProperty);
    }

    private sealed class Subscription : IDisposable
    {
        private readonly DependencyObject scope;
        private readonly IViewerLocalizationProvider provider;
        private int disposed;

        public Subscription(DependencyObject scope, IViewerLocalizationProvider provider)
        {
            this.scope = scope;
            this.provider = provider;
            provider.LanguageChanged += OnLanguageChanged;
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref disposed, 1) != 0)
            {
                return;
            }

            provider.LanguageChanged -= OnLanguageChanged;
        }

        private void OnLanguageChanged(object? sender, EventArgs args)
        {
            if (Volatile.Read(ref disposed) != 0)
            {
                return;
            }

            if (scope.Dispatcher.CheckAccess())
            {
                UpdateRevision();
                return;
            }

            if (scope.Dispatcher.HasShutdownStarted || scope.Dispatcher.HasShutdownFinished)
            {
                return;
            }

            _ = scope.Dispatcher.BeginInvoke(
                DispatcherPriority.DataBind,
                new Action(UpdateRevision));
        }

        private void UpdateRevision()
        {
            if (Volatile.Read(ref disposed) == 0)
            {
                scope.SetCurrentValue(RevisionProperty, provider.Revision);
            }
        }
    }
}
