# InventorySlots 구조 검토 및 개선 — 2026-09-23

## 기준과 범위

- 시작: 실제 `main`, `4f85ec88211dca3553920c520d3e7704a3a3e237`, 추적 파일의 미커밋 변경 없음.
- 대상: InventorySlots **1.5.11**. 버전·의존성·게임 지원 범위는 변경하지 않았다.
- `C:/Users/blizz/.codex/AGENTS.md`와 전역 Valheim `INDEX.md`를 확인했다. 프로젝트 및 상위 작업 폴더에 추가 `AGENTS.md`는 없었다.
- 기존 [9월 12일 구조 리뷰](RefactoringReview-2026-09-12.md), [1.0.15 대응 기록](Valheim-1.0.15-Compatibility.md), [클라이언트 단독 설치 검증](../build/ClientOnlySupport-validation.md), 컨트롤러 검사 설명과 현재 코드·Git 이력을 대조했다. 이전 검사 결과를 이번 실행 결과로 합산하지 않았다.
- 읽기 전용 에이전트가 도메인, UI, 외부 계약을 병렬 조사했다. 편집·빌드·커밋은 한 세션에서 순서대로 수행했다. 새 브랜치/worktree, push, Release 빌드/ZIP/게시를 수행하지 않았다.

검토는 주요 상태 소유권·수명주기·호출 경계와 아래 변경 후보에 집중했다. 모든 소스 줄이나 모든 외부 모드 조합에 대한 완전한 감사를 뜻하지 않는다.

**제외:** `bin`, `obj`, 기존 배포 ZIP, 생성 코드/publicized 중간 DLL의 리팩터링, ServerSync·YamlDotNet 내부 수정, InventoryActions 독립 구현 변경, 외부 모드의 전체 DLL/모든 버전 분석. 공개 API와 알려진 간접 호출 경로는 조사했지만 외부 모드의 임의 private reflection까지 부재를 증명하지는 않았다.

## 실제 빌드 및 게임 계약

| 항목 | 확인 결과 |
| --- | --- |
| 빌드 대상 | `InventorySlots.csproj`, `net48`, 명시적인 Compile 항목. 솔루션에는 InventoryActions와 테스트도 있으므로 이번 모드 빌드는 csproj를 지정했다. |
| 진입점 | `InventorySlotsPlugin : BaseUnityPlugin`, BepInEx 플러그인. 프리로더 패처가 아니다. `Awake`/`Update`/`OnDestroy`는 `PluginLifecycle.cs`에 있다. |
| 소스/리소스 | 루트 C# 및 Shared ItemRules/Persistence/Sorting 등 명시적 공유 소스. 기본 슬롯/ResourceMap YAML, 영문·국문 현지화 등 최종 임베디드 리소스 5개를 전후 비교했다. |
| 설정/저장 | BepInEx 키·기본값·live 이벤트, ServerSync YAML 적용, ClientState 모델·정규화·파일 저장 및 공개 API를 유지했다. |
| 병합 | `build/ILRepack.targets`: 모드 + 고정 `Libs/ServerSync.dll` + YamlDotNet 16.3.0을 최종 DLL로 병합/internalize한다. |
| 배포 | `DeployToGame=true` 타깃이 병합 뒤 최종 `InventorySlots.dll`만 Steam `BepInEx/plugins`에 복사한다. |
| 패키지 | 기존 BepInEx manifest 의존성은 `denikson-BepInExPack_Valheim-5.4.2350`. 이번 변경 없음. |
| 지원 기준 | 현재 코드·문서의 대응 대상은 **Valheim 1.0.15**. `FindFreeStackItem(string, int, float)`를 사용하며 1.0.14 동시 지원으로 확대하지 않았다. |

원본 기준 경로는 `C:/Users/blizz/.codex/references/valheim/snapshots/` 아래다.

- 클라이언트: `client-b25390630-windows-x64-20260918T131715Z/original/valheim_Data/Managed`
- 서버: `dedicated-server-b25390671-windows-x64-20260918T185703Z-depot-restored/original/valheim_server_Data/Managed`

설치된 `assembly_valheim.dll`은 원본 클라이언트의 SHA-256 `59F53FB55D99D22A33E8ED094EEC8D21E9F133543BCE92BC3D80DCE44033ADB1`과 일치했다. 초기 1.0.15 문서의 서버 원본 미확인 기록보다 나중의 전역 스냅샷과 클라이언트 단독 설치 검증 문서를 사용했다. 새 게임 자료 수집·재추출은 하지 않았다.

