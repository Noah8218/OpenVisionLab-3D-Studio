param(
    [string]$ArtifactDirectory = "artifacts\current\20260729-novice-stage-navigation-video-review",
    [int]$WideDurationSeconds = 58,
    [int]$CompactDurationSeconds = 52,
    [switch]$PostProcessOnly,
    [switch]$WideOnly,
    [switch]$CompactOnly,
    [switch]$OwnerPath,
    [switch]$TeachCorrectionOnly
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$repoRoot = Split-Path -Parent $PSScriptRoot
$artifactRoot = [System.IO.Path]::GetFullPath(
    (Join-Path $repoRoot $ArtifactDirectory))
$shellExe = Join-Path $repoRoot `
    "src\OpenVisionLab.ThreeD.Shell\bin\Release\net10.0-windows10.0.19041\OpenVisionLab.ThreeD.Shell.exe"
$recipePath = Join-Path $repoRoot `
    "artifacts\current\20260729-completeness-threshold-assistance\validation-set-fixture\completeness-threshold-fixture.ov3d-recipe.json"
$validationManifestPath = "$recipePath.validation-set.json"
$runRecordPath = Join-Path $repoRoot `
    "artifacts\current\20260729-completeness-results-overlays\runner-record.json"
$ffmpeg = (Get-Command ffmpeg -ErrorAction Stop).Source
$ffprobe = (Get-Command ffprobe -ErrorAction Stop).Source

if ($WideOnly -and $CompactOnly) {
    throw "WideOnly and CompactOnly cannot be used together."
}

foreach ($required in @(
        $shellExe,
        $recipePath,
        $validationManifestPath,
        $runRecordPath)) {
    if (-not (Test-Path -LiteralPath $required -PathType Leaf)) {
        throw "Required novice replay input is missing: $required"
    }
}

New-Item -ItemType Directory -Force -Path $artifactRoot | Out-Null
New-Item -ItemType Directory -Force `
    -Path (Join-Path $artifactRoot "keyframes") | Out-Null

Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
if (-not ("OpenVisionNoviceNativeInput" -as [type])) {
    Add-Type @'
using System;
using System.Runtime.InteropServices;
using System.Threading;

public static class OpenVisionNoviceNativeInput
{
    private delegate bool EnumWindowsProc(IntPtr windowHandle, IntPtr parameter);

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [DllImport("user32.dll")]
    public static extern bool SetCursorPos(int x, int y);

    [DllImport("user32.dll")]
    public static extern bool SetForegroundWindow(IntPtr windowHandle);

    [DllImport("user32.dll")]
    public static extern bool ShowWindow(IntPtr windowHandle, int command);

    [DllImport("user32.dll")]
    public static extern bool SetWindowPos(
        IntPtr windowHandle,
        IntPtr insertAfter,
        int x,
        int y,
        int width,
        int height,
        uint flags);

    [DllImport("user32.dll")]
    public static extern void mouse_event(
        uint flags,
        uint dx,
        uint dy,
        uint data,
        UIntPtr extraInfo);

    [DllImport("user32.dll")]
    public static extern void keybd_event(
        byte virtualKey,
        byte scanCode,
        uint flags,
        UIntPtr extraInfo);

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(
        EnumWindowsProc callback,
        IntPtr parameter);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(
        IntPtr windowHandle,
        out uint processId);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr windowHandle);

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(
        IntPtr windowHandle,
        out NativeRect rectangle);

    public static IntPtr FindLargestVisibleWindow(uint processId)
    {
        var bestWindow = IntPtr.Zero;
        long bestArea = 0;
        EnumWindows(
            (windowHandle, parameter) =>
            {
                uint ownerProcessId;
                NativeRect rectangle;
                GetWindowThreadProcessId(windowHandle, out ownerProcessId);
                if (ownerProcessId != processId ||
                    !IsWindowVisible(windowHandle) ||
                    !GetWindowRect(windowHandle, out rectangle))
                {
                    return true;
                }

                var width = Math.Max(0, rectangle.Right - rectangle.Left);
                var height = Math.Max(0, rectangle.Bottom - rectangle.Top);
                var area = (long)width * height;
                if (area > bestArea)
                {
                    bestArea = area;
                    bestWindow = windowHandle;
                }
                return true;
            },
            IntPtr.Zero);
        return bestWindow;
    }

    public static void Move(int x, int y)
    {
        SetCursorPos(x, y);
    }

    public static void Click(int x, int y)
    {
        SetCursorPos(x, y);
        Thread.Sleep(320);
        mouse_event(0x0002, 0, 0, 0, UIntPtr.Zero);
        Thread.Sleep(110);
        mouse_event(0x0004, 0, 0, 0, UIntPtr.Zero);
    }

    public static void PressSpace()
    {
        keybd_event(0x20, 0, 0, UIntPtr.Zero);
        Thread.Sleep(110);
        keybd_event(0x20, 0, 0x0002, UIntPtr.Zero);
    }
}
'@
}

