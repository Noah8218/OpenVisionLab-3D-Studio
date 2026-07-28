namespace OpenVisionLab.ThreeD.Core;

/// <summary>
/// The smallest immutable geometry surface shared by concrete C3D line
/// artifacts. It intentionally carries source lineage and finite segment
/// evidence only; fitting diagnostics and point-pick provenance remain on
/// their concrete contracts.
/// </summary>
public interface IC3DLineGeometry
{
    string OutputEntityId { get; }
    string ContentSha256 { get; }
    string RootSourceEntityId { get; }
    string RootSourceSha256 { get; }
    string Unit { get; }
    string FrameId { get; }
    string CoordinateConvention { get; }
    C3DLineOriginKind OriginKind { get; }
    double AnchorX { get; }
    double AnchorY { get; }
    double AnchorZ { get; }
    double DirectionX { get; }
    double DirectionY { get; }
    double DirectionZ { get; }
    double SegmentStartX { get; }
    double SegmentStartY { get; }
    double SegmentStartZ { get; }
    double SegmentEndX { get; }
    double SegmentEndY { get; }
    double SegmentEndZ { get; }
}

public enum C3DLineOriginKind
{
    FittedEdge,
    PickedPoints
}
