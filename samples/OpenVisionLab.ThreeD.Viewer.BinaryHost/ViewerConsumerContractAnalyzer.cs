using System.Globalization;

namespace OpenVisionLab.ThreeD.Viewer.BinaryHost;

/// <summary>
/// Evaluates the stable text contracts emitted by the independent Viewer
/// consumer smoke path. It owns no WPF or Viewer state.
/// </summary>
internal sealed class ViewerConsumerContractAnalyzer
{
    private readonly bool requireHardwareOpenGL;
    private readonly bool requireImportedTextureRelease;

    public ViewerConsumerContractAnalyzer(
        bool requireHardwareOpenGL,
        bool requireImportedTextureRelease)
    {
        this.requireHardwareOpenGL = requireHardwareOpenGL;
        this.requireImportedTextureRelease = requireImportedTextureRelease;
    }

    public ViewerConsumerHardwareRenderObservation AnalyzeHardwareRenderPath(
        string c3dContract,
        string meshContract)
    {
        var capabilities = GetContractLine(c3dContract, "OpenGLCapabilities|");
        var renderProxy = GetContractLine(c3dContract, "C3DRenderProxy|loaded=True|");
        var c3dHardware = capabilities is not null
            && !capabilities.Contains("renderer=GDI Generic", StringComparison.Ordinal)
            && !capabilities.Contains("renderer=(pending)", StringComparison.Ordinal)
            && capabilities.Contains("c3dPath=VBO+IBO+DrawElements", StringComparison.Ordinal)
            && capabilities.Contains("fallbacks=0", StringComparison.Ordinal)
            && renderProxy is not null
            && renderProxy.Contains("gpuBufferReady=True", StringComparison.Ordinal);
        var meshLine = GetContractLine(meshContract, "GLB|loaded=True|");
        var textureUploaded = !requireImportedTextureRelease
            || (meshLine is not null
                && string.Equals(
                    GetContractFieldValue(meshLine, "hasTexture"),
                    "True",
                    StringComparison.OrdinalIgnoreCase)
                && GetContractInt(meshLine, "textureUploads") > 0);
        var passed = c3dHardware && textureUploaded;
        return new ViewerConsumerHardwareRenderObservation(
            passed,
            $"c3dHardware={c3dHardware}|textureUploaded={textureUploaded}|capabilities={capabilities ?? "missing"}|renderProxy={renderProxy ?? "missing"}|mesh={meshLine ?? "missing"}");
    }

    public ViewerConsumerResourceRetirementObservation AnalyzeResourceRetirement(string contract)
    {
        var line = GetContractLine(contract, "OpenGLResourceLifetime|");
        if (!requireHardwareOpenGL)
        {
            return new ViewerConsumerResourceRetirementObservation(
                true,
                $"hardwareRequired=False|contract={line ?? "missing"}");
        }

        if (line is null)
        {
            return new ViewerConsumerResourceRetirementObservation(
                false,
                "hardwareRequired=True|contract=missing");
        }

        var passed = GetContractBool(line, "disposed")
            && GetContractBool(line, "managedHandlesCleared")
            && GetContractInt(line, "c3dGpuReleases") > 0
            && GetContractInt(line, "c3dGpuReleaseFailures") == 0
            && GetContractInt(line, "meshTextureReleases") >= (requireImportedTextureRelease ? 1 : 0)
            && GetContractInt(line, "meshTextureReleaseFailures") == 0
            && GetContractInt(line, "displayListReleaseFailures") == 0
            && GetContractInt(line, "retirementAttempts") > 0
            && GetContractInt(line, "retirementCallbacks") > 0
            && GetContractInt(line, "retirementContextUnavailable") == 0
            && GetContractInt(line, "retirementFailures") == 0;
        passed = passed
            && GetContractBool(line, "renderContextDisposeAttempted")
            && GetContractBool(line, "renderContextDisposed")
            && GetContractInt(line, "renderContextDisposeAttempts") == 1
            && GetContractInt(line, "renderContextDisposeFailures") == 0
            && !GetContractBool(line, "renderContextHandleActive");
        return new ViewerConsumerResourceRetirementObservation(
            passed,
            $"hardwareRequired=True|contract={line}");
    }

    private static string? GetContractLine(string content, string prefix) =>
        content
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault(line => line.StartsWith(prefix, StringComparison.Ordinal));

    private static string? GetContractFieldValue(string line, string field)
    {
        foreach (var segment in line.Split('|', StringSplitOptions.RemoveEmptyEntries))
        {
            if (segment.StartsWith(field + "=", StringComparison.Ordinal))
            {
                return segment[(field.Length + 1)..];
            }
        }

        return null;
    }

    private static int GetContractInt(string line, string field) =>
        int.TryParse(
            GetContractFieldValue(line, field),
            NumberStyles.Integer,
            CultureInfo.InvariantCulture,
            out var value)
            ? value
            : 0;

    private static bool GetContractBool(string line, string field) =>
        bool.TryParse(GetContractFieldValue(line, field), out var value) && value;
}

internal sealed record ViewerConsumerHardwareRenderObservation(bool Passed, string Details);

internal sealed record ViewerConsumerResourceRetirementObservation(bool Passed, string Details);
