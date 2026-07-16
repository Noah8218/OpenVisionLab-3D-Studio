[CmdletBinding()]
param(
    [string]$MinizRepositoryDirectory = 'artifacts\dependency-candidates\assimp-miniz-origin-boundary-20260716\official-miniz-full',
    [string]$KubaRepositoryDirectory = 'artifacts\dependency-candidates\assimp-kubazip-provenance-20260716\upstream-kuba-zip',
    [string]$BuildSourceFile = 'artifacts\o3d-clean\b\assimp\src\ext_assimp\contrib\zip\src\miniz.h',
    [string]$ClosureReportPath = 'artifacts\dependency-candidates\assimp-closure-20260716\assimp-closure.json',
    [string]$MinizProvenanceReportPath = 'artifacts\dependency-candidates\assimp-miniz-provenance-20260716\miniz-provenance.json',
    [string]$ReportPath = 'artifacts\dependency-candidates\assimp-miniz-origin-boundary-20260716\miniz-origin-boundary.json',
    [string]$ExpectedMinizRemote = 'https://github.com/richgel999/miniz.git',
    [string]$ExpectedKubaRemote = 'https://github.com/kuba--/zip.git',
    [string]$ExpectedKubaTag = 'v0.3.1',
    [string]$ExpectedKubaRevision = '550905d883b29f0b23e433fdb97f6299b628d4a9',
    [string]$ExpectedKubaBlob = 'cd86483184cfba1dd33c4db7c718965e20926c7a',
    [string]$ExpectedMinizLegacyTag = 'v114',
    [string]$ExpectedMinizLegacyRevision = '48605fb1bd5662effe13b789d866981225e71256',
    [string]$ExpectedMinizLegacyBlob = 'ac3d93569f4f2b5683c639aa6b55db9c64894425',
    [string]$ExpectedMinizModernTag = '3.0.2',
    [string]$ExpectedMinizModernRevision = '293d4db1b7d0ffee9756d035b9ac6f7431ef8492',
    [string]$ExpectedMinizModernCBlob = '1968d62b8f99b897cbe639a422c7775d3271b9a8',
    [string]$ExpectedMinizModernHBlob = '2f86380ad42f6aa9ae3a90bd50bf1928431a351f',
    [string]$ExpectedBuildBlob = 'ad5850ce17d9449cc9356486dff73c8a566e1c46',
    [string]$ExpectedBuildSha256 = 'bba5c196415bda01b460d2dd8be779189bb228c2802b5b61dd20eae5c3921b06',
    [string]$ExpectedClosureSha256 = 'ccc0dd6eef59502e6d9aa1774b21ce1e2038d8448baf6d28bdf878d868a5a67c'
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot

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
    $value = @($output | Where-Object { $_ -match '^[0-9A-Fa-f]{40}$' } | Select-Object -First 1)
    if ($value.Count -ne 1) {
        throw "$Description did not resolve to one full Git revision."
    }

    return $value[0].ToLowerInvariant()
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

    $value = @($output | Where-Object { $_ -match '^[0-9A-Fa-f]{40}$' } | Select-Object -First 1)
    if ($value.Count -ne 1) {
        throw "$Description did not produce one full Git blob identifier."
    }

    return $value[0].ToLowerInvariant()
}

function Test-ExpectedValue([System.Collections.Generic.List[string]]$Issues, [string]$Label, [string]$Actual, [string]$Expected) {
    if ([string]::IsNullOrWhiteSpace($Actual) -or -not $Actual.Equals($Expected, [System.StringComparison]::OrdinalIgnoreCase)) {
        $Issues.Add("$Label expected '$Expected' but was '$Actual'.")
    }
}

function Test-ExpectedBoolean([System.Collections.Generic.List[string]]$Issues, [string]$Label, [bool]$Actual, [bool]$Expected) {
    if ($Actual -ne $Expected) {
        $Issues.Add("$Label expected $Expected but was $Actual.")
    }
}

function Get-GitText([string]$RepositoryDirectory, [string]$Revision, [string]$RelativePath, [string]$Description) {
    return (Invoke-GitOutput $RepositoryDirectory @('show', ('{0}:{1}' -f $Revision, $RelativePath)) $Description) -join "`n"
}

