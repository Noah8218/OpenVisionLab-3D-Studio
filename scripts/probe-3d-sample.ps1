param(
    [Parameter(Mandatory = $true)]
    [string] $SamplePath,

    [string] $ArtifactDir = "artifacts\sample_probe",

    [string] $Configuration = "Debug",

    [string] $MaxSampledPoints = "50000",

    [string] $RenderDensity = "Balanced",

    [switch] $SkipBuild
)

$ErrorActionPreference = "Stop"
$SummaryLines = New-Object System.Collections.Generic.List[string]

function Get-ArtifactPath {
    param([string] $FileName)
    return Join-Path $ArtifactDir $FileName
}

function Get-SafeName {
    param([string] $Path)
    $name = [System.IO.Path]::GetFileNameWithoutExtension($Path)
    return ($name -replace "[^A-Za-z0-9_.-]", "_")
}

function Write-ProbeSummary {
    [System.IO.File]::WriteAllLines((Get-ArtifactPath "probe_summary.txt"), $SummaryLines)
}

function Get-FormatCandidateSummary {
    param([string] $Extension)

    switch ($Extension) {
        ".obj" { return "FORMAT_CANDIDATE|extension=.obj|kind=mesh|next=add OBJ mesh loader with material fallback, then GLB-like viewer probe" }
        ".ply" { return "FORMAT_CANDIDATE|extension=.ply|kind=mesh-or-point-cloud|next=add PLY vertex parser and decide mesh vs point-cloud contract" }
        ".pcd" { return "FORMAT_CANDIDATE|extension=.pcd|kind=point-cloud|next=add point-cloud loader and LAS/LAZ-like viewer probe" }
        ".e57" { return "FORMAT_CANDIDATE|extension=.e57|kind=point-cloud|next=prototype E57 loader before fixed matrix coverage" }
        ".step" { return "FORMAT_CANDIDATE|extension=.step|kind=cad|next=choose CAD kernel only after a small import prototype" }
        ".stp" { return "FORMAT_CANDIDATE|extension=.stp|kind=cad|next=choose CAD kernel only after a small import prototype" }
        ".iges" { return "FORMAT_CANDIDATE|extension=.iges|kind=cad|next=choose CAD kernel only after a small import prototype" }
        ".igs" { return "FORMAT_CANDIDATE|extension=.igs|kind=cad|next=choose CAD kernel only after a small import prototype" }
        default { return "FORMAT_CANDIDATE|extension=$Extension|kind=unknown|next=record sample source and decide whether it belongs in viewer MVP" }
    }
}

function Invoke-ProbeStep {
    param(
        [string] $Name,
        [int] $ExpectedExitCode,
        [string[]] $Arguments
    )

    Write-Host "==> $Name"
    & dotnet @Arguments
    $exitCode = $LASTEXITCODE
    $status = if ($exitCode -eq $ExpectedExitCode) { "PASS" } else { "FAIL" }
    $SummaryLines.Add("$status|$Name|exit=$exitCode")

    if ($exitCode -ne $ExpectedExitCode) {
        Write-ProbeSummary
        throw "$Name returned exit code $exitCode; expected $ExpectedExitCode."
    }
}

New-Item -ItemType Directory -Force -Path $ArtifactDir | Out-Null

$fullSamplePath = [System.IO.Path]::GetFullPath($SamplePath)
if (-not (Test-Path -LiteralPath $fullSamplePath -PathType Leaf)) {
    $SummaryLines.Add("FAIL|sample file not found|path=$fullSamplePath")
    Write-ProbeSummary
    throw "Sample file not found: $fullSamplePath"
}

$parsedMaxSampledPoints = 0
if (-not [int]::TryParse($MaxSampledPoints, [ref] $parsedMaxSampledPoints) -or $parsedMaxSampledPoints -lt 1) {
    $SummaryLines.Add("FAIL|invalid max sampled points|value=$MaxSampledPoints")
    Write-ProbeSummary
    throw "MaxSampledPoints must be greater than zero."
}