컴파일은 기존 AssemblyPublicizer.MSBuild 0.4.2가 `assembly_valheim`, `assembly_utils`, `assembly_guiutils` 참조를 공개화하는 구성을 유지한다. 분석·정적 계약 검사는 **원본** DLL을 사용했다. 원본 `Inventory.FindFreeStackItem`, `Container.m_nview`/`m_loading`/`m_lastRevision`/`Load()`의 private 접근 제한을 확인했다. 이번 수정은 새 게임 멤버 접근을 추가하지 않는다. 기존 `IgnoresAccessChecksTo` 생성 및 컴파일 성공은 Mono 런타임 접근 검증과 별개다.

ServerSync는 기존 vendor 기준 `valheim-1.0.7-r1`, SHA-256 `B4DD786997F4E90D770F09EF3E9D64154754FE7E8EDFB4841795751895B35846`을 그대로 병합한다. 선택적 의존성은 장비/시각화, 제작 UI, 툴팁, 서버 캐릭터/권한, 컨테이너 연동별 adapter로 남긴다. 특히 EpicLoot, Jewelcrafting, RecycleNReclaim, AzuCraftyBoxes의 서로 다른 정책을 공통 인터페이스로 합치지 않았다.

## 영역별 판단

| 영역 | 구조 상태 | 판단 근거 및 처리 |
| --- | --- | --- |
| 정렬 캐시 | 불필요한 분리/간접 참조 | 캐시를 쓰는 파일은 하나인데 삭제된 컨트롤러용 상태 래퍼가 다른 파일에 남아 있었다. 아래 1단계로 공동 배치했다. |
| InventoryState 전반 | 집중은 있으나 대부분 유지할 이유가 있음 | panel, safety, client, equipment 등 상태별 소유권이 구분된다. 독점 사용이 입증된 정렬 상태 외에는 일괄 해체하지 않았다. |
| 제작 model/state | 대체로 균형 | dirty/stamp 변경은 CraftingController, queue와 recipe 캐시는 별도 상태다. 같은 fast-path 조건만 2단계에서 한 곳으로 모았다. |
| 제작 presentation | 일부 집중, 분할 이익 불충분 | BottomControls/GridInteraction/Redesign이 크지만 갱신 순서와 상태가 함께 바뀐다. 파일 크기만으로 새 계층을 추가하지 않았다. |
| Grid/List/외부 제작 탭 | 책임 분리가 적절 | List의 상세 UI 생성·파괴·갱신과 공통 선택/필터는 다르다. Reclaim/Socket/외부 탭의 표시·재료·설명 정책도 다르다. |
| 컨트롤러/버튼/도움 UI | 현재 분리 유지 | 입력 중재, 메뉴 상태, UI 객체 소유권이 다르다. 동일 프레임 입력 예약, stale 입력 차단, owner 파괴 검사를 유지한다. |
| YAML/ClientState | 균형 | watcher thread에서 main-thread로 전달하는 경계, 파싱·검증·적용, 모델·정규화·I/O는 다른 책임이다. load/save의 반복 검증도 적용 조건이 다르다. |
| 슬롯 배치/안전/장비 | core와 실행 경계 유지 | 복구 배치와 일반 배치의 hotbar 정책이 다르고, projection 캐시와 실제 장비 효과도 다르다. Circlet/HipLantern 위임을 억지 통합하지 않았다. |
| 컨테이너 네트워크 | 실행 조정에 집중이 있으나 경계 타당 | custom token/lease 프로토콜, request ID 없는 native RPC, shared GUI replay는 다른 안전 계약이다. 별도 core와 실행 경계를 유지했다. |
| optional reflection/API | 균형 | 공통 반사 탐색·실패 상태와 외부 모드별 필수/선택 API를 분리한다. EpicLoot callback 등록/해제 수명은 다른 adapter와 다르다. |

Git 근거: `dcf3f82`는 ItemSortController를 제거하면서 상태 래퍼를 남겼다. `b32103b`는 제작 dirty 처리를 중앙화했고 `6c926b3`는 List 동적 갱신을 확장했다. 네트워크의 `ab0f9ec`(공동 GUI 복구), `922e189`(클라이언트 단독 설치/native 경로), `dbf0fff`(remote requester의 STUWard 권한)는 여러 파일이 서로 다른 안전 책임으로 함께 변경되는 사례다.

## 구현한 변경

### 1. 정렬 캐시와 실제 사용 코드 공동 배치

커밋: **`eea503c67107aecd75f53b6d3278765c0f28a957`**

