[CmdletBinding()]
param(
    [string]$ReportPath = "artifacts/current/20260726-code-structure-guard/code-structure-report.txt",
    [string]$SolutionPath,
    [string]$SolutionXmlPath
)

$ErrorActionPreference = "Stop"
$repoRoot = [System.IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot))
$SolutionPath = if ($SolutionPath) {
    [System.IO.Path]::GetFullPath($SolutionPath)
} else {
    Join-Path $repoRoot "OpenVisionLab.ThreeDStudio.sln"
}
$SolutionXmlPath = if ($SolutionXmlPath) {
    [System.IO.Path]::GetFullPath($SolutionXmlPath)
} else {
    Join-Path $repoRoot "OpenVisionLab.ThreeDStudio.slnx"
}
$fullReportPath = if ([System.IO.Path]::IsPathRooted($ReportPath)) {
    [System.IO.Path]::GetFullPath($ReportPath)
} else {
    [System.IO.Path]::GetFullPath((Join-Path $repoRoot $ReportPath))
}
$checks = [System.Collections.Generic.List[object]]::new()

function Convert-ToRepoPath([string]$Path)
{
    $fullPath = [System.IO.Path]::GetFullPath($Path)
    if (-not $fullPath.StartsWith($repoRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Path is outside the repository: $fullPath"
    }

    return $fullPath.Substring($repoRoot.Length).TrimStart('\', '/').Replace('\', '/')
}

function Add-Check([string]$Name, [bool]$Passed, [string]$Detail)
{
    $checks.Add([pscustomobject]@{
        Name = $Name
        Passed = $Passed
        Detail = $Detail
    })
}

function Compare-ProjectSet(
    [string]$Name,
    [string[]]$Expected,
    [string[]]$Actual)
{
    $missing = @($Expected | Where-Object { $_ -notin $Actual })
    $extra = @($Actual | Where-Object { $_ -notin $Expected })
    $detail = "expected=$($Expected.Count)|actual=$($Actual.Count)"
    if ($missing.Count -gt 0) {
        $detail += "|missing=$($missing -join ',')"
    }
    if ($extra.Count -gt 0) {
        $detail += "|extra=$($extra -join ',')"
    }
    Add-Check $Name ($missing.Count -eq 0 -and $extra.Count -eq 0) $detail
}

$allProjects = @(
    Get-ChildItem -LiteralPath (Join-Path $repoRoot "src") -Filter "*.csproj" -File -Recurse |
        ForEach-Object { Convert-ToRepoPath $_.FullName } |
        Sort-Object -Unique
)

$classicProjects = @(
    [regex]::Matches(
        [System.IO.File]::ReadAllText($SolutionPath),
        '"(?<path>[^"]+\.csproj)"',
        [System.Text.RegularExpressions.RegexOptions]::IgnoreCase) |
        ForEach-Object {
            Convert-ToRepoPath (Join-Path (Split-Path -Parent $SolutionPath) $_.Groups["path"].Value)
        } |
        Sort-Object -Unique
)

[xml]$solutionXml = [System.IO.File]::ReadAllText($SolutionXmlPath)
$xmlProjects = @(
    $solutionXml.Solution.Project |
        ForEach-Object {
            Convert-ToRepoPath (Join-Path (Split-Path -Parent $SolutionXmlPath) $_.Path)
        } |
        Sort-Object -Unique
)

Compare-ProjectSet "ClassicSolutionProjects" $allProjects $classicProjects
Compare-ProjectSet "XmlSolutionProjects" $allProjects $xmlProjects

$allowedReferences = @{
    "src/OpenVisionLab.ThreeD.Core/OpenVisionLab.ThreeD.Core.csproj" = @()
    "src/OpenVisionLab.ThreeD.Data/OpenVisionLab.ThreeD.Data.csproj" = @(
        "src/OpenVisionLab.ThreeD.Core/OpenVisionLab.ThreeD.Core.csproj"
    )
    "src/OpenVisionLab.ThreeD.Tools/OpenVisionLab.ThreeD.Tools.csproj" = @(
        "src/OpenVisionLab.ThreeD.Core/OpenVisionLab.ThreeD.Core.csproj"
        "src/OpenVisionLab.ThreeD.Data/OpenVisionLab.ThreeD.Data.csproj"
    )
    "src/OpenVisionLab.ThreeD.Runner/OpenVisionLab.ThreeD.Runner.csproj" = @(
        "src/OpenVisionLab.ThreeD.Core/OpenVisionLab.ThreeD.Core.csproj"
        "src/OpenVisionLab.ThreeD.Data/OpenVisionLab.ThreeD.Data.csproj"
        "src/OpenVisionLab.ThreeD.Tools/OpenVisionLab.ThreeD.Tools.csproj"
    )
}

foreach ($projectPath in $allowedReferences.Keys | Sort-Object) {
    $fullProjectPath = Join-Path $repoRoot $projectPath
    [xml]$projectXml = [System.IO.File]::ReadAllText($fullProjectPath)
    $actualReferences = @(
        $projectXml.Project.ItemGroup.ProjectReference |
            Where-Object { $_ -and $_.Include } |
            ForEach-Object {
                Convert-ToRepoPath (Join-Path (Split-Path -Parent $fullProjectPath) $_.Include)
            } |
            Sort-Object -Unique
    )
    Compare-ProjectSet "Dependencies:$projectPath" $allowedReferences[$projectPath] $actualReferences

    $forbiddenPackages = @(
        $projectXml.Project.ItemGroup.PackageReference |
            Where-Object {
                $_ -and $_.Include -match "SharpGL|WPF-UI|AvalonDock|WindowsDesktop"
            } |
            ForEach-Object { $_.Include }
    )
    $usesWpf = @($projectXml.Project.PropertyGroup.UseWPF) -contains "true"
    Add-Check "RuntimeNeutral:$projectPath" (
        -not $usesWpf -and $forbiddenPackages.Count -eq 0
    ) "useWpf=$usesWpf|forbiddenPackages=$($forbiddenPackages -join ',')"
}

$appPath = Join-Path $repoRoot "src/OpenVisionLab.ThreeD.Shell/App.xaml.cs"
$mainWindowPath = Join-Path $repoRoot "src/OpenVisionLab.ThreeD.Shell/MainWindow.xaml.cs"
$runnerProgramPath = Join-Path $repoRoot "src/OpenVisionLab.ThreeD.Runner/Program.cs"
$workbenchViewModelPath = Join-Path $repoRoot "src/OpenVisionLab.ThreeD.Shell/ViewModels/Workbench/ToolWorkbenchViewModel.cs"
$viewerWorkspaceSessionPath = Join-Path $repoRoot "src/OpenVisionLab.ThreeD.Shell/ViewModels/Workbench/ViewerWorkspaceSession.cs"
$workbenchViewPath = Join-Path $repoRoot "src/OpenVisionLab.ThreeD.Shell/Views/Workbench/ToolRecipeWorkbenchView.xaml"
$viewerWorkspaceViewPath = Join-Path $repoRoot "src/OpenVisionLab.ThreeD.Shell/Views/Workbench/ViewerWorkspaceView.xaml"
$thicknessRepeatServicePath = Join-Path $repoRoot "src/OpenVisionLab.ThreeD.Tools/Authoring/ThicknessRepeatGridAuthoringService.cs"
$thicknessRepeatSessionPath = Join-Path $repoRoot "src/OpenVisionLab.ThreeD.Shell/ViewModels/Workbench/ThicknessRepeatGridAuthoringSession.cs"
$thicknessRepeatCompositionPath = Join-Path $repoRoot "src/OpenVisionLab.ThreeD.Shell/ViewModels/Workbench/ToolWorkbenchViewModel.ThicknessRepeatGrid.cs"
$selectedToolWorkspacePath = Join-Path $repoRoot "src/OpenVisionLab.ThreeD.Shell/Views/Workbench/SelectedToolWorkspaceView.xaml"
$teachingCoordinatorPath = Join-Path $repoRoot "src/OpenVisionLab.ThreeD.Shell/Views/Workbench/WorkbenchViewerTeachingCoordinator.cs"
$appSource = [System.IO.File]::ReadAllText($appPath)
$mainWindowSource = [System.IO.File]::ReadAllText($mainWindowPath)
$runnerProgramSource = [System.IO.File]::ReadAllText($runnerProgramPath)
$workbenchViewModelSource = [System.IO.File]::ReadAllText($workbenchViewModelPath)
$workbenchViewSource = [System.IO.File]::ReadAllText($workbenchViewPath)

Add-Check "ShellVerificationRouter" (
    $appSource -match "ShellVerificationCommandRouter\.IsVerificationRequest" -and
    (Test-Path -LiteralPath (Join-Path $repoRoot "src/OpenVisionLab.ThreeD.Shell/Verification/ShellVerificationCommandRouter.cs"))
) "App delegates verification dispatch"
Add-Check "RunnerCommandRouter" (
    $runnerProgramSource -match "RunnerCommandRouter\.Run\(args\)" -and
    (Test-Path -LiteralPath (Join-Path $repoRoot "src/OpenVisionLab.ThreeD.Runner/Application/RunnerCommandRouter.cs"))
) "Program delegates CLI dispatch"
Add-Check "WorkbenchViewerDisplayOwner" (
    $mainWindowSource -match "new WorkbenchViewerDisplayCoordinator\(" -and
    $mainWindowSource -match "_workbenchViewerDisplay\.Dispose\(\)" -and
    (Test-Path -LiteralPath (Join-Path $repoRoot "src/OpenVisionLab.ThreeD.Shell/Views/Workbench/WorkbenchViewerDisplayCoordinator.cs"))
) "MainWindow composes and disposes the display owner"
Add-Check "ViewerWorkspaceCompositionOwner" (
    $workbenchViewModelSource -match "ViewerWorkspace\s*=\s*new ViewerWorkspaceSession\(\)" -and
    $workbenchViewModelSource -match "InitializeViewerWorkspace\(\)" -and
    $workbenchViewSource -match "<workbench:ViewerWorkspaceView" -and
    (Test-Path -LiteralPath $viewerWorkspaceSessionPath) -and
    (Test-Path -LiteralPath $viewerWorkspaceViewPath)
) "Workbench owns the presentation session and the View composes Viewer hosts"
Add-Check "ThicknessRepeatGridAuthoringBoundary" (
    (Test-Path -LiteralPath $thicknessRepeatServicePath) -and
    (Test-Path -LiteralPath $thicknessRepeatSessionPath) -and
    (Test-Path -LiteralPath $thicknessRepeatCompositionPath) -and
    ([System.IO.File]::ReadAllText($thicknessRepeatServicePath) -match "CreateCandidate\(") -and
    -not ([System.IO.File]::ReadAllText($thicknessRepeatServicePath) -match "System\.Windows") -and
    ([System.IO.File]::ReadAllText($thicknessRepeatCompositionPath) -match "ApplyAuthoredDocument\(") -and
    ([System.IO.File]::ReadAllText($selectedToolWorkspacePath) -match "ThicknessRepeatGridPanel") -and
    ([System.IO.File]::ReadAllText($teachingCoordinatorPath) -match "ThicknessRepeatGridPreviewChanged")
) "Tools owns pure translation; Shell owns review/apply; View owns controls and overlay coordination"

$staleDisplayOwner = [regex]::IsMatch(
    $mainWindowSource,
    "_\w+DisplayRequestedHandler|OnWorkbench\w+DisplayRequested")
$staleTransformOwner = @(
    Get-ChildItem -LiteralPath (Join-Path $repoRoot "src/OpenVisionLab.ThreeD.Runner") -Filter "*.cs" -File -Recurse
    Get-ChildItem -LiteralPath (Join-Path $repoRoot "src/OpenVisionLab.ThreeD.Viewer") -Filter "*.cs" -File -Recurse
) | Select-String -Pattern "ApplyModelTransform\s*\("
Add-Check "RemovedShellDisplayOwnership" (-not $staleDisplayOwner) "staleOwner=$staleDisplayOwner"
Add-Check "SharedModelTransformOwnership" (
    $staleTransformOwner.Count -eq 0
) "staleImplementations=$($staleTransformOwner.Count)"

$passedCount = @($checks | Where-Object Passed).Count
$lines = [System.Collections.Generic.List[string]]::new()
$lines.Add("OpenVisionLab 3D code structure verification")
foreach ($check in $checks) {
    $lines.Add("Check|name=$($check.Name)|pass=$($check.Passed)|$($check.Detail)")
}
$lines.Add("Result|pass=$($passedCount -eq $checks.Count)|checks=$passedCount/$($checks.Count)")

New-Item -ItemType Directory -Force -Path (Split-Path -Parent $fullReportPath) | Out-Null
[System.IO.File]::WriteAllLines(
    $fullReportPath,
    $lines,
    [System.Text.UTF8Encoding]::new($false))
$lines | ForEach-Object { Write-Output $_ }

if ($passedCount -ne $checks.Count) {
    exit 1
}
