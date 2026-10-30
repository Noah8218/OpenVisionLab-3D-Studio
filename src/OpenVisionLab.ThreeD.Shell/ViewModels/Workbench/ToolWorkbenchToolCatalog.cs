using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Tools;

namespace OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;

internal static class ToolWorkbenchToolCatalog
{
    private static readonly IReadOnlyDictionary<
        string,
        (string Key, string Korean, IReadOnlyList<string> Aliases)> LocalizedToolNames =
        new Dictionary<string, (string Key, string Korean, IReadOnlyList<string> Aliases)>(StringComparer.Ordinal)
        {
            ["filter"] = ("ThreeD.Tool.Filter", "필터", ["필터"]),
            ["height-threshold-background-removal"] = ("ThreeD.Tool.HeightThresholdBackgroundRemoval", "높이 임계값 배경 제거", ["높이", "임계값", "배경"]),
            ["signed-delta-background-subtraction"] = ("ThreeD.Tool.SignedDeltaBackgroundSubtraction", "부호 있는 배경 차이", ["부호", "배경", "차이", "델타"]),
            ["remove-outlier-pixels"] = ("ThreeD.Tool.RemoveOutlierPixels", "노이즈 제거", ["노이즈", "이상치", "아웃라이어"]),
            ["connected-region"] = ("ThreeD.Tool.ConnectedRegion", "연결 영역", ["연결", "영역"]),
            ["grid-polygon-domain"] = ("ThreeD.Tool.GridPolygonDomain", "그리드 다각형 영역", ["다각형", "폴리곤", "영역"]),
            ["domain-mask"] = ("ThreeD.Tool.DomainMask", "영역 / 마스크", ["도메인", "영역", "마스크"]),
            ["editable-region"] = ("ThreeD.Tool.EditableRegion", "편집 영역", ["편집", "영역"]),
            ["nine-point-surface-compensation"] = ("ThreeD.Tool.NinePointSurfaceCompensation", "9점 곡면 보정", ["9점", "곡면", "PCB"]),
            ["level-surface"] = ("ThreeD.Tool.LevelSurface", "수평면", ["레벨", "높이"]),
            ["roi-crop"] = ("ThreeD.Tool.RoiCrop", "ROI / 자르기", ["ROI", "영역"]),
            ["height-difference-edge"] = ("ThreeD.Tool.HeightDifferenceEdge", "높이차 에지", ["높이", "엣지", "에지"]),
            ["two-point-line"] = ("ThreeD.Tool.TwoPointLine", "2점 직선", ["라인", "직선"]),
            ["three-point-plane"] = ("ThreeD.Tool.ThreePointPlane", "3점 평면", ["평면"]),
            ["datum-plane-raw-height-deviation"] = ("ThreeD.Tool.DatumPlaneDeviation", "기준면 편차", ["데이텀", "높이", "편차"]),
            ["three-d-line-fit"] = ("ThreeD.Tool.ThreeDLineFit", "3D 라인 피팅", ["라인", "피팅"]),
            ["physical-edge-gap-flush"] = ("ThreeD.Tool.PhysicalEdgeGapFlush", "물리 에지 간격 / 단차", ["에지", "간격", "단차"]),
            ["line-intersection"] = ("ThreeD.Tool.LineIntersection", "직선 교점", ["라인", "교점"]),
            ["landmark-correspondence"] = ("ThreeD.Tool.LandmarkCorrespondence", "랜드마크 대응", ["랜드마크", "대응"]),
            ["xyz-affine-solve"] = ("ThreeD.Tool.XYZAffineSolve", "XYZ 어파인 계산", ["어파인", "변환"]),
            ["xyz-affine-apply"] = ("ThreeD.Tool.XYZAffineApply", "XYZ 어파인 적용", ["어파인", "변환"]),
            ["re-grid-height-map"] = ("ThreeD.Tool.RegridHeightMap", "높이 맵 재격자화", ["높이", "재격자"]),
            ["surface-match"] = ("ThreeD.Tool.SurfaceMatch", "표면 매칭", ["표면", "매칭"]),
            ["thickness"] = ("ThreeD.Tool.Thickness", "두께", ["두께"]),
            ["warpage"] = ("ThreeD.Tool.Warpage", "휨", ["휨", "뒤틀림"]),
            ["plane-flatness"] = ("ThreeD.Tool.PlaneFlatness", "평면 평탄도", ["평면", "평탄도"]),
            ["point-pair-dimensions"] = ("ThreeD.Tool.PointPairDimensions", "점 쌍 치수", ["점", "치수"]),
            ["gap-flush"] = ("ThreeD.Tool.GapFlush", "간격 / 단차", ["간격", "단차"]),
            ["volume"] = ("ThreeD.Tool.Volume", "체적", ["체적", "부피"]),
            ["cross-section-dimensions"] = ("ThreeD.Tool.CrossSectionDimensions", "단면 치수", ["단면", "치수"]),
            ["completeness-grid"] = ("ThreeD.Tool.CompletenessGrid", "완전성 그리드", ["완전성", "그리드"]),
            ["connected-region-gate"] = ("ThreeD.Tool.ConnectedRegionGate", "연결 영역 커버리지 / 높이 게이트", ["연결", "영역", "커버리지", "높이", "게이트"]),
            ["overlay-control-review"] = ("ThreeD.Tool.OverlayControlReview", "오버레이 / 제어 검토", ["오버레이", "검토"])
        };

