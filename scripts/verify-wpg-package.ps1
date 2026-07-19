[CmdletBinding()]
param(
    [string]$PackagePath,
    [string]$ChecksumPath,
    [string]$ReportPath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($PackagePath)) {
    $PackagePath = Join-Path $PSScriptRoot "..\third_party\WpgCustom\OpenVisionLab.WpfPropertyGrid.ThreeD.1.0.0-ovl3d.1.nupkg"
}

if ([string]::IsNullOrWhiteSpace($ChecksumPath)) {
    $ChecksumPath = "$PackagePath.sha256"
}

function Write-VerificationReport {
    param([string]$Line)

    Write-Output $Line
    if ([string]::IsNullOrWhiteSpace($ReportPath)) {
        return
    }

    $directory = Split-Path -Parent $ReportPath
    if (-not [string]::IsNullOrWhiteSpace($directory)) {
        New-Item -ItemType Directory -Force -Path $directory | Out-Null
    }

    Set-Content -LiteralPath $ReportPath -Value $Line -Encoding utf8
}

$resolvedPackagePath = (Resolve-Path -LiteralPath $PackagePath -ErrorAction Stop).Path
$resolvedChecksumPath = (Resolve-Path -LiteralPath $ChecksumPath -ErrorAction Stop).Path
$expectedHashMatch = [regex]::Match((Get-Content -LiteralPath $resolvedChecksumPath -Raw), "(?im)\b([A-F0-9]{64})\b")
if (-not $expectedHashMatch.Success) {
    throw "WPG checksum manifest does not contain a SHA-256 value: $resolvedChecksumPath"
}

$expectedHash = $expectedHashMatch.Groups[1].Value.ToUpperInvariant()
$actualHash = (Get-FileHash -LiteralPath $resolvedPackagePath -Algorithm SHA256).Hash.ToUpperInvariant()
if ($actualHash -ne $expectedHash) {
    throw "WPG package SHA-256 mismatch. Expected $expectedHash, actual $actualHash."
}

Add-Type -AssemblyName System.IO.Compression.FileSystem
$archive = [System.IO.Compression.ZipFile]::OpenRead($resolvedPackagePath)
try {
    $requiredEntries = @(
        "OpenVisionLab.WpfPropertyGrid.ThreeD.nuspec",
        "LICENSE",
        "README.md",
        "theme-contract.json",
        "lib/net10.0-windows10.0.19041/OpenVisionLab.WpfPropertyGrid.ThreeD.dll"
    )
    $entryNames = @($archive.Entries | ForEach-Object FullName)
    foreach ($requiredEntry in $requiredEntries) {
        if ($entryNames -notcontains $requiredEntry) {
            throw "WPG package is missing required entry: $requiredEntry"
        }
    }

    $nuspecEntry = $archive.Entries | Where-Object FullName -eq "OpenVisionLab.WpfPropertyGrid.ThreeD.nuspec" | Select-Object -First 1
    $reader = [System.IO.StreamReader]::new($nuspecEntry.Open())
    try {
        [xml]$nuspec = $reader.ReadToEnd()
    }
    finally {
        $reader.Dispose()
    }

    $namespaceManager = [System.Xml.XmlNamespaceManager]::new($nuspec.NameTable)
    $namespaceManager.AddNamespace("n", "http://schemas.microsoft.com/packaging/2012/06/nuspec.xsd")
    $metadata = $nuspec.SelectSingleNode("/n:package/n:metadata", $namespaceManager)
    $repository = $metadata.SelectSingleNode("n:repository", $namespaceManager)
    $license = $metadata.SelectSingleNode("n:license", $namespaceManager)
    if ($null -eq $metadata -or $null -eq $repository -or $null -eq $license) {
        throw "WPG package nuspec metadata is incomplete."
    }

    $id = [string]$metadata.id
    $version = [string]$metadata.version
    $sourceCommit = [string]$repository.commit
    $licenseExpression = [string]$license.InnerText
    $metadataMatches = $id -eq "OpenVisionLab.WpfPropertyGrid.ThreeD" -and $version -eq "1.0.0-ovl3d.1" -and $sourceCommit -eq "2050f36a144f8c4c6964ff5777ec21aa03e89877" -and $licenseExpression -eq "Apache-2.0"
    if (-not $metadataMatches) {
        throw "WPG package metadata mismatch. id=$id version=$version sourceCommit=$sourceCommit license=$licenseExpression"
    }

    $themeEntry = $archive.Entries | Where-Object FullName -eq "theme-contract.json" | Select-Object -First 1
    $reader = [System.IO.StreamReader]::new($themeEntry.Open())
    try {
        $theme = $reader.ReadToEnd() | ConvertFrom-Json
    }
    finally {
        $reader.Dispose()
    }

    $themeMatches = $theme.assemblyName -eq "OpenVisionLab.WpfPropertyGrid.ThreeD" -and $theme.sourceCommit -eq $sourceCommit -and $theme.resourceScope -eq "assembly" -and $theme.applicationMergeRequired -eq $false -and @($theme.keys).Count -eq 8 -and @($theme.keys | Where-Object { -not $_.StartsWith("Ovl3D.Wpg.", [StringComparison]::Ordinal) }).Count -eq 0
    if (-not $themeMatches) {
        throw "WPG theme isolation contract is invalid."
    }

    Write-VerificationReport "WpgPackage|pass=True|id=$id|version=$version|sourceCommit=$sourceCommit|sha256=$actualHash|target=net10.0-windows10.0.19041|themeScope=$($theme.resourceScope)|themeKeys=$(@($theme.keys).Count)"
}
finally {
    $archive.Dispose()
}
