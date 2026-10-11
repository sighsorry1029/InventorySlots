# InventoryActions 구조 검토 및 개선 — 2026-10-11

## 기준과 작업 범위

- 실제 `main`, 기준 커밋 `cfbec9e657fcd44d867eca53ee6218d8a56b25a4`.
- 시작부터 존재한 변경은 tracked 62개 + untracked 18개, 총 80개 파일이었다. 이를 작업 전 diff와 SHA-256으로 기록했다. 빌드 기준은 이 기존 변경을 포함한 작업 트리이며, 깨끗한 HEAD만의 빌드 결과가 아니다.
- InventoryActions **1.1.13**의 기능과 외부 계약을 유지하는 구조 개선이다. 버전·설정·지원 정책·의존성을 변경하지 않았다. 새 브랜치/worktree, push, Release/ZIP/게시 작업 없음.
- 전역 `C:/Users/blizz/.codex/AGENTS.md`, Valheim `INDEX.md`, 관련 빌드/검증 문서를 확인했다. 저장소와 부모 작업 폴더에 추가 AGENTS.md는 없었다.
- [이전 Actions 리뷰](InventoryActionsStructureReview-2026-09-23.md), [MUC 전송 문서](MultiUserChestRestock.md), [UI 성능 문서](InventoryUiPerformance.md), [클라이언트 단독 설치 기록](../build/ClientOnlySupport-validation.md)을 현재 소스와 이력에 대조했다. 이전 리뷰의 게임 버전·의존성·MUC 지원 상태를 현재 사실로 재사용하지 않았다.
- 주 세션이 편집·빌드·커밋을 담당했다. 병렬 조사는 설정/저장, UI/선택적 인벤토리, 컨테이너/프로토콜의 읽기 전용 조사와 최종 diff 리뷰였다.

검토는 실행 흐름, 상태 소유권, 저장·설정, UI 수명주기, 선택적 연동과 전송 정책 경계에 집중했다. 모든 소스 줄이나 모든 조합을 전수 검증한 것은 아니다. `bin/obj`, 기존 ZIP, publicized 중간물, ServerSync/YamlDotNet 내부, 참고용 외부 DLL 전체, 진단 도구와 InventorySlots 고유 기능은 리팩터링에서 제외했다. 공통 저장 도우미를 바꿨으므로 InventorySlots 소비 경로도 빌드·검사했다.

## 빌드·게임·외부 계약

| 항목 | 현재 확인 결과 |
| --- | --- |
| 프로젝트/진입점 | `InventoryActions/InventoryActions.csproj`, net48. `InventoryActionsPlugin : BaseUnityPlugin`의 Awake/Start/Update/OnDestroy와 Harmony 진입점. 일반 플러그인이며 프리로더 패처가 아니다. |
| 컴파일 범위 | Actions 루트 C# 파일, Localizer/AssemblyInfo, Shared의 ItemRules/Persistence/Sorting을 명시적으로 포함한다. |
| 설정/저장 | BepInEx 설정과 ServerSync, 캐릭터별 즐겨찾기 좌표·아이템 기억을 저장하는 `InventoryActions.ClientState.yml`, 저장 실패 재시도. 설정 키·기본값·live 이벤트·YAML 형식 유지. |
| 리소스 | English/Korean 번역과 ILRepack 목록을 포함한 최종 리소스 3개의 내용 해시가 기준 DLL과 동일하다. |
| 선택적 연동 | BepInDependency의 MUC, ExtraSlots, EAQS, AzuEPI, EpicLoot. Comfy 등은 슬롯/인벤토리 경계 정책과 참고 자료 범위에서 검토했다. 전체 외부 DLL 재분석은 하지 않았다. |
| 간접 호출 | Unity 메시지, Harmony 메서드, 선택적 public API reflection을 구분했다. AdventureTools 호환 코드가 이름으로 찾는 `CanStackForTopFirstMove`, `CanStackIntoTargetForTopFirstMove`, `MergeSortableStacks`는 유지했다. |
| 병합 | 부모 `environment.props`와 `build/ILRepack.targets`. 모드 + 고정 `Libs/ServerSync.dll` + YamlDotNet 16.3.0을 병합/internalize한다. 최종 DLL 이후 복사 타깃이 실행된다. |
| 배포 | `-c Debug -p:DeployToGame=true`로 최종 플러그인 DLL만 Steam `BepInEx/plugins`에 복사했다. Debug에서는 ZIP을 만들지 않는다. |
| Manifest | BepInEx 의존성이 이미 `denikson-BepInExPack_Valheim-5.4.2351`이다. 변경할 필요가 없었다. |

