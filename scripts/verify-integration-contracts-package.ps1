[CmdletBinding()]
param([string]$ReportPath)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.IO.Compression.FileSystem

$package = Join-Path $PSScriptRoot "..\third_party\OpenVisionLabIntegrationContracts\OpenVisionLab.Integration.Contracts.0.2.0-alpha.3.nupkg"
$checksum = "$package.sha256"
$expectedHash = ([regex]::Match(
    (Get-Content -LiteralPath $checksum -Raw),
    "(?im)\b([A-F0-9]{64})\b")).Groups[1].Value.ToUpperInvariant()
$actualHash = (Get-FileHash -LiteralPath $package -Algorithm SHA256).Hash.ToUpperInvariant()
if ($actualHash -ne $expectedHash) {
    throw "Integration Contracts package SHA-256 mismatch. Expected $expectedHash, actual $actualHash."
}
$expectedSourceCommit = "f4743f3307d20a963b2197f2019713320b9859b9"

$archive = [System.IO.Compression.ZipFile]::OpenRead((Resolve-Path $package).Path)
try {
    $entries = @($archive.Entries.FullName)
    foreach ($required in @(
        "OpenVisionLab.Integration.Contracts.nuspec",
        "LICENSE",
        "NOTICE",
        "README.md",
        "docs/PROTOCOL.md",
        "fixtures/v1/valid/handoff.json",
        "fixtures/v1/valid/acknowledgement.json",
        "fixtures/v1/valid/result.json",
        "fixtures/v2/valid/handoff.json",
        "fixtures/v2/valid/acknowledgement.json",
        "fixtures/v2/valid/result.json",
        "lib/net8.0/OpenVisionLab.Integration.Contracts.dll")) {
        if ($entries -notcontains $required) {
            throw "Integration Contracts package is missing: $required"
        }
    }

    $entry = $archive.GetEntry("OpenVisionLab.Integration.Contracts.nuspec")
    $reader = [System.IO.StreamReader]::new($entry.Open())
    try { [xml]$nuspec = $reader.ReadToEnd() } finally { $reader.Dispose() }
    $ns = [System.Xml.XmlNamespaceManager]::new($nuspec.NameTable)
    $ns.AddNamespace("n", "http://schemas.microsoft.com/packaging/2012/06/nuspec.xsd")
    $metadata = $nuspec.SelectSingleNode("/n:package/n:metadata", $ns)
    $repository = $metadata.SelectSingleNode("n:repository", $ns)
    $id = [string]$metadata.id
    $version = [string]$metadata.version
    $sourceCommit = [string]$repository.commit
    if ($id -ne "OpenVisionLab.Integration.Contracts" -or $version -ne "0.2.0-alpha.3" -or $sourceCommit -ne $expectedSourceCommit) {
        throw "Integration Contracts package metadata mismatch."
    }
}
finally {
    $archive.Dispose()
}

$result = "IntegrationContractsPackage|pass=True|version=0.2.0-alpha.3|schemas=1.0,2.0|sourceState=clean|sourceCommit=$expectedSourceCommit|sha256=$actualHash|target=net8.0"
if (-not [string]::IsNullOrWhiteSpace($ReportPath)) {
    New-Item -ItemType Directory -Force -Path (Split-Path -Parent $ReportPath) | Out-Null
    Set-Content -LiteralPath $ReportPath -Value $result -Encoding utf8
}
$result
