# InventorySlots / InventoryActions 구조 검토와 적용 결과

2026-09-12. 기능·외부 계약을 유지하는 국소 개선을 적용했다. 게임 실행·멀티플레이·Unity 프로파일링은 수행하지 않았다. 성능 효과는 코드에서 확인한 반복 작업 제거에 대한 예상이며 프레임 시간 측정 결과가 아니다.

## 기준과 기존 작업 보존

- 시작 브랜치: `codex/retire-inventoryslots-multiuserchest`, HEAD `21bda73bbc8e9b5bf0587a13a353b0d02a565c84`.
- 시작 main: `25eb0022f8f757059816baf1804297c49dd93361`. main이 HEAD의 조상임을 확인하고 이미 존재하던 3개 커밋으로 fast-forward한 다음 main으로 전환했다. 새 브랜치/worktree, 기존 변경 일괄 커밋, 이력 재작성, push는 하지 않았다.
- 기존 미커밋 작업은 추적 파일 15개와 미추적 파일 3개다. 특히 `SharedContainerAccess.cs`, `SharedContainerGui.cs`, `docs/SharedContainerReview.md` 및 관련 상자 코드·설정·테스트는 이번 리팩터링 커밋에 포함하지 않았다.
- 기존 파일 18개 중 이번 stamp 테스트를 수정한 `InventorySlots.Tests/Program.cs`를 제외한 17개는 시작 시 사본과 SHA-256이 동일하다. 테스트 파일에서는 stamp 관련 변경만 선택하여 커밋했으며, 남은 전체 미커밋 diff의 추가·삭제 내용이 시작 diff와 동일함을 확인했다.
- 현재 작업 트리는 HEAD의 기존 MultiUserChest 제거 이후 새로운 공동 열람·승인식 소유권 이전 구현을 미커밋 상태로 포함한다. 아래 빌드·검사는 이 작업 트리에서 수행했다. 미커밋 기능을 포함하지 않는 깨끗한 HEAD를 별도로 빌드했다는 의미는 아니다.
- InventorySlots `1.4.10`, InventoryActions `1.0.11`과 manifest는 유지했다. Release 빌드·ZIP 생성·게시·업로더 조작을 하지 않았다.

## 실제 대상과 검토 범위

두 모드 모두 net48 BepInEx 일반 플러그인이다. 프리로더 패처가 아니다.

| 영역 | 실제 대상과 진입점 |
| --- | --- |
| InventorySlots | `InventorySlots.csproj`; `Plugin.cs`의 BepInPlugin, `PluginLifecycle.Awake/Update/OnDestroy`, `InventoryLifecyclePatches`/`InventoryPatchHandlers`, crafting/container/UI Harmony 패치 |
| InventoryActions | `InventoryActions/InventoryActions.csproj`; `Plugin.cs`의 BepInPlugin·Unity 수명주기, `Patches.cs`의 UI·상자·입력 연결 |
| 소스 포함 | 각 프로젝트의 루트 `*.cs`, Localizer, AssemblyInfo를 명시적으로 컴파일. 폴더 이동은 Compile Include 변경까지 필요하다 |
| 설정·리소스 | Slots 설정 바인딩·YAML 적용·기본 InventorySlots/ResourceMap YAML·ClientState·번역, Actions 설정·favorite 좌표 파일·번역, 실제 manifest |
| 빌드 | `environment.props`, 각 csproj, 부모 `build/ILRepack.targets`. ServerSync와 YamlDotNet 16.3.0을 최종 DLL에 병합하며 Debug 배포는 병합 이후 수행 |
| 검사 | net9 콘솔 `InventorySlots.Tests`, 원본 메타데이터를 검사하는 `build/CompatibilityCheck`, Actions의 net48 `build/CompatibilitySmoke` |

솔루션 Debug는 InventoryActions까지 빌드할 수 있으므로 모드별 csproj를 명시했다. 두 모드는 서로 BepInIncompatibility를 선언하므로 실제 게임 확인은 각각 활성화한 환경에서 해야 한다.

현재 설치·전역 보관 자료의 게임은 Windows x64 Valheim 1.0.12이다. 클라이언트 Steam build 25253764, 데디케이트 25253791의 원본을 검사에 사용했다. 이전 1.0.7 대응 기록을 현재 1.0.12 실행 성공으로 간주하지 않았다.