$script:scenarioClock = $null
$script:timelinePath = $null
$script:activeWindowHandle = [IntPtr]::Zero
$script:activeWindowWidth = 0
$script:activeWindowHeight = 0

function Restore-NoviceWindow {
    if ($script:activeWindowHandle -eq [IntPtr]::Zero) {
        return
    }

    [OpenVisionNoviceNativeInput]::ShowWindow(
        $script:activeWindowHandle,
        3) | Out-Null
    Start-Sleep -Milliseconds 120
    [OpenVisionNoviceNativeInput]::ShowWindow(
        $script:activeWindowHandle,
        9) | Out-Null
    [OpenVisionNoviceNativeInput]::SetWindowPos(
        $script:activeWindowHandle,
        [IntPtr](-1),
        0,
        0,
        $script:activeWindowWidth,
        $script:activeWindowHeight,
        0x0040) | Out-Null
    [OpenVisionNoviceNativeInput]::SetForegroundWindow(
        $script:activeWindowHandle) | Out-Null
    Start-Sleep -Milliseconds 250
}

function Write-NoviceEvent {
    param(
        [Parameter(Mandatory)][string]$Event,
        [string]$Detail = ""
    )

    $elapsed = if ($script:scenarioClock) {
        [Math]::Round($script:scenarioClock.Elapsed.TotalSeconds, 3)
    } else {
        0
    }
    $record = [ordered]@{
        elapsedSeconds = $elapsed
        event = $Event
        detail = $Detail
        capturedAt = [DateTimeOffset]::Now.ToString("O")
    }
    ($record | ConvertTo-Json -Compress) |
        Add-Content -LiteralPath $script:timelinePath -Encoding UTF8
}

function Get-WindowRoot {
    param(
        [Parameter(Mandatory)][System.Diagnostics.Process]$Process,
        [int]$TimeoutSeconds = 30
    )

    $deadline = [DateTime]::UtcNow.AddSeconds($TimeoutSeconds)
    do {
        Start-Sleep -Milliseconds 250
        $Process.Refresh()
    } while (
        $Process.MainWindowHandle -eq 0 -and
        [DateTime]::UtcNow -lt $deadline)
    if ($Process.MainWindowHandle -eq 0) {
        throw "OpenVisionLab 3D Studio main window did not appear."
    }
    return [System.Windows.Automation.AutomationElement]::FromHandle(
        $Process.MainWindowHandle)
}

function Find-NoviceElement {
    param(
        [Parameter(Mandatory)]
        [System.Windows.Automation.AutomationElement]$Root,
        [string]$AutomationId,
        [string]$Name
    )

    $useId = -not [string]::IsNullOrWhiteSpace($AutomationId)
    $useName = -not [string]::IsNullOrWhiteSpace($Name)
    if ($useId -eq $useName) {
        throw "Specify exactly one of AutomationId or Name."
    }
    $property = if ($useId) {
        [System.Windows.Automation.AutomationElement]::AutomationIdProperty
    } else {
        [System.Windows.Automation.AutomationElement]::NameProperty
    }
    $value = if ($useId) { $AutomationId } else { $Name }
    $condition = New-Object System.Windows.Automation.PropertyCondition(
        $property,
        $value)
    $matches = $Root.FindAll(
        [System.Windows.Automation.TreeScope]::Descendants,
        $condition)
    $rootRectangle = $Root.Current.BoundingRectangle
    foreach ($element in $matches) {
        try {
            $rectangle = $element.Current.BoundingRectangle
            $visible = -not $element.Current.IsOffscreen `
                -and $rectangle.Width -ge 2 `
                -and $rectangle.Height -ge 2 `
                -and $rectangle.Right -gt $rootRectangle.Left `
                -and $rectangle.Left -lt $rootRectangle.Right `
                -and $rectangle.Bottom -gt $rootRectangle.Top `
                -and $rectangle.Top -lt $rootRectangle.Bottom
            if ($visible) {
                return $element
            }
        } catch {
            continue
        }
    }
    return $null
}

