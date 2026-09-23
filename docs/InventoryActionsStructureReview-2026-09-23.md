# InventoryActions 구조 검토 및 개선 — 2026-09-23

## 기준과 범위

- 시작: 실제 `main`, `5224d396968ff3d54d79e6dfb27a9ab7106dc8a9`, 미커밋 변경 없음. 이전 InventorySlots 구조 개선 커밋 3개를 그대로 보존했다.
- 대상은 InventoryActions **1.1.4**이며 버전·의존성·게임 지원 정책을 변경하지 않았다. 공유 controller 소스 변경은 InventorySlots에도 적용되므로 해당 소비자도 빌드·검사했다.
- 전역 `C:/Users/blizz/.codex/AGENTS.md`, Valheim `INDEX.md`, 프로젝트의 빌드·검사 설명을 확인했다. 프로젝트/상위 작업 폴더에 추가 AGENTS.md는 없었다.
- [9월 12일 리뷰](RefactoringReview-2026-09-12.md), [클라이언트 단독 설치 기록](../build/ClientOnlySupport-validation.md), [컨트롤러 검사 설명](../build/ControllerTests/README.md), 기존 버전 대응 기록을 현재 코드·Git 이력과 대조했다. 이전 리뷰의 1.0.12 결과를 현재 실행 결과로 옮기지 않았다.
- 병렬 에이전트는 도메인, UI, 외부 계약을 읽기 전용으로 조사·리뷰했다. 편집·빌드·커밋은 주 세션이 각 개선별로 수행했다. 새 브랜치/worktree, push, Release 빌드/ZIP/게시 없음.

검토는 주요 실행 흐름과 상태·수명주기·정책 경계에 집중했다. 모든 코드 줄, 모든 게임 버전이나 외부 모드 조합을 전수 검증한 것은 아니다. 생성물 `bin`/`obj`, 기존 패키지, publicized 중간 DLL, ServerSync/YamlDotNet 내부, 외부 모드 전체 구현은 리팩터링에서 제외했다. InventorySlots 고유 기능도 이번 수정 범위가 아니다.

## 실제 빌드와 게임 기준

| 항목 | 확인 결과 |
| --- | --- |
| 프로젝트/진입점 | `InventoryActions/InventoryActions.csproj`, net48. `InventoryActionsPlugin : BaseUnityPlugin`의 Awake/Update/OnDestroy. 일반 BepInEx 플러그인이며 프리로더 패처가 아니다. |
| 컴파일 소스 | Actions 루트 `*.cs`, Localizer/AssemblyInfo, Shared ItemRules/Persistence/Sorting의 명시적 Compile 항목. |
| 리소스/저장 | English/Korean 번역을 포함한 최종 임베디드 리소스 3개. BepInEx 설정, 캐릭터별 favorite 좌표·아이템 기억 파일과 retry 상태를 유지한다. |
| 공개/간접 경계 | 별도 InventoryActionsApi 클래스는 없다. public plugin/enum 등의 signature, Unity 메시지, Harmony 진입점, optional reflection 계약을 보존한다. |
| 선택적 의존성 | MultiUserChest, ExtraSlots, EquipmentAndQuickSlots, AzuExtendedPlayerInventory. Slots의 EpicLoot/Jewelcrafting adapter 구성을 Actions의 계약으로 가정하지 않았다. |
| 병합/배포 | 부모 environment.props와 build/ILRepack.targets 사용. 모드 + 고정 ServerSync + YamlDotNet 16.3.0을 병합/internalize한 뒤 DeployToGame 타깃이 최종 DLL만 Steam plugins로 복사한다. |
| 패키지 | BepInEx manifest 의존성은 이미 `denikson-BepInExPack_Valheim-5.4.2350`. 변경 없음. |
| 게임 기준 | 현재 설치·빌드·관련 대응 기록과 이번 검사는 **Valheim 1.0.15** Windows x64, client b25390630 / server b25390671 기준이다. 이번 작업으로 다른 버전의 지원/비지원 정책을 새로 정하지 않았다. |

