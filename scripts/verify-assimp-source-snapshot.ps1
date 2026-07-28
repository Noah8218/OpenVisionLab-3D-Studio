[CmdletBinding()]
param(
    [string]$ArchivePath = 'artifacts\dependency-candidates\open3d-license-audit-0.19.0\source\Open3D-0.19.0\3rdparty_downloads\assimp\v5.4.2.zip',
    [string]$SourceDirectory = 'artifacts\o3d-clean\b\assimp\src\ext_assimp',
    [string]$ExpectedArchiveSha256 = '03e38d123f6bf19a48658d197fd09c9a69db88c076b56a476ab2da9f5eb87dcc',
    [string]$ReportPath = 'artifacts\dependency-candidates\assimp-source-snapshot\assimp-source-snapshot.json',
    [ValidateRange(1, 1000)]
    [int]$DifferenceSampleLimit = 20
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot

function Resolve-WorkspacePath([string]$Path) {
    if ([System.IO.Path]::IsPathRooted($Path)) {
        return [System.IO.Path]::GetFullPath($Path)
    }

    return [System.IO.Path]::GetFullPath((Join-Path $repoRoot $Path))
}

function Get-Sha256ForStream([System.IO.Stream]$Stream) {
    $sha256 = [System.Security.Cryptography.SHA256]::Create()
    try {
        return ([System.BitConverter]::ToString($sha256.ComputeHash($Stream))).Replace('-', '').ToLowerInvariant()
    }
    finally {
        $sha256.Dispose()
    }
}

function Get-Sha256ForFile([string]$Path) {
    $stream = [System.IO.File]::OpenRead($Path)
    try {
        return Get-Sha256ForStream $stream
    }
    finally {
        $stream.Dispose()
    }
}

function Get-CanonicalManifestSha256([object]$Entries) {
    $keys = [System.Collections.Generic.List[string]]::new()
    foreach ($key in $Entries.Keys) {
        $keys.Add($key)
    }

    $keys.Sort([System.StringComparer]::Ordinal)
    $builder = [System.Text.StringBuilder]::new()
    foreach ($key in $keys) {
        $record = $Entries[$key]
        [void]$builder.Append($key)
        [void]$builder.Append('|')
        [void]$builder.Append($record.Length.ToString([System.Globalization.CultureInfo]::InvariantCulture))
        [void]$builder.Append('|')
        [void]$builder.Append($record.Sha256)
        [void]$builder.Append("`n")
    }

    $bytes = [System.Text.UTF8Encoding]::new($false).GetBytes($builder.ToString())
    $stream = [System.IO.MemoryStream]::new($bytes, $false)
    try {
        return Get-Sha256ForStream $stream
    }
    finally {
        $stream.Dispose()
    }
}

function Get-Sample([System.Collections.IEnumerable]$Items, [int]$Limit) {
    return @($Items | Select-Object -First $Limit)
}

$issues = [System.Collections.Generic.List[string]]::new()
$archive = $null
$archiveEntries = $null
$sourceEntries = $null
$archivePathResolved = Resolve-WorkspacePath $ArchivePath
$sourceDirectoryResolved = Resolve-WorkspacePath $SourceDirectory
$reportPathResolved = Resolve-WorkspacePath $ReportPath
$actualArchiveSha256 = $null
$zipRootDirectory = $null
$archiveManifestSha256 = $null
$sourceManifestSha256 = $null
$missingPaths = [System.Collections.Generic.List[string]]::new()
$extraPaths = [System.Collections.Generic.List[string]]::new()
$modifiedPaths = [System.Collections.Generic.List[object]]::new()

try {
    if ($ExpectedArchiveSha256 -notmatch '^[0-9A-Fa-f]{64}$') {
        throw "ExpectedArchiveSha256 must be a 64-character SHA-256 value."
    }

    if (-not (Test-Path -LiteralPath $archivePathResolved -PathType Leaf)) {
        throw "Archive file does not exist: $archivePathResolved"
    }

    if (-not (Test-Path -LiteralPath $sourceDirectoryResolved -PathType Container)) {
        throw "Source directory does not exist: $sourceDirectoryResolved"
    }

    $actualArchiveSha256 = Get-Sha256ForFile $archivePathResolved
    if (-not $actualArchiveSha256.Equals($ExpectedArchiveSha256, [System.StringComparison]::OrdinalIgnoreCase)) {
        $issues.Add("Archive SHA-256 does not match the expected value.")
    }

    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $archive = [System.IO.Compression.ZipFile]::OpenRead($archivePathResolved)
    $fileEntries = @($archive.Entries | Where-Object { -not [string]::IsNullOrEmpty($_.Name) })
    if ($fileEntries.Count -eq 0) {
        throw "Archive contains no file entries: $archivePathResolved"
    }

    $roots = @($fileEntries | ForEach-Object { $_.FullName.Split('/')[0] } | Sort-Object -Unique)
    if ($roots.Count -ne 1 -or [string]::IsNullOrWhiteSpace($roots[0])) {
        throw "Archive must contain exactly one non-empty root directory."
    }

    $zipRootDirectory = $roots[0]
    $rootPrefix = "$zipRootDirectory/"
    $ordinal = [System.StringComparer]::Ordinal
    $archiveEntries = [System.Collections.Generic.Dictionary[string, object]]::new($ordinal)
    foreach ($entry in $fileEntries) {
        if ($entry.FullName.Contains('\') -or -not $entry.FullName.StartsWith($rootPrefix, [System.StringComparison]::Ordinal)) {
            throw "Archive entry is outside the expected root or uses an invalid separator: $($entry.FullName)"
        }

        $relativePath = $entry.FullName.Substring($rootPrefix.Length)
        $segments = @($relativePath.Split('/'))
        if ($segments.Count -eq 0 -or ($segments | Where-Object { [string]::IsNullOrWhiteSpace($_) -or $_ -eq '.' -or $_ -eq '..' }).Count -gt 0) {
            throw "Archive entry has an unsafe relative path: $($entry.FullName)"
        }

        if ($archiveEntries.ContainsKey($relativePath)) {
            throw "Archive contains duplicate file path: $relativePath"
        }

        $entryStream = $entry.Open()
        try {
            $entrySha256 = Get-Sha256ForStream $entryStream
        }
        finally {
            $entryStream.Dispose()
        }

        $archiveEntries.Add($relativePath, [pscustomobject]@{
            Length = [int64]$entry.Length
            Sha256 = $entrySha256
        })
    }

    $sourceRoot = $sourceDirectoryResolved.TrimEnd('\', '/')
    $sourceEntries = [System.Collections.Generic.Dictionary[string, object]]::new($ordinal)
    Get-ChildItem -LiteralPath $sourceRoot -Recurse -File -Force | ForEach-Object {
        $relativePath = $_.FullName.Substring($sourceRoot.Length).TrimStart([char[]]@('\', '/')).Replace('\', '/')
        if ($sourceEntries.ContainsKey($relativePath)) {
            throw "Source directory contains duplicate file path: $relativePath"
        }

        $sourceEntries.Add($relativePath, [pscustomobject]@{
            Length = [int64]$_.Length
            Sha256 = Get-Sha256ForFile $_.FullName
        })
    }

    foreach ($relativePath in $archiveEntries.Keys) {
        if (-not $sourceEntries.ContainsKey($relativePath)) {
            $missingPaths.Add($relativePath)
            continue
        }

        $archiveRecord = $archiveEntries[$relativePath]
        $sourceRecord = $sourceEntries[$relativePath]
        if ($archiveRecord.Length -ne $sourceRecord.Length -or $archiveRecord.Sha256 -ne $sourceRecord.Sha256) {
            $modifiedPaths.Add([pscustomobject]@{
                Path = $relativePath
                ArchiveLength = $archiveRecord.Length
                SourceLength = $sourceRecord.Length
                ArchiveSha256 = $archiveRecord.Sha256
                SourceSha256 = $sourceRecord.Sha256
            })
        }
    }

    foreach ($relativePath in $sourceEntries.Keys) {
        if (-not $archiveEntries.ContainsKey($relativePath)) {
            $extraPaths.Add($relativePath)
        }
    }

    $archiveManifestSha256 = Get-CanonicalManifestSha256 $archiveEntries
    $sourceManifestSha256 = Get-CanonicalManifestSha256 $sourceEntries
    if ($archiveManifestSha256 -ne $sourceManifestSha256) {
        $issues.Add("Canonical content manifests differ.")
    }
}
catch {
    $issues.Add("Exception|$($_.Exception.Message)")
}
finally {
    if ($null -ne $archive) {
        $archive.Dispose()
    }
}

$status = if ($issues.Count -eq 0 -and $missingPaths.Count -eq 0 -and $extraPaths.Count -eq 0 -and $modifiedPaths.Count -eq 0) { 'Pass' } else { 'Fail' }
$report = [ordered]@{
    SchemaVersion = '1.0'
    Status = $status
    Inputs = [ordered]@{
        ArchivePath = $archivePathResolved
        SourceDirectory = $sourceDirectoryResolved
        ExpectedArchiveSha256 = $ExpectedArchiveSha256.ToLowerInvariant()
        ActualArchiveSha256 = $actualArchiveSha256
        PathComparer = 'Ordinal'
    }
    Archive = [ordered]@{
        ZipRootDirectory = $zipRootDirectory
        FileCount = if ($null -eq $archiveEntries) { 0 } else { $archiveEntries.Count }
        CanonicalContentManifestSha256 = $archiveManifestSha256
    }
    Source = [ordered]@{
        FileCount = if ($null -eq $sourceEntries) { 0 } else { $sourceEntries.Count }
        CanonicalContentManifestSha256 = $sourceManifestSha256
    }
    Comparison = [ordered]@{
        MissingFileCount = $missingPaths.Count
        ExtraFileCount = $extraPaths.Count
        ModifiedFileCount = $modifiedPaths.Count
        DifferenceSampleLimit = $DifferenceSampleLimit
        MissingPathSamples = Get-Sample $missingPaths $DifferenceSampleLimit
        ExtraPathSamples = Get-Sample $extraPaths $DifferenceSampleLimit
        ModifiedFileSamples = Get-Sample $modifiedPaths $DifferenceSampleLimit
    }
    Issues = @($issues)
    ClaimLimit = 'archive-to-build-source-snapshot-only=True|independent-vendored-upstream-revisions=False|vendored-modification-deltas=False|binary-reproducibility=False|distribution-approval=False'
}

$reportDirectory = Split-Path -Parent $reportPathResolved
New-Item -ItemType Directory -Path $reportDirectory -Force | Out-Null
$report | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $reportPathResolved -Encoding utf8

"AssimpSourceSnapshot|$status|archiveFiles=$($report.Archive.FileCount)|sourceFiles=$($report.Source.FileCount)|missing=$($report.Comparison.MissingFileCount)|extra=$($report.Comparison.ExtraFileCount)|modified=$($report.Comparison.ModifiedFileCount)|archiveSha256=$actualArchiveSha256"
"Report|$reportPathResolved"

if ($status -ne 'Pass') {
    exit 1
}