function Move-NoviceToElement {
    param(
        [Parameter(Mandatory)]
        [System.Windows.Automation.AutomationElement]$Root,
        [Parameter(Mandatory)][string]$AutomationId,
        [Parameter(Mandatory)][string]$Event,
        [int]$PauseMilliseconds = 1400
    )

    Restore-NoviceWindow
    $element = Find-NoviceElement -Root $Root -AutomationId $AutomationId
    if (-not $element) {
        Write-NoviceEvent $Event "missing=true;id=$AutomationId"
        return
    }
    $rectangle = $element.Current.BoundingRectangle
    $x = [int]($rectangle.X + $rectangle.Width / 2)
    $y = [int]($rectangle.Y + $rectangle.Height / 2)
    $rectangle = $element.Current.BoundingRectangle
    $x = [int]($rectangle.X + $rectangle.Width / 2)
    $y = [int]($rectangle.Y + $rectangle.Height / 2)
    [OpenVisionNoviceNativeInput]::Move($x, $y)
    Write-NoviceEvent $Event (
        "id=$AutomationId;name=$($element.Current.Name);" +
        "enabled=$($element.Current.IsEnabled);x=$x;y=$y")
    Start-Sleep -Milliseconds $PauseMilliseconds
}

function Click-NoviceElement {
    param(
        [Parameter(Mandatory)]
        [System.Windows.Automation.AutomationElement]$Root,
        [string]$AutomationId,
        [string]$Name,
        [Parameter(Mandatory)][string]$Event,
        [int]$PauseMilliseconds = 2200,
        [int]$MinimumY = -1
    )

    Restore-NoviceWindow
    $element = Find-NoviceElement `
        -Root $Root `
        -AutomationId $AutomationId `
        -Name $Name
    if ($element -and
        $MinimumY -ge 0 -and
        $element.Current.BoundingRectangle.Y -lt $MinimumY) {
        $property = if (-not [string]::IsNullOrWhiteSpace($AutomationId)) {
            [System.Windows.Automation.AutomationElement]::AutomationIdProperty
        } else {
            [System.Windows.Automation.AutomationElement]::NameProperty
        }
        $value = if (-not [string]::IsNullOrWhiteSpace($AutomationId)) {
            $AutomationId
        } else {
            $Name
        }
        $condition = New-Object System.Windows.Automation.PropertyCondition(
            $property,
            $value)
        $element = $Root.FindAll(
                [System.Windows.Automation.TreeScope]::Descendants,
                $condition) |
            Where-Object {
                $_.Current.BoundingRectangle.Y -ge $MinimumY
            } |
            Select-Object -First 1
    }
    if (-not $element) {
        Write-NoviceEvent $Event "missing=true;id=$AutomationId;name=$Name"
        return $false
    }
    $rectangle = $element.Current.BoundingRectangle
    $x = [int]($rectangle.X + $rectangle.Width / 2)
    $y = [int]($rectangle.Y + $rectangle.Height / 2)
    [OpenVisionNoviceNativeInput]::Click($x, $y)
    Write-NoviceEvent $Event (
        "id=$($element.Current.AutomationId);name=$($element.Current.Name);" +
        "enabled=$($element.Current.IsEnabled);x=$x;y=$y")
    Start-Sleep -Milliseconds $PauseMilliseconds
    return $true
}

function Press-NoviceElement {
    param(
        [Parameter(Mandatory)]
        [System.Windows.Automation.AutomationElement]$Root,
        [Parameter(Mandatory)][string]$AutomationId,
        [Parameter(Mandatory)][string]$Event,
        [int]$PauseMilliseconds = 2200
    )

    Restore-NoviceWindow
    $element = Find-NoviceElement -Root $Root -AutomationId $AutomationId
    if (-not $element) {
        Write-NoviceEvent $Event "missing=true;id=$AutomationId"
        return $false
    }
    try {
        $element.SetFocus()
        Start-Sleep -Milliseconds 250
        [OpenVisionNoviceNativeInput]::PressSpace()
    } catch {
        Write-NoviceEvent $Event (
            "focusFailed=true;id=$($element.Current.AutomationId);" +
            "name=$($element.Current.Name);error=$($_.Exception.Message)")
        return $false
    }
    Write-NoviceEvent $Event (
        "id=$($element.Current.AutomationId);name=$($element.Current.Name);" +
        "enabled=$($element.Current.IsEnabled);input=user32-space")
    Start-Sleep -Milliseconds $PauseMilliseconds
    return $true
}

function Start-NoviceRecorder {
    param(
        [Parameter(Mandatory)][string]$OutputPath,
        [Parameter(Mandatory)][int]$Width,
        [Parameter(Mandatory)][int]$Height,
        [Parameter(Mandatory)][int]$DurationSeconds
    )

    if (Test-Path -LiteralPath $OutputPath) {
        Remove-Item -LiteralPath $OutputPath -Force
    }
    $arguments = @(
        "-hide_banner", "-loglevel", "warning", "-y",
        "-f", "gdigrab",
        "-draw_mouse", "1",
        "-framerate", "15",
        "-i", ("hwnd=0x{0:X}" -f $script:activeWindowHandle.ToInt64()),
        "-t", $DurationSeconds.ToString(),
        "-vf", "pad=ceil(iw/2)*2:ceil(ih/2)*2",
        "-c:v", "libx264",
        "-preset", "veryfast",
        "-crf", "21",
        "-pix_fmt", "yuv420p",
        $OutputPath)
    $ffmpegLogPath = [System.IO.Path]::ChangeExtension(
        $OutputPath,
        ".ffmpeg.log")
    if (Test-Path -LiteralPath $ffmpegLogPath) {
        Remove-Item -LiteralPath $ffmpegLogPath -Force
    }
    $process = Start-Process `
        -FilePath $ffmpeg `
        -ArgumentList $arguments `
        -PassThru `
        -RedirectStandardError $ffmpegLogPath `
        -WindowStyle Hidden
    Start-Sleep -Seconds 2
    Write-NoviceEvent "recording-started" (
        "pid=$($process.Id);video=$OutputPath;duration=$DurationSeconds")
    return $process
}

