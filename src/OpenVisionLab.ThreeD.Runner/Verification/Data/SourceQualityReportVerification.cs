using System.Globalization;
using System.Text.Json;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Data;

internal static class SourceQualityReportVerification
{
    private const string ExpectedInvalidMaskSha256 =
        "E55705189A5D08B23D9037386E93CAA3C6A723A3E29A83A993AEAD9908A1D68B";

    public static int Run(string reportPath)
    {
        var source = C3DHeightFieldSnapshot.CreateForVerification(
            "source.quality-fixture",
            4,
            3,
            [1.0, 2.0, 0.0, 4.0, double.NaN, 6.0, 7.0, 8.0, 9.0, 10.0, 11.0, 12.0]);
        var quality = C3DSourceQualityAnalyzer.Create(source, distributionBinCount: 4);
        var invalidCellMap = C3DInvalidCellMap.Create(source);
        var jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        };
        var json = JsonSerializer.Serialize(quality, jsonOptions);
        var roundtrip = JsonSerializer.Deserialize<SourceQualityReport>(json, jsonOptions);

        var cases = new[]
        {
            Check("schema-and-source-identity", () =>
                quality.SchemaVersion == SourceQualityReport.CurrentSchemaVersion
                && quality.Source.EntityId == source.EntityId
                && quality.Source.Format == "C3D"
                && quality.Source.ByteLength == source.ByteLength
                && Same(quality.Source.ContentSha256, source.ContentSha256)
                && Same(quality.Source.RootSourceSha256, source.RootSourceSha256),
                $"schema={quality.SchemaVersion},entity={quality.Source.EntityId},bytes={quality.Source.ByteLength},sha256={quality.Source.ContentSha256}"),
            Check("exact-grid-and-coverage", () =>
                quality.Grid.Width == 4
                && quality.Grid.Height == 3
                && quality.Grid.CellCount == 12
                && quality.Coverage.SampleCount == 12
                && quality.Coverage.ValidSampleCount == 10
                && quality.Coverage.MissingSampleCount == 2
                && Approximately(quality.Coverage.ValidRatio, 10.0 / 12.0)
                && Approximately(quality.Coverage.MissingRatio, 2.0 / 12.0),
                $"grid={quality.Grid.Width}x{quality.Grid.Height},cells={quality.Grid.CellCount},samples={quality.Coverage.SampleCount},valid={quality.Coverage.ValidSampleCount},missing={quality.Coverage.MissingSampleCount},validRatio={Format(quality.Coverage.ValidRatio)}"),
            Check("height-statistics", () =>
                quality.Height.ScalarMeaning == "raw-height"
                && Approximately(quality.Height.Minimum, 1.0)
                && Approximately(quality.Height.Maximum, 12.0)
                && Approximately(quality.Height.Mean, 7.0),
                $"meaning={quality.Height.ScalarMeaning},min={Format(quality.Height.Minimum)},max={Format(quality.Height.Maximum)},mean={Format(quality.Height.Mean)}"),
            Check("deterministic-distribution", () =>
                quality.Height.Distribution is
                {
                    BinCount: 4,
                    PeakBinIndex: 2
                } distribution
                && distribution.Bins.SequenceEqual([2L, 2L, 3L, 3L])
                && distribution.Bins.Sum() == quality.Coverage.ValidSampleCount,
                $"bins={string.Join(',', quality.Height.Distribution?.Bins ?? [])},peak={quality.Height.Distribution?.PeakBinIndex}"),
            Check("coordinate-true-invalid-mask-identity", () =>
                quality.Coverage.InvalidCellMask.ContractVersion == C3DSourceQualityAnalyzer.InvalidCellMaskContractVersion
                && quality.Coverage.InvalidCellMask.Encoding == C3DSourceQualityAnalyzer.InvalidCellMaskEncoding
                && quality.Coverage.InvalidCellMask.ByteLength == 2
                && Same(quality.Coverage.InvalidCellMask.Sha256, ExpectedInvalidMaskSha256),
                $"bytes={quality.Coverage.InvalidCellMask.ByteLength},sha256={quality.Coverage.InvalidCellMask.Sha256},missingLocators=2,4"),
            Check("invalid-mask-bytes-exposed-with-report-parity", () =>
                invalidCellMap.PackedBits.Span.SequenceEqual(
                    new byte[] { 0x14, 0x00 })
                && invalidCellMap.MissingCellCount == quality.Coverage.MissingSampleCount
                && invalidCellMap.PackedByteLength == quality.Coverage.InvalidCellMask.ByteLength
                && Same(invalidCellMap.Sha256, quality.Coverage.InvalidCellMask.Sha256),
                $"bytes={Convert.ToHexString(invalidCellMap.PackedBits.Span)},mapSha={invalidCellMap.Sha256},reportSha={quality.Coverage.InvalidCellMask.Sha256}"),
            Check("frame-unit-and-provenance", () =>
                quality.Coordinates.Unit == "raw-height"
                && quality.Coordinates.FrameId == "frame.c3d-grid-index"
                && quality.Coordinates.CoordinateConvention == "column-rawHeight-row"
                && quality.Provenance.StartsWith("verification:", StringComparison.Ordinal)
                && !quality.IsDerived,
                $"unit={quality.Coordinates.Unit},frame={quality.Coordinates.FrameId},convention={quality.Coordinates.CoordinateConvention},derived={quality.IsDerived}"),
            Check("actual-channel-only", () =>
                quality.Channels.Count == 7
                && quality.Channels.Count(channel => channel.State == SourceQualityChannelState.Available) == 1
                && quality.Channels.Single(channel => channel.State == SourceQualityChannelState.Available).Channel == SourceQualityChannel.Height,
                $"channels={quality.Channels.Count},available={string.Join(',', quality.Channels.Where(channel => channel.State == SourceQualityChannelState.Available).Select(channel => channel.Channel))}"),
            Check("unsupported-channels-explicitly-unavailable", () =>
                quality.Channels
                    .Where(channel => channel.Channel != SourceQualityChannel.Height)
                    .All(channel =>
                        channel.State == SourceQualityChannelState.Unavailable
                        && !string.IsNullOrWhiteSpace(channel.Evidence)),
                string.Join(';', quality.Channels.Where(channel => channel.Channel != SourceQualityChannel.Height).Select(channel => $"{channel.Channel}={channel.State}"))),
            Check("json-roundtrip", () =>
                roundtrip is not null
                && JsonSerializer.Serialize(roundtrip, jsonOptions) == json
                && roundtrip.Channels[0].Channel == SourceQualityChannel.Height
                && roundtrip.Coverage.InvalidCellMask.Sha256 == quality.Coverage.InvalidCellMask.Sha256,
                $"deserialized={roundtrip is not null},chars={json.Length},enumEncoding=string"),
            Check("all-missing-serializes-with-null-statistics", () =>
            {
                var allMissing = C3DSourceQualityAnalyzer.Create(
                    C3DHeightFieldSnapshot.CreateForVerification(
                        "source.all-missing",
                        2,
                        2,
                        [0.0, double.NaN, double.PositiveInfinity, double.NegativeInfinity]),
                    distributionBinCount: 4);
                var serialized = JsonSerializer.Serialize(allMissing, jsonOptions);
                var restored = JsonSerializer.Deserialize<SourceQualityReport>(serialized, jsonOptions);
                return allMissing.Coverage.ValidSampleCount == 0
                    && allMissing.Coverage.MissingSampleCount == 4
                    && allMissing.Height.Minimum is null
                    && allMissing.Height.Maximum is null
                    && allMissing.Height.Mean is null
                    && allMissing.Height.Distribution is null
                    && restored?.Height.Distribution is null;
            }, "all-missing finite statistics and distribution remain null"),
            Check("mask-locator-affects-identity", () =>
            {
                var movedMissing = C3DSourceQualityAnalyzer.Create(
                    C3DHeightFieldSnapshot.CreateForVerification(
                        "source.moved-missing",
                        4,
                        3,
                        [1.0, 2.0, 3.0, 0.0, double.NaN, 6.0, 7.0, 8.0, 9.0, 10.0, 11.0, 12.0]),
                    distributionBinCount: 4);
                return movedMissing.Coverage.MissingSampleCount == quality.Coverage.MissingSampleCount
                    && !Same(
                        movedMissing.Coverage.InvalidCellMask.Sha256,
                        quality.Coverage.InvalidCellMask.Sha256);
            }, "equal missing counts at different row-major locators produce different mask identities"),
            Check("invalid-bin-count-rejected", () =>
            {
                try
                {
                    C3DSourceQualityAnalyzer.Create(source, distributionBinCount: 0);
                    return false;
                }
                catch (ArgumentOutOfRangeException)
                {
                    return true;
                }
            }, "binCount=0 fails closed")
        };