원본 자료는 `C:/Users/blizz/.codex/references/valheim/snapshots/` 아래 기존 자료를 재사용했다.

- client: `client-b25390630-windows-x64-20260918T131715Z/original/valheim_Data/Managed`
- server: `dedicated-server-b25390671-windows-x64-20260918T185703Z-depot-restored/original/valheim_server_Data/Managed`

설치 게임 DLL의 SHA-256 `59F53FB55D99D22A33E8ED094EEC8D21E9F133543BCE92BC3D80DCE44033ADB1`이 원본 클라이언트와 일치한다. ServerSync도 기존 vendor `valheim-1.0.7-r1`, SHA-256 `B4DD786997F4E90D770F09EF3E9D64154754FE7E8EDFB4841795751895B35846`을 유지했다. 새 자료 수집·재추출·upstream 교체는 하지 않았다.

기존 컴파일 설정은 AssemblyPublicizer.MSBuild 0.4.2로 세 게임 참조를 공개화한다. 분석·격리/정적 검사는 **원본** DLL을 사용했다. 원본 Inventory.Changed(bool,bool), Container.m_nview 등의 private 접근을 확인했으며, 이번 변경은 새 게임 멤버 접근을 추가하지 않는다. 공개 getter InventoryGrid.GetInventory는 m_inventory를 반환한다. 원본 DLL을 수정하지 않았으며, 컴파일과 접근 특성 생성은 실제 Mono/Unity 런타임 검증을 대체하지 않는다.

Actions에는 Slots의 FindFreeStackItem 패치가 없다. 따라서 Slots 문서의 1.0.14/1.0.15 overload 제한을 그대로 Actions의 지원 제한으로 기술하지 않는다.

## 영역별 구조 판단

| 영역 | 판단 및 처리 |
| --- | --- |
| Plugin/optional 설정 수명주기 | 작은 delegate 연동 구조는 적절하다. 동일 AzuEPI 구독 정리만 세 경로에 복제되어 아래 1단계로 공동 배치했다. |
| controller 단축키/메뉴 | 입력 gesture 상태와 menu 상태는 구분할 이유가 있다. 반면 동일한 favorite 자격 정책은 메뉴 추가 시 복제되어 아래 2단계로 합쳤다. |
| Actions.cs | 이동·정렬·투입 후보·결과 통지 등이 집중되어 있지만 실제 action별 정책이 다르다. 크기만으로 service/interface로 나누지 않았다. |
| ownership core/실행 | pure state machine과 Unity/ZDO/RPC 관측은 좋은 검증 경계다. 실행 파일의 집중은 protocol·lease·권한·정리 순서를 같이 읽을 이점도 있다. |
| custom/native 프로토콜 | request ID/token이 있는 경로와 ID 없는 vanilla 응답 fence는 다른 정책이다. owner 검사나 timeout 처리를 통합하지 않았다. |
| Favorites/저장 | 좌표·아이템 기억·저장 retry, UI marker가 분리되어 있다. 파일 I/O 및 공유 저장 helper 경계를 유지한다. |
| Ui.cs | 배치·버튼·휴지통이 다소 집중되어 있으나 생성/갱신/복원/확인 흐름이 명시적이다. 새 파일 분할의 이익이 작다. |
| FeatureGuide/버튼 hint | 측정 캐시와 객체 소유권·파괴 경계가 있다. language/device/config/외부 UI 변경 때문에 공격적인 캐시는 검증 비용을 늘린다. |
| ConfigAndUtility | 설정 바인딩과 adapter용 predicate가 모여 있다. 사소한 forwarding 함수는 공유 소스의 host 경계이기도 하므로 일괄 제거하지 않았다. |

이력 근거: `39bbc71`의 아이템 메뉴 추가에서 favorite 검사 중복이 생겼다. `8d39016`의 Changed delegate/인벤토리 경계 대응, `922e189`의 client-only/native handoff 추가는 접근·프로토콜 경계가 별도 책임임을 보여준다. 과거 marker 재검색 제거와 버튼 위치 파싱 캐시 개선은 현재도 반영되어 있어 다시 작업하지 않았다.

