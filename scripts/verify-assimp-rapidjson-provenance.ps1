[CmdletBinding()]
param(
    [string]$RapidJsonRepositoryDirectory = 'artifacts\dependency-candidates\assimp-rapidjson-intake-20260716\official-rapidjson',
    [string]$AssimpRepositoryDirectory = 'artifacts\dependency-candidates\assimp-kubazip-provenance-20260716\official-assimp',
    [string]$BuildSourceDirectory = 'artifacts\o3d-clean\b\assimp\src\ext_assimp\contrib\rapidjson\include',
    [string]$ClosureReportPath = 'artifacts\dependency-candidates\assimp-closure-20260716\assimp-closure.json',
    [string]$WorkingDirectory = 'artifacts\dependency-candidates\assimp-rapidjson-provenance-20260716\rapidjson-provenance-verification-work',
    [string]$ReportPath = 'artifacts\dependency-candidates\assimp-rapidjson-provenance-20260716\rapidjson-provenance.json',
    [string]$ExpectedRapidJsonRemote = 'https://github.com/Tencent/rapidjson.git',
    [string]$ExpectedAssimpRemote = 'https://github.com/assimp/assimp.git',
    [string]$ExpectedRapidJsonTag = 'v1.1.0',
    [string]$ExpectedRapidJsonTagRevision = 'f54b0e47a08782a6131cc3d60f94d038fa6e0a51',
    [string]$ExpectedRapidJsonBaselineRevision = '676d99db96e2108724e62342a47e28c8e991ed3b',
    [string]$ExpectedAssimpTag = 'v5.4.2',
    [string]$ExpectedAssimpRevision = 'ddb74c2bbdee1565dda667e85f0c82a0588c8053',
    [string]$ExpectedAssimpImportRevision = '4a3e0e46ac45867c8c8fac9cbcdee3bc30e99f92'
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

function Invoke-GitOutput(
    [string]$RepositoryDirectory,
    [string[]]$GitArguments,
    [string]$Description,
    [int[]]$AllowedExitCodes = @(0)
) {
    $output = @(& git -C $RepositoryDirectory @GitArguments 2>&1)
    $exitCode = $LASTEXITCODE
    if ($AllowedExitCodes -notcontains $exitCode) {
        $details = ($output | ForEach-Object { $_.ToString() }) -join "`n"
        throw "$Description failed with exit code $exitCode. $details"
    }

    return @($output | ForEach-Object { $_.ToString() })
}

function Get-GitRevision([string]$RepositoryDirectory, [string]$Revision, [string]$Description) {
    $output = Invoke-GitOutput $RepositoryDirectory @('rev-parse', $Revision) $Description
    $value = @($output | Where-Object { -not [string]::IsNullOrWhiteSpace($_) } | Select-Object -First 1)
    if ($value.Count -ne 1 -or $value[0] -notmatch '^[0-9A-Fa-f]{40}$') {
        throw "$Description did not resolve to one full Git revision."
    }

    return $value[0].Trim().ToLowerInvariant()
}

function Get-GitBlob([string]$RepositoryDirectory, [string]$Revision, [string]$RelativePath, [string]$Description) {
    return Get-GitRevision $RepositoryDirectory ('{0}:{1}' -f $Revision, $RelativePath) $Description
}

function Get-FileGitBlob([string]$Path, [string]$Description) {
    $output = @(& git hash-object -- $Path 2>&1)
    if ($LASTEXITCODE -ne 0) {
        $details = ($output | ForEach-Object { $_.ToString() }) -join "`n"
        throw "$Description failed with exit code $LASTEXITCODE. $details"
    }

    $value = @($output | ForEach-Object { $_.ToString() } | Where-Object { -not [string]::IsNullOrWhiteSpace($_) } | Select-Object -First 1)
    if ($value.Count -ne 1 -or $value[0] -notmatch '^[0-9A-Fa-f]{40}$') {
        throw "$Description did not produce one full Git blob identifier."
    }

    return $value[0].Trim().ToLowerInvariant()
}

function Get-FileRecord([string]$Path, [string]$Description) {
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        throw "$Description does not exist: $Path"
    }

    $file = Get-Item -LiteralPath $Path
    return [pscustomobject]@{
        Path = $file.FullName
        Length = [int64]$file.Length
        Sha256 = Get-Sha256ForFile $file.FullName
        GitBlobSha = Get-FileGitBlob $file.FullName "$Description Git blob"
    }
}

