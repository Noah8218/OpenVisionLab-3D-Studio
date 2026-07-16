[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$RuntimeDirectory,
    [Parameter(Mandatory)]
    [version]$MinimumRuntimeVersion,
    [string]$ReportPath = 'artifacts/open3d-runtime-prerequisites/report.txt',
    [string]$SystemDirectory = [Environment]::GetFolderPath([Environment+SpecialFolder]::System),
    [string]$RegistryPath = 'HKLM:\SOFTWARE\Microsoft\VisualStudio\14.0\VC\Runtimes\x64'
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot

function Resolve-WorkspacePath([string]$Path) {
    if ([System.IO.Path]::IsPathRooted($Path)) {
        return [System.IO.Path]::GetFullPath($Path)
    }

    return [System.IO.Path]::GetFullPath((Join-Path $repoRoot $Path))
}

$runtimePath = Resolve-WorkspacePath $RuntimeDirectory
$systemPath = Resolve-WorkspacePath $SystemDirectory
$outputPath = Resolve-WorkspacePath $ReportPath
$issues = [System.Collections.Generic.List[string]]::new()
$records = [System.Collections.Generic.List[string]]::new()

function Add-FileEvidence([string]$Scope, [string]$Directory, [string[]]$Names) {
    $validCount = 0
    foreach ($name in $Names) {
        $path = Join-Path $Directory $name
        $valid = Test-Path -LiteralPath $path -PathType Leaf
        if ($valid) {
            $item = Get-Item -LiteralPath $path
            $valid = $item.Length -gt 0
        }

        if (-not $valid) {
            $issues.Add("$Scope file is missing or empty: $name")
            $records.Add("File|scope=$Scope|name=$name|valid=False")
            continue
        }

        $validCount++
        $hash = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant()
        $version = [string]$item.VersionInfo.FileVersion
        $records.Add("File|scope=$Scope|name=$name|valid=True|size=$($item.Length)|version=$version|sha256=$hash")
    }

    return $validCount
}

function Add-ForbiddenBundleRuntimeEvidence([string]$Directory, [string[]]$Names) {
    $presentCount = 0
    foreach ($name in $Names) {
        $path = Join-Path $Directory $name
        if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
            $records.Add("File|scope=BundleForbiddenRuntime|name=$name|present=False")
            continue
        }

        $item = Get-Item -LiteralPath $path
        $presentCount++
        $hash = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant()
        $records.Add("File|scope=BundleForbiddenRuntime|name=$name|present=True|size=$($item.Length)|sha256=$hash")
        $issues.Add("Bundle must not contain Microsoft VC/OpenMP runtime file: $name")
    }

    return $presentCount
}

$bundleNames = @('open3d-registration-probe.exe', 'Open3D.dll', 'tbb12.dll')
$systemNames = @('MSVCP140.dll', 'VCRUNTIME140.dll', 'VCRUNTIME140_1.dll', 'VCOMP140.dll')
$bundleValid = Add-FileEvidence 'Bundle' $runtimePath $bundleNames
$forbiddenBundleRuntimeCount = Add-ForbiddenBundleRuntimeEvidence $runtimePath $systemNames
$systemValid = Add-FileEvidence 'System' $systemPath $systemNames
$installedVersion = $null

if (-not (Test-Path -LiteralPath $RegistryPath)) {
    $issues.Add("VC x64 runtime registry key is missing: $RegistryPath")
}
else {
    $runtimeRegistration = Get-ItemProperty -LiteralPath $RegistryPath
    if ([int]$runtimeRegistration.Installed -ne 1) {
        $issues.Add("VC x64 runtime is not marked installed: $RegistryPath")
    }

    try {
        $installedVersion = [version](([string]$runtimeRegistration.Version).TrimStart('v', 'V'))
    }
    catch {
        $issues.Add("VC x64 runtime version is invalid: $($runtimeRegistration.Version)")
    }

    if ($null -ne $installedVersion -and $installedVersion -lt $MinimumRuntimeVersion) {
        $issues.Add("VC x64 runtime $installedVersion is older than required $MinimumRuntimeVersion")
    }
}

$status = if ($issues.Count -eq 0) { 'Pass' } else { 'Fail' }
$installedText = if ($null -eq $installedVersion) { 'Unavailable' } else { $installedVersion.ToString() }
$report = [System.Collections.Generic.List[string]]::new()
$report.Add("Open3DRuntimePrerequisites|$status|bundle=$bundleValid/$($bundleNames.Count)|adjacentRuntime=$forbiddenBundleRuntimeCount/$($systemNames.Count)|system=$systemValid/$($systemNames.Count)|installedVersion=$installedText|minimumVersion=$MinimumRuntimeVersion")
$report.Add("Input|runtimeDirectory=$runtimePath|systemDirectory=$systemPath|registryPath=$RegistryPath")
$report.AddRange($records)
foreach ($issue in $issues) {
    $report.Add("Issue|$issue")
}
$report.Add('ClaimLimit|preflight-only=True|cleanHostInstall=False|distributionApproval=False')

$reportDirectory = Split-Path -Parent $outputPath
New-Item -ItemType Directory -Path $reportDirectory -Force | Out-Null
$report | Set-Content -LiteralPath $outputPath -Encoding utf8
$report

if ($issues.Count -gt 0) {
    exit 1
}
