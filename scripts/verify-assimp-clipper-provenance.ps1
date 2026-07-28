[CmdletBinding()]
param(
    [string]$OfficialClipperArchivePath = 'artifacts\dependency-candidates\assimp-clipper-provenance-20260716\clipper_ver6.4.2.zip',
    [string]$ImportCommitSourcePath = 'artifacts\dependency-candidates\assimp-clipper-provenance-20260716\aa1996e1437777af62aac549d55591f1849f90de-clipper.cpp',
    [string]$CurrentTagSourcePath = 'artifacts\dependency-candidates\assimp-clipper-provenance-20260716\v5.4.2-clipper.cpp',
    [string]$BuildSourceDirectory = 'artifacts\o3d-clean\b\assimp\src\ext_assimp\contrib\clipper',
    [string]$TagReferencePath = 'artifacts\dependency-candidates\assimp-clipper-provenance-20260716\assimp-v5.4.2-tag-ref.json',
    [string]$TagCommitPath = 'artifacts\dependency-candidates\assimp-clipper-provenance-20260716\assimp-v5.4.2-commit.json',
    [string]$HistoryPagePath = 'artifacts\dependency-candidates\assimp-clipper-provenance-20260716\assimp-commits-page-1.json',
    [string]$HistoryHeadersPath = 'artifacts\dependency-candidates\assimp-clipper-provenance-20260716\assimp-commits-page-1.headers.txt',
    [string]$ImportCommitDetailPath = 'artifacts\dependency-candidates\assimp-clipper-provenance-20260716\aa1996e1437777af62aac549d55591f1849f90de.json',
    [string]$CommentCommitDetailPath = 'artifacts\dependency-candidates\assimp-clipper-provenance-20260716\bb9101ae9eb2938cadfeadd4690bbdf910ca57f4.json',
    [string]$ImportPatchPath = 'artifacts\dependency-candidates\assimp-clipper-provenance-20260716\aa1996e1437777af62aac549d55591f1849f90de.patch',
    [string]$ExpectedOfficialArchiveSha256 = 'a14320d82194807c4480ce59c98aa71cd4175a5156645c4e2b3edd330b930627',
    [string]$ExpectedImportSourceSha256 = 'df36068025d13428851c08362dda54ad25e3c1cf5585934a8c3b85ae53f6c90f',
    [string]$ExpectedCurrentSourceSha256 = 'a0b1faa0ec2e14e38eae4a90c2d35d7238bbaf159023d0790df515d4cd095d15',
    [string]$ExpectedImportPatchSha256 = 'ff0c0a8959297d43f21f6f463aae47978532082fbf43de57024b781aae09120c',
    [string]$ExpectedTagCommit = 'ddb74c2bbdee1565dda667e85f0c82a0588c8053',
    [string]$ExpectedImportCommit = 'aa1996e1437777af62aac549d55591f1849f90de',
    [string]$ExpectedCommentCommit = 'bb9101ae9eb2938cadfeadd4690bbdf910ca57f4',
    [string]$ExpectedInitialPathCommit = '56eb2dd7ee912bfa494df3a3a71e927bfe3956dd',
    [string]$ExpectedImportBlob = 'd75974336b34975721598acceac797da15709d2f',
    [string]$ExpectedCurrentBlob = 'c0a8565bb98568dcca4a5350ca52fa08152bea51',
    [ValidateRange(1, 100)]
    [int]$ExpectedHistoryCommitCount = 12,
    [ValidateRange(0, 100000)]
    [int]$ExpectedArchiveToImportAdditions = 7,
    [ValidateRange(0, 100000)]
    [int]$ExpectedArchiveToImportDeletions = 6,
    [ValidateRange(0, 100000)]
    [int]$ExpectedImportToCurrentAdditions = 4,
    [ValidateRange(0, 100000)]
    [int]$ExpectedImportToCurrentDeletions = 4,
    [string]$ReportPath = 'artifacts\dependency-candidates\assimp-clipper-provenance-20260716\clipper-provenance-report.json'
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

function Get-CrLfNormalizedRecord([byte[]]$SourceBytes) {
    $normalized = [System.IO.MemoryStream]::new($SourceBytes.Length)
    try {
        for ($index = 0; $index -lt $SourceBytes.Length; $index++) {
            if ($SourceBytes[$index] -eq 13 -and $index + 1 -lt $SourceBytes.Length -and $SourceBytes[$index + 1] -eq 10) {
                $normalized.WriteByte(10)
                $index++
            }
            else {
                $normalized.WriteByte($SourceBytes[$index])
            }
        }

        $normalizedBytes = $normalized.ToArray()
        return [pscustomobject]@{
            RawLength = [int64]$SourceBytes.Length
            RawSha256 = Get-Sha256ForBytes $SourceBytes
            CrLfNormalizedLength = [int64]$normalizedBytes.Length
            CrLfNormalizedSha256 = Get-Sha256ForBytes $normalizedBytes
        }
    }
    finally {
        $normalized.Dispose()
    }
}

function Get-ZipEntryBytes([System.IO.Compression.ZipArchive]$Archive, [string]$EntryName) {
    $entries = @($Archive.Entries | Where-Object { $_.FullName -eq $EntryName })
    if ($entries.Count -ne 1) {
        throw "Expected exactly one archive entry named $EntryName, found $($entries.Count)."
    }

    $stream = $entries[0].Open()
    $memory = [System.IO.MemoryStream]::new()
    try {
        $stream.CopyTo($memory)
        return $memory.ToArray()
    }
    finally {
        $memory.Dispose()
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

function Get-GitDiffNumStat([string]$LeftPath, [string]$RightPath) {
    $output = @(& git -c core.autocrlf=false diff --no-index --ignore-space-at-eol --numstat -- $LeftPath $RightPath 2>&1)
    $exitCode = $LASTEXITCODE
    if ($exitCode -notin @(0, 1)) {
        throw "git diff failed for $LeftPath and $RightPath with exit code ${exitCode}: $(($output | ForEach-Object { $_.ToString() }) -join "`n")"
    }

    $lines = @($output | ForEach-Object { $_.ToString() } | Where-Object { $_ -match '^\d+\t\d+\t' })
    if ($lines.Count -ne 1) {
        throw "Expected one git numstat row for $LeftPath and $RightPath, found $($lines.Count)."
    }

    $parts = $lines[0].Split("`t", 3)
    if ($parts.Count -ne 3 -or $parts[0] -notmatch '^\d+$' -or $parts[1] -notmatch '^\d+$') {
        throw "Unexpected git numstat row: $($lines[0])"
    }

    return [pscustomobject]@{
        Additions = [int]$parts[0]
        Deletions = [int]$parts[1]
        Path = $parts[2]
    }
}

function Get-ClipperFileRecord([object]$CommitDetail, [string]$CommitName) {
    $files = @($CommitDetail.files | Where-Object { $_.filename -eq 'contrib/clipper/clipper.cpp' })
    if ($files.Count -ne 1) {
        throw "$CommitName must contain exactly one contrib/clipper/clipper.cpp record, found $($files.Count)."
    }

    return $files[0]
}

function Add-TokenIssue([System.Collections.Generic.List[string]]$Issues, [string]$Text, [string]$Token, [string]$Description) {
    if ($Text.IndexOf($Token, [System.StringComparison]::Ordinal) -lt 0) {
        $Issues.Add("$Description is missing expected token: $Token")
    }
}

$issues = [System.Collections.Generic.List[string]]::new()
$archive = $null
$temporaryArchiveCppPath = $null
$archiveEntryRecords = [ordered]@{}
$archiveToImport = $null
$importToCurrent = $null
$tagReference = $null
$tagCommit = $null
$history = $null
$importDetail = $null
$commentDetail = $null
$importFile = $null
$commentFile = $null
$buildCppRecord = $null
$importSourceRecord = $null
$currentTagSourceRecord = $null

$officialArchiveResolved = Resolve-WorkspacePath $OfficialClipperArchivePath
$importSourceResolved = Resolve-WorkspacePath $ImportCommitSourcePath
$currentTagSourceResolved = Resolve-WorkspacePath $CurrentTagSourcePath
$buildSourceDirectoryResolved = Resolve-WorkspacePath $BuildSourceDirectory
$tagReferenceResolved = Resolve-WorkspacePath $TagReferencePath
$tagCommitResolved = Resolve-WorkspacePath $TagCommitPath
$historyPageResolved = Resolve-WorkspacePath $HistoryPagePath
$historyHeadersResolved = Resolve-WorkspacePath $HistoryHeadersPath
$importCommitDetailResolved = Resolve-WorkspacePath $ImportCommitDetailPath
$commentCommitDetailResolved = Resolve-WorkspacePath $CommentCommitDetailPath
$importPatchResolved = Resolve-WorkspacePath $ImportPatchPath
$reportPathResolved = Resolve-WorkspacePath $ReportPath

try {
    foreach ($hash in @($ExpectedOfficialArchiveSha256, $ExpectedImportSourceSha256, $ExpectedCurrentSourceSha256, $ExpectedImportPatchSha256)) {
        if ($hash -notmatch '^[0-9A-Fa-f]{64}$') {
            throw "Expected SHA-256 is invalid: $hash"
        }
    }

    foreach ($revision in @($ExpectedTagCommit, $ExpectedImportCommit, $ExpectedCommentCommit, $ExpectedInitialPathCommit, $ExpectedImportBlob, $ExpectedCurrentBlob)) {
        if ($revision -notmatch '^[0-9A-Fa-f]{40}$') {
            throw "Expected Git revision or blob SHA is invalid: $revision"
        }
    }

    foreach ($path in @($officialArchiveResolved, $importSourceResolved, $currentTagSourceResolved, $tagReferenceResolved, $tagCommitResolved, $historyPageResolved, $historyHeadersResolved, $importCommitDetailResolved, $commentCommitDetailResolved, $importPatchResolved)) {
        if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
            throw "Required evidence file does not exist: $path"
        }
    }

    if (-not (Test-Path -LiteralPath $buildSourceDirectoryResolved -PathType Container)) {
        throw "Build source directory does not exist: $buildSourceDirectoryResolved"
    }

    $buildCppPath = Join-Path $buildSourceDirectoryResolved 'clipper.cpp'
    $buildHppPath = Join-Path $buildSourceDirectoryResolved 'clipper.hpp'
    $buildLicensePath = Join-Path $buildSourceDirectoryResolved 'License.txt'
    foreach ($path in @($buildCppPath, $buildHppPath, $buildLicensePath)) {
        if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
            throw "Build source file does not exist: $path"
        }
    }

    $actualArchiveSha256 = Get-Sha256ForFile $officialArchiveResolved
    if ($actualArchiveSha256 -ne $ExpectedOfficialArchiveSha256.ToLowerInvariant()) {
        $issues.Add('Official Clipper archive SHA-256 does not match the fixed value.')
    }

    $importPatchSha256 = Get-Sha256ForFile $importPatchResolved
    if ($importPatchSha256 -ne $ExpectedImportPatchSha256.ToLowerInvariant()) {
        $issues.Add('Official Assimp import patch SHA-256 does not match the fixed value.')
    }

    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $archive = [System.IO.Compression.ZipFile]::OpenRead($officialArchiveResolved)
    $archiveCppBytes = Get-ZipEntryBytes $archive 'cpp/clipper.cpp'
    $archiveHppBytes = Get-ZipEntryBytes $archive 'cpp/clipper.hpp'
    $archiveLicenseBytes = Get-ZipEntryBytes $archive 'License.txt'
    $archiveEntryRecords['cpp/clipper.cpp'] = Get-CrLfNormalizedRecord $archiveCppBytes
    $archiveEntryRecords['cpp/clipper.hpp'] = Get-CrLfNormalizedRecord $archiveHppBytes
    $archiveEntryRecords['License.txt'] = Get-CrLfNormalizedRecord $archiveLicenseBytes

    if ($archiveEntryRecords['cpp/clipper.cpp'].RawSha256 -ne '5c642a3668311701f72572443aa42c1a981edb037298efc015166d9d90be0755') {
        $issues.Add('Official archive cpp/clipper.cpp SHA-256 does not match the fixed 6.4.2 source value.')
    }
    if ($archiveEntryRecords['cpp/clipper.hpp'].RawSha256 -ne '734eba9dc9d399089b2b467017074bd24728a1b9e64c7429e827806ed10e54cc') {
        $issues.Add('Official archive cpp/clipper.hpp SHA-256 does not match the fixed 6.4.2 source value.')
    }
    if ($archiveEntryRecords['License.txt'].RawSha256 -ne 'e3cc0380cc5eb9aedf5a7616fe9fde3710891bced21fd6a20a19194ccd4c1c88') {
        $issues.Add('Official archive License.txt SHA-256 does not match the fixed 6.4.2 source value.')
    }

    $temporaryArchiveCppPath = [System.IO.Path]::GetTempFileName()
    [System.IO.File]::WriteAllBytes($temporaryArchiveCppPath, $archiveCppBytes)

    $importSourceRecord = Get-CrLfNormalizedRecord ([System.IO.File]::ReadAllBytes($importSourceResolved))
    $currentTagSourceRecord = Get-CrLfNormalizedRecord ([System.IO.File]::ReadAllBytes($currentTagSourceResolved))
    $buildCppRecord = Get-CrLfNormalizedRecord ([System.IO.File]::ReadAllBytes($buildCppPath))
    $buildHppRecord = Get-CrLfNormalizedRecord ([System.IO.File]::ReadAllBytes($buildHppPath))
    $buildLicenseRecord = Get-CrLfNormalizedRecord ([System.IO.File]::ReadAllBytes($buildLicensePath))

    if ($importSourceRecord.RawSha256 -ne $ExpectedImportSourceSha256.ToLowerInvariant()) {
        $issues.Add('Captured aa1996 Clipper source SHA-256 does not match the fixed value.')
    }
    if ($currentTagSourceRecord.RawSha256 -ne $ExpectedCurrentSourceSha256.ToLowerInvariant()) {
        $issues.Add('Captured v5.4.2 Clipper source SHA-256 does not match the fixed value.')
    }
    if ($buildCppRecord.RawSha256 -ne $currentTagSourceRecord.RawSha256) {
        $issues.Add('The build-source Clipper cpp file differs from the captured official v5.4.2 tag source.')
    }
    if ($archiveEntryRecords['cpp/clipper.hpp'].CrLfNormalizedSha256 -ne $buildHppRecord.CrLfNormalizedSha256) {
        $issues.Add('The build-source Clipper hpp file differs from the official 6.4.2 archive after CRLF normalization.')
    }
    if ($archiveEntryRecords['License.txt'].CrLfNormalizedSha256 -ne $buildLicenseRecord.CrLfNormalizedSha256) {
        $issues.Add('The build-source Clipper license differs from the official 6.4.2 archive after CRLF normalization.')
    }

    $tagReference = Get-Content -Raw -LiteralPath $tagReferenceResolved | ConvertFrom-Json
    $tagCommit = Get-Content -Raw -LiteralPath $tagCommitResolved | ConvertFrom-Json
    if ($tagReference.ref -ne 'refs/tags/v5.4.2' -or $tagReference.object.type -ne 'commit' -or -not $tagReference.object.sha.Equals($ExpectedTagCommit, [System.StringComparison]::OrdinalIgnoreCase)) {
        $issues.Add('Official v5.4.2 tag reference does not resolve to the fixed commit.')
    }
    if (-not $tagCommit.sha.Equals($ExpectedTagCommit, [System.StringComparison]::OrdinalIgnoreCase)) {
        $issues.Add('Official v5.4.2 commit response does not contain the fixed tag target.')
    }

    $history = Get-Content -Raw -LiteralPath $historyPageResolved | ConvertFrom-Json
    $historyShas = @($history | ForEach-Object { $_.sha.ToLowerInvariant() })
    if ($historyShas.Count -ne $ExpectedHistoryCommitCount) {
        $issues.Add("Official Clipper path-history count is $($historyShas.Count), expected $ExpectedHistoryCommitCount.")
    }
    if ($historyShas.Count -eq 0 -or $historyShas[0] -ne $ExpectedCommentCommit.ToLowerInvariant() -or $historyShas[$historyShas.Count - 1] -ne $ExpectedInitialPathCommit.ToLowerInvariant()) {
        $issues.Add('Official Clipper history does not begin/end at the expected path commits.')
    }
    if ($historyShas.Count -lt 2 -or $historyShas[1] -ne $ExpectedImportCommit.ToLowerInvariant()) {
        $issues.Add('The import commit is not the direct predecessor of the latest Clipper path commit.')
    }
    if (@(Get-Content -LiteralPath $historyHeadersResolved | Where-Object { $_ -match '^Link:' }).Count -ne 0) {
        $issues.Add('Official Clipper path-history response is paginated; captured evidence is incomplete.')
    }

    $importDetail = Get-Content -Raw -LiteralPath $importCommitDetailResolved | ConvertFrom-Json
    $commentDetail = Get-Content -Raw -LiteralPath $commentCommitDetailResolved | ConvertFrom-Json
    if (-not $importDetail.sha.Equals($ExpectedImportCommit, [System.StringComparison]::OrdinalIgnoreCase)) {
        $issues.Add('Import commit detail does not contain the fixed commit SHA.')
    }
    if (-not $commentDetail.sha.Equals($ExpectedCommentCommit, [System.StringComparison]::OrdinalIgnoreCase)) {
        $issues.Add('Comment commit detail does not contain the fixed commit SHA.')
    }

    $importFile = Get-ClipperFileRecord $importDetail 'Import commit detail'
    $commentFile = Get-ClipperFileRecord $commentDetail 'Comment commit detail'
    if ($importFile.status -ne 'modified' -or $importFile.additions -ne 3335 -or $importFile.deletions -ne 2169 -or -not $importFile.sha.Equals($ExpectedImportBlob, [System.StringComparison]::OrdinalIgnoreCase)) {
        $issues.Add('Import commit Clipper file contract does not match the fixed 6.4.2 update record.')
    }
    if ($commentFile.status -ne 'modified' -or $commentFile.additions -ne 4 -or $commentFile.deletions -ne 4 -or -not $commentFile.sha.Equals($ExpectedCurrentBlob, [System.StringComparison]::OrdinalIgnoreCase)) {
        $issues.Add('Comment commit Clipper file contract does not match the fixed ASCII-comment update record.')
    }
    if ([string]::IsNullOrWhiteSpace($commentFile.patch) -or $commentFile.patch.IndexOf('x_1', [System.StringComparison]::Ordinal) -lt 0 -or $commentFile.patch.IndexOf('A^2', [System.StringComparison]::Ordinal) -lt 0) {
        $issues.Add('Comment commit detail does not preserve the expected four-line ASCII comment patch.')
    }

    $importBlob = Get-GitBlobSha $importSourceResolved
    $tagBlob = Get-GitBlobSha $currentTagSourceResolved
    $buildBlob = Get-GitBlobSha $buildCppPath
    if ($importBlob -ne $ExpectedImportBlob.ToLowerInvariant()) {
        $issues.Add('Captured aa1996 source does not hash to the official import commit blob.')
    }
    if ($tagBlob -ne $ExpectedCurrentBlob.ToLowerInvariant() -or $buildBlob -ne $ExpectedCurrentBlob.ToLowerInvariant()) {
        $issues.Add('Captured or build v5.4.2 source does not hash to the official latest Clipper blob.')
    }

    $archiveToImport = Get-GitDiffNumStat $temporaryArchiveCppPath $importSourceResolved
    $importToCurrent = Get-GitDiffNumStat $importSourceResolved $currentTagSourceResolved
    if ($archiveToImport.Additions -ne $ExpectedArchiveToImportAdditions -or $archiveToImport.Deletions -ne $ExpectedArchiveToImportDeletions) {
        $issues.Add("Official archive-to-import delta is $($archiveToImport.Additions)/$($archiveToImport.Deletions), expected $ExpectedArchiveToImportAdditions/$ExpectedArchiveToImportDeletions.")
    }
    if ($importToCurrent.Additions -ne $ExpectedImportToCurrentAdditions -or $importToCurrent.Deletions -ne $ExpectedImportToCurrentDeletions) {
        $issues.Add("Import-to-current delta is $($importToCurrent.Additions)/$($importToCurrent.Deletions), expected $ExpectedImportToCurrentAdditions/$ExpectedImportToCurrentDeletions.")
    }

    $archiveText = [System.Text.Encoding]::UTF8.GetString($archiveCppBytes)
    $importText = [System.IO.File]::ReadAllText($importSourceResolved, [System.Text.Encoding]::UTF8)
    $currentText = [System.IO.File]::ReadAllText($currentTagSourceResolved, [System.Text.Encoding]::UTF8)
    $importPatchText = [System.IO.File]::ReadAllText($importPatchResolved, [System.Text.Encoding]::UTF8)
    Add-TokenIssue $issues $archiveText 'std::memset(e, 0, sizeof(TEdge));' 'Official 6.4.2 InitEdge source'
    Add-TokenIssue $issues $archiveText 'for (int i = 0; i < cnt; ++i)' 'Official 6.4.2 BuildResult source'
    Add-TokenIssue $issues $importText '*e = {};' 'Assimp import InitEdge source'
    Add-TokenIssue $issues $importText '//std::memset(e, 0, sizeof(TEdge));' 'Assimp import InitEdge compatibility comment'
    Add-TokenIssue $issues $importText 'for (int j = 0; j < cnt; ++j)' 'Assimp import BuildResult source'
    Add-TokenIssue $issues $currentText 'for (int j = 0; j < cnt; ++j)' 'Current v5.4.2 BuildResult source'
    Add-TokenIssue $issues $currentText 'x_1, y_1' 'Current v5.4.2 ASCII comment source'
    Add-TokenIssue $issues $importPatchText 'diff --git a/contrib/clipper/clipper.cpp b/contrib/clipper/clipper.cpp' 'Official import patch'
    Add-TokenIssue $issues $importPatchText '*e = {};' 'Official import patch InitEdge change'
    Add-TokenIssue $issues $importPatchText 'for (int j = 0; j < cnt; ++j)' 'Official import patch hidden-variable change'
}
catch {
    $issues.Add("Exception|$($_.Exception.Message)")
}
finally {
    if ($null -ne $archive) {
        $archive.Dispose()
    }
    if ($null -ne $temporaryArchiveCppPath -and (Test-Path -LiteralPath $temporaryArchiveCppPath -PathType Leaf)) {
        Remove-Item -LiteralPath $temporaryArchiveCppPath -Force
    }
}

$status = if ($issues.Count -eq 0) { 'Pass' } else { 'Fail' }
$report = [ordered]@{
    SchemaVersion = '1.0'
    Status = $status
    Inputs = [ordered]@{
        OfficialClipperArchivePath = $officialArchiveResolved
        OfficialClipperArchiveSha256 = if (Test-Path -LiteralPath $officialArchiveResolved -PathType Leaf) { Get-Sha256ForFile $officialArchiveResolved } else { $null }
        ImportCommitSourcePath = $importSourceResolved
        CurrentTagSourcePath = $currentTagSourceResolved
        BuildSourceDirectory = $buildSourceDirectoryResolved
        TagReferencePath = $tagReferenceResolved
        TagCommitPath = $tagCommitResolved
        HistoryPagePath = $historyPageResolved
        HistoryHeadersPath = $historyHeadersResolved
        ImportCommitDetailPath = $importCommitDetailResolved
        CommentCommitDetailPath = $commentCommitDetailResolved
        ImportPatchPath = $importPatchResolved
        ImportPatchSha256 = if (Test-Path -LiteralPath $importPatchResolved -PathType Leaf) { Get-Sha256ForFile $importPatchResolved } else { $null }
    }
    OfficialTag = [ordered]@{
        Tag = if ($null -eq $tagReference) { $null } else { $tagReference.ref }
        Commit = if ($null -eq $tagCommit) { $null } else { $tagCommit.sha }
        Tree = if ($null -eq $tagCommit) { $null } else { $tagCommit.commit.tree.sha }
    }
    ArchiveEntries = $archiveEntryRecords
    SourceBlobs = [ordered]@{
        ImportCommit = [ordered]@{
            Commit = $ExpectedImportCommit.ToLowerInvariant()
            CapturedSourceSha256 = if ($null -eq $importSourceRecord) { $null } else { $importSourceRecord.RawSha256 }
            GitBlobSha = if (Test-Path -LiteralPath $importSourceResolved -PathType Leaf) { Get-GitBlobSha $importSourceResolved } else { $null }
        }
        CurrentTag = [ordered]@{
            Commit = $ExpectedTagCommit.ToLowerInvariant()
            CapturedSourceSha256 = if ($null -eq $currentTagSourceRecord) { $null } else { $currentTagSourceRecord.RawSha256 }
            CapturedGitBlobSha = if (Test-Path -LiteralPath $currentTagSourceResolved -PathType Leaf) { Get-GitBlobSha $currentTagSourceResolved } else { $null }
            BuildSourceSha256 = if ($null -eq $buildCppRecord) { $null } else { $buildCppRecord.RawSha256 }
            BuildGitBlobSha = if (Test-Path -LiteralPath $buildSourceDirectoryResolved -PathType Container) { Get-GitBlobSha (Join-Path $buildSourceDirectoryResolved 'clipper.cpp') } else { $null }
        }
    }
    PathHistory = [ordered]@{
        CommitCount = if ($null -eq $history) { 0 } else { @($history).Count }
        LatestPathCommit = if ($null -eq $history -or @($history).Count -eq 0) { $null } else { $history[0].sha }
        DirectPredecessor = if ($null -eq $history -or @($history).Count -lt 2) { $null } else { $history[1].sha }
        InitialPathCommit = if ($null -eq $history -or @($history).Count -eq 0) { $null } else { $history[@($history).Count - 1].sha }
        PaginationLinkHeaderCount = if (Test-Path -LiteralPath $historyHeadersResolved -PathType Leaf) { @(Get-Content -LiteralPath $historyHeadersResolved | Where-Object { $_ -match '^Link:' }).Count } else { $null }
    }
    OfficialCommitRecords = [ordered]@{
        Import = if ($null -eq $importFile) { $null } else { [ordered]@{ Commit = $importDetail.sha; Additions = $importFile.additions; Deletions = $importFile.deletions; BlobSha = $importFile.sha; Message = (($importDetail.commit.message -split "`r?`n")[0]) } }
        Comment = if ($null -eq $commentFile) { $null } else { [ordered]@{ Commit = $commentDetail.sha; Additions = $commentFile.additions; Deletions = $commentFile.deletions; BlobSha = $commentFile.sha; Message = (($commentDetail.commit.message -split "`r?`n")[0]) } }
    }
    Deltas = [ordered]@{
        OfficialArchiveToImport = $archiveToImport
        ImportToCurrentTag = $importToCurrent
    }
    VerifiedChanges = @(
        'official-clipper-6.4.2-to-aa1996-init-edge-update',
        'official-clipper-6.4.2-to-aa1996-build-result-hidden-variable-update',
        'aa1996-to-current-bb9101-ascii-comment-update',
        'official-v5.4.2-tag-source-to-build-source-identity'
    )
    Issues = @($issues)
    ClaimLimit = 'fixed-clipper-6.4.2-original-source=True|fixed-aa1996-and-bb9101-current-delta=True|current-v5.4.2-tag-to-build-source=True|single-line-author-attribution=False|other-assimp-contrib-provenance=False|third-party-notice-disposition=False|binary-reproducibility=False|distribution-approval=False'
}

$reportDirectory = Split-Path -Parent $reportPathResolved
New-Item -ItemType Directory -Path $reportDirectory -Force | Out-Null
$report | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $reportPathResolved -Encoding utf8

"AssimpClipperProvenance|$status|history=$($report.PathHistory.CommitCount)|archiveToImport=$($report.Deltas.OfficialArchiveToImport.Additions)/$($report.Deltas.OfficialArchiveToImport.Deletions)|importToCurrent=$($report.Deltas.ImportToCurrentTag.Additions)/$($report.Deltas.ImportToCurrentTag.Deletions)|buildBlob=$($report.SourceBlobs.CurrentTag.BuildGitBlobSha)"
"Report|$reportPathResolved"

if ($status -ne 'Pass') {
    exit 1
}