function Get-PathHistory([string]$RepositoryDirectory, [string]$FromRevision, [string]$ToRevision, [string]$RelativePath, [string]$Description) {
    $range = '{0}..{1}' -f $FromRevision, $ToRevision
    $output = Invoke-GitOutput $RepositoryDirectory @('log', '--format=%H', $range, '--', $RelativePath) $Description
    return @($output | Where-Object { $_ -match '^[0-9A-Fa-f]{40}$' } | ForEach-Object { $_.ToLowerInvariant() })
}

function Test-GitAncestor([string]$RepositoryDirectory, [string]$Ancestor, [string]$Descendant, [string]$Description) {
    $output = @(& git -C $RepositoryDirectory merge-base --is-ancestor $Ancestor $Descendant 2>&1)
    $exitCode = $LASTEXITCODE
    if ($exitCode -notin @(0, 1)) {
        $details = ($output | ForEach-Object { $_.ToString() }) -join "`n"
        throw "$Description failed with exit code $exitCode. $details"
    }

    return $exitCode -eq 0
}

function Export-GitArchive(
    [string]$RepositoryDirectory,
    [string]$Revision,
    [string[]]$RelativePaths,
    [string]$ArchivePath,
    [string]$DestinationDirectory,
    [string]$Description
) {
    $arguments = @('archive', '--format=zip', ('--output={0}' -f $ArchivePath), $Revision) + $RelativePaths
    Invoke-GitOutput $RepositoryDirectory $arguments $Description | Out-Null
    Expand-Archive -LiteralPath $ArchivePath -DestinationPath $DestinationDirectory -Force
}

function Test-ExpectedValue([System.Collections.Generic.List[string]]$Issues, [string]$Label, [string]$Actual, [string]$Expected) {
    if ([string]::IsNullOrWhiteSpace($Actual) -or -not $Actual.Equals($Expected, [System.StringComparison]::OrdinalIgnoreCase)) {
        $Issues.Add("$Label expected '$Expected' but was '$Actual'.")
    }
}

function Test-ExpectedSequence([System.Collections.Generic.List[string]]$Issues, [string]$Label, [string[]]$Actual, [string[]]$Expected) {
    if ($Actual.Count -ne $Expected.Count) {
        $Issues.Add("$Label expected $($Expected.Count) entries but found $($Actual.Count).")
        return
    }

    for ($index = 0; $index -lt $Expected.Count; $index++) {
        if (-not $Actual[$index].Equals($Expected[$index], [System.StringComparison]::OrdinalIgnoreCase)) {
            $Issues.Add("$Label entry $index expected '$($Expected[$index])' but was '$($Actual[$index])'.")
        }
    }
}

function Test-ExpectedBoolean([System.Collections.Generic.List[string]]$Issues, [string]$Label, [bool]$Actual) {
    if (-not $Actual) {
        $Issues.Add("$Label was false.")
    }
}

$issues = [System.Collections.Generic.List[string]]::new()
$sourceRecords = [System.Collections.Generic.List[object]]::new()
$historyRecords = [System.Collections.Generic.List[object]]::new()
$rapidJsonRemote = $null
$assimpRemote = $null
$rapidJsonTagRevision = $null
$rapidJsonBaselineRevision = $null
$assimpTagRevision = $null
$compilerInputContract = $null
$rapidJsonRepositoryResolved = Resolve-WorkspacePath $RapidJsonRepositoryDirectory
$assimpRepositoryResolved = Resolve-WorkspacePath $AssimpRepositoryDirectory
$buildSourceResolved = Resolve-WorkspacePath $BuildSourceDirectory
$closureReportResolved = Resolve-WorkspacePath $ClosureReportPath
$workingDirectoryResolved = Resolve-WorkspacePath $WorkingDirectory
$reportPathResolved = Resolve-WorkspacePath $ReportPath

