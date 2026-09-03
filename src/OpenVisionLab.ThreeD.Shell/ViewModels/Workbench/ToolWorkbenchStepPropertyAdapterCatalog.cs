using System.Globalization;
using OpenVisionLab.ThreeD.Core;

namespace OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;

/// <summary>
/// Owns the ToolId-to-typed-property mapping used by the Workbench PropertyGrid.
/// The session keeps draft state; this catalog remains stateless and only adapts
/// typed drafts to the recipe parameter contract.
/// </summary>
internal static class ToolWorkbenchStepPropertyAdapterCatalog
{
    private delegate bool ParameterWriter(
        object draft,
        out IReadOnlyDictionary<string, string> values,
        out string message);

    private sealed class Adapter
    {
        public Adapter(
            Type draftType,
            IReadOnlySet<string> mappedNames,
            Func<ToolWorkbenchPipelineStepItem, object> createDraft,
            ParameterWriter writeParameters)
        {
            DraftType = draftType;
            MappedNames = mappedNames;
            CreateDraft = createDraft;
            WriteParameters = writeParameters;
        }

        public Type DraftType { get; }

        public IReadOnlySet<string> MappedNames { get; }

        public Func<ToolWorkbenchPipelineStepItem, object> CreateDraft { get; }

        public ParameterWriter WriteParameters { get; }
    }

    private static readonly IReadOnlyDictionary<string, Adapter> adapters =
        CreateAdapters();

    private static readonly IReadOnlySet<string> EmptyMappedNames =
        new HashSet<string>(StringComparer.Ordinal);

    private static readonly IReadOnlyDictionary<string, string> EmptyParameters =
        new Dictionary<string, string>(StringComparer.Ordinal);

    internal static bool IsSupported(string toolId) =>
        adapters.ContainsKey(toolId);

    internal static bool TryGetMappedNames(
        string toolId,
        out IReadOnlySet<string> mappedNames)
    {
        if (adapters.TryGetValue(toolId, out var adapter))
        {
            mappedNames = adapter.MappedNames;
            return true;
        }

        mappedNames = EmptyMappedNames;
        return false;
    }

    internal static bool TryCreateDraft(
        string toolId,
        ToolWorkbenchPipelineStepItem step,
        out object? draft)
    {
        if (adapters.TryGetValue(toolId, out var adapter))
        {
            draft = adapter.CreateDraft(step);
            return true;
        }

        draft = null;
        return false;
    }

    internal static bool TryCreateParameterValues(
        object? draft,
        out IReadOnlyDictionary<string, string> values,
        out string message)
    {
        values = EmptyParameters;
        message = string.Empty;
        if (draft is null || !TryGetForDraft(draft, out var adapter))
        {
            message = "This step has no typed parameter adapter.";
            return false;
        }

        return adapter.WriteParameters(draft, out values, out message);
    }

    private static bool TryGetForDraft(object draft, out Adapter adapter)
    {
        foreach (var candidate in adapters.Values)
        {
            if (candidate.DraftType.IsInstanceOfType(draft))
            {
                adapter = candidate;
                return true;
            }
        }

        adapter = null!;
        return false;
    }