    private static readonly IReadOnlyDictionary<
        string,
        (string Key, string Korean, string English)> LocalizedToolDescriptions =
        new Dictionary<string, (string Key, string Korean, string English)>(StringComparer.Ordinal)
        {
            ["filter"] = (
                "ThreeD.Tool.Description.Filter",
                "입력: C3D 높이 맵. 결과: 결측 셀을 유지하면서 높이 노이즈를 줄인 맵.",
                "Input: a C3D height map. Result: a denoised map with missing cells preserved."),
            ["height-threshold-background-removal"] = (
                "ThreeD.Tool.Description.HeightThresholdBackgroundRemoval",
                "입력: 원본 높이 맵. 결과: 포함 임계값으로 남긴 별도 동일 격자 맵이며 원본과 기존 결측은 유지됩니다.",
                "Input: a raw-height source map. Result: a separate same-grid map retaining samples at the inclusive threshold; the source and existing missing cells stay unchanged."),
            ["signed-delta-background-subtraction"] = (
                "ThreeD.Tool.Description.SignedDeltaBackgroundSubtraction",
                "입력: 현재 높이 데이터와 파일 식별 정보를 확인한 배경 데이터. 결과: 현재 높이에서 배경 높이를 뺀 값입니다. 0은 유효한 차이값이고 어느 입력이든 누락된 셀은 결측으로 유지합니다. C3D로 저장하면 0을 결측으로 처리하므로 별도의 결과 데이터로 보관합니다.",
                "Input: the current raw-height C3D and a SHA-256-bound saved background. Result: signed current-minus-background deltas; finite zero remains valid zero and cells missing in either input remain missing. The result is kept as an evidence artifact because writing it as C3D would turn zero into missing."),
            ["remove-outlier-pixels"] = (
                "ThreeD.Tool.Description.RemoveOutlierPixels",
                "입력: 높이 맵. 결과: 주변과 높이 차이가 큰 데이터를 계산에서 제외한 별도 맵이며 원본과 기존 결측은 유지됩니다.",
                "Input: a height map. Result: a separate map with local outliers marked missing; the source and existing missing cells stay unchanged."),
            ["connected-region"] = (
                "ThreeD.Tool.Description.ConnectedRegion",
                "입력: 적용된 필터 결과. 결과: 연결된 셀 묶음과 각 경계.",
                "Input: a Published filtered result. Result: connected cell groups and their bounds."),
            ["grid-polygon-domain"] = (
                "ThreeD.Tool.Description.GridPolygonDomain",
                "입력: 높이 맵과 직접 만든 격자 다각형. 결과: 다각형 안에 포함된 원본 셀 영역이며 높이값이나 물리 배율은 계산하지 않습니다.",
                "Input: a height map and an authored grid polygon. Result: the source cells inside the polygon; height values and physical scale are not calculated."),
            ["domain-mask"] = (
                "ThreeD.Tool.Description.DomainMask",
                "입력: 기존 v1은 높이 데이터와 영역, v2는 원본 C3D·검출에 사용한 적용된 필터 높이 데이터·연결 영역입니다. 결과: v2는 선택한 검출 영역의 원본 높이만 남깁니다.",
                "Input: v1 uses a HeightField and domain; v2 uses the raw C3D, the Published FilteredHeightField used for detection, and a Connected Region. Result: v2 keeps raw heights only for the selected detected region."),
            ["editable-region"] = (
                "ThreeD.Tool.Description.EditableRegion",
                "입력: 적용된 연결 영역. 결과: 선택한 한 영역의 셀과 경계를 담은 편집용 결과.",
                "Input: a Published connected-region result. Result: an editable result containing one selected region's cells and bounds."),
            ["nine-point-surface-compensation"] = (
                "ThreeD.Tool.Description.NinePointSurfaceCompensation",
                "3×3 기준점으로 기판 곡면을 보정하고 국부 높이 차이를 유지합니다.",
                "Compensate a curved substrate from a 3×3 reference grid while retaining local height differences."),
            ["level-surface"] = (
                "ThreeD.Tool.Description.LevelSurface",
                "입력: 원본 높이 맵과 기준 ROI. 결과: 기준면에 맞춘 별도 높이 맵과 소프트웨어 기준 좌표계입니다.",
                "Input: a raw height map and reference ROIs. Result: a separate leveled map and software reference frame; the source is unchanged."),
            ["roi-crop"] = (
                "ThreeD.Tool.Description.RoiCrop",
                "입력: 높이 맵과 ROI. 결과: 지정한 영역을 후속 도구가 사용할 수 있는 별도 맵.",
                "Input: a height map and an ROI. Result: a separate map restricted to the authored region for later tools."),
            ["height-difference-edge"] = (
                "ThreeD.Tool.Description.HeightDifferenceEdge",
                "입력: 적용된 필터 맵과 검색 ROI. 결과: 각 스캔라인에서 선택한 인접 높이 차이의 에지 점 집합.",
                "Input: a Published filtered map and search ROI. Result: an EdgePointSet with one selected adjacent-height candidate per scanline."),
            ["two-point-line"] = (
                "ThreeD.Tool.Description.TwoPointLine",
                "입력: 순서가 있는 두 개의 C3D 격자 점. 결과: 두 점을 잇는 3D 선.",
                "Input: two ordered C3D grid picks. Result: a 3D line joining those picks."),
            ["three-point-plane"] = (
                "ThreeD.Tool.Description.ThreePointPlane",
                "입력: 순서가 있는 세 개의 C3D 격자 점. 결과: 세 점으로 만든 기준 평면.",
                "Input: three ordered C3D grid picks. Result: a datum plane constructed from the three points."),
            ["datum-plane-raw-height-deviation"] = (
                "ThreeD.Tool.Description.DatumPlaneDeviation",
                "입력: 원본 높이 맵, 적용된 기준 평면, 측정 ROI. 결과: 원본 높이와 평면의 P2V 편차.",
                "Input: a raw height map, a Published datum plane, and a measurement ROI. Result: raw-height peak-to-valley deviation from the plane."),
            ["three-d-line-fit"] = (
                "ThreeD.Tool.Description.ThreeDLineFit",
                "입력: 적용된 에지 점 집합. 결과: 선택된 점에 맞춘 3D 선과 피팅 근거.",
                "Input: a Published EdgePointSet. Result: a 3D line fitted to the accepted points with fit evidence."),
            ["physical-edge-gap-flush"] = (
                "ThreeD.Tool.Description.PhysicalEdgeGapFlush",
                "입력: 순서가 정해진 두 선과 두 에지 점 집합. 결과: 부호가 있는 간격·단차와 유효성/허용값 정보.",
                "Input: two ordered lines and two matching EdgePointSets. Result: signed gap/flush with validity and tolerance information."),
            ["line-intersection"] = (
                "ThreeD.Tool.Description.LineIntersection",
                "입력: 두 개의 적용된 3D 선. 결과: 조건을 통과한 코너 기준점.",
                "Input: two Published 3D lines. Result: a corner datum when the angle and closest-approach checks pass."),
            ["landmark-correspondence"] = (
                "ThreeD.Tool.Description.LandmarkCorrespondence",
                "입력: 네 개의 적용된 코너 기준점과 참조 좌표. 결과: 변환 계산에 사용할 대응 쌍.",
                "Input: four Published corner datums and reference coordinates. Result: correspondence pairs for a later transform."),
            ["xyz-affine-solve"] = (
                "ThreeD.Tool.Description.XyzAffineSolve",
                "입력: 적용된 대응 쌍 네 개. 결과: 원본과 참조 사이의 XYZ 변환과 잔차 정보이며 점 자체는 이동하지 않습니다.",
                "Input: four Published correspondence pairs. Result: an XYZ transform and residual information; points are not moved by this step."),
            ["xyz-affine-apply"] = (
                "ThreeD.Tool.Description.XyzAffineApply",
                "입력: 검증된 원본 C3D와 적용된 XYZ 변환. 결과: 행·열 위치를 유지한 변환 점 집합이며 재격자화나 측정은 하지 않습니다.",
                "Input: verified raw C3D and a Published XYZ transform. Result: a transformed point set with row/column locators preserved; it does not re-grid or measure."),
            ["re-grid-height-map"] = (
                "ThreeD.Tool.Description.RegridHeightMap",
                "입력: 적용된 변환 점 집합과 명시한 참조 격자. 결과: 참조 격자의 높이 맵이며 범위 밖 점과 결측은 그대로 판정합니다.",
                "Input: a Published transformed point set and an authored reference grid. Result: a height map on that grid with out-of-bounds points and missing cells reported by the contract."),
            ["surface-match"] = (
                "ThreeD.Tool.Description.SurfaceMatch",
                "입력: 적용된 표면 모델과 준비된 장면. 결과: 지정한 자세 범위 안의 매칭 결과와 품질 정보.",
                "Input: a Published surface model and a prepared scene. Result: a match assessment and quality information inside the authored pose bounds."),
            ["thickness"] = (
                "ThreeD.Tool.Description.Thickness",
                "입력: 높이 맵과 기준면/측정면 ROI. 결과: 기준면에서 측정면까지의 부호 있는 H축 두께.",
                "Input: a height map and Reference/Measurement surface ROIs. Result: signed H-axis thickness from the fitted reference surface to the measurement surface."),
            ["warpage"] = (
                "ThreeD.Tool.Description.Warpage",
                "입력: 높이 맵과 측정 ROI. 결과: ROI 안 기준 평면에 대한 높이 편차.",
                "Input: a height map and measurement ROI. Result: height residuals from a best-fit plane inside the ROI."),
            ["plane-flatness"] = (
                "ThreeD.Tool.Description.PlaneFlatness",
                "입력: 변환 높이 맵과 기준/측정 ROI. 결과: 측정 영역의 기준 평면 대비 평탄도.",
                "Input: a transformed height map and Reference/Measurement ROIs. Result: flatness of the measurement region against the reference plane."),
            ["point-pair-dimensions"] = (
                "ThreeD.Tool.Description.PointPairDimensions",
                "입력: 변환 높이 맵과 두 점. 결과: 두 점 사이의 3D 거리·평면 폭·높이 차이.",
                "Input: a transformed height map and two points. Result: 3D distance, planar width, and height difference between the points."),
            ["gap-flush"] = (
                "ThreeD.Tool.Description.GapFlush",
                "입력: 변환 높이 맵과 두 ROI. 결과: ROI 순서에 따른 부호 있는 간격과 평균 높이 단차.",
                "Input: a transformed height map and two ROIs. Result: signed gap and mean-height flush determined by ROI order."),
            ["volume"] = (
                "ThreeD.Tool.Description.Volume",
                "입력: 변환 높이 맵과 기준/측정 ROI. 결과: 기준면 대비 측정 영역의 부호 있는 체적.",
                "Input: a transformed height map and Reference/Measurement ROIs. Result: signed volume of the measurement region relative to the reference plane."),
            ["cross-section-dimensions"] = (
                "ThreeD.Tool.Description.CrossSectionDimensions",
                "입력: 변환 높이 맵과 한 줄 ROI. 결과: 단면의 폭과 높이 범위.",
                "Input: a transformed height map and a one-row ROI. Result: cross-section width and height range."),
            ["completeness-grid"] = (
                "ThreeD.Tool.Description.CompletenessGrid",
                "입력: 높이 맵, 기준 ROI, 검사 격자 또는 편집 영역. 결과: 셀별·전체 Pass/Fail 근거.",
                "Input: a height map, reference ROI, and inspection grid or editable region. Result: per-cell and aggregate Pass/Fail evidence."),
            ["connected-region-gate"] = (
                "ThreeD.Tool.Description.ConnectedRegionGate",
                "입력: 원본 C3D, 검출에 사용한 적용된 높이 데이터, 연결 영역. 결과: 선택한 검출 영역 하나의 유한 커버리지와 평균 높이 게이트이며 원본·검출·영역 출처를 함께 보존합니다. 검출되지 않음은 예상물 없음으로 판정하지 않습니다.",
                "Input: raw C3D, the Published HeightField used for detection, and a ConnectedRegionArtifact. Result: a bounded finite-coverage and mean-height gate for one selected detected region with raw/detection/region provenance. Missing detection is not inferred as expected-object absence."),
            ["overlay-control-review"] = (
                "ThreeD.Tool.Description.OverlayControlReview",
                "입력: 기존 측정 결과와 그 표시 상태. 결과: 선택한 결과 표시와 검토 정보.",
                "Input: an existing measurement result and its display state. Result: selected result overlays and review information.")
        };