$files = @(
    'rapidjson/allocators.h',
    'rapidjson/document.h',
    'rapidjson/encodedstream.h',
    'rapidjson/encodings.h',
    'rapidjson/error/en.h',
    'rapidjson/error/error.h',
    'rapidjson/internal/biginteger.h',
    'rapidjson/internal/clzll.h',
    'rapidjson/internal/diyfp.h',
    'rapidjson/internal/dtoa.h',
    'rapidjson/internal/ieee754.h',
    'rapidjson/internal/itoa.h',
    'rapidjson/internal/meta.h',
    'rapidjson/internal/pow10.h',
    'rapidjson/internal/regex.h',
    'rapidjson/internal/stack.h',
    'rapidjson/internal/strfunc.h',
    'rapidjson/internal/strtod.h',
    'rapidjson/internal/swap.h',
    'rapidjson/memorystream.h',
    'rapidjson/pointer.h',
    'rapidjson/prettywriter.h',
    'rapidjson/rapidjson.h',
    'rapidjson/reader.h',
    'rapidjson/schema.h',
    'rapidjson/stream.h',
    'rapidjson/stringbuffer.h',
    'rapidjson/uri.h',
    'rapidjson/writer.h'
)

$expectedClosureFiles = @($files | ForEach-Object { 'contrib/rapidjson/include/{0}' -f $_ })
$expectedClosureFileSetSha256 = 'f9f5aec8c411fb5af185f0a148d1615539ca9837f3a05fa992df0cef315006fb'

