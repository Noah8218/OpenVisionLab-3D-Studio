using System.ComponentModel;
using System.Globalization;

namespace OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;

internal sealed class ToolWorkbenchStepPropertySession : INotifyPropertyChanged
{
    public const string AdapterStatusPropertyName = "AdapterStatus";

    private object? draft;
    private string? draftStepId;
    private bool hasPendingChanges;
    private string status = "Select a typed tool to teach parameters. Apply XYZ Affine has a fixed no-parameter A2 contract.";

    public event PropertyChangedEventHandler? PropertyChanged;

    public object? Draft
    {
        get => draft;
        private set
        {
            if (ReferenceEquals(draft, value))
            {
                return;
            }

            draft = value;
            OnPropertyChanged(nameof(Draft));
        }
    }

    public bool IsSupported => Draft is not null;

    public bool HasPendingChanges => hasPendingChanges;

    public string Status => status;

    public string GetAdapterStatus(ToolWorkbenchPipelineStepItem? step) => step switch
    {
        null => "No step selected",
        { ToolId: "filter" } => FormatAdapterStatus(step, FilterStepProperties.MappedNames),
        { ToolId: "remove-outlier-pixels" } => FormatAdapterStatus(step, RemoveOutlierPixelsStepProperties.MappedNames),
        { ToolId: "height-difference-edge" } => FormatAdapterStatus(step, HeightDifferenceEdgeStepProperties.MappedNames),
        { ToolId: "two-point-line" } => FormatAdapterStatus(step, TwoPointLineStepProperties.MappedNames),
        { ToolId: "three-point-plane" } => FormatAdapterStatus(step, ThreePointPlaneStepProperties.MappedNames),
        { ToolId: "datum-plane-raw-height-deviation" } => FormatAdapterStatus(step, DatumPlaneDeviationStepProperties.MappedNames),
        { ToolId: "three-d-line-fit" } => FormatAdapterStatus(step, LineFitStepProperties.MappedNames),
        { ToolId: "line-intersection" } => FormatAdapterStatus(step, LineIntersectionStepProperties.MappedNames),
        { ToolId: "landmark-correspondence" } => FormatAdapterStatus(step, LandmarkCorrespondenceStepProperties.MappedNames),
        { ToolId: "xyz-affine-solve" } => FormatAdapterStatus(step, XYZAffineSolveStepProperties.MappedNames),
        { ToolId: "xyz-affine-apply" } => FormatAdapterStatus(step, XYZAffineApplyStepProperties.MappedNames),
        { ToolId: "re-grid-height-map" } => FormatAdapterStatus(step, RegridHeightMapStepProperties.MappedNames),
        { ToolId: "thickness" } => FormatAdapterStatus(step, ThicknessStepProperties.MappedNames),
        { ToolId: "warpage" } => FormatAdapterStatus(step, WarpageStepProperties.MappedNames),
        { ToolId: "plane-flatness" } => FormatAdapterStatus(step, PlaneFlatnessStepProperties.MappedNames),
        { ToolId: "point-pair-dimensions" } => FormatAdapterStatus(step, PointPairDimensionsStepProperties.MappedNames),
        { ToolId: "gap-flush" } => FormatAdapterStatus(step, GapFlushStepProperties.MappedNames),
        { ToolId: "volume" } => FormatAdapterStatus(step, VolumeStepProperties.MappedNames),
        { ToolId: "cross-section-dimensions" } => FormatAdapterStatus(step, CrossSectionDimensionsStepProperties.MappedNames),
        _ => "Partially supported - parameters are preserved read-only"
    };