전역 자료는 `C:/Users/blizz/.codex/references/valheim/INDEX.md`와 그 아래 `comparisons/1.0.7--1.0.12-windows-x64/mod-patching-guide.md`, `dedicated-server-review.md`를 참고했다. 새 수집·전체 재추출·지원 범위 확대는 하지 않았다.

기존 컴파일에는 AssemblyPublicizer 0.4.2가 적용된다. 원본 HintPath를 사용하지만 실제 컴파일 참조는 공개화 처리되므로 컴파일 성공을 원본의 접근 제한 또는 Unity/Mono 접근 성공과 동일시하지 않는다. 분석·정적 검사는 원본 DLL로 했으며 게임 DLL이나 접근 방식을 일괄 변경하지 않았다.

ServerSync는 기존 `Libs/ServerSync.dll` 공통 고정본 `valheim-1.0.7-r1`을 유지했다. SHA-256은 `B4DD786997F4E90D770F09EF3E9D64154754FE7E8EDFB4841795751895B35846`이다. 두 manifest의 BepInEx 의존성은 이미 5.4.2350이었다.

검토는 진입점·상태 소유권·수명주기·관련 호출·Git 변경 사례와 후보 주변 코드를 중심으로 했다. 모든 파일의 모든 분기를 줄 단위로 감사한 것은 아니다. bin/obj/ZIP·추출물은 구조 변경 대상에서 제외했고 검증 근거로만 사용했다. 외부 라이브러리와 reference 소스 전체, 전체 게임 리소스의 의미, 모든 외부 모드의 임의 비공개 리플렉션, Linux·크로스플레이·실제 네트워크는 미검토다.

## 영역별 구조 판정

| 영역 | 판정과 근거 |
| --- | --- |
| Slots crafting | partial 파일 사이 탐색 비용이 있지만 tab adapter·recipe view·요구 재료·순수 stamp·Harmony 경계는 책임이 다르다. 전면 병합/재분리보다 숫자 캐시 표현을 단순화했다 |
| Slots 설정/YAML | 대체로 균형. 순수 파싱과 파일/동기화/Unity 적용을 구분한다. 슬롯 YAML은 실제 배치까지, ResourceMap은 주로 분류·정렬 캐시까지 전파되므로 generic 적용 계층으로 합치지 않았다 |
| Slots ClientState·슬롯 복구 | 저장 모델·정규화·파일 교체·게임 적용 분리가 유효하다. 로드/저장 경계의 검증은 중복처럼 보여도 입력과 책임이 다르다 |
| Slots 메타데이터·API | 현재 dictionary 정책과 외부 callback 경계는 적절하다. 삭제된 원격 직렬화 경로의 목록 어댑터만 제거했다 |
| Slots tooltip·preview | 객체 수명·표시 복원·외부 연동별 분리는 유지 가치가 있다. Preview Prefix/Postfix 사이에 바닐라나 다른 패치가 상태를 바꿀 수 있어 반복 보호를 제거하지 않았다 |
| Actions Actions.cs | 후보 선정·이동·정렬·FX가 다소 집중됐다. 그러나 실제 수정은 입력·anchor·권한과 함께 전파된다. 여러 service/interface로 나누는 이익은 입증되지 않아 유지했다 |
| Actions 소유권 코드/core | 실제 RPC 연결과 순수 상태 전이를 분리한 현재 경계가 적절하다 |
| Actions UI/Favorites | UI marker가 해당 객체의 초기화·라벨·파괴 복구를 소유한다. 이 소유권을 유지하면서 marker 중복 검색과 설정 반복 파싱을 줄였다 |
| 선택적 연동·코어 복사 | 외부 모드마다 실패 시 정책이 다르므로 adapter 경계를 유지했다. 작은 순수 코어 복사를 새 공용 런타임 라이브러리로 바꾸는 배포·병합 비용은 정당화되지 않았다 |

이 판단에는 실제 Git 사례를 사용했다. `711b014`는 crafting 휠 경로의 공동 배치, requirement strip 분리, favorite marker 재사용을 포함한다. `8d39016`의 네이티브 행 대응은 기존 슬롯 경계에서 작은 수정으로 수렴했다. `21bda73`은 구 MultiUserContainer 프로토콜을 제거했지만 목록 메타데이터 어댑터는 남겼다. 후보 파일의 과거 큰 diff에는 개행 차이가 포함돼 있어 `--ignore-space-at-eol`로 실제 정책 변경과 구분했다.

## 구현 커밋

### ecbcad6 — 폐기된 메타데이터 목록 어댑터 제거

