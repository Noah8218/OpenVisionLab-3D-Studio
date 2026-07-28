param(
    [ValidateSet("Release", "Debug")]
    [string]$Configuration = "Release",
    [int]$Repetitions = 3,
    [string]$ArtifactDirectory = "artifacts/current/20260724-release-multi-c3d-performance",
    [switch]$SkipBuild
)

$ErrorActionPreference = "Stop"
$culture = [System.Globalization.CultureInfo]::InvariantCulture
$repoRoot = Split-Path -Parent $PSScriptRoot
$artifactRoot = [System.IO.Path]::GetFullPath((Join-Path $repoRoot $ArtifactDirectory))
$fixtureRoot = Join-Path $artifactRoot "fixtures"
$runRoot = Join-Path $artifactRoot "matrix-runs"
New-Item -ItemType Directory -Force -Path $fixtureRoot, $runRoot | Out-Null

if ($Repetitions -lt 1) {
    throw "Repetitions must be at least 1."
}

if (-not $SkipBuild) {
    & dotnet build (Join-Path $repoRoot "OpenVisionLab.ThreeDStudio.sln") `
        -c $Configuration -p:Platform="Any CPU"
    if ($LASTEXITCODE -ne 0) {
        throw "Solution build failed with exit code $LASTEXITCODE."
    }
}

$shellExe = Join-Path $repoRoot (
    "src/OpenVisionLab.ThreeD.Shell/bin/{0}/net10.0-windows10.0.19041/OpenVisionLab.ThreeD.Shell.exe" -f
    $Configuration)
if (-not (Test-Path -LiteralPath $shellExe -PathType Leaf)) {
    throw "Shell EXE was not found: $shellExe"
}

$sources = @(
    [pscustomobject]@{
        Id = "synthetic-affine"
        Path = Join-Path $repoRoot "3D/SyntheticValidation/AffineInspectionPlateV1/source-affine-inspection-plate-v1.C3D"
        OriginalPath = $null
    },
    [pscustomobject]@{
        Id = "synthetic-thickness"
        Path = Join-Path $repoRoot "3D/Samples/ThicknessCouponV1/thickness-coupon-v1.C3D"
        OriginalPath = $null
    }
)

function Read-C3DHeader([string]$path) {
    $stream = [System.IO.File]::OpenRead($path)
    try {
        $reader = [System.IO.BinaryReader]::new($stream)
        $width = $reader.ReadInt32()
        $height = $reader.ReadInt32()
        $expectedLength = 8L + ([long]$width * [long]$height * 4L)
        if ($width -le 0 -or $height -le 0 -or $stream.Length -ne $expectedLength) {
            throw "Unsupported C3D layout: $path"
        }
        return [pscustomobject]@{
            Width = $width
            Height = $height
            ByteLength = $stream.Length
        }
    }
    finally {
        $stream.Dispose()
    }
}

function Read-Report([string]$path) {
    $values = @{}
    foreach ($line in Get-Content -LiteralPath $path) {
        if ($line -match "^([^:]+):\s*(.*)$") {
            $values[$matches[1]] = $matches[2]
        }
    }
    return $values
}

function Read-Double($values, [string]$key) {
    $number = 0.0
    if (-not $values.ContainsKey($key) -or
        -not [double]::TryParse(
            $values[$key],
            [System.Globalization.NumberStyles]::Float,
            $culture,
            [ref]$number)) {
        throw "Report field '$key' is missing or non-numeric."
    }
    return $number
}

function Read-Int($values, [string]$key) {
    $number = 0
    if (-not $values.ContainsKey($key) -or
        -not [int]::TryParse($values[$key], [ref]$number)) {
        throw "Report field '$key' is missing or non-numeric."
    }
    return $number
}

function Get-Median([double[]]$values) {
    $ordered = @($values | Sort-Object)
    if (($ordered.Count % 2) -eq 1) {
        return $ordered[[int][Math]::Floor($ordered.Count / 2)]
    }
    $upper = $ordered.Count / 2
    return ($ordered[$upper - 1] + $ordered[$upper]) / 2.0
}

$runRows = [System.Collections.Generic.List[object]]::new()
foreach ($source in $sources) {
    if (-not (Test-Path -LiteralPath $source.Path -PathType Leaf)) {
        throw "C3D source was not found: $($source.Path)"
    }
    $header = Read-C3DHeader $source.Path
    $sha256 = (Get-FileHash -LiteralPath $source.Path -Algorithm SHA256).Hash

    foreach ($run in 1..$Repetitions) {
        $reportPath = Join-Path $runRoot ("{0}-run{1}-load.txt" -f $source.Id, $run)
        $contractPath = Join-Path $runRoot ("{0}-run{1}-contract.txt" -f $source.Id, $run)
        $arguments = @(
            "--smoke-input-first-start",
            "--smoke-async-c3d-load", $source.Path,
            "--smoke-async-c3d-load-report", $reportPath,
            "--smoke-contracts", $contractPath)
        $process = Start-Process -FilePath $shellExe -ArgumentList $arguments -Wait -PassThru
        if ($process.ExitCode -ne 0) {
            throw "$($source.Id) run $run failed with exit code $($process.ExitCode)."
        }

        $values = Read-Report $reportPath
        $targetPath = [System.IO.Path]::GetFullPath($source.Path)
        $resultPass = $values["Result"] -eq "Pass"
        $pathPass = $values["TargetPath"] -eq $targetPath -and $values["CurrentPath"] -eq $targetPath
        $bindingMilliseconds = Read-Double $values "WorkbenchSourceBindingMilliseconds"
        $applyMilliseconds = Read-Double $values "UiApplyAndFirstRenderMilliseconds"
        $renderExecutions = Read-Int $values "UiApplyRenderExecutions"
        $suppressedRenders = Read-Int $values "UiApplySuppressedRenderRequests"
        $contract = Get-Content -LiteralPath $contractPath -Raw
        $vboPass = $contract.Contains("c3dPath=VBO+IBO+DrawElements") `
            -and $contract.Contains("fallbacks=0") `
            -and $contract.Contains("gpuBufferReady=True")
        $acceptancePass = $resultPass `
            -and $pathPass `
            -and $bindingMilliseconds -le 200.0 `
            -and $applyMilliseconds -le 300.0 `
            -and $renderExecutions -eq 1 `
            -and $suppressedRenders -ge 1 `
            -and $vboPass `
            -and $values["LoadStateCleared"] -eq "True" `
            -and $values["FinalProgressPercent"] -eq "100.0"

        $runRows.Add([pscustomobject]@{
            Source = $source.Id
            Run = $run
            Grid = "$($header.Width)x$($header.Height)"
            Bytes = $header.ByteLength
            Sha256 = $sha256
            ElapsedMilliseconds = Read-Double $values "ElapsedMilliseconds"
            WorkerMilliseconds = Read-Double $values "WorkerTotalMilliseconds"
            ApplyMilliseconds = $applyMilliseconds
            WorkbenchBindingMilliseconds = $bindingMilliseconds
            WorkbenchClearPreviewMilliseconds = Read-Double $values "WorkbenchClearPreviewMilliseconds"
            DispatcherTicks = Read-Int $values "DispatcherTicksDuringLoad"
            RenderExecutions = $renderExecutions
            SuppressedRenders = $suppressedRenders
            VboIbo = $vboPass
            Pass = $acceptancePass
        })
    }
}

$summaryRows = foreach ($group in $runRows | Group-Object Source) {
    $first = $group.Group[0]
    [pscustomobject]@{
        Source = $group.Name
        Grid = $first.Grid
        MiB = [Math]::Round($first.Bytes / 1MB, 3)
        Runs = $group.Count
        MedianElapsedMilliseconds = [Math]::Round((Get-Median @($group.Group.ElapsedMilliseconds)), 3)
        MedianWorkerMilliseconds = [Math]::Round((Get-Median @($group.Group.WorkerMilliseconds)), 3)
        MedianApplyMilliseconds = [Math]::Round((Get-Median @($group.Group.ApplyMilliseconds)), 3)
        MedianWorkbenchBindingMilliseconds = [Math]::Round(
            (Get-Median @($group.Group.WorkbenchBindingMilliseconds)),
            3)
        MaximumWorkbenchBindingMilliseconds = [Math]::Round(
            (($group.Group.WorkbenchBindingMilliseconds | Measure-Object -Maximum).Maximum),
            3)
        Pass = ($group.Group | Where-Object { -not $_.Pass }).Count -eq 0
    }
}

$distinctGrids = @($summaryRows.Grid | Sort-Object -Unique).Count
$maximumBytes = ($runRows.Bytes | Measure-Object -Maximum).Maximum
$matrixPass = $runRows.Count -eq ($sources.Count * $Repetitions) `
    -and ($runRows | Where-Object { -not $_.Pass }).Count -eq 0 `
    -and $distinctGrids -ge 2 `
    -and $maximumBytes -ge 1MB

$runCsvPath = Join-Path $artifactRoot "release-multi-c3d-runs.csv"
$summaryCsvPath = Join-Path $artifactRoot "release-multi-c3d-summary.csv"
$summaryTextPath = Join-Path $artifactRoot "release-multi-c3d-summary.txt"
$runRows | Export-Csv -LiteralPath $runCsvPath -NoTypeInformation -Encoding utf8
$summaryRows | Export-Csv -LiteralPath $summaryCsvPath -NoTypeInformation -Encoding utf8

$summaryLines = [System.Collections.Generic.List[string]]::new()
$summaryLines.Add("OpenVisionLab 3D Release multi-C3D performance matrix")
$summaryLines.Add("Result: $(if ($matrixPass) { 'Pass' } else { 'Fail' })")
$summaryLines.Add("Configuration: $Configuration")
$summaryLines.Add("Sources: $($sources.Count)")
$summaryLines.Add("DistinctGrids: $distinctGrids")
$summaryLines.Add("Runs: $($runRows.Count)")
$summaryLines.Add("MaximumSourceBytes: $maximumBytes")
$summaryLines.Add("Acceptance: actual EXE Pass; exact target/current path; Workbench binding <= 200 ms; UI apply <= 300 ms; one apply render; suppressed duplicate renders; VBO+IBO with zero fallback; load state cleared.")
$summaryLines.Add("")
foreach ($row in $summaryRows) {
    $summaryLines.Add(
        "$($row.Source) | $($row.Grid) | $($row.MiB) MiB | " +
        "median total $($row.MedianElapsedMilliseconds) ms | " +
        "worker $($row.MedianWorkerMilliseconds) ms | " +
        "apply $($row.MedianApplyMilliseconds) ms | " +
        "workbench $($row.MedianWorkbenchBindingMilliseconds) ms | " +
        "workbench max $($row.MaximumWorkbenchBindingMilliseconds) ms | " +
        "pass=$($row.Pass)")
}
$summaryLines | Set-Content -LiteralPath $summaryTextPath -Encoding utf8

Get-Content -LiteralPath $summaryTextPath
if (-not $matrixPass) {
    exit 1
}
