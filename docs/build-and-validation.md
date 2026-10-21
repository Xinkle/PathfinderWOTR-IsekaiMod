# 안전한 로컬 검사와 빌드 상태

2026-10-21. 로컬 후보 5.4.4, SDK 8.0.425, C# 9, net481입니다.
Enhance의 C# 5·루트 파일 한정 규칙은 적용하지 않습니다.

```sh
python3 scripts/localization.py                  # 생성
python3 scripts/localization.py --check          # 원문·태그·누락·생성 결과 검사
scripts/verify.sh --localization-only            # 번역 검사·모델·API 부분 컴파일
scripts/verify.sh                                # 위 검사와 전체 빌드 시도; 실패를 숨기지 않음
```

개별 명령은 다음과 같습니다.

```sh
python3 -m unittest discover -s tests -v
scripts/dotnet.sh run --project tests/LocalizationHarness -c Release
scripts/dotnet.sh build tests/GameApiCompile -c Release --nologo
scripts/dotnet.sh build IsekaiMod.Local.csproj -c Release --nologo
```

`DOTNET_BIN`으로 SDK 실행 파일을 지정할 수 있습니다. 기본값은 기존
`$HOME/.local/share/tactical-virtuoso/dotnet/dotnet`입니다.
참조 기본값은 자체 `.references/game`이 있으면 해당 캐시를 사용하며, 없으면 인접 `WOTR-Enhance/.references/game`과 조사용
`/tmp/wotr-ttt-integration/TabletopTweaks-Core.dll`입니다. 자체 캐시에 TTT가 있으면 우선 사용합니다. 다른 환경에서는
`-p:GameReferencesPath=/absolute/path`와 `-p:TTTCorePath=/absolute/path/TabletopTweaks-Core.dll`을
지정합니다. /tmp 파일은 영구 의존성으로 간주하지 마세요.

전체 빌드용 공개화 게임 어셈블리는 `-p:GameAssemblyPath=/absolute/path/Assembly-CSharp.dll`로
별도 지정할 수 있습니다. 추가 게임 참조 DLL 폴더는
`-p:AdditionalGameReferencesPath=/absolute/path`로 지정합니다.
없는 참조를 가짜 stub DLL로 대체하여 배포 가능한 전체 빌드라고 주장하지 않습니다.

### 공개화 참조 재생성

최종 로컬 캐시의 출처·해시는 무시된 `.references/manifest.json`에 있습니다.
다른 PC에서 재현할 때에는 실제 사용자가 소유한 게임 DLL을 별도로 확보하고,
새 출력 폴더를 지정합니다. 원본·출력은 설치 경로가 아닌 로컬 참조 캐시에 둡니다.

```sh
scripts/dotnet.sh run --project scripts/PublicizeReferences -c Release -- \
  /absolute/local/cache/Assembly-CSharp.dll /absolute/local/cache/new-publicized
```

생성한 Assembly-CSharp.dll, 실제 TTT DLL, Validation·Astar DLL을 사용하도록
위 참조 경로를 지정하세요. 참조 준비 도구의 실행은 설치 작업이 아닙니다.

## 결과와 경계

- 생성·태그·변수·누락 검사와 Python **16개 단위 검사** (번역 11개·패키징 5개)를 통과했습니다.
  원문의 기존 818개 행을 현행 785개와 과거 33개로 분류하고, 재구성한 현재
  819개 표시 정체성의 번역 누락이 없는지 검사합니다.
- Harmony 모델은 819개 전체 번역, 원문·템플릿 변화, GUID fallback,
  다른 팩의 동일 문자열 보호, 초기·동적 게시, 캐시 초기화·중복 방지,
  예외·중첩·스레드 로컬 가드, 언어와 UI·골드 포맷을 검사합니다.
  실제 Harmony 2.3.6 detour를 사용하지만 TTT와 게임은 작은 API 모델입니다.
