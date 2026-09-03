using System.Windows;
using System.Threading;
using OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;

namespace OpenVisionLab.ThreeD.Shell.Views.Tooling;

internal sealed class ToolLabWindowManager : IDisposable
{
    private readonly Window owner;
    private readonly ToolWorkbenchViewModel workbench;
    private readonly Action<string> showMissingStep;
    private FilterToolLabWindow? filter;
    private HeightDifferenceEdgeToolLabWindow? heightDifferenceEdge;
    private TwoPointLineToolLabWindow? twoPointLine;
    private ThreePointPlaneToolLabWindow? threePointPlane;
    private DatumPlaneDeviationToolLabWindow? datumPlaneDeviation;
    private LineIntersectionToolLabWindow? lineIntersection;
    private LandmarkCorrespondenceToolLabWindow? landmarkCorrespondence;
    private XYZAffineSolveToolLabWindow? xyzAffineSolve;
    private XYZAffineApplyToolLabWindow? xyzAffineApply;
    private RegridHeightMapToolLabWindow? regridHeightMap;
    private int disposalState;

    public ToolLabWindowManager(
        Window owner,
        ToolWorkbenchViewModel workbench,
        Action<string> showMissingStep)
    {
        this.owner = owner ?? throw new ArgumentNullException(nameof(owner));
        this.workbench = workbench ?? throw new ArgumentNullException(nameof(workbench));
        this.showMissingStep = showMissingStep ?? throw new ArgumentNullException(nameof(showMissingStep));
    }

    public FilterToolLabWindow? Filter => filter;

    public HeightDifferenceEdgeToolLabWindow? HeightDifferenceEdge => heightDifferenceEdge;

    public TwoPointLineToolLabWindow? TwoPointLine => twoPointLine;

    public ThreePointPlaneToolLabWindow? ThreePointPlane => threePointPlane;

    public DatumPlaneDeviationToolLabWindow? DatumPlaneDeviation => datumPlaneDeviation;

    public LineIntersectionToolLabWindow? LineIntersection => lineIntersection;

    public LandmarkCorrespondenceToolLabWindow? LandmarkCorrespondence => landmarkCorrespondence;

    public XYZAffineSolveToolLabWindow? XYZAffineSolve => xyzAffineSolve;

    public XYZAffineApplyToolLabWindow? XYZAffineApply => xyzAffineApply;

    public RegridHeightMapToolLabWindow? RegridHeightMap => regridHeightMap;

    internal bool IsDisposed => Volatile.Read(ref disposalState) != 0;

    public bool EnsureStepSelected(string toolId, bool preserveSelectedStep) =>
        !IsDisposed
        && ((preserveSelectedStep
            && string.Equals(workbench.SelectedPipelineStep?.ToolId, toolId, StringComparison.Ordinal))
        || workbench.SelectFirstPipelineStepForTool(toolId));

    public bool ShowForTool(
        string toolId,
        bool showMissing,
        bool preserveSelectedStep = false) =>
        toolId switch
        {
            "filter" => ShowFilter(showMissing, preserveSelectedStep),
            "height-difference-edge" => ShowHeightDifferenceEdge(showMissing, preserveSelectedStep),
            "two-point-line" => ShowTwoPointLine(showMissing, preserveSelectedStep),
            "three-point-plane" => ShowThreePointPlane(showMissing, preserveSelectedStep),
            "datum-plane-raw-height-deviation" => ShowDatumPlaneDeviation(showMissing, preserveSelectedStep),
            "line-intersection" => ShowLineIntersection(showMissing, preserveSelectedStep),
            "landmark-correspondence" => ShowLandmarkCorrespondence(showMissing, preserveSelectedStep),
            "xyz-affine-solve" => ShowXYZAffineSolve(showMissing, preserveSelectedStep),
            "xyz-affine-apply" => ShowXYZAffineApply(showMissing, preserveSelectedStep),
            "re-grid-height-map" => ShowRegridHeightMap(showMissing, preserveSelectedStep),
            _ => false
        };

    public bool ShowFilter(bool showMissing, bool preserveSelectedStep = false) =>
        Show(
            ref filter,
            "filter",
            "Filter",
            showMissing,
            preserveSelectedStep,
            step => new FilterToolLabWindow(workbench, step),
            (window, step) => window.SetLabStep(step),
            window => window.RefreshViews());

    public bool ShowHeightDifferenceEdge(bool showMissing, bool preserveSelectedStep = false) =>
        Show(
            ref heightDifferenceEdge,
            "height-difference-edge",
            "Height Difference Edge",
            showMissing,
            preserveSelectedStep,
            step => new HeightDifferenceEdgeToolLabWindow(workbench, step),
            (window, step) => window.SetLabStep(step),
            window => window.RefreshViews());

    public bool ShowTwoPointLine(bool showMissing, bool preserveSelectedStep = false) =>
        Show(
            ref twoPointLine,
            "two-point-line",
            "2-Point Line",
            showMissing,
            preserveSelectedStep,
            step => new TwoPointLineToolLabWindow(workbench, step),
            (window, step) => window.SetLabStep(step),
            window => window.RefreshViews());

    public bool ShowThreePointPlane(bool showMissing, bool preserveSelectedStep = false) =>
        Show(
            ref threePointPlane,
            "three-point-plane",
            "3-Point Plane",
            showMissing,
            preserveSelectedStep,
            step => new ThreePointPlaneToolLabWindow(workbench, step),
            (window, step) => window.SetLabStep(step),
            window => window.RefreshViews());