    private static IReadOnlyDictionary<string, Adapter> CreateAdapters() =>
        new Dictionary<string, Adapter>(StringComparer.Ordinal)
        {
            ["filter"] = Create<FilterStepProperties>(
                FilterStepProperties.MappedNames,
                FilterStepProperties.From,
                TryCreateFilterParameters),
            ["remove-outlier-pixels"] = Create<RemoveOutlierPixelsStepProperties>(
                RemoveOutlierPixelsStepProperties.MappedNames,
                RemoveOutlierPixelsStepProperties.From,
                TryCreateRemoveOutlierParameters),
            ["connected-region"] = Create<ConnectedRegionStepProperties>(
                ConnectedRegionStepProperties.MappedNames,
                ConnectedRegionStepProperties.From,
                TryCreateConnectedRegionParameters),
            ["domain-mask"] = Create<DomainMaskStepProperties>(
                DomainMaskStepProperties.MappedNames,
                DomainMaskStepProperties.From,
                TryCreateDomainMaskParameters),
            ["editable-region"] = Create<EditableRegionStepProperties>(
                EditableRegionStepProperties.MappedNames,
                EditableRegionStepProperties.From,
                TryCreateEditableRegionParameters),
            ["level-surface"] = Create<LevelSurfaceStepProperties>(
                LevelSurfaceStepProperties.MappedNames,
                LevelSurfaceStepProperties.From,
                TryCreateLevelSurfaceParameters),
            ["height-difference-edge"] = Create<HeightDifferenceEdgeStepProperties>(
                HeightDifferenceEdgeStepProperties.MappedNames,
                HeightDifferenceEdgeStepProperties.From,
                TryCreateHeightDifferenceEdgeParameters),
            ["two-point-line"] = Create<TwoPointLineStepProperties>(
                TwoPointLineStepProperties.MappedNames,
                TwoPointLineStepProperties.From,
                TryCreateTwoPointLineParameters),
            ["three-point-plane"] = Create<ThreePointPlaneStepProperties>(
                ThreePointPlaneStepProperties.MappedNames,
                ThreePointPlaneStepProperties.From,
                TryCreateThreePointPlaneParameters),
            ["datum-plane-raw-height-deviation"] = Create<DatumPlaneDeviationStepProperties>(
                DatumPlaneDeviationStepProperties.MappedNames,
                DatumPlaneDeviationStepProperties.From,
                TryCreateDatumPlaneDeviationParameters),
            ["three-d-line-fit"] = Create<LineFitStepProperties>(
                LineFitStepProperties.MappedNames,
                LineFitStepProperties.From,
                TryCreateLineFitParameters),
            ["line-intersection"] = Create<LineIntersectionStepProperties>(
                LineIntersectionStepProperties.MappedNames,
                LineIntersectionStepProperties.From,
                TryCreateLineIntersectionParameters),
            ["landmark-correspondence"] = Create<LandmarkCorrespondenceStepProperties>(
                LandmarkCorrespondenceStepProperties.MappedNames,
                LandmarkCorrespondenceStepProperties.From,
                TryCreateLandmarkCorrespondenceParameters),
            ["xyz-affine-solve"] = Create<XYZAffineSolveStepProperties>(
                XYZAffineSolveStepProperties.MappedNames,
                XYZAffineSolveStepProperties.From,
                TryCreateXyzAffineSolveParameters),
            ["xyz-affine-apply"] = Create<XYZAffineApplyStepProperties>(
                XYZAffineApplyStepProperties.MappedNames,
                XYZAffineApplyStepProperties.From,
                TryCreateXyzAffineApplyParameters),
            ["re-grid-height-map"] = Create<RegridHeightMapStepProperties>(
                RegridHeightMapStepProperties.MappedNames,
                RegridHeightMapStepProperties.From,
                TryCreateRegridHeightMapParameters),
            ["surface-match"] = Create<SurfaceMatchStepProperties>(
                SurfaceMatchStepProperties.MappedNames,
                SurfaceMatchStepProperties.From,
                TryCreateSurfaceMatchParameters),
            ["thickness"] = Create<ThicknessStepProperties>(
                ThicknessStepProperties.MappedNames,
                ThicknessStepProperties.From,
                TryCreateThicknessParameters),
            ["warpage"] = Create<WarpageStepProperties>(
                WarpageStepProperties.MappedNames,
                WarpageStepProperties.From,
                TryCreateWarpageParameters),
            ["plane-flatness"] = Create<PlaneFlatnessStepProperties>(
                PlaneFlatnessStepProperties.MappedNames,
                PlaneFlatnessStepProperties.From,
                TryCreatePlaneFlatnessParameters),
            ["point-pair-dimensions"] = Create<PointPairDimensionsStepProperties>(
                PointPairDimensionsStepProperties.MappedNames,
                PointPairDimensionsStepProperties.From,
                TryCreatePointPairParameters),
            ["gap-flush"] = Create<GapFlushStepProperties>(
                GapFlushStepProperties.MappedNames,
                GapFlushStepProperties.From,
                TryCreateGapFlushParameters),
            ["volume"] = Create<VolumeStepProperties>(
                VolumeStepProperties.MappedNames,
                VolumeStepProperties.From,
                TryCreateVolumeParameters),
            ["cross-section-dimensions"] = Create<CrossSectionDimensionsStepProperties>(
                CrossSectionDimensionsStepProperties.MappedNames,
                CrossSectionDimensionsStepProperties.From,
                TryCreateCrossSectionParameters),
            ["completeness-grid"] = Create<CompletenessGridStepProperties>(
                CompletenessGridStepProperties.MappedNames,
                CompletenessGridStepProperties.From,
                TryCreateCompletenessGridParameters)
        };

