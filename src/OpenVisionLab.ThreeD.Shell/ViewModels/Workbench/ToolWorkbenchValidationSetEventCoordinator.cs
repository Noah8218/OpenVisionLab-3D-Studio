using System.ComponentModel;

namespace OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;

/// <summary>
/// Owns Validation Set owner notifications and forwards them to the Workbench
/// facade. Definition, review, and threshold state remain in their respective
/// owners; this type only makes their subscription lifetime explicit.
/// </summary>
internal sealed class ToolWorkbenchValidationSetEventCoordinator : IDisposable
{
    private readonly INotifyPropertyChanged definitionOwner;
    private readonly INotifyPropertyChanged reviewOwner;
    private readonly INotifyPropertyChanged thresholdOwner;
    private readonly Action<PropertyChangedEventArgs> definitionPropertyChanged;
    private readonly Action<PropertyChangedEventArgs> reviewPropertyChanged;
    private readonly Action<PropertyChangedEventArgs> thresholdPropertyChanged;
    private readonly object gate = new();
    private bool isDisposed;

    public ToolWorkbenchValidationSetEventCoordinator(
        INotifyPropertyChanged definitionOwner,
        INotifyPropertyChanged reviewOwner,
        INotifyPropertyChanged thresholdOwner,
        Action<PropertyChangedEventArgs> definitionPropertyChanged,
        Action<PropertyChangedEventArgs> reviewPropertyChanged,
        Action<PropertyChangedEventArgs> thresholdPropertyChanged)
    {
        this.definitionOwner = definitionOwner ?? throw new ArgumentNullException(nameof(definitionOwner));
        this.reviewOwner = reviewOwner ?? throw new ArgumentNullException(nameof(reviewOwner));
        this.thresholdOwner = thresholdOwner ?? throw new ArgumentNullException(nameof(thresholdOwner));
        this.definitionPropertyChanged = definitionPropertyChanged ?? throw new ArgumentNullException(nameof(definitionPropertyChanged));
        this.reviewPropertyChanged = reviewPropertyChanged ?? throw new ArgumentNullException(nameof(reviewPropertyChanged));
        this.thresholdPropertyChanged = thresholdPropertyChanged ?? throw new ArgumentNullException(nameof(thresholdPropertyChanged));

        definitionOwner.PropertyChanged += OnDefinitionPropertyChanged;
        reviewOwner.PropertyChanged += OnReviewPropertyChanged;
        thresholdOwner.PropertyChanged += OnThresholdPropertyChanged;
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
            definitionOwner.PropertyChanged -= OnDefinitionPropertyChanged;
            reviewOwner.PropertyChanged -= OnReviewPropertyChanged;
            thresholdOwner.PropertyChanged -= OnThresholdPropertyChanged;
        }
    }

    private void OnDefinitionPropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        lock (gate)
        {
            if (!isDisposed)
            {
                definitionPropertyChanged(args);
            }
        }
    }

    private void OnReviewPropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        lock (gate)
        {
            if (!isDisposed)
            {
                reviewPropertyChanged(args);
            }
        }
    }

    private void OnThresholdPropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        lock (gate)
        {
            if (!isDisposed)
            {
                thresholdPropertyChanged(args);
            }
        }
    }
}
