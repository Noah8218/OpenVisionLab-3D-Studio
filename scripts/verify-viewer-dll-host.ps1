[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',
    [string]$ArtifactDirectory = 'artifacts/viewer-dll-host',
    [string]$ViewerBundlePath,
    [switch]$NoRestore
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$sampleProject = Join-Path $repoRoot 'samples/OpenVisionLab.ThreeD.Viewer.BinaryHost/OpenVisionLab.ThreeD.Viewer.BinaryHost.csproj'
$artifactPath = [System.IO.Path]::GetFullPath((Join-Path $repoRoot $ArtifactDirectory))

if (Select-String -LiteralPath $sampleProject -Pattern '<ProjectReference' -Quiet) {
    throw 'Binary Host must not contain a ProjectReference.'
}

if ([string]::IsNullOrWhiteSpace($ViewerBundlePath)) {
    $bundlePath = Join-Path $repoRoot 'artifacts/viewer-dll/net10.0-windows'
    $bundleArguments = @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', (Join-Path $PSScriptRoot 'build-viewer-dll.ps1'), '-Configuration', $Configuration)
    if ($NoRestore) { $bundleArguments += '-NoRestore' }
    & powershell @bundleArguments
    if ($LASTEXITCODE -ne 0) { throw "Viewer DLL bundle failed with exit code $LASTEXITCODE." }
}
else {
    $bundlePath = if ([System.IO.Path]::IsPathRooted($ViewerBundlePath)) {
        [System.IO.Path]::GetFullPath($ViewerBundlePath)
    }
    else {
        [System.IO.Path]::GetFullPath((Join-Path $repoRoot $ViewerBundlePath))
    }
    if (-not (Test-Path -LiteralPath $bundlePath -PathType Container)) {
        throw "Viewer DLL bundle directory was not found: $bundlePath"
    }
}

$manifestPath = Join-Path $bundlePath 'viewer-dll-manifest.json'
if (-not (Test-Path -LiteralPath $manifestPath -PathType Leaf)) {
    throw "Viewer DLL bundle manifest was not found: $manifestPath"
}

$manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
$manifestFiles = @($manifest.files)
if ($manifestFiles.Count -eq 0) {
    throw 'Viewer DLL bundle manifest does not contain any files.'
}

$bundleRoot = $bundlePath.TrimEnd([System.IO.Path]::DirectorySeparatorChar, [System.IO.Path]::AltDirectorySeparatorChar) + [System.IO.Path]::DirectorySeparatorChar
foreach ($file in $manifestFiles) {
    $fileName = [string]$file.name
    if ([string]::IsNullOrWhiteSpace($fileName) -or [System.IO.Path]::IsPathRooted($fileName)) {
        throw "Viewer DLL bundle manifest contains an invalid file name: $fileName"
    }

    $filePath = [System.IO.Path]::GetFullPath((Join-Path $bundlePath $fileName))
    if (-not $filePath.StartsWith($bundleRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Viewer DLL bundle manifest file is outside the bundle directory: $fileName"
    }
    if (-not (Test-Path -LiteralPath $filePath -PathType Leaf)) {
        throw "Viewer DLL bundle file is missing: $fileName"
    }
    if ($null -eq $file.sizeBytes -or (Get-Item -LiteralPath $filePath).Length -ne [long]$file.sizeBytes) {
        throw "Viewer DLL bundle file size does not match the manifest: $fileName"
    }

    $expectedHash = [string]$file.sha256
    $actualHash = (Get-FileHash -LiteralPath $filePath -Algorithm SHA256).Hash
    if ([string]::IsNullOrWhiteSpace($expectedHash) -or $actualHash -ne $expectedHash) {
        throw "Viewer DLL bundle file SHA-256 does not match the manifest: $fileName"
    }
}

$buildArguments = @('build', $sampleProject, '-c', $Configuration, "-p:ViewerBundlePath=$bundlePath")
if ($NoRestore) { $buildArguments += '--no-restore' }
& dotnet @buildArguments
if ($LASTEXITCODE -ne 0) { throw "Binary Host build failed with exit code $LASTEXITCODE." }

New-Item -ItemType Directory -Force -Path $artifactPath | Out-Null
$screenshotPath = Join-Path $artifactPath 'viewer-binary-host.png'
$screenshotQualityPath = Join-Path $artifactPath 'viewer-binary-host-quality.txt'
$contractPath = Join-Path $artifactPath 'viewer-binary-host.txt'
$hostApiReportPath = Join-Path $artifactPath 'viewer-binary-host-api.txt'
$hostApiRecipePath = Join-Path $artifactPath 'viewer-binary-host.recipe.json'
$manifestEvidencePath = Join-Path $artifactPath 'viewer-dll-manifest.json'
Copy-Item -LiteralPath $manifestPath -Destination $manifestEvidencePath -Force
$outputPath = Join-Path (Split-Path $sampleProject -Parent) "bin/$Configuration/net10.0-windows"
$hostExecutable = Join-Path $outputPath 'OpenVisionLab.ThreeD.Viewer.BinaryHost.exe'
$runArguments = @(
    '--smoke-screenshot', $screenshotPath,
    '--smoke-screenshot-quality-report', $screenshotQualityPath,
    '--smoke-c3d', 'thickness',
    '--smoke-pick', 'c3d',
    '--smoke-contracts', $contractPath,
    '--host-api-report', $hostApiReportPath,
    '--host-api-save-recipe', $hostApiRecipePath)
$hostStart = @{
    FilePath = $hostExecutable
    ArgumentList = $runArguments
    WorkingDirectory = $repoRoot
    Wait = $true
    PassThru = $true
    WindowStyle = 'Hidden'
}
$hostProcess = Start-Process @hostStart
if ($hostProcess.ExitCode -ne 0) { throw "Binary Host runtime smoke failed with exit code $($hostProcess.ExitCode)." }

$requiredOutputs = @(
    'OpenVisionLab.ThreeD.Viewer.BinaryHost.exe',
    'OpenVisionLab.ThreeD.Viewer.BinaryHost.deps.json',
    'OpenVisionLab.ThreeD.Viewer.BinaryHost.runtimeconfig.json',
    'OpenVisionLab.ThreeD.Viewer.dll',
    'OpenVisionLab.ThreeD.Core.dll',
    'OpenVisionLab.ThreeD.Data.dll',
    'OpenVisionLab.ThreeD.Tools.dll',
    'SharpGL.dll',
    'SharpGL.SceneGraph.dll',
    'SharpGL.WPF.dll',
    'Unofficial.laszip.netstandard.dll',
    'viewer-dll-manifest.json')
$missingOutputs = $requiredOutputs | Where-Object { -not (Test-Path -LiteralPath (Join-Path $outputPath $_)) }
if ($missingOutputs.Count -gt 0) { throw "Binary Host output is incomplete: $($missingOutputs -join ', ')" }
if (-not (Test-Path -LiteralPath $screenshotPath) -or (Get-Item -LiteralPath $screenshotPath).Length -eq 0) {
    throw 'Binary Host screenshot was not created.'
}
if (-not (Select-String -LiteralPath $screenshotQualityPath -Pattern 'ViewerScreenshotResult\|accepted=True' -Quiet)) {
    throw 'Binary Host screenshot quality was not accepted.'
}
if (-not (Select-String -LiteralPath $contractPath -Pattern 'ViewerStatus\|summary=Smoke pick: C3D height grid\|smokeExitCode=0' -Quiet)) {
    throw 'Binary Host Viewer contract did not prove C3D picking.'
}
if (-not (Select-String -LiteralPath $hostApiReportPath -Pattern "HostApi\|version=$([regex]::Escape([string]$manifest.viewerHostApiVersion))" -Quiet)) {
    throw 'Binary Host report did not prove the Viewer Host API version.'
}
$hostEventMatch = Select-String -LiteralPath $hostApiReportPath -Pattern 'HostEvents\|count=(\d+)' | Select-Object -First 1
if ($null -eq $hostEventMatch -or [int]$hostEventMatch.Matches[0].Groups[1].Value -le 0) {
    throw 'Binary Host report did not prove HostStateChanged events.'
}
if (-not (Select-String -LiteralPath $hostApiReportPath -Pattern 'HostState\|activeEntity=C3D Height Grid\|selectionMode=Point\|viewerStatus=.+' -Quiet)) {
    throw 'Binary Host report did not prove the expected HostState snapshot.'
}
if (-not (Select-String -LiteralPath $hostApiReportPath -Pattern 'HostCommands\|invoked=ResetView,FitAll,FitSelection\|saveRecipe=True' -Quiet)) {
    throw 'Binary Host report did not prove Host API command invocation and recipe save.'
}
if (-not (Test-Path -LiteralPath $hostApiRecipePath -PathType Leaf) -or (Get-Item -LiteralPath $hostApiRecipePath).Length -eq 0) {
    throw 'Binary Host API recipe was not saved.'
}
$hostApiRecipe = Get-Content -LiteralPath $hostApiRecipePath -Raw | ConvertFrom-Json
if ($hostApiRecipe.recipeType -ne 'c3d-height-deviation') {
    throw "Binary Host API saved an unexpected recipe type: $($hostApiRecipe.recipeType)"
}

$reportPath = Join-Path $artifactPath 'viewer-binary-host-report.txt'
@(
    'BinaryHost|projectReferenceCount=0|targetFramework=net10.0-windows'
    "ViewerBundle|applicationVersion=$($manifest.applicationVersion)|hostApiVersion=$($manifest.viewerHostApiVersion)|viewerAssemblyVersion=$($manifest.viewerAssemblyVersion)|manifestFiles=$($manifestFiles.Count)/$($manifestFiles.Count)|requiredOutputs=$($requiredOutputs.Count)/$($requiredOutputs.Count)"
    "HostApi|version=$($manifest.viewerHostApiVersion)|stateSnapshot=True|events=$($hostEventMatch.Matches[0].Groups[1].Value)|commands=3/3|saveRecipe=True"
    'Runtime|exitCode=0|scenario=C3D thickness pick'
    "Evidence|screenshot=$screenshotPath|quality=$screenshotQualityPath|contract=$contractPath|hostApi=$hostApiReportPath|hostRecipe=$hostApiRecipePath"
) | Set-Content -LiteralPath $reportPath -Encoding utf8

Get-Content -LiteralPath $reportPath