    public void Refresh(ToolWorkbenchPipelineStepItem? step, string? newStatus = null)
    {
        draftStepId = step?.Id;
        Draft = step?.ToolId switch
        {
            "filter" => FilterStepProperties.From(step),
            "remove-outlier-pixels" => RemoveOutlierPixelsStepProperties.From(step),
            "height-difference-edge" => HeightDifferenceEdgeStepProperties.From(step),
            "two-point-line" => TwoPointLineStepProperties.From(step),
            "three-point-plane" => ThreePointPlaneStepProperties.From(step),
            "datum-plane-raw-height-deviation" => DatumPlaneDeviationStepProperties.From(step),
            "three-d-line-fit" => LineFitStepProperties.From(step),
            "line-intersection" => LineIntersectionStepProperties.From(step),
            "landmark-correspondence" => LandmarkCorrespondenceStepProperties.From(step),
            "xyz-affine-solve" => XYZAffineSolveStepProperties.From(step),
            "xyz-affine-apply" => XYZAffineApplyStepProperties.From(step),
            "re-grid-height-map" => RegridHeightMapStepProperties.From(step),
            "thickness" => ThicknessStepProperties.From(step),
            "warpage" => WarpageStepProperties.From(step),
            "plane-flatness" => PlaneFlatnessStepProperties.From(step),
            "point-pair-dimensions" => PointPairDimensionsStepProperties.From(step),
            "gap-flush" => GapFlushStepProperties.From(step),
            "volume" => VolumeStepProperties.From(step),
            "cross-section-dimensions" => CrossSectionDimensionsStepProperties.From(step),
            _ => null
        };

        SetState(
            false,
            newStatus ?? (Draft is null
                ? "This step is preserved, but no typed parameter editor is available yet."
                : "Parameters match the committed recipe. Editing does not run Preview or Publish."));
        OnPropertyChanged(nameof(IsSupported));
        OnPropertyChanged(AdapterStatusPropertyName);
    }

    public void MarkDirty() =>
        SetState(true, "Unapplied parameter changes. Apply or discard before changing recipe sessions.");

    public void SetStatus(string message)
    {
        status = message;
        OnPropertyChanged(nameof(Status));
    }

    internal void ResetDraftForSmoke(object value)
    {
        Draft = null;
        Draft = value;
    }

    public bool TryCreateParameterValues(
        ToolWorkbenchPipelineStepItem step,
        out IReadOnlyDictionary<string, string> values,
        out string message)
    {
        values = new Dictionary<string, string>(StringComparer.Ordinal);
        message = string.Empty;
        if (!string.Equals(step.Id, draftStepId, StringComparison.Ordinal))
        {
            message = "The selected step changed. Discard the draft and select the step again.";
            SetStatus(message);
            return false;
        }

        switch (Draft)
        {
            case FilterStepProperties filter:
                if (!filter.TryValidate(out message))
                {
                    SetStatus(message);
                    return false;
                }

                values = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["Method"] = filter.Method.ToString(),
                    ["KernelSize"] = filter.KernelSize.ToString(CultureInfo.InvariantCulture),
                    ["MissingValuePolicy"] = filter.MissingValuePolicy.ToString(),
                    ["BoundaryPolicy"] = filter.BoundaryPolicy.ToString()
                };
                break;
            case RemoveOutlierPixelsStepProperties outlier:
                if (!outlier.TryValidate(out message))
                {
                    SetStatus(message);
                    return false;
                }

                values = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["Rule"] = outlier.Rule.ToString(),
                    ["WindowSize"] =
                        outlier.WindowSize.ToString(CultureInfo.InvariantCulture),
                    ["MaximumAbsoluteDeviation"] =
                        outlier.MaximumAbsoluteDeviation.ToString(
                            "G17",
                            CultureInfo.InvariantCulture),
                    ["MinimumValidNeighbors"] =
                        outlier.MinimumValidNeighbors.ToString(
                            CultureInfo.InvariantCulture),
                    ["MissingValuePolicy"] = outlier.MissingValuePolicy.ToString(),
                    ["BoundaryPolicy"] = outlier.BoundaryPolicy.ToString(),
                    ["OutlierPolicy"] = outlier.OutlierPolicy.ToString()
                };
                break;
            case HeightDifferenceEdgeStepProperties edge:
                if (!edge.TryValidate(out message))
                {
                    SetStatus(message);
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
                break;
            case TwoPointLineStepProperties twoPointLine:
                if (!twoPointLine.TryValidate(out message))
                {
                    SetStatus(message);
                    return false;
                }

                values = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["OutputRole"] = twoPointLine.OutputRole,
                    ["ConstructionPolicy"] = twoPointLine.ConstructionPolicy.ToString()
                };
                break;
            case ThreePointPlaneStepProperties threePointPlane:
                if (!threePointPlane.TryValidate(out message))
                {
                    SetStatus(message);
                    return false;
                }

