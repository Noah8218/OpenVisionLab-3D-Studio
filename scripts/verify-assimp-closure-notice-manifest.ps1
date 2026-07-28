[CmdletBinding()]
param(
    [string]$ClosureReportPath = 'artifacts\dependency-candidates\assimp-closure-20260716\assimp-closure.json',
    [string]$SourceSnapshotReportPath = 'artifacts\dependency-candidates\assimp-source-snapshot-20260716\assimp-source-snapshot.json',
    [string]$SbomPath = 'docs\open3d-0.19.0-nongui-windows-candidate.cdx.json',
    [string]$BuildSourceDirectory = 'artifacts\o3d-clean\b\assimp\src\ext_assimp',
    [string]$ReportPath = 'artifacts\dependency-candidates\assimp-closure-notice-manifest-20260716\assimp-closure-notice-manifest.json',
    [string]$ExpectedAssimpTag = 'v5.4.2',
    [string]$ExpectedAssimpRevision = 'ddb74c2bbdee1565dda667e85f0c82a0588c8053',
    [string]$ExpectedAssimpArchiveSha256 = '03e38d123f6bf19a48658d197fd09c9a69db88c076b56a476ab2da9f5eb87dcc'
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

function Get-CanonicalTextSha256([string]$Text) {
    $bytes = [System.Text.UTF8Encoding]::new($false).GetBytes($Text)
    $sha256 = [System.Security.Cryptography.SHA256]::Create()
    try {
        return ([System.BitConverter]::ToString($sha256.ComputeHash($bytes))).Replace('-', '').ToLowerInvariant()
    }
    finally {
        $sha256.Dispose()
    }
}

function Add-Issue([System.Collections.Generic.List[string]]$Issues, [string]$Message) {
    $Issues.Add($Message)
}

function Test-ExpectedValue([System.Collections.Generic.List[string]]$Issues, [string]$Label, [string]$Actual, [string]$Expected) {
    if ([string]::IsNullOrWhiteSpace($Actual) -or -not $Actual.Equals($Expected, [System.StringComparison]::OrdinalIgnoreCase)) {
        Add-Issue $Issues "$Label expected '$Expected' but was '$Actual'."
    }
}

function Test-ExpectedInt([System.Collections.Generic.List[string]]$Issues, [string]$Label, [int]$Actual, [int]$Expected) {
    if ($Actual -ne $Expected) {
        Add-Issue $Issues "$Label expected $Expected but was $Actual."
    }
}

function Test-ExpectedSequence([System.Collections.Generic.List[string]]$Issues, [string]$Label, [string[]]$Actual, [string[]]$Expected) {
    if ($Actual.Count -ne $Expected.Count) {
        Add-Issue $Issues "$Label expected $($Expected.Count) entries but found $($Actual.Count)."
        return
    }

    for ($index = 0; $index -lt $Expected.Count; $index++) {
        if (-not $Actual[$index].Equals($Expected[$index], [System.StringComparison]::OrdinalIgnoreCase)) {
            Add-Issue $Issues "$Label entry $index expected '$($Expected[$index])' but was '$($Actual[$index])'."
        }
    }
}

function Get-SafeSourcePath([string]$SourceDirectory, [string]$RelativePath) {
    if ([string]::IsNullOrWhiteSpace($RelativePath) -or [System.IO.Path]::IsPathRooted($RelativePath)) {
        throw "Unsafe source-relative path: $RelativePath"
    }

    $normalizedRelativePath = $RelativePath.Replace('/', '\')
    $segments = @($normalizedRelativePath.Split('\'))
    if (($segments | Where-Object { [string]::IsNullOrWhiteSpace($_) -or $_ -eq '.' -or $_ -eq '..' }).Count -gt 0) {
        throw "Unsafe source-relative path: $RelativePath"
    }

    $sourceRoot = [System.IO.Path]::GetFullPath($SourceDirectory).TrimEnd('\', '/')
    $resolvedPath = [System.IO.Path]::GetFullPath((Join-Path $sourceRoot $normalizedRelativePath))
    $prefix = $sourceRoot + [System.IO.Path]::DirectorySeparatorChar
    if (-not $resolvedPath.StartsWith($prefix, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Source-relative path escaped the source root: $RelativePath"
    }

    return $resolvedPath
}

function Get-SbomLicenseValues([object]$Component) {
    $values = [System.Collections.Generic.List[string]]::new()
    foreach ($licenseEntry in @($Component.licenses)) {
        if ($null -ne $licenseEntry.license) {
            if (-not [string]::IsNullOrWhiteSpace($licenseEntry.license.id)) {
                $values.Add($licenseEntry.license.id)
            }
            elseif (-not [string]::IsNullOrWhiteSpace($licenseEntry.license.expression)) {
                $values.Add($licenseEntry.license.expression)
            }
        }
        elseif (-not [string]::IsNullOrWhiteSpace($licenseEntry.expression)) {
            $values.Add($licenseEntry.expression)
        }
    }

    return @($values | Sort-Object)
}

function Get-SbomPropertyValues([object]$Component, [string]$PropertyName) {
    return @($Component.properties | Where-Object { $_.name -eq $PropertyName } | ForEach-Object { $_.value })
}

function Test-SbomNoticeEvidence([object]$Component, [object[]]$NoticeEvidence) {
    $licenseEvidence = (Get-SbomPropertyValues $Component 'openvisionlab:licenseEvidence') -join "`n"
    foreach ($evidence in $NoticeEvidence) {
        $needle = '{0};sha256={1}' -f $evidence.SbomPath, $evidence.ExpectedSha256
        if ($licenseEvidence.IndexOf($needle, [System.StringComparison]::OrdinalIgnoreCase) -lt 0) {
            return $false
        }
    }

    return $true
}

function Get-NoticeContractSha256([object[]]$Entries) {
    $builder = [System.Text.StringBuilder]::new()
    foreach ($entry in @($Entries | Sort-Object Component)) {
        [void]$builder.Append($entry.Component)
        [void]$builder.Append('|')
        [void]$builder.Append($entry.Version)
        [void]$builder.Append('|')
        [void]$builder.Append($entry.ClosureKey)
        [void]$builder.Append('|')
        [void]$builder.Append($entry.UsedFileCount)
        [void]$builder.Append('|')
        [void]$builder.Append($entry.UsedFileSetSha256)
        [void]$builder.Append("`n")
        foreach ($evidence in @($entry.NoticeEvidence | Sort-Object SourcePath)) {
            [void]$builder.Append($evidence.License)
            [void]$builder.Append('|')
            [void]$builder.Append($evidence.SourcePath)
            [void]$builder.Append('|')
            [void]$builder.Append($evidence.Sha256)
            [void]$builder.Append("`n")
        }
    }

    return Get-CanonicalTextSha256 $builder.ToString()
}

$issues = [System.Collections.Generic.List[string]]::new()
$entries = [System.Collections.Generic.List[object]]::new()
$closureReportResolved = Resolve-WorkspacePath $ClosureReportPath
$sourceSnapshotReportResolved = Resolve-WorkspacePath $SourceSnapshotReportPath
$sbomPathResolved = Resolve-WorkspacePath $SbomPath
$buildSourceResolved = Resolve-WorkspacePath $BuildSourceDirectory
$reportPathResolved = Resolve-WorkspacePath $ReportPath
$closure = $null
$sourceSnapshot = $null
$sbom = $null

$contracts = @(
    [pscustomobject]@{
        ClosureKey = $null
        Component = 'Assimp'
        Version = '5.4.2'
        Licenses = @('BSD-3-Clause')
        UsedFileCount = 232
        UsedFileSetSha256 = $null
        NoticeEvidence = @(
            [pscustomobject]@{ License = 'BSD-3-Clause'; SourcePath = 'LICENSE'; SbomPath = '3rdparty/assimp/LICENSE'; ExpectedSha256 = '147874443d242b4e2bae97036e26ec9d6b37f706174c1bd5ecfcc8c1294cef51' }
        )
    }
    [pscustomobject]@{
        ClosureKey = 'clipper'
        Component = 'Clipper'
        Version = '6.4.2'
        Licenses = @('BSL-1.0')
        UsedFileCount = 2
        UsedFileSetSha256 = 'fbb929aa50cbbcbc8588d7d692daf4e6f781400e1e621c3c47c95f59e2087c15'
        NoticeEvidence = @(
            [pscustomobject]@{ License = 'BSL-1.0'; SourcePath = 'contrib/clipper/License.txt'; SbomPath = 'contrib/clipper/License.txt'; ExpectedSha256 = '1bc8ddf91ba50e4186abb6b2339910e4529471ad8181b7cf2a5026c09ce09294' }
        )
    }
    [pscustomobject]@{
        ClosureKey = 'Open3DGC'
        Component = 'Open3DGC'
        Version = 'assimp-v5.4.2-vendored'
        Licenses = @('BSD-2-Clause', 'MIT')
        UsedFileCount = 29
        UsedFileSetSha256 = 'bb290db0bc142ae99b6b07774091db1f07f29e820b544518c609d6559494f410'
        NoticeEvidence = @(
            [pscustomobject]@{ License = 'MIT'; SourcePath = 'contrib/Open3DGC/o3dgcCommon.h'; SbomPath = 'contrib/Open3DGC/o3dgcCommon.h'; ExpectedSha256 = '4e46f0d8edeb2781fb860d4b7261d28ffed684e5bedb45f0f7493efcec49787c' }
            [pscustomobject]@{ License = 'BSD-2-Clause'; SourcePath = 'contrib/Open3DGC/o3dgcArithmeticCodec.h'; SbomPath = 'contrib/Open3DGC/o3dgcArithmeticCodec.h'; ExpectedSha256 = 'c3082225c449bce11fff8f4d8ab1b50c1f0745ea8120db3469873deb60458861' }
            [pscustomobject]@{ License = 'BSD-2-Clause'; SourcePath = 'contrib/Open3DGC/o3dgcArithmeticCodec.cpp'; SbomPath = 'contrib/Open3DGC/o3dgcArithmeticCodec.cpp'; ExpectedSha256 = '01fc0d7b27811e3b72f424dfb8028a3a09dffba04d3efe25ca893f25b1199579' }
        )
    }
    [pscustomobject]@{
        ClosureKey = 'openddlparser'
        Component = 'OpenDDL Parser'
        Version = '0.4.0'
        Licenses = @('MIT')
        UsedFileCount = 13
        UsedFileSetSha256 = '85156d0fafb1ade930ed3e330e69841a2e68aa48bfb29f46e604fe290bc6e3f1'
        NoticeEvidence = @(
            [pscustomobject]@{ License = 'MIT'; SourcePath = 'contrib/openddlparser/LICENSE'; SbomPath = 'contrib/openddlparser/LICENSE'; ExpectedSha256 = '656597ef86119e4a526fb8621e0112b95051cf9d63a311b09b3a73346da00ead' }
        )
    }
    [pscustomobject]@{
        ClosureKey = 'poly2tri'
        Component = 'Poly2Tri'
        Version = 'assimp-v5.4.2-vendored'
        Licenses = @('BSD-3-Clause')
        UsedFileCount = 12
        UsedFileSetSha256 = '6be0cbf33f91510fcb906186e898b3a3226c0fdd5f471789a7e0aafe4361d5e3'
        NoticeEvidence = @(
            [pscustomobject]@{ License = 'BSD-3-Clause'; SourcePath = 'contrib/poly2tri/LICENSE'; SbomPath = 'contrib/poly2tri/LICENSE'; ExpectedSha256 = 'a4d34b9e99a7f11d095ba330da5a4723ec00b019ae1ba42cba46ad1d89902c4e' }
        )
    }
    [pscustomobject]@{
        ClosureKey = 'pugixml'
        Component = 'pugixml'
        Version = '1.13'
        Licenses = @('MIT')
        UsedFileCount = 3
        UsedFileSetSha256 = '3b5568e97b10d2e4ebe2972c0a99cf9a64510fe10556997a788b445b0db7aaf7'
        NoticeEvidence = @(
            [pscustomobject]@{ License = 'MIT'; SourcePath = 'contrib/pugixml/readme.txt'; SbomPath = 'contrib/pugixml/readme.txt'; ExpectedSha256 = '4093595a904844f34a8f48e2eff63bcfd0f41ad0adf95e90fff216b3da944861' }
        )
    }
    [pscustomobject]@{
        ClosureKey = 'rapidjson'
        Component = 'RapidJSON'
        Version = '1.1.0'
        Licenses = @('MIT')
        UsedFileCount = 29
        UsedFileSetSha256 = 'f9f5aec8c411fb5af185f0a148d1615539ca9837f3a05fa992df0cef315006fb'
        NoticeEvidence = @(
            [pscustomobject]@{ License = 'MIT'; SourcePath = 'contrib/rapidjson/license.txt'; SbomPath = 'contrib/rapidjson/license.txt'; ExpectedSha256 = 'a140e5d46fe734a1c78f1a3c3ef207871dd75648be71fdda8e309b23ab8b1f32' }
        )
    }
    [pscustomobject]@{
        ClosureKey = 'stb'
        Component = 'stb_image'
        Version = '2.29'
        Licenses = @('MIT OR Unlicense')
        UsedFileCount = 1
        UsedFileSetSha256 = 'bb64489c54fc5fab837758271cb3687777ad9127ee2e9d9098994f48dbf7d0ac'
        NoticeEvidence = @(
            [pscustomobject]@{ License = 'MIT OR Unlicense'; SourcePath = 'contrib/stb/stb_image.h'; SbomPath = 'contrib/stb/stb_image.h'; ExpectedSha256 = 'c54b15a689e6a1f32c75e2ec23afa442e3e0e37e894b73c1974d08679b20dd5c' }
        )
    }
    [pscustomobject]@{
        ClosureKey = 'unzip'
        Component = 'MiniZip'
        Version = '1.1'
        Licenses = @('Zlib')
        UsedFileCount = 4
        UsedFileSetSha256 = '2e24ca3bfc05768d96770aa57d73843c01c0c3abd7acd3e3643b4db4177dc19f'
        NoticeEvidence = @(
            [pscustomobject]@{ License = 'Zlib'; SourcePath = 'contrib/unzip/unzip.h'; SbomPath = 'contrib/unzip/unzip.h'; ExpectedSha256 = '2ff9df0b1da7499adc806bc0ec098ef1c7b9d361a8fd5b08a98299a1a4e88399' }
        )
    }
    [pscustomobject]@{
        ClosureKey = 'utf8cpp'
        Component = 'UTF8-CPP'
        Version = 'assimp-v5.4.2-vendored'
        Licenses = @('BSL-1.0')
        UsedFileCount = 4
        UsedFileSetSha256 = '29a1bcc593f7b655228ea1deb6340c41047fbe2ea7a2d20b3c888057e3770c0c'
        NoticeEvidence = @(
            [pscustomobject]@{ License = 'BSL-1.0'; SourcePath = 'contrib/utf8cpp/doc/LICENSE'; SbomPath = 'contrib/utf8cpp/doc/LICENSE'; ExpectedSha256 = 'c9bff75738922193e67fa726fa225535870d2aa1059f91452c411736284ad566' }
        )
    }
    [pscustomobject]@{
        ClosureKey = 'zip'
        Component = 'kuba--zip'
        Version = '0.3.0'
        Licenses = @('Unlicense')
        UsedFileCount = 2
        UsedFileSetSha256 = 'b643dff91dba634e8a2e44859a83208418cb88c0d39ffe6611bc911d2f054133'
        NoticeEvidence = @(
            [pscustomobject]@{ License = 'Unlicense'; SourcePath = 'contrib/zip/UNLICENSE'; SbomPath = 'contrib/zip/UNLICENSE'; ExpectedSha256 = '6a872e74952f8f332fc8eec087bf08ada61c4ef5cab5266de3a6dfb3e0ee309e' }
        )
    }
    [pscustomobject]@{
        ClosureKey = 'miniz'
        Component = 'miniz'
        Version = '3.0.0-header'
        Licenses = @('MIT OR Unlicense')
        UsedFileCount = 1
        UsedFileSetSha256 = 'ccc0dd6eef59502e6d9aa1774b21ce1e2038d8448baf6d28bdf878d868a5a67c'
        NoticeEvidence = @(
            [pscustomobject]@{ License = 'MIT OR Unlicense'; SourcePath = 'contrib/zip/src/miniz.h'; SbomPath = 'contrib/zip/src/miniz.h'; ExpectedSha256 = 'bba5c196415bda01b460d2dd8be779189bb228c2802b5b61dd20eae5c3921b06' }
        )
    }
    [pscustomobject]@{
        ClosureKey = 'zlib'
        Component = 'zlib'
        Version = '1.2.13'
        Licenses = @('Zlib')
        UsedFileCount = 25
        UsedFileSetSha256 = '2548d872937793a4eb1ced0641cad59eaad3c1ec0c6f43a271c2637989cd8b94'
        NoticeEvidence = @(
            [pscustomobject]@{ License = 'Zlib'; SourcePath = 'contrib/zlib/LICENSE'; SbomPath = 'contrib/zlib/LICENSE'; ExpectedSha256 = '845efc77857d485d91fb3e0b884aaa929368c717ae8186b66fe1ed2495753243' }
        )
    }
)

try {
    foreach ($requiredPath in @($closureReportResolved, $sourceSnapshotReportResolved, $sbomPathResolved)) {
        if (-not (Test-Path -LiteralPath $requiredPath -PathType Leaf)) {
            throw "Required report does not exist: $requiredPath"
        }
    }

    if (-not (Test-Path -LiteralPath $buildSourceResolved -PathType Container)) {
        throw "Build source directory does not exist: $buildSourceResolved"
    }

    $closure = Get-Content -LiteralPath $closureReportResolved -Raw | ConvertFrom-Json
    $sourceSnapshot = Get-Content -LiteralPath $sourceSnapshotReportResolved -Raw | ConvertFrom-Json
    $sbom = Get-Content -LiteralPath $sbomPathResolved -Raw | ConvertFrom-Json

    Test-ExpectedValue $issues 'Closure status' $closure.status 'Pass'
    Test-ExpectedValue $issues 'Assimp requested tag' $closure.assimp.requestedTag $ExpectedAssimpTag
    Test-ExpectedValue $issues 'Assimp tag revision' $closure.assimp.tagCommit $ExpectedAssimpRevision
    Test-ExpectedValue $issues 'Assimp source archive SHA-256' $closure.assimp.sourceArchiveSha256 $ExpectedAssimpArchiveSha256
    Test-ExpectedInt $issues 'Assimp Release source count' ([int]$closure.assimp.releaseProjectSources) 232
    Test-ExpectedInt $issues 'Assimp Release object mapping count' ([int]$closure.assimp.releaseObjectMappings) 232

    Test-ExpectedValue $issues 'Source snapshot status' $sourceSnapshot.Status 'Pass'
    Test-ExpectedValue $issues 'Source snapshot archive SHA-256' $sourceSnapshot.Inputs.ExpectedArchiveSha256 $ExpectedAssimpArchiveSha256
    Test-ExpectedValue $issues 'Source snapshot archive manifest' $sourceSnapshot.Archive.CanonicalContentManifestSha256 $sourceSnapshot.Source.CanonicalContentManifestSha256
    Test-ExpectedInt $issues 'Source snapshot missing paths' ([int]$sourceSnapshot.Comparison.MissingFileCount) 0
    Test-ExpectedInt $issues 'Source snapshot extra paths' ([int]$sourceSnapshot.Comparison.ExtraFileCount) 0
    Test-ExpectedInt $issues 'Source snapshot modified paths' ([int]$sourceSnapshot.Comparison.ModifiedFileCount) 0
    Test-ExpectedInt $issues 'Source snapshot archive file count' ([int]$sourceSnapshot.Archive.FileCount) 2940
    Test-ExpectedInt $issues 'Source snapshot source file count' ([int]$sourceSnapshot.Source.FileCount) 2940
    Test-ExpectedValue $issues 'CycloneDX spec version' $sbom.specVersion '1.6'

    $closureComponents = @($closure.components)
    $expectedClosureKeys = @($contracts | Where-Object { -not [string]::IsNullOrWhiteSpace($_.ClosureKey) } | ForEach-Object { $_.ClosureKey })
    Test-ExpectedSequence $issues 'Closure component keys' @($closureComponents | ForEach-Object { $_.key }) $expectedClosureKeys

    foreach ($contract in $contracts) {
        $closureComponent = $null
        $usedFileCount = $contract.UsedFileCount
        $usedFileSetSha256 = $contract.UsedFileSetSha256
        $usedFiles = @()

        if (-not [string]::IsNullOrWhiteSpace($contract.ClosureKey)) {
            $matches = @($closureComponents | Where-Object { $_.key -eq $contract.ClosureKey })
            if ($matches.Count -ne 1) {
                Add-Issue $issues "Closure component '$($contract.ClosureKey)' expected one record but found $($matches.Count)."
            }
            else {
                $closureComponent = $matches[0]
                Test-ExpectedInt $issues "Closure $($contract.ClosureKey) used file count" ([int]$closureComponent.usedFileCount) $contract.UsedFileCount
                Test-ExpectedValue $issues "Closure $($contract.ClosureKey) used file set SHA-256" $closureComponent.usedFileSetSha256 $contract.UsedFileSetSha256
                $usedFiles = @($closureComponent.usedFiles | ForEach-Object { $_.ToString() })
                foreach ($usedFile in $usedFiles) {
                    $usedFilePath = Get-SafeSourcePath $buildSourceResolved $usedFile
                    if (-not (Test-Path -LiteralPath $usedFilePath -PathType Leaf)) {
                        Add-Issue $issues "Compiler-read source does not exist for $($contract.Component): $usedFile"
                    }
                }
            }
        }

        $sbomMatches = @($sbom.components | Where-Object { $_.name -eq $contract.Component } | Where-Object { Test-SbomNoticeEvidence $_ $contract.NoticeEvidence })
        if ($sbomMatches.Count -ne 1) {
            Add-Issue $issues "SBOM component '$($contract.Component)' with matching notice evidence expected one record but found $($sbomMatches.Count)."
            $sbomComponent = $null
        }
        else {
            $sbomComponent = $sbomMatches[0]
            Test-ExpectedValue $issues "SBOM $($contract.Component) version" $sbomComponent.version $contract.Version
            Test-ExpectedSequence $issues "SBOM $($contract.Component) licenses" (Get-SbomLicenseValues $sbomComponent) @($contract.Licenses | Sort-Object)
        }

        $noticeEvidence = [System.Collections.Generic.List[object]]::new()
        foreach ($evidence in $contract.NoticeEvidence) {
            $sourceFilePath = Get-SafeSourcePath $buildSourceResolved $evidence.SourcePath
            if (-not (Test-Path -LiteralPath $sourceFilePath -PathType Leaf)) {
                Add-Issue $issues "Notice source does not exist for $($contract.Component): $($evidence.SourcePath)"
                continue
            }

            $actualSha256 = Get-Sha256ForFile $sourceFilePath
            Test-ExpectedValue $issues "Notice source $($evidence.SourcePath) SHA-256" $actualSha256 $evidence.ExpectedSha256
            $noticeEvidence.Add([pscustomobject]@{
                License = $evidence.License
                SourcePath = $evidence.SourcePath
                Sha256 = $actualSha256
                Bytes = [int64](Get-Item -LiteralPath $sourceFilePath).Length
            })
        }

        $entries.Add([pscustomobject]@{
            Component = $contract.Component
            Version = $contract.Version
            Licenses = @($contract.Licenses | Sort-Object)
            ClosureKey = $contract.ClosureKey
            UsedFileCount = $usedFileCount
            UsedFileSetSha256 = $usedFileSetSha256
            UsedFiles = @($usedFiles)
            SbomBomRef = if ($null -eq $sbomComponent) { $null } else { $sbomComponent.'bom-ref' }
            NoticeEvidence = @($noticeEvidence)
        })
    }
}
catch {
    Add-Issue $issues "Exception|$($_.Exception.Message)"
}

$status = if ($issues.Count -eq 0) { 'Pass' } else { 'Fail' }
$noticeEvidenceCount = @($entries | ForEach-Object { @($_.NoticeEvidence).Count } | Measure-Object -Sum).Sum
$report = [ordered]@{
    SchemaVersion = '1.0'
    Status = $status
    Scope = [ordered]@{
        CandidateNoticeManifest = $true
        FinalThirdPartyNotices = $false
        LegalApproval = $false
        DistributionApproval = $false
        Claim = 'Fixed clean Open3D Release Assimp core plus compiler-read contrib closure source-notice evidence only'
    }
    Inputs = [ordered]@{
        ClosureReportPath = $closureReportResolved
        SourceSnapshotReportPath = $sourceSnapshotReportResolved
        SbomPath = $sbomPathResolved
        BuildSourceDirectory = $buildSourceResolved
        ExpectedAssimpTag = $ExpectedAssimpTag
        ExpectedAssimpRevision = $ExpectedAssimpRevision.ToLowerInvariant()
        ExpectedAssimpArchiveSha256 = $ExpectedAssimpArchiveSha256.ToLowerInvariant()
    }
    Summary = [ordered]@{
        EntryCount = $entries.Count
        ClosureComponentCount = @($entries | Where-Object { -not [string]::IsNullOrWhiteSpace($_.ClosureKey) }).Count
        CompilerReadFileCount = @($entries | Where-Object { -not [string]::IsNullOrWhiteSpace($_.ClosureKey) } | ForEach-Object { $_.UsedFileCount } | Measure-Object -Sum).Sum
        NoticeEvidenceCount = $noticeEvidenceCount
        NoticeContractSha256 = Get-NoticeContractSha256 @($entries)
    }
    Entries = @($entries)
    Issues = @($issues)
}

$reportDirectory = Split-Path -Parent $reportPathResolved
New-Item -ItemType Directory -Path $reportDirectory -Force | Out-Null
$report | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $reportPathResolved -Encoding utf8

Write-Output ('AssimpClosureNoticeManifest|{0}|entries={1}|closureComponents={2}|compilerFiles={3}|noticeEvidence={4}|issues={5}' -f $status, $report.Summary.EntryCount, $report.Summary.ClosureComponentCount, $report.Summary.CompilerReadFileCount, $report.Summary.NoticeEvidenceCount, $issues.Count)
Write-Output ('Report|{0}' -f $reportPathResolved)

if ($status -ne 'Pass') {
    exit 1
}
