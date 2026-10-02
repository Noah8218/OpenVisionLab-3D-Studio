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
/// Candidate evidence exposed by the upstream edge extractor. The current
/// Vision3D contract retains only the strongest candidate per scanline, so the
/// second-candidate score is explicitly unavailable rather than inferred.
/// </summary>
public sealed record C3DHeightDifferenceEdgeCandidateEvidence(
    string Policy,
    string Provider,
    int TopCandidateCount,
    int SecondCandidateCount,
    string Limitation)
{
    public const string StrongestPerScanlinePolicy = "StrongestPerScanline";

    public bool HasNumericTopAndSecond => SecondCandidateCount > 0;

    public string Summary =>
        $"policy={Policy};provider={Provider};topCount={TopCandidateCount};secondCount={SecondCandidateCount};numericTopAndSecond={HasNumericTopAndSecond};limitation={Limitation}";

    public static C3DHeightDifferenceEdgeCandidateEvidence StrongestOnly(
        int topCandidateCount,
        string provider,
        string limitation) =>
        new(StrongestPerScanlinePolicy, provider, topCandidateCount, 0, limitation);
}

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
        C3DHeightDifferenceEdgeCandidateEvidence candidateEvidence,
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
        CandidateEvidence = candidateEvidence;
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
    public C3DHeightDifferenceEdgeCandidateEvidence CandidateEvidence { get; }
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
        string provenance,
        C3DHeightDifferenceEdgeCandidateEvidence? candidateEvidence = null)
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

        candidateEvidence ??= C3DHeightDifferenceEdgeCandidateEvidence.StrongestOnly(
            diagnostics.AcceptedScanlineCount,
            "OpenVisionLab.ThreeD.Core factory",
            "The caller did not supply second-candidate evidence.");
        ValidateCandidateEvidence(candidateEvidence, diagnostics.ScanlineCount);

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
            diagnostics,
            candidateEvidence);
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
            candidateEvidence,
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
        C3DHeightDifferenceEdgeDiagnostics diagnostics,
        C3DHeightDifferenceEdgeCandidateEvidence candidateEvidence)
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
            writer.Write(candidateEvidence.Policy);
            writer.Write(candidateEvidence.Provider);
            writer.Write(candidateEvidence.TopCandidateCount);
            writer.Write(candidateEvidence.SecondCandidateCount);
            writer.Write(candidateEvidence.Limitation);
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

    private static void ValidateCandidateEvidence(
        C3DHeightDifferenceEdgeCandidateEvidence candidateEvidence,
        int scanlineCount)
    {
        ArgumentNullException.ThrowIfNull(candidateEvidence);
        ArgumentException.ThrowIfNullOrWhiteSpace(candidateEvidence.Policy);
        ArgumentException.ThrowIfNullOrWhiteSpace(candidateEvidence.Provider);
        ArgumentException.ThrowIfNullOrWhiteSpace(candidateEvidence.Limitation);
        if (candidateEvidence.TopCandidateCount < 0
            || candidateEvidence.SecondCandidateCount < 0
            || candidateEvidence.TopCandidateCount > scanlineCount
            || candidateEvidence.SecondCandidateCount > candidateEvidence.TopCandidateCount)
        {
            throw new ArgumentOutOfRangeException(nameof(candidateEvidence), "Candidate evidence counts must be ordered and fit within the scanline count.");
        }
        if (!string.Equals(candidateEvidence.Policy, C3DHeightDifferenceEdgeCandidateEvidence.StrongestPerScanlinePolicy, StringComparison.Ordinal))
        {
            throw new InvalidDataException("Height Difference Edge candidate evidence must use the approved StrongestPerScanline policy.");
        }
        if (candidateEvidence.SecondCandidateCount != 0)
        {
            throw new InvalidDataException("StrongestPerScanline candidate evidence cannot report a retained second candidate.");
        }
    }
}