function Start-NoviceApplication {
    param(
        [Parameter(Mandatory)][int]$Width,
        [Parameter(Mandatory)][int]$Height
    )

    $arguments = @(
        "--tool-teaching-recipe", $recipePath,
        "--run-record", $runRecordPath,
        "--shell-workspace", "Workbench")
    $process = Start-Process `
        -FilePath $shellExe `
        -ArgumentList $arguments `
        -PassThru
    $root = Get-WindowRoot -Process $process
    [OpenVisionNoviceNativeInput]::ShowWindow(
        $process.MainWindowHandle,
        9) | Out-Null
    [OpenVisionNoviceNativeInput]::SetWindowPos(
        $process.MainWindowHandle,
        [IntPtr](-1),
        0,
        0,
        $Width,
        $Height,
        0x0040) | Out-Null
    [OpenVisionNoviceNativeInput]::SetForegroundWindow(
        $process.MainWindowHandle) | Out-Null
    $script:activeWindowHandle = $process.MainWindowHandle
    $script:activeWindowWidth = $Width
    $script:activeWindowHeight = $Height
    Start-Sleep -Seconds 7
    $process.Refresh()
    $largestWindow =
        [OpenVisionNoviceNativeInput]::FindLargestVisibleWindow(
            [uint32]$process.Id)
    $script:activeWindowHandle = if ($largestWindow -ne [IntPtr]::Zero) {
        $largestWindow
    } else {
        $process.MainWindowHandle
    }
    $script:activeWindowWidth = $Width
    $script:activeWindowHeight = $Height
    Restore-NoviceWindow
    $root = [System.Windows.Automation.AutomationElement]::FromHandle(
        $script:activeWindowHandle)
    Write-NoviceEvent "application-ready" (
        "pid=$($process.Id);window=${Width}x${Height};" +
        "languageArgument=none;recipe=$recipePath;runRecord=$runRecordPath")
    return [pscustomobject]@{
        Process = $process
        Root = $root
    }
}

function Stop-NoviceApplication {
    param([Parameter(Mandatory)]$Application)

    if (-not $Application.Process.HasExited) {
        $Application.Process.CloseMainWindow() | Out-Null
        if (-not $Application.Process.WaitForExit(6000)) {
            Stop-Process -Id $Application.Process.Id -Force
            $Application.Process.WaitForExit()
        }
    }
    Write-NoviceEvent "application-closed" "pid=$($Application.Process.Id)"
}

function Click-ValidationSampleSetRun {
    param(
        [Parameter(Mandatory)]
        [System.Windows.Automation.AutomationElement]$Root
    )

    $clicked = Click-NoviceElement `
        -Root $Root `
        -AutomationId "ValidationSetRunAllButton" `
        -Event "execute-validation-sample-set" `
        -PauseMilliseconds 9000
    if (-not $clicked) {
        Start-Sleep -Milliseconds 800
        $Root = [System.Windows.Automation.AutomationElement]::FromHandle(
            $script:activeWindowHandle)
        $clicked = Click-NoviceElement `
            -Root $Root `
            -AutomationId "ValidationSetRunAllButton" `
            -Event "execute-validation-sample-set-uia-retry" `
            -PauseMilliseconds 9000
    }
    if (-not $clicked) {
        $clicked = Click-NoviceElement `
            -Root $Root `
            -Name "$([char]0xC0D8)$([char]0xD50C) $([char]0xC138)$([char]0xD2B8) $([char]0xC2E4)$([char]0xD589)" `
            -Event "execute-validation-sample-set-name-fallback" `
            -PauseMilliseconds 9000
    }
    if (-not $clicked) {
        Restore-NoviceWindow
        $navigation = Find-NoviceElement `
            -Root $Root `
            -AutomationId "ValidationSamplesNavigation"
        if ($navigation) {
            $rootRectangle = $Root.Current.BoundingRectangle
            $navigationRectangle = $navigation.Current.BoundingRectangle
            $x = [int]($rootRectangle.X + $rootRectangle.Width - 324)
            $y = [int](
                $navigationRectangle.Y +
                $navigationRectangle.Height / 2 +
                55)
            [OpenVisionNoviceNativeInput]::Click($x, $y)
            Write-NoviceEvent "execute-validation-sample-set-layout-fallback" (
                "x=$x;y=$y;anchor=ValidationSamplesNavigation")
            Start-Sleep -Seconds 9
            $clicked = $true
        }
    }
    return $clicked
}

function Invoke-OwnerPathScenario {
    param(
        [Parameter(Mandatory)]
        [System.Windows.Automation.AutomationElement]$Root,
        [Parameter(Mandatory)]
        [System.Diagnostics.Process]$Process
    )

    Write-NoviceEvent "owner-path-goal" `
        "Run sample set, open a failure in Teach, inspect Results, open Advanced, return to Results, and confirm Validation evidence remains."
    Click-NoviceElement `
        -Root $Root `
        -AutomationId "ValidateModeButton" `
        -Event "enter-validate-for-owner-path" | Out-Null
    $Root = [System.Windows.Automation.AutomationElement]::FromHandle(
        $script:activeWindowHandle)
    Click-NoviceElement `
        -Root $Root `
        -AutomationId "ValidationSamplesNavigation" `
        -Event "open-validation-samples-for-run" `
        -PauseMilliseconds 1600 | Out-Null
    $Root = [System.Windows.Automation.AutomationElement]::FromHandle(
        $script:activeWindowHandle)
    Click-ValidationSampleSetRun -Root $Root | Out-Null
    $failureAnalysisOpened = Click-NoviceElement `
        -Root $Root `
        -AutomationId "ValidationFailuresNavigation" `
        -Event "open-failure-analysis"
    $Root = [System.Windows.Automation.AutomationElement]::FromHandle(
        $script:activeWindowHandle)
    $openInTeach = Find-NoviceElement `
        -Root $Root `
        -AutomationId "OpenValidationIssueInTeach"
    if ($failureAnalysisOpened -and -not $openInTeach) {
        Click-NoviceElement `
            -Root $Root `
            -AutomationId "ValidationFailuresNavigation" `
            -Event "open-failure-analysis-retry" `
            -PauseMilliseconds 1800 | Out-Null
        $Root = [System.Windows.Automation.AutomationElement]::FromHandle(
            $script:activeWindowHandle)
    }
    Move-NoviceToElement `
        -Root $Root `
        -AutomationId "OpenValidationIssueInTeach" `
        -Event "recognize-open-failure-in-teach" `
        -PauseMilliseconds 1300
    Click-NoviceElement `
        -Root $Root `
        -AutomationId "OpenValidationIssueInTeach" `
        -Event "open-failure-in-teach" `
        -PauseMilliseconds 3500 | Out-Null
    $Root = [System.Windows.Automation.AutomationElement]::FromHandle(
        $script:activeWindowHandle)
    $preview = Find-NoviceElement `
        -Root $Root `
        -AutomationId "PreviewTeachingToolButton"
    if (-not $preview) {
        Press-NoviceElement `
            -Root $Root `
            -AutomationId "OpenValidationIssueInTeach" `
            -Event "open-failure-in-teach-keyboard-fallback" `
            -PauseMilliseconds 3500 | Out-Null
        $Root = [System.Windows.Automation.AutomationElement]::FromHandle(
            $script:activeWindowHandle)
    }
    Move-NoviceToElement `
        -Root $Root `
        -AutomationId "PreviewTeachingToolButton" `
        -Event "confirm-failed-step-selected-in-teach" `
        -PauseMilliseconds 1800
    if ($TeachCorrectionOnly) {
        Move-NoviceToElement `
            -Root $Root `
            -AutomationId "TeachFailureCorrectionContext" `
            -Event "confirm-failure-context-in-teach" `
            -PauseMilliseconds 4500
        return
    }
    Click-NoviceElement `
        -Root $Root `
        -AutomationId "ResultsModeButton" `
        -Event "open-results-after-failure" `
        -PauseMilliseconds 2500 | Out-Null
    $Root = [System.Windows.Automation.AutomationElement]::FromHandle(
        $script:activeWindowHandle)
    Move-NoviceToElement `
        -Root $Root `
        -AutomationId "ResultsRunRecordNavigation" `
        -Event "confirm-run-record-after-failure" `
        -PauseMilliseconds 1800
    Click-NoviceElement `
        -Root $Root `
        -AutomationId "ResultsAdvancedDiagnostics" `
        -Event "open-advanced-from-results" `
        -PauseMilliseconds 3500 | Out-Null
    $Root = [System.Windows.Automation.AutomationElement]::FromHandle(
        $script:activeWindowHandle)
    $advancedViewer = Find-NoviceElement `
        -Root $Root `
        -AutomationId "ViewerFitAll"
    if (-not $advancedViewer) {
        Write-NoviceEvent "advanced-viewer-visible-assertion" `
            "passed=false;expected=ViewerFitAll"
        throw "Advanced workspace did not expose a visible 3D Viewer."
    }
    Move-NoviceToElement `
        -Root $Root `
        -AutomationId "ViewerFitAll" `
        -Event "advanced-viewer-visible-assertion" `
        -PauseMilliseconds 1600
    Click-NoviceElement `
        -Root $Root `
        -AutomationId "ResultsModeButton" `
        -Event "return-results-from-advanced" `
        -PauseMilliseconds 2800 | Out-Null
    $Root = [System.Windows.Automation.AutomationElement]::FromHandle(
        $script:activeWindowHandle)
    Move-NoviceToElement `
        -Root $Root `
        -AutomationId "ResultsRunRecordNavigation" `
        -Event "confirm-results-preserved" `
        -PauseMilliseconds 1800
    Click-NoviceElement `
        -Root $Root `
        -AutomationId "ValidateModeButton" `
        -Event "return-validation-after-results" `
        -PauseMilliseconds 2200 | Out-Null
    $Root = [System.Windows.Automation.AutomationElement]::FromHandle(
        $script:activeWindowHandle)
    $failureEvidenceClicked = Click-NoviceElement `
        -Root $Root `
        -AutomationId "ValidationFailuresNavigation" `
        -Event "confirm-failure-evidence-preserved" `
        -PauseMilliseconds 2500
    $Root = [System.Windows.Automation.AutomationElement]::FromHandle(
        $script:activeWindowHandle)
    $finalFailureEvidence = Find-NoviceElement `
        -Root $Root `
        -AutomationId "OpenValidationIssueInTeach"
    if ($failureEvidenceClicked -and -not $finalFailureEvidence) {
        Click-NoviceElement `
            -Root $Root `
            -AutomationId "ValidationFailuresNavigation" `
            -Event "confirm-failure-evidence-preserved-retry" `
            -PauseMilliseconds 2500 | Out-Null
        $Root = [System.Windows.Automation.AutomationElement]::FromHandle(
            $script:activeWindowHandle)
        $finalFailureEvidence = Find-NoviceElement `
            -Root $Root `
            -AutomationId "OpenValidationIssueInTeach"
    }
    if (-not $finalFailureEvidence) {
        Write-NoviceEvent "final-failure-evidence-assertion" `
            "passed=false;expected=OpenValidationIssueInTeach"
        throw "Final Failure Analysis evidence did not become visible."
    }
    Move-NoviceToElement `
        -Root $Root `
        -AutomationId "OpenValidationIssueInTeach" `
        -Event "final-failure-evidence-assertion" `
        -PauseMilliseconds 2500
}

