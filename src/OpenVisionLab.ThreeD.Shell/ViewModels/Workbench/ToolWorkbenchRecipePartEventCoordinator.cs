using System.Collections.Specialized;
using System.ComponentModel;

namespace OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;

/// <summary>
/// Owns the retained recipe source, step, and parameter notification
/// subscriptions. Recipe dirty-state and execution invalidation remain in the
/// Workbench callback.
/// </summary>
internal sealed class ToolWorkbenchRecipePartEventCoordinator : IDisposable
{
    private readonly ToolWorkbenchSourceItem source;
    private readonly PropertyChangedEventHandler propertyChanged;
    private readonly HashSet<ToolWorkbenchPipelineStepItem> steps = [];
    private readonly HashSet<ToolWorkbenchParameterItem> parameters = [];
    private readonly object gate = new();
    private bool isDisposed;

    public ToolWorkbenchRecipePartEventCoordinator(
        ToolWorkbenchSourceItem source,
        PropertyChangedEventHandler propertyChanged)
    {
        this.source = source ?? throw new ArgumentNullException(nameof(source));
        this.propertyChanged = propertyChanged
            ?? throw new ArgumentNullException(nameof(propertyChanged));

        source.PropertyChanged += OnPropertyChanged;
    }

    public void SubscribeStep(ToolWorkbenchPipelineStepItem step)
    {
        ArgumentNullException.ThrowIfNull(step);
        lock (gate)
        {
            if (isDisposed || !steps.Add(step))
            {
                return;
            }

            step.PropertyChanged += OnPropertyChanged;
            step.Parameters.CollectionChanged += OnStepParametersChanged;
            foreach (var parameter in step.Parameters)
            {
                SubscribeParameterCore(parameter);
            }
        }
    }

    public void UnsubscribeStep(ToolWorkbenchPipelineStepItem step)
    {
        ArgumentNullException.ThrowIfNull(step);
        lock (gate)
        {
            if (!steps.Remove(step))
            {
                return;
            }

            step.Parameters.CollectionChanged -= OnStepParametersChanged;
            step.PropertyChanged -= OnPropertyChanged;
            foreach (var parameter in step.Parameters)
            {
                UnsubscribeParameterCore(parameter);
            }
        }
    }

    public void SubscribeParameter(ToolWorkbenchParameterItem parameter)
    {
        ArgumentNullException.ThrowIfNull(parameter);
        lock (gate)
        {
            if (!isDisposed)
            {
                SubscribeParameterCore(parameter);
            }
        }
    }

    public void UnsubscribeParameter(ToolWorkbenchParameterItem parameter)
    {
        ArgumentNullException.ThrowIfNull(parameter);
        lock (gate)
        {
            UnsubscribeParameterCore(parameter);
        }
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
            source.PropertyChanged -= OnPropertyChanged;
            foreach (var step in steps)
            {
                step.Parameters.CollectionChanged -= OnStepParametersChanged;
                step.PropertyChanged -= OnPropertyChanged;
            }

            foreach (var parameter in parameters)
            {
                parameter.PropertyChanged -= OnPropertyChanged;
            }

            steps.Clear();
            parameters.Clear();
        }
    }

    private void SubscribeParameterCore(ToolWorkbenchParameterItem parameter)
    {
        if (parameters.Add(parameter))
        {
            parameter.PropertyChanged += OnPropertyChanged;
        }
    }

    private void UnsubscribeParameterCore(ToolWorkbenchParameterItem parameter)
    {
        if (parameters.Remove(parameter))
        {
            parameter.PropertyChanged -= OnPropertyChanged;
        }
    }

    private void OnPropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        lock (gate)
        {
            if (!isDisposed)
            {
                propertyChanged(sender, args);
            }
        }
    }

    private void OnStepParametersChanged(object? sender, NotifyCollectionChangedEventArgs args)
    {
        lock (gate)
        {
            if (isDisposed)
            {
                return;
            }

            if (args.OldItems is not null)
            {
                foreach (var item in args.OldItems.OfType<ToolWorkbenchParameterItem>())
                {
                    UnsubscribeParameterCore(item);
                }
            }

            if (args.NewItems is not null)
            {
                foreach (var item in args.NewItems.OfType<ToolWorkbenchParameterItem>())
                {
                    SubscribeParameterCore(item);
                }
            }
        }
    }
}
