[CmdletBinding()]
param(
    [string]$KubaRepositoryDirectory = 'artifacts\dependency-candidates\assimp-kubazip-provenance-20260716\upstream-kuba-zip',
    [string]$AssimpRepositoryDirectory = 'artifacts\dependency-candidates\assimp-miniz-intake-20260716\official-assimp-full',
    [string]$BuildSourceFile = 'artifacts\o3d-clean\b\assimp\src\ext_assimp\contrib\zip\src\miniz.h',
    [string]$ClosureReportPath = 'artifacts\dependency-candidates\assimp-closure-20260716\assimp-closure.json',
    [string]$ReportPath = 'artifacts\dependency-candidates\assimp-miniz-provenance-20260716\miniz-provenance.json',
    [string]$ExpectedKubaRemote = 'https://github.com/kuba--/zip.git',
    [string]$ExpectedAssimpRemote = 'https://github.com/assimp/assimp.git',
    [string]$ExpectedKubaTag = 'v0.3.1',
    [string]$ExpectedKubaRevision = '550905d883b29f0b23e433fdb97f6299b628d4a9',
    [string]$ExpectedPullRequestRef = 'refs/remotes/origin/pr-5499',
    [string]$ExpectedPullRequestRevision = 'afef86519689ce64c992610e5ae3b76fdf222edf',
    [string]$ExpectedPullRequestBaselineRevision = '7d4a5c7af3951557717c0bbc9630f67e5eeb28e9',
    [string]$ExpectedPullRequestMacroRevision = '3ff60401a7172514ec026f6746b55ce766ad8433',
    [string]$ExpectedPullRequestHeaderRevision = '8dac9e7581f3baf8cb710432ebf69e1257a776aa',
    [string]$ExpectedAssimpImportRevision = '83d7216726726a07e9e40f86cc2322b22fec11fa',
    [string]$ExpectedAssimpCurrentChangeRevision = '0d546b3d2edb5ae737c11971b26233f5a5316a43',
    [string]$ExpectedAssimpTag = 'v5.4.2',
    [string]$ExpectedAssimpRevision = 'ddb74c2bbdee1565dda667e85f0c82a0588c8053'
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$minizPath = 'contrib/zip/src/miniz.h'
$kubaMinizPath = 'src/miniz.h'
$expectedKubaBlob = 'cd86483184cfba1dd33c4db7c718965e20926c7a'
$expectedMacroBlob = 'ee9a2899bda9d3fee94fffb308b40c09af1fa36b'
$expectedImportBlob = 'f3b3456bdb93809f6b5b0b3ebba0e7e3f5a24a19'
$expectedCurrentBlob = 'ad5850ce17d9449cc9356486dff73c8a566e1c46'
$expectedBuildSha256 = 'bba5c196415bda01b460d2dd8be779189bb228c2802b5b61dd20eae5c3921b06'
$expectedClosureSha256 = 'ccc0dd6eef59502e6d9aa1774b21ce1e2038d8448baf6d28bdf878d868a5a67c'

function Resolve-WorkspacePath([string]$Path) {
    if ([System.IO.Path]::IsPathRooted($Path)) {
        return [System.IO.Path]::GetFullPath($Path)
    }

    return [System.IO.Path]::GetFullPath((Join-Path $repoRoot $Path))
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

function Get-GitNumstat(
    [string]$RepositoryDirectory,
    [string]$LeftRevision,
    [string]$RightRevision,
    [string]$RelativePath,
    [string]$Description
) {
    $output = Invoke-GitOutput $RepositoryDirectory @('-c', 'core.autocrlf=false', 'diff', '--numstat', $LeftRevision, $RightRevision, '--', $RelativePath) $Description @(0, 1)
    $records = @()
    foreach ($line in $output) {
        if ($line -match '^(?<Added>\d+|-)\t(?<Deleted>\d+|-)\t') {
            $records += [pscustomobject]@{
                Added = $matches['Added']
                Deleted = $matches['Deleted']
            }
        }
    }

    if ($records.Count -eq 0) {
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

function Test-GitAncestor([System.Collections.Generic.List[string]]$Issues, [string]$RepositoryDirectory, [string]$Ancestor, [string]$Descendant, [string]$Label) {
    & git -C $RepositoryDirectory merge-base --is-ancestor $Ancestor $Descendant 2>$null
    if ($LASTEXITCODE -ne 0) {
        $Issues.Add("$Label expected '$Ancestor' to be an ancestor of '$Descendant'.")
    }
}

$issues = [System.Collections.Generic.List[string]]::new()
$kubaRepositoryResolved = Resolve-WorkspacePath $KubaRepositoryDirectory
$assimpRepositoryResolved = Resolve-WorkspacePath $AssimpRepositoryDirectory
$buildSourceResolved = Resolve-WorkspacePath $BuildSourceFile
$closureReportResolved = Resolve-WorkspacePath $ClosureReportPath
$reportPathResolved = Resolve-WorkspacePath $ReportPath
$gitEvidence = [ordered]@{}
$sourceIdentity = [ordered]@{}
$deltas = [ordered]@{}
$compilerInput = [ordered]@{}

try {
    foreach ($value in @(
            $ExpectedKubaRevision,
            $ExpectedPullRequestRevision,
            $ExpectedPullRequestBaselineRevision,
            $ExpectedPullRequestMacroRevision,
            $ExpectedPullRequestHeaderRevision,
            $ExpectedAssimpImportRevision,
            $ExpectedAssimpCurrentChangeRevision,
            $ExpectedAssimpRevision
        )) {
        if ($value -notmatch '^[0-9A-Fa-f]{40}$') {
            throw "Expected revision has an invalid format: $value"
        }
    }

    foreach ($path in @($kubaRepositoryResolved, $assimpRepositoryResolved)) {
        if (-not (Test-Path -LiteralPath $path -PathType Container)) {
            throw "Required repository directory does not exist: $path"
        }
    }

    foreach ($path in @($buildSourceResolved, $closureReportResolved)) {
        if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
            throw "Required evidence file does not exist: $path"
        }
    }

    $kubaRemote = ((Invoke-GitOutput $kubaRepositoryResolved @('remote', 'get-url', 'origin') 'Kuba upstream remote lookup') | Select-Object -First 1).Trim()
    $assimpRemote = ((Invoke-GitOutput $assimpRepositoryResolved @('remote', 'get-url', 'origin') 'Assimp upstream remote lookup') | Select-Object -First 1).Trim()
    Test-ExpectedValue $issues 'Kuba origin remote' $kubaRemote $ExpectedKubaRemote
    Test-ExpectedValue $issues 'Assimp origin remote' $assimpRemote $ExpectedAssimpRemote

    $kubaTagRevision = Get-GitRevision $kubaRepositoryResolved $ExpectedKubaTag 'Kuba tag resolution'
    $assimpTagRevision = Get-GitRevision $assimpRepositoryResolved $ExpectedAssimpTag 'Assimp tag resolution'
    $pullRequestRevision = Get-GitRevision $assimpRepositoryResolved $ExpectedPullRequestRef 'Assimp pull-request ref resolution'
    Test-ExpectedValue $issues 'Kuba tag revision' $kubaTagRevision $ExpectedKubaRevision
    Test-ExpectedValue $issues 'Assimp tag revision' $assimpTagRevision $ExpectedAssimpRevision
    Test-ExpectedValue $issues 'Assimp pull-request head revision' $pullRequestRevision $ExpectedPullRequestRevision

    foreach ($revision in @(
            $ExpectedPullRequestBaselineRevision,
            $ExpectedPullRequestMacroRevision,
            $ExpectedPullRequestHeaderRevision,
            $ExpectedAssimpImportRevision,
            $ExpectedAssimpCurrentChangeRevision
        )) {
        [void](Get-GitRevision $assimpRepositoryResolved $revision "Assimp fixed revision lookup $revision")
    }

    $kubaBlob = Get-GitBlob $kubaRepositoryResolved $ExpectedKubaTag $kubaMinizPath 'Kuba miniz baseline blob'
    $prBaselineBlob = Get-GitBlob $assimpRepositoryResolved $ExpectedPullRequestBaselineRevision $minizPath 'Assimp pull-request baseline miniz blob'
    $prMacroBlob = Get-GitBlob $assimpRepositoryResolved $ExpectedPullRequestMacroRevision $minizPath 'Assimp pull-request macro miniz blob'
    $prHeaderBlob = Get-GitBlob $assimpRepositoryResolved $ExpectedPullRequestHeaderRevision $minizPath 'Assimp pull-request header miniz blob'
    $prHeadBlob = Get-GitBlob $assimpRepositoryResolved $ExpectedPullRequestRef $minizPath 'Assimp pull-request head miniz blob'
    $assimpImportBlob = Get-GitBlob $assimpRepositoryResolved $ExpectedAssimpImportRevision $minizPath 'Assimp merge miniz blob'
    $assimpCurrentChangeBlob = Get-GitBlob $assimpRepositoryResolved $ExpectedAssimpCurrentChangeRevision $minizPath 'Assimp current-change miniz blob'
    $assimpTagBlob = Get-GitBlob $assimpRepositoryResolved $ExpectedAssimpTag $minizPath 'Assimp tag miniz blob'
    $buildBlob = Get-FileGitBlob $buildSourceResolved 'Clean build miniz input blob'
    $buildSha256 = (Get-FileHash -LiteralPath $buildSourceResolved -Algorithm SHA256).Hash.ToLowerInvariant()

    Test-ExpectedValue $issues 'Kuba miniz baseline blob' $kubaBlob $expectedKubaBlob
    Test-ExpectedValue $issues 'Assimp pull-request baseline miniz blob' $prBaselineBlob $expectedKubaBlob
    Test-ExpectedValue $issues 'Assimp pull-request macro miniz blob' $prMacroBlob $expectedMacroBlob
    Test-ExpectedValue $issues 'Assimp pull-request header miniz blob' $prHeaderBlob $expectedImportBlob
    Test-ExpectedValue $issues 'Assimp pull-request head miniz blob' $prHeadBlob $expectedImportBlob
    Test-ExpectedValue $issues 'Assimp merge miniz blob' $assimpImportBlob $expectedImportBlob
    Test-ExpectedValue $issues 'Assimp current-change miniz blob' $assimpCurrentChangeBlob $expectedCurrentBlob
    Test-ExpectedValue $issues 'Assimp tag miniz blob' $assimpTagBlob $expectedCurrentBlob
    Test-ExpectedValue $issues 'Clean build miniz input blob' $buildBlob $expectedCurrentBlob
    Test-ExpectedValue $issues 'Clean build miniz input SHA-256' $buildSha256 $expectedBuildSha256

    Test-GitAncestor $issues $assimpRepositoryResolved $ExpectedPullRequestBaselineRevision $ExpectedPullRequestRevision 'Pull-request baseline ancestry'
    Test-GitAncestor $issues $assimpRepositoryResolved $ExpectedPullRequestHeaderRevision $ExpectedPullRequestRevision 'Pull-request header-update ancestry'
    Test-GitAncestor $issues $assimpRepositoryResolved $ExpectedAssimpImportRevision $ExpectedAssimpCurrentChangeRevision 'Assimp merge-to-current-change ancestry'
    Test-GitAncestor $issues $assimpRepositoryResolved $ExpectedAssimpCurrentChangeRevision $ExpectedAssimpTag 'Assimp current-change-to-tag ancestry'

    $deltas['PullRequestBaselineToMacro'] = Get-GitNumstat $assimpRepositoryResolved $ExpectedPullRequestBaselineRevision $ExpectedPullRequestMacroRevision $minizPath 'Pull-request baseline-to-macro delta'
    $deltas['PullRequestMacroToHeader'] = Get-GitNumstat $assimpRepositoryResolved $ExpectedPullRequestMacroRevision $ExpectedPullRequestHeaderRevision $minizPath 'Pull-request macro-to-header delta'
    $deltas['PullRequestHeaderToHead'] = Get-GitNumstat $assimpRepositoryResolved $ExpectedPullRequestHeaderRevision $ExpectedPullRequestRevision $minizPath 'Pull-request header-to-head delta'
    $deltas['PullRequestHeadToMerge'] = Get-GitNumstat $assimpRepositoryResolved $ExpectedPullRequestRevision $ExpectedAssimpImportRevision $minizPath 'Pull-request head-to-merge content delta'
    $deltas['MergeToCurrentChange'] = Get-GitNumstat $assimpRepositoryResolved $ExpectedAssimpImportRevision $ExpectedAssimpCurrentChangeRevision $minizPath 'Assimp merge-to-current-change delta'
    Test-ExpectedNumstat $issues 'Pull-request baseline-to-macro delta' $deltas['PullRequestBaselineToMacro'] 2 0
    Test-ExpectedNumstat $issues 'Pull-request macro-to-header delta' $deltas['PullRequestMacroToHeader'] 4 1
    Test-ExpectedNumstat $issues 'Pull-request header-to-head delta' $deltas['PullRequestHeaderToHead'] 0 0
    Test-ExpectedNumstat $issues 'Pull-request head-to-merge content delta' $deltas['PullRequestHeadToMerge'] 0 0
    Test-ExpectedNumstat $issues 'Assimp merge-to-current-change delta' $deltas['MergeToCurrentChange'] 1 1
    $postMergeHistory = Get-PathHistory $assimpRepositoryResolved $ExpectedAssimpImportRevision $ExpectedAssimpTag $minizPath 'Assimp post-merge miniz history'
    Test-ExpectedSequence $issues 'Assimp post-merge miniz history' $postMergeHistory @($ExpectedAssimpCurrentChangeRevision)
    $deltas['PostMergeHistory'] = @($postMergeHistory)

    $cmakePath = Join-Path (Split-Path -Parent $buildSourceResolved) '..\CMakeLists.txt'
    $cmakePath = [System.IO.Path]::GetFullPath($cmakePath)
    if (-not (Test-Path -LiteralPath $cmakePath -PathType Leaf)) {
        $issues.Add("CMakeLists.txt does not exist: $cmakePath")
    }
    else {
        $cmakeText = Get-Content -LiteralPath $cmakePath -Raw
        $compilerInput['CMakeSourceListMatches'] = $cmakeText -match 'set\s*\(\s*SRC\s+src/miniz\.h\s+src/zip\.h\s+src/zip\.c\s*\)'
        if (-not $compilerInput['CMakeSourceListMatches']) {
            $issues.Add('CMakeLists.txt does not declare miniz.h, zip.h, and zip.c as the expected zip source group.')
        }
    }

    $closure = Get-Content -LiteralPath $closureReportResolved -Raw | ConvertFrom-Json
    $minizComponent = @($closure.components | Where-Object { $_.key -eq 'miniz' })
    if ($minizComponent.Count -ne 1) {
        $issues.Add("Compiler-read closure expected one miniz component but found $($minizComponent.Count).")
    }
    else {
        $component = $minizComponent[0]
        $compilerInput['ClosureUsedFileCount'] = [int]$component.usedFileCount
        $compilerInput['ClosureUsedFileSetSha256'] = $component.usedFileSetSha256
        $compilerInput['ClosureUsedFiles'] = @($component.usedFiles)
        if ($component.usedFileCount -ne 1 -or @($component.usedFiles).Count -ne 1 -or $component.usedFiles[0] -ne $minizPath) {
            $issues.Add('Compiler-read closure does not contain only contrib/zip/src/miniz.h for miniz.')
        }

        Test-ExpectedValue $issues 'Compiler-read miniz closure SHA-256' $component.usedFileSetSha256 $expectedClosureSha256
    }

    $gitEvidence = [ordered]@{
        KubaRemote = $kubaRemote
        AssimpRemote = $assimpRemote
        KubaTagRevision = $kubaTagRevision
        AssimpTagRevision = $assimpTagRevision
        PullRequestRef = $ExpectedPullRequestRef
        PullRequestRevision = $pullRequestRevision
    }
    $sourceIdentity = [ordered]@{
        KubaV031 = $kubaBlob
        PullRequestBaseline = $prBaselineBlob
        PullRequestMacro = $prMacroBlob
        PullRequestHeader = $prHeaderBlob
        PullRequestHead = $prHeadBlob
        AssimpMerge = $assimpImportBlob
        AssimpCurrentChange = $assimpCurrentChangeBlob
        AssimpV542 = $assimpTagBlob
        BuildInput = $buildBlob
        BuildInputSha256 = $buildSha256
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
        BuildSourceFile = $buildSourceResolved
        ClosureReportPath = $closureReportResolved
        ExpectedKubaTag = $ExpectedKubaTag
        ExpectedKubaRevision = $ExpectedKubaRevision.ToLowerInvariant()
        ExpectedPullRequestRef = $ExpectedPullRequestRef
        ExpectedPullRequestRevision = $ExpectedPullRequestRevision.ToLowerInvariant()
        ExpectedAssimpTag = $ExpectedAssimpTag
        ExpectedAssimpRevision = $ExpectedAssimpRevision.ToLowerInvariant()
    }
    Git = $gitEvidence
    CompilerInput = $compilerInput
    SourceIdentity = $sourceIdentity
    Deltas = $deltas
    Issues = @($issues)
    ClaimLimit = 'kuba-v0.3.1-to-assimp-pr-content-identity=True|bounded-assimp-pr-and-post-merge-delta-to-build-source=True|pull-request-branch-ancestry-to-merge=False|independent-richgel-miniz-source-identity=False|complete-upstream-miniz-history=False|notice-or-license-disposition=False|binary-reproducibility=False|distribution-approval=False'
}

$reportDirectory = Split-Path -Parent $reportPathResolved
New-Item -ItemType Directory -Path $reportDirectory -Force | Out-Null
$report | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $reportPathResolved -Encoding utf8

"AssimpMinizProvenance|$status|kuba=$($gitEvidence.KubaTagRevision)|assimp=$($gitEvidence.AssimpTagRevision)|blobs=$($sourceIdentity.Count)|issues=$($issues.Count)"
"Report|$reportPathResolved"

if ($status -ne 'Pass') {
    exit 1
}
