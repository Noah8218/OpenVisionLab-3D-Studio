using System.Globalization;
using System.Numerics;
using OpenVisionLab.ThreeD.Data;
using OpenVisionLab.ThreeD.Tools;
using OpenVisionLab.ThreeD.Viewer.Rendering;
using OpenVisionLab.ThreeD.Viewer.ViewModels;

namespace OpenVisionLab.ThreeD.Viewer.Recipes;

/// <summary>
/// Owns the legacy C3D ROI editing workflow: alternating picks, recipe restore,
/// validation, reference alignment and the shared ROI overlay values.
/// The Viewer supplies native input/render callbacks; this session never owns
/// a control, a source buffer or an event subscription. Use on the caller's UI thread.
/// </summary>
internal sealed class C3DRoiEditingSession
{
    private const string RoiStepSelectionMode = "ROI Step Compare";
    private readonly MainWindowViewModel viewModel;
    private readonly Func<C3DHeightGrid?> getSample;
    private readonly Action ClearPlaneReferenceMeasurement;
    private readonly Action RenderNow;
    private (float MinX, float MaxX, float MinZ, float MaxZ, float MeanY)? roiStepLeftBounds;
    private (float MinX, float MaxX, float MinZ, float MaxZ, float MeanY)? roiStepRightBounds;
    private Vector3? roiStepLeftCenter;
    private Vector3? roiStepRightCenter;
    private Vector3? roiStepLeftAnchor;
    private Vector3? roiStepRightAnchor;
    private HeightDeviationRecipeRoiRegion? roiStepLeftRecipeRegion;
    private HeightDeviationRecipeRoiRegion? roiStepRightRecipeRegion;
    private bool roiStepInteractiveSelection;
    private bool roiStepNextPickSetsRight;
    private bool suppressRecipeParameterSync;

    public C3DRoiEditingSession(MainWindowViewModel viewModel, Func<C3DHeightGrid?> getSample,
        Action clearPlaneReferenceMeasurement, Action renderNow)
    {
        this.viewModel = viewModel;
        this.getSample = getSample;
        ClearPlaneReferenceMeasurement = clearPlaneReferenceMeasurement;
        RenderNow = renderNow;
    }

    private C3DHeightGrid? c3dSample => getSample();
    public (float MinX, float MaxX, float MinZ, float MaxZ, float MeanY)? LeftBounds => roiStepLeftBounds;
    public (float MinX, float MaxX, float MinZ, float MaxZ, float MeanY)? RightBounds => roiStepRightBounds;
    public Vector3? LeftCenter => roiStepLeftCenter;
    public Vector3? RightCenter => roiStepRightCenter;

    // Rendering reads these values; only the session changes the editing/overlay state.
    public void ClearOverlay()
    {
        roiStepLeftBounds = null;
        roiStepRightBounds = null;
        roiStepLeftCenter = null;
        roiStepRightCenter = null;
    }

    public void Reset()
    {
        ClearOverlay();
        roiStepLeftAnchor = null;
        roiStepRightAnchor = null;
        ClearRecipeRoiStep();
        roiStepInteractiveSelection = false;
        roiStepNextPickSetsRight = false;
    }

    // Property changes raised by recipe synchronization must not recursively edit the ROI.
    public void ApplyEditedParametersFromPropertyChange()
    {
        if (!suppressRecipeParameterSync) ApplyEditedRoiStepParameters();
    }

    // Volume shares the legacy left ROI outline, without changing ROI anchors/centers.
    public void ApplyVolumeOverlay(HeightDeviationRecipeRoiRegion region, double meanY)
    {
        roiStepLeftBounds = (
            (float)(region.CenterX - region.HalfWidth),
            (float)(region.CenterX + region.HalfWidth),
            (float)(region.CenterZ - region.HalfDepth),
            (float)(region.CenterZ + region.HalfDepth),
            (float)meanY);
        roiStepRightBounds = null;
    }

    private Vector3 TransformC3DPosition(Vector3 sourcePosition) => viewModel.C3DModelTransform.Apply(sourcePosition);
    private void SetRecipeValidationOk() => viewModel.SetRecipeValidationSummary("Validation: OK");
    private void SetRecipeValidationWarning(string warning) => viewModel.SetRecipeValidationSummary(warning);

