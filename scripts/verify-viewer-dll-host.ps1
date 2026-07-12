[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',
    [string]$ArtifactDirectory = 'artifacts/viewer-dll-host',
    [switch]$NoRestore
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$sampleProject = Join-Path $repoRoot 'samples/OpenVisionLab.ThreeD.Viewer.BinaryHost/OpenVisionLab.ThreeD.Viewer.BinaryHost.csproj'
$bundlePath = Join-Path $repoRoot 'artifacts/viewer-dll/net10.0-windows'
$artifactPath = [System.IO.Path]::GetFullPath((Join-Path $repoRoot $ArtifactDirectory))

if (Select-String -LiteralPath $sampleProject -Pattern '<ProjectReference' -Quiet) {
    throw 'Binary Host must not contain a ProjectReference.'
}

$bundleArguments = @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', (Join-Path $PSScriptRoot 'build-viewer-dll.ps1'), '-Configuration', $Configuration)
if ($NoRestore) { $bundleArguments += '-NoRestore' }
& powershell @bundleArguments
if ($LASTEXITCODE -ne 0) { throw "Viewer DLL bundle failed with exit code $LASTEXITCODE." }

$buildArguments = @('build', $sampleProject, '-c', $Configuration, "-p:ViewerBundlePath=$bundlePath")
if ($NoRestore) { $buildArguments += '--no-restore' }
& dotnet @buildArguments
if ($LASTEXITCODE -ne 0) { throw "Binary Host build failed with exit code $LASTEXITCODE." }

New-Item -ItemType Directory -Force -Path $artifactPath | Out-Null
$screenshotPath = Join-Path $artifactPath 'viewer-binary-host.png'
$contractPath = Join-Path $artifactPath 'viewer-binary-host.txt'
$manifestEvidencePath = Join-Path $artifactPath 'viewer-dll-manifest.json'
Copy-Item -LiteralPath (Join-Path $bundlePath 'viewer-dll-manifest.json') -Destination $manifestEvidencePath -Force
$outputPath = Join-Path (Split-Path $sampleProject -Parent) "bin/$Configuration/net10.0-windows"
$hostExecutable = Join-Path $outputPath 'OpenVisionLab.ThreeD.Viewer.BinaryHost.exe'
$runArguments = @(
    '--smoke-screenshot', $screenshotPath,
    '--smoke-c3d', 'thickness',
    '--smoke-pick', 'c3d',
    '--smoke-contracts', $contractPath)
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
if (-not (Select-String -LiteralPath $contractPath -Pattern 'ViewerStatus\|summary=Smoke pick: C3D height grid\|smokeExitCode=0' -Quiet)) {
    throw 'Binary Host Viewer contract did not prove C3D picking.'
}

$manifest = Get-Content $manifestEvidencePath -Raw | ConvertFrom-Json
$reportPath = Join-Path $artifactPath 'viewer-binary-host-report.txt'
@(
    'BinaryHost|projectReferenceCount=0|targetFramework=net10.0-windows'
    "ViewerBundle|applicationVersion=$($manifest.applicationVersion)|hostApiVersion=$($manifest.viewerHostApiVersion)|viewerAssemblyVersion=$($manifest.viewerAssemblyVersion)|requiredOutputs=$($requiredOutputs.Count)/$($requiredOutputs.Count)"
    'Runtime|exitCode=0|scenario=C3D thickness pick'
    "Evidence|screenshot=$screenshotPath|contract=$contractPath"
) | Set-Content -LiteralPath $reportPath -Encoding utf8

Get-Content -LiteralPath $reportPath
