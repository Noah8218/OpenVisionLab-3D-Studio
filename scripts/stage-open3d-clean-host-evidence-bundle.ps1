[CmdletBinding()]
param(
    [string]$OutputDirectory = 'artifacts/open3d-clean-host-evidence-bundle-20260716',
    [string]$CandidateRuntimeDirectory = 'artifacts/o3d-vtk-candidate-20260716/probe-build/Release',
    [string]$InputDirectory = 'artifacts/public-sample-candidates/open3d-demo-icp-20260713/extracted',
    [string]$BaselinePath = 'artifacts/o3d-vtk-candidate-20260716/probe-results/demo-current/candidate_repeat_1.json',
    [string]$VerifierPath = 'scripts/verify-open3d-runtime-prerequisites.ps1',
    [string]$ProtocolPath = 'docs/OPENVISIONLAB_3D_OPEN3D_CLEAN_HOST_EXECUTION_PROTOCOL_20260716.md'
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot

function Resolve-WorkspacePath([string]$Path) {
    if ([System.IO.Path]::IsPathRooted($Path)) {
        return [System.IO.Path]::GetFullPath($Path)
    }

    return [System.IO.Path]::GetFullPath((Join-Path $repoRoot $Path))
}

function Assert-WorkspaceOutputPath([string]$Path) {
    $root = [System.IO.Path]::GetFullPath($repoRoot).TrimEnd([System.IO.Path]::DirectorySeparatorChar, [System.IO.Path]::AltDirectorySeparatorChar)
    $prefix = $root + [System.IO.Path]::DirectorySeparatorChar
    if (-not $Path.StartsWith($prefix, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "OutputDirectory must be inside the workspace: $Path"
    }
}

function Get-ValidatedFileRecord($Entry) {
    if (-not (Test-Path -LiteralPath $Entry.Source -PathType Leaf)) {
        throw "Required source file is missing: $($Entry.Source)"
    }

    $item = Get-Item -LiteralPath $Entry.Source
    if ($item.Length -ne $Entry.ExpectedSize) {
        throw "Unexpected source size for $($Entry.RelativePath): expected $($Entry.ExpectedSize), actual $($item.Length)"
    }

    $hash = (Get-FileHash -LiteralPath $Entry.Source -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($hash -cne $Entry.ExpectedSha256) {
        throw "Unexpected source hash for $($Entry.RelativePath): expected $($Entry.ExpectedSha256), actual $hash"
    }

    return [pscustomobject]@{
        Scope = $Entry.Scope
        RelativePath = $Entry.RelativePath.Replace('\', '/')
        Size = $item.Length
        Sha256 = $hash
    }
}

$runtimeDirectory = Resolve-WorkspacePath $CandidateRuntimeDirectory
$inputDirectory = Resolve-WorkspacePath $InputDirectory
$baselineFile = Resolve-WorkspacePath $BaselinePath
$verifierFile = Resolve-WorkspacePath $VerifierPath
$protocolFile = Resolve-WorkspacePath $ProtocolPath
$outputDirectory = Resolve-WorkspacePath $OutputDirectory

Assert-WorkspaceOutputPath $outputDirectory
if (Test-Path -LiteralPath $outputDirectory) {
    throw "OutputDirectory already exists and will not be overwritten: $outputDirectory"
}

$forbiddenBundleRuntimeNames = @('MSVCP140.dll', 'VCRUNTIME140.dll', 'VCRUNTIME140_1.dll', 'VCOMP140.dll')
foreach ($name in $forbiddenBundleRuntimeNames) {
    if (Test-Path -LiteralPath (Join-Path $runtimeDirectory $name) -PathType Leaf) {
        throw "Candidate runtime directory must not contain Microsoft VC/OpenMP runtime file: $name"
    }
}

$entries = @(
    [pscustomobject]@{ Scope = 'Bundle'; Source = (Join-Path $runtimeDirectory 'open3d-registration-probe.exe'); RelativePath = 'bundle\open3d-registration-probe.exe'; ExpectedSize = 70656; ExpectedSha256 = 'a4425b268bf7f7ab0ff58ad0e006f878a023a223cce7df6bfab5e363a66293fb' },
    [pscustomobject]@{ Scope = 'Bundle'; Source = (Join-Path $runtimeDirectory 'Open3D.dll'); RelativePath = 'bundle\Open3D.dll'; ExpectedSize = 58220032; ExpectedSha256 = '88ab8ee38f218c9ba02a929f07462554d555e0c131d771a5343777445ec2e19f' },
    [pscustomobject]@{ Scope = 'Bundle'; Source = (Join-Path $runtimeDirectory 'tbb12.dll'); RelativePath = 'bundle\tbb12.dll'; ExpectedSize = 328704; ExpectedSha256 = '2785a4688dcb2aa197a49b4be7a94b471a1fb951bd8efe46476f5ba95ad449e3' },
    [pscustomobject]@{ Scope = 'Tool'; Source = $verifierFile; RelativePath = 'tools\verify-open3d-runtime-prerequisites.ps1'; ExpectedSize = 4842; ExpectedSha256 = '6e5b1c2cbf62cf6998340207ed73d5965281b6dd0563ccf170a4c8115210fbc5' },
    [pscustomobject]@{ Scope = 'Baseline'; Source = $baselineFile; RelativePath = 'baseline\candidate_repeat_1.json'; ExpectedSize = 793; ExpectedSha256 = '8eae43d0bb64f7047a241a3a681aa9f9c60124820d1c955e79893e23151ee72f' },
    [pscustomobject]@{ Scope = 'Input'; Source = (Join-Path $inputDirectory 'cloud_bin_0.pcd'); RelativePath = 'input\cloud_bin_0.pcd'; ExpectedSize = 6362965; ExpectedSha256 = 'e1e100802c29ef454c6b523084668ee0e2f365ec52eaeebe79ae804c20447b15' },
    [pscustomobject]@{ Scope = 'Input'; Source = (Join-Path $inputDirectory 'cloud_bin_1.pcd'); RelativePath = 'input\cloud_bin_1.pcd'; ExpectedSize = 4410901; ExpectedSha256 = 'a4c3dc0ad7b1279736491b9b2638991d4c808605997be4f9ab174c24a9fa6e52' },
    [pscustomobject]@{ Scope = 'Input'; Source = (Join-Path $inputDirectory 'init.log'); RelativePath = 'input\init.log'; ExpectedSize = 385; ExpectedSha256 = '609896dbdd666b7ae0bb7390c52730ca8aca10c2b5886b895acdb36f4a202156' }
)

$records = @($entries | ForEach-Object { Get-ValidatedFileRecord $_ })
if (-not (Test-Path -LiteralPath $protocolFile -PathType Leaf)) {
    throw "Required clean-host protocol is missing: $protocolFile"
}

$protocolItem = Get-Item -LiteralPath $protocolFile
$records += [pscustomobject]@{
    Scope = 'Instruction'
    RelativePath = 'instructions/OPENVISIONLAB_3D_OPEN3D_CLEAN_HOST_EXECUTION_PROTOCOL_20260716.md'
    Size = $protocolItem.Length
    Sha256 = (Get-FileHash -LiteralPath $protocolFile -Algorithm SHA256).Hash.ToLowerInvariant()
}

New-Item -ItemType Directory -Path $outputDirectory | Out-Null
foreach ($entry in $entries) {
    $destination = Join-Path $outputDirectory $entry.RelativePath
    New-Item -ItemType Directory -Path (Split-Path -Parent $destination) -Force | Out-Null
    Copy-Item -LiteralPath $entry.Source -Destination $destination
}

$protocolDestination = Join-Path $outputDirectory 'instructions\OPENVISIONLAB_3D_OPEN3D_CLEAN_HOST_EXECUTION_PROTOCOL_20260716.md'
New-Item -ItemType Directory -Path (Split-Path -Parent $protocolDestination) -Force | Out-Null
Copy-Item -LiteralPath $protocolFile -Destination $protocolDestination

foreach ($record in $records) {
    $stagedPath = Join-Path $outputDirectory $record.RelativePath
    $stagedItem = Get-Item -LiteralPath $stagedPath
    $stagedHash = (Get-FileHash -LiteralPath $stagedPath -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($stagedItem.Length -ne $record.Size -or $stagedHash -cne $record.Sha256) {
        throw "Staged file verification failed: $($record.RelativePath)"
    }
}

$manifest = [pscustomobject]@{
    SchemaVersion = '1.0'
    GeneratedUtc = [DateTime]::UtcNow.ToString('o')
    Candidate = 'Open3D 0.19.0 with controlled VTK 9.1.0 candidate'
    MinimumRuntimeVersion = '14.44.35211.0'
    InstallerIncluded = $false
    RequiredProtocol = 'instructions/OPENVISIONLAB_3D_OPEN3D_CLEAN_HOST_EXECUTION_PROTOCOL_20260716.md'
    Files = $records
    ClaimLimit = 'staging-only; clean-host installation, distribution approval, and product integration remain unverified'
}

$manifestPath = Join-Path $outputDirectory 'clean-host-evidence-manifest.json'
$manifest | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $manifestPath -Encoding utf8

"StagedCleanHostEvidenceBundle|Pass|files=$($records.Count)|output=$outputDirectory|manifest=$manifestPath"