    public static IReadOnlyList<ToolWorkbenchToolItem> Create() =>
    Localize(
    [
        new("Prepare", "Filter", "filter", 1, "HeightField", "FilteredHeightField", "Denoise the C3D raw-height source with the approved Median contract.", [new("Method", "Median"), new("KernelSize", "3"), new("MissingValuePolicy", "PreserveMask"), new("BoundaryPolicy", "AvailableNeighbors")]),
        new("Prepare", "Height Threshold Background Removal", "height-threshold-background-removal", 1, "HeightField", "FilteredHeightField", "Retain raw-height samples at an inclusive threshold in a separate same-grid output; original values and existing missing cells stay unchanged.", [new("Threshold", "0"), new("Mode", "KeepAtOrAboveThreshold"), new("ComparisonPolicy", C3DHeightThresholdBackgroundRemovalEvidence.ComparisonPolicyName), new("MissingValuePolicy", C3DHeightThresholdBackgroundRemovalEvidence.MissingValuePolicyName), new("BackgroundPolicy", C3DHeightThresholdBackgroundRemovalEvidence.BackgroundPolicyName)]),
        new("Prepare", "Signed Delta Background Subtraction", "signed-delta-background-subtraction", 1, "HeightField + identity-bound saved background", "SignedDeltaHeightField", "Compute signed current-minus-background deltas while preserving finite zero and distinguishing missing cells. The output is an in-memory evidence artifact, not a C3D file.", [new("BackgroundPath", "Select a C3D background"), new("BackgroundEntityId", "Set saved background ID"), new("BackgroundByteLength", "0"), new("BackgroundContentSha256", "Set 64-character SHA-256"), new("BackgroundWidth", "0"), new("BackgroundHeight", "0"), new("BackgroundUnit", "raw-height"), new("BackgroundFrameId", "Set frame ID"), new("SubtractionPolicy", C3DSignedHeightDeltaRecipePolicy.SubtractionPolicyName), new("MissingValuePolicy", C3DSignedHeightDeltaRecipePolicy.MissingValuePolicyName), new("ZeroDeltaPolicy", C3DSignedHeightDeltaRecipePolicy.ZeroDeltaPolicyName)]),
        new("Prepare", "Remove Outlier Pixels", "remove-outlier-pixels", 1, "HeightField", "FilteredHeightField", "Set isolated raw-height outliers missing when their local-median absolute deviation exceeds an explicit threshold. Original missing cells and source bytes stay unchanged.", [new("Rule", "LocalMedianAbsoluteDeviation"), new("WindowSize", "3"), new("MaximumAbsoluteDeviation", "25"), new("MinimumValidNeighbors", "3"), new("MissingValuePolicy", "PreserveMask"), new("BoundaryPolicy", "AvailableNeighbors"), new("OutlierPolicy", "SetMissing")]),
        new("Feature & Datum", "Connected Region", "connected-region", 1, "Published FilteredHeightField", "ConnectedRegionArtifact", "Label the exact published outlier mask as deterministic connected regions with source-grid cells, bounds, and optional G12 metrics.", [new("Connectivity", "Four"), new("OriginX", "0"), new("OriginY", "0"), new("ColumnPitch", "1"), new("RowPitch", "1"), new("AreaUnit", "grid-unit^2")]),
        new("Prepare", "Grid Polygon Domain", "grid-polygon-domain", 2, "HeightField + GridPolygon selection", "GridDomainArtifact", "Publish a source-bound authored GridPolygon domain using deterministic cell-center coverage; no height or physical scale is inferred.", []),
        new("Prepare", "Domain / Mask", "domain-mask", 2, "HeightField + domain (v1) or raw C3D + filtered detection HeightField + ConnectedRegionArtifact (v2)", "HeightField", "v1 reduces a complete validated domain; v2 applies one explicitly selected detected region from the Published filtered source to the original raw C3D without changing the detection source.", []),
        new("Feature & Datum", "Editable Region", "editable-region", 1, "Published ConnectedRegionArtifact", "EditableRegionArtifact", "Select one detected connected region by stable index as an exact source-grid editable artifact without changing the connected-region result or source data.", [new("SelectedRegionIndex", "0")]),
        new("Prepare", "Nine-point surface compensation", "nine-point-surface-compensation", 1, "HeightField", "HeightField",
            "Compensate a curved substrate from a 3×3 reference grid while retaining local height differences.",
            [new("MiddleRow", "1"), new("MiddleColumn", "1"), new("TargetHeight", "1"), new("Semantics", ToolRecipeNinePointSurfaceCompensationExecution.Semantics)]),
        new("Prepare", "Level Surface", "level-surface", 2, "Raw C3D HeightField + one or more GridRectangles", "LeveledHeightField + LevelingTransform + LevelFrame", "Fit one plane across explicit reference ROIs, remove its X/Z raw-height trend, and publish a reusable software level frame with a named source/reference/result frame chain without changing the source grid or missing mask.", [new("ReferenceFitPolicy", "LeastSquaresHeightPlane"), new("LevelingPolicy", "HeightDetrendToReferenceMean"), new("MissingValuePolicy", "PreserveMask"), new("GridPolicy", "PreserveSourceGrid"), new("MinimumValidSampleCount", "16"), new("MaximumReferenceRmsResidual", "5")]),
        new("Prepare", "ROI / Crop", "roi-crop", 1, "HeightField", "HeightField", "Restrict a later tool to an explicit source region.", [new("ROI", "Select in Viewer"), new("Output frame", "Keep source frame")]),
        new("Feature & Datum", "Height Difference Edge", "height-difference-edge", 1, "Published FilteredHeightField + GridRectangle", "EdgePointSet", "Extract one deterministic adjacent-height candidate per scanline in an explicit search band.", [new("ComparisonAxis", "AcrossColumns"), new("Polarity", "Rising"), new("MinimumDelta", "Set explicitly"), new("CandidatePolicy", "StrongestPerScanline"), new("PointPolicy", "PairMidpoint"), new("MissingValuePolicy", "SkipPair"), new("BoundaryPolicy", "WithinSelection")]),
        new("Feature & Datum", "2-Point Line", "two-point-line", 1, "Raw C3D HeightField + PointSet(2)", "LineFeature", "Construct a named ordered full-XYZ segment from two recipe-owned C3D grid picks.", [new("OutputRole", "Set explicitly"), new("ConstructionPolicy", "OrderedPointsDefineSegment")]),
        new("Feature & Datum", "3-Point Plane", "three-point-plane", 2, "Raw C3D HeightField + PointSet(3)", "PlaneFeature", "Construct a named ordered full-XYZ datum plane from three recipe-owned C3D grid picks.", [new("OutputRole", "Set explicitly"), new("ConstructionPolicy", "OrderedPointsDefineOrientedPlane")]),
        new("Measure", "Datum Plane Raw-Height Deviation", "datum-plane-raw-height-deviation", 2, "Raw C3D HeightField + Published PlaneFeature + GridRectangle", "DatumPlaneDeviationResult", "Measure raw-height residual P2V against one published manual datum plane without changing or re-gridding the source C3D.", [new("MaximumPeakToValleyRawHeight", "Set explicitly"), new("OutputRole", "Set explicitly"), new("ResidualPolicy", "RawHeightMinusDatumPlanePredictedRawHeight"), new("MinimumValidSampleCount", "3"), new("MinimumAbsoluteNormalY", "0.1")]),
        new("Feature & Datum", "3D Line Fit", "three-d-line-fit", 1, "Published EdgePointSet", "LineFeature", "Fit one deterministic full-XYZ line to an explicit published edge-point entity.", [new("FitMethod", "DeterministicConsensusOrthogonalTls"), new("MaximumOrthogonalResidual", "Set explicitly"), new("MinimumInlierCount", "Set explicitly"), new("MinimumInlierRatio", "Set explicitly"), new("MinimumInlierScanlineSpan", "Set explicitly"), new("HypothesisPolicy", "Sha256PairSchedule"), new("MaximumHypotheses", "256"), new("RefinementPolicy", "OrthogonalTlsUntilStable10"), new("DirectionPolicy", "PositiveScanlineAxis"), new("EndpointPolicy", "InlierProjectionExtents")]),
        new("Measure", "Physical Edge Gap / Flush", "physical-edge-gap-flush", 4, "Published LineFeature (FirstLine) + Published LineFeature (SecondLine) + Published EdgePointSet (FirstEdge) + Published EdgePointSet (SecondEdge)", "MeasurementResult", "Pair two explicitly ordered published line/edge evidences and expose signed gap/flush, support, ambiguity, validity, and tolerance evidence. Preview and Publish are explicit; Run replays the published recipe.", CreateDefaultPhysicalEdgeGapFlushParameters()),
        new("Feature & Datum", "Line Intersection", "line-intersection", 2, "Published LineFeature + LineFeature", "CornerAnchor", "Create a corner anchor only after full-XYZ closest-approach, angle, and bounded-support gates pass.", [new("MaximumClosestApproachDistance", "Set explicitly"), new("MinimumAcuteAngleDegrees", "Set explicitly"), new("MaximumSupportExtension", "Set explicitly"), new("OutputRole", "Set explicitly"), new("ClosestApproachPolicy", "MidpointOfClosestPoints"), new("ParallelPolicy", "RejectBelowMinimumAcuteAngle"), new("SupportPolicy", "WithinInlierProjectionExtentsWithMaximumExtension")]),
        new("Feature & Datum", "Landmark Correspondence", "landmark-correspondence", 1, "Correspondence selection", "CorrespondenceSet", "Validate four named Published CornerAnchors against explicit reference coordinates before a later transform.", [new("PairCountPolicy", "ExactlyFour"), new("SourceArtifactPolicy", "CurrentPublishedCornerAnchor"), new("AffineIndependencePolicy", "RequireNonDegenerateTetrahedra")]),
        new("Transform", "XYZ Affine Solve", "xyz-affine-solve", 1, "Published CorrespondenceSet", "AffineTransform3D", "Solve one full XYZ source-to-reference matrix from exactly four published correspondence pairs. It does not move C3D points.", [new("SolvePolicy", "ExactFourPartialPivot"), new("MaximumConditionEstimate", "1000000"), new("ArithmeticResidualWarning", "0.001")]),
        new("Transform", "Apply XYZ Affine", "xyz-affine-apply", 2, "Verified raw C3D + Published AffineTransform3D", "TransformedPointCloud", "Transform each finite raw source-grid point once in full XYZ while preserving its row/column locator. It does not re-grid, interpolate, triangulate, or measure.", []),
        new("Transform", "Re-grid Height Map", "re-grid-height-map", 1, "Published TransformedPointCloud", "TransformedHeightField", "Project a published full-XYZ cloud into one explicit right-handed reference grid. Missing cells remain missing; Preview rejects out-of-bounds points and Publish requires authored coverage.", CreateDefaultRegridParameters()),
        new("Match", "Surface Match", "surface-match", 2, "Published SurfaceModel + Prepared Scene", "SurfaceMatchAssessment", "Search only inside explicit pose bounds, preserve raw coverage and overlay, then apply separate authored Pass/Fail limits. Parameter Apply never executes Preview or Run.", CreateDefaultSurfaceMatchParameters(), RouteKind: ToolWorkbenchToolRouteKind.SpecialExperiment),
        new("Measure", "Thickness", "thickness", 3, "HeightField + Reference GridRectangle + Measurement GridRectangle", "MeasurementResult", "Fit a reference surface, then measure signed H-axis separation across a second recipe-owned ROI. Preview remains explicit.", [new("MinimumThickness", "0"), new("MaximumThickness", "100000"), new("MinimumValidSampleCount", "1")]),
        new("Measure", "Warpage", "warpage", 2, "HeightField + GridRectangle", "MeasurementResult", "Measure best-fit-plane residual warpage inside one recipe-owned grid ROI. This is one reusable measurement tool, not a product mode.", [new("MaximumPeakToValley", "100000"), new("MaximumRms", "100000"), new("MinimumValidSampleCount", "3")]),
        new("Measure", "Plane Flatness", "plane-flatness", 3, "TransformedHeightField + Reference GridRectangle + Measurement GridRectangle", "MeasurementResult", "Fit a reference plane in one artifact-owned ROI and measure signed-distance flatness in a second ROI. Preview and Publish remain explicit.", [new("MaximumFlatness", "100000"), new("MinimumReferenceSampleCount", "3"), new("MinimumMeasurementSampleCount", "3")]),
        new("Measure", "Point Pair Dimensions", "point-pair-dimensions", 2, "TransformedHeightField + PointSet(2)", "MeasurementResult", "Measure full-XYZ distance, reference-plane width, height-axis delta, and elevation angle between two ordered artifact-owned picks.", [new("ExpectedDistance", "0"), new("DistanceTolerance", "100000"), new("ExpectedPlanarWidth", "0"), new("PlanarWidthTolerance", "100000"), new("ExpectedElevationAngleDegrees", "0"), new("ElevationAngleToleranceDegrees", "90")]),
        new("Measure", "Gap / Flush", "gap-flush", 3, "TransformedHeightField + First GridRectangle + Second GridRectangle", "MeasurementResult", "Measure signed U-axis separation between facing ROI edges and signed H-axis mean-height difference. ROI order defines the sign; Preview and Publish remain explicit.", [new("ExpectedGap", "0"), new("GapTolerance", "100000"), new("ExpectedFlush", "0"), new("FlushTolerance", "100000")]),
        new("Measure", "Volume", "volume", 3, "TransformedHeightField + Reference GridRectangle + Measurement GridRectangle", "MeasurementResult", "Fit an A3 reference-plane ROI and integrate signed H-axis height over a second ROI. Values remain in the declared reference-grid model unit cubed; Preview and Publish remain explicit.", [new("ExpectedNetVolume", "0"), new("VolumeTolerance", "100000")]),
        new("Measure", "Cross-section Dimensions", "cross-section-dimensions", 2, "TransformedHeightField + one-row GridRectangle", "MeasurementResult", "Measure U-axis width and H-axis range along one explicitly authored A3 row segment. Preview and Publish remain explicit.", [new("ExpectedWidth", "0"), new("WidthTolerance", "100000"), new("ExpectedHeightRange", "0"), new("HeightTolerance", "100000")]),
        new("Measure", "Completeness Grid", "completeness-grid", 3, "HeightField + Reference GridRectangle + Inspection GridRectangle or EditableRegionArtifact", "CompletenessGridMetrics", "Generate deterministic native-grid cells from an Inspection GridRectangle or the selected editable-region bounds, apply inclusive finite-coverage and reference-relative height limits, then publish cell and aggregate Pass/Fail evidence.", [new("Rows", "2"), new("Columns", "2"), new("XPitchColumns", "2"), new("ZPitchRows", "2"), new("CellWidthColumns", "2"), new("CellHeightRows", "2"), new("CellShape", "GridRectangle"), new("MinimumFiniteCoverageRatio", "0.95"), new("MinimumReferenceRelativeMeanRawHeight", "-100000"), new("MaximumReferenceRelativeMeanRawHeight", "100000")]),
        new("Measure", "Connected Region Coverage / Height Gate", "connected-region-gate", 3, "Raw C3D + Published FilteredHeightField + ConnectedRegionArtifact", "ConnectedRegionGate", "Evaluate one explicitly selected detected region through the existing G13/G14 coverage and height adapters without changing the raw source or inferring expected-object absence.", [new("GateMode", "Presence"), new("SelectedRegionIndex", "0"), new("MinimumFiniteCoverageRatio", "0.95"), new("MinimumMeanHeight", "-100000"), new("MaximumMeanHeight", "100000"), new("ReferenceSlopeX", "0"), new("ReferenceSlopeZ", "0"), new("ReferenceIntercept", "0")]),
        new("Review", "Overlay / Control Review", "overlay-control-review", 1, "MeasurementResult", "ReviewOverlay", "Group overlays, controls, acceptance, and run evidence without changing the source.", [new("Overlay", "Selected results"), new("Publish", "Explicit")], RouteKind: ToolWorkbenchToolRouteKind.ReviewOnly)
    ]);