function Test-TextMarker([System.Collections.Generic.List[string]]$Issues, [string]$Label, [string]$Text, [string]$ExpectedMarker) {
    if ($Text.IndexOf($ExpectedMarker, [System.StringComparison]::Ordinal) -lt 0) {
        $Issues.Add("$Label does not contain expected marker '$ExpectedMarker'.")
    }
}

function Test-GitAncestor([string]$RepositoryDirectory, [string]$Ancestor, [string]$Descendant, [string]$Description) {
    & git -C $RepositoryDirectory merge-base --is-ancestor $Ancestor $Descendant 2>$null
    $exitCode = $LASTEXITCODE
    if ($exitCode -notin @(0, 1)) {
        throw "$Description failed with exit code $exitCode."
    }

    return $exitCode -eq 0
}

$issues = [System.Collections.Generic.List[string]]::new()
$minizRepositoryResolved = Resolve-WorkspacePath $MinizRepositoryDirectory
$kubaRepositoryResolved = Resolve-WorkspacePath $KubaRepositoryDirectory
$buildSourceResolved = Resolve-WorkspacePath $BuildSourceFile
$closureReportResolved = Resolve-WorkspacePath $ClosureReportPath
$minizProvenanceReportResolved = Resolve-WorkspacePath $MinizProvenanceReportPath
$reportPathResolved = Resolve-WorkspacePath $ReportPath
$closure = $null
$minizProvenance = $null
$reportFields = [ordered]@{}

