using System.ComponentModel;
using System.Globalization;
using OpenVisionLab;

namespace OpenVisionLab.ThreeD.Shell.PropertyGrid;

internal static class LocalizedPropertyGridObject
{
    internal static readonly IReadOnlyDictionary<string, string> OptionTranslations = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["OrderedPointsDefineSegment"] = "두 점을 순서대로 연결",
        ["OrderedPointsDefineOrientedPlane"] = "세 점의 순서로 평면 방향 결정",
        ["RawHeightMinusDatumPlanePredictedRawHeight"] = "원시 높이에서 기준면 높이를 뺌",
        ["Median"] = "중앙값 필터",
        ["PreserveMask"] = "누락 위치 유지",
        ["AvailableNeighbors"] = "사용 가능한 주변 셀",
        ["KeepAtOrAboveThreshold"] = "임계값 이상 유지",
        ["KeepAtOrBelowThreshold"] = "임계값 이하 유지",
        ["LocalMedianAbsoluteDeviation"] = "주변 중앙값과의 높이차",
        ["SetMissing"] = "누락값으로 처리",
        ["Four"] = "4방향 연결",
        ["Eight"] = "8방향 연결",
        ["LeastSquaresHeightPlane"] = "최소제곱 평면 피팅",
        ["HeightDetrendToReferenceMean"] = "기준 평균 높이로 기울기 보정",
        ["PreserveSourceGrid"] = "원본 격자 유지",
        ["Unspecified"] = "미설정",
        ["AcrossColumns"] = "열 방향",
        ["AcrossRows"] = "행 방향",
        ["Rising"] = "상승 에지",
        ["Falling"] = "하강 에지",
        ["Absolute"] = "절대 높이차",
        ["StrongestPerScanline"] = "스캔별 최대 높이차",
        ["PairMidpoint"] = "두 셀의 중점",
        ["SkipPair"] = "누락값이 있으면 제외",
        ["WithinSelection"] = "선택 영역 안에서만 처리",
        ["DeterministicConsensusOrthogonalTls"] = "합의 기반 직교 최소제곱 피팅",
        ["Sha256PairSchedule"] = "고정 순서로 점 쌍 선택",
        ["OrthogonalTlsUntilStable10"] = "최대 10회 직교 피팅",
        ["PositiveScanlineAxis"] = "스캔 축의 양의 방향",
        ["InlierProjectionExtents"] = "유효점의 투영 범위",
        ["MidpointOfClosestPoints"] = "최근접 두 점의 중점",
        ["RejectBelowMinimumAcuteAngle"] = "최소 예각 미만 제외",
        ["WithinInlierProjectionExtentsWithMaximumExtension"] = "유효점 범위와 최대 연장 거리",
        ["ExactlyFour"] = "4쌍",
        ["CurrentPublishedCornerAnchor"] = "현재 적용된 교점",
        ["RequireNonDegenerateTetrahedra"] = "네 점이 같은 평면에 놓이지 않음",
        ["ExactFourPartialPivot"] = "4쌍으로 변환 계산",
        ["PlanarNearestCellCenter"] = "평면에서 가장 가까운 셀 중심",
        ["NearestCenterThenSourceLocator"] = "가장 가까운 중심·원본 위치 순",
        ["RejectPreview"] = "미리보기 중단",
        ["PreserveMissing"] = "누락 셀 유지",
        ["InclusiveFiniteSamplePredicate"] = "임계값 포함 비교",
        ["PreserveExistingMissing"] = "기존 누락값 유지",
        ["FailPredicateToMissing"] = "조건 밖 값을 누락으로 처리",
        ["CurrentMinusSavedBackground"] = "현재 높이에서 배경 높이를 뺌",
        ["MissingIfEitherInputMissing"] = "어느 입력이든 누락되면 제외",
        ["PreserveFiniteZeroInSignedDeltaArtifact"] = "0 높이차를 유효값으로 유지",
        ["GridRectangle"] = "사각형 셀",
        ["Presence"] = "유효 영역 검사",
        ["FillHeight"] = "충전 높이 검사",
        ["Raw source + Published A1"] = "원본 데이터와 적용된 변환 행렬",
        ["No re-grid / no measurement"] = "격자 변환·측정 안 함",
        ["Complete Published ConnectedRegionArtifact"] = "적용된 전체 연결 영역",
        ["Selected Published ConnectedRegionArtifact region"] = "적용된 연결 영역 중 선택한 영역",
        ["v1: selected HeightField; v2: recipe raw C3D"] = "v1: 선택한 높이 데이터 / v2: 원본 높이 데이터",
        ["v2: exact Published FilteredHeightField"] = "v2: 검출에 사용한 필터 높이 데이터",
        ["Outside → missing; inside → preserve"] = "영역 밖은 누락 / 안은 유지",
        ["Explicit Preview → Publish → Run"] = "미리보기 → 결과 적용 → 검사",
        ["First"] = "첫 번째 면",
        ["Second"] = "두 번째 면",
        ["MeanInlierPairs"] = "유효점 쌍의 평균",
        ["RejectNearTie"] = "비슷한 후보가 있으면 제외",
        ["Enabled"] = "사용",
        ["Disabled"] = "사용 안 함",
        ["true"] = "허용",
        ["false"] = "허용 안 함",
    };

    internal static string LocalizeOption(string value) => OpenVisionLanguageService.CurrentLanguage == OpenVisionLanguage.English
        ? value : OptionTranslations.GetValueOrDefault(value, value);


    public static object Create(object source, bool includeAdvanced = true) =>
        Activator.CreateInstance(
            typeof(LocalizedPropertyGridObject<>).MakeGenericType(source.GetType()),
            source, includeAdvanced)!;
}

