using System.Buffers.Binary;
using System.Globalization;
using System.Text.Json;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Data;

internal static class SourceQualityReportVerification
{
    private const string ExpectedInvalidMaskSha256 =
        "E55705189A5D08B23D9037386E93CAA3C6A723A3E29A83A993AEAD9908A1D68B";
    private const string ExpectedLegacyReportSha256 =
        "E2176611372E01F26A8208A9C7C09154209A8DB50BA4774A1F4DA6670B9F82A2";

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
        var legacyQuality = quality with
        {
            SchemaVersion = SourceQualityReport.LegacySchemaVersion,
            GridDiagnostics = null
        };
        var legacyJson = JsonSerializer.Serialize(legacyQuality, jsonOptions);
        var legacyRoundtrip = JsonSerializer.Deserialize<SourceQualityReport>(
            legacyJson,
            jsonOptions);
        var currentIdentity = SourceQualityReportContentIdentity.CalculateSha256(quality);
        var changedChecks = quality.GridDiagnostics!.Checks.ToArray();
        changedChecks[0] = changedChecks[0] with
        {
            Message = changedChecks[0].Message + " Changed."
        };
        var changedDiagnosticQuality = quality with
        {
            GridDiagnostics = quality.GridDiagnostics with
            {
                Checks = changedChecks
            }
        };
        var fullReportPath = Path.GetFullPath(reportPath);
        var artifactDirectory = Path.GetDirectoryName(fullReportPath) ?? Environment.CurrentDirectory;
        Directory.CreateDirectory(artifactDirectory);

