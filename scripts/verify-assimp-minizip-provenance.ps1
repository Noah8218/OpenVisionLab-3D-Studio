[CmdletBinding()]
param(
    [string]$ZlibRepositoryDirectory = 'artifacts\dependency-candidates\assimp-minizip-intake-20260716\official-zlib',
    [string]$AssimpRepositoryDirectory = 'artifacts\dependency-candidates\assimp-kubazip-provenance-20260716\official-assimp',
    [string]$BuildSourceDirectory = 'artifacts\o3d-clean\b\assimp\src\ext_assimp\contrib\unzip',
    [string]$ClosureReportPath = 'artifacts\dependency-candidates\assimp-closure-20260716\assimp-closure.json',
    [string]$WorkingDirectory = 'artifacts\dependency-candidates\assimp-minizip-provenance-20260716\minizip-provenance-verification-work',
    [string]$ReportPath = 'artifacts\dependency-candidates\assimp-minizip-provenance-20260716\minizip-provenance.json',
    [string]$ExpectedZlibRemote = 'https://github.com/madler/zlib.git',
    [string]$ExpectedAssimpRemote = 'https://github.com/assimp/assimp.git',
    [string]$ExpectedZlibTag = 'v1.3.1',
    [string]$ExpectedZlibRevision = '51b7f2abdade71cd9bb0e7a373ef2610ec6f9daf',
    [string]$ExpectedAssimpTag = 'v5.4.2',
    [string]$ExpectedAssimpRevision = 'ddb74c2bbdee1565dda667e85f0c82a0588c8053',
    [string]$ExpectedAssimpImportRevision = '64d88276ef7117c09165e468dbb9acd999e324ac'
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
$zlibRemote = $null
$assimpRemote = $null
$zlibTagRevision = $null
$assimpTagRevision = $null
$compilerInputContract = $null
$zlibRepositoryResolved = Resolve-WorkspacePath $ZlibRepositoryDirectory
$assimpRepositoryResolved = Resolve-WorkspacePath $AssimpRepositoryDirectory
$buildSourceResolved = Resolve-WorkspacePath $BuildSourceDirectory
$closureReportResolved = Resolve-WorkspacePath $ClosureReportPath
$workingDirectoryResolved = Resolve-WorkspacePath $WorkingDirectory
$reportPathResolved = Resolve-WorkspacePath $ReportPath

$files = @(
    [pscustomobject]@{
        Name = 'ioapi.c'
        ZlibPath = 'contrib/minizip/ioapi.c'
        AssimpPath = 'contrib/unzip/ioapi.c'
        BuildPath = 'ioapi.c'
        ExpectedBlob = '782d32469ae5d5dc3515b9e589737c3b0c661dda'
    },
    [pscustomobject]@{
        Name = 'ioapi.h'
        ZlibPath = 'contrib/minizip/ioapi.h'
        AssimpPath = 'contrib/unzip/ioapi.h'
        BuildPath = 'ioapi.h'
        ExpectedBlob = 'a2d2e6e60d9250b048d50320a60dccc9d99e0264'
    },
    [pscustomobject]@{
        Name = 'unzip.c'
        ZlibPath = 'contrib/minizip/unzip.c'
        AssimpPath = 'contrib/unzip/unzip.c'
        BuildPath = 'unzip.c'
        ExpectedBlob = 'ea05b7d62a07f6ada2cb5a7723f398d9f44a8822'
    },
    [pscustomobject]@{
        Name = 'unzip.h'
        ZlibPath = 'contrib/minizip/unzip.h'
        AssimpPath = 'contrib/unzip/unzip.h'
        BuildPath = 'unzip.h'
        ExpectedBlob = '5cfc9c6274e75e32ae79f5d51a18fbd874f62711'
    }
)

$expectedClosureFiles = @(
    'contrib/unzip/ioapi.c',
    'contrib/unzip/ioapi.h',
    'contrib/unzip/unzip.c',
    'contrib/unzip/unzip.h'
)
$expectedClosureFileSetSha256 = '2e24ca3bfc05768d96770aa57d73843c01c0c3abd7acd3e3643b4db4177dc19f'

try {
    foreach ($value in @($ExpectedZlibRevision, $ExpectedAssimpRevision, $ExpectedAssimpImportRevision)) {
        if ($value -notmatch '^[0-9A-Fa-f]{40}$') {
            throw "Expected revision has an invalid format: $value"
        }
    }

    foreach ($directory in @($zlibRepositoryResolved, $assimpRepositoryResolved, $buildSourceResolved)) {
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

    $zlibRemote = ((Invoke-GitOutput $zlibRepositoryResolved @('remote', 'get-url', 'origin') 'zlib upstream remote lookup') | Select-Object -First 1).Trim()
    $assimpRemote = ((Invoke-GitOutput $assimpRepositoryResolved @('remote', 'get-url', 'origin') 'Assimp upstream remote lookup') | Select-Object -First 1).Trim()
    Test-ExpectedValue $issues 'zlib origin remote' $zlibRemote $ExpectedZlibRemote
    Test-ExpectedValue $issues 'Assimp origin remote' $assimpRemote $ExpectedAssimpRemote

    $zlibTagRevision = Get-GitRevision $zlibRepositoryResolved ('{0}^{{}}' -f $ExpectedZlibTag) 'zlib tag resolution'
    $assimpTagRevision = Get-GitRevision $assimpRepositoryResolved ('{0}^{{}}' -f $ExpectedAssimpTag) 'Assimp tag resolution'
    Test-ExpectedValue $issues 'zlib tag revision' $zlibTagRevision $ExpectedZlibRevision
    Test-ExpectedValue $issues 'Assimp tag revision' $assimpTagRevision $ExpectedAssimpRevision
    [void](Get-GitRevision $assimpRepositoryResolved $ExpectedAssimpImportRevision 'Assimp MiniZip import revision lookup')

    New-Item -ItemType Directory -Path $workingDirectoryResolved -Force | Out-Null
    $zlibArchive = Join-Path $workingDirectoryResolved 'zlib-v1.3.1.zip'
    $assimpImportArchive = Join-Path $workingDirectoryResolved 'assimp-import.zip'
    $assimpCurrentArchive = Join-Path $workingDirectoryResolved 'assimp-v5.4.2.zip'
    $zlibExtract = Join-Path $workingDirectoryResolved 'zlib-v1.3.1'
    $assimpImportExtract = Join-Path $workingDirectoryResolved 'assimp-import'
    $assimpCurrentExtract = Join-Path $workingDirectoryResolved 'assimp-v5.4.2'

    Export-GitArchive $zlibRepositoryResolved $ExpectedZlibTag @($files | ForEach-Object { $_.ZlibPath }) $zlibArchive $zlibExtract 'zlib fixed-tag MiniZip source export'
    Export-GitArchive $assimpRepositoryResolved $ExpectedAssimpImportRevision @($files | ForEach-Object { $_.AssimpPath }) $assimpImportArchive $assimpImportExtract 'Assimp MiniZip import source export'
    Export-GitArchive $assimpRepositoryResolved $ExpectedAssimpTag @($files | ForEach-Object { $_.AssimpPath }) $assimpCurrentArchive $assimpCurrentExtract 'Assimp current-tag MiniZip source export'

    foreach ($file in $files) {
        $upstreamBlob = Get-GitBlob $zlibRepositoryResolved $ExpectedZlibTag $file.ZlibPath "zlib $($file.Name) blob"
        $importBlob = Get-GitBlob $assimpRepositoryResolved $ExpectedAssimpImportRevision $file.AssimpPath "Assimp import $($file.Name) blob"
        $currentBlob = Get-GitBlob $assimpRepositoryResolved $ExpectedAssimpTag $file.AssimpPath "Assimp current $($file.Name) blob"
        Test-ExpectedValue $issues "zlib $($file.Name) blob" $upstreamBlob $file.ExpectedBlob
        Test-ExpectedValue $issues "Assimp import $($file.Name) blob" $importBlob $file.ExpectedBlob
        Test-ExpectedValue $issues "Assimp current $($file.Name) blob" $currentBlob $file.ExpectedBlob

        $upstreamPath = Join-Path $zlibExtract $file.ZlibPath
        $importPath = Join-Path $assimpImportExtract $file.AssimpPath
        $currentPath = Join-Path $assimpCurrentExtract $file.AssimpPath
        $buildPath = Join-Path $buildSourceResolved $file.BuildPath
        $upstreamRecord = Get-FileRecord $upstreamPath "zlib exported $($file.Name)"
        $importRecord = Get-FileRecord $importPath "Assimp import exported $($file.Name)"
        $currentRecord = Get-FileRecord $currentPath "Assimp current $($file.Name)"
        $buildRecord = Get-FileRecord $buildPath "Build $($file.Name)"
        Test-ExpectedValue $issues "zlib exported $($file.Name) blob" $upstreamRecord.GitBlobSha $file.ExpectedBlob
        Test-ExpectedValue $issues "Assimp import exported $($file.Name) blob" $importRecord.GitBlobSha $file.ExpectedBlob
        Test-ExpectedValue $issues "Assimp current exported $($file.Name) blob" $currentRecord.GitBlobSha $file.ExpectedBlob
        Test-ExpectedValue $issues "Build $($file.Name) blob" $buildRecord.GitBlobSha $file.ExpectedBlob

        $history = Get-PathHistory $assimpRepositoryResolved $ExpectedAssimpImportRevision $ExpectedAssimpTag $file.AssimpPath "Assimp post-import $($file.Name) history"
        Test-ExpectedSequence $issues "Assimp post-import $($file.Name) history" $history @()

        $sourceRecords.Add([pscustomobject]@{
            File = $file.Name
            ZlibBaseline = $upstreamRecord
            AssimpImport = $importRecord
            AssimpCurrent = $currentRecord
            BuildInput = $buildRecord
        })
        $historyRecords.Add([pscustomobject]@{
            File = $file.Name
            Commits = @($history)
        })
    }

    $contribRoot = Split-Path -Parent $buildSourceResolved
    $extAssimpRoot = Split-Path -Parent $contribRoot
    $assimpCodeCmakePath = Join-Path (Join-Path $extAssimpRoot 'code') 'CMakeLists.txt'
    $unzipSourcePath = Join-Path $buildSourceResolved 'unzip.c'
    $unzipHeaderPath = Join-Path $buildSourceResolved 'unzip.h'
    foreach ($path in @($assimpCodeCmakePath, $unzipSourcePath, $unzipHeaderPath)) {
        if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
            throw "Required compiler-input contract file does not exist: $path"
        }
    }

    $assimpCodeCmakeText = Get-Content -LiteralPath $assimpCodeCmakePath -Raw
    $unzipSourceText = Get-Content -LiteralPath $unzipSourcePath -Raw
    $unzipHeaderText = Get-Content -LiteralPath $unzipHeaderPath -Raw
    $closure = Get-Content -LiteralPath $closureReportResolved -Raw | ConvertFrom-Json
    $closureComponents = @($closure.Components | Where-Object { $_.key -eq 'unzip' })
    if ($closureComponents.Count -ne 1) {
        throw "Expected one unzip component in closure report but found $($closureComponents.Count)."
    }

    $closureComponent = $closureComponents[0]
    $closureUsedFiles = @($closureComponent.usedFiles | ForEach-Object { $_.ToString() })
    $sourceGroupMatches = [regex]::IsMatch($assimpCodeCmakeText, '(?is)SET\s*\(\s*unzip_SRCS\s+[^)]*crypt\.h\s+[^)]*ioapi\.c\s+[^)]*ioapi\.h\s+[^)]*unzip\.c\s+[^)]*unzip\.h\s*\)')
    $includeDirectoryMatches = [regex]::IsMatch($assimpCodeCmakeText, '(?is)INCLUDE_DIRECTORIES\s*\(\s*"\.\./contrib/unzip/"\s*\)')
    $nouncryptDefinitionIndex = $unzipSourceText.IndexOf('#define NOUNCRYPT', [System.StringComparison]::Ordinal)
    $cryptIncludeIndex = $unzipSourceText.IndexOf('#include "crypt.h"', [System.StringComparison]::Ordinal)
    $nouncryptPreventsCryptHeader = $nouncryptDefinitionIndex -ge 0 -and $cryptIncludeIndex -ge 0 -and $nouncryptDefinitionIndex -lt $cryptIncludeIndex
    $unzipIncludesHeader = [regex]::IsMatch($unzipSourceText, '(?m)^\s*#include\s+"unzip\.h"\s*$')
    $unzipHeaderIncludesIoApi = [regex]::IsMatch($unzipHeaderText, '(?m)^\s*#include\s+"ioapi\.h"\s*$')
    $compilerInputContract = [ordered]@{
        SourceGroupMatches = $sourceGroupMatches
        IncludeDirectoryMatches = $includeDirectoryMatches
        CMakeListsCryptHeader = $true
        NouncryptPreventsCryptHeader = $nouncryptPreventsCryptHeader
        UnzipIncludesHeader = $unzipIncludesHeader
        UnzipHeaderIncludesIoApi = $unzipHeaderIncludesIoApi
        ClosureComponentKey = $closureComponent.key
        ClosureUsedFileCount = [int]$closureComponent.usedFileCount
        ClosureUsedFiles = @($closureUsedFiles)
        ClosureUsedFileSetSha256 = $closureComponent.usedFileSetSha256
    }
    foreach ($property in @('SourceGroupMatches', 'IncludeDirectoryMatches', 'NouncryptPreventsCryptHeader', 'UnzipIncludesHeader', 'UnzipHeaderIncludesIoApi')) {
        Test-ExpectedBoolean $issues "Compiler input contract $property" ([bool]$compilerInputContract[$property])
    }
    Test-ExpectedValue $issues 'Closure component key' $compilerInputContract.ClosureComponentKey 'unzip'
    if ($compilerInputContract.ClosureUsedFileCount -ne 4) {
        $issues.Add("Closure used file count expected 4 but was $($compilerInputContract.ClosureUsedFileCount).")
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
        ZlibRepositoryDirectory = $zlibRepositoryResolved
        AssimpRepositoryDirectory = $assimpRepositoryResolved
        BuildSourceDirectory = $buildSourceResolved
        ClosureReportPath = $closureReportResolved
        WorkingDirectory = $workingDirectoryResolved
        ExpectedZlibRemote = $ExpectedZlibRemote
        ExpectedAssimpRemote = $ExpectedAssimpRemote
        ExpectedZlibTag = $ExpectedZlibTag
        ExpectedZlibRevision = $ExpectedZlibRevision.ToLowerInvariant()
        ExpectedAssimpTag = $ExpectedAssimpTag
        ExpectedAssimpRevision = $ExpectedAssimpRevision.ToLowerInvariant()
        ExpectedAssimpImportRevision = $ExpectedAssimpImportRevision.ToLowerInvariant()
    }
    Git = [ordered]@{
        ZlibRemote = $zlibRemote
        AssimpRemote = $assimpRemote
        ZlibTagRevision = $zlibTagRevision
        AssimpTagRevision = $assimpTagRevision
    }
    CompilerInputs = $compilerInputContract
    SourceIdentity = @($sourceRecords)
    AssimpPostImportHistory = @($historyRecords)
    Issues = @($issues)
    ClaimLimit = 'fixed-zlib-v1.3.1-contrib-minizip-four-file-source-to-assimp-import-current-build-source-identity=True|fixed-assimp-post-import-history-empty=True|crypt-header-not-compiler-read-under-source-defined-NOUNCRYPT=True|zlib-contrib-is-not-general-minizip-upstream-history=True|upstream-release-signature-or-owner-identity=False|third-party-notice-disposition=False|binary-reproducibility=False|distribution-approval=False'
}

$reportDirectory = Split-Path -Parent $reportPathResolved
New-Item -ItemType Directory -Path $reportDirectory -Force | Out-Null
$report | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $reportPathResolved -Encoding utf8

"AssimpMiniZipProvenance|$status|zlib=$zlibTagRevision|assimp=$assimpTagRevision|files=$($sourceRecords.Count)|history=$($historyRecords.Count)|issues=$($issues.Count)"
"Report|$reportPathResolved"

if ($status -ne 'Pass') {
    exit 1
}