    public void Pick(Vector3 anchor)
    {
        roiStepInteractiveSelection = true;
        ClearRecipeRoiStep();
        if (!roiStepNextPickSetsRight || roiStepLeftAnchor is null || roiStepRightAnchor is not null)
        {
            roiStepLeftAnchor = anchor;
            roiStepRightAnchor = null;
            roiStepNextPickSetsRight = true;
        }
        else
        {
            roiStepRightAnchor = anchor;
            roiStepNextPickSetsRight = false;
        }

        UpdateRoiStepMeasurement();
    }

    public bool ApplyRoiReferenceAlignment()
    {
        if (!ValidateRecipeState(requireRoi: true, out var warning))
        {
            SetRecipeValidationWarning(warning);
            viewModel.ViewerStatus = warning;
            return false;
        }

        if (!viewModel.C3DSampleVisible || c3dSample is null)
        {
            viewModel.ViewerStatus = "ROI alignment requires a visible C3D height grid";
            return false;
        }

        if (!UpdateRoiStepMeasurement()
            || roiStepLeftBounds is not { } leftBounds
            || roiStepRightBounds is not { } rightBounds
            || roiStepLeftCenter is not { } leftCenter
            || roiStepRightCenter is not { } rightCenter)
        {
            SetRecipeValidationWarning("Validation warning: ROI alignment requires valid left and right ROI regions.");
            viewModel.ViewerStatus = "ROI alignment requires left and right ROI regions";
            return false;
        }

        var referenceX = (leftCenter.X + rightCenter.X) * 0.5f;
        var referenceY = (leftCenter.Y + rightCenter.Y) * 0.5f;
        var referenceZ = (leftCenter.Z + rightCenter.Z) * 0.5f;
        var alignedLeft = HeightDeviationRoiGeometry.Offset(
            HeightDeviationRoiGeometry.FromBounds(
                new(leftBounds.MinX, leftBounds.MaxX, leftBounds.MinZ, leftBounds.MaxZ)),
            -referenceX,
            -referenceZ);
        var alignedRight = HeightDeviationRoiGeometry.Offset(
            HeightDeviationRoiGeometry.FromBounds(
                new(rightBounds.MinX, rightBounds.MaxX, rightBounds.MinZ, rightBounds.MaxZ)),
            -referenceX,
            -referenceZ);
        var current = viewModel.C3DModelTransform;
        var transform = current with
        {
            TranslateX = current.TranslateX - referenceX,
            TranslateY = current.TranslateY - referenceY,
            TranslateZ = current.TranslateZ - referenceZ
        };

        roiStepInteractiveSelection = true;
        roiStepNextPickSetsRight = false;
        roiStepLeftRecipeRegion = alignedLeft;
        roiStepRightRecipeRegion = alignedRight;
        roiStepLeftAnchor = new Vector3((float)alignedLeft.CenterX, 0.0f, (float)alignedLeft.CenterZ);
        roiStepRightAnchor = new Vector3((float)alignedRight.CenterX, 0.0f, (float)alignedRight.CenterZ);
        viewModel.SelectedSelectionMode = RoiStepSelectionMode;
        viewModel.SelectionOverlayVisible = true;
        viewModel.SetC3DAlignment(transform, "ROI reference alignment", "ROI step centers");
        SyncRecipeRoiEditFromRegions("Interactive", alignedLeft, alignedRight, viewModel.RecipeRoiMaxSampledPoints);

        if (UpdateRoiStepMeasurement())
        {
            viewModel.SetAlignmentWorkflowSummary(string.Create(
                CultureInfo.InvariantCulture,
                $"ROI alignment: ROI pair centered at origin; dT({-referenceX:F3}, {-referenceY:F3}, {-referenceZ:F3})"));
            SetRecipeValidationOk();
            viewModel.SelectedEntity = "ROI Step Compare";
            viewModel.ViewerStatus = "ROI alignment applied from selected regions";
            RenderNow();
            return true;
        }

        viewModel.ViewerStatus = "ROI alignment applied, but ROI measurement could not be recalculated";
        RenderNow();
        return false;
    }

