using System.Security.Cryptography;
using System.Text;

namespace OpenVisionLab.ThreeD.Core;

public enum C3DHeightDifferenceComparisonAxis
{
    AcrossColumns,
    AcrossRows
}

public enum C3DHeightDifferencePolarity
{
    Rising,
    Falling,
    Absolute
}

public sealed record C3DHeightDifferenceEdgePoint(
    int ScanlineIndex,
    int FirstRow,
    int FirstColumn,
    double FirstHeight,
    int SecondRow,
    int SecondColumn,
    double SecondHeight,
    double SignedDelta,
    double Magnitude,
    double X,
    double Y,
    double Z);

public sealed record C3DHeightDifferenceEdgeDiagnostics(
    int ScanlineCount,
    long EligiblePairCount,
    long SkippedMissingPairCount,
    int AcceptedScanlineCount,
    int NoCandidateScanlineCount,
    double AcceptedMagnitudeMinimum,
    double AcceptedMagnitudeMaximum,
    double AcceptedMagnitudeMean);

/// <summary>
/// Immutable, ordered feature-extraction output in the numeric C3D frame:
/// X=column, Y=raw height, Z=row. The hash is repeatability evidence only.
/// </summary>
public sealed class C3DHeightDifferenceEdgePointSet
{
    public const string ContractVersion = "1.0";
    private readonly C3DHeightDifferenceEdgePoint[] points;

    private C3DHeightDifferenceEdgePointSet(
        string outputEntityId,
        string rootSourceEntityId,
        string rootSourceSha256,
        string inputEntityId,
        string inputContentSha256,
        string selectionId,
        ToolRecipeGridRectangle selection,
        string unit,
        string frameId,
        C3DHeightDifferenceComparisonAxis comparisonAxis,
        C3DHeightDifferencePolarity polarity,
        double minimumDelta,
        C3DHeightDifferenceEdgePoint[] points,
        C3DHeightDifferenceEdgeDiagnostics diagnostics,
        string provenance,
        string contentSha256)
    {
        OutputEntityId = outputEntityId;
        RootSourceEntityId = rootSourceEntityId;
        RootSourceSha256 = rootSourceSha256;
        InputEntityId = inputEntityId;
        InputContentSha256 = inputContentSha256;
        SelectionId = selectionId;
        Selection = selection;
        Unit = unit;
        FrameId = frameId;
        ComparisonAxis = comparisonAxis;
        Polarity = polarity;
        MinimumDelta = minimumDelta;
        this.points = points;
        Diagnostics = diagnostics;
        Provenance = provenance;
        ContentSha256 = contentSha256;
    }

    public string OutputEntityId { get; }
    public string RootSourceEntityId { get; }
    public string RootSourceSha256 { get; }
    public string InputEntityId { get; }
    public string InputContentSha256 { get; }
    public string SelectionId { get; }
    public ToolRecipeGridRectangle Selection { get; }
    public string Unit { get; }
    public string FrameId { get; }
    public string ScalarMeaning => "raw-height";
    public C3DHeightDifferenceComparisonAxis ComparisonAxis { get; }
    public C3DHeightDifferencePolarity Polarity { get; }
    public double MinimumDelta { get; }
    public IReadOnlyList<C3DHeightDifferenceEdgePoint> Points => points;
    public C3DHeightDifferenceEdgeDiagnostics Diagnostics { get; }
    public string Provenance { get; }
    public string ContentSha256 { get; }

