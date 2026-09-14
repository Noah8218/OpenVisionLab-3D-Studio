using System.IO;

namespace OpenVisionLab.ThreeD.Viewer.BinaryHost;

/// <summary>
/// Validates consumer source files and prepares report output directories
/// without owning WPF application or Viewer state.
/// </summary>
internal sealed class ViewerConsumerInputPreconditionValidator
{
    private readonly string reportPath;
    private readonly string c3DPath;
    private readonly string meshPath;
    private readonly string pointCloudPath;
    private readonly string? smokeContractPath;

    public ViewerConsumerInputPreconditionValidator(
        string reportPath,
        string c3DPath,
        string meshPath,
        string pointCloudPath,
        string? smokeContractPath)
    {
        this.reportPath = reportPath;
        this.c3DPath = c3DPath;
        this.meshPath = meshPath;
        this.pointCloudPath = pointCloudPath;
        this.smokeContractPath = smokeContractPath;
    }

    public void Validate()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(reportPath)!);
        RequireFile(c3DPath, "C3D source");
        RequireFile(meshPath, "mesh source");
        RequireFile(pointCloudPath, "point-cloud source");
        if (smokeContractPath is not null)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(smokeContractPath)!);
        }
    }

    private static void RequireFile(string path, string label)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"The {label} was not found.", path);
        }
    }
}
