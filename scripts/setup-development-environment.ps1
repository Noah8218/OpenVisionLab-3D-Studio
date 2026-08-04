[CmdletBinding()]
param(
    [switch]$CheckOnly,
    [switch]$InstallMissing,
    [string]$ReportPath = 'artifacts\setup\development-environment.txt'
)

$ErrorActionPreference = 'Stop'

if ($CheckOnly -and $InstallMissing) {
    throw 'Choose either -CheckOnly or -InstallMissing, not both.'
}

$mode = if ($InstallMissing) { 'InstallMissing' } else { 'CheckOnly' }
$repoRoot = Split-Path -Parent $PSScriptRoot
$fullReportPath = if ([System.IO.Path]::IsPathRooted($ReportPath)) {
    [System.IO.Path]::GetFullPath($ReportPath)
}
else {
    [System.IO.Path]::GetFullPath((Join-Path $repoRoot $ReportPath))
}

function Refresh-ProcessPath {
    $machinePath = [Environment]::GetEnvironmentVariable('Path', 'Machine')
    $userPath = [Environment]::GetEnvironmentVariable('Path', 'User')
    $env:Path = @($machinePath, $userPath) -join ';'
}

function Get-WindowsState {
    $currentVersion = Get-ItemProperty -LiteralPath 'HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion'
    $build = [int]$currentVersion.CurrentBuildNumber
    $displayVersion = [string]$currentVersion.DisplayVersion
    $productName = if ($build -ge 22000) {
        "Windows 11 $($currentVersion.EditionID)"
    }
    else {
        [string]$currentVersion.ProductName
    }
    $ready = $build -ge 19041 -and [Environment]::Is64BitOperatingSystem
    [pscustomobject]@{
        Name = 'Windows x64'
        Category = 'Application runtime'
        Required = $true
        Ready = $ready
        Version = "$productName $displayVersion build $build"
        Detail = 'Windows 10 build 19041 or later, or Windows 11, on x64.'
        PackageId = $null
    }
}

function Get-DotNetSdkState {
    $command = Get-Command dotnet -ErrorAction SilentlyContinue
    $versions = [System.Collections.Generic.List[version]]::new()
    if ($command) {
        foreach ($line in @(& $command.Source --list-sdks 2>$null)) {
            if ($line -match '^(?<version>\d+\.\d+\.\d+)') {
                $versions.Add([version]$Matches.version)
            }
        }
    }

    $matching = @($versions | Where-Object { $_.Major -eq 10 -and $_ -ge [version]'10.0.300' } | Sort-Object -Descending)
    [pscustomobject]@{
        Name = '.NET 10 SDK'
        Category = 'Source build'
        Required = $true
        Ready = $matching.Count -gt 0
        Version = if ($matching.Count -gt 0) { $matching[0].ToString() } else { 'missing' }
        Detail = 'Minimum supported SDK is 10.0.300, as declared by global.json.'
        PackageId = 'Microsoft.DotNet.SDK.10'
    }
}

function Get-GitState {
    $command = Get-Command git -ErrorAction SilentlyContinue
    $version = if ($command) { ((& $command.Source --version 2>$null) -join ' ').Trim() } else { 'missing' }
    [pscustomobject]@{
        Name = 'Git'
        Category = 'Source checkout'
        Required = $true
        Ready = $null -ne $command
        Version = $version
        Detail = 'Required to clone the repository and preserve source identity.'
        PackageId = 'Git.Git'
    }
}

function Get-PythonState {
    $versionText = $null
    $launcher = Get-Command py -ErrorAction SilentlyContinue
    if ($launcher) {
        $candidate = ((& $launcher.Source -3.13 --version 2>&1) -join ' ').Trim()
        if ($LASTEXITCODE -eq 0 -and $candidate -match '^Python 3\.13(?:\.|$)') {
            $versionText = $candidate
        }
    }

    if (-not $versionText) {
        $python = Get-Command python -ErrorAction SilentlyContinue
        if ($python) {
            $candidate = ((& $python.Source --version 2>&1) -join ' ').Trim()
            if ($LASTEXITCODE -eq 0 -and $candidate -match '^Python 3\.13(?:\.|$)') {
                $versionText = $candidate
            }
        }
    }

    [pscustomobject]@{
        Name = 'Python 3.13'
        Category = 'Full verification'
        Required = $true
        Ready = -not [string]::IsNullOrWhiteSpace($versionText)
        Version = if ($versionText) { $versionText } else { 'missing' }
        Detail = 'Required by the independent C3D and NuGet health verification gates; not required to run the application.'
        PackageId = 'Python.Python.3.13'
    }
}