## 구현과 독립 커밋

### 1. AzuEPI 설정 구독 정리의 공동 배치

**`4fd01abdb2dd1ee557972603a8b602e2b8b48793`** — `InventoryActions/Plugin.cs`, 기존 CompatibilitySmoke 검사.

- OnDestroy, InitializeAzuEpiCompatibility 실패, RefreshAzuEpiSeparatePanelSetting 실패의 동일 코드를 같은 파일의 private `ClearAzuEpiSeparatePanelSubscription`으로 모았다.
- 이벤트 해제 → config/entry/cached value 초기화 순서를 보존한다. unsubscribe 자체가 예외를 내면 이후 초기화/로그가 실행되지 않는 기존 예외 전파도 바꾸지 않았다.
- **슬롯 경계 delegate는 helper에 넣지 않았다.** 종료/초기화 실패에서는 해제하지만 UI 설정 읽기 실패에서는 계속 사용한다. 설정 실패가 특수 슬롯 보호를 끄지 않아야 한다.
- 새 계층·인터페이스·캐시는 없다. 같은 상태를 정리하는 변경 지점이 3곳에서 1곳으로 줄어든다.

기존 실제 DLL 격리 검사에 설정 타입/값 불일치 이벤트를 추가했다. 실제 ConfigFile 이벤트로 실패 경로를 거쳐 전체 높이 UI fallback, 특수 슬롯 보호 유지, config 참조 정리를 확인한다. 새 helper를 직접 호출해 구현을 반복하는 검사는 아니다. 변경 전 DLL부터 신규 3개를 포함한 321개가 통과함을 확인했다.

### 2. 컨트롤러 favorite 자격 정책의 단일화

**`1cef34201ce5fa587d0712db7a2c83f9c6915437`** — `Shared/ItemRules/InventoryController.cs`, `ControllerItemMenu.cs`.

- 기존 CanControllerFavoriteCell을 controller 공통 경로 옆으로 옮기고 단축키도 호출한다. player grid/실제 player inventory/bounds/모드별 칸 정책을 한곳에서 관리한다.
- inventory를 인자로 받아 단축키의 `GetInventory → Selection` 읽기 순서를 유지했다. 메뉴는 `cell → GetInventory` 평가 순서를 유지한다.
- 기존 guard의 early return이 false 반환으로 바뀌어도 같은 if/else-if 분기 종료 지점이므로 Sort/Restock으로 흘러가지 않는다. 앞선 null/loading/teleport 검사, 입력 alias 소비·동일 프레임 예약은 유지한다.
- 단축키에서 player-grid 동일성 비교가 한 번 더 수행된다. 두 비교 사이의 원본 getter와 FieldRef는 상태를 변경하는 callback이 없는 읽기다. 비교를 없애기 위해 별도 wrapper를 추가하지 않았다. 제3자의 임의 getter 패치 조합까지 실행 검증한 것은 아니다.
- Slots의 CanFavoriteSlot과 Actions의 CanFavoriteCell을 같은 정책으로 합치지 않았다. 기존 조건부 host 분기를 유지한다.

공유 소스이므로 양 모드의 기존 source-linked controller 검사와 Debug/원본 DLL 계약 검사를 수행했다. 기존 검사에 정상/빈 칸/다른 inventory/범위 밖/특수 칸/상자/메뉴 무효화/중복 입력이 포함되어 있어 복제형 테스트는 추가하지 않았다.

## 유지한 계약과 보류한 개선

