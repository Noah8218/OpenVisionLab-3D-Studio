using System.IO;
using OpenVisionLab;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Data;
using OpenVisionLab.ThreeD.Shell;
using OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;
using OpenVisionLab.ThreeD.Tools;

namespace OpenVisionLab.ThreeD.Verification.Shell.Workbench;

internal static class ToolWorkbenchArtifactNavigatorEventCoordinatorVerification
{
    public static bool Verify(string reportPath, out string summary)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reportPath);
        var lines = new List<string>
        {
            "OpenVisionLab 3D ToolWorkbench Artifact Navigator event coordinator verification",
            $"Generated: {DateTimeOffset.Now:O}",
            "Scope: navigator PropertyChanged/Rebuilt routing and disposal"
        };
        var passed = 0;
        var total = 0;
        Exception? failure = null;

        void Check(string name, bool condition, string detail)
        {
            total++;
            lines.Add($"{(condition ? "PASS" : "FAIL")} | {name} | {detail}");
            if (condition)
            {
                passed++;
            }
        }

        try
        {
            var source = new ToolWorkbenchSourceItem(
                "source.c3d.height-map",
                "Source",
                "C3D",
                "raw-height",
                "frame.c3d-grid-index",
                string.Empty);
            var snapshot = new ToolWorkbenchArtifactProjectionSnapshot(
                source,
                false,
                null,
                "Source required",
                "Source context",
                "Verified C3D source · {0} × {1}",
                Array.Empty<ToolWorkbenchReferenceItem>(),
                Array.Empty<ToolRecipeSelection>(),
                null!,
                Array.Empty<ToolWorkbenchPipelineStepItem>(),
                null!,
                delta => delta.Summary,
                "Quality delta unavailable",
                "not evaluated by ROI / Crop",
                "not evaluated by Level Surface",
                "domain cells are the explicit Connected Region union",
                "detected by Remove Outlier Pixels mask",
                "not evaluated by Median Filter",
                "Typed output '{0}' is declared, but has no current Preview or Published evidence.",
                "Step '{0}' disabled output policy. No Preview, Run output, or evidence is fabricated.",
                "Recipe-owned ROI / selection",
                "Recapture is required because the source binding changed.",
                "source identity retained",
                "domain-reduced",
                "{0} × {1} | valid {2:N0} | missing {3:N0} | {4} | {5} | {6}",
                "Connected Region detail: {0:N0} region(s) | mask {1} | filtered {2} | root {3}",
                "Editable Region detail: region {0} | {1:N0} exact cell(s) | bounds {2} × {3} | connected {4}",
                "Remove Outlier detail: {0} × {1} | removed {2:N0} | outlier mask {3} | {4} | {5}",
                "{0} × {1} | source origin ({2}, {3}) | valid {4:N0} | missing {5:N0} | {6} | {7}",
                "{0} × {1} | reference RMS {2:G6} | transform {3} | level frame {4} | frame chain {5} | quality {6} {7} | {8} | {9}",
                "{0} × {1} | {2} | {3}",
                "{0:N0} points | {1}",
                "{0:N0}/{1:N0} inliers | {2}",
                "ordered picks ({0}, {1}) -> ({2}, {3}) | {4}",
                 "ordered picks ({0}, {1}) -> ({2}, {3}) -> ({4}, {5}) | {6}",
                 "{0} | P2V {1:G6} raw-height | {2:N0} samples | {3}",
                 "{0} | gap {1:G6} | acute {2:G6} degrees",
                 "{0}/4 pairs | source rank {1}/4 | reference rank {2}/4 | correspondence evidence only",
                 "condition {0:G6} | max residual {1:G6} | matrix evidence only",
                 "{0:N0} finite transformed points | {1:N0} missing source cells | A3 re-grid excluded",
                 "{0:N0}/{1:N0} populated | coverage {2:P2} | missing {3:N0} | collisions {4:N0}",
                 "{0} | {1}",
                 status => status.ToString(),
                 new ToolWorkbenchArtifactPreview<C3DHeightFieldSnapshot>(null, false, false),
                new ToolWorkbenchLevelSurfaceArtifactPreview(null, null, null, null, null, false, false),
                new ToolWorkbenchArtifactPreview<C3DConnectedRegionArtifact>(null, false, false),
                new ToolWorkbenchArtifactPreview<C3DHeightFieldSnapshot>(null, false, false),
                new ToolWorkbenchArtifactPreview<C3DEditableRegionArtifact>(null, false, false),
                new ToolWorkbenchRemoveOutlierArtifactPreview(null, null, false, false),
                new ToolWorkbenchArtifactPreview<C3DHeightFieldSnapshot>(null, false, false),
                new ToolWorkbenchArtifactPreview<C3DHeightDifferenceEdgePointSet>(null, false, false),
                null!,
                null!,
                null!,
                null!,
                null!,
                new ToolWorkbenchArtifactPreview<C3DLandmarkCorrespondenceSet>(null, false, false),
                null!,
                new ToolWorkbenchArtifactPreview<C3DTransformedPointCloud>(null, false, false),
                new ToolWorkbenchArtifactPreview<C3DTransformedHeightField>(null, false, false),
                new ToolWorkbenchArtifactPreview<ToolRecipeHeightMeasurementOutput>(null, false, false));
            var owner = new ToolWorkbenchArtifactNavigatorOwner(
                new ToolWorkbenchArtifactProjection(),
                () => snapshot,
                () => null,
                _ => { },
                _ => { });
            var propertyChangedCount = 0;
            var rebuiltCount = 0;
            var coordinator = new ToolWorkbenchArtifactNavigatorEventCoordinator(
                owner,
                _ => propertyChangedCount++,
                () => rebuiltCount++);

            try
            {
                owner.Rebuild();
                Check(
                    "Navigator property notifications reach the Workbench callback",
                    propertyChangedCount > 0,
                    $"propertyChangedCount={propertyChangedCount}");
                Check(
                    "Navigator rebuild notification reaches the dependent-refresh callback",
                    rebuiltCount == 1,
                    $"rebuiltCount={rebuiltCount}");

                var qualityDeltaTool = new ToolWorkbenchToolItem(
                    "Preparation",
                    "Filter",
                    "filter",
                    1,
                    "HeightField",
                    "FilteredHeightField",
                    "Verification filter.",
                    []);
                var qualityDeltaStep = new ToolWorkbenchPipelineStepItem(
                    "step.quality-delta",
                    qualityDeltaTool,
                    source.Id,
                    "derived.quality-delta");
                var qualityDeltaOutput = C3DHeightFieldSnapshot.CreateForVerification(
                    "derived.quality-delta",
                    2,
                    2,
                    [1, 2, 3, 4]);
                var qualityDeltaLanguage = OpenVisionLanguageService.CurrentLanguage;
                try
                {
                    OpenVisionLanguageService.SetLanguage(OpenVisionLanguage.English, save: false);
                    var englishArtifact = new ToolWorkbenchArtifactProjection()
                        .Project(snapshot with
                        {
                            PipelineSteps = [qualityDeltaStep],
                            CreateSourceQualityDelta = (_, _, _) => null,
                            QualityDeltaUnavailable = ThreeDLocalization.Shared.OutputCompareQualityDeltaUnavailable,
                            FilterArtifactDetailFormat = ThreeDLocalization.Shared.FilterArtifactDetailFormat,
                            Filter = new ToolWorkbenchArtifactPreview<C3DHeightFieldSnapshot>(
                                qualityDeltaOutput,
                                false,
                                false)
                        })
                        .Single(item => item.Id == qualityDeltaOutput.EntityId);
                    OpenVisionLanguageService.SetLanguage(OpenVisionLanguage.Korean, save: false);
                    var koreanArtifact = new ToolWorkbenchArtifactProjection()
                        .Project(snapshot with
                        {
                            PipelineSteps = [qualityDeltaStep],
                            CreateSourceQualityDelta = (_, _, _) => null,
                            QualityDeltaUnavailable = ThreeDLocalization.Shared.OutputCompareQualityDeltaUnavailable,
                            FilterArtifactDetailFormat = ThreeDLocalization.Shared.FilterArtifactDetailFormat,
                            Filter = new ToolWorkbenchArtifactPreview<C3DHeightFieldSnapshot>(
                                qualityDeltaOutput,
                                false,
                                false)
                        })
                        .Single(item => item.Id == qualityDeltaOutput.EntityId);
                    Check(
                        "filter detail localizes grid labels while preserving provenance, output identity, source, quality fallback, and state",
                        englishArtifact.Detail.Contains("grid 2 × 2", StringComparison.Ordinal)
                        && koreanArtifact.Detail.Contains("격자 2 × 2", StringComparison.Ordinal)
                        && englishArtifact.Detail.Contains(qualityDeltaOutput.Provenance, StringComparison.Ordinal)
                        && koreanArtifact.Detail.Contains(qualityDeltaOutput.Provenance, StringComparison.Ordinal)
                        && englishArtifact.Detail.Contains("Quality delta unavailable", StringComparison.Ordinal)
                        && koreanArtifact.Detail.Contains(
                            ThreeDLocalization.Shared.OutputCompareQualityDeltaUnavailable,
                            StringComparison.Ordinal)
                        && englishArtifact.Detail != koreanArtifact.Detail
                        && englishArtifact.Id == koreanArtifact.Id
                        && englishArtifact.ContentSha256 == koreanArtifact.ContentSha256
                        && englishArtifact.RootSourceId == source.Id
                        && koreanArtifact.RootSourceId == source.Id
                        && englishArtifact.State == "Preview"
                        && koreanArtifact.State == "Preview"
                        && englishArtifact.NodeKind == "FilteredHeightField"
                        && koreanArtifact.NodeKind == "FilteredHeightField"
                        && !koreanArtifact.Detail.Contains("quality delta unavailable", StringComparison.Ordinal),
                        $"en={englishArtifact.Detail}; ko={koreanArtifact.Detail}; id={koreanArtifact.Id}; hash={koreanArtifact.ContentSha256}");

                    var filterQualityDeltaFactory = (string evidence) => new SourceQualityDelta(
                        source.Id,
                        qualityDeltaOutput.ContentSha256,
                        qualityDeltaOutput.EntityId,
                        qualityDeltaOutput.ContentSha256,
                        qualityDeltaOutput.RootSourceSha256,
                        qualityDeltaOutput.RootSourceSha256,
                        qualityDeltaOutput.ValidCount,
                        qualityDeltaOutput.ValidCount,
                        qualityDeltaOutput.MissingCount,
                        qualityDeltaOutput.MissingCount,
                        null,
                        evidence);
                    OpenVisionLanguageService.SetLanguage(OpenVisionLanguage.English, save: false);
                    var englishFilterQualityDeltaEvidence = ThreeDLocalization.Shared.FilterQualityDeltaEvidence;
                    var englishFilterQualityDeltaArtifact = new ToolWorkbenchArtifactProjection()
                        .Project(snapshot with
                        {
                            PipelineSteps = [qualityDeltaStep],
                            CreateSourceQualityDelta = (_, _, evidence) => filterQualityDeltaFactory(evidence),
                            FormatSourceQualityDeltaSummary = delta => ToolWorkbenchViewModel.FormatSourceQualityDeltaSummary(delta, ThreeDLocalization.Shared),
                            FilterQualityDeltaEvidence = englishFilterQualityDeltaEvidence,
                            Filter = new ToolWorkbenchArtifactPreview<C3DHeightFieldSnapshot>(qualityDeltaOutput, false, false)
                        })
                        .Single(item => item.Id == qualityDeltaOutput.EntityId);
                    var englishFilterQualityDeltaSummary = ToolWorkbenchViewModel.FormatSourceQualityDeltaSummary(
                        englishFilterQualityDeltaArtifact.PreparationQualityDelta!,
                        ThreeDLocalization.Shared);
                    OpenVisionLanguageService.SetLanguage(OpenVisionLanguage.Korean, save: false);
                    var koreanFilterQualityDeltaEvidence = ThreeDLocalization.Shared.FilterQualityDeltaEvidence;
                    var koreanFilterQualityDeltaArtifact = new ToolWorkbenchArtifactProjection()
                        .Project(snapshot with
                        {
                            PipelineSteps = [qualityDeltaStep],
                            CreateSourceQualityDelta = (_, _, evidence) => filterQualityDeltaFactory(evidence),
                            FormatSourceQualityDeltaSummary = delta => ToolWorkbenchViewModel.FormatSourceQualityDeltaSummary(delta, ThreeDLocalization.Shared),
                            FilterQualityDeltaEvidence = koreanFilterQualityDeltaEvidence,
                            Filter = new ToolWorkbenchArtifactPreview<C3DHeightFieldSnapshot>(qualityDeltaOutput, false, false)
                        })
                        .Single(item => item.Id == qualityDeltaOutput.EntityId);
                    var koreanFilterQualityDeltaSummary = ToolWorkbenchViewModel.FormatSourceQualityDeltaSummary(
                        koreanFilterQualityDeltaArtifact.PreparationQualityDelta!,
                        ThreeDLocalization.Shared);
                    Check(
                        "filter quality delta evidence localizes while preserving counts, hashes, root, output identity, state, and node kind",
                        englishFilterQualityDeltaArtifact.PreparationQualityDelta?.Summary.Contains(
                            englishFilterQualityDeltaEvidence,
                            StringComparison.Ordinal) == true
                        && koreanFilterQualityDeltaArtifact.PreparationQualityDelta?.Summary.Contains(
                            koreanFilterQualityDeltaEvidence,
                            StringComparison.Ordinal) == true
                        && englishFilterQualityDeltaEvidence != koreanFilterQualityDeltaEvidence
                        && englishFilterQualityDeltaArtifact.PreparationQualityDelta?.DetectedOutlierCount is null
                        && koreanFilterQualityDeltaArtifact.PreparationQualityDelta?.DetectedOutlierCount is null
                        && englishFilterQualityDeltaArtifact.PreparationQualityDelta?.BeforeValidSampleCount == qualityDeltaOutput.ValidCount
                        && koreanFilterQualityDeltaArtifact.PreparationQualityDelta?.BeforeValidSampleCount == qualityDeltaOutput.ValidCount
                        && englishFilterQualityDeltaArtifact.PreparationQualityDelta?.AfterValidSampleCount == qualityDeltaOutput.ValidCount
                        && koreanFilterQualityDeltaArtifact.PreparationQualityDelta?.AfterValidSampleCount == qualityDeltaOutput.ValidCount
                        && englishFilterQualityDeltaArtifact.PreparationQualityDelta?.BeforeMissingSampleCount == qualityDeltaOutput.MissingCount
                        && koreanFilterQualityDeltaArtifact.PreparationQualityDelta?.BeforeMissingSampleCount == qualityDeltaOutput.MissingCount
                        && englishFilterQualityDeltaArtifact.PreparationQualityDelta?.AfterMissingSampleCount == qualityDeltaOutput.MissingCount
                        && koreanFilterQualityDeltaArtifact.PreparationQualityDelta?.AfterMissingSampleCount == qualityDeltaOutput.MissingCount
                        && englishFilterQualityDeltaArtifact.PreparationQualityDelta?.SourceContentSha256 == koreanFilterQualityDeltaArtifact.PreparationQualityDelta?.SourceContentSha256
                        && englishFilterQualityDeltaArtifact.PreparationQualityDelta?.DerivedContentSha256 == koreanFilterQualityDeltaArtifact.PreparationQualityDelta?.DerivedContentSha256
                        && englishFilterQualityDeltaArtifact.PreparationQualityDelta?.SourceIdentityRetained == true
                        && koreanFilterQualityDeltaArtifact.PreparationQualityDelta?.SourceIdentityRetained == true
                        && englishFilterQualityDeltaArtifact.Detail.Contains(englishFilterQualityDeltaSummary, StringComparison.Ordinal)
                        && koreanFilterQualityDeltaArtifact.Detail.Contains(koreanFilterQualityDeltaSummary, StringComparison.Ordinal)
                        && englishFilterQualityDeltaArtifact.Id == koreanFilterQualityDeltaArtifact.Id
                        && englishFilterQualityDeltaArtifact.RootSourceId == source.Id
                        && koreanFilterQualityDeltaArtifact.RootSourceId == source.Id
                        && englishFilterQualityDeltaArtifact.InputEntityIds == source.Id
                        && koreanFilterQualityDeltaArtifact.InputEntityIds == source.Id
                        && englishFilterQualityDeltaArtifact.State == "Preview"
                        && koreanFilterQualityDeltaArtifact.State == "Preview"
                        && englishFilterQualityDeltaArtifact.NodeKind == "FilteredHeightField"
                        && koreanFilterQualityDeltaArtifact.NodeKind == "FilteredHeightField",
                        $"en={englishFilterQualityDeltaEvidence}; ko={koreanFilterQualityDeltaEvidence}; enSummary={englishFilterQualityDeltaSummary}; koSummary={koreanFilterQualityDeltaSummary}");
                    Check(
                        "filter artifact quality delta summary localizes while preserving values and identity",
                        englishFilterQualityDeltaArtifact.Detail.Contains(englishFilterQualityDeltaSummary, StringComparison.Ordinal)
                        && koreanFilterQualityDeltaArtifact.Detail.Contains(koreanFilterQualityDeltaSummary, StringComparison.Ordinal)
                        && englishFilterQualityDeltaArtifact.PreparationQualityDelta?.DetectedOutlierCount is null
                        && koreanFilterQualityDeltaArtifact.PreparationQualityDelta?.DetectedOutlierCount is null
                        && englishFilterQualityDeltaArtifact.ContentSha256 == koreanFilterQualityDeltaArtifact.ContentSha256
                        && englishFilterQualityDeltaArtifact.RootSourceId == koreanFilterQualityDeltaArtifact.RootSourceId
                        && englishFilterQualityDeltaArtifact.State == koreanFilterQualityDeltaArtifact.State
                        && englishFilterQualityDeltaArtifact.NodeKind == koreanFilterQualityDeltaArtifact.NodeKind,
                        $"enDetail={englishFilterQualityDeltaArtifact.Detail}; koDetail={koreanFilterQualityDeltaArtifact.Detail}");

                    var edgeTool = new ToolWorkbenchToolItem(
                        "Prepare",
                        "Height Difference Edge",
                        "height-difference-edge",
                        1,
                        "HeightField",
                        "EdgePointSet",
                        "Verification edge.",
                        []);
                    var edgeStep = new ToolWorkbenchPipelineStepItem(
                        "step.edge",
                        edgeTool,
                        source.Id,
                        "derived.edgepoints");
                    var edgeOutput = C3DHeightDifferenceEdgePointSet.Create(
                        "derived.edgepoints",
                        source.Id,
                        new string('A', 64),
                        source.Id,
                        new string('B', 64),
                        "selection.edge",
                        new ToolRecipeGridRectangle(0, 0, 1, 1),
                        source.Unit,
                        source.FrameId,
                        C3DHeightDifferenceComparisonAxis.AcrossColumns,
                        C3DHeightDifferencePolarity.Absolute,
                        0.5,
                        [
                            new C3DHeightDifferenceEdgePoint(
                                0,
                                0,
                                0,
                                1,
                                0,
                                1,
                                2,
                                1,
                                1,
                                0.5,
                                1,
                                0)
                        ],
                        new C3DHeightDifferenceEdgeDiagnostics(2, 2, 0, 1, 1, 1, 1, 1),
                        "verification-edge");
                    OpenVisionLanguageService.SetLanguage(OpenVisionLanguage.English, save: false);
                    var englishEdgeArtifact = new ToolWorkbenchArtifactProjection()
                        .Project(snapshot with
                        {
                            PipelineSteps = [edgeStep],
                            HeightDifferenceEdgeArtifactDetailFormat = ThreeDLocalization.Shared.HeightDifferenceEdgeArtifactDetailFormat,
                            HeightDifferenceEdge = new ToolWorkbenchArtifactPreview<C3DHeightDifferenceEdgePointSet>(
                                edgeOutput,
                                false,
                                false)
                        })
                        .Single(item => item.Id == edgeOutput.OutputEntityId);
                    OpenVisionLanguageService.SetLanguage(OpenVisionLanguage.Korean, save: false);
                    var koreanEdgeArtifact = new ToolWorkbenchArtifactProjection()
                        .Project(snapshot with
                        {
                            PipelineSteps = [edgeStep],
                            HeightDifferenceEdgeArtifactDetailFormat = ThreeDLocalization.Shared.HeightDifferenceEdgeArtifactDetailFormat,
                            HeightDifferenceEdge = new ToolWorkbenchArtifactPreview<C3DHeightDifferenceEdgePointSet>(
                                edgeOutput,
                                false,
                                false)
                        })
                        .Single(item => item.Id == edgeOutput.OutputEntityId);
                    Check(
                        "height-edge detail localizes point labels while preserving provenance, output identity, source, state, and node kind",
                        englishEdgeArtifact.Detail.Contains("1 points", StringComparison.Ordinal)
                        && koreanEdgeArtifact.Detail.Contains("포인트 1개", StringComparison.Ordinal)
                        && englishEdgeArtifact.Detail.Contains("verification-edge", StringComparison.Ordinal)
                        && koreanEdgeArtifact.Detail.Contains("verification-edge", StringComparison.Ordinal)
                        && englishEdgeArtifact.Detail != koreanEdgeArtifact.Detail
                        && englishEdgeArtifact.Id == koreanEdgeArtifact.Id
                        && englishEdgeArtifact.ContentSha256 == koreanEdgeArtifact.ContentSha256
                        && englishEdgeArtifact.RootSourceId == source.Id
                        && koreanEdgeArtifact.RootSourceId == source.Id
                        && englishEdgeArtifact.InputEntityIds == source.Id
                        && koreanEdgeArtifact.InputEntityIds == source.Id
                        && englishEdgeArtifact.State == "Preview"
                        && koreanEdgeArtifact.State == "Preview"
                        && englishEdgeArtifact.NodeKind == "EdgePointSet"
                        && koreanEdgeArtifact.NodeKind == "EdgePointSet",
                        $"en={englishEdgeArtifact.Detail}; ko={koreanEdgeArtifact.Detail}; id={koreanEdgeArtifact.Id}; state={koreanEdgeArtifact.State}; hash={koreanEdgeArtifact.ContentSha256}");

                    var lineOutput = C3DLineFeature.Create(
                        "derived.line",
                        edgeOutput,
                        1,
                        1,
                        0.5,
                        0,
                        0,
                        0,
                        0,
                        1,
                        0,
                        0,
                        0,
                        0,
                        0,
                        1,
                        0,
                        0,
                        new C3DLineFeatureDiagnostics(
                            1,
                            1,
                            0,
                            1,
                            0,
                            0,
                            0,
                            0,
                            0,
                            0,
                            1,
                            1,
                            1),
                        [
                            new C3DLineFeaturePointDiagnostic(
                                0,
                                0,
                                0,
                                0,
                                0,
                                0,
                                0,
                                0,
                                0,
                                true)
                        ],
                        "verification-line");
                    var lineTool = new ToolWorkbenchToolItem(
                        "Measure",
                        "3D Line Fit",
                        "three-d-line-fit",
                        1,
                        "EdgePointSet",
                        "LineFeature",
                        "Verification line fit.",
                        []);
                    var lineStep = new ToolWorkbenchPipelineStepItem(
                        "step.line-fit",
                        lineTool,
                        edgeOutput.OutputEntityId,
                        lineOutput.OutputEntityId);
                    OpenVisionLanguageService.SetLanguage(OpenVisionLanguage.English, save: false);
                    var englishLineArtifact = new ToolWorkbenchArtifactProjection()
                        .Project(snapshot with
                        {
                            PipelineSteps = [lineStep],
                            LineFitArtifactDetailFormat = ThreeDLocalization.Shared.LineFitArtifactDetailFormat,
                            GetPublishedLineFitOutput = outputId => string.Equals(
                                outputId,
                                lineOutput.OutputEntityId,
                                StringComparison.Ordinal)
                                ? lineOutput
                                : null
                        })
                        .Single(item => item.Id == lineOutput.OutputEntityId);
                    OpenVisionLanguageService.SetLanguage(OpenVisionLanguage.Korean, save: false);
                    var koreanLineArtifact = new ToolWorkbenchArtifactProjection()
                        .Project(snapshot with
                        {
                            PipelineSteps = [lineStep],
                            LineFitArtifactDetailFormat = ThreeDLocalization.Shared.LineFitArtifactDetailFormat,
                            GetPublishedLineFitOutput = outputId => string.Equals(
                                outputId,
                                lineOutput.OutputEntityId,
                                StringComparison.Ordinal)
                                ? lineOutput
                                : null
                        })
                        .Single(item => item.Id == lineOutput.OutputEntityId);
                    Check(
                        "line-fit detail localizes inlier labels while preserving provenance, output identity, source, state, and node kind",
                        englishLineArtifact.Detail.Contains("inliers 1/1", StringComparison.Ordinal)
                        && koreanLineArtifact.Detail.Contains("인라이어 1/1개", StringComparison.Ordinal)
                        && englishLineArtifact.Detail.Contains("verification-line", StringComparison.Ordinal)
                        && koreanLineArtifact.Detail.Contains("verification-line", StringComparison.Ordinal)
                        && englishLineArtifact.Detail != koreanLineArtifact.Detail
                        && englishLineArtifact.Id == koreanLineArtifact.Id
                        && englishLineArtifact.ContentSha256 == koreanLineArtifact.ContentSha256
                        && englishLineArtifact.RootSourceId == source.Id
                        && koreanLineArtifact.RootSourceId == source.Id
                        && englishLineArtifact.InputEntityIds == edgeOutput.OutputEntityId
                        && koreanLineArtifact.InputEntityIds == edgeOutput.OutputEntityId
                        && englishLineArtifact.State == "Published"
                        && koreanLineArtifact.State == "Published"
                        && englishLineArtifact.NodeKind == "LineFeature"
                        && koreanLineArtifact.NodeKind == "LineFeature",
                        $"en={englishLineArtifact.Detail}; ko={koreanLineArtifact.Detail}; id={koreanLineArtifact.Id}; state={koreanLineArtifact.State}; hash={koreanLineArtifact.ContentSha256}");

                    var twoPointLineOutput = C3DTwoPointLineFeature.Create(
                        "derived.two-point-line",
                        source.Id,
                        new string('R', 64),
                        source.Unit,
                        source.FrameId,
                        "selection.two-point",
                        new string('S', 64),
                        0,
                        1,
                        1,
                        2,
                        1,
                        2,
                        3,
                        1,
                        0,
                        0,
                        2,
                        2,
                        3,
                        1,
                        "verification-two-point",
                        "verification-two-point");
                    var twoPointLineTool = new ToolWorkbenchToolItem(
                        "Measure",
                        "2-Point Line",
                        "two-point-line",
                        1,
                        "HeightField",
                        "LineFeature",
                        "Verification two-point line.",
                        []);
                    var twoPointLineStep = new ToolWorkbenchPipelineStepItem(
                        "step.two-point-line",
                        twoPointLineTool,
                        source.Id,
                        twoPointLineOutput.OutputEntityId);
                    OpenVisionLanguageService.SetLanguage(OpenVisionLanguage.English, save: false);
                    var englishTwoPointLineArtifact = new ToolWorkbenchArtifactProjection()
                        .Project(snapshot with
                        {
                            PipelineSteps = [twoPointLineStep],
                            TwoPointLineArtifactDetailFormat = ThreeDLocalization.Shared.TwoPointLineArtifactDetailFormat,
                            GetPublishedTwoPointLineOutput = outputId => string.Equals(
                                outputId,
                                twoPointLineOutput.OutputEntityId,
                                StringComparison.Ordinal)
                                ? twoPointLineOutput
                                : null
                        })
                        .Single(item => item.Id == twoPointLineOutput.OutputEntityId);
                    OpenVisionLanguageService.SetLanguage(OpenVisionLanguage.Korean, save: false);
                    var koreanTwoPointLineArtifact = new ToolWorkbenchArtifactProjection()
                        .Project(snapshot with
                        {
                            PipelineSteps = [twoPointLineStep],
                            TwoPointLineArtifactDetailFormat = ThreeDLocalization.Shared.TwoPointLineArtifactDetailFormat,
                            GetPublishedTwoPointLineOutput = outputId => string.Equals(
                                outputId,
                                twoPointLineOutput.OutputEntityId,
                                StringComparison.Ordinal)
                                ? twoPointLineOutput
                                : null
                        })
                        .Single(item => item.Id == twoPointLineOutput.OutputEntityId);
                    Check(
                        "two-point-line detail localizes ordered picks while preserving provenance, output identity, source, state, and node kind",
                        englishTwoPointLineArtifact.Detail.Contains("ordered picks (0, 1) -> (1, 2)", StringComparison.Ordinal)
                        && koreanTwoPointLineArtifact.Detail.Contains("선택 점 (0, 1) → (1, 2)", StringComparison.Ordinal)
                        && englishTwoPointLineArtifact.Detail.Contains("verification-two-point", StringComparison.Ordinal)
                        && koreanTwoPointLineArtifact.Detail.Contains("verification-two-point", StringComparison.Ordinal)
                        && englishTwoPointLineArtifact.Detail != koreanTwoPointLineArtifact.Detail
                        && englishTwoPointLineArtifact.Id == koreanTwoPointLineArtifact.Id
                        && englishTwoPointLineArtifact.ContentSha256 == koreanTwoPointLineArtifact.ContentSha256
                        && englishTwoPointLineArtifact.RootSourceId == source.Id
                        && koreanTwoPointLineArtifact.RootSourceId == source.Id
                        && englishTwoPointLineArtifact.InputEntityIds == "selection.two-point"
                        && koreanTwoPointLineArtifact.InputEntityIds == "selection.two-point"
                        && englishTwoPointLineArtifact.State == "Published"
                        && koreanTwoPointLineArtifact.State == "Published"
                        && englishTwoPointLineArtifact.NodeKind == "LineFeature"
                        && koreanTwoPointLineArtifact.NodeKind == "LineFeature",
                        $"en={englishTwoPointLineArtifact.Detail}; ko={koreanTwoPointLineArtifact.Detail}; id={koreanTwoPointLineArtifact.Id}; state={koreanTwoPointLineArtifact.State}; hash={koreanTwoPointLineArtifact.ContentSha256}");

                    var threePointPlaneOutput = C3DThreePointPlaneFeature.Create(
                        "derived.three-point-plane",
                        source.Id,
                        new string('R', 64),
                        source.Unit,
                        source.FrameId,
                        "selection.three-point",
                        new string('S', 64),
                        0,
                        0,
                        1,
                        1,
                        2,
                        0,
                        0,
                        1,
                        0,
                        0,
                        0,
                        1,
                        0,
                        1,
                        1,
                        0,
                        2,
                        0,
                        0,
                        1,
                        "verification-three-point",
                        "verification-three-point");
                    var threePointPlaneTool = new ToolWorkbenchToolItem(
                        "Measure",
                        "3-Point Plane",
                        "three-point-plane",
                        1,
                        "HeightField",
                        "PlaneFeature",
                        "Verification three-point plane.",
                        []);
                    var threePointPlaneStep = new ToolWorkbenchPipelineStepItem(
                        "step.three-point-plane",
                        threePointPlaneTool,
                        source.Id,
                        threePointPlaneOutput.OutputEntityId);
                    OpenVisionLanguageService.SetLanguage(OpenVisionLanguage.English, save: false);
                    var englishThreePointPlaneArtifact = new ToolWorkbenchArtifactProjection()
                        .Project(snapshot with
                        {
                            PipelineSteps = [threePointPlaneStep],
                            ThreePointPlaneArtifactDetailFormat = ThreeDLocalization.Shared.ThreePointPlaneArtifactDetailFormat,
                            GetPublishedThreePointPlaneOutput = outputId => string.Equals(
                                outputId,
                                threePointPlaneOutput.OutputEntityId,
                                StringComparison.Ordinal)
                                ? threePointPlaneOutput
                                : null
                        })
                        .Single(item => item.Id == threePointPlaneOutput.OutputEntityId);
                    OpenVisionLanguageService.SetLanguage(OpenVisionLanguage.Korean, save: false);
                    var koreanThreePointPlaneArtifact = new ToolWorkbenchArtifactProjection()
                        .Project(snapshot with
                        {
                            PipelineSteps = [threePointPlaneStep],
                            ThreePointPlaneArtifactDetailFormat = ThreeDLocalization.Shared.ThreePointPlaneArtifactDetailFormat,
                            GetPublishedThreePointPlaneOutput = outputId => string.Equals(
                                outputId,
                                threePointPlaneOutput.OutputEntityId,
                                StringComparison.Ordinal)
                                ? threePointPlaneOutput
                                : null
                        })
                        .Single(item => item.Id == threePointPlaneOutput.OutputEntityId);
                    Check(
                        "three-point-plane detail localizes ordered picks while preserving provenance, output identity, source, state, and node kind",
                        englishThreePointPlaneArtifact.Detail.Contains("ordered picks (0, 0) -> (1, 1) -> (2, 0)", StringComparison.Ordinal)
                        && koreanThreePointPlaneArtifact.Detail.Contains("선택 점 (0, 0) → (1, 1) → (2, 0)", StringComparison.Ordinal)
                        && englishThreePointPlaneArtifact.Detail.Contains("verification-three-point", StringComparison.Ordinal)
                        && koreanThreePointPlaneArtifact.Detail.Contains("verification-three-point", StringComparison.Ordinal)
                        && englishThreePointPlaneArtifact.Detail != koreanThreePointPlaneArtifact.Detail
                        && englishThreePointPlaneArtifact.Id == koreanThreePointPlaneArtifact.Id
                        && englishThreePointPlaneArtifact.ContentSha256 == koreanThreePointPlaneArtifact.ContentSha256
                        && englishThreePointPlaneArtifact.RootSourceId == source.Id
                        && koreanThreePointPlaneArtifact.RootSourceId == source.Id
                        && englishThreePointPlaneArtifact.InputEntityIds == "selection.three-point"
                        && koreanThreePointPlaneArtifact.InputEntityIds == "selection.three-point"
                        && englishThreePointPlaneArtifact.State == "Published"
                        && koreanThreePointPlaneArtifact.State == "Published"
                        && englishThreePointPlaneArtifact.NodeKind == "PlaneFeature"
                        && koreanThreePointPlaneArtifact.NodeKind == "PlaneFeature",
                        $"en={englishThreePointPlaneArtifact.Detail}; ko={koreanThreePointPlaneArtifact.Detail}; id={koreanThreePointPlaneArtifact.Id}; state={koreanThreePointPlaneArtifact.State}; hash={koreanThreePointPlaneArtifact.ContentSha256}");

                    var datumMeasurementSelection = new ToolRecipeSelection(
                        "selection.datum",
                        "Datum ROI",
                        ToolRecipeSelectionKinds.GridRectangle,
                        source.Id,
                        source.FrameId,
                        new ToolRecipeSelectionSourceBinding(
                            "C3D",
                            new string('R', 64),
                            2,
                            2),
                        new ToolRecipeGridRectangle(0, 0, 2, 2),
                        null,
                        null);
                    var datumDeviationOutput = C3DDatumPlaneDeviationFeature.Create(
                        "derived.datum-deviation",
                        threePointPlaneOutput,
                        datumMeasurementSelection,
                        1,
                        3,
                        0.1,
                        -0.1,
                        0.1,
                        0.2,
                        0.1,
                        4,
                        0,
                        0,
                        0,
                        1,
                        1,
                        ResultStatus.Pass,
                        "verification-datum",
                        [new C3DDatumPlaneDeviationOverlaySample(0, 0, 1, 0)],
                        "verification-datum");
                    var datumDeviationTool = new ToolWorkbenchToolItem(
                        "Measure",
                        "Datum Plane Deviation",
                        "datum-plane-raw-height-deviation",
                        1,
                        "PlaneFeature",
                        "DatumPlaneDeviationResult",
                        "Verification datum deviation.",
                        []);
                    var datumDeviationStep = new ToolWorkbenchPipelineStepItem(
                        "step.datum-deviation",
                        datumDeviationTool,
                        source.Id,
                        datumDeviationOutput.OutputEntityId);
                    OpenVisionLanguageService.SetLanguage(OpenVisionLanguage.English, save: false);
                    var englishDatumArtifact = new ToolWorkbenchArtifactProjection()
                        .Project(snapshot with
                        {
                            PipelineSteps = [datumDeviationStep],
                            DatumPlaneDeviationArtifactDetailFormat = ThreeDLocalization.Shared.DatumPlaneDeviationArtifactDetailFormat,
                            GetPublishedDatumPlaneDeviationOutput = outputId => string.Equals(
                                outputId,
                                datumDeviationOutput.OutputEntityId,
                                StringComparison.Ordinal)
                                ? datumDeviationOutput
                                : null
                        })
                        .Single(item => item.Id == datumDeviationOutput.OutputEntityId);
                    OpenVisionLanguageService.SetLanguage(OpenVisionLanguage.Korean, save: false);
                    var koreanDatumArtifact = new ToolWorkbenchArtifactProjection()
                        .Project(snapshot with
                        {
                            PipelineSteps = [datumDeviationStep],
                            DatumPlaneDeviationArtifactDetailFormat = ThreeDLocalization.Shared.DatumPlaneDeviationArtifactDetailFormat,
                            GetPublishedDatumPlaneDeviationOutput = outputId => string.Equals(
                                outputId,
                                datumDeviationOutput.OutputEntityId,
                                StringComparison.Ordinal)
                                ? datumDeviationOutput
                                : null
                        })
                        .Single(item => item.Id == datumDeviationOutput.OutputEntityId);
                    Check(
                        "datum-plane detail localizes raw-height labels while preserving output role, P2V value, samples, provenance, source, state, and node kind",
                        englishDatumArtifact.Detail.Contains("verification-datum", StringComparison.Ordinal)
                        && koreanDatumArtifact.Detail.Contains("verification-datum", StringComparison.Ordinal)
                        && englishDatumArtifact.Detail.Contains("P2V 0.2 raw-height", StringComparison.Ordinal)
                        && koreanDatumArtifact.Detail.Contains("P2V 0.2 원시 높이", StringComparison.Ordinal)
                        && englishDatumArtifact.Detail.Contains("4 samples", StringComparison.Ordinal)
                        && koreanDatumArtifact.Detail.Contains("유효 샘플 4개", StringComparison.Ordinal)
                        && englishDatumArtifact.Detail != koreanDatumArtifact.Detail
                        && englishDatumArtifact.Id == koreanDatumArtifact.Id
                        && englishDatumArtifact.ContentSha256 == koreanDatumArtifact.ContentSha256
                        && englishDatumArtifact.RootSourceId == source.Id
                        && koreanDatumArtifact.RootSourceId == source.Id
                        && englishDatumArtifact.InputEntityIds == "derived.three-point-plane; selection.datum"
                        && koreanDatumArtifact.InputEntityIds == "derived.three-point-plane; selection.datum"
                        && englishDatumArtifact.State == "Published"
                        && koreanDatumArtifact.State == "Published"
                        && englishDatumArtifact.NodeKind == "DatumPlaneDeviationResult"
                        && koreanDatumArtifact.NodeKind == "DatumPlaneDeviationResult",
                         $"en={englishDatumArtifact.Detail}; ko={koreanDatumArtifact.Detail}; id={koreanDatumArtifact.Id}; state={koreanDatumArtifact.State}; hash={koreanDatumArtifact.ContentSha256}");

                    var lineIntersectionOutput = C3DLineIntersectionFeature.Create(
                        "derived.line-intersection",
                        lineOutput,
                        twoPointLineOutput,
                        0.001,
                        45,
                        1.5,
                        "verification-corner-anchor",
                        0,
                        0,
                        0,
                        0,
                        0,
                        0,
                        0,
                        0,
                        0,
                        0,
                        0,
                        90,
                        0.25,
                        0,
                        1,
                        0,
                        0,
                        1,
                        0,
                        "verification-line-intersection");
                    var lineIntersectionTool = new ToolWorkbenchToolItem(
                        "Measure",
                        "Line Intersection",
                        "line-intersection",
                        2,
                        "LineFeature",
                        "CornerAnchor",
                        "Verification line intersection.",
                        []);
                    var lineIntersectionStep = new ToolWorkbenchPipelineStepItem(
                        "step.line-intersection",
                        lineIntersectionTool,
                        lineOutput.OutputEntityId,
                        lineIntersectionOutput.OutputEntityId);
                    OpenVisionLanguageService.SetLanguage(OpenVisionLanguage.English, save: false);
                    var englishLineIntersectionArtifact = new ToolWorkbenchArtifactProjection()
                        .Project(snapshot with
                        {
                            PipelineSteps = [lineIntersectionStep],
                            LineIntersectionArtifactDetailFormat = ThreeDLocalization.Shared.LineIntersectionArtifactDetailFormat,
                            GetPublishedLineIntersectionOutput = outputId => string.Equals(
                                outputId,
                                lineIntersectionOutput.OutputEntityId,
                                StringComparison.Ordinal)
                                ? lineIntersectionOutput
                                : null
                        })
                        .Single(item => item.Id == lineIntersectionOutput.OutputEntityId);
                    OpenVisionLanguageService.SetLanguage(OpenVisionLanguage.Korean, save: false);
                    var koreanLineIntersectionArtifact = new ToolWorkbenchArtifactProjection()
                        .Project(snapshot with
                        {
                            PipelineSteps = [lineIntersectionStep],
                            LineIntersectionArtifactDetailFormat = ThreeDLocalization.Shared.LineIntersectionArtifactDetailFormat,
                            GetPublishedLineIntersectionOutput = outputId => string.Equals(
                                outputId,
                                lineIntersectionOutput.OutputEntityId,
                                StringComparison.Ordinal)
                                ? lineIntersectionOutput
                                : null
                        })
                        .Single(item => item.Id == lineIntersectionOutput.OutputEntityId);
                    Check(
                        "line-intersection detail localizes gap and acute-angle labels while preserving output role, values, line identity, source, state, and node kind",
                        englishLineIntersectionArtifact.Detail.Contains("verification-corner-anchor | gap 0.25 | acute 90 degrees", StringComparison.Ordinal)
                        && koreanLineIntersectionArtifact.Detail.Contains("verification-corner-anchor | 간격 0.25 | 예각 90도", StringComparison.Ordinal)
                        && englishLineIntersectionArtifact.Detail != koreanLineIntersectionArtifact.Detail
                        && englishLineIntersectionArtifact.Id == koreanLineIntersectionArtifact.Id
                        && englishLineIntersectionArtifact.ContentSha256 == koreanLineIntersectionArtifact.ContentSha256
                        && englishLineIntersectionArtifact.RootSourceId == source.Id
                        && koreanLineIntersectionArtifact.RootSourceId == source.Id
                        && englishLineIntersectionArtifact.InputEntityIds == "derived.line; derived.two-point-line"
                        && koreanLineIntersectionArtifact.InputEntityIds == "derived.line; derived.two-point-line"
                        && englishLineIntersectionArtifact.State == "Published"
                        && koreanLineIntersectionArtifact.State == "Published"
                        && englishLineIntersectionArtifact.NodeKind == "CornerAnchor"
                        && koreanLineIntersectionArtifact.NodeKind == "CornerAnchor",
                         $"en={englishLineIntersectionArtifact.Detail}; ko={koreanLineIntersectionArtifact.Detail}; id={koreanLineIntersectionArtifact.Id}; state={koreanLineIntersectionArtifact.State}; hash={koreanLineIntersectionArtifact.ContentSha256}");

                    var correspondenceOutput = C3DLandmarkCorrespondenceSet.Create(
                        "derived.landmark-correspondence",
                        [
                            new C3DLandmarkCorrespondencePair("derived.corner-a", "CornerAnchor", new string('R', 64), 0, 0, 0, "reference.a", 10, 20, 30),
                            new C3DLandmarkCorrespondencePair("derived.corner-b", "CornerAnchor", new string('R', 64), 1, 0, 0, "reference.b", 11, 20, 30),
                            new C3DLandmarkCorrespondencePair("derived.corner-c", "CornerAnchor", new string('R', 64), 0, 1, 0, "reference.c", 10, 21, 30),
                            new C3DLandmarkCorrespondencePair("derived.corner-d", "CornerAnchor", new string('R', 64), 0, 0, 1, "reference.d", 10, 20, 31)
                        ],
                        source.Id,
                        new string('R', 64),
                        source.Unit,
                        source.FrameId,
                        "frame.reference",
                        "mm",
                        "verification-reference",
                        "R1",
                        1e-12,
                        4,
                        4,
                        0.1,
                        0.1,
                        "verification-correspondence");
                    var correspondenceTool = new ToolWorkbenchToolItem(
                        "Align",
                        "Landmark Correspondence",
                        "landmark-correspondence",
                        1,
                        "CornerAnchor",
                        "CorrespondenceSet",
                        "Verification landmark correspondence.",
                        []);
                    var correspondenceStep = new ToolWorkbenchPipelineStepItem(
                        "step.landmark-correspondence",
                        correspondenceTool,
                        string.Empty,
                        correspondenceOutput.OutputEntityId);
                    OpenVisionLanguageService.SetLanguage(OpenVisionLanguage.English, save: false);
                    var englishCorrespondenceArtifact = new ToolWorkbenchArtifactProjection()
                        .Project(snapshot with
                        {
                            PipelineSteps = [correspondenceStep],
                            LandmarkCorrespondenceArtifactDetailFormat = ThreeDLocalization.Shared.LandmarkCorrespondenceArtifactDetailFormat,
                            LandmarkCorrespondence = new ToolWorkbenchArtifactPreview<C3DLandmarkCorrespondenceSet>(
                                correspondenceOutput,
                                false,
                                true)
                        })
                        .Single(item => item.Id == correspondenceOutput.OutputEntityId);
                    OpenVisionLanguageService.SetLanguage(OpenVisionLanguage.Korean, save: false);
                    var koreanCorrespondenceArtifact = new ToolWorkbenchArtifactProjection()
                        .Project(snapshot with
                        {
                            PipelineSteps = [correspondenceStep],
                            LandmarkCorrespondenceArtifactDetailFormat = ThreeDLocalization.Shared.LandmarkCorrespondenceArtifactDetailFormat,
                            LandmarkCorrespondence = new ToolWorkbenchArtifactPreview<C3DLandmarkCorrespondenceSet>(
                                correspondenceOutput,
                                false,
                                true)
                        })
                        .Single(item => item.Id == correspondenceOutput.OutputEntityId);
                    Check(
                        "landmark-correspondence detail localizes pair and rank labels while preserving evidence, output identity, source, state, and node kind",
                        englishCorrespondenceArtifact.Detail.Contains("4/4 pairs | source rank 4/4 | reference rank 4/4 | correspondence evidence only", StringComparison.Ordinal)
                        && koreanCorrespondenceArtifact.Detail.Contains("쌍 4/4 | 소스 순위 4/4 | 참조 순위 4/4 | 대응 증거 전용", StringComparison.Ordinal)
                        && englishCorrespondenceArtifact.Detail != koreanCorrespondenceArtifact.Detail
                        && englishCorrespondenceArtifact.Id == koreanCorrespondenceArtifact.Id
                        && englishCorrespondenceArtifact.ContentSha256 == koreanCorrespondenceArtifact.ContentSha256
                        && englishCorrespondenceArtifact.RootSourceId == source.Id
                        && koreanCorrespondenceArtifact.RootSourceId == source.Id
                        && englishCorrespondenceArtifact.InputEntityIds == "derived.corner-a; derived.corner-b; derived.corner-c; derived.corner-d"
                        && koreanCorrespondenceArtifact.InputEntityIds == "derived.corner-a; derived.corner-b; derived.corner-c; derived.corner-d"
                        && englishCorrespondenceArtifact.State == "Published"
                        && koreanCorrespondenceArtifact.State == "Published"
                        && englishCorrespondenceArtifact.NodeKind == "CorrespondenceSet"
                        && koreanCorrespondenceArtifact.NodeKind == "CorrespondenceSet",
                         $"en={englishCorrespondenceArtifact.Detail}; ko={koreanCorrespondenceArtifact.Detail}; id={koreanCorrespondenceArtifact.Id}; state={koreanCorrespondenceArtifact.State}; hash={koreanCorrespondenceArtifact.ContentSha256}");

                    var affineCorrespondence = C3DLandmarkCorrespondenceSet.Create(
                        "derived.landmark-correspondence",
                        [],
                        source.Id,
                        new string('R', 64),
                        source.Unit,
                        source.FrameId,
                        "frame.reference",
                        "mm",
                        "verification-reference",
                        "R1",
                        1e-12,
                        4,
                        4,
                        0.1,
                        0.1,
                        "verification-correspondence");
                    var affineOutput = C3DAffineTransform3D.Create(
                        "derived.xyz-affine",
                        affineCorrespondence,
                        new C3DAffineMatrix3x4(
                            1,
                            0,
                            0,
                            10,
                            0,
                            1,
                            0,
                            20,
                            0,
                            0,
                            1,
                            30),
                        1,
                        1,
                        12.5,
                        100,
                        0.1,
                        0.25,
                        0.5,
                        [],
                        "verification-affine");
                    var affineTool = new ToolWorkbenchToolItem(
                        "Align",
                        "XYZ Affine Solve",
                        "xyz-affine-solve",
                        1,
                        "CorrespondenceSet",
                        "AffineTransform3D",
                        "Verification XYZ affine solve.",
                        []);
                    var affineStep = new ToolWorkbenchPipelineStepItem(
                        "step.xyz-affine-solve",
                        affineTool,
                        affineCorrespondence.OutputEntityId,
                        affineOutput.OutputEntityId);
                    OpenVisionLanguageService.SetLanguage(OpenVisionLanguage.English, save: false);
                    var englishAffineArtifact = new ToolWorkbenchArtifactProjection()
                        .Project(snapshot with
                        {
                            PipelineSteps = [affineStep],
                            XyzAffineSolveArtifactDetailFormat = ThreeDLocalization.Shared.XyzAffineSolveArtifactDetailFormat,
                            GetPublishedAffineSolveOutput = outputId => string.Equals(
                                outputId,
                                affineOutput.OutputEntityId,
                                StringComparison.Ordinal)
                                ? affineOutput
                                : null
                        })
                        .Single(item => item.Id == affineOutput.OutputEntityId);
                    OpenVisionLanguageService.SetLanguage(OpenVisionLanguage.Korean, save: false);
                    var koreanAffineArtifact = new ToolWorkbenchArtifactProjection()
                        .Project(snapshot with
                        {
                            PipelineSteps = [affineStep],
                            XyzAffineSolveArtifactDetailFormat = ThreeDLocalization.Shared.XyzAffineSolveArtifactDetailFormat,
                            GetPublishedAffineSolveOutput = outputId => string.Equals(
                                outputId,
                                affineOutput.OutputEntityId,
                                StringComparison.Ordinal)
                                ? affineOutput
                                : null
                        })
                        .Single(item => item.Id == affineOutput.OutputEntityId);
                    Check(
                        "xyz-affine-solve detail localizes condition and residual labels while preserving matrix evidence, output identity, correspondence source, state, and node kind",
                        englishAffineArtifact.Detail.Contains("condition 12.5 | max residual 0.25 | matrix evidence only", StringComparison.Ordinal)
                        && koreanAffineArtifact.Detail.Contains("조건수 12.5 | 최대 잔차 0.25 | 행렬 증거 전용", StringComparison.Ordinal)
                        && englishAffineArtifact.Detail != koreanAffineArtifact.Detail
                        && englishAffineArtifact.Id == koreanAffineArtifact.Id
                        && englishAffineArtifact.ContentSha256 == koreanAffineArtifact.ContentSha256
                        && englishAffineArtifact.RootSourceId == source.Id
                        && koreanAffineArtifact.RootSourceId == source.Id
                        && englishAffineArtifact.InputEntityIds == affineCorrespondence.OutputEntityId
                        && koreanAffineArtifact.InputEntityIds == affineCorrespondence.OutputEntityId
                        && englishAffineArtifact.State == "Published"
                        && koreanAffineArtifact.State == "Published"
                        && englishAffineArtifact.NodeKind == "AffineTransform3D"
                        && koreanAffineArtifact.NodeKind == "AffineTransform3D",
                         $"en={englishAffineArtifact.Detail}; ko={koreanAffineArtifact.Detail}; id={koreanAffineArtifact.Id}; state={koreanAffineArtifact.State}; hash={koreanAffineArtifact.ContentSha256}");

                    var affineApplyOutput = C3DTransformedPointCloud.Create(
                        "derived.xyz-affine-apply",
                        source.Id,
                        new string('R', 64),
                        source.Unit,
                        source.FrameId,
                        "column-rawHeight-row",
                        2,
                        2,
                        affineOutput,
                        [
                            new C3DTransformedPoint(0, 0, 1, 10, 20, 31),
                            new C3DTransformedPoint(0, 1, 2, 11, 20, 32)
                        ],
                        "verification-affine-apply");
                    var affineApplyTool = new ToolWorkbenchToolItem(
                        "Align",
                        "Apply XYZ Affine",
                        "xyz-affine-apply",
                        2,
                        "AffineTransform3D",
                        "TransformedPointCloud",
                        "Verification XYZ affine apply.",
                        []);
                    var affineApplyStep = new ToolWorkbenchPipelineStepItem(
                        "step.xyz-affine-apply",
                        affineApplyTool,
                        affineOutput.OutputEntityId,
                        affineApplyOutput.OutputEntityId);
                    OpenVisionLanguageService.SetLanguage(OpenVisionLanguage.English, save: false);
                    var englishAffineApplyArtifact = new ToolWorkbenchArtifactProjection()
                        .Project(snapshot with
                        {
                            PipelineSteps = [affineApplyStep],
                            XyzAffineApplyArtifactDetailFormat = ThreeDLocalization.Shared.XyzAffineApplyArtifactDetailFormat,
                            AffineApply = new ToolWorkbenchArtifactPreview<C3DTransformedPointCloud>(
                                affineApplyOutput,
                                false,
                                true)
                        })
                        .Single(item => item.Id == affineApplyOutput.OutputEntityId);
                    OpenVisionLanguageService.SetLanguage(OpenVisionLanguage.Korean, save: false);
                    var koreanAffineApplyArtifact = new ToolWorkbenchArtifactProjection()
                        .Project(snapshot with
                        {
                            PipelineSteps = [affineApplyStep],
                            XyzAffineApplyArtifactDetailFormat = ThreeDLocalization.Shared.XyzAffineApplyArtifactDetailFormat,
                            AffineApply = new ToolWorkbenchArtifactPreview<C3DTransformedPointCloud>(
                                affineApplyOutput,
                                false,
                                true)
                        })
                        .Single(item => item.Id == affineApplyOutput.OutputEntityId);
                    Check(
                        "xyz-affine-apply detail localizes transformed/missing labels while preserving A3 wording, output identity, affine source, state, and node kind",
                        englishAffineApplyArtifact.Detail.Contains("2 finite transformed points | 2 missing source cells | A3 re-grid excluded", StringComparison.Ordinal)
                        && koreanAffineApplyArtifact.Detail.Contains("변환 유효 점 2개 | 원본 누락 셀 2개 | A3 재격자화 제외", StringComparison.Ordinal)
                        && englishAffineApplyArtifact.Detail != koreanAffineApplyArtifact.Detail
                        && englishAffineApplyArtifact.Id == koreanAffineApplyArtifact.Id
                        && englishAffineApplyArtifact.ContentSha256 == koreanAffineApplyArtifact.ContentSha256
                        && englishAffineApplyArtifact.RootSourceId == source.Id
                        && koreanAffineApplyArtifact.RootSourceId == source.Id
                        && englishAffineApplyArtifact.InputEntityIds == affineOutput.OutputEntityId
                        && koreanAffineApplyArtifact.InputEntityIds == affineOutput.OutputEntityId
                        && englishAffineApplyArtifact.State == "Published"
                        && koreanAffineApplyArtifact.State == "Published"
                        && englishAffineApplyArtifact.NodeKind == "TransformedPointCloud"
                        && koreanAffineApplyArtifact.NodeKind == "TransformedPointCloud",
                        $"en={englishAffineApplyArtifact.Detail}; ko={koreanAffineApplyArtifact.Detail}; id={koreanAffineApplyArtifact.Id}; state={koreanAffineApplyArtifact.State}; hash={koreanAffineApplyArtifact.ContentSha256}");

                    var regridProfile = C3DReferenceGridProfile.Create(
                        affineOutput.ReferenceFrameId,
                        affineOutput.ReferenceUnit,
                        affineOutput.ReferenceProvenance,
                        affineOutput.ReferenceRevision,
                        new C3DReferenceGridVector(0, 0, 0),
                        new C3DReferenceGridVector(1, 0, 0),
                        new C3DReferenceGridVector(0, 1, 0),
                        new C3DReferenceGridVector(0, 0, 1),
                        1,
                        1,
                        2,
                        2,
                        0.5);
                    var regridOutput = C3DTransformedHeightField.Create(
                        "derived.regrid-height-map",
                        affineApplyOutput,
                        regridProfile,
                        [
                            new C3DTransformedHeightCell(0, 0, 31, 0, 0, 0),
                            new C3DTransformedHeightCell(0, 1, 32, 0, 1, 0.25),
                            new C3DTransformedHeightCell(1, 0, double.NaN, -1, -1, double.NaN),
                            new C3DTransformedHeightCell(1, 1, double.NaN, -1, -1, double.NaN)
                        ],
                        1,
                        "verification-regrid");
                    var regridTool = new ToolWorkbenchToolItem(
                        "Alignment",
                        "Re-grid Height Map",
                        "re-grid-height-map",
                        3,
                        "TransformedPointCloud",
                        "TransformedHeightField",
                        "Verification re-grid.",
                        []);
                    var regridStep = new ToolWorkbenchPipelineStepItem(
                        "step.re-grid-height-map",
                        regridTool,
                        affineApplyOutput.OutputEntityId,
                        regridOutput.OutputEntityId);
                    OpenVisionLanguageService.SetLanguage(OpenVisionLanguage.English, save: false);
                    var englishRegridArtifact = new ToolWorkbenchArtifactProjection()
                        .Project(snapshot with
                        {
                            PipelineSteps = [regridStep],
                            RegridHeightFieldArtifactDetailFormat = ThreeDLocalization.Shared.RegridHeightFieldArtifactDetailFormat,
                            RegridHeightField = new ToolWorkbenchArtifactPreview<C3DTransformedHeightField>(
                                regridOutput,
                                false,
                                true)
                        })
                        .Single(item => item.Id == regridOutput.OutputEntityId);
                    OpenVisionLanguageService.SetLanguage(OpenVisionLanguage.Korean, save: false);
                    var koreanRegridArtifact = new ToolWorkbenchArtifactProjection()
                        .Project(snapshot with
                        {
                            PipelineSteps = [regridStep],
                            RegridHeightFieldArtifactDetailFormat = ThreeDLocalization.Shared.RegridHeightFieldArtifactDetailFormat,
                            RegridHeightField = new ToolWorkbenchArtifactPreview<C3DTransformedHeightField>(
                                regridOutput,
                                false,
                                true)
                        })
                        .Single(item => item.Id == regridOutput.OutputEntityId);
                    Check(
                        "re-grid height-map detail localizes populated/coverage/missing/collision labels while preserving output, transform source, state, and node kind",
                        englishRegridArtifact.Detail.Contains("2/4 populated", StringComparison.Ordinal)
                        && englishRegridArtifact.Detail.Contains("coverage 50.00", StringComparison.Ordinal)
                        && englishRegridArtifact.Detail.Contains("missing 2", StringComparison.Ordinal)
                        && englishRegridArtifact.Detail.Contains("collisions 1", StringComparison.Ordinal)
                        && koreanRegridArtifact.Detail.Contains("채워진 셀 2/4", StringComparison.Ordinal)
                        && koreanRegridArtifact.Detail.Contains("커버리지 50.00", StringComparison.Ordinal)
                        && koreanRegridArtifact.Detail.Contains("누락 2개", StringComparison.Ordinal)
                        && koreanRegridArtifact.Detail.Contains("충돌 1개", StringComparison.Ordinal)
                        && englishRegridArtifact.Detail != koreanRegridArtifact.Detail
                        && englishRegridArtifact.Id == koreanRegridArtifact.Id
                        && englishRegridArtifact.ContentSha256 == koreanRegridArtifact.ContentSha256
                        && englishRegridArtifact.RootSourceId == source.Id
                        && koreanRegridArtifact.RootSourceId == source.Id
                        && englishRegridArtifact.InputEntityIds == affineOutput.OutputEntityId
                        && koreanRegridArtifact.InputEntityIds == affineOutput.OutputEntityId
                        && englishRegridArtifact.Unit == regridOutput.ReferenceUnit
                        && koreanRegridArtifact.FrameId == regridOutput.ReferenceFrameId
                        && englishRegridArtifact.State == "Published"
                        && koreanRegridArtifact.State == "Published"
                        && englishRegridArtifact.NodeKind == "TransformedHeightField"
                        && koreanRegridArtifact.NodeKind == "TransformedHeightField",
                        $"en={englishRegridArtifact.Detail}; ko={koreanRegridArtifact.Detail}; id={koreanRegridArtifact.Id}; state={koreanRegridArtifact.State}; hash={koreanRegridArtifact.ContentSha256}");

                    var measurementOutput = new ToolRecipeHeightMeasurementOutput(
                        "derived.thickness",
                        source.Id,
                        source.Id,
                        "selection.thickness",
                        source.Unit,
                        source.FrameId,
                        new string('M', 64),
                        new ToolResult(
                            "Thickness",
                            ResultStatus.Pass,
                            "Verification measurement.",
                            TimeSpan.Zero,
                            [],
                            []),
                        "H-axis thickness mean 12.5 | min 10 | max 15 | reference 4 | measurement 4 finite samples");
                    var measurementTool = new ToolWorkbenchToolItem(
                        "Measurement",
                        "Thickness",
                        "thickness",
                        4,
                        "HeightField",
                        "MeasurementResult",
                        "Verification measurement.",
                        []);
                    var measurementStep = new ToolWorkbenchPipelineStepItem(
                        "step.thickness",
                        measurementTool,
                        source.Id,
                        measurementOutput.OutputEntityId);
                    OpenVisionLanguageService.SetLanguage(OpenVisionLanguage.English, save: false);
                    var englishMeasurementArtifact = new ToolWorkbenchArtifactProjection()
                        .Project(snapshot with
                        {
                            PipelineSteps = [measurementStep],
                            MeasurementArtifactDetailFormat = ThreeDLocalization.Shared.MeasurementArtifactDetailFormat,
                            ResultStatusLabel = status => status == ResultStatus.Pass
                                ? ThreeDLocalization.Shared.ValidationSetFilterPass
                                : status.ToString(),
                            Measurement = new ToolWorkbenchArtifactPreview<ToolRecipeHeightMeasurementOutput>(
                                measurementOutput,
                                false,
                                true)
                        })
                        .Single(item => item.Id == measurementOutput.OutputEntityId);
                    OpenVisionLanguageService.SetLanguage(OpenVisionLanguage.Korean, save: false);
                    var koreanMeasurementArtifact = new ToolWorkbenchArtifactProjection()
                        .Project(snapshot with
                        {
                            PipelineSteps = [measurementStep],
                            MeasurementArtifactDetailFormat = ThreeDLocalization.Shared.MeasurementArtifactDetailFormat,
                            ResultStatusLabel = status => status == ResultStatus.Pass
                                ? ThreeDLocalization.Shared.ValidationSetFilterPass
                                : status.ToString(),
                            Measurement = new ToolWorkbenchArtifactPreview<ToolRecipeHeightMeasurementOutput>(
                                measurementOutput,
                                false,
                                true)
                        })
                        .Single(item => item.Id == measurementOutput.OutputEntityId);
                    Check(
                        "measurement detail localizes result status while preserving evidence values, output identity, source, state, and node kind",
                        englishMeasurementArtifact.Detail.StartsWith("Pass | H-axis thickness mean 12.5", StringComparison.Ordinal)
                        && koreanMeasurementArtifact.Detail.StartsWith("통과 | H-axis thickness mean 12.5", StringComparison.Ordinal)
                        && englishMeasurementArtifact.Detail.Contains("reference 4 | measurement 4 finite samples", StringComparison.Ordinal)
                        && koreanMeasurementArtifact.Detail.Contains("reference 4 | measurement 4 finite samples", StringComparison.Ordinal)
                        && englishMeasurementArtifact.Detail != koreanMeasurementArtifact.Detail
                        && englishMeasurementArtifact.Id == koreanMeasurementArtifact.Id
                        && englishMeasurementArtifact.ContentSha256 == koreanMeasurementArtifact.ContentSha256
                        && englishMeasurementArtifact.RootSourceId == source.Id
                        && koreanMeasurementArtifact.RootSourceId == source.Id
                        && englishMeasurementArtifact.InputEntityIds == $"{source.Id}; selection.thickness"
                        && koreanMeasurementArtifact.InputEntityIds == $"{source.Id}; selection.thickness"
                        && englishMeasurementArtifact.Unit == measurementOutput.Unit
                        && koreanMeasurementArtifact.FrameId == measurementOutput.FrameId
                        && englishMeasurementArtifact.State == "Published"
                        && koreanMeasurementArtifact.State == "Published"
                        && englishMeasurementArtifact.NodeKind == "MeasurementResult"
                        && koreanMeasurementArtifact.NodeKind == "MeasurementResult",
                        $"en={englishMeasurementArtifact.Detail}; ko={koreanMeasurementArtifact.Detail}; id={koreanMeasurementArtifact.Id}; state={koreanMeasurementArtifact.State}; hash={koreanMeasurementArtifact.ContentSha256}");

                    OpenVisionLanguageService.SetLanguage(OpenVisionLanguage.English, save: false);
                    var englishDeclaredArtifact = new ToolWorkbenchArtifactProjection()
                        .Project(snapshot with
                        {
                            PipelineSteps = [qualityDeltaStep],
                            DeclaredOutputDetailFormat = ThreeDLocalization.Shared.FlowPortDeclaredDetailFormat,
                            Filter = new ToolWorkbenchArtifactPreview<C3DHeightFieldSnapshot>(
                                null,
                                false,
                                false)
                        })
                        .Single(item => item.Id == qualityDeltaStep.OutputEntityId);
                    var englishDeclaredDetail = englishDeclaredArtifact.Detail;
                    OpenVisionLanguageService.SetLanguage(OpenVisionLanguage.Korean, save: false);
                    var koreanDeclaredArtifact = new ToolWorkbenchArtifactProjection()
                        .Project(snapshot with
                        {
                            PipelineSteps = [qualityDeltaStep],
                            DeclaredOutputDetailFormat = ThreeDLocalization.Shared.FlowPortDeclaredDetailFormat,
                            Filter = new ToolWorkbenchArtifactPreview<C3DHeightFieldSnapshot>(
                                null,
                                false,
                                false)
                        })
                        .Single(item => item.Id == qualityDeltaStep.OutputEntityId);
                    var koreanDeclaredDetail = koreanDeclaredArtifact.Detail;
                    Check(
                        "declared artifact fallback localizes while preserving output identity and state",
                        englishDeclaredDetail == "Typed output 'derived.quality-delta' is declared, but has no current Preview or Published evidence."
                        && koreanDeclaredDetail == "정식 출력 'derived.quality-delta'이(가) 선언되었지만 현재 Preview/Published 증거는 없습니다."
                        && englishDeclaredDetail != koreanDeclaredDetail
                        && englishDeclaredArtifact.Id == koreanDeclaredArtifact.Id
                        && englishDeclaredArtifact.State == "Declared"
                        && koreanDeclaredArtifact.State == "Declared"
                        && englishDeclaredArtifact.NodeKind == "DeclaredOutput"
                        && koreanDeclaredArtifact.NodeKind == "DeclaredOutput",
                        $"en={englishDeclaredDetail}; ko={koreanDeclaredDetail}; id={koreanDeclaredArtifact.Id}; state={koreanDeclaredArtifact.State}");

                    var disabledQualityDeltaStep = new ToolWorkbenchPipelineStepItem(
                        "step.disabled-output",
                        qualityDeltaTool,
                        source.Id,
                        "derived.disabled-output",
                        outputEnabled: false);
                    OpenVisionLanguageService.SetLanguage(OpenVisionLanguage.English, save: false);
                    var englishDisabledArtifact = new ToolWorkbenchArtifactProjection()
                        .Project(snapshot with
                        {
                            PipelineSteps = [disabledQualityDeltaStep],
                            OutputDisabledDetailFormat = ThreeDLocalization.Shared.SelectedToolOutputDisabledDetailFormat
                        })
                        .Single(item => item.Id == disabledQualityDeltaStep.OutputEntityId);
                    var englishDisabledDetail = englishDisabledArtifact.Detail;
                    OpenVisionLanguageService.SetLanguage(OpenVisionLanguage.Korean, save: false);
                    var koreanDisabledArtifact = new ToolWorkbenchArtifactProjection()
                        .Project(snapshot with
                        {
                            PipelineSteps = [disabledQualityDeltaStep],
                            OutputDisabledDetailFormat = ThreeDLocalization.Shared.SelectedToolOutputDisabledDetailFormat
                        })
                        .Single(item => item.Id == disabledQualityDeltaStep.OutputEntityId);
                    var koreanDisabledDetail = koreanDisabledArtifact.Detail;
                    Check(
                        "disabled artifact detail localizes while preserving policy state and step identity",
                        englishDisabledDetail == "Step 'step.disabled-output' disabled output policy. No Preview, Run output, or evidence is fabricated."
                        && koreanDisabledDetail == "단계 'step.disabled-output'의 출력 정책이 비활성화했습니다. Preview, Run 출력 또는 증거를 만들지 않습니다."
                        && englishDisabledDetail != koreanDisabledDetail
                        && englishDisabledArtifact.Id == koreanDisabledArtifact.Id
                        && englishDisabledArtifact.State == "Disabled"
                        && koreanDisabledArtifact.State == "Disabled"
                        && englishDisabledArtifact.NodeKind == "DisabledOutput"
                        && koreanDisabledArtifact.NodeKind == "DisabledOutput",
                        $"en={englishDisabledDetail}; ko={koreanDisabledDetail}; id={koreanDisabledArtifact.Id}; state={koreanDisabledArtifact.State}");

                    var currentSelection = new ToolRecipeSelection(
                        "selection.current",
                        "Current selection",
                        ToolRecipeSelectionKinds.GridRectangle,
                        source.Id,
                        source.FrameId,
                        new ToolRecipeSelectionSourceBinding(
                            source.Format,
                            string.Empty,
                            2,
                            2),
                        new ToolRecipeGridRectangle(0, 0, 1, 1),
                        null,
                        null);
                    OpenVisionLanguageService.SetLanguage(OpenVisionLanguage.English, save: false);
                    var englishSelectionArtifact = new ToolWorkbenchArtifactProjection()
                        .Project(snapshot with
                        {
                            Selections = [currentSelection],
                            IsSelectionCurrent = _ => true,
                            CurrentSelectionDetail = ThreeDLocalization.Shared.RecipeOwnedSelection
                        })
                        .Single(item => item.Id == currentSelection.Id);
                    var englishSelectionDetail = englishSelectionArtifact.Detail;
                    OpenVisionLanguageService.SetLanguage(OpenVisionLanguage.Korean, save: false);
                    var koreanSelectionArtifact = new ToolWorkbenchArtifactProjection()
                        .Project(snapshot with
                        {
                            Selections = [currentSelection],
                            IsSelectionCurrent = _ => true,
                            CurrentSelectionDetail = ThreeDLocalization.Shared.RecipeOwnedSelection
                        })
                        .Single(item => item.Id == currentSelection.Id);
                    var koreanSelectionDetail = koreanSelectionArtifact.Detail;
                    Check(
                        "current selection detail localizes while preserving selection identity and state",
                        englishSelectionDetail == "Recipe-owned ROI / selection"
                        && koreanSelectionDetail == "레시피에 저장된 ROI / 선택"
                        && englishSelectionDetail != koreanSelectionDetail
                        && englishSelectionArtifact.Id == koreanSelectionArtifact.Id
                        && englishSelectionArtifact.State == "Current selection"
                        && koreanSelectionArtifact.State == "Current selection"
                        && englishSelectionArtifact.RootSourceId == source.Id
                        && koreanSelectionArtifact.RootSourceId == source.Id
                        && englishSelectionArtifact.NodeKind == "Selection"
                        && koreanSelectionArtifact.NodeKind == "Selection",
                        $"en={englishSelectionDetail}; ko={koreanSelectionDetail}; id={koreanSelectionArtifact.Id}; state={koreanSelectionArtifact.State}; root={koreanSelectionArtifact.RootSourceId}");

                    OpenVisionLanguageService.SetLanguage(OpenVisionLanguage.English, save: false);
                    var englishStaleArtifact = new ToolWorkbenchArtifactProjection()
                        .Project(snapshot with
                        {
                            Selections = [currentSelection],
                            IsSelectionCurrent = _ => false,
                            StaleSelectionDetail = ThreeDLocalization.Shared.StaleSelectionRecaptureDetail
                        })
                        .Single(item => item.Id == currentSelection.Id);
                    var englishStaleDetail = englishStaleArtifact.Detail;
                    OpenVisionLanguageService.SetLanguage(OpenVisionLanguage.Korean, save: false);
                    var koreanStaleArtifact = new ToolWorkbenchArtifactProjection()
                        .Project(snapshot with
                        {
                            Selections = [currentSelection],
                            IsSelectionCurrent = _ => false,
                            StaleSelectionDetail = ThreeDLocalization.Shared.StaleSelectionRecaptureDetail
                        })
                        .Single(item => item.Id == currentSelection.Id);
                    var koreanStaleDetail = koreanStaleArtifact.Detail;
                    Check(
                        "stale selection recapture detail localizes while preserving identity and state",
                        englishStaleDetail == "Recapture is required because the source binding changed."
                        && koreanStaleDetail == "소스 바인딩이 변경되어 선택 영역을 다시 지정해야 합니다."
                        && englishStaleDetail != koreanStaleDetail
                        && englishStaleArtifact.Id == koreanStaleArtifact.Id
                        && englishStaleArtifact.State == "Stale"
                        && koreanStaleArtifact.State == "Stale"
                        && englishStaleArtifact.RootSourceId == source.Id
                        && koreanStaleArtifact.RootSourceId == source.Id
                        && englishStaleArtifact.ContentSha256 == koreanStaleArtifact.ContentSha256
                        && englishStaleArtifact.NodeKind == "Selection"
                        && koreanStaleArtifact.NodeKind == "Selection",
                        $"en={englishStaleDetail}; ko={koreanStaleDetail}; id={koreanStaleArtifact.Id}; state={koreanStaleArtifact.State}; root={koreanStaleArtifact.RootSourceId}; hash={koreanStaleArtifact.ContentSha256}");

                    var roiCropTool = new ToolWorkbenchToolItem(
                        "Prepare",
                        "ROI / Crop",
                        "roi-crop",
                        1,
                        "HeightField",
                        "HeightField",
                        "Verification ROI / Crop.",
                        []);
                    var roiCropStep = new ToolWorkbenchPipelineStepItem(
                        "step.roi-crop",
                        roiCropTool,
                        source.Id,
                        qualityDeltaOutput.EntityId);
                    OpenVisionLanguageService.SetLanguage(OpenVisionLanguage.English, save: false);
                    var englishRoiCropArtifact = new ToolWorkbenchArtifactProjection()
                        .Project(snapshot with
                        {
                            PipelineSteps = [roiCropStep],
                            CreateSourceQualityDelta = (_, _, _) => null,
                            QualityDeltaUnavailable = ThreeDLocalization.Shared.OutputCompareQualityDeltaUnavailable,
                            RoiCropArtifactDetailFormat = ThreeDLocalization.Shared.RoiCropArtifactDetailFormat,
                            SourceIdentityRetainedDetail = ThreeDLocalization.Shared.OutputCompareSourceIdentityRetained,
                            RoiCrop = new ToolWorkbenchArtifactPreview<C3DHeightFieldSnapshot>(qualityDeltaOutput, false, false)
                        })
                        .Single(item => item.Id == qualityDeltaOutput.EntityId);
                    OpenVisionLanguageService.SetLanguage(OpenVisionLanguage.Korean, save: false);
                    var koreanRoiCropArtifact = new ToolWorkbenchArtifactProjection()
                        .Project(snapshot with
                        {
                            PipelineSteps = [roiCropStep],
                            CreateSourceQualityDelta = (_, _, _) => null,
                            QualityDeltaUnavailable = ThreeDLocalization.Shared.OutputCompareQualityDeltaUnavailable,
                            RoiCropArtifactDetailFormat = ThreeDLocalization.Shared.RoiCropArtifactDetailFormat,
                            SourceIdentityRetainedDetail = ThreeDLocalization.Shared.OutputCompareSourceIdentityRetained,
                            RoiCrop = new ToolWorkbenchArtifactPreview<C3DHeightFieldSnapshot>(qualityDeltaOutput, false, false)
                        })
                        .Single(item => item.Id == qualityDeltaOutput.EntityId);
                    Check(
                        "roi-crop detail localizes while preserving output, origin, valid/missing counts, and source evidence",
                        englishRoiCropArtifact.Detail.Contains("2 × 2", StringComparison.Ordinal)
                        && koreanRoiCropArtifact.Detail.Contains("2 × 2", StringComparison.Ordinal)
                        && englishRoiCropArtifact.Detail.Contains("source origin (0, 0)", StringComparison.Ordinal)
                        && koreanRoiCropArtifact.Detail.Contains("원본 위치 (0, 0)", StringComparison.Ordinal)
                        && englishRoiCropArtifact.Detail.Contains("valid 4", StringComparison.Ordinal)
                        && koreanRoiCropArtifact.Detail.Contains("유효 4개", StringComparison.Ordinal)
                        && englishRoiCropArtifact.Detail.Contains("missing 0", StringComparison.Ordinal)
                        && koreanRoiCropArtifact.Detail.Contains("누락 0개", StringComparison.Ordinal)
                        && englishRoiCropArtifact.Detail.Contains("source identity retained", StringComparison.Ordinal)
                        && koreanRoiCropArtifact.Detail.Contains("원본 ID 유지", StringComparison.Ordinal)
                        && englishRoiCropArtifact.Detail.Contains("Quality delta unavailable", StringComparison.Ordinal)
                        && koreanRoiCropArtifact.Detail.Contains("품질 변화 정보 없음", StringComparison.Ordinal)
                        && englishRoiCropArtifact.Detail != koreanRoiCropArtifact.Detail
                        && englishRoiCropArtifact.Id == koreanRoiCropArtifact.Id
                        && englishRoiCropArtifact.ContentSha256 == koreanRoiCropArtifact.ContentSha256
                        && englishRoiCropArtifact.State == "Preview"
                        && koreanRoiCropArtifact.State == "Preview"
                        && englishRoiCropArtifact.NodeKind == "HeightField"
                        && koreanRoiCropArtifact.NodeKind == "HeightField"
                        && englishRoiCropArtifact.RootSourceId == source.Id
                        && koreanRoiCropArtifact.RootSourceId == source.Id
                        && englishRoiCropArtifact.InputEntityIds == source.Id
                        && koreanRoiCropArtifact.InputEntityIds == source.Id,
                        $"en={englishRoiCropArtifact.Detail}; ko={koreanRoiCropArtifact.Detail}; id={koreanRoiCropArtifact.Id}; state={koreanRoiCropArtifact.State}; hash={koreanRoiCropArtifact.ContentSha256}");

                    var roiCropQualityDeltaFactory = (string evidence) => new SourceQualityDelta(
                        source.Id,
                        qualityDeltaOutput.ContentSha256,
                        qualityDeltaOutput.EntityId,
                        qualityDeltaOutput.ContentSha256,
                        qualityDeltaOutput.RootSourceSha256,
                        qualityDeltaOutput.RootSourceSha256,
                        qualityDeltaOutput.ValidCount,
                        qualityDeltaOutput.ValidCount,
                        qualityDeltaOutput.MissingCount,
                        qualityDeltaOutput.MissingCount,
                        null,
                        evidence);
                    OpenVisionLanguageService.SetLanguage(OpenVisionLanguage.English, save: false);
                    var englishRoiCropQualityDeltaEvidence = ThreeDLocalization.Shared.RoiCropQualityDeltaEvidence;
                    var englishRoiCropQualityDeltaArtifact = new ToolWorkbenchArtifactProjection()
                        .Project(snapshot with
                        {
                            PipelineSteps = [roiCropStep],
                            CreateSourceQualityDelta = (_, _, evidence) => roiCropQualityDeltaFactory(evidence),
                            RoiCropQualityDeltaEvidence = englishRoiCropQualityDeltaEvidence,
                            RoiCrop = new ToolWorkbenchArtifactPreview<C3DHeightFieldSnapshot>(qualityDeltaOutput, false, false)
                        })
                        .Single(item => item.Id == qualityDeltaOutput.EntityId);
                    OpenVisionLanguageService.SetLanguage(OpenVisionLanguage.Korean, save: false);
                    var koreanRoiCropQualityDeltaEvidence = ThreeDLocalization.Shared.RoiCropQualityDeltaEvidence;
                    var koreanRoiCropQualityDeltaArtifact = new ToolWorkbenchArtifactProjection()
                        .Project(snapshot with
                        {
                            PipelineSteps = [roiCropStep],
                            CreateSourceQualityDelta = (_, _, evidence) => roiCropQualityDeltaFactory(evidence),
                            RoiCropQualityDeltaEvidence = koreanRoiCropQualityDeltaEvidence,
                            RoiCrop = new ToolWorkbenchArtifactPreview<C3DHeightFieldSnapshot>(qualityDeltaOutput, false, false)
                        })
                        .Single(item => item.Id == qualityDeltaOutput.EntityId);
                    Check(
                        "roi-crop quality delta evidence localizes while preserving counts, hashes, root, output identity, state, and node kind",
                        englishRoiCropQualityDeltaArtifact.PreparationQualityDelta?.Summary.Contains(
                            englishRoiCropQualityDeltaEvidence,
                            StringComparison.Ordinal) == true
                        && koreanRoiCropQualityDeltaArtifact.PreparationQualityDelta?.Summary.Contains(
                            koreanRoiCropQualityDeltaEvidence,
                            StringComparison.Ordinal) == true
                        && englishRoiCropQualityDeltaEvidence != koreanRoiCropQualityDeltaEvidence
                        && englishRoiCropQualityDeltaArtifact.PreparationQualityDelta?.BeforeValidSampleCount == qualityDeltaOutput.ValidCount
                        && koreanRoiCropQualityDeltaArtifact.PreparationQualityDelta?.AfterValidSampleCount == qualityDeltaOutput.ValidCount
                        && englishRoiCropQualityDeltaArtifact.PreparationQualityDelta?.BeforeMissingSampleCount == qualityDeltaOutput.MissingCount
                        && koreanRoiCropQualityDeltaArtifact.PreparationQualityDelta?.AfterMissingSampleCount == qualityDeltaOutput.MissingCount
                        && englishRoiCropQualityDeltaArtifact.PreparationQualityDelta?.SourceContentSha256 == qualityDeltaOutput.ContentSha256
                        && koreanRoiCropQualityDeltaArtifact.PreparationQualityDelta?.DerivedContentSha256 == qualityDeltaOutput.ContentSha256
                        && englishRoiCropQualityDeltaArtifact.PreparationQualityDelta?.SourceIdentityRetained == true
                        && koreanRoiCropQualityDeltaArtifact.PreparationQualityDelta?.SourceRootSourceSha256 == qualityDeltaOutput.RootSourceSha256
                        && koreanRoiCropQualityDeltaArtifact.PreparationQualityDelta?.DerivedRootSourceSha256 == qualityDeltaOutput.RootSourceSha256
                        && englishRoiCropQualityDeltaArtifact.Id == koreanRoiCropQualityDeltaArtifact.Id
                        && englishRoiCropQualityDeltaArtifact.ContentSha256 == koreanRoiCropQualityDeltaArtifact.ContentSha256
                        && englishRoiCropQualityDeltaArtifact.RootSourceId == source.Id
                        && koreanRoiCropQualityDeltaArtifact.RootSourceId == source.Id
                        && englishRoiCropQualityDeltaArtifact.InputEntityIds == source.Id
                        && koreanRoiCropQualityDeltaArtifact.InputEntityIds == source.Id
                        && englishRoiCropQualityDeltaArtifact.Unit == qualityDeltaOutput.Unit
                        && koreanRoiCropQualityDeltaArtifact.FrameId == qualityDeltaOutput.FrameId
                        && englishRoiCropQualityDeltaArtifact.State == "Preview"
                        && koreanRoiCropQualityDeltaArtifact.State == "Preview"
                        && englishRoiCropQualityDeltaArtifact.NodeKind == "HeightField"
                        && koreanRoiCropQualityDeltaArtifact.NodeKind == "HeightField",
                        $"en={englishRoiCropQualityDeltaArtifact.Detail}; ko={koreanRoiCropQualityDeltaArtifact.Detail}; enSummary={englishRoiCropQualityDeltaArtifact.PreparationQualityDelta?.Summary}; koSummary={koreanRoiCropQualityDeltaArtifact.PreparationQualityDelta?.Summary}");

                    var domainMaskTool = new ToolWorkbenchToolItem(
                        "Prepare",
                        "Domain / Mask",
                        "domain-mask",
                        1,
                        "HeightField",
                        "HeightField",
                        "Verification Domain / Mask.",
                        []);
                    var domainMaskStep = new ToolWorkbenchPipelineStepItem(
                        "step.domain-mask",
                        domainMaskTool,
                        source.Id,
                        qualityDeltaOutput.EntityId);
                    OpenVisionLanguageService.SetLanguage(OpenVisionLanguage.English, save: false);
                    var englishDomainMaskArtifact = new ToolWorkbenchArtifactProjection()
                        .Project(snapshot with
                        {
                            PipelineSteps = [domainMaskStep],
                            CreateSourceQualityDelta = (_, _, _) => null,
                            QualityDeltaUnavailable = ThreeDLocalization.Shared.OutputCompareQualityDeltaUnavailable,
                            DomainMaskReducedDetail = ThreeDLocalization.Shared.DomainMaskReducedDetail,
                            DomainMaskArtifactDetailFormat = ThreeDLocalization.Shared.DomainMaskArtifactDetailFormat,
                            SourceIdentityRetainedDetail = ThreeDLocalization.Shared.OutputCompareSourceIdentityRetained,
                            DomainMask = new ToolWorkbenchArtifactPreview<C3DHeightFieldSnapshot>(qualityDeltaOutput, false, false)
                        })
                        .Single(item => item.Id == qualityDeltaOutput.EntityId);
                    OpenVisionLanguageService.SetLanguage(OpenVisionLanguage.Korean, save: false);
                    var koreanDomainMaskArtifact = new ToolWorkbenchArtifactProjection()
                        .Project(snapshot with
                        {
                            PipelineSteps = [domainMaskStep],
                            CreateSourceQualityDelta = (_, _, _) => null,
                            QualityDeltaUnavailable = ThreeDLocalization.Shared.OutputCompareQualityDeltaUnavailable,
                            DomainMaskReducedDetail = ThreeDLocalization.Shared.DomainMaskReducedDetail,
                            DomainMaskArtifactDetailFormat = ThreeDLocalization.Shared.DomainMaskArtifactDetailFormat,
                            SourceIdentityRetainedDetail = ThreeDLocalization.Shared.OutputCompareSourceIdentityRetained,
                            DomainMask = new ToolWorkbenchArtifactPreview<C3DHeightFieldSnapshot>(qualityDeltaOutput, false, false)
                        })
                        .Single(item => item.Id == qualityDeltaOutput.EntityId);
                    Check(
                        "domain-mask detail localizes while preserving reduction evidence, output identity, grid, valid/missing counts, and source quality",
                        englishDomainMaskArtifact.Detail.Contains("domain-reduced", StringComparison.Ordinal)
                        && koreanDomainMaskArtifact.Detail.Contains("도메인으로 축소됨", StringComparison.Ordinal)
                        && englishDomainMaskArtifact.Detail.Contains("valid 4", StringComparison.Ordinal)
                        && koreanDomainMaskArtifact.Detail.Contains("유효 4개", StringComparison.Ordinal)
                        && englishDomainMaskArtifact.Detail.Contains("missing 0", StringComparison.Ordinal)
                        && koreanDomainMaskArtifact.Detail.Contains("누락 0개", StringComparison.Ordinal)
                        && englishDomainMaskArtifact.Detail.Contains("source identity retained", StringComparison.Ordinal)
                        && koreanDomainMaskArtifact.Detail.Contains("원본 ID 유지", StringComparison.Ordinal)
                        && englishDomainMaskArtifact.Detail.Contains("Quality delta unavailable", StringComparison.Ordinal)
                        && koreanDomainMaskArtifact.Detail.Contains("품질 변화 정보 없음", StringComparison.Ordinal)
                        && englishDomainMaskArtifact.Detail != koreanDomainMaskArtifact.Detail
                        && englishDomainMaskArtifact.Id == koreanDomainMaskArtifact.Id
                        && englishDomainMaskArtifact.ContentSha256 == koreanDomainMaskArtifact.ContentSha256
                        && englishDomainMaskArtifact.State == "Preview"
                        && koreanDomainMaskArtifact.State == "Preview"
                        && englishDomainMaskArtifact.NodeKind == "HeightField"
                        && koreanDomainMaskArtifact.NodeKind == "HeightField"
                        && englishDomainMaskArtifact.Detail.Contains("2 × 2", StringComparison.Ordinal)
                        && koreanDomainMaskArtifact.Detail.Contains("2 × 2", StringComparison.Ordinal),
                        $"en={englishDomainMaskArtifact.Detail}; ko={koreanDomainMaskArtifact.Detail}; id={koreanDomainMaskArtifact.Id}; state={koreanDomainMaskArtifact.State}; hash={koreanDomainMaskArtifact.ContentSha256}");

                    var domainMaskQualityDeltaFactory = (string evidence) => new SourceQualityDelta(
                        source.Id,
                        qualityDeltaOutput.ContentSha256,
                        qualityDeltaOutput.EntityId,
                        qualityDeltaOutput.ContentSha256,
                        qualityDeltaOutput.RootSourceSha256,
                        qualityDeltaOutput.RootSourceSha256,
                        qualityDeltaOutput.ValidCount,
                        qualityDeltaOutput.ValidCount,
                        qualityDeltaOutput.MissingCount,
                        qualityDeltaOutput.MissingCount,
                        null,
                        evidence);
                    OpenVisionLanguageService.SetLanguage(OpenVisionLanguage.English, save: false);
                    var englishDomainMaskQualityDeltaEvidence = ThreeDLocalization.Shared.DomainMaskQualityDeltaEvidence;
                    var englishDomainMaskQualityDeltaArtifact = new ToolWorkbenchArtifactProjection()
                        .Project(snapshot with
                        {
                            PipelineSteps = [domainMaskStep],
                            CreateSourceQualityDelta = (_, _, evidence) => domainMaskQualityDeltaFactory(evidence),
                            DomainMaskQualityDeltaEvidence = englishDomainMaskQualityDeltaEvidence,
                            DomainMask = new ToolWorkbenchArtifactPreview<C3DHeightFieldSnapshot>(
                                qualityDeltaOutput,
                                false,
                                false)
                        })
                        .Single(item => item.Id == qualityDeltaOutput.EntityId);
                    OpenVisionLanguageService.SetLanguage(OpenVisionLanguage.Korean, save: false);
                    var koreanDomainMaskQualityDeltaEvidence = ThreeDLocalization.Shared.DomainMaskQualityDeltaEvidence;
                    var koreanDomainMaskQualityDeltaArtifact = new ToolWorkbenchArtifactProjection()
                        .Project(snapshot with
                        {
                            PipelineSteps = [domainMaskStep],
                            CreateSourceQualityDelta = (_, _, evidence) => domainMaskQualityDeltaFactory(evidence),
                            DomainMaskQualityDeltaEvidence = koreanDomainMaskQualityDeltaEvidence,
                            DomainMask = new ToolWorkbenchArtifactPreview<C3DHeightFieldSnapshot>(
                                qualityDeltaOutput,
                                false,
                                false)
                        })
                        .Single(item => item.Id == qualityDeltaOutput.EntityId);
                    Check(
                        "domain-mask quality delta evidence localizes while preserving counts, hashes, root, output identity, state, and node kind",
                        englishDomainMaskQualityDeltaArtifact.PreparationQualityDelta?.Summary.Contains(
                            englishDomainMaskQualityDeltaEvidence,
                            StringComparison.Ordinal) == true
                        && koreanDomainMaskQualityDeltaArtifact.PreparationQualityDelta?.Summary.Contains(
                            koreanDomainMaskQualityDeltaEvidence,
                            StringComparison.Ordinal) == true
                        && englishDomainMaskQualityDeltaEvidence != koreanDomainMaskQualityDeltaEvidence
                        && englishDomainMaskQualityDeltaArtifact.PreparationQualityDelta?.BeforeValidSampleCount == qualityDeltaOutput.ValidCount
                        && koreanDomainMaskQualityDeltaArtifact.PreparationQualityDelta?.AfterValidSampleCount == qualityDeltaOutput.ValidCount
                        && englishDomainMaskQualityDeltaArtifact.PreparationQualityDelta?.BeforeMissingSampleCount == qualityDeltaOutput.MissingCount
                        && koreanDomainMaskQualityDeltaArtifact.PreparationQualityDelta?.AfterMissingSampleCount == qualityDeltaOutput.MissingCount
                        && englishDomainMaskQualityDeltaArtifact.PreparationQualityDelta?.SourceContentSha256 == qualityDeltaOutput.ContentSha256
                        && koreanDomainMaskQualityDeltaArtifact.PreparationQualityDelta?.DerivedContentSha256 == qualityDeltaOutput.ContentSha256
                        && englishDomainMaskQualityDeltaArtifact.PreparationQualityDelta?.SourceIdentityRetained == true
                        && koreanDomainMaskQualityDeltaArtifact.PreparationQualityDelta?.SourceRootSourceSha256 == qualityDeltaOutput.RootSourceSha256
                        && koreanDomainMaskQualityDeltaArtifact.PreparationQualityDelta?.DerivedRootSourceSha256 == qualityDeltaOutput.RootSourceSha256
                        && englishDomainMaskQualityDeltaArtifact.Id == koreanDomainMaskQualityDeltaArtifact.Id
                        && englishDomainMaskQualityDeltaArtifact.ContentSha256 == koreanDomainMaskQualityDeltaArtifact.ContentSha256
                        && englishDomainMaskQualityDeltaArtifact.RootSourceId == source.Id
                        && koreanDomainMaskQualityDeltaArtifact.RootSourceId == source.Id
                        && englishDomainMaskQualityDeltaArtifact.InputEntityIds == source.Id
                        && koreanDomainMaskQualityDeltaArtifact.InputEntityIds == source.Id
                        && englishDomainMaskQualityDeltaArtifact.Unit == qualityDeltaOutput.Unit
                        && koreanDomainMaskQualityDeltaArtifact.FrameId == qualityDeltaOutput.FrameId
                        && englishDomainMaskQualityDeltaArtifact.State == "Preview"
                        && koreanDomainMaskQualityDeltaArtifact.State == "Preview"
                        && englishDomainMaskQualityDeltaArtifact.NodeKind == "HeightField"
                        && koreanDomainMaskQualityDeltaArtifact.NodeKind == "HeightField",
                        $"en={englishDomainMaskQualityDeltaArtifact.Detail}; ko={koreanDomainMaskQualityDeltaArtifact.Detail}; enSummary={englishDomainMaskQualityDeltaArtifact.PreparationQualityDelta?.Summary}; koSummary={koreanDomainMaskQualityDeltaArtifact.PreparationQualityDelta?.Summary}");

                    var connectedRegionTool = new ToolWorkbenchToolItem(
                        "Prepare",
                        "Connected Region",
                        "connected-region",
                        1,
                        "ConnectedRegionArtifact",
                        "ConnectedRegionArtifact",
                        "Verification Connected Region.",
                        []);
                    var connectedRegionStep = new ToolWorkbenchPipelineStepItem(
                        "step.connected-region",
                        connectedRegionTool,
                        source.Id,
                        "connected.01");
                    var connectedRegionSourceHash = new string('A', 64);
                    var connectedRegionRootHash = new string('B', 64);
                    var connectedRegionMaskHash = new string('C', 64);
                    var connectedRegionArtifact = C3DConnectedRegionArtifact.Create(
                        "connected.01",
                        "Connected Region",
                        source.Id,
                        connectedRegionSourceHash,
                        connectedRegionRootHash,
                        connectedRegionMaskHash,
                        source.Unit,
                        source.FrameId,
                        2,
                        2,
                        C3DConnectedRegionArtifact.FourConnectivity,
                        0d,
                        0d,
                        1d,
                        1d,
                        "grid-unit^2",
                        [
                            new C3DConnectedRegionArtifactRegion(
                                0,
                                0,
                                0,
                                [
                                    new C3DConnectedRegionArtifactCell(0, 0),
                                    new C3DConnectedRegionArtifactCell(0, 1),
                                    new C3DConnectedRegionArtifactCell(1, 0)
                                ],
                                0,
                                0,
                                1,
                                1,
                                null)
                        ]);
                    OpenVisionLanguageService.SetLanguage(OpenVisionLanguage.English, save: false);
                    var englishConnectedRegionArtifact = new ToolWorkbenchArtifactProjection()
                        .Project(snapshot with
                        {
                            PipelineSteps = [connectedRegionStep],
                            ConnectedRegionArtifactDetailFormat = ThreeDLocalization.Shared.ConnectedRegionArtifactDetailFormat,
                            ConnectedRegion = new ToolWorkbenchArtifactPreview<C3DConnectedRegionArtifact>(
                                connectedRegionArtifact,
                                false,
                                false)
                        })
                        .Single(item => item.Id == connectedRegionArtifact.ArtifactId);
                    OpenVisionLanguageService.SetLanguage(OpenVisionLanguage.Korean, save: false);
                    var koreanConnectedRegionArtifact = new ToolWorkbenchArtifactProjection()
                        .Project(snapshot with
                        {
                            PipelineSteps = [connectedRegionStep],
                            ConnectedRegionArtifactDetailFormat = ThreeDLocalization.Shared.ConnectedRegionArtifactDetailFormat,
                            ConnectedRegion = new ToolWorkbenchArtifactPreview<C3DConnectedRegionArtifact>(
                                connectedRegionArtifact,
                                false,
                                false)
                        })
                        .Single(item => item.Id == connectedRegionArtifact.ArtifactId);
                    Check(
                        "connected-region detail localizes while preserving artifact identity, hashes, and region count",
                        englishConnectedRegionArtifact.Detail.Contains("1 region(s)", StringComparison.Ordinal)
                        && koreanConnectedRegionArtifact.Detail.Contains("연결 영역 1개", StringComparison.Ordinal)
                        && englishConnectedRegionArtifact.Detail.Contains(connectedRegionMaskHash, StringComparison.Ordinal)
                        && koreanConnectedRegionArtifact.Detail.Contains(connectedRegionMaskHash, StringComparison.Ordinal)
                        && englishConnectedRegionArtifact.Detail.Contains(connectedRegionSourceHash, StringComparison.Ordinal)
                        && koreanConnectedRegionArtifact.Detail.Contains(connectedRegionRootHash, StringComparison.Ordinal)
                        && englishConnectedRegionArtifact.Detail != koreanConnectedRegionArtifact.Detail
                        && englishConnectedRegionArtifact.Id == koreanConnectedRegionArtifact.Id
                        && englishConnectedRegionArtifact.ContentSha256 == koreanConnectedRegionArtifact.ContentSha256
                        && englishConnectedRegionArtifact.State == "Preview"
                        && koreanConnectedRegionArtifact.State == "Preview"
                        && englishConnectedRegionArtifact.NodeKind == "ConnectedRegionArtifact"
                        && koreanConnectedRegionArtifact.NodeKind == "ConnectedRegionArtifact"
                        && englishConnectedRegionArtifact.RootSourceId == source.Id
                        && koreanConnectedRegionArtifact.RootSourceId == source.Id,
                        $"en={englishConnectedRegionArtifact.Detail}; ko={koreanConnectedRegionArtifact.Detail}; id={koreanConnectedRegionArtifact.Id}; state={koreanConnectedRegionArtifact.State}; hash={koreanConnectedRegionArtifact.ContentSha256}");
                                        var editableRegionTool = new ToolWorkbenchToolItem(
                        "Prepare",
                        "Editable Region",
                        "editable-region",
                        1,
                        "EditableRegionArtifact",
                        "EditableRegionArtifact",
                        "Verification Editable Region.",
                        []);
                    var editableRegionStep = new ToolWorkbenchPipelineStepItem(
                        "step.editable-region",
                        editableRegionTool,
                        source.Id,
                        "editable.01");
                    var editableRegionArtifact = C3DEditableRegionArtifact.Create(
                        "editable.01",
                        "Editable Region",
                        connectedRegionArtifact,
                        0);
                    OpenVisionLanguageService.SetLanguage(OpenVisionLanguage.English, save: false);
                    var englishEditableRegionArtifact = new ToolWorkbenchArtifactProjection()
                        .Project(snapshot with
                        {
                            PipelineSteps = [editableRegionStep],
                            EditableRegionArtifactDetailFormat = ThreeDLocalization.Shared.EditableRegionArtifactDetailFormat,
                            EditableRegion = new ToolWorkbenchArtifactPreview<C3DEditableRegionArtifact>(
                                editableRegionArtifact,
                                false,
                                false)
                        })
                        .Single(item => item.Id == editableRegionArtifact.ArtifactId);
                    OpenVisionLanguageService.SetLanguage(OpenVisionLanguage.Korean, save: false);
                    var koreanEditableRegionArtifact = new ToolWorkbenchArtifactProjection()
                        .Project(snapshot with
                        {
                            PipelineSteps = [editableRegionStep],
                            EditableRegionArtifactDetailFormat = ThreeDLocalization.Shared.EditableRegionArtifactDetailFormat,
                            EditableRegion = new ToolWorkbenchArtifactPreview<C3DEditableRegionArtifact>(
                                editableRegionArtifact,
                                false,
                                false)
                        })
                        .Single(item => item.Id == editableRegionArtifact.ArtifactId);
                    Check(
                        "editable-region detail localizes while preserving artifact identity, cells, bounds, and connected hash",
                        englishEditableRegionArtifact.Detail.Contains("region 0", StringComparison.Ordinal)
                        && koreanEditableRegionArtifact.Detail.Contains("영역 0", StringComparison.Ordinal)
                        && englishEditableRegionArtifact.Detail.Contains("3 exact cell(s)", StringComparison.Ordinal)
                        && koreanEditableRegionArtifact.Detail.Contains("정확한 셀 3개", StringComparison.Ordinal)
                        && englishEditableRegionArtifact.Detail.Contains("bounds 2 × 2", StringComparison.Ordinal)
                        && koreanEditableRegionArtifact.Detail.Contains("범위 2 × 2", StringComparison.Ordinal)
                        && englishEditableRegionArtifact.Detail.Contains(connectedRegionArtifact.ContentSha256, StringComparison.Ordinal)
                        && koreanEditableRegionArtifact.Detail.Contains(connectedRegionArtifact.ContentSha256, StringComparison.Ordinal)
                        && englishEditableRegionArtifact.Detail != koreanEditableRegionArtifact.Detail
                        && englishEditableRegionArtifact.Id == koreanEditableRegionArtifact.Id
                        && englishEditableRegionArtifact.ContentSha256 == koreanEditableRegionArtifact.ContentSha256
                        && englishEditableRegionArtifact.State == "Preview"
                        && koreanEditableRegionArtifact.State == "Preview"
                        && englishEditableRegionArtifact.NodeKind == "EditableRegionArtifact"
                        && koreanEditableRegionArtifact.NodeKind == "EditableRegionArtifact"
                        && englishEditableRegionArtifact.RootSourceId == source.Id
                        && koreanEditableRegionArtifact.RootSourceId == source.Id
                        && englishEditableRegionArtifact.InputEntityIds == connectedRegionArtifact.ArtifactId
                        && koreanEditableRegionArtifact.InputEntityIds == connectedRegionArtifact.ArtifactId,
                        $"en={englishEditableRegionArtifact.Detail}; ko={koreanEditableRegionArtifact.Detail}; id={koreanEditableRegionArtifact.Id}; state={koreanEditableRegionArtifact.State}; hash={koreanEditableRegionArtifact.ContentSha256}");
                                        var removeOutlierTool = new ToolWorkbenchToolItem(
                        "Prepare",
                        "Remove Outlier Pixels",
                        "remove-outlier-pixels",
                        1,
                        "HeightField",
                        "FilteredHeightField",
                        "Verification Remove Outlier Pixels.",
                        []);
                    var removeOutlierStep = new ToolWorkbenchPipelineStepItem(
                        "step.remove-outlier-pixels",
                        removeOutlierTool,
                        source.Id,
                        qualityDeltaOutput.EntityId);
                    var outlierMask = C3DOutlierCellMap.Create(2, 2, [1]);
                    OpenVisionLanguageService.SetLanguage(OpenVisionLanguage.English, save: false);
                    var englishRemoveOutlierArtifact = new ToolWorkbenchArtifactProjection()
                        .Project(snapshot with
                        {
                            PipelineSteps = [removeOutlierStep],
                            CreateSourceQualityDelta = (_, _, _) => null,
                            QualityDeltaUnavailable = ThreeDLocalization.Shared.OutputCompareQualityDeltaUnavailable,
                            RemoveOutlierArtifactDetailFormat = ThreeDLocalization.Shared.RemoveOutlierArtifactDetailFormat,
                            SourceIdentityRetainedDetail = ThreeDLocalization.Shared.OutputCompareSourceIdentityRetained,
                            RemoveOutlier = new ToolWorkbenchRemoveOutlierArtifactPreview(
                                qualityDeltaOutput,
                                outlierMask,
                                false,
                                false)
                        })
                        .Single(item => item.Id == qualityDeltaOutput.EntityId);
                    OpenVisionLanguageService.SetLanguage(OpenVisionLanguage.Korean, save: false);
                    var koreanRemoveOutlierArtifact = new ToolWorkbenchArtifactProjection()
                        .Project(snapshot with
                        {
                            PipelineSteps = [removeOutlierStep],
                            CreateSourceQualityDelta = (_, _, _) => null,
                            QualityDeltaUnavailable = ThreeDLocalization.Shared.OutputCompareQualityDeltaUnavailable,
                            RemoveOutlierArtifactDetailFormat = ThreeDLocalization.Shared.RemoveOutlierArtifactDetailFormat,
                            SourceIdentityRetainedDetail = ThreeDLocalization.Shared.OutputCompareSourceIdentityRetained,
                            RemoveOutlier = new ToolWorkbenchRemoveOutlierArtifactPreview(
                                qualityDeltaOutput,
                                outlierMask,
                                false,
                                false)
                        })
                        .Single(item => item.Id == qualityDeltaOutput.EntityId);
                    Check(
                        "remove-outlier detail localizes while preserving output identity, grid, mask, and source detail",
                        englishRemoveOutlierArtifact.Detail.Contains("2 × 2", StringComparison.Ordinal)
                        && koreanRemoveOutlierArtifact.Detail.Contains("2 × 2", StringComparison.Ordinal)
                        && englishRemoveOutlierArtifact.Detail.Contains("removed 1", StringComparison.Ordinal)
                        && koreanRemoveOutlierArtifact.Detail.Contains("제거 1개", StringComparison.Ordinal)
                        && englishRemoveOutlierArtifact.Detail.Contains(outlierMask.Sha256, StringComparison.Ordinal)
                        && koreanRemoveOutlierArtifact.Detail.Contains(outlierMask.Sha256, StringComparison.Ordinal)
                        && englishRemoveOutlierArtifact.Detail.Contains("source identity retained", StringComparison.Ordinal)
                        && koreanRemoveOutlierArtifact.Detail.Contains("원본 ID 유지", StringComparison.Ordinal)
                        && englishRemoveOutlierArtifact.Detail.Contains("Quality delta unavailable", StringComparison.Ordinal)
                        && koreanRemoveOutlierArtifact.Detail.Contains("품질 변화 정보 없음", StringComparison.Ordinal)
                        && englishRemoveOutlierArtifact.Detail != koreanRemoveOutlierArtifact.Detail
                        && englishRemoveOutlierArtifact.Id == koreanRemoveOutlierArtifact.Id
                        && englishRemoveOutlierArtifact.ContentSha256 == koreanRemoveOutlierArtifact.ContentSha256
                        && englishRemoveOutlierArtifact.State == "Preview"
                        && koreanRemoveOutlierArtifact.State == "Preview"
                        && englishRemoveOutlierArtifact.NodeKind == "FilteredHeightField"
                        && koreanRemoveOutlierArtifact.NodeKind == "FilteredHeightField"
                        && englishRemoveOutlierArtifact.RootSourceId == source.Id
                        && koreanRemoveOutlierArtifact.RootSourceId == source.Id
                        && englishRemoveOutlierArtifact.InputEntityIds == source.Id
                        && koreanRemoveOutlierArtifact.InputEntityIds == source.Id,
                        $"en={englishRemoveOutlierArtifact.Detail}; ko={koreanRemoveOutlierArtifact.Detail}; id={koreanRemoveOutlierArtifact.Id}; state={koreanRemoveOutlierArtifact.State}; hash={koreanRemoveOutlierArtifact.ContentSha256}");

                    var removeOutlierQualityDeltaFactory = (string evidence) => new SourceQualityDelta(
                        source.Id,
                        qualityDeltaOutput.ContentSha256,
                        qualityDeltaOutput.EntityId,
                        qualityDeltaOutput.ContentSha256,
                        qualityDeltaOutput.RootSourceSha256,
                        qualityDeltaOutput.RootSourceSha256,
                        qualityDeltaOutput.ValidCount,
                        qualityDeltaOutput.ValidCount,
                        qualityDeltaOutput.MissingCount,
                        qualityDeltaOutput.MissingCount,
                        outlierMask.OutlierCellCount,
                        evidence);
                    OpenVisionLanguageService.SetLanguage(OpenVisionLanguage.English, save: false);
                    var englishRemoveOutlierQualityDeltaEvidence = ThreeDLocalization.Shared.RemoveOutlierQualityDeltaEvidence;
                    var englishRemoveOutlierQualityDeltaArtifact = new ToolWorkbenchArtifactProjection()
                        .Project(snapshot with
                        {
                            PipelineSteps = [removeOutlierStep],
                            CreateSourceQualityDelta = (_, _, evidence) => removeOutlierQualityDeltaFactory(evidence),
                            RemoveOutlierQualityDeltaEvidence = englishRemoveOutlierQualityDeltaEvidence,
                            RemoveOutlier = new ToolWorkbenchRemoveOutlierArtifactPreview(
                                qualityDeltaOutput,
                                outlierMask,
                                false,
                                false)
                        })
                        .Single(item => item.Id == qualityDeltaOutput.EntityId);
                    OpenVisionLanguageService.SetLanguage(OpenVisionLanguage.Korean, save: false);
                    var koreanRemoveOutlierQualityDeltaEvidence = ThreeDLocalization.Shared.RemoveOutlierQualityDeltaEvidence;
                    var koreanRemoveOutlierQualityDeltaArtifact = new ToolWorkbenchArtifactProjection()
                        .Project(snapshot with
                        {
                            PipelineSteps = [removeOutlierStep],
                            CreateSourceQualityDelta = (_, _, evidence) => removeOutlierQualityDeltaFactory(evidence),
                            RemoveOutlierQualityDeltaEvidence = koreanRemoveOutlierQualityDeltaEvidence,
                            RemoveOutlier = new ToolWorkbenchRemoveOutlierArtifactPreview(
                                qualityDeltaOutput,
                                outlierMask,
                                false,
                                false)
                        })
                        .Single(item => item.Id == qualityDeltaOutput.EntityId);
                    Check(
                        "remove-outlier quality delta evidence localizes while preserving count, hashes, root, output identity, state, and node kind",
                        englishRemoveOutlierQualityDeltaArtifact.PreparationQualityDelta?.OutlierEvidence
                            == englishRemoveOutlierQualityDeltaEvidence
                        && koreanRemoveOutlierQualityDeltaArtifact.PreparationQualityDelta?.OutlierEvidence
                            == koreanRemoveOutlierQualityDeltaEvidence
                        && englishRemoveOutlierQualityDeltaEvidence != koreanRemoveOutlierQualityDeltaEvidence
                        && englishRemoveOutlierQualityDeltaArtifact.PreparationQualityDelta?.DetectedOutlierCount == outlierMask.OutlierCellCount
                        && koreanRemoveOutlierQualityDeltaArtifact.PreparationQualityDelta?.DetectedOutlierCount == outlierMask.OutlierCellCount
                        && englishRemoveOutlierQualityDeltaArtifact.PreparationQualityDelta?.BeforeValidSampleCount == qualityDeltaOutput.ValidCount
                        && koreanRemoveOutlierQualityDeltaArtifact.PreparationQualityDelta?.AfterValidSampleCount == qualityDeltaOutput.ValidCount
                        && englishRemoveOutlierQualityDeltaArtifact.PreparationQualityDelta?.BeforeMissingSampleCount == qualityDeltaOutput.MissingCount
                        && koreanRemoveOutlierQualityDeltaArtifact.PreparationQualityDelta?.AfterMissingSampleCount == qualityDeltaOutput.MissingCount
                        && englishRemoveOutlierQualityDeltaArtifact.PreparationQualityDelta?.SourceContentSha256 == qualityDeltaOutput.ContentSha256
                        && koreanRemoveOutlierQualityDeltaArtifact.PreparationQualityDelta?.DerivedContentSha256 == qualityDeltaOutput.ContentSha256
                        && englishRemoveOutlierQualityDeltaArtifact.PreparationQualityDelta?.SourceIdentityRetained == true
                        && koreanRemoveOutlierQualityDeltaArtifact.PreparationQualityDelta?.SourceRootSourceSha256 == qualityDeltaOutput.RootSourceSha256
                        && koreanRemoveOutlierQualityDeltaArtifact.PreparationQualityDelta?.DerivedRootSourceSha256 == qualityDeltaOutput.RootSourceSha256
                        && englishRemoveOutlierQualityDeltaArtifact.Id == koreanRemoveOutlierQualityDeltaArtifact.Id
                        && englishRemoveOutlierQualityDeltaArtifact.ContentSha256 == koreanRemoveOutlierQualityDeltaArtifact.ContentSha256
                        && englishRemoveOutlierQualityDeltaArtifact.RootSourceId == source.Id
                        && koreanRemoveOutlierQualityDeltaArtifact.RootSourceId == source.Id
                        && englishRemoveOutlierQualityDeltaArtifact.InputEntityIds == source.Id
                        && koreanRemoveOutlierQualityDeltaArtifact.InputEntityIds == source.Id
                        && englishRemoveOutlierQualityDeltaArtifact.Unit == qualityDeltaOutput.Unit
                        && koreanRemoveOutlierQualityDeltaArtifact.FrameId == qualityDeltaOutput.FrameId
                        && englishRemoveOutlierQualityDeltaArtifact.State == "Preview"
                        && koreanRemoveOutlierQualityDeltaArtifact.State == "Preview"
                        && englishRemoveOutlierQualityDeltaArtifact.NodeKind == "FilteredHeightField"
                        && koreanRemoveOutlierQualityDeltaArtifact.NodeKind == "FilteredHeightField",
                        $"en={englishRemoveOutlierQualityDeltaArtifact.Detail}; ko={koreanRemoveOutlierQualityDeltaArtifact.Detail}; enSummary={englishRemoveOutlierQualityDeltaArtifact.PreparationQualityDelta?.Summary}; koSummary={koreanRemoveOutlierQualityDeltaArtifact.PreparationQualityDelta?.Summary}");

                    var levelSurfaceSource = C3DHeightFieldSnapshot.CreateForVerification(
                        "source.level-surface",
                        2,
                        2,
                        [1d, 1d, 1d, 1d]);
                    var levelSurfaceSelection = new ToolRecipeSelection(
                        "selection.level-surface",
                        "Level Surface reference",
                        ToolRecipeSelectionKinds.GridRectangle,
                        levelSurfaceSource.EntityId,
                        levelSurfaceSource.FrameId,
                        new ToolRecipeSelectionSourceBinding(
                            "C3D",
                            levelSurfaceSource.RootSourceSha256,
                            2,
                            2),
                        new ToolRecipeGridRectangle(0, 0, 2, 2),
                        null,
                        null);
                    var levelSurfaceEvaluation = C3DLevelSurfaceRule.Evaluate(
                        new C3DLevelSurfaceInput(
                            "step.level-surface",
                            levelSurfaceSource,
                            [levelSurfaceSelection],
                            "derived.level-surface",
                            3,
                            1d));
                    var levelSurfaceOutput = levelSurfaceEvaluation.Output
                        ?? throw new InvalidOperationException("Level Surface verification fixture did not produce output.");
                    var levelSurfaceTransform = levelSurfaceEvaluation.Transform
                        ?? throw new InvalidOperationException("Level Surface verification fixture did not produce transform.");
                    var levelSurfaceFrame = levelSurfaceEvaluation.LevelFrame
                        ?? throw new InvalidOperationException("Level Surface verification fixture did not produce level frame.");
                    var levelSurfaceChain = levelSurfaceEvaluation.FrameChain
                        ?? throw new InvalidOperationException("Level Surface verification fixture did not produce frame chain.");
                    var levelSurfaceQuality = levelSurfaceEvaluation.QualityEvidence
                        ?? throw new InvalidOperationException("Level Surface verification fixture did not produce quality evidence.");
                    var levelSurfaceTool = new ToolWorkbenchToolItem(
                        "Prepare",
                        "Level Surface",
                        "level-surface",
                        1,
                        "HeightField",
                        "LeveledHeightField",
                        "Verification Level Surface.",
                        []);
                    var levelSurfaceStep = new ToolWorkbenchPipelineStepItem(
                        "step.level-surface",
                        levelSurfaceTool,
                        source.Id,
                        levelSurfaceOutput.EntityId);
                    OpenVisionLanguageService.SetLanguage(OpenVisionLanguage.English, save: false);
                    var englishLevelSurfaceArtifact = new ToolWorkbenchArtifactProjection()
                        .Project(snapshot with
                        {
                            PipelineSteps = [levelSurfaceStep],
                            CreateSourceQualityDelta = (_, _, _) => null,
                            QualityDeltaUnavailable = ThreeDLocalization.Shared.OutputCompareQualityDeltaUnavailable,
                            SourceIdentityRetainedDetail = ThreeDLocalization.Shared.OutputCompareSourceIdentityRetained,
                            LevelSurfaceArtifactDetailFormat = ThreeDLocalization.Shared.LevelSurfaceArtifactDetailFormat,
                            LevelSurface = new ToolWorkbenchLevelSurfaceArtifactPreview(
                                levelSurfaceOutput,
                                levelSurfaceTransform,
                                levelSurfaceFrame,
                                levelSurfaceChain,
                                levelSurfaceQuality,
                                false,
                                false)
                        })
                        .Single(item => item.Id == levelSurfaceOutput.EntityId);
                    OpenVisionLanguageService.SetLanguage(OpenVisionLanguage.Korean, save: false);
                    var koreanLevelSurfaceArtifact = new ToolWorkbenchArtifactProjection()
                        .Project(snapshot with
                        {
                            PipelineSteps = [levelSurfaceStep],
                            CreateSourceQualityDelta = (_, _, _) => null,
                            QualityDeltaUnavailable = ThreeDLocalization.Shared.OutputCompareQualityDeltaUnavailable,
                            SourceIdentityRetainedDetail = ThreeDLocalization.Shared.OutputCompareSourceIdentityRetained,
                            LevelSurfaceArtifactDetailFormat = ThreeDLocalization.Shared.LevelSurfaceArtifactDetailFormat,
                            LevelSurface = new ToolWorkbenchLevelSurfaceArtifactPreview(
                                levelSurfaceOutput,
                                levelSurfaceTransform,
                                levelSurfaceFrame,
                                levelSurfaceChain,
                                levelSurfaceQuality,
                                false,
                                false)
                        })
                        .Single(item => item.Id == levelSurfaceOutput.EntityId);
                    Check(
                        "level-surface detail localizes while preserving output, transform, frame-chain, quality, and source evidence",
                        englishLevelSurfaceArtifact.Detail.Contains("2 × 2", StringComparison.Ordinal)
                        && koreanLevelSurfaceArtifact.Detail.Contains("2 × 2", StringComparison.Ordinal)
                        && englishLevelSurfaceArtifact.Detail.Contains("reference RMS", StringComparison.Ordinal)
                        && koreanLevelSurfaceArtifact.Detail.Contains("기준 RMS", StringComparison.Ordinal)
                        && englishLevelSurfaceArtifact.Detail.Contains(levelSurfaceTransform.ContentSha256, StringComparison.Ordinal)
                        && koreanLevelSurfaceArtifact.Detail.Contains(levelSurfaceTransform.ContentSha256, StringComparison.Ordinal)
                        && englishLevelSurfaceArtifact.Detail.Contains(levelSurfaceFrame.ContentSha256, StringComparison.Ordinal)
                        && koreanLevelSurfaceArtifact.Detail.Contains(levelSurfaceFrame.ContentSha256, StringComparison.Ordinal)
                        && englishLevelSurfaceArtifact.Detail.Contains(levelSurfaceChain.ContentSha256, StringComparison.Ordinal)
                        && koreanLevelSurfaceArtifact.Detail.Contains(levelSurfaceChain.ContentSha256, StringComparison.Ordinal)
                        && englishLevelSurfaceArtifact.Detail.Contains(levelSurfaceQuality.State.ToString(), StringComparison.Ordinal)
                        && koreanLevelSurfaceArtifact.Detail.Contains(levelSurfaceQuality.State.ToString(), StringComparison.Ordinal)
                        && englishLevelSurfaceArtifact.Detail.Contains(levelSurfaceQuality.ContentSha256, StringComparison.Ordinal)
                        && koreanLevelSurfaceArtifact.Detail.Contains(levelSurfaceQuality.ContentSha256, StringComparison.Ordinal)
                        && englishLevelSurfaceArtifact.Detail.Contains("source identity retained", StringComparison.Ordinal)
                        && koreanLevelSurfaceArtifact.Detail.Contains("원본 ID 유지", StringComparison.Ordinal)
                        && englishLevelSurfaceArtifact.Detail.Contains("Quality delta unavailable", StringComparison.Ordinal)
                        && koreanLevelSurfaceArtifact.Detail.Contains("품질 변화 정보 없음", StringComparison.Ordinal)
                        && englishLevelSurfaceArtifact.Detail != koreanLevelSurfaceArtifact.Detail
                        && englishLevelSurfaceArtifact.Id == koreanLevelSurfaceArtifact.Id
                        && englishLevelSurfaceArtifact.ContentSha256 == koreanLevelSurfaceArtifact.ContentSha256
                        && englishLevelSurfaceArtifact.State == "Preview"
                        && koreanLevelSurfaceArtifact.State == "Preview"
                        && englishLevelSurfaceArtifact.NodeKind == "LeveledHeightField"
                        && koreanLevelSurfaceArtifact.NodeKind == "LeveledHeightField"
                        && englishLevelSurfaceArtifact.RootSourceId == source.Id
                        && koreanLevelSurfaceArtifact.RootSourceId == source.Id
                        && englishLevelSurfaceArtifact.InputEntityIds == source.Id
                        && koreanLevelSurfaceArtifact.InputEntityIds == source.Id,
                        $"en={englishLevelSurfaceArtifact.Detail}; ko={koreanLevelSurfaceArtifact.Detail}; id={koreanLevelSurfaceArtifact.Id}; state={koreanLevelSurfaceArtifact.State}; hash={koreanLevelSurfaceArtifact.ContentSha256}");

                    var levelSurfaceQualityDeltaFactory = (string evidence) => new SourceQualityDelta(
                        source.Id,
                        levelSurfaceOutput.ContentSha256,
                        levelSurfaceOutput.EntityId,
                        levelSurfaceOutput.ContentSha256,
                        levelSurfaceOutput.RootSourceSha256,
                        levelSurfaceOutput.RootSourceSha256,
                        levelSurfaceOutput.ValidCount,
                        levelSurfaceOutput.ValidCount,
                        levelSurfaceOutput.MissingCount,
                        levelSurfaceOutput.MissingCount,
                        null,
                        evidence);
                    OpenVisionLanguageService.SetLanguage(OpenVisionLanguage.English, save: false);
                    var englishLevelSurfaceQualityDeltaEvidence = ThreeDLocalization.Shared.LevelSurfaceQualityDeltaEvidence;
                    var englishLevelSurfaceQualityDeltaArtifact = new ToolWorkbenchArtifactProjection()
                        .Project(snapshot with
                        {
                            PipelineSteps = [levelSurfaceStep],
                            CreateSourceQualityDelta = (_, _, evidence) => levelSurfaceQualityDeltaFactory(evidence),
                            LevelSurfaceQualityDeltaEvidence = englishLevelSurfaceQualityDeltaEvidence,
                            LevelSurface = new ToolWorkbenchLevelSurfaceArtifactPreview(
                                levelSurfaceOutput,
                                levelSurfaceTransform,
                                levelSurfaceFrame,
                                levelSurfaceChain,
                                levelSurfaceQuality,
                                false,
                                false)
                        })
                        .Single(item => item.Id == levelSurfaceOutput.EntityId);
                    OpenVisionLanguageService.SetLanguage(OpenVisionLanguage.Korean, save: false);
                    var koreanLevelSurfaceQualityDeltaEvidence = ThreeDLocalization.Shared.LevelSurfaceQualityDeltaEvidence;
                    var koreanLevelSurfaceQualityDeltaArtifact = new ToolWorkbenchArtifactProjection()
                        .Project(snapshot with
                        {
                            PipelineSteps = [levelSurfaceStep],
                            CreateSourceQualityDelta = (_, _, evidence) => levelSurfaceQualityDeltaFactory(evidence),
                            LevelSurfaceQualityDeltaEvidence = koreanLevelSurfaceQualityDeltaEvidence,
                            LevelSurface = new ToolWorkbenchLevelSurfaceArtifactPreview(
                                levelSurfaceOutput,
                                levelSurfaceTransform,
                                levelSurfaceFrame,
                                levelSurfaceChain,
                                levelSurfaceQuality,
                                false,
                                false)
                        })
                        .Single(item => item.Id == levelSurfaceOutput.EntityId);
                    Check(
                        "level-surface quality delta evidence localizes while preserving counts, hashes, root, output identity, state, and node kind",
                        englishLevelSurfaceQualityDeltaArtifact.PreparationQualityDelta?.Summary.Contains(
                            englishLevelSurfaceQualityDeltaEvidence,
                            StringComparison.Ordinal) == true
                        && koreanLevelSurfaceQualityDeltaArtifact.PreparationQualityDelta?.Summary.Contains(
                            koreanLevelSurfaceQualityDeltaEvidence,
                            StringComparison.Ordinal) == true
                        && englishLevelSurfaceQualityDeltaEvidence != koreanLevelSurfaceQualityDeltaEvidence
                        && englishLevelSurfaceQualityDeltaArtifact.PreparationQualityDelta?.BeforeValidSampleCount == levelSurfaceOutput.ValidCount
                        && koreanLevelSurfaceQualityDeltaArtifact.PreparationQualityDelta?.AfterValidSampleCount == levelSurfaceOutput.ValidCount
                        && englishLevelSurfaceQualityDeltaArtifact.PreparationQualityDelta?.BeforeMissingSampleCount == levelSurfaceOutput.MissingCount
                        && koreanLevelSurfaceQualityDeltaArtifact.PreparationQualityDelta?.AfterMissingSampleCount == levelSurfaceOutput.MissingCount
                        && englishLevelSurfaceQualityDeltaArtifact.PreparationQualityDelta?.SourceContentSha256 == levelSurfaceOutput.ContentSha256
                        && koreanLevelSurfaceQualityDeltaArtifact.PreparationQualityDelta?.DerivedContentSha256 == levelSurfaceOutput.ContentSha256
                        && englishLevelSurfaceQualityDeltaArtifact.PreparationQualityDelta?.SourceIdentityRetained == true
                        && koreanLevelSurfaceQualityDeltaArtifact.PreparationQualityDelta?.SourceRootSourceSha256 == levelSurfaceOutput.RootSourceSha256
                        && koreanLevelSurfaceQualityDeltaArtifact.PreparationQualityDelta?.DerivedRootSourceSha256 == levelSurfaceOutput.RootSourceSha256
                        && englishLevelSurfaceQualityDeltaArtifact.Id == koreanLevelSurfaceQualityDeltaArtifact.Id
                        && englishLevelSurfaceQualityDeltaArtifact.ContentSha256 == koreanLevelSurfaceQualityDeltaArtifact.ContentSha256
                        && englishLevelSurfaceQualityDeltaArtifact.RootSourceId == source.Id
                        && koreanLevelSurfaceQualityDeltaArtifact.RootSourceId == source.Id
                        && englishLevelSurfaceQualityDeltaArtifact.InputEntityIds == source.Id
                        && koreanLevelSurfaceQualityDeltaArtifact.InputEntityIds == source.Id
                        && englishLevelSurfaceQualityDeltaArtifact.Unit == levelSurfaceOutput.Unit
                        && koreanLevelSurfaceQualityDeltaArtifact.FrameId == levelSurfaceOutput.FrameId
                        && englishLevelSurfaceQualityDeltaArtifact.State == "Preview"
                        && koreanLevelSurfaceQualityDeltaArtifact.State == "Preview"
                        && englishLevelSurfaceQualityDeltaArtifact.NodeKind == "LeveledHeightField"
                        && koreanLevelSurfaceQualityDeltaArtifact.NodeKind == "LeveledHeightField",
                        $"en={englishLevelSurfaceQualityDeltaArtifact.Detail}; ko={koreanLevelSurfaceQualityDeltaArtifact.Detail}; enSummary={englishLevelSurfaceQualityDeltaArtifact.PreparationQualityDelta?.Summary}; koSummary={koreanLevelSurfaceQualityDeltaArtifact.PreparationQualityDelta?.Summary}");

                    var sourceBinding = new ToolRecipeSelectionSourceBinding(
                        source.Format,
                        "source-content-hash",
                        2,
                        2);
                    OpenVisionLanguageService.SetLanguage(OpenVisionLanguage.English, save: false);
                    var englishReadySourceArtifact = new ToolWorkbenchArtifactProjection()
                        .Project(snapshot with
                        {
                            IsSourceReadyForRecipe = true,
                            SourceBinding = sourceBinding,
                            SourceArtifactReadyDetailFormat = ThreeDLocalization.Shared.SourceArtifactReadyDetailFormat
                        })
                        .Single(item => item.Id == source.Id);
                    OpenVisionLanguageService.SetLanguage(OpenVisionLanguage.Korean, save: false);
                    var koreanReadySourceArtifact = new ToolWorkbenchArtifactProjection()
                        .Project(snapshot with
                        {
                            IsSourceReadyForRecipe = true,
                            SourceBinding = sourceBinding,
                            SourceArtifactReadyDetailFormat = ThreeDLocalization.Shared.SourceArtifactReadyDetailFormat
                        })
                        .Single(item => item.Id == source.Id);
                    Check(
                        "source-ready detail localizes while preserving source identity and grid",
                        englishReadySourceArtifact.Detail == "Verified C3D source · 2 × 2"
                        && koreanReadySourceArtifact.Detail == "검증된 C3D 입력 · 2 × 2"
                        && englishReadySourceArtifact.Detail != koreanReadySourceArtifact.Detail
                        && englishReadySourceArtifact.Id == koreanReadySourceArtifact.Id
                        && englishReadySourceArtifact.ContentSha256 == koreanReadySourceArtifact.ContentSha256
                        && englishReadySourceArtifact.State == "Ready"
                        && koreanReadySourceArtifact.State == "Ready"
                        && englishReadySourceArtifact.NodeKind == "Source"
                        && koreanReadySourceArtifact.NodeKind == "Source",
                        $"en={englishReadySourceArtifact.Detail}; ko={koreanReadySourceArtifact.Detail}; id={koreanReadySourceArtifact.Id}; state={koreanReadySourceArtifact.State}; hash={koreanReadySourceArtifact.ContentSha256}");
                }
                finally
                {
                    OpenVisionLanguageService.SetLanguage(qualityDeltaLanguage, save: false);
                }

                coordinator.Dispose();
                coordinator.Dispose();
                var propertyChangedAfterDispose = propertyChangedCount;
                var rebuiltAfterDispose = rebuiltCount;
                owner.Rebuild();
                Check(
                    "Dispose is idempotent and suppresses both later callbacks",
                    propertyChangedCount == propertyChangedAfterDispose
                    && rebuiltCount == rebuiltAfterDispose,
                    $"property={propertyChangedCount}; rebuilt={rebuiltCount}");
            }
            finally
            {
                coordinator.Dispose();
            }
        }
        catch (Exception exception)
        {
            failure = exception;
        }

        if (failure is not null)
        {
            lines.Add($"FAIL | verifier exception | {failure.GetType().Name}: {failure.Message}");
        }

        var succeeded = failure is null && passed == total && total > 0;
        lines.Add($"Result: {(succeeded ? "Pass" : "Fail")} ({passed}/{total} checks)");
        var fullReportPath = Path.GetFullPath(reportPath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullReportPath)!);
        File.WriteAllLines(fullReportPath, lines);
        summary = $"ToolWorkbenchArtifactNavigatorEventCoordinator|pass={succeeded}|checks={passed}/{total}|report={fullReportPath}";
        return succeeded;
    }
}
