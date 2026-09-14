using System.Globalization;
using OpenVisionLab.ThreeD.Core;

namespace OpenVisionLab.ThreeD.Viewer.Automation;

/// <summary>
/// Owns command-line Smoke action projection. It translates stable CLI aliases
/// into Viewer ViewModel state and host operations; the scenario runner retains
/// Loaded admission, lifetime, stage ordering, rendering, capture, and shutdown.
/// </summary>
internal sealed class ViewerSmokeScenarioActionRouter
{
    private readonly IViewerSmokeHost host;

    public ViewerSmokeScenarioActionRouter(IViewerSmokeHost host)
    {
        this.host = host ?? throw new ArgumentNullException(nameof(host));
    }

    public void ApplyAction(string action)
    {
        if (action.Equals("fit-selection", StringComparison.OrdinalIgnoreCase))
        {
            host.FitSelection();
        }
        else if (action.Equals("color-height", StringComparison.OrdinalIgnoreCase)
            || action.Equals("height-color", StringComparison.OrdinalIgnoreCase))
        {
            host.SelectedColorMode = "Height";
        }
        else if (action.Equals("color-rgb", StringComparison.OrdinalIgnoreCase)
            || action.Equals("rgb-color", StringComparison.OrdinalIgnoreCase))
        {
            host.SelectedColorMode = "RGB";
        }
        else if (action.Equals("color-intensity", StringComparison.OrdinalIgnoreCase)
            || action.Equals("intensity-color", StringComparison.OrdinalIgnoreCase))
        {
            host.SelectedColorMode = "Intensity";
        }
        else if (action.Equals("color-normal", StringComparison.OrdinalIgnoreCase)
            || action.Equals("normal-color", StringComparison.OrdinalIgnoreCase))
        {
            host.SelectedColorMode = "Normal";
        }
        else if (action.Equals("color-solid", StringComparison.OrdinalIgnoreCase)
            || action.Equals("solid-color", StringComparison.OrdinalIgnoreCase))
        {
            host.SelectedColorMode = "Solid";
        }
        else if (action.Equals("color-grayscale", StringComparison.OrdinalIgnoreCase)
            || action.Equals("grayscale-color", StringComparison.OrdinalIgnoreCase))
        {
            host.SelectedColorMode = "Grayscale";
        }
        else if (action.Equals("color-thermal", StringComparison.OrdinalIgnoreCase)
            || action.Equals("thermal-color", StringComparison.OrdinalIgnoreCase))
        {
            host.SelectedColorMode = "Thermal";
        }
        else if (action.Equals("color-deviation", StringComparison.OrdinalIgnoreCase)
            || action.Equals("deviation-color", StringComparison.OrdinalIgnoreCase))
        {
            host.SelectedColorMode = "Deviation";
        }
        else if (action.Equals("geometry-points", StringComparison.OrdinalIgnoreCase))
        {
            host.SelectedGeometryStyle = "Points";
        }
        else if (action.Equals("geometry-wireframe", StringComparison.OrdinalIgnoreCase))
        {
            host.SelectedGeometryStyle = "Wireframe";
        }
        else if (action.Equals("geometry-surface", StringComparison.OrdinalIgnoreCase))
        {
            host.SelectedGeometryStyle = "Surface";
        }
        else if (action.Equals("geometry-surface-edges", StringComparison.OrdinalIgnoreCase)
            || action.Equals("geometry-surface-with-edges", StringComparison.OrdinalIgnoreCase))
        {
            host.SelectedGeometryStyle = "Surface + Edges";
        }
        else if (action.Equals("pan", StringComparison.OrdinalIgnoreCase))
        {
            host.Pan(-0.75, 0.35, 0.0);
        }
    }

    public void ApplySelection(string mode)
    {
        var selectionMode = mode.ToLowerInvariant() switch
        {
            "box" or "box-roi" => "Box ROI",
            "roi" or "roi-step" or "step-height" or "roi-interactive" or "interactive-roi" => "ROI Step Compare",
            "section" or "section-plane" => "Section Plane",
            "two-point" or "distance" or "distance-height" => "Two Point Measure",
            _ => "Point"
        };

        if (selectionMode == "Two Point Measure")
        {
            if (host.LazSampleVisible && host.HasLazPointCloud)
            {
                host.Measure(ViewerSmokeMeasurement.LazTwoPoint);
            }
            else if (host.GlbSampleVisible && host.HasImportedMesh)
            {
                host.Measure(ViewerSmokeMeasurement.MeshTwoPoint);
            }
            else
            {
                host.Measure(ViewerSmokeMeasurement.C3DTwoPoint);
            }

            return;
        }

        if (selectionMode == "ROI Step Compare")
        {
            if (mode.Equals("roi-interactive", StringComparison.OrdinalIgnoreCase)
                || mode.Equals("interactive-roi", StringComparison.OrdinalIgnoreCase))
            {
                host.Measure(ViewerSmokeMeasurement.InteractiveRoiStep);
            }
            else
            {
                host.Measure(ViewerSmokeMeasurement.RoiStep);
            }

            return;
        }

        host.UseSelectionSmokeScene(selectionMode);
    }