    public HeightDeviationRecipeRoiStep? CreateCurrentRoiStepRecipe()
    {
        if (!viewModel.RoiStepMeasurementVisible)
        {
            return null;
        }

        return new HeightDeviationRecipeRoiStep(
            viewModel.RecipeRoiMode,
            CreateLeftRoiRegionFromViewModel(),
            CreateRightRoiRegionFromViewModel(),
            viewModel.RecipeRoiMaxSampledPoints);
    }

    private HeightDeviationRecipeRoiRegion CreateLeftRoiRegionFromViewModel() =>
        new(
            viewModel.RecipeRoiLeftCenterX,
            viewModel.RecipeRoiLeftCenterZ,
            viewModel.RecipeRoiLeftHalfWidth,
            viewModel.RecipeRoiLeftHalfDepth);

    private HeightDeviationRecipeRoiRegion CreateRightRoiRegionFromViewModel() =>
        new(
            viewModel.RecipeRoiRightCenterX,
            viewModel.RecipeRoiRightCenterZ,
            viewModel.RecipeRoiRightHalfWidth,
            viewModel.RecipeRoiRightHalfDepth);

    public bool ValidateRecipeState(bool requireRoi, out string warning)
    {
        var transform = viewModel.C3DModelTransform;
        if (!double.IsFinite(transform.TranslateX)
            || !double.IsFinite(transform.TranslateY)
            || !double.IsFinite(transform.TranslateZ)
            || !double.IsFinite(transform.RotateXDegrees)
            || !double.IsFinite(transform.RotateYDegrees)
            || !double.IsFinite(transform.RotateZDegrees)
            || !double.IsFinite(transform.Scale)
            || transform.Scale <= 0.0)
        {
            warning = "Validation warning: transform values must be finite and scale must be positive.";
            return false;
        }

        if (!requireRoi)
        {
            warning = "Validation: OK";
            return true;
        }

        if (!viewModel.C3DSampleVisible || c3dSample is null || c3dSample.Points.Length == 0)
        {
            warning = "Validation warning: ROI validation requires a visible C3D height grid.";
            return false;
        }

        var left = CreateLeftRoiRegionFromViewModel();
        var right = CreateRightRoiRegionFromViewModel();
        if (!HeightDeviationRoiGeometry.IsValid(left)
            || !HeightDeviationRoiGeometry.IsValid(right))
        {
            warning = "Validation warning: ROI center and size values must be finite and positive.";
            return false;
        }

        var bounds = GetTransformedC3DBounds();
        if (!HeightDeviationRoiGeometry.Intersects(
                left,
                new(bounds.MinX, bounds.MaxX, bounds.MinZ, bounds.MaxZ)))
        {
            warning = "Validation warning: left ROI is outside the visible C3D bounds.";
            return false;
        }

        if (!HeightDeviationRoiGeometry.Intersects(
                right,
                new(bounds.MinX, bounds.MaxX, bounds.MinZ, bounds.MaxZ)))
        {
            warning = "Validation warning: right ROI is outside the visible C3D bounds.";
            return false;
        }

        if (HeightDeviationRoiGeometry.Overlaps(left, right))
        {
            warning = "Validation warning: left and right ROI regions overlap.";
            return false;
        }

        if (!TryCalculateRoiStats(CreateRoiBounds(left, bounds), out var leftStats) || leftStats.Count < 10)
        {
            warning = "Validation warning: left ROI has too few C3D samples.";
            return false;
        }

        if (!TryCalculateRoiStats(CreateRoiBounds(right, bounds), out var rightStats) || rightStats.Count < 10)
        {
            warning = "Validation warning: right ROI has too few C3D samples.";
            return false;
        }

        warning = "Validation: OK";
        return true;
    }

