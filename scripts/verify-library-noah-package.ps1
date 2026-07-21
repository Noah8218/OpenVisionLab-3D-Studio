[CmdletBinding()]
param(
    [string]$PackagePath,
    [string]$ChecksumPath,
    [string]$ReportPath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($PackagePath)) {
    $PackagePath = Join-Path $PSScriptRoot "..\third_party\LibraryNoah\Lib.ThreeD.2.3.0.nupkg"
}

if ([string]::IsNullOrWhiteSpace($ChecksumPath)) {
    $ChecksumPath = Join-Path $PSScriptRoot "..\third_party\LibraryNoah\Lib.ThreeD.2.3.0.nupkg.sha256"
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
    throw "Library-Noah checksum manifest does not contain a SHA-256 value: $resolvedChecksumPath"
}

$expectedHash = $expectedHashMatch.Groups[1].Value.ToUpperInvariant()
$actualHash = (Get-FileHash -LiteralPath $resolvedPackagePath -Algorithm SHA256).Hash.ToUpperInvariant()
if ($actualHash -ne $expectedHash) {
    throw "Library-Noah package SHA-256 mismatch. Expected $expectedHash, actual $actualHash."
}

Add-Type -AssemblyName System.IO.Compression.FileSystem
$archive = [System.IO.Compression.ZipFile]::OpenRead($resolvedPackagePath)
try {
    $entries = @($archive.Entries | ForEach-Object FullName)
    foreach ($requiredEntry in @(
        "Lib.ThreeD.nuspec",
        "LICENSE",
        "NOTICE",
        "lib/netstandard2.0/Lib.ThreeD.dll")) {
        if ($entries -notcontains $requiredEntry) {
            throw "Library-Noah package is missing required entry: $requiredEntry"
        }
    }

    $nuspecEntry = $archive.Entries | Where-Object FullName -eq "Lib.ThreeD.nuspec" | Select-Object -First 1
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
        throw "Library-Noah package nuspec metadata is incomplete."
    }

    $id = [string]$metadata.id
    $version = [string]$metadata.version
    $sourceCommit = [string]$repository.commit
    if ($id -ne "Lib.ThreeD" -or $version -ne "2.3.0" -or $sourceCommit -ne "630e37b9111f3223217c815e19c480546fde8ad7") {
        throw "Library-Noah package metadata mismatch. id=$id version=$version sourceCommit=$sourceCommit"
    }

    Write-VerificationReport "LibraryNoahPackage|pass=True|id=$id|version=$version|sourceCommit=$sourceCommit|sha256=$actualHash|target=netstandard2.0"
}
finally {
    $archive.Dispose()
}
