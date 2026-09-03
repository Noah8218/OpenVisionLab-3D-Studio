using SharpGL;
using SharpGL.RenderContextProviders;
using SharpGL.WPF;
using System.Runtime.InteropServices;

namespace OpenVisionLab.ThreeD.Viewer.Rendering;

/// <summary>
/// Owns the cleanup boundary that SharpGL.WPF does not expose from its
/// OpenGLControl.Unloaded handler. The Viewer releases its application-owned
/// buffers first, then this owner releases the provider context and the
/// provider-specific DIB section. Keeping this code behind one owner prevents
/// lifecycle policy from leaking into render and input code.
/// </summary>
internal sealed class SharpGlRenderContextLifetime
{
    private bool disposeAttempted;
    private bool disposeSucceeded;
    private string? disposeFailureType;

    public int DisposeAttempts { get; private set; }

    public int DisposeFailures { get; private set; }

    public bool DisposeSucceeded => disposeSucceeded;

    public bool DisposeAttempted => disposeAttempted;

    public string FailureType => disposeFailureType ?? "(none)";

    public void Dispose(OpenGLControl control)
    {
        ArgumentNullException.ThrowIfNull(control);
        if (disposeAttempted)
        {
            return;
        }

        disposeAttempted = true;
        DisposeAttempts++;

        var gl = control.OpenGL;
        var provider = gl.RenderContextProvider;
        if (provider is null)
        {
            disposeSucceeded = true;
            return;
        }

        var compatibleDeviceContext = provider is DIBSectionRenderContextProvider
            ? provider.DeviceContextHandle
            : IntPtr.Zero;
        try
        {
            // Provider-owned framebuffers and DIB sections are not released by
            // SharpGL.WPF's Unloaded event. Make the context current so FBO
            // teardown can delete its GL objects before the provider is gone.
            gl.MakeCurrent();
            DestroyProviderOwnedDib(provider);
            provider.Destroy();
            disposeSucceeded = true;
        }
        catch (Exception exception)
        {
            DisposeFailures++;
            disposeFailureType = exception.GetType().Name;
        }
        finally
        {
            if (compatibleDeviceContext != IntPtr.Zero)
            {
                // SharpGL 3.1.1 calls ReleaseDC for the compatible DC created
                // by DIBSectionRenderContextProvider. ReleaseDC cannot own
                // that handle; DeleteDC is the matching Win32 release.
                _ = DeleteDC(compatibleDeviceContext);
            }

            try
            {
                gl.MakeNothingCurrent();
            }
            catch
            {
                // The context may already have been torn down by the provider.
            }
        }
    }

    [DllImport("gdi32.dll", SetLastError = true)]
    private static extern bool DeleteDC(IntPtr hdc);

    private static void DestroyProviderOwnedDib(IRenderContextProvider provider)
    {
        switch (provider)
        {
            case FBORenderContextProvider fbo:
                fbo.InternalDIBSection.Destroy();
                break;
            case DIBSectionRenderContextProvider dib:
                dib.DIBSection.Destroy();
                break;
        }
    }
}
