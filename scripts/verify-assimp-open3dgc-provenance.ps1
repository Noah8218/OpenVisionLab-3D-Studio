[CmdletBinding()]
param(
    [string]$KhronosRepositoryDirectory = 'artifacts\dependency-candidates\assimp-open3dgc-intake-20260716\official-khronos-gltf',
    [string]$AssimpRepositoryDirectory = 'artifacts\dependency-candidates\assimp-kubazip-provenance-20260716\official-assimp',
    [string]$BuildSourceDirectory = 'artifacts\o3d-clean\b\assimp\src\ext_assimp\contrib\Open3DGC',
    [string]$ClosureReportPath = 'artifacts\dependency-candidates\assimp-closure-20260716\assimp-closure.json',
    [string]$WorkingDirectory = 'artifacts\dependency-candidates\assimp-open3dgc-provenance-20260716\open3dgc-provenance-verification-work',
    [string]$ReportPath = 'artifacts\dependency-candidates\assimp-open3dgc-provenance-20260716\open3dgc-provenance.json',
    [string]$ExpectedKhronosRemote = 'https://github.com/KhronosGroup/glTF.git',
    [string]$ExpectedKhronosBranchRef = 'refs/remotes/origin/mesh-compression-open3dgc',
    [string]$ExpectedKhronosRevision = '7b61d5e065f98058fa12fadfec821546f486d960',
    [string]$ExpectedAssimpRemote = 'https://github.com/assimp/assimp.git',
    [string]$ExpectedAssimpTag = 'v5.4.2',
    [string]$ExpectedAssimpRevision = 'ddb74c2bbdee1565dda667e85f0c82a0588c8053',
    [string]$ExpectedAssimpImportRevision = '054820e6ffc03f1a914f2bc688d7f030cf01894b'
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
$khronosRemote = $null
$assimpRemote = $null
$khronosBranchRevision = $null
$assimpTagRevision = $null
$compilerInputContract = $null
$licenseContract = $null
$actualDeltaFiles = @()
$khronosRepositoryResolved = Resolve-WorkspacePath $KhronosRepositoryDirectory
$assimpRepositoryResolved = Resolve-WorkspacePath $AssimpRepositoryDirectory
$buildSourceResolved = Resolve-WorkspacePath $BuildSourceDirectory
$closureReportResolved = Resolve-WorkspacePath $ClosureReportPath
$workingDirectoryResolved = Resolve-WorkspacePath $WorkingDirectory
$reportPathResolved = Resolve-WorkspacePath $ReportPath

$files = @(
    [pscustomobject]@{ File = 'o3dgcAdjacencyInfo.h'; KhronosPath = 'COLLADA2GLTF/dependencies/o3dgc/src/o3dgc_common_lib/inc/o3dgcAdjacencyInfo.h' },
    [pscustomobject]@{ File = 'o3dgcArithmeticCodec.cpp'; KhronosPath = 'COLLADA2GLTF/dependencies/o3dgc/src/o3dgc_common_lib/src/o3dgcArithmeticCodec.cpp' },
    [pscustomobject]@{ File = 'o3dgcArithmeticCodec.h'; KhronosPath = 'COLLADA2GLTF/dependencies/o3dgc/src/o3dgc_common_lib/inc/o3dgcArithmeticCodec.h' },
    [pscustomobject]@{ File = 'o3dgcBinaryStream.h'; KhronosPath = 'COLLADA2GLTF/dependencies/o3dgc/src/o3dgc_common_lib/inc/o3dgcBinaryStream.h' },
    [pscustomobject]@{ File = 'o3dgcCommon.h'; KhronosPath = 'COLLADA2GLTF/dependencies/o3dgc/src/o3dgc_common_lib/inc/o3dgcCommon.h' },
    [pscustomobject]@{ File = 'o3dgcDVEncodeParams.h'; KhronosPath = 'COLLADA2GLTF/dependencies/o3dgc/src/o3dgc_common_lib/inc/o3dgcDVEncodeParams.h' },
    [pscustomobject]@{ File = 'o3dgcDynamicVector.h'; KhronosPath = 'COLLADA2GLTF/dependencies/o3dgc/src/o3dgc_common_lib/inc/o3dgcDynamicVector.h' },
    [pscustomobject]@{ File = 'o3dgcDynamicVectorDecoder.cpp'; KhronosPath = 'COLLADA2GLTF/dependencies/o3dgc/src/o3dgc_decode_lib/src/o3dgcDynamicVectorDecoder.cpp' },
    [pscustomobject]@{ File = 'o3dgcDynamicVectorDecoder.h'; KhronosPath = 'COLLADA2GLTF/dependencies/o3dgc/src/o3dgc_decode_lib/inc/o3dgcDynamicVectorDecoder.h' },
    [pscustomobject]@{ File = 'o3dgcDynamicVectorEncoder.cpp'; KhronosPath = 'COLLADA2GLTF/dependencies/o3dgc/src/o3dgc_encode_lib/src/o3dgcDynamicVectorEncoder.cpp' },
    [pscustomobject]@{ File = 'o3dgcDynamicVectorEncoder.h'; KhronosPath = 'COLLADA2GLTF/dependencies/o3dgc/src/o3dgc_encode_lib/inc/o3dgcDynamicVectorEncoder.h' },
    [pscustomobject]@{ File = 'o3dgcFIFO.h'; KhronosPath = 'COLLADA2GLTF/dependencies/o3dgc/src/o3dgc_common_lib/inc/o3dgcFIFO.h' },
    [pscustomobject]@{ File = 'o3dgcIndexedFaceSet.h'; KhronosPath = 'COLLADA2GLTF/dependencies/o3dgc/src/o3dgc_common_lib/inc/o3dgcIndexedFaceSet.h' },
    [pscustomobject]@{ File = 'o3dgcIndexedFaceSet.inl'; KhronosPath = 'COLLADA2GLTF/dependencies/o3dgc/src/o3dgc_common_lib/inc/o3dgcIndexedFaceSet.inl' },
    [pscustomobject]@{ File = 'o3dgcSC3DMCDecoder.h'; KhronosPath = 'COLLADA2GLTF/dependencies/o3dgc/src/o3dgc_decode_lib/inc/o3dgcSC3DMCDecoder.h' },
    [pscustomobject]@{ File = 'o3dgcSC3DMCDecoder.inl'; KhronosPath = 'COLLADA2GLTF/dependencies/o3dgc/src/o3dgc_decode_lib/inc/o3dgcSC3DMCDecoder.inl' },
    [pscustomobject]@{ File = 'o3dgcSC3DMCEncodeParams.h'; KhronosPath = 'COLLADA2GLTF/dependencies/o3dgc/src/o3dgc_common_lib/inc/o3dgcSC3DMCEncodeParams.h' },
    [pscustomobject]@{ File = 'o3dgcSC3DMCEncoder.h'; KhronosPath = 'COLLADA2GLTF/dependencies/o3dgc/src/o3dgc_encode_lib/inc/o3dgcSC3DMCEncoder.h' },
    [pscustomobject]@{ File = 'o3dgcSC3DMCEncoder.inl'; KhronosPath = 'COLLADA2GLTF/dependencies/o3dgc/src/o3dgc_encode_lib/inc/o3dgcSC3DMCEncoder.inl' },
    [pscustomobject]@{ File = 'o3dgcTimer.h'; KhronosPath = 'COLLADA2GLTF/dependencies/o3dgc/src/o3dgc_common_lib/inc/o3dgcTimer.h' },
    [pscustomobject]@{ File = 'o3dgcTools.cpp'; KhronosPath = 'COLLADA2GLTF/dependencies/o3dgc/src/o3dgc_common_lib/src/o3dgcTools.cpp' },
    [pscustomobject]@{ File = 'o3dgcTriangleFans.cpp'; KhronosPath = 'COLLADA2GLTF/dependencies/o3dgc/src/o3dgc_common_lib/src/o3dgcTriangleFans.cpp' },
    [pscustomobject]@{ File = 'o3dgcTriangleFans.h'; KhronosPath = 'COLLADA2GLTF/dependencies/o3dgc/src/o3dgc_common_lib/inc/o3dgcTriangleFans.h' },
    [pscustomobject]@{ File = 'o3dgcTriangleListDecoder.h'; KhronosPath = 'COLLADA2GLTF/dependencies/o3dgc/src/o3dgc_decode_lib/inc/o3dgcTriangleListDecoder.h' },
    [pscustomobject]@{ File = 'o3dgcTriangleListDecoder.inl'; KhronosPath = 'COLLADA2GLTF/dependencies/o3dgc/src/o3dgc_decode_lib/inc/o3dgcTriangleListDecoder.inl' },
    [pscustomobject]@{ File = 'o3dgcTriangleListEncoder.h'; KhronosPath = 'COLLADA2GLTF/dependencies/o3dgc/src/o3dgc_encode_lib/inc/o3dgcTriangleListEncoder.h' },
    [pscustomobject]@{ File = 'o3dgcTriangleListEncoder.inl'; KhronosPath = 'COLLADA2GLTF/dependencies/o3dgc/src/o3dgc_encode_lib/inc/o3dgcTriangleListEncoder.inl' },
    [pscustomobject]@{ File = 'o3dgcVector.h'; KhronosPath = 'COLLADA2GLTF/dependencies/o3dgc/src/o3dgc_common_lib/inc/o3dgcVector.h' },
    [pscustomobject]@{ File = 'o3dgcVector.inl'; KhronosPath = 'COLLADA2GLTF/dependencies/o3dgc/src/o3dgc_common_lib/inc/o3dgcVector.inl' }
)

$expectedClosureFiles = @($files | ForEach-Object { 'contrib/Open3DGC/{0}' -f $_.File })
$expectedClosureFileSetSha256 = 'bb290db0bc142ae99b6b07774091db1f07f29e820b544518c609d6559494f410'
$expectedDeltaFiles = @(
    'contrib/Open3DGC/o3dgcAdjacencyInfo.h',
    'contrib/Open3DGC/o3dgcArithmeticCodec.cpp',
    'contrib/Open3DGC/o3dgcArithmeticCodec.h',
    'contrib/Open3DGC/o3dgcBinaryStream.h',
    'contrib/Open3DGC/o3dgcCommon.h',
    'contrib/Open3DGC/o3dgcDynamicVector.h',
    'contrib/Open3DGC/o3dgcFIFO.h',
    'contrib/Open3DGC/o3dgcIndexedFaceSet.h',
    'contrib/Open3DGC/o3dgcSC3DMCDecoder.h',
    'contrib/Open3DGC/o3dgcSC3DMCDecoder.inl',
    'contrib/Open3DGC/o3dgcSC3DMCEncoder.inl',
    'contrib/Open3DGC/o3dgcTimer.h',
    'contrib/Open3DGC/o3dgcTriangleListEncoder.h',
    'contrib/Open3DGC/o3dgcTriangleListEncoder.inl',
    'contrib/Open3DGC/o3dgcVector.h',
    'contrib/Open3DGC/o3dgcVector.inl'
)

try {
    foreach ($value in @($ExpectedKhronosRevision, $ExpectedAssimpRevision, $ExpectedAssimpImportRevision)) {
        if ($value -notmatch '^[0-9A-Fa-f]{40}$') {
            throw "Expected revision has an invalid format: $value"
        }
    }

    foreach ($directory in @($khronosRepositoryResolved, $assimpRepositoryResolved, $buildSourceResolved)) {
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

    $khronosRemote = ((Invoke-GitOutput $khronosRepositoryResolved @('remote', 'get-url', 'origin') 'Khronos glTF upstream remote lookup') | Select-Object -First 1).Trim()
    $assimpRemote = ((Invoke-GitOutput $assimpRepositoryResolved @('remote', 'get-url', 'origin') 'Assimp upstream remote lookup') | Select-Object -First 1).Trim()
    Test-ExpectedValue $issues 'Khronos glTF origin remote' $khronosRemote $ExpectedKhronosRemote
    Test-ExpectedValue $issues 'Assimp origin remote' $assimpRemote $ExpectedAssimpRemote

    $khronosBranchRevision = Get-GitRevision $khronosRepositoryResolved $ExpectedKhronosBranchRef 'Khronos Open3DGC branch revision lookup'
    $assimpTagRevision = Get-GitRevision $assimpRepositoryResolved ('{0}^{{}}' -f $ExpectedAssimpTag) 'Assimp tag resolution'
    [void](Get-GitRevision $khronosRepositoryResolved $ExpectedKhronosRevision 'Khronos Open3DGC snapshot revision lookup')
    [void](Get-GitRevision $assimpRepositoryResolved $ExpectedAssimpImportRevision 'Assimp Open3DGC import revision lookup')
    Test-ExpectedValue $issues 'Khronos Open3DGC branch revision' $khronosBranchRevision $ExpectedKhronosRevision
    Test-ExpectedValue $issues 'Assimp tag revision' $assimpTagRevision $ExpectedAssimpRevision
    Test-ExpectedBoolean $issues 'Khronos snapshot is reachable from Open3DGC branch' (Test-GitAncestor $khronosRepositoryResolved $ExpectedKhronosRevision $khronosBranchRevision 'Khronos Open3DGC branch ancestry check')
    Test-ExpectedBoolean $issues 'Assimp Open3DGC import is ancestor of fixed tag' (Test-GitAncestor $assimpRepositoryResolved $ExpectedAssimpImportRevision $ExpectedAssimpRevision 'Assimp Open3DGC import ancestry check')

    New-Item -ItemType Directory -Path $workingDirectoryResolved -Force | Out-Null
    $khronosArchive = Join-Path $workingDirectoryResolved 'khronos-open3dgc.zip'
    $assimpImportArchive = Join-Path $workingDirectoryResolved 'assimp-open3dgc-import.zip'
    $assimpCurrentArchive = Join-Path $workingDirectoryResolved 'assimp-open3dgc-v5.4.2.zip'
    $khronosExtract = Join-Path $workingDirectoryResolved 'khronos-open3dgc'
    $assimpImportExtract = Join-Path $workingDirectoryResolved 'assimp-open3dgc-import'
    $assimpCurrentExtract = Join-Path $workingDirectoryResolved 'assimp-open3dgc-v5.4.2'
    $khronosPaths = @($files | ForEach-Object { $_.KhronosPath })
    $assimpPaths = @($files | ForEach-Object { 'contrib/Open3DGC/{0}' -f $_.File })

    Export-GitArchive $khronosRepositoryResolved $ExpectedKhronosRevision $khronosPaths $khronosArchive $khronosExtract 'Khronos Open3DGC source export'
    Export-GitArchive $assimpRepositoryResolved $ExpectedAssimpImportRevision $assimpPaths $assimpImportArchive $assimpImportExtract 'Assimp Open3DGC import source export'
    Export-GitArchive $assimpRepositoryResolved $ExpectedAssimpTag $assimpPaths $assimpCurrentArchive $assimpCurrentExtract 'Assimp current Open3DGC source export'

    foreach ($file in $files) {
        $assimpPath = 'contrib/Open3DGC/{0}' -f $file.File
        $khronosBlob = Get-GitBlob $khronosRepositoryResolved $ExpectedKhronosRevision $file.KhronosPath "Khronos Open3DGC $($file.File) blob"
        $importBlob = Get-GitBlob $assimpRepositoryResolved $ExpectedAssimpImportRevision $assimpPath "Assimp Open3DGC import $($file.File) blob"
        $currentBlob = Get-GitBlob $assimpRepositoryResolved $ExpectedAssimpTag $assimpPath "Assimp Open3DGC current $($file.File) blob"
        Test-ExpectedValue $issues "Assimp import $($file.File) blob" $importBlob $khronosBlob

        $khronosRecord = Get-FileRecord (Join-Path $khronosExtract $file.KhronosPath) "Khronos Open3DGC exported $($file.File)"
        $importRecord = Get-FileRecord (Join-Path $assimpImportExtract $assimpPath) "Assimp Open3DGC import exported $($file.File)"
        $currentRecord = Get-FileRecord (Join-Path $assimpCurrentExtract $assimpPath) "Assimp Open3DGC current exported $($file.File)"
        $buildRecord = Get-FileRecord (Join-Path $buildSourceResolved $file.File) "Build Open3DGC $($file.File)"
        Test-ExpectedValue $issues "Khronos exported $($file.File) blob" $khronosRecord.GitBlobSha $khronosBlob
        Test-ExpectedValue $issues "Assimp import exported $($file.File) blob" $importRecord.GitBlobSha $khronosBlob
        Test-ExpectedValue $issues "Assimp current exported $($file.File) blob" $currentRecord.GitBlobSha $currentBlob
        Test-ExpectedValue $issues "Build $($file.File) blob" $buildRecord.GitBlobSha $currentBlob

        $sourceRecords.Add([pscustomobject]@{
            File = $file.File
            KhronosPath = $file.KhronosPath
            KhronosSnapshot = $khronosRecord
            AssimpImport = $importRecord
            AssimpCurrent = $currentRecord
            BuildInput = $buildRecord
            AssimpChangedAfterImport = -not $currentBlob.Equals($importBlob, [System.StringComparison]::OrdinalIgnoreCase)
        })
    }

    $actualDeltaFiles = @(Invoke-GitOutput $assimpRepositoryResolved @('diff', '--name-only', $ExpectedAssimpImportRevision, $ExpectedAssimpTag, '--', 'contrib/Open3DGC') 'Assimp Open3DGC fixed-tag delta' | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
    Test-ExpectedSequence $issues 'Assimp Open3DGC fixed-tag delta files' $actualDeltaFiles $expectedDeltaFiles

    $contribRoot = Split-Path -Parent $buildSourceResolved
    $extAssimpRoot = Split-Path -Parent $contribRoot
    $codeCmakePath = Join-Path (Join-Path $extAssimpRoot 'code') 'CMakeLists.txt'
    if (-not (Test-Path -LiteralPath $codeCmakePath -PathType Leaf)) {
        throw "Required Open3DGC compiler-input contract file does not exist: $codeCmakePath"
    }

    $codeCmakeText = Get-Content -LiteralPath $codeCmakePath -Raw
    $closure = Get-Content -LiteralPath $closureReportResolved -Raw | ConvertFrom-Json
    $closureComponents = @($closure.Components | Where-Object { $_.key -eq 'Open3DGC' })
    if ($closureComponents.Count -ne 1) {
        throw "Expected one Open3DGC component in closure report but found $($closureComponents.Count)."
    }

    $closureComponent = $closureComponents[0]
    $closureUsedFiles = @($closureComponent.usedFiles | ForEach-Object { $_.ToString() })
    $cmakeFileMatches = @($files | ForEach-Object {
        $relativePath = '../contrib/Open3DGC/{0}' -f $_.File
        [pscustomobject]@{ Path = $relativePath; Present = $codeCmakeText.Contains($relativePath) }
    })
    $compilerInputContract = [ordered]@{
        SourceListMatches = [regex]::IsMatch($codeCmakeText, '(?is)SET\s*\(\s*open3dgc_SRCS\b')
        FeatureDefinitionMatches = [regex]::IsMatch($codeCmakeText, '(?is)ADD_DEFINITIONS\s*\(\s*-DASSIMP_IMPORTER_GLTF_USE_OPEN3DGC=1\s*\)')
        FileEntries = @($cmakeFileMatches)
        ClosureComponentKey = $closureComponent.key
        ClosureUsedFileCount = [int]$closureComponent.usedFileCount
        ClosureUsedFiles = @($closureUsedFiles)
        ClosureUsedFileSetSha256 = $closureComponent.usedFileSetSha256
    }
    Test-ExpectedBoolean $issues 'Compiler input contract source list' ([bool]$compilerInputContract.SourceListMatches)
    Test-ExpectedBoolean $issues 'Compiler input contract feature definition' ([bool]$compilerInputContract.FeatureDefinitionMatches)
    foreach ($entry in $cmakeFileMatches) {
        Test-ExpectedBoolean $issues "Compiler input contract $($entry.Path)" ([bool]$entry.Present)
    }
    Test-ExpectedValue $issues 'Closure component key' $compilerInputContract.ClosureComponentKey 'Open3DGC'
    if ($compilerInputContract.ClosureUsedFileCount -ne 29) {
        $issues.Add("Closure used file count expected 29 but was $($compilerInputContract.ClosureUsedFileCount).")
    }
    Test-ExpectedSequence $issues 'Closure used files' $closureUsedFiles $expectedClosureFiles
    Test-ExpectedValue $issues 'Closure used file set SHA-256' $compilerInputContract.ClosureUsedFileSetSha256 $expectedClosureFileSetSha256

    $mitText = (Invoke-GitOutput $khronosRepositoryResolved @('show', ('{0}:{1}' -f $ExpectedKhronosRevision, 'COLLADA2GLTF/dependencies/o3dgc/src/o3dgc_common_lib/inc/o3dgcCommon.h')) 'Khronos Open3DGC MIT license evidence') -join "`n"
    $arithmeticHeaderText = (Invoke-GitOutput $khronosRepositoryResolved @('show', ('{0}:{1}' -f $ExpectedKhronosRevision, 'COLLADA2GLTF/dependencies/o3dgc/src/o3dgc_common_lib/inc/o3dgcArithmeticCodec.h')) 'Khronos arithmetic header license evidence') -join "`n"
    $arithmeticSourceText = (Invoke-GitOutput $khronosRepositoryResolved @('show', ('{0}:{1}' -f $ExpectedKhronosRevision, 'COLLADA2GLTF/dependencies/o3dgc/src/o3dgc_common_lib/src/o3dgcArithmeticCodec.cpp')) 'Khronos arithmetic source license evidence') -join "`n"
    $licenseContract = [ordered]@{
        Open3DgcMitNoticeMatches = $mitText.Contains('Copyright (c) 2013 Khaled Mammou - Advanced Micro Devices, Inc.') -and $mitText.Contains('Permission is hereby granted, free of charge')
        ArithmeticHeaderBsdTwoClauseMatches = $arithmeticHeaderText.Contains('Redistribution and use in source and binary forms') -and $arithmeticHeaderText.Contains('All rights reserved.')
        ArithmeticSourceBsdTwoClauseMatches = $arithmeticSourceText.Contains('Redistribution and use in source and binary forms') -and $arithmeticSourceText.Contains('All rights reserved.')
    }
    foreach ($property in @('Open3DgcMitNoticeMatches', 'ArithmeticHeaderBsdTwoClauseMatches', 'ArithmeticSourceBsdTwoClauseMatches')) {
        Test-ExpectedBoolean $issues "License contract $property" ([bool]$licenseContract[$property])
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
        KhronosRepositoryDirectory = $khronosRepositoryResolved
        AssimpRepositoryDirectory = $assimpRepositoryResolved
        BuildSourceDirectory = $buildSourceResolved
        ClosureReportPath = $closureReportResolved
        WorkingDirectory = $workingDirectoryResolved
        ExpectedKhronosRemote = $ExpectedKhronosRemote
        ExpectedKhronosBranchRef = $ExpectedKhronosBranchRef
        ExpectedKhronosRevision = $ExpectedKhronosRevision.ToLowerInvariant()
        ExpectedAssimpRemote = $ExpectedAssimpRemote
        ExpectedAssimpTag = $ExpectedAssimpTag
        ExpectedAssimpRevision = $ExpectedAssimpRevision.ToLowerInvariant()
        ExpectedAssimpImportRevision = $ExpectedAssimpImportRevision.ToLowerInvariant()
    }
    Git = [ordered]@{
        KhronosRemote = $khronosRemote
        AssimpRemote = $assimpRemote
        KhronosBranchRevision = $khronosBranchRevision
        AssimpTagRevision = $assimpTagRevision
    }
    CompilerInputs = $compilerInputContract
    LicenseContract = $licenseContract
    AssimpPostImportDeltaFiles = @($actualDeltaFiles)
    SourceIdentity = @($sourceRecords)
    Issues = @($issues)
}

$reportDirectory = Split-Path -Parent $reportPathResolved
if (-not [string]::IsNullOrWhiteSpace($reportDirectory)) {
    New-Item -ItemType Directory -Path $reportDirectory -Force | Out-Null
}
$report | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $reportPathResolved -Encoding utf8

Write-Output ('AssimpOpen3DGCProvenance|{0}|khronos={1}|assimp={2}|files={3}|deltas={4}|issues={5}' -f $status, $ExpectedKhronosRevision.ToLowerInvariant(), $ExpectedAssimpRevision.ToLowerInvariant(), $files.Count, $actualDeltaFiles.Count, $issues.Count)
Write-Output ('Report|{0}' -f $reportPathResolved)

if ($status -ne 'Pass') {
    exit 1
}