    private static IReadOnlyList<ToolWorkbenchToolItem> Localize(
        IReadOnlyList<ToolWorkbenchToolItem> tools) =>
        tools.Select(ApplyLocalization).ToArray();

    private static ToolWorkbenchToolItem ApplyLocalization(ToolWorkbenchToolItem tool)
    {
        if (!LocalizedToolNames.TryGetValue(tool.Id, out var localization))
        {
            localization = ($"ThreeD.Tool.{tool.Id}", tool.Name, Array.Empty<string>());
        }

        LocalizedToolDescriptions.TryGetValue(tool.Id, out var description);

        return tool with
        {
            LocalizedNameKey = localization.Key,
            KoreanDisplayName = localization.Korean,
            SearchAliases = localization.Aliases,
            LocalizedDescriptionKey = string.IsNullOrWhiteSpace(description.Key)
                ? null
                : description.Key,
            KoreanDescription = description.Korean,
            EnglishDescription = description.English
        };
    }

    private static IReadOnlyList<ToolWorkbenchParameterSeed> CreateDefaultRegridParameters() =>
        C3DReferenceGridProfile.Create(
            "frame.reference", "unitless", "Authored reference grid", "R1",
            new C3DReferenceGridVector(0, 0, 0),
            new C3DReferenceGridVector(1, 0, 0),
            new C3DReferenceGridVector(0, 1, 0),
            new C3DReferenceGridVector(0, 0, 1),
            1, 1, 1, 1, 1)
        .ToRecipeParameters()
        .Select(parameter => new ToolWorkbenchParameterSeed(parameter.Name, parameter.Value))
        .ToArray();

