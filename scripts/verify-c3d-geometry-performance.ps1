[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Debug',
    [string]$ArtifactDirectory = 'artifacts/c3d_geometry_performance_20260715',
    [ValidateRange(31, 200)]
    [int]$RenderFrames = 31,
    [switch]$SkipBuild
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
Set-Location $repoRoot
$project = Join-Path $repoRoot 'src/OpenVisionLab.ThreeDStudio/OpenVisionLab.ThreeDStudio.csproj'
$solution = Join-Path $repoRoot 'OpenVisionLab.ThreeDStudio.slnx'
$artifactRoot = [System.IO.Path]::GetFullPath((Join-Path $repoRoot $ArtifactDirectory))
New-Item -ItemType Directory -Force -Path $artifactRoot | Out-Null

if (-not $SkipBuild) {
    & dotnet build $solution -c $Configuration
    if ($LASTEXITCODE -ne 0) {
        throw "Solution build failed with exit code $LASTEXITCODE."
    }
}

$densities = @(
    [pscustomobject]@{ Name = 'Fast'; MinimumFps = 20.0; MaximumDrawMs = 50.0 },
    [pscustomobject]@{ Name = 'Balanced'; MinimumFps = 12.0; MaximumDrawMs = 83.333 },
    [pscustomobject]@{ Name = 'Detailed'; MinimumFps = 6.0; MaximumDrawMs = 166.667 }
)
$styles = @(
    [pscustomobject]@{ Id = 'Points'; Action = 'geometry-points'; Slug = 'points' },
    [pscustomobject]@{ Id = 'Wireframe'; Action = 'geometry-wireframe'; Slug = 'wireframe' },
    [pscustomobject]@{ Id = 'Surface'; Action = 'geometry-surface'; Slug = 'surface' },
    [pscustomobject]@{ Id = 'SurfaceWithEdges'; Action = 'geometry-surface-edges'; Slug = 'surface_edges' }
)

$invariant = [System.Globalization.CultureInfo]::InvariantCulture
$results = New-Object System.Collections.Generic.List[string]
$measurements = @{}
$imageHashes = @{}
$pointCounts = @{}
$failed = $false

$results.Add('OpenVisionLab 3D C3D Geometry Style performance verification')
$results.Add("Generated|utc=$([DateTimeOffset]::UtcNow.ToString('O', $invariant))|configuration=$Configuration|renderFrames=$RenderFrames")
foreach ($density in $densities) {
    $results.Add("Threshold|density=$($density.Name)|minimumFps=$($density.MinimumFps.ToString('F3', $invariant))|maximumDrawMs=$($density.MaximumDrawMs.ToString('F3', $invariant))")
    $measurements[$density.Name] = @()
    $imageHashes[$density.Name] = @()
    $pointCounts[$density.Name] = @()
}

function Get-ContractLine {
    param([string]$Content, [string]$Prefix)

    $line = @($Content -split '\r?\n' | Where-Object { $_.StartsWith($Prefix, [StringComparison]::Ordinal) })
    if ($line.Count -ne 1) {
        throw "Expected one '$Prefix' line, found $($line.Count)."
    }

    return $line[0]
}

function Get-ContractField {
    param([string]$Line, [string]$Name)

    $match = [regex]::Match($Line, "(?:^|\|)$([regex]::Escape($Name))=([^|]*)")
    if (-not $match.Success) {
        throw "Field '$Name' was not found in '$Line'."
    }

    return $match.Groups[1].Value
}

foreach ($density in $densities) {
    foreach ($style in $styles) {
        $caseName = "$($density.Name.ToLowerInvariant())_$($style.Slug)"
        $screenshot = Join-Path $artifactRoot "$caseName.png"
        $quality = Join-Path $artifactRoot "$caseName-quality.txt"
        $contract = Join-Path $artifactRoot "$caseName-contract.txt"
        Write-Host "==> C3D performance $($density.Name) / $($style.Id)"

        $arguments = @(
            'run',
            '--project', $project,
            '-c', $Configuration,
            '--no-build',
            '--',
            '--smoke-screenshot', $screenshot,
            '--smoke-screenshot-quality-report', $quality,
            '--smoke-contracts', $contract,
            '--smoke-c3d', 'thickness',
            '--smoke-density', $density.Name,
            '--smoke-action', $style.Action,
            '--smoke-measure', 'two-point',
            '--smoke-render-frames', $RenderFrames.ToString($invariant)
        )
        & dotnet @arguments
        $exitCode = $LASTEXITCODE
        if ($exitCode -ne 0) {
            $failed = $true
            $results.Add("FAIL|density=$($density.Name)|style=$($style.Id)|exit=$exitCode")
            continue
        }

        try {
            if (-not (Test-Path -LiteralPath $screenshot -PathType Leaf)) {
                throw "Screenshot was not created: $screenshot"
            }
            if (-not (Test-Path -LiteralPath $quality -PathType Leaf)) {
                throw "Quality report was not created: $quality"
            }
            if (-not (Test-Path -LiteralPath $contract -PathType Leaf)) {
                throw "Contract was not created: $contract"
            }

            $qualityText = Get-Content -LiteralPath $quality -Raw
            if ($qualityText.IndexOf('ViewerScreenshotResult|accepted=True', [StringComparison]::Ordinal) -lt 0) {
                throw 'Screenshot quality was not accepted.'
            }

            $content = Get-Content -LiteralPath $contract -Raw
            $displayLine = Get-ContractLine $content 'DisplaySettings|'
            $densityLine = Get-ContractLine $content 'RenderDensity|'
            $proxyLine = Get-ContractLine $content 'C3DRenderProxy|'
            $performanceLine = Get-ContractLine $content 'Performance|'
            $performanceSmokeLine = Get-ContractLine $content 'PerformanceSmoke|'
            $measurementLine = Get-ContractLine $content 'TwoPoint|'

            $actualStyle = Get-ContractField $displayLine 'geometryStyleId'
            $actualDensity = Get-ContractField $densityLine 'mode'
            $fpsText = Get-ContractField $performanceLine 'fps'
            $drawText = Get-ContractField $performanceLine 'drawMs'
            $configured = Get-ContractField $performanceSmokeLine 'configured'
            $requestedFrames = [int](Get-ContractField $performanceSmokeLine 'requestedFrames')
            $completedFrames = [int](Get-ContractField $performanceSmokeLine 'completedFrames')
            $finite = Get-ContractField $performanceSmokeLine 'finite'
            $points = [int](Get-ContractField $proxyLine 'points')
            $triangles = [int](Get-ContractField $proxyLine 'triangles')
            $edges = [int](Get-ContractField $proxyLine 'edges')
            $gridEdges = [int](Get-ContractField $proxyLine 'gridEdges')
            $surfaceEdges = [int](Get-ContractField $proxyLine 'surfaceEdges')
            $surfaceEdgeInterval = [int](Get-ContractField $proxyLine 'surfaceEdgeInterval')
            $renderCache = Get-ContractField $proxyLine 'renderCache'
            $renderCacheReady = Get-ContractField $proxyLine 'renderCacheReady'

            if ($actualStyle -ne $style.Id) {
                throw "Effective style was '$actualStyle', expected '$($style.Id)'."
            }
            if ($actualDensity -ne $density.Name) {
                throw "Density was '$actualDensity', expected '$($density.Name)'."
            }
            if ($configured -ne 'True' -or $finite -ne 'True') {
                throw "Performance smoke was not finite: $performanceSmokeLine"
            }
            if ($requestedFrames -ne $RenderFrames -or $completedFrames -ne $RenderFrames) {
                throw "Render-frame count mismatch: $performanceSmokeLine"
            }
            if ($gridEdges -le 0 -or
                $gridEdges -ge $edges -or
                $surfaceEdges -le 0 -or
                $surfaceEdges -ge $gridEdges -or
                $surfaceEdgeInterval -ne 4) {
                throw "Display edge proxies were not reduced: edges=$edges gridEdges=$gridEdges surfaceEdges=$surfaceEdges interval=$surfaceEdgeInterval"
            }
            if ($renderCache -ne 'OpenGLDisplayList' -or $renderCacheReady -ne 'True') {
                throw "Static C3D render cache was not ready: cache=$renderCache ready=$renderCacheReady"
            }

            $fps = [double]::Parse($fpsText, $invariant)
            $drawMs = [double]::Parse($drawText, $invariant)
            $casePassed = (
                (-not [double]::IsNaN($fps)) -and
                (-not [double]::IsInfinity($fps)) -and
                (-not [double]::IsNaN($drawMs)) -and
                (-not [double]::IsInfinity($drawMs)) -and
                $fps -ge $density.MinimumFps -and
                $drawMs -le $density.MaximumDrawMs
            )
            $status = if ($casePassed) { 'PASS' } else { 'FAIL' }
            if (-not $casePassed) {
                $failed = $true
            }

            $hash = (Get-FileHash -LiteralPath $screenshot -Algorithm SHA256).Hash
            $measurements[$density.Name] += $measurementLine
            $imageHashes[$density.Name] += $hash
            $pointCounts[$density.Name] += $points
            $results.Add(
                "$status|density=$($density.Name)|style=$($style.Id)|fps=$($fps.ToString('F3', $invariant))|drawMs=$($drawMs.ToString('F3', $invariant))|minimumFps=$($density.MinimumFps.ToString('F3', $invariant))|maximumDrawMs=$($density.MaximumDrawMs.ToString('F3', $invariant))|points=$points|triangles=$triangles|edges=$edges|gridEdges=$gridEdges|surfaceEdges=$surfaceEdges|surfaceEdgeInterval=$surfaceEdgeInterval|renderCache=$renderCache|renderCacheReady=$renderCacheReady|frames=$completedFrames|screenshotSha256=$hash")
        }
        catch {
            $failed = $true
            $results.Add("FAIL|density=$($density.Name)|style=$($style.Id)|error=$($_.Exception.Message)")
        }
    }
}

foreach ($density in $densities) {
    $measurementVariants = @($measurements[$density.Name] | Sort-Object -Unique).Count
    $hashVariants = @($imageHashes[$density.Name] | Sort-Object -Unique).Count
    $densityPointVariants = @($pointCounts[$density.Name] | Sort-Object -Unique)
    $aggregatePassed = (
        $measurementVariants -eq 1 -and
        $hashVariants -eq $styles.Count -and
        $densityPointVariants.Count -eq 1
    )
    if (-not $aggregatePassed) {
        $failed = $true
    }

    $status = if ($aggregatePassed) { 'PASS' } else { 'FAIL' }
    $pointValue = if ($densityPointVariants.Count -eq 1) { $densityPointVariants[0] } else { '(mixed)' }
    $results.Add("$status|density=$($density.Name)|measurementVariants=$measurementVariants|imageHashVariants=$hashVariants|pointCountVariants=$($densityPointVariants.Count)|points=$pointValue")
}

$fastPoints = @($pointCounts['Fast'] | Sort-Object -Unique)
$balancedPoints = @($pointCounts['Balanced'] | Sort-Object -Unique)
$detailedPoints = @($pointCounts['Detailed'] | Sort-Object -Unique)
$densityOrderPassed = (
    $fastPoints.Count -eq 1 -and
    $balancedPoints.Count -eq 1 -and
    $detailedPoints.Count -eq 1 -and
    $fastPoints[0] -lt $balancedPoints[0] -and
    $balancedPoints[0] -lt $detailedPoints[0]
)
if (-not $densityOrderPassed) {
    $failed = $true
}
$densityOrderStatus = if ($densityOrderPassed) { 'PASS' } else { 'FAIL' }
$results.Add("$densityOrderStatus|densityOrder|fast=$($fastPoints -join ',')|balanced=$($balancedPoints -join ',')|detailed=$($detailedPoints -join ',')")

$casePassCount = @($results | Where-Object { $_ -like 'PASS|density=*|style=*' }).Count
$caseFailCount = @($results | Where-Object { $_ -like 'FAIL|density=*|style=*' }).Count
$results.Add("Summary|pass=$(-not $failed)|casesPassed=$casePassCount|casesFailed=$caseFailCount|casesExpected=$($densities.Count * $styles.Count)")

$summaryPath = Join-Path $artifactRoot 'c3d-geometry-performance-summary.txt'
[System.IO.File]::WriteAllLines($summaryPath, $results, [System.Text.UTF8Encoding]::new($false))
Write-Host "Summary: $summaryPath"
if ($failed) {
    exit 1
}
