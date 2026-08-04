[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',
    [ValidateSet('win-x64')]
    [string]$RuntimeIdentifier = 'win-x64',
    [string]$OutputDirectory = 'artifacts\release\openvisionlab-3d-studio-win-x64',
    [string]$BuildArtifactsPath,
    [switch]$NoRestore
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$artifactRoot = [System.IO.Path]::GetFullPath((Join-Path $repoRoot 'artifacts'))
$outputPath = [System.IO.Path]::GetFullPath((Join-Path $repoRoot $OutputDirectory))
$artifactPrefix = $artifactRoot.TrimEnd([System.IO.Path]::DirectorySeparatorChar) + [System.IO.Path]::DirectorySeparatorChar

if (-not $outputPath.StartsWith($artifactPrefix, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "Release output must stay under $artifactRoot"
}
if ($outputPath -eq $artifactRoot) {
    throw 'Release output cannot be the artifact root itself.'
}
if (Test-Path -LiteralPath $outputPath) {
    Remove-Item -LiteralPath $outputPath -Recurse -Force
}

$projectPath = Join-Path $repoRoot 'src\OpenVisionLab.ThreeD.Shell\OpenVisionLab.ThreeD.Shell.csproj'
$publishArguments = @(
    'publish', $projectPath,
    '-c', $Configuration,
    '-r', $RuntimeIdentifier,
    '--self-contained', 'true',
    '-p:PublishSingleFile=false',
    '-o', $outputPath)
if ($NoRestore) {
    $publishArguments += '--no-restore'
}
if (-not [string]::IsNullOrWhiteSpace($BuildArtifactsPath)) {
    $resolvedBuildArtifactsPath = if ([System.IO.Path]::IsPathRooted($BuildArtifactsPath)) {
        [System.IO.Path]::GetFullPath($BuildArtifactsPath)
    }
    else {
        [System.IO.Path]::GetFullPath((Join-Path $repoRoot $BuildArtifactsPath))
    }
    $publishArguments += @('--artifacts-path', $resolvedBuildArtifactsPath)
}

& dotnet @publishArguments
if ($LASTEXITCODE -ne 0) {
    throw "Self-contained publish failed with exit code $LASTEXITCODE."
}

$requiredFiles = @(
    'OpenVisionLab.ThreeD.Shell.exe',
    'OpenVisionLab.ThreeD.Shell.dll',
    'OpenVisionLab.ThreeD.Core.dll',
    'OpenVisionLab.ThreeD.Data.dll',
    'OpenVisionLab.ThreeD.Tools.dll',
    'OpenVisionLab.ThreeD.Viewer.dll',
    'OpenVisionLab.ThreeD.Docking.Controls.dll',
    'Lib.ThreeD.dll',
    'SharpGL.dll',
    'SharpGL.WPF.dll',
    'Unofficial.laszip.netstandard.dll')
$missingFiles = @($requiredFiles | Where-Object { -not (Test-Path -LiteralPath (Join-Path $outputPath $_) -PathType Leaf) })
if ($missingFiles.Count -gt 0) {
    throw "Self-contained package is incomplete: $($missingFiles -join ', ')"
}

Copy-Item -LiteralPath (Join-Path $repoRoot 'LICENSE') -Destination $outputPath
Copy-Item -LiteralPath (Join-Path $repoRoot 'NOTICE') -Destination $outputPath
Copy-Item `
    -LiteralPath (Join-Path $repoRoot 'docs\OPENVISIONLAB_3D_WINDOWS_PACKAGE_QUICK_START.md') `
    -Destination (Join-Path $outputPath 'README.md')
Copy-Item -LiteralPath (Join-Path $repoRoot 'recipes') -Destination (Join-Path $outputPath 'recipes') -Recurse
$sampleDestination = Join-Path $outputPath '3D\Samples'
New-Item -ItemType Directory -Force -Path $sampleDestination | Out-Null
Copy-Item -LiteralPath (Join-Path $repoRoot '3D\Samples\ThicknessCouponV1') -Destination $sampleDestination -Recurse
$publicSampleDestination = Join-Path $outputPath '3D\PublicSamples'
New-Item -ItemType Directory -Force -Path $publicSampleDestination | Out-Null
Copy-Item -LiteralPath (Join-Path $repoRoot '3D\PublicSamples\README.md') -Destination $publicSampleDestination
foreach ($sampleFolder in @('glTF', 'STL', 'PointCloud')) {
    Copy-Item `
        -LiteralPath (Join-Path $repoRoot "3D\PublicSamples\$sampleFolder") `
        -Destination $publicSampleDestination `
        -Recurse
}
$documentationDestination = Join-Path $outputPath 'documentation'
New-Item -ItemType Directory -Force -Path $documentationDestination | Out-Null
Copy-Item `
    -LiteralPath (Join-Path $repoRoot 'docs\OPENVISIONLAB_3D_USER_TUTORIAL.md') `
    -Destination (Join-Path $documentationDestination 'USER_TUTORIAL.md')
Copy-Item `
    -LiteralPath (Join-Path $repoRoot 'docs\OPENVISIONLAB_3D_SYSTEM_REQUIREMENTS_AND_SETUP.md') `
    -Destination (Join-Path $documentationDestination 'SYSTEM_REQUIREMENTS.md')

[xml]$buildProperties = Get-Content -LiteralPath (Join-Path $repoRoot 'Directory.Build.props') -Raw
$productVersion = [string]$buildProperties.Project.PropertyGroup.OpenVisionLabProductVersion
$gitCommit = (& git -C $repoRoot rev-parse HEAD 2>$null)
$gitStatus = (& git -C $repoRoot status --porcelain 2>$null)
$files = @(
    Get-ChildItem -LiteralPath $outputPath -Recurse -File |
        Sort-Object FullName |
        ForEach-Object {
            [ordered]@{
                path = $_.FullName.Substring($outputPath.Length + 1).Replace('\', '/')
                sizeBytes = $_.Length
                sha256 = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash
            }
        })
$manifest = [ordered]@{
    schemaVersion = '1.0'
    applicationName = 'OpenVisionLab 3D Studio'
    applicationVersion = $productVersion
    configuration = $Configuration
    runtimeIdentifier = $RuntimeIdentifier
    selfContained = $true
    separateDotNetInstallationRequired = $false
    gitCommit = if ($gitCommit) { $gitCommit.Trim() } else { 'unknown' }
    gitWorkingTree = if ($gitStatus) { 'dirty' } else { 'clean' }
    dotNetSdkVersion = (& dotnet --version).Trim()
    prerequisites = @(
        'Windows 10 build 19041 or later, or Windows 11, x64',
        'OpenGL-compatible GPU and current vendor driver')
    files = $files
}
$manifestPath = Join-Path $outputPath 'openvisionlab-3d-studio-manifest.json'
$manifest | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $manifestPath -Encoding utf8

Write-Host "Self-contained package: $outputPath"
Write-Host "Required application files: $($requiredFiles.Count)/$($requiredFiles.Count)"
Write-Host "Payload files: $($files.Count)"
Write-Host "Manifest: $manifestPath"
