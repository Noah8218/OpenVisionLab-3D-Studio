using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace OpenVisionLab.ThreeD.Core;

/// <summary>
/// Immutable, recipe-owned A3 reference-grid definition. This describes a
/// display-frame grid only; it does not assert physical calibration.
/// </summary>
public sealed class C3DReferenceGridProfile
{
    public const string ContractVersion = "1.0";
    public const string CellAssignment = "PlanarNearestCellCenter";
    public const string CollisionPolicy = "NearestCenterThenSourceLocator";
    public const string OutOfBoundsPolicy = "RejectPreview";
    public const string HolePolicy = "PreserveMissing";

    private C3DReferenceGridProfile(
        string referenceFrameId,
        string referenceUnit,
        string referenceProvenance,
        string referenceRevision,
        C3DReferenceGridVector origin,
        C3DReferenceGridVector uAxis,
        C3DReferenceGridVector vAxis,
        C3DReferenceGridVector hAxis,
        double pitchU,
        double pitchV,
        int rowCount,
        int columnCount,
        double minimumCoverageRatio,
        string contentSha256)
    {
        ReferenceFrameId = referenceFrameId;
        ReferenceUnit = referenceUnit;
        ReferenceProvenance = referenceProvenance;
        ReferenceRevision = referenceRevision;
        Origin = origin;
        UAxis = uAxis;
        VAxis = vAxis;
        HAxis = hAxis;
        PitchU = pitchU;
        PitchV = pitchV;
        RowCount = rowCount;
        ColumnCount = columnCount;
        MinimumCoverageRatio = minimumCoverageRatio;
        ContentSha256 = contentSha256;
    }

    public string ReferenceFrameId { get; }
    public string ReferenceUnit { get; }
    public string ReferenceProvenance { get; }
    public string ReferenceRevision { get; }
    public C3DReferenceGridVector Origin { get; }
    public C3DReferenceGridVector UAxis { get; }
    public C3DReferenceGridVector VAxis { get; }
    public C3DReferenceGridVector HAxis { get; }
    public double PitchU { get; }
    public double PitchV { get; }
    public int RowCount { get; }
    public int ColumnCount { get; }
    public double MinimumCoverageRatio { get; }
    public string ContentSha256 { get; }

    public static C3DReferenceGridProfile Create(
        string referenceFrameId,
        string referenceUnit,
        string referenceProvenance,
        string referenceRevision,
        C3DReferenceGridVector origin,
        C3DReferenceGridVector uAxis,
        C3DReferenceGridVector vAxis,
        C3DReferenceGridVector hAxis,
        double pitchU,
        double pitchV,
        int rowCount,
        int columnCount,
        double minimumCoverageRatio)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(referenceFrameId);
        ArgumentException.ThrowIfNullOrWhiteSpace(referenceUnit);
        ArgumentException.ThrowIfNullOrWhiteSpace(referenceProvenance);
        ArgumentException.ThrowIfNullOrWhiteSpace(referenceRevision);
        if (!origin.IsFinite || !uAxis.IsFinite || !vAxis.IsFinite || !hAxis.IsFinite
            || !double.IsFinite(pitchU) || !double.IsFinite(pitchV) || !double.IsFinite(minimumCoverageRatio)
            || pitchU <= 0 || pitchV <= 0 || rowCount <= 0 || columnCount <= 0
            || minimumCoverageRatio < 0 || minimumCoverageRatio > 1)
        {
            throw new InvalidDataException("ReferenceGridProfile requires finite origin/axes, positive pitches/dimensions, and a minimum coverage ratio within [0, 1].");
        }