    public bool ShowDatumPlaneDeviation(bool showMissing, bool preserveSelectedStep = false) =>
        Show(
            ref datumPlaneDeviation,
            "datum-plane-raw-height-deviation",
            "Datum Plane Raw-Height Deviation",
            showMissing,
            preserveSelectedStep,
            step => new DatumPlaneDeviationToolLabWindow(workbench, step),
            (window, step) => window.SetLabStep(step),
            window => window.RefreshViews());

    public bool ShowLineIntersection(bool showMissing, bool preserveSelectedStep = false) =>
        Show(
            ref lineIntersection,
            "line-intersection",
            "Line Intersection",
            showMissing,
            preserveSelectedStep,
            step => new LineIntersectionToolLabWindow(workbench, step),
            (window, step) => window.SetLabStep(step),
            window => window.RefreshViews());

    public bool ShowLandmarkCorrespondence(bool showMissing, bool preserveSelectedStep = false) =>
        Show(
            ref landmarkCorrespondence,
            "landmark-correspondence",
            "Landmark Correspondence",
            showMissing,
            preserveSelectedStep,
            step => new LandmarkCorrespondenceToolLabWindow(workbench, step),
            (window, step) => window.SetLabStep(step),
            window => window.RefreshViews());

    public bool ShowXYZAffineSolve(bool showMissing, bool preserveSelectedStep = false) =>
        Show(
            ref xyzAffineSolve,
            "xyz-affine-solve",
            "XYZ Affine Solve",
            showMissing,
            preserveSelectedStep,
            step => new XYZAffineSolveToolLabWindow(workbench, step),
            (window, step) => window.SetLabStep(step),
            window => window.RefreshViews());

    public bool ShowXYZAffineApply(bool showMissing, bool preserveSelectedStep = false) =>
        Show(
            ref xyzAffineApply,
            "xyz-affine-apply",
            "Apply XYZ Affine",
            showMissing,
            preserveSelectedStep,
            step => new XYZAffineApplyToolLabWindow(workbench, step),
            (window, step) => window.SetLabStep(step),
            window => window.RefreshViews());

    public bool ShowRegridHeightMap(bool showMissing, bool preserveSelectedStep = false) =>
        Show(
            ref regridHeightMap,
            "re-grid-height-map",
            "Re-grid Height Map",
            showMissing,
            preserveSelectedStep,
            step => new RegridHeightMapToolLabWindow(workbench, step),
            (window, step) => window.SetLabStep(step),
            window => window.RefreshViews());

    private bool Show<TWindow>(
        ref TWindow? window,
        string toolId,
        string toolName,
        bool showMissing,
        bool preserveSelectedStep,
        Func<ToolWorkbenchPipelineStepItem, TWindow> create,
        Action<TWindow, ToolWorkbenchPipelineStepItem> setStep,
        Action<TWindow> refresh)
        where TWindow : ToolLabWindowBase
    {
        if (IsDisposed)
        {
            return false;
        }

        if (!EnsureStepSelected(toolId, preserveSelectedStep))
        {
            if (showMissing)
            {
                showMissingStep(toolName);
            }
            return false;
        }

        var step = workbench.SelectedPipelineStep!;
        if (window is null)
        {
            window = create(step);
            window.Owner = owner;
            window.Closed += OnToolLabWindowClosed;
        }
        else
        {
            setStep(window, step);
        }

        refresh(window);
        window.Show();
        window.Activate();
        return true;
    }

    private void OnToolLabWindowClosed(object? sender, EventArgs args)
    {
        if (sender is not ToolLabWindowBase window)
        {
            return;
        }

        window.Closed -= OnToolLabWindowClosed;
        if (IsDisposed)
        {
            return;
        }

        ClearWindowReference(window);
    }

    private void ClearWindowReference(ToolLabWindowBase window)
    {
        if (ReferenceEquals(filter, window))
        {
            filter = null;
        }
        else if (ReferenceEquals(heightDifferenceEdge, window))
        {
            heightDifferenceEdge = null;
        }
        else if (ReferenceEquals(twoPointLine, window))
        {
            twoPointLine = null;
        }
        else if (ReferenceEquals(threePointPlane, window))
        {
            threePointPlane = null;
        }
        else if (ReferenceEquals(datumPlaneDeviation, window))
        {
            datumPlaneDeviation = null;
        }
        else if (ReferenceEquals(lineIntersection, window))
        {
            lineIntersection = null;
        }
        else if (ReferenceEquals(landmarkCorrespondence, window))
        {
            landmarkCorrespondence = null;
        }
        else if (ReferenceEquals(xyzAffineSolve, window))
        {
            xyzAffineSolve = null;
        }
        else if (ReferenceEquals(xyzAffineApply, window))
        {
            xyzAffineApply = null;
        }
        else if (ReferenceEquals(regridHeightMap, window))
        {
            regridHeightMap = null;
        }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref disposalState, 1) != 0)
        {
            return;
        }

        var windows = new ToolLabWindowBase?[]
        {
            filter,
            heightDifferenceEdge,
            twoPointLine,
            threePointPlane,
            datumPlaneDeviation,
            lineIntersection,
            landmarkCorrespondence,
            xyzAffineSolve,
            xyzAffineApply,
            regridHeightMap
        };
        filter = null;
        heightDifferenceEdge = null;
        twoPointLine = null;
        threePointPlane = null;
        datumPlaneDeviation = null;
        lineIntersection = null;
        landmarkCorrespondence = null;
        xyzAffineSolve = null;
        xyzAffineApply = null;
        regridHeightMap = null;

        foreach (var window in windows)
        {
            if (window is null)
            {
                continue;
            }

            window.Closed -= OnToolLabWindowClosed;
            window.Dispose();
            if (window.IsVisible)
            {
                window.Close();
            }
        }

        GC.SuppressFinalize(this);
    }
}
