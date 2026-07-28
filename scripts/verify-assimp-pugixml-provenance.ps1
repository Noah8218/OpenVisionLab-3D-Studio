[CmdletBinding()]
param(
    [string]$PugixmlRepositoryDirectory = 'artifacts\dependency-candidates\assimp-pugixml-provenance-20260716\official-pugixml',
    [string]$AssimpRepositoryDirectory = 'artifacts\dependency-candidates\assimp-kubazip-provenance-20260716\official-assimp',
    [string]$BuildSourceDirectory = 'artifacts\o3d-clean\b\assimp\src\ext_assimp\contrib\pugixml\src',
    [string]$WorkingDirectory = 'artifacts\dependency-candidates\assimp-pugixml-provenance-20260716\pugixml-provenance-verification-work',
    [string]$ReportPath = 'artifacts\dependency-candidates\assimp-pugixml-provenance-20260716\pugixml-provenance.json',
    [string]$ExpectedPugixmlRemote = 'https://github.com/zeux/pugixml.git',
    [string]$ExpectedAssimpRemote = 'https://github.com/assimp/assimp.git',
    [string]$ExpectedPugixmlTag = 'v1.13',
    [string]$ExpectedPugixmlRevision = 'a0e064336317c9347a91224112af9933598714e9',
    [string]$ExpectedAssimpTag = 'v5.4.2',
    [string]$ExpectedAssimpRevision = 'ddb74c2bbdee1565dda667e85f0c82a0588c8053',
    [string]$ExpectedAssimpImportRevision = '62cefd5b275628ff97a77d0cd9220e1c35794a3f',
    [string]$ExpectedAssimpFollowUpRevision = '01231d0e6001f555c81dcfcc6c581fa5797ccac9'
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

function Get-Sha256ForBytes([byte[]]$Bytes) {
    $sha256 = [System.Security.Cryptography.SHA256]::Create()
    try {
        return ([System.BitConverter]::ToString($sha256.ComputeHash($Bytes))).Replace('-', '').ToLowerInvariant()
    }
    finally {
        $sha256.Dispose()
    }
}

function Write-CrLfNormalizedCopy([string]$InputPath, [string]$OutputPath) {
    [byte[]]$sourceBytes = [System.IO.File]::ReadAllBytes($InputPath)
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

        [byte[]]$normalizedBytes = $normalized.ToArray()
        [System.IO.File]::WriteAllBytes($OutputPath, $normalizedBytes)
        return [pscustomobject]@{
            Path = $OutputPath
            Length = [int64]$normalizedBytes.Length
            Sha256 = Get-Sha256ForBytes $normalizedBytes
        }
    }
    finally {
        $normalized.Dispose()
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

function Get-Numstat([string]$LeftPath, [string]$RightPath, [string]$Description) {
    $output = @(& git -c core.autocrlf=false -c core.safecrlf=false diff --no-index --numstat -- $LeftPath $RightPath 2>&1)
    $exitCode = $LASTEXITCODE
    if ($exitCode -notin @(0, 1)) {
        $details = ($output | ForEach-Object { $_.ToString() }) -join "`n"
        throw "$Description failed with exit code $exitCode. $details"
    }

    $records = @()
    foreach ($lineValue in $output) {
        $line = $lineValue.ToString()
        if ($line -match '^(?<Added>\d+|-)\t(?<Deleted>\d+|-)\t') {
            $records += [pscustomobject]@{
                Added = $matches['Added']
                Deleted = $matches['Deleted']
            }
        }
    }

    if ($records.Count -eq 0) {
        if ($exitCode -ne 0) {
            throw "$Description reported a difference without a numstat record."
        }

        return [pscustomobject]@{ Added = 0; Deleted = 0 }
    }

    if ($records.Count -ne 1 -or $records[0].Added -eq '-' -or $records[0].Deleted -eq '-') {
        throw "$Description did not produce one text numstat record."
    }

    return [pscustomobject]@{
        Added = [int]$records[0].Added
        Deleted = [int]$records[0].Deleted
    }
}

function Get-PathHistory([string]$RepositoryDirectory, [string]$FromRevision, [string]$ToRevision, [string]$RelativePath, [string]$Description) {
    $range = '{0}..{1}' -f $FromRevision, $ToRevision
    $output = Invoke-GitOutput $RepositoryDirectory @('log', '--format=%H', $range, '--', $RelativePath) $Description
    return @($output | Where-Object { $_ -match '^[0-9A-Fa-f]{40}$' } | ForEach-Object { $_.ToLowerInvariant() })
}

function Get-CommitNumstat([string]$RepositoryDirectory, [string]$Commit, [string]$RelativePath, [string]$Description) {
    $output = Invoke-GitOutput $RepositoryDirectory @('diff-tree', '--no-commit-id', '--numstat', '-r', $Commit, '--', $RelativePath) $Description
    $records = @()
    foreach ($line in $output) {
        if ($line -match '^(?<Added>\d+|-)\t(?<Deleted>\d+|-)\t') {
            $records += [pscustomobject]@{
                Added = $matches['Added']
                Deleted = $matches['Deleted']
            }
        }
    }

    if ($records.Count -ne 1 -or $records[0].Added -eq '-' -or $records[0].Deleted -eq '-') {
        throw "$Description did not produce one text numstat record."
    }

    return [pscustomobject]@{
        Added = [int]$records[0].Added
        Deleted = [int]$records[0].Deleted
    }
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

function Test-ExpectedNumstat([System.Collections.Generic.List[string]]$Issues, [string]$Label, [object]$Actual, [int]$ExpectedAdded, [int]$ExpectedDeleted) {
    if ($Actual.Added -ne $ExpectedAdded -or $Actual.Deleted -ne $ExpectedDeleted) {
        $Issues.Add("$Label expected $ExpectedAdded/$ExpectedDeleted but was $($Actual.Added)/$($Actual.Deleted).")
    }
}

function Test-ExpectedSequence([System.Collections.Generic.List[string]]$Issues, [string]$Label, [string[]]$Actual, [string[]]$Expected) {
    if ($Actual.Count -ne $Expected.Count) {
        $Issues.Add("$Label expected $($Expected.Count) commits but found $($Actual.Count).")
        return
    }

    for ($index = 0; $index -lt $Expected.Count; $index++) {
        if (-not $Actual[$index].Equals($Expected[$index], [System.StringComparison]::OrdinalIgnoreCase)) {
            $Issues.Add("$Label commit $index expected '$($Expected[$index])' but was '$($Actual[$index])'.")
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
$deltaRecords = [System.Collections.Generic.List[object]]::new()
$historyRecords = [System.Collections.Generic.List[object]]::new()
$followUpRecords = [System.Collections.Generic.List[object]]::new()
$pugixmlRemote = $null
$assimpRemote = $null
$pugixmlTagRevision = $null
$assimpTagRevision = $null
$compilerInputContract = $null
$pugixmlRepositoryResolved = Resolve-WorkspacePath $PugixmlRepositoryDirectory
$assimpRepositoryResolved = Resolve-WorkspacePath $AssimpRepositoryDirectory
$buildSourceResolved = Resolve-WorkspacePath $BuildSourceDirectory
$workingDirectoryResolved = Resolve-WorkspacePath $WorkingDirectory
$reportPathResolved = Resolve-WorkspacePath $ReportPath

$files = @(
    [pscustomobject]@{
        Name = 'pugiconfig.hpp'
        PugixmlPath = 'src/pugiconfig.hpp'
        AssimpPath = 'contrib/pugixml/src/pugiconfig.hpp'
        ExpectedPugixmlBlob = '88b2f2aee09f4752048fdfb5ecd093fbd55f65cf'
        ExpectedImportBlob = '9bf2efd39dc65020a21e0f7cfb0d1ee504b311b7'
        ExpectedCurrentBlob = '1a395690311ffd7c118ea16b1039a651e074707a'
        BaselineToImport = [pscustomobject]@{ Added = 1; Deleted = 1 }
        ImportToCurrent = [pscustomobject]@{ Added = 2; Deleted = 2 }
        ExpectedPostImportHistory = @('01231d0e6001f555c81dcfcc6c581fa5797ccac9')
    },
    [pscustomobject]@{
        Name = 'pugixml.cpp'
        PugixmlPath = 'src/pugixml.cpp'
        AssimpPath = 'contrib/pugixml/src/pugixml.cpp'
        ExpectedPugixmlBlob = 'c63645b67fd59acd53a4cbc725fc99da570b15a0'
        ExpectedImportBlob = 'c63645b67fd59acd53a4cbc725fc99da570b15a0'
        ExpectedCurrentBlob = '6d6bd0edb210a00c63ae5e99de0cdade540fbc64'
        BaselineToImport = [pscustomobject]@{ Added = 0; Deleted = 0 }
        ImportToCurrent = [pscustomobject]@{ Added = 2; Deleted = 2 }
        ExpectedPostImportHistory = @('01231d0e6001f555c81dcfcc6c581fa5797ccac9')
    },
    [pscustomobject]@{
        Name = 'pugixml.hpp'
        PugixmlPath = 'src/pugixml.hpp'
        AssimpPath = 'contrib/pugixml/src/pugixml.hpp'
        ExpectedPugixmlBlob = '050df154cc77124dd30cea09ab2ece270c4bd4bc'
        ExpectedImportBlob = '050df154cc77124dd30cea09ab2ece270c4bd4bc'
        ExpectedCurrentBlob = 'fde6a4a862a6fc176a32d2f247b2c9a03fc1fbeb'
        BaselineToImport = [pscustomobject]@{ Added = 0; Deleted = 0 }
        ImportToCurrent = [pscustomobject]@{ Added = 2; Deleted = 2 }
        ExpectedPostImportHistory = @('01231d0e6001f555c81dcfcc6c581fa5797ccac9')
    }
)

try {
    foreach ($value in @($ExpectedPugixmlRevision, $ExpectedAssimpRevision, $ExpectedAssimpImportRevision, $ExpectedAssimpFollowUpRevision)) {
        if ($value -notmatch '^[0-9A-Fa-f]{40}$') {
            throw "Expected revision has an invalid format: $value"
        }
    }

    foreach ($directory in @($pugixmlRepositoryResolved, $assimpRepositoryResolved, $buildSourceResolved)) {
        if (-not (Test-Path -LiteralPath $directory -PathType Container)) {
            throw "Required source directory does not exist: $directory"
        }
    }

    if (Test-Path -LiteralPath $workingDirectoryResolved) {
        throw "WorkingDirectory already exists. Choose a new empty artifact path: $workingDirectoryResolved"
    }

    $pugixmlRemote = ((Invoke-GitOutput $pugixmlRepositoryResolved @('remote', 'get-url', 'origin') 'pugixml upstream remote lookup') | Select-Object -First 1).Trim()
    $assimpRemote = ((Invoke-GitOutput $assimpRepositoryResolved @('remote', 'get-url', 'origin') 'Assimp upstream remote lookup') | Select-Object -First 1).Trim()
    Test-ExpectedValue $issues 'pugixml origin remote' $pugixmlRemote $ExpectedPugixmlRemote
    Test-ExpectedValue $issues 'Assimp origin remote' $assimpRemote $ExpectedAssimpRemote

    $pugixmlTagRevision = Get-GitRevision $pugixmlRepositoryResolved ('{0}^{{}}' -f $ExpectedPugixmlTag) 'pugixml tag resolution'
    $assimpTagRevision = Get-GitRevision $assimpRepositoryResolved ('{0}^{{}}' -f $ExpectedAssimpTag) 'Assimp tag resolution'
    Test-ExpectedValue $issues 'pugixml tag revision' $pugixmlTagRevision $ExpectedPugixmlRevision
    Test-ExpectedValue $issues 'Assimp tag revision' $assimpTagRevision $ExpectedAssimpRevision
    [void](Get-GitRevision $assimpRepositoryResolved $ExpectedAssimpImportRevision 'Assimp import revision lookup')
    [void](Get-GitRevision $assimpRepositoryResolved $ExpectedAssimpFollowUpRevision 'Assimp follow-up revision lookup')

    New-Item -ItemType Directory -Path $workingDirectoryResolved -Force | Out-Null
    $pugixmlArchive = Join-Path $workingDirectoryResolved 'pugixml-v1.13.zip'
    $assimpImportArchive = Join-Path $workingDirectoryResolved 'assimp-import.zip'
    $assimpCurrentArchive = Join-Path $workingDirectoryResolved 'assimp-v5.4.2.zip'
    $pugixmlExtract = Join-Path $workingDirectoryResolved 'pugixml-v1.13'
    $assimpImportExtract = Join-Path $workingDirectoryResolved 'assimp-import'
    $assimpCurrentExtract = Join-Path $workingDirectoryResolved 'assimp-v5.4.2'

    Export-GitArchive $pugixmlRepositoryResolved $ExpectedPugixmlTag @($files | ForEach-Object { $_.PugixmlPath }) $pugixmlArchive $pugixmlExtract 'pugixml fixed-tag source export'
    Export-GitArchive $assimpRepositoryResolved $ExpectedAssimpImportRevision @($files | ForEach-Object { $_.AssimpPath }) $assimpImportArchive $assimpImportExtract 'Assimp import source export'
    Export-GitArchive $assimpRepositoryResolved $ExpectedAssimpTag @($files | ForEach-Object { $_.AssimpPath }) $assimpCurrentArchive $assimpCurrentExtract 'Assimp current-tag source export'

    foreach ($file in $files) {
        $pugixmlBlob = Get-GitBlob $pugixmlRepositoryResolved $ExpectedPugixmlTag $file.PugixmlPath "pugixml $($file.Name) blob"
        $importBlob = Get-GitBlob $assimpRepositoryResolved $ExpectedAssimpImportRevision $file.AssimpPath "Assimp import $($file.Name) blob"
        $currentBlob = Get-GitBlob $assimpRepositoryResolved $ExpectedAssimpTag $file.AssimpPath "Assimp current $($file.Name) blob"
        Test-ExpectedValue $issues "pugixml $($file.Name) blob" $pugixmlBlob $file.ExpectedPugixmlBlob
        Test-ExpectedValue $issues "Assimp import $($file.Name) blob" $importBlob $file.ExpectedImportBlob
        Test-ExpectedValue $issues "Assimp current $($file.Name) blob" $currentBlob $file.ExpectedCurrentBlob

        $pugixmlPath = Join-Path $pugixmlExtract $file.PugixmlPath
        $importPath = Join-Path $assimpImportExtract $file.AssimpPath
        $currentPath = Join-Path $assimpCurrentExtract $file.AssimpPath
        $buildPath = Join-Path $buildSourceResolved $file.Name
        $pugixmlRecord = Get-FileRecord $pugixmlPath "pugixml exported $($file.Name)"
        $importRecord = Get-FileRecord $importPath "Assimp import exported $($file.Name)"
        $currentRecord = Get-FileRecord $currentPath "Assimp current exported $($file.Name)"
        $buildRecord = Get-FileRecord $buildPath "Build $($file.Name)"
        Test-ExpectedValue $issues "pugixml exported $($file.Name) blob" $pugixmlRecord.GitBlobSha $file.ExpectedPugixmlBlob
        Test-ExpectedValue $issues "Assimp import exported $($file.Name) blob" $importRecord.GitBlobSha $file.ExpectedImportBlob
        Test-ExpectedValue $issues "Assimp current exported $($file.Name) blob" $currentRecord.GitBlobSha $file.ExpectedCurrentBlob
        Test-ExpectedValue $issues "Build $($file.Name) blob" $buildRecord.GitBlobSha $file.ExpectedCurrentBlob

        $pugixmlNormalizedPath = Join-Path $workingDirectoryResolved ('normalized-pugixml-{0}' -f $file.Name)
        $importNormalizedPath = Join-Path $workingDirectoryResolved ('normalized-assimp-import-{0}' -f $file.Name)
        $currentNormalizedPath = Join-Path $workingDirectoryResolved ('normalized-assimp-current-{0}' -f $file.Name)
        $pugixmlNormalized = Write-CrLfNormalizedCopy $pugixmlPath $pugixmlNormalizedPath
        $importNormalized = Write-CrLfNormalizedCopy $importPath $importNormalizedPath
        $currentNormalized = Write-CrLfNormalizedCopy $currentPath $currentNormalizedPath
        $baselineToImport = Get-Numstat $pugixmlNormalizedPath $importNormalizedPath "$($file.Name) CRLF-normalized fixed upstream baseline to Assimp import delta"
        $importToCurrent = Get-Numstat $importNormalizedPath $currentNormalizedPath "$($file.Name) CRLF-normalized Assimp import to current delta"
        Test-ExpectedNumstat $issues "$($file.Name) CRLF-normalized fixed upstream baseline to Assimp import delta" $baselineToImport $file.BaselineToImport.Added $file.BaselineToImport.Deleted
        Test-ExpectedNumstat $issues "$($file.Name) CRLF-normalized Assimp import to current delta" $importToCurrent $file.ImportToCurrent.Added $file.ImportToCurrent.Deleted

        $history = Get-PathHistory $assimpRepositoryResolved $ExpectedAssimpImportRevision $ExpectedAssimpTag $file.AssimpPath "Assimp post-import $($file.Name) history"
        Test-ExpectedSequence $issues "Assimp post-import $($file.Name) history" $history $file.ExpectedPostImportHistory
        $followUpDelta = Get-CommitNumstat $assimpRepositoryResolved $ExpectedAssimpFollowUpRevision $file.AssimpPath "Assimp follow-up $($file.Name) delta"
        Test-ExpectedNumstat $issues "Assimp follow-up $($file.Name) delta" $followUpDelta 2 2

        $sourceRecords.Add([pscustomobject]@{
            File = $file.Name
            PugixmlBaseline = $pugixmlRecord
            AssimpImport = $importRecord
            AssimpCurrent = $currentRecord
            BuildInput = $buildRecord
        })
        $deltaRecords.Add([pscustomobject]@{
            File = $file.Name
            PugixmlV113ToAssimpImportCrLfNormalized = $baselineToImport
            AssimpImportToV542CrLfNormalized = $importToCurrent
            NormalizedInputs = [pscustomobject]@{
                PugixmlBaseline = $pugixmlNormalized
                AssimpImport = $importNormalized
                AssimpCurrent = $currentNormalized
            }
        })
        $historyRecords.Add([pscustomobject]@{
            File = $file.Name
            Commits = @($history)
        })
        $followUpRecords.Add([pscustomobject]@{
            File = $file.Name
            Commit = $ExpectedAssimpFollowUpRevision.ToLowerInvariant()
            Delta = $followUpDelta
        })
    }

    $pugixmlRoot = Split-Path -Parent $buildSourceResolved
    $contribRoot = Split-Path -Parent $pugixmlRoot
    $extAssimpRoot = Split-Path -Parent $contribRoot
    $pugixmlCmakePath = Join-Path $pugixmlRoot 'CMakeLists.txt'
    $assimpCodeCmakePath = Join-Path (Join-Path $extAssimpRoot 'code') 'CMakeLists.txt'
    $buildConfigPath = Join-Path $buildSourceResolved 'pugiconfig.hpp'
    $buildHeaderPath = Join-Path $buildSourceResolved 'pugixml.hpp'
    $baselineConfigPath = Join-Path $pugixmlExtract 'src\pugiconfig.hpp'
    $importConfigPath = Join-Path $assimpImportExtract 'contrib\pugixml\src\pugiconfig.hpp'
    $currentConfigPath = Join-Path $assimpCurrentExtract 'contrib\pugixml\src\pugiconfig.hpp'

    foreach ($path in @($pugixmlCmakePath, $assimpCodeCmakePath, $buildConfigPath, $buildHeaderPath, $baselineConfigPath, $importConfigPath, $currentConfigPath)) {
        if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
            throw "Required compiler-input contract file does not exist: $path"
        }
    }

    $pugixmlCmakeText = Get-Content -LiteralPath $pugixmlCmakePath -Raw
    $assimpCodeCmakeText = Get-Content -LiteralPath $assimpCodeCmakePath -Raw
    $baselineConfigText = Get-Content -LiteralPath $baselineConfigPath -Raw
    $importConfigText = Get-Content -LiteralPath $importConfigPath -Raw
    $currentConfigText = Get-Content -LiteralPath $currentConfigPath -Raw
    $buildConfigText = Get-Content -LiteralPath $buildConfigPath -Raw
    $buildHeaderText = Get-Content -LiteralPath $buildHeaderPath -Raw

    $assimpPugixmlSourceGroupMatches = [regex]::IsMatch($assimpCodeCmakeText, '(?is)SET\s*\(\s*Pugixml_SRCS\s+[^)]*pugiconfig\.hpp\s+[^)]*pugixml\.hpp\s*\)')
    $pugixmlStandaloneSourceListMatches = [regex]::IsMatch($pugixmlCmakeText, '(?is)set\s*\(\s*HEADERS\s+src/pugixml\.hpp\s+src/pugiconfig\.hpp\s*\)\s*set\s*\(\s*SOURCES\s+src/pugixml\.cpp\s*\)')
    $pugixmlStandaloneVersionMetadataIs19 = [regex]::IsMatch($pugixmlCmakeText, '(?is)set_target_properties\s*\(\s*pugixml\s+PROPERTIES\s+VERSION\s+1\.9\s+SOVERSION\s+1\s*\)')
    $baselineHeaderOnlyIsCommented = [regex]::IsMatch($baselineConfigText, '(?m)^\s*//\s*#define\s+PUGIXML_HEADER_ONLY\s*$')
    $importHeaderOnlyIsEnabled = [regex]::IsMatch($importConfigText, '(?m)^\s*#define\s+PUGIXML_HEADER_ONLY\s*$')
    $currentHeaderOnlyIsEnabled = [regex]::IsMatch($currentConfigText, '(?m)^\s*#define\s+PUGIXML_HEADER_ONLY\s*$')
    $buildHeaderOnlyIsEnabled = [regex]::IsMatch($buildConfigText, '(?m)^\s*#define\s+PUGIXML_HEADER_ONLY\s*$')
    $headerIncludesImplementation = [regex]::IsMatch($buildHeaderText, '(?is)#if\s+defined\(PUGIXML_HEADER_ONLY\)\s*&&\s*!defined\(PUGIXML_SOURCE\).*?#\s*define\s+PUGIXML_SOURCE\s+"pugixml\.cpp".*?#\s*include\s+PUGIXML_SOURCE\s*#endif')
    $compilerInputContract = [ordered]@{
        AssimpPugixmlSourceGroupMatches = $assimpPugixmlSourceGroupMatches
        PugixmlStandaloneSourceListMatches = $pugixmlStandaloneSourceListMatches
        PugixmlStandaloneVersionMetadata = '1.9'
        PugixmlStandaloneVersionMetadataIsNotSourceIdentity = $pugixmlStandaloneVersionMetadataIs19
        BaselineHeaderOnlyIsCommented = $baselineHeaderOnlyIsCommented
        ImportHeaderOnlyIsEnabled = $importHeaderOnlyIsEnabled
        CurrentHeaderOnlyIsEnabled = $currentHeaderOnlyIsEnabled
        BuildHeaderOnlyIsEnabled = $buildHeaderOnlyIsEnabled
        HeaderIncludesPugixmlCpp = $headerIncludesImplementation
        EffectiveFiles = @('pugiconfig.hpp', 'pugixml.hpp', 'pugixml.cpp')
    }

    foreach ($property in @(
            'AssimpPugixmlSourceGroupMatches',
            'PugixmlStandaloneSourceListMatches',
            'PugixmlStandaloneVersionMetadataIsNotSourceIdentity',
            'BaselineHeaderOnlyIsCommented',
            'ImportHeaderOnlyIsEnabled',
            'CurrentHeaderOnlyIsEnabled',
            'BuildHeaderOnlyIsEnabled',
            'HeaderIncludesPugixmlCpp'
        )) {
        Test-ExpectedBoolean $issues "Compiler input contract $property" ([bool]$compilerInputContract[$property])
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
        PugixmlRepositoryDirectory = $pugixmlRepositoryResolved
        AssimpRepositoryDirectory = $assimpRepositoryResolved
        BuildSourceDirectory = $buildSourceResolved
        WorkingDirectory = $workingDirectoryResolved
        ExpectedPugixmlRemote = $ExpectedPugixmlRemote
        ExpectedAssimpRemote = $ExpectedAssimpRemote
        ExpectedPugixmlTag = $ExpectedPugixmlTag
        ExpectedPugixmlRevision = $ExpectedPugixmlRevision.ToLowerInvariant()
        ExpectedAssimpTag = $ExpectedAssimpTag
        ExpectedAssimpRevision = $ExpectedAssimpRevision.ToLowerInvariant()
        ExpectedAssimpImportRevision = $ExpectedAssimpImportRevision.ToLowerInvariant()
        ExpectedAssimpFollowUpRevision = $ExpectedAssimpFollowUpRevision.ToLowerInvariant()
    }
    Git = [ordered]@{
        PugixmlRemote = $pugixmlRemote
        AssimpRemote = $assimpRemote
        PugixmlTagRevision = $pugixmlTagRevision
        AssimpTagRevision = $assimpTagRevision
    }
    CompilerInputs = $compilerInputContract
    SourceIdentity = @($sourceRecords)
    Deltas = [ordered]@{
        CrossRepository = @($deltaRecords)
        AssimpPostImportHistory = @($historyRecords)
        AssimpFollowUpCommitDeltas = @($followUpRecords)
    }
    Issues = @($issues)
    ClaimLimit = 'fixed-upstream-v1.13-to-assimp-current-upstream-only-identity=False|fixed-upstream-v1.13-plus-CRLF-normalized-bounded-assimp-delta-to-build-source=True|header-only-compiler-input-chain=True|standalone-cmake-version-1.9-is-not-source-identity=True|upstream-release-status=False|upstream-history-complete=False|upstream-signature-or-owner-identity=False|third-party-notice-disposition=False|binary-reproducibility=False|distribution-approval=False'
}

$reportDirectory = Split-Path -Parent $reportPathResolved
New-Item -ItemType Directory -Path $reportDirectory -Force | Out-Null
$report | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $reportPathResolved -Encoding utf8

"AssimpPugixmlProvenance|$status|pugixml=$pugixmlTagRevision|assimp=$assimpTagRevision|files=$($sourceRecords.Count)|deltas=$($deltaRecords.Count)|issues=$($issues.Count)"
"Report|$reportPathResolved"

if ($status -ne 'Pass') {
    exit 1
}
