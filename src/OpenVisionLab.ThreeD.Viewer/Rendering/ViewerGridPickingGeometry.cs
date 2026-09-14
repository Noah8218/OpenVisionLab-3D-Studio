using System.Numerics;

namespace OpenVisionLab.ThreeD.Viewer.Rendering;

/// <summary>
/// WPF-neutral projection of a pick ray onto a rectangular C3D grid plane.
/// </summary>
internal static class ViewerGridPickingGeometry
{
    public static bool TryMapRayToGrid(
        Vector3 rayOrigin,
        Vector3 rayDirection,
        Vector3 gridOrigin,
        Vector3 rowSpan,
        Vector3 columnSpan,
        int rowCount,
        int columnCount,
        out int row,
        out int column)
    {
        row = 0;
        column = 0;
        if (rowCount < 2 || columnCount < 2)
        {
            return false;
        }

        var normal = Vector3.Cross(rowSpan, columnSpan);
        var denominator = Vector3.Dot(rayDirection, normal);
        if (normal.LengthSquared() < 0.0000001f || Math.Abs(denominator) < 0.000001f)
        {
            return false;
        }

        var distance = Vector3.Dot(gridOrigin - rayOrigin, normal) / denominator;
        if (!float.IsFinite(distance) || distance < 0.0f)
        {
            return false;
        }

        var offset = rayOrigin + rayDirection * distance - gridOrigin;
        var rowRow = Vector3.Dot(rowSpan, rowSpan);
        var columnColumn = Vector3.Dot(columnSpan, columnSpan);
        var rowColumn = Vector3.Dot(rowSpan, columnSpan);
        var determinant = rowRow * columnColumn - rowColumn * rowColumn;
        if (Math.Abs(determinant) < 0.0000001f)
        {
            return false;
        }

        var offsetRow = Vector3.Dot(offset, rowSpan);
        var offsetColumn = Vector3.Dot(offset, columnSpan);
        var rowFraction = (offsetRow * columnColumn - offsetColumn * rowColumn) / determinant;
        var columnFraction = (offsetColumn * rowRow - offsetRow * rowColumn) / determinant;
        row = Math.Clamp(
            (int)Math.Round(rowFraction * (rowCount - 1), MidpointRounding.AwayFromZero),
            0,
            rowCount - 1);
        column = Math.Clamp(
            (int)Math.Round(columnFraction * (columnCount - 1), MidpointRounding.AwayFromZero),
            0,
            columnCount - 1);
        return true;
    }
}
