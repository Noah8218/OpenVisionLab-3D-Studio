using System.IO;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using OpenVisionLab.Logging;
using OpenVisionLab.ThreeD.Viewer.Rendering;

namespace OpenVisionLab.ThreeD.Shell.Coordination;

/// <summary>Explicit, presentation-only export of this Shell and its open WPF popups.</summary>
internal sealed class ShellWindowScreenshotController : IDisposable
{
    private readonly Window owner;
    private readonly string outputDirectory;
    private readonly CommandBinding commandBinding;
    private bool saving;
    private bool disposed;

    internal ShellWindowScreenshotController(Window owner, string? outputDirectory = null)
    {
        this.owner = owner;
        this.outputDirectory = outputDirectory
            ?? Path.Combine(AppContext.BaseDirectory, "artifacts", "screenshots");
        commandBinding = new CommandBinding(
            MainWindow.SaveFullWindowPngCommand,
            OnExecuted,
            (_, args) => args.CanExecute = !disposed && !saving && owner.IsVisible);
        owner.CommandBindings.Add(commandBinding);
        // A Popup has a separate presentation source. Observe only this dispatcher's
        // explicit screenshot chord and accept it only from this Shell's visual trees.
        InputManager.Current.PreProcessInput += OnPreProcessInput;
    }

    private void OnPreProcessInput(object sender, PreProcessInputEventArgs args)
    {
        if (disposed || !owner.IsActive
            || args.StagingItem.Input is not KeyEventArgs key
            || key.RoutedEvent != Keyboard.PreviewKeyDownEvent
            || key.Key != Key.F12
            || Keyboard.Modifiers != (ModifierKeys.Control | ModifierKeys.Shift)
            || key.IsRepeat || !OwnsInput(
                key.OriginalSource as DependencyObject ?? Keyboard.FocusedElement as DependencyObject))
        {
            return;
        }

        key.Handled = true;
        if (MainWindow.SaveFullWindowPngCommand.CanExecute(null, owner))
        {
            MainWindow.SaveFullWindowPngCommand.Execute(null, owner);
        }
    }

    private bool OwnsInput(DependencyObject? source)
    {
        if (source is null) return false;
        if (Window.GetWindow(source) == owner) return true;
        return FindOpenPopups(owner).Any(popup =>
            popup.Child is Visual child
            && (ReferenceEquals(child, source)
                || source is Visual visual && child.IsAncestorOf(visual)));
    }

    private async void OnExecuted(object sender, ExecutedRoutedEventArgs args)
    {
        args.Handled = true;
        if (saving || disposed) return;
        saving = true;
        try
        {
            // Let a clicked menu finish closing; a shortcut leaves the current
            // measurement popup and keyboard focus untouched.
            var path = await owner.Dispatcher.InvokeAsync(
                () => disposed || !owner.IsVisible ? null : Save(owner, outputDirectory),
                DispatcherPriority.ContextIdle);
            if (path is null) return;
            OVLog.Write(LogCategory.UI, LogLevel.Info,
                $"Full-window PNG saved: {Path.GetFileName(path)}");
        }
        catch (Exception exception)
        {
            OVLog.Write(LogCategory.UI, LogLevel.Error,
                $"Full-window PNG could not be saved ({exception.GetType().Name}).");
        }
        finally
        {
            saving = false;
        }
    }

    internal static string Save(Window owner, string outputDirectory)
    {
        var bitmap = Capture(owner);
        var path = Path.Combine(outputDirectory,
            $"shell-window-{DateTime.UtcNow:yyyyMMdd-HHmmss-fff}-{Guid.NewGuid():N}.png");
        WpfScreenshotCapture.Save(bitmap, path);
        return path;
    }

    internal static BitmapSource Capture(Window owner)
    {
        owner.VerifyAccess();
        owner.UpdateLayout();
        var main = WpfScreenshotCapture.Capture(owner).Bitmap;
        var bounds = new Rect(0, 0, main.PixelWidth, main.PixelHeight);
        var drawing = new DrawingVisual();
        using (var context = drawing.RenderOpen())
        {
            context.DrawImage(main, bounds);
            foreach (var popup in FindOpenPopups(owner))
            {
                if (popup.Child is not FrameworkElement child || !child.IsVisible) continue;
                child.UpdateLayout();
                var origin = owner.PointFromScreen(child.PointToScreen(new Point()));
                var rectangle = new Rect(origin, new Size(child.ActualWidth, child.ActualHeight));
                // Never silently deliver a truncated measurement popup.
                if (!bounds.Contains(rectangle))
                    throw new InvalidOperationException("An open popup extends outside the main window.");
                context.DrawImage(WpfScreenshotCapture.Capture(child).Bitmap, rectangle);
            }
        }

        var result = new RenderTargetBitmap(main.PixelWidth, main.PixelHeight, 96, 96, PixelFormats.Pbgra32);
        result.Render(drawing);
        result.Freeze();
        return result;
    }

    internal static IReadOnlyList<Popup> FindOpenPopups(DependencyObject root)
    {
        var popups = new List<Popup>();
        var pending = new Stack<DependencyObject>();
        var seen = new HashSet<DependencyObject>();
        pending.Push(root);
        while (pending.TryPop(out var current))
        {
            if (!seen.Add(current)) continue;
            if (current is Popup popup)
            {
                if (popup.IsOpen) popups.Add(popup);
                // Its content is a separate presentation source, rendered above.
                continue;
            }
            if (current is Visual || current is System.Windows.Media.Media3D.Visual3D)
                for (var index = 0; index < VisualTreeHelper.GetChildrenCount(current); index++)
                    pending.Push(VisualTreeHelper.GetChild(current, index));
            foreach (var child in LogicalTreeHelper.GetChildren(current).OfType<DependencyObject>())
                pending.Push(child);
        }
        return popups;
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        InputManager.Current.PreProcessInput -= OnPreProcessInput;
        owner.CommandBindings.Remove(commandBinding);
    }
}