    public void ApplyMeasure(string measure)
    {
        if (measure.Equals("dimensions", StringComparison.OrdinalIgnoreCase)
            || measure.Equals("point-pair-dimensions", StringComparison.OrdinalIgnoreCase)
            || measure.Equals("width-distance-angle", StringComparison.OrdinalIgnoreCase))
        {
            host.Measure(ViewerSmokeMeasurement.PointPairDimensions);
        }
        else if (measure.Equals("two-point", StringComparison.OrdinalIgnoreCase)
            || measure.Equals("distance-height", StringComparison.OrdinalIgnoreCase)
            || measure.Equals("laz-two-point", StringComparison.OrdinalIgnoreCase)
            || measure.Equals("laz-distance-height", StringComparison.OrdinalIgnoreCase)
            || measure.Equals("glb-two-point", StringComparison.OrdinalIgnoreCase)
            || measure.Equals("mesh-two-point", StringComparison.OrdinalIgnoreCase)
            || measure.Equals("glb-distance-height", StringComparison.OrdinalIgnoreCase)
            || measure.Equals("mesh-distance-height", StringComparison.OrdinalIgnoreCase))
        {
            if (measure.StartsWith("laz-", StringComparison.OrdinalIgnoreCase)
                || (host.LazSampleVisible && host.HasLazPointCloud))
            {
                host.Measure(ViewerSmokeMeasurement.LazTwoPoint);
            }
            else if (measure.StartsWith("glb-", StringComparison.OrdinalIgnoreCase)
                || measure.StartsWith("mesh-", StringComparison.OrdinalIgnoreCase)
                || (host.GlbSampleVisible && host.HasImportedMesh))
            {
                host.Measure(ViewerSmokeMeasurement.MeshTwoPoint);
            }
            else
            {
                host.Measure(ViewerSmokeMeasurement.C3DTwoPoint);
            }
        }
        else if (measure.Equals("roi-step", StringComparison.OrdinalIgnoreCase)
            || measure.Equals("step-height", StringComparison.OrdinalIgnoreCase)
            || measure.Equals("roi", StringComparison.OrdinalIgnoreCase))
        {
            host.Measure(ViewerSmokeMeasurement.RoiStep);
        }
        else if (measure.Equals("roi-interactive", StringComparison.OrdinalIgnoreCase)
            || measure.Equals("interactive-roi", StringComparison.OrdinalIgnoreCase))
        {
            host.Measure(ViewerSmokeMeasurement.InteractiveRoiStep);
        }
        else if (measure.Equals("plane-distance", StringComparison.OrdinalIgnoreCase)
            || measure.Equals("distance-to-plane", StringComparison.OrdinalIgnoreCase)
            || measure.Equals("reference-plane", StringComparison.OrdinalIgnoreCase))
        {
            host.Measure(ViewerSmokeMeasurement.PlaneReference);
        }
        else if (measure.Equals("flatness", StringComparison.OrdinalIgnoreCase)
            || measure.Equals("plane-flatness", StringComparison.OrdinalIgnoreCase)
            || measure.Equals("reference-roi-flatness", StringComparison.OrdinalIgnoreCase))
        {
            host.Measure(ViewerSmokeMeasurement.PlaneFlatness);
        }
        else if (measure.Equals("gap-flush", StringComparison.OrdinalIgnoreCase)
            || measure.Equals("gapflush", StringComparison.OrdinalIgnoreCase))
        {
            host.Measure(ViewerSmokeMeasurement.GapFlush);
        }
        else if (measure.Equals("volume", StringComparison.OrdinalIgnoreCase))
        {
            host.Measure(ViewerSmokeMeasurement.Volume);
        }
        else if (measure.Equals("cross-section", StringComparison.OrdinalIgnoreCase)
            || measure.Equals("cross-section-dimensions", StringComparison.OrdinalIgnoreCase))
        {
            host.Measure(ViewerSmokeMeasurement.CrossSection);
        }
    }