- 문제: `StackMetadataPolicy.AreCompatible(IReadOnlyList<...>, ...)`가 목록을 dictionary로 바꾸는 별도 경로를 유지하고 있었다. 원래 호출자는 `8d39016`의 `MultiUserContainerTransferCore.CanStackTogether`이며 해당 프로토콜은 `21bda73`에서 삭제됐다.
- 최소 변경: internal 목록 오버로드와 그 전용 private `ToDictionary`만 삭제했다. 현재 `StackMetadataInventoryIntegration`은 dictionary 오버로드를 사용한다.
- 효과: 폐기된 직렬화 형식의 중복 키 검증과 현재 아이템 병합 정책을 혼동할 여지를 없앴다. 실행 성능 개선을 주장하는 변경은 아니다.
- 위험/보존: 공개 `RegisterStackMetadataPolicy` 두 오버로드, first-wins·대소문자 구분·양방향 거절·예외 처리·부분 스택 원본 보존은 유지했다. 알려진 FineDining 연동은 공개 등록 API를 리플렉션으로 호출한다. 미조사 외부 모드의 임의 internal 접근까지 없다고 단정하지 않는다.
- 검증: 기존 metadata 테스트를 포함한 166개 검사, Debug, 원본 client/server 계약 검사 통과. 삭제한 구현을 반복하는 새 테스트는 만들지 않았다.

### 36c584b — 버튼 marker 확보 책임을 한곳으로 정리

- 문제: `InventoryActions/Ui.EnsureActionButton`이 marker를 Get/Add한 직후 `SetActionButtonLabel`이 같은 컴포넌트를 다시 Get/Add했다.
- 최소 변경: 이미 확보한 marker를 private 라벨 helper에 전달했다. 새 캐시·객체·파일은 없다.
- 효과: 버튼 표시 경로의 중복 컴포넌트 검색을 제거하고 marker 생성 책임을 EnsureActionButton에 모았다.
- 위험/보존: 라벨 helper 호출 전 사용자 callback 실행이 없음을 확인했다. 버튼 listener 초기화, label signature, TMP/legacy Text 처리, GUI 재생성 시 marker 확보는 동일하다.
- 검증: Debug, 166개 검사, 원본 client/server 계약 검사와 독립 diff 리뷰 통과. 실제 GUI 재열기·언어 변경·버튼 재생성은 실행 검증이 남는다.

### c9555ef — 제작 UI 자식 수를 정수 캐시 키로 보관

- 문제: `GetCraftingTextCache`와 `GetCraftingRequirementUiMarker`가 열린 제작 패널의 동적 프레임 경로에서도 childCount를 문자열로 만들었다.
- 최소 변경: 두 내부 MonoBehaviour의 ChildCount와 `CraftingTextStamp`/`CraftingTextColorStamp`의 해당 값을 int로 바꿨다. 새 캐시 계층은 없다.
- 효과: 숫자의 문자열 생성·비교를 제거하고 실제 캐시 조건을 이름과 타입으로 표현했다.
- 위험/보존: marker 초기값 -1로 자식이 0개인 경우에도 최초 수집을 보존했다. 자식 수 변경·파괴 객체 확인·stamp 초기화·실제 텍스트/색 대조는 유지했다. Jewelcrafting의 실제 복합 ChildSignature는 별도 정책이므로 변경하지 않았다.
- 검증: 기존 두 stamp 테스트에 자식 수 변경, 동등 값의 해시, 미초기화/빈 계층 구분을 추가했다. 166개 검사, Debug, 원본 client/server 계약 검사 통과. 게임의 제작/업그레이드/socket 전환·진행 라벨·동적 텍스트·GUI 재생성은 미실행이다.

### cf02e23 — 버튼 위치 설정 값이 바뀔 때만 파싱

- 문제: `InventoryGui.Update`의 UI 갱신에서 Sort/Trash 설정마다 `ToLowerInvariant`, Replace, Split, TryParse가 반복됐다.
- 최소 변경: 기존 설정 파일 안에 두 설정의 마지막 원문과 Vector2 결과만 보관한다. UI getter가 현재 원문을 Ordinal로 비교하고 변경 시 기존 파서를 호출한다. 결과를 먼저 저장하고 원문을 갱신한다.
- 효과/비용: 변하지 않은 설정의 반복 문자열 처리 대신 문자열 비교를 수행한다. 관리할 값은 문자열/Vector2 두 쌍이며 새 클래스·이벤트 구독·Unity 객체 참조는 없다.
- 위험/보존: null/빈 값/잘못된 값의 zero 반환, live 변경, 설정 객체 재바인딩, 두 버튼의 독립성을 보존했다. 파서의 문법·문화권·NaN 처리 자체를 변경하지 않았다. 현재 UI 주 스레드 호출 범위에서 사용한다.
- 검증: 기존 smoke 도구에 `--button-offsets` 선택 모드를 추가했다. 저장을 끈 임시 ConfigFile과 실제 ConfigEntry를 사용해 컴파일된 getter를 호출한다. 변경 전 DLL과 변경 후 DLL 모두 client/server 각각 20개 격리 검사를 통과했다. null·반복 조회·live 변경·잘못된 값에서 복구·재바인딩을 확인했다. NaN 사례를 별도로 실행한 것은 아니다.

