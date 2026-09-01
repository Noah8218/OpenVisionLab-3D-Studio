using System.Numerics;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Data;
using OpenVisionLab.ThreeD.Viewer.Models;

namespace OpenVisionLab.ThreeD.Viewer.Teaching;

/// <summary>
/// Prepares the source-bound portion of a teaching capture without a WPF
/// control, ViewModel, OpenGL context, or rendering side effect.
/// </summary>
internal static class TeachingCapturePreparation
{
    public static TeachingCapturePreparationResult Prepare(
        TeachingCaptureRequest request,
        ToolRecipeSelection? initialSelection,
        bool c3dSourceVisible,
        TeachingCaptureSourceSnapshot? c3dSource,
        C3DTransformedHeightField? transformedHeightField)
    {
        ArgumentNullException.ThrowIfNull(request);
        var isTransformedHeightField = string.Equals(
            request.SourceBinding.Format,
            "TransformedHeightField",
            StringComparison.Ordinal);
        if (isTransformedHeightField)
        {
            if (transformedHeightField is null)
            {
                return Failure(
                    "The owned Published TransformedHeightField must be visible before teaching capture.");
            }

            var verification = ToolRecipeSelectionSourceBindingVerifier.Verify(
                transformedHeightField,
                request.SourceBinding);
            if (!verification.IsCurrent)
            {
                return Failure(verification.Message);
            }
        }
        else if (!c3dSourceVisible || c3dSource is null)
        {
            return Failure("A visible C3D source is required before teaching capture.");
        }

        if (!isTransformedHeightField
            && (!string.Equals(
                    request.SourceBinding.Format,
                    "C3D",
                    StringComparison.OrdinalIgnoreCase)
                || request.SourceBinding.GridWidth != c3dSource!.Value.Width
                || request.SourceBinding.GridHeight != c3dSource.Value.Height))
        {
            return Failure(
                "Teaching capture source format or grid dimensions do not match the loaded C3D source.");
        }

        if (!IsSha256(request.SourceBinding.ContentSha256))
        {
            return Failure("Teaching capture requires a valid C3D source SHA-256 binding.");
        }

        if (!isTransformedHeightField
            && !string.Equals(
                request.SourceBinding.ContentSha256,
                c3dSource!.Value.ContentSha256,
                StringComparison.OrdinalIgnoreCase))
        {
            return Failure("Teaching capture source SHA-256 does not match the loaded C3D bytes.");
        }

        IReadOnlyList<ToolRecipeSelectionPoint>? initialPoints = null;
        ToolRecipeGridCircle? initialGridCircle = null;
        ToolRecipeGridPolygon? initialGridPolygon = null;
        if (!isTransformedHeightField
            && initialSelection?.GridRectangle is { } initialRectangle)
        {
            var geometry = TryCreateGridRectanglePoints(c3dSource!.Value, initialRectangle);
            if (!geometry.IsValid)
            {
                return Failure(geometry.Message);
            }

            initialPoints = geometry.Points;
        }
        else if (!isTransformedHeightField
                 && initialSelection?.GridCircle is { } circle)
        {
            var geometry = TryCreateGridCirclePoints(c3dSource!.Value, circle);
            if (!geometry.IsValid)
            {
                return Failure(geometry.Message);
            }

            initialPoints = geometry.Points;
            initialGridCircle = circle;
        }
        else if (!isTransformedHeightField
                 && initialSelection?.GridPolygon is { } polygon)
        {
            var geometry = TryCreateGridPolygonPoints(c3dSource!.Value, polygon);
            if (!geometry.IsValid)
            {
                return Failure(geometry.Message);
            }

            initialPoints = geometry.Points;
            initialGridPolygon = polygon;
        }

        return new TeachingCapturePreparationResult(
            true,
            string.Empty,
            initialPoints,
            initialGridCircle,
            initialGridPolygon);
    }

    public static TeachingCaptureGeometryResult TryCreateGridRectanglePoints(
        TeachingCaptureSourceSnapshot source,
        ToolRecipeGridRectangle rectangle)
    {
        if (rectangle.Row < 0
            || rectangle.Column < 0
            || rectangle.RowCount <= 0
            || rectangle.ColumnCount <= 0
            || (long)rectangle.Row + rectangle.RowCount > source.Height
            || (long)rectangle.Column + rectangle.ColumnCount > source.Width)
        {
            return GeometryFailure("The Surface ROI must stay inside the loaded C3D source grid.");
        }

        return GeometrySuccess(
        [
            CreateSelectionPoint(source, rectangle.Row, rectangle.Column),
            CreateSelectionPoint(
                source,
                rectangle.Row + rectangle.RowCount - 1,
                rectangle.Column + rectangle.ColumnCount - 1)
        ]);
    }