                values = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["OutputRole"] = threePointPlane.OutputRole,
                    ["ConstructionPolicy"] = threePointPlane.ConstructionPolicy.ToString()
                };
                break;
            case DatumPlaneDeviationStepProperties datum:
                if (!datum.TryValidate(out message))
                {
                    SetStatus(message);
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
                break;
            case LineFitStepProperties lineFit:
                if (!lineFit.TryValidate(out message))
                {
                    SetStatus(message);
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
                break;
            case LineIntersectionStepProperties intersection:
                if (!intersection.TryValidate(out message))
                {
                    SetStatus(message);
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
                break;
            case LandmarkCorrespondenceStepProperties correspondence:
                if (!correspondence.TryValidate(out message))
                {
                    SetStatus(message);
                    return false;
                }

                values = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["PairCountPolicy"] = correspondence.PairCountPolicy,
                    ["SourceArtifactPolicy"] = correspondence.SourceArtifactPolicy,
                    ["AffineIndependencePolicy"] = correspondence.AffineIndependencePolicy
                };
                break;
            case XYZAffineSolveStepProperties affine:
                if (!affine.TryValidate(out message))
                {
                    SetStatus(message);
                    return false;
                }

                values = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["SolvePolicy"] = affine.SolvePolicy,
                    ["MaximumConditionEstimate"] = affine.MaximumConditionEstimate.ToString("G17", CultureInfo.InvariantCulture),
                    ["ArithmeticResidualWarning"] = affine.ArithmeticResidualWarning.ToString("G17", CultureInfo.InvariantCulture)
                };
                break;
            case XYZAffineApplyStepProperties:
                values = new Dictionary<string, string>(StringComparer.Ordinal);
                break;
            case RegridHeightMapStepProperties regrid:
                if (!regrid.TryCreateProfile(out var profile, out message) || profile is null)
                {
                    SetStatus(string.IsNullOrWhiteSpace(message) ? "Reference-grid profile could not be created." : message);
                    return false;
                }

                values = profile.ToRecipeParameters().ToDictionary(parameter => parameter.Name, parameter => parameter.Value, StringComparer.Ordinal);
                break;
            case ThicknessStepProperties thickness:
                if (!thickness.TryValidate(out message))
                {
                    SetStatus(message);
                    return false;
                }
                values = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["MinimumThickness"] = thickness.MinimumThickness.ToString("G17", CultureInfo.InvariantCulture),
                    ["MaximumThickness"] = thickness.MaximumThickness.ToString("G17", CultureInfo.InvariantCulture),
                    ["MinimumValidSampleCount"] = thickness.MinimumValidSampleCount.ToString(CultureInfo.InvariantCulture)
                };
                break;
            case WarpageStepProperties warpage:
                if (!warpage.TryValidate(out message))
                {
                    SetStatus(message);
                    return false;
                }
                values = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["MaximumPeakToValley"] = warpage.MaximumPeakToValley.ToString("G17", CultureInfo.InvariantCulture),
                    ["MaximumRms"] = warpage.MaximumRms.ToString("G17", CultureInfo.InvariantCulture),
                    ["MinimumValidSampleCount"] = warpage.MinimumValidSampleCount.ToString(CultureInfo.InvariantCulture)
                };
                break;
            case PlaneFlatnessStepProperties flatness:
                if (!flatness.TryValidate(out message))
                {
                    SetStatus(message);
                    return false;
                }
                values = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["MaximumFlatness"] = flatness.MaximumFlatness.ToString("G17", CultureInfo.InvariantCulture),
                    ["MinimumReferenceSampleCount"] = flatness.MinimumReferenceSampleCount.ToString(CultureInfo.InvariantCulture),
                    ["MinimumMeasurementSampleCount"] = flatness.MinimumMeasurementSampleCount.ToString(CultureInfo.InvariantCulture)
                };
                break;
            case PointPairDimensionsStepProperties pointPair:
                if (!pointPair.TryValidate(out message))
                {
                    SetStatus(message);
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
                break;
            case GapFlushStepProperties gapFlush:
                if (!gapFlush.TryValidate(out message))
                {
                    SetStatus(message);
                    return false;
                }
                values = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["ExpectedGap"] = gapFlush.ExpectedGap.ToString("G17", CultureInfo.InvariantCulture),
                    ["GapTolerance"] = gapFlush.GapTolerance.ToString("G17", CultureInfo.InvariantCulture),
                    ["ExpectedFlush"] = gapFlush.ExpectedFlush.ToString("G17", CultureInfo.InvariantCulture),
                    ["FlushTolerance"] = gapFlush.FlushTolerance.ToString("G17", CultureInfo.InvariantCulture)
                };
                break;
            case VolumeStepProperties volume:
                if (!volume.TryValidate(out message))
                {
                    SetStatus(message);
                    return false;
                }
                values = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["ExpectedNetVolume"] = volume.ExpectedNetVolume.ToString("G17", CultureInfo.InvariantCulture),
                    ["VolumeTolerance"] = volume.VolumeTolerance.ToString("G17", CultureInfo.InvariantCulture)
                };
                break;
            case CrossSectionDimensionsStepProperties crossSection:
                if (!crossSection.TryValidate(out message))
                {
                    SetStatus(message);
                    return false;
                }
                values = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["ExpectedWidth"] = crossSection.ExpectedWidth.ToString("G17", CultureInfo.InvariantCulture),
                    ["WidthTolerance"] = crossSection.WidthTolerance.ToString("G17", CultureInfo.InvariantCulture),
                    ["ExpectedHeightRange"] = crossSection.ExpectedHeightRange.ToString("G17", CultureInfo.InvariantCulture),
                    ["HeightTolerance"] = crossSection.HeightTolerance.ToString("G17", CultureInfo.InvariantCulture)
                };
                break;
            default:
                message = "This step has no typed parameter adapter.";
                SetStatus(message);
                return false;
        }
        return true;
    }

    public static bool IsSupportedTool(ToolWorkbenchPipelineStepItem step) =>
        step.ToolId is "filter" or "remove-outlier-pixels" or "height-difference-edge" or "two-point-line" or "three-point-plane" or "datum-plane-raw-height-deviation" or "three-d-line-fit" or "line-intersection" or "landmark-correspondence" or "xyz-affine-solve" or "xyz-affine-apply" or "re-grid-height-map" or "thickness" or "warpage" or "plane-flatness" or "point-pair-dimensions" or "gap-flush" or "volume" or "cross-section-dimensions";

    internal static string GetParameter(ToolWorkbenchPipelineStepItem step, string name) =>
        step.Parameters.FirstOrDefault(parameter =>
            string.Equals(parameter.Name, name, StringComparison.Ordinal))?.Value ?? string.Empty;

    internal static string GetUnmappedParameters(
        ToolWorkbenchPipelineStepItem step,
        IReadOnlySet<string> mappedNames)
    {
        var values = step.Parameters
            .Where(parameter => !mappedNames.Contains(parameter.Name))
            .Select(parameter => $"{parameter.Name}={parameter.Value}")
            .ToArray();
        return values.Length == 0 ? "(none)" : string.Join("; ", values);
    }

    private static string FormatAdapterStatus(
        ToolWorkbenchPipelineStepItem step,
        IReadOnlySet<string> mappedNames)
    {
        var unmappedCount = step.Parameters.Count(parameter => !mappedNames.Contains(parameter.Name));
        return unmappedCount == 0
            ? "Typed adapter ready"
            : $"Typed adapter ready | {unmappedCount} unmapped preserved";
    }

    private void SetState(bool pending, string message)
    {
        hasPendingChanges = pending;
        status = message;
        OnPropertyChanged(nameof(HasPendingChanges));
        OnPropertyChanged(nameof(Status));
    }

    private void OnPropertyChanged(string propertyName) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