    public bool ValidatePlaneFlatnessRecipeState(out string warning)
    {
        var step = viewModel.CreatePlaneFlatnessRecipeStep();
        if (!HeightDeviationRoiGeometry.IsValid(step.ReferenceRegion)
            || !double.IsFinite(step.Tolerance)
            || step.Tolerance <= 0.0)
        {
            warning = "Validation warning: flatness reference ROI and tolerance must be finite and positive.";
            return false;
        }

        if (!viewModel.C3DSampleVisible || c3dSample is null || c3dSample.Points.Length == 0)
        {
            warning = "Validation warning: plane flatness requires a visible C3D height grid.";
            return false;
        }

        var referenceSampleCount = c3dSample.Points.Count(
            point => HeightDeviationRoiGeometry.Contains(
                step.ReferenceRegion,
                TransformC3DPosition(point.Position)));
        if (referenceSampleCount < 3)
        {
            warning = "Validation warning: flatness reference ROI contains fewer than three C3D samples.";
            return false;
        }

        warning = "Validation: OK";
        return true;
    }

    public void ApplyEditedRoiStepParameters()
    {
        if (!viewModel.C3DSampleVisible)
        {
            viewModel.UseC3DSmokeScene();
        }

        roiStepInteractiveSelection = true;
        roiStepNextPickSetsRight = false;
        roiStepLeftRecipeRegion = CreateLeftRoiRegionFromViewModel();
        roiStepRightRecipeRegion = CreateRightRoiRegionFromViewModel();
        roiStepLeftAnchor = new Vector3((float)viewModel.RecipeRoiLeftCenterX, 0.0f, (float)viewModel.RecipeRoiLeftCenterZ);
        roiStepRightAnchor = new Vector3((float)viewModel.RecipeRoiRightCenterX, 0.0f, (float)viewModel.RecipeRoiRightCenterZ);
        viewModel.SelectedSelectionMode = RoiStepSelectionMode;
        viewModel.SelectionOverlayVisible = true;

        if (UpdateRoiStepMeasurement())
        {
            if (ValidateRecipeState(requireRoi: true, out var warning))
            {
                SetRecipeValidationOk();
            }
            else
            {
                SetRecipeValidationWarning(warning);
            }

            viewModel.SelectedEntity = "ROI Step Compare";
            viewModel.ViewerStatus = "Recipe ROI parameters updated";
        }
        else
        {
            ValidateRecipeState(requireRoi: true, out var warning);
            SetRecipeValidationWarning(warning);
        }
    }

    public void ApplyRecipeRoiStep(HeightDeviationRecipeRoiStep? roiStep)
    {
        ClearRecipeRoiStep();
        if (roiStep is null)
        {
            return;
        }

        if (!viewModel.C3DSampleVisible)
        {
            viewModel.UseC3DSmokeScene();
        }

        roiStepInteractiveSelection = roiStep.Mode.Equals("Interactive", StringComparison.OrdinalIgnoreCase);
        roiStepNextPickSetsRight = false;
        roiStepLeftRecipeRegion = roiStep.Left;
        roiStepRightRecipeRegion = roiStep.Right;
        SyncRecipeRoiEditFromRegions(roiStep.Mode, roiStep.Left, roiStep.Right, roiStep.MaxSampledPoints);
        roiStepLeftAnchor = new Vector3((float)roiStep.Left.CenterX, 0.0f, (float)roiStep.Left.CenterZ);
        roiStepRightAnchor = new Vector3((float)roiStep.Right.CenterX, 0.0f, (float)roiStep.Right.CenterZ);
        viewModel.SelectedSelectionMode = RoiStepSelectionMode;
        viewModel.SelectionOverlayVisible = true;

        if (UpdateRoiStepMeasurement())
        {
            viewModel.SelectedEntity = "ROI Step Compare";
            viewModel.ViewerStatus = "Recipe ROI step restored";
        }
    }

    private void ClearRecipeRoiStep()
    {
        roiStepLeftRecipeRegion = null;
        roiStepRightRecipeRegion = null;
    }

