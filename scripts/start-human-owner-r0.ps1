[CmdletBinding()]
param(
    [ValidateSet("Wide", "Compact")]
    [string]$Layout = "Wide",

    [switch]$ValidateOnly
)

$ErrorActionPreference = "Stop"

$workspaceRoot = Split-Path -Parent $PSScriptRoot
$shellExe = Join-Path $workspaceRoot `
    "src\OpenVisionLab.ThreeD.Shell\bin\Release\net10.0-windows10.0.19041\OpenVisionLab.ThreeD.Shell.exe"
$shellAssembly = [System.IO.Path]::ChangeExtension($shellExe, ".dll")
$coreAssembly = Join-Path (Split-Path -Parent $shellExe) `
    "OpenVisionLab.ThreeD.Core.dll"
$dataAssembly = Join-Path (Split-Path -Parent $shellExe) `
    "OpenVisionLab.ThreeD.Data.dll"
$toolsAssembly = Join-Path (Split-Path -Parent $shellExe) `
    "OpenVisionLab.ThreeD.Tools.dll"
$viewerAssembly = Join-Path (Split-Path -Parent $shellExe) `
    "OpenVisionLab.ThreeD.Viewer.dll"
$dockingAssembly = Join-Path (Split-Path -Parent $shellExe) `
    "OpenVisionLab.ThreeD.Docking.Controls.dll"
$recipePath = Join-Path $workspaceRoot `
    "artifacts\current\20260729-completeness-threshold-assistance\validation-set-fixture\completeness-threshold-fixture.ov3d-recipe.json"
$runRecordPath = Join-Path $workspaceRoot `
    "artifacts\current\20260729-completeness-results-overlays\runner-record.json"

foreach ($requiredPath in @(
        $shellExe,
        $shellAssembly,
        $coreAssembly,
        $dataAssembly,
        $toolsAssembly,
        $viewerAssembly,
        $dockingAssembly,
        $recipePath,
        $runRecordPath)) {
    if (-not (Test-Path -LiteralPath $requiredPath -PathType Leaf)) {
        throw "Required R0 input is missing: $requiredPath"
    }
}

$latestSource = Get-ChildItem -LiteralPath (Join-Path $workspaceRoot "src") `
        -Recurse `
        -File |
    Where-Object {
        $_.Extension -in ".cs", ".xaml", ".csproj" -and
        $_.FullName -notmatch "\\bin\\|\\obj\\"
    } |
    Sort-Object LastWriteTimeUtc -Descending |
    Select-Object -First 1
$shellItem = Get-Item -LiteralPath $shellExe
if ($latestSource -and $shellItem.LastWriteTimeUtc -lt $latestSource.LastWriteTimeUtc) {
    throw (
        "Release EXE is older than current source. Build Release before R0. " +
        "EXE=$($shellItem.LastWriteTimeUtc.ToString('O')); " +
        "source=$($latestSource.LastWriteTimeUtc.ToString('O')) " +
        "($($latestSource.FullName))"
    )
}

$layoutSize = if ($Layout -eq "Wide") {
    [pscustomobject]@{ Width = 1920; Height = 1040 }
} else {
    [pscustomobject]@{ Width = 1280; Height = 760 }
}

$inputHashes = @(
    Get-FileHash -Algorithm SHA256 -LiteralPath $shellExe
    Get-FileHash -Algorithm SHA256 -LiteralPath $shellAssembly
    Get-FileHash -Algorithm SHA256 -LiteralPath $coreAssembly
    Get-FileHash -Algorithm SHA256 -LiteralPath $dataAssembly
    Get-FileHash -Algorithm SHA256 -LiteralPath $toolsAssembly
    Get-FileHash -Algorithm SHA256 -LiteralPath $viewerAssembly
    Get-FileHash -Algorithm SHA256 -LiteralPath $dockingAssembly
    Get-FileHash -Algorithm SHA256 -LiteralPath $recipePath
    Get-FileHash -Algorithm SHA256 -LiteralPath $runRecordPath
)
$expectedHashes = @{}
$expectedHashes[(Resolve-Path -LiteralPath $shellExe).Path] =
    "871DD11B0B048C851DA6752982AFDD96BF780101B8DB81F72C944A100183B743"
$expectedHashes[(Resolve-Path -LiteralPath $shellAssembly).Path] =
    "390DE2E02B95F74CFD6E9C0214E70B09479055398BF77ED8187A80DE7B1E764D"
$expectedHashes[(Resolve-Path -LiteralPath $coreAssembly).Path] =
    "62DCA103CAB4FC9ECE72EC98F5F47CBA0C368EC1F6DEF55EF121624B87B71D3F"
$expectedHashes[(Resolve-Path -LiteralPath $dataAssembly).Path] =
    "95CBAA8D86190C5D200704CE77BCE86AC3672141C72191B69C108869C3E2264A"
$expectedHashes[(Resolve-Path -LiteralPath $toolsAssembly).Path] =
    "BC6D917113850418A3C64E7896886C88C34347122B4FF3EA8E9BDEEAD41B4B80"
$expectedHashes[(Resolve-Path -LiteralPath $viewerAssembly).Path] =
    "0CBAA865FB0D1A9B21818CE5FE9867B0F598AAFCEC1FBE1EC586A4DA80A9A4D5"
$expectedHashes[(Resolve-Path -LiteralPath $dockingAssembly).Path] =
    "570CE47C54A8A7F1EEBF50DD730EBA7777A23C7BC39A2ADCB4A81AB8A46FA197"
$expectedHashes[(Resolve-Path -LiteralPath $recipePath).Path] =
    "0DABE2D9A0B1931FD4E5F3E064C8157C02EC6DF60807C84B530128099B3CC461"