## 보존한 기능·외부 계약

- Harmony 대상·오버로드·priority·반환값·__state·finalizer를 변경하지 않았다. Unity 메시지나 패치 진입점 이름으로 사용되는 경로를 삭제하지 않았다.
- 슬롯 ID/좌표, 모드 3행과 바닐라 구매 행의 독립성, 저장 키·backup·ClientState 형식, 사망/묘비 복구와 서버 권위 캐릭터 정책을 유지했다.
- 설정 키·기본값·서버 잠금·동기화 문자열·공개 API·manifest·모드 버전을 변경하지 않았다.
- 네트워크 owner와 거리/ward/개인 상자 권한, RPC 이름·lease token·revision·timeout·중복/지연 응답·취소 순서를 변경하지 않았다. 미커밋 새 공동 상자 구현의 동시 접근 정책도 이번 수정 대상이 아니다.
- Slots의 등록된 metadata 정책과 Actions의 빈 customData만 자동 처리하는 정책을 합치지 않았다. 자동 처리의 '모든 키 등록'과 병합 데이터 존재의 '하나라도 등록'은 서로 다른 조건이다.
- ServerCharacters/ServerManager, external MultiUserChest, BetterArchery, AzuCraftyBoxes, EpicLoot, Jewelcrafting 등 선택적 연동은 기존 adapter와 fallback을 유지했다.
- Preview 양쪽 패치의 보호, 진행 텍스트의 추가 Find 보완, 외부 모드가 덮어쓴 텍스트/색의 실제값 확인은 유지했다.

## 보류한 개선과 별도 결함 후보

| 항목 | 유지·분리 이유 |
| --- | --- |
| Actions 정렬 그룹의 두 번째 ToList | 그룹이 외부에 노출되지 않는 별도 snapshot이라 제거 후보는 유효하다. 다만 이번에는 정렬/이동 경로 변경까지 확대하지 않았고, 기존 전체 격리 도구도 1.0.12 런타임 제약으로 실행되지 않는다 |
| Actions enum 문자열 분기 | 원본 enum은 Trophy인데 현재 문자열 switch는 Trophie를 사용해 트로피가 fallback 90으로 간다. Trophy를 60으로 바꾸는 것은 정책/동작 변경이므로 별도 후보로 남겼다 |
| Favorites.GetButtonPos 반복 검색 | 원본은 element 목록을 선형 검색하므로 전체 갱신에서 중첩 검색이 발생한다. 인덱스 직접 계산은 외부 UI/패치 경로를 우회할 수 있어 이번에는 유지했다 |
| Slots 패널 확장 시 보호 자식 이중 순회 | 동일 대상 수집의 공동 배치 후보는 있지만 기존 fast path가 크기 갱신을 이미 줄이고 있다. 실제 패널·외부 탭 복원 검증 없이 확대하지 않았다 |
| 슬롯 해금 signature·아이템 identity iterator | 반복 hash/문자열/작은 배열 비용은 확인했다. 새 캐시는 캐릭터·YAML·ObjectDB·장비 변화 무효화 책임을 늘리므로 프로파일링 없이 추가하지 않았다 |
| 이전 Release sort 예외 | s_bypassCheatChecks의 필드→getter 변경에 따른 기존 바이너리 호환성 문제다. 현재 Debug와 오래된 Release를 구분하며 EpicLoot 원인으로 확정하지 않는다 |
| A 선도착/B 후도착 상자 조작 실패 | 사용자 제보와 owner 경로의 연관은 있으나 특정 RPC/revision 하나의 원인은 미확정. 미커밋 공유 상자 작업의 실제 A/B 검증이 필요하다 |
| 모두넣기/모두빼기 수량 증가, 음식 자동 전송, 미열람 상자 quickstack | 기존 미재현 후보다. 이번 리팩터링으로 해결했다고 주장하지 않는다. 음식의 실제 metadata를 확보하기 전 FineDining 원인으로 확정하지 않는다 |

