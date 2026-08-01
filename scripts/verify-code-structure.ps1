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
$surfacePoseAdapterPath = Join-Path $repoRoot "src/OpenVisionLab.ThreeD.Tools/Matching/RigidSurfacePoseSearch.cs"
$surfaceCoverageAdapterPath = Join-Path $repoRoot "src/OpenVisionLab.ThreeD.Tools/Matching/SurfaceCoverageScorer.cs"
$surfaceNoahAdapterPath = Join-Path $repoRoot "src/OpenVisionLab.ThreeD.Tools/Matching/LibraryNoahSurfaceMatching.cs"
$surfaceModelPreparationAdapterPath = Join-Path $repoRoot "src/OpenVisionLab.ThreeD.Tools/Matching/SurfaceModelPreparation.cs"
$preparedSceneAdapterPath = Join-Path $repoRoot "src/OpenVisionLab.ThreeD.Tools/Matching/PreparedScenePreparation.cs"
$modelSurfaceEdgeAdapterPath = Join-Path $repoRoot "src/OpenVisionLab.ThreeD.Tools/Matching/ModelSurfaceEdgeExtractor.cs"
$sceneSurfaceEdgeAdapterPath = Join-Path $repoRoot "src/OpenVisionLab.ThreeD.Tools/Matching/SceneSurfaceEdgeExtractor.cs"
$surfaceEdgeCoverageAdapterPath = Join-Path $repoRoot "src/OpenVisionLab.ThreeD.Tools/Matching/SurfaceAndEdgeMatchScorer.cs"
$removeOutlierAdapterPath = Join-Path $repoRoot "src/OpenVisionLab.ThreeD.Tools/Filtering/C3DRemoveOutlierPixelsRule.cs"
$levelSurfaceAdapterPath = Join-Path $repoRoot "src/OpenVisionLab.ThreeD.Tools/Preparation/C3DLevelSurfaceRule.cs"
$nominalActualAdapterPath = Join-Path $repoRoot "src/OpenVisionLab.ThreeD.Tools/Comparison/NominalActualComparisonExecutor.cs"
$meshDistanceAdapterPath = Join-Path $repoRoot "src/OpenVisionLab.ThreeD.Tools/Comparison/TriangleMeshDistanceIndex.cs"
$registrationAcceptanceAdapterPath = Join-Path $repoRoot "src/OpenVisionLab.ThreeD.Tools/Comparison/RegistrationAcceptanceRule.cs"
$heightGridAdapterPath = Join-Path $repoRoot "src/OpenVisionLab.ThreeD.Data/HeightMaps/C3DHeightGrid.cs"
$heightDistributionAdapterPath = Join-Path $repoRoot "src/OpenVisionLab.ThreeD.Data/HeightMaps/C3DHeightDistribution.cs"
$heightFieldSnapshotAdapterPath = Join-Path $repoRoot "src/OpenVisionLab.ThreeD.Data/HeightMaps/C3DHeightFieldSnapshot.cs"
$sourceQualityAdapterPath = Join-Path $repoRoot "src/OpenVisionLab.ThreeD.Data/HeightMaps/C3DSourceQualityAnalyzer.cs"
$completenessGridAdapterPath = Join-Path $repoRoot "src/OpenVisionLab.ThreeD.Tools/Measurement/C3DCompletenessGridRule.cs"
$heightMeasurementExecutionPath = Join-Path $repoRoot "src/OpenVisionLab.ThreeD.Tools/Measurement/ToolRecipeHeightMeasurementExecution.cs"
$dualSurfaceThicknessAdapterPath = Join-Path $repoRoot "src/OpenVisionLab.ThreeD.Tools/Rules/DualSurfaceThicknessRule.cs"
$heightDeviationAdapterPath = Join-Path $repoRoot "src/OpenVisionLab.ThreeD.Tools/Rules/HeightDeviationRule.cs"
$declaredNormalQualityAdapterPath = Join-Path $repoRoot "src/OpenVisionLab.ThreeD.Data/Quality/ImportedMeshNormalQualityAnalyzer.cs"
$landmarkCorrespondenceAdapterPath = Join-Path $repoRoot "src/OpenVisionLab.ThreeD.Tools/FeatureExtraction/C3DLandmarkCorrespondenceRule.cs"
$noahToolContractPath = Join-Path $repoRoot "docs/OPENVISIONLAB_3D_NOAH_TOOL_CONTRACT_AND_MIGRATION_BASELINE_20260801.md"
$noahToolBaselinePath = Join-Path $repoRoot "docs/OPENVISIONLAB_3D_NOAH_TOOL_MIGRATION_BASELINE_20260801.json"
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
Add-Check "LibraryNoahSurfaceMatchingOwnership" (
    (Test-Path -LiteralPath $surfacePoseAdapterPath) -and
    (Test-Path -LiteralPath $surfaceCoverageAdapterPath) -and
    (Test-Path -LiteralPath $surfaceNoahAdapterPath) -and
    ([System.IO.File]::ReadAllText($surfacePoseAdapterPath) -match "DeterministicRigidSurfacePoseSearchTool") -and
    ([System.IO.File]::ReadAllText($surfaceCoverageAdapterPath) -match "DeterministicSurfaceCoverageTool") -and
    ([System.IO.File]::ReadAllText($surfaceNoahAdapterPath) -match "Lib\.ThreeD\.FeatureExtraction") -and
    -not ([System.IO.File]::ReadAllText($surfacePoseAdapterPath) -match "Math\.|AxisCandidates|Centroid\(|Rotation3|InsideTranslationBounds") -and
    -not ([System.IO.File]::ReadAllText($surfaceCoverageAdapterPath) -match "Math\.|DistanceSquared|claimedSceneSamples")
) "Studio validates identities and maps evidence; vendored Library-Noah owns pose-search and coverage arithmetic"
Add-Check "LibraryNoahSurfacePreparationAndEdgeOwnership" (
    (Test-Path -LiteralPath $surfaceModelPreparationAdapterPath) -and
    (Test-Path -LiteralPath $preparedSceneAdapterPath) -and
    (Test-Path -LiteralPath $modelSurfaceEdgeAdapterPath) -and
    (Test-Path -LiteralPath $sceneSurfaceEdgeAdapterPath) -and
    (Test-Path -LiteralPath $surfaceEdgeCoverageAdapterPath) -and
    ([System.IO.File]::ReadAllText($surfaceModelPreparationAdapterPath) -match "DeterministicSurfaceModelPreparationTool") -and
    ([System.IO.File]::ReadAllText($preparedSceneAdapterPath) -match "DeterministicPreparedScenePreparationTool") -and
    ([System.IO.File]::ReadAllText($modelSurfaceEdgeAdapterPath) -match "DeterministicModelSurfaceEdgeExtractionTool") -and
    ([System.IO.File]::ReadAllText($sceneSurfaceEdgeAdapterPath) -match "DeterministicOrganizedSceneSurfaceEdgeExtractionTool") -and
    ([System.IO.File]::ReadAllText($surfaceEdgeCoverageAdapterPath) -match "DeterministicSurfaceEdgeCoverageTool") -and
    -not ([System.IO.File]::ReadAllText($surfaceModelPreparationAdapterPath) -match "Math\.|Vector3\.Normalize|GetEvenTriangleIndex") -and
    -not ([System.IO.File]::ReadAllText($preparedSceneAdapterPath) -match "Math\.|GetEvenPointIndex") -and
    -not ([System.IO.File]::ReadAllText($modelSurfaceEdgeAdapterPath) -match "Math\.|TriangleNormal|Distance\(|Dot\(|Cross\(") -and
    -not ([System.IO.File]::ReadAllText($sceneSurfaceEdgeAdapterPath) -match "Math\.|AddCandidate|Math\.Abs") -and
    -not ([System.IO.File]::ReadAllText($surfaceEdgeCoverageAdapterPath) -match "Math\.|DistanceSquared|claimedSceneEdges|squaredErrorSum")
) "Studio composes identified artifacts; vendored Library-Noah owns surface preparation, edge extraction, and edge coverage arithmetic"
Add-Check "LibraryNoahOutlierFilteringAndLevelingOwnership" (
    (Test-Path -LiteralPath $removeOutlierAdapterPath) -and
    (Test-Path -LiteralPath $levelSurfaceAdapterPath) -and
    ([System.IO.File]::ReadAllText($removeOutlierAdapterPath) -match "DeterministicLocalMedianOutlierFilterTool") -and
    ([System.IO.File]::ReadAllText($levelSurfaceAdapterPath) -match "LevelSurfaceTool") -and
    -not ([System.IO.File]::ReadAllText($removeOutlierAdapterPath) -match "Math\.|\.Sort\s*\(|Median\s*\(") -and
    -not ([System.IO.File]::ReadAllText($levelSurfaceAdapterPath) -match "Math\.|\.Average\s*\(|\.Sum\s*\(|HeightFieldPlaneFit\.Fit|TransformHeight\s*\(")
) "Studio validates identities and composes evidence; vendored Library-Noah owns local-median filtering and leveling arithmetic"
Add-Check "LibraryNoahNominalComparisonAndTransformDiagnosticsOwnership" (
    (Test-Path -LiteralPath $nominalActualAdapterPath) -and
    (Test-Path -LiteralPath $meshDistanceAdapterPath) -and
    (Test-Path -LiteralPath $registrationAcceptanceAdapterPath) -and
    ([System.IO.File]::ReadAllText($nominalActualAdapterPath) -match "NominalActualMeshComparisonTool") -and
    ([System.IO.File]::ReadAllText($meshDistanceAdapterPath) -match "TriangleMeshDistanceTool") -and
    ([System.IO.File]::ReadAllText($registrationAcceptanceAdapterPath) -match "RigidTransformDiagnosticsTool") -and
    -not ([System.IO.File]::ReadAllText($nominalActualAdapterPath) -match "Math\.|RunningStatistics|FindClosest\(") -and
    -not ([System.IO.File]::ReadAllText($meshDistanceAdapterPath) -match "Math\.|BuildNode|FindClosestPoint|SearchRobustCandidates") -and
    -not ([System.IO.File]::ReadAllText($registrationAcceptanceAdapterPath) -match "Math\.|Vector3d|rotationRows|translationMagnitude\s*=|rotationAngleDegrees\s*=")
) "Studio retains identity, policy, lifecycle, and result composition; vendored Library-Noah owns mesh distance/comparison and transform-diagnostic arithmetic"
Add-Check "LibraryNoahHeightMapInspectionPreparationOwnership" (
    (Test-Path -LiteralPath $heightGridAdapterPath) -and
    (Test-Path -LiteralPath $heightDistributionAdapterPath) -and
    (Test-Path -LiteralPath $heightFieldSnapshotAdapterPath) -and
    (Test-Path -LiteralPath $sourceQualityAdapterPath) -and
    (Test-Path -LiteralPath $completenessGridAdapterPath) -and
    (Test-Path -LiteralPath $heightMeasurementExecutionPath) -and
    ([System.IO.File]::ReadAllText($heightGridAdapterPath) -match "HeightGridSummaryTool") -and
    ([System.IO.File]::ReadAllText($heightFieldSnapshotAdapterPath) -match "HeightDistributionStatisticsTool") -and
    ([System.IO.File]::ReadAllText($sourceQualityAdapterPath) -match "HeightDistributionStatisticsTool") -and
    ([System.IO.File]::ReadAllText($completenessGridAdapterPath) -match "CompletenessGridInspectionTool") -and
    ([System.IO.File]::ReadAllText($heightMeasurementExecutionPath) -match "HeightMapRegionStatisticsTool") -and
    ([System.IO.File]::ReadAllText($heightMeasurementExecutionPath) -match "ReferenceGridPointReconstructionTool") -and
    -not ([System.IO.File]::ReadAllText($heightGridAdapterPath) -match "validCount\+\+|zeroCount\+\+|sum\s*\+=") -and
    -not ([System.IO.File]::ReadAllText($heightDistributionAdapterPath) -match "Math\.|\.Average\s*\(|\.Sum\s*\(") -and
    -not ([System.IO.File]::ReadAllText($heightFieldSnapshotAdapterPath) -match "validCount\+\+|missingCount\+\+|sum\s*\+=") -and
    -not ([System.IO.File]::ReadAllText($sourceQualityAdapterPath) -match "Math\.|\.Average\s*\(|\.Sum\s*\(") -and
    -not ([System.IO.File]::ReadAllText($completenessGridAdapterPath) -match "FiniteValues\s*\(|\.Average\s*\(|finite\.Length\s*/") -and
    -not ([System.IO.File]::ReadAllText($heightMeasurementExecutionPath) -match "profile\.Origin\.[XYZ]\s*\+|sum\s*\+=|\(column\s*\+\s*0\.5d\)|\(row\s*\+\s*0\.5d\)")
) "Studio retains decoding, identity, recipes, hashes, metrics, and presentation; vendored Library-Noah owns height-grid summaries, distributions, ROI statistics, completeness inspection, and reference-grid reconstruction"
Add-Check "LibraryNoahHeightInspectionRuleOwnership" (
    (Test-Path -LiteralPath $dualSurfaceThicknessAdapterPath) -and
    (Test-Path -LiteralPath $heightDeviationAdapterPath) -and
    ([System.IO.File]::ReadAllText($dualSurfaceThicknessAdapterPath) -match "DualSurfaceThicknessInspectionTool") -and
    ([System.IO.File]::ReadAllText($heightDeviationAdapterPath) -match "HeightDeviationInspectionTool") -and
    -not ([System.IO.File]::ReadAllText($dualSurfaceThicknessAdapterPath) -match "Math\.|\.Average\s*\(|\.Min\s*\(|\.Max\s*\(|residual\s*=|below\s*=|above\s*=") -and
    -not ([System.IO.File]::ReadAllText($heightDeviationAdapterPath) -match "Math\.|lowDeviation|highDeviation|peakDeviation\s*=|PeakTolerance\s*\?")
) "Studio retains identity, timing, metrics, overlays, and lifecycle; vendored Library-Noah owns dual-surface residual statistics and height-deviation decisions"
Add-Check "LibraryNoahGeometryQualityOwnership" (
    (Test-Path -LiteralPath $declaredNormalQualityAdapterPath) -and
    (Test-Path -LiteralPath $landmarkCorrespondenceAdapterPath) -and
    ([System.IO.File]::ReadAllText($declaredNormalQualityAdapterPath) -match "DeclaredMeshNormalQualityTool") -and
    ([System.IO.File]::ReadAllText($landmarkCorrespondenceAdapterPath) -match "LandmarkCorrespondenceValidationTool") -and
    -not ([System.IO.File]::ReadAllText($declaredNormalQualityAdapterPath) -match "Math\.|Vector[234]?\.(?:Cross|Dot|Normalize)") -and
    -not ([System.IO.File]::ReadAllText($landmarkCorrespondenceAdapterPath) -match "Math\.|GetAugmentedRank|GetNormalizedTetrahedronVolume|RankRelativeTolerance")
) "Studio retains source/landmark identity, report/artifact policy, hashing, and lifecycle; vendored Library-Noah owns declared-normal geometry and four-point independence arithmetic"

