using System.IO;
using System.Text.Json;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using OpenVisionLab.ThreeD.Data;
using OpenVisionLab.ThreeD.Viewer.Loading;
using OpenVisionLab.ThreeD.Viewer.Rendering;
using OpenVisionLab.ThreeD.Viewer.ViewModels;

namespace OpenVisionLab.ThreeD.Viewer.Verification;

public static class C3DTextureVerification
{
    public static bool Verify(string reportPath, out string summary)
    {
        var root = Path.GetDirectoryName(Path.GetFullPath(reportPath))!;
        Directory.CreateDirectory(root);
        var lines = new List<string> { "Boundary|Display image admission, source identity, sample-centre UV and preference restoration; no SDK inspection qualification." };
        var passed = 0;
        void Check(string name, bool condition)
        {
            lines.Add($"{(condition ? "PASS" : "FAIL")} | {name}");
            if (!condition) throw new InvalidOperationException(name);
            passed++;
        }
        try
        {
            var mapPath = Path.Combine(root, "texture-grid.C3D");
            WriteGrid(mapPath, false);
            var map = C3DHeightGrid.Load(mapPath, maxRenderedPoints: 1000);
            var imagePath = Path.Combine(root, "quadrants.png");
            WriteImage(imagePath);
            var texture = new C3DTextureViewModel();
            Check("attachment is disabled without a trusted C3D source", !texture.CanLoad && !texture.TryLoad(imagePath));
            texture.SetSource(map);
            Check("explicit attachment selects a visible decoded image", texture.TryLoad(imagePath) && texture.IsVisible && texture.ImageName == "quadrants.png");
            var image = texture.Image!;
            Check("decoder retains top-left red and bottom-right yellow pixels", image.Width == 8 && image.Height == 6
                && image.Pixels[0] == 0 && image.Pixels[1] == 0 && image.Pixels[2] == 255
                && image.Pixels[^4] == 0 && image.Pixels[^3] == 255 && image.Pixels[^2] == 255);
            using (var unlocked = new FileStream(imagePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                Check("image admission releases the file handle", unlocked.Length > 0);
            var badPath = Path.Combine(root, "truncated.png");
            File.WriteAllBytes(badPath, [137, 80, 78, 71, 13, 10, 26, 10]);
            Check("truncated replacement retains the last valid image and exposes the error", !texture.TryLoad(badPath)
                && ReferenceEquals(image, texture.Image) && texture.IsVisible && texture.Error.Length > 0);
            Check("valid selection recovers from decode failure", texture.TryLoad(imagePath) && texture.Error.Length == 0);
            Check("visibility can be changed without replacing pixels", texture.SetVisible(false) && texture.HasImage && !texture.IsVisible
                && texture.SetVisible(true) && texture.IsVisible);

            var uv = C3DTextureImage.Coordinate(0, 0, 8, 6);
            Check("first sample uses the top-left pixel centre", Math.Abs(uv.X - .0625) < 1e-7 && Math.Abs(uv.Y - 1d / 12) < 1e-7);
            uv = C3DTextureImage.Coordinate(5, 7, 8, 6);
            Check("last sample has no mirror, vertical flip or edge repeat", Math.Abs(uv.X - .9375) < 1e-7 && Math.Abs(uv.Y - 11d / 12) < 1e-7);
            var sparse = map.WithMaxRenderedPoints(12);
            var coordinates = C3DTextureImage.CreateCoordinates(sparse);
            Check("sparse render UV still uses full source dimensions", sparse.PointStride > 1 && coordinates.Length == sparse.Points.Length * 2
                && sparse.Points.Select((point, index) => Math.Abs(coordinates[index * 2] - (point.Column + .5) / 8) < 1e-7
                    && Math.Abs(coordinates[index * 2 + 1] - (point.Row + .5) / 6) < 1e-7).All(value => value));
            var proxy = C3DHeightGridRenderProxy.Create(map);
            Check("invalid cells cannot be vertices in the retained surface topology", !proxy.Points.Any(point => point.Row == 2 && point.Column == 3)
                && proxy.TriangleIndices.All(index => proxy.Points[index].RawValue > 0));

            var settingsPath = Path.Combine(root, "texture-preferences.json");
            File.WriteAllText(settingsPath, JsonSerializer.Serialize(texture.CaptureSettings()));
            var saved = JsonSerializer.Deserialize<Dictionary<string, HeightTextureImageSettings>>(File.ReadAllText(settingsPath))!;
            var reopened = new C3DTextureViewModel();
            reopened.RestoreSettings(saved);
            Check("restoration before source load causes no image action", !reopened.HasImage && !reopened.CanLoad);
            reopened.SetSource(map);
            Check("save, reload and reopen restores the matching visible image", reopened.IsVisible && reopened.Image!.Sha256 == texture.Image!.Sha256);
            var anotherPath = Path.Combine(root, "other-grid.C3D");
            WriteGrid(anotherPath, true);
            var anotherMap = C3DHeightGrid.Load(anotherPath, maxRenderedPoints: 1000);
            reopened.SetSource(anotherMap);
            Check("a different source never inherits the previous image", reopened.CanLoad && !reopened.HasImage && !reopened.IsVisible);
            reopened.SetSource(map);
            Check("returning to the same source restores its own association", reopened.IsVisible);
            reopened.Clear();
            reopened.SetSource(anotherMap);
            reopened.SetSource(map);
            Check("remove is durable across source changes", !reopened.HasImage && reopened.CaptureSettings().Count == 0);
            var stale = saved.ToDictionary(entry => entry.Key, entry => entry.Value with { ImageSha256 = new string('0', 64) });
            reopened.RestoreSettings(stale);
            Check("changed image bytes are rejected on restoration", !reopened.HasImage && !reopened.IsVisible && reopened.Error.Contains("changed", StringComparison.Ordinal));
            reopened.RestoreSettings(saved.ToDictionary(entry => entry.Key, entry => entry.Value with { GridWidth = 9 }));
            Check("incompatible source dimensions are rejected", !reopened.HasImage && reopened.Error.Contains("dimensions", StringComparison.Ordinal));
            reopened.RestoreSettings(saved);
            Check("valid restoration recovers after stale preferences", reopened.IsVisible && reopened.Error.Length == 0);
            reopened.RestoreSettings(null);
            Check("recipe-context reset clears the association", !reopened.HasImage && reopened.CaptureSettings().Count == 0);
            Check("texture operations preserve all original raw samples and content identity", map.ContentSha256 == C3DHeightGrid.Load(mapPath, 1000).ContentSha256
                && map.ReadHeightMapValues().SequenceEqual(C3DHeightGrid.Load(mapPath, 1000).ReadHeightMapValues()));
            summary = $"C3D texture verification: Pass ({passed} checks)";
            File.WriteAllLines(reportPath, lines.Append(summary));
            return true;
        }
        catch (Exception exception)
        {
            summary = $"C3D texture verification: Fail after {passed} checks: {exception}";
            File.WriteAllLines(reportPath, lines.Append(summary));
            return false;
        }
    }

    private static void WriteGrid(string path, bool different)
    {
        using var writer = new BinaryWriter(File.Create(path));
        writer.Write(8);
        writer.Write(6);
        for (var row = 0; row < 6; row++)
            for (var column = 0; column < 8; column++)
                writer.Write(row == 2 && column == 3 ? 0f : different ? 101f : 100f);
    }

    private static void WriteImage(string path)
    {
        var pixels = new byte[8 * 6 * 4];
        for (var row = 0; row < 6; row++)
            for (var column = 0; column < 8; column++)
            {
                var offset = (row * 8 + column) * 4;
                pixels[offset] = row >= 3 && column < 4 ? (byte)255 : (byte)0;
                pixels[offset + 1] = column >= 4 ? (byte)255 : (byte)0;
                pixels[offset + 2] = row < 3 && column < 4 || row >= 3 && column >= 4 ? (byte)255 : (byte)0;
                pixels[offset + 3] = 255;
            }
        var bitmap = BitmapSource.Create(8, 6, 96, 96, PixelFormats.Bgra32, null, pixels, 8 * 4);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(path);
        encoder.Save(stream);
    }
}