        var hash = CalculateContentSha256(
            referenceFrameId, referenceUnit, referenceProvenance, referenceRevision,
            origin, uAxis, vAxis, hAxis, pitchU, pitchV, rowCount, columnCount, minimumCoverageRatio);
        return new C3DReferenceGridProfile(
            referenceFrameId, referenceUnit, referenceProvenance, referenceRevision,
            origin, uAxis, vAxis, hAxis, pitchU, pitchV, rowCount, columnCount, minimumCoverageRatio, hash);
    }

    public IReadOnlyList<ToolRecipeParameter> ToRecipeParameters() =>
    [
        new("ReferenceFrameId", ReferenceFrameId), new("ReferenceUnit", ReferenceUnit),
        new("ReferenceProvenance", ReferenceProvenance), new("ReferenceRevision", ReferenceRevision),
        Number("OriginX", Origin.X), Number("OriginY", Origin.Y), Number("OriginZ", Origin.Z),
        Number("UAxisX", UAxis.X), Number("UAxisY", UAxis.Y), Number("UAxisZ", UAxis.Z),
        Number("VAxisX", VAxis.X), Number("VAxisY", VAxis.Y), Number("VAxisZ", VAxis.Z),
        Number("HAxisX", HAxis.X), Number("HAxisY", HAxis.Y), Number("HAxisZ", HAxis.Z),
        Number("PitchU", PitchU), Number("PitchV", PitchV),
        new("RowCount", RowCount.ToString(CultureInfo.InvariantCulture)), new("ColumnCount", ColumnCount.ToString(CultureInfo.InvariantCulture)),
        Number("MinimumCoverageRatio", MinimumCoverageRatio),
        new("CellAssignment", CellAssignment), new("CollisionPolicy", CollisionPolicy),
        new("OutOfBoundsPolicy", OutOfBoundsPolicy), new("HolePolicy", HolePolicy)
    ];

    public static C3DReferenceGridProfile FromRecipeParameters(IReadOnlyList<ToolRecipeParameter> parameters)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        var values = parameters.ToDictionary(parameter => parameter.Name, parameter => parameter.Value, StringComparer.Ordinal);
        if (values.Count != parameters.Count || values.Count != 25
            || !values.TryGetValue("CellAssignment", out var assignment) || assignment != CellAssignment
            || !values.TryGetValue("CollisionPolicy", out var collision) || collision != CollisionPolicy
            || !values.TryGetValue("OutOfBoundsPolicy", out var outOfBounds) || outOfBounds != OutOfBoundsPolicy
            || !values.TryGetValue("HolePolicy", out var hole) || hole != HolePolicy)
        {
            throw new InvalidDataException("Re-grid Height Map v1 requires exactly the typed ReferenceGridProfile parameters and fixed assignment/missing policies.");
        }

        return Create(
            Required(values, "ReferenceFrameId"), Required(values, "ReferenceUnit"), Required(values, "ReferenceProvenance"), Required(values, "ReferenceRevision"),
            new C3DReferenceGridVector(Number(values, "OriginX"), Number(values, "OriginY"), Number(values, "OriginZ")),
            new C3DReferenceGridVector(Number(values, "UAxisX"), Number(values, "UAxisY"), Number(values, "UAxisZ")),
            new C3DReferenceGridVector(Number(values, "VAxisX"), Number(values, "VAxisY"), Number(values, "VAxisZ")),
            new C3DReferenceGridVector(Number(values, "HAxisX"), Number(values, "HAxisY"), Number(values, "HAxisZ")),
            Number(values, "PitchU"), Number(values, "PitchV"),
            Integer(values, "RowCount"), Integer(values, "ColumnCount"), Number(values, "MinimumCoverageRatio"));
    }

    private static ToolRecipeParameter Number(string name, double value) => new(name, value.ToString("G17", CultureInfo.InvariantCulture));
    private static string Required(IReadOnlyDictionary<string, string> values, string name) =>
        values.TryGetValue(name, out var value) && !string.IsNullOrWhiteSpace(value) ? value : throw new InvalidDataException($"ReferenceGridProfile parameter '{name}' is required.");
    private static double Number(IReadOnlyDictionary<string, string> values, string name) =>
        double.TryParse(Required(values, name), NumberStyles.Float, CultureInfo.InvariantCulture, out var value) && double.IsFinite(value)
            ? value : throw new InvalidDataException($"ReferenceGridProfile parameter '{name}' must be a finite invariant number.");
    private static int Integer(IReadOnlyDictionary<string, string> values, string name) =>
        int.TryParse(Required(values, name), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
            ? value : throw new InvalidDataException($"ReferenceGridProfile parameter '{name}' must be an invariant integer.");

    private static string CalculateContentSha256(
        string frameId, string unit, string provenance, string revision,
        C3DReferenceGridVector origin, C3DReferenceGridVector uAxis, C3DReferenceGridVector vAxis, C3DReferenceGridVector hAxis,
        double pitchU, double pitchV, int rows, int columns, double minimumCoverage)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        writer.Write("OpenVisionLab.C3DReferenceGridProfile"); writer.Write(ContractVersion);
        writer.Write(frameId); writer.Write(unit); writer.Write(provenance); writer.Write(revision);
        WriteVector(writer, origin); WriteVector(writer, uAxis); WriteVector(writer, vAxis); WriteVector(writer, hAxis);
        writer.Write(pitchU); writer.Write(pitchV); writer.Write(rows); writer.Write(columns); writer.Write(minimumCoverage);
        writer.Write(CellAssignment); writer.Write(CollisionPolicy); writer.Write(OutOfBoundsPolicy); writer.Write(HolePolicy);
        return Convert.ToHexString(SHA256.HashData(stream.ToArray()));
    }

    private static void WriteVector(BinaryWriter writer, C3DReferenceGridVector vector)
    {
        writer.Write(vector.X); writer.Write(vector.Y); writer.Write(vector.Z);
    }
}