$noahToolContractExists = Test-Path -LiteralPath $noahToolContractPath
$noahToolBaselineExists = Test-Path -LiteralPath $noahToolBaselinePath
$noahToolContractSource = if ($noahToolContractExists) {
    [System.IO.File]::ReadAllText($noahToolContractPath)
} else {
    ""
}
$noahToolBaseline = if ($noahToolBaselineExists) {
    [System.IO.File]::ReadAllText($noahToolBaselinePath) | ConvertFrom-Json
} else {
    $null
}
Add-Check "NoahToolOwnershipContract" (
    $noahToolContractExists -and
    $noahToolBaselineExists -and
    $noahToolContractSource -match 'public,?\s+sealed\s+`XxxTool`' -and
    $noahToolContractSource -match "IThreeDInspectionTool" -and
    $noahToolContractSource -match "decreasing\s+migration baseline"
) "contract=$noahToolContractExists|baseline=$noahToolBaselineExists"

$migrationDebt = @($noahToolBaseline.migrationDebt)
$studioBoundaries = @($noahToolBaseline.studioBoundaries)
$noahInventory = @($migrationDebt + $studioBoundaries)
$inventoryPaths = @($noahInventory | ForEach-Object { [string]$_.path })
$duplicateInventoryPaths = @(
    $inventoryPaths |
        Group-Object |
        Where-Object Count -gt 1 |
        ForEach-Object Name
)
$missingInventoryFiles = @(
    $noahInventory |
        Where-Object { -not (Test-Path -LiteralPath (Join-Path $repoRoot $_.path)) } |
        ForEach-Object path
)
$invalidDebtEntries = @(
    $migrationDebt |
        Where-Object {
            [string]::IsNullOrWhiteSpace([string]$_.targetNoahTool) -or
            [int]$_.maximumSignalCount -lt 0
        } |
        ForEach-Object path
)
$invalidBoundaryEntries = @(
    $studioBoundaries |
        Where-Object {
            [string]::IsNullOrWhiteSpace([string]$_.responsibility) -or
            [int]$_.maximumSignalCount -lt 0
        } |
        ForEach-Object path
)
Add-Check "NoahToolMigrationInventory" (
    $null -ne $noahToolBaseline -and
    [int]$noahToolBaseline.schemaVersion -eq 1 -and
    [string]$noahToolBaseline.contract -eq "docs/OPENVISIONLAB_3D_NOAH_TOOL_CONTRACT_AND_MIGRATION_BASELINE_20260801.md" -and
    $migrationDebt.Count -gt 0 -and
    $duplicateInventoryPaths.Count -eq 0 -and
    $missingInventoryFiles.Count -eq 0 -and
    $invalidDebtEntries.Count -eq 0 -and
    $invalidBoundaryEntries.Count -eq 0
) "debt=$($migrationDebt.Count)|boundaries=$($studioBoundaries.Count)|duplicates=$($duplicateInventoryPaths -join ',')|missing=$($missingInventoryFiles -join ',')|invalidDebt=$($invalidDebtEntries -join ',')|invalidBoundaries=$($invalidBoundaryEntries -join ',')"

