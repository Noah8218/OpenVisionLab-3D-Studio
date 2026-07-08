param(
    [string]$Configuration = "Debug",
    [string]$ArtifactDir = "artifacts",
    [switch]$SkipBuild
)

$ErrorActionPreference = "Stop"

$RepoRoot = Split-Path -Parent $PSScriptRoot
Set-Location $RepoRoot

$ArtifactPath = Join-Path $RepoRoot $ArtifactDir
New-Item -ItemType Directory -Force -Path $ArtifactPath | Out-Null

$SummaryPath = Join-Path $ArtifactPath "matrix_smoke_summary_after.txt"
$Results = New-Object System.Collections.Generic.List[string]
$Failed = $false

function Add-Result {
    param(
        [string]$Status,
        [string]$Name,
        [string]$Detail
    )

    $Results.Add("$Status|$Name|$Detail")
}

function Get-ArtifactPath {
    param(
        [string]$FileName
    )

    Join-Path $script:ArtifactPath $FileName
}

function Invoke-MatrixStep {
    param(
        [string]$Name,
        [int]$ExpectedExitCode,
        [string[]]$Arguments
    )

    Write-Host "==> $Name"
    & dotnet @Arguments
    $ExitCode = $LASTEXITCODE
    if ($ExitCode -ne $ExpectedExitCode) {
        $script:Failed = $true
        Add-Result "FAIL" $Name "exit=$ExitCode expected=$ExpectedExitCode"
        Write-Host "FAIL $Name exit=$ExitCode expected=$ExpectedExitCode"
        return
    }

    Add-Result "PASS" $Name "exit=$ExitCode"
}

function Assert-FileContains {
    param(
        [string]$Name,
        [string]$Path,
        [string]$ExpectedText
    )

    if (-not (Test-Path -LiteralPath $Path)) {
        $script:Failed = $true
        Add-Result "FAIL" $Name "missing=$Path"
        Write-Host "FAIL $Name missing=$Path"
        return
    }

    $Content = Get-Content -LiteralPath $Path -Raw
    if (-not $Content.Contains($ExpectedText)) {
        $script:Failed = $true
        Add-Result "FAIL" $Name "missing text=$ExpectedText"
        Write-Host "FAIL $Name missing text=$ExpectedText"
        return
    }

    Add-Result "PASS" $Name "contains=$ExpectedText"
}

if (-not $SkipBuild) {
    Invoke-MatrixStep "build solution" 0 @(
        "build", "OpenVisionLab.ThreeDStudio.slnx",
        "-c", $Configuration,
        "--no-restore")
}

Invoke-MatrixStep "viewer C3D thickness" 0 @(
    "run", "--project", "src\OpenVisionLab.ThreeDStudio\OpenVisionLab.ThreeDStudio.csproj",
    "-c", $Configuration, "--no-build", "--",
    "--smoke-screenshot", (Get-ArtifactPath "matrix_c3d_thickness_after.png"),
    "--smoke-c3d", "thickness",
    "--smoke-contracts", (Get-ArtifactPath "matrix_c3d_thickness_after.txt"))

Invoke-MatrixStep "viewer GLB box" 0 @(
    "run", "--project", "src\OpenVisionLab.ThreeDStudio\OpenVisionLab.ThreeDStudio.csproj",
    "-c", $Configuration, "--no-build", "--",
    "--smoke-screenshot", (Get-ArtifactPath "matrix_glb_box_after.png"),
    "--smoke-glb", "3D\PublicSamples\glTF\Box.glb",
    "--smoke-contracts", (Get-ArtifactPath "matrix_glb_box_after.txt"))

Invoke-MatrixStep "viewer GLB vertex colors" 0 @(
    "run", "--project", "src\OpenVisionLab.ThreeDStudio\OpenVisionLab.ThreeDStudio.csproj",
    "-c", $Configuration, "--no-build", "--",
    "--smoke-screenshot", (Get-ArtifactPath "matrix_glb_vertex_color_after.png"),
    "--smoke-glb", "3D\PublicSamples\glTF\BoxVertexColors.glb",
    "--smoke-contracts", (Get-ArtifactPath "matrix_glb_vertex_color_after.txt"))