$expectedHashes[(Resolve-Path -LiteralPath $runRecordPath).Path] =
    "BAB565978CF786D5C8795D0F8F6898F29D1085820CF032EECC9F315B1544340A"

foreach ($hash in $inputHashes) {
    $expectedHash = $expectedHashes[$hash.Path]
    if (-not $expectedHash -or
        -not $hash.Hash.Equals(
            $expectedHash,
            [System.StringComparison]::OrdinalIgnoreCase)) {
        throw (
            "R0 input hash mismatch. Rebuild and record a new fixed evidence " +
            "set before launching. Expected=$expectedHash; " +
            "Actual=$($hash.Hash); Path=$($hash.Path)"
        )
    }
}

Write-Output "OpenVisionLab 3D Studio human-owner R0"
Write-Output "Layout: $Layout ($($layoutSize.Width) x $($layoutSize.Height))"
Write-Output "Release: $shellExe"
Write-Output "Shell assembly: $shellAssembly"
Write-Output "Core assembly: $coreAssembly"
Write-Output "Data assembly: $dataAssembly"
Write-Output "Tools assembly: $toolsAssembly"
Write-Output "Viewer assembly: $viewerAssembly"
Write-Output "Docking assembly: $dockingAssembly"
Write-Output "Recipe: $recipePath"
Write-Output "Run Record: $runRecordPath"
foreach ($hash in $inputHashes) {
    Write-Output "SHA-256 $($hash.Hash)  $($hash.Path)"
}

if ($ValidateOnly) {
    Write-Output "Validation passed. No application was launched."
    return
}

if (-not ("OpenVisionLabOwnerR0NativeWindow" -as [type])) {
    Add-Type @"
using System;
using System.Runtime.InteropServices;

public static class OpenVisionLabOwnerR0NativeWindow
{
    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [DllImport("user32.dll")]
    public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    public static extern bool SetWindowPos(
        IntPtr hWnd,
        IntPtr hWndInsertAfter,
        int X,
        int Y,
        int cx,
        int cy,
        uint uFlags);

    [DllImport("user32.dll")]
    public static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc callback, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(
        IntPtr hWnd,
        out uint processId);

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
        EnumWindows(
            delegate(IntPtr hWnd, IntPtr lParam)
            {
                uint ownerProcessId;
                if (!IsWindowVisible(hWnd) ||
                    GetWindowThreadProcessId(hWnd, out ownerProcessId) == 0 ||
                    ownerProcessId != processId)
                {
                    return true;
                }

                Rect rect;
                if (!GetWindowRect(hWnd, out rect))
                {
                    return true;
                }

                long width = Math.Max(0, rect.Right - rect.Left);
                long height = Math.Max(0, rect.Bottom - rect.Top);
                long area = width * height;
                if (area > bestArea)
                {
                    bestArea = area;
                    bestWindow = hWnd;
                }
                return true;
            },
            IntPtr.Zero);
        return bestWindow;
    }
}
"@
}

$arguments = @(
    "--tool-teaching-recipe", $recipePath,
    "--run-record", $runRecordPath,
    "--shell-workspace", "Workbench"
)
$process = Start-Process `
    -FilePath $shellExe `
    -ArgumentList $arguments `
    -PassThru

$deadline = [DateTime]::UtcNow.AddSeconds(30)
$windowHandle = [IntPtr]::Zero
do {
    Start-Sleep -Milliseconds 250
    $process.Refresh()
    $windowHandle = [OpenVisionLabOwnerR0NativeWindow]::FindLargestVisibleWindow(
        [uint32]$process.Id)
} while ($windowHandle -eq [IntPtr]::Zero -and [DateTime]::UtcNow -lt $deadline)

if ($windowHandle -eq [IntPtr]::Zero) {
    throw "The Release application did not expose a main window within 30 seconds."
}

[OpenVisionLabOwnerR0NativeWindow]::ShowWindow(
    $windowHandle,
    9) | Out-Null
[OpenVisionLabOwnerR0NativeWindow]::SetWindowPos(
    $windowHandle,
    [IntPtr]::Zero,
    0,
    0,
    $layoutSize.Width,
    $layoutSize.Height,
    0x0040) | Out-Null
[OpenVisionLabOwnerR0NativeWindow]::SetForegroundWindow(
    $windowHandle) | Out-Null

Start-Sleep -Seconds 7
$stableWindowHandle =
    [OpenVisionLabOwnerR0NativeWindow]::FindLargestVisibleWindow(
        [uint32]$process.Id)
if ($stableWindowHandle -ne [IntPtr]::Zero) {
    $windowHandle = $stableWindowHandle
    [OpenVisionLabOwnerR0NativeWindow]::ShowWindow(
        $windowHandle,
        9) | Out-Null
    [OpenVisionLabOwnerR0NativeWindow]::SetWindowPos(
        $windowHandle,
        [IntPtr]::Zero,
        0,
        0,
        $layoutSize.Width,
        $layoutSize.Height,
        0x0040) | Out-Null
    [OpenVisionLabOwnerR0NativeWindow]::SetForegroundWindow(
        $windowHandle) | Out-Null
}

Write-Output ""
Write-Output "Owner goal (no click-by-click assistance):"
Write-Output (
    "Run the supplied five-sample Completeness validation, investigate one " +
    "failure in Teach, review Results and Advanced, return to Validation, " +
    "and determine whether the same failure evidence is preserved."
)
Write-Output (
    "Stop and record Fail for any hesitation, misleading label, clipped " +
    "required action, unexpected execution/mutation, or lost state."
)
Write-Output "Process ID: $($process.Id)"
