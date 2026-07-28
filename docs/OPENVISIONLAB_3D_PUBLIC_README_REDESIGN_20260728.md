# OpenVisionLab 3D Studio 공개 README 사용자 중심 개편

Date: 2026-07-28

> Superseded public-copy policy: the root README is now English, leads with
> supported inspection workflows, does not market the product through lists of
> unsupported industrial systems, names the included example by inspection
> task, and links to the Apache-2.0 license. README media must contain only the
> application window and a geometrically valid same-part Thickness ROI pair.
> Preserve the current contract in
> `docs/OPENVISIONLAB_3D_PUBLIC_README_AND_MEDIA_POLICY.md`.

## 문제

기존 공개 README는 `548`줄, `64,167`바이트였고 첫 화면부터 Viewer 단계,
NIST/CloudCompare 수치, schema, SHA-256, Smoke 명령과 내부 검증 상태를
설명했습니다. 개발 근거는 풍부했지만 처음 방문한 사용자가 다음 질문에
답하기 어려웠습니다.

- 이 프로그램은 무엇인가?
- 어떤 3D 검사 작업을 할 수 있는가?
- 실제 화면과 작업 흐름은 어떤가?
- 어떻게 실행하는가?
- 지금 믿어도 되는 범위와 아직 안 되는 범위는 무엇인가?

## 참고한 공개 README 패턴

- `Noah8218/OpenVisionLab-Labeling-Studio`: 한국어 제품 설명, 실제 화면,
  1분 요약, 바로 시작하기
- `Noah8218/RawBufferVisualizer`: 문제/가치 문장, 상태 배지, GIF, 빠른
  설치와 차별점
- `Noah8218/Library-Noah`: 개발자 Quick Start와 사용 예제

3D Studio는 GUI 제품이므로 Labeling Studio와 Raw Buffer Visualizer의
첫 화면 구성을 우선 적용하고, 개발자 명령은 별도 가이드로 분리했습니다.

## 변경 결과

루트 `README.md`는 `204`줄, `11,069`바이트의 사용자 중심 문서가
되었습니다.

상단 순서:

1. 제품 이름과 영어 한 줄 설명
2. CI, Windows, .NET 10, active development 배지
3. 사용자 가치 문장
4. 현재 ROI teaching GIF
5. `raw-height`와 물리 계측 경계
6. 1분 요약과 검사 작업 흐름

본문은 다음 안정적인 정보만 유지합니다.

- 제품 정체성과 검사 흐름
- 현재 할 수 있는 작업
- Workbench 화면 구성
- 대표 검사 도구와 지원 형식
- 일반 사용자의 빌드/실행과 첫 검사
- 단축키
- 현재 개발 범위와 제한
- Viewer-only 공개 prerelease
- 개발자 문서와 실제 라이선스 상태

## 개발자 정보의 새 소유자

`docs/OPENVISIONLAB_3D_DEVELOPMENT_AND_VERIFICATION_GUIDE.md`가 다음 내용을
소유합니다.

- Debug/Release 복원과 빌드
- Shell, 독립 Viewer, Headless Runner 실행
- 코드 구조 검사
- focused Workbench verification
- 실제 UI 영상과 README GIF 생성
- Runner/알고리즘 검증
- 데이터 로딩 매트릭스와 단일 샘플 probe
- Viewer DLL 번들
- Windows CI 범위
- 완료 전 체크리스트와 상세 검증 문서 탐색

## 개발 증거의 소유 위치

- `.C3D` 값은 현재 `raw-height`이며 교정된 mm 두께가 아닙니다.
- `GridRectangle`은 X/Z footprint이고 Viewer 오버레이 Y 위치는 보기
  전용입니다.
- `OrientedBox3D` Viewer outline/handle은 아직 다음 제품 우선순위입니다.
- 데이터 생성 방식, 해시, 내부 범위 경계와 재현 절차는 개발 증거 문서가
  소유하며 사용자 README의 제품 소개 문구로 사용하지 않습니다.
- 프로젝트 라이선스는 루트 `LICENSE`와 `NOTICE`가 소유합니다.

## 검증

```text
README local links                       Pass
Developer guide local links              Pass
git diff --check                         Pass
README heading count                     17 sections/subsections
README image count in browser            5
CI badge natural size                    90 x 20
Windows badge natural size               116 x 20
.NET badge natural size                  87 x 20
Development status badge natural size    162 x 20
ROI workflow GIF natural size            960 x 520
```

README의 빠른 실행에 기록한 실제 빌드 명령:

```powershell
dotnet build OpenVisionLab.ThreeDStudio.sln -c Debug -p:Platform="Any CPU"
```

## 화면 증거

- Before:
  `artifacts/current/20260728-readme-user-facing/01-before-public-main.png`
- After:
  `artifacts/current/20260728-readme-user-facing/02-after-user-facing.png`
- Local preview:
  `artifacts/current/20260728-readme-user-facing/readme-after-preview.html`

Before는 2026-07-28 GitHub 기본 `main`의 실제 공개 첫 화면입니다. After는
동일 작업에서 현재 소스 README를 GitHub에 가까운 폭과 스타일로 렌더링한
현재 브랜치 미리보기입니다. 기본 `main`은 브랜치가 병합되기 전까지
변경되지 않습니다.

## 완료 기록

```text
Status: Complete
Scope: Public README information architecture, hero GIF placement, user workflow, quick start, scope boundaries, and separate developer verification guide
Acceptance criteria: User value first -> Pass; GIF first fold -> Pass; quick start and shortcuts -> Pass; metrology boundary retained -> Pass; developer detail separated -> Pass; local links/render -> Pass
Verification: local Markdown link scan; git diff --check; browser render and image natural-size inspection; documented Debug build command
Evidence: artifacts/current/20260728-readme-user-facing/
Boundary / next dependency: Public main remains unchanged until branch merge; license choice requires owner decision; E-09 remains the next product implementation priority
```
