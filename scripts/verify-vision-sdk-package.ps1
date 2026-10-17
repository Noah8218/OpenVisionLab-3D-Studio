[CmdletBinding()]
param(
    [string]$PackagePath,
    [string]$ChecksumPath,
    [string]$ReportPath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($PackagePath)) {
    $PackagePath = Join-Path $PSScriptRoot "..\third_party\OpenVisionLabVisionSdk\OpenVisionLab.Vision3D.3.3.0-dev.20261017.physical-edge-gap-flush.1.nupkg"
}

if ([string]::IsNullOrWhiteSpace($ChecksumPath)) {
    $ChecksumPath = Join-Path $PSScriptRoot "..\third_party\OpenVisionLabVisionSdk\OpenVisionLab.Vision3D.3.3.0-dev.20261017.physical-edge-gap-flush.1.nupkg.sha256"
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
    $namespaceManager.AddNamespace("n", $nuspec.DocumentElement.NamespaceURI)
    $metadata = $nuspec.SelectSingleNode("/n:package/n:metadata", $namespaceManager)
    if ($null -eq $metadata) { throw "OpenVisionLab Vision SDK package nuspec metadata is incomplete." }
    $repository = $metadata.SelectSingleNode("n:repository", $namespaceManager)
    if ($null -eq $metadata -or $null -eq $repository) {
        throw "OpenVisionLab Vision SDK package nuspec metadata is incomplete."
    }

    $id = [string]$metadata.id
    $version = [string]$metadata.version
    $sourceCommit = [string]$repository.commit
    if ($id -ne "OpenVisionLab.Vision3D" -or $version -ne "3.3.0-dev.20261017.physical-edge-gap-flush.1" -or $sourceCommit -ne "456eb2af7a62962144757d967538594fde79e481") {
        throw "OpenVisionLab Vision SDK package metadata mismatch. id=$id version=$version sourceCommit=$sourceCommit"
    }

    $computeVersion = "3.3.0-dev.20261017.physical-edge-gap-flush.1"
    $computeDependency = $metadata.SelectSingleNode("n:dependencies/n:group/n:dependency[@id='OpenVisionLab.Compute']", $namespaceManager)
    if ($null -eq $computeDependency -or [string]$computeDependency.version -ne $computeVersion) {
        throw "Vision3D must declare the pinned OpenVisionLab.Compute dependency."
    }
    $computeName = "OpenVisionLab.Compute.$computeVersion.nupkg"
    $computePath = Join-Path (Split-Path -Parent $resolvedPackagePath) $computeName
    $computeChecksum = Get-Content -LiteralPath ($computePath + ".sha256") -Raw
    $computeExpected = [regex]::Match($computeChecksum, "(?im)\b([A-F0-9]{64})\b")
    $computeActual = (Get-FileHash -LiteralPath $computePath -Algorithm SHA256).Hash.ToUpperInvariant()
    if (-not $computeExpected.Success -or $computeExpected.Groups[1].Value.ToUpperInvariant() -ne $computeActual) {
        throw "OpenVisionLab.Compute package SHA-256 mismatch or missing checksum."
    }
    $computeArchive = [System.IO.Compression.ZipFile]::OpenRead($computePath)
    try {
        foreach ($requiredEntry in @("OpenVisionLab.Compute.nuspec", "LICENSE", "NOTICE", "README.md", "lib/netstandard2.0/OpenVisionLab.Compute.dll", "lib/netstandard2.0/OpenVisionLab.Compute.xml")) {
            if (@($computeArchive.Entries.FullName) -notcontains $requiredEntry) {
                throw "OpenVisionLab.Compute package is missing required entry: $requiredEntry"
            }
        }
        $computeEntry = $computeArchive.GetEntry("OpenVisionLab.Compute.nuspec")
        $computeReader = [System.IO.StreamReader]::new($computeEntry.Open())
        try { [xml]$computeSpec = $computeReader.ReadToEnd() }
        finally { $computeReader.Dispose() }
        $computeNamespace = [System.Xml.XmlNamespaceManager]::new($computeSpec.NameTable)
        $computeNamespace.AddNamespace("n", $computeSpec.DocumentElement.NamespaceURI)
        $computeMetadata = $computeSpec.SelectSingleNode("/n:package/n:metadata", $computeNamespace)
        $computeRepository = $computeMetadata.SelectSingleNode("n:repository", $computeNamespace)
        if ([string]$computeMetadata.id -ne "OpenVisionLab.Compute" -or [string]$computeMetadata.version -ne $computeVersion -or [string]$computeRepository.commit -ne $sourceCommit) {
            throw "OpenVisionLab.Compute package identity or committed source mismatch."
        }
    }
    finally { $computeArchive.Dispose() }
    Write-Output "VisionSdkDependency|pass=True|id=OpenVisionLab.Compute|version=$computeVersion|sourceCommit=$sourceCommit|sha256=$computeActual|target=netstandard2.0"

    Write-VerificationReport "VisionSdkPackage|pass=True|id=$id|version=$version|sourceCommit=$sourceCommit|sha256=$actualHash|target=netstandard2.0"
}
finally {
    $archive.Dispose()
}
