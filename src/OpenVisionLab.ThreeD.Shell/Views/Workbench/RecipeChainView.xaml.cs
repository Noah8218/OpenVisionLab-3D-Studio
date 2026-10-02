using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using OpenVisionLab.ThreeD.Presentation.Commands;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;

namespace OpenVisionLab.ThreeD.Shell.Views.Workbench;

public partial class RecipeChainView : UserControl
{
    private const double FlowCanvasMinimumWidth = 192;
    private const double FlowNodeColumnGap = 24;
    private const double FlowNodeRowGap = 42;
    private const double FlowNodeSideInset = 8;
    private const double FlowNodeWidth = 160;
    private const double FlowZoomMaximum = 1.5;
    private const double FlowZoomMinimum = 0.5;
    private const double FlowZoomStep = 0.1;

    private readonly RelayCommand decreaseFlowZoomCommand;
    private readonly RelayCommand increaseFlowZoomCommand;
    private string? flowConnectionSignature;

    public static readonly DependencyProperty FlowZoomProperty =
        DependencyProperty.Register(
            nameof(FlowZoom),
            typeof(double),
            typeof(RecipeChainView),
            new PropertyMetadata(1d));

    public static readonly DependencyProperty IsTeachingModeProperty =
        DependencyProperty.Register(
            nameof(IsTeachingMode),
            typeof(bool),
            typeof(RecipeChainView),
            new PropertyMetadata(false));

    public RecipeChainView()
    {
        decreaseFlowZoomCommand = new RelayCommand(_ => ChangeFlowZoom(-FlowZoomStep), _ => FlowZoom > FlowZoomMinimum);
        increaseFlowZoomCommand = new RelayCommand(_ => ChangeFlowZoom(FlowZoomStep), _ => FlowZoom < FlowZoomMaximum);
        ResetFlowViewCommand = new RelayCommand(_ => ResetFlowView());
        BringSelectedToolIntoViewCommand = new RelayCommand(_ => BringSelectedToolIntoView());
        InitializeComponent();
        LayoutUpdated += OnLayoutUpdated;
    }

    public ICommand BringSelectedToolIntoViewCommand { get; }

    public ICommand DecreaseFlowZoomCommand => decreaseFlowZoomCommand;

    public double FlowZoom
    {
        get => (double)GetValue(FlowZoomProperty);
        private set => SetCurrentValue(FlowZoomProperty, value);
    }

    public ICommand IncreaseFlowZoomCommand => increaseFlowZoomCommand;

    public bool IsTeachingMode
    {
        get => (bool)GetValue(IsTeachingModeProperty);
        set => SetValue(IsTeachingModeProperty, value);
    }

    public ICommand ResetFlowViewCommand { get; }

    public bool HasRecipeHealthNavigation =>
        RecipeHealthCard is not null
        && PreviousRecipeHealthIssueButton is not null
        && NextRecipeHealthIssueButton is not null;

    internal int FlowConnectionCount { get; private set; }

    private void BringSelectedToolIntoView()
    {
        if (RecipeStepList.SelectedItem is not { } selectedItem) return;

        RecipeStepList.ScrollIntoView(selectedItem);
        _ = Dispatcher.BeginInvoke(() =>
        {
            if (RecipeStepList.ItemContainerGenerator.ContainerFromItem(selectedItem) is FrameworkElement container)
            {
                container.BringIntoView();
            }
        });
    }

    private void ChangeFlowZoom(double delta)
    {
        FlowZoom = Math.Clamp(Math.Round(FlowZoom + delta, 1), FlowZoomMinimum, FlowZoomMaximum);
        flowConnectionSignature = null;
        decreaseFlowZoomCommand.RaiseCanExecuteChanged();
        increaseFlowZoomCommand.RaiseCanExecuteChanged();
    }

    private void OnLayoutUpdated(object? sender, EventArgs e) => UpdateFlowConnections();

