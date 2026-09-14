using System.Collections.Specialized;
using System.ComponentModel;
using OpenVisionLab;
using OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;

namespace OpenVisionLab.ThreeD.Shell;

/// <summary>
/// Owns Shell event subscriptions that outlive individual commands. The
/// ViewModel keeps presentation state and workflow policy; this coordinator
/// makes collection, Workbench, ordered-run, and language lifetimes explicit.
/// </summary>
internal sealed class ShellMainWindowEventCoordinator : IDisposable
{
    private readonly INotifyCollectionChanged inspectionSteps;
    private readonly ToolWorkbenchViewModel workbench;
    private readonly Action inspectionStepsChanged;
    private readonly Action<PropertyChangedEventArgs> workbenchPropertyChanged;
    private readonly Action<ToolWorkbenchOrderedRunCompletedEventArgs> orderedRunCompleted;
    private readonly Action orderedRunInvalidated;
    private readonly Action languageChanged;
    private readonly object gate = new();
    private bool isDisposed;

    public ShellMainWindowEventCoordinator(
        INotifyCollectionChanged inspectionSteps,
        ToolWorkbenchViewModel workbench,
        Action inspectionStepsChanged,
        Action<PropertyChangedEventArgs> workbenchPropertyChanged,
        Action<ToolWorkbenchOrderedRunCompletedEventArgs> orderedRunCompleted,
        Action orderedRunInvalidated,
        Action languageChanged)
    {
        this.inspectionSteps = inspectionSteps ?? throw new ArgumentNullException(nameof(inspectionSteps));
        this.workbench = workbench ?? throw new ArgumentNullException(nameof(workbench));
        this.inspectionStepsChanged = inspectionStepsChanged ?? throw new ArgumentNullException(nameof(inspectionStepsChanged));
        this.workbenchPropertyChanged = workbenchPropertyChanged ?? throw new ArgumentNullException(nameof(workbenchPropertyChanged));
        this.orderedRunCompleted = orderedRunCompleted ?? throw new ArgumentNullException(nameof(orderedRunCompleted));
        this.orderedRunInvalidated = orderedRunInvalidated ?? throw new ArgumentNullException(nameof(orderedRunInvalidated));
        this.languageChanged = languageChanged ?? throw new ArgumentNullException(nameof(languageChanged));

        inspectionSteps.CollectionChanged += OnInspectionStepsChanged;
        workbench.PropertyChanged += OnWorkbenchPropertyChanged;
        workbench.OrderedRunCompleted += OnOrderedRunCompleted;
        workbench.OrderedRunInvalidated += OnOrderedRunInvalidated;
        OpenVisionLanguageService.LanguageChanged += OnLanguageChanged;
    }

    public void Dispose()
    {
        lock (gate)
        {
            if (isDisposed)
            {
                return;
            }

            isDisposed = true;
            inspectionSteps.CollectionChanged -= OnInspectionStepsChanged;
            workbench.PropertyChanged -= OnWorkbenchPropertyChanged;
            workbench.OrderedRunCompleted -= OnOrderedRunCompleted;
            workbench.OrderedRunInvalidated -= OnOrderedRunInvalidated;
            OpenVisionLanguageService.LanguageChanged -= OnLanguageChanged;
        }
    }

    private void OnInspectionStepsChanged(object? sender, NotifyCollectionChangedEventArgs args)
    {
        lock (gate)
        {
            if (!isDisposed)
            {
                inspectionStepsChanged();
            }
        }
    }

    private void OnWorkbenchPropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        lock (gate)
        {
            if (!isDisposed)
            {
                workbenchPropertyChanged(args);
            }
        }
    }

    private void OnOrderedRunCompleted(object? sender, ToolWorkbenchOrderedRunCompletedEventArgs args)
    {
        lock (gate)
        {
            if (!isDisposed)
            {
                orderedRunCompleted(args);
            }
        }
    }

    private void OnOrderedRunInvalidated(object? sender, EventArgs args)
    {
        lock (gate)
        {
            if (!isDisposed)
            {
                orderedRunInvalidated();
            }
        }
    }

    private void OnLanguageChanged(object? sender, EventArgs args)
    {
        lock (gate)
        {
            if (!isDisposed)
            {
                languageChanged();
            }
        }
    }
}