function Invoke-NoviceScenario {
    param(
        [Parameter(Mandatory)][string]$Name,
        [Parameter(Mandatory)][int]$Width,
        [Parameter(Mandatory)][int]$Height,
        [Parameter(Mandatory)][int]$DurationSeconds
    )

    $script:timelinePath = Join-Path $artifactRoot "$Name-timeline.jsonl"
    if (Test-Path -LiteralPath $script:timelinePath) {
        Remove-Item -LiteralPath $script:timelinePath -Force
    }
    $script:scenarioClock = [System.Diagnostics.Stopwatch]::StartNew()
    Write-NoviceEvent "scenario-started" "$Name;noviceNoDocumentation=true"
    $videoPath = Join-Path $artifactRoot "$Name.mp4"
    $application = $null
    $recorder = $null
    try {
        $application = Start-NoviceApplication -Width $Width -Height $Height
        Restore-NoviceWindow
        $recorder = Start-NoviceRecorder `
            -OutputPath $videoPath `
            -Width $Width `
            -Height $Height `
            -DurationSeconds $DurationSeconds
        $recordingStartedAtSeconds =
            $script:scenarioClock.Elapsed.TotalSeconds

        if ($OwnerPath) {
            Invoke-OwnerPathScenario `
                -Root $application.Root `
                -Process $application.Process
        } else {
            Write-NoviceEvent "novice-goal" `
                "Find where to configure, teach, validate, inspect results, and open advanced diagnostics."
            Start-Sleep -Seconds 3

            Move-NoviceToElement `
                -Root $application.Root `
                -AutomationId "TeachModeButton" `
                -Event "recognize-teach-stage"
            Click-NoviceElement `
                -Root $application.Root `
                -AutomationId "TeachModeButton" `
                -Event "enter-teach-stage" | Out-Null
            $application.Root = [System.Windows.Automation.AutomationElement]::FromHandle(
                $script:activeWindowHandle)
            Move-NoviceToElement `
                -Root $application.Root `
                -AutomationId "PreviewTeachingToolButton" `
                -Event "seek-preview-action"
            Start-Sleep -Seconds 2

            Move-NoviceToElement `
                -Root $application.Root `
                -AutomationId "ValidateModeButton" `
                -Event "enter-validate-stage" | Out-Null
            $application.Root = [System.Windows.Automation.AutomationElement]::FromHandle(
                $script:activeWindowHandle)
            foreach ($id in @(
                    "ValidationSamplesNavigation",
                    "ValidationResultsNavigation",
                    "ValidationFailuresNavigation",
                    "ValidationThresholdNavigation",
                    "ValidationHeldOutNavigation")) {
                Move-NoviceToElement `
                    -Root $application.Root `
                    -AutomationId $id `
                    -Event "inspect-validate-local-navigation" `
                    -PauseMilliseconds 900
            }
            $runAllName =
                "$([char]0xC0D8)$([char]0xD50C) " +
                "$([char]0xC138)$([char]0xD2B8) " +
                "$([char]0xC2E4)$([char]0xD589)"
            Click-NoviceElement `
                -Root $application.Root `
                -Name $runAllName `
                -Event "attempt-run-all" `
                -MinimumY 180 | Out-Null

            Click-NoviceElement `
                -Root $application.Root `
                -AutomationId "ResultsModeButton" `
                -Event "enter-results-stage" | Out-Null
            $application.Root = [System.Windows.Automation.AutomationElement]::FromHandle(
                $script:activeWindowHandle)
            foreach ($id in @(
                    "ResultsRunRecordNavigation",
                    "ResultsOutputCompareNavigation",
                    "ResultsReportsNavigation")) {
                Move-NoviceToElement `
                    -Root $application.Root `
                    -AutomationId $id `
                    -Event "inspect-results-local-navigation" `
                    -PauseMilliseconds 1100
            }
            Click-NoviceElement `
                -Root $application.Root `
                -AutomationId "ResultsAdvancedDiagnostics" `
                -Event "attempt-open-advanced-diagnostics" | Out-Null
            Start-Sleep -Seconds 3

            Click-NoviceElement `
                -Root $application.Root `
                -AutomationId "WorkbenchModeButton" `
                -Event "return-setup-stage" | Out-Null
            Start-Sleep -Seconds 2
        }

        $remaining = [Math]::Ceiling(
            $DurationSeconds -
            ($script:scenarioClock.Elapsed.TotalSeconds -
                $recordingStartedAtSeconds) +
            2)
        if ($remaining -gt 0) {
            Start-Sleep -Seconds $remaining
        }
        if (-not $recorder.WaitForExit(10000)) {
            throw "ffmpeg recording did not complete: $videoPath"
        }
        $recorder.Refresh()
        if ($recorder.ExitCode -ne 0) {
            Write-NoviceEvent "recording-exit-warning" (
                "video=$videoPath;exitCode=$($recorder.ExitCode);" +
                "media verification will decide acceptance")
        }
        if (-not (Test-Path -LiteralPath $videoPath) -or
            (Get-Item -LiteralPath $videoPath).Length -eq 0) {
            throw "ffmpeg recording failed: $videoPath"
        }
        Write-NoviceEvent "recording-completed" (
            "video=$videoPath;bytes=$((Get-Item -LiteralPath $videoPath).Length)")
    }
    finally {
        if ($recorder -and -not $recorder.HasExited) {
            Stop-Process -Id $recorder.Id -Force -ErrorAction SilentlyContinue
        }
        if ($application) {
            Stop-NoviceApplication -Application $application
        }
        Write-NoviceEvent "scenario-completed" $Name
        $script:scenarioClock.Stop()
    }
}

