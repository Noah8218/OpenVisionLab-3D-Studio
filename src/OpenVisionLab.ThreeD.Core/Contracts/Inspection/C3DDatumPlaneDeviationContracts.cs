using System.Security.Cryptography;
using System.Text;

namespace OpenVisionLab.ThreeD.Core;

/// <summary>
/// One deterministic display sample from a datum-plane raw-height result.
/// It is a read-only overlay sample, not a derived source height field.
/// </summary>
public sealed record C3DDatumPlaneDeviationOverlaySample(int Row, int Column, double RawHeight, double Residual);

/// <summary>
/// Immutable local raw-height result for one published manual C3D datum plane
/// and one recipe-owned measurement rectangle. It does not establish physical
/// flatness, Warpage, calibration, or a transformed/re-gridded source.
/// </summary>
public sealed class C3DDatumPlaneDeviationFeature
{
    public const string ContractVersion = "1.0";
    public const string ResidualPolicyName = "RawHeightMinusDatumPlanePredictedRawHeight";
    public const string DisplaySamplingPolicyName = "DeterministicGridStrideMax25000";
    public const int MaximumOverlaySampleCount = 25000;

    private C3DDatumPlaneDeviationFeature(
        string outputEntityId,
        C3DThreePointPlaneFeature plane,
        ToolRecipeSelection measurementSelection,
        double maximumPeakToValleyRawHeight,
        int minimumValidSampleCount,
        double minimumAbsoluteNormalY,
        double minimumRawHeightResidual,
        double maximumRawHeightResidual,
        double peakToValleyRawHeight,
        double rmsRawHeightResidual,
        int validSampleCount,
        int missingSampleCount,
        int minimumResidualRow,
        int minimumResidualColumn,
        int maximumResidualRow,
        int maximumResidualColumn,
        ResultStatus status,
        string outputRole,
        IReadOnlyList<C3DDatumPlaneDeviationOverlaySample> overlaySamples,
        string provenance,
        string contentSha256)
    {
        OutputEntityId = outputEntityId;
        PlaneFeatureEntityId = plane.OutputEntityId;
        PlaneFeatureContentSha256 = plane.ContentSha256;
        RootSourceEntityId = plane.RootSourceEntityId;
        RootSourceSha256 = plane.RootSourceSha256;
        Unit = plane.Unit;
        FrameId = plane.FrameId;
        MeasurementSelectionId = measurementSelection.Id;
        MeasurementSelectionContentSha256 = CalculateMeasurementSelectionContentSha256(measurementSelection);
        MeasurementRectangle = measurementSelection.GridRectangle!;
        MaximumPeakToValleyRawHeight = maximumPeakToValleyRawHeight;
        MinimumValidSampleCount = minimumValidSampleCount;
        MinimumAbsoluteNormalY = minimumAbsoluteNormalY;
        MinimumRawHeightResidual = minimumRawHeightResidual;
        MaximumRawHeightResidual = maximumRawHeightResidual;
        PeakToValleyRawHeight = peakToValleyRawHeight;
        RmsRawHeightResidual = rmsRawHeightResidual;
        ValidSampleCount = validSampleCount;
        MissingSampleCount = missingSampleCount;
        MinimumResidualRow = minimumResidualRow;
        MinimumResidualColumn = minimumResidualColumn;
        MaximumResidualRow = maximumResidualRow;
        MaximumResidualColumn = maximumResidualColumn;
        Status = status;
        OutputRole = outputRole;
        OverlaySamples = overlaySamples;
        Provenance = provenance;
        ContentSha256 = contentSha256;
    }