현 설치·컴파일 및 정적 검사 기준은 Windows x64 **Valheim 클라이언트 1.0.17 / Steam build 25730771**이다. 원본 경로는 `C:/Program Files (x86)/Steam/steamapps/common/Valheim/valheim_Data/Managed`, `assembly_valheim.dll` SHA-256은 `25A0A107DCE4D834C44C2B72D0EAFD5CB7793933BDA81816ACCFA1EA9543DACE`이다.

전역 인덱스의 준비된 전체 스냅샷은 1.0.16 client 25527674/server 25527701까지이며, 이번 설치 클라이언트와 동일한 서버 검증 자료로 취급하지 않았다. 기존 1.0.12→1.0.14 자료 중 입력·스택 정체성·접근 제한 관련 부분만 참고했다. 새 수집/전체 추출이나 지원 범위 확대는 하지 않았다. Actions에는 Slots의 FindFreeStackItem 패치가 없으므로 그쪽의 과거 버전 제한을 Actions에 옮기지 않았다.

기존 컴파일은 AssemblyPublicizer.MSBuild 0.4.2로 게임 참조 3개를 공개화한다. 이 구성을 일괄 변경하지 않았다. 분석과 계약 검사, 아래 격리 하네스는 **원본 게임 DLL**을 사용했다. 이번 종료 hook 대상 `ZNet.StopAll(bool)`은 원본에서 **private**임을 확인했다. 기존 Harmony postfix를 유지하며 새 직접 접근을 추가하지 않았다. 최종 DLL의 기존 비공개 참조 30개와 접근 특성 3개는 기준과 같고, 이것이 대상 게임 런타임에서 모두 검증됐다는 뜻은 아니다.

ServerSync는 기존 `valheim-1.0.7-r1` 기준본, SHA-256 `B4DD786997F4E90D770F09EF3E9D64154754FE7E8EDFB4841795751895B35846`을 유지했다. Jotunn 의존성 추가/제거 또는 공통 라이브러리 교체 없음.

## 영역별 판단

| 영역 | 구조 판단과 처리 |
| --- | --- |
| Plugin / ConfigAndUtility | 초기화·설정·선택적 연동이 다소 집중돼 있으나, 구독·해제·실패 정책을 함께 읽는 이점이 있다. 전면 분할은 보류. MUC 종료 상태 정리만 소유 파일로 모았다. |
| Actions.cs | 정렬·이동·후보 선별이 집중돼 있지만 실제 정책이 다르다. 서비스/인터페이스 추가 없이 검색 가능한 함수 경계를 유지했다. |
| UI / FeatureGuide / RuntimeModels | UI 파일의 집중은 존재하지만 원본 복원, 외부 모드 재배치, 표시 상태가 밀접하다. 가이드의 생성·측정·입력·파괴 소유권은 적절하다. 상태를 분류하기 위한 파일 이동은 하지 않았다. |
| ClientState / Favorites | 캐릭터별 snapshot과 재시도, UI 표시/입력의 경계가 적절하다. malformed 파일 보호와 전환·종료 시 저장을 유지했다. |
| Shared 저장 도우미 | 종료된 TXT 저장 경로와 이를 위한 범용 delegate 계층이 남아 있었다. 현재 텍스트 저장 경로로 축소했다. |
| 규칙 core / 저장 / Unity 편집기 / controller | 순수 정책과 Unity 입력·객체 수명주기의 분리는 검증 경계로 유용하다. 유지. |
| 소유권 handoff core / Unity·RPC 실행 | 상태기계와 외부 관측을 분리한 현재 구조가 적절하다. owner와 사용자 권한, grant/token/timeout 검사를 합치지 않았다. |
| MUC adapter / request gate | 외부 트랜잭션과 로컬 연속 작업은 수명주기가 다르므로 분리 유지. 종료 정리만 공동 배치했다. |
| 인벤토리 선택적 연동 | ExtraSlots UI 표시행, EAQS 표시/자동작업 경계, AzuEPI slot-index 경계와 별도 패널 설정은 서로 다른 정책이다. 하나의 행 API로 통합하지 않았다. |
| Harmony 패치 모음 | 대상/overload/우선순위를 명시적으로 읽을 수 있는 경계를 유지했다. 외형이 비슷한 prefix/postfix를 합치지 않았다. |