    public static C3DHeightDifferenceEdgePointSet Create(
        string outputEntityId,
        string rootSourceEntityId,
        string rootSourceSha256,
        string inputEntityId,
        string inputContentSha256,
        string selectionId,
        ToolRecipeGridRectangle selection,
        string unit,
        string frameId,
        C3DHeightDifferenceComparisonAxis comparisonAxis,
        C3DHeightDifferencePolarity polarity,
        double minimumDelta,
        IReadOnlyList<C3DHeightDifferenceEdgePoint> points,
        C3DHeightDifferenceEdgeDiagnostics diagnostics,
        string provenance)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputEntityId);
        ArgumentException.ThrowIfNullOrWhiteSpace(rootSourceEntityId);
        ArgumentException.ThrowIfNullOrWhiteSpace(rootSourceSha256);
        ArgumentException.ThrowIfNullOrWhiteSpace(inputEntityId);
        ArgumentException.ThrowIfNullOrWhiteSpace(inputContentSha256);
        ArgumentException.ThrowIfNullOrWhiteSpace(selectionId);
        ArgumentNullException.ThrowIfNull(selection);
        ArgumentNullException.ThrowIfNull(points);
        ArgumentNullException.ThrowIfNull(diagnostics);
        ArgumentException.ThrowIfNullOrWhiteSpace(provenance);

        var copy = points.ToArray();
        var hash = CalculateContentSha256(
            outputEntityId,
            rootSourceEntityId,
            rootSourceSha256,
            inputEntityId,
            inputContentSha256,
            selectionId,
            selection,
            unit,
            frameId,
            comparisonAxis,
            polarity,
            minimumDelta,
            copy,
            diagnostics);
        return new C3DHeightDifferenceEdgePointSet(
            outputEntityId,
            rootSourceEntityId,
            rootSourceSha256,
            inputEntityId,
            inputContentSha256,
            selectionId,
            selection,
            unit,
            frameId,
            comparisonAxis,
            polarity,
            minimumDelta,
            copy,
            diagnostics,
            provenance,
            hash);
    }

    private static string CalculateContentSha256(
        string outputEntityId,
        string rootSourceEntityId,
        string rootSourceSha256,
        string inputEntityId,
        string inputContentSha256,
        string selectionId,
        ToolRecipeGridRectangle selection,
        string unit,
        string frameId,
        C3DHeightDifferenceComparisonAxis comparisonAxis,
        C3DHeightDifferencePolarity polarity,
        double minimumDelta,
        IReadOnlyList<C3DHeightDifferenceEdgePoint> points,
        C3DHeightDifferenceEdgeDiagnostics diagnostics)
    {
        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true))
        {
            writer.Write("OpenVisionLab.C3DHeightDifferenceEdgePointSet");
            writer.Write(ContractVersion);
            writer.Write(outputEntityId);
            writer.Write(rootSourceEntityId);
            writer.Write(rootSourceSha256.ToUpperInvariant());
            writer.Write(inputEntityId);
            writer.Write(inputContentSha256.ToUpperInvariant());
            writer.Write(selectionId);
            writer.Write(selection.Row);
            writer.Write(selection.Column);
            writer.Write(selection.RowCount);
            writer.Write(selection.ColumnCount);
            writer.Write(unit);
            writer.Write(frameId);
            writer.Write((int)comparisonAxis);
            writer.Write((int)polarity);
            writer.Write(minimumDelta);
            writer.Write("StrongestPerScanline");
            writer.Write("PairMidpoint");
            writer.Write("SkipPair");
            writer.Write("WithinSelection");
            writer.Write(diagnostics.ScanlineCount);
            writer.Write(diagnostics.EligiblePairCount);
            writer.Write(diagnostics.SkippedMissingPairCount);
            writer.Write(diagnostics.AcceptedScanlineCount);
            writer.Write(diagnostics.NoCandidateScanlineCount);
            writer.Write(diagnostics.AcceptedMagnitudeMinimum);
            writer.Write(diagnostics.AcceptedMagnitudeMaximum);
            writer.Write(diagnostics.AcceptedMagnitudeMean);
            writer.Write(points.Count);
            foreach (var point in points)
            {
                writer.Write(point.ScanlineIndex);
                writer.Write(point.FirstRow);
                writer.Write(point.FirstColumn);
                writer.Write(point.FirstHeight);
                writer.Write(point.SecondRow);
                writer.Write(point.SecondColumn);
                writer.Write(point.SecondHeight);
                writer.Write(point.SignedDelta);
                writer.Write(point.Magnitude);
                writer.Write(point.X);
                writer.Write(point.Y);
                writer.Write(point.Z);
            }
        }

        return Convert.ToHexString(SHA256.HashData(stream.ToArray()));
    }
}
