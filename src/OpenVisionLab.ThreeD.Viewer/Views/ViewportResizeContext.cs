using System.Windows;
using SharpGL.WPF;

namespace OpenVisionLab.ThreeD.Viewer;

public static class ViewportResizeContext
{
    public static readonly DependencyProperty IsEnabledProperty = DependencyProperty.RegisterAttached("IsEnabled", typeof(bool), typeof(ViewportResizeContext), new PropertyMetadata(false));

    static ViewportResizeContext()
    {
        EventManager.RegisterClassHandler(typeof(OpenGLControl), FrameworkElement.LoadedEvent, new RoutedEventHandler(OnViewportContextRequired));
        EventManager.RegisterClassHandler(typeof(OpenGLControl), FrameworkElement.SizeChangedEvent, new SizeChangedEventHandler(OnViewportContextRequired));
    }

    public static void SetIsEnabled(DependencyObject element, bool value) => element.SetValue(IsEnabledProperty, value);
    public static bool GetIsEnabled(DependencyObject element) => (bool)element.GetValue(IsEnabledProperty);

    private static void OnViewportContextRequired(object sender, RoutedEventArgs args)
    {
        var viewport = (OpenGLControl)sender;
        // SharpGL 3.1.1의 Loaded/SizeChanged는 활성 문맥에서 FBO를 재생성하므로 먼저 선택한다.
        if (GetIsEnabled(viewport) && viewport.OpenGL.RenderContextProvider is { RenderContextHandle: var handle } && handle != IntPtr.Zero)
            viewport.OpenGL.MakeCurrent();
    }
}