특히 Actions의 `includeArea=true`와 `AreaFavoriteRestock`은 실제 MUC/기본 게임 소유권 승인 경로에서 사용된다. 앞선 InventorySlots 구조 개선에서 삭제한 단일 상자 래퍼와 동일하게 취급할 수 없다. native 응답, 자체 lease, MUC의 취소·중복·timeout·환불 책임 역시 다르다.

## 단계 1 — 사용이 끝난 TXT 저장 경로 제거

커밋 **`e21dd37` — Simplify client state text persistence after YAML migration**

- 파일: `Shared/Persistence/LinkCompatibleFileWriter.cs`.
- `4f85ec8`에서 Actions의 TXT 저장과 Slots의 텍스트 저장을 위해 두 쓰기 형식과 delegate helper가 도입됐다. `117e2ae`에서 Actions가 통합 YAML 저장으로 전환하며 유일한 `WriteAllLines` 호출이 없어졌다.
- 현재 생산 호출자는 두 모드의 ClientState 저장이며 모두 `WriteAllText`를 사용한다. 삭제 대상은 internal 도우미로, 알려진 Unity/Harmony/직렬화/public API/reflection 진입점이 아니다.
- `WriteAllLines`와 `Action<string>` 중간 계층을 삭제하고 본문을 `WriteAllText`로 인라인했다. 새 클래스·인터페이스 없음.
- 임시파일→Move/Replace→허용된 링크 실패의 직접 쓰기→finally 정리 순서, 반환하는 fallback 원인과 기존 예외 정책을 유지했다.
- 효과는 과거 형식과 불필요한 호출·탐색 단계 제거다. 성능 향상을 측정하거나 FPS 개선으로 주장하지 않는다.
- 주요 위험은 저장 실패 시 데이터/재시도 상태 보존이다. 기존 일반 파일 교체·생성·실제 잠금·캐릭터 전환·플레이어 없이 종료 flush 검사를 수행했다.

## 단계 2 — MUC 세션 종료 상태 소유권 공동 배치

커밋 **`ab42032` — Centralize MultiUserChest session teardown state**

- 파일: `InventoryActions/MultiUserChestTransfer.cs`, `Plugin.cs`, 기존 CompatibilitySmoke 하네스 2개 파일.
- `f0215a9`에서 도입된 MUC의 pending/seed 상태가 네트워크 종료와 플러그인 종료에 중복 정리돼 있었다. `5239ecf`의 API 계약 기반 활성화와 별개인 수명주기 책임이다.
- `ClearMucTransferSession`을 MUC 소유 파일에 두어 두 종료점이 호출하게 했다. 일반 오류/취소는 계속 `CancelMucTransfer`를 호출한다.
- 일반 취소는 batch만 중지하고 pending·gate·미확정 즐겨찾기 기억 보호를 남긴다. 종료만 이전 세션의 참조를 해제한다.
- 네트워크 종료는 다음 월드를 위해 API 바인딩을 유지한다. OnDestroy만 helper 호출 뒤 API를 해제한다. 순서가 바뀐 구간은 필드 대입과 Dictionary.Clear뿐이며 외부 콜백/RPC/Unity 호출이 없다.
- 선언과 할당만 있던 private 상태 객체의 `MucTransferPending.Container`도 제거했다. 알려진 직접/문자열 reflection 사용, Unity 메시지와 직렬화 대상이 아니었다. 실제 대상 컨테이너/인벤토리 참조는 기존 전송 경로에 유지된다.
- 신규 `--muc-session` 검사는 실제 빌드 DLL의 Cancel, NetworkStopped postfix와 request gate를 실행한다. 정책을 테스트에 복제하거나 fake MUC 전송을 구현하지 않았다.
- 이익은 종료 상태가 추가될 때 두 파일의 정리 목록을 동시에 수정할 필요를 없애는 것이다. 지연 응답의 수량/환불/적용 정책과 동시 접근 허용 범위는 바꾸지 않았다.

## 프레임 경로와 유지한 부분

