namespace OpenVisionLab.ThreeD.Core;

/// <summary>
/// Zero-preserving display contract for a signed current-minus-background grid.
/// The signed artifact remains the source of truth; rendered pixels are only a
/// presentation projection and must not be written back as ordinary C3D.
/// </summary>
public static class C3DSignedHeightDeltaViewerContract
{
    public const string CurrentSchemaVersion = "1.0";
    public const string ContractName = "SignedDeltaHeightFieldViewer";
    public const string CoordinateMapping =
        "pixelX=column;pixelY=row;no-flip;one-source-cell-per-pixel";
    public const string MissingValueEncoding =
        "non-finite=missing;finite-zero=valid";
    public const string DisplayRangePolicy =
        "symmetric-zero-centered;amplitude=max-absolute-finite;zero-only-amplitude=1";
    public const string PixelEncoding =
        "BGRA32;alpha=255;PNG-display-only;not-C3D-source";

    public static bool TryGetDisplayRange(
        IReadOnlyList<double> values,
        out double minimum,
        out double maximum,
        out string message)
    {
        ArgumentNullException.ThrowIfNull(values);
        var amplitude = 0.0;
        var finiteCount = 0;
        foreach (var value in values)
        {
            if (!double.IsFinite(value))
            {
                continue;
            }

            finiteCount++;
            amplitude = Math.Max(amplitude, Math.Abs(value));
        }

        if (finiteCount == 0 || !double.IsFinite(amplitude))
        {
            minimum = 0.0;
            maximum = 0.0;
            message = "Signed-delta viewer range requires at least one finite value.";
            return false;
        }

        if (amplitude == 0.0)
        {
            amplitude = 1.0;
        }

        minimum = -amplitude;
        maximum = amplitude;
        message = "Signed-delta viewer range is symmetric around finite zero.";
        return true;
    }
}
