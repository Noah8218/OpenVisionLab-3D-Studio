[CmdletBinding()]
param(
    [string]$OfficialSourceArchive = 'artifacts\dependency-candidates\assimp-poly2tri-provenance-20260716\google-code-archive\source-archive.zip',
    [string]$MercurialRepositoryDirectory = 'artifacts\dependency-candidates\assimp-poly2tri-provenance-20260716\google-code-archive\source\poly2tri',
    [string]$GitRepositoryDirectory = 'artifacts\dependency-candidates\assimp-poly2tri-provenance-20260716\greenm01-poly2tri',
    [string]$PortableMercurialRuntimeDirectory = 'artifacts\dependency-candidates\assimp-poly2tri-provenance-20260716\portable-mercurial\runtime',
    [string]$WorkingDirectory = 'artifacts\dependency-candidates\assimp-poly2tri-provenance-20260716\poly2tri-origin-verification-work',
    [string]$ExpectedOfficialSourceArchiveSha256 = '02092826bf5c539ed5a904386a2439eb608cc4d1d008adc7034ae3a2230a05bb',
    [string]$ExpectedOfficialMercurialRevision = '5de9623d6a500d8b0ad3126a48957c5152c15ad2',
    [string]$ExpectedCandidateGitRevision = '99927efa011013154460ca4cb06bcd64d4768edb',
    [ValidateRange(1, 100000)]
    [int]$ExpectedFileCount = 35,
    [string]$ReportPath = 'artifacts\dependency-candidates\assimp-poly2tri-provenance-20260716\poly2tri-origin-identity.json',
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

function Get-Sha256ForBytes([byte[]]$Bytes) {
    $sha256 = [System.Security.Cryptography.SHA256]::Create()
    try {
        return ([System.BitConverter]::ToString($sha256.ComputeHash($Bytes))).Replace('-', '').ToLowerInvariant()
    }
    finally {
        $sha256.Dispose()
    }
}

function Get-Sha256ForFile([string]$Path) {
    $stream = [System.IO.File]::OpenRead($Path)
    try {
        $sha256 = [System.Security.Cryptography.SHA256]::Create()
        try {
            return ([System.BitConverter]::ToString($sha256.ComputeHash($stream))).Replace('-', '').ToLowerInvariant()
        }
        finally {
            $sha256.Dispose()
        }
    }
    finally {
        $stream.Dispose()
    }
}

function Get-CrLfNormalizedRecord([string]$Path) {
    [byte[]]$sourceBytes = [System.IO.File]::ReadAllBytes($Path)
    $normalized = [System.IO.MemoryStream]::new($sourceBytes.Length)
    try {
        for ($index = 0; $index -lt $sourceBytes.Length; $index++) {
            if ($sourceBytes[$index] -eq 13 -and $index + 1 -lt $sourceBytes.Length -and $sourceBytes[$index + 1] -eq 10) {
                $normalized.WriteByte(10)
                $index++
            }
            else {
                $normalized.WriteByte($sourceBytes[$index])
            }
        }

        $normalizedBytes = $normalized.ToArray()
        return [pscustomobject]@{
            Length = [int64]$normalizedBytes.Length
            Sha256 = Get-Sha256ForBytes $normalizedBytes
        }
    }
    finally {
        $normalized.Dispose()
    }
}

function Assert-SafeRelativePath([string]$RelativePath) {
    if ([string]::IsNullOrWhiteSpace($RelativePath) -or $RelativePath.Contains('\') -or [System.IO.Path]::IsPathRooted($RelativePath)) {
        throw "Unsafe relative path: $RelativePath"
    }

    $segments = @($RelativePath.Split('/'))
    if ($segments.Count -eq 0 -or ($segments | Where-Object { [string]::IsNullOrWhiteSpace($_) -or $_ -eq '.' -or $_ -eq '..' }).Count -gt 0) {
        throw "Unsafe relative path: $RelativePath"
    }
}

function Get-DirectoryEntries([string]$Root, [string[]]$IgnoredPaths) {
    $ordinal = [System.StringComparer]::Ordinal
    $entries = [System.Collections.Generic.Dictionary[string, object]]::new($ordinal)
    $rootWithSeparator = $Root.TrimEnd('\', '/') + [System.IO.Path]::DirectorySeparatorChar

    Get-ChildItem -LiteralPath $Root -Recurse -File -Force | ForEach-Object {
        $relativePath = $_.FullName.Substring($rootWithSeparator.Length).Replace('\', '/')
        Assert-SafeRelativePath $relativePath
        if ($IgnoredPaths -contains $relativePath) {
            return
        }

        if ($entries.ContainsKey($relativePath)) {
            throw "Duplicate archive file path: $relativePath"
        }

        $normalized = Get-CrLfNormalizedRecord $_.FullName
        $entries.Add($relativePath, [pscustomobject]@{
            RawLength = [int64]$_.Length
            RawSha256 = Get-Sha256ForFile $_.FullName
            CrLfNormalizedLength = $normalized.Length
            CrLfNormalizedSha256 = $normalized.Sha256
        })
    }

    return $entries
}

function Get-CanonicalManifestSha256([System.Collections.Generic.Dictionary[string, object]]$Entries, [string]$LengthProperty, [string]$Sha256Property) {
    $paths = [System.Collections.Generic.List[string]]::new()
    foreach ($path in $Entries.Keys) {
        $paths.Add($path)
    }

    $paths.Sort([System.StringComparer]::Ordinal)
    $builder = [System.Text.StringBuilder]::new()
    foreach ($path in $paths) {
        $record = $Entries[$path]
        [void]$builder.Append($path)
        [void]$builder.Append('|')
        [void]$builder.Append($record.PSObject.Properties[$LengthProperty].Value.ToString([System.Globalization.CultureInfo]::InvariantCulture))
        [void]$builder.Append('|')
        [void]$builder.Append($record.PSObject.Properties[$Sha256Property].Value)
        [void]$builder.Append("`n")
    }

    $bytes = [System.Text.UTF8Encoding]::new($false).GetBytes($builder.ToString())
    return Get-Sha256ForBytes $bytes
}

function Get-Sample([System.Collections.IEnumerable]$Items, [int]$Limit) {
    return @($Items | Select-Object -First $Limit)
}

function Invoke-ExternalCommand([string]$FilePath, [string[]]$Arguments, [string]$Description) {
    $output = @(& $FilePath @Arguments 2>&1)
    if ($LASTEXITCODE -ne 0) {
        $details = ($output | ForEach-Object { $_.ToString() }) -join "`n"
        throw "$Description failed with exit code $LASTEXITCODE. $details"
    }

    return $output
}

$issues = [System.Collections.Generic.List[string]]::new()
$missingPaths = [System.Collections.Generic.List[string]]::new()
$extraPaths = [System.Collections.Generic.List[string]]::new()
$rawMismatchPaths = [System.Collections.Generic.List[object]]::new()
$normalizedMismatchPaths = [System.Collections.Generic.List[object]]::new()
$officialEntries = $null
$candidateEntries = $null
$actualArchiveSha256 = $null
$hgVersion = $null
$officialRevisionMetadata = $null
$candidateResolvedRevision = $null
$officialArchiveMetadata = [ordered]@{}
$candidateArchiveSha256 = $null
$rawOfficialManifestSha256 = $null
$rawCandidateManifestSha256 = $null
$normalizedOfficialManifestSha256 = $null
$normalizedCandidateManifestSha256 = $null
$previousPythonPath = $env:PYTHONPATH

$officialSourceArchiveResolved = Resolve-WorkspacePath $OfficialSourceArchive
$mercurialRepositoryResolved = Resolve-WorkspacePath $MercurialRepositoryDirectory
$gitRepositoryResolved = Resolve-WorkspacePath $GitRepositoryDirectory
$portableMercurialRuntimeResolved = Resolve-WorkspacePath $PortableMercurialRuntimeDirectory
$workingDirectoryResolved = Resolve-WorkspacePath $WorkingDirectory
$reportPathResolved = Resolve-WorkspacePath $ReportPath

try {
    foreach ($value in @($ExpectedOfficialSourceArchiveSha256, $ExpectedOfficialMercurialRevision, $ExpectedCandidateGitRevision)) {
        if ($value -notmatch '^[0-9A-Fa-f]{40}$' -and $value -notmatch '^[0-9A-Fa-f]{64}$') {
            throw "Expected hash or revision has an invalid format: $value"
        }
    }

    if ($ExpectedOfficialSourceArchiveSha256 -notmatch '^[0-9A-Fa-f]{64}$') {
        throw 'ExpectedOfficialSourceArchiveSha256 must be a 64-character SHA-256 value.'
    }

    if ($ExpectedOfficialMercurialRevision -notmatch '^[0-9A-Fa-f]{40}$' -or $ExpectedCandidateGitRevision -notmatch '^[0-9A-Fa-f]{40}$') {
        throw 'Expected revision values must be full 40-character identifiers.'
    }

    if (-not (Test-Path -LiteralPath $officialSourceArchiveResolved -PathType Leaf)) {
        throw "Official source archive does not exist: $officialSourceArchiveResolved"
    }

    if (-not (Test-Path -LiteralPath $mercurialRepositoryResolved -PathType Container)) {
        throw "Mercurial repository does not exist: $mercurialRepositoryResolved"
    }

    if (-not (Test-Path -LiteralPath $gitRepositoryResolved -PathType Container)) {
        throw "Git repository does not exist: $gitRepositoryResolved"
    }

    if (-not (Test-Path -LiteralPath $portableMercurialRuntimeResolved -PathType Container)) {
        throw "Portable Mercurial runtime does not exist: $portableMercurialRuntimeResolved"
    }

    if (Test-Path -LiteralPath $workingDirectoryResolved) {
        throw "WorkingDirectory already exists. Choose a new empty artifact path: $workingDirectoryResolved"
    }

    $actualArchiveSha256 = Get-Sha256ForFile $officialSourceArchiveResolved
    if (-not $actualArchiveSha256.Equals($ExpectedOfficialSourceArchiveSha256, [System.StringComparison]::OrdinalIgnoreCase)) {
        $issues.Add('Official source archive SHA-256 does not match the expected value.')
    }

    $env:PYTHONPATH = $portableMercurialRuntimeResolved
    $hgVersion = ((Invoke-ExternalCommand 'py' @('-3.11', '-m', 'mercurial', 'version', '--quiet') 'Portable Mercurial version check') | ForEach-Object { $_.ToString() }) -join "`n"
    $officialRevisionMetadata = ((Invoke-ExternalCommand 'py' @('-3.11', '-m', 'mercurial', '-R', $mercurialRepositoryResolved, 'log', '-r', $ExpectedOfficialMercurialRevision, '--template', '{node}|{author}|{date|isodate}|{desc}') 'Official Mercurial revision lookup') | ForEach-Object { $_.ToString() }) -join "`n"

    New-Item -ItemType Directory -Path $workingDirectoryResolved -Force | Out-Null
    $officialExportDirectory = Join-Path $workingDirectoryResolved 'official-hg-export'
    $candidateExportZip = Join-Path $workingDirectoryResolved 'candidate-git-export.zip'
    $candidateExportDirectory = Join-Path $workingDirectoryResolved 'candidate-git-export'

    Invoke-ExternalCommand 'py' @('-3.11', '-m', 'mercurial', '-R', $mercurialRepositoryResolved, 'archive', '--type', 'files', '--rev', $ExpectedOfficialMercurialRevision, $officialExportDirectory) 'Official Mercurial archive export' | Out-Null

    $candidateResolvedRevision = ((Invoke-ExternalCommand 'git' @('-C', $gitRepositoryResolved, 'rev-parse', $ExpectedCandidateGitRevision) 'Candidate Git revision lookup') | Select-Object -First 1).ToString().Trim().ToLowerInvariant()
    if (-not $candidateResolvedRevision.Equals($ExpectedCandidateGitRevision, [System.StringComparison]::OrdinalIgnoreCase)) {
        $issues.Add('Candidate Git revision does not resolve to the expected full identifier.')
    }

    Invoke-ExternalCommand 'git' @('-C', $gitRepositoryResolved, '-c', 'core.autocrlf=false', 'archive', '--format=zip', "--output=$candidateExportZip", $ExpectedCandidateGitRevision) 'Candidate Git archive export' | Out-Null
    Expand-Archive -LiteralPath $candidateExportZip -DestinationPath $candidateExportDirectory -Force
    $candidateArchiveSha256 = Get-Sha256ForFile $candidateExportZip

    $archivalFile = Join-Path $officialExportDirectory '.hg_archival.txt'
    if (-not (Test-Path -LiteralPath $archivalFile -PathType Leaf)) {
        $issues.Add('Official Mercurial export does not contain .hg_archival.txt.')
    }
    else {
        foreach ($line in Get-Content -LiteralPath $archivalFile) {
            $separatorIndex = $line.IndexOf(':')
            if ($separatorIndex -gt 0) {
                $key = $line.Substring(0, $separatorIndex).Trim()
                $value = $line.Substring($separatorIndex + 1).Trim()
                $officialArchiveMetadata[$key] = $value
            }
        }

        if (-not $officialArchiveMetadata.Contains('node') -or -not $officialArchiveMetadata['node'].Equals($ExpectedOfficialMercurialRevision, [System.StringComparison]::OrdinalIgnoreCase)) {
            $issues.Add('Official Mercurial export does not identify the expected revision node.')
        }
    }

    $officialEntries = Get-DirectoryEntries $officialExportDirectory @('.hg_archival.txt')
    $candidateEntries = Get-DirectoryEntries $candidateExportDirectory @()

    if ($officialEntries.Count -ne $ExpectedFileCount) {
        $issues.Add("Official export file count is $($officialEntries.Count), expected $ExpectedFileCount.")
    }

    if ($candidateEntries.Count -ne $ExpectedFileCount) {
        $issues.Add("Candidate export file count is $($candidateEntries.Count), expected $ExpectedFileCount.")
    }

    foreach ($path in $officialEntries.Keys) {
        if (-not $candidateEntries.ContainsKey($path)) {
            $missingPaths.Add($path)
            continue
        }

        $officialRecord = $officialEntries[$path]
        $candidateRecord = $candidateEntries[$path]
        if ($officialRecord.RawLength -ne $candidateRecord.RawLength -or $officialRecord.RawSha256 -ne $candidateRecord.RawSha256) {
            $rawMismatchPaths.Add([pscustomobject]@{
                Path = $path
                OfficialLength = $officialRecord.RawLength
                CandidateLength = $candidateRecord.RawLength
                OfficialSha256 = $officialRecord.RawSha256
                CandidateSha256 = $candidateRecord.RawSha256
            })
        }

        if ($officialRecord.CrLfNormalizedLength -ne $candidateRecord.CrLfNormalizedLength -or $officialRecord.CrLfNormalizedSha256 -ne $candidateRecord.CrLfNormalizedSha256) {
            $normalizedMismatchPaths.Add([pscustomobject]@{
                Path = $path
                OfficialLength = $officialRecord.CrLfNormalizedLength
                CandidateLength = $candidateRecord.CrLfNormalizedLength
                OfficialSha256 = $officialRecord.CrLfNormalizedSha256
                CandidateSha256 = $candidateRecord.CrLfNormalizedSha256
            })
        }
    }

    foreach ($path in $candidateEntries.Keys) {
        if (-not $officialEntries.ContainsKey($path)) {
            $extraPaths.Add($path)
        }
    }

    $rawOfficialManifestSha256 = Get-CanonicalManifestSha256 $officialEntries 'RawLength' 'RawSha256'
    $rawCandidateManifestSha256 = Get-CanonicalManifestSha256 $candidateEntries 'RawLength' 'RawSha256'
    $normalizedOfficialManifestSha256 = Get-CanonicalManifestSha256 $officialEntries 'CrLfNormalizedLength' 'CrLfNormalizedSha256'
    $normalizedCandidateManifestSha256 = Get-CanonicalManifestSha256 $candidateEntries 'CrLfNormalizedLength' 'CrLfNormalizedSha256'

    if ($rawOfficialManifestSha256 -ne $rawCandidateManifestSha256) {
        $issues.Add('Raw-byte canonical manifests differ.')
    }

    if ($normalizedOfficialManifestSha256 -ne $normalizedCandidateManifestSha256) {
        $issues.Add('CRLF-to-LF normalized canonical manifests differ.')
    }
}
catch {
    $issues.Add("Exception|$($_.Exception.Message)")
}
finally {
    if ($null -eq $previousPythonPath) {
        Remove-Item Env:PYTHONPATH -ErrorAction SilentlyContinue
    }
    else {
        $env:PYTHONPATH = $previousPythonPath
    }
}

$status = if ($issues.Count -eq 0 -and $missingPaths.Count -eq 0 -and $extraPaths.Count -eq 0 -and $rawMismatchPaths.Count -eq 0 -and $normalizedMismatchPaths.Count -eq 0) { 'Pass' } else { 'Fail' }
$report = [ordered]@{
    SchemaVersion = '1.0'
    Status = $status
    Inputs = [ordered]@{
        OfficialSourceArchive = $officialSourceArchiveResolved
        ExpectedOfficialSourceArchiveSha256 = $ExpectedOfficialSourceArchiveSha256.ToLowerInvariant()
        ActualOfficialSourceArchiveSha256 = $actualArchiveSha256
        MercurialRepositoryDirectory = $mercurialRepositoryResolved
        ExpectedOfficialMercurialRevision = $ExpectedOfficialMercurialRevision.ToLowerInvariant()
        GitRepositoryDirectory = $gitRepositoryResolved
        ExpectedCandidateGitRevision = $ExpectedCandidateGitRevision.ToLowerInvariant()
        PortableMercurialRuntimeDirectory = $portableMercurialRuntimeResolved
        WorkingDirectory = $workingDirectoryResolved
        ExpectedFileCount = $ExpectedFileCount
        PathComparer = 'Ordinal'
    }
    Tooling = [ordered]@{
        PortableMercurialVersion = $hgVersion
        OfficialRevisionMetadata = $officialRevisionMetadata
        CandidateResolvedGitRevision = $candidateResolvedRevision
    }
    ArchiveExports = [ordered]@{
        OfficialHgArchivalMetadata = $officialArchiveMetadata
        CandidateGitArchiveSha256 = $candidateArchiveSha256
        OfficialFileCount = if ($null -eq $officialEntries) { 0 } else { $officialEntries.Count }
        CandidateFileCount = if ($null -eq $candidateEntries) { 0 } else { $candidateEntries.Count }
        RawCanonicalManifestSha256 = [ordered]@{
            Official = $rawOfficialManifestSha256
            Candidate = $rawCandidateManifestSha256
        }
        CrLfNormalizedCanonicalManifestSha256 = [ordered]@{
            Official = $normalizedOfficialManifestSha256
            Candidate = $normalizedCandidateManifestSha256
        }
    }
    Comparison = [ordered]@{
        MissingFileCount = $missingPaths.Count
        ExtraFileCount = $extraPaths.Count
        RawByteMismatchCount = $rawMismatchPaths.Count
        CrLfNormalizedMismatchCount = $normalizedMismatchPaths.Count
        DifferenceSampleLimit = $DifferenceSampleLimit
        MissingPathSamples = Get-Sample $missingPaths $DifferenceSampleLimit
        ExtraPathSamples = Get-Sample $extraPaths $DifferenceSampleLimit
        RawByteMismatchSamples = Get-Sample $rawMismatchPaths $DifferenceSampleLimit
        CrLfNormalizedMismatchSamples = Get-Sample $normalizedMismatchPaths $DifferenceSampleLimit
    }
    Issues = @($issues)
    ClaimLimit = 'official-hg-revision-to-candidate-git-content-equivalence=True|raw-byte-archive-export-identity=True|candidate-git-mirror-signature-or-ownership=False|assimp-current-modification-attribution=False|third-party-notice-disposition=False|binary-reproducibility=False|distribution-approval=False'
}

$reportDirectory = Split-Path -Parent $reportPathResolved
New-Item -ItemType Directory -Path $reportDirectory -Force | Out-Null
$report | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $reportPathResolved -Encoding utf8

"AssimpPoly2TriOrigin|$status|officialFiles=$($report.ArchiveExports.OfficialFileCount)|candidateFiles=$($report.ArchiveExports.CandidateFileCount)|missing=$($report.Comparison.MissingFileCount)|extra=$($report.Comparison.ExtraFileCount)|raw=$($report.Comparison.RawByteMismatchCount)|normalized=$($report.Comparison.CrLfNormalizedMismatchCount)|archiveSha256=$actualArchiveSha256"
"Report|$reportPathResolved"

if ($status -ne 'Pass') {
    exit 1
}