    public string OutputEntityId { get; }
    public string PlaneFeatureEntityId { get; }
    public string PlaneFeatureContentSha256 { get; }
    public string RootSourceEntityId { get; }
    public string RootSourceSha256 { get; }
    public string Unit { get; }
    public string FrameId { get; }
    public string CoordinateConvention => "column-rawHeight-row";
    public string ResidualUnit => "raw-height";
    public string ResidualPolicy => ResidualPolicyName;
    public string DisplaySamplingPolicy => DisplaySamplingPolicyName;
    public string MeasurementSelectionId { get; }
    public string MeasurementSelectionContentSha256 { get; }
    public ToolRecipeGridRectangle MeasurementRectangle { get; }
    public double MaximumPeakToValleyRawHeight { get; }
    public int MinimumValidSampleCount { get; }
    public double MinimumAbsoluteNormalY { get; }
    public double MinimumRawHeightResidual { get; }
    public double MaximumRawHeightResidual { get; }
    public double PeakToValleyRawHeight { get; }
    public double RmsRawHeightResidual { get; }
    public int ValidSampleCount { get; }
    public int MissingSampleCount { get; }
    public int MinimumResidualRow { get; }
    public int MinimumResidualColumn { get; }
    public int MaximumResidualRow { get; }
    public int MaximumResidualColumn { get; }
    public ResultStatus Status { get; }
    public string OutputRole { get; }
    public IReadOnlyList<C3DDatumPlaneDeviationOverlaySample> OverlaySamples { get; }
    public string Provenance { get; }
    public string ContentSha256 { get; }