function New-NoviceContactSheet {
    param(
        [Parameter(Mandatory)][string]$VideoPath,
        [Parameter(Mandatory)][string]$OutputPath
    )

    & $ffmpeg -hide_banner -loglevel error -y `
        -i $VideoPath `
        -vf "fps=1/8,scale=480:-1,tile=4x2:padding=4:margin=4" `
        -frames:v 1 `
        $OutputPath
    if ($LASTEXITCODE -ne 0) {
        throw "Contact sheet generation failed: $VideoPath"
    }
}

if (-not $PostProcessOnly) {
    $wideName = if ($OwnerPath) {
        "01-wide-ia4b-owner-path"
    } else {
        "01-wide-novice-stage-navigation"
    }
    $compactName = if ($OwnerPath) {
        "02-compact-ia4b-owner-path"
    } else {
        "02-compact-novice-stage-navigation"
    }
    if (-not $CompactOnly) {
        Invoke-NoviceScenario `
            -Name $wideName `
            -Width 1920 `
            -Height 1040 `
            -DurationSeconds $WideDurationSeconds
    }
    if (-not $WideOnly) {
        Invoke-NoviceScenario `
            -Name $compactName `
            -Width 1280 `
            -Height 760 `
            -DurationSeconds $CompactDurationSeconds
    }
}

