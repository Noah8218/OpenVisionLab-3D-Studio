using System.Runtime.InteropServices;
using SharpGL;

namespace OpenVisionLab.ThreeD.Viewer.Rendering;

internal enum C3DWireframeLodLevel
{
    Precise,
    Medium,
    Coarse
}

internal sealed class C3DGpuBufferSet
{
    private const uint GlArrayBuffer = 0x8892;
    private const uint GlElementArrayBuffer = 0x8893;
    private const uint GlStaticDraw = 0x88E4;
    private const uint GlFloat = 0x1406;
    private const uint GlUnsignedInt = 0x1405;
    private const uint GlVertexArray = 0x8074;
    private const uint GlColorArray = 0x8076;
    private const int VertexStrideBytes = 6 * sizeof(float);
    private const int ColorOffsetBytes = 3 * sizeof(float);

    private readonly uint[] bufferIds;

    private C3DGpuBufferSet(
        uint[] bufferIds,
        int pointCount,
        int triangleIndexCount,
        int preciseGridIndexCount,
        int mediumGridIndexCount,
        int coarseGridIndexCount,
        int surfaceEdgeIndexCount,
        long uploadedBytes)
    {
        this.bufferIds = bufferIds;
        PointCount = pointCount;
        TriangleIndexCount = triangleIndexCount;
        PreciseGridIndexCount = preciseGridIndexCount;
        MediumGridIndexCount = mediumGridIndexCount;
        CoarseGridIndexCount = coarseGridIndexCount;
        SurfaceEdgeIndexCount = surfaceEdgeIndexCount;
        UploadedBytes = uploadedBytes;
    }

    public int PointCount { get; }

    public int TriangleIndexCount { get; }

    public int PreciseGridIndexCount { get; }

    public int MediumGridIndexCount { get; }

    public int CoarseGridIndexCount { get; }

    public int SurfaceEdgeIndexCount { get; }

    public long UploadedBytes { get; }

    public static bool TryCreate(
        OpenGL gl,
        float[] interleavedVertices,
        C3DHeightGridRenderProxy renderProxy,
        out C3DGpuBufferSet? result,
        out string failure)
    {
        ArgumentNullException.ThrowIfNull(gl);
        ArgumentNullException.ThrowIfNull(interleavedVertices);
        ArgumentNullException.ThrowIfNull(renderProxy);
        if (interleavedVertices.Length != renderProxy.Points.Length * 6)
        {
            throw new ArgumentException(
                "C3D GPU vertices must contain XYZRGB floats for every render point.",
                nameof(interleavedVertices));
        }

        var ids = new uint[6];
        try
        {
            gl.GenBuffers(ids.Length, ids);
            if (ids.Any(id => id == 0))
            {
                throw new InvalidOperationException("OpenGL did not allocate all required C3D buffers.");
            }

            Upload(gl, GlArrayBuffer, ids[0], interleavedVertices);
            Upload(gl, GlElementArrayBuffer, ids[1], renderProxy.TriangleIndices);
            Upload(gl, GlElementArrayBuffer, ids[2], renderProxy.GridEdgeIndices);
            Upload(gl, GlElementArrayBuffer, ids[3], renderProxy.InteractionGridEdgeIndices);
            Upload(gl, GlElementArrayBuffer, ids[4], renderProxy.CoarseInteractionGridEdgeIndices);
            Upload(gl, GlElementArrayBuffer, ids[5], renderProxy.SurfaceEdgeIndices);

            var uploadedBytes = checked(
                (long)interleavedVertices.Length * sizeof(float)
                + ((long)renderProxy.TriangleIndices.Length
                    + renderProxy.GridEdgeIndices.Length
                    + renderProxy.InteractionGridEdgeIndices.Length
                    + renderProxy.CoarseInteractionGridEdgeIndices.Length
                    + renderProxy.SurfaceEdgeIndices.Length) * sizeof(int));
            result = new C3DGpuBufferSet(
                ids,
                renderProxy.Points.Length,
                renderProxy.TriangleIndices.Length,
                renderProxy.GridEdgeIndices.Length,
                renderProxy.InteractionGridEdgeIndices.Length,
                renderProxy.CoarseInteractionGridEdgeIndices.Length,
                renderProxy.SurfaceEdgeIndices.Length,
                uploadedBytes);
            failure = string.Empty;
            return true;
        }
        catch (Exception exception)
        {
            TryDelete(gl, ids);
            result = null;
            failure = exception.GetBaseException().Message;
            return false;
        }
        finally
        {
            try
            {
                gl.BindBuffer(GlArrayBuffer, 0);
                gl.BindBuffer(GlElementArrayBuffer, 0);
            }
            catch
            {
                // The compatibility context may not expose buffer-object entry points.
            }
        }
    }