    public static TeachingCaptureGeometryResult TryCreateGridCirclePoints(
        TeachingCaptureSourceSnapshot source,
        ToolRecipeGridCircle circle)
    {
        if (ToolRecipeGridCircleGeometry.Validate(
                circle,
                source.Width,
                source.Height).Count > 0)
        {
            return GeometryFailure("The Circular ROI must stay inside the loaded C3D source grid.");
        }

        var boundaryColumn = Math.Clamp(
            circle.CenterColumn + Math.Max(1, (int)Math.Floor(circle.Radius)),
            0,
            source.Width - 1);
        return GeometrySuccess(
        [
            CreateSelectionPoint(source, circle.CenterRow, circle.CenterColumn),
            CreateSelectionPoint(source, circle.CenterRow, boundaryColumn)
        ]);
    }

    public static TeachingCaptureGeometryResult TryCreateGridPolygonPoints(
        TeachingCaptureSourceSnapshot source,
        ToolRecipeGridPolygon polygon)
    {
        if (ToolRecipeGridPolygonGeometry.Validate(
                polygon,
                source.Width,
                source.Height).Count > 0)
        {
            return GeometryFailure(
                "The polygon ROI must stay finite, ordered, non-degenerate, and inside the loaded C3D source grid.");
        }

        return GeometrySuccess(
            polygon.Vertices
                .Select(vertex => CreateSelectionPoint(
                    source,
                    Math.Clamp(
                        (int)Math.Round(vertex.Row, MidpointRounding.AwayFromZero),
                        0,
                        source.Height - 1),
                    Math.Clamp(
                        (int)Math.Round(vertex.Column, MidpointRounding.AwayFromZero),
                        0,
                        source.Width - 1)))
                .ToArray());
    }

    public static ToolRecipeSelectionPoint CreateGridPoint(
        TeachingCaptureSourceSnapshot source,
        int row,
        int column)
    {
        if (row < 0 || row >= source.Height)
        {
            throw new ArgumentOutOfRangeException(nameof(row));
        }

        if (column < 0 || column >= source.Width)
        {
            throw new ArgumentOutOfRangeException(nameof(column));
        }

        return CreateSelectionPoint(source, row, column);
    }

    private static TeachingCapturePreparationResult Failure(string message) =>
        new(false, message, null, null, null);

    private static TeachingCaptureGeometryResult GeometryFailure(string message) =>
        new(false, message, null);

    private static TeachingCaptureGeometryResult GeometrySuccess(
        IReadOnlyList<ToolRecipeSelectionPoint> points) =>
        new(true, string.Empty, points);

    private static ToolRecipeSelectionPoint CreateSelectionPoint(
        TeachingCaptureSourceSnapshot source,
        int row,
        int column)
    {
        var position = new Vector3(
            (column - (source.Width - 1) / 2.0f) * source.HorizontalScale,
            0,
            (row - (source.Height - 1) / 2.0f) * source.HorizontalScale);
        return new ToolRecipeSelectionPoint(
            new ToolRecipeGridCellLocator("grid-cell", row, column),
            new ToolRecipeXyz(position.X, position.Y, position.Z),
            source.Mean);
    }

    private static bool IsSha256(string value) =>
        value.Length == 64 && value.All(Uri.IsHexDigit);
}

internal readonly record struct TeachingCaptureSourceSnapshot(
    int Width,
    int Height,
    float HorizontalScale,
    double Mean,
    string ContentSha256)
{
    public static TeachingCaptureSourceSnapshot From(C3DHeightGrid source) =>
        new(
            source.Width,
            source.Height,
            source.HorizontalScale,
            source.Mean,
            source.ContentSha256);
}

internal sealed record TeachingCapturePreparationResult(
    bool IsValid,
    string Message,
    IReadOnlyList<ToolRecipeSelectionPoint>? InitialPoints,
    ToolRecipeGridCircle? InitialGridCircle,
    ToolRecipeGridPolygon? InitialGridPolygon);

internal sealed record TeachingCaptureGeometryResult(
    bool IsValid,
    string Message,
    IReadOnlyList<ToolRecipeSelectionPoint>? Points);
