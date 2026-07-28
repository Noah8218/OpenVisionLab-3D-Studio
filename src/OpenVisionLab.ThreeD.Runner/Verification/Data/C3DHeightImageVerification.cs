using OpenVisionLab.ThreeD.Data;

internal static class C3DHeightImageVerification
{
    private const string ExpectedPixelSha256 =
        "FE918C4C9929A663F13BFED9C30485EC9260F9A557AFEAF250926D27A7978A92";

    public static int RunProbe(
        string sourcePath,
        string entityId,
        string unit,
        string frameId,
        string reportPath)
    {
        try
        {
            var source = C3DHeightFieldSnapshot.LoadIdentified(
                sourcePath,
                entityId,
                unit,
                frameId);
            var frame = C3DHeightImageFrame.Create(source);
            var quality = C3DSourceQualityAnalyzer.Create(source);
            var invalidOverlay = frame.CreateDisplayFrame(
                C3DHeightImagePalette.Height,
                frame.Minimum,
                frame.Maximum,
                C3DHeightImageInvalidOverlayMode.Visible);
            var pixelCount = checked(frame.Width * frame.Height);
            var passed = frame.Bgra32Pixels.Length == checked(pixelCount * 4)
                         && frame.ValidCount + frame.MissingCount == pixelCount
                         && frame.SourceContentSha256 == source.ContentSha256
                         && frame.InvalidCellMap.Width == quality.Grid.Width
                         && frame.InvalidCellMap.Height == quality.Grid.Height
                         && frame.InvalidCellMap.MissingCellCount
                            == quality.Coverage.MissingSampleCount
                         && string.Equals(
                             frame.InvalidCellMap.Sha256,
                             quality.Coverage.InvalidCellMask.Sha256,
                             StringComparison.OrdinalIgnoreCase)
                         && invalidOverlay.InvalidOverlayMode
                            == C3DHeightImageInvalidOverlayMode.Visible
                         && invalidOverlay.InvalidOverlayPixelCount
                            == quality.Coverage.MissingSampleCount
                         && string.Equals(
                             invalidOverlay.InvalidCellMapSha256,
                             quality.Coverage.InvalidCellMask.Sha256,
                             StringComparison.OrdinalIgnoreCase)
                         && frame.TryGetCell(0, 0, out _)
                         && frame.TryGetCell(frame.Width - 1, frame.Height - 1, out _);
            var status = passed ? "Pass" : "Fail";
            var fullReportPath = Path.GetFullPath(reportPath);
            Directory.CreateDirectory(
                Path.GetDirectoryName(fullReportPath) ?? Environment.CurrentDirectory);
            File.WriteAllLines(
                fullReportPath,
                [
                    $"C3DHeightImageProbe|{status}|mapping={C3DHeightImageFrame.CoordinateMapping}|viewOnly=true",
                    $"Source|path={source.SourcePath}|bytes={source.ByteLength}|sha256={source.ContentSha256}|entity={source.EntityId}|frame={source.FrameId}|unit={source.Unit}",
                    $"Frame|width={frame.Width}|height={frame.Height}|pixels={pixelCount}|bgraBytes={frame.Bgra32Pixels.Length}|valid={frame.ValidCount}|missing={frame.MissingCount}|pixelSha256={frame.PixelSha256}",
                    $"InvalidCellMap|bytes={frame.InvalidCellMap.PackedByteLength}|missing={frame.InvalidCellMap.MissingCellCount}|sha256={frame.InvalidCellMap.Sha256}|sourceQualitySha256={quality.Coverage.InvalidCellMask.Sha256}|parity={string.Equals(frame.InvalidCellMap.Sha256, quality.Coverage.InvalidCellMask.Sha256, StringComparison.OrdinalIgnoreCase)}",
                    $"VisibleInvalidOverlay|mode={invalidOverlay.InvalidOverlayMode}|pixels={invalidOverlay.InvalidOverlayPixelCount}|maskSha256={invalidOverlay.InvalidCellMapSha256}|displayPixelSha256={invalidOverlay.PixelSha256}|viewOnly=true",
                    $"Height|min={frame.Minimum:R}|max={frame.Maximum:R}|mean={frame.Mean:R}"
                ]);
            Console.WriteLine(
                $"C3D Height Image probe: {status} ({frame.Width} x {frame.Height}, {pixelCount:N0} pixels)");
            return passed ? 0 : 5;
        }
        catch (Exception exception) when (
            exception is IOException
                or InvalidDataException
                or UnauthorizedAccessException
                or ArgumentException
                or OverflowException)
        {
            Console.Error.WriteLine(exception.Message);
            return 1;
        }
    }

