using System.Numerics;
using System.Windows.Controls;
using System.Windows.Shapes;
using OpenVisionLab.ThreeD.Viewer.Rendering;

namespace OpenVisionLab.ThreeD.Viewer;

public sealed partial class OpenVisionThreeDViewerControl
{
    private const double OrientationTriadCenter = 39.0;
    private const double OrientationTriadLength = 28.0;

    private void UpdateOrientationTriad()
    {
        var eye = GetCameraPosition();
        var target = GetCameraTarget();
        var direction = Vector3.Normalize(eye - target);
        var up = CameraMath.CameraUp(eye, target);
        OrientationCubeCamera.Position = new System.Windows.Media.Media3D.Point3D(direction.X * 4, direction.Y * 4, direction.Z * 4);
        OrientationCubeCamera.LookDirection = new System.Windows.Media.Media3D.Vector3D(-direction.X, -direction.Y, -direction.Z);
        OrientationCubeCamera.UpDirection = new System.Windows.Media.Media3D.Vector3D(up.X, up.Y, up.Z);
        SetOrientationAxis(
            OrientationXAxis,
            OrientationXLabel,
            CameraMath.ProjectWorldDirectionToScreen(Vector3.UnitX, eye, target));
        SetOrientationAxis(
            OrientationYAxis,
            OrientationYLabel,
            CameraMath.ProjectWorldDirectionToScreen(Vector3.UnitY, eye, target));
        SetOrientationAxis(
            OrientationZAxis,
            OrientationZLabel,
            CameraMath.ProjectWorldDirectionToScreen(Vector3.UnitZ, eye, target));
    }

    private static void SetOrientationAxis(Line line, Border label, Vector2 direction)
    {
        var x = OrientationTriadCenter + direction.X * OrientationTriadLength;
        var y = OrientationTriadCenter + direction.Y * OrientationTriadLength;
        line.X2 = x;
        line.Y2 = y;
        Canvas.SetLeft(label, x - label.Width / 2.0);
        Canvas.SetTop(label, y - label.Height / 2.0);
    }
}