    private static IReadOnlyList<ToolWorkbenchParameterSeed>
        CreateDefaultSurfaceMatchParameters() =>
    [
        new("MinimumCoverageRatio", "0.9"),
        new("MaximumInlierRmse", "0.25"),
        new("MinimumEdgeCoverageRatio", "0.9"),
        new("MaximumEdgeInlierRmse", "0.25"),
        new("MinimumRotationXDegrees", "0"),
        new("MaximumRotationXDegrees", "0"),
        new("RotationStepXDegrees", "1"),
        new("MinimumRotationYDegrees", "0"),
        new("MaximumRotationYDegrees", "0"),
        new("RotationStepYDegrees", "1"),
        new("MinimumRotationZDegrees", "-45"),
        new("MaximumRotationZDegrees", "45"),
        new("RotationStepZDegrees", "15"),
        new("MinimumTranslationX", "-10"),
        new("MaximumTranslationX", "10"),
        new("MinimumTranslationY", "-10"),
        new("MaximumTranslationY", "10"),
        new("MinimumTranslationZ", "-10"),
        new("MaximumTranslationZ", "10"),
        new("MaximumCorrespondenceDistance", "1"),
        new("MinimumMatchedSampleCount", "3"),
        new("MaximumCandidateCount", "10000")
    ];

    private static IReadOnlyList<ToolWorkbenchParameterSeed>
        CreateDefaultPhysicalEdgeGapFlushParameters() =>
        C3DPhysicalEdgeGapFlushProfile.Default
            .ToRecipeParameters()
            .Select(parameter => new ToolWorkbenchParameterSeed(parameter.Name, parameter.Value))
            .ToArray();
}