try {
    foreach ($value in @($ExpectedRapidJsonTagRevision, $ExpectedRapidJsonBaselineRevision, $ExpectedAssimpRevision, $ExpectedAssimpImportRevision)) {
        if ($value -notmatch '^[0-9A-Fa-f]{40}$') {
            throw "Expected revision has an invalid format: $value"
        }
    }

    foreach ($directory in @($rapidJsonRepositoryResolved, $assimpRepositoryResolved, $buildSourceResolved)) {
        if (-not (Test-Path -LiteralPath $directory -PathType Container)) {
            throw "Required source directory does not exist: $directory"
        }
    }

    if (-not (Test-Path -LiteralPath $closureReportResolved -PathType Leaf)) {
        throw "Compiler-read closure report does not exist: $closureReportResolved"
    }

    if (Test-Path -LiteralPath $workingDirectoryResolved) {
        throw "WorkingDirectory already exists. Choose a new empty artifact path: $workingDirectoryResolved"
    }

    $rapidJsonRemote = ((Invoke-GitOutput $rapidJsonRepositoryResolved @('remote', 'get-url', 'origin') 'RapidJSON upstream remote lookup') | Select-Object -First 1).Trim()
    $assimpRemote = ((Invoke-GitOutput $assimpRepositoryResolved @('remote', 'get-url', 'origin') 'Assimp upstream remote lookup') | Select-Object -First 1).Trim()
    Test-ExpectedValue $issues 'RapidJSON origin remote' $rapidJsonRemote $ExpectedRapidJsonRemote
    Test-ExpectedValue $issues 'Assimp origin remote' $assimpRemote $ExpectedAssimpRemote

    $rapidJsonTagRevision = Get-GitRevision $rapidJsonRepositoryResolved ('{0}^{{}}' -f $ExpectedRapidJsonTag) 'RapidJSON tag resolution'
    $rapidJsonBaselineRevision = Get-GitRevision $rapidJsonRepositoryResolved $ExpectedRapidJsonBaselineRevision 'RapidJSON post-tag baseline revision lookup'
    $assimpTagRevision = Get-GitRevision $assimpRepositoryResolved ('{0}^{{}}' -f $ExpectedAssimpTag) 'Assimp tag resolution'
    Test-ExpectedValue $issues 'RapidJSON tag revision' $rapidJsonTagRevision $ExpectedRapidJsonTagRevision
    Test-ExpectedValue $issues 'RapidJSON post-tag baseline revision' $rapidJsonBaselineRevision $ExpectedRapidJsonBaselineRevision
    Test-ExpectedValue $issues 'Assimp tag revision' $assimpTagRevision $ExpectedAssimpRevision
    [void](Get-GitRevision $assimpRepositoryResolved $ExpectedAssimpImportRevision 'Assimp RapidJSON import revision lookup')
    Test-ExpectedBoolean $issues 'RapidJSON v1.1.0 tag is ancestor of post-tag baseline' (Test-GitAncestor $rapidJsonRepositoryResolved $ExpectedRapidJsonTagRevision $ExpectedRapidJsonBaselineRevision 'RapidJSON tag ancestry check')
    Test-ExpectedBoolean $issues 'Assimp import is ancestor of fixed tag' (Test-GitAncestor $assimpRepositoryResolved $ExpectedAssimpImportRevision $ExpectedAssimpRevision 'Assimp import ancestry check')

    New-Item -ItemType Directory -Path $workingDirectoryResolved -Force | Out-Null
    $rapidJsonArchive = Join-Path $workingDirectoryResolved 'rapidjson-post-v1.1.0-baseline.zip'
    $assimpImportArchive = Join-Path $workingDirectoryResolved 'assimp-import.zip'
    $assimpCurrentArchive = Join-Path $workingDirectoryResolved 'assimp-v5.4.2.zip'
    $rapidJsonExtract = Join-Path $workingDirectoryResolved 'rapidjson-post-v1.1.0-baseline'
    $assimpImportExtract = Join-Path $workingDirectoryResolved 'assimp-import'
    $assimpCurrentExtract = Join-Path $workingDirectoryResolved 'assimp-v5.4.2'
    $rapidJsonPaths = @($files | ForEach-Object { 'include/{0}' -f $_ })
    $assimpPaths = @($files | ForEach-Object { 'contrib/rapidjson/include/{0}' -f $_ })

    Export-GitArchive $rapidJsonRepositoryResolved $ExpectedRapidJsonBaselineRevision $rapidJsonPaths $rapidJsonArchive $rapidJsonExtract 'RapidJSON post-tag source export'
    Export-GitArchive $assimpRepositoryResolved $ExpectedAssimpImportRevision $assimpPaths $assimpImportArchive $assimpImportExtract 'Assimp RapidJSON import source export'
    Export-GitArchive $assimpRepositoryResolved $ExpectedAssimpTag $assimpPaths $assimpCurrentArchive $assimpCurrentExtract 'Assimp current-tag RapidJSON source export'

    foreach ($file in $files) {
        $rapidJsonPath = 'include/{0}' -f $file
        $assimpPath = 'contrib/rapidjson/include/{0}' -f $file
        $upstreamBlob = Get-GitBlob $rapidJsonRepositoryResolved $ExpectedRapidJsonBaselineRevision $rapidJsonPath "RapidJSON $file blob"
        $importBlob = Get-GitBlob $assimpRepositoryResolved $ExpectedAssimpImportRevision $assimpPath "Assimp import $file blob"
        $currentBlob = Get-GitBlob $assimpRepositoryResolved $ExpectedAssimpTag $assimpPath "Assimp current $file blob"
        Test-ExpectedValue $issues "Assimp import $file blob" $importBlob $upstreamBlob
        Test-ExpectedValue $issues "Assimp current $file blob" $currentBlob $upstreamBlob

        $upstreamRecord = Get-FileRecord (Join-Path $rapidJsonExtract $rapidJsonPath) "RapidJSON exported $file"
        $importRecord = Get-FileRecord (Join-Path $assimpImportExtract $assimpPath) "Assimp import exported $file"
        $currentRecord = Get-FileRecord (Join-Path $assimpCurrentExtract $assimpPath) "Assimp current exported $file"
        $buildRecord = Get-FileRecord (Join-Path $buildSourceResolved $file) "Build $file"
        Test-ExpectedValue $issues "RapidJSON exported $file blob" $upstreamRecord.GitBlobSha $upstreamBlob
        Test-ExpectedValue $issues "Assimp import exported $file blob" $importRecord.GitBlobSha $upstreamBlob
        Test-ExpectedValue $issues "Assimp current exported $file blob" $currentRecord.GitBlobSha $upstreamBlob
        Test-ExpectedValue $issues "Build $file blob" $buildRecord.GitBlobSha $upstreamBlob

        $history = Get-PathHistory $assimpRepositoryResolved $ExpectedAssimpImportRevision $ExpectedAssimpTag $assimpPath "Assimp post-import $file history"
        Test-ExpectedSequence $issues "Assimp post-import $file history" $history @()

        $sourceRecords.Add([pscustomobject]@{
            File = $file
            RapidJsonBaseline = $upstreamRecord
            AssimpImport = $importRecord
            AssimpCurrent = $currentRecord
            BuildInput = $buildRecord
        })
        $historyRecords.Add([pscustomobject]@{
            File = $file
            Commits = @($history)
        })
    }

    $rapidJsonRoot = Split-Path -Parent $buildSourceResolved
    $contribRoot = Split-Path -Parent $rapidJsonRoot
    $extAssimpRoot = Split-Path -Parent $contribRoot
    $codeCmakePath = Join-Path (Join-Path $extAssimpRoot 'code') 'CMakeLists.txt'
    if (-not (Test-Path -LiteralPath $codeCmakePath -PathType Leaf)) {
        throw "Required RapidJSON compiler-input contract file does not exist: $codeCmakePath"
    }

    $codeCmakeText = Get-Content -LiteralPath $codeCmakePath -Raw
    $closure = Get-Content -LiteralPath $closureReportResolved -Raw | ConvertFrom-Json
    $closureComponents = @($closure.Components | Where-Object { $_.key -eq 'rapidjson' })
    if ($closureComponents.Count -ne 1) {
        throw "Expected one RapidJSON component in closure report but found $($closureComponents.Count)."
    }

    $closureComponent = $closureComponents[0]
    $closureUsedFiles = @($closureComponent.usedFiles | ForEach-Object { $_.ToString() })
    $includeDirectoryMatches = [regex]::IsMatch($codeCmakeText, '(?is)INCLUDE_DIRECTORIES\s*\(\s*"\.\./contrib/rapidjson/include"\s*\)')
    $stdStringDefinitionMatches = [regex]::IsMatch($codeCmakeText, '(?is)ADD_DEFINITIONS\s*\(\s*-DRAPIDJSON_HAS_STDSTRING=1\s*\)')
    $memberIteratorOptionMatches = [regex]::IsMatch($codeCmakeText, '(?is)option\s*\(\s*ASSIMP_RAPIDJSON_NO_MEMBER_ITERATOR\b')
    $memberIteratorDefinitionMatches = [regex]::IsMatch($codeCmakeText, '(?is)ADD_DEFINITIONS\s*\(\s*-DRAPIDJSON_NOMEMBERITERATORCLASS\s*\)')
    $compilerInputContract = [ordered]@{
        IncludeDirectoryMatches = $includeDirectoryMatches
        StdStringDefinitionMatches = $stdStringDefinitionMatches
        MemberIteratorOptionMatches = $memberIteratorOptionMatches
        MemberIteratorDefinitionMatches = $memberIteratorDefinitionMatches
        ClosureComponentKey = $closureComponent.key
        ClosureUsedFileCount = [int]$closureComponent.usedFileCount
        ClosureUsedFiles = @($closureUsedFiles)
        ClosureUsedFileSetSha256 = $closureComponent.usedFileSetSha256
    }
    foreach ($property in @('IncludeDirectoryMatches', 'StdStringDefinitionMatches', 'MemberIteratorOptionMatches', 'MemberIteratorDefinitionMatches')) {
        Test-ExpectedBoolean $issues "Compiler input contract $property" ([bool]$compilerInputContract[$property])
    }
    Test-ExpectedValue $issues 'Closure component key' $compilerInputContract.ClosureComponentKey 'rapidjson'
    if ($compilerInputContract.ClosureUsedFileCount -ne 29) {
        $issues.Add("Closure used file count expected 29 but was $($compilerInputContract.ClosureUsedFileCount).")
    }
    Test-ExpectedSequence $issues 'Closure used files' $closureUsedFiles $expectedClosureFiles
    Test-ExpectedValue $issues 'Closure used file set SHA-256' $compilerInputContract.ClosureUsedFileSetSha256 $expectedClosureFileSetSha256
}
catch {
    $issues.Add("Exception|$($_.Exception.Message)")
}