## 검증 결과와 한계

기준과 각 변경 후 모드별 Debug 빌드·필요 검사·diff 리뷰를 수행하고 커밋했다. 편집·빌드·커밋은 한 세션이 맡았으며 병렬 에이전트는 읽기 전용 조사·리뷰만 수행했다.

| 종류 | 결과 |
| --- | --- |
| 두 모드 Debug | 경고 0, 오류 0. DeployToGame=true로 최종 병합 DLL을 Steam plugins에 갱신하고 원본/설치 SHA-256 일치 확인 |
| 순수/소스 계약 검사 | 기준 166개, 변경 후 166개 통과. 기존 두 stamp 테스트의 사례를 보강했으므로 테스트 항목 수는 동일 |
| Slots 원본 계약 | client/server 각각 직접 참조 962, 정적 Harmony 대상 128, 등록된 리플렉션 계약 40, 실패 0. 수동 확인 표시 8개는 남음 |
| Actions 원본 계약 | client/server 각각 직접 참조 387, 정적 Harmony 대상 23, 실패 0. 수동 확인 표시 1개. Actions의 모든 리플렉션을 자동 검사했다는 의미는 아님 |
| 위치 설정 선택 격리 검사 | 기준/최종 DLL에서 client/server 각각 20개 통과. 게임 원본을 publicize하거나 변경하지 않음 |
| 기존 전체 Actions smoke | 기준부터 client/server 모두 Inventory.Changed에서 .NET Framework의 default-interface 미지원 TypeLoadException으로 실패. 모드 변경으로 생긴 실패가 아님 |
| .NET 9 전체 smoke 시도 | 첫 빌드는 net9 restore 부재로 실패했고 명시 restore 후 빌드 성공(기존 FormatterServices 경고 1). 실행은 설치 Harmony.AccessTools 초기화에서 실패. 도구·게임 접근 제한을 완화해 통과시키지 않았으며 모드 의존성도 변경하지 않음 |
| 실제 실행 | Unity/Mono 게임·호스트·데디케이트 A/B·외부 모드 패치 조합·성능 측정은 수행하지 않음 |

최종 DLL:

| 파일 | SHA-256 |
| --- | --- |
| `bin/Debug/InventorySlots.dll` | `6FADF4399A0556173DFA90C5E6FBB3F1C99CCA44F7CE4DCE5AC1D7C948167B8B` |
| `InventoryActions/bin/Debug/InventoryActions.dll` | `0AEA6F4E9FBDDD0A2946DFF20DD5562687B78B4D84B7482D7E5205A4123C9FF9` |

로컬 로그·보고서는 Git 제외 폴더 `artifacts/refactor-20260912/`에 있다. `baseline/working-tree.json`, `baseline/existing-changes.patch`, `final-existing-changes.patch`, `final-dlls.json`, 단계별 build/tests/smoke 로그·계약 JSON을 보관했다. 이미 만들어진 Release ZIP과 이 Debug DLL이 같다고 가정하면 안 된다.

재현 명령은 저장소 루트 기준이다. 일반 모드 Debug 빌드는 로컬 게임 DLL을 갱신한다.

```powershell
dotnet build InventorySlots.csproj -c Debug -p:DeployToGame=true
dotnet build InventoryActions/InventoryActions.csproj -c Debug -p:DeployToGame=true
dotnet run --project InventorySlots.Tests/InventorySlots.Tests.csproj -c Debug
dotnet build InventoryActions/build/CompatibilitySmoke/CompatibilitySmoke.csproj -c Debug
# 생성된 CompatibilitySmoke.exe에 다음 인자를 전달:
# <final InventoryActions.dll> <original Managed> <BepInEx core> --button-offsets
# 전체 실행 모드와 위치 설정 선택 모드의 결과를 구분한다.
```

남은 게임 확인은 두 모드를 각각 활성화하여 수행한다. Slots는 제작/업그레이드/socket 전환·진행 표시·GUI 재생성을, Actions는 버튼 라벨·위치 설정 live 변경/리로드를 확인한다. 상자 A/B·동시 부분 스택·취소·재접속·총수량/품질/customData 보존은 기존 공유 상자 기능 검증으로 별도 유지한다. 프로세스 중단 시 캐릭터와 상자 저장의 영속 원자성을 이번 작업에서 보장하지 않는다.
