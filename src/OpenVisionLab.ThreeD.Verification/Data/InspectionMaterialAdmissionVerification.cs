using System.IO;
using System.Text.Json;
using OpenVisionLab.ThreeD.Data;

namespace OpenVisionLab.ThreeD.Verification.Data;

internal static class InspectionMaterialAdmissionVerification
{
    public static bool Verify(string reportPath, out string summary)
    {
        var lines = new List<string> { "Boundary|External synthetic material admission through Studio snapshot and height-image adapters. Not texture-pair GPU support, physical calibration, thickness/warpage accuracy, or SDK numerical qualification." };
        var passed = 0;
        var total = 0;
        var caseCount = 0;
        void Check(string name, bool condition)
        {
            total++;
            if (condition) passed++;
            lines.Add($"{(condition ? "PASS" : "FAIL")} | {name}");
        }

        try
        {
            reportPath = Path.GetFullPath(reportPath);
            var folder = Path.GetDirectoryName(reportPath)!;
            Directory.CreateDirectory(folder);
            var inputs = Path.GetFullPath(File.ReadAllText(Path.Combine(folder, "materials-root.txt")).Trim());
            foreach (var pairPath in Directory.EnumerateFiles(inputs, "texture-pair.json", SearchOption.AllDirectories).Order(StringComparer.Ordinal))
            {
                caseCount++;
                var caseFolder = Path.GetDirectoryName(pairPath)!;
                using var metadata = JsonDocument.Parse(File.ReadAllText(Path.Combine(caseFolder, "metadata.json")));
                var root = metadata.RootElement;
                var id = root.GetProperty("id").GetString()!;
                var grid = root.GetProperty("grid");
                foreach (var role in new[] { "upper", "lower", "metrology_upper", "axial_thickness" })
                {
                    try
                    {
                        var expected = root.GetProperty("files").GetProperty(role);
                        var source = Path.Combine(caseFolder, expected.GetProperty("path").GetString()!);
                        var snapshot = C3DHeightFieldSnapshot.LoadIdentified(source, id + "." + role,
                            grid.GetProperty("studioHeightUnit").GetString()!, grid.GetProperty("frameId").GetString()!);
                        Check(id + "/" + role + " Studio snapshot matches supplied source identity and validity", snapshot.Width == expected.GetProperty("width").GetInt32()
                            && snapshot.Height == expected.GetProperty("height").GetInt32()
                            && snapshot.ByteLength == expected.GetProperty("byteLength").GetInt64()
                            && string.Equals(snapshot.ContentSha256, expected.GetProperty("sha256").GetString(), StringComparison.OrdinalIgnoreCase)
                            && snapshot.ValidCount == expected.GetProperty("validCount").GetInt32()
                            && snapshot.MissingCount == expected.GetProperty("missingCount").GetInt32());
                        var frame = C3DHeightImageFrame.Create(snapshot);
                        Check(id + "/" + role + " height-image adapter preserves input grid and invalid samples", frame.Width == snapshot.Width && frame.Height == snapshot.Height
                            && frame.ValidCount == snapshot.ValidCount && frame.MissingCount == snapshot.MissingCount
                            && string.Equals(frame.SourceContentSha256, snapshot.ContentSha256, StringComparison.OrdinalIgnoreCase));
                    }
                    catch (Exception exception) { Check(id + "/" + role + " controlled execution: " + exception, false); }
                }
            }
            Check("all supplied industry cases were admitted", caseCount == 30);
        }
        catch (Exception exception) { Check("execution: " + exception, false); }
        summary = $"Inspection material admission|pass={passed == total}|cases={caseCount}|checks={passed}/{total}";
        File.WriteAllLines(reportPath, lines.Append(summary));
        return passed == total;
    }
}