- Harmony target/overload/priority/반환값/`__state`/finalizer, 공개 signature, 설정 키·기본값·live 이벤트 정책, 저장 형식, 버전·manifest를 유지한다.
- client-only 접속 허용, 모드 서버의 동버전 요구, connection별 capability 수명과 disconnect/StopAll 정리를 유지한다.
- custom handoff의 requester/거리/ward/개인 상자 권한, owner/revision/token/lease 확인 후 최신 inventory를 로드하는 순서를 유지한다. callback 전 target 진행과 실패 시 inventory flush, native 늦은 응답 fence도 변경하지 않았다.
- MUC 활성 시 일반 area mutation/handoff를 로컬 owner 여부와 별개로 차단한다. 지원 MUC의 remote TakeAll 위임은 별도 정책이다. Slots의 공동 GUI 정책과 합치지 않았다.
- ExtraSlots/EAQS/AzuEPI API는 초기화 시 해석하고 live row 경계를 읽는다. 조회를 프레임 캐시로 바꾸면 외부 API의 호출 시점과 갱신 조건이 달라져 보류했다.
- Actions의 customData 보유 스택 자동 병합 제한과 Slots의 등록 metadata 정책은 다르다. 이를 통합하지 않았다.
- MergeSortableStacks의 두 번째 group.ToList는 이미 독립된 local snapshot이라 제거 가능하다. 다만 현재 우선한 수명주기/입력 정책 중복보다 이익이 작으며, 소진 donor의 RemoveItem/native notification까지 검증 범위를 넓힐 필요가 있어 이번에는 유지했다. 기존 --favorite-fill의 30개 검사는 해당 소진 donor 제거 경로를 실행하지 않는다.
- per-frame GetButtonPos/hierarchy 검색, controller caption 및 guide text 갱신에는 외부 UI/언어/입력 장치 무효화 문제가 있다. 측정 없이 새 캐시를 넣지 않았다. 이번 변경으로 측정된 성능 향상을 주장하지 않는다.

## 별도로 남긴 결함·정책 후보

1. **트로피 분류:** 원본 1.0.15 enum은 Trophy인데 Actions.GetItemSortCategory는 Trophie 문자열을 사용하여 fallback 90으로 간다. 기존 리뷰의 후보가 현재도 유효하다. 60으로 바꾸면 실제 정렬 결과가 달라지므로 별도 버그 수정으로 남겼다.
2. **파괴된 custom handoff 대상:** PendingTarget이 Unity fake-null이 되면 Observe(timeout 포함)를 건너뛰고, Continue는 phase가 Idle이 아니어서 반환할 가능성이 있다. 정상 OnDestroyed 등록 해제는 처리하지만 그 경로 없는 unload를 추가 재현해야 한다. 확인된 플레이 장애로 단정하거나 이번 구조 변경에 섞지 않았다.
3. **공유 파일 정리 예외:** 이전 Slots 리뷰에 기록한 LinkCompatibleFileWriter의 finally 삭제 예외가 원래 오류/성공 결과를 가릴 가능성은 Actions에도 해당한다. 이번에는 오류·재시도 정책을 변경하지 않았다.

## 수행한 검증

| 검사 | 결과/범위 |
| --- | --- |
| Actions Debug | 기준 및 두 단계 모두 경고 0, 오류 0. DeployToGame=true로 최종 병합 DLL 복사 및 SHA-256 일치. |
| Slots Debug | 공유 controller 변경 후 경고 0, 오류 0. 같은 배포·해시 검증 수행. |
| 기존 일반 검사 | 기준/각 변경 후 176개 통과. 모델 및 source guard 검사이며 Unity 실행은 아니다. |
| 규칙·favorite memory | Actions 기준 135개 통과. 이번에 해당 구현은 변경하지 않았다. |
| compiled DLL 격리 | 기준 --favorite-fill 30개, 기존 --ui-layout 318개 통과. 실패 경로 3개 추가 후 기준/구독 정리 DLL 모두 client/server 각각 321개 통과. .NET Framework CLR 4.0.30319.42000, 정상 종료. |
| controller fake host | 변경 전후 Actions 423개, Slots 425개 통과. 실제 공유 소스를 실행하지만 Unity/Harmony 설치/gamepad 하드웨어는 실행하지 않는다. |
| Actions 원본 계약 | 기준/각 단계 client/server 각각 참조 652, 정적 Harmony 47, 등록 반사 계약 7, 실패 0, 수동 확인 2. |
| Slots 원본 계약 | 공유 변경 후 client/server 각각 참조 1,096, 정적 Harmony 153, 등록 반사 계약 49, 실패 0, 수동 확인 10. |
| DLL 전후 비교 | 양 모드 public/protected type/member signature·상수 필드, Actions 리소스 3개/Slots 5개 동일. BindConfigs IL도 compiler-generated lambda 번호만 정규화하면 동일. |
| diff/리뷰 | 각 단계 git diff --check 및 읽기 전용 독립 리뷰 후 해당 변경만 커밋. |

