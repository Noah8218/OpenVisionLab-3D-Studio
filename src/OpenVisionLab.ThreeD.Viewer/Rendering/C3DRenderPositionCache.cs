using System.Numerics;
using OpenVisionLab.ThreeD.Core;

namespace OpenVisionLab.ThreeD.Viewer.Rendering;

/// <summary>
/// Caches transformed C3D render positions for one proxy and one model pose.
/// The owner performs only managed geometry transformation; OpenGL resources
/// and presentation policy remain with the Viewer.
/// </summary>
internal sealed class C3DRenderPositionCache
{
    private C3DHeightGridRenderProxy? sourceProxy;
    private Vector3[]? positions;
    private ModelTransform transform;
    private int heightEmphasis = 1;

    public Vector3[]? Current => positions;

    public ModelTransform CurrentTransform => transform;

    public int CurrentHeightEmphasis => heightEmphasis;

    public bool HasValue => sourceProxy is not null || positions is not null;

    public Vector3[] GetOrCreate(C3DHeightGridRenderProxy renderProxy, ModelTransform nextTransform, int nextHeightEmphasis = 1)
    {
        ArgumentNullException.ThrowIfNull(renderProxy);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(nextHeightEmphasis);
        if (!ReferenceEquals(sourceProxy, renderProxy)
            || positions is null
            || transform != nextTransform
            || heightEmphasis != nextHeightEmphasis)
        {
            sourceProxy = renderProxy;
            transform = nextTransform;
            heightEmphasis = nextHeightEmphasis;
            positions = new Vector3[renderProxy.Points.Length];
            for (var index = 0; index < renderProxy.Points.Length; index++)
            {
                var sourcePosition = renderProxy.Points[index].Position;
                sourcePosition.Y *= nextHeightEmphasis;
                positions[index] = nextTransform.Apply(sourcePosition);
            }
        }

        return positions;
    }

    public void Set(
        C3DHeightGridRenderProxy renderProxy,
        ModelTransform preparedTransform,
        Vector3[] preparedPositions,
        int preparedHeightEmphasis = 1)
    {
        ArgumentNullException.ThrowIfNull(renderProxy);
        ArgumentNullException.ThrowIfNull(preparedPositions);
        if (preparedPositions.Length != renderProxy.Points.Length)
        {
            throw new ArgumentException(
                "Prepared C3D render positions must match the proxy point count.",
                nameof(preparedPositions));
        }

        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(preparedHeightEmphasis);

        sourceProxy = renderProxy;
        transform = preparedTransform;
        heightEmphasis = preparedHeightEmphasis;
        positions = preparedPositions;
    }

    public void Clear()
    {
        sourceProxy = null;
        positions = null;
        transform = default;
        heightEmphasis = 1;
    }
}
