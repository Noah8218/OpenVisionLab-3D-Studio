param(
    [string]$Configuration = "Release",
    [switch]$SkipBuild,
    [string]$MachineRepo = $env:OPENVISIONLAB_MACHINE_STUDIO_ROOT,
    [string]$TestRoot = $env:OPENVISIONLAB_3D_TEST_ARTIFACT_ROOT
)

$ErrorActionPreference = "Stop"

$ThreeDRepo = Split-Path -Parent $PSScriptRoot
if ([string]::IsNullOrWhiteSpace($MachineRepo)) {
    throw "Set OPENVISIONLAB_MACHINE_STUDIO_ROOT or pass -MachineRepo to run the cross-process smoke."
}
if ([string]::IsNullOrWhiteSpace($TestRoot)) {
    $TestRoot = Join-Path ([IO.Path]::GetTempPath()) "OpenVisionLab-3D-Studio"
}
$MachineProducerProject = Join-Path $MachineRepo "tools\MachineIntegrationProducerSmoke\MachineIntegrationProducerSmoke.csproj"
$ConsumerProject = Join-Path $ThreeDRepo "tools\ThreeDIntegrationConsumerSmoke\ThreeDIntegrationConsumerSmoke.csproj"
$MachineProducerDll = Join-Path $MachineRepo "tools\MachineIntegrationProducerSmoke\bin\$Configuration\net8.0\MachineIntegrationProducerSmoke.dll"
$ConsumerDll = Join-Path $ThreeDRepo "tools\ThreeDIntegrationConsumerSmoke\bin\$Configuration\net10.0\ThreeDIntegrationConsumerSmoke.dll"
$MachineProjectPath = Join-Path $MachineRepo "samples\VisionInspectionCell\VisionInspectionCell.ovmachine"
$SourceC3DPath = Join-Path $ThreeDRepo "3D\Samples\ThicknessCouponV1\thickness-coupon-v1.C3D"
$RecipePath = Join-Path $ThreeDRepo "recipes\c3d-warpage.recipe.json"
$RunRoot = Join-Path (Join-Path $TestRoot "integration-3d-cross-process") ("run-" + (Get-Date -Format "yyyyMMdd-HHmmss") + "-" + [Guid]::NewGuid().ToString("N"))
$ExchangeRoot = Join-Path $RunRoot "exchange"
$ManifestPath = Join-Path $RunRoot "machine-producer-manifest.json"
$ConsumerEvidencePath = Join-Path $RunRoot "consumer-evidence.txt"
$SummaryPath = Join-Path $RunRoot "cross-process-summary.txt"
$ProducerOutputPath = Join-Path $RunRoot "machine-producer.stdout.txt"
$ProducerErrorPath = Join-Path $RunRoot "machine-producer.stderr.txt"
$ConsumerOutputPath = Join-Path $RunRoot "3d-consumer.stdout.txt"
$ConsumerErrorPath = Join-Path $RunRoot "3d-consumer.stderr.txt"

New-Item -ItemType Directory -Force -Path $RunRoot | Out-Null
New-Item -ItemType Directory -Force -Path $ExchangeRoot | Out-Null

function Invoke-DotnetStep {
    param(
        [string]$Name,
        [string]$WorkingDirectory,
        [string[]]$Arguments,
        [string]$StandardOutputPath,
        [string]$StandardErrorPath
    )

    Write-Host "==> $Name"
    $process = Start-Process `
        -FilePath "dotnet" `
        -ArgumentList $Arguments `
        -WorkingDirectory $WorkingDirectory `
        -RedirectStandardOutput $StandardOutputPath `
        -RedirectStandardError $StandardErrorPath `
        -PassThru `
        -Wait `
        -NoNewWindow
    if ($process.ExitCode -ne 0) {
        Write-Host "FAIL $Name exit=$($process.ExitCode) pid=$($process.Id)"
        Get-Content -LiteralPath $StandardOutputPath -ErrorAction SilentlyContinue
        Get-Content -LiteralPath $StandardErrorPath -ErrorAction SilentlyContinue
        throw "$Name failed with exit code $($process.ExitCode)."
    }

    Write-Host "PASS $Name exit=0 pid=$($process.Id)"
    return $process
}

function Get-GitText {
    param(
        [string]$Repository,
        [string[]]$Arguments
    )

    ((& git -C $Repository @Arguments) | Out-String).Trim()
}

$consumerCommit = Get-GitText $ThreeDRepo @("rev-parse", "HEAD")
if ($consumerCommit.Length -ne 40) {
    throw "Unable to resolve the 3D Studio consumer commit."
}

$consumerStatus = Get-GitText $ThreeDRepo @("status", "--porcelain=v1", "--untracked-files=normal")
$consumerActualSourceState = if ([string]::IsNullOrWhiteSpace($consumerStatus)) { "Clean" } else { "Dirty" }