$validRenderDensities = @("Fast", "Balanced", "Detailed")
if ($RenderDensity -notin $validRenderDensities) {
    $SummaryLines.Add("FAIL|invalid render density|value=$RenderDensity|valid=Fast,Balanced,Detailed")
    Write-ProbeSummary
    throw "RenderDensity must be one of Fast, Balanced, or Detailed."
}

if (-not $SkipBuild) {
    Invoke-ProbeStep "build" 0 @(
        "build", "OpenVisionLab.ThreeDStudio.slnx",
        "-c", $Configuration)
}

$safeName = Get-SafeName $fullSamplePath
$extension = [System.IO.Path]::GetExtension($fullSamplePath).ToLowerInvariant()
$SummaryLines.Add("SETTINGS|configuration=$Configuration|renderDensity=$RenderDensity|maxSampledPoints=$parsedMaxSampledPoints")

switch ($extension) {
    ".stl" {
        Invoke-ProbeStep "viewer STL sample" 0 @(
            "run", "--project", "src\OpenVisionLab.ThreeDStudio\OpenVisionLab.ThreeDStudio.csproj",
            "-c", $Configuration, "--no-build", "--",
            "--smoke-screenshot", (Get-ArtifactPath "viewer_${safeName}_stl.png"),
            "--smoke-stl", $fullSamplePath,
            "--smoke-contracts", (Get-ArtifactPath "viewer_${safeName}_stl.txt"))

        Invoke-ProbeStep "viewer STL pick" 0 @(
            "run", "--project", "src\OpenVisionLab.ThreeDStudio\OpenVisionLab.ThreeDStudio.csproj",
            "-c", $Configuration, "--no-build", "--",
            "--smoke-screenshot", (Get-ArtifactPath "viewer_${safeName}_stl_pick.png"),
            "--smoke-stl", $fullSamplePath,
            "--smoke-pick", "mesh",
            "--smoke-contracts", (Get-ArtifactPath "viewer_${safeName}_stl_pick.txt"))

        Invoke-ProbeStep "viewer STL measurement" 0 @(
            "run", "--project", "src\OpenVisionLab.ThreeDStudio\OpenVisionLab.ThreeDStudio.csproj",
            "-c", $Configuration, "--no-build", "--",
            "--smoke-screenshot", (Get-ArtifactPath "viewer_${safeName}_stl_measure.png"),
            "--smoke-stl", $fullSamplePath,
            "--smoke-measure", "mesh-two-point",
            "--smoke-contracts", (Get-ArtifactPath "viewer_${safeName}_stl_measure.txt"))

        Invoke-ProbeStep "shell STL sample" 0 @(
            "run", "--project", "src\OpenVisionLab.ThreeD.Shell\OpenVisionLab.ThreeD.Shell.csproj",
            "-c", $Configuration, "--no-build", "--",
            "--shell-smoke-screenshot", (Get-ArtifactPath "shell_${safeName}_stl.png"),
            "--smoke-stl", $fullSamplePath)

        Invoke-ProbeStep "shell STL pick" 0 @(
            "run", "--project", "src\OpenVisionLab.ThreeD.Shell\OpenVisionLab.ThreeD.Shell.csproj",
            "-c", $Configuration, "--no-build", "--",
            "--shell-smoke-screenshot", (Get-ArtifactPath "shell_${safeName}_stl_pick.png"),
            "--smoke-stl", $fullSamplePath,
            "--smoke-pick", "mesh")

        Invoke-ProbeStep "shell STL measurement" 0 @(
            "run", "--project", "src\OpenVisionLab.ThreeD.Shell\OpenVisionLab.ThreeD.Shell.csproj",
            "-c", $Configuration, "--no-build", "--",
            "--shell-smoke-screenshot", (Get-ArtifactPath "shell_${safeName}_stl_measure.png"),
            "--smoke-stl", $fullSamplePath,
            "--smoke-measure", "mesh-two-point")
    }
    ".glb" {
        Invoke-ProbeStep "viewer GLB sample" 0 @(
            "run", "--project", "src\OpenVisionLab.ThreeDStudio\OpenVisionLab.ThreeDStudio.csproj",
            "-c", $Configuration, "--no-build", "--",
            "--smoke-screenshot", (Get-ArtifactPath "viewer_${safeName}_glb.png"),
            "--smoke-glb", $fullSamplePath,
            "--smoke-contracts", (Get-ArtifactPath "viewer_${safeName}_glb.txt"))

        Invoke-ProbeStep "viewer GLB pick" 0 @(
            "run", "--project", "src\OpenVisionLab.ThreeDStudio\OpenVisionLab.ThreeDStudio.csproj",
            "-c", $Configuration, "--no-build", "--",
            "--smoke-screenshot", (Get-ArtifactPath "viewer_${safeName}_glb_pick.png"),
            "--smoke-glb", $fullSamplePath,
            "--smoke-pick", "glb",
            "--smoke-contracts", (Get-ArtifactPath "viewer_${safeName}_glb_pick.txt"))

        Invoke-ProbeStep "viewer GLB measurement" 0 @(
            "run", "--project", "src\OpenVisionLab.ThreeDStudio\OpenVisionLab.ThreeDStudio.csproj",
            "-c", $Configuration, "--no-build", "--",
            "--smoke-screenshot", (Get-ArtifactPath "viewer_${safeName}_glb_measure.png"),
            "--smoke-glb", $fullSamplePath,
            "--smoke-measure", "glb-two-point",
            "--smoke-contracts", (Get-ArtifactPath "viewer_${safeName}_glb_measure.txt"))

        Invoke-ProbeStep "shell GLB sample" 0 @(
            "run", "--project", "src\OpenVisionLab.ThreeD.Shell\OpenVisionLab.ThreeD.Shell.csproj",
            "-c", $Configuration, "--no-build", "--",
            "--shell-smoke-screenshot", (Get-ArtifactPath "shell_${safeName}_glb.png"),
            "--smoke-glb", $fullSamplePath)

        Invoke-ProbeStep "shell GLB pick" 0 @(
            "run", "--project", "src\OpenVisionLab.ThreeD.Shell\OpenVisionLab.ThreeD.Shell.csproj",
            "-c", $Configuration, "--no-build", "--",
            "--shell-smoke-screenshot", (Get-ArtifactPath "shell_${safeName}_glb_pick.png"),
            "--smoke-glb", $fullSamplePath,
            "--smoke-pick", "glb")

        Invoke-ProbeStep "shell GLB measurement" 0 @(
            "run", "--project", "src\OpenVisionLab.ThreeD.Shell\OpenVisionLab.ThreeD.Shell.csproj",
            "-c", $Configuration, "--no-build", "--",
            "--shell-smoke-screenshot", (Get-ArtifactPath "shell_${safeName}_glb_measure.png"),
            "--smoke-glb", $fullSamplePath,
            "--smoke-measure", "glb-two-point")
    }
    { $_ -in ".las", ".laz" } {
        Invoke-ProbeStep "runner LAS/LAZ probe" 0 @(
            "run", "--project", "src\OpenVisionLab.ThreeD.Runner\OpenVisionLab.ThreeD.Runner.csproj",
            "-c", $Configuration, "--no-build", "--",
            "--laz-probe", $fullSamplePath,
            "--report", (Get-ArtifactPath "runner_${safeName}_probe.txt"),
            "--max-sampled-points", $parsedMaxSampledPoints.ToString([System.Globalization.CultureInfo]::InvariantCulture))

        Invoke-ProbeStep "viewer LAS/LAZ points" 0 @(
            "run", "--project", "src\OpenVisionLab.ThreeDStudio\OpenVisionLab.ThreeDStudio.csproj",
            "-c", $Configuration, "--no-build", "--",
            "--smoke-density", $RenderDensity,
            "--smoke-screenshot", (Get-ArtifactPath "viewer_${safeName}_points.png"),
            "--smoke-laz-points", $fullSamplePath,
            "--smoke-contracts", (Get-ArtifactPath "viewer_${safeName}_points.txt"))

        Invoke-ProbeStep "viewer LAS/LAZ height color" 0 @(
            "run", "--project", "src\OpenVisionLab.ThreeDStudio\OpenVisionLab.ThreeDStudio.csproj",
            "-c", $Configuration, "--no-build", "--",
            "--smoke-density", $RenderDensity,
            "--smoke-screenshot", (Get-ArtifactPath "viewer_${safeName}_height.png"),
            "--smoke-laz-points", $fullSamplePath,
            "--smoke-action", "color-height",
            "--smoke-contracts", (Get-ArtifactPath "viewer_${safeName}_height.txt"))

        Invoke-ProbeStep "viewer LAS/LAZ pick" 0 @(
            "run", "--project", "src\OpenVisionLab.ThreeDStudio\OpenVisionLab.ThreeDStudio.csproj",
            "-c", $Configuration, "--no-build", "--",
            "--smoke-density", $RenderDensity,
            "--smoke-screenshot", (Get-ArtifactPath "viewer_${safeName}_pick.png"),
            "--smoke-laz-points", $fullSamplePath,
            "--smoke-pick", "laz",
            "--smoke-contracts", (Get-ArtifactPath "viewer_${safeName}_pick.txt"))

        Invoke-ProbeStep "viewer LAS/LAZ measurement" 0 @(
            "run", "--project", "src\OpenVisionLab.ThreeDStudio\OpenVisionLab.ThreeDStudio.csproj",
            "-c", $Configuration, "--no-build", "--",
            "--smoke-density", $RenderDensity,
            "--smoke-screenshot", (Get-ArtifactPath "viewer_${safeName}_measure.png"),
            "--smoke-laz-points", $fullSamplePath,
            "--smoke-measure", "two-point",
            "--smoke-contracts", (Get-ArtifactPath "viewer_${safeName}_measure.txt"))

        Invoke-ProbeStep "shell LAS/LAZ height color" 0 @(
            "run", "--project", "src\OpenVisionLab.ThreeD.Shell\OpenVisionLab.ThreeD.Shell.csproj",
            "-c", $Configuration, "--no-build", "--",
            "--smoke-density", $RenderDensity,
            "--shell-smoke-screenshot", (Get-ArtifactPath "shell_${safeName}_height.png"),
            "--smoke-laz-points", $fullSamplePath,
            "--smoke-action", "color-height")

        Invoke-ProbeStep "shell LAS/LAZ measurement" 0 @(
            "run", "--project", "src\OpenVisionLab.ThreeD.Shell\OpenVisionLab.ThreeD.Shell.csproj",
            "-c", $Configuration, "--no-build", "--",
            "--smoke-density", $RenderDensity,
            "--shell-smoke-screenshot", (Get-ArtifactPath "shell_${safeName}_measure.png"),
            "--smoke-laz-points", $fullSamplePath,
            "--smoke-measure", "two-point")
    }
    default {
        $SummaryLines.Add((Get-FormatCandidateSummary $extension))
        $SummaryLines.Add("FAIL|unsupported sample extension|extension=$extension")
        Write-ProbeSummary
        throw "Unsupported sample extension '$extension'. Current probe supports .glb, .stl, .las, and .laz."
    }
}

$SummaryLines.Add("SUMMARY|sample=$fullSamplePath|artifacts=$([System.IO.Path]::GetFullPath($ArtifactDir))")
Write-ProbeSummary
Write-Host "Summary: $(Get-ArtifactPath "probe_summary.txt")"
