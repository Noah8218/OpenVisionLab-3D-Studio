using OpenVisionLab.ThreeD.Core;

namespace OpenVisionLab.ThreeD.Data;

public sealed record ToolRecipeSelectionSourceBindingVerificationResult(
    bool IsCurrent,
    string Message,
    ToolRecipeSelectionSourceBinding? CurrentBinding);

/// <summary>
/// Reads and compares the exact C3D source identity used by a structured
/// teaching selection. This service never remaps a locator or executes a tool.
/// </summary>
public static class ToolRecipeSelectionSourceBindingVerifier
{
    public static ToolRecipeSelectionSourceBinding ReadIdentity(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var grid = C3DHeightGrid.Load(Path.GetFullPath(path), maxRenderedPoints: 0);
        return new ToolRecipeSelectionSourceBinding(
            "C3D",
            grid.ContentSha256,
            grid.Width,
            grid.Height);
    }

    public static ToolRecipeSelectionSourceBindingVerificationResult Verify(
        string path,
        ToolRecipeSelectionSourceBinding binding)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(binding);

        try
        {
            return Verify(ReadIdentity(path), binding);
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException
            or InvalidDataException
            or ArgumentException
            or NotSupportedException
            or OverflowException)
        {
            return new ToolRecipeSelectionSourceBindingVerificationResult(
                false,
                $"C3D selection source identity could not be read: {exception.Message}",
                null);
        }
    }

    internal static ToolRecipeSelectionSourceBindingVerificationResult Verify(
        ToolRecipeSelectionSourceBinding current,
        ToolRecipeSelectionSourceBinding binding)
    {
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(binding);

        var formatMatches = string.Equals(current.Format, binding.Format, StringComparison.OrdinalIgnoreCase);
        var hashMatches = string.Equals(current.ContentSha256, binding.ContentSha256, StringComparison.OrdinalIgnoreCase);
        var dimensionsMatch = current.GridWidth == binding.GridWidth && current.GridHeight == binding.GridHeight;
        if (formatMatches && hashMatches && dimensionsMatch)
        {
            return new ToolRecipeSelectionSourceBindingVerificationResult(
                true,
                $"C3D selection source binding is current ({current.GridWidth} x {current.GridHeight}, SHA-256 {current.ContentSha256}).",
                current);
        }

        var mismatches = new List<string>();
        if (!formatMatches) mismatches.Add($"format expected {binding.Format}, actual {current.Format}");
        if (!hashMatches) mismatches.Add($"SHA-256 expected {binding.ContentSha256}, actual {current.ContentSha256}");
        if (!dimensionsMatch)
        {
            mismatches.Add(
                $"grid expected {binding.GridWidth} x {binding.GridHeight}, actual {current.GridWidth} x {current.GridHeight}");
        }

        return new ToolRecipeSelectionSourceBindingVerificationResult(
            false,
            $"C3D selection source binding is stale: {string.Join("; ", mismatches)}.",
            current);
    }
}