    public static int Run(string reportPath)
    {
        var source = C3DHeightFieldSnapshot.CreateForVerification(
            "source.height-image",
            3,
            2,
            [10.0, 20.0, 0.0, 40.0, 50.0, 60.0]);
        var frame = C3DHeightImageFrame.Create(source);
        var quality = C3DSourceQualityAnalyzer.Create(source);
        var manualHeight = frame.CreateDisplayFrame(
            C3DHeightImagePalette.Height,
            20.0,
            50.0);
        var manualHeightReplay = frame.CreateDisplayFrame(
            C3DHeightImagePalette.Height,
            20.0,
            50.0);
        var grayscale = frame.CreateDisplayFrame(
            C3DHeightImagePalette.Grayscale,
            frame.Minimum,
            frame.Maximum);
        var thermal = frame.CreateDisplayFrame(
            C3DHeightImagePalette.Thermal,
            frame.Minimum,
            frame.Maximum);
        var invalidOverlay = frame.CreateDisplayFrame(
            C3DHeightImagePalette.Height,
            frame.Minimum,
            frame.Maximum,
            C3DHeightImageInvalidOverlayMode.Visible);
        var invalidOverlayReplay = frame.CreateDisplayFrame(
            C3DHeightImagePalette.Height,
            frame.Minimum,
            frame.Maximum,
            C3DHeightImageInvalidOverlayMode.Visible);
        var expectedPixels = new byte[]
        {
            242, 89, 12, 255,
            211, 145, 12, 255,
            39, 24, 17, 255,
            137, 219, 61, 255,
            81, 198, 158, 255,
            25, 178, 255, 255
        };
        var cases = new[]
        {
            Check(
                "native-dimensions-and-stride",
                () => frame.Width == 3
                      && frame.Height == 2
                      && frame.Stride == 12
                      && frame.Bgra32Pixels.Length == 24,
                $"size={frame.Width}x{frame.Height},stride={frame.Stride},bytes={frame.Bgra32Pixels.Length}"),
            Check(
                "coordinate-contract-explicit",
                () => C3DHeightImageFrame.CoordinateMapping
                    == "pixelX=column;pixelY=row;no-flip;one-source-cell-per-pixel",
                C3DHeightImageFrame.CoordinateMapping),
            Check(
                "top-left-is-source-row-zero-column-zero",
                () => frame.TryGetCell(0, 0, out var cell)
                      && cell.Row == 0
                      && cell.Column == 0
                      && cell.IsValid
                      && cell.RawHeight == 10.0,
                CellEvidence(frame, 0, 0)),
            Check(
                "top-right-preserves-missing-cell",
                () => frame.TryGetCell(2, 0, out var cell)
                      && cell.Row == 0
                      && cell.Column == 2
                      && !cell.IsValid
                      && double.IsNaN(cell.RawHeight),
                CellEvidence(frame, 2, 0)),
            Check(
                "bottom-left-is-source-row-one-column-zero",
                () => frame.TryGetCell(0, 1, out var cell)
                      && cell.Row == 1
                      && cell.Column == 0
                      && cell.IsValid
                      && cell.RawHeight == 40.0,
                CellEvidence(frame, 0, 1)),
            Check(
                "bottom-right-is-last-source-cell",
                () => frame.TryGetCell(2, 1, out var cell)
                      && cell.Row == 1
                      && cell.Column == 2
                      && cell.IsValid
                      && cell.RawHeight == 60.0,
                CellEvidence(frame, 2, 1)),
            Check(
                "bgra-pixels-are-row-major-and-deterministic",
                () => frame.Bgra32Pixels.Span.SequenceEqual(expectedPixels),
                $"actual={Convert.ToHexString(frame.Bgra32Pixels.Span)},expected={Convert.ToHexString(expectedPixels)}"),
            Check(
                "pixel-sha-is-stable",
                () => string.Equals(frame.PixelSha256, ExpectedPixelSha256, StringComparison.Ordinal),
                $"actual={frame.PixelSha256},expected={ExpectedPixelSha256}"),
            Check(
                "default-display-frame-reuses-native-auto-range",
                () => frame.DefaultDisplayFrame.Bgra32Pixels.Span.SequenceEqual(frame.Bgra32Pixels.Span)
                      && frame.DefaultDisplayFrame.Palette == C3DHeightImagePalette.Height
                      && frame.DefaultDisplayFrame.Minimum == frame.Minimum
                      && frame.DefaultDisplayFrame.Maximum == frame.Maximum
                      && frame.DefaultDisplayFrame.PixelSha256 == frame.PixelSha256,
                $"palette={frame.DefaultDisplayFrame.Palette},range={frame.DefaultDisplayFrame.Minimum:R}..{frame.DefaultDisplayFrame.Maximum:R},sha={frame.DefaultDisplayFrame.PixelSha256}"),
            Check(
                "manual-range-preserves-native-dimensions",
                () => manualHeight.Width == frame.Width
                      && manualHeight.Height == frame.Height
                      && manualHeight.Stride == frame.Stride
                      && manualHeight.Bgra32Pixels.Length == frame.Bgra32Pixels.Length,
                $"size={manualHeight.Width}x{manualHeight.Height},stride={manualHeight.Stride},bytes={manualHeight.Bgra32Pixels.Length}"),
            Check(
                "manual-range-clamps-only-display-colors",
                () => manualHeight.Minimum == 20.0
                      && manualHeight.Maximum == 50.0
                      && manualHeight.Bgra32Pixels.Span[..4]
                          .SequenceEqual(new byte[] { 242, 89, 12, 255 })
                      && manualHeight.Bgra32Pixels.Span[16..20]
                          .SequenceEqual(new byte[] { 25, 178, 255, 255 })
                      && frame.TryGetCell(0, 0, out var lowCell)
                      && lowCell.RawHeight == 10.0
                      && frame.TryGetCell(1, 1, out var highCell)
                      && highCell.RawHeight == 50.0,
                $"range={manualHeight.Minimum:R}..{manualHeight.Maximum:R},rawLow=10,rawHigh=50"),
            Check(
                "manual-range-preserves-missing-pixel",
                () => manualHeight.Bgra32Pixels.Span[8..12]
                    .SequenceEqual(new byte[] { 39, 24, 17, 255 })
                      && frame.InvalidCellMap.TryIsMissing(2, 0, out var isMissing)
                      && isMissing,
                $"missingPixel={Convert.ToHexString(manualHeight.Bgra32Pixels.Span[8..12])}"),
            Check(
                "manual-range-render-is-deterministic-and-distinct",
                () => manualHeight.PixelSha256 == manualHeightReplay.PixelSha256
                      && manualHeight.Bgra32Pixels.Span.SequenceEqual(
                          manualHeightReplay.Bgra32Pixels.Span)
                      && manualHeight.PixelSha256 != frame.PixelSha256,
                $"manual={manualHeight.PixelSha256},replay={manualHeightReplay.PixelSha256},auto={frame.PixelSha256}"),
            Check(
                "palette-renders-are-typed-and-distinct",
                () => grayscale.Palette == C3DHeightImagePalette.Grayscale
                      && thermal.Palette == C3DHeightImagePalette.Thermal
                      && grayscale.PixelSha256 != thermal.PixelSha256
                      && grayscale.PixelSha256 != frame.PixelSha256
                      && thermal.PixelSha256 != frame.PixelSha256,
                $"height={frame.PixelSha256},gray={grayscale.PixelSha256},thermal={thermal.PixelSha256}"),
            Check(
                "invalid-manual-range-rejected",
                () =>
                {
                    try
                    {
                        frame.CreateDisplayFrame(
                            C3DHeightImagePalette.Height,
                            50.0,
                            20.0);
                        return false;
                    }
                    catch (ArgumentOutOfRangeException)
                    {
                        return true;
                    }
                },
                "maximum below minimum fails closed"),
            Check(
                "source-statistics-preserved",
                () => frame.ValidCount == 5
                      && frame.MissingCount == 1
                      && frame.Minimum == 10.0
                      && frame.Maximum == 60.0
                      && frame.Mean == 36.0,
                $"valid={frame.ValidCount},missing={frame.MissingCount},min={frame.Minimum},max={frame.Maximum},mean={frame.Mean}"),
            Check(
                "invalid-cell-map-bytes-are-exposed",
                () => frame.InvalidCellMap.Width == 3
                      && frame.InvalidCellMap.Height == 2
                      && frame.InvalidCellMap.MissingCellCount == 1
                      && frame.InvalidCellMap.PackedBits.Span.SequenceEqual(
                          new byte[] { 0x04 }),
                $"bytes={Convert.ToHexString(frame.InvalidCellMap.PackedBits.Span)},missing={frame.InvalidCellMap.MissingCellCount}"),
            Check(
                "invalid-cell-coordinate-matches-height-image",
                () => frame.InvalidCellMap.TryIsMissing(2, 0, out var isMissing)
                      && isMissing
                      && frame.TryGetCell(2, 0, out var cell)
                      && !cell.IsValid,
                "pixel=(2,0) is row=0,column=2 and missing in both owners"),
            Check(
                "source-quality-mask-identity-matches-height-image",
                () => quality.Coverage.InvalidCellMask.ByteLength
                        == frame.InvalidCellMap.PackedByteLength
                      && string.Equals(
                          quality.Coverage.InvalidCellMask.Sha256,
                          frame.InvalidCellMap.Sha256,
                          StringComparison.OrdinalIgnoreCase),
                $"qualitySha={quality.Coverage.InvalidCellMask.Sha256},heightImageSha={frame.InvalidCellMap.Sha256}"),
            Check(
                "visible-invalid-overlay-count-matches-source-quality",
                () => invalidOverlay.InvalidOverlayMode
                        == C3DHeightImageInvalidOverlayMode.Visible
                      && invalidOverlay.InvalidOverlayPixelCount
                        == frame.InvalidCellMap.MissingCellCount
                      && invalidOverlay.InvalidOverlayPixelCount
                        == quality.Coverage.MissingSampleCount,
                $"overlay={invalidOverlay.InvalidOverlayPixelCount},map={frame.InvalidCellMap.MissingCellCount},quality={quality.Coverage.MissingSampleCount}"),
            Check(
                "visible-invalid-overlay-retains-mask-identity",
                () => string.Equals(
                          invalidOverlay.InvalidCellMapSha256,
                          frame.InvalidCellMap.Sha256,
                          StringComparison.OrdinalIgnoreCase)
                      && string.Equals(
                          invalidOverlay.InvalidCellMapSha256,
                          quality.Coverage.InvalidCellMask.Sha256,
                          StringComparison.OrdinalIgnoreCase),
                $"overlaySha={invalidOverlay.InvalidCellMapSha256},mapSha={frame.InvalidCellMap.Sha256}"),
            Check(
                "visible-invalid-overlay-colors-only-missing-cell",
                () => invalidOverlay.Bgra32Pixels.Span[8..12]
                          .SequenceEqual(new byte[] { 0x8D, 0x1D, 0xE1, 0xFF })
                      && invalidOverlay.Bgra32Pixels.Span[..8]
                          .SequenceEqual(frame.Bgra32Pixels.Span[..8])
                      && invalidOverlay.Bgra32Pixels.Span[12..]
                          .SequenceEqual(frame.Bgra32Pixels.Span[12..]),
                $"missingPixel={Convert.ToHexString(invalidOverlay.Bgra32Pixels.Span[8..12])}"),
            Check(
                "visible-invalid-overlay-is-deterministic-and-view-only",
                () => invalidOverlay.PixelSha256 == invalidOverlayReplay.PixelSha256
                      && invalidOverlay.Bgra32Pixels.Span.SequenceEqual(
                          invalidOverlayReplay.Bgra32Pixels.Span)
                      && invalidOverlay.PixelSha256 != frame.PixelSha256
                      && frame.DefaultDisplayFrame.InvalidOverlayMode
                        == C3DHeightImageInvalidOverlayMode.Hidden
                      && frame.DefaultDisplayFrame.InvalidOverlayPixelCount == 0
                      && frame.PixelSha256 == ExpectedPixelSha256,
                $"overlay={invalidOverlay.PixelSha256},replay={invalidOverlayReplay.PixelSha256},source={frame.PixelSha256}"),
            Check(
                "out-of-range-coordinate-rejected",
                () => !frame.TryGetCell(-1, 0, out _)
                      && !frame.TryGetCell(0, -1, out _)
                      && !frame.TryGetCell(frame.Width, 0, out _)
                      && !frame.TryGetCell(0, frame.Height, out _),
                "negative and end-exclusive bounds rejected"),
            Check(
                "cancellation-fails-closed",
                () =>
                {
                    using var cancellation = new CancellationTokenSource();
                    cancellation.Cancel();
                    try
                    {
                        C3DHeightImageFrame.Create(source, cancellation.Token);
                        return false;
                    }
                    catch (OperationCanceledException)
                    {
                        return true;
                    }
                },
                "pre-cancelled token rejects frame creation")
        };

        var passedCount = cases.Count(item => item.Passed);
        var status = passedCount == cases.Length ? "Pass" : "Fail";
        var fullReportPath = Path.GetFullPath(reportPath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullReportPath) ?? Environment.CurrentDirectory);
        File.WriteAllLines(
            fullReportPath,
            [
                $"C3DHeightImageVerification|{status}|cases={cases.Length}|passed={passedCount}|failed={cases.Length - passedCount}",
                $"Contract|mapping={C3DHeightImageFrame.CoordinateMapping}|wpfNeutral=true|viewOnly=true",
                $"Frame|width={frame.Width}|height={frame.Height}|valid={frame.ValidCount}|missing={frame.MissingCount}|pixelSha256={frame.PixelSha256}",
                $"InvalidCellMap|bytes={frame.InvalidCellMap.PackedByteLength}|missing={frame.InvalidCellMap.MissingCellCount}|sha256={frame.InvalidCellMap.Sha256}",
                ..cases.Select(item => $"Case|{item.Name}|{(item.Passed ? "Pass" : "Fail")}|{Clean(item.Evidence)}")
            ]);
        Console.WriteLine($"C3D Height Image verification: {status} ({passedCount}/{cases.Length})");
        return passedCount == cases.Length ? 0 : 5;
    }

    private static string CellEvidence(C3DHeightImageFrame frame, int pixelX, int pixelY) =>
        frame.TryGetCell(pixelX, pixelY, out var cell)
            ? $"pixel={pixelX},{pixelY};row={cell.Row};column={cell.Column};valid={cell.IsValid};raw={cell.RawHeight}"
            : $"pixel={pixelX},{pixelY};missing";

    private static VerificationCase Check(string name, Func<bool> verify, string evidence)
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

    private static string Clean(string value) =>
        value.Replace('|', '/').Replace('\r', ' ').Replace('\n', ' ');

    private sealed record VerificationCase(string Name, bool Passed, string Evidence);
}
