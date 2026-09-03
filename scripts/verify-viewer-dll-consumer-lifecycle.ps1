[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',
    [string]$ArtifactDirectory = 'artifacts/viewer-consumer-lifecycle',
    [string]$ViewerBundlePath,
    [ValidateRange(10, 100)]
    [int]$RecreateCycles = 10,
    [switch]$NoRestore,
    [switch]$HardwareOpenGL,
    [switch]$UseTexturedMesh,
    [ValidateRange(0, 20)]
    [int]$WindowCloseCycles = 0,
    [switch]$ObserveGpuProcessMemory,
    [switch]$RequireGpuProcessMemoryCounter
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$artifactPath = [System.IO.Path]::GetFullPath($ArtifactDirectory)
$testRoot = [System.IO.Path]::GetFullPath((Join-Path $repoRoot 'artifacts'))
$artifactPrefix = $testRoot.TrimEnd([System.IO.Path]::DirectorySeparatorChar, [System.IO.Path]::AltDirectorySeparatorChar) + [System.IO.Path]::DirectorySeparatorChar
if (-not $artifactPath.StartsWith($artifactPrefix, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "Consumer lifecycle artifacts must stay under $testRoot."
}

$sampleProject = Join-Path $repoRoot 'samples/OpenVisionLab.ThreeD.Viewer.BinaryHost/OpenVisionLab.ThreeD.Viewer.BinaryHost.csproj'
$c3dPath = Join-Path $repoRoot '3D/Samples/ThicknessCouponV1/thickness-coupon-v1.C3D'
$meshPath = Join-Path $repoRoot $(if ($UseTexturedMesh) { '3D/PublicSamples/glTF/BoxTextured.glb' } else { '3D/PublicSamples/glTF/Box.glb' })
$pointCloudPath = Join-Path $repoRoot '3D/PublicSamples/PointCloud/xyzrgb_manuscript.laz'
$reportPath = Join-Path $artifactPath 'consumer-lifecycle.txt'
$contractPath = Join-Path $artifactPath 'current-scene-contract.txt'
$monitorPath = Join-Path $artifactPath 'monitor.txt'
$buildLogPath = Join-Path $artifactPath 'binary-host-build.log'
$tempPath = Join-Path $artifactPath 'temp'
$gpuPostCloseBarrierPath = Join-Path $artifactPath 'gpu-post-close-observation.ready'
$gpuPostCloseContinuePath = "$gpuPostCloseBarrierPath.continue"
$gpuObservationPath = Join-Path $artifactPath 'gpu-process-memory.txt'
$null = New-Item -ItemType Directory -Force -Path $artifactPath, $tempPath

if ($RequireGpuProcessMemoryCounter -and -not $ObserveGpuProcessMemory) {
    throw '-RequireGpuProcessMemoryCounter requires -ObserveGpuProcessMemory.'
}
if ($ObserveGpuProcessMemory) {
    foreach ($stalePath in @($gpuPostCloseBarrierPath, $gpuPostCloseContinuePath, $gpuObservationPath)) {
        if (Test-Path -LiteralPath $stalePath) {
            Remove-Item -LiteralPath $stalePath -Force
        }
    }
}

foreach ($inputPath in @($c3dPath, $meshPath, $pointCloudPath)) {
    if (-not (Test-Path -LiteralPath $inputPath -PathType Leaf)) {
        throw "Consumer lifecycle input was not found: $inputPath"
    }
}
if (Select-String -LiteralPath $sampleProject -Pattern '<ProjectReference' -Quiet) {
    throw 'Binary Host must not contain a ProjectReference.'
}

if ([string]::IsNullOrWhiteSpace($ViewerBundlePath)) {
    $bundleArguments = @(
        '-NoProfile',
        '-ExecutionPolicy',
        'Bypass',
        '-File',
        (Join-Path $PSScriptRoot 'build-viewer-dll.ps1'),
        '-Configuration',
        $Configuration)
    if ($NoRestore) { $bundleArguments += '-NoRestore' }
    & powershell @bundleArguments
    if ($LASTEXITCODE -ne 0) {
        throw "Viewer DLL bundle failed with exit code $LASTEXITCODE."
    }
    $bundlePath = Join-Path $repoRoot 'artifacts/viewer-dll/net10.0-windows'
}
else {
    $bundlePath = if ([System.IO.Path]::IsPathRooted($ViewerBundlePath)) {
        [System.IO.Path]::GetFullPath($ViewerBundlePath)
    }
    else {
        [System.IO.Path]::GetFullPath((Join-Path $repoRoot $ViewerBundlePath))
    }
}

$manifestPath = Join-Path $bundlePath 'viewer-dll-manifest.json'
if (-not (Test-Path -LiteralPath $manifestPath -PathType Leaf)) {
    throw "Viewer DLL bundle manifest was not found: $manifestPath"
}
$manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
$manifestFiles = @($manifest.files)
foreach ($file in $manifestFiles) {
    $filePath = Join-Path $bundlePath ([string]$file.name)
    if (-not (Test-Path -LiteralPath $filePath -PathType Leaf)) {
        throw "Viewer DLL bundle file is missing: $($file.name)"
    }
    $hash = (Get-FileHash -LiteralPath $filePath -Algorithm SHA256).Hash
    if ($hash -ne [string]$file.sha256) {
        throw "Viewer DLL bundle hash mismatch: $($file.name)"
    }
}

$buildArguments = @('build', $sampleProject, '-c', $Configuration, "-p:ViewerBundlePath=$bundlePath", '--nologo')
if ($NoRestore) { $buildArguments += '--no-restore' }
& dotnet @buildArguments 2>&1 | Tee-Object -FilePath $buildLogPath
if ($LASTEXITCODE -ne 0) {
    throw "Binary Host build failed with exit code $LASTEXITCODE."
}

$outputPath = Join-Path (Split-Path $sampleProject -Parent) "bin/$Configuration/net10.0-windows"
$hostExecutable = Join-Path $outputPath 'OpenVisionLab.ThreeD.Viewer.BinaryHost.exe'
if (-not (Test-Path -LiteralPath $hostExecutable -PathType Leaf)) {
    throw "Binary Host executable was not found: $hostExecutable"
}

Add-Type -AssemblyName System.Windows.Forms
$screens = @([System.Windows.Forms.Screen]::AllScreens)
if ($screens.Count -eq 0) {
    throw 'No display is available for the independent consumer run.'
}
if ($screens.Count -gt 2) {
    $topology = $screens | ForEach-Object {
        "$($_.DeviceName)=[$($_.Bounds.Left),$($_.Bounds.Top),$($_.Bounds.Width),$($_.Bounds.Height)]"
    }
    throw "Monitor policy is defined only for one or two independent monitors: $($topology -join '; ')"
}
$selectedMonitor = if ($screens.Count -eq 2) {
    $candidate = $screens |
        Sort-Object `
            @{ Expression = { [long]$_.WorkingArea.Width * [long]$_.WorkingArea.Height }; Ascending = $true }, `
            @{ Expression = { $_.Bounds.Left }; Ascending = $true } |
        Select-Object -First 1
    $leftmost = $screens | Sort-Object { $_.Bounds.Left } | Select-Object -First 1
    if ($candidate.DeviceName -ne $leftmost.DeviceName) {
        throw "Two-monitor policy requires the smaller working-area monitor to be on the left. Smaller=$($candidate.DeviceName); Leftmost=$($leftmost.DeviceName)"
    }
    $candidate
}
else {
    $screens[0]
}

$windowWidth = 1280
$windowHeight = 760
if ($selectedMonitor.WorkingArea.Width -lt $windowWidth -or $selectedMonitor.WorkingArea.Height -lt $windowHeight) {
    throw "Selected monitor working area is smaller than the consumer window: $($selectedMonitor.WorkingArea.Width)x$($selectedMonitor.WorkingArea.Height)"
}

if (-not ('OpenVisionLabConsumerLifecycleNativeWindow' -as [type])) {
    Add-Type @"
using System;
using System.Runtime.InteropServices;

public static class OpenVisionLabConsumerLifecycleNativeWindow
{
    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [DllImport("user32.dll")]
    public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    public static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

    [DllImport("user32.dll")]
    public static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc callback, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr hWnd, out Rect rect);

    [StructLayout(LayoutKind.Sequential)]
    private struct Rect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    public static IntPtr FindLargestVisibleWindow(uint processId)
    {
        IntPtr bestWindow = IntPtr.Zero;
        long bestArea = 0;
        EnumWindows((hWnd, _) =>
        {
            uint ownerProcessId;
            if (!IsWindowVisible(hWnd) || GetWindowThreadProcessId(hWnd, out ownerProcessId) == 0 || ownerProcessId != processId)
            {
                return true;
            }

            Rect rect;
            if (!GetWindowRect(hWnd, out rect))
            {
                return true;
            }

            var width = Math.Max(0, rect.Right - rect.Left);
            var height = Math.Max(0, rect.Bottom - rect.Top);
            var area = (long)width * height;
            if (area > bestArea)
            {
                bestArea = area;
                bestWindow = hWnd;
            }
            return true;
        }, IntPtr.Zero);
        return bestWindow;
    }

    public static bool Intersects(IntPtr hWnd, int left, int top, int width, int height)
    {
        Rect rect;
        if (!GetWindowRect(hWnd, out rect))
        {
            return false;
        }

        return rect.Right > left && rect.Left < left + width && rect.Bottom > top && rect.Top < top + height;
    }

    public static string GetBounds(IntPtr hWnd)
    {
        Rect rect;
        return GetWindowRect(hWnd, out rect)
            ? String.Format(
                "[{0},{1},{2},{3}]",
                rect.Left,
                rect.Top,
                rect.Right - rect.Left,
                rect.Bottom - rect.Top)
            : "Unavailable";
    }
}
"@
}

$monitorLines = [System.Collections.Generic.List[string]]::new()
$monitorLines.Add("Screens=$($screens.Count)")
foreach ($screen in $screens) {
    $monitorLines.Add("Monitor|name=$($screen.DeviceName)|primary=$($screen.Primary)|bounds=[$($screen.Bounds.Left),$($screen.Bounds.Top),$($screen.Bounds.Width),$($screen.Bounds.Height)]|workingArea=[$($screen.WorkingArea.Left),$($screen.WorkingArea.Top),$($screen.WorkingArea.Width),$($screen.WorkingArea.Height)]")
}
$monitorLines.Add("Selected|name=$($selectedMonitor.DeviceName)|bounds=[$($selectedMonitor.Bounds.Left),$($selectedMonitor.Bounds.Top),$($selectedMonitor.Bounds.Width),$($selectedMonitor.Bounds.Height)]|workingArea=[$($selectedMonitor.WorkingArea.Left),$($selectedMonitor.WorkingArea.Top),$($selectedMonitor.WorkingArea.Width),$($selectedMonitor.WorkingArea.Height)]")
Set-Content -LiteralPath $monitorPath -Value $monitorLines -Encoding utf8

$runArguments = @(
    '--smoke-render-frames', '16',
    '--smoke-contracts', $contractPath,
    '--consumer-lifecycle-report', $reportPath,
    '--consumer-c3d', $c3dPath,
    '--consumer-mesh', $meshPath,
    '--consumer-pointcloud', $pointCloudPath,
    '--consumer-lifecycle-recreate-count', $RecreateCycles.ToString([System.Globalization.CultureInfo]::InvariantCulture))
if (-not $HardwareOpenGL) {
    $runArguments = @('--smoke-software-rendering') + $runArguments
}
if ($HardwareOpenGL) {
    $runArguments += '--consumer-require-hardware-opengl'
}
if ($UseTexturedMesh) {
    $runArguments += '--consumer-require-texture-release'
}
if ($WindowCloseCycles -gt 0) {
    $runArguments += '--consumer-window-close-cycles', $WindowCloseCycles.ToString([System.Globalization.CultureInfo]::InvariantCulture)
}
if ($ObserveGpuProcessMemory) {
    $runArguments += '--consumer-gpu-post-close-observation-barrier', $gpuPostCloseBarrierPath
}

$oldTemp = $env:TEMP
$oldTmp = $env:TMP
$env:TEMP = $tempPath
$env:TMP = $tempPath

function Get-GpuProcessMemoryObservation {
    param([int]$ProcessId)

    $observation = [ordered]@{
        QuerySucceeded = $false
        ProcessInstancePresent = $false
        InstanceCount = 0
        DedicatedBytes = 0L
        SharedBytes = 0L
        Error = ''
    }
    try {
        $instancePrefix = "pid_${ProcessId}_"
        $dedicatedSamples = @((Get-Counter '\GPU Process Memory(*)\Dedicated Usage' -ErrorAction Stop).CounterSamples |
            Where-Object { ([string]$_.InstanceName).StartsWith($instancePrefix, [System.StringComparison]::OrdinalIgnoreCase) })
        $sharedSamples = @((Get-Counter '\GPU Process Memory(*)\Shared Usage' -ErrorAction Stop).CounterSamples |
            Where-Object { ([string]$_.InstanceName).StartsWith($instancePrefix, [System.StringComparison]::OrdinalIgnoreCase) })
        $instanceNames = @($dedicatedSamples + $sharedSamples |
            ForEach-Object { [string]$_.InstanceName } |
            Sort-Object -Unique)
        $dedicatedSum = ($dedicatedSamples | Measure-Object -Property CookedValue -Sum).Sum
        $sharedSum = ($sharedSamples | Measure-Object -Property CookedValue -Sum).Sum
        $observation.QuerySucceeded = $true
        $observation.ProcessInstancePresent = $instanceNames.Count -gt 0
        $observation.InstanceCount = $instanceNames.Count
        $observation.DedicatedBytes = if ($null -eq $dedicatedSum) { 0L } else { [int64]$dedicatedSum }
        $observation.SharedBytes = if ($null -eq $sharedSum) { 0L } else { [int64]$sharedSum }
    }
    catch {
        $observation.Error = $_.Exception.Message.Replace('|', '/').Replace([Environment]::NewLine, ' ')
    }

    [pscustomobject]$observation
}

function Format-GpuProcessMemoryObservation {
    param(
        [string]$Stage,
        [pscustomobject]$Observation
    )

    "GpuProcessMemory|stage=$Stage|querySucceeded=$($Observation.QuerySucceeded)|processInstancePresent=$($Observation.ProcessInstancePresent)|instances=$($Observation.InstanceCount)|dedicatedBytes=$($Observation.DedicatedBytes)|sharedBytes=$($Observation.SharedBytes)|error=$($Observation.Error)"
}

$process = $null
$windowHandle = [IntPtr]::Zero
$windowBounds = 'Unavailable'
$windowIntersectsMonitor = $false
$gpuObservationLines = [System.Collections.Generic.List[string]]::new()
$gpuActiveObservation = $null
$gpuPostCloseObservation = $null
$gpuBarrierObserved = $false
try {
    $process = Start-Process -FilePath $hostExecutable -ArgumentList $runArguments -WorkingDirectory $repoRoot -PassThru -WindowStyle Hidden
    $windowDeadline = [DateTime]::UtcNow.AddSeconds(45)
    do {
        Start-Sleep -Milliseconds 250
        $process.Refresh()
        if ($process.HasExited) {
            break
        }
        $windowHandle = [OpenVisionLabConsumerLifecycleNativeWindow]::FindLargestVisibleWindow([uint32]$process.Id)
    }
    while ($windowHandle -eq [IntPtr]::Zero -and [DateTime]::UtcNow -lt $windowDeadline)

    if ($windowHandle -eq [IntPtr]::Zero) {
        if (-not $process.HasExited) { $process.Kill() }
        throw 'Independent consumer did not expose a visible Window within 45 seconds.'
    }

    [OpenVisionLabConsumerLifecycleNativeWindow]::ShowWindow($windowHandle, 9) | Out-Null
    [OpenVisionLabConsumerLifecycleNativeWindow]::SetWindowPos(
        $windowHandle,
        [IntPtr]::Zero,
        $selectedMonitor.WorkingArea.Left,
        $selectedMonitor.WorkingArea.Top,
        $windowWidth,
        $windowHeight,
        0x0040) | Out-Null
    [OpenVisionLabConsumerLifecycleNativeWindow]::SetForegroundWindow($windowHandle) | Out-Null
    Start-Sleep -Milliseconds 250
    $windowBounds = [OpenVisionLabConsumerLifecycleNativeWindow]::GetBounds($windowHandle)
    $windowIntersectsMonitor = [OpenVisionLabConsumerLifecycleNativeWindow]::Intersects(
        $windowHandle,
        $selectedMonitor.Bounds.Left,
        $selectedMonitor.Bounds.Top,
        $selectedMonitor.Bounds.Width,
        $selectedMonitor.Bounds.Height)
    $monitorLines.Add("Window|bounds=$windowBounds|intersectsSelected=$windowIntersectsMonitor")
    Set-Content -LiteralPath $monitorPath -Value $monitorLines -Encoding utf8
    if (-not $windowIntersectsMonitor) {
        if (-not $process.HasExited) { $process.Kill() }
        throw "Independent consumer Window did not intersect selected monitor. Bounds=$windowBounds"
    }

    if ($ObserveGpuProcessMemory) {
        $gpuActiveObservation = Get-GpuProcessMemoryObservation -ProcessId $process.Id
        $gpuObservationLines.Add((Format-GpuProcessMemoryObservation -Stage 'active' -Observation $gpuActiveObservation))
        if ($RequireGpuProcessMemoryCounter -and
            (-not $gpuActiveObservation.QuerySucceeded -or -not $gpuActiveObservation.ProcessInstancePresent)) {
            if (-not $process.HasExited) { $process.Kill() }
            throw "GPU Process Memory counter did not expose the consumer process: $($gpuActiveObservation.Error)"
        }
    }

    while (-not $process.HasExited) {
        if ($ObserveGpuProcessMemory -and -not $gpuBarrierObserved -and (Test-Path -LiteralPath $gpuPostCloseBarrierPath)) {
            $gpuPostCloseObservation = Get-GpuProcessMemoryObservation -ProcessId $process.Id
            $gpuObservationLines.Add((Format-GpuProcessMemoryObservation -Stage 'post-window-close' -Observation $gpuPostCloseObservation))
            New-Item -ItemType File -Force -Path $gpuPostCloseContinuePath | Out-Null
            $gpuBarrierObserved = $true
        }

        Start-Sleep -Milliseconds 100
        $process.Refresh()
    }
    $process.WaitForExit()
    if ($ObserveGpuProcessMemory -and -not $gpuBarrierObserved) {
        throw 'The consumer exited before publishing the post-Window.Close GPU observation barrier.'
    }
}
finally {
    $env:TEMP = $oldTemp
    $env:TMP = $oldTmp
}

if ($ObserveGpuProcessMemory) {
    $gpuObservationLines.Add("GpuProcessMemorySummary|barrierObserved=$gpuBarrierObserved|interpretation=process-counter-observation-only;driver-attribution-not-proven")
    Set-Content -LiteralPath $gpuObservationPath -Value $gpuObservationLines -Encoding utf8
}

if ($null -eq $process -or $process.ExitCode -ne 0) {
    $processExitCode = if ($null -eq $process) { 'none' } else { $process.ExitCode }
    throw "Independent consumer failed with exit code $processExitCode."
}
if (-not (Test-Path -LiteralPath $reportPath -PathType Leaf)) {
    throw "Independent consumer report was not created: $reportPath"
}

Add-Content -LiteralPath $reportPath -Value "Monitor|selected=$($selectedMonitor.DeviceName)|window=$windowBounds|intersects=$windowIntersectsMonitor"
$reportText = Get-Content -LiteralPath $reportPath -Raw
if ($reportText -notmatch '(?m)^Result\|Pass\|') {
    throw 'Independent consumer lifecycle report did not pass all checks.'
}
$cycleMatches = [regex]::Matches($reportText, '(?m)^RecreateCycle\|')
if ($cycleMatches.Count -lt $RecreateCycles) {
    throw "Independent consumer report contains only $($cycleMatches.Count) recreate cycles; expected at least $RecreateCycles."
}
if ($WindowCloseCycles -gt 0) {
    $windowCloseMatches = [regex]::Matches($reportText, '(?m)^WindowCloseCycle\|')
    if ($windowCloseMatches.Count -lt $WindowCloseCycles) {
        throw "Independent consumer report contains only $($windowCloseMatches.Count) Window.Close cycles; expected at least $WindowCloseCycles."
    }
}
if ($reportText -notmatch '(?m)^MemoryObservation\|') {
    throw 'Independent consumer report did not contain a memory observation.'
}

$evidenceLine = "Evidence|report=$reportPath|monitor=$monitorPath|contract=$contractPath|build=$buildLogPath"
if ($ObserveGpuProcessMemory) {
    $evidenceLine += "|gpu=$gpuObservationPath"
}

@(
    "Result|Pass|cycles=$($cycleMatches.Count)|monitor=$($selectedMonitor.DeviceName)|window=$windowBounds|intersects=$windowIntersectsMonitor"
    $evidenceLine
) | Write-Output
