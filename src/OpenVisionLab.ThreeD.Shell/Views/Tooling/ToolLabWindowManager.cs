using System.Windows;
using OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;

namespace OpenVisionLab.ThreeD.Shell.Views.Tooling;

internal sealed class ToolLabWindowManager
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

    public bool EnsureStepSelected(string toolId, bool preserveSelectedStep) =>
        (preserveSelectedStep
            && string.Equals(workbench.SelectedPipelineStep?.ToolId, toolId, StringComparison.Ordinal))
        || workbench.SelectFirstPipelineStepForTool(toolId);

    public bool ShowFilter(bool showMissing, bool preserveSelectedStep = false) =>
        Show(
            ref filter,
            "filter",
            "Filter",
            showMissing,
            preserveSelectedStep,
            step => new FilterToolLabWindow(workbench, step),
            (window, step) => window.SetLabStep(step),
            window => window.RefreshViews(),
            () => filter = null);

    public bool ShowHeightDifferenceEdge(bool showMissing, bool preserveSelectedStep = false) =>
        Show(
            ref heightDifferenceEdge,
            "height-difference-edge",
            "Height Difference Edge",
            showMissing,
            preserveSelectedStep,
            step => new HeightDifferenceEdgeToolLabWindow(workbench, step),
            (window, step) => window.SetLabStep(step),
            window => window.RefreshViews(),
            () => heightDifferenceEdge = null);

    public bool ShowTwoPointLine(bool showMissing, bool preserveSelectedStep = false) =>
        Show(
            ref twoPointLine,
            "two-point-line",
            "2-Point Line",
            showMissing,
            preserveSelectedStep,
            step => new TwoPointLineToolLabWindow(workbench, step),
            (window, step) => window.SetLabStep(step),
            window => window.RefreshViews(),
            () => twoPointLine = null);

    public bool ShowThreePointPlane(bool showMissing, bool preserveSelectedStep = false) =>
        Show(
            ref threePointPlane,
            "three-point-plane",
            "3-Point Plane",
            showMissing,
            preserveSelectedStep,
            step => new ThreePointPlaneToolLabWindow(workbench, step),
            (window, step) => window.SetLabStep(step),
            window => window.RefreshViews(),
            () => threePointPlane = null);

    public bool ShowDatumPlaneDeviation(bool showMissing, bool preserveSelectedStep = false) =>
        Show(
            ref datumPlaneDeviation,
            "datum-plane-raw-height-deviation",
            "Datum Plane Raw-Height Deviation",
            showMissing,
            preserveSelectedStep,
            step => new DatumPlaneDeviationToolLabWindow(workbench, step),
            (window, step) => window.SetLabStep(step),
            window => window.RefreshViews(),
            () => datumPlaneDeviation = null);

    public bool ShowLineIntersection(bool showMissing, bool preserveSelectedStep = false) =>
        Show(
            ref lineIntersection,
            "line-intersection",
            "Line Intersection",
            showMissing,
            preserveSelectedStep,
            step => new LineIntersectionToolLabWindow(workbench, step),
            (window, step) => window.SetLabStep(step),
            window => window.RefreshViews(),
            () => lineIntersection = null);

    public bool ShowLandmarkCorrespondence(bool showMissing, bool preserveSelectedStep = false) =>
        Show(
            ref landmarkCorrespondence,
            "landmark-correspondence",
            "Landmark Correspondence",
            showMissing,
            preserveSelectedStep,
            step => new LandmarkCorrespondenceToolLabWindow(workbench, step),
            (window, step) => window.SetLabStep(step),
            window => window.RefreshViews(),
            () => landmarkCorrespondence = null);

    public bool ShowXYZAffineSolve(bool showMissing, bool preserveSelectedStep = false) =>
        Show(
            ref xyzAffineSolve,
            "xyz-affine-solve",
            "XYZ Affine Solve",
            showMissing,
            preserveSelectedStep,
            step => new XYZAffineSolveToolLabWindow(workbench, step),
            (window, step) => window.SetLabStep(step),
            window => window.RefreshViews(),
            () => xyzAffineSolve = null);

    public bool ShowXYZAffineApply(bool showMissing, bool preserveSelectedStep = false) =>
        Show(
            ref xyzAffineApply,
            "xyz-affine-apply",
            "Apply XYZ Affine",
            showMissing,
            preserveSelectedStep,
            step => new XYZAffineApplyToolLabWindow(workbench, step),
            (window, step) => window.SetLabStep(step),
            window => window.RefreshViews(),
            () => xyzAffineApply = null);

    public bool ShowRegridHeightMap(bool showMissing, bool preserveSelectedStep = false) =>
        Show(
            ref regridHeightMap,
            "re-grid-height-map",
            "Re-grid Height Map",
            showMissing,
            preserveSelectedStep,
            step => new RegridHeightMapToolLabWindow(workbench, step),
            (window, step) => window.SetLabStep(step),
            window => window.RefreshViews(),
            () => regridHeightMap = null);

    private bool Show<TWindow>(
        ref TWindow? window,
        string toolId,
        string toolName,
        bool showMissing,
        bool preserveSelectedStep,
        Func<ToolWorkbenchPipelineStepItem, TWindow> create,
        Action<TWindow, ToolWorkbenchPipelineStepItem> setStep,
        Action<TWindow> refresh,
        Action clear)
        where TWindow : Window
    {
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
            window.Closed += (_, _) => clear();
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
}