public sealed class PropertyGridOptionTextConverter : System.Windows.Data.IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is null ? string.Empty : LocalizedPropertyGridObject.LocalizeOption(value.ToString()!);

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        System.Windows.Data.Binding.DoNothing;
}

internal sealed class LocalizedPropertyGridObject<T>(T source, bool includeAdvanced) : CustomTypeDescriptor
    where T : class
{
    internal T Source => source;

    public override AttributeCollection GetAttributes() =>
        TypeDescriptor.GetAttributes(source, noCustomTypeDesc: true);

    public override string? GetClassName() =>
        TypeDescriptor.GetClassName(source, noCustomTypeDesc: true);

    public override string? GetComponentName() =>
        TypeDescriptor.GetComponentName(source, noCustomTypeDesc: true);

    public override TypeConverter GetConverter() =>
        TypeDescriptor.GetConverter(source, noCustomTypeDesc: true);

    public override EventDescriptor? GetDefaultEvent() =>
        TypeDescriptor.GetDefaultEvent(source, noCustomTypeDesc: true);

    public override PropertyDescriptor? GetDefaultProperty() =>
        TypeDescriptor.GetDefaultProperty(source, noCustomTypeDesc: true);

    public override object? GetEditor(Type editorBaseType) =>
        TypeDescriptor.GetEditor(source, editorBaseType, noCustomTypeDesc: true);

    public override EventDescriptorCollection GetEvents() =>
        TypeDescriptor.GetEvents(source, noCustomTypeDesc: true);

    public override EventDescriptorCollection GetEvents(Attribute[]? attributes) =>
        TypeDescriptor.GetEvents(source, attributes, noCustomTypeDesc: true);

    public override PropertyDescriptorCollection GetProperties() =>
        GetProperties(null);

    public override PropertyDescriptorCollection GetProperties(Attribute[]? attributes)
    {
        var properties = TypeDescriptor.GetProperties(source, noCustomTypeDesc: true)
            .Cast<PropertyDescriptor>()
            .Where(property => property.Name != "UnmappedParameters"
                || !Equals(property.GetValue(source), "(none)"))
            .Where(property => includeAdvanced || property.Name is not ("MinimumReferenceSampleCount" or "MinimumMeasurementSampleCount" or "MinimumValidSampleCount" or "MinimumCoverageRatio"))
            .Select(property => new LocalizedPropertyDescriptor(property))
            .ToArray();
        return new PropertyDescriptorCollection(properties, readOnly: true);
    }

    public override object GetPropertyOwner(PropertyDescriptor? propertyDescriptor) => this;

    private sealed class LocalizedPropertyDescriptor(PropertyDescriptor property)
        : PropertyDescriptor(property.Name, property.Attributes.Cast<Attribute>().ToArray())
    {
        public override Type ComponentType => property.ComponentType;
        public override bool IsReadOnly => property.IsReadOnly;
        public override Type PropertyType => property.PropertyType;
        public override string DisplayName => LocalizePropertyName(property.Name, property.DisplayName);
        public override string Category => LocalizeCategory(property.Category);
        public override string Description => property.Name == "ConstructionPolicy"
            ? L("지정한 점의 순서로 직선이나 기준면을 정의합니다. 피팅·스냅·보간은 하지 않습니다.", property.Description) : property.Description;
        public override TypeConverter Converter => property.PropertyType.IsEnum ? new LocalizedEnumConverter(property.PropertyType)
            : property.PropertyType == typeof(string) && (property.IsReadOnly || property.Name is "ComparisonAxis" or "FirstSurfaceSide" or "SecondSurfaceSide" or "SurfaceAggregation" or "AmbiguityPolicy" or "ContactAllowed" or "ToleranceMode")
                ? new LocalizedStringConverter() : property.Converter;
        public override bool SupportsChangeEvents => property.SupportsChangeEvents;

        public override bool CanResetValue(object component) => property.CanResetValue(GetSource(component));

        public override object? GetValue(object? component)
        {
            var value = property.GetValue(GetSource(component));
            return property.Name == "UnmappedParameters" && Equals(value, "(none)")
                ? L("(없음)", "(none)")
                : value;
        }

        public override void ResetValue(object component) => property.ResetValue(GetSource(component));

        public override void SetValue(object? component, object? value) => property.SetValue(GetSource(component), value);

        public override bool ShouldSerializeValue(object component) => property.ShouldSerializeValue(GetSource(component));

        public override void AddValueChanged(object component, EventHandler handler) =>
            property.AddValueChanged(GetSource(component), handler);

        public override void RemoveValueChanged(object component, EventHandler handler) =>
            property.RemoveValueChanged(GetSource(component), handler);

        private static T GetSource(object? component) =>
            ((LocalizedPropertyGridObject<T>)component!).Source;
    }

    private sealed class LocalizedEnumConverter(Type enumType) : EnumConverter(enumType)
    {
        public override object? ConvertTo(ITypeDescriptorContext? context, CultureInfo? culture, object? value, Type destinationType) =>
            destinationType == typeof(string) && value is not null ? LocalizedPropertyGridObject.LocalizeOption(value.ToString()!) : base.ConvertTo(context, culture, value, destinationType);

        public override object? ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object value)
        {
            if (value is string text)
            {
                foreach (var option in GetStandardValues(context)!)
                    if (string.Equals(LocalizedPropertyGridObject.LocalizeOption(option!.ToString()!), text, StringComparison.Ordinal)) return option;
            }
            return base.ConvertFrom(context, culture, value);
        }
    }

    private sealed class LocalizedStringConverter : StringConverter
    {
        public override object? ConvertTo(ITypeDescriptorContext? context, CultureInfo? culture, object? value, Type destinationType) =>
            destinationType == typeof(string) && value is string text ? LocalizedPropertyGridObject.LocalizeOption(text) : base.ConvertTo(context, culture, value, destinationType);

        public override object? ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object value)
        {
            if (OpenVisionLanguageService.CurrentLanguage == OpenVisionLanguage.Korean && value is string text)
            {
                foreach (var option in LocalizedPropertyGridObject.OptionTranslations)
                    if (string.Equals(option.Value, text, StringComparison.Ordinal)) return option.Key;
            }
            return base.ConvertFrom(context, culture, value);
        }
    }

    private static string LocalizePropertyName(string name, string fallback) =>
        OpenVisionLanguageService.CurrentLanguage == OpenVisionLanguage.English
            ? name switch
            {
                "MinimumAcuteAngleDegrees" => $"{fallback} (°)",
                _ => fallback
            }
            : name switch
            {
                "AffineIndependencePolicy" => "대응점 독립 조건",
                "ArithmeticResidualWarning" => "산술 잔차 경고",
                "BoundaryPolicy" => "경계 처리",
                "CellAssignment" => "셀 할당",
                "CandidatePolicy" => "검출점 선택",
                "CollisionPolicy" => "충돌 처리 방법",
                "ColumnCount" => "열 수",
                "ClosestApproachPolicy" => "최근접 처리 방법",
                "ComparisonAxis" => "비교 축",
                "ConstructionPolicy" => "생성 방법",
                "DirectionPolicy" => "방향 처리 방법",
                "DistanceTolerance" => "거리 허용오차",
                "ElevationAngleToleranceDegrees" => "높이각 허용오차(도)",
                "EndpointPolicy" => "끝점 처리 방법",
                "ExcludedOperations" => "제외 연산",
                "ExecutionPolicy" => "실행 처리 방법",
                "ExpectedDistance" => "기준 거리",
                "ExpectedElevationAngleDegrees" => "기준 높이각(도)",
                "ExpectedFlush" => "기준 단차",
                "ExpectedGap" => "기준 간격",
                "ExpectedHeightRange" => "기준 높이 범위",
                "ExpectedNetVolume" => "기준 순체적",
                "ExpectedPlanarWidth" => "기준 평면 폭",
                "ExpectedWidth" => "기준 폭",
                "FitMethod" => "피팅 방법",
                "FlushTolerance" => "단차 허용오차",
                "GapTolerance" => "간격 허용오차",
                "HeightTolerance" => "높이 허용오차",
                "HAxisX" => "높이 축 X",
                "HAxisY" => "높이 축 Y",
                "HAxisZ" => "높이 축 Z",
                "HolePolicy" => "누락 셀 처리",
                "HypothesisPolicy" => "피팅 후보 선택",
                "KernelSize" => "필터 크기",
                "MaximumClosestApproachDistance" => "최대 최근접 거리",
                "MaximumConditionEstimate" => "최대 조건 추정값",
                "MaximumFlatness" => "최대 평탄도",
                "MaximumHypotheses" => "최대 피팅 후보 수",
                "MaximumOrthogonalResidual" => "최대 직교 잔차",
                "MaximumPeakToValley" => "최대 P2V",
                "MaximumPeakToValleyRawHeight" => "최대 원시 높이 P2V",
                "MaximumRms" => "최대 RMS",
                "MaximumSupportExtension" => "최대 직선 연장 거리",
                "MaximumThickness" => "최대 두께",
                "Method" => "방법",
                "MinimumAbsoluteNormalY" => "최소 |법선 Y|",
                "MinimumAcuteAngleDegrees" => "최소 예각(도)",
                "MinimumDelta" => "최소 높이차",
                "MinimumInlierCount" => "최소 유효점 수",
                "MinimumInlierRatio" => "최소 유효점 비율",
                "MinimumInlierScanlineSpan" => "최소 유효 스캔 범위",
                "MinimumMeasurementSampleCount" => "최소 측정 샘플 수",
                "MinimumReferenceSampleCount" => "최소 기준 샘플 수",
                "MinimumThickness" => "최소 두께",
                "MinimumCoverageRatio" => "최소 유효 셀 비율",
                "MinimumValidSampleCount" => "최소 유효 샘플 수",
                "MissingValuePolicy" => "누락값 처리",
                "OutputRole" => "출력 이름",
                "OriginX" => "원점 X",
                "OriginY" => "원점 Y",
                "OriginZ" => "원점 Z",
                "OutOfBoundsPolicy" => "범위 밖 처리 방법",
                "PairCountPolicy" => "대응 쌍 수 처리 방법",
                "ParallelPolicy" => "평행 처리 방법",
                "Path" => "경로",
                "PlanarWidthTolerance" => "평면 폭 허용오차",
                "PointPolicy" => "검출점 위치",
                "Polarity" => "극성",
                "PitchU" => "U 피치",
                "PitchV" => "V 피치",
                "ReferenceFrameId" => "기준 좌표계 ID",
                "ReferenceProvenance" => "기준 출처",
                "ReferenceRevision" => "기준 버전",
                "ReferenceUnit" => "기준 단위",
                "RefinementPolicy" => "피팅 보정 방법",
                "ResidualPolicy" => "잔차 처리 방법",
                "RowCount" => "행 수",
                "SolvePolicy" => "계산 처리 방법",
                "SourceArtifactPolicy" => "입력 데이터 조건",
                "SupportPolicy" => "직선 유효 범위",
                "UnmappedParameters" => "추가 설정값",
                "UAxisX" => "U 축 X",
                "UAxisY" => "U 축 Y",
                "UAxisZ" => "U 축 Z",
                "VAxisX" => "V 축 X",
                "VAxisY" => "V 축 Y",
                "VAxisZ" => "V 축 Z",
                "VolumeTolerance" => "체적 허용오차",
                "WidthTolerance" => "폭 허용오차",
                "DomainPolicy" => "사용 영역",
                "RawSourcePolicy" => "원본 데이터",
                "DetectionSourcePolicy" => "검출 데이터",
                "SelectedRegionIndex" => "선택 영역 번호",
                "MaximumInlierRmse" => "최대 유효점 RMSE",
                "MinimumEdgeCoverageRatio" => "최소 에지 검출 비율",
                "MaximumEdgeInlierRmse" => "최대 에지 유효점 RMSE",
                "MinimumRotationXDegrees" => "X 회전 하한(도)",
                "MaximumRotationXDegrees" => "X 회전 상한(도)",
                "RotationStepXDegrees" => "X 회전 간격(도)",
                "MinimumRotationYDegrees" => "Y 회전 하한(도)",
                "MaximumRotationYDegrees" => "Y 회전 상한(도)",
                "RotationStepYDegrees" => "Y 회전 간격(도)",
                "MinimumRotationZDegrees" => "Z 회전 하한(도)",
                "MaximumRotationZDegrees" => "Z 회전 상한(도)",
                "RotationStepZDegrees" => "Z 회전 간격(도)",
                "MinimumTranslationX" => "X 이동 하한",
                "MaximumTranslationX" => "X 이동 상한",
                "MinimumTranslationY" => "Y 이동 하한",
                "MaximumTranslationY" => "Y 이동 상한",
                "MinimumTranslationZ" => "Z 이동 하한",
                "MaximumTranslationZ" => "Z 이동 상한",
                "MaximumCorrespondenceDistance" => "최대 대응 거리",
                "MinimumMatchedSampleCount" => "최소 매칭 점 수",
                "MaximumCandidateCount" => "최대 후보 수",
                "SectionId" => "측정 구간 ID",
                "FirstSurfaceSide" => "첫 번째 면",
                "SecondSurfaceSide" => "두 번째 면",
                "SurfaceAggregation" => "면 높이 계산",
                "MinimumValidPairCount" => "최소 유효점 쌍 수",
                "MaximumResidual" => "최대 잔차",
                "AmbiguityPolicy" => "유사 후보 처리",
                "AmbiguityEpsilon" => "유사 후보 판별 오차",
                "ContactAllowed" => "접촉 허용",
                "ToleranceMode" => "허용오차 판정",
                "Rows" => "행 수",
                "Columns" => "열 수",
                "XPitchColumns" => "X 간격(열)",
                "ZPitchRows" => "Z 간격(행)",
                "CellWidthColumns" => "셀 너비(열)",
                "CellHeightRows" => "셀 높이(행)",
                "CellShape" => "셀 형상",
                "MinimumFiniteCoverageRatio" => "최소 유효 셀 비율",
                "MinimumReferenceRelativeMeanRawHeight" => "기준면 대비 평균 높이 하한",
                "MaximumReferenceRelativeMeanRawHeight" => "기준면 대비 평균 높이 상한",
                "GateMode" => "검사 방법",
                "MinimumMeanHeight" => "평균 높이 하한",
                "MaximumMeanHeight" => "평균 높이 상한",
                "ReferenceSlopeX" => "기준면 X 기울기",
                "ReferenceSlopeZ" => "기준면 Z 기울기",
                "ReferenceIntercept" => "기준면 높이",
                "Threshold" => "높이 임계값",
                "Mode" => "처리 방향",
                "ComparisonPolicy" => "비교 방법",
                "BackgroundPolicy" => "배경 처리",
                "BackgroundPath" => "배경 파일",
                "BackgroundEntityId" => "배경 데이터 ID",
                "BackgroundByteLength" => "배경 파일 크기(바이트)",
                "BackgroundContentSha256" => "배경 파일 식별값(SHA-256)",
                "BackgroundWidth" => "배경 너비(열)",
                "BackgroundHeight" => "배경 높이(행)",
                "BackgroundUnit" => "배경 높이 단위",
                "BackgroundFrameId" => "배경 좌표계 ID",
                "SubtractionPolicy" => "높이차 계산",
                "ZeroDeltaPolicy" => "0 높이차 처리",
                "Rule" => "제거 방법",
                "WindowSize" => "주변 셀 범위",
                "MaximumAbsoluteDeviation" => "최대 절대 높이차",
                "MinimumValidNeighbors" => "최소 유효 주변 셀 수",
                "OutlierPolicy" => "이상값 처리",
                "Connectivity" => "연결 방향",
                "ColumnPitch" => "열 간격",
                "RowPitch" => "행 간격",
                "AreaUnit" => "면적 단위",
                "ReferenceFitPolicy" => "기준면 피팅",
                "LevelingPolicy" => "수평 보정 방법",
                "MaximumReferenceRmsResidual" => "최대 기준면 RMS 잔차",
                "GridPolicy" => "격자 처리",
                _ => fallback
            };

    private static string LocalizeCategory(string category) =>
        OpenVisionLanguageService.CurrentLanguage == OpenVisionLanguage.English
            ? category
            : category switch
            {
                "Acceptance" => "허용 기준",
                "A3 publish policy" => "결과 적용 조건",
                "A3 reference grid" => "기준 그리드",
                "Execution" => "실행",
                "Geometry" => "형상",
                "Input" => "입력",
                "Measurement" => "측정",
                "Output" => "출력",
                "Policy" => "처리 방법",
                "Solve policy" => "계산 처리 방법",
                "Construction" => "생성 방법",
                "Fixed v1 policy" => "고정 조건",
                "Compatibility" => "추가 설정",
                "Numerical review" => "수치 검토",
                "A2 contract" => "좌표 변환 조건",
                "D-07 contract" => "영역 적용 조건",
                "Region" => "영역",
                "Surface acceptance" => "표면 판정 기준",
                "Edge acceptance" => "에지 판정 기준",
                "Rotation search (deg)" => "회전 검색(도)",
                "Translation bounds" => "이동 범위",
                "Search guard" => "검색 제한",
                "Identity" => "식별 정보",
                "Edge policy" => "에지 조건",
                "Support" => "유효점 조건",
                "Sampling" => "샘플링",
                "Grid layout" => "격자 배치",
                "Cell geometry" => "셀 형상",
                "Gate" => "판정 기준",
                "Reference surface" => "기준면",
                "Filter" => "필터",
                "Threshold rule" => "임계값 조건",
                "Evidence policy" => "결과 처리",
                "Saved background" => "배경 파일",
                "Outlier rule" => "이상값 제거",
                "Connectivity" => "연결 방향",
                "Coordinates" => "좌표",
                "Leveling" => "수평 보정",
                "Preservation" => "데이터 유지",
                "Edge" => "에지",
                "Policies" => "처리 방법",
                "Deviation rule" => "높이차 조건",
                "Fit rule" => "피팅 조건",
                "Corner rule" => "교점 조건",
                "Correspondence policy" => "좌표 대응 조건",
                _ => category
            };

    private static string L(string korean, string english) =>
        OpenVisionLanguageService.CurrentLanguage == OpenVisionLanguage.English ? english : korean;
}