        var cases = new[]
        {
            Check("schema-and-source-identity", () =>
                quality.SchemaVersion == SourceQualityReport.CurrentSchemaVersion
                && quality.Source.EntityId == source.EntityId
                && quality.Source.Format == "C3D"
                && quality.Source.ByteLength == source.ByteLength
                && Same(quality.Source.ContentSha256, source.ContentSha256)
                && Same(quality.Source.RootSourceSha256, source.RootSourceSha256)
                && quality.TryValidateGridDiagnostics(out _)
                && quality.GridDiagnostics is
                {
                    SchemaVersion: SourceQualityGridDiagnostics.CurrentSchemaVersion,
                    State: SourceQualityGridDiagnosticState.Pass,
                    DeclaredCellCount: 12,
                    ObservedSampleCount: 12,
                    UniqueLocatorCount: 12,
                    Checks.Count: 4
                },
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
            Check("signed-finite-height-statistics", () =>
            {
                var signed = C3DSourceQualityAnalyzer.Create(
                    C3DHeightFieldSnapshot.CreateForVerification(
                        "source.signed-finite",
                        2,
                        2,
                        [-4.0, -1.0, 2.0, 5.0]),
                    distributionBinCount: 2);
                return signed.Coverage.ValidSampleCount == 4
                    && signed.Coverage.MissingSampleCount == 0
                    && Approximately(signed.Coverage.ValidRatio, 1.0)
                    && Approximately(signed.Coverage.MissingRatio, 0.0)
                    && Approximately(signed.Height.Minimum, -4.0)
                    && Approximately(signed.Height.Maximum, 5.0)
                    && Approximately(signed.Height.Mean, 0.5)
                    && signed.Height.Distribution is { PeakBinIndex: 0 } distribution
                    && distribution.Bins.SequenceEqual([2L, 2L]);
            }, "finite negative and positive heights retain exact statistics, ratios, and distribution"),
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
                && roundtrip.Coverage.InvalidCellMask.Sha256 == quality.Coverage.InvalidCellMask.Sha256
                && roundtrip.GridDiagnostics?.Checks[0].Code
                    == SourceQualityGridDiagnosticCode.Topology
                && legacyRoundtrip is
                {
                    SchemaVersion: SourceQualityReport.LegacySchemaVersion,
                    GridDiagnostics: null
                }
                && SourceQualityReportContentIdentity.CalculateSha256(legacyRoundtrip)
                    == ExpectedLegacyReportSha256
                && currentIdentity
                    != SourceQualityReportContentIdentity.CalculateSha256(
                        changedDiagnosticQuality)
                && !legacyJson.Contains("gridDiagnostics", StringComparison.Ordinal),
                $"deserialized={roundtrip is not null},chars={json.Length},enumEncoding=string,currentSha256={currentIdentity},legacySha256={SourceQualityReportContentIdentity.CalculateSha256(legacyQuality)}"),
            Check("all-missing-serializes-with-null-statistics", () =>
            {
                var path = Path.Combine(artifactDirectory, "raw-non-finite-height.c3d");
                try
                {
                    File.WriteAllBytes(
                        path,
                        CreateC3DBytes(
                            2,
                            2,
                            [0.0f, float.NaN, float.PositiveInfinity, float.NegativeInfinity]));
                    var allMissing = C3DSourceQualityAnalyzer.Create(
                        C3DHeightFieldSnapshot.LoadIdentified(
                            path,
                        "source.all-missing",
                            "raw-height",
                            "frame.c3d-grid-index"),
                        distributionBinCount: 4);
                    var serialized = JsonSerializer.Serialize(allMissing, jsonOptions);
                    var restored = JsonSerializer.Deserialize<SourceQualityReport>(serialized, jsonOptions);
                    return allMissing.Coverage.ValidSampleCount == 0
                        && allMissing.Coverage.MissingSampleCount == 4
                        && allMissing.Height.Minimum is null
                        && allMissing.Height.Maximum is null
                        && allMissing.Height.Mean is null
                        && allMissing.Height.Distribution is null
                        && allMissing.GridDiagnostics?.Checks.Single(check =>
                            check.Code == SourceQualityGridDiagnosticCode.CoordinateFiniteness).State
                            == SourceQualityGridDiagnosticState.Pass
                        && restored?.Height.Distribution is null;
                }
                finally
                {
                    File.Delete(path);
                }
            }, "height NaN/Inf remain missing coverage, not coordinate-finiteness errors"),
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
            }, "binCount=0 fails closed"),
            Check("non-monotonic-grid-locator-diagnostic", () =>
            {
                var diagnostics = SourceQualityGridDiagnosticsAnalyzer.AnalyzeExplicit(
                    2,
                    2,
                    [
                        new(0, 0, 0.0, 0.0, 1.0),
                        new(1, 0, 0.0, 1.0, 2.0),
                        new(0, 1, 1.0, 0.0, 3.0),
                        new(1, 1, 1.0, 1.0, 4.0)
                    ]);
                var check = diagnostics.Checks[1];
                return diagnostics.TryValidate(out _)
                    && diagnostics.Checks.Select(item => item.Code).SequenceEqual(
                    [
                        SourceQualityGridDiagnosticCode.Topology,
                        SourceQualityGridDiagnosticCode.LocatorMonotonicity,
                        SourceQualityGridDiagnosticCode.DuplicateLocator,
                        SourceQualityGridDiagnosticCode.CoordinateFiniteness
                    ])
                    && diagnostics.Checks[0].State == SourceQualityGridDiagnosticState.Pass
                    && check is
                    {
                        Code: SourceQualityGridDiagnosticCode.LocatorMonotonicity,
                        State: SourceQualityGridDiagnosticState.Error,
                        AffectedCount: 1,
                        FirstSampleOrdinal: 2,
                        FirstRow: 0,
                        FirstColumn: 1,
                        FirstComponent: "Locator",
                        Message: "Grid has 1 descending locator transition(s)."
                    };
            }, "first descending row-major locator is retained at ordinal 2, row 0, column 1"),
            Check("duplicate-grid-locator-diagnostic", () =>
            {
                var diagnostics = SourceQualityGridDiagnosticsAnalyzer.AnalyzeExplicit(
                    2,
                    2,
                    [
                        new(0, 0, 0.0, 0.0, 1.0),
                        new(0, 1, 1.0, 0.0, 2.0),
                        new(0, 1, 1.0, 0.0, 3.0),
                        new(1, 1, 1.0, 1.0, 4.0)
                    ]);
                var check = diagnostics.Checks[2];
                return diagnostics.TryValidate(out _)
                    && diagnostics.State == SourceQualityGridDiagnosticState.Error
                    && diagnostics.UniqueLocatorCount == 3
                    && diagnostics.Checks[0] is
                    {
                        Code: SourceQualityGridDiagnosticCode.Topology,
                        State: SourceQualityGridDiagnosticState.Error,
                        AffectedCount: 1
                    }
                    && check is
                    {
                        Code: SourceQualityGridDiagnosticCode.DuplicateLocator,
                        State: SourceQualityGridDiagnosticState.Error,
                        AffectedCount: 1,
                        FirstSampleOrdinal: 2,
                        FirstRow: 0,
                        FirstColumn: 1,
                        FirstComponent: "Locator",
                        Message: "Grid has 1 duplicate locator occurrence(s)."
                    };
            }, "first duplicate locator is retained at ordinal 2, row 0, column 1"),
            Check("non-finite-coordinate-diagnostic", () =>
            {
                var diagnostics = SourceQualityGridDiagnosticsAnalyzer.AnalyzeExplicit(
                    2,
                    2,
                    [
                        new(0, 0, 0.0, 0.0, 1.0),
                        new(0, 1, 1.0, double.NaN, double.PositiveInfinity),
                        new(1, 0, 0.0, 1.0, 3.0),
                        new(1, 1, 1.0, 1.0, 4.0)
                    ]);
                var check = diagnostics.Checks[3];
                return diagnostics.TryValidate(out _)
                    && diagnostics.Checks[0].State == SourceQualityGridDiagnosticState.Pass
                    && check is
                    {
                        Code: SourceQualityGridDiagnosticCode.CoordinateFiniteness,
                        State: SourceQualityGridDiagnosticState.Error,
                        AffectedCount: 2,
                        FirstSampleOrdinal: 1,
                        FirstRow: 0,
                        FirstColumn: 1,
                        FirstComponent: "Y",
                        Message: "Grid has 2 non-finite coordinate component(s)."
                    };
            }, "first non-finite XYZ component is retained at ordinal 1, row 0, column 1, component Y"),
            Check("diagnostic-payload-contradictions-rejected", () =>
                VerifyDiagnosticPayloadContract(quality),
                "out-of-range ordinals, contradictory topology/duplicate states, and incorrect duplicate counts fail closed; extra unique out-of-grid locators remain representable"),
            Check("incomplete-c3d-header-rejected", () =>
                RejectsMalformedC3D(
                    artifactDirectory,
                    "incomplete-header.c3d",
                    [0x01, 0x02, 0x03, 0x04],
                    C3DSourceTopologyReason.HeaderIncomplete),
                C3DSourceTopologyException.MessageFor(
                    C3DSourceTopologyReason.HeaderIncomplete)),
            Check("non-positive-c3d-dimensions-rejected", () =>
                new[]
                {
                    ("zero-width.c3d", CreateC3DBytes(0, 2, valueCount: 0)),
                    ("negative-width.c3d", CreateC3DBytes(-1, 2, valueCount: 0)),
                    ("zero-height.c3d", CreateC3DBytes(2, 0, valueCount: 0)),
                    ("negative-height.c3d", CreateC3DBytes(2, -1, valueCount: 0))
                }.All(fixture => RejectsMalformedC3D(
                    artifactDirectory,
                    fixture.Item1,
                    fixture.Item2,
                    C3DSourceTopologyReason.DimensionsNonPositive)),
                C3DSourceTopologyException.MessageFor(
                    C3DSourceTopologyReason.DimensionsNonPositive)),
            Check("mismatched-c3d-length-rejected", () =>
                RejectsMalformedC3D(
                    artifactDirectory,
                    "short-payload.c3d",
                    CreateC3DBytes(2, 2, valueCount: 3),
                    C3DSourceTopologyReason.PayloadLengthMismatch)
                && RejectsMalformedC3D(
                    artifactDirectory,
                    "trailing-payload.c3d",
                    CreateC3DBytes(2, 2, valueCount: 5),
                    C3DSourceTopologyReason.PayloadLengthMismatch),
                C3DSourceTopologyException.MessageFor(
                    C3DSourceTopologyReason.PayloadLengthMismatch)),
            Check("overflowing-c3d-dimensions-rejected", () =>
                RejectsMalformedC3D(
                    artifactDirectory,
                    "overflowing-dimensions.c3d",
                    CreateC3DBytes(int.MaxValue, int.MaxValue, valueCount: 0),
                    C3DSourceTopologyReason.CellCountOverflow),
                C3DSourceTopologyException.MessageFor(
                    C3DSourceTopologyReason.CellCountOverflow))
        };

        var passedCount = cases.Count(item => item.Passed);
        var status = passedCount == cases.Length ? "Pass" : "Fail";
        var jsonPath = Path.Combine(artifactDirectory, "source-quality-report.json");
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

    private static bool VerifyDiagnosticPayloadContract(
        SourceQualityReport quality)
    {
        var invalidLocationChecks = quality.GridDiagnostics!.Checks.ToArray();
        invalidLocationChecks[3] = invalidLocationChecks[3] with
        {
            State = SourceQualityGridDiagnosticState.Error,
            AffectedCount = 1,
            FirstSampleOrdinal = 999,
            FirstRow = 0,
            FirstColumn = 0,
            FirstComponent = "X"
        };
        var invalidLocation = quality with
        {
            GridDiagnostics = quality.GridDiagnostics with
            {
                State = SourceQualityGridDiagnosticState.Error,
                Checks = invalidLocationChecks
            }
        };
        invalidLocationChecks = invalidLocationChecks.ToArray();
        invalidLocationChecks[3] = invalidLocationChecks[3] with
        {
            FirstSampleOrdinal = 0,
            FirstRow = -1,
            FirstColumn = 999
        };
        var invalidGridLocator = quality with
        {
            GridDiagnostics = quality.GridDiagnostics with
            {
                State = SourceQualityGridDiagnosticState.Error,
                Checks = invalidLocationChecks
            }
        };

        var contradictoryChecks = quality.GridDiagnostics.Checks.ToArray();
        contradictoryChecks[3] = contradictoryChecks[3] with
        {
            State = SourceQualityGridDiagnosticState.Error,
            AffectedCount = 1,
            FirstSampleOrdinal = 0,
            FirstRow = 0,
            FirstColumn = 0,
            FirstComponent = "X"
        };
        var contradictoryStates = quality with
        {
            GridDiagnostics = quality.GridDiagnostics with
            {
                State = SourceQualityGridDiagnosticState.Error,
                UniqueLocatorCount = 11,
                Checks = contradictoryChecks
            }
        };

        var incorrectDuplicateChecks = quality.GridDiagnostics.Checks.ToArray();
        incorrectDuplicateChecks[0] = incorrectDuplicateChecks[0] with
        {
            State = SourceQualityGridDiagnosticState.Error,
            AffectedCount = 1,
            FirstSampleOrdinal = 1,
            FirstRow = 0,
            FirstColumn = 1,
            FirstComponent = "Locator"
        };
        incorrectDuplicateChecks[2] = incorrectDuplicateChecks[2] with
        {
            State = SourceQualityGridDiagnosticState.Error,
            AffectedCount = 2,
            FirstSampleOrdinal = 1,
            FirstRow = 0,
            FirstColumn = 1,
            FirstComponent = "Locator"
        };
        var incorrectDuplicateCount = quality with
        {
            GridDiagnostics = quality.GridDiagnostics with
            {
                State = SourceQualityGridDiagnosticState.Error,
                UniqueLocatorCount = 11,
                Checks = incorrectDuplicateChecks
            }
        };

        var extraUniqueLocatorDiagnostics =
            SourceQualityGridDiagnosticsAnalyzer.AnalyzeExplicit(
                2,
                2,
                [
                    new(0, 0, 0.0, 0.0, 1.0),
                    new(0, 1, 1.0, 0.0, 2.0),
                    new(1, 0, 0.0, 1.0, 3.0),
                    new(1, 1, 1.0, 1.0, 4.0),
                    new(2, 0, 0.0, 2.0, 5.0)
                ]);
        var extraUniqueLocatorReport = quality with
        {
            Grid = new SourceQualityGrid(2, 2, 4),
            Coverage = quality.Coverage with { SampleCount = 5 },
            GridDiagnostics = extraUniqueLocatorDiagnostics
        };

        return !invalidLocation.TryValidateGridDiagnostics(out _)
            && !invalidGridLocator.TryValidateGridDiagnostics(out _)
            && !contradictoryStates.TryValidateGridDiagnostics(out _)
            && !incorrectDuplicateCount.TryValidateGridDiagnostics(out _)
            && extraUniqueLocatorDiagnostics.UniqueLocatorCount == 5
            && extraUniqueLocatorDiagnostics.Checks[0].State
                == SourceQualityGridDiagnosticState.Error
            && extraUniqueLocatorDiagnostics.Checks[2].State
                == SourceQualityGridDiagnosticState.Pass
            && extraUniqueLocatorReport.TryValidateGridDiagnostics(out _);
    }

    private static bool RejectsMalformedC3D(
        string artifactDirectory,
        string fileName,
        byte[] bytes,
        C3DSourceTopologyReason expectedReason)
    {
        var path = Path.Combine(artifactDirectory, fileName);
        try
        {
            File.WriteAllBytes(path, bytes);
            return HasExactTopologyFailure(
                    () => C3DHeightFieldSnapshot.LoadIdentified(
                        path,
                        $"source.{Path.GetFileNameWithoutExtension(fileName)}",
                        "raw-height",
                        "frame.c3d-grid-index"),
                    expectedReason)
                && HasExactTopologyFailure(
                    () => C3DHeightGrid.Load(path),
                    expectedReason);
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static bool HasExactTopologyFailure<T>(
        Func<T> action,
        C3DSourceTopologyReason expectedReason)
    {
        try
        {
            _ = action();
            return false;
        }
        catch (C3DSourceTopologyException exception)
        {
            return exception.Reason == expectedReason
                && exception.Message
                    == C3DSourceTopologyException.MessageFor(expectedReason);
        }
    }

    private static byte[] CreateC3DBytes(
        int width,
        int height,
        int valueCount)
    {
        var bytes = new byte[checked(8 + valueCount * sizeof(float))];
        BinaryPrimitives.WriteInt32LittleEndian(bytes, width);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(4), height);
        return bytes;
    }

    private static byte[] CreateC3DBytes(
        int width,
        int height,
        IReadOnlyList<float> values)
    {
        var bytes = CreateC3DBytes(width, height, values.Count);
        for (var index = 0; index < values.Count; index++)
        {
            BinaryPrimitives.WriteInt32LittleEndian(
                bytes.AsSpan(8 + index * sizeof(float)),
                BitConverter.SingleToInt32Bits(values[index]));
        }

        return bytes;
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
