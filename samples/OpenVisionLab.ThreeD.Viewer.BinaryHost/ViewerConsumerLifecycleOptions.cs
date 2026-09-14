using System.Globalization;
using System.IO;

namespace OpenVisionLab.ThreeD.Viewer.BinaryHost;

/// <summary>
/// Parses the independent-consumer lifecycle options without owning WPF or
/// Viewer state.
/// </summary>
internal sealed record ViewerConsumerLifecycleOptions(
    string ReportPath,
    string C3DPath,
    string MeshPath,
    string PointCloudPath,
    int RecreateCycles,
    int WindowCloseCycles,
    string? SmokeContractPath,
    bool RequireHardwareOpenGL,
    bool RequireImportedTextureRelease,
    string? GpuPostCloseObservationBarrierPath)
{
    public static ViewerConsumerLifecycleOptions Parse(
        string[] args,
        string reportPath)
    {
        return new ViewerConsumerLifecycleOptions(
            Path.GetFullPath(reportPath),
            GetRequiredPath(args, "--consumer-c3d"),
            GetRequiredPath(args, "--consumer-mesh"),
            GetRequiredPath(args, "--consumer-pointcloud"),
            GetCycleCount(args),
            GetWindowCloseCycles(args),
            GetOptionalPath(args, "--smoke-contracts"),
            HasFlag(args, "--consumer-require-hardware-opengl"),
            HasFlag(args, "--consumer-require-texture-release"),
            GetOptionalPath(args, "--consumer-gpu-post-close-observation-barrier"));
    }

    private static int GetCycleCount(string[] args)
    {
        var value = GetArgumentValue(args, "--consumer-lifecycle-recreate-count");
        if (value is null)
        {
            return 10;
        }

        if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var cycles)
            || cycles < 10
            || cycles > 100)
        {
            throw new ArgumentException(
                "--consumer-lifecycle-recreate-count must be an integer from 10 through 100.",
                nameof(args));
        }

        return cycles;
    }

    private static int GetWindowCloseCycles(string[] args)
    {
        var value = GetArgumentValue(args, "--consumer-window-close-cycles");
        if (value is null)
        {
            return 0;
        }

        if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var cycles)
            || cycles < 0
            || cycles > 20)
        {
            throw new ArgumentException(
                "--consumer-window-close-cycles must be an integer from 0 through 20.",
                nameof(args));
        }

        return cycles;
    }

    private static string GetRequiredPath(string[] args, string name)
    {
        var value = GetOptionalPath(args, name);
        return value ?? throw new ArgumentException(
            $"The independent consumer lifecycle requires {name}.",
            nameof(args));
    }

    private static string? GetOptionalPath(string[] args, string name)
    {
        var value = GetArgumentValue(args, name);
        return string.IsNullOrWhiteSpace(value) ? null : Path.GetFullPath(value);
    }

    private static string? GetArgumentValue(string[] args, string name)
    {
        var index = Array.IndexOf(args, name);
        return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
    }

    private static bool HasFlag(string[] args, string name) =>
        args.Any(argument => string.Equals(argument, name, StringComparison.OrdinalIgnoreCase));
}
