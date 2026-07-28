[CmdletBinding()]
param(
    [string]$PayloadDirectory = 'artifacts/open3d-clean-host-evidence-bundle-20260716-sandbox',
    [string]$InstallerPath = 'artifacts/dependency-candidates/microsoft-vc-redist-20260716/vc_redist.x64.exe',
    [string]$RunDirectory = 'artifacts/windows-sandbox-clean-host-20260716',
    [switch]$Launch,
    [ValidateRange(30, 1200)]
    [int]$TimeoutSeconds = 600
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot

function Resolve-WorkspacePath([string]$Path) {
    if ([System.IO.Path]::IsPathRooted($Path)) {
        return [System.IO.Path]::GetFullPath($Path)
    }

    return [System.IO.Path]::GetFullPath((Join-Path $repoRoot $Path))
}

function Assert-WorkspacePath([string]$Path, [string]$Label) {
    $root = [System.IO.Path]::GetFullPath($repoRoot).TrimEnd([System.IO.Path]::DirectorySeparatorChar, [System.IO.Path]::AltDirectorySeparatorChar)
    $prefix = $root + [System.IO.Path]::DirectorySeparatorChar
    if (-not $Path.StartsWith($prefix, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "$Label must be inside the workspace: $Path"
    }
}

function Get-ValidatedPayloadRecords([string]$Directory) {
    $manifestPath = Join-Path $Directory 'clean-host-evidence-manifest.json'
    if (-not (Test-Path -LiteralPath $manifestPath -PathType Leaf)) {
        throw "Clean-host payload manifest is missing: $manifestPath"
    }

    $manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
    if ([string]$manifest.SchemaVersion -ne '1.0') {
        throw "Unsupported clean-host payload manifest schema: $($manifest.SchemaVersion)"
    }

    $expectedPaths = @(
        'baseline/candidate_repeat_1.json',
        'bundle/Open3D.dll',
        'bundle/open3d-registration-probe.exe',
        'bundle/tbb12.dll',
        'input/cloud_bin_0.pcd',
        'input/cloud_bin_1.pcd',
        'input/init.log',
        'instructions/OPENVISIONLAB_3D_OPEN3D_CLEAN_HOST_EXECUTION_PROTOCOL_20260716.md',
        'tools/verify-open3d-runtime-prerequisites.ps1'
    )
    $actualPaths = @($manifest.Files | ForEach-Object { ([string]$_.RelativePath).Replace('\\', '/') } | Sort-Object)
    if ($actualPaths.Count -ne $expectedPaths.Count -or (@($actualPaths | Where-Object { $_ -notin $expectedPaths }).Count -ne 0)) {
        throw 'Clean-host payload manifest does not contain the fixed nine-file input set.'
    }

    $directoryRoot = [System.IO.Path]::GetFullPath($Directory).TrimEnd([System.IO.Path]::DirectorySeparatorChar, [System.IO.Path]::AltDirectorySeparatorChar)
    $directoryPrefix = $directoryRoot + [System.IO.Path]::DirectorySeparatorChar
    $records = [System.Collections.Generic.List[object]]::new()
    foreach ($entry in $manifest.Files) {
        $relativePath = ([string]$entry.RelativePath).Replace('/', [System.IO.Path]::DirectorySeparatorChar)
        if ([System.IO.Path]::IsPathRooted($relativePath) -or $relativePath -match '(^|[\\/])\.\.([\\/]|$)') {
            throw "Payload manifest contains an unsafe relative path: $($entry.RelativePath)"
        }

        $path = [System.IO.Path]::GetFullPath((Join-Path $directoryRoot $relativePath))
        if (-not $path.StartsWith($directoryPrefix, [System.StringComparison]::OrdinalIgnoreCase)) {
            throw "Payload manifest path escapes the staged directory: $($entry.RelativePath)"
        }

        if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
            throw "Payload file is missing: $($entry.RelativePath)"
        }

        $item = Get-Item -LiteralPath $path
        $hash = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant()
        if ($item.Length -ne [int64]$entry.Size -or $hash -cne ([string]$entry.Sha256).ToLowerInvariant()) {
            throw "Payload file hash or size mismatch: $($entry.RelativePath)"
        }

        $records.Add([pscustomobject]@{
            RelativePath = ([string]$entry.RelativePath).Replace('\\', '/')
            Size = $item.Length
            Sha256 = $hash
        })
    }

    foreach ($name in @('MSVCP140.dll', 'VCRUNTIME140.dll', 'VCRUNTIME140_1.dll', 'VCOMP140.dll')) {
        if (Test-Path -LiteralPath (Join-Path $Directory "bundle\\$name") -PathType Leaf) {
            throw "Payload must not contain adjacent Microsoft VC/OpenMP runtime file: $name"
        }
    }

    return $records
}

function Get-ValidatedInstallerRecord([string]$Path) {
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        throw "Reviewed Microsoft installer is missing: $Path"
    }

    $item = Get-Item -LiteralPath $Path
    $hash = (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($item.Length -ne 18731856 -or $hash -cne '843068991daaa1f73ad9f6239bce4d0f6a07a51f18c37ea2a867e9beca71295c') {
        throw "Installer identity differs from the reviewed 2026-07-16 Microsoft VC++ x64 prerequisite: $Path"
    }

    $signature = Get-AuthenticodeSignature -LiteralPath $Path
    if ($signature.Status -ne 'Valid') {
        throw "Installer Authenticode signature is not valid: $($signature.Status)"
    }

    return [pscustomobject]@{
        Name = $item.Name
        Size = $item.Length
        Sha256 = $hash
        FileVersion = [string]$item.VersionInfo.FileVersion
        SignatureStatus = [string]$signature.Status
        SignerSubject = [string]$signature.SignerCertificate.Subject
        SignerThumbprint = [string]$signature.SignerCertificate.Thumbprint
    }
}

$payloadDirectory = Resolve-WorkspacePath $PayloadDirectory
$installerPath = Resolve-WorkspacePath $InstallerPath
$runDirectory = Resolve-WorkspacePath $RunDirectory
Assert-WorkspacePath $payloadDirectory 'PayloadDirectory'
Assert-WorkspacePath $installerPath 'InstallerPath'
Assert-WorkspacePath $runDirectory 'RunDirectory'

if (-not (Test-Path -LiteralPath $payloadDirectory -PathType Container)) {
    throw "Clean-host payload directory is missing: $payloadDirectory"
}

if (Test-Path -LiteralPath $runDirectory) {
    throw "RunDirectory already exists and will not be overwritten: $runDirectory"
}

$payloadRecords = Get-ValidatedPayloadRecords $payloadDirectory
$installerRecord = Get-ValidatedInstallerRecord $installerPath

$installerDirectory = Join-Path $runDirectory 'installer'
$scriptDirectory = Join-Path $runDirectory 'sandbox-scripts'
$evidenceDirectory = Join-Path $runDirectory 'evidence'
New-Item -ItemType Directory -Path $installerDirectory, $scriptDirectory, $evidenceDirectory | Out-Null

$copiedInstallerPath = Join-Path $installerDirectory 'vc_redist.x64.exe'
Copy-Item -LiteralPath $installerPath -Destination $copiedInstallerPath
$copiedInstallerRecord = Get-ValidatedInstallerRecord $copiedInstallerPath

$sandboxRunner = @'
$ErrorActionPreference = 'Stop'
$payloadRoot = 'C:\OVL3D-Payload'
$installerRoot = 'C:\OVL3D-Installer'
$evidenceRoot = 'C:\OVL3D-Evidence'
$minimumRuntimeVersion = '14.44.35211.0'
$expectedInstallerSize = [int64]18731856
$expectedInstallerHash = '843068991daaa1f73ad9f6239bce4d0f6a07a51f18c37ea2a867e9beca71295c'

function Write-Json([string]$Path, $Value) {
    $Value | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $Path -Encoding utf8
}

function Get-FileRecord([string]$Path) {
    $item = Get-Item -LiteralPath $Path
    return [ordered]@{
        Path = $Path
        Size = $item.Length
        Sha256 = (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant()
    }
}

function Get-NormalizedProbeJson([string]$Path) {
    $value = Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json
    [void]$value.PSObject.Properties.Remove('elapsedMilliseconds')
    return ($value | ConvertTo-Json -Depth 8 -Compress)
}

function Invoke-CapturedProgram([string]$FilePath, [string[]]$Arguments, [string]$ConsolePath) {
    $result = [ordered]@{
        FilePath = $FilePath
        Arguments = $Arguments
        ExitCode = $null
        LaunchException = $null
    }

    try {
        $global:LASTEXITCODE = $null
        & $FilePath @Arguments *> $ConsolePath
        $result.ExitCode = $LASTEXITCODE
    }
    catch {
        $_ | Out-String | Set-Content -LiteralPath $ConsolePath -Encoding utf8
        $result.LaunchException = $_.Exception.Message
    }

    return [pscustomobject]$result
}

New-Item -ItemType Directory -Path $evidenceRoot -Force | Out-Null
$summaryPath = Join-Path $evidenceRoot 'clean-host-summary.json'
$summary = [ordered]@{
    SchemaVersion = '1.0'
    ClaimLimit = 'clean-host prerequisite evidence only; redistribution, product integration, result mapping, and metrology remain unverified'
    StartedUtc = [DateTime]::UtcNow.ToString('o')
    Sandbox = $null
    PayloadIntegrity = $null
    InstallerIntegrity = $null
    PreInstall = $null
    Installer = $null
    PostInstall = $null
    Probe = $null
    Outcome = 'Started'
    Error = $null
}
$exitCode = 1

try {
    $os = Get-CimInstance -ClassName Win32_OperatingSystem
    $summary.Sandbox = [ordered]@{
        Caption = [string]$os.Caption
        Version = [string]$os.Version
        BuildNumber = [string]$os.BuildNumber
        OsArchitecture = [string]$os.OSArchitecture
        LastBootUpTime = ([DateTime]$os.LastBootUpTime).ToUniversalTime().ToString('o')
        UserName = [Environment]::UserName
    }

    $manifestPath = Join-Path $payloadRoot 'clean-host-evidence-manifest.json'
    $manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
    if ([string]$manifest.SchemaVersion -ne '1.0') {
        throw "Unsupported payload manifest schema: $($manifest.SchemaVersion)"
    }

    $payloadRootFull = [System.IO.Path]::GetFullPath($payloadRoot).TrimEnd([System.IO.Path]::DirectorySeparatorChar, [System.IO.Path]::AltDirectorySeparatorChar)
    $payloadPrefix = $payloadRootFull + [System.IO.Path]::DirectorySeparatorChar
    $payloadFiles = [System.Collections.Generic.List[object]]::new()
    foreach ($entry in $manifest.Files) {
        $relativePath = ([string]$entry.RelativePath).Replace('/', [System.IO.Path]::DirectorySeparatorChar)
        if ([System.IO.Path]::IsPathRooted($relativePath) -or $relativePath -match '(^|[\\/])\.\.([\\/]|$)') {
            throw "Payload manifest contains an unsafe path: $($entry.RelativePath)"
        }

        $path = [System.IO.Path]::GetFullPath((Join-Path $payloadRootFull $relativePath))
        if (-not $path.StartsWith($payloadPrefix, [System.StringComparison]::OrdinalIgnoreCase) -or -not (Test-Path -LiteralPath $path -PathType Leaf)) {
            throw "Payload file is absent or escapes the mapped directory: $($entry.RelativePath)"
        }

        $record = Get-FileRecord $path
        if ($record.Size -ne [int64]$entry.Size -or $record.Sha256 -cne ([string]$entry.Sha256).ToLowerInvariant()) {
            throw "Payload hash or size mismatch: $($entry.RelativePath)"
        }

        $payloadFiles.Add([pscustomobject]@{
            RelativePath = ([string]$entry.RelativePath).Replace('\\', '/')
            Size = $record.Size
            Sha256 = $record.Sha256
        })
    }

    $forbiddenRuntime = @('MSVCP140.dll', 'VCRUNTIME140.dll', 'VCRUNTIME140_1.dll', 'VCOMP140.dll')
    $presentSidecars = @($forbiddenRuntime | Where-Object { Test-Path -LiteralPath (Join-Path $payloadRoot "bundle\\$_") -PathType Leaf })
    if ($presentSidecars.Count -ne 0) {
        throw "Payload contains prohibited adjacent VC/OpenMP runtime files: $($presentSidecars -join ', ')"
    }

    $summary.PayloadIntegrity = [ordered]@{
        ManifestPath = $manifestPath
        FileCount = $payloadFiles.Count
        Files = $payloadFiles
        AdjacentRuntimeFiles = $presentSidecars
        Pass = $true
    }
    Write-Json (Join-Path $evidenceRoot 'payload-integrity.json') $summary.PayloadIntegrity

    $installerPath = Join-Path $installerRoot 'vc_redist.x64.exe'
    $installerRecord = Get-FileRecord $installerPath
    $signature = Get-AuthenticodeSignature -LiteralPath $installerPath
    $summary.InstallerIntegrity = [ordered]@{
        Path = $installerPath
        Size = $installerRecord.Size
        Sha256 = $installerRecord.Sha256
        FileVersion = [string](Get-Item -LiteralPath $installerPath).VersionInfo.FileVersion
        SignatureStatus = [string]$signature.Status
        SignerSubject = [string]$signature.SignerCertificate.Subject
        SignerThumbprint = [string]$signature.SignerCertificate.Thumbprint
        Pass = ($installerRecord.Size -eq $expectedInstallerSize -and $installerRecord.Sha256 -ceq $expectedInstallerHash -and $signature.Status -eq 'Valid')
    }
    Write-Json (Join-Path $evidenceRoot 'installer-integrity.json') $summary.InstallerIntegrity
    if (-not $summary.InstallerIntegrity.Pass) {
        throw 'Mapped installer identity or signature does not match the reviewed Microsoft prerequisite.'
    }

    $verifier = Join-Path $payloadRoot 'tools\verify-open3d-runtime-prerequisites.ps1'
    $runtimeDirectory = Join-Path $payloadRoot 'bundle'
    $preInstallReport = Join-Path $evidenceRoot 'pre-install-prerequisites.txt'
    $preInstallConsole = Join-Path $evidenceRoot 'pre-install-prerequisites-console.txt'
    $preInstall = Invoke-CapturedProgram -FilePath 'powershell.exe' -Arguments @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', $verifier, '-RuntimeDirectory', $runtimeDirectory, '-MinimumRuntimeVersion', $minimumRuntimeVersion, '-ReportPath', $preInstallReport) -ConsolePath $preInstallConsole

    $preInstallProbe = Join-Path $evidenceRoot 'pre-install-probe.json'
    $preInstallProbeConsole = Join-Path $evidenceRoot 'pre-install-probe-console.txt'
    $preInstallProbeResult = Invoke-CapturedProgram -FilePath (Join-Path $runtimeDirectory 'open3d-registration-probe.exe') -Arguments @((Join-Path $payloadRoot 'input\cloud_bin_0.pcd'), (Join-Path $payloadRoot 'input\cloud_bin_1.pcd'), (Join-Path $payloadRoot 'input\init.log'), $preInstallProbe, '0') -ConsolePath $preInstallProbeConsole

    $summary.PreInstall = [ordered]@{
        Prerequisite = $preInstall
        PrerequisiteReportExists = Test-Path -LiteralPath $preInstallReport -PathType Leaf
        Probe = $preInstallProbeResult
        ProbeReportExists = Test-Path -LiteralPath $preInstallProbe -PathType Leaf
        ExpectedPrerequisiteExitCode = 1
        ExpectedProbeReportAbsent = $true
        Pass = ($preInstall.ExitCode -eq 1 -and -not (Test-Path -LiteralPath $preInstallProbe -PathType Leaf))
    }

    if (-not $summary.PreInstall.Pass) {
        $summary.Outcome = 'RejectedPreInstallNotClean'
        $exitCode = 2
    }
    else {
        $installerLog = Join-Path $evidenceRoot 'vc-redist-install.log'
        $installerCommand = @('/install', '/quiet', '/norestart', '/log', $installerLog)
        $installerProcess = Start-Process -FilePath $installerPath -ArgumentList $installerCommand -Wait -PassThru
        $summary.Installer = [ordered]@{
            Command = "`"$installerPath`" $($installerCommand -join ' ')"
            ExitCode = $installerProcess.ExitCode
            LogExists = Test-Path -LiteralPath $installerLog -PathType Leaf
            RestartRequired = ($installerProcess.ExitCode -eq 3010)
        }

        if ($installerProcess.ExitCode -eq 3010) {
            $summary.Outcome = 'BlockedRestartRequired'
            $exitCode = 3
        }
        elseif ($installerProcess.ExitCode -ne 0) {
            $summary.Outcome = 'InstallerFailed'
            $exitCode = 4
        }
        else {
            $postInstallReport = Join-Path $evidenceRoot 'post-install-prerequisites.txt'
            $postInstallConsole = Join-Path $evidenceRoot 'post-install-prerequisites-console.txt'
            $postInstall = Invoke-CapturedProgram -FilePath 'powershell.exe' -Arguments @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', $verifier, '-RuntimeDirectory', $runtimeDirectory, '-MinimumRuntimeVersion', $minimumRuntimeVersion, '-ReportPath', $postInstallReport) -ConsolePath $postInstallConsole
            $summary.PostInstall = [ordered]@{
                Prerequisite = $postInstall
                PrerequisiteReportExists = Test-Path -LiteralPath $postInstallReport -PathType Leaf
                ExpectedPrerequisiteExitCode = 0
                Pass = ($postInstall.ExitCode -eq 0)
            }

            if (-not $summary.PostInstall.Pass) {
                $summary.Outcome = 'PostInstallPrerequisiteFailed'
                $exitCode = 5
            }
            else {
                $postInstallProbe = Join-Path $evidenceRoot 'post-install-probe.json'
                $postInstallProbeConsole = Join-Path $evidenceRoot 'post-install-probe-console.txt'
                $postInstallProbeResult = Invoke-CapturedProgram -FilePath (Join-Path $runtimeDirectory 'open3d-registration-probe.exe') -Arguments @((Join-Path $payloadRoot 'input\cloud_bin_0.pcd'), (Join-Path $payloadRoot 'input\cloud_bin_1.pcd'), (Join-Path $payloadRoot 'input\init.log'), $postInstallProbe, '0') -ConsolePath $postInstallProbeConsole
                $normalizedMatch = $false
                $comparisonError = $null
                if ($postInstallProbeResult.ExitCode -eq 0 -and (Test-Path -LiteralPath $postInstallProbe -PathType Leaf)) {
                    try {
                        $actual = Get-NormalizedProbeJson $postInstallProbe
                        $baseline = Get-NormalizedProbeJson (Join-Path $payloadRoot 'baseline\candidate_repeat_1.json')
                        $normalizedMatch = $actual -ceq $baseline
                        [pscustomobject]@{
                            NormalizedBaselineMatch = $normalizedMatch
                            RemovedField = 'elapsedMilliseconds'
                        } | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $evidenceRoot 'post-install-probe-parity.json') -Encoding utf8
                    }
                    catch {
                        $comparisonError = $_.Exception.Message
                    }
                }

                $summary.Probe = [ordered]@{
                    Result = $postInstallProbeResult
                    ReportExists = Test-Path -LiteralPath $postInstallProbe -PathType Leaf
                    NormalizedBaselineMatch = $normalizedMatch
                    ComparisonError = $comparisonError
                    RemovedField = 'elapsedMilliseconds'
                    Pass = ($postInstallProbeResult.ExitCode -eq 0 -and (Test-Path -LiteralPath $postInstallProbe -PathType Leaf) -and $normalizedMatch)
                }

                if ($summary.Probe.Pass) {
                    $summary.Outcome = 'Passed'
                    $exitCode = 0
                }
                else {
                    $summary.Outcome = 'PostInstallProbeFailed'
                    $exitCode = 6
                }
            }
        }
    }
}
catch {
    $summary.Outcome = 'RunnerError'
    $summary.Error = $_.Exception.Message
    $_ | Out-String | Set-Content -LiteralPath (Join-Path $evidenceRoot 'runner-error.txt') -Encoding utf8
    $exitCode = 1
}

$summary.CompletedUtc = [DateTime]::UtcNow.ToString('o')
$summary.ExitCode = $exitCode
Write-Json $summaryPath $summary
$summary.Outcome | Set-Content -LiteralPath (Join-Path $evidenceRoot 'clean-host-outcome.txt') -Encoding utf8
Start-Sleep -Seconds 3
& shutdown.exe /s /t 5 /f *> (Join-Path $evidenceRoot 'sandbox-shutdown-console.txt')
exit $exitCode
'@

$runnerPath = Join-Path $scriptDirectory 'run-open3d-clean-host.ps1'
Set-Content -LiteralPath $runnerPath -Value $sandboxRunner -Encoding utf8

$escapedPayload = [System.Security.SecurityElement]::Escape($payloadDirectory)
$escapedInstaller = [System.Security.SecurityElement]::Escape($installerDirectory)
$escapedScripts = [System.Security.SecurityElement]::Escape($scriptDirectory)
$escapedEvidence = [System.Security.SecurityElement]::Escape($evidenceDirectory)
$wsbContent = @"
<Configuration>
  <VGpu>Disable</VGpu>
  <Networking>Disable</Networking>
  <ClipboardRedirection>Disable</ClipboardRedirection>
  <MappedFolders>
    <MappedFolder>
      <HostFolder>$escapedPayload</HostFolder>
      <SandboxFolder>C:\OVL3D-Payload</SandboxFolder>
      <ReadOnly>true</ReadOnly>
    </MappedFolder>
    <MappedFolder>
      <HostFolder>$escapedInstaller</HostFolder>
      <SandboxFolder>C:\OVL3D-Installer</SandboxFolder>
      <ReadOnly>true</ReadOnly>
    </MappedFolder>
    <MappedFolder>
      <HostFolder>$escapedScripts</HostFolder>
      <SandboxFolder>C:\OVL3D-Scripts</SandboxFolder>
      <ReadOnly>true</ReadOnly>
    </MappedFolder>
    <MappedFolder>
      <HostFolder>$escapedEvidence</HostFolder>
      <SandboxFolder>C:\OVL3D-Evidence</SandboxFolder>
      <ReadOnly>false</ReadOnly>
    </MappedFolder>
  </MappedFolders>
  <LogonCommand>
    <Command>powershell.exe -NoProfile -ExecutionPolicy Bypass -File C:\OVL3D-Scripts\run-open3d-clean-host.ps1</Command>
  </LogonCommand>
</Configuration>
"@

$wsbPath = Join-Path $runDirectory 'open3d-clean-host.wsb'
Set-Content -LiteralPath $wsbPath -Value $wsbContent -Encoding utf8
$hostPreparation = [pscustomobject]@{
    SchemaVersion = '1.0'
    PreparedUtc = [DateTime]::UtcNow.ToString('o')
    HostUser = "$env:USERDOMAIN\\$env:USERNAME"
    PayloadDirectory = $payloadDirectory
    PayloadRecords = $payloadRecords
    Installer = $copiedInstallerRecord
    EvidenceDirectory = $evidenceDirectory
    WsbPath = $wsbPath
    Networking = 'Disabled'
    VGpu = 'Disabled'
    ClaimLimit = 'Sandbox configuration prepared; clean-host evidence is pending the guest summary.'
}
$hostPreparation | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $runDirectory 'host-preparation.json') -Encoding utf8

if (-not $Launch) {
    "SandboxPreparation|Pass|payloadFiles=$($payloadRecords.Count)|run=$runDirectory|wsb=$wsbPath"
    return
}

$sandboxExe = Join-Path $env:WINDIR 'System32\WindowsSandbox.exe'
if (-not (Test-Path -LiteralPath $sandboxExe -PathType Leaf)) {
    throw "Windows Sandbox executable is unavailable: $sandboxExe"
}

$sandboxProcess = Start-Process -FilePath $sandboxExe -ArgumentList ('"{0}"' -f $wsbPath) -PassThru
$summaryPath = Join-Path $evidenceDirectory 'clean-host-summary.json'
$cleanupPath = Join-Path $evidenceDirectory 'host-sandbox-cleanup.json'
$deadline = [DateTime]::UtcNow.AddSeconds($TimeoutSeconds)
while ([DateTime]::UtcNow -lt $deadline) {
    if (Test-Path -LiteralPath $summaryPath -PathType Leaf) {
        try {
            $summary = Get-Content -LiteralPath $summaryPath -Raw | ConvertFrom-Json
            if (-not [string]::IsNullOrWhiteSpace([string]$summary.Outcome) -and [string]$summary.Outcome -ne 'Started') {
                $sandboxClientIds = @(
                    Get-CimInstance Win32_Process -Filter "ParentProcessId = $($sandboxProcess.Id)" |
                        Where-Object { $_.Name -eq 'WindowsSandboxClient.exe' } |
                        ForEach-Object { [int]$_.ProcessId }
                )
                $cleanup = [ordered]@{
                    RequestedUtc = [DateTime]::UtcNow.ToString('o')
                    SandboxProcessId = $sandboxProcess.Id
                    SandboxClientProcessIds = $sandboxClientIds
                    WasSandboxRunningBeforeCleanup = -not $sandboxProcess.HasExited
                    ForcedSandboxTermination = $false
                    ForcedSandboxClientTermination = $false
                    RemainingProcessIds = @()
                    Exited = $false
                    Error = $null
                }
                try {
                    if (-not $sandboxProcess.HasExited) {
                        Start-Sleep -Seconds 5
                        $sandboxProcess.Refresh()
                        if (-not $sandboxProcess.HasExited) {
                            Stop-Process -Id $sandboxProcess.Id -Force
                            $cleanup.ForcedTermination = $true
                            [void]$sandboxProcess.WaitForExit(30000)
                        }
                    }

                    foreach ($sandboxClientId in $sandboxClientIds) {
                        if ($null -ne (Get-Process -Id $sandboxClientId -ErrorAction SilentlyContinue)) {
                            Stop-Process -Id $sandboxClientId -Force
                            $cleanup.ForcedSandboxClientTermination = $true
                        }
                    }

                    $remainingProcessIds = [System.Collections.Generic.List[int]]::new()
                    if (-not $sandboxProcess.HasExited) {
                        $remainingProcessIds.Add($sandboxProcess.Id)
                    }

                    foreach ($sandboxClientId in $sandboxClientIds) {
                        if ($null -ne (Get-Process -Id $sandboxClientId -ErrorAction SilentlyContinue)) {
                            $remainingProcessIds.Add($sandboxClientId)
                        }
                    }

                    $cleanup.RemainingProcessIds = @($remainingProcessIds)
                    $cleanup.Exited = ($remainingProcessIds.Count -eq 0)
                }
                catch {
                    $cleanup.Error = $_.Exception.Message
                    $cleanup.Exited = $false
                }

                $cleanup | ConvertTo-Json | Set-Content -LiteralPath $cleanupPath -Encoding utf8
                if (-not $cleanup.Exited) {
                    throw "Windows Sandbox process remained active after guest summary: pid=$($sandboxProcess.Id)"
                }

                "SandboxCleanHost|outcome=$($summary.Outcome)|exitCode=$($summary.ExitCode)|summary=$summaryPath|pid=$($sandboxProcess.Id)"
                if ($summary.Outcome -ne 'Passed') {
                    exit 1
                }

                exit 0
            }
        }
        catch {
            # The guest can be writing the JSON while the host polls it.
        }
    }

    Start-Sleep -Seconds 2
}

"SandboxCleanHost|outcome=TimedOut|summary=$summaryPath|pid=$($sandboxProcess.Id)" | Set-Content -LiteralPath (Join-Path $evidenceDirectory 'host-timeout.txt') -Encoding utf8
throw "Windows Sandbox did not produce a completed clean-host summary within $TimeoutSeconds seconds."