$status = if ($issues.Count -eq 0) { 'Pass' } else { 'Fail' }
$report = [ordered]@{
    SchemaVersion = '1.0'
    Status = $status
    Inputs = [ordered]@{
        RapidJsonRepositoryDirectory = $rapidJsonRepositoryResolved
        AssimpRepositoryDirectory = $assimpRepositoryResolved
        BuildSourceDirectory = $buildSourceResolved
        ClosureReportPath = $closureReportResolved
        WorkingDirectory = $workingDirectoryResolved
        ExpectedRapidJsonRemote = $ExpectedRapidJsonRemote
        ExpectedAssimpRemote = $ExpectedAssimpRemote
        ExpectedRapidJsonTag = $ExpectedRapidJsonTag
        ExpectedRapidJsonTagRevision = $ExpectedRapidJsonTagRevision.ToLowerInvariant()
        ExpectedRapidJsonBaselineRevision = $ExpectedRapidJsonBaselineRevision.ToLowerInvariant()
        ExpectedAssimpTag = $ExpectedAssimpTag
        ExpectedAssimpRevision = $ExpectedAssimpRevision.ToLowerInvariant()
        ExpectedAssimpImportRevision = $ExpectedAssimpImportRevision.ToLowerInvariant()
    }
    Git = [ordered]@{
        RapidJsonRemote = $rapidJsonRemote
        AssimpRemote = $assimpRemote
        RapidJsonTagRevision = $rapidJsonTagRevision
        RapidJsonBaselineRevision = $rapidJsonBaselineRevision
        AssimpTagRevision = $assimpTagRevision
    }
    CompilerInputs = $compilerInputContract
    SourceIdentity = @($sourceRecords)
    AssimpPostImportHistory = @($historyRecords)
    Issues = @($issues)
    ClaimLimit = 'fixed-rapidjson-public-post-v1.1.0-baseline-29-header-source-to-assimp-import-current-build-source-identity=True|fixed-assimp-post-import-history-empty-for-29-inputs=True|v1.1.0-tag-is-ancestor-not-source-snapshot-identity=True|upstream-release-signature-or-owner-identity=False|third-party-notice-disposition=False|binary-reproducibility=False|distribution-approval=False'
}

$reportDirectory = Split-Path -Parent $reportPathResolved
New-Item -ItemType Directory -Path $reportDirectory -Force | Out-Null
$report | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $reportPathResolved -Encoding utf8

"AssimpRapidJsonProvenance|$status|rapidjson=$rapidJsonBaselineRevision|assimp=$assimpTagRevision|files=$($sourceRecords.Count)|history=$($historyRecords.Count)|issues=$($issues.Count)"
"Report|$reportPathResolved"

if ($status -ne 'Pass') {
    exit 1
}