반사 계약은 검사기에 등록된 범위이며 모든 reflection을 포함하지 않는다. 동적/수동 확인 항목을 통과로 계산하지 않았다. 전체 CompatibilitySmoke 모드, 별도 network/controller rendering suite 또는 실제 AzuEPI 플러그인 설치·초기화/종료를 실행했다고 보고하지 않는다.

주요 명령:

```powershell
dotnet build InventoryActions/InventoryActions.csproj -c Debug -p:DeployToGame=true
dotnet build InventorySlots.csproj -c Debug -p:DeployToGame=true
dotnet run --project InventorySlots.Tests/InventorySlots.Tests.csproj -c Debug
dotnet run --project InventoryActions/build/RuleTests/RuleTests.csproj -c Debug
dotnet run --project build/ControllerTests/ControllerTests.csproj -c Debug -p:ControllerTarget=InventoryActions
dotnet run --project build/ControllerTests/ControllerTests.csproj -c Debug -p:ControllerTarget=InventorySlots
dotnet run --project build/CompatibilityCheck -c Debug -- <final.dll> <originalManaged> <BepInExCore> <report.json>
```

compiled DLL 검사는 `InventoryActions/build/CompatibilitySmoke`를 Debug로 빌드한 뒤 exe에 동일한 세 경로와 `--ui-layout` 또는 `--favorite-fill`을 전달했다. ignored `artifacts/actions-refactor-20260923/`에 기준/단계별 DLL, 로그, 원본 client/server JSON을 보관한다.

- Actions 기준 DLL: `DC31759ADBF6F6A9F6B460FA9E774F6EE2BE355E201F44DA08016BA99DC3D9CD`
- Actions 구독 정리 DLL: `7A728459B66BAF76E06F26402C823061F3C35E787F12A40EE1BB2EC2EC36B1CB`
- Actions 최종 DLL: `C69C0D9A62EB0A7A241E535720BD3F0755610604D9A06C21DC9613BDCB29E424`
- Slots 공유 변경 전 DLL: `D833D1AA9DDBF42AE678A6395A88D195FBE44AEB1502C088B60828BA47B49EA0`
- Slots 공유 변경 후 DLL: `53D5E54B227BBFA810FA52D1A152F1B6B0F73A441AAF97A37BF94FB95583A034`

최종 DLL은 각각 `InventoryActions/bin/Debug/InventoryActions.dll`, `bin/Debug/InventorySlots.dll`이며 Steam `BepInEx/plugins`의 같은 이름 DLL과 해시가 일치한다. 두 모드는 동시 사용을 지원하지 않으므로 실제 게임 검증은 각각 활성화한 환경에서 해야 한다. 이번에는 게임을 실행하지 않았다.

## 남은 실행 확인과 저장소 상태

실제 Valheim/Unity/Mono 게임, 호스트·데디케이트 연결, Steam/PlayFab 크로스플레이·동시 상자 접근·저장/재접속은 미실행이다. 관련 수동 확인은 AzuEPI 설정 live 변경/실패 fallback/종료 정리와, 두 모드의 controller 단축키·메뉴가 일반/특수/빈 칸에서 같은 eligibility를 적용하는지다. 실제 UI·입력 장치·제3자 Harmony 조합은 위 격리 결과의 보장 범위를 넘는다.

두 구현 커밋은 각각 독립적으로 되돌릴 수 있다. 이 문서는 별도 커밋으로 남기며, 최종 HEAD와 작업 트리 상태는 완료 보고 시 git log/status로 확인한다. 이전 Slots 작업 커밋을 이동·일괄 커밋하거나 이력을 재작성하지 않았다.