- `ItemSortCategorizer.cs`에 recipe-output dictionary와 signature를 private static 필드로 옮겼다.
- `InventoryState.cs`의 `InventorySortRuntimeState`와 singleton을 제거했다.
- 쓰이지 않는 두 인자 private comparator를 제거했다. 실제 경로는 `ContainerSort.SortInventoryInternal →` 미리 계산한 SortKey/원래 위치를 받는 여섯 인자 comparator다. source, `nameof`/reflection 문자열, Harmony, 공개 API, Unity 메시지 경로에서 삭제 대상의 사용 근거를 찾지 못했다.
- dictionary 비교자, signature 형식·계산, first-wins 등록, clear 순서와 `ClearCraftingRecipeCaches → ClearInventorySortCaches` 호출을 그대로 유지했다.
- 새 필드 초기화는 comparer/dictionary/빈 문자열뿐이며 Unity나 다른 plugin 상태에 의존하지 않는다. 조사한 다른 정적 initializer도 이 캐시를 호출하지 않는다.

효과는 캐시 변경 시 두 파일과 중간 객체를 따라갈 필요를 없애는 것이다. 정렬 정책이나 측정된 속도 향상을 주장하지 않는다. 위험은 초기화 순서·캐시 무효화·간접 참조 변경이므로 해당 호출과 생성 코드를 전후 대조했다.

### 2. 제작 fast-path 진입/저장 조건의 단일화

커밋: **`c054e58e29682a179699e7ac2ebcebf9723e87b1`**

- `CraftingFrameFastPath.cs`의 `StoreCraftingFrameFastPathSignature`에서 중복된 일곱 조건 대신 `!CanRunCraftingPanelFrameFastPath()`를 사용한다.
- 전체 갱신과 `FinalizeCraftingTabAdapterFrame` **이후** 다시 평가하는 시점을 유지했다. search/queue/dirty 상태를 미리 저장해 재사용하지 않는다.
- stamp 생성·optional adapter 호출 뒤 grid null/파괴 상태를 재확인하는 코드는 별개의 보호 경계라 유지했다.
- 조건 불충족 시 stamp reset, 충족 시 새 stamp 저장도 동일하다.

효과는 향후 fast-path 조건을 바꿀 때 진입과 저장의 두 목록이 어긋날 가능성을 줄이는 것이다. 새 상태·캐시·계층은 추가하지 않았다. 새로운 UI stub harness는 이 작은 조건을 반복하는 비용이 커서 추가하지 않았다.

## 보존 및 보류 판단

- Harmony 대상·overload·priority·반환값·`__state`·finalizer/예외 정책은 변경하지 않았다.
- 공개 `InventorySlotsApi`, 설정 키/기본값, 저장 모델, YAML, 번역 리소스, 초기화/이벤트 해제/파괴 경로를 변경하지 않았다.
- 클라이언트 단독 설치 허용 및 모드 서버의 동버전 요구를 유지한다. connection별 capability 정리와 서버/클라이언트 실행 분기를 유지한다.
- custom handoff의 sender/player, request/action/container, owner/revision/token, 거리·권한 검증과 최신 inventory load 순서를 유지한다. native 응답 지연 fence, shared GUI epoch/snapshot/drag 재확인도 유지한다. 로컬 owner와 사용자 권한을 합치지 않았다.
- 슬롯 backup의 서버 캐릭터 관리 시 차단, local-player/loading 검사, 기존 tail item 검사 및 reentrancy guard를 유지한다.
- 패널 resize의 protected-child 이중 열거는 실제 resize 때만 실행된다. Unity 복원·외부 UI 검증 비용에 비해 이익이 작아 이번에 합치지 않았다.
- 매 프레임 stamp/context 문자열, optional adapter 조회, tooltip/foreign UI 보정, controller glyph 갱신을 확인했다. 새 캐시를 넣으면 localization, 입력 레이아웃, 선택/drag, GUI 파괴까지 무효화 조건이 늘어난다. 측정 없이 지속 캐시를 추가하지 않았다.
- 외형이 비슷한 슬롯 탐색, 반사 실패 guard, 권한 검사에는 서로 다른 정책이 있어 통합하지 않았다.

**별도 결함/정책 후보:** `Shared/Persistence/LinkCompatibleFileWriter.Write`의 `finally`에서 임시 파일 삭제가 실패하면, 이미 목적지 기록이 성공했더라도 정리 예외가 전달되거나 원래 쓰기 예외를 가릴 수 있다. 코드상 조건부 위험이며 이번 환경에서 재현한 저장 장애는 아니다. 오류 보고/재시도 정책 및 파일 I/O 실패 주입을 별도로 검토할 항목으로 남겼다. 공유 파일의 다중 인스턴스 저장 정책도 이번 구조 변경에 섞지 않았다.

