[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Debug',
    [string]$ArtifactDirectory = 'artifacts/imported_mesh_geometry_styles_20260715/verification',
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

$samples = @(
    [pscustomobject]@{
        Name = 'BoxTextured'
        Loader = '--smoke-glb'
        Path = '3D/PublicSamples/glTF/BoxTextured.glb'
        Format = 'GLB'
        ColorMap = 'Source'
        SourceKind = 'Texture'
    },
    [pscustomobject]@{
        Name = 'BoxVertexColors'
        Loader = '--smoke-glb'
        Path = '3D/PublicSamples/glTF/BoxVertexColors.glb'
        Format = 'GLB'
        ColorMap = 'Source'
        SourceKind = 'VertexColors'
    },
    [pscustomobject]@{
        Name = 'Tetrahedron'
        Loader = '--smoke-stl'
        Path = '3D/PublicSamples/STL/Tetrahedron.stl'
        Format = 'STL'
        ColorMap = 'Solid'
        SourceKind = 'Solid'
    }
)
$styles = @(
    [pscustomobject]@{ Id = 'Points'; Action = 'geometry-points'; Slug = 'points' },
    [pscustomobject]@{ Id = 'Wireframe'; Action = 'geometry-wireframe'; Slug = 'wireframe' },
    [pscustomobject]@{ Id = 'Surface'; Action = 'geometry-surface'; Slug = 'surface' },
    [pscustomobject]@{ Id = 'SurfaceWithEdges'; Action = 'geometry-surface-edges'; Slug = 'surface_edges' }
)

$results = New-Object System.Collections.Generic.List[string]
$failed = $false
$passed = 0
$results.Add('OpenVisionLab 3D imported-mesh Geometry Style verification')
$results.Add("Generated|utc=$([DateTimeOffset]::UtcNow.ToString('O'))|configuration=$Configuration")

function Get-ContractLine {
    param([string]$Content, [string]$Prefix)

    $lines = @($Content -split '\r?\n' | Where-Object { $_.StartsWith($Prefix, [StringComparison]::Ordinal) })
    if ($lines.Count -ne 1) {
        throw "Expected one '$Prefix' line, found $($lines.Count)."
    }

    return $lines[0]
}

function Get-ContractField {
    param([string]$Line, [string]$Name)

    $match = [regex]::Match($Line, "(?:^|\|)$([regex]::Escape($Name))=([^|]*)")
    if (-not $match.Success) {
        throw "Field '$Name' was not found in '$Line'."
    }

    return $match.Groups[1].Value
}

foreach ($sample in $samples) {
    $sampleDirectory = Join-Path $artifactRoot $sample.Name
    New-Item -ItemType Directory -Force -Path $sampleDirectory | Out-Null
    $pickLines = New-Object System.Collections.Generic.List[string]
    $measurementLines = New-Object System.Collections.Generic.List[string]
    $imageHashes = New-Object System.Collections.Generic.List[string]

    foreach ($style in $styles) {
        $screenshot = Join-Path $sampleDirectory "$($style.Slug).png"
        $quality = Join-Path $sampleDirectory "$($style.Slug)-quality.txt"
        $contract = Join-Path $sampleDirectory "$($style.Slug)-contract.txt"
        Write-Host "==> Imported mesh $($sample.Name) / $($style.Id)"

        $arguments = @(
            'run',
            '--project', $project,
            '-c', $Configuration,
            '--no-build',
            '--',
            '--smoke-screenshot', $screenshot,
            '--smoke-screenshot-quality-report', $quality,
            '--smoke-contracts', $contract,
            $sample.Loader, $sample.Path,
            '--smoke-action', $style.Action,
            '--smoke-pick', 'glb',
            '--smoke-measure', 'two-point'
        )
        & dotnet @arguments
        $exitCode = $LASTEXITCODE

        try {
            if ($exitCode -ne 0) {
                throw "Viewer exited with code $exitCode."
            }

            $qualityText = Get-Content -LiteralPath $quality -Raw
            if ($qualityText.IndexOf('ViewerScreenshotResult|accepted=True', [StringComparison]::Ordinal) -lt 0) {
                throw 'Screenshot quality was not accepted.'
            }

            $content = Get-Content -LiteralPath $contract -Raw
            $displayLine = Get-ContractLine $content 'DisplaySettings|'
            $meshLine = Get-ContractLine $content "$($sample.Format)|"
            $pickLine = Get-ContractLine $content "$($sample.Format)Pick|"
            $measurementLine = Get-ContractLine $content 'TwoPoint|'

            if ((Get-ContractField $displayLine 'sourceId') -ne 'ImportedTriangleMesh' -or
                (Get-ContractField $displayLine 'geometryStyleId') -ne $style.Id -or
                (Get-ContractField $displayLine 'geometrySelectable') -ne 'True' -or
                (Get-ContractField $displayLine 'colorMapId') -ne $sample.ColorMap -or
                (Get-ContractField $displayLine 'geometryRenderBridge') -ne 'SharpGLImportedTriangleMesh') {
                throw "Display contract mismatch: $displayLine"
            }

            if ((Get-ContractField $meshLine 'loaded') -ne 'True' -or
                [int](Get-ContractField $meshLine 'renderedTriangles') -le 0 -or
                (Get-ContractField $pickLine 'selected') -ne 'True' -or
                (Get-ContractField $measurementLine 'visible') -ne 'True') {
                throw 'Mesh, pick, or measurement evidence is incomplete.'
            }

            switch ($sample.SourceKind) {
                'Texture' {
                    if ((Get-ContractField $meshLine 'hasTexture') -ne 'True' -or
                        [int](Get-ContractField $meshLine 'texCoords') -le 0 -or
                        -not (Get-ContractField $meshLine 'textureUpload').StartsWith('uploaded ', [StringComparison]::Ordinal)) {
                        throw "Texture behavior was not preserved: $meshLine"
                    }
                }
                'VertexColors' {
                    if ((Get-ContractField $meshLine 'usesVertexColors') -ne 'True' -or
                        [int](Get-ContractField $meshLine 'vertexColors') -le 0) {
                        throw "Vertex-color behavior was not preserved: $meshLine"
                    }
                }
                'Solid' {
                    if ((Get-ContractField $meshLine 'hasTexture') -ne 'False' -or
                        (Get-ContractField $meshLine 'usesVertexColors') -ne 'False') {
                        throw "Solid-color fallback changed: $meshLine"
                    }
                }
            }

            $pickLines.Add($pickLine)
            $measurementLines.Add($measurementLine)
            $hash = (Get-FileHash -LiteralPath $screenshot -Algorithm SHA256).Hash
            $imageHashes.Add($hash)
            $passed++
            $results.Add("PASS|sample=$($sample.Name)|style=$($style.Id)|colorMap=$($sample.ColorMap)|sourceKind=$($sample.SourceKind)|screenshotSha256=$hash")
        }
        catch {
            $failed = $true
            $results.Add("FAIL|sample=$($sample.Name)|style=$($style.Id)|error=$($_.Exception.Message)")
        }
    }

    $pickVariants = @($pickLines | Sort-Object -Unique).Count
    $measurementVariants = @($measurementLines | Sort-Object -Unique).Count
    $imageHashVariants = @($imageHashes | Sort-Object -Unique).Count
    $invariantsPassed = $pickVariants -eq 1 -and $measurementVariants -eq 1 -and $imageHashVariants -eq $styles.Count
    if ($invariantsPassed) {
        $passed++
        $results.Add("PASS|sample=$($sample.Name)|pickVariants=$pickVariants|measurementVariants=$measurementVariants|imageHashVariants=$imageHashVariants")
    }
    else {
        $failed = $true
        $results.Add("FAIL|sample=$($sample.Name)|pickVariants=$pickVariants|measurementVariants=$measurementVariants|imageHashVariants=$imageHashVariants")
    }
}

$summary = if ($failed) {
    "Imported-mesh Geometry Style verification: FAIL ($passed/15 checks passed)"
}
else {
    "Imported-mesh Geometry Style verification: PASS ($passed/15 checks passed)"
}
$results.Add($summary)
$summaryPath = Join-Path $artifactRoot 'summary.txt'
$results | Set-Content -LiteralPath $summaryPath -Encoding utf8
Write-Host $summary
Write-Host "Summary: $summaryPath"

if ($failed) {
    exit 1
}