    private bool ArrangeFlowNodes(ToolWorkbenchViewModel workbench)
    {
        var width = Math.Max(FlowCanvasMinimumWidth, FlowGraphViewport.ViewportWidth);
        var columns = width >= (2 * FlowNodeWidth) + FlowNodeColumnGap + (2 * FlowNodeSideInset) ? 2 : 1;
        var depthByOutput = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            [workbench.Source.Id] = 0,
        };
        var stepDepths = workbench.PipelineSteps.Select(step =>
        {
            var depth = 1 + step.InputEntityIds
                .Select(inputId => depthByOutput.TryGetValue(inputId, out var inputDepth) ? inputDepth : 0)
                .DefaultIfEmpty(0)
                .Max();
            if (!string.IsNullOrWhiteSpace(step.OutputEntityId)) depthByOutput[step.OutputEntityId] = depth;
            return (Step: step, Depth: depth);
        });
        var layoutChanged = false;
        var top = 0d;

        foreach (var depthGroup in stepDepths.GroupBy(item => item.Depth).OrderBy(group => group.Key))
        {
            var group = depthGroup.ToArray();
            for (var firstIndex = 0; firstIndex < group.Length; firstIndex += columns)
            {
                var row = group.Skip(firstIndex).Take(columns)
                    .Select((item, column) => (
                        Container: RecipeStepList.ItemContainerGenerator.ContainerFromItem(item.Step) as ListBoxItem,
                        X: Math.Min(columns, group.Length - firstIndex) == 1
                            ? (width - FlowNodeWidth) / 2
                            : FlowNodeSideInset + (column * (FlowNodeWidth + FlowNodeColumnGap))))
                    .ToArray();
                if (row.Any(item => item.Container is null || item.Container.DesiredSize.Height <= 0)) return false;

                var rowHeight = row.Max(item => Math.Max(item.Container!.ActualHeight, item.Container.DesiredSize.Height));
                foreach (var item in row)
                {
                    var left = Canvas.GetLeft(item.Container!);
                    var currentTop = Canvas.GetTop(item.Container!);
                    if (double.IsNaN(left) || Math.Abs(left - item.X) > 0.5)
                    {
                        Canvas.SetLeft(item.Container!, item.X);
                        layoutChanged = true;
                    }
                    if (double.IsNaN(currentTop) || Math.Abs(currentTop - top) > 0.5)
                    {
                        Canvas.SetTop(item.Container!, top);
                        layoutChanged = true;
                    }
                }

                top += rowHeight + FlowNodeRowGap;
            }
        }

        var height = Math.Max(1, top == 0 ? 1 : top - FlowNodeRowGap);
        if (Math.Abs(RecipeStepList.Width - width) > 0.5)
        {
            RecipeStepList.Width = width;
            layoutChanged = true;
        }
        if (double.IsNaN(RecipeStepList.Height) || Math.Abs(RecipeStepList.Height - height) > 0.5)
        {
            RecipeStepList.Height = height;
            layoutChanged = true;
        }