    public static C3DDatumPlaneDeviationFeature Create(
        string outputEntityId,
        C3DThreePointPlaneFeature plane,
        ToolRecipeSelection measurementSelection,
        double maximumPeakToValleyRawHeight,
        int minimumValidSampleCount,
        double minimumAbsoluteNormalY,
        double minimumRawHeightResidual,
        double maximumRawHeightResidual,
        double peakToValleyRawHeight,
        double rmsRawHeightResidual,
        int validSampleCount,
        int missingSampleCount,
        int minimumResidualRow,
        int minimumResidualColumn,
        int maximumResidualRow,
        int maximumResidualColumn,
        ResultStatus status,
        string outputRole,
        IReadOnlyList<C3DDatumPlaneDeviationOverlaySample> overlaySamples,
        string provenance)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputEntityId);
        ArgumentNullException.ThrowIfNull(plane);
        ArgumentNullException.ThrowIfNull(measurementSelection);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputRole);
        ArgumentException.ThrowIfNullOrWhiteSpace(provenance);
        if (measurementSelection.Kind != ToolRecipeSelectionKinds.GridRectangle
            || measurementSelection.GridRectangle is null)
        {
            throw new ArgumentException("Datum-plane deviation requires one GridRectangle selection.", nameof(measurementSelection));
        }

        var rectangle = measurementSelection.GridRectangle;
        if (rectangle.Row < 0 || rectangle.Column < 0 || rectangle.RowCount <= 0 || rectangle.ColumnCount <= 0
            || rectangle.Row > measurementSelection.SourceBinding.GridHeight - rectangle.RowCount
            || rectangle.Column > measurementSelection.SourceBinding.GridWidth - rectangle.ColumnCount)
        {
            throw new ArgumentException("Datum-plane measurement rectangle is outside its recorded C3D grid.", nameof(measurementSelection));
        }

        if (!string.Equals(measurementSelection.RootSourceId, plane.RootSourceEntityId, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(measurementSelection.FrameId, plane.FrameId, StringComparison.Ordinal)
            || !string.Equals(measurementSelection.SourceBinding.ContentSha256, plane.RootSourceSha256, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Datum-plane measurement selection source identity does not match the plane feature.", nameof(measurementSelection));
        }

        var values = new[]
        {
            maximumPeakToValleyRawHeight, minimumAbsoluteNormalY, minimumRawHeightResidual,
            maximumRawHeightResidual, peakToValleyRawHeight, rmsRawHeightResidual
        };
        if (values.Any(value => !double.IsFinite(value))
            || maximumPeakToValleyRawHeight <= 0d
            || minimumAbsoluteNormalY <= 0d || minimumAbsoluteNormalY > 1d
            || peakToValleyRawHeight < 0d || rmsRawHeightResidual < 0d
            || validSampleCount < minimumValidSampleCount || minimumValidSampleCount < 3 || missingSampleCount < 0
            || status is not (ResultStatus.Pass or ResultStatus.Fail))
        {
            throw new ArgumentException("Datum-plane deviation result values are invalid.");
        }

        var samples = overlaySamples?.ToArray() ?? throw new ArgumentNullException(nameof(overlaySamples));
        if (samples.Length == 0 || samples.Length > MaximumOverlaySampleCount
            || samples.Any(sample => !double.IsFinite(sample.RawHeight) || !double.IsFinite(sample.Residual)
                || sample.Row < rectangle.Row || sample.Row >= rectangle.Row + rectangle.RowCount
                || sample.Column < rectangle.Column || sample.Column >= rectangle.Column + rectangle.ColumnCount))
        {
            throw new ArgumentException("Datum-plane display samples are invalid.", nameof(overlaySamples));
        }

        var selectionHash = CalculateMeasurementSelectionContentSha256(measurementSelection);
        var hash = CalculateContentSha256(
            outputEntityId, plane, selectionHash, maximumPeakToValleyRawHeight, minimumValidSampleCount,
            minimumAbsoluteNormalY, minimumRawHeightResidual, maximumRawHeightResidual, peakToValleyRawHeight,
            rmsRawHeightResidual, validSampleCount, missingSampleCount, minimumResidualRow, minimumResidualColumn,
            maximumResidualRow, maximumResidualColumn, status, outputRole);
        return new C3DDatumPlaneDeviationFeature(
            outputEntityId, plane, measurementSelection, maximumPeakToValleyRawHeight, minimumValidSampleCount,
            minimumAbsoluteNormalY, minimumRawHeightResidual, maximumRawHeightResidual, peakToValleyRawHeight,
            rmsRawHeightResidual, validSampleCount, missingSampleCount, minimumResidualRow, minimumResidualColumn,
            maximumResidualRow, maximumResidualColumn, status, outputRole, samples, provenance, hash);
    }

    public static string CalculateMeasurementSelectionContentSha256(ToolRecipeSelection selection)
    {
        ArgumentNullException.ThrowIfNull(selection);
        var rectangle = selection.GridRectangle ?? throw new ArgumentException("Grid rectangle is required.", nameof(selection));
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        writer.Write("OpenVisionLab.C3DDatumPlaneMeasurementSelection"); writer.Write(ContractVersion);
        writer.Write(selection.Id); writer.Write(selection.Kind); writer.Write(selection.RootSourceId); writer.Write(selection.FrameId);
        writer.Write(selection.SourceBinding.Format); writer.Write(selection.SourceBinding.ContentSha256.ToUpperInvariant());
        writer.Write(selection.SourceBinding.GridWidth); writer.Write(selection.SourceBinding.GridHeight);
        writer.Write(rectangle.Row); writer.Write(rectangle.Column); writer.Write(rectangle.RowCount); writer.Write(rectangle.ColumnCount);
        return Convert.ToHexString(SHA256.HashData(stream.ToArray()));
    }

    private static string CalculateContentSha256(
        string outputEntityId,
        C3DThreePointPlaneFeature plane,
        string measurementSelectionHash,
        double maximumPeakToValleyRawHeight,
        int minimumValidSampleCount,
        double minimumAbsoluteNormalY,
        double minimumRawHeightResidual,
        double maximumRawHeightResidual,
        double peakToValleyRawHeight,
        double rmsRawHeightResidual,
        int validSampleCount,
        int missingSampleCount,
        int minimumResidualRow,
        int minimumResidualColumn,
        int maximumResidualRow,
        int maximumResidualColumn,
        ResultStatus status,
        string outputRole)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        writer.Write("OpenVisionLab.C3DDatumPlaneDeviationFeature"); writer.Write(ContractVersion);
        writer.Write(outputEntityId); writer.Write(plane.OutputEntityId); writer.Write(plane.ContentSha256.ToUpperInvariant());
        writer.Write(plane.RootSourceEntityId); writer.Write(plane.RootSourceSha256.ToUpperInvariant()); writer.Write(plane.Unit); writer.Write(plane.FrameId);
        writer.Write("column-rawHeight-row"); writer.Write("RawHeightMinusDatumPlanePredictedRawHeight");
        writer.Write(measurementSelectionHash.ToUpperInvariant()); writer.Write(maximumPeakToValleyRawHeight);
        writer.Write(minimumValidSampleCount); writer.Write(minimumAbsoluteNormalY);
        writer.Write(minimumRawHeightResidual); writer.Write(maximumRawHeightResidual); writer.Write(peakToValleyRawHeight); writer.Write(rmsRawHeightResidual);
        writer.Write(validSampleCount); writer.Write(missingSampleCount); writer.Write(minimumResidualRow); writer.Write(minimumResidualColumn);
        writer.Write(maximumResidualRow); writer.Write(maximumResidualColumn); writer.Write(status.ToString()); writer.Write(outputRole);
        return Convert.ToHexString(SHA256.HashData(stream.ToArray()));
    }
}
