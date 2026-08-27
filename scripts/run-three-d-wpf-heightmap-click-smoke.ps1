param(
    [ValidateSet("Wide", "Compact")]
    [string]$Layout = "Wide",
    [string]$Configuration = "Release",
    [switch]$SkipBuild,
    [string]$MachineRepo = $env:OPENVISIONLAB_MACHINE_STUDIO_ROOT,
    [string]$EvidenceRoot = $env:OPENVISIONLAB_3D_TEST_ARTIFACT_ROOT
)

$ErrorActionPreference = "Stop"

$ThreeDRepo = Split-Path -Parent $PSScriptRoot
if ([string]::IsNullOrWhiteSpace($MachineRepo)) {
    throw "Set OPENVISIONLAB_MACHINE_STUDIO_ROOT or pass -MachineRepo to run the WPF height-map smoke."
}
if ([string]::IsNullOrWhiteSpace($EvidenceRoot)) {
    $EvidenceRoot = Join-Path ([IO.Path]::GetTempPath()) "OpenVisionLab-3D-Studio"
}
$MachineProducerProject = Join-Path $MachineRepo "tools\MachineIntegrationProducerSmoke\MachineIntegrationProducerSmoke.csproj"
$ShellProject = Join-Path $ThreeDRepo "src\OpenVisionLab.ThreeD.Shell\OpenVisionLab.ThreeD.Shell.csproj"
$MachineProducerDll = Join-Path $MachineRepo "tools\MachineIntegrationProducerSmoke\bin\$Configuration\net8.0\MachineIntegrationProducerSmoke.dll"
$ShellDll = Join-Path $ThreeDRepo "src\OpenVisionLab.ThreeD.Shell\bin\$Configuration\net10.0-windows10.0.19041\OpenVisionLab.ThreeD.Shell.dll"
$MachineProjectPath = Join-Path $MachineRepo "samples\VisionInspectionCell\VisionInspectionCell.ovmachine"
$SourceC3DPath = Join-Path $ThreeDRepo "3D\Samples\ThicknessCouponV1\thickness-coupon-v1.C3D"
$RecipePath = Join-Path $ThreeDRepo "recipes\c3d-warpage.recipe.json"
$RunRoot = Join-Path (Join-Path $EvidenceRoot "wpf-heightmap-click-smoke") ("run-" + (Get-Date -Format "yyyyMMdd-HHmmss") + "-" + [Guid]::NewGuid().ToString("N"))
$ExchangeRoot = Join-Path $RunRoot "exchange"
$SettingsPath = Join-Path $RunRoot "machine-exchange.json"
$ManifestPath = Join-Path $RunRoot "machine-producer-manifest.json"
$ShellOutputPath = Join-Path $RunRoot "shell.stdout.txt"
$ShellErrorPath = Join-Path $RunRoot "shell.stderr.txt"
$ProducerOutputPath = Join-Path $RunRoot "machine-producer.stdout.txt"
$ProducerErrorPath = Join-Path $RunRoot "machine-producer.stderr.txt"
$screenshotName = if ($Layout -eq "Wide") { "heightmap-wide-pressed.png" } else { "heightmap-compact-pressed.png" }
$qualityReportName = if ($Layout -eq "Wide") { "heightmap-wide-quality.txt" } else { "heightmap-compact-quality.txt" }
$ScreenshotPath = Join-Path $RunRoot $screenshotName
$ResultScreenshotPath = Join-Path $RunRoot ("{0}-result{1}" -f [IO.Path]::GetFileNameWithoutExtension($ScreenshotPath), [IO.Path]::GetExtension($ScreenshotPath))
$QualityReportPath = Join-Path $RunRoot $qualityReportName
$SummaryPath = Join-Path $RunRoot "wpf-heightmap-click-summary.txt"
$ConsumerCommit = ((& git -C $ThreeDRepo rev-parse HEAD) | Out-String).Trim()

if ($ConsumerCommit.Length -ne 40) {
    throw "Unable to resolve the 3D Studio consumer commit."
}

New-Item -ItemType Directory -Force -Path $RunRoot, $ExchangeRoot | Out-Null

function Invoke-Checked {
    param(
        [string]$Name,
        [string]$WorkingDirectory,
        [string[]]$Arguments,
        [string]$StandardOutputPath,
        [string]$StandardErrorPath
    )

    Write-Host "==> $Name"
    $process = Start-Process `
        -FilePath "dotnet" `
        -ArgumentList $Arguments `
        -WorkingDirectory $WorkingDirectory `
        -RedirectStandardOutput $StandardOutputPath `
        -RedirectStandardError $StandardErrorPath `
        -PassThru `
        -Wait `
        -NoNewWindow
    if ($process.ExitCode -ne 0) {
        Get-Content -LiteralPath $StandardOutputPath -ErrorAction SilentlyContinue
        Get-Content -LiteralPath $StandardErrorPath -ErrorAction SilentlyContinue
        throw "$Name failed with exit code $($process.ExitCode)."
    }

    Write-Host "PASS $Name exit=0 pid=$($process.Id)"
}