- KoreanTranslationOverride 및 나중에 설치되는 XinkleWotrEnhance 소유자의
  Last-priority 경쟁 postfix를 모델로 검사합니다. 다른 팩의 기존 표시를
  보존하는지도 확인합니다. 실제 두 모드와의 Unity 공존을 관찰한 것은 아닙니다.
- 실제 게임·TTT DLL을 참조한 `GameApiCompile`은 **0 경고·0 오류**입니다.
  번역 런타임·데이터·UMMSettingsUI·AddedContent·ModContextBase를 컴파일하며,
  Main과 Blueprint 조회 부분만 명시적인 테스트용 host로 대체합니다.
  이 테스트 어셈블리는 모드 배포용이 아닙니다.
- /tmp 사본에서 영어 조회 가드와 동적 표시 복원을 각각 제거한 두 변이도
  모델 테스트에서 실패하여 안전장치의 회귀를 검출함을 확인했습니다.
- **후속 게임 PC 참조 확보 뒤 전체 모드 Rebuild가 0 경고·0 오류로 통과했습니다.**
  사용자 요청으로 Tailscale 경유 SSH 읽기 전용 접속을 수행했습니다.
  Xync의 「Tailscale 노드별 접속 가이드」에 기재된 계정으로 접속했으며,
  게임·모드·설정·프로세스는 변경하지 않았습니다.
- `Owlcat.Runtime.Validation.dll`을 확보하자 `ValidationContext` 오류가 해소되고,
  그다음 드러난 `NNInfo` 참조 오류는 `AstarPathfindingProject.dll`을 확보하여 해소했습니다.
  두 DLL과 현재 `Assembly-CSharp.dll`, TTT-Core DLL을 별도 무시 캐시에 내려받아
  원격/로컬 SHA-256을 대조했습니다. 기존 캐시의 13개 DLL도 현재 PC와 해시가 같습니다.
  설치된 Steam build ID는 20204175입니다.
- `scripts/PublicizeReferences`는 원본 프로젝트에 이미 포함된 Aze 1.1.0 패키지의
  dnlib를 사용하여 현재 게임 Assembly-CSharp의 **로컬 사본만** 공개화합니다.
  원본 Windows 빌드와 같은 `PubliciseCompilerGenerated=true` 정책이며,
  게임플레이 소스 수정이나 가짜 Validation 타입 추가는 없습니다.
  원본 입력의 해시를 다시 검사하고, 출력은 CreateNew 스트림으로 생성하여 기존 파일을 덮어쓰지 않습니다.
- `.references/game`은 기존 캐시와 새로 확보한 참조, 공개화 사본을 가리키는 로컬 overlay입니다.
  이 캐시가 있으면 안전한 SDK 프로젝트가 우선 사용합니다. DLL·캐시 manifest는 Git에서 제외합니다.
  추가 참조 폴더에서는 Assembly-CSharp·TTT를 제외해 명시한 참조가 덮이지 않게 했습니다.
  다른 DLL은 기본 폴더와 중복되지 않는 추가 폴더를 사용하세요.
- 앞서 현재의 비공개 멤버 캐시로 시도한 원본·후보는 동일한 18개 오류로 실패했고,
  보관 ZIP의 공개화 참조로는 ValidationContext 오류 1개가 남았습니다.
  이는 해결 전 이력이며, 최종 성공 빌드는 보관본이 아니라 현재 PC와 해시가 일치하는 참조를 사용합니다.
- 출력은 `bin/Release/net481/IsekaiMod.dll`입니다. 전체 빌드 성공은
  패키징·설치·Unity 로드·인게임 검증 성공을 의미하지 않습니다.
  게임 DLL과 공개화 사본, 테스트 DLL은 모드 배포물에 포함하면 안 됩니다.

`local-validation.json`에 검사 결과, 참조 해시, 초기 빌드 실패·참조 확보 후 성공과 품질 리뷰를
구분해 기록합니다. TTT 어셈블리 버전 1.0.0.0은 UMM 모드 버전 0.6.2의
증거가 아닙니다. 기존 요구 버전은 보존하며 검사에는 정확한 DLL 해시와 API를
사용했습니다. 현재 게임 PC의 설치 파일을 새로 조회하지 않았습니다.