Invoke-MatrixStep "viewer GLB textured" 0 @(
    "run", "--project", "src\OpenVisionLab.ThreeDStudio\OpenVisionLab.ThreeDStudio.csproj",
    "-c", $Configuration, "--no-build", "--",
    "--smoke-screenshot", (Get-ArtifactPath "matrix_glb_textured_after.png"),
    "--smoke-glb", "3D\PublicSamples\glTF\BoxTextured.glb",
    "--smoke-contracts", (Get-ArtifactPath "matrix_glb_textured_after.txt"))

Invoke-MatrixStep "viewer GLB avocado" 0 @(
    "run", "--project", "src\OpenVisionLab.ThreeDStudio\OpenVisionLab.ThreeDStudio.csproj",
    "-c", $Configuration, "--no-build", "--",
    "--smoke-screenshot", (Get-ArtifactPath "matrix_glb_avocado_after.png"),
    "--smoke-glb", "3D\PublicSamples\glTF\Avocado.glb",
    "--smoke-contracts", (Get-ArtifactPath "matrix_glb_avocado_after.txt"))

Invoke-MatrixStep "viewer GLB avocado pick" 0 @(
    "run", "--project", "src\OpenVisionLab.ThreeDStudio\OpenVisionLab.ThreeDStudio.csproj",
    "-c", $Configuration, "--no-build", "--",
    "--smoke-screenshot", (Get-ArtifactPath "matrix_glb_avocado_pick_after.png"),
    "--smoke-glb", "3D\PublicSamples\glTF\Avocado.glb",
    "--smoke-pick", "glb",
    "--smoke-contracts", (Get-ArtifactPath "matrix_glb_avocado_pick_after.txt"))

Invoke-MatrixStep "viewer GLB avocado measurement" 0 @(
    "run", "--project", "src\OpenVisionLab.ThreeDStudio\OpenVisionLab.ThreeDStudio.csproj",
    "-c", $Configuration, "--no-build", "--",
    "--smoke-screenshot", (Get-ArtifactPath "matrix_glb_avocado_measure_after.png"),
    "--smoke-glb", "3D\PublicSamples\glTF\Avocado.glb",
    "--smoke-measure", "glb-two-point",
    "--smoke-contracts", (Get-ArtifactPath "matrix_glb_avocado_measure_after.txt"))

Invoke-MatrixStep "viewer GLB simple instancing" 0 @(
    "run", "--project", "src\OpenVisionLab.ThreeDStudio\OpenVisionLab.ThreeDStudio.csproj",
    "-c", $Configuration, "--no-build", "--",
    "--smoke-screenshot", (Get-ArtifactPath "matrix_glb_simple_instancing_after.png"),
    "--smoke-glb", "3D\PublicSamples\glTF\SimpleInstancing.glb",
    "--smoke-contracts", (Get-ArtifactPath "matrix_glb_simple_instancing_after.txt"))

Invoke-MatrixStep "viewer GLB simple instancing pick" 0 @(
    "run", "--project", "src\OpenVisionLab.ThreeDStudio\OpenVisionLab.ThreeDStudio.csproj",
    "-c", $Configuration, "--no-build", "--",
    "--smoke-screenshot", (Get-ArtifactPath "matrix_glb_simple_instancing_pick_after.png"),
    "--smoke-glb", "3D\PublicSamples\glTF\SimpleInstancing.glb",
    "--smoke-pick", "glb",
    "--smoke-contracts", (Get-ArtifactPath "matrix_glb_simple_instancing_pick_after.txt"))

Invoke-MatrixStep "viewer GLB simple instancing measurement" 0 @(
    "run", "--project", "src\OpenVisionLab.ThreeDStudio\OpenVisionLab.ThreeDStudio.csproj",
    "-c", $Configuration, "--no-build", "--",
    "--smoke-screenshot", (Get-ArtifactPath "matrix_glb_simple_instancing_measure_after.png"),
    "--smoke-glb", "3D\PublicSamples\glTF\SimpleInstancing.glb",
    "--smoke-measure", "glb-two-point",
    "--smoke-contracts", (Get-ArtifactPath "matrix_glb_simple_instancing_measure_after.txt"))

Invoke-MatrixStep "viewer STL tetrahedron" 0 @(
    "run", "--project", "src\OpenVisionLab.ThreeDStudio\OpenVisionLab.ThreeDStudio.csproj",
    "-c", $Configuration, "--no-build", "--",
    "--smoke-screenshot", (Get-ArtifactPath "matrix_stl_tetrahedron_after.png"),
    "--smoke-stl", "3D\PublicSamples\STL\Tetrahedron.stl",
    "--smoke-contracts", (Get-ArtifactPath "matrix_stl_tetrahedron_after.txt"))

