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
    private const double FlowNodeRowGap = 26;
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

    public static readonly DependencyProperty FlowCardWidthProperty = DependencyProperty.Register(
        nameof(FlowCardWidth), typeof(double), typeof(RecipeChainView), new PropertyMetadata(FlowNodeWidth));

    public static readonly DependencyProperty IsThicknessGroupExpandedProperty = DependencyProperty.Register(
        nameof(IsThicknessGroupExpanded), typeof(bool), typeof(RecipeChainView), new PropertyMetadata(false));

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
        Loaded += OnLoaded;
    }

    public ICommand BringSelectedToolIntoViewCommand { get; }

    public ICommand DecreaseFlowZoomCommand => decreaseFlowZoomCommand;

    public double FlowZoom
    {
        get => (double)GetValue(FlowZoomProperty);
        private set => SetCurrentValue(FlowZoomProperty, value);
    }

    public ICommand IncreaseFlowZoomCommand => increaseFlowZoomCommand;

    public double FlowCardWidth
    {
        get => (double)GetValue(FlowCardWidthProperty);
        private set => SetCurrentValue(FlowCardWidthProperty, value);
    }

    public bool IsTeachingMode
    {
        get => (bool)GetValue(IsTeachingModeProperty);
        set => SetValue(IsTeachingModeProperty, value);
    }

    public bool IsThicknessGroupExpanded
    {
        get => (bool)GetValue(IsThicknessGroupExpandedProperty);
        set => SetValue(IsThicknessGroupExpandedProperty, value);
    }

    public ICommand ResetFlowViewCommand { get; }

    public bool HasRecipeHealthNavigation =>
        RecipeHealthCard is not null
        && PreviousRecipeHealthIssueButton is not null
        && NextRecipeHealthIssueButton is not null;

    internal int FlowConnectionCount { get; private set; }

    private void OnStepRequestBringIntoView(object sender, RequestBringIntoViewEventArgs args)
    {
        if (!IsTeachingMode || RecipeStepList.SelectedIndex != 0 || RecipeStepList.IsKeyboardFocusWithin
            || !ReferenceEquals(args.TargetObject, RecipeStepList.ItemContainerGenerator.ContainerFromIndex(0))) return;
        // Initial/restored selection of the first tool includes its source card in the viewport.
        args.Handled = true;
        FlowGraphViewport.ScrollToTop();
    }

    private void BringSelectedToolIntoView()
    {
        if (RecipeStepList.SelectedItem is not { } selectedItem) return;

        if (IsTeachingMode && selectedItem is ToolWorkbenchPipelineStepItem { IsFlowRepeatGroupMember: true })
            IsThicknessGroupExpanded = true;
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

    private void OnToolMenuClick(object sender, RoutedEventArgs args)
    {
        if (sender is Button { ContextMenu: { } menu } button)
        {
            // ButtonBase executes its selection command after the routed Click event.
            // Open only after selection and scrolling settle so the popup keeps its owner.
            _ = Dispatcher.BeginInvoke(() =>
            {
                if (!IsLoaded || !button.IsLoaded || !ReferenceEquals(button.ContextMenu, menu)) return;
                // Selection can expand the card and replace its header menu with the footer button.
                var target = button.IsVisible ? button
                    : RecipeStepList.ItemContainerGenerator.ContainerFromItem(button.DataContext) is FrameworkElement container
                        ? FindNamedPort(container, "FlowExpandedMenuButton") as Button : null;
                if (target is not { IsLoaded: true, IsVisible: true }) return;
                menu.PlacementTarget = target;
                menu.IsOpen = true;
            }, System.Windows.Threading.DispatcherPriority.ContextIdle);
        }
    }

    private void OnFlowViewOptionsKeyDown(object sender, KeyEventArgs args)
    {
        if (args.Key != Key.Escape) return;
        FlowViewOptionsToggle.SetCurrentValue(System.Windows.Controls.Primitives.ToggleButton.IsCheckedProperty, false);
        FlowViewOptionsToggle.Focus();
        args.Handled = true;
    }

    private void OnExpandAllChanged(object sender, RoutedEventArgs args) => IsThicknessGroupExpanded = FlowExpandAllToggle.IsChecked == true;

    private void OnLoaded(object sender, RoutedEventArgs args)
    {
        if (!IsTeachingMode) return;
        // A newly loaded flow starts with its input card visible. Later selection still uses the existing scroll behavior.
        _ = Dispatcher.BeginInvoke(() => FlowGraphViewport.ScrollToTop(), System.Windows.Threading.DispatcherPriority.ContextIdle);
    }

    private void OnLayoutUpdated(object? sender, EventArgs e) => UpdateFlowConnections();

    private bool ArrangeFlowNodes(ToolWorkbenchViewModel workbench)
    {
        var width = Math.Max(FlowCanvasMinimumWidth, FlowGraphViewport.ViewportWidth);
        var nodeWidth = IsTeachingMode ? Math.Clamp(width - 36, 192, 470) : FlowNodeWidth;
        if (Math.Abs(FlowCardWidth - nodeWidth) > 0.5) FlowCardWidth = nodeWidth;
        var columns = !IsTeachingMode && width >= (2 * nodeWidth) + FlowNodeColumnGap + (2 * FlowNodeSideInset) ? 2 : 1;
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
            var group = depthGroup.Where(item => !IsTeachingMode || !item.Step.IsFlowRepeatGroupMember || item.Step.IsFlowRepeatGroupLead || IsThicknessGroupExpanded).ToArray();
            for (var firstIndex = 0; firstIndex < group.Length; firstIndex += columns)
            {
                var row = group.Skip(firstIndex).Take(columns)
                    .Select((item, column) => (
                        Container: RecipeStepList.ItemContainerGenerator.ContainerFromItem(item.Step) as ListBoxItem,
                        X: Math.Min(columns, group.Length - firstIndex) == 1
                            ? IsTeachingMode ? 22 : (width - nodeWidth) / 2
                            : FlowNodeSideInset + (column * (nodeWidth + FlowNodeColumnGap))))
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

                var lastStep = group[Math.Min(firstIndex + columns, group.Length) - 1].Step;
                var rowGap = IsTeachingMode && IsThicknessGroupExpanded && lastStep.IsFlowRepeatGroupMember && !lastStep.IsFlowRepeatGroupLast ? 0 : FlowNodeRowGap;
                top += rowHeight + rowGap;
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
                Container: RecipeStepList.ItemContainerGenerator.ContainerFromItem(IsTeachingMode && step.FlowRepeatGroupAnchor is { } anchor ? anchor : step) as ListBoxItem))
            .Where(item => item.Container is { ActualWidth: > 0, ActualHeight: > 0 })
            .Select(item => (item.Step, Container: item.Container!))
            .ToArray();
        var producers = containers
            .Where(item => !string.IsNullOrWhiteSpace(item.Step.OutputEntityId))
            .GroupBy(item => item.Step.OutputEntityId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First().Container, StringComparer.OrdinalIgnoreCase);
        var edges = new List<(string FromId, Point Start, double ExitX, string ToId, Point End, double EntryX, bool IsSelected, string Contract, bool HasIssue)>();

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

                if (producer is null) continue;
                edges.Add((
                    producerId,
                    GetPortCenter(producer, producer == RecipeInputNode ? "SourceOutputPort" : "StepOutputPort", true),
                    producer.TranslatePoint(new Point(producer.ActualWidth + 8, 0), FlowConnectionCanvas).X,
                    target.Step.Id,
                    GetPortCenter(target.Container, "StepInputPort", false, inputPort.Index),
                    target.Container.TranslatePoint(new Point(-8, 0), FlowConnectionCanvas).X,
                    ReferenceEquals(target.Step, workbench.SelectedPipelineStep), inputPort.Contract, target.Step.FlowNodeStatusHasIssue));
            }
        }

        var signature = string.Join(
            '|',
            $"{FlowConnectionCanvas.ActualWidth:F0}x{FlowConnectionCanvas.ActualHeight:F0}",
            workbench.Source.Id,
            workbench.SelectedPipelineStep?.Id ?? string.Empty,
            string.Join(';', containers.Select(item => $"{item.Step.Id}:{item.Container.TranslatePoint(new Point(), FlowConnectionCanvas)}")),
            string.Join(';', edges.Select(edge => $"{edge.FromId}>{edge.ToId}:{edge.Start}:{edge.ExitX}:{edge.End}:{edge.EntryX}:{edge.Contract}:{edge.HasIssue}:{edge.IsSelected}")));
        if (string.Equals(flowConnectionSignature, signature, StringComparison.Ordinal)) return;

        flowConnectionSignature = signature;
        FlowConnectionCanvas.Children.Clear();
        FlowPortConnectionCanvas.Children.Clear();
        // A folded repeat group shares its visible endpoints; its logical connections remain distinct.
        foreach (var renderedEdges in edges.GroupBy(edge => (edge.Start, edge.ExitX, edge.End, edge.EntryX, edge.Contract)))
        {
            var edge = renderedEdges.First();
            DrawFlowConnection(edge.Start, edge.ExitX, edge.End, edge.EntryX, renderedEdges.Any(item => item.IsSelected), edge.Contract, renderedEdges.Any(item => item.HasIssue));
        }

        FlowConnectionCount = edges.Count;
        AutomationProperties.SetItemStatus(FlowConnectionCanvas, FlowConnectionCount.ToString());
    }

    private Point GetPortCenter(FrameworkElement node, string portName, bool output, int inputIndex = 0)
    {
        if (IsTeachingMode && node.DataContext is ToolWorkbenchPipelineStepItem { IsFlowRepeatGroupLead: true })
        {
            var groupPort = FindNamedPort(node, output ? "GroupOutputPortVisual" : "GroupInputPortVisual");
            if (groupPort is { ActualWidth: > 0, ActualHeight: > 0 } && HasVisiblePortPath(groupPort))
                return groupPort.TranslatePoint(new Point(groupPort.ActualWidth / 2, groupPort.ActualHeight / 2), FlowConnectionCanvas);
        }
        var port = node.Name == "SelectionOutputPort" ? node
            : node == RecipeInputNode ? SourceOutputPort
            : output ? FindNamedPort(node, portName) : FindInputPort(node, inputIndex);
        if (port is { ActualWidth: > 0, ActualHeight: > 0 } && HasVisiblePortPath(port))
        {
            if (FindNamedPort(port, "PortVisual") is { ActualWidth: > 0, ActualHeight: > 0 } glyph)
            {
                return glyph.TranslatePoint(new Point(glyph.ActualWidth / 2, glyph.ActualHeight / 2), FlowConnectionCanvas);
            }
            return port.TranslatePoint(new Point(port.ActualWidth / 2, port.ActualHeight / 2), FlowConnectionCanvas);
        }

        var closedPort = FindNamedPort(node, output ? "CollapsedOutputPortVisual" : "CollapsedInputPortVisual");
        if (closedPort is { ActualWidth: > 0, ActualHeight: > 0 } && HasVisiblePortPath(closedPort))
        {
            return closedPort.TranslatePoint(new Point(closedPort.ActualWidth / 2, closedPort.ActualHeight / 2), FlowConnectionCanvas);
        }
        var origin = node.TranslatePoint(new Point(), FlowConnectionCanvas);
        return new Point(origin.X + (output ? node.ActualWidth : 0), origin.Y + (node.ActualHeight * (output ? 0.72 : 0.48)));
    }

    private static bool HasVisiblePortPath(DependencyObject port)
    {
        for (var current = port; current is not null; current = VisualTreeHelper.GetParent(current))
        {
            if (current is UIElement { Visibility: not Visibility.Visible }) return false;
        }
        return true;
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

    private void DrawFlowConnection(Point start, double producerExitX, Point end, double targetEntryX, bool isSelected, string contract, bool hasIssue)
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
        var brushKey = contract.Contains("SignedDeltaHeightField", StringComparison.Ordinal) ? "FlowDeviationBrush"
            : contract.Contains("GridRectangle", StringComparison.Ordinal) || contract.Contains("RegionArtifact", StringComparison.Ordinal) ? "FlowRegionBrush"
            : contract.Contains("HeightField", StringComparison.Ordinal) ? "ThreeD.AccentBrush" : "ThreeD.SecondaryTextBrush";
        var path = new System.Windows.Shapes.Path
        {
            Data = geometry,
            StrokeThickness = isSelected ? 2.4 : 1.7,
            StrokeLineJoin = PenLineJoin.Round,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round,
            Opacity = isSelected ? 1 : 0.82,
        };
        path.SetResourceReference(Shape.StrokeProperty, brushKey);
        if (hasIssue) path.StrokeDashArray = new DoubleCollection([3d, 3d]);
        FlowConnectionCanvas.Children.Add(path);

        var ports = new GeometryGroup
        {
            Children = [new LineGeometry(start, new Point(exitX, start.Y)), new LineGeometry(new Point(entryX, end.Y), end)],
        };
        ports.Freeze();
        var portPath = new System.Windows.Shapes.Path { Data = ports, StrokeThickness = path.StrokeThickness, Opacity = path.Opacity };
        portPath.SetResourceReference(Shape.StrokeProperty, brushKey);
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
        arrow.SetResourceReference(Shape.FillProperty, brushKey);
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