## 보존 검사와 빌드 안전성

- Python 검사는 기존 영어 팩과 설정 JSON의 바이트 보존, 게임플레이 소스의
  변경 여부, 상인 코드의 표시 wrapper 외 동일성도 확인합니다.
- `IsekaiMod.Local.csproj`는 Windows csproj를 import하지 않습니다.
  Publicise task, robocopy, SSH, 설치·발행 후크가 없습니다.
- 원본 `IsekaiMod/IsekaiMod.csproj`에는 자동 robocopy가 남아 있습니다.
  **안전한 로컬 검사 명령으로 사용하지 마세요.**
- 모든 게임·TTT 참조는 `Private=false`입니다. bin/obj·DLL·참조 캐시·개인 설정은
  Git에서 제외합니다. 원래 추적 중이던 보관 ZIP을 새 배포 패키지로 사용하지 마세요.
- 자체 버전은 Info.json 5.4.4, 어셈블리·파일 버전 5.4.4.0입니다.
  원 저자, 모드 ID, EntryMethod와 Requirements는 보존합니다.
- 사용자 후속 요청으로 커밋·푸시 및 포크 저장소에 ZIP 사전 릴리스 발행을 승인받았습니다.
  원격 설치·게임 실행은 승인 범위가 아니며 수행하지 않습니다.

## 품질 승인과 실제 게임 검증

Astra-low 리뷰는 [3차 최종 보고서](reviews/astra-low-pass3.md)에서
번역 품질을 승인했습니다. Enhance 29개 비교 표본과 본편 용어 124개를
검토한 결과이며, 전체 효과 코드·외부 Blueprint를 전수 검증한 결과는 아닙니다.

실제 Unity 로드, UMM 설정 저장·다시 읽기, Blueprint 등록·전투 동작,
레벨 변화 시 갱신, 한글 글꼴·줄바꿈·잘림은 아직 확인하지 않았습니다.
긴 궁극의 강화 목록과 원본 결함이 있는 능력은 특히 별도 테스트가 필요합니다.

## ZIP 사전 릴리스 패키징

사용자가 커밋·푸시와 포크 저장소의 한국어 번역판 ZIP 발행을 승인했습니다.
안전한 검사를 마치고 소스를 커밋한 뒤 다음 명령으로 패키징합니다.

```sh
scripts/dotnet.sh build IsekaiMod.Local.csproj -t:Rebuild -c Release --nologo
python3 scripts/package.py
```

패키저는 clean working tree에서 현재 HEAD의 Rebuild를 직접 실행하고,
빌드 도중 소스/HEAD가 바뀌지 않았는지 확인한 뒤 실제 DLL의 이름·버전·진입점·내장 번역 타입과
기본 리소스, 전체 한국어 문자열의 DLL 내장을 확인합니다.
ZIP 루트는 `IsekaiMod/`이며 64개 파일만 허용합니다.

- `IsekaiMod.dll`, `Info.json`, `Localization/LocalizationPack.json`.
- 원본에서 추적하는 PNG 자산 59개, `LICENSE`, `README-ko.md`.

게임 DLL·공개화 사본·TTT DLL·PDB·개인 설정은 포함하지 않습니다.
기존 설정을 덮어쓰지 않도록 `AddedContent.json`은 외부 파일로 넣지 않습니다.
동일 파일의 ZIP 중복, 예상 밖 파일, 변조된 내용과 symlink를 거부합니다.
이미 존재하는 같은 이름의 ZIP을 덮어쓰지 않습니다.
`dist/`에는 ZIP과 파일별 해시·소스 커밋을 기록한 manifest 및 SHA-256 파일을 생성합니다.
이들은 Git에서 제외하며 릴리스 자산으로만 별도 게시합니다.

이 패키징 명령은 설치하지 않으며 네트워크에도 접속하지 않습니다.
실제 GitHub 릴리스는 명시적으로 사전 릴리스로 게시하고 인게임 미검증 경고를 남깁니다.
