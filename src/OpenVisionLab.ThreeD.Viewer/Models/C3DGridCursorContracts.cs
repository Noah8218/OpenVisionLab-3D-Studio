namespace OpenVisionLab.ThreeD.Viewer.Models;

/// <summary>
/// View-only native C3D grid cursor exchanged with a Viewer host.
/// </summary>
public readonly record struct C3DGridCursor(
    C3DGridCursorOrigin Origin,
    string SourceContentSha256,
    int Row,
    int Column,
    double RawHeight,
    bool IsValid);

public enum C3DGridCursorOrigin
{
    HeightImage,
    ThreeDViewer
}

public sealed class C3DGridHoverChangedEventArgs(C3DGridCursor? cursor) : EventArgs
{
    public C3DGridCursor? Cursor { get; } = cursor;
}
