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
        var stride = Math.Max(1, (int)Math.Ceiling(samples.Count / 80.0));
        var reduced = samples.Where((_, index) => index % stride == 0).ToList();
        if (reduced[^1] != samples[^1])
        {
            reduced.Add(samples[^1]);
        }

        var builder = new StringBuilder();
        for (var index = 0; index < reduced.Count; index++)
        {
            var sample = reduced[index];
            var x = reduced.Count == 1 ? 0.0 : chartWidth * index / (reduced.Count - 1);
            var y = padding + (1.0 - ((sample.RawValue - min) / span)) * (chartHeight - padding * 2.0);
            builder.Append(index == 0 ? "M " : " L ");
            builder.Append(x.ToString("F1", CultureInfo.InvariantCulture));
            builder.Append(',');
            builder.Append(y.ToString("F1", CultureInfo.InvariantCulture));
        }

        return builder.ToString();
    }
}
