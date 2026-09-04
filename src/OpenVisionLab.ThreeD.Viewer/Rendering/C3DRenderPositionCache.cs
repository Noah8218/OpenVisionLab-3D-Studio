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

    public Vector3[]? Current => positions;

    public ModelTransform CurrentTransform => transform;

    public bool HasValue => sourceProxy is not null || positions is not null;

    public Vector3[] GetOrCreate(C3DHeightGridRenderProxy renderProxy, ModelTransform nextTransform)
    {
        ArgumentNullException.ThrowIfNull(renderProxy);
        if (!ReferenceEquals(sourceProxy, renderProxy)
            || positions is null
            || transform != nextTransform)
        {
            sourceProxy = renderProxy;
            transform = nextTransform;
            positions = new Vector3[renderProxy.Points.Length];
            for (var index = 0; index < renderProxy.Points.Length; index++)
            {
                positions[index] = nextTransform.Apply(renderProxy.Points[index].Position);
            }
        }

        return positions;
    }

    public void Set(
        C3DHeightGridRenderProxy renderProxy,
        ModelTransform preparedTransform,
        Vector3[] preparedPositions)
    {
        ArgumentNullException.ThrowIfNull(renderProxy);
        ArgumentNullException.ThrowIfNull(preparedPositions);
        if (preparedPositions.Length != renderProxy.Points.Length)
        {
            throw new ArgumentException(
                "Prepared C3D render positions must match the proxy point count.",
                nameof(preparedPositions));
        }

        sourceProxy = renderProxy;
        transform = preparedTransform;
        positions = preparedPositions;
    }

    public void Clear()
    {
        sourceProxy = null;
        positions = null;
        transform = default;
    }
}