    public void ApplyRecipeParameterEdit(string mode)
    {
        if (mode.Equals("laz-acceptance", StringComparison.OrdinalIgnoreCase)
            || mode.Equals("laz-two-point", StringComparison.OrdinalIgnoreCase))
        {
            if (!host.HasLazTwoPointMeasurement)
            {
                host.Measure(ViewerSmokeMeasurement.LazTwoPoint);
            }

            if (double.IsFinite(host.TwoPointDistance))
            {
                host.LazTwoPointExpectedDistance = host.TwoPointDistance - 0.001;
            }

            if (double.IsFinite(host.TwoPointRawHeightDelta))
            {
                host.LazTwoPointExpectedHeightDelta = host.TwoPointRawHeightDelta - 0.001;
            }

            host.LazTwoPointDistanceTolerance = 0.020;
            host.LazTwoPointHeightDeltaTolerance = 0.020;
            host.SelectedEntity = "LAZ/LAS Two Point Measurement";
            host.ViewerStatus = "Smoke recipe parameter edit: LAZ/LAS acceptance";
            return;
        }

        if (!mode.Equals("roi-align", StringComparison.OrdinalIgnoreCase)
            && !mode.Equals("roi-alignment", StringComparison.OrdinalIgnoreCase)
            && !mode.Equals("parameters", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (!host.C3DSampleVisible)
        {
            host.LoadSource(ViewerSmokeSource.C3D, null);
        }

        if (!host.RoiStepMeasurementVisible)
        {
            host.Measure(ViewerSmokeMeasurement.InteractiveRoiStep);
        }

        host.RecipeTransformTranslateX += 0.125;
        host.RecipeTransformTranslateY += 0.025;
        host.RecipeRoiLeftCenterX += 0.120;
        host.RecipeRoiRightCenterZ += 0.080;
        host.RecipeRoiLeftHalfWidth = Math.Max(0.050, host.RecipeRoiLeftHalfWidth * 0.92);
        host.RecipeRoiRightHalfDepth = Math.Max(0.050, host.RecipeRoiRightHalfDepth * 0.96);
        host.ApplyEditedRoiParameters();
        host.ViewerStatus = "Smoke recipe parameter edit: ROI/alignment";
    }

    public void ApplyInvalidRoi(string mode)
    {
        if (!host.C3DSampleVisible)
        {
            host.LoadSource(ViewerSmokeSource.C3D, null);
        }

        if (!host.RoiStepMeasurementVisible)
        {
            host.Measure(ViewerSmokeMeasurement.InteractiveRoiStep);
        }

        if (mode.Equals("overlap", StringComparison.OrdinalIgnoreCase))
        {
            host.RecipeRoiRightCenterX = host.RecipeRoiLeftCenterX;
            host.RecipeRoiRightCenterZ = host.RecipeRoiLeftCenterZ;
        }
        else
        {
            host.RecipeRoiLeftCenterX = 1000.0;
            host.RecipeRoiRightCenterX = 1002.0;
        }

        host.ApplyEditedRoiParameters();
        if (!host.ValidateRoi(out var warning))
        {
            host.SetRecipeValidationSummary(warning);
            host.ViewerStatus = "Smoke invalid ROI: validation warning";
        }
    }

    public void ApplyOverlay(string overlay)
    {
        if (overlay.Equals("result", StringComparison.OrdinalIgnoreCase))
        {
            if (host.C3DSampleVisible)
            {
                host.UseC3DHeightDeviationRuleSmokeScene();
            }
            else
            {
                host.UseResultSmokeScene();
            }
        }
    }

    public void ApplyRule(string rule)
    {
        if (rule.Equals("height-deviation", StringComparison.OrdinalIgnoreCase))
        {
            host.UseC3DHeightDeviationRuleSmokeScene();
        }
    }

    public bool ApplyRecipe(string path) => host.LoadRecipe(path);

    public void ApplyTolerance(string[] args)
    {
        var toleranceIndex = Array.IndexOf(args, "--smoke-tolerance");
        if (toleranceIndex >= 0
            && toleranceIndex + 1 < args.Length
            && double.TryParse(args[toleranceIndex + 1], NumberStyles.Float, CultureInfo.InvariantCulture, out var tolerance))
        {
            host.RecipePeakTolerance = tolerance;
        }

        var flatnessToleranceIndex = Array.IndexOf(args, "--smoke-flatness-tolerance");
        if (flatnessToleranceIndex >= 0
            && flatnessToleranceIndex + 1 < args.Length
            && double.TryParse(args[flatnessToleranceIndex + 1], NumberStyles.Float, CultureInfo.InvariantCulture, out var flatnessTolerance))
        {
            host.PlaneFlatnessTolerance = flatnessTolerance;
        }
    }

    public void ApplyAlignment(string mode)
    {
        if (!host.C3DSampleVisible)
        {
            host.LoadSource(ViewerSmokeSource.C3D, null);
        }

        var transform = mode.ToLowerInvariant() switch
        {
            "offset" or "translated" => new ModelTransform(0.350, 0.180, -0.250, 0.0, 0.0, 0.0, 1.0),
            "tilt" or "rotated" => new ModelTransform(0.250, 0.120, -0.180, 0.0, 0.0, 2.5, 1.0),
            _ => ModelTransform.Identity
        };
        var alignmentName = transform.Equals(ModelTransform.Identity) ? "Identity / not aligned" : $"Smoke {mode} alignment";
        host.SetC3DAlignment(transform, alignmentName, "C3D source frame");
        host.SelectedEntity = "C3D Alignment";
        host.ViewerStatus = $"Smoke alignment: {mode}";
    }
}
