[CmdletBinding()]
param(
    [string]$UpstreamRepositoryDirectory = 'artifacts\dependency-candidates\assimp-openddl-intake-20260716\official-openddl-parser',
    [string]$AssimpRepositoryDirectory = 'artifacts\dependency-candidates\assimp-miniz-intake-20260716\official-assimp-full',
    [string]$BuildSourceDirectory = 'artifacts\o3d-clean\b\assimp\src\ext_assimp\contrib\openddlparser',
    [string]$ClosureReportPath = 'artifacts\dependency-candidates\assimp-closure-20260716\assimp-closure.json',
    [string]$ReportPath = 'artifacts\dependency-candidates\assimp-openddl-provenance-20260716\openddl-provenance.json',
    [string]$ExpectedUpstreamRemote = 'https://github.com/kimkulling/openddl-parser.git',
    [string]$ExpectedAssimpRemote = 'https://github.com/assimp/assimp.git',
    [string]$ExpectedUpstreamTag = 'v0.5.1',
    [string]$ExpectedUpstreamRevision = 'ffad343385f550b933c7e498e9bd0a861605102c',
    [string]$ExpectedAssimpBaselineRevision = 'bc7ef58b4947a01f4f7163b47b96ca273473d7eb',
    [string]$ExpectedAssimpCommonChangeRevision = '7cbf4c4136bf9884fad408e6e388b10ba3ace635',
    [string]$ExpectedAssimpParserChangeRevision = '081cae6a950204ced52f5ca09b78fe7446286967',
    [string]$ExpectedAssimpTag = 'v5.4.2',
    [string]$ExpectedAssimpRevision = 'ddb74c2bbdee1565dda667e85f0c82a0588c8053'
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$assimpPrefix = 'contrib/openddlparser/'
$expectedClosureSha256 = '85156d0fafb1ade930ed3e330e69841a2e68aa48bfb29f46e604fe290bc6e3f1'
$expectedMetadataVersion = '0.4.0'

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

function Get-GitText([string]$RepositoryDirectory, [string]$Revision, [string]$RelativePath, [string]$Description) {
    return (Invoke-GitOutput $RepositoryDirectory @('show', ('{0}:{1}' -f $Revision, $RelativePath)) $Description) -join "`n"
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

function Get-VersionMarker([string]$Text, [string]$Description) {
    if ($Text -notmatch 'static const char \*Version\s*=\s*"(?<Version>[0-9]+\.[0-9]+\.[0-9]+)"') {
        throw "$Description does not contain one static Version marker."
    }

    return $matches['Version']
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
        $Issues.Add("$Label expected $($Expected.Count) entries but found $($Actual.Count).")
        return
    }

    for ($index = 0; $index -lt $Expected.Count; $index++) {
        if (-not $Actual[$index].Equals($Expected[$index], [System.StringComparison]::OrdinalIgnoreCase)) {
            $Issues.Add("$Label entry $index expected '$($Expected[$index])' but was '$($Actual[$index])'.")
        }
    }
}

function Test-GitAncestor([System.Collections.Generic.List[string]]$Issues, [string]$RepositoryDirectory, [string]$Ancestor, [string]$Descendant, [string]$Label) {
    & git -C $RepositoryDirectory merge-base --is-ancestor $Ancestor $Descendant 2>$null
    if ($LASTEXITCODE -ne 0) {
        $Issues.Add("$Label expected '$Ancestor' to be an ancestor of '$Descendant'.")
    }
}

$files = @(
    [pscustomobject]@{ RelativePath = 'code/DDLNode.cpp'; ExpectedHistory = @(); ExpectedCurrentBlob = $null },
    [pscustomobject]@{ RelativePath = 'code/OpenDDLCommon.cpp'; ExpectedHistory = @(); ExpectedCurrentBlob = $null },
    [pscustomobject]@{ RelativePath = 'code/OpenDDLExport.cpp'; ExpectedHistory = @(); ExpectedCurrentBlob = $null },
    [pscustomobject]@{ RelativePath = 'code/OpenDDLParser.cpp'; ExpectedHistory = @($ExpectedAssimpParserChangeRevision); ExpectedCurrentBlob = '3d7dce45ec5267687afbe4d502cfe5033f57046b' },
    [pscustomobject]@{ RelativePath = 'code/OpenDDLStream.cpp'; ExpectedHistory = @(); ExpectedCurrentBlob = $null },
    [pscustomobject]@{ RelativePath = 'code/Value.cpp'; ExpectedHistory = @(); ExpectedCurrentBlob = $null },
    [pscustomobject]@{ RelativePath = 'include/openddlparser/DDLNode.h'; ExpectedHistory = @(); ExpectedCurrentBlob = $null },
    [pscustomobject]@{ RelativePath = 'include/openddlparser/OpenDDLCommon.h'; ExpectedHistory = @($ExpectedAssimpCommonChangeRevision); ExpectedCurrentBlob = '4b92d1406f353917788bc76fcef0c3fbde62eb3e' },
    [pscustomobject]@{ RelativePath = 'include/openddlparser/OpenDDLExport.h'; ExpectedHistory = @(); ExpectedCurrentBlob = $null },
    [pscustomobject]@{ RelativePath = 'include/openddlparser/OpenDDLParser.h'; ExpectedHistory = @(); ExpectedCurrentBlob = $null },
    [pscustomobject]@{ RelativePath = 'include/openddlparser/OpenDDLParserUtils.h'; ExpectedHistory = @(); ExpectedCurrentBlob = $null },
    [pscustomobject]@{ RelativePath = 'include/openddlparser/OpenDDLStream.h'; ExpectedHistory = @(); ExpectedCurrentBlob = $null },
    [pscustomobject]@{ RelativePath = 'include/openddlparser/Value.h'; ExpectedHistory = @(); ExpectedCurrentBlob = $null }
)

$issues = [System.Collections.Generic.List[string]]::new()
$sourceRecords = [System.Collections.Generic.List[object]]::new()
$historyRecords = [System.Collections.Generic.List[object]]::new()
$upstreamRepositoryResolved = Resolve-WorkspacePath $UpstreamRepositoryDirectory
$assimpRepositoryResolved = Resolve-WorkspacePath $AssimpRepositoryDirectory
$buildSourceResolved = Resolve-WorkspacePath $BuildSourceDirectory
$closureReportResolved = Resolve-WorkspacePath $ClosureReportPath
$reportPathResolved = Resolve-WorkspacePath $ReportPath
$gitEvidence = [ordered]@{}
$compilerInput = [ordered]@{}
$deltas = [ordered]@{}
$metadataVersions = [ordered]@{}

try {
    foreach ($value in @(
            $ExpectedUpstreamRevision,
            $ExpectedAssimpBaselineRevision,
            $ExpectedAssimpCommonChangeRevision,
            $ExpectedAssimpParserChangeRevision,
            $ExpectedAssimpRevision
        )) {
        if ($value -notmatch '^[0-9A-Fa-f]{40}$') {
            throw "Expected revision has an invalid format: $value"
        }
    }

    foreach ($path in @($upstreamRepositoryResolved, $assimpRepositoryResolved, $buildSourceResolved)) {
        if (-not (Test-Path -LiteralPath $path -PathType Container)) {
            throw "Required source directory does not exist: $path"
        }
    }

    if (-not (Test-Path -LiteralPath $closureReportResolved -PathType Leaf)) {
        throw "Required compiler-read closure report does not exist: $closureReportResolved"
    }

    $upstreamRemote = ((Invoke-GitOutput $upstreamRepositoryResolved @('remote', 'get-url', 'origin') 'OpenDDL upstream remote lookup') | Select-Object -First 1).Trim()
    $assimpRemote = ((Invoke-GitOutput $assimpRepositoryResolved @('remote', 'get-url', 'origin') 'Assimp upstream remote lookup') | Select-Object -First 1).Trim()
    Test-ExpectedValue $issues 'OpenDDL origin remote' $upstreamRemote $ExpectedUpstreamRemote
    Test-ExpectedValue $issues 'Assimp origin remote' $assimpRemote $ExpectedAssimpRemote

    $upstreamTagRevision = Get-GitRevision $upstreamRepositoryResolved $ExpectedUpstreamTag 'OpenDDL upstream tag resolution'
    $assimpTagRevision = Get-GitRevision $assimpRepositoryResolved $ExpectedAssimpTag 'Assimp tag resolution'
    Test-ExpectedValue $issues 'OpenDDL upstream tag revision' $upstreamTagRevision $ExpectedUpstreamRevision
    Test-ExpectedValue $issues 'Assimp tag revision' $assimpTagRevision $ExpectedAssimpRevision

    foreach ($revision in @($ExpectedAssimpBaselineRevision, $ExpectedAssimpCommonChangeRevision, $ExpectedAssimpParserChangeRevision)) {
        [void](Get-GitRevision $assimpRepositoryResolved $revision "Assimp fixed revision lookup $revision")
    }

    Test-GitAncestor $issues $assimpRepositoryResolved $ExpectedAssimpBaselineRevision $ExpectedAssimpCommonChangeRevision 'OpenDDL baseline-to-common-change ancestry'
    Test-GitAncestor $issues $assimpRepositoryResolved $ExpectedAssimpBaselineRevision $ExpectedAssimpParserChangeRevision 'OpenDDL baseline-to-parser-change ancestry'
    Test-GitAncestor $issues $assimpRepositoryResolved $ExpectedAssimpCommonChangeRevision $ExpectedAssimpTag 'OpenDDL common-change-to-tag ancestry'
    Test-GitAncestor $issues $assimpRepositoryResolved $ExpectedAssimpParserChangeRevision $ExpectedAssimpTag 'OpenDDL parser-change-to-tag ancestry'

    foreach ($file in $files) {
        $assimpPath = $assimpPrefix + $file.RelativePath
        $upstreamBlob = Get-GitBlob $upstreamRepositoryResolved $ExpectedUpstreamTag $file.RelativePath "OpenDDL upstream $($file.RelativePath) blob"
        $baselineBlob = Get-GitBlob $assimpRepositoryResolved $ExpectedAssimpBaselineRevision $assimpPath "Assimp baseline $($file.RelativePath) blob"
        $currentBlob = Get-GitBlob $assimpRepositoryResolved $ExpectedAssimpTag $assimpPath "Assimp current $($file.RelativePath) blob"
        $buildPath = Join-Path $buildSourceResolved $file.RelativePath
        $buildBlob = Get-FileGitBlob $buildPath "Clean build $($file.RelativePath) blob"
        $expectedCurrentBlob = if ([string]::IsNullOrWhiteSpace($file.ExpectedCurrentBlob)) { $upstreamBlob } else { $file.ExpectedCurrentBlob }

        Test-ExpectedValue $issues "Assimp baseline $($file.RelativePath) blob" $baselineBlob $upstreamBlob
        Test-ExpectedValue $issues "Assimp current $($file.RelativePath) blob" $currentBlob $expectedCurrentBlob
        Test-ExpectedValue $issues "Clean build $($file.RelativePath) blob" $buildBlob $expectedCurrentBlob

        $history = Get-PathHistory $assimpRepositoryResolved $ExpectedAssimpBaselineRevision $ExpectedAssimpTag $assimpPath "Assimp post-baseline $($file.RelativePath) history"
        Test-ExpectedSequence $issues "Assimp post-baseline $($file.RelativePath) history" $history $file.ExpectedHistory

        $sourceRecords.Add([pscustomobject]@{
            File = $file.RelativePath
            UpstreamV051Blob = $upstreamBlob
            AssimpBaselineBlob = $baselineBlob
            AssimpV542Blob = $currentBlob
            BuildInputBlob = $buildBlob
            ExpectedCurrentBlob = $expectedCurrentBlob
        })
        $historyRecords.Add([pscustomobject]@{
            File = $file.RelativePath
            Commits = @($history)
        })
    }

    $deltas['OpenDDLCommonHeader'] = Get-GitNumstat $assimpRepositoryResolved $ExpectedAssimpBaselineRevision $ExpectedAssimpCommonChangeRevision ($assimpPrefix + 'include/openddlparser/OpenDDLCommon.h') 'OpenDDL common-header Assimp delta'
    $deltas['OpenDDLParserCpp'] = Get-GitNumstat $assimpRepositoryResolved $ExpectedAssimpBaselineRevision $ExpectedAssimpParserChangeRevision ($assimpPrefix + 'code/OpenDDLParser.cpp') 'OpenDDL parser Assimp delta'
    Test-ExpectedNumstat $issues 'OpenDDL common-header Assimp delta' $deltas['OpenDDLCommonHeader'] 12 15
    Test-ExpectedNumstat $issues 'OpenDDL parser Assimp delta' $deltas['OpenDDLParserCpp'] 3 1

    $versionPath = 'code/OpenDDLParser.cpp'
    $metadataVersions['UpstreamV051'] = Get-VersionMarker (Get-GitText $upstreamRepositoryResolved $ExpectedUpstreamTag $versionPath 'OpenDDL upstream version-marker lookup') 'OpenDDL upstream version marker'
    $metadataVersions['AssimpBaseline'] = Get-VersionMarker (Get-GitText $assimpRepositoryResolved $ExpectedAssimpBaselineRevision ($assimpPrefix + $versionPath) 'Assimp baseline version-marker lookup') 'Assimp baseline version marker'
    $metadataVersions['AssimpV542'] = Get-VersionMarker (Get-GitText $assimpRepositoryResolved $ExpectedAssimpTag ($assimpPrefix + $versionPath) 'Assimp current version-marker lookup') 'Assimp current version marker'
    foreach ($name in @($metadataVersions.Keys)) {
        Test-ExpectedValue $issues "OpenDDL $name metadata version" $metadataVersions[$name] $expectedMetadataVersion
    }

    $cmakePath = Join-Path $buildSourceResolved 'CMakeLists.txt'
    $cmakeText = Get-Content -LiteralPath $cmakePath -Raw
    $missingCmakeEntries = @()
    foreach ($file in $files) {
        if ($cmakeText -notmatch [regex]::Escape($file.RelativePath)) {
            $missingCmakeEntries += $file.RelativePath
        }
    }
    $compilerInput['CMakeEntryCount'] = $files.Count - $missingCmakeEntries.Count
    $compilerInput['CMakeMissingEntries'] = @($missingCmakeEntries)
    if ($missingCmakeEntries.Count -gt 0) {
        $issues.Add("CMakeLists.txt does not declare expected OpenDDL inputs: $($missingCmakeEntries -join ', ')")
    }

    $closure = Get-Content -LiteralPath $closureReportResolved -Raw | ConvertFrom-Json
    $openddlComponent = @($closure.components | Where-Object { $_.key -eq 'openddlparser' })
    if ($openddlComponent.Count -ne 1) {
        $issues.Add("Compiler-read closure expected one openddlparser component but found $($openddlComponent.Count).")
    }
    else {
        $component = $openddlComponent[0]
        $expectedClosureFiles = @($files | ForEach-Object { $assimpPrefix + $_.RelativePath })
        $compilerInput['ClosureUsedFileCount'] = [int]$component.usedFileCount
        $compilerInput['ClosureUsedFileSetSha256'] = $component.usedFileSetSha256
        $compilerInput['ClosureUsedFiles'] = @($component.usedFiles)
        if ($component.usedFileCount -ne $files.Count) {
            $issues.Add("Compiler-read OpenDDL closure expected $($files.Count) files but found $($component.usedFileCount).")
        }

        Test-ExpectedValue $issues 'Compiler-read OpenDDL closure SHA-256' $component.usedFileSetSha256 $expectedClosureSha256
        Test-ExpectedSequence $issues 'Compiler-read OpenDDL closure files' @($component.usedFiles) $expectedClosureFiles
    }

    $gitEvidence = [ordered]@{
        UpstreamRemote = $upstreamRemote
        AssimpRemote = $assimpRemote
        UpstreamTagRevision = $upstreamTagRevision
        AssimpTagRevision = $assimpTagRevision
        AssimpBaselineRevision = $ExpectedAssimpBaselineRevision
        AssimpCommonChangeRevision = $ExpectedAssimpCommonChangeRevision
        AssimpParserChangeRevision = $ExpectedAssimpParserChangeRevision
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
        UpstreamRepositoryDirectory = $upstreamRepositoryResolved
        AssimpRepositoryDirectory = $assimpRepositoryResolved
        BuildSourceDirectory = $buildSourceResolved
        ClosureReportPath = $closureReportResolved
        ExpectedUpstreamTag = $ExpectedUpstreamTag
        ExpectedUpstreamRevision = $ExpectedUpstreamRevision.ToLowerInvariant()
        ExpectedAssimpTag = $ExpectedAssimpTag
        ExpectedAssimpRevision = $ExpectedAssimpRevision.ToLowerInvariant()
    }
    Git = $gitEvidence
    MetadataVersions = $metadataVersions
    CompilerInput = $compilerInput
    SourceIdentity = @($sourceRecords)
    Deltas = [ordered]@{
        FixedAssimpChanges = $deltas
        PostBaselineHistory = @($historyRecords)
    }
    Issues = @($issues)
    ClaimLimit = 'upstream-v0.5.1-to-assimp-baseline-identity=True|bounded-assimp-delta-to-current-build-source=True|cmake-static-0.4.0-metadata-is-source-identity=False|upstream-signature-or-owner-identity=False|notice-or-license-disposition=False|binary-reproducibility=False|distribution-approval=False'
}

$reportDirectory = Split-Path -Parent $reportPathResolved
New-Item -ItemType Directory -Path $reportDirectory -Force | Out-Null
$report | ConvertTo-Json -Depth 14 | Set-Content -LiteralPath $reportPathResolved -Encoding utf8

"AssimpOpenDDLProvenance|$status|upstream=$($gitEvidence.UpstreamTagRevision)|assimp=$($gitEvidence.AssimpTagRevision)|files=$($sourceRecords.Count)|deltas=$($deltas.Count)|issues=$($issues.Count)"
"Report|$reportPathResolved"

if ($status -ne 'Pass') {
    exit 1
}
