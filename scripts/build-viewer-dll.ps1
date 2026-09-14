[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',
    [string]$OutputDirectory = 'artifacts/viewer-dll/net10.0-windows',
    [switch]$NoRestore
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$artifactRoot = [System.IO.Path]::GetFullPath((Join-Path $repoRoot 'artifacts'))
$outputPath = [System.IO.Path]::GetFullPath((Join-Path $repoRoot $OutputDirectory))
$artifactPrefix = $artifactRoot.TrimEnd([System.IO.Path]::DirectorySeparatorChar) + [System.IO.Path]::DirectorySeparatorChar

if (-not $outputPath.StartsWith($artifactPrefix, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "Viewer DLL output must stay under $artifactRoot"
}

if (Test-Path -LiteralPath $outputPath) {
    Remove-Item -LiteralPath $outputPath -Recurse -Force
}

$projectPath = Join-Path $repoRoot 'src/OpenVisionLab.ThreeD.Viewer/OpenVisionLab.ThreeD.Viewer.csproj'
$buildPropertiesPath = Join-Path $repoRoot 'Directory.Build.props'
[xml]$buildProperties = Get-Content -LiteralPath $buildPropertiesPath -Raw
$productVersion = [string]$buildProperties.Project.PropertyGroup.OpenVisionLabProductVersion
$viewerHostApiVersion = [string]$buildProperties.Project.PropertyGroup.OpenVisionLabViewerHostApiVersion
$publishArguments = @('publish', $projectPath, '-c', $Configuration, '-o', $outputPath)
if ($NoRestore) {
    $publishArguments += '--no-restore'
}

& dotnet @publishArguments
if ($LASTEXITCODE -ne 0) {
    throw "Viewer DLL publish failed with exit code $LASTEXITCODE."
}

Copy-Item -LiteralPath (Join-Path $repoRoot 'LICENSE') -Destination $outputPath -Force
Copy-Item -LiteralPath (Join-Path $repoRoot 'NOTICE') -Destination $outputPath -Force
$attributionRoot = Join-Path $repoRoot 'third_party/Viewer'
$attributionMapPath = Join-Path $attributionRoot 'VIEWER_DEPENDENCY_ATTRIBUTION.json'
$attributionNoticePath = Join-Path $attributionRoot 'THIRD-PARTY-NOTICES.txt'
if (-not (Test-Path -LiteralPath $attributionMapPath -PathType Leaf) -or
    -not (Test-Path -LiteralPath $attributionNoticePath -PathType Leaf)) {
    throw "Viewer dependency attribution files are missing under $attributionRoot"
}
$attributionMap = Get-Content -LiteralPath $attributionMapPath -Raw | ConvertFrom-Json
if ([string]$attributionMap.bundle.manifestField -ne 'dependencyAttributionFile' -or
    [string]$attributionMap.bundle.attributionFile -ne 'THIRD-PARTY-NOTICES.txt') {
    throw 'Viewer dependency attribution map has an invalid manifest contract.'
}
Copy-Item -LiteralPath $attributionMapPath -Destination $outputPath -Force
Copy-Item -LiteralPath $attributionNoticePath -Destination $outputPath -Force

$requiredFiles = @(
    'OpenVisionLab.ThreeD.Viewer.dll',
    'OpenVisionLab.ThreeD.Viewer.deps.json',
    'OpenVisionLab.ThreeD.Core.dll',
    'OpenVisionLab.ThreeD.Data.dll',
    'OpenVisionLab.ThreeD.Tools.dll',
    'SharpGL.dll',
    'SharpGL.SceneGraph.dll',
    'SharpGL.WPF.dll',
    'Unofficial.laszip.netstandard.dll',
    'LICENSE',
    'NOTICE',
    'VIEWER_DEPENDENCY_ATTRIBUTION.json',
    'THIRD-PARTY-NOTICES.txt',
    'runtimes/win-x64/native/OpenCvSharpExtern.dll'
)

$missingFiles = @($requiredFiles | Where-Object { -not (Test-Path -LiteralPath (Join-Path $outputPath $_) -PathType Leaf) })
if ($missingFiles.Count -gt 0) {
    throw "Viewer DLL bundle is incomplete: $($missingFiles -join ', ')"
}

$viewerAssembly = [System.Reflection.AssemblyName]::GetAssemblyName(
    (Join-Path $outputPath 'OpenVisionLab.ThreeD.Viewer.dll'))
$gitCommit = (& git -C $repoRoot rev-parse HEAD 2>$null)
if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($gitCommit)) {
    $gitCommit = if ([string]::IsNullOrWhiteSpace($env:GITHUB_SHA)) { 'unknown' } else { $env:GITHUB_SHA }
} else {
    $gitCommit = $gitCommit.Trim()
}
$gitStatus = (& git -C $repoRoot status --porcelain 2>$null)
$gitWorkingTree = if ($LASTEXITCODE -ne 0) { 'unknown' } elseif ($gitStatus) { 'dirty' } else { 'clean' }
$dotNetSdkVersion = (& dotnet --version).Trim()
$manifestPath = Join-Path $outputPath 'viewer-dll-manifest.json'
$manifestFiles = @(
    Get-ChildItem -LiteralPath $outputPath -Recurse -File -Force |
        Where-Object {
            -not $_.FullName.Equals($manifestPath, [System.StringComparison]::OrdinalIgnoreCase)
        } |
        Sort-Object { $_.FullName.Substring($outputPath.Length).TrimStart('\', '/').Replace('\', '/') } |
        ForEach-Object {
            $relativePath = $_.FullName.Substring($outputPath.Length).TrimStart('\', '/').Replace('\', '/')
            [ordered]@{
                name = $relativePath
                sizeBytes = $_.Length
                sha256 = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
            }
        }
)
$manifest = [ordered]@{
    schemaVersion = '1.0'
    applicationName = 'OpenVisionLab 3D Studio Viewer'
    applicationVersion = $productVersion
    viewerHostApiVersion = $viewerHostApiVersion
    targetFramework = 'net10.0-windows'
    configuration = $Configuration
    viewerAssembly = $viewerAssembly.Name
    viewerAssemblyVersion = $viewerAssembly.Version.ToString()
    gitCommit = $gitCommit
    gitWorkingTree = $gitWorkingTree
    dotNetSdkVersion = $dotNetSdkVersion
    dependencyAttributionFile = 'VIEWER_DEPENDENCY_ATTRIBUTION.json'
    attributionFile = 'THIRD-PARTY-NOTICES.txt'
    dependencyAttributionSchemaVersion = [string]$attributionMap.schemaVersion
    files = $manifestFiles
}

$manifest | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $manifestPath -Encoding utf8

Write-Output "Viewer DLL bundle: $outputPath"
Write-Output "Viewer assembly: $($viewerAssembly.Name) $($viewerAssembly.Version)"
Write-Output "Required files: $($requiredFiles.Count)/$($requiredFiles.Count)"
Write-Output "Payload files: $($manifestFiles.Count)"
Write-Output "Manifest: $manifestPath"