Invoke-MatrixStep "viewer STL tetrahedron pick" 0 @(
    "run", "--project", "src\OpenVisionLab.ThreeDStudio\OpenVisionLab.ThreeDStudio.csproj",
    "-c", $Configuration, "--no-build", "--",
    "--smoke-screenshot", (Get-ArtifactPath "matrix_stl_tetrahedron_pick_after.png"),
    "--smoke-stl", "3D\PublicSamples\STL\Tetrahedron.stl",
    "--smoke-pick", "mesh",
    "--smoke-contracts", (Get-ArtifactPath "matrix_stl_tetrahedron_pick_after.txt"))

Invoke-MatrixStep "viewer STL tetrahedron measurement" 0 @(
    "run", "--project", "src\OpenVisionLab.ThreeDStudio\OpenVisionLab.ThreeDStudio.csproj",
    "-c", $Configuration, "--no-build", "--",
    "--smoke-screenshot", (Get-ArtifactPath "matrix_stl_tetrahedron_measure_after.png"),
    "--smoke-stl", "3D\PublicSamples\STL\Tetrahedron.stl",
    "--smoke-measure", "mesh-two-point",
    "--smoke-contracts", (Get-ArtifactPath "matrix_stl_tetrahedron_measure_after.txt"))

Invoke-MatrixStep "viewer LAZ points" 0 @(
    "run", "--project", "src\OpenVisionLab.ThreeDStudio\OpenVisionLab.ThreeDStudio.csproj",
    "-c", $Configuration, "--no-build", "--",
    "--smoke-screenshot", (Get-ArtifactPath "matrix_laz_points_after.png"),
    "--smoke-laz-points", "3D\PublicSamples\PointCloud\xyzrgb_manuscript.laz",
    "--smoke-contracts", (Get-ArtifactPath "matrix_laz_points_after.txt"))

Invoke-MatrixStep "viewer LAZ points fast density" 0 @(
    "run", "--project", "src\OpenVisionLab.ThreeDStudio\OpenVisionLab.ThreeDStudio.csproj",
    "-c", $Configuration, "--no-build", "--",
    "--smoke-screenshot", (Get-ArtifactPath "matrix_laz_points_fast_after.png"),
    "--smoke-density", "Fast",
    "--smoke-laz-points", "3D\PublicSamples\PointCloud\xyzrgb_manuscript.laz",
    "--smoke-contracts", (Get-ArtifactPath "matrix_laz_points_fast_after.txt"))

Invoke-MatrixStep "viewer LAS interesting points" 0 @(
    "run", "--project", "src\OpenVisionLab.ThreeDStudio\OpenVisionLab.ThreeDStudio.csproj",
    "-c", $Configuration, "--no-build", "--",
    "--smoke-screenshot", (Get-ArtifactPath "matrix_las_interesting_after.png"),
    "--smoke-laz-points", "3D\PublicSamples\PointCloud\interesting.las",
    "--smoke-contracts", (Get-ArtifactPath "matrix_las_interesting_after.txt"))

Invoke-MatrixStep "viewer LAS interesting height color" 0 @(
    "run", "--project", "src\OpenVisionLab.ThreeDStudio\OpenVisionLab.ThreeDStudio.csproj",
    "-c", $Configuration, "--no-build", "--",
    "--smoke-screenshot", (Get-ArtifactPath "matrix_las_interesting_height_after.png"),
    "--smoke-laz-points", "3D\PublicSamples\PointCloud\interesting.las",
    "--smoke-action", "color-height",
    "--smoke-contracts", (Get-ArtifactPath "matrix_las_interesting_height_after.txt"))

Invoke-MatrixStep "viewer LAS deviation color guard" 0 @(
    "run", "--project", "src\OpenVisionLab.ThreeDStudio\OpenVisionLab.ThreeDStudio.csproj",
    "-c", $Configuration, "--no-build", "--",
    "--smoke-screenshot", (Get-ArtifactPath "matrix_las_interesting_deviation_guard_after.png"),
    "--smoke-laz-points", "3D\PublicSamples\PointCloud\interesting.las",
    "--smoke-action", "color-deviation",
    "--smoke-contracts", (Get-ArtifactPath "matrix_las_interesting_deviation_guard_after.txt"))

