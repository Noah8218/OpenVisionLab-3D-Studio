using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using OpenVisionLab.ThreeD.Viewer.Rendering;

namespace OpenVisionLab.ThreeD.Viewer;

public sealed partial class OpenVisionThreeDViewerControl
{
    public async Task<bool> RunProfilePointerSmokeAsync(string reportPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reportPath);
        var lines = new List<string>
        {
            "OpenVisionLab 3D interactive height-profile pointer smoke",
            $"Generated: {DateTimeOffset.Now:O}"
        };
        Window? hostWindow = null;
        var originalTopmost = false;
        var hasOriginalPointer = false;
        var originalPointer = default(Point);
        var firstClickPassed = false;
        var secondClickPassed = false;
        var endpointDragPassed = false;
        var outsideDragOrbitPassed = false;
        var previewUnchanged = false;
        var sourceBindingPassed = false;
        var failure = string.Empty;
        var initialPreview = viewModel.PreviewToolResult;
        var initialResults = viewModel.ResultEntities;
        var initialPointPairStep = viewModel.CreatePointPairDimensionsRecipeStep();
        var beforeOrbit = default(CameraSnapshot);
        var afterOrbit = default(CameraSnapshot);

        try
        {
            if (!viewModel.C3DSampleVisible || c3dSample is null)
            {
                throw new InvalidOperationException("Profile pointer smoke requires a visible loaded C3D source.");
            }

            hostWindow = Window.GetWindow(this)
                ?? throw new InvalidOperationException("Viewer is not attached to a visible WPF window.");
            originalTopmost = hostWindow.Topmost;
            hostWindow.Topmost = true;
            hostWindow.Activate();
            hostWindow.Focus();
            viewModel.ProfileCommand.Execute(null);
            await Dispatcher.InvokeAsync(RenderNow, DispatcherPriority.Render);
            await Task.Delay(240);

            if (!Viewport.IsVisible || Viewport.ActualWidth < 200.0 || Viewport.ActualHeight < 180.0)
            {
                throw new InvalidOperationException(
                    $"Viewport is not ready for profile pointer input ({Viewport.ActualWidth:F0}x{Viewport.ActualHeight:F0}).");
            }

            profileFirst = null;
            profileSecond = null;
            profileDraggedEndpoint = 0;
            profileSourceSha256 = c3dSample.ContentSha256;
            viewModel.ClearProfile();
            hasOriginalPointer = WindowsPointerInput.TryGetPosition(out originalPointer);

            if (!TryFindTeachingCapturePickPoint(
                    new HashSet<(int Row, int Column)>(),
                    out var firstLocal,
                    out var firstPoint))
            {
                throw new InvalidOperationException("No first pickable C3D profile cell was found.");
            }

            await SendTeachingLeftClickAsync(hostWindow, firstLocal);
            firstClickPassed = profileFirst is not null
                && profileSecond is null
                && viewModel.ProfileVisible
                && viewModel.ProfileValidSampleCount == 1;

            var excluded = new HashSet<(int Row, int Column)> { (firstPoint.Row, firstPoint.Column) };
            if (!TryFindTeachingCapturePickPoint(excluded, out var secondLocal, out var secondPoint))
            {
                throw new InvalidOperationException("No distinct second C3D profile cell was found.");
            }

            await SendTeachingLeftClickAsync(hostWindow, secondLocal);
            secondClickPassed = profileFirst is { } selectedP1
                && profileSecond is { } selectedP2
                && !SameGridCell(selectedP1, selectedP2)
                && viewModel.ProfileVisible
                && viewModel.ProfileValidSampleCount >= 2
                && viewModel.ProfilePathData.Contains('L');

            excluded.Add((secondPoint.Row, secondPoint.Column));
            if (!TryFindTeachingCapturePickPoint(excluded, out var thirdLocal, out var thirdPoint))
            {
                throw new InvalidOperationException("No third C3D cell was found for profile endpoint drag.");
            }

            var profileBeforeDrag = viewModel.ProfileEndpointSummary;
            await SendTeachingDragAsync(hostWindow, firstLocal, thirdLocal, MouseButton.Left);
            endpointDragPassed = profileFirst is { } movedFirst
                && SameGridCell(movedFirst, thirdPoint)
                && profileSecond is { } retainedSecond
                && SameGridCell(retainedSecond, secondPoint)
                && !string.Equals(profileBeforeDrag, viewModel.ProfileEndpointSummary, StringComparison.Ordinal)
                && viewModel.ProfileValidSampleCount >= 2;

            var endpointsBeforeOrbit = viewModel.ProfileEndpointSummary;
            beforeOrbit = CaptureCameraSnapshot();
            var orbitStart = new Point(Viewport.ActualWidth * 0.82, Viewport.ActualHeight * 0.24);
            var orbitEnd = new Point(Viewport.ActualWidth * 0.91, Viewport.ActualHeight * 0.36);
            await SendTeachingDragAsync(hostWindow, orbitStart, orbitEnd, MouseButton.Left);
            afterOrbit = CaptureCameraSnapshot();
            outsideDragOrbitPassed = Math.Abs(afterOrbit.Yaw - beforeOrbit.Yaw) > 1.0
                && Math.Abs(afterOrbit.Pitch - beforeOrbit.Pitch) > 1.0
                && string.Equals(endpointsBeforeOrbit, viewModel.ProfileEndpointSummary, StringComparison.Ordinal);

            previewUnchanged = ReferenceEquals(initialPreview, viewModel.PreviewToolResult)
                && ReferenceEquals(initialResults, viewModel.ResultEntities)
                && Equals(initialPointPairStep, viewModel.CreatePointPairDimensionsRecipeStep());
            sourceBindingPassed = profileSourceSha256 is not null
                && profileSourceSha256.Equals(c3dSample.ContentSha256, StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception exception)
        {
            failure = exception.Message;
        }
        finally
        {
            profileDraggedEndpoint = 0;
            profilePointerDownPosition = null;
            profilePointerDragExceeded = false;
            if (hasOriginalPointer)
            {
                try
                {
                    WindowsPointerInput.MoveTo(originalPointer);
                }
                catch (Win32Exception)
                {
                    // Pointer restoration is best effort after evidence capture.
                }
            }

            if (hostWindow is not null)
            {
                hostWindow.Topmost = originalTopmost;
            }
        }

        var passed = firstClickPassed
            && secondClickPassed
            && endpointDragPassed
            && outsideDragOrbitPassed
            && previewUnchanged
            && sourceBindingPassed;
        if (!passed && string.IsNullOrWhiteSpace(failure))
        {
            failure = "One or more interactive profile pointer assertions failed.";
        }

        lines.Add($"FirstClick|pass={firstClickPassed}");
        lines.Add($"SecondClick|pass={secondClickPassed}|summary={viewModel.ProfileEndpointSummary}");
        lines.Add($"EndpointDrag|pass={endpointDragPassed}|summary={viewModel.ProfileEndpointSummary}");
        lines.Add($"OutsideHandleLeftDragOrbit|pass={outsideDragOrbitPassed}|before={beforeOrbit}|after={afterOrbit}");
        lines.Add($"DisplayOnlyBoundary|pass={previewUnchanged}|previewStatus={viewModel.PreviewToolResult.Status}|results={viewModel.ResultEntities.Count}|pointPairUnchanged={Equals(initialPointPairStep, viewModel.CreatePointPairDimensionsRecipeStep())}");
        lines.Add($"SourceBinding|pass={sourceBindingPassed}|sha256={profileSourceSha256 ?? "(none)"}");
        lines.Add($"Profile|visible={viewModel.ProfileVisible}|valid={viewModel.ProfileValidSampleCount}|missing={viewModel.ProfileMissingSampleCount}|summary={viewModel.ProfileSummary}|range={viewModel.ProfileRange}");
        lines.Add($"Result: {(passed ? "Pass" : "Fail")}|failure={failure}");
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(reportPath))!);
        File.WriteAllLines(reportPath, lines);
        return passed;
    }
}
