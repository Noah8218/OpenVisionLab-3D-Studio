[CmdletBinding()]
param(
    [string]$KubaRepositoryDirectory = 'artifacts\dependency-candidates\assimp-kubazip-provenance-20260716\upstream-kuba-zip',
    [string]$AssimpRepositoryDirectory = 'artifacts\dependency-candidates\assimp-kubazip-provenance-20260716\official-assimp',
    [string]$BuildSourceDirectory = 'artifacts\o3d-clean\b\assimp\src\ext_assimp\contrib\zip\src',
    [string]$WorkingDirectory = 'artifacts\dependency-candidates\assimp-kubazip-provenance-20260716\kubazip-provenance-verification-work',
    [string]$ReportPath = 'artifacts\dependency-candidates\assimp-kubazip-provenance-20260716\kubazip-provenance.json',
    [string]$ExpectedKubaRemote = 'https://github.com/kuba--/zip.git',
    [string]$ExpectedAssimpRemote = 'https://github.com/assimp/assimp.git',
    [string]$ExpectedKubaTag = 'v0.3.1',
    [string]$ExpectedKubaRevision = '550905d883b29f0b23e433fdb97f6299b628d4a9',
    [string]$ExpectedAssimpTag = 'v5.4.2',
    [string]$ExpectedAssimpRevision = 'ddb74c2bbdee1565dda667e85f0c82a0588c8053',
    [string]$ExpectedAssimpImportRevision = '83d7216726726a07e9e40f86cc2322b22fec11fa'
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
    $spec = '{0}:{1}' -f $Revision, $RelativePath
    return Get-GitRevision $RepositoryDirectory $spec $Description
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

$issues = [System.Collections.Generic.List[string]]::new()
$sourceRecords = [System.Collections.Generic.List[object]]::new()
$deltaRecords = [System.Collections.Generic.List[object]]::new()
$historyRecords = [System.Collections.Generic.List[object]]::new()
$commitDeltaRecords = [System.Collections.Generic.List[object]]::new()
$kubaRemote = $null
$assimpRemote = $null
$kubaTagRevision = $null
$assimpTagRevision = $null
$cmakeSourceListMatches = $false
$kubaRepositoryResolved = Resolve-WorkspacePath $KubaRepositoryDirectory
$assimpRepositoryResolved = Resolve-WorkspacePath $AssimpRepositoryDirectory
$buildSourceResolved = Resolve-WorkspacePath $BuildSourceDirectory
$workingDirectoryResolved = Resolve-WorkspacePath $WorkingDirectory
$reportPathResolved = Resolve-WorkspacePath $ReportPath

$files = @(
    [pscustomobject]@{
        Name = 'miniz.h'
        KubaPath = 'src/miniz.h'
        AssimpPath = 'contrib/zip/src/miniz.h'
        ExpectedKubaBlob = 'cd86483184cfba1dd33c4db7c718965e20926c7a'
        ExpectedImportBlob = 'f3b3456bdb93809f6b5b0b3ebba0e7e3f5a24a19'
        ExpectedCurrentBlob = 'ad5850ce17d9449cc9356486dff73c8a566e1c46'
        BaselineToImport = [pscustomobject]@{ Added = 6; Deleted = 1 }
        ImportToCurrent = [pscustomobject]@{ Added = 1; Deleted = 1 }
        ExpectedPostImportHistory = @('0d546b3d2edb5ae737c11971b26233f5a5316a43')
    },
    [pscustomobject]@{
        Name = 'zip.c'
        KubaPath = 'src/zip.c'
        AssimpPath = 'contrib/zip/src/zip.c'
        ExpectedKubaBlob = 'a35f86e34216003266f0497b108b5adfb23ef114'
        ExpectedImportBlob = '5b8955dba3ee65c52915fc4fe636d757200db124'
        ExpectedCurrentBlob = 'deef56178b9139869e559cd2bb9d3d4182a0a8c4'
        BaselineToImport = [pscustomobject]@{ Added = 2; Deleted = 1 }
        ImportToCurrent = [pscustomobject]@{ Added = 7; Deleted = 3 }
        ExpectedPostImportHistory = @(
            'dd1474e2801c6e0261e8a2d88fbe189789f76d14',
            '8231d99a8547574bd9bc984c7c15702d71cf77e4'
        )
    },
    [pscustomobject]@{
        Name = 'zip.h'
        KubaPath = 'src/zip.h'
        AssimpPath = 'contrib/zip/src/zip.h'
        ExpectedKubaBlob = '324904ca6c8d803c50f0365394a928fbddfba5b8'
        ExpectedImportBlob = '324904ca6c8d803c50f0365394a928fbddfba5b8'
        ExpectedCurrentBlob = '324904ca6c8d803c50f0365394a928fbddfba5b8'
        BaselineToImport = [pscustomobject]@{ Added = 0; Deleted = 0 }
        ImportToCurrent = [pscustomobject]@{ Added = 0; Deleted = 0 }
        ExpectedPostImportHistory = @()
    }
)

$expectedCommitDeltas = @(
    [pscustomobject]@{
        Commit = '0d546b3d2edb5ae737c11971b26233f5a5316a43'
        Path = 'contrib/zip/src/miniz.h'
        Added = 1
        Deleted = 1
    },
    [pscustomobject]@{
        Commit = '8231d99a8547574bd9bc984c7c15702d71cf77e4'
        Path = 'contrib/zip/src/zip.c'
        Added = 1
        Deleted = 1
    },
    [pscustomobject]@{
        Commit = 'dd1474e2801c6e0261e8a2d88fbe189789f76d14'
        Path = 'contrib/zip/src/zip.c'
        Added = 6
        Deleted = 2
    }
)

try {
    foreach ($value in @($ExpectedKubaRevision, $ExpectedAssimpRevision, $ExpectedAssimpImportRevision)) {
        if ($value -notmatch '^[0-9A-Fa-f]{40}$') {
            throw "Expected revision has an invalid format: $value"
        }
    }

    foreach ($directory in @($kubaRepositoryResolved, $assimpRepositoryResolved, $buildSourceResolved)) {
        if (-not (Test-Path -LiteralPath $directory -PathType Container)) {
            throw "Required source directory does not exist: $directory"
        }
    }

    if (Test-Path -LiteralPath $workingDirectoryResolved) {
        throw "WorkingDirectory already exists. Choose a new empty artifact path: $workingDirectoryResolved"
    }

    $kubaRemote = ((Invoke-GitOutput $kubaRepositoryResolved @('remote', 'get-url', 'origin') 'Kuba upstream remote lookup') | Select-Object -First 1).Trim()
    $assimpRemote = ((Invoke-GitOutput $assimpRepositoryResolved @('remote', 'get-url', 'origin') 'Assimp upstream remote lookup') | Select-Object -First 1).Trim()
    Test-ExpectedValue $issues 'Kuba origin remote' $kubaRemote $ExpectedKubaRemote
    Test-ExpectedValue $issues 'Assimp origin remote' $assimpRemote $ExpectedAssimpRemote

    $kubaTagRevision = Get-GitRevision $kubaRepositoryResolved $ExpectedKubaTag 'Kuba tag resolution'
    $assimpTagRevision = Get-GitRevision $assimpRepositoryResolved $ExpectedAssimpTag 'Assimp tag resolution'
    Test-ExpectedValue $issues 'Kuba tag revision' $kubaTagRevision $ExpectedKubaRevision
    Test-ExpectedValue $issues 'Assimp tag revision' $assimpTagRevision $ExpectedAssimpRevision
    [void](Get-GitRevision $assimpRepositoryResolved $ExpectedAssimpImportRevision 'Assimp import revision lookup')

    New-Item -ItemType Directory -Path $workingDirectoryResolved -Force | Out-Null
    $kubaArchive = Join-Path $workingDirectoryResolved 'kuba-v0.3.1.zip'
    $assimpImportArchive = Join-Path $workingDirectoryResolved 'assimp-import.zip'
    $assimpCurrentArchive = Join-Path $workingDirectoryResolved 'assimp-v5.4.2.zip'
    $kubaExtract = Join-Path $workingDirectoryResolved 'kuba-v0.3.1'
    $assimpImportExtract = Join-Path $workingDirectoryResolved 'assimp-import'
    $assimpCurrentExtract = Join-Path $workingDirectoryResolved 'assimp-v5.4.2'

    Export-GitArchive $kubaRepositoryResolved $ExpectedKubaTag @($files | ForEach-Object { $_.KubaPath }) $kubaArchive $kubaExtract 'Kuba fixed-tag source export'
    Export-GitArchive $assimpRepositoryResolved $ExpectedAssimpImportRevision @($files | ForEach-Object { $_.AssimpPath }) $assimpImportArchive $assimpImportExtract 'Assimp import source export'
    Export-GitArchive $assimpRepositoryResolved $ExpectedAssimpTag @($files | ForEach-Object { $_.AssimpPath }) $assimpCurrentArchive $assimpCurrentExtract 'Assimp current-tag source export'

    foreach ($file in $files) {
        $kubaBlob = Get-GitBlob $kubaRepositoryResolved $ExpectedKubaTag $file.KubaPath "Kuba $($file.Name) blob"
        $importBlob = Get-GitBlob $assimpRepositoryResolved $ExpectedAssimpImportRevision $file.AssimpPath "Assimp import $($file.Name) blob"
        $currentBlob = Get-GitBlob $assimpRepositoryResolved $ExpectedAssimpTag $file.AssimpPath "Assimp current $($file.Name) blob"
        Test-ExpectedValue $issues "Kuba $($file.Name) blob" $kubaBlob $file.ExpectedKubaBlob
        Test-ExpectedValue $issues "Assimp import $($file.Name) blob" $importBlob $file.ExpectedImportBlob
        Test-ExpectedValue $issues "Assimp current $($file.Name) blob" $currentBlob $file.ExpectedCurrentBlob

        $kubaPath = Join-Path $kubaExtract $file.KubaPath
        $importPath = Join-Path $assimpImportExtract $file.AssimpPath
        $currentPath = Join-Path $assimpCurrentExtract $file.AssimpPath
        $buildPath = Join-Path $buildSourceResolved $file.Name
        $kubaRecord = Get-FileRecord $kubaPath "Kuba exported $($file.Name)"
        $importRecord = Get-FileRecord $importPath "Assimp import exported $($file.Name)"
        $currentRecord = Get-FileRecord $currentPath "Assimp current exported $($file.Name)"
        $buildRecord = Get-FileRecord $buildPath "Build $($file.Name)"
        Test-ExpectedValue $issues "Kuba exported $($file.Name) blob" $kubaRecord.GitBlobSha $file.ExpectedKubaBlob
        Test-ExpectedValue $issues "Assimp import exported $($file.Name) blob" $importRecord.GitBlobSha $file.ExpectedImportBlob
        Test-ExpectedValue $issues "Assimp current exported $($file.Name) blob" $currentRecord.GitBlobSha $file.ExpectedCurrentBlob
        Test-ExpectedValue $issues "Build $($file.Name) blob" $buildRecord.GitBlobSha $file.ExpectedCurrentBlob

        $kubaNormalizedPath = Join-Path $workingDirectoryResolved ('normalized-kuba-{0}' -f $file.Name)
        $importNormalizedPath = Join-Path $workingDirectoryResolved ('normalized-assimp-import-{0}' -f $file.Name)
        $currentNormalizedPath = Join-Path $workingDirectoryResolved ('normalized-assimp-current-{0}' -f $file.Name)
        $kubaNormalized = Write-CrLfNormalizedCopy $kubaPath $kubaNormalizedPath
        $importNormalized = Write-CrLfNormalizedCopy $importPath $importNormalizedPath
        $currentNormalized = Write-CrLfNormalizedCopy $currentPath $currentNormalizedPath
        $baselineToImport = Get-Numstat $kubaNormalizedPath $importNormalizedPath "$($file.Name) CRLF-normalized fixed upstream baseline to Assimp import delta"
        $importToCurrent = Get-Numstat $importNormalizedPath $currentNormalizedPath "$($file.Name) CRLF-normalized Assimp import to current delta"
        Test-ExpectedNumstat $issues "$($file.Name) CRLF-normalized fixed upstream baseline to Assimp import delta" $baselineToImport $file.BaselineToImport.Added $file.BaselineToImport.Deleted
        Test-ExpectedNumstat $issues "$($file.Name) CRLF-normalized Assimp import to current delta" $importToCurrent $file.ImportToCurrent.Added $file.ImportToCurrent.Deleted

        $history = Get-PathHistory $assimpRepositoryResolved $ExpectedAssimpImportRevision $ExpectedAssimpTag $file.AssimpPath "Assimp post-import $($file.Name) history"
        Test-ExpectedSequence $issues "Assimp post-import $($file.Name) history" $history $file.ExpectedPostImportHistory

        $sourceRecords.Add([pscustomobject]@{
            File = $file.Name
            KubaBaseline = $kubaRecord
            AssimpImport = $importRecord
            AssimpCurrent = $currentRecord
            BuildInput = $buildRecord
        })
        $deltaRecords.Add([pscustomobject]@{
            File = $file.Name
            KubaV031ToAssimpImportCrLfNormalized = $baselineToImport
            AssimpImportToV542CrLfNormalized = $importToCurrent
            NormalizedInputs = [pscustomobject]@{
                KubaBaseline = $kubaNormalized
                AssimpImport = $importNormalized
                AssimpCurrent = $currentNormalized
            }
        })
        $historyRecords.Add([pscustomobject]@{
            File = $file.Name
            Commits = @($history)
        })
    }

    foreach ($expectedCommitDelta in $expectedCommitDeltas) {
        [void](Get-GitRevision $assimpRepositoryResolved $expectedCommitDelta.Commit "Assimp follow-up commit lookup")
        $actualCommitDelta = Get-CommitNumstat $assimpRepositoryResolved $expectedCommitDelta.Commit $expectedCommitDelta.Path "Assimp follow-up $($expectedCommitDelta.Commit) delta"
        Test-ExpectedNumstat $issues "Assimp follow-up $($expectedCommitDelta.Commit) delta" $actualCommitDelta $expectedCommitDelta.Added $expectedCommitDelta.Deleted
        $commitDeltaRecords.Add([pscustomobject]@{
            Commit = $expectedCommitDelta.Commit
            Path = $expectedCommitDelta.Path
            Delta = $actualCommitDelta
        })
    }

    $cmakeListsPath = Join-Path (Split-Path -Parent $buildSourceResolved) 'CMakeLists.txt'
    if (-not (Test-Path -LiteralPath $cmakeListsPath -PathType Leaf)) {
        $issues.Add("CMakeLists.txt does not exist: $cmakeListsPath")
    }
    else {
        $cmakeText = Get-Content -LiteralPath $cmakeListsPath -Raw
        $cmakeSourceListMatches = $cmakeText -match 'set\s*\(\s*SRC\s+src/miniz\.h\s+src/zip\.h\s+src/zip\.c\s*\)'
        if (-not $cmakeSourceListMatches) {
            $issues.Add('CMakeLists.txt does not declare the expected three zip compiler inputs in order.')
        }
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
        KubaRepositoryDirectory = $kubaRepositoryResolved
        AssimpRepositoryDirectory = $assimpRepositoryResolved
        BuildSourceDirectory = $buildSourceResolved
        WorkingDirectory = $workingDirectoryResolved
        ExpectedKubaRemote = $ExpectedKubaRemote
        ExpectedAssimpRemote = $ExpectedAssimpRemote
        ExpectedKubaTag = $ExpectedKubaTag
        ExpectedKubaRevision = $ExpectedKubaRevision.ToLowerInvariant()
        ExpectedAssimpTag = $ExpectedAssimpTag
        ExpectedAssimpRevision = $ExpectedAssimpRevision.ToLowerInvariant()
        ExpectedAssimpImportRevision = $ExpectedAssimpImportRevision.ToLowerInvariant()
    }
    Git = [ordered]@{
        KubaRemote = $kubaRemote
        AssimpRemote = $assimpRemote
        KubaTagRevision = $kubaTagRevision
        AssimpTagRevision = $assimpTagRevision
    }
    CompilerInputs = [ordered]@{
        CMakeSourceListMatches = $cmakeSourceListMatches
        Files = @('src/miniz.h', 'src/zip.h', 'src/zip.c')
    }
    SourceIdentity = @($sourceRecords)
    Deltas = [ordered]@{
        CrossRepository = @($deltaRecords)
        AssimpPostImportHistory = @($historyRecords)
        AssimpFollowUpCommitDeltas = @($commitDeltaRecords)
    }
    Issues = @($issues)
    ClaimLimit = 'fixed-upstream-v0.3.1-to-assimp-current-upstream-only-identity=False|fixed-upstream-v0.3.1-plus-CRLF-normalized-bounded-assimp-delta-to-build-source=True|upstream-release-status=False|upstream-history-complete=False|upstream-signature-or-owner-identity=False|third-party-notice-disposition=False|binary-reproducibility=False|distribution-approval=False'
}

$reportDirectory = Split-Path -Parent $reportPathResolved
New-Item -ItemType Directory -Path $reportDirectory -Force | Out-Null
$report | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $reportPathResolved -Encoding utf8

"AssimpKubaZipProvenance|$status|kuba=$kubaTagRevision|assimp=$assimpTagRevision|files=$($sourceRecords.Count)|deltas=$($deltaRecords.Count)|issues=$($issues.Count)"
"Report|$reportPathResolved"

if ($status -ne 'Pass') {
    exit 1
}