- `78801dd`의 컨테이너 배치 snapshot 비교가 이미 안정 프레임의 복원·재배치를 줄이고 있다. 외부 UI 모드의 버튼 교체/이름 변경 때문에 `EnsureActionButton`의 재탐색을 무조건 캐시로 대체하지 않았다.
- 가이드는 본문 갱신 0.25초, 폰트 탐색 실패 재시도 0.5초와 문자열/폰트/폭 기반 측정 캐시가 있다. 부모 변경 시 listener 해제·재생성도 유지했다.
- 즐겨찾기 셀 좌표는 다른 Harmony 패치 존재 시 native 변환을 사용해야 한다. 슬롯 경계 delegate의 live 값과 UI 표시행/자동작업 허용행 차이를 보존했다.
- 선택적 API/비공개 접근자는 초기화 시 확인해 저장하는 기존 경로를 유지했다. 새 프레임 캐시는 도입하지 않았다.
- `MergeSortableStacks`의 지역 group에 대한 추가 ToList는 제거 가능성이 있으나 실익이 작고 donor 제거/이벤트 검증 범위를 늘리므로 보류했다.
- `FilterUnsignedIntText`는 Shared ConfigDrawerLayout에서 호출된다. 단일 파일의 직접 참조 검색만으로 미사용 삭제하지 않았다.

## 검증 결과와 한계

| 검사 | 결과 |
| --- | --- |
| 기준 Actions Debug + Deploy | 성공, 0 warning/error. 기준 DLL과 plugins 사본 SHA-256 일치. |
| 변경 후 Actions / 공통 소비자 Slots Debug + Deploy | 모두 성공, 0 warning/error. 최종 병합 DLL과 plugins 사본 해시 일치. |
| InventorySlots.Tests | 기준·변경 후 183개 통과. MUC signed ID/중복/적용 중 취소/timeout·늦은 응답/부분 응답 포함. 기존 StuWard nullable 경고 2개는 변경 전부터 존재. |
| Actions RuleTests | 기준 144개 통과. 이번에 규칙 구현은 변경하지 않음. |
| InventoryActionsUiTests | 기준 164개 통과. 이번에 UI 배치 구현은 변경하지 않음. |
| ControllerTests Actions | 기준·저장 도우미 변경 후 565개 통과. 실제 파일 잠금/재시도/캐릭터 전환 포함. |
| ControllerTests Slots | 공통 저장 도우미 변경 후 690개 통과. source-linked fake host 검사이며 실제 입력 장치 검증 아님. |
| 원본 게임 계약 검사 | 기준·최종 모두 참조 665개, Harmony 대상 51개, 명시적 reflection 계약 7개, 실패 0, 기존 수동 검토 2개. |
| 최종 DLL 대조 | public signature 11개 항목, plugin 특성 10개, Harmony 특성 85개, BindConfigs IL 431개 명령, 리소스 3개와 참조 어셈블리 16개가 기준과 동일. |
| 신규 MUC 수명주기 하네스 | 변경 전후 Unity Editor Mono에서 10개 assertion 모두 완료. 다만 두 경우 모두 프로세스 exit 1이어서 정상 종료한 테스트로 집계하지 않음. |
| Git 검증 | 각 단계 diff 검토와 diff --check. 이번 코드 변경만 선택 staging/커밋. |

MUC 하네스는 `InventoryActions/build/CompatibilitySmoke/CompatibilitySmoke.csproj`를 Debug로 빌드한 뒤 다음 인자로 실행한다.

```
CompatibilitySmoke.exe <final InventoryActions.dll> <original Managed> <BepInEx core> --muc-session
```

이번에는 Unity Editor 6000.0.61f1의 Mono를 사용했다. Desktop .NET Framework 실행은 원본 Player 인터페이스의 기본 구현을 로드하지 못했고, .NET 9 우회 실행은 기존 Harmony 초기화 단계에서 실패했다. Mono에서는 10개 assertion 완료 후에도 기존 문서와 같은 exit 1이 남았다. 이 런타임 한계를 이번 구조 개선에서 수정하지 않았다.

**같은 날 후속 수정:** 위 내용은 구조 개선 당시의 기록이다. 별도 조사에서 기존 Editor 실행기가 x86이고 Harmony 초기화만으로 종료 접근 위반이 재현됨을 확인했다. [새 검사 실행기](../InventoryActions/build/CompatibilitySmoke/README.md)는 원본 Valheim x64 Mono에서 정리까지 수행하며, `muc-session` 10개와 `muc-restock` 22개 검사 모두 종료 코드 0을 확인했다. 의도적인 managed 실패도 종료 코드 1로 전달된다. 생산 모드 코드와 게임/라이브러리 DLL은 수정하지 않았으며, 실제 게임 실행의 미검증 범위는 그대로다.