$requiredPaths = @(
    $MachineProducerProject,
    $ShellProject,
    $MachineProjectPath,
    $SourceC3DPath,
    $RecipePath
)
$missingPath = $requiredPaths |
    Where-Object { -not (Test-Path -LiteralPath $_) } |
    Select-Object -First 1
if ($null -ne $missingPath) {
    throw "A required integration fixture or project path is missing: $missingPath"
}

if (-not $SkipBuild) {
    Invoke-Checked `
        -Name "build Machine producer" `
        -WorkingDirectory $MachineRepo `
        -Arguments @("build", $MachineProducerProject, "-c", $Configuration, "--nologo") `
        -StandardOutputPath (Join-Path $RunRoot "machine-producer-build.stdout.txt") `
        -StandardErrorPath (Join-Path $RunRoot "machine-producer-build.stderr.txt")
    Invoke-Checked `
        -Name "build 3D Shell" `
        -WorkingDirectory $ThreeDRepo `
        -Arguments @("build", $ShellProject, "-c", $Configuration, "--nologo") `
        -StandardOutputPath (Join-Path $RunRoot "shell-build.stdout.txt") `
        -StandardErrorPath (Join-Path $RunRoot "shell-build.stderr.txt")
}

if (-not (Test-Path -LiteralPath $MachineProducerDll)) {
    throw "Machine producer assembly was not found: $MachineProducerDll"
}
if (-not (Test-Path -LiteralPath $ShellDll)) {
    throw "3D Shell assembly was not found: $ShellDll"
}

$consumerAssemblyVersion = [System.Reflection.AssemblyName]::GetAssemblyName($ShellDll).Version
if ($null -eq $consumerAssemblyVersion) {
    throw "Unable to resolve the 3D Shell assembly version."
}
$ConsumerVersion = $consumerAssemblyVersion.ToString(3)

$producerArguments = @(
    $MachineProducerDll,
    "--publish-3d", $ExchangeRoot, $ManifestPath, $MachineProjectPath,
    $SourceC3DPath, $RecipePath, $ConsumerVersion, $ConsumerCommit, "Clean"
)
Invoke-Checked `
    -Name "publish ThreeD/HeightMap Handoff" `
    -WorkingDirectory $MachineRepo `
    -Arguments $producerArguments `
    -StandardOutputPath $ProducerOutputPath `
    -StandardErrorPath $ProducerErrorPath

$windowSize = if ($Layout -eq "Wide") { @("1920", "1040") } else { @("1280", "760") }
$shellArguments = @(
    "run", "--project", $ShellProject, "-c", $Configuration, "--no-build", "--",
    "--shell-workspace", "Exchange",
    "--smoke-integration-exchange-state", "heightmap-run",
    "--smoke-integration-exchange-root", $ExchangeRoot,
    "--smoke-integration-settings-path", $SettingsPath,
    "--shell-smoke-width", $windowSize[0],
    "--shell-smoke-height", $windowSize[1],
    "--shell-smoke-leftmost",
    "--shell-smoke-screenshot", $ScreenshotPath,
    "--shell-screenshot-quality-report", $QualityReportPath
)
Invoke-Checked `
    -Name "click accepted HeightMap in 3D Shell" `
    -WorkingDirectory $ThreeDRepo `
    -Arguments $shellArguments `
    -StandardOutputPath $ShellOutputPath `
    -StandardErrorPath $ShellErrorPath

$transactionDirectories = @(Get-ChildItem -LiteralPath (Join-Path $ExchangeRoot "transactions") -Directory)
if ($transactionDirectories.Count -ne 1) {
    throw "Expected one transaction directory, found $($transactionDirectories.Count)."
}
$resultPath = Join-Path $transactionDirectories[0].FullName "result.json"
if (-not (Test-Path -LiteralPath $resultPath)) {
    throw "The WPF click smoke did not create a Result: $resultPath"
}

$quality = Get-Content -LiteralPath $QualityReportPath -Raw
foreach ($expected in @(
        "PointerDown|scope=IntegrationExchangeHeightMapRunPressed|state=held",
        "IntegrationExchangeHeightMapRun|pendingDisabled=true|acceptedEnabled=true|visibleClick=true|resultPublished=true|rerunDisabled=true",
        "intersects=True")) {
    if (-not $quality.Contains($expected)) {
        throw "Quality report is missing expected evidence: $expected"
    }
}

@(
    "OpenVisionLab 3D WPF accepted HeightMap click smoke",
    "configuration=$Configuration",
    "layout=$Layout",
    "runRoot=$RunRoot",
    "exchangeRoot=$ExchangeRoot",
    "settingsPath=$SettingsPath",
    "manifestPath=$ManifestPath",
    "pressedScreenshot=$ScreenshotPath",
    "resultScreenshot=$ResultScreenshotPath",
    "qualityReport=$QualityReportPath",
    "resultPath=$resultPath",
    "status=PASS"
) | Set-Content -LiteralPath $SummaryPath -Encoding UTF8

Write-Host "Summary: $SummaryPath"
Write-Host "Quality report: $QualityReportPath"
exit 0
