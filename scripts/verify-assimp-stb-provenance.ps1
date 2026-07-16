[CmdletBinding()]
param(
    [string]$UpstreamSourcePath = 'artifacts\dependency-candidates\assimp-stb-provenance-20260716\upstream-0bc88af4de5fb022db643c2d8e549a0927749354-stb_image.h',
    [string]$AssimpImportSourcePath = 'artifacts\dependency-candidates\assimp-stb-provenance-20260716\assimp-3ff7851-stb_image.h',
    [string]$AssimpTagSourcePath = 'artifacts\dependency-candidates\assimp-stb-provenance-20260716\assimp-v5.4.2-stb_image.h',
    [string]$BuildSourcePath = 'artifacts\o3d-clean\b\assimp\src\ext_assimp\contrib\stb\stb_image.h',
    [string]$AssimpCodeDirectory = 'artifacts\o3d-clean\b\assimp\src\ext_assimp\code',
    [string]$UpstreamCommitPath = 'artifacts\dependency-candidates\assimp-stb-provenance-20260716\stb-0bc88af4de5fb022db643c2d8e549a0927749354.json',
    [string]$UpstreamTagsPath = 'artifacts\dependency-candidates\assimp-stb-provenance-20260716\stb-tags-page-1.json',
    [string]$UpstreamTagsHeadersPath = 'artifacts\dependency-candidates\assimp-stb-provenance-20260716\stb-tags-page-1.headers.txt',
    [string]$UpstreamReleasesPath = 'artifacts\dependency-candidates\assimp-stb-provenance-20260716\stb-releases-page-1.json',
    [string]$UpstreamReleasesHeadersPath = 'artifacts\dependency-candidates\assimp-stb-provenance-20260716\stb-releases-page-1.headers.txt',
    [string]$AssimpTagReferencePath = 'artifacts\dependency-candidates\assimp-stb-provenance-20260716\assimp-v5.4.2-tag-ref.json',
    [string]$AssimpTagCommitPath = 'artifacts\dependency-candidates\assimp-stb-provenance-20260716\assimp-v5.4.2-commit.json',
    [string]$AssimpHistoryPath = 'artifacts\dependency-candidates\assimp-stb-provenance-20260716\assimp-stb-history-page-1.json',
    [string]$AssimpHistoryHeadersPath = 'artifacts\dependency-candidates\assimp-stb-provenance-20260716\assimp-stb-history-page-1.headers.txt',
    [string]$AssimpImportCommitPath = 'artifacts\dependency-candidates\assimp-stb-provenance-20260716\assimp-3ff7851ff9ad3004bb934fedaf657ffad0572573.json',
    [string]$ExpectedSourceSha256 = 'c54b15a689e6a1f32c75e2ec23afa442e3e0e37e894b73c1974d08679b20dd5c',
    [string]$ExpectedBlobSha = 'a632d543510ebf4410f124369b07a303e1d096d6',
    [string]$ExpectedUpstreamCommit = '0bc88af4de5fb022db643c2d8e549a0927749354',
    [string]$ExpectedAssimpImportCommit = '3ff7851ff9ad3004bb934fedaf657ffad0572573',
    [string]$ExpectedAssimpTagCommit = 'ddb74c2bbdee1565dda667e85f0c82a0588c8053',
    [string]$ExpectedInitialAssimpPathCommit = '1b37b74f9e95844a4603623335378c5e87bb23e7',
    [ValidateRange(1, 100)]
    [int]$ExpectedAssimpHistoryCommitCount = 4,
    [string]$ReportPath = 'artifacts\dependency-candidates\assimp-stb-provenance-20260716\stb-provenance-report.json'
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot

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

function Get-GitBlobSha([string]$Path) {
    $output = @(& git hash-object --no-filters $Path 2>&1)
    if ($LASTEXITCODE -ne 0) {
        throw "git hash-object failed for ${Path} with exit code ${LASTEXITCODE}: $(($output | ForEach-Object { $_.ToString() }) -join "`n")"
    }

    $value = (($output | ForEach-Object { $_.ToString() } | Where-Object { -not [string]::IsNullOrWhiteSpace($_) }) -join '').Trim().ToLowerInvariant()
    if ($value -notmatch '^[0-9a-f]{40}$') {
        throw "git hash-object did not return a full blob SHA for ${Path}: $value"
    }

    return $value
}

function Get-RequiredFileRecord([object]$CommitDetail, [string]$ExpectedPath, [string]$Description) {
    $records = @($CommitDetail.files | Where-Object { $_.filename -eq $ExpectedPath })
    if ($records.Count -ne 1) {
        throw "$Description must contain exactly one $ExpectedPath file record, found $($records.Count)."
    }

    return $records[0]
}

function Test-OrdinalContains([string]$Text, [string]$Token) {
    return $Text.IndexOf($Token, [System.StringComparison]::Ordinal) -ge 0
}

$issues = [System.Collections.Generic.List[string]]::new()
$upstreamCommit = $null
$assimpImportCommit = $null
$assimpTagReference = $null
$assimpTagCommit = $null
$assimpHistory = $null
$upstreamRecord = $null
$assimpRecord = $null
$sourceRecords = [ordered]@{}

$upstreamSourceResolved = Resolve-WorkspacePath $UpstreamSourcePath
$assimpImportSourceResolved = Resolve-WorkspacePath $AssimpImportSourcePath
$assimpTagSourceResolved = Resolve-WorkspacePath $AssimpTagSourcePath
$buildSourceResolved = Resolve-WorkspacePath $BuildSourcePath
$assimpCodeResolved = Resolve-WorkspacePath $AssimpCodeDirectory
$upstreamCommitResolved = Resolve-WorkspacePath $UpstreamCommitPath
$upstreamTagsResolved = Resolve-WorkspacePath $UpstreamTagsPath
$upstreamTagsHeadersResolved = Resolve-WorkspacePath $UpstreamTagsHeadersPath
$upstreamReleasesResolved = Resolve-WorkspacePath $UpstreamReleasesPath
$upstreamReleasesHeadersResolved = Resolve-WorkspacePath $UpstreamReleasesHeadersPath
$assimpTagReferenceResolved = Resolve-WorkspacePath $AssimpTagReferencePath
$assimpTagCommitResolved = Resolve-WorkspacePath $AssimpTagCommitPath
$assimpHistoryResolved = Resolve-WorkspacePath $AssimpHistoryPath
$assimpHistoryHeadersResolved = Resolve-WorkspacePath $AssimpHistoryHeadersPath
$assimpImportCommitResolved = Resolve-WorkspacePath $AssimpImportCommitPath
$reportPathResolved = Resolve-WorkspacePath $ReportPath

try {
    if ($ExpectedSourceSha256 -notmatch '^[0-9A-Fa-f]{64}$') {
        throw "Expected source SHA-256 is invalid: $ExpectedSourceSha256"
    }
    foreach ($revision in @($ExpectedBlobSha, $ExpectedUpstreamCommit, $ExpectedAssimpImportCommit, $ExpectedAssimpTagCommit, $ExpectedInitialAssimpPathCommit)) {
        if ($revision -notmatch '^[0-9A-Fa-f]{40}$') {
            throw "Expected revision or blob SHA is invalid: $revision"
        }
    }

    foreach ($path in @($upstreamSourceResolved, $assimpImportSourceResolved, $assimpTagSourceResolved, $buildSourceResolved, $upstreamCommitResolved, $upstreamTagsResolved, $upstreamTagsHeadersResolved, $upstreamReleasesResolved, $upstreamReleasesHeadersResolved, $assimpTagReferenceResolved, $assimpTagCommitResolved, $assimpHistoryResolved, $assimpHistoryHeadersResolved, $assimpImportCommitResolved)) {
        if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
            throw "Required evidence file does not exist: $path"
        }
    }
    if (-not (Test-Path -LiteralPath $assimpCodeResolved -PathType Container)) {
        throw "Assimp code directory does not exist: $assimpCodeResolved"
    }

    foreach ($entry in @(
        [pscustomobject]@{ Name = 'UpstreamCommit'; Path = $upstreamSourceResolved },
        [pscustomobject]@{ Name = 'AssimpImportCommit'; Path = $assimpImportSourceResolved },
        [pscustomobject]@{ Name = 'AssimpV5_4_2Tag'; Path = $assimpTagSourceResolved },
        [pscustomobject]@{ Name = 'BuildSource'; Path = $buildSourceResolved }
    )) {
        $sourceRecords[$entry.Name] = [ordered]@{
            Path = $entry.Path
            Sha256 = Get-Sha256ForFile $entry.Path
            GitBlobSha = Get-GitBlobSha $entry.Path
        }
        if ($sourceRecords[$entry.Name].Sha256 -ne $ExpectedSourceSha256.ToLowerInvariant()) {
            $issues.Add("$($entry.Name) source SHA-256 does not match the expected fixed source.")
        }
        if ($sourceRecords[$entry.Name].GitBlobSha -ne $ExpectedBlobSha.ToLowerInvariant()) {
            $issues.Add("$($entry.Name) source Git blob does not match the expected fixed source.")
        }
    }

    $upstreamCommit = Get-Content -Raw -LiteralPath $upstreamCommitResolved | ConvertFrom-Json
    $assimpImportCommit = Get-Content -Raw -LiteralPath $assimpImportCommitResolved | ConvertFrom-Json
    $assimpTagReference = Get-Content -Raw -LiteralPath $assimpTagReferenceResolved | ConvertFrom-Json
    $assimpTagCommit = Get-Content -Raw -LiteralPath $assimpTagCommitResolved | ConvertFrom-Json
    $assimpHistory = Get-Content -Raw -LiteralPath $assimpHistoryResolved | ConvertFrom-Json

    if (-not $upstreamCommit.sha.Equals($ExpectedUpstreamCommit, [System.StringComparison]::OrdinalIgnoreCase)) {
        $issues.Add('Upstream stb commit response does not contain the expected commit SHA.')
    }
    if (-not $assimpImportCommit.sha.Equals($ExpectedAssimpImportCommit, [System.StringComparison]::OrdinalIgnoreCase)) {
        $issues.Add('Assimp stb import commit response does not contain the expected commit SHA.')
    }
    if ($assimpTagReference.ref -ne 'refs/tags/v5.4.2' -or $assimpTagReference.object.type -ne 'commit' -or -not $assimpTagReference.object.sha.Equals($ExpectedAssimpTagCommit, [System.StringComparison]::OrdinalIgnoreCase)) {
        $issues.Add('Assimp v5.4.2 tag reference does not resolve to the expected commit.')
    }
    if (-not $assimpTagCommit.sha.Equals($ExpectedAssimpTagCommit, [System.StringComparison]::OrdinalIgnoreCase)) {
        $issues.Add('Assimp v5.4.2 commit response does not contain the expected tag target.')
    }

    $upstreamRecord = Get-RequiredFileRecord $upstreamCommit 'stb_image.h' 'Upstream stb commit'
    $assimpRecord = Get-RequiredFileRecord $assimpImportCommit 'contrib/stb/stb_image.h' 'Assimp stb import commit'
    if ($upstreamRecord.status -ne 'modified' -or $upstreamRecord.additions -ne 2 -or $upstreamRecord.deletions -ne 1 -or -not $upstreamRecord.sha.Equals($ExpectedBlobSha, [System.StringComparison]::OrdinalIgnoreCase)) {
        $issues.Add('Upstream fixed commit file record does not match the expected v2.29 source blob contract.')
    }
    if ($assimpRecord.status -ne 'modified' -or $assimpRecord.additions -ne 173 -or $assimpRecord.deletions -ne 175 -or -not $assimpRecord.sha.Equals($ExpectedBlobSha, [System.StringComparison]::OrdinalIgnoreCase)) {
        $issues.Add('Assimp fixed import file record does not match the expected upstream source blob contract.')
    }

    $historyShas = @($assimpHistory | ForEach-Object { $_.sha.ToLowerInvariant() })
    if ($historyShas.Count -ne $ExpectedAssimpHistoryCommitCount) {
        $issues.Add("Assimp stb path-history count is $($historyShas.Count), expected $ExpectedAssimpHistoryCommitCount.")
    }
    if ($historyShas.Count -eq 0 -or $historyShas[0] -ne $ExpectedAssimpImportCommit.ToLowerInvariant() -or $historyShas[$historyShas.Count - 1] -ne $ExpectedInitialAssimpPathCommit.ToLowerInvariant()) {
        $issues.Add('Assimp stb path history does not begin/end at the expected commits.')
    }
    if (@(Get-Content -LiteralPath $assimpHistoryHeadersResolved | Where-Object { $_ -match '^Link:' }).Count -ne 0) {
        $issues.Add('Assimp stb path-history response is paginated; captured evidence is incomplete.')
    }

    foreach ($endpoint in @(
        [pscustomobject]@{ Name = 'upstream tags'; BodyPath = $upstreamTagsResolved; HeaderPath = $upstreamTagsHeadersResolved },
        [pscustomobject]@{ Name = 'upstream releases'; BodyPath = $upstreamReleasesResolved; HeaderPath = $upstreamReleasesHeadersResolved }
    )) {
        if ((Get-Content -Raw -LiteralPath $endpoint.BodyPath).Trim() -ne '[]') {
            $issues.Add("Captured $($endpoint.Name) endpoint is not the expected empty array.")
        }
        if (@(Get-Content -LiteralPath $endpoint.HeaderPath | Where-Object { $_ -match '^Link:' }).Count -ne 0) {
            $issues.Add("Captured $($endpoint.Name) endpoint is paginated; absence evidence is incomplete.")
        }
    }

    $cmakePath = Join-Path $assimpCodeResolved 'CMakeLists.txt'
    $implementationPath = Join-Path $assimpCodeResolved 'Common\Assimp.cpp'
    $wrapperPath = Join-Path $assimpCodeResolved 'Common\StbCommon.h'
    foreach ($path in @($cmakePath, $implementationPath, $wrapperPath)) {
        if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
            throw "Assimp stb compiler-use evidence does not exist: $path"
        }
    }

    $cmakeText = Get-Content -Raw -LiteralPath $cmakePath
    $implementationText = Get-Content -Raw -LiteralPath $implementationPath
    $wrapperText = Get-Content -Raw -LiteralPath $wrapperPath
    $sourceText = Get-Content -Raw -LiteralPath $buildSourceResolved
    if (-not (Test-OrdinalContains $cmakeText '../contrib/stb/stb_image.h')) {
        $issues.Add('Assimp CMake source list does not include contrib/stb/stb_image.h.')
    }
    if (-not (Test-OrdinalContains $implementationText 'define STB_IMAGE_IMPLEMENTATION')) {
        $issues.Add('Assimp implementation source does not instantiate stb_image.')
    }
    if (-not (Test-OrdinalContains $wrapperText '#include "stb/stb_image.h"')) {
        $issues.Add('Assimp stb wrapper does not include the fixed source header.')
    }
    if (-not (Test-OrdinalContains $sourceText 'stb_image - v2.29') -or -not (Test-OrdinalContains $sourceText 'public domain image loader')) {
        $issues.Add('Fixed source header does not identify stb_image v2.29 and its public-domain notice.')
    }
}
catch {
    $issues.Add("Exception|$($_.Exception.Message)")
}

$status = if ($issues.Count -eq 0) { 'Pass' } else { 'Fail' }
$report = [ordered]@{
    SchemaVersion = '1.0'
    Status = $status
    Inputs = [ordered]@{
        UpstreamSourcePath = $upstreamSourceResolved
        AssimpImportSourcePath = $assimpImportSourceResolved
        AssimpTagSourcePath = $assimpTagSourceResolved
        BuildSourcePath = $buildSourceResolved
        AssimpCodeDirectory = $assimpCodeResolved
        UpstreamCommitPath = $upstreamCommitResolved
        AssimpImportCommitPath = $assimpImportCommitResolved
        AssimpHistoryPath = $assimpHistoryResolved
    }
    Upstream = [ordered]@{
        Commit = if ($null -eq $upstreamCommit) { $null } else { $upstreamCommit.sha }
        Tree = if ($null -eq $upstreamCommit) { $null } else { $upstreamCommit.commit.tree.sha }
        CommitMessage = if ($null -eq $upstreamCommit) { $null } else { (($upstreamCommit.commit.message -split "`r?`n")[0]) }
        TagsEndpoint = [ordered]@{
            CapturedItemCount = if ((Test-Path -LiteralPath $upstreamTagsResolved -PathType Leaf) -and (Get-Content -Raw -LiteralPath $upstreamTagsResolved).Trim() -eq '[]') { 0 } else { $null }
            PaginationLinkHeaderCount = if (Test-Path -LiteralPath $upstreamTagsHeadersResolved -PathType Leaf) { @(Get-Content -LiteralPath $upstreamTagsHeadersResolved | Where-Object { $_ -match '^Link:' }).Count } else { $null }
        }
        ReleasesEndpoint = [ordered]@{
            CapturedItemCount = if ((Test-Path -LiteralPath $upstreamReleasesResolved -PathType Leaf) -and (Get-Content -Raw -LiteralPath $upstreamReleasesResolved).Trim() -eq '[]') { 0 } else { $null }
            PaginationLinkHeaderCount = if (Test-Path -LiteralPath $upstreamReleasesHeadersResolved -PathType Leaf) { @(Get-Content -LiteralPath $upstreamReleasesHeadersResolved | Where-Object { $_ -match '^Link:' }).Count } else { $null }
        }
        FileRecord = if ($null -eq $upstreamRecord) { $null } else { [ordered]@{ Additions = $upstreamRecord.additions; Deletions = $upstreamRecord.deletions; BlobSha = $upstreamRecord.sha } }
    }
    Assimp = [ordered]@{
        TagCommit = if ($null -eq $assimpTagCommit) { $null } else { $assimpTagCommit.sha }
        LatestPathCommit = if ($null -eq $assimpHistory -or @($assimpHistory).Count -eq 0) { $null } else { $assimpHistory[0].sha }
        InitialPathCommit = if ($null -eq $assimpHistory -or @($assimpHistory).Count -eq 0) { $null } else { $assimpHistory[@($assimpHistory).Count - 1].sha }
        HistoryCommitCount = if ($null -eq $assimpHistory) { 0 } else { @($assimpHistory).Count }
        HistoryPaginationLinkHeaderCount = if (Test-Path -LiteralPath $assimpHistoryHeadersResolved -PathType Leaf) { @(Get-Content -LiteralPath $assimpHistoryHeadersResolved | Where-Object { $_ -match '^Link:' }).Count } else { $null }
        ImportFileRecord = if ($null -eq $assimpRecord) { $null } else { [ordered]@{ Additions = $assimpRecord.additions; Deletions = $assimpRecord.deletions; BlobSha = $assimpRecord.sha } }
    }
    SourceIdentity = $sourceRecords
    CompilerUse = [ordered]@{
        CMakeListsIncludesHeader = if (Test-Path -LiteralPath (Join-Path $assimpCodeResolved 'CMakeLists.txt') -PathType Leaf) { Test-OrdinalContains (Get-Content -Raw -LiteralPath (Join-Path $assimpCodeResolved 'CMakeLists.txt')) '../contrib/stb/stb_image.h' } else { $false }
        AssimpCppInstantiatesHeader = if (Test-Path -LiteralPath (Join-Path $assimpCodeResolved 'Common\Assimp.cpp') -PathType Leaf) { Test-OrdinalContains (Get-Content -Raw -LiteralPath (Join-Path $assimpCodeResolved 'Common\Assimp.cpp')) 'define STB_IMAGE_IMPLEMENTATION' } else { $false }
        WrapperIncludesHeader = if (Test-Path -LiteralPath (Join-Path $assimpCodeResolved 'Common\StbCommon.h') -PathType Leaf) { Test-OrdinalContains (Get-Content -Raw -LiteralPath (Join-Path $assimpCodeResolved 'Common\StbCommon.h')) '#include "stb/stb_image.h"' } else { $false }
    }
    Issues = @($issues)
    ClaimLimit = 'fixed-upstream-commit-to-assimp-current-source=True|upstream-release-or-tag=False|upstream-history-complete=False|assimp-wrapper-or-prefix-modifications=False|other-assimp-contrib-provenance=False|third-party-notice-disposition=False|binary-reproducibility=False|distribution-approval=False'
}

$reportDirectory = Split-Path -Parent $reportPathResolved
New-Item -ItemType Directory -Path $reportDirectory -Force | Out-Null
$report | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $reportPathResolved -Encoding utf8

"AssimpStbProvenance|$status|assimpHistory=$($report.Assimp.HistoryCommitCount)|upstreamBlob=$($report.SourceIdentity.UpstreamCommit.GitBlobSha)|buildBlob=$($report.SourceIdentity.BuildSource.GitBlobSha)|upstreamTags=$($report.Upstream.TagsEndpoint.CapturedItemCount)|upstreamReleases=$($report.Upstream.ReleasesEndpoint.CapturedItemCount)"
"Report|$reportPathResolved"

if ($status -ne 'Pass') {
    exit 1
}
