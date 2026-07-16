[CmdletBinding()]
param(
    [string]$ZlibRepositoryDirectory = 'artifacts\dependency-candidates\assimp-minizip-intake-20260716\official-zlib',
    [string]$AssimpRepositoryDirectory = 'artifacts\dependency-candidates\assimp-kubazip-provenance-20260716\official-assimp',
    [string]$BuildSourceDirectory = 'artifacts\o3d-clean\b\assimp\src\ext_assimp\contrib\zlib',
    [string]$ClosureReportPath = 'artifacts\dependency-candidates\assimp-closure-20260716\assimp-closure.json',
    [string]$WorkingDirectory = 'artifacts\dependency-candidates\assimp-zlib-provenance-20260716\zlib-provenance-verification-work',
    [string]$ReportPath = 'artifacts\dependency-candidates\assimp-zlib-provenance-20260716\zlib-provenance.json',
    [string]$ExpectedZlibRemote = 'https://github.com/madler/zlib.git',
    [string]$ExpectedAssimpRemote = 'https://github.com/assimp/assimp.git',
    [string]$ExpectedZlibTag = 'v1.2.13',
    [string]$ExpectedZlibRevision = '04f42ceca40f73e2978b50e93806c2a18c1281fc',
    [string]$ExpectedAssimpTag = 'v5.4.2',
    [string]$ExpectedAssimpRevision = 'ddb74c2bbdee1565dda667e85f0c82a0588c8053',
    [string]$ExpectedAssimpImportRevision = '8741da2036cba41cf55fd5805e7a9730a70d2a3a'
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

function Get-CmakeVariableItems([string]$CmakeText, [string]$VariableName) {
    $pattern = '(?is)set\s*\(\s*' + [regex]::Escape($VariableName) + '\s+(?<body>.*?)\)'
    $match = [regex]::Match($CmakeText, $pattern)
    if (-not $match.Success) {
        throw "CMake variable was not found: $VariableName"
    }

    return @([regex]::Matches($match.Groups['body'].Value, '[A-Za-z0-9_.]+') | ForEach-Object { $_.Value })
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
    'adler32.c',
    'compress.c',
    'crc32.c',
    'crc32.h',
    'deflate.c',
    'deflate.h',
    'gzclose.c',
    'gzguts.h',
    'gzlib.c',
    'gzread.c',
    'gzwrite.c',
    'infback.c',
    'inffast.c',
    'inffast.h',
    'inffixed.h',
    'inflate.c',
    'inflate.h',
    'inftrees.c',
    'inftrees.h',
    'trees.c',
    'trees.h',
    'uncompr.c',
    'zlib.h',
    'zutil.c',
    'zutil.h'
)

$expectedCmakeSources = @(
    'adler32.c',
    'compress.c',
    'crc32.c',
    'deflate.c',
    'gzclose.c',
    'gzlib.c',
    'gzread.c',
    'gzwrite.c',
    'inflate.c',
    'infback.c',
    'inftrees.c',
    'inffast.c',
    'trees.c',
    'uncompr.c',
    'zutil.c'
)

$expectedCmakePrivateHeaders = @(
    'crc32.h',
    'deflate.h',
    'gzguts.h',
    'inffast.h',
    'inffixed.h',
    'inflate.h',
    'inftrees.h',
    'trees.h',
    'zutil.h'
)

$expectedClosureFiles = @($files | ForEach-Object { 'contrib/zlib/{0}' -f $_ })
$expectedClosureFileSetSha256 = '2548d872937793a4eb1ced0641cad59eaad3c1ec0c6f43a271c2637989cd8b94'

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
    [void](Get-GitRevision $assimpRepositoryResolved $ExpectedAssimpImportRevision 'Assimp zlib import revision lookup')

    New-Item -ItemType Directory -Path $workingDirectoryResolved -Force | Out-Null
    $zlibArchive = Join-Path $workingDirectoryResolved 'zlib-v1.2.13.zip'
    $assimpImportArchive = Join-Path $workingDirectoryResolved 'assimp-import.zip'
    $assimpCurrentArchive = Join-Path $workingDirectoryResolved 'assimp-v5.4.2.zip'
    $zlibExtract = Join-Path $workingDirectoryResolved 'zlib-v1.2.13'
    $assimpImportExtract = Join-Path $workingDirectoryResolved 'assimp-import'
    $assimpCurrentExtract = Join-Path $workingDirectoryResolved 'assimp-v5.4.2'
    $assimpPaths = @($files | ForEach-Object { 'contrib/zlib/{0}' -f $_ })

    Export-GitArchive $zlibRepositoryResolved $ExpectedZlibTag $files $zlibArchive $zlibExtract 'zlib fixed-tag core source export'
    Export-GitArchive $assimpRepositoryResolved $ExpectedAssimpImportRevision $assimpPaths $assimpImportArchive $assimpImportExtract 'Assimp zlib import source export'
    Export-GitArchive $assimpRepositoryResolved $ExpectedAssimpTag $assimpPaths $assimpCurrentArchive $assimpCurrentExtract 'Assimp current-tag zlib source export'

    foreach ($file in $files) {
        $assimpPath = 'contrib/zlib/{0}' -f $file
        $upstreamBlob = Get-GitBlob $zlibRepositoryResolved $ExpectedZlibTag $file "zlib $file blob"
        $importBlob = Get-GitBlob $assimpRepositoryResolved $ExpectedAssimpImportRevision $assimpPath "Assimp import $file blob"
        $currentBlob = Get-GitBlob $assimpRepositoryResolved $ExpectedAssimpTag $assimpPath "Assimp current $file blob"
        Test-ExpectedValue $issues "Assimp import $file blob" $importBlob $upstreamBlob
        Test-ExpectedValue $issues "Assimp current $file blob" $currentBlob $upstreamBlob

        $upstreamRecord = Get-FileRecord (Join-Path $zlibExtract $file) "zlib exported $file"
        $importRecord = Get-FileRecord (Join-Path $assimpImportExtract $assimpPath) "Assimp import exported $file"
        $currentRecord = Get-FileRecord (Join-Path $assimpCurrentExtract $assimpPath) "Assimp current exported $file"
        $buildRecord = Get-FileRecord (Join-Path $buildSourceResolved $file) "Build $file"
        Test-ExpectedValue $issues "zlib exported $file blob" $upstreamRecord.GitBlobSha $upstreamBlob
        Test-ExpectedValue $issues "Assimp import exported $file blob" $importRecord.GitBlobSha $upstreamBlob
        Test-ExpectedValue $issues "Assimp current exported $file blob" $currentRecord.GitBlobSha $upstreamBlob
        Test-ExpectedValue $issues "Build $file blob" $buildRecord.GitBlobSha $upstreamBlob

        $history = Get-PathHistory $assimpRepositoryResolved $ExpectedAssimpImportRevision $ExpectedAssimpTag $assimpPath "Assimp post-import $file history"
        Test-ExpectedSequence $issues "Assimp post-import $file history" $history @()

        $sourceRecords.Add([pscustomobject]@{
            File = $file
            ZlibBaseline = $upstreamRecord
            AssimpImport = $importRecord
            AssimpCurrent = $currentRecord
            BuildInput = $buildRecord
        })
        $historyRecords.Add([pscustomobject]@{
            File = $file
            Commits = @($history)
        })
    }

    $contribRoot = Split-Path -Parent $buildSourceResolved
    $extAssimpRoot = Split-Path -Parent $contribRoot
    $assimpCmakePath = Join-Path $extAssimpRoot 'CMakeLists.txt'
    $zlibCmakePath = Join-Path $buildSourceResolved 'CMakeLists.txt'
    $zlibHeaderPath = Join-Path $buildSourceResolved 'zlib.h'
    foreach ($path in @($assimpCmakePath, $zlibCmakePath, $zlibHeaderPath)) {
        if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
            throw "Required compiler-input contract file does not exist: $path"
        }
    }

    $assimpCmakeText = Get-Content -LiteralPath $assimpCmakePath -Raw
    $zlibCmakeText = Get-Content -LiteralPath $zlibCmakePath -Raw
    $zlibHeaderText = Get-Content -LiteralPath $zlibHeaderPath -Raw
    $closure = Get-Content -LiteralPath $closureReportResolved -Raw | ConvertFrom-Json
    $closureComponents = @($closure.Components | Where-Object { $_.key -eq 'zlib' })
    if ($closureComponents.Count -ne 1) {
        throw "Expected one zlib component in closure report but found $($closureComponents.Count)."
    }

    $closureComponent = $closureComponents[0]
    $closureUsedFiles = @($closureComponent.usedFiles | ForEach-Object { $_.ToString() })
    $cmakeSources = @(Get-CmakeVariableItems $zlibCmakeText 'ZLIB_SRCS')
    $cmakePrivateHeaders = @(Get-CmakeVariableItems $zlibCmakeText 'ZLIB_PRIVATE_HDRS')
    $buildsBundledZlib = [regex]::IsMatch($assimpCmakeText, '(?is)ADD_SUBDIRECTORY\s*\(\s*contrib/zlib\s*\)') -and [regex]::IsMatch($assimpCmakeText, '(?is)SET\s*\(\s*ZLIB_LIBRARIES\s+zlibstatic\s*\)')
    $generatesZconf = [regex]::IsMatch($zlibCmakeText, '(?is)configure_file\s*\(\s*\$\{CMAKE_CURRENT_SOURCE_DIR\}/zconf\.h\.cmakein\s+\$\{CMAKE_CURRENT_BINARY_DIR\}/zconf\.h\s+(?:@ONLY\s*)?\)')
    $zlibHeaderIncludesZconf = [regex]::IsMatch($zlibHeaderText, '(?m)^\s*#include\s+"zconf\.h"\s*$')
    $closureExcludesGeneratedZconf = $closureUsedFiles -notcontains 'contrib/zlib/zconf.h'
    $compilerInputContract = [ordered]@{
        BuildsBundledZlib = $buildsBundledZlib
        CmakeSources = @($cmakeSources)
        CmakePrivateHeaders = @($cmakePrivateHeaders)
        CmakePublicSourceHeader = 'zlib.h'
        GeneratesZconf = $generatesZconf
        ZlibHeaderIncludesZconf = $zlibHeaderIncludesZconf
        ClosureExcludesGeneratedZconf = $closureExcludesGeneratedZconf
        ClosureComponentKey = $closureComponent.key
        ClosureUsedFileCount = [int]$closureComponent.usedFileCount
        ClosureUsedFiles = @($closureUsedFiles)
        ClosureUsedFileSetSha256 = $closureComponent.usedFileSetSha256
    }
    foreach ($property in @('BuildsBundledZlib', 'GeneratesZconf', 'ZlibHeaderIncludesZconf', 'ClosureExcludesGeneratedZconf')) {
        Test-ExpectedBoolean $issues "Compiler input contract $property" ([bool]$compilerInputContract[$property])
    }
    Test-ExpectedSequence $issues 'CMake zlib source list' $cmakeSources $expectedCmakeSources
    Test-ExpectedSequence $issues 'CMake zlib private-header list' $cmakePrivateHeaders $expectedCmakePrivateHeaders
    Test-ExpectedValue $issues 'Closure component key' $compilerInputContract.ClosureComponentKey 'zlib'
    if ($compilerInputContract.ClosureUsedFileCount -ne 25) {
        $issues.Add("Closure used file count expected 25 but was $($compilerInputContract.ClosureUsedFileCount).")
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
    ClaimLimit = 'fixed-zlib-v1.2.13-core-25-source-to-assimp-import-current-build-source-identity=True|fixed-assimp-post-import-history-empty-for-25-inputs=True|generated-zconf-header-is-outside-upstream-source-identity-subset=True|upstream-release-signature-or-owner-identity=False|third-party-notice-disposition=False|binary-reproducibility=False|distribution-approval=False'
}

$reportDirectory = Split-Path -Parent $reportPathResolved
New-Item -ItemType Directory -Path $reportDirectory -Force | Out-Null
$report | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $reportPathResolved -Encoding utf8

"AssimpZlibProvenance|$status|zlib=$zlibTagRevision|assimp=$assimpTagRevision|files=$($sourceRecords.Count)|history=$($historyRecords.Count)|issues=$($issues.Count)"
"Report|$reportPathResolved"

if ($status -ne 'Pass') {
    exit 1
}
