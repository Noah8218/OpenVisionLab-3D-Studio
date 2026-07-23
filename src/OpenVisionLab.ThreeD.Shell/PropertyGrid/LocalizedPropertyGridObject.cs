using System.ComponentModel;
using OpenVisionLab;

namespace OpenVisionLab.ThreeD.Shell.PropertyGrid;

internal static class LocalizedPropertyGridObject
{
    public static object Create(object source) =>
        Activator.CreateInstance(
            typeof(LocalizedPropertyGridObject<>).MakeGenericType(source.GetType()),
            source)!;
}

internal sealed class LocalizedPropertyGridObject<T>(T source) : CustomTypeDescriptor
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
        public override string Description => property.Description;
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

    private static string LocalizePropertyName(string name, string fallback) =>
        OpenVisionLanguageService.CurrentLanguage == OpenVisionLanguage.English
            ? fallback
            : name switch
            {
                "AffineIndependencePolicy" => "어파인 독립 정책",
                "ArithmeticResidualWarning" => "산술 잔차 경고",
                "BoundaryPolicy" => "경계 정책",
                "CellAssignment" => "셀 할당",
                "CandidatePolicy" => "후보 정책",
                "CollisionPolicy" => "충돌 정책",
                "ColumnCount" => "열 수",
                "ClosestApproachPolicy" => "최근접 정책",
                "ComparisonAxis" => "비교 축",
                "ConstructionPolicy" => "구성 정책",
                "DirectionPolicy" => "방향 정책",
                "DistanceTolerance" => "거리 허용오차",
                "ElevationAngleToleranceDegrees" => "높이각 허용오차(도)",
                "EndpointPolicy" => "끝점 정책",
                "ExcludedOperations" => "제외 연산",
                "ExecutionPolicy" => "실행 정책",
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
                "HolePolicy" => "누락 셀 정책",
                "HypothesisPolicy" => "가설 정책",
                "KernelSize" => "커널 크기",
                "MaximumClosestApproachDistance" => "최대 최근접 거리",
                "MaximumConditionEstimate" => "최대 조건 추정값",
                "MaximumFlatness" => "최대 평탄도",
                "MaximumHypotheses" => "최대 가설 수",
                "MaximumOrthogonalResidual" => "최대 직교 잔차",
                "MaximumPeakToValley" => "최대 P2V",
                "MaximumPeakToValleyRawHeight" => "최대 Raw-Height P2V",
                "MaximumRms" => "최대 RMS",
                "MaximumSupportExtension" => "최대 지지 연장",
                "MaximumThickness" => "최대 두께",
                "Method" => "방법",
                "MinimumAbsoluteNormalY" => "최소 |법선 Y|",
                "MinimumAcuteAngleDegrees" => "최소 예각(도)",
                "MinimumDelta" => "최소 높이차",
                "MinimumInlierCount" => "최소 인라이어 수",
                "MinimumInlierRatio" => "최소 인라이어 비율",
                "MinimumInlierScanlineSpan" => "최소 인라이어 스캔라인 범위",
                "MinimumMeasurementSampleCount" => "최소 측정 샘플 수",
                "MinimumReferenceSampleCount" => "최소 기준 샘플 수",
                "MinimumThickness" => "최소 두께",
                "MinimumCoverageRatio" => "최소 커버리지 비율",
                "MinimumValidSampleCount" => "최소 유효 샘플 수",
                "MissingValuePolicy" => "누락값 정책",
                "OutputRole" => "출력 역할",
                "OriginX" => "원점 X",
                "OriginY" => "원점 Y",
                "OriginZ" => "원점 Z",
                "OutOfBoundsPolicy" => "범위 밖 정책",
                "PairCountPolicy" => "대응 쌍 수 정책",
                "ParallelPolicy" => "평행 정책",
                "Path" => "경로",
                "PlanarWidthTolerance" => "평면 폭 허용오차",
                "PointPolicy" => "포인트 정책",
                "Polarity" => "극성",
                "PitchU" => "U 피치",
                "PitchV" => "V 피치",
                "ReferenceFrameId" => "기준 프레임 ID",
                "ReferenceProvenance" => "기준 출처",
                "ReferenceRevision" => "기준 리비전",
                "ReferenceUnit" => "기준 단위",
                "RefinementPolicy" => "정제 정책",
                "ResidualPolicy" => "잔차 정책",
                "RowCount" => "행 수",
                "SolvePolicy" => "계산 정책",
                "SourceArtifactPolicy" => "소스 산출물 정책",
                "SupportPolicy" => "지지 정책",
                "UnmappedParameters" => "매핑되지 않은 파라미터",
                "UAxisX" => "U 축 X",
                "UAxisY" => "U 축 Y",
                "UAxisZ" => "U 축 Z",
                "VAxisX" => "V 축 X",
                "VAxisY" => "V 축 Y",
                "VAxisZ" => "V 축 Z",
                "VolumeTolerance" => "체적 허용오차",
                "WidthTolerance" => "폭 허용오차",
                _ => fallback
            };

    private static string LocalizeCategory(string category) =>
        OpenVisionLanguageService.CurrentLanguage == OpenVisionLanguage.English
            ? category
            : category switch
            {
                "Acceptance" => "허용 기준",
                "A3 publish policy" => "A3 게시 정책",
                "A3 reference grid" => "A3 기준 그리드",
                "Execution" => "실행",
                "Geometry" => "형상",
                "Input" => "입력",
                "Measurement" => "측정",
                "Output" => "출력",
                "Policy" => "정책",
                "Solve policy" => "계산 정책",
                _ => category
            };

    private static string L(string korean, string english) =>
        OpenVisionLanguageService.CurrentLanguage == OpenVisionLanguage.English ? english : korean;
}
