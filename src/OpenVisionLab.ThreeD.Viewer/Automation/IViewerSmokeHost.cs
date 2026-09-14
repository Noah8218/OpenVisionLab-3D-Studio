namespace OpenVisionLab.ThreeD.Viewer.Automation;

using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Viewer.ViewModels;

// Native operation boundary shared by the real View adapter and the independent
// scenario verifier. It exposes no Window, Dispatcher, control or GPU object.
internal interface IViewerSmokeHost
{
    bool IsDisposed { get; }
    bool IsDispatcherStopping { get; }
    bool C3DSampleVisible { get; }
    bool GlbSampleVisible { get; }
    string GlbSampleSourcePath { get; }
    bool LazSampleVisible { get; }
    bool RoiStepMeasurementVisible { get; }
    NominalActualComparisonState NominalActualState { get; }
    double NominalActualLowerTolerance { get; }
    double NominalActualUpperTolerance { get; }
    double TwoPointDistance { get; }
    double TwoPointRawHeightDelta { get; }
    double ViewportFps { get; }
    double ViewportDrawMilliseconds { get; }
    double RecipeTransformTranslateX { get; set; }
    double RecipeTransformTranslateY { get; set; }
    double RecipeRoiLeftCenterX { get; set; }
    double RecipeRoiLeftCenterZ { get; set; }
    double RecipeRoiLeftHalfWidth { get; set; }
    double RecipeRoiRightCenterX { get; set; }
    double RecipeRoiRightCenterZ { get; set; }
    double RecipeRoiRightHalfDepth { get; set; }
    double RecipePeakTolerance { get; set; }
    double PlaneFlatnessTolerance { get; set; }
    double LazTwoPointExpectedDistance { get; set; }
    double LazTwoPointExpectedHeightDelta { get; set; }
    double LazTwoPointDistanceTolerance { get; set; }
    double LazTwoPointHeightDeltaTolerance { get; set; }
    string SelectedColorMode { get; set; }
    string SelectedGeometryStyle { get; set; }
    string SelectedRenderDensity { get; set; }
    double PointSize { get; set; }
    bool HudDetailsVisible { get; set; }
    string SelectedEntity { get; set; }
    string ViewerStatus { get; set; }
    bool HasLazPointCloud { get; }
    bool HasImportedMesh { get; }
    bool HasLazTwoPointMeasurement { get; }
    int ImportedMeshTextureUploads { get; }
    int ImportedMeshTextureReleases { get; }

    void LoadSource(ViewerSmokeSource source, string? path);
    void Measure(ViewerSmokeMeasurement measurement);
    bool LoadRecipe(string path);
    bool SaveRecipe(string path);
    bool PublishPreview();
    void ApplyEditedRoiParameters();
    bool ValidateRoi(out string warning);
    bool AlignRoiReference();
    void ConfigureTeachingPointer(string[] args);
    void FitSelection();
    void Pan(double deltaX, double deltaY, double deltaZ);
    void UseSelectionSmokeScene(string mode);
    void UsePointCloudSmokeScene();
    void UseC3DHeightDeviationRuleSmokeScene();
    void UseResultSmokeScene();
    void ConfigureNominalActualComparison(NominalActualComparisonInput input);
    void PreviewNominalActual();
    void ClearNominalActualComparison(string validationIssue);
    void SetRecipeValidationSummary(string summary);
    void SetC3DAlignment(ModelTransform transform, string alignmentName, string referenceName);

    void Render();
    Task RenderAsync(bool atRenderPriority);
    void ResetRenderPerformance();
    void BeginInteractionLod();
    Task ApplyDensityRaceAsync();
    Task ApplyNextDensityAsync();
    Task ReloadLazPointCloudAsync();
    bool ApplyPick();
    Task<bool> RunPointerRegressionAsync();
    void WriteSceneContracts(string path);
    Task<bool> CaptureScreenshotAsync(string path, string? qualityReportPath);
    void Shutdown(int exitCode, bool requireApplication);
}

internal enum ViewerSmokeSource
{
    C3D,
    Glb,
    Stl,
    LazMetadata,
    LazPoints
}

internal enum ViewerSmokeMeasurement
{
    PointPairDimensions,
    LazTwoPoint,
    MeshTwoPoint,
    C3DTwoPoint,
    RoiStep,
    InteractiveRoiStep,
    PlaneReference,
    PlaneFlatness,
    GapFlush,
    Volume,
    CrossSection
}