    private static Adapter Create<T>(
        IReadOnlySet<string> mappedNames,
        Func<ToolWorkbenchPipelineStepItem, T> createDraft,
        ParameterWriter writeParameters)
        where T : class =>
        new(
            typeof(T),
            mappedNames,
            step => createDraft(step),
            writeParameters);

    private static bool TryGetDraft<T>(
        object draft,
        out T properties,
        out IReadOnlyDictionary<string, string> values,
        out string message)
        where T : class
    {
        if (draft is T typed)
        {
            properties = typed;
            values = EmptyParameters;
            message = string.Empty;
            return true;
        }

        properties = null!;
        values = EmptyParameters;
        message = "This step has no typed parameter adapter.";
        return false;
    }

    private static bool TryCreateFilterParameters(
        object draft,
        out IReadOnlyDictionary<string, string> values,
        out string message)
    {
        if (!TryGetDraft<FilterStepProperties>(draft, out var filter, out values, out message)
            || !filter.TryValidate(out message))
        {
            return false;
        }

        values = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Method"] = filter.Method.ToString(),
            ["KernelSize"] = filter.KernelSize.ToString(CultureInfo.InvariantCulture),
            ["MissingValuePolicy"] = filter.MissingValuePolicy.ToString(),
            ["BoundaryPolicy"] = filter.BoundaryPolicy.ToString()
        };
        return true;
    }

    private static bool TryCreateRemoveOutlierParameters(
        object draft,
        out IReadOnlyDictionary<string, string> values,
        out string message)
    {
        if (!TryGetDraft<RemoveOutlierPixelsStepProperties>(
                draft,
                out var outlier,
                out values,
                out message)
            || !outlier.TryValidate(out message))
        {
            return false;
        }

        values = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Rule"] = outlier.Rule.ToString(),
            ["WindowSize"] = outlier.WindowSize.ToString(CultureInfo.InvariantCulture),
            ["MaximumAbsoluteDeviation"] = outlier.MaximumAbsoluteDeviation.ToString("G17", CultureInfo.InvariantCulture),
            ["MinimumValidNeighbors"] = outlier.MinimumValidNeighbors.ToString(CultureInfo.InvariantCulture),
            ["MissingValuePolicy"] = outlier.MissingValuePolicy.ToString(),
            ["BoundaryPolicy"] = outlier.BoundaryPolicy.ToString(),
            ["OutlierPolicy"] = outlier.OutlierPolicy.ToString()
        };
        return true;
    }

    private static bool TryCreateConnectedRegionParameters(
        object draft,
        out IReadOnlyDictionary<string, string> values,
        out string message)
    {
        if (!TryGetDraft<ConnectedRegionStepProperties>(
                draft,
                out var connectedRegion,
                out values,
                out message)
            || !connectedRegion.TryValidate(out message))
        {
            return false;
        }

        values = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Connectivity"] = connectedRegion.Connectivity.ToString(),
            ["OriginX"] = connectedRegion.OriginX.ToString("G17", CultureInfo.InvariantCulture),
            ["OriginY"] = connectedRegion.OriginY.ToString("G17", CultureInfo.InvariantCulture),
            ["ColumnPitch"] = connectedRegion.ColumnPitch.ToString("G17", CultureInfo.InvariantCulture),
            ["RowPitch"] = connectedRegion.RowPitch.ToString("G17", CultureInfo.InvariantCulture),
            ["AreaUnit"] = connectedRegion.AreaUnit
        };
        return true;
    }

    private static bool TryCreateDomainMaskParameters(
        object draft,
        out IReadOnlyDictionary<string, string> values,
        out string message)
    {
        if (!TryGetDraft<DomainMaskStepProperties>(draft, out _, out values, out message))
        {
            return false;
        }

        values = EmptyParameters;
        return true;
    }

    private static bool TryCreateEditableRegionParameters(
        object draft,
        out IReadOnlyDictionary<string, string> values,
        out string message)
    {
        if (!TryGetDraft<EditableRegionStepProperties>(
                draft,
                out var editableRegion,
                out values,
                out message)
            || !editableRegion.TryValidate(out message))
        {
            return false;
        }

        values = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["SelectedRegionIndex"] = editableRegion.SelectedRegionIndex.ToString(CultureInfo.InvariantCulture)
        };
        return true;
    }

    private static bool TryCreateLevelSurfaceParameters(
        object draft,
        out IReadOnlyDictionary<string, string> values,
        out string message)
    {
        if (!TryGetDraft<LevelSurfaceStepProperties>(
                draft,
                out var level,
                out values,
                out message)
            || !level.TryValidate(out message))
        {
            return false;
        }

        values = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["ReferenceFitPolicy"] = level.ReferenceFitPolicy.ToString(),
            ["LevelingPolicy"] = level.LevelingPolicy.ToString(),
            ["MissingValuePolicy"] = level.MissingValuePolicy.ToString(),
            ["GridPolicy"] = level.GridPolicy.ToString(),
            ["MinimumValidSampleCount"] = level.MinimumValidSampleCount.ToString(CultureInfo.InvariantCulture),
            ["MaximumReferenceRmsResidual"] = level.MaximumReferenceRmsResidual.ToString("G17", CultureInfo.InvariantCulture)
        };
        return true;
    }

    private static bool TryCreateHeightDifferenceEdgeParameters(
        object draft,
        out IReadOnlyDictionary<string, string> values,
        out string message)
    {
        if (!TryGetDraft<HeightDifferenceEdgeStepProperties>(
                draft,
                out var edge,
                out values,
                out message)
            || !edge.TryValidate(out message))
        {
            return false;
        }

        values = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["ComparisonAxis"] = edge.ComparisonAxis.ToString(),
            ["Polarity"] = edge.Polarity.ToString(),
            ["MinimumDelta"] = edge.MinimumDelta.ToString("G17", CultureInfo.InvariantCulture),
            ["CandidatePolicy"] = edge.CandidatePolicy.ToString(),
            ["PointPolicy"] = edge.PointPolicy.ToString(),
            ["MissingValuePolicy"] = edge.MissingValuePolicy.ToString(),
            ["BoundaryPolicy"] = edge.BoundaryPolicy.ToString()
        };
        return true;
    }

    private static bool TryCreateTwoPointLineParameters(
        object draft,
        out IReadOnlyDictionary<string, string> values,
        out string message)
    {
        if (!TryGetDraft<TwoPointLineStepProperties>(
                draft,
                out var twoPointLine,
                out values,
                out message)
            || !twoPointLine.TryValidate(out message))
        {
            return false;
        }

        values = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["OutputRole"] = twoPointLine.OutputRole,
            ["ConstructionPolicy"] = twoPointLine.ConstructionPolicy.ToString()
        };
        return true;
    }

    private static bool TryCreateThreePointPlaneParameters(
        object draft,
        out IReadOnlyDictionary<string, string> values,
        out string message)
    {
        if (!TryGetDraft<ThreePointPlaneStepProperties>(
                draft,
                out var threePointPlane,
                out values,
                out message)
            || !threePointPlane.TryValidate(out message))
        {
            return false;
        }

        values = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["OutputRole"] = threePointPlane.OutputRole,
            ["ConstructionPolicy"] = threePointPlane.ConstructionPolicy.ToString()
        };
        return true;
    }

    private static bool TryCreateDatumPlaneDeviationParameters(
        object draft,
        out IReadOnlyDictionary<string, string> values,
        out string message)
    {
        if (!TryGetDraft<DatumPlaneDeviationStepProperties>(
                draft,
                out var datum,
                out values,
                out message)
            || !datum.TryValidate(out message))
        {
            return false;
        }

        values = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["MaximumPeakToValleyRawHeight"] = datum.MaximumPeakToValleyRawHeight.ToString("G17", CultureInfo.InvariantCulture),
            ["OutputRole"] = datum.OutputRole,
            ["ResidualPolicy"] = datum.ResidualPolicy.ToString(),
            ["MinimumValidSampleCount"] = datum.MinimumValidSampleCount.ToString(CultureInfo.InvariantCulture),
            ["MinimumAbsoluteNormalY"] = datum.MinimumAbsoluteNormalY.ToString("G17", CultureInfo.InvariantCulture)
        };
        return true;
    }

    private static bool TryCreateLineFitParameters(
        object draft,
        out IReadOnlyDictionary<string, string> values,
        out string message)
    {
        if (!TryGetDraft<LineFitStepProperties>(
                draft,
                out var lineFit,
                out values,
                out message)
            || !lineFit.TryValidate(out message))
        {
            return false;
        }

        values = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["FitMethod"] = lineFit.FitMethod.ToString(),
            ["MaximumOrthogonalResidual"] = lineFit.MaximumOrthogonalResidual.ToString("G17", CultureInfo.InvariantCulture),
            ["MinimumInlierCount"] = lineFit.MinimumInlierCount.ToString(CultureInfo.InvariantCulture),
            ["MinimumInlierRatio"] = lineFit.MinimumInlierRatio.ToString("G17", CultureInfo.InvariantCulture),
            ["MinimumInlierScanlineSpan"] = lineFit.MinimumInlierScanlineSpan.ToString(CultureInfo.InvariantCulture),
            ["HypothesisPolicy"] = lineFit.HypothesisPolicy.ToString(),
            ["MaximumHypotheses"] = "256",
            ["RefinementPolicy"] = lineFit.RefinementPolicy.ToString(),
            ["DirectionPolicy"] = lineFit.DirectionPolicy.ToString(),
            ["EndpointPolicy"] = lineFit.EndpointPolicy.ToString()
        };
        return true;
    }

    private static bool TryCreateLineIntersectionParameters(
        object draft,
        out IReadOnlyDictionary<string, string> values,
        out string message)
    {
        if (!TryGetDraft<LineIntersectionStepProperties>(
                draft,
                out var intersection,
                out values,
                out message)
            || !intersection.TryValidate(out message))
        {
            return false;
        }

        values = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["MaximumClosestApproachDistance"] = intersection.MaximumClosestApproachDistance.ToString("G17", CultureInfo.InvariantCulture),
            ["MinimumAcuteAngleDegrees"] = intersection.MinimumAcuteAngleDegrees.ToString("G17", CultureInfo.InvariantCulture),
            ["MaximumSupportExtension"] = intersection.MaximumSupportExtension.ToString("G17", CultureInfo.InvariantCulture),
            ["OutputRole"] = intersection.OutputRole,
            ["ClosestApproachPolicy"] = intersection.ClosestApproachPolicy.ToString(),
            ["ParallelPolicy"] = intersection.ParallelPolicy.ToString(),
            ["SupportPolicy"] = intersection.SupportPolicy.ToString()
        };
        return true;
    }

    private static bool TryCreateLandmarkCorrespondenceParameters(
        object draft,
        out IReadOnlyDictionary<string, string> values,
        out string message)
    {
        if (!TryGetDraft<LandmarkCorrespondenceStepProperties>(
                draft,
                out var correspondence,
                out values,
                out message)
            || !correspondence.TryValidate(out message))
        {
            return false;
        }

        values = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["PairCountPolicy"] = correspondence.PairCountPolicy,
            ["SourceArtifactPolicy"] = correspondence.SourceArtifactPolicy,
            ["AffineIndependencePolicy"] = correspondence.AffineIndependencePolicy
        };
        return true;
    }

    private static bool TryCreateXyzAffineSolveParameters(
        object draft,
        out IReadOnlyDictionary<string, string> values,
        out string message)
    {
        if (!TryGetDraft<XYZAffineSolveStepProperties>(
                draft,
                out var affine,
                out values,
                out message)
            || !affine.TryValidate(out message))
        {
            return false;
        }

        values = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["SolvePolicy"] = affine.SolvePolicy,
            ["MaximumConditionEstimate"] = affine.MaximumConditionEstimate.ToString("G17", CultureInfo.InvariantCulture),
            ["ArithmeticResidualWarning"] = affine.ArithmeticResidualWarning.ToString("G17", CultureInfo.InvariantCulture)
        };
        return true;
    }

    private static bool TryCreateXyzAffineApplyParameters(
        object draft,
        out IReadOnlyDictionary<string, string> values,
        out string message)
    {
        if (!TryGetDraft<XYZAffineApplyStepProperties>(
                draft,
                out _,
                out values,
                out message))
        {
            return false;
        }

        values = EmptyParameters;
        return true;
    }

    private static bool TryCreateRegridHeightMapParameters(
        object draft,
        out IReadOnlyDictionary<string, string> values,
        out string message)
    {
        if (!TryGetDraft<RegridHeightMapStepProperties>(
                draft,
                out var regrid,
                out values,
                out message)
            || !regrid.TryCreateProfile(out var profile, out message)
            || profile is null)
        {
            message = string.IsNullOrWhiteSpace(message)
                ? "Reference-grid profile could not be created."
                : message;
            return false;
        }

        values = profile.ToRecipeParameters().ToDictionary(
            parameter => parameter.Name,
            parameter => parameter.Value,
            StringComparer.Ordinal);
        return true;
    }

    private static bool TryCreateSurfaceMatchParameters(
        object draft,
        out IReadOnlyDictionary<string, string> values,
        out string message)
    {
        if (!TryGetDraft<SurfaceMatchStepProperties>(
                draft,
                out var surfaceMatch,
                out values,
                out message)
            || !surfaceMatch.TryCreateIndependentContracts(
                out _,
                out _,
                out message))
        {
            return false;
        }

        values = surfaceMatch.ToRecipeParameters();
        return true;
    }

    private static bool TryCreateThicknessParameters(
        object draft,
        out IReadOnlyDictionary<string, string> values,
        out string message)
    {
        if (!TryGetDraft<ThicknessStepProperties>(
                draft,
                out var thickness,
                out values,
                out message)
            || !thickness.TryValidate(out message))
        {
            return false;
        }

        values = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["MinimumThickness"] = thickness.MinimumThickness.ToString("G17", CultureInfo.InvariantCulture),
            ["MaximumThickness"] = thickness.MaximumThickness.ToString("G17", CultureInfo.InvariantCulture),
            ["MinimumValidSampleCount"] = thickness.MinimumValidSampleCount.ToString(CultureInfo.InvariantCulture)
        };
        return true;
    }

    private static bool TryCreateWarpageParameters(
        object draft,
        out IReadOnlyDictionary<string, string> values,
        out string message)
    {
        if (!TryGetDraft<WarpageStepProperties>(
                draft,
                out var warpage,
                out values,
                out message)
            || !warpage.TryValidate(out message))
        {
            return false;
        }

        values = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["MaximumPeakToValley"] = warpage.MaximumPeakToValley.ToString("G17", CultureInfo.InvariantCulture),
            ["MaximumRms"] = warpage.MaximumRms.ToString("G17", CultureInfo.InvariantCulture),
            ["MinimumValidSampleCount"] = warpage.MinimumValidSampleCount.ToString(CultureInfo.InvariantCulture)
        };
        return true;
    }

    private static bool TryCreatePlaneFlatnessParameters(
        object draft,
        out IReadOnlyDictionary<string, string> values,
        out string message)
    {
        if (!TryGetDraft<PlaneFlatnessStepProperties>(
                draft,
                out var flatness,
                out values,
                out message)
            || !flatness.TryValidate(out message))
        {
            return false;
        }

        values = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["MaximumFlatness"] = flatness.MaximumFlatness.ToString("G17", CultureInfo.InvariantCulture),
            ["MinimumReferenceSampleCount"] = flatness.MinimumReferenceSampleCount.ToString(CultureInfo.InvariantCulture),
            ["MinimumMeasurementSampleCount"] = flatness.MinimumMeasurementSampleCount.ToString(CultureInfo.InvariantCulture)
        };
        return true;
    }

    private static bool TryCreatePointPairParameters(
        object draft,
        out IReadOnlyDictionary<string, string> values,
        out string message)
    {
        if (!TryGetDraft<PointPairDimensionsStepProperties>(
                draft,
                out var pointPair,
                out values,
                out message)
            || !pointPair.TryValidate(out message))
        {
            return false;
        }

        values = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["ExpectedDistance"] = pointPair.ExpectedDistance.ToString("G17", CultureInfo.InvariantCulture),
            ["DistanceTolerance"] = pointPair.DistanceTolerance.ToString("G17", CultureInfo.InvariantCulture),
            ["ExpectedPlanarWidth"] = pointPair.ExpectedPlanarWidth.ToString("G17", CultureInfo.InvariantCulture),
            ["PlanarWidthTolerance"] = pointPair.PlanarWidthTolerance.ToString("G17", CultureInfo.InvariantCulture),
            ["ExpectedElevationAngleDegrees"] = pointPair.ExpectedElevationAngleDegrees.ToString("G17", CultureInfo.InvariantCulture),
            ["ElevationAngleToleranceDegrees"] = pointPair.ElevationAngleToleranceDegrees.ToString("G17", CultureInfo.InvariantCulture)
        };
        return true;
    }

    private static bool TryCreateGapFlushParameters(
        object draft,
        out IReadOnlyDictionary<string, string> values,
        out string message)
    {
        if (!TryGetDraft<GapFlushStepProperties>(
                draft,
                out var gapFlush,
                out values,
                out message)
            || !gapFlush.TryValidate(out message))
        {
            return false;
        }

        values = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["ExpectedGap"] = gapFlush.ExpectedGap.ToString("G17", CultureInfo.InvariantCulture),
            ["GapTolerance"] = gapFlush.GapTolerance.ToString("G17", CultureInfo.InvariantCulture),
            ["ExpectedFlush"] = gapFlush.ExpectedFlush.ToString("G17", CultureInfo.InvariantCulture),
            ["FlushTolerance"] = gapFlush.FlushTolerance.ToString("G17", CultureInfo.InvariantCulture)
        };
        return true;
    }

    private static bool TryCreateVolumeParameters(
        object draft,
        out IReadOnlyDictionary<string, string> values,
        out string message)
    {
        if (!TryGetDraft<VolumeStepProperties>(
                draft,
                out var volume,
                out values,
                out message)
            || !volume.TryValidate(out message))
        {
            return false;
        }

        values = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["ExpectedNetVolume"] = volume.ExpectedNetVolume.ToString("G17", CultureInfo.InvariantCulture),
            ["VolumeTolerance"] = volume.VolumeTolerance.ToString("G17", CultureInfo.InvariantCulture)
        };
        return true;
    }

    private static bool TryCreateCrossSectionParameters(
        object draft,
        out IReadOnlyDictionary<string, string> values,
        out string message)
    {
        if (!TryGetDraft<CrossSectionDimensionsStepProperties>(
                draft,
                out var crossSection,
                out values,
                out message)
            || !crossSection.TryValidate(out message))
        {
            return false;
        }

        values = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["ExpectedWidth"] = crossSection.ExpectedWidth.ToString("G17", CultureInfo.InvariantCulture),
            ["WidthTolerance"] = crossSection.WidthTolerance.ToString("G17", CultureInfo.InvariantCulture),
            ["ExpectedHeightRange"] = crossSection.ExpectedHeightRange.ToString("G17", CultureInfo.InvariantCulture),
            ["HeightTolerance"] = crossSection.HeightTolerance.ToString("G17", CultureInfo.InvariantCulture)
        };
        return true;
    }

    private static bool TryCreateCompletenessGridParameters(
        object draft,
        out IReadOnlyDictionary<string, string> values,
        out string message)
    {
        if (!TryGetDraft<CompletenessGridStepProperties>(
                draft,
                out var completeness,
                out values,
                out message)
            || !completeness.TryCreateContracts(
                out var completenessProfile,
                out var completenessPolicy,
                out message)
            || completenessProfile is null
            || completenessPolicy is null)
        {
            return false;
        }

        values = completenessProfile.ToRecipeParameters()
            .Concat(completenessPolicy.ToRecipeParameters())
            .ToDictionary(
                parameter => parameter.Name,
                parameter => parameter.Value,
                StringComparer.Ordinal);
        return true;
    }
}