Invoke-MatrixStep "viewer LAS interesting pick" 0 @(
    "run", "--project", "src\OpenVisionLab.ThreeDStudio\OpenVisionLab.ThreeDStudio.csproj",
    "-c", $Configuration, "--no-build", "--",
    "--smoke-screenshot", (Get-ArtifactPath "matrix_las_interesting_pick_after.png"),
    "--smoke-laz-points", "3D\PublicSamples\PointCloud\interesting.las",
    "--smoke-pick", "laz",
    "--smoke-contracts", (Get-ArtifactPath "matrix_las_interesting_pick_after.txt"))

Invoke-MatrixStep "viewer LAS interesting measurement" 0 @(
    "run", "--project", "src\OpenVisionLab.ThreeDStudio\OpenVisionLab.ThreeDStudio.csproj",
    "-c", $Configuration, "--no-build", "--",
    "--smoke-screenshot", (Get-ArtifactPath "matrix_las_interesting_measure_after.png"),
    "--smoke-laz-points", "3D\PublicSamples\PointCloud\interesting.las",
    "--smoke-measure", "two-point",
    "--smoke-contracts", (Get-ArtifactPath "matrix_las_interesting_measure_after.txt"))

Invoke-MatrixStep "runner LAZ probe" 0 @(
    "run", "--project", "src\OpenVisionLab.ThreeD.Runner\OpenVisionLab.ThreeD.Runner.csproj",
    "-c", $Configuration, "--no-build", "--",
    "--laz-probe", "3D\PublicSamples\PointCloud\xyzrgb_manuscript.laz",
    "--report", (Get-ArtifactPath "matrix_laz_probe_after.txt"),
    "--max-sampled-points", "50000")

Invoke-MatrixStep "runner LAS interesting probe" 0 @(
    "run", "--project", "src\OpenVisionLab.ThreeD.Runner\OpenVisionLab.ThreeD.Runner.csproj",
    "-c", $Configuration, "--no-build", "--",
    "--laz-probe", "3D\PublicSamples\PointCloud\interesting.las",
    "--report", (Get-ArtifactPath "matrix_las_interesting_probe_after.txt"),
    "--max-sampled-points", "50000")

Invoke-MatrixStep "shell LAZ points" 0 @(
    "run", "--project", "src\OpenVisionLab.ThreeD.Shell\OpenVisionLab.ThreeD.Shell.csproj",
    "-c", $Configuration, "--no-build", "--",
    "--shell-smoke-screenshot", (Get-ArtifactPath "matrix_shell_laz_points_after.png"),
    "--smoke-laz-points", "3D\PublicSamples\PointCloud\xyzrgb_manuscript.laz")

Invoke-MatrixStep "shell LAS interesting points" 0 @(
    "run", "--project", "src\OpenVisionLab.ThreeD.Shell\OpenVisionLab.ThreeD.Shell.csproj",
    "-c", $Configuration, "--no-build", "--",
    "--shell-smoke-screenshot", (Get-ArtifactPath "matrix_shell_las_interesting_after.png"),
    "--smoke-laz-points", "3D\PublicSamples\PointCloud\interesting.las")

Invoke-MatrixStep "shell LAS interesting height color" 0 @(
    "run", "--project", "src\OpenVisionLab.ThreeD.Shell\OpenVisionLab.ThreeD.Shell.csproj",
    "-c", $Configuration, "--no-build", "--",
    "--shell-smoke-screenshot", (Get-ArtifactPath "matrix_shell_las_interesting_height_after.png"),
    "--smoke-laz-points", "3D\PublicSamples\PointCloud\interesting.las",
    "--smoke-action", "color-height")

Invoke-MatrixStep "shell LAS interesting measurement" 0 @(
    "run", "--project", "src\OpenVisionLab.ThreeD.Shell\OpenVisionLab.ThreeD.Shell.csproj",
    "-c", $Configuration, "--no-build", "--",
    "--shell-smoke-screenshot", (Get-ArtifactPath "matrix_shell_las_interesting_measure_after.png"),
    "--smoke-laz-points", "3D\PublicSamples\PointCloud\interesting.las",
    "--smoke-measure", "two-point")

