[CmdletBinding()]
param(
    [string]$Utf8CppRepositoryDirectory = 'artifacts\dependency-candidates\assimp-utf8cpp-provenance-20260716\official-utf8cpp',
    [string]$AssimpRepositoryDirectory = 'artifacts\dependency-candidates\assimp-kubazip-provenance-20260716\official-assimp',
    [string]$BuildSourceDirectory = 'artifacts\o3d-clean\b\assimp\src\ext_assimp\contrib\utf8cpp\source',
    [string]$ClosureReportPath = 'artifacts\dependency-candidates\assimp-closure-20260716\assimp-closure.json',
    [string]$WorkingDirectory = 'artifacts\dependency-candidates\assimp-utf8cpp-provenance-20260716\utf8cpp-provenance-verification-work',
    [string]$ReportPath = 'artifacts\dependency-candidates\assimp-utf8cpp-provenance-20260716\utf8cpp-provenance.json',
    [string]$ExpectedUtf8CppRemote = 'https://github.com/nemtrif/utfcpp.git',
    [string]$ExpectedAssimpRemote = 'https://github.com/assimp/assimp.git',
    [string]$ExpectedUtf8CppTag = 'v3.2.3',
    [string]$ExpectedUtf8CppRevision = '79835a5fa57271f07a90ed36123e30ae9741178e',
    [string]$ExpectedAssimpTag = 'v5.4.2',
    [string]$ExpectedAssimpRevision = 'ddb74c2bbdee1565dda667e85f0c82a0588c8053',
    [string]$ExpectedAssimpImportRevision = 'ce59d49dd9ce93ccf8585f78c70e58cb0e5d4961'
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
$utf8CppRemote = $null
$assimpRemote = $null
$utf8CppTagRevision = $null
$assimpTagRevision = $null
$compilerInputContract = $null
$utf8CppRepositoryResolved = Resolve-WorkspacePath $Utf8CppRepositoryDirectory
$assimpRepositoryResolved = Resolve-WorkspacePath $AssimpRepositoryDirectory
$buildSourceResolved = Resolve-WorkspacePath $BuildSourceDirectory
$closureReportResolved = Resolve-WorkspacePath $ClosureReportPath
$workingDirectoryResolved = Resolve-WorkspacePath $WorkingDirectory
$reportPathResolved = Resolve-WorkspacePath $ReportPath

$files = @(
    [pscustomobject]@{
        Name = 'utf8.h'
        Utf8CppPath = 'source/utf8.h'
        AssimpPath = 'contrib/utf8cpp/source/utf8.h'
        BuildPath = 'utf8.h'
        ExpectedBlob = '82b13f59f983c57ea5bba18bcb58f836eaba8d5e'
    },
    [pscustomobject]@{
        Name = 'utf8/checked.h'
        Utf8CppPath = 'source/utf8/checked.h'
        AssimpPath = 'contrib/utf8cpp/source/utf8/checked.h'
        BuildPath = 'utf8/checked.h'
        ExpectedBlob = '512dcc2fbac82c55afb24f1ffc99d677b2a8e86a'
    },
    [pscustomobject]@{
        Name = 'utf8/core.h'
        Utf8CppPath = 'source/utf8/core.h'
        AssimpPath = 'contrib/utf8cpp/source/utf8/core.h'
        BuildPath = 'utf8/core.h'
        ExpectedBlob = '34371ee31c8c3f48dc86c74991bc74230d08d3a7'
    },
    [pscustomobject]@{
        Name = 'utf8/unchecked.h'
        Utf8CppPath = 'source/utf8/unchecked.h'
        AssimpPath = 'contrib/utf8cpp/source/utf8/unchecked.h'
        BuildPath = 'utf8/unchecked.h'
        ExpectedBlob = '8fe83c9ecbc7eeffbf693bc8a50cd1833f816e82'
    }
)

$expectedClosureFiles = @(
    'contrib/utf8cpp/source/utf8.h',
    'contrib/utf8cpp/source/utf8/checked.h',
    'contrib/utf8cpp/source/utf8/core.h',
    'contrib/utf8cpp/source/utf8/unchecked.h'
)
$expectedClosureFileSetSha256 = '29a1bcc593f7b655228ea1deb6340c41047fbe2ea7a2d20b3c888057e3770c0c'

try {
    foreach ($value in @($ExpectedUtf8CppRevision, $ExpectedAssimpRevision, $ExpectedAssimpImportRevision)) {
        if ($value -notmatch '^[0-9A-Fa-f]{40}$') {
            throw "Expected revision has an invalid format: $value"
        }
    }

    foreach ($directory in @($utf8CppRepositoryResolved, $assimpRepositoryResolved, $buildSourceResolved)) {
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

    $utf8CppRemote = ((Invoke-GitOutput $utf8CppRepositoryResolved @('remote', 'get-url', 'origin') 'UTF8-CPP upstream remote lookup') | Select-Object -First 1).Trim()
    $assimpRemote = ((Invoke-GitOutput $assimpRepositoryResolved @('remote', 'get-url', 'origin') 'Assimp upstream remote lookup') | Select-Object -First 1).Trim()
    Test-ExpectedValue $issues 'UTF8-CPP origin remote' $utf8CppRemote $ExpectedUtf8CppRemote
    Test-ExpectedValue $issues 'Assimp origin remote' $assimpRemote $ExpectedAssimpRemote

    $utf8CppTagRevision = Get-GitRevision $utf8CppRepositoryResolved ('{0}^{{}}' -f $ExpectedUtf8CppTag) 'UTF8-CPP tag resolution'
    $assimpTagRevision = Get-GitRevision $assimpRepositoryResolved ('{0}^{{}}' -f $ExpectedAssimpTag) 'Assimp tag resolution'
    Test-ExpectedValue $issues 'UTF8-CPP tag revision' $utf8CppTagRevision $ExpectedUtf8CppRevision
    Test-ExpectedValue $issues 'Assimp tag revision' $assimpTagRevision $ExpectedAssimpRevision
    [void](Get-GitRevision $assimpRepositoryResolved $ExpectedAssimpImportRevision 'Assimp UTF8-CPP import revision lookup')

    New-Item -ItemType Directory -Path $workingDirectoryResolved -Force | Out-Null
    $utf8CppArchive = Join-Path $workingDirectoryResolved 'utf8cpp-v3.2.3.zip'
    $assimpImportArchive = Join-Path $workingDirectoryResolved 'assimp-import.zip'
    $assimpCurrentArchive = Join-Path $workingDirectoryResolved 'assimp-v5.4.2.zip'
    $utf8CppExtract = Join-Path $workingDirectoryResolved 'utf8cpp-v3.2.3'
    $assimpImportExtract = Join-Path $workingDirectoryResolved 'assimp-import'
    $assimpCurrentExtract = Join-Path $workingDirectoryResolved 'assimp-v5.4.2'

    Export-GitArchive $utf8CppRepositoryResolved $ExpectedUtf8CppTag @($files | ForEach-Object { $_.Utf8CppPath }) $utf8CppArchive $utf8CppExtract 'UTF8-CPP fixed-tag source export'
    Export-GitArchive $assimpRepositoryResolved $ExpectedAssimpImportRevision @($files | ForEach-Object { $_.AssimpPath }) $assimpImportArchive $assimpImportExtract 'Assimp UTF8-CPP import source export'
    Export-GitArchive $assimpRepositoryResolved $ExpectedAssimpTag @($files | ForEach-Object { $_.AssimpPath }) $assimpCurrentArchive $assimpCurrentExtract 'Assimp current-tag source export'

    foreach ($file in $files) {
        $upstreamBlob = Get-GitBlob $utf8CppRepositoryResolved $ExpectedUtf8CppTag $file.Utf8CppPath "UTF8-CPP $($file.Name) blob"
        $importBlob = Get-GitBlob $assimpRepositoryResolved $ExpectedAssimpImportRevision $file.AssimpPath "Assimp import $($file.Name) blob"
        $currentBlob = Get-GitBlob $assimpRepositoryResolved $ExpectedAssimpTag $file.AssimpPath "Assimp current $($file.Name) blob"
        Test-ExpectedValue $issues "UTF8-CPP $($file.Name) blob" $upstreamBlob $file.ExpectedBlob
        Test-ExpectedValue $issues "Assimp import $($file.Name) blob" $importBlob $file.ExpectedBlob
        Test-ExpectedValue $issues "Assimp current $($file.Name) blob" $currentBlob $file.ExpectedBlob

        $upstreamPath = Join-Path $utf8CppExtract $file.Utf8CppPath
        $importPath = Join-Path $assimpImportExtract $file.AssimpPath
        $currentPath = Join-Path $assimpCurrentExtract $file.AssimpPath
        $buildPath = Join-Path $buildSourceResolved $file.BuildPath
        $upstreamRecord = Get-FileRecord $upstreamPath "UTF8-CPP exported $($file.Name)"
        $importRecord = Get-FileRecord $importPath "Assimp import exported $($file.Name)"
        $currentRecord = Get-FileRecord $currentPath "Assimp current exported $($file.Name)"
        $buildRecord = Get-FileRecord $buildPath "Build $($file.Name)"
        Test-ExpectedValue $issues "UTF8-CPP exported $($file.Name) blob" $upstreamRecord.GitBlobSha $file.ExpectedBlob
        Test-ExpectedValue $issues "Assimp import exported $($file.Name) blob" $importRecord.GitBlobSha $file.ExpectedBlob
        Test-ExpectedValue $issues "Assimp current exported $($file.Name) blob" $currentRecord.GitBlobSha $file.ExpectedBlob
        Test-ExpectedValue $issues "Build $($file.Name) blob" $buildRecord.GitBlobSha $file.ExpectedBlob

        $history = Get-PathHistory $assimpRepositoryResolved $ExpectedAssimpImportRevision $ExpectedAssimpTag $file.AssimpPath "Assimp post-import $($file.Name) history"
        Test-ExpectedSequence $issues "Assimp post-import $($file.Name) history" $history @()

        $sourceRecords.Add([pscustomobject]@{
            File = $file.Name
            Utf8CppBaseline = $upstreamRecord
            AssimpImport = $importRecord
            AssimpCurrent = $currentRecord
            BuildInput = $buildRecord
        })
        $historyRecords.Add([pscustomobject]@{
            File = $file.Name
            Commits = @($history)
        })
    }

    $preambleRoot = Split-Path -Parent $buildSourceResolved
    $contribRoot = Split-Path -Parent $preambleRoot
    $extAssimpRoot = Split-Path -Parent $contribRoot
    $assimpCodeCmakePath = Join-Path (Join-Path $extAssimpRoot 'code') 'CMakeLists.txt'
    $utf8HeaderPath = Join-Path $buildSourceResolved 'utf8.h'
    $checkedHeaderPath = Join-Path $buildSourceResolved 'utf8\checked.h'
    $uncheckedHeaderPath = Join-Path $buildSourceResolved 'utf8\unchecked.h'
    foreach ($path in @($assimpCodeCmakePath, $utf8HeaderPath, $checkedHeaderPath, $uncheckedHeaderPath)) {
        if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
            throw "Required compiler-input contract file does not exist: $path"
        }
    }

    $assimpCodeCmakeText = Get-Content -LiteralPath $assimpCodeCmakePath -Raw
    $utf8HeaderText = Get-Content -LiteralPath $utf8HeaderPath -Raw
    $checkedHeaderText = Get-Content -LiteralPath $checkedHeaderPath -Raw
    $uncheckedHeaderText = Get-Content -LiteralPath $uncheckedHeaderPath -Raw
    $closure = Get-Content -LiteralPath $closureReportResolved -Raw | ConvertFrom-Json
    $closureComponents = @($closure.Components | Where-Object { $_.key -eq 'utf8cpp' })
    if ($closureComponents.Count -ne 1) {
        throw "Expected one utf8cpp component in closure report but found $($closureComponents.Count)."
    }

    $closureComponent = $closureComponents[0]
    $closureUsedFiles = @($closureComponent.usedFiles | ForEach-Object { $_.ToString() })
    $assimpIncludeDirectoryMatches = [regex]::IsMatch($assimpCodeCmakeText, '(?is)INCLUDE_DIRECTORIES\s*\(\s*"\.\./contrib/utf8cpp/source"\s*\)')
    $rootIncludesChecked = [regex]::IsMatch($utf8HeaderText, '(?m)^\s*#include\s+"utf8/checked\.h"\s*$')
    $rootIncludesUnchecked = [regex]::IsMatch($utf8HeaderText, '(?m)^\s*#include\s+"utf8/unchecked\.h"\s*$')
    $checkedIncludesCore = [regex]::IsMatch($checkedHeaderText, '(?m)^\s*#include\s+"core\.h"\s*$')
    $uncheckedIncludesCore = [regex]::IsMatch($uncheckedHeaderText, '(?m)^\s*#include\s+"core\.h"\s*$')
    $compilerInputContract = [ordered]@{
        AssimpIncludeDirectoryMatches = $assimpIncludeDirectoryMatches
        RootIncludesChecked = $rootIncludesChecked
        RootIncludesUnchecked = $rootIncludesUnchecked
        CheckedIncludesCore = $checkedIncludesCore
        UncheckedIncludesCore = $uncheckedIncludesCore
        ClosureComponentKey = $closureComponent.key
        ClosureUsedFileCount = [int]$closureComponent.usedFileCount
        ClosureUsedFiles = @($closureUsedFiles)
        ClosureUsedFileSetSha256 = $closureComponent.usedFileSetSha256
    }
    foreach ($property in @('AssimpIncludeDirectoryMatches', 'RootIncludesChecked', 'RootIncludesUnchecked', 'CheckedIncludesCore', 'UncheckedIncludesCore')) {
        Test-ExpectedBoolean $issues "Compiler input contract $property" ([bool]$compilerInputContract[$property])
    }
    Test-ExpectedValue $issues 'Closure component key' $compilerInputContract.ClosureComponentKey 'utf8cpp'
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
        Utf8CppRepositoryDirectory = $utf8CppRepositoryResolved
        AssimpRepositoryDirectory = $assimpRepositoryResolved
        BuildSourceDirectory = $buildSourceResolved
        ClosureReportPath = $closureReportResolved
        WorkingDirectory = $workingDirectoryResolved
        ExpectedUtf8CppRemote = $ExpectedUtf8CppRemote
        ExpectedAssimpRemote = $ExpectedAssimpRemote
        ExpectedUtf8CppTag = $ExpectedUtf8CppTag
        ExpectedUtf8CppRevision = $ExpectedUtf8CppRevision.ToLowerInvariant()
        ExpectedAssimpTag = $ExpectedAssimpTag
        ExpectedAssimpRevision = $ExpectedAssimpRevision.ToLowerInvariant()
        ExpectedAssimpImportRevision = $ExpectedAssimpImportRevision.ToLowerInvariant()
    }
    Git = [ordered]@{
        Utf8CppRemote = $utf8CppRemote
        AssimpRemote = $assimpRemote
        Utf8CppTagRevision = $utf8CppTagRevision
        AssimpTagRevision = $assimpTagRevision
    }
    CompilerInputs = $compilerInputContract
    SourceIdentity = @($sourceRecords)
    AssimpPostImportHistory = @($historyRecords)
    Issues = @($issues)
    ClaimLimit = 'fixed-upstream-v3.2.3-to-assimp-import-current-build-source-identity=True|fixed-assimp-post-import-history-empty=True|fixed-compiler-read-header-chain=True|upstream-release-signature-or-owner-identity=False|upstream-history-complete=False|third-party-notice-disposition=False|binary-reproducibility=False|distribution-approval=False'
}

$reportDirectory = Split-Path -Parent $reportPathResolved
New-Item -ItemType Directory -Path $reportDirectory -Force | Out-Null
$report | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $reportPathResolved -Encoding utf8

"AssimpUtf8CppProvenance|$status|utf8cpp=$utf8CppTagRevision|assimp=$assimpTagRevision|files=$($sourceRecords.Count)|history=$($historyRecords.Count)|issues=$($issues.Count)"
"Report|$reportPathResolved"

if ($status -ne 'Pass') {
    exit 1
}