    public bool Release(OpenGL gl)
    {
        ArgumentNullException.ThrowIfNull(gl);
        var deleted = TryDelete(gl, bufferIds);
        Array.Clear(bufferIds);
        return deleted;
    }

    public void DrawPoints(OpenGL gl, float pointSize)
    {
        BeginVertexArrays(gl, usePointColors: true);
        try
        {
            gl.PointSize(pointSize);
            gl.DrawArrays(OpenGL.GL_POINTS, 0, PointCount);
            gl.PointSize(1.0f);
        }
        finally
        {
            EndVertexArrays(gl, usedPointColors: true);
        }
    }

    public void DrawWireframe(OpenGL gl, C3DWireframeLodLevel lodLevel)
    {
        var (bufferId, indexCount) = lodLevel switch
        {
            C3DWireframeLodLevel.Coarse => (bufferIds[4], CoarseGridIndexCount),
            C3DWireframeLodLevel.Medium => (bufferIds[3], MediumGridIndexCount),
            _ => (bufferIds[2], PreciseGridIndexCount)
        };

        DrawIndexed(gl, OpenGL.GL_LINES, bufferId, indexCount, usePointColors: true);
    }

    public void DrawSurface(OpenGL gl, bool withEdges)
    {
        if (withEdges)
        {
            gl.Enable(OpenGL.GL_POLYGON_OFFSET_FILL);
            gl.PolygonOffset(1.0f, 1.0f);
        }

        try
        {
            DrawIndexed(
                gl,
                OpenGL.GL_TRIANGLES,
                bufferIds[1],
                TriangleIndexCount,
                usePointColors: true);
        }
        finally
        {
            if (withEdges)
            {
                gl.Disable(OpenGL.GL_POLYGON_OFFSET_FILL);
            }
        }

        if (!withEdges)
        {
            return;
        }

        gl.Enable(OpenGL.GL_BLEND);
        gl.BlendFunc(OpenGL.GL_SRC_ALPHA, OpenGL.GL_ONE_MINUS_SRC_ALPHA);
        gl.Color(0.02, 0.05, 0.08, 0.32);
        try
        {
            DrawIndexed(
                gl,
                OpenGL.GL_LINES,
                bufferIds[5],
                SurfaceEdgeIndexCount,
                usePointColors: false);
        }
        finally
        {
            gl.Disable(OpenGL.GL_BLEND);
        }
    }

    private void DrawIndexed(
        OpenGL gl,
        uint primitive,
        uint indexBuffer,
        int indexCount,
        bool usePointColors)
    {
        BeginVertexArrays(gl, usePointColors);
        try
        {
            gl.BindBuffer(GlElementArrayBuffer, indexBuffer);
            gl.DrawElements(primitive, indexCount, GlUnsignedInt, IntPtr.Zero);
        }
        finally
        {
            gl.BindBuffer(GlElementArrayBuffer, 0);
            EndVertexArrays(gl, usePointColors);
        }
    }

    private void BeginVertexArrays(OpenGL gl, bool usePointColors)
    {
        gl.BindBuffer(GlArrayBuffer, bufferIds[0]);
        gl.EnableClientState(GlVertexArray);
        gl.VertexPointer(3, GlFloat, VertexStrideBytes, IntPtr.Zero);
        if (usePointColors)
        {
            gl.EnableClientState(GlColorArray);
            gl.ColorPointer(3, GlFloat, VertexStrideBytes, new IntPtr(ColorOffsetBytes));
        }
    }

    private static void EndVertexArrays(OpenGL gl, bool usedPointColors)
    {
        if (usedPointColors)
        {
            gl.DisableClientState(GlColorArray);
        }

        gl.DisableClientState(GlVertexArray);
        gl.BindBuffer(GlArrayBuffer, 0);
    }

    private static void Upload<T>(OpenGL gl, uint target, uint bufferId, T[] data)
        where T : unmanaged
    {
        var handle = GCHandle.Alloc(data, GCHandleType.Pinned);
        try
        {
            gl.BindBuffer(target, bufferId);
            gl.BufferData(target, checked(data.Length * Marshal.SizeOf<T>()), handle.AddrOfPinnedObject(), GlStaticDraw);
        }
        finally
        {
            handle.Free();
        }
    }

    private static bool TryDelete(OpenGL gl, uint[] ids)
    {
        if (!ids.Any(id => id != 0))
        {
            return true;
        }

        try
        {
            gl.DeleteBuffers(ids.Length, ids);
            return true;
        }
        catch
        {
            // Context teardown already releases its objects. Fallback rendering remains available.
            return false;
        }
    }
}