Invoke-MatrixStep "shell GLB avocado measurement" 0 @(
    "run", "--project", "src\OpenVisionLab.ThreeD.Shell\OpenVisionLab.ThreeD.Shell.csproj",
    "-c", $Configuration, "--no-build", "--",
    "--shell-smoke-screenshot", (Get-ArtifactPath "matrix_shell_glb_avocado_measure_after.png"),
    "--smoke-glb", "3D\PublicSamples\glTF\Avocado.glb",
    "--smoke-measure", "glb-two-point")

Invoke-MatrixStep "shell GLB simple instancing measurement" 0 @(
    "run", "--project", "src\OpenVisionLab.ThreeD.Shell\OpenVisionLab.ThreeD.Shell.csproj",
    "-c", $Configuration, "--no-build", "--",
    "--shell-smoke-screenshot", (Get-ArtifactPath "matrix_shell_glb_simple_instancing_measure_after.png"),
    "--smoke-glb", "3D\PublicSamples\glTF\SimpleInstancing.glb",
    "--smoke-measure", "glb-two-point")

Invoke-MatrixStep "shell STL tetrahedron measurement" 0 @(
    "run", "--project", "src\OpenVisionLab.ThreeD.Shell\OpenVisionLab.ThreeD.Shell.csproj",
    "-c", $Configuration, "--no-build", "--",
    "--shell-smoke-screenshot", (Get-ArtifactPath "matrix_shell_stl_tetrahedron_measure_after.png"),
    "--smoke-stl", "3D\PublicSamples\STL\Tetrahedron.stl",
    "--smoke-measure", "mesh-two-point")

Invoke-MatrixStep "viewer missing GLB failure" 1 @(
    "run", "--project", "src\OpenVisionLab.ThreeDStudio\OpenVisionLab.ThreeDStudio.csproj",
    "-c", $Configuration, "--no-build", "--",
    "--smoke-screenshot", (Get-ArtifactPath "viewer_missing_glb_cause_after.png"),
    "--smoke-glb", "3D\PublicSamples\glTF\missing.glb",
    "--smoke-contracts", (Get-ArtifactPath "viewer_missing_glb_cause_after.txt"))

Invoke-MatrixStep "viewer missing STL failure" 1 @(
    "run", "--project", "src\OpenVisionLab.ThreeDStudio\OpenVisionLab.ThreeDStudio.csproj",
    "-c", $Configuration, "--no-build", "--",
    "--smoke-screenshot", (Get-ArtifactPath "viewer_missing_stl_cause_after.png"),
    "--smoke-stl", "3D\PublicSamples\STL\missing.stl",
    "--smoke-contracts", (Get-ArtifactPath "viewer_missing_stl_cause_after.txt"))

Invoke-MatrixStep "viewer missing LAZ failure" 1 @(
    "run", "--project", "src\OpenVisionLab.ThreeDStudio\OpenVisionLab.ThreeDStudio.csproj",
    "-c", $Configuration, "--no-build", "--",
    "--smoke-screenshot", (Get-ArtifactPath "viewer_missing_laz_cause_after.png"),
    "--smoke-laz-points", "3D\PublicSamples\PointCloud\missing.laz",
    "--smoke-contracts", (Get-ArtifactPath "viewer_missing_laz_cause_after.txt"))

Invoke-MatrixStep "viewer corrupt GLB failure" 1 @(
    "run", "--project", "src\OpenVisionLab.ThreeDStudio\OpenVisionLab.ThreeDStudio.csproj",
    "-c", $Configuration, "--no-build", "--",
    "--smoke-screenshot", (Get-ArtifactPath "viewer_corrupt_glb_cause_after.png"),
    "--smoke-glb", "3D\PublicSamples\Invalid\corrupt.glb",
    "--smoke-contracts", (Get-ArtifactPath "viewer_corrupt_glb_cause_after.txt"))

Invoke-MatrixStep "viewer corrupt STL failure" 1 @(
    "run", "--project", "src\OpenVisionLab.ThreeDStudio\OpenVisionLab.ThreeDStudio.csproj",
    "-c", $Configuration, "--no-build", "--",
    "--smoke-screenshot", (Get-ArtifactPath "viewer_corrupt_stl_cause_after.png"),
    "--smoke-stl", "3D\PublicSamples\Invalid\corrupt.stl",
    "--smoke-contracts", (Get-ArtifactPath "viewer_corrupt_stl_cause_after.txt"))