try {
    foreach ($revision in @(
            $ExpectedKubaRevision,
            $ExpectedMinizLegacyRevision,
            $ExpectedMinizModernRevision
        )) {
        if ($revision -notmatch '^[0-9A-Fa-f]{40}$') {
            throw "Expected revision has an invalid format: $revision"
        }
    }

    foreach ($path in @($minizRepositoryResolved, $kubaRepositoryResolved)) {
        if (-not (Test-Path -LiteralPath $path -PathType Container)) {
            throw "Required repository directory does not exist: $path"
        }
    }

    foreach ($path in @($buildSourceResolved, $closureReportResolved, $minizProvenanceReportResolved)) {
        if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
            throw "Required evidence file does not exist: $path"
        }
    }

    $minizRemote = ((Invoke-GitOutput $minizRepositoryResolved @('remote', 'get-url', 'origin') 'Official miniz remote lookup') | Select-Object -First 1).Trim()
    $kubaRemote = ((Invoke-GitOutput $kubaRepositoryResolved @('remote', 'get-url', 'origin') 'Kuba remote lookup') | Select-Object -First 1).Trim()
    $minizIsShallow = ((Invoke-GitOutput $minizRepositoryResolved @('rev-parse', '--is-shallow-repository') 'Official miniz shallow-state lookup') | Select-Object -First 1).Trim()
    Test-ExpectedValue $issues 'Official miniz origin remote' $minizRemote $ExpectedMinizRemote
    Test-ExpectedValue $issues 'Kuba origin remote' $kubaRemote $ExpectedKubaRemote
    Test-ExpectedValue $issues 'Official miniz shallow state' $minizIsShallow 'false'

    $kubaRevision = Get-GitRevision $kubaRepositoryResolved $ExpectedKubaTag 'Kuba tag resolution'
    $legacyRevision = Get-GitRevision $minizRepositoryResolved $ExpectedMinizLegacyTag 'Official miniz legacy tag resolution'
    $modernRevision = Get-GitRevision $minizRepositoryResolved $ExpectedMinizModernTag 'Official miniz modern tag resolution'
    Test-ExpectedValue $issues 'Kuba tag revision' $kubaRevision $ExpectedKubaRevision
    Test-ExpectedValue $issues 'Official miniz legacy tag revision' $legacyRevision $ExpectedMinizLegacyRevision
    Test-ExpectedValue $issues 'Official miniz modern tag revision' $modernRevision $ExpectedMinizModernRevision

    $kubaBlob = Get-GitBlob $kubaRepositoryResolved $ExpectedKubaTag 'src/miniz.h' 'Kuba miniz baseline blob'
    $legacyBlob = Get-GitBlob $minizRepositoryResolved $ExpectedMinizLegacyTag 'miniz.c' 'Official miniz v114 blob'
    $modernCBlob = Get-GitBlob $minizRepositoryResolved $ExpectedMinizModernTag 'miniz.c' 'Official miniz v3.0.2 implementation blob'
    $modernHBlob = Get-GitBlob $minizRepositoryResolved $ExpectedMinizModernTag 'miniz.h' 'Official miniz v3.0.2 header blob'
    $buildBlob = Get-FileGitBlob $buildSourceResolved 'Clean-build miniz blob'
    $buildSha256 = (Get-FileHash -LiteralPath $buildSourceResolved -Algorithm SHA256).Hash.ToLowerInvariant()
    Test-ExpectedValue $issues 'Kuba baseline miniz blob' $kubaBlob $ExpectedKubaBlob
    Test-ExpectedValue $issues 'Official miniz v114 blob' $legacyBlob $ExpectedMinizLegacyBlob
    Test-ExpectedValue $issues 'Official miniz v3.0.2 implementation blob' $modernCBlob $ExpectedMinizModernCBlob
    Test-ExpectedValue $issues 'Official miniz v3.0.2 header blob' $modernHBlob $ExpectedMinizModernHBlob
    Test-ExpectedValue $issues 'Clean-build miniz blob' $buildBlob $ExpectedBuildBlob
    Test-ExpectedValue $issues 'Clean-build miniz SHA-256' $buildSha256 $ExpectedBuildSha256

    $legacyIsAncestorOfModern = Test-GitAncestor $minizRepositoryResolved $ExpectedMinizLegacyTag $ExpectedMinizModernTag 'Official miniz legacy-to-modern ancestry'
    Test-ExpectedBoolean $issues 'Official miniz v114 ancestor of v3.0.2' $legacyIsAncestorOfModern $false

    $reachableObjectIds = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
    foreach ($line in Invoke-GitOutput $minizRepositoryResolved @('rev-list', '--all', '--objects') 'Official miniz reachable-object listing') {
        $match = [regex]::Match($line, '^(?<Object>[0-9A-Fa-f]{40})(?:\s|$)')
        if ($match.Success) {
            [void]$reachableObjectIds.Add($match.Groups['Object'].Value)
        }
    }

    $kubaBlobReachable = $reachableObjectIds.Contains($kubaBlob)
    $buildBlobReachable = $reachableObjectIds.Contains($buildBlob)
    Test-ExpectedBoolean $issues 'Kuba baseline blob directly reachable from official miniz refs' $kubaBlobReachable $false
    Test-ExpectedBoolean $issues 'Assimp clean-build blob directly reachable from official miniz refs' $buildBlobReachable $false

    $kubaText = Get-GitText $kubaRepositoryResolved $ExpectedKubaTag 'src/miniz.h' 'Kuba miniz source text'
    $legacyText = Get-GitText $minizRepositoryResolved $ExpectedMinizLegacyTag 'miniz.c' 'Official miniz v114 source text'
    $modernLicenseText = Get-GitText $minizRepositoryResolved $ExpectedMinizModernTag 'LICENSE' 'Official miniz v3.0.2 license text'
    $buildText = Get-Content -LiteralPath $buildSourceResolved -Raw
    Test-TextMarker $issues 'Kuba miniz source' $kubaText 'miniz.c 3.0.0'
    Test-TextMarker $issues 'Kuba miniz source' $kubaText 'last updated Oct. 13,'
    Test-TextMarker $issues 'Kuba miniz source' $kubaText '2013 Implements RFC 1950'
    Test-TextMarker $issues 'Kuba miniz source' $kubaText 'MZ_VERSION "11.0.2"'
    Test-TextMarker $issues 'Official miniz v114 source' $legacyText 'miniz.c v1.14'
    Test-TextMarker $issues 'Official miniz v114 source' $legacyText 'last updated May 20, 2012'
    Test-TextMarker $issues 'Official miniz v114 source' $legacyText 'MZ_VERSION          "9.1.14"'
    Test-TextMarker $issues 'Official miniz v3.0.2 license' $modernLicenseText 'Copyright 2013-2014 RAD Game Tools and Valve Software'
    Test-TextMarker $issues 'Official miniz v3.0.2 license' $modernLicenseText 'Permission is hereby granted, free of charge'
    Test-TextMarker $issues 'Clean-build miniz source' $buildText 'This is free and unencumbered software released into the public domain.'
    Test-TextMarker $issues 'Clean-build miniz source' $buildText 'Permission is hereby granted, free of charge'

    $closure = Get-Content -LiteralPath $closureReportResolved -Raw | ConvertFrom-Json
    $minizProvenance = Get-Content -LiteralPath $minizProvenanceReportResolved -Raw | ConvertFrom-Json
    Test-ExpectedValue $issues 'Assimp miniz provenance status' $minizProvenance.Status 'Pass'
    $closureComponents = @($closure.components | Where-Object { $_.key -eq 'miniz' })
    if ($closureComponents.Count -ne 1) {
        $issues.Add("Expected one miniz closure component but found $($closureComponents.Count).")
    }
    else {
        Test-ExpectedValue $issues 'Miniz closure file-set SHA-256' $closureComponents[0].usedFileSetSha256 $ExpectedClosureSha256
        if ([int]$closureComponents[0].usedFileCount -ne 1 -or @($closureComponents[0].usedFiles).Count -ne 1 -or $closureComponents[0].usedFiles[0] -ne 'contrib/zip/src/miniz.h') {
            $issues.Add('Miniz closure is not the expected one-file compiler input.')
        }
    }

    $reportFields = [ordered]@{
        OfficialMiniz = [ordered]@{
            Remote = $minizRemote
            IsShallow = $minizIsShallow
            LegacyTag = $ExpectedMinizLegacyTag
            LegacyRevision = $legacyRevision
            LegacyBlob = $legacyBlob
            ModernTag = $ExpectedMinizModernTag
            ModernRevision = $modernRevision
            ModernCBlob = $modernCBlob
            ModernHBlob = $modernHBlob
            LegacyIsAncestorOfModern = $legacyIsAncestorOfModern
            ReachableObjectCount = $reachableObjectIds.Count
        }
        Kuba = [ordered]@{
            Remote = $kubaRemote
            Tag = $ExpectedKubaTag
            Revision = $kubaRevision
            MinizBlob = $kubaBlob
        }
        AssimpBuild = [ordered]@{
            Blob = $buildBlob
            Sha256 = $buildSha256
            ClosureFileSetSha256 = if ($null -eq $closureComponents -or $closureComponents.Count -ne 1) { $null } else { $closureComponents[0].usedFileSetSha256 }
        }
        DirectRawIdentity = [ordered]@{
            KubaBaselineBlobReachableFromOfficialMiniz = $kubaBlobReachable
            AssimpBuildBlobReachableFromOfficialMiniz = $buildBlobReachable
            Status = 'Not observed in current official reachable object set'
        }
        SourceNoticeText = [ordered]@{
            BuildContainsUnlicense = $buildText.IndexOf('This is free and unencumbered software released into the public domain.', [System.StringComparison]::Ordinal) -ge 0
            BuildContainsMit = $buildText.IndexOf('Permission is hereby granted, free of charge', [System.StringComparison]::Ordinal) -ge 0
            OfficialModernLicenseContainsMit = $modernLicenseText.IndexOf('Permission is hereby granted, free of charge', [System.StringComparison]::Ordinal) -ge 0
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
    OriginIdentityStatus = 'Unresolved'
    Scope = [ordered]@{
        Claim = 'Current official richgel999/miniz reachable-history boundary and source-notice-text audit only'
        OriginalUpstreamByteIdentity = $false
        FinalNoticeApproval = $false
        DistributionApproval = $false
    }
    Inputs = [ordered]@{
        MinizRepositoryDirectory = $minizRepositoryResolved
        KubaRepositoryDirectory = $kubaRepositoryResolved
        BuildSourceFile = $buildSourceResolved
        ClosureReportPath = $closureReportResolved
        MinizProvenanceReportPath = $minizProvenanceReportResolved
    }
    Evidence = $reportFields
    Issues = @($issues)
}

$reportDirectory = Split-Path -Parent $reportPathResolved
New-Item -ItemType Directory -Path $reportDirectory -Force | Out-Null
$report | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $reportPathResolved -Encoding utf8

$reachableMatches = if ($reportFields.Contains('DirectRawIdentity')) { [int]$reportFields.DirectRawIdentity.KubaBaselineBlobReachableFromOfficialMiniz + [int]$reportFields.DirectRawIdentity.AssimpBuildBlobReachableFromOfficialMiniz } else { -1 }
Write-Output ('AssimpMinizOriginBoundary|{0}|origin={1}|reachableMatches={2}|issues={3}' -f $status, $report.OriginIdentityStatus, $reachableMatches, $issues.Count)
Write-Output ('Report|{0}' -f $reportPathResolved)

if ($status -ne 'Pass') {
    exit 1
}