원본 `ZNet.StopAll`을 실제 실행하거나 Harmony로 패치해 종료시킨 테스트가 아니다. postfix의 managed 상태 정리와 gate 동작을 독립 호출한 검사다. OnDestroy 전체는 UI/게임 의존성이 있으므로 코드 검토만 했다. 또한 심볼릭 링크 검사는 오류 분류만 검사했으며 실제 링크 fallback 쓰기 실행 검증은 아니다.

기존 수동 계약 검토 항목은 동적 controller Harmony 대상과 ServerSync.VersionCheck다. 원본 메타데이터 검사·빌드 성공은 기존 publicizer 접근 전략이나 전체 선택적 reflection 계약의 런타임 보장이 아니다.

실제 Unity 게임, UI 조작, 호스트/데디케이트/크로스플레이, MUC 전송·환불·두 사용자 경합·접속 해제 후 재접속, 실제 게임 저장과 아이템 수량 보존은 실행하지 않았다. 다음 게임 검증에서는 일반 취소 후 늦은 응답, StopAll 후 새 월드 진입, 빈 즐겨찾기 복원 중 종료를 우선 확인해야 한다.

최종 Debug DLL:

- Actions: `InventoryActions/bin/Debug/InventoryActions.dll`
  - SHA-256 `FFB42554CAB77FC4831F5BA2BDA1E7437B9A8C59454B589E98B2C1E02BC6098A`
- 공통 도우미 소비자 Slots: `bin/Debug/InventorySlots.dll`
  - SHA-256 `E63E2EF6715ABDCD3D7C3ABFD1CB7ED3FAE1090C53D9D21335581FB43362B6C6`

각각 `C:/Program Files (x86)/Steam/steamapps/common/Valheim/BepInEx/plugins`의 동명 파일과 일치한다. 실행 중 게임이 새 DLL을 로드했다고 주장하지 않는다.

## 별도 검토 후보

이번 기능 보존 리팩터링에는 섞지 않았다.

- `Actions.GetItemSortCategory`의 문자열 `Trophie`: 기본 게임의 Trophy와 달라 의도한 정렬 우선순위를 받지 못할 후보다. 정렬 결과를 바꾸는 버그 수정으로 따로 다뤄야 한다.
- `HasContainerPlayerAccessForRpc`의 ward 허가와 로컬 PrivateArea.CheckAccess 경로가 다르다. STUWard가 로컬에서 허용하는 그룹이 원격 승인에서 거절될 가능성이 있으나 실제 재현은 하지 않았다. 권한 연동의 별도 정책 검토가 필요하다.
- EAQS/AzuEPI의 슬롯 경계 API 자체가 예외를 던지면 일반 인벤토리 높이로 돌아가는 실패 정책이 있다. 숨은 특수행을 자동작업에서 어떻게 보호할지는 별도 검토 대상이다. AzuEPI의 별도 패널 설정 조회만 실패한 경우 슬롯 보호 delegate를 유지하는 기존 경계는 그대로다.
- Restock 설정의 익명 이벤트 구독, 한 번 생성해 재사용하는 trash Sprite/texture의 종료 정리는 플러그인 종료 후 Harmony 유지 정책과 함께 검토해야 한다. 반복 누수나 실제 고장을 확인한 것은 아니다.
- 저장 도우미 finally의 임시파일 삭제 예외가 앞선 결과를 가릴 수 있는 기존 정책은 유지했다. 오류 보고 동작을 바꾸는 별도 수정 대상이다.

## 커밋과 최종 작업 트리

- `e21dd37`: 공통 텍스트 저장 도우미 축소.
- `ab42032`: MUC 종료 상태 공동 배치와 실제 DLL 수명주기 검사.
- 이 문서는 별도 문서 커밋으로 남긴다.

코드 커밋 후 기존 dirty 파일 80개가 그대로 남았다. 79개는 바이트 단위로 동일하며, `InventoryActions/Plugin.cs`는 이번 커밋의 종료 정리만 반영됐다. 그 파일에 이미 있던 sort 이벤트 해제 변경은 staging에서 제외했다. 남은 전체 tracked diff의 추가/삭제 줄이 작업 전 기록과 정확히 일치함을 확인했다. 인덱스는 비어 있다. 기존 변경을 일괄 커밋하거나 이동·폐기하지 않았다.