    private void SyncRecipeRoiEditFromBounds(
        string mode,
        (float MinX, float MaxX, float MinZ, float MaxZ, float MeanY) leftBounds,
        (float MinX, float MaxX, float MinZ, float MaxZ, float MeanY) rightBounds)
    {
        SyncRecipeRoiEditFromRegions(
            mode,
            HeightDeviationRoiGeometry.FromBounds(
                new(leftBounds.MinX, leftBounds.MaxX, leftBounds.MinZ, leftBounds.MaxZ)),
            HeightDeviationRoiGeometry.FromBounds(
                new(rightBounds.MinX, rightBounds.MaxX, rightBounds.MinZ, rightBounds.MaxZ)),
            viewModel.RecipeRoiMaxSampledPoints);
    }

    private void SyncRecipeRoiEditFromRegions(
        string mode,
        HeightDeviationRecipeRoiRegion left,
        HeightDeviationRecipeRoiRegion right,
        int maxSampledPoints)
    {
        suppressRecipeParameterSync = true;
        try
        {
            viewModel.SetRecipeRoiStepEdit(
                mode,
                left.CenterX,
                left.CenterZ,
                left.HalfWidth,
                left.HalfDepth,
                right.CenterX,
                right.CenterZ,
                right.HalfWidth,
                right.HalfDepth,
                maxSampledPoints);
        }
        finally
        {
            suppressRecipeParameterSync = false;
        }
    }

    public bool UpdateRoiStepMeasurement()
    {
        ClearPlaneReferenceMeasurement();
        roiStepLeftBounds = null;
        roiStepRightBounds = null;
        roiStepLeftCenter = null;
        roiStepRightCenter = null;

        if (!viewModel.C3DSampleVisible || c3dSample is null || c3dSample.Points.Length < 2)
        {
            viewModel.ClearRoiStepMeasurement("ROI step requires a visible C3D height grid.");
            viewModel.SelectedEntity = "ROI Step Compare";
            return false;
        }

        var bounds = GetTransformedC3DBounds();
        var width = Math.Max(0.001f, bounds.MaxX - bounds.MinX);
        var depth = Math.Max(0.001f, bounds.MaxZ - bounds.MinZ);
        var halfWidth = width * 0.15f;
        var halfDepth = depth * 0.25f;
        var zMin = bounds.MinZ + depth * 0.25f;
        var zMax = bounds.MinZ + depth * 0.75f;
        var leftBounds = roiStepLeftRecipeRegion is { } leftRegion
            ? CreateRoiBounds(leftRegion, bounds)
            : roiStepInteractiveSelection && roiStepLeftAnchor is { } leftAnchor
                ? CreateRoiBounds(leftAnchor, halfWidth, halfDepth, bounds)
                : (MinX: bounds.MinX + width * 0.10f, MaxX: bounds.MinX + width * 0.40f, MinZ: zMin, MaxZ: zMax, MeanY: 0.0f);
        var rightBounds = roiStepRightRecipeRegion is { } rightRegion
            ? CreateRoiBounds(rightRegion, bounds)
            : roiStepInteractiveSelection && roiStepRightAnchor is { } rightAnchor
                ? CreateRoiBounds(rightAnchor, halfWidth, halfDepth, bounds)
                : (MinX: bounds.MinX + width * 0.60f, MaxX: bounds.MinX + width * 0.90f, MinZ: zMin, MaxZ: zMax, MeanY: 0.0f);

        if (!TryCalculateRoiStats(leftBounds, out var left))
        {
            viewModel.ClearRoiStepMeasurement("ROI step found no C3D points in the left region.");
            viewModel.SelectedEntity = "ROI Step Compare";
            return false;
        }

        roiStepLeftBounds = (leftBounds.MinX, leftBounds.MaxX, leftBounds.MinZ, leftBounds.MaxZ, (float)left.ModelYMean);
        roiStepLeftCenter = left.Center;

        if (roiStepInteractiveSelection && roiStepRightAnchor is null)
        {
            viewModel.SetRoiStepSelectionPending(
                string.Create(CultureInfo.InvariantCulture, $"ROI step: L {left.Count:N0} pts, pick R"),
                string.Create(CultureInfo.InvariantCulture, $"Left mean raw {left.RawMean:F3}; click right ROI center."),
                "Interactive");
            viewModel.SelectedEntity = "ROI Step Compare";
            return true;
        }

        if (!TryCalculateRoiStats(rightBounds, out var right))
        {
            viewModel.ClearRoiStepMeasurement("ROI step found no C3D points in the right region.");
            viewModel.SelectedEntity = "ROI Step Compare";
            return false;
        }

        roiStepRightBounds = (rightBounds.MinX, rightBounds.MaxX, rightBounds.MinZ, rightBounds.MaxZ, (float)right.ModelYMean);
        roiStepRightCenter = right.Center;

        viewModel.SetRoiStepMeasurement(
            left.Count,
            left.RawMean,
            left.ModelYMean,
            right.Count,
            right.RawMean,
            right.ModelYMean,
            roiStepInteractiveSelection ? "Interactive" : "Auto");
        SyncRecipeRoiEditFromBounds(roiStepInteractiveSelection ? "Interactive" : "Auto", leftBounds, rightBounds);
        viewModel.SelectedEntity = "ROI Step Compare";
        viewModel.PickCoordinate = string.Create(
            CultureInfo.InvariantCulture,
            $"ROI centers: L {CameraMath.FormatPoint(left.Center)} | R {CameraMath.FormatPoint(right.Center)}");
        return true;
    }

