using OpenVisionLab.ThreeD.Data;

internal static class C3DInvalidCellMapVerification
{
    private const string ExpectedSha256 =
        "23B3A4FCF69F0D63302DE1B5A67C00750EF237A3C98BDA194E3E6D32AC805BF0";

    public static int Run(string reportPath)
    {
        var source = C3DHeightFieldSnapshot.CreateForVerification(
            "source.invalid-cell-map",
            5,
            3,
            [
                0.0, 2.0, 3.0, 4.0, 5.0,
                6.0, 7.0, 0.0, 0.0, 10.0,
                11.0, 12.0, 13.0, 14.0, 0.0
            ]);
        var map = C3DInvalidCellMap.Create(source);
        var quality = C3DSourceQualityAnalyzer.Create(source);
        var heightImage = C3DHeightImageFrame.Create(source);
        var reshapedSource = C3DHeightFieldSnapshot.CreateForVerification(
            "source.invalid-cell-map-reshaped",
            3,
            5,
            [
                0.0, 2.0, 3.0, 4.0, 5.0,
                6.0, 7.0, 0.0, 0.0, 10.0,
                11.0, 12.0, 13.0, 14.0, 0.0
            ]);

        var cases = new[]
        {
            Check(
                "native-grid-metadata",
                () => map.Width == 5
                      && map.Height == 3
                      && map.CellCount == 15
                      && map.MissingCellCount == 4
                      && map.PackedByteLength == 2,
                $"grid={map.Width}x{map.Height},cells={map.CellCount},missing={map.MissingCellCount},bytes={map.PackedByteLength}"),
            Check(
                "exact-row-major-lsb-first-bytes",
                () => map.PackedBits.Span.SequenceEqual(
                    new byte[] { 0x81, 0x41 }),
                $"actual={Convert.ToHexString(map.PackedBits.Span)},expected=8141"),
            Check(
                "first-and-last-cell-locators",
                () => map.TryIsMissing(0, 0, out var first)
                      && first
                      && map.TryIsMissing(4, 2, out var last)
                      && last,
                "missing=(column 0,row 0),(column 4,row 2)"),
            Check(
                "byte-boundary-locators",
                () => map.TryIsMissing(2, 1, out var indexSeven)
                      && indexSeven
                      && map.TryIsMissing(3, 1, out var indexEight)
                      && indexEight,
                "row-major indexes 7 and 8 remain adjacent across packed bytes"),
            Check(
                "valid-locators-remain-clear",
                () => map.TryIsMissing(1, 0, out var firstValid)
                      && !firstValid
                      && map.TryIsMissing(3, 2, out var lastValid)
                      && !lastValid,
                "finite cells are zero bits"),
            Check(
                "out-of-range-locators-rejected",
                () => !map.TryIsMissing(-1, 0, out _)
                      && !map.TryIsMissing(0, -1, out _)
                      && !map.TryIsMissing(map.Width, 0, out _)
                      && !map.TryIsMissing(0, map.Height, out _),
                "negative and end-exclusive bounds rejected"),
            Check(
                "mask-sha-is-stable",
                () => Same(map.Sha256, ExpectedSha256),
                $"actual={map.Sha256},expected={ExpectedSha256}"),
            Check(
                "identity-exposes-map-contract",
                () => map.Identity.ContractVersion == C3DInvalidCellMap.ContractVersion
                      && map.Identity.Encoding == C3DInvalidCellMap.Encoding
                      && map.Identity.ByteLength == map.PackedByteLength
                      && Same(map.Identity.Sha256, map.Sha256),
                $"contract={map.Identity.ContractVersion},encoding={map.Identity.Encoding},bytes={map.Identity.ByteLength}"),
            Check(
                "source-quality-uses-identical-map-identity",
                () => quality.Coverage.MissingSampleCount == map.MissingCellCount
                      && quality.Coverage.InvalidCellMask.ByteLength == map.PackedByteLength
                      && Same(quality.Coverage.InvalidCellMask.Sha256, map.Sha256),
                $"qualitySha={quality.Coverage.InvalidCellMask.Sha256},mapSha={map.Sha256}"),
            Check(
                "height-image-owns-identical-map-bytes",
                () => heightImage.InvalidCellMap.PackedBits.Span.SequenceEqual(map.PackedBits.Span)
                      && Same(heightImage.InvalidCellMap.Sha256, map.Sha256),
                $"heightImageSha={heightImage.InvalidCellMap.Sha256},mapSha={map.Sha256}"),
            Check(
                "height-image-validity-follows-map",
                () =>
                {
                    for (var row = 0; row < map.Height; row++)
                    {
                        for (var column = 0; column < map.Width; column++)
                        {
                            if (!map.TryIsMissing(column, row, out var isMissing)
                                || !heightImage.TryGetCell(column, row, out var cell)
                                || cell.IsValid == isMissing)
                            {
                                return false;
                            }
                        }
                    }

                    return true;
                },
                "all 15 native cells preserve map/image validity parity"),
            Check(
                "grid-dimensions-affect-identity",
                () =>
                {
                    var reshaped = C3DInvalidCellMap.Create(reshapedSource);
                    return reshaped.PackedBits.Span.SequenceEqual(map.PackedBits.Span)
                           && !Same(reshaped.Sha256, map.Sha256);
                },
                "same bits with 5x3 versus 3x5 produce different identities"),
            Check(
                "missing-locator-affects-bytes-and-identity",
                () =>
                {
                    var movedSource = C3DHeightFieldSnapshot.CreateForVerification(
                        "source.invalid-cell-map-moved",
                        5,
                        3,
                        [
                            1.0, 0.0, 3.0, 4.0, 5.0,
                            6.0, 7.0, 0.0, 0.0, 10.0,
                            11.0, 12.0, 13.0, 14.0, 0.0
                        ]);
                    var moved = C3DInvalidCellMap.Create(movedSource);
                    return moved.MissingCellCount == map.MissingCellCount
                           && !moved.PackedBits.Span.SequenceEqual(map.PackedBits.Span)
                           && !Same(moved.Sha256, map.Sha256);
                },
                "equal missing counts at different locators cannot share identity"),
            Check(
                "all-valid-map-is-explicit",
                () =>
                {
                    var allValid = C3DInvalidCellMap.Create(
                        C3DHeightFieldSnapshot.CreateForVerification(
                            "source.invalid-cell-map-valid",
                            2,
                            2,
                            [1.0, 2.0, 3.0, 4.0]));
                    return allValid.MissingCellCount == 0
                           && allValid.PackedByteLength == 1
                           && allValid.PackedBits.Span.SequenceEqual(
                               new byte[] { 0x00 });
                },
                "four valid cells serialize as one zero byte"),
            Check(
                "all-missing-map-is-explicit",
                () =>
                {
                    var allMissing = C3DInvalidCellMap.Create(
                        C3DHeightFieldSnapshot.CreateForVerification(
                            "source.invalid-cell-map-missing",
                            2,
                            2,
                            [0.0, double.NaN, double.PositiveInfinity, double.NegativeInfinity]));
                    return allMissing.MissingCellCount == 4
                           && allMissing.PackedByteLength == 1
                           && allMissing.PackedBits.Span.SequenceEqual(
                               new byte[] { 0x0F });
                },
                "four missing cells serialize as low four bits set")
        };

        var passedCount = cases.Count(item => item.Passed);
        var status = passedCount == cases.Length ? "Pass" : "Fail";
        var fullReportPath = Path.GetFullPath(reportPath);
        Directory.CreateDirectory(
            Path.GetDirectoryName(fullReportPath) ?? Environment.CurrentDirectory);
        File.WriteAllLines(
            fullReportPath,
            [
                $"C3DInvalidCellMapVerification|{status}|cases={cases.Length}|passed={passedCount}|failed={cases.Length - passedCount}",
                $"Contract|version={C3DInvalidCellMap.ContractVersion}|encoding={C3DInvalidCellMap.Encoding}|wpfNeutral=true",
                $"Map|width={map.Width}|height={map.Height}|cells={map.CellCount}|missing={map.MissingCellCount}|bytes={map.PackedByteLength}|sha256={map.Sha256}",
                ..cases.Select(item => $"Case|{item.Name}|{(item.Passed ? "Pass" : "Fail")}|{Clean(item.Evidence)}")
            ]);
        Console.WriteLine($"C3D invalid-cell map verification: {status} ({passedCount}/{cases.Length})");
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

    private static bool Same(string first, string second) =>
        string.Equals(first, second, StringComparison.OrdinalIgnoreCase);

    private static string Clean(string value) =>
        value.Replace('|', '/').Replace('\r', ' ').Replace('\n', ' ');

    private sealed record VerificationCase(string Name, bool Passed, string Evidence);
}
