param(
    [string]$ArtifactDirectory = "artifacts\current\20260728-dual-roi-role-preservation",
    [string]$ReadmeGifPath = "docs\assets\openvisionlab-3d-roi-workflow.gif",
    [switch]$SkipBuild,
    [switch]$SkipWideReplay,
    [switch]$CompactOnly
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$repoRoot = Split-Path -Parent $PSScriptRoot
$artifactRoot = [System.IO.Path]::GetFullPath((Join-Path $repoRoot $ArtifactDirectory))
$readmeGif = [System.IO.Path]::GetFullPath((Join-Path $repoRoot $ReadmeGifPath))
$shellExe = Join-Path $repoRoot "src\OpenVisionLab.ThreeD.Shell\bin\Release\net10.0-windows10.0.19041\OpenVisionLab.ThreeD.Shell.exe"
$thicknessBaseline = Join-Path $repoRoot "3D\SyntheticValidation\ThicknessCouponV1\inspection-recipe.ov3d-recipe.json"
$boxBaseline = Join-Path $repoRoot "3D\SyntheticValidation\ThicknessCouponV1\oriented-box-demo.ov3d-recipe.json"
$ffmpeg = (Get-Command ffmpeg -ErrorAction Stop).Source
$ffprobe = (Get-Command ffprobe -ErrorAction Stop).Source

New-Item -ItemType Directory -Force -Path $artifactRoot | Out-Null
New-Item -ItemType Directory -Force -Path (Join-Path $artifactRoot "keyframes") | Out-Null
New-Item -ItemType Directory -Force -Path (Split-Path -Parent $readmeGif) | Out-Null

if (-not $SkipBuild) {
    & dotnet build (Join-Path $repoRoot "OpenVisionLab.ThreeDStudio.sln") -c Release '-p:Platform=Any CPU'
    if ($LASTEXITCODE -ne 0) {
        throw "Release build failed."
    }
}

foreach ($required in @($shellExe, $thicknessBaseline, $boxBaseline)) {
    if (-not (Test-Path -LiteralPath $required -PathType Leaf)) {
        throw "Required operator-replay input is missing: $required"
    }
}

Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
Add-Type -AssemblyName System.Windows.Forms
if (-not ("OpenVisionOperatorNativeInput" -as [type])) {
    Add-Type @'
using System;
using System.Runtime.InteropServices;
using System.Threading;

public static class OpenVisionOperatorNativeInput
{
    [DllImport("user32.dll")]
    public static extern bool SetCursorPos(int x, int y);

    [DllImport("user32.dll")]
    public static extern bool SetForegroundWindow(IntPtr windowHandle);

    [DllImport("user32.dll")]
    public static extern void keybd_event(
        byte virtualKey,
        byte scanCode,
        uint flags,
        UIntPtr extraInfo);

    [DllImport("user32.dll")]
    public static extern void mouse_event(
        uint flags,
        uint dx,
        uint dy,
        uint data,
        UIntPtr extraInfo);

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

    public static void Click(int x, int y)
    {
        SetCursorPos(x, y);
        Thread.Sleep(260);
        mouse_event(0x0002, 0, 0, 0, UIntPtr.Zero);
        Thread.Sleep(90);
        mouse_event(0x0004, 0, 0, 0, UIntPtr.Zero);
    }

    public static void Drag(int startX, int startY, int endX, int endY)
    {
        SetCursorPos(startX, startY);
        Thread.Sleep(260);
        mouse_event(0x0002, 0, 0, 0, UIntPtr.Zero);
        Thread.Sleep(120);
        const int steps = 12;
        for (var index = 1; index <= steps; index++)
        {
            var x = startX + (endX - startX) * index / steps;
            var y = startY + (endY - startY) * index / steps;
            SetCursorPos(x, y);
            Thread.Sleep(28);
        }
        mouse_event(0x0004, 0, 0, 0, UIntPtr.Zero);
    }

    public static void ControlS(IntPtr windowHandle)
    {
        SetForegroundWindow(windowHandle);
        Thread.Sleep(250);
        keybd_event(0x11, 0, 0, UIntPtr.Zero);
        keybd_event(0x53, 0, 0, UIntPtr.Zero);
        Thread.Sleep(80);
        keybd_event(0x53, 0, 0x0002, UIntPtr.Zero);
        keybd_event(0x11, 0, 0x0002, UIntPtr.Zero);
    }
}
'@
}

$script:scenarioClock = $null
$script:timelinePath = $null
$script:activeWindowHandle = [IntPtr]::Zero

function Write-OperatorEvent {
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

function Wait-WindowRoot {
    param(
        [Parameter(Mandatory)][System.Diagnostics.Process]$Process,
        [int]$TimeoutSeconds = 30
    )

    $deadline = [DateTime]::UtcNow.AddSeconds($TimeoutSeconds)
    do {
        Start-Sleep -Milliseconds 250
        $Process.Refresh()
    } while ($Process.MainWindowHandle -eq 0 -and [DateTime]::UtcNow -lt $deadline)

    if ($Process.MainWindowHandle -eq 0) {
        throw "OpenVisionLab Shell main window did not appear."
    }

    return [System.Windows.Automation.AutomationElement]::FromHandle(
        $Process.MainWindowHandle)
}

function Start-OperatorApplication {
    param(
        [Parameter(Mandatory)][string]$RecipePath,
        [Parameter(Mandatory)][string]$StepId,
        [Parameter(Mandatory)][ValidateSet("ko", "en")][string]$Language,
        [Parameter(Mandatory)][int]$Width,
        [Parameter(Mandatory)][int]$Height
    )

    $arguments = @(
        "--tool-teaching-recipe", $RecipePath,
        "--tool-teaching-step", $StepId,
        "--smoke-input-first-start",
        "--ui-language", $Language
    )
    $process = Start-Process -FilePath $shellExe -ArgumentList $arguments -PassThru
    $root = Wait-WindowRoot -Process $process
    [OpenVisionOperatorNativeInput]::ShowWindow($process.MainWindowHandle, 9) | Out-Null
    [OpenVisionOperatorNativeInput]::SetWindowPos(
        $process.MainWindowHandle,
        [IntPtr]::Zero,
        0,
        0,
        $Width,
        $Height,
        0x0040) | Out-Null
    [OpenVisionOperatorNativeInput]::SetForegroundWindow(
        $process.MainWindowHandle) | Out-Null
    $script:activeWindowHandle = $process.MainWindowHandle
    Start-Sleep -Seconds 7
    $process.Refresh()
    $root = [System.Windows.Automation.AutomationElement]::FromHandle(
        $process.MainWindowHandle)
    Write-OperatorEvent "application-ready" "pid=$($process.Id);window=${Width}x${Height};recipe=$RecipePath"
    return [pscustomobject]@{
        Process = $process
        Root = $root
    }
}

function Stop-OperatorApplication {
    param(
        [Parameter(Mandatory)]$Application,
        [switch]$UseKeyboard
    )

    if ($Application.Process.HasExited) {
        return
    }
    if ($UseKeyboard) {
        [OpenVisionOperatorNativeInput]::SetForegroundWindow(
            $Application.Process.MainWindowHandle) | Out-Null
        Start-Sleep -Milliseconds 200
        [System.Windows.Forms.SendKeys]::SendWait("%{F4}")
    } else {
        $Application.Process.CloseMainWindow() | Out-Null
    }
    if (-not $Application.Process.WaitForExit(7000)) {
        Stop-Process -Id $Application.Process.Id -Force
        $Application.Process.WaitForExit()
    }
    Write-OperatorEvent "application-closed" "pid=$($Application.Process.Id)"
}

function Wait-OperatorElement {
    param(
        [Parameter(Mandatory)][System.Windows.Automation.AutomationElement]$Root,
        [string]$AutomationId,
        [string]$Name,
        [int]$TimeoutSeconds = 12
    )

    $hasAutomationId = -not [string]::IsNullOrWhiteSpace($AutomationId)
    $hasName = -not [string]::IsNullOrWhiteSpace($Name)
    if ($hasAutomationId -eq $hasName) {
        throw "Specify exactly one of AutomationId or Name."
    }

    $property = if ($AutomationId) {
        [System.Windows.Automation.AutomationElement]::AutomationIdProperty
    } else {
        [System.Windows.Automation.AutomationElement]::NameProperty
    }
    $value = if ($AutomationId) { $AutomationId } else { $Name }
    $condition = New-Object System.Windows.Automation.PropertyCondition(
        $property,
        $value)
    $deadline = [DateTime]::UtcNow.AddSeconds($TimeoutSeconds)
    do {
        $element = $Root.FindFirst(
            [System.Windows.Automation.TreeScope]::Descendants,
            $condition)
        if ($element) {
            return $element
        }
        Start-Sleep -Milliseconds 250
    } while ([DateTime]::UtcNow -lt $deadline)

    throw "UI element was not found: $value"
}

function Click-OperatorElement {
    param(
        [Parameter(Mandatory)][System.Windows.Automation.AutomationElement]$Element,
        [Parameter(Mandatory)][string]$Event
    )

    if ($Element.Current.IsOffscreen) {
        $pattern = $null
        if ($Element.TryGetCurrentPattern(
                [System.Windows.Automation.ScrollItemPattern]::Pattern,
                [ref]$pattern)) {
            ([System.Windows.Automation.ScrollItemPattern]$pattern).ScrollIntoView()
            Start-Sleep -Milliseconds 500
        }
    }

    $rectangle = $Element.Current.BoundingRectangle
    if ($rectangle.Width -le 1 -or $rectangle.Height -le 1) {
        throw "UI element has no clickable rectangle: $($Element.Current.Name)"
    }

    $x = [int]($rectangle.X + $rectangle.Width / 2)
    $y = [int]($rectangle.Y + $rectangle.Height / 2)
    [OpenVisionOperatorNativeInput]::Click($x, $y)
    Write-OperatorEvent $Event "x=$x;y=$y;name=$($Element.Current.Name);id=$($Element.Current.AutomationId)"
    Start-Sleep -Milliseconds 950
}

function Invoke-OperatorElement {
    param(
        [Parameter(Mandatory)][System.Windows.Automation.AutomationElement]$Element,
        [Parameter(Mandatory)][string]$Event
    )

    $pattern = $null
    if (-not $Element.TryGetCurrentPattern(
            [System.Windows.Automation.InvokePattern]::Pattern,
            [ref]$pattern)) {
        throw "UI element does not expose Invoke: $($Element.Current.Name)"
    }
    ([System.Windows.Automation.InvokePattern]$pattern).Invoke()
    Write-OperatorEvent $Event "name=$($Element.Current.Name);id=$($Element.Current.AutomationId);input=UIAutomationInvoke"
    Start-Sleep -Milliseconds 950
}

function Click-OperatorPoint {
    param(
        [Parameter(Mandatory)][System.Windows.Automation.AutomationElement]$Surface,
        [Parameter(Mandatory)][double]$RelativeX,
        [Parameter(Mandatory)][double]$RelativeY,
        [Parameter(Mandatory)][string]$Event
    )

    $rectangle = $Surface.Current.BoundingRectangle
    $x = [int]($rectangle.X + $rectangle.Width * $RelativeX)
    $y = [int]($rectangle.Y + $rectangle.Height * $RelativeY)
    [OpenVisionOperatorNativeInput]::Click($x, $y)
    Write-OperatorEvent $Event "x=$x;y=$y;relative=$RelativeX,$RelativeY"
    Start-Sleep -Milliseconds 800
}

function Drag-OperatorRegion {
    param(
        [Parameter(Mandatory)][System.Windows.Automation.AutomationElement]$Surface,
        [Parameter(Mandatory)][double]$StartRelativeX,
        [Parameter(Mandatory)][double]$StartRelativeY,
        [Parameter(Mandatory)][double]$EndRelativeX,
        [Parameter(Mandatory)][double]$EndRelativeY,
        [Parameter(Mandatory)][string]$Event
    )

    $rectangle = $Surface.Current.BoundingRectangle
    $startX = [int]($rectangle.X + $rectangle.Width * $StartRelativeX)
    $startY = [int]($rectangle.Y + $rectangle.Height * $StartRelativeY)
    $endX = [int]($rectangle.X + $rectangle.Width * $EndRelativeX)
    $endY = [int]($rectangle.Y + $rectangle.Height * $EndRelativeY)
    [OpenVisionOperatorNativeInput]::Drag($startX, $startY, $endX, $endY)
    Write-OperatorEvent $Event "start=$startX,$startY;end=$endX,$endY;relative=$StartRelativeX,$StartRelativeY->$EndRelativeX,$EndRelativeY"
    Start-Sleep -Milliseconds 1000
}

function Send-OperatorKeys {
    param(
        [Parameter(Mandatory)][string]$Keys,
        [Parameter(Mandatory)][string]$Event
    )

    if ($script:activeWindowHandle -ne [IntPtr]::Zero) {
        [OpenVisionOperatorNativeInput]::SetForegroundWindow(
            $script:activeWindowHandle) | Out-Null
        Start-Sleep -Milliseconds 250
    }
    [System.Windows.Forms.SendKeys]::SendWait($Keys)
    Write-OperatorEvent $Event "keys=$Keys"
    Start-Sleep -Milliseconds 900
}

function Send-OperatorControlS {
    param([Parameter(Mandatory)][string]$Event)

    if ($script:activeWindowHandle -eq [IntPtr]::Zero) {
        throw "No active operator window is available for Ctrl+S."
    }
    [OpenVisionOperatorNativeInput]::ControlS($script:activeWindowHandle)
    Write-OperatorEvent $Event "keys=Ctrl+S;input=user32-keybd_event"
    Start-Sleep -Milliseconds 900
}

function Set-OperatorText {
    param(
        [Parameter(Mandatory)][System.Windows.Automation.AutomationElement]$Element,
        [Parameter(Mandatory)][string]$Value,
        [Parameter(Mandatory)][string]$Event
    )

    Click-OperatorElement -Element $Element -Event "$Event-focus"
    [System.Windows.Forms.SendKeys]::SendWait("^a")
    [System.Windows.Forms.SendKeys]::SendWait($Value)
    [System.Windows.Forms.SendKeys]::SendWait("{TAB}")
    Write-OperatorEvent $Event "value=$Value"
    Start-Sleep -Milliseconds 800
}

function Get-LifecycleState {
    param(
        [Parameter(Mandatory)][System.Windows.Automation.AutomationElement]$Root,
        [Parameter(Mandatory)][string]$AutomationId
    )

    return (Wait-OperatorElement -Root $Root -AutomationId $AutomationId).Current.Name
}

function Assert-Lifecycle {
    param(
        [Parameter(Mandatory)][System.Windows.Automation.AutomationElement]$Root,
        [Parameter(Mandatory)][string]$AutomationId,
        [Parameter(Mandatory)][string]$ExpectedEnglish,
        [Parameter(Mandatory)][string]$Event
    )

    $actual = Get-LifecycleState -Root $Root -AutomationId $AutomationId
    $expectedKorean = switch ($ExpectedEnglish) {
        "Missing" { -join @([char]0xC5C6, [char]0xC74C) }
        "Drawing" { -join @([char]0xADF8, [char]0xB9AC, [char]0xB294, [char]0x20, [char]0xC911) }
        "Review" { -join @([char]0xAC80, [char]0xD1A0) }
        "Applied" { -join @([char]0xC801, [char]0xC6A9, [char]0xB428) }
        default { "" }
    }
    $passed = $actual -eq $ExpectedEnglish -or $actual -eq $expectedKorean
    Write-OperatorEvent $Event "actual=$actual;passed=$passed"
    if (-not $passed) {
        throw "Unexpected ROI lifecycle: expected $ExpectedEnglish/$expectedKorean, actual $actual"
    }
}

function Record-LifecycleExpectation {
    param(
        [Parameter(Mandatory)][System.Windows.Automation.AutomationElement]$Root,
        [Parameter(Mandatory)][string]$AutomationId,
        [Parameter(Mandatory)][string]$ExpectedEnglish,
        [Parameter(Mandatory)][string]$Event
    )

    $actual = Get-LifecycleState -Root $Root -AutomationId $AutomationId
    $expectedKorean = switch ($ExpectedEnglish) {
        "Missing" { -join @([char]0xC5C6, [char]0xC74C) }
        "Drawing" { -join @([char]0xADF8, [char]0xB9AC, [char]0xB294, [char]0x20, [char]0xC911) }
        "Review" { -join @([char]0xAC80, [char]0xD1A0) }
        "Applied" { -join @([char]0xC801, [char]0xC6A9, [char]0xB428) }
        default { "" }
    }
    $passed = $actual -eq $ExpectedEnglish -or $actual -eq $expectedKorean
    Write-OperatorEvent $Event "actual=$actual;expected=$ExpectedEnglish;passed=$passed;nonBlockingEvidence=true"
    return $passed
}

function Start-DesktopRecorder {
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
        "-offset_x", "0",
        "-offset_y", "0",
        "-video_size", "${Width}x${Height}",
        "-i", "desktop",
        "-t", $DurationSeconds.ToString(),
        "-c:v", "libx264",
        "-preset", "veryfast",
        "-crf", "21",
        "-pix_fmt", "yuv420p",
        $OutputPath
    )
    $process = Start-Process -FilePath $ffmpeg -ArgumentList $arguments -PassThru -WindowStyle Hidden
    Start-Sleep -Seconds 2
    Write-OperatorEvent "recording-started" "pid=$($process.Id);video=$OutputPath;duration=$DurationSeconds"
    return $process
}

function Complete-DesktopRecorder {
    param(
        [Parameter(Mandatory)][System.Diagnostics.Process]$Recorder,
        [Parameter(Mandatory)][string]$OutputPath,
        [Parameter(Mandatory)][int]$TimeoutSeconds
    )

    if (-not $Recorder.WaitForExit($TimeoutSeconds * 1000)) {
        throw "ffmpeg recording did not complete: $OutputPath"
    }
    $missingOutput = -not (Test-Path -LiteralPath $OutputPath -PathType Leaf)
    $emptyOutput = -not $missingOutput -and (Get-Item -LiteralPath $OutputPath).Length -eq 0
    if ($Recorder.ExitCode -ne 0 -or $missingOutput -or $emptyOutput) {
        throw "ffmpeg recording failed: $OutputPath"
    }
    Write-OperatorEvent "recording-completed" "video=$OutputPath;bytes=$((Get-Item -LiteralPath $OutputPath).Length)"
}

function Start-Scenario {
    param([Parameter(Mandatory)][string]$Name)

    $script:timelinePath = Join-Path $artifactRoot "$Name-timeline.jsonl"
    if (Test-Path -LiteralPath $script:timelinePath) {
        Remove-Item -LiteralPath $script:timelinePath -Force
    }
    $script:scenarioClock = [System.Diagnostics.Stopwatch]::StartNew()
    Write-OperatorEvent "scenario-started" $Name
}

function Complete-Scenario {
    param([Parameter(Mandatory)][string]$Name)

    Write-OperatorEvent "scenario-completed" $Name
    $script:scenarioClock.Stop()
}

function Invoke-ThicknessScenario {
    param(
        [Parameter(Mandatory)][string]$Name,
        [Parameter(Mandatory)][string]$RecipePath,
        [Parameter(Mandatory)][int]$Width,
        [Parameter(Mandatory)][int]$Height,
        [Parameter(Mandatory)][int]$DurationSeconds,
        [switch]$Compact
    )

    Start-Scenario $Name
    $videoPath = Join-Path $artifactRoot "$Name.mp4"
    $application = $null
    $recorder = $null
    $scenarioError = $null
    try {
        $application = Start-OperatorApplication `
            -RecipePath $RecipePath `
            -StepId "step.synthetic-pad-thickness.01" `
            -Language "ko" `
            -Width $Width `
            -Height $Height
        $recorder = Start-DesktopRecorder `
            -OutputPath $videoPath `
            -Width $Width `
            -Height $Height `
            -DurationSeconds $DurationSeconds

        Click-OperatorElement `
            -Element (Wait-OperatorElement -Root $application.Root -AutomationId "ViewerTopView") `
            -Event "viewer-top"
        Invoke-OperatorElement `
            -Element (Wait-OperatorElement -Root $application.Root -AutomationId "OpenHeightImage") `
            -Event "height-image-opened"
        $pixels = Wait-OperatorElement -Root $application.Root -AutomationId "HeightImagePixels"
        Write-OperatorEvent "height-image-ready" "width=$($pixels.Current.BoundingRectangle.Width);height=$($pixels.Current.BoundingRectangle.Height)"
        Start-Sleep -Seconds 2

        Invoke-OperatorElement `
            -Element (Wait-OperatorElement -Root $application.Root -Name "Select second ROI role") `
            -Event "measurement-role-selected-for-delete"
        Invoke-OperatorElement `
            -Element (Wait-OperatorElement -Root $application.Root -Name "Delete Measurement ROI") `
            -Event "measurement-delete-before-reference"
        Record-LifecycleExpectation `
            -Root $application.Root `
            -AutomationId "MeasurementRoiLifecycleState" `
            -ExpectedEnglish "Missing" `
            -Event "measurement-delete-ui-projection" | Out-Null
        Invoke-OperatorElement `
            -Element (Wait-OperatorElement -Root $application.Root -Name "Select first ROI role") `
            -Event "reference-role-selected-for-delete"
        Invoke-OperatorElement `
            -Element (Wait-OperatorElement -Root $application.Root -Name "Delete Reference ROI") `
            -Event "reference-delete"
        Record-LifecycleExpectation `
            -Root $application.Root `
            -AutomationId "ReferenceRoiLifecycleState" `
            -ExpectedEnglish "Missing" `
            -Event "reference-delete-ui-projection" | Out-Null
        Invoke-OperatorElement `
            -Element (Wait-OperatorElement -Root $application.Root -Name "Draw or redraw Reference ROI") `
            -Event "reference-draw-started"
        Assert-Lifecycle `
            -Root $application.Root `
            -AutomationId "ReferenceRoiLifecycleState" `
            -ExpectedEnglish "Drawing" `
            -Event "reference-drawing"
        Drag-OperatorRegion `
            -Surface $pixels `
            -StartRelativeX 0.35 `
            -StartRelativeY 0.20 `
            -EndRelativeX 0.55 `
            -EndRelativeY 0.42 `
            -Event "reference-region-drag"
        Assert-Lifecycle `
            -Root $application.Root `
            -AutomationId "ReferenceRoiLifecycleState" `
            -ExpectedEnglish "Review" `
            -Event "reference-review"
        Send-OperatorKeys -Keys "{ENTER}" -Event "reference-apply"
        Assert-Lifecycle `
            -Root $application.Root `
            -AutomationId "ReferenceRoiLifecycleState" `
            -ExpectedEnglish "Applied" `
            -Event "reference-applied"

        Invoke-OperatorElement `
            -Element (Wait-OperatorElement -Root $application.Root -Name "Select second ROI role") `
            -Event "measurement-role-selected"
        $measurementDraw = Wait-OperatorElement `
            -Root $application.Root `
            -Name "Draw or redraw Measurement ROI"
        Write-OperatorEvent `
            "measurement-draw-readiness-after-reference-apply" `
            "enabled=$($measurementDraw.Current.IsEnabled);expected=True;passed=$($measurementDraw.Current.IsEnabled)"
        if (-not $measurementDraw.Current.IsEnabled) {
            throw "Measurement ROI Draw remained disabled after Reference Apply."
        }
        Invoke-OperatorElement `
            -Element $measurementDraw `
            -Event "measurement-draw-started"
        Assert-Lifecycle `
            -Root $application.Root `
            -AutomationId "MeasurementRoiLifecycleState" `
            -ExpectedEnglish "Drawing" `
            -Event "measurement-drawing"
        Drag-OperatorRegion `
            -Surface $pixels `
            -StartRelativeX 0.57 `
            -StartRelativeY 0.48 `
            -EndRelativeX 0.77 `
            -EndRelativeY 0.70 `
            -Event "measurement-region-drag"
        Assert-Lifecycle `
            -Root $application.Root `
            -AutomationId "MeasurementRoiLifecycleState" `
            -ExpectedEnglish "Review" `
            -Event "measurement-review"
        Send-OperatorKeys -Keys "{ENTER}" -Event "measurement-apply"
        Assert-Lifecycle `
            -Root $application.Root `
            -AutomationId "MeasurementRoiLifecycleState" `
            -ExpectedEnglish "Applied" `
            -Event "measurement-applied"

        $preview = Wait-OperatorElement -Root $application.Root -Name "Preview selected inspection step"
        Write-OperatorEvent "preview-readiness" "enabled=$($preview.Current.IsEnabled);notInvoked=True"
        $run = Wait-OperatorElement -Root $application.Root -Name "Run complete inspection recipe"
        Write-OperatorEvent "run-readiness" "enabled=$($run.Current.IsEnabled);notInvoked=True"

        $saveButton = Wait-OperatorElement `
            -Root $application.Root `
            -AutomationId "SaveTeachingRecipe"
        $saveButton.SetFocus()
        Write-OperatorEvent "dual-roi-save-focus" "id=SaveTeachingRecipe"
        Send-OperatorControlS -Event "dual-roi-recipe-save-shortcut"
        Start-Sleep -Seconds 2
        $savedRecipe = Get-Content -LiteralPath $RecipePath -Raw |
            ConvertFrom-Json
        $savedStep = $savedRecipe.steps |
            Where-Object id -eq "step.synthetic-pad-thickness.01"
        $savedRoleContractPassed =
            $savedRecipe.schemaVersion -eq "1.5" -and
            $savedStep.inputEntityIds.Count -eq 3 -and
            -not [string]::IsNullOrWhiteSpace(
                [string]$savedStep.dualRoiRouting.firstRegionSelectionId) -and
            -not [string]::IsNullOrWhiteSpace(
                [string]$savedStep.dualRoiRouting.secondRegionSelectionId)
        Write-OperatorEvent `
            "dual-roi-save-contract" `
            "schema=$($savedRecipe.schemaVersion);inputs=$($savedStep.inputEntityIds.Count);passed=$savedRoleContractPassed"
        if (-not $savedRoleContractPassed) {
            throw "Ctrl+S did not persist the schema 1.5 dual-ROI role contract."
        }
        Stop-OperatorApplication -Application $application -UseKeyboard
        $application = Start-OperatorApplication `
            -RecipePath $RecipePath `
            -StepId "step.synthetic-pad-thickness.01" `
            -Language "ko" `
            -Width $Width `
            -Height $Height
        Assert-Lifecycle `
            -Root $application.Root `
            -AutomationId "ReferenceRoiLifecycleState" `
            -ExpectedEnglish "Applied" `
            -Event "reference-reopened"
        Assert-Lifecycle `
            -Root $application.Root `
            -AutomationId "MeasurementRoiLifecycleState" `
            -ExpectedEnglish "Applied" `
            -Event "measurement-reopened"
        Start-Sleep -Seconds 2
    }
    catch {
        $scenarioError = $_
        Write-OperatorEvent "scenario-failed" $_.Exception.Message
    }
    finally {
        if ($application) {
            Stop-OperatorApplication -Application $application -UseKeyboard
        }
        if ($recorder) {
            if ($scenarioError) {
                if (-not $recorder.HasExited) {
                    Stop-Process -Id $recorder.Id -Force
                    $recorder.WaitForExit()
                }
            } else {
                Complete-DesktopRecorder `
                    -Recorder $recorder `
                    -OutputPath $videoPath `
                    -TimeoutSeconds ([Math]::Min(60, $DurationSeconds + 4))
            }
        }
        Complete-Scenario $Name
    }
    if ($scenarioError) {
        throw $scenarioError
    }
}

function Get-OrientedBoxEdits {
    param([Parameter(Mandatory)][System.Windows.Automation.AutomationElement]$Root)

    $newButton = Wait-OperatorElement -Root $Root -AutomationId "NewOrientedBox3D"
    $anchor = $newButton.Current.BoundingRectangle
    $condition = New-Object System.Windows.Automation.PropertyCondition(
        [System.Windows.Automation.AutomationElement]::ControlTypeProperty,
        [System.Windows.Automation.ControlType]::Edit)
    $allEdits = $Root.FindAll(
        [System.Windows.Automation.TreeScope]::Descendants,
        $condition)
    $result = [System.Collections.Generic.List[System.Windows.Automation.AutomationElement]]::new()
    foreach ($edit in $allEdits) {
        $rectangle = $edit.Current.BoundingRectangle
        $isVisible = -not $edit.Current.IsOffscreen
        $isWithinHorizontalBounds = $rectangle.X -ge $anchor.X - 260 -and
            $rectangle.Right -le $anchor.Right + 50
        $isWithinVerticalBounds = $rectangle.Y -ge $anchor.Bottom + 20 -and
            $rectangle.Bottom -le $anchor.Bottom + 360
        if ($isVisible -and $isWithinHorizontalBounds -and $isWithinVerticalBounds) {
            $result.Add($edit)
        }
    }
    return $result
}

function Read-AutomationValue {
    param([Parameter(Mandatory)][System.Windows.Automation.AutomationElement]$Element)

    $pattern = $null
    if ($Element.TryGetCurrentPattern(
            [System.Windows.Automation.ValuePattern]::Pattern,
            [ref]$pattern)) {
        return ([System.Windows.Automation.ValuePattern]$pattern).Current.Value
    }
    return $Element.Current.Name
}

function Invoke-OrientedBoxScenario {
    param(
        [Parameter(Mandatory)][string]$Name,
        [Parameter(Mandatory)][string]$RecipePath
    )

    Start-Scenario $Name
    $videoPath = Join-Path $artifactRoot "$Name.mp4"
    $durationSeconds = 55
    $application = $null
    $recorder = $null
    try {
        $application = Start-OperatorApplication `
            -RecipePath $RecipePath `
            -StepId "step.oriented-box-authoring.01" `
            -Language "en" `
            -Width 1920 `
            -Height 1040
        $recorder = Start-DesktopRecorder `
            -OutputPath $videoPath `
            -Width 1920 `
            -Height 1040 `
            -DurationSeconds $durationSeconds

        Click-OperatorElement `
            -Element (Wait-OperatorElement -Root $application.Root -AutomationId "NewOrientedBox3D") `
            -Event "box-new"
        $edits = Get-OrientedBoxEdits -Root $application.Root
        if ($edits.Count -ne 16) {
            throw "Expected 16 OrientedBox3D edit controls, found $($edits.Count)."
        }

        Set-OperatorText -Element $edits.Item(0) -Value "README demo volume" -Event "box-name"
        Set-OperatorText -Element $edits.Item(7) -Value "1" -Event "box-invalid-axis-yx"
        Start-Sleep -Seconds 2
        Write-OperatorEvent "box-invalid-evidence" "applyEnabled=$((Wait-OperatorElement -Root $application.Root -AutomationId 'ApplyOrientedBox3D').Current.IsEnabled)"
        Set-OperatorText -Element $edits.Item(7) -Value "0" -Event "box-axis-yx-restored"
        Set-OperatorText -Element $edits.Item(14) -Value "350" -Event "box-half-y"

        $apply = Wait-OperatorElement -Root $application.Root -AutomationId "ApplyOrientedBox3D"
        if (-not $apply.Current.IsEnabled) {
            throw "OrientedBox3D Apply did not become enabled after valid numeric input."
        }
        Click-OperatorElement -Element $apply -Event "box-applied"
        Send-OperatorKeys -Keys "^s" -Event "box-recipe-save"
        Start-Sleep -Seconds 2

        Stop-OperatorApplication -Application $application -UseKeyboard
        $application = Start-OperatorApplication `
            -RecipePath $RecipePath `
            -StepId "step.oriented-box-authoring.01" `
            -Language "en" `
            -Width 1920 `
            -Height 1040
        $combo = Wait-OperatorElement -Root $application.Root -Name "Applied OrientedBox3D selections"
        Click-OperatorElement -Element $combo -Event "box-selection-opened"
        Send-OperatorKeys -Keys "{END}{ENTER}" -Event "box-last-selection"
        $reopenedEdits = Get-OrientedBoxEdits -Root $application.Root
        $reopenedName = Read-AutomationValue -Element $reopenedEdits.Item(0)
        Write-OperatorEvent "box-reopened" "name=$reopenedName;passed=$($reopenedName -eq 'README demo volume')"
        if ($reopenedName -ne "README demo volume") {
            throw "Saved OrientedBox3D was not selected after reopen."
        }
        Start-Sleep -Seconds 3
    }
    finally {
        if ($application) {
            Stop-OperatorApplication -Application $application -UseKeyboard
        }
        if ($recorder) {
            Complete-DesktopRecorder `
                -Recorder $recorder `
                -OutputPath $videoPath `
                -TimeoutSeconds ([Math]::Min(60, $durationSeconds + 4))
        }
        Complete-Scenario $Name
    }
}

function New-ContactSheet {
    param(
        [Parameter(Mandatory)][string]$Video,
        [Parameter(Mandatory)][string]$Output
    )

    & $ffmpeg -hide_banner -loglevel error -y `
        -i $Video `
        -vf "fps=1/3,scale=460:-1:flags=lanczos,tile=4x4:padding=4:margin=4:color=0x101722" `
        -frames:v 1 `
        $Output
    if ($LASTEXITCODE -ne 0 -or -not (Test-Path -LiteralPath $Output)) {
        throw "Contact sheet generation failed: $Output"
    }
}

function Trim-Video {
    param(
        [Parameter(Mandatory)][string]$Video,
        [Parameter(Mandatory)][double]$DurationSeconds
    )

    $temporary = "$Video.trim.mp4"
    & $ffmpeg -hide_banner -loglevel error -y `
        -i $Video `
        -t $DurationSeconds.ToString([System.Globalization.CultureInfo]::InvariantCulture) `
        -c copy `
        $temporary
    $trimFailed = $LASTEXITCODE -ne 0 -or
        -not (Test-Path -LiteralPath $temporary -PathType Leaf)
    if (-not $trimFailed) {
        $trimFailed = (Get-Item -LiteralPath $temporary).Length -eq 0
    }
    if ($trimFailed) {
        throw "Video trim failed: $Video"
    }
    Move-Item -LiteralPath $temporary -Destination $Video -Force
}

function New-ReadmeGif {
    param(
        [Parameter(Mandatory)][string]$Video,
        [Parameter(Mandatory)][string]$Output
    )

    $palette = Join-Path $artifactRoot "readme-gif-palette.png"
    & $ffmpeg -hide_banner -loglevel error -y `
        -ss 42 -t 28 -i $Video `
        -vf "fps=8,scale=960:-1:flags=lanczos,palettegen=max_colors=128:stats_mode=diff" `
        $palette
    if ($LASTEXITCODE -ne 0) {
        throw "README GIF palette generation failed."
    }
    & $ffmpeg -hide_banner -loglevel error -y `
        -ss 42 -t 28 -i $Video -i $palette `
        -lavfi "fps=8,scale=960:-1:flags=lanczos[x];[x][1:v]paletteuse=dither=bayer:bayer_scale=3:diff_mode=rectangle" `
        $Output
    if ($LASTEXITCODE -ne 0 -or -not (Test-Path -LiteralPath $Output)) {
        throw "README GIF generation failed."
    }

    if ((Get-Item -LiteralPath $Output).Length -gt 10MB) {
        & $ffmpeg -hide_banner -loglevel error -y `
            -ss 42 -t 22 -i $Video `
            -vf "fps=6,scale=800:-1:flags=lanczos,palettegen=max_colors=96:stats_mode=diff" `
            $palette
        & $ffmpeg -hide_banner -loglevel error -y `
            -ss 42 -t 22 -i $Video -i $palette `
            -lavfi "fps=6,scale=800:-1:flags=lanczos[x];[x][1:v]paletteuse=dither=bayer:bayer_scale=4:diff_mode=rectangle" `
            $Output
    }
    if ((Get-Item -LiteralPath $Output).Length -gt 10MB) {
        throw "README GIF remains larger than 10 MiB."
    }
}

$wideRecipe = Join-Path $artifactRoot "wide-thickness-replay.ov3d-recipe.json"
$compactRecipe = Join-Path $artifactRoot "compact-thickness-replay.ov3d-recipe.json"
$boxRecipe = Join-Path $artifactRoot "oriented-box-replay.ov3d-recipe.json"

function Copy-SyntheticThicknessRecipe {
    param([Parameter(Mandatory)][string]$Destination)

    Copy-Item -LiteralPath $thicknessBaseline -Destination $Destination -Force
    $document = Get-Content -LiteralPath $Destination -Raw | ConvertFrom-Json
    $document.source.path = Join-Path $repoRoot `
        "3D\SyntheticValidation\ThicknessCouponV1\synthetic-thickness-coupon-v1.C3D"
    $document |
        ConvertTo-Json -Depth 32 |
        Set-Content -LiteralPath $Destination -Encoding UTF8
}

if (-not $CompactOnly) {
    Copy-SyntheticThicknessRecipe -Destination $wideRecipe
    Copy-Item -LiteralPath $boxBaseline -Destination $boxRecipe -Force
    $boxDocument = Get-Content -LiteralPath $boxRecipe -Raw | ConvertFrom-Json
    $boxDocument.source.path = Join-Path $repoRoot `
        "3D\SyntheticValidation\ThicknessCouponV1\synthetic-thickness-coupon-v1.C3D"
    $boxDocument |
        ConvertTo-Json -Depth 32 |
        Set-Content -LiteralPath $boxRecipe -Encoding UTF8
}
Copy-SyntheticThicknessRecipe -Destination $compactRecipe

if (-not $CompactOnly -and -not $SkipWideReplay) {
    Invoke-ThicknessScenario `
        -Name "01-wide-thickness-roi-replay" `
        -RecipePath $wideRecipe `
        -Width 1920 `
        -Height 1040 `
        -DurationSeconds 100
}

if (-not $CompactOnly) {
    Invoke-OrientedBoxScenario `
        -Name "02-oriented-box-numeric-replay" `
        -RecipePath $boxRecipe
}

Invoke-ThicknessScenario `
    -Name "03-compact-thickness-roi-replay" `
    -RecipePath $compactRecipe `
    -Width 1280 `
    -Height 760 `
    -DurationSeconds 100 `
    -Compact

$wideVideo = Join-Path $artifactRoot "01-wide-thickness-roi-replay.mp4"
$boxVideo = Join-Path $artifactRoot "02-oriented-box-numeric-replay.mp4"
$compactVideo = Join-Path $artifactRoot "03-compact-thickness-roi-replay.mp4"
Trim-Video -Video $wideVideo -DurationSeconds 90
Trim-Video -Video $compactVideo -DurationSeconds 90
New-ContactSheet -Video $wideVideo -Output (Join-Path $artifactRoot "01-wide-contact-sheet.png")
New-ContactSheet -Video $boxVideo -Output (Join-Path $artifactRoot "02-oriented-box-contact-sheet.png")
New-ContactSheet -Video $compactVideo -Output (Join-Path $artifactRoot "03-compact-contact-sheet.png")
New-ReadmeGif -Video $wideVideo -Output $readmeGif

$videoFiles = @($wideVideo, $boxVideo, $compactVideo)
$mediaReport = Join-Path $artifactRoot "media-verification.txt"
$mediaLines = [System.Collections.Generic.List[string]]::new()
$mediaLines.Add("OpenVisionLab 3D operator video media verification")
foreach ($video in $videoFiles) {
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
}
$mediaLines.Add("Gif|path=$readmeGif|bytes=$((Get-Item -LiteralPath $readmeGif).Length)")
$mediaLines | Set-Content -LiteralPath $mediaReport -Encoding UTF8

$environmentReport = Join-Path $artifactRoot "environment.txt"
@(
    "OpenVisionLab 3D operator replay environment"
    "CapturedAt=$([DateTimeOffset]::Now.ToString('O'))"
    "GitCommit=$(& git -C $repoRoot rev-parse HEAD)"
    "GitBranch=$(& git -C $repoRoot branch --show-current)"
    "ShellExe=$shellExe"
    "ShellExeSha256=$((Get-FileHash -LiteralPath $shellExe -Algorithm SHA256).Hash)"
    "Ffmpeg=$ffmpeg"
    "FfmpegVersion=$((& $ffmpeg -version | Select-Object -First 1))"
    "ThicknessSourceSha256=$((Get-FileHash -LiteralPath (Join-Path $repoRoot '3D\SyntheticValidation\ThicknessCouponV1\synthetic-thickness-coupon-v1.C3D') -Algorithm SHA256).Hash)"
    "InputBoundary=actual Release WPF window, external UI Automation lookup, user32 pointer input, SendKeys keyboard input"
    "ClaimBoundary=no physical calibration, metrology, first-time-human, camera, PLC, robot, or production-platform claim"
) | Set-Content -LiteralPath $environmentReport -Encoding UTF8

Write-Output "OperatorVideoReplay|Pass|artifacts=$artifactRoot|gif=$readmeGif"
