using System.IO;
using System.Security.Cryptography;
using OpenVisionLab.ThreeD.Core;

namespace OpenVisionLab.ThreeD.Viewer.Automation;

/// <summary>
/// Owns the Viewer Smoke Nominal/Actual input boundary. It resolves the
/// dataset alias, captures immutable file identity, prepares the existing Core
/// input contract, and asks the existing ViewModel/host owners to display and
/// preview it. Scenario lifetime and shutdown remain with the runner.
/// </summary>
internal sealed class ViewerSmokeNominalActualConfigurator
{
    private readonly IViewerSmokeHost host;

    public ViewerSmokeNominalActualConfigurator(IViewerSmokeHost host)
    {
        this.host = host ?? throw new ArgumentNullException(nameof(host));
    }

    public ViewerSmokeNominalActualConfiguration Configure(string[] args)
    {
        var comparisonIndex = Array.IndexOf(args, "--smoke-nominal-actual");
        if (comparisonIndex < 0)
        {
            return default;
        }

        try
        {
            if (comparisonIndex + 3 >= args.Length
                || args[comparisonIndex + 1].StartsWith("--", StringComparison.Ordinal)
                || args[comparisonIndex + 2].StartsWith("--", StringComparison.Ordinal)
                || args[comparisonIndex + 3].StartsWith("--", StringComparison.Ordinal))
            {
                return Failure(
                    "Nominal/actual smoke requires <actual.stl> <validation-query.ply> <nominal.stl>.");
            }

            var sourceIdentity = ResolveSourceIdentity(args);
            var actual = CaptureFileIdentity(
                sourceIdentity.ActualId,
                sourceIdentity.ActualName,
                args[comparisonIndex + 1]);
            var query = CaptureFileIdentity(
                sourceIdentity.QueryId,
                sourceIdentity.QueryName,
                args[comparisonIndex + 2]);
            var nominal = CaptureFileIdentity(
                "source.nist-overhang-x4-nominal-9x5x5",
                "NIST Overhang X4 nominal 9x5x5 mm",
                args[comparisonIndex + 3]);
            var input = new NominalActualComparisonInput(
                "step.nist-overhang-x4-surface-deviation",
                actual,
                nominal,
                query,
                "mm",
                "frame.nist-overhang-x4-321-part",
                "alignment.identity-source-provided",
                host.NominalActualLowerTolerance,
                host.NominalActualUpperTolerance);

            host.LoadSource(ViewerSmokeSource.Stl, nominal.Path);
            if (!host.HasImportedMesh)
            {
                throw new InvalidDataException("The nominal comparison mesh could not be loaded for display.");
            }

            host.ConfigureNominalActualComparison(input);
            host.PreviewNominalActual();
            return new ViewerSmokeNominalActualConfiguration(true, null);
        }
        catch (Exception exception)
        {
            host.ClearNominalActualComparison(exception.Message);
            return Failure($"Nominal/actual smoke failed: {exception.Message}");
        }
    }

    private static ViewerSmokeNominalActualConfiguration Failure(string message) =>
        new(true, message);

    private static (string ActualId, string ActualName, string QueryId, string QueryName)
        ResolveSourceIdentity(string[] args)
    {
        var datasetIndex = Array.IndexOf(args, "--smoke-nominal-actual-dataset");
        var dataset = datasetIndex < 0
            ? "nist-overhang-x4-part1"
            : datasetIndex + 1 < args.Length
                && !args[datasetIndex + 1].StartsWith("--", StringComparison.Ordinal)
                    ? args[datasetIndex + 1]
                    : throw new ArgumentException(
                        "Nominal/actual smoke dataset requires nist-overhang-x4-part1 or nist-overhang-x4-part2.");

        return dataset.ToLowerInvariant() switch
        {
            "nist-overhang-x4-part1" => (
                "source.nist-overhang-x4-actual-part1",
                "NIST Overhang X4 Part 1 XCT surface",
                "query.nist-overhang-x4-cloudcompare-vertices",
                "NIST Overhang X4 validation vertices"),
            "nist-overhang-x4-part2" => (
                "source.nist-overhang-x4-actual-part2",
                "NIST Overhang X4 Part 2 XCT surface",
                "query.nist-overhang-x4-part2-cloudcompare-vertices",
                "NIST Overhang X4 Part 2 validation vertices"),
            _ => throw new ArgumentException($"Unsupported nominal/actual smoke dataset: {dataset}"),
        };
    }

    private static NominalActualFileIdentity CaptureFileIdentity(
        string id,
        string name,
        string path)
    {
        var fullPath = Path.GetFullPath(path);
        using var stream = File.OpenRead(fullPath);
        return new NominalActualFileIdentity(
            id,
            name,
            fullPath,
            stream.Length,
            Convert.ToHexString(SHA256.HashData(stream)));
    }
}

internal readonly record struct ViewerSmokeNominalActualConfiguration(
    bool Requested,
    string? Failure);
