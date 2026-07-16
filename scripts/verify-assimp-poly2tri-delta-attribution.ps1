[CmdletBinding()]
param(
    [string]$AssimpRepositoryDirectory = 'artifacts\dependency-candidates\assimp-poly2tri-provenance-20260716\assimp-history.git',
    [string]$HistoryPagePath = 'artifacts\dependency-candidates\assimp-poly2tri-attribution-20260716\commits-page-1.json',
    [string]$HistoryHeadersPath = 'artifacts\dependency-candidates\assimp-poly2tri-attribution-20260716\commits-page-1.headers.txt',
    [string]$CommitDetailsSummaryPath = 'artifacts\dependency-candidates\assimp-poly2tri-attribution-20260716\commit-details-summary.json',
    [string]$CommitDetailsDirectory = 'artifacts\dependency-candidates\assimp-poly2tri-attribution-20260716',
    [string]$TagReferencePath = 'artifacts\dependency-candidates\assimp-poly2tri-attribution-20260716\official-check-0.json',
    [string]$CurrentCommitPath = 'artifacts\dependency-candidates\assimp-poly2tri-attribution-20260716\official-check-1.json',
    [string]$InitialTreePath = 'artifacts\dependency-candidates\assimp-poly2tri-attribution-20260716\initial-tree.json',
    [string]$CurrentTreePath = 'artifacts\dependency-candidates\assimp-poly2tri-attribution-20260716\v5.4.2-tree.json',
    [string]$ExpectedInitialRevision = '1ebd116dff21ca8e347676288bfda3056af18a8c',
    [string]$ExpectedCurrentRevision = 'ddb74c2bbdee1565dda667e85f0c82a0588c8053',
    [string]$ExpectedLatestPathCommit = '6bcdf989fb7331aab8fa3b1afe6a0740b1a4ec9b',
    [ValidateRange(1, 1000)]
    [int]$ExpectedHistoryCommitCount = 28,
    [ValidateRange(1, 1000)]
    [int]$ExpectedDirectDeltaPathCount = 14,
    [ValidateRange(0, 1000000)]
    [int]$ExpectedDirectAdditions = 1407,
    [ValidateRange(0, 1000000)]
    [int]$ExpectedDirectDeletions = 1328,
    [string]$ReportPath = 'artifacts\dependency-candidates\assimp-poly2tri-attribution-20260716\poly2tri-delta-attribution.json',
    [ValidateRange(1, 1000)]
    [int]$DifferenceSampleLimit = 20
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$poly2TriPrefix = 'contrib/poly2tri/'

function Resolve-WorkspacePath([string]$Path) {
    if ([System.IO.Path]::IsPathRooted($Path)) {
        return [System.IO.Path]::GetFullPath($Path)
    }

    return [System.IO.Path]::GetFullPath((Join-Path $repoRoot $Path))
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

function New-OrdinalDictionary() {
    return [System.Collections.Generic.Dictionary[string, object]]::new([System.StringComparer]::Ordinal)
}

function Add-TreeEntries([object]$TreeDocument, [string]$Prefix, [string]$Description) {
    if ($TreeDocument.truncated -ne $false) {
        throw "$Description Git tree response is truncated."
    }

    $entries = New-OrdinalDictionary
    foreach ($entry in @($TreeDocument.tree | Where-Object { $_.type -eq 'blob' -and $_.path.StartsWith($Prefix, [System.StringComparison]::Ordinal) })) {
        if ($entries.ContainsKey($entry.path)) {
            throw "$Description Git tree response has a duplicate path: $($entry.path)"
        }

        $entries.Add($entry.path, [pscustomobject]@{
            Sha = $entry.sha.ToLowerInvariant()
            Size = [int64]$entry.size
        })
    }

    return $entries
}

function Get-LocalGitTreeEntries([string]$RepositoryDirectory, [string]$Revision, [string]$Prefix) {
    $entries = New-OrdinalDictionary
    $lines = Invoke-ExternalCommand 'git' @('-C', $RepositoryDirectory, 'ls-tree', '-r', '--format=%(objectname)|%(objecttype)|%(path)', $Revision, '--', 'contrib/poly2tri') "Git tree lookup for $Revision"
    foreach ($line in $lines) {
        $parts = $line.ToString().Split('|', 3)
        if ($parts.Count -ne 3 -or $parts[1] -ne 'blob' -or -not $parts[2].StartsWith($Prefix, [System.StringComparison]::Ordinal)) {
            throw "Unexpected Git tree entry for ${Revision}: $line"
        }

        if ($entries.ContainsKey($parts[2])) {
            throw "Local Git tree has a duplicate path: $($parts[2])"
        }

        $entries.Add($parts[2], [pscustomobject]@{
            Sha = $parts[0].ToLowerInvariant()
            Size = [int64]0
        })
    }

    return $entries
}

function Compare-TreeEntries([System.Collections.Generic.Dictionary[string, object]]$ExpectedEntries, [System.Collections.Generic.Dictionary[string, object]]$ActualEntries, [System.Collections.Generic.List[string]]$Issues, [string]$Description) {
    foreach ($path in $ExpectedEntries.Keys) {
        if (-not $ActualEntries.ContainsKey($path)) {
            $Issues.Add("$Description is missing local path: $path")
            continue
        }

        if ($ExpectedEntries[$path].Sha -ne $ActualEntries[$path].Sha) {
            $Issues.Add("$Description blob SHA differs for path: $path")
        }
    }

    foreach ($path in $ActualEntries.Keys) {
        if (-not $ExpectedEntries.ContainsKey($path)) {
            $Issues.Add("$Description has unexpected local path: $path")
        }
    }
}

function Get-CanonicalFileChanges([object[]]$Files) {
    $records = [System.Collections.Generic.List[string]]::new()
    foreach ($file in $Files) {
        $records.Add("$($file.Path)|$($file.Status)|$($file.Additions)|$($file.Deletions)|$($file.Changes)|$($file.BlobSha)")
    }

    $records.Sort([System.StringComparer]::Ordinal)
    return $records.ToArray()
}

$issues = [System.Collections.Generic.List[string]]::new()
$uncoveredPaths = [System.Collections.Generic.List[string]]::new()
$officialTreeDeltaPaths = [System.Collections.Generic.List[string]]::new()
$localDeltaPaths = [System.Collections.Generic.List[string]]::new()
$attributionRows = [System.Collections.Generic.List[object]]::new()
$historyPage = $null
$historySummary = $null
$initialOfficialTree = $null
$currentOfficialTree = $null
$initialOfficialEntries = $null
$currentOfficialEntries = $null
$initialLocalEntries = $null
$currentLocalEntries = $null
$directAdditions = 0
$directDeletions = 0
$historyDetailValidationCount = 0

$assimpRepositoryResolved = Resolve-WorkspacePath $AssimpRepositoryDirectory
$historyPageResolved = Resolve-WorkspacePath $HistoryPagePath
$historyHeadersResolved = Resolve-WorkspacePath $HistoryHeadersPath
$commitDetailsSummaryResolved = Resolve-WorkspacePath $CommitDetailsSummaryPath
$commitDetailsDirectoryResolved = Resolve-WorkspacePath $CommitDetailsDirectory
$tagReferenceResolved = Resolve-WorkspacePath $TagReferencePath
$currentCommitResolved = Resolve-WorkspacePath $CurrentCommitPath
$initialTreeResolved = Resolve-WorkspacePath $InitialTreePath
$currentTreeResolved = Resolve-WorkspacePath $CurrentTreePath
$reportPathResolved = Resolve-WorkspacePath $ReportPath

try {
    foreach ($revision in @($ExpectedInitialRevision, $ExpectedCurrentRevision, $ExpectedLatestPathCommit)) {
        if ($revision -notmatch '^[0-9A-Fa-f]{40}$') {
            throw "Expected revision must be a full 40-character identifier: $revision"
        }
    }

    foreach ($path in @($assimpRepositoryResolved, $commitDetailsDirectoryResolved)) {
        if (-not (Test-Path -LiteralPath $path -PathType Container)) {
            throw "Required directory does not exist: $path"
        }
    }

    foreach ($path in @($historyPageResolved, $historyHeadersResolved, $commitDetailsSummaryResolved, $tagReferenceResolved, $currentCommitResolved, $initialTreeResolved, $currentTreeResolved)) {
        if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
            throw "Required evidence file does not exist: $path"
        }
    }

    $tagReference = Get-Content -Raw -LiteralPath $tagReferenceResolved | ConvertFrom-Json
    if ($tagReference.ref -ne 'refs/tags/v5.4.2' -or $tagReference.object.type -ne 'commit' -or -not $tagReference.object.sha.Equals($ExpectedCurrentRevision, [System.StringComparison]::OrdinalIgnoreCase)) {
        $issues.Add('Official v5.4.2 tag reference does not resolve to the expected current commit.')
    }

    $currentCommit = Get-Content -Raw -LiteralPath $currentCommitResolved | ConvertFrom-Json
    if (-not $currentCommit.sha.Equals($ExpectedCurrentRevision, [System.StringComparison]::OrdinalIgnoreCase)) {
        $issues.Add('Official current commit evidence does not have the expected commit SHA.')
    }

    $initialCommitPath = Join-Path $commitDetailsDirectoryResolved "$ExpectedInitialRevision.json"
    if (-not (Test-Path -LiteralPath $initialCommitPath -PathType Leaf)) {
        throw "Initial commit detail evidence does not exist: $initialCommitPath"
    }

    $initialCommit = Get-Content -Raw -LiteralPath $initialCommitPath | ConvertFrom-Json
    if (-not $initialCommit.sha.Equals($ExpectedInitialRevision, [System.StringComparison]::OrdinalIgnoreCase)) {
        $issues.Add('Official initial commit evidence does not have the expected commit SHA.')
    }

    $initialOfficialTree = Get-Content -Raw -LiteralPath $initialTreeResolved | ConvertFrom-Json
    $currentOfficialTree = Get-Content -Raw -LiteralPath $currentTreeResolved | ConvertFrom-Json
    if ($initialOfficialTree.sha -ne $initialCommit.commit.tree.sha) {
        $issues.Add('Official initial tree SHA does not match the initial commit tree SHA.')
    }

    if ($currentOfficialTree.sha -ne $currentCommit.commit.tree.sha) {
        $issues.Add('Official current tree SHA does not match the v5.4.2 commit tree SHA.')
    }

    $initialOfficialEntries = Add-TreeEntries $initialOfficialTree $poly2TriPrefix 'Initial official'
    $currentOfficialEntries = Add-TreeEntries $currentOfficialTree $poly2TriPrefix 'Current official'
    $initialLocalEntries = Get-LocalGitTreeEntries $assimpRepositoryResolved $ExpectedInitialRevision $poly2TriPrefix
    $currentLocalEntries = Get-LocalGitTreeEntries $assimpRepositoryResolved $ExpectedCurrentRevision $poly2TriPrefix
    Compare-TreeEntries $initialOfficialEntries $initialLocalEntries $issues 'Initial official-to-local tree'
    Compare-TreeEntries $currentOfficialEntries $currentLocalEntries $issues 'Current official-to-local tree'

    foreach ($path in $currentOfficialEntries.Keys) {
        if (-not $initialOfficialEntries.ContainsKey($path) -or $currentOfficialEntries[$path].Sha -ne $initialOfficialEntries[$path].Sha) {
            $officialTreeDeltaPaths.Add($path)
        }
    }

    foreach ($path in $initialOfficialEntries.Keys) {
        if (-not $currentOfficialEntries.ContainsKey($path)) {
            $issues.Add("Official current tree removed initial Poly2Tri path: $path")
        }
    }

    $nameStatusLines = Invoke-ExternalCommand 'git' @('-C', $assimpRepositoryResolved, 'diff', '--no-renames', '--name-status', $ExpectedInitialRevision, $ExpectedCurrentRevision, '--', 'contrib/poly2tri') 'Local direct Poly2Tri name-status diff'
    foreach ($line in $nameStatusLines) {
        $parts = $line.ToString().Split("`t", 2)
        if ($parts.Count -ne 2 -or $parts[0] -ne 'M' -or -not $parts[1].StartsWith($poly2TriPrefix, [System.StringComparison]::Ordinal)) {
            throw "Unexpected direct Poly2Tri name-status entry: $line"
        }

        $localDeltaPaths.Add($parts[1])
    }

    $numStatLines = Invoke-ExternalCommand 'git' @('-C', $assimpRepositoryResolved, 'diff', '--no-renames', '--numstat', $ExpectedInitialRevision, $ExpectedCurrentRevision, '--', 'contrib/poly2tri') 'Local direct Poly2Tri numstat diff'
    foreach ($line in $numStatLines) {
        $parts = $line.ToString().Split("`t", 3)
        if ($parts.Count -ne 3 -or $parts[0] -notmatch '^\d+$' -or $parts[1] -notmatch '^\d+$' -or -not $parts[2].StartsWith($poly2TriPrefix, [System.StringComparison]::Ordinal)) {
            throw "Unexpected direct Poly2Tri numstat entry: $line"
        }

        $directAdditions += [int]$parts[0]
        $directDeletions += [int]$parts[1]
    }

    $officialTreeDeltaPaths.Sort([System.StringComparer]::Ordinal)
    $localDeltaPaths.Sort([System.StringComparer]::Ordinal)
    if ($officialTreeDeltaPaths.Count -ne $ExpectedDirectDeltaPathCount -or $localDeltaPaths.Count -ne $ExpectedDirectDeltaPathCount) {
        $issues.Add("Direct Poly2Tri delta path count differs from expected $ExpectedDirectDeltaPathCount.")
    }

    if (($officialTreeDeltaPaths -join "`n") -ne ($localDeltaPaths -join "`n")) {
        $issues.Add('Official tree blob delta paths differ from the local direct diff paths.')
    }

    if ($directAdditions -ne $ExpectedDirectAdditions -or $directDeletions -ne $ExpectedDirectDeletions) {
        $issues.Add("Direct Poly2Tri line delta is $directAdditions additions / $directDeletions deletions, expected $ExpectedDirectAdditions / $ExpectedDirectDeletions.")
    }

    $historyPage = Get-Content -Raw -LiteralPath $historyPageResolved | ConvertFrom-Json
    $historySummary = Get-Content -Raw -LiteralPath $commitDetailsSummaryResolved | ConvertFrom-Json
    $historyShas = @($historyPage | ForEach-Object { $_.sha.ToLowerInvariant() })
    $summaryShas = @($historySummary | ForEach-Object { $_.Sha.ToLowerInvariant() })
    if ($historyShas.Count -ne $ExpectedHistoryCommitCount -or $summaryShas.Count -ne $ExpectedHistoryCommitCount) {
        $issues.Add("Official history count differs from expected $ExpectedHistoryCommitCount.")
    }

    if (($historyShas -join "`n") -ne ($summaryShas -join "`n")) {
        $issues.Add('Official history page and commit-detail summary SHA order differ.')
    }

    if ($historyShas.Count -eq 0 -or $historyShas[0] -ne $ExpectedLatestPathCommit.ToLowerInvariant() -or $historyShas[$historyShas.Count - 1] -ne $ExpectedInitialRevision.ToLowerInvariant()) {
        $issues.Add('Official history page does not begin/end at the expected path-history commits.')
    }

    $linkHeaders = @(Get-Content -LiteralPath $historyHeadersResolved | Where-Object { $_ -match '^Link:' })
    if ($linkHeaders.Count -ne 0) {
        $issues.Add('Official history response is paginated; this verifier requires the complete one-page history evidence.')
    }

    foreach ($summaryRecord in $historySummary) {
        $detailPath = Join-Path $commitDetailsDirectoryResolved "$($summaryRecord.Sha).json"
        if (-not (Test-Path -LiteralPath $detailPath -PathType Leaf)) {
            $issues.Add("Official commit detail file is missing: $detailPath")
            continue
        }

        $actualDetailSha256 = Get-Sha256ForFile $detailPath
        if (-not $actualDetailSha256.Equals($summaryRecord.ResponseSha256, [System.StringComparison]::OrdinalIgnoreCase)) {
            $issues.Add("Official commit detail hash differs from the summary for $($summaryRecord.Sha).")
            continue
        }

        $detail = Get-Content -Raw -LiteralPath $detailPath | ConvertFrom-Json
        if (-not $detail.sha.Equals($summaryRecord.Sha, [System.StringComparison]::OrdinalIgnoreCase)) {
            $issues.Add("Official commit detail SHA differs from the summary for $($summaryRecord.Sha).")
            continue
        }

        $rawFiles = @($detail.files | Where-Object { $_.filename.StartsWith($poly2TriPrefix, [System.StringComparison]::Ordinal) } | ForEach-Object {
            [pscustomobject]@{
                Path = $_.filename
                Status = $_.status
                Additions = $_.additions
                Deletions = $_.deletions
                Changes = $_.changes
                BlobSha = $_.sha
            }
        })
        $summaryFiles = @($summaryRecord.Files)
        $rawCanonicalFiles = (Get-CanonicalFileChanges $rawFiles) -join "`n"
        $summaryCanonicalFiles = (Get-CanonicalFileChanges $summaryFiles) -join "`n"
        if ($rawCanonicalFiles -ne $summaryCanonicalFiles) {
            $issues.Add("Official commit file details differ from the summary for $($summaryRecord.Sha).")
            continue
        }

        $historyDetailValidationCount++
    }

    $postInitialHistory = @($historySummary | Where-Object { -not $_.Sha.Equals($ExpectedInitialRevision, [System.StringComparison]::OrdinalIgnoreCase) } | Sort-Object AuthorDate, Sha)
    foreach ($path in $localDeltaPaths) {
        $changes = @($postInitialHistory | Where-Object { @($_.Files | Where-Object { $_.Path -eq $path }).Count -gt 0 })
        if ($changes.Count -eq 0) {
            $uncoveredPaths.Add($path)
        }

        $attributionRows.Add([pscustomobject]@{
            Path = $path
            CommitCount = $changes.Count
            CommitShas = @($changes | ForEach-Object { $_.Sha })
        })
    }
}
catch {
    $issues.Add("Exception|$($_.Exception.Message)")
}

$status = if ($issues.Count -eq 0 -and $uncoveredPaths.Count -eq 0) { 'Pass' } else { 'Fail' }
$report = [ordered]@{
    SchemaVersion = '1.0'
    Status = $status
    Inputs = [ordered]@{
        AssimpRepositoryDirectory = $assimpRepositoryResolved
        HistoryPagePath = $historyPageResolved
        HistoryPageSha256 = if (Test-Path -LiteralPath $historyPageResolved) { Get-Sha256ForFile $historyPageResolved } else { $null }
        HistoryHeadersPath = $historyHeadersResolved
        CommitDetailsSummaryPath = $commitDetailsSummaryResolved
        CommitDetailsSummarySha256 = if (Test-Path -LiteralPath $commitDetailsSummaryResolved) { Get-Sha256ForFile $commitDetailsSummaryResolved } else { $null }
        TagReferencePath = $tagReferenceResolved
        CurrentCommitPath = $currentCommitResolved
        InitialTreePath = $initialTreeResolved
        CurrentTreePath = $currentTreeResolved
        ExpectedInitialRevision = $ExpectedInitialRevision.ToLowerInvariant()
        ExpectedCurrentRevision = $ExpectedCurrentRevision.ToLowerInvariant()
        ExpectedLatestPathCommit = $ExpectedLatestPathCommit.ToLowerInvariant()
    }
    OfficialHistory = [ordered]@{
        CommitCount = if ($null -eq $historyPage) { 0 } else { @($historyPage).Count }
        LatestPathCommit = if ($null -eq $historyPage -or @($historyPage).Count -eq 0) { $null } else { $historyPage[0].sha }
        InitialPathCommit = if ($null -eq $historyPage -or @($historyPage).Count -eq 0) { $null } else { $historyPage[@($historyPage).Count - 1].sha }
        DetailFilesValidated = $historyDetailValidationCount
        PaginationLinkHeaderCount = if (Test-Path -LiteralPath $historyHeadersResolved) { @(Get-Content -LiteralPath $historyHeadersResolved | Where-Object { $_ -match '^Link:' }).Count } else { $null }
    }
    TreeCrossCheck = [ordered]@{
        InitialOfficialBlobCount = if ($null -eq $initialOfficialEntries) { 0 } else { $initialOfficialEntries.Count }
        CurrentOfficialBlobCount = if ($null -eq $currentOfficialEntries) { 0 } else { $currentOfficialEntries.Count }
        DirectChangedBlobPathCount = $officialTreeDeltaPaths.Count
        DirectChangedBlobPaths = @($officialTreeDeltaPaths)
        LocalInitialBlobCount = if ($null -eq $initialLocalEntries) { 0 } else { $initialLocalEntries.Count }
        LocalCurrentBlobCount = if ($null -eq $currentLocalEntries) { 0 } else { $currentLocalEntries.Count }
    }
    DirectDelta = [ordered]@{
        PathCount = $localDeltaPaths.Count
        Additions = $directAdditions
        Deletions = $directDeletions
        ExpectedPathCount = $ExpectedDirectDeltaPathCount
        ExpectedAdditions = $ExpectedDirectAdditions
        ExpectedDeletions = $ExpectedDirectDeletions
    }
    PathHistoryCoverage = [ordered]@{
        PostInitialCommitCount = if ($null -eq $historySummary) { 0 } else { @($historySummary | Where-Object { -not $_.Sha.Equals($ExpectedInitialRevision, [System.StringComparison]::OrdinalIgnoreCase) }).Count }
        CoveredPathCount = $attributionRows.Count - $uncoveredPaths.Count
        UncoveredPathCount = $uncoveredPaths.Count
        DifferenceSampleLimit = $DifferenceSampleLimit
        UncoveredPathSamples = Get-Sample $uncoveredPaths $DifferenceSampleLimit
        PathAttributionSamples = Get-Sample $attributionRows $DifferenceSampleLimit
    }
    Issues = @($issues)
    ClaimLimit = 'official-path-history-and-tree-coverage=True|final-delta-path-to-ordered-commit-sets=True|net-line-to-single-commit-attribution=False|git-mirror-ownership-or-signature=False|other-vendored-component-provenance=False|third-party-notice-disposition=False|binary-reproducibility=False|distribution-approval=False'
}

$reportDirectory = Split-Path -Parent $reportPathResolved
New-Item -ItemType Directory -Path $reportDirectory -Force | Out-Null
$report | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $reportPathResolved -Encoding utf8

"AssimpPoly2TriDeltaAttribution|$status|history=$($report.OfficialHistory.CommitCount)|validatedDetails=$($report.OfficialHistory.DetailFilesValidated)|paths=$($report.DirectDelta.PathCount)|additions=$($report.DirectDelta.Additions)|deletions=$($report.DirectDelta.Deletions)|uncovered=$($report.PathHistoryCoverage.UncoveredPathCount)"
"Report|$reportPathResolved"

if ($status -ne 'Pass') {
    exit 1
}