        var passedCount = cases.Count(item => item.Passed);
        var status = passedCount == cases.Length ? "Pass" : "Fail";
        var fullReportPath = Path.GetFullPath(reportPath);
        var artifactDirectory = Path.GetDirectoryName(fullReportPath) ?? Environment.CurrentDirectory;
        var jsonPath = Path.Combine(artifactDirectory, "source-quality-report.json");
        Directory.CreateDirectory(artifactDirectory);
        File.WriteAllText(jsonPath, json);
        File.WriteAllLines(
            fullReportPath,
            [
                $"SourceQualityReportVerification|{status}|cases={cases.Length}|passed={passedCount}|failed={cases.Length - passedCount}",
                $"Contract|schema={quality.SchemaVersion}|wpfNeutral=true|sourceFormat={quality.Source.Format}|missingPolicy={quality.Coverage.MissingSamplePolicy}",
                $"Artifact|json={jsonPath}|bytes={new FileInfo(jsonPath).Length}",
                ..cases.Select(item => $"Case|{item.Name}|{(item.Passed ? "Pass" : "Fail")}|{Clean(item.Evidence)}")
            ]);
        Console.WriteLine($"SourceQualityReport verification: {status} ({passedCount}/{cases.Length})");
        return passedCount == cases.Length ? 0 : 5;
    }

    private static VerificationCase Check(
        string name,
        Func<bool> verify,
        string evidence)
    {
        try
        {
            return new VerificationCase(name, verify(), evidence);
        }
        catch (Exception exception)
        {
            return new VerificationCase(
                name,
                false,
                $"unexpected {exception.GetType().Name}: {exception.Message}");
        }
    }

    private static bool Approximately(double? actual, double expected, double tolerance = 1e-12) =>
        actual.HasValue && double.IsFinite(actual.Value) && Math.Abs(actual.Value - expected) <= tolerance;

    private static bool Same(string first, string second) =>
        string.Equals(first, second, StringComparison.OrdinalIgnoreCase);

    private static string Format(double? value) =>
        value?.ToString("G17", CultureInfo.InvariantCulture) ?? "null";

    private static string Clean(string value) =>
        value.Replace('|', '/').Replace('\r', ' ').Replace('\n', ' ');

    private sealed record VerificationCase(string Name, bool Passed, string Evidence);
}