public readonly record struct C3DReferenceGridVector(double X, double Y, double Z)
{
    public bool IsFinite => double.IsFinite(X) && double.IsFinite(Y) && double.IsFinite(Z);
}

/// <summary>
/// Immutable A3 output. Cells are explicit reference-grid samples and missing
/// cells remain missing; the field is not a mesh or a physical measurement.
/// </summary>
public sealed class C3DTransformedHeightField
{
    public const string ContractVersion = "1.0";
    private readonly C3DTransformedHeightCell[] cells;

    private C3DTransformedHeightField(
        string outputEntityId,
        C3DTransformedPointCloud source,
        C3DReferenceGridProfile profile,
        C3DTransformedHeightCell[] cells,
        int collisionCount,
        string provenance,
        string contentSha256)
    {
        OutputEntityId = outputEntityId;
        RootSourceEntityId = source.RootSourceEntityId;
        RootSourceSha256 = source.RootSourceSha256;
        SourceContentSha256 = source.ContentSha256;
        AffineTransformEntityId = source.AffineTransformEntityId;
        AffineTransformContentSha256 = source.AffineTransformContentSha256;
        ReferenceFrameId = profile.ReferenceFrameId;
        ReferenceUnit = profile.ReferenceUnit;
        ReferenceProvenance = profile.ReferenceProvenance;
        ReferenceRevision = profile.ReferenceRevision;
        ReferenceGridProfileSha256 = profile.ContentSha256;
        ReferenceGridProfile = profile;
        RowCount = profile.RowCount;
        ColumnCount = profile.ColumnCount;
        this.cells = cells;
        PopulatedCellCount = cells.Count(cell => cell.HasValue);
        MissingCellCount = cells.Length - PopulatedCellCount;
        CoverageRatio = (double)PopulatedCellCount / cells.Length;
        MinimumCoverageRatio = profile.MinimumCoverageRatio;
        MeetsMinimumCoverage = CoverageRatio >= MinimumCoverageRatio;
        CollisionCount = collisionCount;
        Provenance = provenance;
        ContentSha256 = contentSha256;
    }

    public string OutputEntityId { get; }
    public string RootSourceEntityId { get; }
    public string RootSourceSha256 { get; }
    public string SourceContentSha256 { get; }
    public string AffineTransformEntityId { get; }
    public string AffineTransformContentSha256 { get; }
    public string ReferenceFrameId { get; }
    public string ReferenceUnit { get; }
    public string ReferenceProvenance { get; }
    public string ReferenceRevision { get; }
    public string ReferenceGridProfileSha256 { get; }
    public C3DReferenceGridProfile ReferenceGridProfile { get; }
    public int RowCount { get; }
    public int ColumnCount { get; }
    public int PopulatedCellCount { get; }
    public int MissingCellCount { get; }
    public double CoverageRatio { get; }
    public double MinimumCoverageRatio { get; }
    public bool MeetsMinimumCoverage { get; }
    public int CollisionCount { get; }
    public IReadOnlyList<C3DTransformedHeightCell> Cells => cells;
    public string Provenance { get; }
    public string ContentSha256 { get; }

