# Isekai 한국어 번역 작업 지침

## 기준과 작업 경계

- `localization/ko-kr`의 기준은 `e4d5ac3`입니다. master, V3 Git 태그,
  미병합 수정 PR이나 베타 DLL로 기준을 바꾸지 않습니다.
- 모드 자체에 한국어를 포함하며 Enhance 없이 동작해야 합니다.
  인게임 문구와 UMM 설정창이 번역 범위입니다.
- 실제 효과 코드가 설명보다 우선합니다. 번역을 위해 전투·성장·밸런스 코드를
  변경하지 않습니다. 미확인 동작은 `docs/source-discrepancies.md`에 기록합니다.
- 인접 프로젝트는 참고만 하며 수정하지 않습니다. 기존 사용자 변경을 보존합니다.
- 커밋·푸시·PR·릴리스·원격 설치·게임 실행은 별도 사용자 승인이 필요합니다.
  Xync 로그도 기록 전에 승인을 받습니다.

## 번역과 식별자

- 원문 팩과 영어 조회 인덱스를 유지합니다. enGB, 기존 GUID, Blueprint ID,
  설정 키와 기본값을 번역 때문에 변경하지 않습니다.
- 원문·번역·템플릿 여부·근거는 `localization/ko-kr.json`에서 관리합니다.
  `python3 scripts/localization.py`로 생성하며 생성 C#은 직접 편집하지 않습니다.
- 태그·링크 대상·변수·수식과 행동 종류를 보존합니다. 원문이 바뀌면 이전 번역을
  자동 적용하지 않습니다. 다른 모드·다른 언어·알 수 없는 항목은 보존합니다.
- `source-audit.json`의 상수 일치 여부는 실행 경로·미사용 판정이 아닙니다.
  간접 생성과 wrapper 접미문까지 조사한 뒤 최종 번역 목록을 확정합니다.
- 현재 상태와 남은 범위는 `docs/localization.md`를 확인합니다.

## 빌드와 검증

- C# 9 / net481입니다. Enhance의 C# 5·루트 *.cs 규칙은 적용하지 않습니다.
- 안전한 로컬 명령은 `docs/build-and-validation.md`를 따릅니다.
  원본 Windows csproj에는 자동 robocopy가 있으므로 로컬 검사에 사용하지 않습니다.
- 코드 후보 버전 5.4.4입니다. 변경 시 Info.json과 Properties/AssemblyInfo.cs,
  로컬 SDK 프로젝트를 일치시킵니다. 원 저자·모드 ID는 보존합니다.
- 게임 PC에서 확보한 Validation·Astar 참조와 현재 게임 DLL의 공개화 사본으로
  전체 로컬 빌드가 통과했습니다. `.references/game`의 비공개 manifest를 보존합니다.
  `scripts/PublicizeReferences`는 로컬 빌드 참조만 생성하며 게임 PC에 설치하지 않습니다.
  빌드 성공을 인게임 검증 성공으로 보고하지 않습니다.
- 게임 참조 DLL, 공개화 DLL, 캐시, 개인 설정은 커밋하거나 배포하지 않습니다.
  설정을 보존하고, 로컬 검사와 실제 게임 검증을 구분합니다.