`AzuCraftyBoxesApi`의 Invoke helper는 methodName을 받지만 캐시는 Type만 키로 사용한다. 현재 호출은 각각 고정 메서드 한 종류라 확인된 오류는 없다. 이름을 구체화하는 작은 정리는 이득이 작아 보류했다. 기타 신규 게임 호환성 결함이나 정책 변경을 이번 커밋에 포함하지 않았다.

## 검증

| 단계 | Debug 빌드 | 기존 자동 검사 | 원본 게임 DLL 계약 검사 |
| --- | --- | --- | --- |
| 기준 `4f85ec8` | 성공, 경고/오류 0 | 176 통과 | 클라이언트·서버 각각 실패 0 |
| 정렬 개선 `eea503c` | 성공, 경고/오류 0 | 176 통과 | 클라이언트 실패 0 |
| 제작 개선 `c054e58` | 성공, 경고/오류 0 | 176 통과 | 클라이언트·서버 각각 실패 0 |

각 계약 보고서: 직접 참조 **1,096**, 정적 Harmony 대상 **153**, 반사 계약 **49**, 기존 수동 확인 항목 **10**. 반사 계약 49개는 검사기에 등록된 항목이며 모든 reflection 경로의 완전 목록은 아니다. 동적 Harmony 및 런타임 항목 10개를 통과로 계산하지 않았다. 모델 실행 검사와 source guard 검사가 포함된 176개 검사이며 Unity 실행 검사가 아니다. 변경이 없는 controller/network 전용 suite를 이번에 재실행한 것으로 보고하지 않는다.

추가 전후 DLL 비교(Mono.Cecil):

- `InventorySlotsApi`의 signature 및 메서드 IL 동일.
- 임베디드 리소스 5개 byte 동일.
- 실제 여섯 인자 comparator, sort orchestration, recipe signature/tier/등록, crafting cache clear의 IL 동일(정렬 단계).
- fast-path eligibility/진입, 제작 전체 갱신, `Awake`/`OnDestroy` IL 동일(최종 단계).
- `BindConfigs`의 엄격 IL 비교에서는 삭제된 private 멤버로 인해 compiler-generated lambda 번호가 달랐다. 해당 번호만 정규화한 뒤 전체 본문이 동일함을 확인했다. `BindItemRuleConfigs`도 동일했다. 이를 설정 변경이나 게임 실패로 분류하지 않았다.
- 단계마다 diff 및 `git diff --check` 검토, 읽기 전용 독립 리뷰 완료.

사용 명령:

```powershell
dotnet build InventorySlots.csproj -c Debug -p:DeployToGame=true
dotnet run --project InventorySlots.Tests/InventorySlots.Tests.csproj -c Debug
dotnet run --project build/CompatibilityCheck -c Debug -- bin/Debug/InventorySlots.dll <originalManaged> <BepInExCore> <report.json>
```

로컬 결과는 ignored `artifacts/refactor-20260923/`에 기준/단계별 DLL, 검사 로그와 client/server JSON으로 보관했다. 배포 원본은 `bin/Debug/InventorySlots.dll`, 설치 대상은 `C:/Program Files (x86)/Steam/steamapps/common/Valheim/BepInEx/plugins/InventorySlots.dll`이다. 각 단계에서 두 파일 SHA-256이 일치했다.

- 기준: `BA0DA406C353BFAB549FB834255F621534424EDBF9B9AFC9C5498C175F38683C`
- 정렬 단계: `5E099BDBD617A945F3F822BC81C0CC8BEBE4AAA9362A1486203178F99563A72E`
- 최종 구현: `D833D1AA9DDBF42AE678A6395A88D195FBE44AEB1502C088B60828BA47B49EA0`

## 남은 실행 검증

이번에 실제 Valheim/Unity/Mono 게임 세션, 호스트·데디케이트 접속, Steam/PlayFab 크로스플레이 또는 동시 플레이를 실행하지 않았다. 공개화된 컴파일 참조나 원본 메타데이터 검사는 실제 런타임 접근·Harmony 합성·화면 동작·네트워크 안전성의 실행 증명이 아니다.

변경과 직접 관련된 게임 확인은 플레이어/상자 정렬의 동률 순서, YAML/ResourceMap 변경 뒤 분류 갱신, 제작 검색/queue/Grid↔List/외부 제작 탭 전환 뒤 갱신과 닫기·재열기다. 기존 전체 모드 검증의 멀티플레이/소유권/저장·재접속 한계는 그대로 남으며 이번 작은 변경의 통과 범위로 확대하지 않는다.

구현 커밋 두 개는 각각 독립적으로 되돌릴 수 있다. 이 기록은 별도 문서 커밋으로 남긴다. 최종 저장소 상태와 문서 커밋을 포함한 최종 HEAD는 작업 완료 시 `git status`와 `git log`로 확인한다.