    public static C3DTransformedHeightField Create(
        string outputEntityId,
        C3DTransformedPointCloud source,
        C3DReferenceGridProfile profile,
        IReadOnlyList<C3DTransformedHeightCell> cells,
        int collisionCount,
        string provenance)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputEntityId);
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(cells);
        ArgumentException.ThrowIfNullOrWhiteSpace(provenance);
        if (string.Equals(outputEntityId, source.OutputEntityId, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("TransformedHeightField output ID must differ from its TransformedPointCloud input.");
        }
        if (!string.Equals(profile.ReferenceFrameId, source.ReferenceFrameId, StringComparison.Ordinal)
            || !string.Equals(profile.ReferenceUnit, source.ReferenceUnit, StringComparison.Ordinal)
            || !string.Equals(profile.ReferenceProvenance, source.ReferenceProvenance, StringComparison.Ordinal)
            || !string.Equals(profile.ReferenceRevision, source.ReferenceRevision, StringComparison.Ordinal))
        {
            throw new InvalidDataException("ReferenceGridProfile identity/frame/unit/provenance must match the Published TransformedPointCloud.");
        }
        var copy = cells.ToArray();
        ValidateCells(copy, profile, source);
        if (collisionCount < 0) throw new ArgumentOutOfRangeException(nameof(collisionCount));
        var hash = CalculateContentSha256(outputEntityId, source, profile, copy, collisionCount);
        return new C3DTransformedHeightField(outputEntityId, source, profile, copy, collisionCount, provenance, hash);
    }

    private static void ValidateCells(IReadOnlyList<C3DTransformedHeightCell> cells, C3DReferenceGridProfile profile, C3DTransformedPointCloud source)
    {
        if (cells.Count != checked(profile.RowCount * profile.ColumnCount))
        {
            throw new InvalidDataException("TransformedHeightField must contain every authored reference-grid cell exactly once.");
        }
        for (var index = 0; index < cells.Count; index++)
        {
            var cell = cells[index];
            if (cell.Row != index / profile.ColumnCount || cell.Column != index % profile.ColumnCount)
            {
                throw new InvalidDataException("TransformedHeightField cells must be row-major in authored grid order.");
            }
            if (cell.HasValue)
            {
                if (!double.IsFinite(cell.Height) || cell.SourceRow < 0 || cell.SourceRow >= source.SourceGridHeight
                    || cell.SourceColumn < 0 || cell.SourceColumn >= source.SourceGridWidth || !double.IsFinite(cell.PlanarDistanceSquared)
                    || cell.PlanarDistanceSquared < 0)
                {
                    throw new InvalidDataException("TransformedHeightField populated cells require finite height, source locator, and planar-distance evidence.");
                }
            }
            else if (!double.IsNaN(cell.Height) || cell.SourceRow != -1 || cell.SourceColumn != -1 || !double.IsNaN(cell.PlanarDistanceSquared))
            {
                throw new InvalidDataException("TransformedHeightField missing cells must preserve NaN height, no source locator, and no planar distance.");
            }
        }
    }

    private static string CalculateContentSha256(
        string outputEntityId, C3DTransformedPointCloud source, C3DReferenceGridProfile profile,
        IReadOnlyList<C3DTransformedHeightCell> cells, int collisionCount)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        writer.Write("OpenVisionLab.C3DTransformedHeightField"); writer.Write(ContractVersion);
        writer.Write(outputEntityId); writer.Write(source.ContentSha256.ToUpperInvariant());
        writer.Write(source.AffineTransformContentSha256.ToUpperInvariant()); writer.Write(profile.ContentSha256.ToUpperInvariant());
        writer.Write(cells.Count); writer.Write(collisionCount);
        foreach (var cell in cells)
        {
            writer.Write(cell.Row); writer.Write(cell.Column); writer.Write(cell.Height); writer.Write(cell.SourceRow); writer.Write(cell.SourceColumn); writer.Write(cell.PlanarDistanceSquared);
        }
        return Convert.ToHexString(SHA256.HashData(stream.ToArray()));
    }
}

public readonly record struct C3DTransformedHeightCell(int Row, int Column, double Height, int SourceRow, int SourceColumn, double PlanarDistanceSquared)
{
    public bool HasValue => double.IsFinite(Height);
}