$wideVideoName = if ($OwnerPath) {
    "01-wide-ia4b-owner-path.mp4"
} else {
    "01-wide-novice-stage-navigation.mp4"
}
$compactVideoName = if ($OwnerPath) {
    "02-compact-ia4b-owner-path.mp4"
} else {
    "02-compact-novice-stage-navigation.mp4"
}
$videos = @(
    Join-Path $artifactRoot $wideVideoName
    Join-Path $artifactRoot $compactVideoName)
$mediaLines = [System.Collections.Generic.List[string]]::new()
$mediaLines.Add("OpenVisionLab 3D novice stage-navigation video verification")
foreach ($video in $videos) {
    $contactSheet = Join-Path $artifactRoot (
        "$([System.IO.Path]::GetFileNameWithoutExtension($video))-contact-sheet.png")
    New-NoviceContactSheet -VideoPath $video -OutputPath $contactSheet
    $probe = & $ffprobe -v error `
        -select_streams v:0 `
        -show_entries "stream=width,height,avg_frame_rate,pix_fmt:format=duration,size" `
        -of "default=noprint_wrappers=1" `
        $video
    if ($LASTEXITCODE -ne 0) {
        throw "ffprobe failed: $video"
    }
    $mediaLines.Add("Video|path=$video")
    foreach ($line in $probe) {
        $mediaLines.Add($line)
    }
    $mediaLines.Add("ContactSheet|path=$contactSheet")
}
$mediaLines |
    Set-Content `
        -LiteralPath (Join-Path $artifactRoot "media-verification.txt") `
        -Encoding UTF8