    private (float MinX, float MaxX, float MinZ, float MaxZ) GetTransformedC3DBounds()
    {
        var minX = float.PositiveInfinity;
        var maxX = float.NegativeInfinity;
        var minZ = float.PositiveInfinity;
        var maxZ = float.NegativeInfinity;

        foreach (var point in c3dSample!.Points)
        {
            var position = TransformC3DPosition(point.Position);
            minX = Math.Min(minX, position.X);
            maxX = Math.Max(maxX, position.X);
            minZ = Math.Min(minZ, position.Z);
            maxZ = Math.Max(maxZ, position.Z);
        }

        return (minX, maxX, minZ, maxZ);
    }

    private static (float MinX, float MaxX, float MinZ, float MaxZ, float MeanY) CreateRoiBounds(
        Vector3 center,
        float halfWidth,
        float halfDepth,
        (float MinX, float MaxX, float MinZ, float MaxZ) sceneBounds) =>
        (
            Math.Clamp(center.X - halfWidth, sceneBounds.MinX, sceneBounds.MaxX),
            Math.Clamp(center.X + halfWidth, sceneBounds.MinX, sceneBounds.MaxX),
            Math.Clamp(center.Z - halfDepth, sceneBounds.MinZ, sceneBounds.MaxZ),
            Math.Clamp(center.Z + halfDepth, sceneBounds.MinZ, sceneBounds.MaxZ),
            center.Y);

    private static (float MinX, float MaxX, float MinZ, float MaxZ, float MeanY) CreateRoiBounds(
        HeightDeviationRecipeRoiRegion region,
        (float MinX, float MaxX, float MinZ, float MaxZ) sceneBounds) =>
        (
            Math.Clamp((float)(region.CenterX - region.HalfWidth), sceneBounds.MinX, sceneBounds.MaxX),
            Math.Clamp((float)(region.CenterX + region.HalfWidth), sceneBounds.MinX, sceneBounds.MaxX),
            Math.Clamp((float)(region.CenterZ - region.HalfDepth), sceneBounds.MinZ, sceneBounds.MaxZ),
            Math.Clamp((float)(region.CenterZ + region.HalfDepth), sceneBounds.MinZ, sceneBounds.MaxZ),
            0.0f);

    private bool TryCalculateRoiStats(
        (float MinX, float MaxX, float MinZ, float MaxZ, float MeanY) bounds,
        out (int Count, double RawMean, double ModelYMean, Vector3 Center) stats)
    {
        var count = 0;
        var rawSum = 0.0;
        var xSum = 0.0;
        var ySum = 0.0;
        var zSum = 0.0;

        foreach (var point in c3dSample!.Points)
        {
            var position = TransformC3DPosition(point.Position);
            if (position.X < bounds.MinX || position.X > bounds.MaxX
                || position.Z < bounds.MinZ || position.Z > bounds.MaxZ)
            {
                continue;
            }

            count++;
            rawSum += point.RawValue;
            xSum += position.X;
            ySum += position.Y;
            zSum += position.Z;
        }

        if (count == 0)
        {
            stats = default;
            return false;
        }

        var inverse = 1.0 / count;
        stats = (
            count,
            rawSum * inverse,
            ySum * inverse,
            new Vector3((float)(xSum * inverse), (float)(ySum * inverse), (float)(zSum * inverse)));
        return true;
    }