Invoke-MatrixStep "viewer corrupt LAZ failure" 1 @(
    "run", "--project", "src\OpenVisionLab.ThreeDStudio\OpenVisionLab.ThreeDStudio.csproj",
    "-c", $Configuration, "--no-build", "--",
    "--smoke-screenshot", (Get-ArtifactPath "viewer_corrupt_laz_cause_after.png"),
    "--smoke-laz-points", "3D\PublicSamples\Invalid\corrupt.laz",
    "--smoke-contracts", (Get-ArtifactPath "viewer_corrupt_laz_cause_after.txt"))

Invoke-MatrixStep "shell corrupt GLB failure" 1 @(
    "run", "--project", "src\OpenVisionLab.ThreeD.Shell\OpenVisionLab.ThreeD.Shell.csproj",
    "-c", $Configuration, "--no-build", "--",
    "--shell-smoke-screenshot", (Get-ArtifactPath "shell_corrupt_glb_cause_after.png"),
    "--smoke-glb", "3D\PublicSamples\Invalid\corrupt.glb")

Invoke-MatrixStep "shell corrupt STL failure" 1 @(
    "run", "--project", "src\OpenVisionLab.ThreeD.Shell\OpenVisionLab.ThreeD.Shell.csproj",
    "-c", $Configuration, "--no-build", "--",
    "--shell-smoke-screenshot", (Get-ArtifactPath "shell_corrupt_stl_cause_after.png"),
    "--smoke-stl", "3D\PublicSamples\Invalid\corrupt.stl")

Invoke-MatrixStep "shell corrupt LAZ failure" 1 @(
    "run", "--project", "src\OpenVisionLab.ThreeD.Shell\OpenVisionLab.ThreeD.Shell.csproj",
    "-c", $Configuration, "--no-build", "--",
    "--shell-smoke-screenshot", (Get-ArtifactPath "shell_corrupt_laz_cause_after.png"),
    "--smoke-laz-points", "3D\PublicSamples\Invalid\corrupt.laz")