@(
    "OpenVisionLab 3D novice stage-navigation replay environment"
    "CapturedAt=$([DateTimeOffset]::Now.ToString('O'))"
    "GitCommit=$(& git -C $repoRoot rev-parse HEAD)"
    "GitBranch=$(& git -C $repoRoot branch --show-current)"
    "ShellExe=$shellExe"
    "ShellExeTimestamp=$((Get-Item -LiteralPath $shellExe).LastWriteTime.ToString('O'))"
    "ShellExeSha256=$((Get-FileHash -LiteralPath $shellExe -Algorithm SHA256).Hash)"
    "ShellAssembly=$([System.IO.Path]::ChangeExtension($shellExe, '.dll'))"
    "ShellAssemblyTimestamp=$((Get-Item -LiteralPath ([System.IO.Path]::ChangeExtension($shellExe, '.dll'))).LastWriteTime.ToString('O'))"
    "ShellAssemblySha256=$((Get-FileHash -LiteralPath ([System.IO.Path]::ChangeExtension($shellExe, '.dll')) -Algorithm SHA256).Hash)"
    "Recipe=$recipePath"
    "ValidationManifest=$validationManifestPath"
    "RunRecord=$runRecordPath"
    "Ffmpeg=$ffmpeg"
    "FfmpegVersion=$((& $ffmpeg -version | Select-Object -First 1))"
    "InputBoundary=actual Release WPF window; external UI Automation lookup; user32 pointer movement and clicks"
    "NoviceBoundary=no documentation lookup; no hidden in-product command invocation; identifiers used only to locate visible controls"
    "ClaimBoundary=simulated novice usability evidence, not human-owner unaided acceptance, physical calibration, or metrology"
) | Set-Content `
    -LiteralPath (Join-Path $artifactRoot "environment.txt") `
    -Encoding UTF8

Write-Output "NoviceStageNavigationVideo|Pass|artifacts=$artifactRoot"
