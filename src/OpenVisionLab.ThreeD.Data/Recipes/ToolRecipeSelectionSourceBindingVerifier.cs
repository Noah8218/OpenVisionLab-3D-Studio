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
    public static ToolRecipeSelectionSourceBinding FromTransformedHeightField(C3DTransformedHeightField output)
    {
        ArgumentNullException.ThrowIfNull(output);
        return new ToolRecipeSelectionSourceBinding(
            "TransformedHeightField",
            output.ContentSha256,
            output.ColumnCount,
            output.RowCount,
            output.OutputEntityId,
            output.RootSourceSha256,
            output.ReferenceUnit,
            output.ReferenceFrameId);
    }

    public static ToolRecipeSelectionSourceBindingVerificationResult Verify(
        C3DTransformedHeightField output,
        ToolRecipeSelectionSourceBinding binding)
    {
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(binding);
        var current = FromTransformedHeightField(output);
        var matches = BindingsEqual(current, binding);
        return new ToolRecipeSelectionSourceBindingVerificationResult(
            matches,
            matches
                ? $"TransformedHeightField selection binding is current ({current.GridWidth} x {current.GridHeight}, owner {current.OwnerEntityId}, SHA-256 {current.ContentSha256})."
                : "TransformedHeightField selection binding is stale because owner, bytes, grid, unit, frame, or root-source identity changed.",
            current);
    }

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

    public static bool BindingsEqual(
        ToolRecipeSelectionSourceBinding first,
        ToolRecipeSelectionSourceBinding second) =>
        string.Equals(first.Format, second.Format, StringComparison.OrdinalIgnoreCase)
        && string.Equals(first.ContentSha256, second.ContentSha256, StringComparison.OrdinalIgnoreCase)
        && first.GridWidth == second.GridWidth
        && first.GridHeight == second.GridHeight
        && string.Equals(first.OwnerEntityId, second.OwnerEntityId, StringComparison.OrdinalIgnoreCase)
        && string.Equals(first.RootSourceContentSha256, second.RootSourceContentSha256, StringComparison.OrdinalIgnoreCase)
        && string.Equals(first.Unit, second.Unit, StringComparison.Ordinal)
        && string.Equals(first.FrameId, second.FrameId, StringComparison.Ordinal);
}