if (-not (Test-Path -LiteralPath $MachineProducerProject)) {
    throw "Machine producer project was not found: $MachineProducerProject"
}
if (-not (Test-Path -LiteralPath $ConsumerProject)) {
    throw "3D consumer project was not found: $ConsumerProject"
}
if (-not (Test-Path -LiteralPath $MachineProjectPath)) {
    throw "Machine project fixture was not found: $MachineProjectPath"
}
if (-not (Test-Path -LiteralPath $SourceC3DPath)) {
    throw "C3D source fixture was not found: $SourceC3DPath"
}
if (-not (Test-Path -LiteralPath $RecipePath)) {
    throw "3D recipe fixture was not found: $RecipePath"
}

if (-not $SkipBuild) {
    Invoke-DotnetStep `
        -Name "build Machine producer" `
        -WorkingDirectory $MachineRepo `
        -Arguments @("build", $MachineProducerProject, "-c", $Configuration) `
        -StandardOutputPath (Join-Path $RunRoot "machine-producer-build.stdout.txt") `
        -StandardErrorPath (Join-Path $RunRoot "machine-producer-build.stderr.txt") | Out-Null

    Invoke-DotnetStep `
        -Name "build 3D consumer" `
        -WorkingDirectory $ThreeDRepo `
        -Arguments @("build", $ConsumerProject, "-c", $Configuration) `
        -StandardOutputPath (Join-Path $RunRoot "3d-consumer-build.stdout.txt") `
        -StandardErrorPath (Join-Path $RunRoot "3d-consumer-build.stderr.txt") | Out-Null
}

if (-not (Test-Path -LiteralPath $MachineProducerDll)) {
    throw "Machine producer assembly was not found: $MachineProducerDll"
}
if (-not (Test-Path -LiteralPath $ConsumerDll)) {
    throw "3D consumer assembly was not found: $ConsumerDll"
}

$producer = Invoke-DotnetStep `
    -Name "Machine producer process" `
    -WorkingDirectory $MachineRepo `
    -Arguments @(
        $MachineProducerDll,
        "--publish-3d", $ExchangeRoot, $ManifestPath, $MachineProjectPath, $SourceC3DPath,
        $RecipePath, "0.2.0-alpha.1", $consumerCommit, "Clean") `
    -StandardOutputPath $ProducerOutputPath `
    -StandardErrorPath $ProducerErrorPath

$consumer = Invoke-DotnetStep `
    -Name "3D consumer process" `
    -WorkingDirectory $ThreeDRepo `
    -Arguments @(
        $ConsumerDll,
        "--consume-3d", $ExchangeRoot, $ManifestPath, $ConsumerEvidencePath) `
    -StandardOutputPath $ConsumerOutputPath `
    -StandardErrorPath $ConsumerErrorPath

if ($producer.Id -eq $consumer.Id) {
    throw "Producer and consumer did not execute as separate processes."
}
if (-not (Test-Path -LiteralPath $ManifestPath)) {
    throw "Machine producer manifest was not created."
}
if (-not (Test-Path -LiteralPath $ConsumerEvidencePath)) {
    throw "3D consumer evidence was not created."
}

$producerOutput = Get-Content -LiteralPath $ProducerOutputPath -Raw
$producerActualSourceState = if ($producerOutput.Contains("Worktree=Dirty")) { "Dirty" } else { "Clean" }
$evidence = Get-Content -LiteralPath $ConsumerEvidencePath -Raw
$requiredEvidence = @(
    "acknowledgement=Accepted",
    "resultStatus=Completed",
    "modality=ThreeD",
    "inputKind=HeightMap",
    "rawHeightBufferMaterialized=True",
    "runRecordRelativePath=artifacts/3d-run-record.json"
)
foreach ($expected in $requiredEvidence) {
    if (-not $evidence.Contains($expected)) {
        throw "Consumer evidence is missing: $expected"
    }
}

@(
    "OpenVisionLab 3D cross-process HeightMap integration smoke",
    "configuration=$Configuration",
    "runRoot=$RunRoot",
    "exchangeRoot=$ExchangeRoot",
    "producerProcessId=$($producer.Id)",
    "consumerProcessId=$($consumer.Id)",
    "producerRepository=$MachineRepo",
    "consumerRepository=$ThreeDRepo",
    "consumerCommit=$consumerCommit",
    "producerDeclaredSourceState=Clean",
    "producerActualWorktreeState=$producerActualSourceState",
    "consumerDeclaredSourceState=Clean",
    "consumerActualWorktreeState=$consumerActualSourceState",
    "sourceC3D=$SourceC3DPath",
    "recipe=$RecipePath",
    "manifest=$ManifestPath",
    "consumerEvidence=$ConsumerEvidencePath",
    "producerStdout=$ProducerOutputPath",
    "consumerStdout=$ConsumerOutputPath",
    "status=PASS"
) | Set-Content -LiteralPath $SummaryPath -Encoding UTF8

Write-Host "Summary: $SummaryPath"
Write-Host "Consumer evidence: $ConsumerEvidencePath"
exit 0