        return !layoutChanged;
    }

    private void ResetFlowView()
    {
        FlowZoom = 1d;
        flowConnectionSignature = null;
        decreaseFlowZoomCommand.RaiseCanExecuteChanged();
        increaseFlowZoomCommand.RaiseCanExecuteChanged();
        _ = Dispatcher.BeginInvoke(() =>
        {
            FlowGraphViewport.ScrollToLeftEnd();
            FlowGraphViewport.ScrollToTop();
        });
    }

    private void UpdateFlowConnections()
    {
        if (DataContext is not ToolWorkbenchViewModel workbench
            || FlowConnectionCanvas.ActualWidth <= 0
            || FlowConnectionCanvas.ActualHeight <= 0)
        {
            ClearFlowConnections();
            return;
        }

        if (!ArrangeFlowNodes(workbench)) return;

        var containers = workbench.PipelineSteps
            .Select((step, index) => (
                Step: step,
                Container: RecipeStepList.ItemContainerGenerator.ContainerFromIndex(index) as ListBoxItem))
            .Where(item => item.Container is { ActualWidth: > 0, ActualHeight: > 0 })
            .Select(item => (item.Step, Container: item.Container!))
            .ToArray();
        var producers = containers
            .Where(item => !string.IsNullOrWhiteSpace(item.Step.OutputEntityId))
            .GroupBy(item => item.Step.OutputEntityId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First().Container, StringComparer.OrdinalIgnoreCase);
        var edges = new List<(string FromId, Point Start, double ExitX, string ToId, Point End, double EntryX, bool IsSelected)>();

        foreach (var target in containers)
        {
            foreach (var inputPort in target.Step.FlowInputs)
            {
                var inputId = inputPort.EntityId;
                FrameworkElement? producer = null;
                var producerId = inputId;
                if (string.Equals(inputId, workbench.Source.Id, StringComparison.OrdinalIgnoreCase))
                {
                    producer = RecipeInputNode;
                }
                else if (producers.TryGetValue(inputId, out var upstream))
                {
                    producer = upstream;
                }

                producer ??= FindSelectionPort(FlowSelectionSources, inputId);
                if (producer is null) continue;
                edges.Add((
                    producerId,
                    GetPortCenter(producer, producer == RecipeInputNode ? "SourceOutputPort" : "StepOutputPort", true),
                    producer.TranslatePoint(new Point(producer.ActualWidth + 8, 0), FlowConnectionCanvas).X,
                    target.Step.Id,
                    GetPortCenter(target.Container, "StepInputPort", false, inputPort.Index),
                    target.Container.TranslatePoint(new Point(-8, 0), FlowConnectionCanvas).X,
                    ReferenceEquals(target.Step, workbench.SelectedPipelineStep)));
            }
        }

        var signature = string.Join(
            '|',
            $"{FlowConnectionCanvas.ActualWidth:F0}x{FlowConnectionCanvas.ActualHeight:F0}",
            workbench.Source.Id,
            workbench.SelectedPipelineStep?.Id ?? string.Empty,
            string.Join(';', containers.Select(item => $"{item.Step.Id}:{item.Container.TranslatePoint(new Point(), FlowConnectionCanvas)}")),
            string.Join(';', edges.Select(edge => $"{edge.FromId}>{edge.ToId}:{edge.Start}:{edge.ExitX}:{edge.End}:{edge.EntryX}")));
        if (string.Equals(flowConnectionSignature, signature, StringComparison.Ordinal)) return;

        flowConnectionSignature = signature;
        FlowConnectionCanvas.Children.Clear();
        FlowPortConnectionCanvas.Children.Clear();
        foreach (var edge in edges)
        {
            DrawFlowConnection(edge.Start, edge.ExitX, edge.End, edge.EntryX, edge.IsSelected);
        }

        FlowConnectionCount = edges.Count;
        AutomationProperties.SetItemStatus(FlowConnectionCanvas, FlowConnectionCount.ToString());
    }

    private Point GetPortCenter(FrameworkElement node, string portName, bool output, int inputIndex = 0)
    {
        var port = node.Name == "SelectionOutputPort" ? node
            : node == RecipeInputNode ? SourceOutputPort
            : output ? FindNamedPort(node, portName) : FindInputPort(node, inputIndex);
        if (port is { ActualWidth: > 0, ActualHeight: > 0 })
        {
            if (FindNamedPort(port, "PortVisual") is { ActualWidth: > 0, ActualHeight: > 0 } glyph)
            {
                return glyph.TranslatePoint(new Point(glyph.ActualWidth / 2, glyph.ActualHeight / 2), FlowConnectionCanvas);
            }
            return port.TranslatePoint(new Point(port.ActualWidth / 2, port.ActualHeight / 2), FlowConnectionCanvas);
        }

        var origin = node.TranslatePoint(new Point(), FlowConnectionCanvas);
        return new Point(origin.X + (output ? node.ActualWidth : 0), origin.Y + (node.ActualHeight * (output ? 0.72 : 0.48)));
    }

    private static FrameworkElement? FindSelectionPort(DependencyObject parent, string id)
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(parent); index++)
        {
            var child = VisualTreeHelper.GetChild(parent, index);
            if (child is FrameworkElement { Name: "SelectionOutputPort", DataContext: ToolRecipeSelection selection } element && string.Equals(selection.Id, id, StringComparison.OrdinalIgnoreCase)) return element;
            if (FindSelectionPort(child, id) is { } found) return found;
        }
        return null;
    }

    private static FrameworkElement? FindInputPort(DependencyObject parent, int index)
    {
        for (var childIndex = 0; childIndex < VisualTreeHelper.GetChildrenCount(parent); childIndex++)
        {
            var child = VisualTreeHelper.GetChild(parent, childIndex);
            if (child is FrameworkElement { Name: "StepInputPort", DataContext: ToolWorkbenchFlowInputItem port } element && port.Index == index) return element;
            if (FindInputPort(child, index) is { } found) return found;
        }
        return null;
    }

    private static FrameworkElement? FindNamedPort(DependencyObject parent, string name)
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(parent); index++)
        {
            var child = VisualTreeHelper.GetChild(parent, index);
            if (child is FrameworkElement element && element.Name == name) return element;
            var descendant = FindNamedPort(child, name);
            if (descendant is not null) return descendant;
        }

        return null;
    }

    private void DrawFlowConnection(Point start, double producerExitX, Point end, double targetEntryX, bool isSelected)
    {
        var exitX = Math.Min(FlowConnectionCanvas.ActualWidth - 2, Math.Max(start.X + 9, producerExitX));
        var entryX = Math.Max(2, Math.Min(end.X - 9, targetEntryX));
        var middleY = start.Y + ((end.Y - start.Y) / 2);
        var geometry = new StreamGeometry();
        using (var context = geometry.Open())
        {
            context.BeginFigure(start, false, false);
            context.LineTo(new Point(exitX, start.Y), true, false);
            context.LineTo(new Point(exitX, middleY), true, false);
            context.LineTo(new Point(entryX, middleY), true, false);
            context.LineTo(new Point(entryX, end.Y), true, false);
            context.LineTo(end, true, false);
        }
        geometry.Freeze();
        var path = new System.Windows.Shapes.Path
        {
            Data = geometry,
            StrokeThickness = isSelected ? 2.25 : 1.25,
            Opacity = isSelected ? 1 : 0.82,
        };
        path.SetResourceReference(Shape.StrokeProperty, isSelected ? "ThreeD.AccentBrush" : "ThreeD.MutedTextBrush");
        FlowConnectionCanvas.Children.Add(path);

        var ports = new GeometryGroup
        {
            Children = [new LineGeometry(start, new Point(exitX, start.Y)), new LineGeometry(new Point(entryX, end.Y), end)],
        };
        ports.Freeze();
        var portPath = new System.Windows.Shapes.Path { Data = ports, StrokeThickness = path.StrokeThickness, Opacity = path.Opacity };
        portPath.SetResourceReference(Shape.StrokeProperty, isSelected ? "ThreeD.AccentBrush" : "ThreeD.MutedTextBrush");
        FlowPortConnectionCanvas.Children.Add(portPath);

        var arrow = new Polygon
        {
            Points = [
                new Point(end.X, end.Y),
                new Point(end.X - 7, end.Y - 4),
                new Point(end.X - 7, end.Y + 4),
            ],
            Opacity = path.Opacity,
        };
        arrow.SetResourceReference(Shape.FillProperty, isSelected ? "ThreeD.AccentBrush" : "ThreeD.MutedTextBrush");
        FlowPortConnectionCanvas.Children.Add(arrow);
    }

    private void ClearFlowConnections()
    {
        if (FlowConnectionCanvas.Children.Count == 0 && FlowPortConnectionCanvas.Children.Count == 0 && FlowConnectionCount == 0) return;
        flowConnectionSignature = null;
        FlowConnectionCanvas.Children.Clear();
        FlowPortConnectionCanvas.Children.Clear();
        FlowConnectionCount = 0;
        AutomationProperties.SetItemStatus(FlowConnectionCanvas, "0");
    }
}