    public void ApplySmokeRoiStepMeasurement()
    {
        if (c3dSample is null || c3dSample.Points.Length < 2)
        {
            viewModel.ViewerStatus = "Smoke measure failed: C3D sample missing";
            return;
        }

        viewModel.UseC3DSmokeScene();
        viewModel.SelectedSelectionMode = RoiStepSelectionMode;
        viewModel.SelectionOverlayVisible = true;
        roiStepInteractiveSelection = false;
        roiStepLeftAnchor = null;
        roiStepRightAnchor = null;
        ClearRecipeRoiStep();
        roiStepNextPickSetsRight = false;

        if (UpdateRoiStepMeasurement())
        {
            viewModel.SelectedEntity = "ROI Step Compare";
            viewModel.ViewerStatus = "Smoke measure: ROI step-height comparison";
        }
    }

    public void ApplySmokeInteractiveRoiStepMeasurement()
    {
        if (c3dSample is null || c3dSample.Points.Length < 2)
        {
            viewModel.ViewerStatus = "Smoke measure failed: C3D sample missing";
            return;
        }

        viewModel.UseC3DSmokeScene();
        viewModel.SelectedSelectionMode = RoiStepSelectionMode;
        viewModel.SelectionOverlayVisible = true;
        roiStepInteractiveSelection = true;
        roiStepNextPickSetsRight = false;
        ClearRecipeRoiStep();

        var bounds = GetTransformedC3DBounds();
        var centerZ = (bounds.MinZ + bounds.MaxZ) * 0.5f;
        roiStepLeftAnchor = new Vector3(bounds.MinX + (bounds.MaxX - bounds.MinX) * 0.30f, 0.0f, centerZ);
        roiStepRightAnchor = new Vector3(bounds.MinX + (bounds.MaxX - bounds.MinX) * 0.70f, 0.0f, centerZ);

        if (UpdateRoiStepMeasurement())
        {
            viewModel.SelectedEntity = "ROI Step Compare";
            viewModel.ViewerStatus = "Smoke measure: interactive ROI step-height comparison";
        }
    }

    public void ApplyGapFlushRecipeRoiState(C3DGapFlushStep step)
    {
        roiStepLeftRecipeRegion = step.LeftRegion;
        roiStepRightRecipeRegion = step.RightRegion;
        roiStepInteractiveSelection = false;
        roiStepNextPickSetsRight = false;
    }

    public void ApplyGapFlushPreviewOverlay(
        C3DGapFlushStep step,
        GapFlushRegionStats left,
        GapFlushRegionStats right)
    {
        ApplyGapFlushRecipeRoiState(step);
        roiStepLeftBounds = (
            (float)(step.LeftRegion.CenterX - step.LeftRegion.HalfWidth),
            (float)(step.LeftRegion.CenterX + step.LeftRegion.HalfWidth),
            (float)(step.LeftRegion.CenterZ - step.LeftRegion.HalfDepth),
            (float)(step.LeftRegion.CenterZ + step.LeftRegion.HalfDepth),
            (float)left.ModelYMean);
        roiStepRightBounds = (
            (float)(step.RightRegion.CenterX - step.RightRegion.HalfWidth),
            (float)(step.RightRegion.CenterX + step.RightRegion.HalfWidth),
            (float)(step.RightRegion.CenterZ - step.RightRegion.HalfDepth),
            (float)(step.RightRegion.CenterZ + step.RightRegion.HalfDepth),
            (float)right.ModelYMean);
        roiStepLeftCenter = new Vector3(
            (float)step.LeftRegion.CenterX,
            (float)left.ModelYMean,
            (float)step.LeftRegion.CenterZ);
        roiStepRightCenter = new Vector3(
            (float)step.RightRegion.CenterX,
            (float)right.ModelYMean,
            (float)step.RightRegion.CenterZ);
    }
}
