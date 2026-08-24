using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.Vision3D.FeatureExtraction;

namespace OpenVisionLab.ThreeD.Data;

public readonly record struct SourceQualityGridCoordinateSample(
    int Row,
    int Column,
    double X,
    double Y,
    double Z);

/// <summary>
/// Produces deterministic grid diagnostics without interpreting height values
/// as coordinates. C3D owns an implicit, valid row-major locator sequence;
/// explicit grids retain their supplied locator and XYZ defects.
/// </summary>
public static class SourceQualityGridDiagnosticsAnalyzer
{
    private static readonly GridDiagnosticsTool Tool = new();

    public static SourceQualityGridDiagnostics AnalyzeImplicitC3D(
        int width,
        int height)
    {
        if (width <= 0 || height <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(width),
                "Implicit C3D grid dimensions must be positive.");
        }

        return Map(Tool.Execute(width, height), implicitC3D: true);
    }

    public static SourceQualityGridDiagnostics AnalyzeExplicit(
        int width,
        int height,
        IReadOnlyList<SourceQualityGridCoordinateSample> samples)
    {
        ArgumentNullException.ThrowIfNull(samples);

        return Map(
            Tool.Execute(
                width,
                height,
                samples.Select(sample => new GridCoordinateSample(
                    sample.Row,
                    sample.Column,
                    sample.X,
                    sample.Y,
                    sample.Z)).ToArray()),
            implicitC3D: false);
    }

    private static SourceQualityGridDiagnostics Map(
        GridDiagnosticsResult result,
        bool implicitC3D)
    {
        var checks = result.Checks.Select(check =>
            new SourceQualityGridDiagnosticCheck(
                Map(check.Code),
                Map(check.State),
                check.AffectedCount,
                check.FirstSampleOrdinal,
                check.FirstRow,
                check.FirstColumn,
                check.FirstComponent,
                implicitC3D
                    && check.Code == GridDiagnosticCode.CoordinateFiniteness
                    && check.State == GridDiagnosticState.Pass
                        ? "Implicit C3D grid coordinates are finite."
                        : check.Message)).ToArray();
        var diagnostics = new SourceQualityGridDiagnostics(
            SourceQualityGridDiagnostics.CurrentSchemaVersion,
            Map(result.State),
            result.DeclaredCellCount,
            result.ObservedSampleCount,
            result.UniqueLocatorCount,
            Array.AsReadOnly(checks));
        if (!diagnostics.TryValidate(out var validationMessage))
        {
            throw new InvalidDataException(validationMessage);
        }

        return diagnostics;
    }

    private static SourceQualityGridDiagnosticCode Map(
        GridDiagnosticCode code) => code switch
        {
            GridDiagnosticCode.Topology => SourceQualityGridDiagnosticCode.Topology,
            GridDiagnosticCode.LocatorMonotonicity => SourceQualityGridDiagnosticCode.LocatorMonotonicity,
            GridDiagnosticCode.DuplicateLocator => SourceQualityGridDiagnosticCode.DuplicateLocator,
            GridDiagnosticCode.CoordinateFiniteness => SourceQualityGridDiagnosticCode.CoordinateFiniteness,
            _ => throw new InvalidDataException($"Unsupported SDK grid diagnostic code: {code}.")
        };

    private static SourceQualityGridDiagnosticState Map(
        GridDiagnosticState state) => state switch
        {
            GridDiagnosticState.Pass => SourceQualityGridDiagnosticState.Pass,
            GridDiagnosticState.Error => SourceQualityGridDiagnosticState.Error,
            _ => throw new InvalidDataException($"Unsupported SDK grid diagnostic state: {state}.")
        };
}
