using System.Globalization;
using System.Text;
using OpenVisionLab.ThreeD.Data;

namespace OpenVisionLab.ThreeD.Viewer.Rendering;

/// <summary>
/// Builds the display-only polyline path for a C3D section profile.
/// Physical units and measurement decisions stay with the source/inspection
/// owners; this class only formats finite profile samples for presentation.
/// </summary>
internal static class C3DSectionProfilePathBuilder
{
    public static string Build(IReadOnlyList<HeightGridPoint> samples, double min, double max)
    {
        const double chartWidth = 240.0;
        const double chartHeight = 54.0;
        const double padding = 3.0;
        var span = Math.Max(0.001, max - min);
        if (samples.Count == 0) return string.Empty;
        var first = samples[0];
        var last = samples[^1];
        var steps = Math.Max(Math.Abs(last.Row - first.Row), Math.Abs(last.Column - first.Column));
        var builder = new StringBuilder();
        var previousStep = -2;
        for (var index = 0; index < samples.Count; index++)
        {
            var sample = samples[index];
            var step = Math.Max(Math.Abs(sample.Row - first.Row), Math.Abs(sample.Column - first.Column));
            var x = steps == 0 ? 0.0 : chartWidth * step / steps;
            var y = padding + (1.0 - ((sample.RawValue - min) / span)) * (chartHeight - padding * 2.0);
            builder.Append(step == previousStep + 1 ? " L " : " M ");
            builder.Append(x.ToString("F1", CultureInfo.InvariantCulture));
            builder.Append(',');
            builder.Append(y.ToString("F1", CultureInfo.InvariantCulture));
            previousStep = step;
        }

        return builder.ToString();
    }
}