$algorithmOwnerPattern = "public\s+(?:static\s+|sealed\s+)?class\s+\w*(?:Rule|Analyzer|Scorer|Extractor|Preparation|Executor|Execution|Index|Builder|Statistics|Filter|Fit|Matcher|Tool)\b"
$numericalSignalPattern = "Math\.|MathF\.|Vector[234]?\.(?:Cross|Dot|Distance|DistanceSquared|Normalize)|\.Average\s*\(|\.Sum\s*\(|\.Sort\s*\("
$noahCandidateRoots = @(
    Join-Path $repoRoot "src/OpenVisionLab.ThreeD.Tools"
    Join-Path $repoRoot "src/OpenVisionLab.ThreeD.Data/Quality"
    Join-Path $repoRoot "src/OpenVisionLab.ThreeD.Data/HeightMaps"
)
$detectedNumericalOwners = @(
    Get-ChildItem -LiteralPath $noahCandidateRoots -Filter "*.cs" -File -Recurse |
        ForEach-Object {
            $source = [System.IO.File]::ReadAllText($_.FullName)
            $signalCount = [regex]::Matches($source, $numericalSignalPattern).Count
            if ($source -match $algorithmOwnerPattern -and $signalCount -gt 0) {
                [pscustomobject]@{
                    Path = Convert-ToRepoPath $_.FullName
                    SignalCount = $signalCount
                }
            }
        }
)
$unexpectedNumericalOwners = @(
    $detectedNumericalOwners |
        Where-Object { $_.Path -notin $inventoryPaths } |
        ForEach-Object { "$($_.Path):$($_.SignalCount)" }
)
$expandedNumericalOwners = @(
    $noahInventory |
        ForEach-Object {
            $baseline = $_
            $source = [System.IO.File]::ReadAllText((Join-Path $repoRoot $baseline.path))
            $signalCount = [regex]::Matches($source, $numericalSignalPattern).Count
            if ($signalCount -gt [int]$baseline.maximumSignalCount) {
                "$($baseline.path):$signalCount>$([int]$baseline.maximumSignalCount)"
            }
        }
)
Add-Check "NoNewStudioNumericalOwnership" (
    $unexpectedNumericalOwners.Count -eq 0 -and
    $expandedNumericalOwners.Count -eq 0
) "detected=$($detectedNumericalOwners.Count)|debtBaseline=$($migrationDebt.Count)|unexpected=$($unexpectedNumericalOwners -join ',')|expanded=$($expandedNumericalOwners -join ',')"

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
