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
    "src/OpenVisionLab.ThreeD.Reporting/OpenVisionLab.ThreeD.Reporting.csproj" = @(
        "src/OpenVisionLab.ThreeD.Core/OpenVisionLab.ThreeD.Core.csproj"
        "src/OpenVisionLab.ThreeD.Data/OpenVisionLab.ThreeD.Data.csproj"
        "src/OpenVisionLab.ThreeD.Tools/OpenVisionLab.ThreeD.Tools.csproj"
    )
    "src/OpenVisionLab.ThreeD.Runner/OpenVisionLab.ThreeD.Runner.csproj" = @(
        "src/OpenVisionLab.ThreeD.Core/OpenVisionLab.ThreeD.Core.csproj"
        "src/OpenVisionLab.ThreeD.Data/OpenVisionLab.ThreeD.Data.csproj"
        "src/OpenVisionLab.ThreeD.Reporting/OpenVisionLab.ThreeD.Reporting.csproj"
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

$presentationReferences = @{
    "src/OpenVisionLab.ThreeD.Presentation/OpenVisionLab.ThreeD.Presentation.csproj" = @()
    "src/OpenVisionLab.ThreeD.Viewer/OpenVisionLab.ThreeD.Viewer.csproj" = @(
        "src/OpenVisionLab.Localization/OpenVisionLab.Localization.csproj"
        "src/OpenVisionLab.ThreeD.Core/OpenVisionLab.ThreeD.Core.csproj"
        "src/OpenVisionLab.ThreeD.Data/OpenVisionLab.ThreeD.Data.csproj"
        "src/OpenVisionLab.ThreeD.Presentation/OpenVisionLab.ThreeD.Presentation.csproj"
        "src/OpenVisionLab.ThreeD.Tools/OpenVisionLab.ThreeD.Tools.csproj"
    )
    "src/OpenVisionLab.ThreeD.Shell/OpenVisionLab.ThreeD.Shell.csproj" = @(
        "src/OpenVisionLab.Localization/OpenVisionLab.Localization.csproj"
        "src/OpenVisionLab.Logging.Controls/OpenVisionLab.Logging.Controls.csproj"
        "src/OpenVisionLab.Logging/OpenVisionLab.Logging.csproj"
        "src/OpenVisionLab.ThreeD.Core/OpenVisionLab.ThreeD.Core.csproj"
        "src/OpenVisionLab.ThreeD.Data/OpenVisionLab.ThreeD.Data.csproj"
        "src/OpenVisionLab.ThreeD.Docking.Controls/OpenVisionLab.ThreeD.Docking.Controls.csproj"
        "src/OpenVisionLab.ThreeD.Presentation/OpenVisionLab.ThreeD.Presentation.csproj"
        "src/OpenVisionLab.ThreeD.Reporting/OpenVisionLab.ThreeD.Reporting.csproj"
        "src/OpenVisionLab.ThreeD.Tools/OpenVisionLab.ThreeD.Tools.csproj"
        "src/OpenVisionLab.ThreeD.Viewer/OpenVisionLab.ThreeD.Viewer.csproj"
        "src/OpenVisionLab.Wpf.MessageDialogs/OpenVisionLab.Wpf.MessageDialogs.csproj"
    )
}

foreach ($projectPath in $presentationReferences.Keys | Sort-Object) {
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
    Compare-ProjectSet "Dependencies:$projectPath" $presentationReferences[$projectPath] $actualReferences
}

$presentationCommandPath = Join-Path $repoRoot "src/OpenVisionLab.ThreeD.Presentation/Commands/RelayCommand.cs"
$shellGlobalUsingsPath = Join-Path $repoRoot "src/OpenVisionLab.ThreeD.Shell/GlobalUsings.cs"
$viewerCompatibilityCommandPath = Join-Path $repoRoot "src/OpenVisionLab.ThreeD.Viewer/Presentation/Commands/RelayCommand.cs"
$presentationCommandSource = if (Test-Path -LiteralPath $presentationCommandPath) {
    [System.IO.File]::ReadAllText($presentationCommandPath)
} else {
    ""
}
$shellGlobalUsingsSource = if (Test-Path -LiteralPath $shellGlobalUsingsPath) {
    [System.IO.File]::ReadAllText($shellGlobalUsingsPath)
} else {
    ""
}
$viewerCompatibilityCommandSource = if (Test-Path -LiteralPath $viewerCompatibilityCommandPath) {
    [System.IO.File]::ReadAllText($viewerCompatibilityCommandPath)
} else {
    ""
}
Add-Check "SharedPresentationCommandOwnership" (
    $presentationCommandSource -match "public sealed class RelayCommand" -and
    $shellGlobalUsingsSource -match "OpenVisionLab\.ThreeD\.Presentation\.Commands\.RelayCommand" -and
    $viewerCompatibilityCommandSource -match "PresentationRelayCommand" -and
    $viewerCompatibilityCommandSource -notmatch "private readonly Func<object\?, bool>\? canExecute"
) "Shell uses Presentation command owner; Viewer retains a delegating public compatibility surface"

$orderedRunRecordFactoryPath = Join-Path $repoRoot "src/OpenVisionLab.ThreeD.Reporting/RunRecords/OrderedRunRecordFactory.cs"
$runRecordJsonPath = Join-Path $repoRoot "src/OpenVisionLab.ThreeD.Reporting/RunRecords/InspectionRunRecordJson.cs"
$shellOrderedRunRecordWriterPath = Join-Path $repoRoot "src/OpenVisionLab.ThreeD.Shell/ViewModels/Shell/ShellOrderedRunRecordWriter.cs"
$runnerRunRecordWriterPath = Join-Path $repoRoot "src/OpenVisionLab.ThreeD.Runner/Reporting/RunRecordWriter.cs"
$shellOrderedRunRecordWriterSource = if (Test-Path -LiteralPath $shellOrderedRunRecordWriterPath) {
    [System.IO.File]::ReadAllText($shellOrderedRunRecordWriterPath)
} else {
    ""
}
$runnerRunRecordWriterSource = if (Test-Path -LiteralPath $runnerRunRecordWriterPath) {
    [System.IO.File]::ReadAllText($runnerRunRecordWriterPath)
} else {
    ""
}
Add-Check "SharedOrderedRunRecordOwnership" (
    (Test-Path -LiteralPath $orderedRunRecordFactoryPath) -and
    (Test-Path -LiteralPath $runRecordJsonPath) -and
    $shellOrderedRunRecordWriterSource -match "OrderedRunRecordFactory\.Create" -and
    $shellOrderedRunRecordWriterSource -match "InspectionRunRecordJson\.Write" -and
    $shellOrderedRunRecordWriterSource -notmatch "new InspectionRunRecord\(" -and
    $runnerRunRecordWriterSource -match "OrderedRunRecordFactory\.Create" -and
    $runnerRunRecordWriterSource -match "InspectionRunRecordJson\.Write"
) "Reporting owns ordered graph record composition and shared JSON output; Shell and Runner retain route-specific artifact policy"

$appPath = Join-Path $repoRoot "src/OpenVisionLab.ThreeD.Shell/App.xaml.cs"
$mainWindowPath = Join-Path $repoRoot "src/OpenVisionLab.ThreeD.Shell/MainWindow.xaml.cs"
$studioLayoutControllerPath = Join-Path $repoRoot "src/OpenVisionLab.ThreeD.Shell/Layout/StudioLayoutController.cs"
$shellRequestCoordinatorPath = Join-Path $repoRoot "src/OpenVisionLab.ThreeD.Shell/Coordination/ShellRequestCoordinator.cs"
$shellEvidenceDialogControllerPath = Join-Path $repoRoot "src/OpenVisionLab.ThreeD.Shell/Dialogs/ShellEvidenceDialogController.cs"
$recipeFileDialogServicePath = Join-Path $repoRoot "src/OpenVisionLab.ThreeD.Shell/Dialogs/RecipeFileDialogService.cs"
$shellWorkbenchLifecycleControllerPath = Join-Path $repoRoot "src/OpenVisionLab.ThreeD.Shell/Coordination/ShellWorkbenchLifecycleController.cs"
$mainWindowSource = if (Test-Path -LiteralPath $mainWindowPath) {
    [System.IO.File]::ReadAllText($mainWindowPath)
} else {
    ""
}
$studioLayoutControllerSource = if (Test-Path -LiteralPath $studioLayoutControllerPath) {
    [System.IO.File]::ReadAllText($studioLayoutControllerPath)
} else {
    ""
}
$shellRequestCoordinatorSource = if (Test-Path -LiteralPath $shellRequestCoordinatorPath) {
    [System.IO.File]::ReadAllText($shellRequestCoordinatorPath)
} else {
    ""
}
$shellEvidenceDialogControllerSource = if (Test-Path -LiteralPath $shellEvidenceDialogControllerPath) {
    [System.IO.File]::ReadAllText($shellEvidenceDialogControllerPath)
} else {
    ""
}
$recipeFileDialogServiceSource = if (Test-Path -LiteralPath $recipeFileDialogServicePath) {
    [System.IO.File]::ReadAllText($recipeFileDialogServicePath)
} else {
    ""
}
$shellWorkbenchLifecycleControllerSource = if (Test-Path -LiteralPath $shellWorkbenchLifecycleControllerPath) {
    [System.IO.File]::ReadAllText($shellWorkbenchLifecycleControllerPath)
} else {
    ""
}
Add-Check "StudioLayoutControllerOwnership" (
    $studioLayoutControllerSource -match "internal sealed class StudioLayoutController" -and
    $studioLayoutControllerSource -match "StudioLayoutProfileStore" -and
    $studioLayoutControllerSource -match "public void Save\(\)" -and
    $studioLayoutControllerSource -match "public void Reset\(\)" -and
    $mainWindowSource -match "new StudioLayoutController\(" -and
    $mainWindowSource -match "_studioLayout\.Save\(\)" -and
    $mainWindowSource -match "_studioLayout\.Reset\(\)" -and
    $mainWindowSource -notmatch "StudioLayoutProfileStore\? _studioLayoutStore" -and
    $mainWindowSource -notmatch "ConfigureStudioLayoutPersistence" -and
    $mainWindowSource -notmatch "SaveStudioLayout\("
) "StudioLayoutController owns layout load/save/reset; MainWindow retains only lifecycle delegation"
Add-Check "ShellRequestCoordinatorOwnership" (
    $shellRequestCoordinatorSource -match "internal sealed class ShellRequestCoordinator" -and
    $shellRequestCoordinatorSource -match "ProfileViewRequested \+= callbacks\.ProfileView" -and
    $shellRequestCoordinatorSource -match "ValidationSetComparisonRequested -= callbacks\.ValidationSetComparison" -and
    $mainWindowSource -match "new ShellRequestCoordinator\(" -and
    $mainWindowSource -match "_requestCoordinator\.Dispose\(\)" -and
    $mainWindowSource -notmatch "_profileViewRequestedHandler" -and
    $mainWindowSource -notmatch "_workbenchValidationSetComparisonRequestedHandler"
) "ShellRequestCoordinator owns presentation-request subscription lifetime; MainWindow supplies explicit WPF callbacks"
Add-Check "ShellEvidenceDialogOwnership" (
    $shellEvidenceDialogControllerSource -match "internal sealed class ShellEvidenceDialogController" -and
    $shellEvidenceDialogControllerSource -match "OpenFileDialog" -and
    $shellEvidenceDialogControllerSource -match "OpenFolderDialog" -and
    $shellEvidenceDialogControllerSource -match "ExportPrivacySafeSupportBundle" -and
    $mainWindowSource -match "new ShellEvidenceDialogController\(" -and
    $mainWindowSource -match "_evidenceDialogs\.OpenEvidenceArtifact" -and
    $mainWindowSource -notmatch "OnOpenEvidenceArtifactRequested" -and
    $mainWindowSource -notmatch "OnExportPrivacySafeSupportBundleRequested"
) "ShellEvidenceDialogController owns evidence and Run Record dialog adapters; MainWindow only composes callbacks"
Add-Check "RecipeFileDialogOwnership" (
    $recipeFileDialogServiceSource -match "internal sealed class RecipeFileDialogService" -and
    $recipeFileDialogServiceSource -match "TrySelectSavePath" -and
    $recipeFileDialogServiceSource -match "TrySelectOpenPath" -and
    $mainWindowSource -match "new RecipeFileDialogService\(" -and
    $shellWorkbenchLifecycleControllerSource -match "_recipeFileDialogs\.TrySelectSavePath" -and
    $shellWorkbenchLifecycleControllerSource -match "_recipeFileDialogs\.TrySelectOpenPath" -and
    $mainWindowSource -notmatch "Save 3D Inspection Recipe As" -and
    $mainWindowSource -notmatch "Open 3D Inspection Recipe"
) "RecipeFileDialogService owns recipe Save/Open selection; MainWindow retains lifecycle policy"
Add-Check "ShellWorkbenchLifecycleOwnership" (
    $shellWorkbenchLifecycleControllerSource -match "internal sealed class ShellWorkbenchLifecycleController" -and
    $shellWorkbenchLifecycleControllerSource -match "LoadWorkbenchC3DSourceAsync" -and
    $shellWorkbenchLifecycleControllerSource -match "OpenWorkbenchRecipe" -and
    $shellWorkbenchLifecycleControllerSource -match "TryResolveWorkbenchChanges" -and
    $shellWorkbenchLifecycleControllerSource -match "ClickUnsavedRecipeDoNotSaveForSmokeAsync" -and
    $mainWindowSource -match "new ShellWorkbenchLifecycleController\(" -and
    $mainWindowSource -match "_workbenchLifecycle\.Dispose\(\)" -and
    $mainWindowSource -notmatch "RecipeManagerWindow\? recipeManagerWindow" -and
    $mainWindowSource -notmatch "CancellationTokenSource\? c3dSourceLoadCancellation" -and
    $mainWindowSource -notmatch "double lastWorkbenchSourceBindingMilliseconds"
) "ShellWorkbenchLifecycleController owns recipe/source lifecycle state and smoke hooks; MainWindow retains composition wrappers"
$runnerProgramPath = Join-Path $repoRoot "src/OpenVisionLab.ThreeD.Runner/Program.cs"
$workbenchViewModelPath = Join-Path $repoRoot "src/OpenVisionLab.ThreeD.Shell/ViewModels/Workbench/ToolWorkbenchViewModel.cs"
$workbenchViewModelDirectory = Split-Path -Parent $workbenchViewModelPath
$viewerWorkspaceSessionPath = Join-Path $repoRoot "src/OpenVisionLab.ThreeD.Shell/ViewModels/Workbench/ViewerWorkspaceSession.cs"
$workbenchRecipeSessionPath = Join-Path $repoRoot "src/OpenVisionLab.ThreeD.Shell/ViewModels/Workbench/ToolWorkbenchRecipeSession.cs"
$workbenchSourceSessionPath = Join-Path $repoRoot "src/OpenVisionLab.ThreeD.Shell/ViewModels/Workbench/ToolWorkbenchSourceSession.cs"
$workbenchViewPath = Join-Path $repoRoot "src/OpenVisionLab.ThreeD.Shell/Views/Workbench/ToolRecipeWorkbenchView.xaml"
$viewerWorkspaceViewPath = Join-Path $repoRoot "src/OpenVisionLab.ThreeD.Shell/Views/Workbench/ViewerWorkspaceView.xaml"
$thicknessRepeatServicePath = Join-Path $repoRoot "src/OpenVisionLab.ThreeD.Tools/Authoring/ThicknessRepeatGridAuthoringService.cs"
$thicknessRepeatSessionPath = Join-Path $repoRoot "src/OpenVisionLab.ThreeD.Shell/ViewModels/Workbench/ThicknessRepeatGridAuthoringSession.cs"
$thicknessRepeatCompositionPath = Join-Path $repoRoot "src/OpenVisionLab.ThreeD.Shell/ViewModels/Workbench/ToolWorkbenchViewModel.ThicknessRepeatGrid.cs"
$selectedToolWorkspacePath = Join-Path $repoRoot "src/OpenVisionLab.ThreeD.Shell/Views/Workbench/SelectedToolWorkspaceView.xaml"
$teachingCoordinatorPath = Join-Path $repoRoot "src/OpenVisionLab.ThreeD.Shell/Views/Workbench/WorkbenchViewerTeachingCoordinator.cs"
$surfacePoseAdapterPath = Join-Path $repoRoot "src/OpenVisionLab.ThreeD.Tools/Matching/RigidSurfacePoseSearch.cs"
$surfaceCoverageAdapterPath = Join-Path $repoRoot "src/OpenVisionLab.ThreeD.Tools/Matching/SurfaceCoverageScorer.cs"
$surfaceSdkAdapterPath = Join-Path $repoRoot "src/OpenVisionLab.ThreeD.Tools/Matching/VisionSdkSurfaceMatching.cs"
$multipleSurfaceMatchAdapterPath = Join-Path $repoRoot "src/OpenVisionLab.ThreeD.Tools/Matching/MultipleSurfaceMatchEvaluationExecutor.cs"
$surfacePoseEquivalenceAdapterPath = Join-Path $repoRoot "src/OpenVisionLab.ThreeD.Tools/Matching/SurfaceMatchPoseEquivalenceEvaluator.cs"
$surfacePoseEquivalenceContractPath = Join-Path $repoRoot "src/OpenVisionLab.ThreeD.Core/Contracts/Matching/SurfaceMatchPoseEquivalenceEvaluation.cs"
$surfaceModelPreparationAdapterPath = Join-Path $repoRoot "src/OpenVisionLab.ThreeD.Tools/Matching/SurfaceModelPreparation.cs"
$modelKeyPointAdapterPath = Join-Path $repoRoot "src/OpenVisionLab.ThreeD.Tools/Matching/ModelKeyPointExtractor.cs"
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
$alignedPointRepeatabilityAdapterPath = Join-Path $repoRoot "src/OpenVisionLab.ThreeD.Tools/Calibration/AlignedPointRepeatabilityRule.cs"
$thicknessRepeatabilityAdapterPath = Join-Path $repoRoot "src/OpenVisionLab.ThreeD.Tools/Calibration/ThicknessRepeatabilityRule.cs"
$labeledEvidenceStatisticsAdapterPath = Join-Path $repoRoot "src/OpenVisionLab.ThreeD.Tools/Validation/ToolRecipeLabeledEvidenceAnalyzer.cs"
$thresholdCandidateAnalysisAdapterPath = Join-Path $repoRoot "src/OpenVisionLab.ThreeD.Tools/Validation/ToolRecipeThresholdCandidateAnalyzer.cs"
$visionSdkToolContractPath = Join-Path $repoRoot "docs/OPENVISIONLAB_3D_VISION_SDK_TOOL_CONTRACT_AND_MIGRATION_BASELINE_20260805.md"
$visionSdkToolBaselinePath = Join-Path $repoRoot "docs/OPENVISIONLAB_3D_VISION_SDK_TOOL_MIGRATION_BASELINE_20260805.json"
$appSource = [System.IO.File]::ReadAllText($appPath)
$mainWindowSource = [System.IO.File]::ReadAllText($mainWindowPath)
$runnerProgramSource = [System.IO.File]::ReadAllText($runnerProgramPath)
$workbenchViewModelSource = [System.IO.File]::ReadAllText($workbenchViewModelPath)
$workbenchViewModelFamilySource = @(
    Get-ChildItem -LiteralPath $workbenchViewModelDirectory -Filter "ToolWorkbenchViewModel*.cs" -File -Recurse |
        ForEach-Object { [System.IO.File]::ReadAllText($_.FullName) }
) -join "`n"
$viewerWorkspaceSessionSource = [System.IO.File]::ReadAllText($viewerWorkspaceSessionPath)
$workbenchViewerWorkspaceSource = [System.IO.File]::ReadAllText((Join-Path $repoRoot "src/OpenVisionLab.ThreeD.Shell/ViewModels/Workbench/ToolWorkbenchViewModel.ViewerWorkspace.cs"))
$workbenchViewSource = [System.IO.File]::ReadAllText($workbenchViewPath)
$scrollIntoViewBehaviorPath = Join-Path $repoRoot "src/OpenVisionLab.ThreeD.Shell/Behaviors/ScrollIntoViewOnSelectionChangedBehavior.cs"
$recipeChainViewSource = [System.IO.File]::ReadAllText((Join-Path $repoRoot "src/OpenVisionLab.ThreeD.Shell/Views/Workbench/RecipeChainView.xaml"))
$recipePipelineReviewViewSource = [System.IO.File]::ReadAllText((Join-Path $repoRoot "src/OpenVisionLab.ThreeD.Shell/Views/Workbench/RecipePipelineReviewView.xaml"))
$recipeChainCodeBehindSource = [System.IO.File]::ReadAllText((Join-Path $repoRoot "src/OpenVisionLab.ThreeD.Shell/Views/Workbench/RecipeChainView.xaml.cs"))
$recipePipelineReviewCodeBehindSource = [System.IO.File]::ReadAllText((Join-Path $repoRoot "src/OpenVisionLab.ThreeD.Shell/Views/Workbench/RecipePipelineReviewView.xaml.cs"))
$resultsWorkspaceViewSource = [System.IO.File]::ReadAllText((Join-Path $repoRoot "src/OpenVisionLab.ThreeD.Shell/Views/Workbench/ResultsWorkspaceView.xaml"))
$resultsWorkspaceCodeBehindSource = [System.IO.File]::ReadAllText((Join-Path $repoRoot "src/OpenVisionLab.ThreeD.Shell/Views/Workbench/ResultsWorkspaceView.xaml.cs"))
$workspaceNavigationViewModelsSource = [System.IO.File]::ReadAllText((Join-Path $repoRoot "src/OpenVisionLab.ThreeD.Shell/ViewModels/Workbench/WorkspaceNavigationViewModels.cs"))
$mainWindowSmokeSourceQualitySource = $mainWindowSource
$shellSourceQualitySmokeSource = [System.IO.File]::ReadAllText((Join-Path $repoRoot "src/OpenVisionLab.ThreeD.Shell/Verification/Smoke/ShellSourceQualitySmoke.cs"))
$viewerDisplaySettingsSource = [System.IO.File]::ReadAllText((Join-Path $repoRoot "src/OpenVisionLab.ThreeD.Viewer/ViewModels/ViewerDisplaySettingsViewModel.cs"))
$viewerCameraSessionSource = [System.IO.File]::ReadAllText((Join-Path $repoRoot "src/OpenVisionLab.ThreeD.Viewer/ViewModels/ViewerCameraSession.cs"))
$viewerSelectionSessionSource = [System.IO.File]::ReadAllText((Join-Path $repoRoot "src/OpenVisionLab.ThreeD.Viewer/ViewModels/ViewerSelectionSession.cs"))
$viewerRootViewModelSource = [System.IO.File]::ReadAllText((Join-Path $repoRoot "src/OpenVisionLab.ThreeD.Viewer/ViewModels/MainWindowViewModel.cs"))
$viewerSceneViewModelSource = [System.IO.File]::ReadAllText((Join-Path $repoRoot "src/OpenVisionLab.ThreeD.Viewer/ViewModels/MainWindowViewModel.Scene.cs"))
$workbenchTeachingCaptureSessionSource = [System.IO.File]::ReadAllText((Join-Path $repoRoot "src/OpenVisionLab.ThreeD.Shell/ViewModels/Workbench/ToolWorkbenchTeachingCaptureSession.cs"))
$workbenchRecipeSessionSource = [System.IO.File]::ReadAllText($workbenchRecipeSessionPath)
$workbenchSourceSessionSource = [System.IO.File]::ReadAllText($workbenchSourceSessionPath)
$workbenchRootViewModelSource = [System.IO.File]::ReadAllText((Join-Path $repoRoot "src/OpenVisionLab.ThreeD.Shell/ViewModels/Workbench/ToolWorkbenchViewModel.cs"))
$viewerRecipeViewModelSource = [System.IO.File]::ReadAllText((Join-Path $repoRoot "src/OpenVisionLab.ThreeD.Viewer/ViewModels/MainWindowViewModel.Recipes.cs"))
$viewerRecipeRecipesSource = [System.IO.File]::ReadAllText((Join-Path $repoRoot "src/OpenVisionLab.ThreeD.Viewer/Recipes/HeightDeviationRecipeLoadPlan.cs"))
$viewerRecipeApplyCoordinatorSource = [System.IO.File]::ReadAllText((Join-Path $repoRoot "src/OpenVisionLab.ThreeD.Viewer/Recipes/HeightDeviationRecipeApplyCoordinator.cs"))
$viewerRecipeSaveCoordinatorSource = [System.IO.File]::ReadAllText((Join-Path $repoRoot "src/OpenVisionLab.ThreeD.Viewer/Recipes/HeightDeviationRecipeSaveCoordinator.cs"))
$viewerRecipeViewSource = [System.IO.File]::ReadAllText((Join-Path $repoRoot "src/OpenVisionLab.ThreeD.Viewer/Views/OpenVisionThreeDViewerControl.Recipes.cs"))
$viewerHeightDeviationApplySource = [System.Text.RegularExpressions.Regex]::Match(
    $viewerRecipeViewSource,
    "private bool ApplyHeightDeviationRecipe[\s\S]*?(?=private bool ApplyC3DThicknessRecipe)").Value
$viewerHeightDeviationSaveSource = [System.Text.RegularExpressions.Regex]::Match(
    $viewerRecipeViewSource,
    "private bool SaveCurrentHeightDeviationRecipe[\s\S]*?(?=private bool SaveCurrentLazTwoPointRecipe)").Value

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
Add-Check "ViewerWorkspaceStateOwner" (
    $viewerWorkspaceSessionSource -match "public bool TrySetLayout\(" -and
    $viewerWorkspaceSessionSource -match "public bool TryOpenAuxiliaryContent\(" -and
    $workbenchViewerWorkspaceSource -match "ViewerWorkspace\.TrySetLayout\(" -and
    $workbenchViewerWorkspaceSource -match "ViewerWorkspace\.TryOpenAuxiliaryContent\(" -and
    $workbenchViewerWorkspaceSource -notmatch "ViewerWorkspace\.SetLayout\(" -and
    $workbenchViewerWorkspaceSource -notmatch "ViewerWorkspace\.FocusSlot\(ViewerWorkspaceSession\.MainSlotId\)"
) "ViewerWorkspaceSession owns layout and auxiliary-slot transitions; Workbench maps candidates and selection state"
Add-Check "ViewerDisplayStateOwner" (
    $viewerDisplaySettingsSource -match "public double PointSize" -and
    $viewerDisplaySettingsSource -match "public string SelectedRenderDensity" -and
    $viewerDisplaySettingsSource -match "public int C3DMaxRenderedPoints" -and
    $viewerDisplaySettingsSource -match "private static string FormatRenderDensitySummary" -and
    $viewerRootViewModelSource -match "get => Display\.PointSize" -and
    $viewerRootViewModelSource -match "get => Display\.SelectedRenderDensity" -and
    $viewerRootViewModelSource -notmatch "private double pointSize" -and
    $viewerRootViewModelSource -notmatch "private string selectedRenderDensity" -and
    $viewerRootViewModelSource -notmatch "private string renderDensitySummary" -and
    $viewerRecipeViewModelSource -notmatch "FormatRenderDensitySummary"
) "ViewerDisplaySettingsViewModel owns point size, render-density budgets, summary, and revision; MainWindowViewModel retains compatibility bindings"
Add-Check "ViewerCameraStateOwner" (
    $viewerCameraSessionSource -match "internal sealed class ViewerCameraSession" -and
    $viewerCameraSessionSource -match "public void SavePerspective\(\)" -and
    $viewerCameraSessionSource -match "public bool TryGetSavedPerspective" -and
    $viewerRootViewModelSource -match "internal ViewerCameraSession CameraSession \{ get; \} = new\(\);" -and
    $viewerRootViewModelSource -notmatch "private double cameraTargetX" -and
    $viewerRootViewModelSource -notmatch "private ViewerProjectionMode projectionMode" -and
    $viewerRootViewModelSource -notmatch "private double savedPerspectiveYaw" -and
    $viewerRootViewModelSource -notmatch "private bool hasSavedPerspectiveCamera" -and
    $viewerSceneViewModelSource -match "CameraSession\.SavePerspective\(\)" -and
    $viewerSceneViewModelSource -match "CameraSession\.TryGetSavedPerspective" -and
    $viewerSceneViewModelSource -notmatch "private void SavePerspectiveCamera\(\)"
) "ViewerCameraSession owns camera/projection state and saved-perspective lifetime; MainWindowViewModel retains compatibility bindings"
Add-Check "ViewerSelectionStateOwner" (
    $viewerSelectionSessionSource -match "internal sealed class ViewerSelectionSession" -and
    $viewerSelectionSessionSource -match "public string SelectedEntity" -and
    $viewerSelectionSessionSource -match "public string PickCoordinate" -and
    $viewerSelectionSessionSource -match "public string SelectedMode" -and
    $viewerSelectionSessionSource -match "public string Summary" -and
    $viewerSelectionSessionSource -match "public bool OverlayVisible" -and
    $viewerRootViewModelSource -match "internal ViewerSelectionSession SelectionSession \{ get; \} = new\(\);" -and
    $viewerRootViewModelSource -notmatch "private string selectedEntity" -and
    $viewerRootViewModelSource -notmatch "private string pickCoordinate" -and
    $viewerRootViewModelSource -notmatch "private string selectedSelectionMode" -and
    $viewerRootViewModelSource -notmatch "private string selectionSummary" -and
    $viewerRootViewModelSource -notmatch "private bool selectionOverlayVisible"
) "ViewerSelectionSession owns selection mode, entity, pick, summary, and overlay state; MainWindowViewModel retains policy and compatibility bindings"
Add-Check "WorkbenchTeachingCaptureStateOwner" (
    $workbenchTeachingCaptureSessionSource -match "internal sealed class ToolWorkbenchTeachingCaptureSession" -and
    $workbenchTeachingCaptureSessionSource -match "public void SetState\(" -and
    $workbenchTeachingCaptureSessionSource -match "public ToolRecipeGridRectangle GridRectangleDraft" -and
    $workbenchTeachingCaptureSessionSource -match "public void SetGridRectangleDraft\(" -and
    $workbenchTeachingCaptureSessionSource -match "public void Clear\(\)" -and
    $workbenchRootViewModelSource -match "internal ToolWorkbenchTeachingCaptureSession TeachingCaptureSession \{ get; \} = new\(\);" -and
    $workbenchRootViewModelSource -match "TeachingCaptureSession\.SetState\(" -and
    $workbenchRootViewModelSource -match "TeachingCaptureSession\.Clear\(\)" -and
    $workbenchRootViewModelSource -notmatch "private bool isTeachingSelectionCaptureActive" -and
    $workbenchRootViewModelSource -notmatch "private string\? teachingSelectionCaptureStepId" -and
    $workbenchRootViewModelSource -notmatch "private int teachingSelectionCapturedPointCount" -and
    $workbenchRootViewModelSource -notmatch "private bool canApplyTeachingSelectionCapture" -and
    $workbenchRootViewModelSource -notmatch "private bool captureAdditionalLevelSurfaceReference" -and
    $workbenchRootViewModelSource -notmatch "private int teachingGridRectangleRow" -and
    $workbenchRootViewModelSource -notmatch "private int teachingGridRectangleColumn" -and
    $workbenchRootViewModelSource -notmatch "private int teachingGridRectangleRowCount" -and
    $workbenchRootViewModelSource -notmatch "private int teachingGridRectangleColumnCount"
) "ToolWorkbenchTeachingCaptureSession owns transient capture lifetime, progress, and ROI draft data; ToolWorkbenchViewModel retains recipe policy, validation, notifications, and Viewer coordination"
Add-Check "WorkbenchRecipeStateOwner" (
    $workbenchRecipeSessionSource -match "internal sealed class ToolWorkbenchRecipeSession" -and
    $workbenchRecipeSessionSource -match "public bool SetSchemaVersion\(" -and
    $workbenchRecipeSessionSource -match "public bool SetName\(" -and
    $workbenchRecipeSessionSource -match "public bool SetPath\(" -and
    $workbenchRecipeSessionSource -match "public bool SetDirty\(" -and
    $workbenchRecipeSessionSource -match "public void SetValidation\(" -and
    $workbenchRootViewModelSource -match "internal ToolWorkbenchRecipeSession RecipeSession \{ get; \} = new\(\);" -and
    $workbenchRootViewModelSource -match "RecipeSession\.SetValidation\(" -and
    $workbenchRootViewModelSource -notmatch "private string recipeSchemaVersion" -and
    $workbenchRootViewModelSource -notmatch "private string recipeName" -and
    $workbenchRootViewModelSource -notmatch "private string\? recipePath" -and
    $workbenchRootViewModelSource -notmatch "private bool isDirty" -and
    $workbenchRootViewModelSource -notmatch "private ToolRecipeValidationResult validation" -and
    $workbenchRootViewModelSource -notmatch "private ToolRecipeValidationResult storageValidation" -and
    $workbenchRootViewModelSource -notmatch "private IReadOnlyList<string> sourceBindingErrors"
) "ToolWorkbenchRecipeSession owns recipe identity, path, dirty state, and validation results; ToolWorkbenchViewModel retains normalization, persistence, execution invalidation, and notifications"
Add-Check "WorkbenchSourceStateOwner" (
    $workbenchSourceSessionSource -match "internal sealed class ToolWorkbenchSourceSession" -and
    $workbenchSourceSessionSource -match "public ToolRecipeSelectionSourceBinding\? SourceBinding" -and
    $workbenchSourceSessionSource -match "public ToolRecipeAcquisitionProvenance\? SourceAcquisitionProvenance" -and
    $workbenchSourceSessionSource -match "public ToolRecipeSource\? OpenedSourceIdentity" -and
    $workbenchSourceSessionSource -match "public IReadOnlyList<string> SourceIdentityErrors" -and
    $workbenchSourceSessionSource -match "public bool SetSourceBinding" -and
    $workbenchSourceSessionSource -match "public bool SetSourceAcquisitionProvenance" -and
    $workbenchSourceSessionSource -match "public void CaptureOpenedSourceIdentity" -and
    $workbenchSourceSessionSource -match "public bool SetSourceIdentityErrors" -and
    $workbenchRootViewModelSource -match "internal ToolWorkbenchSourceSession SourceSession \{ get; \} = new\(\);" -and
    $workbenchViewModelFamilySource -match "SourceSession\.SetSourceBinding\(" -and
    $workbenchViewModelFamilySource -match "SourceSession\.SetSourceAcquisitionProvenance\(" -and
    $workbenchViewModelFamilySource -match "SourceSession\.SetSourceIdentityErrors\(" -and
    $workbenchRootViewModelSource -notmatch "private ToolRecipeSelectionSourceBinding\? loadedSourceBinding" -and
    $workbenchRootViewModelSource -notmatch "private ToolRecipeAcquisitionProvenance\? sourceAcquisitionProvenance" -and
    $workbenchRootViewModelSource -notmatch "private ToolRecipeSource\? openedSourceIdentity" -and
    $workbenchRootViewModelSource -notmatch "private IReadOnlyList<string> sourceIdentityErrors" -and
    $workbenchRootViewModelSource -notmatch "private ToolRecipeSelectionSourceBinding\\?\\s*SourceSession\\.SourceBinding"
) "ToolWorkbenchSourceSession owns loaded source identity, provenance, opened-source snapshot, and source-identity errors while ToolWorkbenchViewModel retains runtime policy"
Add-Check "ScrollIntoViewBehaviorBoundary" (
    (Test-Path -LiteralPath $scrollIntoViewBehaviorPath) -and
    ([System.IO.File]::ReadAllText($scrollIntoViewBehaviorPath) -match "DependencyProperty.RegisterAttached") -and
    ([System.IO.File]::ReadAllText($scrollIntoViewBehaviorPath) -match "ScrollIntoView") -and
    $recipeChainViewSource -match 'ScrollIntoViewOnSelectionChangedBehavior.IsEnabled="True"' -and
    $recipePipelineReviewViewSource -match 'ScrollIntoViewOnSelectionChangedBehavior.IsEnabled="True"' -and
    $recipeChainCodeBehindSource -notmatch "RecipeStepListSelectionChanged" -and
    $recipePipelineReviewCodeBehindSource -notmatch "ValidationSetStepsList_SelectionChanged"
) "Shell owns the reusable WPF selection-scroll behavior; Views no longer duplicate ListBox event handlers"
Add-Check "ResultsValidationNavigationViewModelBoundary" (
    $workspaceNavigationViewModelsSource -match "class ResultsWorkspaceViewModel" -and
    $workspaceNavigationViewModelsSource -match "class RecipePipelineReviewValidationViewModel" -and
    $workspaceNavigationViewModelsSource -match "SelectSectionCommand" -and
    $workspaceNavigationViewModelsSource -match "SetValidationSetFilterCommand\.Execute" -and
    $workspaceNavigationViewModelsSource -match "SelectedValidationSetSample =" -and
    $resultsWorkspaceViewSource -match "ResultsWorkspace\.SelectSectionCommand" -and
    $resultsWorkspaceViewSource -match "ResultsWorkspace\.IsRunRecordSelected" -and
    $resultsWorkspaceCodeBehindSource -match "ResultsWorkspace\.SelectSection\(section\)" -and
    $resultsWorkspaceCodeBehindSource -notmatch "enum ResultsWorkspaceSection" -and
    $resultsWorkspaceCodeBehindSource -notmatch "Navigation_Click" -and
    $recipePipelineReviewCodeBehindSource -match "validationWorkspace\?\.SelectSection\(section\)" -and
    $recipePipelineReviewCodeBehindSource -notmatch "Navigation_Click" -and
    $recipePipelineReviewCodeBehindSource -notmatch "SetValidationSetFilterCommand\.Execute" -and
    $recipePipelineReviewCodeBehindSource -notmatch "SelectedValidationSetSample\s*=\s*\r?\n\s*workbench\.ValidationSetSamples"
) "Results and Validation section state/commands belong to child ViewModels; Views retain binding, layout, and thin request adapters"
Add-Check "ShellSourceQualitySmokeBoundary" (
    $shellSourceQualitySmokeSource -match "internal static class ShellSourceQualitySmoke" -and
    $shellSourceQualitySmokeSource -match "public static async Task<bool> RunAsync" -and
    $shellSourceQualitySmokeSource -match "viewOnly=true\|recipeChanged=false\|inspectionRun=false" -and
    $mainWindowSmokeSourceQualitySource -match "ShellSourceQualitySmoke\.RunAsync" -and
    $mainWindowSmokeSourceQualitySource -notmatch "RunSourceQualitySmokeAsync" -and
    $mainWindowSmokeSourceQualitySource -notmatch 'SourceQualityWorkspaceSmoke\|\{\(passed \? "Pass" : "Fail"\)'
) "Source Quality smoke policy and report ownership moved to Verification/Smoke; MainWindow retains invocation and failure callback"
Add-Check "ViewerHeightDeviationRecipeLoadBoundary" (
    $viewerRecipeRecipesSource -match "internal sealed record HeightDeviationRecipeLoadPlan" -and
    $viewerRecipeRecipesSource -match "static HeightDeviationRecipeLoadPlan Create" -and
    $viewerRecipeRecipesSource -match "C3DHeightGrid\.Load\(" -and
    $viewerRecipeRecipesSource -match "HeightDeviationRule\.Evaluate\(" -and
    $viewerHeightDeviationApplySource -match "HeightDeviationRecipeLoadPlan\.Create\(" -and
    $viewerHeightDeviationApplySource -notmatch "C3DHeightGrid\.Load\(sourcePath, viewModel\.C3DMaxRenderedPoints\)" -and
    $viewerHeightDeviationApplySource -notmatch "HeightDeviationRule\.Evaluate\(new HeightDeviationRuleInput"
) "Height Deviation recipe source loading and rule preparation moved to an independent recipe owner; View retains state/render application"
Add-Check "ViewerHeightDeviationRecipeApplyBoundary" (
    $viewerRecipeApplyCoordinatorSource -match "internal static class HeightDeviationRecipeApplyCoordinator" -and
    $viewerRecipeApplyCoordinatorSource -match "public static bool Apply\(" -and
    $viewerRecipeApplyCoordinatorSource -match "SetRecipeLoaded\(" -and
    $viewerRecipeApplyCoordinatorSource -match "SetC3DAlignment\(" -and
    $viewerRecipeApplyCoordinatorSource -match "applyRoiStep\(" -and
    $viewerHeightDeviationApplySource -match "HeightDeviationRecipeApplyCoordinator\.Apply\(" -and
    $viewerHeightDeviationApplySource -notmatch "viewModel\.ClearPlaneFlatnessRecipeStep\(\)" -and
    $viewerHeightDeviationApplySource -notmatch "viewModel\.SetRecipeLoaded\(" -and
    $viewerHeightDeviationApplySource -notmatch "viewModel\.SetC3DAlignment\("
) "Height Deviation recipe state/application sequence belongs to the non-WPF coordinator; View supplies only rendering and ROI/preview callbacks"
Add-Check "ViewerHeightDeviationRecipeSaveBoundary" (
    $viewerRecipeSaveCoordinatorSource -match "internal static class HeightDeviationRecipeSaveCoordinator" -and
    $viewerRecipeSaveCoordinatorSource -match "public static bool Save\(" -and
    $viewerRecipeSaveCoordinatorSource -match "new HeightDeviationRecipe\(" -and
    $viewerRecipeSaveCoordinatorSource -match "recipe\.Save\(" -and
    $viewerRecipeSaveCoordinatorSource -match "SetRecipeSaved\(" -and
    $viewerHeightDeviationSaveSource -match "HeightDeviationRecipeSaveCoordinator\.Save\(" -and
    $viewerHeightDeviationSaveSource -notmatch "new HeightDeviationRecipe\(" -and
    $viewerHeightDeviationSaveSource -notmatch "recipe\.Save\(fullRecipePath\)"
) "Height Deviation recipe construction, relative source mapping, persistence, and saved-state update belong to the non-WPF coordinator; View retains validation and source/ROI input"
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
Add-Check "VisionSdkSurfaceMatchingOwnership" (
    (Test-Path -LiteralPath $surfacePoseAdapterPath) -and
    (Test-Path -LiteralPath $surfaceCoverageAdapterPath) -and
    (Test-Path -LiteralPath $surfaceSdkAdapterPath) -and
    (Test-Path -LiteralPath $multipleSurfaceMatchAdapterPath) -and
    (Test-Path -LiteralPath $surfacePoseEquivalenceAdapterPath) -and
    (Test-Path -LiteralPath $surfacePoseEquivalenceContractPath) -and
    ([System.IO.File]::ReadAllText($surfacePoseAdapterPath) -match "DeterministicRigidSurfacePoseSearchTool") -and
    ([System.IO.File]::ReadAllText($surfaceCoverageAdapterPath) -match "DeterministicSurfaceCoverageTool") -and
    ([System.IO.File]::ReadAllText($surfaceSdkAdapterPath) -match "OpenVisionLab\.Vision3D\.FeatureExtraction") -and
    ([System.IO.File]::ReadAllText($multipleSurfaceMatchAdapterPath) -match "DeterministicMultipleSurfaceMatchTool") -and
    ([System.IO.File]::ReadAllText($surfacePoseEquivalenceAdapterPath) -match "RigidPoseSymmetryEquivalenceTool") -and
    ([System.IO.File]::ReadAllText($surfaceSdkAdapterPath) -match "RigidPoseSymmetryEquivalenceOptions") -and
    -not ([System.IO.File]::ReadAllText($surfacePoseAdapterPath) -match "Math\.|AxisCandidates|Centroid\(|Rotation3|InsideTranslationBounds") -and
    -not ([System.IO.File]::ReadAllText($surfaceCoverageAdapterPath) -match "Math\.|DistanceSquared|claimedSceneSamples") -and
    -not ([System.IO.File]::ReadAllText($multipleSurfaceMatchAdapterPath) -match "Math\.|DistanceSquared|claimedSceneSamples|AxisCandidates|Rotation3") -and
    -not ([System.IO.File]::ReadAllText($surfacePoseEquivalenceAdapterPath) -match "Math\.|Acos|Atan2|OperationTrace|RelativeRotation")
) "Studio validates identities and maps single/multiple/equivalence evidence; vendored OpenVisionLab Vision SDK owns pose-search, disjoint result collection, coverage, and symmetry-equivalence arithmetic"
Add-Check "VisionSdkSurfacePreparationAndEdgeOwnership" (
    (Test-Path -LiteralPath $surfaceModelPreparationAdapterPath) -and
    (Test-Path -LiteralPath $modelKeyPointAdapterPath) -and
    (Test-Path -LiteralPath $preparedSceneAdapterPath) -and
    (Test-Path -LiteralPath $modelSurfaceEdgeAdapterPath) -and
    (Test-Path -LiteralPath $sceneSurfaceEdgeAdapterPath) -and
    (Test-Path -LiteralPath $surfaceEdgeCoverageAdapterPath) -and
    ([System.IO.File]::ReadAllText($surfaceModelPreparationAdapterPath) -match "DeterministicModelSurfaceSelectionTool") -and
    ([System.IO.File]::ReadAllText($surfaceModelPreparationAdapterPath) -match "DeterministicSurfaceModelPreparationTool") -and
    ([System.IO.File]::ReadAllText($modelKeyPointAdapterPath) -match "DeterministicModelKeyPointExtractionTool") -and
    ([System.IO.File]::ReadAllText($preparedSceneAdapterPath) -match "DeterministicPreparedScenePreparationTool") -and
    ([System.IO.File]::ReadAllText($modelSurfaceEdgeAdapterPath) -match "DeterministicModelSurfaceEdgeExtractionTool") -and
    ([System.IO.File]::ReadAllText($sceneSurfaceEdgeAdapterPath) -match "DeterministicOrganizedSceneSurfaceEdgeExtractionTool") -and
    ([System.IO.File]::ReadAllText($surfaceEdgeCoverageAdapterPath) -match "DeterministicSurfaceEdgeCoverageTool") -and
    -not ([System.IO.File]::ReadAllText($surfaceModelPreparationAdapterPath) -match "Math\.|Vector3\.Normalize|GetEvenTriangleIndex|TriangleKey|DuplicateGeometry|Dictionary<") -and
    -not ([System.IO.File]::ReadAllText($modelKeyPointAdapterPath) -match "Math\.|Distance\(|\.OrderBy|\.Aggregate|\.Max\(|\.Min\(") -and
    -not ([System.IO.File]::ReadAllText($preparedSceneAdapterPath) -match "Math\.|GetEvenPointIndex") -and
    -not ([System.IO.File]::ReadAllText($modelSurfaceEdgeAdapterPath) -match "Math\.|TriangleNormal|Distance\(|Dot\(|Cross\(") -and
    -not ([System.IO.File]::ReadAllText($sceneSurfaceEdgeAdapterPath) -match "Math\.|AddCandidate|Math\.Abs") -and
    -not ([System.IO.File]::ReadAllText($surfaceEdgeCoverageAdapterPath) -match "Math\.|DistanceSquared|claimedSceneEdges|squaredErrorSum")
) "Studio composes identified artifacts and active-domain evidence; vendored OpenVisionLab Vision SDK owns model-surface selection, surface preparation, key-point extraction, edge extraction, and edge coverage arithmetic"
Add-Check "VisionSdkOutlierFilteringAndLevelingOwnership" (
    (Test-Path -LiteralPath $removeOutlierAdapterPath) -and
    (Test-Path -LiteralPath $levelSurfaceAdapterPath) -and
    ([System.IO.File]::ReadAllText($removeOutlierAdapterPath) -match "DeterministicLocalMedianOutlierFilterTool") -and
    ([System.IO.File]::ReadAllText($levelSurfaceAdapterPath) -match "LevelSurfaceTool") -and
    -not ([System.IO.File]::ReadAllText($removeOutlierAdapterPath) -match "Math\.|\.Sort\s*\(|Median\s*\(") -and
    -not ([System.IO.File]::ReadAllText($levelSurfaceAdapterPath) -match "Math\.|\.Average\s*\(|\.Sum\s*\(|HeightFieldPlaneFit\.Fit|TransformHeight\s*\(")
) "Studio validates identities and composes evidence; vendored OpenVisionLab Vision SDK owns local-median filtering and leveling arithmetic"
Add-Check "VisionSdkNominalComparisonAndTransformDiagnosticsOwnership" (
    (Test-Path -LiteralPath $nominalActualAdapterPath) -and
    (Test-Path -LiteralPath $meshDistanceAdapterPath) -and
    (Test-Path -LiteralPath $registrationAcceptanceAdapterPath) -and
    ([System.IO.File]::ReadAllText($nominalActualAdapterPath) -match "NominalActualMeshComparisonTool") -and
    ([System.IO.File]::ReadAllText($meshDistanceAdapterPath) -match "TriangleMeshDistanceTool") -and
    ([System.IO.File]::ReadAllText($registrationAcceptanceAdapterPath) -match "RigidTransformDiagnosticsTool") -and
    -not ([System.IO.File]::ReadAllText($nominalActualAdapterPath) -match "Math\.|RunningStatistics|FindClosest\(") -and
    -not ([System.IO.File]::ReadAllText($meshDistanceAdapterPath) -match "Math\.|BuildNode|FindClosestPoint|SearchRobustCandidates") -and
    -not ([System.IO.File]::ReadAllText($registrationAcceptanceAdapterPath) -match "Math\.|Vector3d|rotationRows|translationMagnitude\s*=|rotationAngleDegrees\s*=")
) "Studio retains identity, policy, lifecycle, and result composition; vendored OpenVisionLab Vision SDK owns mesh distance/comparison and transform-diagnostic arithmetic"
Add-Check "VisionSdkHeightMapInspectionPreparationOwnership" (
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
) "Studio retains decoding, identity, recipes, hashes, metrics, and presentation; vendored OpenVisionLab Vision SDK owns height-grid summaries, distributions, ROI statistics, completeness inspection, and reference-grid reconstruction"
Add-Check "VisionSdkHeightInspectionRuleOwnership" (
    (Test-Path -LiteralPath $dualSurfaceThicknessAdapterPath) -and
    (Test-Path -LiteralPath $heightDeviationAdapterPath) -and
    ([System.IO.File]::ReadAllText($dualSurfaceThicknessAdapterPath) -match "DualSurfaceThicknessInspectionTool") -and
    ([System.IO.File]::ReadAllText($heightDeviationAdapterPath) -match "HeightDeviationInspectionTool") -and
    -not ([System.IO.File]::ReadAllText($dualSurfaceThicknessAdapterPath) -match "Math\.|\.Average\s*\(|\.Min\s*\(|\.Max\s*\(|residual\s*=|below\s*=|above\s*=") -and
    -not ([System.IO.File]::ReadAllText($heightDeviationAdapterPath) -match "Math\.|lowDeviation|highDeviation|peakDeviation\s*=|PeakTolerance\s*\?")
) "Studio retains identity, timing, metrics, overlays, and lifecycle; vendored OpenVisionLab Vision SDK owns dual-surface residual statistics and height-deviation decisions"
Add-Check "VisionSdkGeometryQualityOwnership" (
    (Test-Path -LiteralPath $declaredNormalQualityAdapterPath) -and
    (Test-Path -LiteralPath $landmarkCorrespondenceAdapterPath) -and
    ([System.IO.File]::ReadAllText($declaredNormalQualityAdapterPath) -match "DeclaredMeshNormalQualityTool") -and
    ([System.IO.File]::ReadAllText($landmarkCorrespondenceAdapterPath) -match "LandmarkCorrespondenceValidationTool") -and
    -not ([System.IO.File]::ReadAllText($declaredNormalQualityAdapterPath) -match "Math\.|Vector[234]?\.(?:Cross|Dot|Normalize)") -and
    -not ([System.IO.File]::ReadAllText($landmarkCorrespondenceAdapterPath) -match "Math\.|GetAugmentedRank|GetNormalizedTetrahedronVolume|RankRelativeTolerance")
) "Studio retains source/landmark identity, report/artifact policy, hashing, and lifecycle; vendored OpenVisionLab Vision SDK owns declared-normal geometry and four-point independence arithmetic"
Add-Check "VisionSdkRepeatabilityStatisticsOwnership" (
    (Test-Path -LiteralPath $alignedPointRepeatabilityAdapterPath) -and
    (Test-Path -LiteralPath $thicknessRepeatabilityAdapterPath) -and
    ([System.IO.File]::ReadAllText($alignedPointRepeatabilityAdapterPath) -match "RepeatabilityStatisticsTool") -and
    ([System.IO.File]::ReadAllText($thicknessRepeatabilityAdapterPath) -match "RepeatabilityStatisticsTool") -and
    -not ([System.IO.File]::ReadAllText($alignedPointRepeatabilityAdapterPath) -match "sumSquared|variance\s*=|Math\.Sqrt|SixSigmaSpread\s*=|maximum\s*-\s*minimum") -and
    -not ([System.IO.File]::ReadAllText($thicknessRepeatabilityAdapterPath) -match "sumSquared|variance\s*=|Math\.Sqrt|SixSigmaSpread\s*=|maximum\s*-\s*minimum")
) "Studio retains study/source identity, unit/frame/alignment policy, acceptance, metrics, and evidence; vendored OpenVisionLab Vision SDK owns scalar repeatability statistics"
Add-Check "VisionSdkValidationStatisticsOwnership" (
    (Test-Path -LiteralPath $labeledEvidenceStatisticsAdapterPath) -and
    (Test-Path -LiteralPath $thresholdCandidateAnalysisAdapterPath) -and
    ([System.IO.File]::ReadAllText($labeledEvidenceStatisticsAdapterPath) -match "LabeledEvidenceStatisticsTool") -and
    ([System.IO.File]::ReadAllText($labeledEvidenceStatisticsAdapterPath) -match "HeightMapRegionStatisticsTool") -and
    ([System.IO.File]::ReadAllText($thresholdCandidateAnalysisAdapterPath) -match "ThresholdCandidateAnalysisTool") -and
    -not ([System.IO.File]::ReadAllText($labeledEvidenceStatisticsAdapterPath) -match "Math\.|\.Average\s*\(|\.Sum\s*\(") -and
    -not ([System.IO.File]::ReadAllText($thresholdCandidateAnalysisAdapterPath) -match "Math\.|BitIncrement|BitDecrement|\.Average\s*\(|\.Sum\s*\(")
) "Studio retains observation/metric identity, routing, warnings, hashing, and reports; vendored OpenVisionLab Vision SDK owns role statistics and deterministic threshold analysis"

$visionSdkToolContractExists = Test-Path -LiteralPath $visionSdkToolContractPath
$visionSdkToolBaselineExists = Test-Path -LiteralPath $visionSdkToolBaselinePath
$visionSdkToolContractSource = if ($visionSdkToolContractExists) {
    [System.IO.File]::ReadAllText($visionSdkToolContractPath)
} else {
    ""
}
$visionSdkToolBaseline = if ($visionSdkToolBaselineExists) {
    [System.IO.File]::ReadAllText($visionSdkToolBaselinePath) | ConvertFrom-Json
} else {
    $null
}
Add-Check "VisionSdkToolOwnershipContract" (
    $visionSdkToolContractExists -and
    $visionSdkToolBaselineExists -and
    $visionSdkToolContractSource -match 'public,?\s+sealed\s+`XxxTool`' -and
    $visionSdkToolContractSource -match "IThreeDInspectionTool" -and
    $visionSdkToolContractSource -match "decreasing\s+migration baseline"
) "contract=$visionSdkToolContractExists|baseline=$visionSdkToolBaselineExists"

$migrationDebt = @($visionSdkToolBaseline.migrationDebt)
$studioBoundaries = @($visionSdkToolBaseline.studioBoundaries)
$hasMigrationDebtProperty = $null -ne $visionSdkToolBaseline -and
    $visionSdkToolBaseline.PSObject.Properties.Name -contains "migrationDebt"
$visionSdkInventory = @($migrationDebt + $studioBoundaries)
$inventoryPaths = @($visionSdkInventory | ForEach-Object { [string]$_.path })
$duplicateInventoryPaths = @(
    $inventoryPaths |
        Group-Object |
        Where-Object Count -gt 1 |
        ForEach-Object Name
)
$missingInventoryFiles = @(
    $visionSdkInventory |
        Where-Object { -not (Test-Path -LiteralPath (Join-Path $repoRoot $_.path)) } |
        ForEach-Object path
)
$invalidDebtEntries = @(
    $migrationDebt |
        Where-Object {
            [string]::IsNullOrWhiteSpace([string]$_.targetVisionSdkTool) -or
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
Add-Check "VisionSdkToolMigrationInventory" (
    $null -ne $visionSdkToolBaseline -and
    [int]$visionSdkToolBaseline.schemaVersion -eq 1 -and
    [string]$visionSdkToolBaseline.contract -eq "docs/OPENVISIONLAB_3D_VISION_SDK_TOOL_CONTRACT_AND_MIGRATION_BASELINE_20260805.md" -and
    $hasMigrationDebtProperty -and
    $duplicateInventoryPaths.Count -eq 0 -and
    $missingInventoryFiles.Count -eq 0 -and
    $invalidDebtEntries.Count -eq 0 -and
    $invalidBoundaryEntries.Count -eq 0
) "debt=$($migrationDebt.Count)|boundaries=$($studioBoundaries.Count)|duplicates=$($duplicateInventoryPaths -join ',')|missing=$($missingInventoryFiles -join ',')|invalidDebt=$($invalidDebtEntries -join ',')|invalidBoundaries=$($invalidBoundaryEntries -join ',')"

$algorithmOwnerPattern = "public\s+(?:static\s+|sealed\s+)?class\s+\w*(?:Rule|Analyzer|Scorer|Extractor|Preparation|Executor|Execution|Index|Builder|Statistics|Filter|Fit|Matcher|Tool)\b"
$numericalSignalPattern = "Math\.|MathF\.|Vector[234]?\.(?:Cross|Dot|Distance|DistanceSquared|Normalize)|\.Average\s*\(|\.Sum\s*\(|\.Sort\s*\("
$visionSdkCandidateRoots = @(
    Join-Path $repoRoot "src/OpenVisionLab.ThreeD.Tools"
    Join-Path $repoRoot "src/OpenVisionLab.ThreeD.Data/Quality"
    Join-Path $repoRoot "src/OpenVisionLab.ThreeD.Data/HeightMaps"
)
$detectedNumericalOwners = @(
    Get-ChildItem -LiteralPath $visionSdkCandidateRoots -Filter "*.cs" -File -Recurse |
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
    $visionSdkInventory |
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