function Get-PowerShellState {
    [pscustomobject]@{
        Name = 'Windows PowerShell'
        Category = 'Automation'
        Required = $true
        Ready = $PSVersionTable.PSVersion -ge [version]'5.1'
        Version = $PSVersionTable.PSVersion.ToString()
        Detail = 'PowerShell 5.1 or later is required by repository automation.'
        PackageId = $null
    }
}

function Get-WingetState {
    $command = Get-Command winget -ErrorAction SilentlyContinue
    $version = if ($command) { ((& $command.Source --version 2>$null) -join ' ').Trim() } else { 'missing' }
    [pscustomobject]@{
        Name = 'Windows Package Manager'
        Category = 'Setup helper'
        Required = $false
        Ready = $null -ne $command
        Version = $version
        Detail = 'Optional in check-only mode; required only when -InstallMissing is requested.'
        PackageId = $null
    }
}

function Get-EnvironmentState {
    Refresh-ProcessPath
    @(
        Get-WindowsState
        Get-DotNetSdkState
        Get-GitState
        Get-PythonState
        Get-PowerShellState
        Get-WingetState
    )
}

function Install-Utility([string]$PackageId) {
    Write-Host "Installing $PackageId from the official winget source..."
    & winget install `
        --id $PackageId `
        --exact `
        --source winget `
        --accept-package-agreements `
        --accept-source-agreements `
        --disable-interactivity
    if ($LASTEXITCODE -ne 0) {
        throw "winget failed to install $PackageId (exit $LASTEXITCODE)."
    }
}

$states = @(Get-EnvironmentState)
if ($InstallMissing) {
    $winget = $states | Where-Object Name -eq 'Windows Package Manager'
    if (-not $winget.Ready) {
        throw 'Windows Package Manager (winget) is required for -InstallMissing. Install or repair Microsoft App Installer first.'
    }

    foreach ($state in @($states | Where-Object { $_.Required -and -not $_.Ready -and $_.PackageId })) {
        Install-Utility $state.PackageId
    }
    $states = @(Get-EnvironmentState)
}

$requiredStates = @($states | Where-Object Required)
$readyCount = @($requiredStates | Where-Object Ready).Count
$status = if ($readyCount -eq $requiredStates.Count) { 'Ready' } else { 'NeedsAction' }
$lines = [System.Collections.Generic.List[string]]::new()
$lines.Add("DevelopmentEnvironment|$status|mode=$mode|required=$readyCount/$($requiredStates.Count)")
foreach ($state in $states) {
    $stateStatus = if ($state.Ready) { 'Ready' } else { 'Missing' }
    $required = if ($state.Required) { 'Required' } else { 'Optional' }
    $lines.Add("Utility|name=$($state.Name)|category=$($state.Category)|requirement=$required|status=$stateStatus|version=$($state.Version)|detail=$($state.Detail)")
}
$lines.Add('Boundary|Application operators do not need Git or Python. Use the self-contained Windows package to avoid a separate .NET runtime installation.')
$lines.Add('Safety|InstallMissing uses explicit winget package IDs and never launches the application or changes recipe/project state.')

$reportDirectory = Split-Path -Parent $fullReportPath
New-Item -ItemType Directory -Force -Path $reportDirectory | Out-Null
$lines | Set-Content -LiteralPath $fullReportPath -Encoding utf8
$lines | ForEach-Object { Write-Host $_ }
Write-Host "Report: $fullReportPath"

if ($status -ne 'Ready') {
    exit 1
}
