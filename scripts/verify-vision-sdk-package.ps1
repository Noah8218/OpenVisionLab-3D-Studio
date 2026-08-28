[CmdletBinding()]
param(
    [string]$PackagePath,
    [string]$ChecksumPath,
    [string]$ReportPath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($PackagePath)) {
    $PackagePath = Join-Path $PSScriptRoot "..\third_party\OpenVisionLabVisionSdk\OpenVisionLab.Vision3D.3.0.1-dev.20260828.point-cloud-background-filter.1.nupkg"
}

if ([string]::IsNullOrWhiteSpace($ChecksumPath)) {
    $ChecksumPath = Join-Path $PSScriptRoot "..\third_party\OpenVisionLabVisionSdk\OpenVisionLab.Vision3D.3.0.1-dev.20260828.point-cloud-background-filter.1.nupkg.sha256"
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
$checksumText = Get-Content -LiteralPath $resolvedChecksumPath -Raw
$expectedHashMatch = [regex]::Match($checksumText, "(?im)\b([A-F0-9]{64})\b")
if (-not $expectedHashMatch.Success) {
    throw "OpenVisionLab Vision SDK checksum manifest does not contain a SHA-256 value: $resolvedChecksumPath"
}

$expectedHash = $expectedHashMatch.Groups[1].Value.ToUpperInvariant()
$actualHash = (Get-FileHash -LiteralPath $resolvedPackagePath -Algorithm SHA256).Hash.ToUpperInvariant()
if ($actualHash -ne $expectedHash) {
    throw "OpenVisionLab Vision SDK package SHA-256 mismatch. Expected $expectedHash, actual $actualHash."
}

Add-Type -AssemblyName System.IO.Compression.FileSystem
$archive = [System.IO.Compression.ZipFile]::OpenRead($resolvedPackagePath)
try {
    $entries = @($archive.Entries | ForEach-Object FullName)
    foreach ($requiredEntry in @(
        "OpenVisionLab.Vision3D.nuspec",
        "LICENSE",
        "NOTICE",
        "README.md",
        "docs/three-d-inspection.md",
        "lib/netstandard2.0/OpenVisionLab.Vision3D.dll",
        "lib/netstandard2.0/OpenVisionLab.Vision3D.xml")) {
        if ($entries -notcontains $requiredEntry) {
            throw "OpenVisionLab Vision SDK package is missing required entry: $requiredEntry"
        }
    }

    $nuspecEntry = $archive.Entries | Where-Object FullName -eq "OpenVisionLab.Vision3D.nuspec" | Select-Object -First 1
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
    if ($null -eq $metadata -or $null -eq $repository) {
        throw "OpenVisionLab Vision SDK package nuspec metadata is incomplete."
    }

    $id = [string]$metadata.id
    $version = [string]$metadata.version
    $sourceCommit = [string]$repository.commit
    if ($id -ne "OpenVisionLab.Vision3D" -or $version -ne "3.0.1-dev.20260828.point-cloud-background-filter.1" -or $sourceCommit -ne "35f1eef6626db710ac18452cd1e729530f2c0f2f") {
        throw "OpenVisionLab Vision SDK package metadata mismatch. id=$id version=$version sourceCommit=$sourceCommit"
    }

    Write-VerificationReport "VisionSdkPackage|pass=True|id=$id|version=$version|sourceCommit=$sourceCommit|sha256=$actualHash|target=netstandard2.0"
}
finally {
    $archive.Dispose()
}