Assert-FileContains "contract C3D entity" (Get-ArtifactPath "matrix_c3d_thickness_after.txt") "source.c3d-thickness|HeightGrid"
Assert-FileContains "contract coordinate frame" (Get-ArtifactPath "matrix_c3d_thickness_after.txt") "CoordinateFrame|visible=True"
Assert-FileContains "contract GLB box" (Get-ArtifactPath "matrix_glb_box_after.txt") "GLB|loaded=True"
Assert-FileContains "contract GLB vertex color" (Get-ArtifactPath "matrix_glb_vertex_color_after.txt") "usesVertexColors=True"
Assert-FileContains "contract GLB texture" (Get-ArtifactPath "matrix_glb_textured_after.txt") "hasTexture=True"
Assert-FileContains "contract GLB avocado" (Get-ArtifactPath "matrix_glb_avocado_after.txt") "Avocado.glb"
Assert-FileContains "contract GLB avocado texture" (Get-ArtifactPath "matrix_glb_avocado_after.txt") "hasTexture=True"
Assert-FileContains "contract GLB avocado pick" (Get-ArtifactPath "matrix_glb_avocado_pick_after.txt") "GLBPick|selected=True"
Assert-FileContains "contract GLB avocado surface pick" (Get-ArtifactPath "matrix_glb_avocado_pick_after.txt") "kind=mesh surface"
Assert-FileContains "contract GLB avocado surface triangle" (Get-ArtifactPath "matrix_glb_avocado_pick_after.txt") "triangleIndex="
Assert-FileContains "contract GLB avocado surface normal" (Get-ArtifactPath "matrix_glb_avocado_pick_after.txt") "normal=("
Assert-FileContains "contract GLB avocado surface overlay" (Get-ArtifactPath "matrix_glb_avocado_pick_after.txt") "GLBSurfaceOverlay|visible=True"
Assert-FileContains "contract GLB avocado fit camera" (Get-ArtifactPath "matrix_glb_avocado_pick_after.txt") "distance=0.350"
Assert-FileContains "contract GLB hides C3D height map" (Get-ArtifactPath "matrix_glb_avocado_after.txt") "HeightMap|visible=False"
Assert-FileContains "contract GLB hides C3D profile" (Get-ArtifactPath "matrix_glb_avocado_after.txt") "SectionProfile|visible=False"
Assert-FileContains "contract GLB avocado measurement" (Get-ArtifactPath "matrix_glb_avocado_measure_after.txt") "TwoPoint|visible=True"
Assert-FileContains "contract GLB avocado measurement mode" (Get-ArtifactPath "matrix_glb_avocado_measure_after.txt") "SelectionMode|value=Two Point Measure"
Assert-FileContains "contract GLB avocado measurement overlay" (Get-ArtifactPath "matrix_glb_avocado_measure_after.txt") "MeasurementOverlay|visible=True"
Assert-FileContains "contract GLB hides LAZ/LAS acceptance" (Get-ArtifactPath "matrix_glb_avocado_measure_after.txt") "LAZAcceptance|visible=False"
Assert-FileContains "contract GLB simple instancing sample" (Get-ArtifactPath "matrix_glb_simple_instancing_after.txt") "SimpleInstancing.glb"
Assert-FileContains "contract GLB simple instancing vertices" (Get-ArtifactPath "matrix_glb_simple_instancing_after.txt") "vertices=3000"
Assert-FileContains "contract GLB simple instancing triangles" (Get-ArtifactPath "matrix_glb_simple_instancing_after.txt") "triangles=1500"
Assert-FileContains "contract GLB simple instancing bounds" (Get-ArtifactPath "matrix_glb_simple_instancing_after.txt") "max=(12.732, 12.732, 12.732)"
Assert-FileContains "contract GLB simple instancing pick" (Get-ArtifactPath "matrix_glb_simple_instancing_pick_after.txt") "GLBPick|selected=True"
Assert-FileContains "contract GLB simple instancing measurement" (Get-ArtifactPath "matrix_glb_simple_instancing_measure_after.txt") "TwoPoint|visible=True"
Assert-FileContains "contract STL tetrahedron" (Get-ArtifactPath "matrix_stl_tetrahedron_after.txt") "STL|loaded=True"
Assert-FileContains "contract STL tetrahedron bounds" (Get-ArtifactPath "matrix_stl_tetrahedron_after.txt") "max=(1.000, 1.000, 1.000)"
Assert-FileContains "contract STL tetrahedron pick" (Get-ArtifactPath "matrix_stl_tetrahedron_pick_after.txt") "STLPick|selected=True"
Assert-FileContains "contract STL tetrahedron surface overlay" (Get-ArtifactPath "matrix_stl_tetrahedron_pick_after.txt") "STLSurfaceOverlay|visible=True"
Assert-FileContains "contract STL tetrahedron measurement" (Get-ArtifactPath "matrix_stl_tetrahedron_measure_after.txt") "TwoPoint|visible=True"
Assert-FileContains "contract STL hides LAZ/LAS acceptance" (Get-ArtifactPath "matrix_stl_tetrahedron_measure_after.txt") "LAZAcceptance|visible=False"
Assert-FileContains "contract LAZ points" (Get-ArtifactPath "matrix_laz_points_after.txt") "decoder=points-decoded"
Assert-FileContains "contract LAZ fit camera" (Get-ArtifactPath "matrix_laz_points_after.txt") "distance=343."
Assert-FileContains "contract LAZ performance telemetry" (Get-ArtifactPath "matrix_laz_points_after.txt") "PointCloudPerformance|loadMs="
Assert-FileContains "contract LAZ sampling wording" (Get-ArtifactPath "matrix_laz_points_after.txt") "summary=LAZ/LAS sampling:"
Assert-FileContains "contract LAZ render-density wording" (Get-ArtifactPath "matrix_laz_points_after.txt") "LAZ/LAS points"
Assert-FileContains "contract LAZ sample percent" (Get-ArtifactPath "matrix_laz_points_after.txt") "samplePercent=2.320"
Assert-FileContains "contract LAZ fast density mode" (Get-ArtifactPath "matrix_laz_points_fast_after.txt") "RenderDensity|mode=Fast"
Assert-FileContains "contract LAZ fast density sample budget" (Get-ArtifactPath "matrix_laz_points_fast_after.txt") "sampledLazPoints=25000"
Assert-FileContains "contract LAZ fast density sample percent" (Get-ArtifactPath "matrix_laz_points_fast_after.txt") "samplePercent=1.160"
Assert-FileContains "contract LAS interesting points" (Get-ArtifactPath "matrix_las_interesting_after.txt") "interesting.las"
Assert-FileContains "contract LAS interesting fit camera" (Get-ArtifactPath "matrix_las_interesting_after.txt") "distance=9337."
Assert-FileContains "contract LAS interesting sample percent" (Get-ArtifactPath "matrix_las_interesting_after.txt") "samplePercent=100.000"
Assert-FileContains "contract LAS hides C3D height map" (Get-ArtifactPath "matrix_las_interesting_after.txt") "HeightMap|visible=False"
Assert-FileContains "contract LAS hides C3D profile" (Get-ArtifactPath "matrix_las_interesting_after.txt") "SectionProfile|visible=False"
Assert-FileContains "contract LAS interesting height color" (Get-ArtifactPath "matrix_las_interesting_height_after.txt") "ColorMode|mode=Height"
Assert-FileContains "contract LAS interesting height legend" (Get-ArtifactPath "matrix_las_interesting_height_after.txt") "PointCloudColorLegend|visible=True"
Assert-FileContains "contract LAS deviation guard mode" (Get-ArtifactPath "matrix_las_interesting_deviation_guard_after.txt") "ColorMode|mode=RGB"
Assert-FileContains "contract LAS deviation guard status" (Get-ArtifactPath "matrix_las_interesting_deviation_guard_after.txt") "Deviation requires an active result"
Assert-FileContains "contract LAS interesting uncompressed" (Get-ArtifactPath "matrix_las_interesting_after.txt") "compressed=False"
Assert-FileContains "contract LAS interesting format" (Get-ArtifactPath "matrix_las_interesting_after.txt") "pointFormat=3"
Assert-FileContains "contract LAS interesting pick" (Get-ArtifactPath "matrix_las_interesting_pick_after.txt") "LAZPick|selected=True"
Assert-FileContains "contract LAS pick wording" (Get-ArtifactPath "matrix_las_interesting_pick_after.txt") "Smoke pick: LAZ/LAS sampled point"
Assert-FileContains "contract LAS interesting measurement" (Get-ArtifactPath "matrix_las_interesting_measure_after.txt") "TwoPoint|visible=True"
Assert-FileContains "contract LAS measurement tool wording" (Get-ArtifactPath "matrix_las_interesting_measure_after.txt") "LAZ/LAS Two Point Measurement"
Assert-FileContains "contract LAS measurement status wording" (Get-ArtifactPath "matrix_las_interesting_measure_after.txt") "Smoke measure: LAZ/LAS two-point distance and height delta"
Assert-FileContains "contract LAS interesting measurement overlay" (Get-ArtifactPath "matrix_las_interesting_measure_after.txt") "MeasurementOverlay|visible=True"
Assert-FileContains "contract LAS shows LAZ/LAS acceptance" (Get-ArtifactPath "matrix_las_interesting_measure_after.txt") "LAZAcceptance|visible=True"
Assert-FileContains "contract LAS acceptance wording" (Get-ArtifactPath "matrix_las_interesting_measure_after.txt") "LAZAcceptance|visible=True|summary=LAZ/LAS acceptance"
Assert-FileContains "runner LAZ bounds" (Get-ArtifactPath "matrix_laz_probe_after.txt") "boundsMatch=True"
Assert-FileContains "runner LAS interesting bounds" (Get-ArtifactPath "matrix_las_interesting_probe_after.txt") "boundsMatch=True"
Assert-FileContains "missing GLB cause" (Get-ArtifactPath "viewer_missing_glb_cause_after.txt") "Missing GLB sample"
Assert-FileContains "missing STL cause" (Get-ArtifactPath "viewer_missing_stl_cause_after.txt") "Missing STL sample"
Assert-FileContains "missing LAZ cause" (Get-ArtifactPath "viewer_missing_laz_cause_after.txt") "Missing LAZ/LAS sample"
Assert-FileContains "corrupt GLB cause" (Get-ArtifactPath "viewer_corrupt_glb_cause_after.txt") "Unsupported or corrupt GLB"
Assert-FileContains "corrupt STL cause" (Get-ArtifactPath "viewer_corrupt_stl_cause_after.txt") "Unsupported or corrupt STL"
Assert-FileContains "corrupt LAZ cause" (Get-ArtifactPath "viewer_corrupt_laz_cause_after.txt") "Unsupported or corrupt LAZ/LAS point decode"

$Header = @(
    "OpenVisionLab 3D data loading matrix smoke",
    "Generated: $(Get-Date -Format o)",
    "Configuration: $Configuration",
    ""
)

$Header + $Results | Set-Content -LiteralPath $SummaryPath
Write-Host "Summary: $SummaryPath"

if ($Failed) {
    exit 1
}

exit 0
