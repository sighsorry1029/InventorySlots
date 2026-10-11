# InventorySlots 구조 검토 — 2026-10-11

## 기준과 검토 범위

- 시작 브랜치/커밋: 실제 `main`, `ac196938477ef11dae575a4d04a99f9494ce46c5` (InventorySlots 1.6.0).
- 시작부터 추적 파일 62개와 미추적 파일 18개에 기존 작업이 있었다. 설정 섹션 재배치, UI 위치·닫기 수명주기, 단일 재료 퀵스택, InventoryActions 변경과 진단 도구 등이 포함된다. 이번 리팩터링의 결과로 집계하거나 커밋하지 않았다.
- 전역 `C:/Users/blizz/.codex/AGENTS.md`, Valheim `INDEX.md`, 관련 버전 가이드, 프로젝트 빌드 설정과 문서를 확인했다. 프로젝트 및 상위 작업 폴더에는 추가 AGENTS.md가 없었다.
- [9월 23일 구조 리뷰](StructureReview-2026-09-23.md), [9월 12일 리뷰](RefactoringReview-2026-09-12.md), [공동 상자 검토](SharedContainerReview.md), [UI 성능 기록](InventoryUiPerformance.md), [1.0.15 대응 기록](Valheim-1.0.15-Compatibility.md)은 조사 단서로 사용하고 현재 호출 관계와 대조했다.
- 읽기 전용 에이전트가 장비/저장, 제작/툴팁/채팅, 컨테이너/권한을 분담했다. 편집·빌드·스테이징·커밋은 한 세션에서 처리했다. 브랜치나 worktree를 만들지 않았다.

검토는 주요 상태 소유권·수명주기·반복 경로와 아래 변경의 계약에 집중했다. 모든 소스 줄, 모든 외부 모드 조합 또는 모든 게임 기능의 완전한 감사는 아니다.

제외 범위: 생성물 `bin`/`obj`, 기존 ZIP, publicizer 생성 코드, 외부 ServerSync/YamlDotNet 내부, `refer`의 전체 외부 DLL, InventoryActions 독립 구현 변경, 전체 리소스 재추출, 진단 도구 구현 변경. Harmony·Unity 메시지·알려진 reflection/API 호출은 확인했지만 외부 모드의 임의 private reflection까지 부재를 증명하지는 않았다.

## 실제 빌드와 외부 계약

| 항목 | 확인 내용 |
| --- | --- |
| 빌드 대상 | `InventorySlots.csproj`, net48. 솔루션의 다른 모드까지 빌드하지 않고 이 프로젝트를 지정했다. |
| 진입점 | `InventorySlotsPlugin : BaseUnityPlugin`. 프리로더 패처가 아닌 일반 BepInEx 플러그인이다. Awake/Update/OnDisable/OnDestroy는 `PluginLifecycle.cs`에 있다. |
| 컴파일 소스 | 루트 C#, Shared/ItemRules·Persistence·Sorting, Localizer 및 AssemblyInfo를 명시적으로 포함한다. 테스트용 Roslyn은 모드에 포함되지 않는다. |
| 리소스 | 기본 슬롯 YAML, ResourceMap YAML, 영어·한국어 번역 4개와 병합 메타데이터 `ILRepack.List`: 최종 DLL에 5개. |
| 병합 | ILRepack 2.0.44.1이 모드 DLL, `Libs/ServerSync.dll`, YamlDotNet 16.3.0을 병합/internalize한다. 최종 DLL에 별도 ServerSync/YamlDotNet AssemblyReference가 없다. |
| ServerSync | 기존 고정 vendor `valheim-1.0.7-r1`, SHA-256 `B4DD786997F4E90D770F09EF3E9D64154754FE7E8EDFB4841795751895B35846` 유지. 공통 라이브러리 업데이트 작업을 추가하지 않았다. |
| 로컬 배포 | `DeployToGame=true` 타깃이 병합 완료 후 최종 DLL만 Steam Valheim의 `BepInEx/plugins`로 복사한다. |
| 버전/의존성 | 모드 1.6.0 유지. manifest의 BepInEx 의존성은 이미 `denikson-BepInExPack_Valheim-5.4.2351`이다. 런타임 의존성 변경 없음. |
| 설정/저장 | 설정 키·기본값·live 이벤트, YAML, ClientState, 아이템 custom data, RPC 식별자/직렬화 형식 변경 없음. 앞선 미커밋 설정 변경을 그대로 보존했다. |
| 공개 API | `InventorySlotsApi`의 metadata 등록 2개 overload, 커스텀 장비 조회, 시각 루트 조회 시그니처 유지. 내부 partial 파일 이동은 선언 타입을 바꾸지 않는다. |

현재 설치 원본과 실제 컴파일 참조는 **Windows 클라이언트 Valheim 1.0.17 / Steam build 25730771**이다. `assembly_valheim.dll` SHA-256은 `25A0A107DCE4D834C44C2B72D0EAFD5CB7793933BDA81816ACCFA1EA9543DACE`다. 기존 문서의 1.0.15 설명을 현재 게임 버전으로 오인하지 않았다. 전역 자료의 최신 완전한 클라이언트/서버 스냅샷은 1.0.16이므로 이를 1.0.17 서버 검증으로 간주하지 않았다. 지원 범위를 넓히거나 구 버전 fallback을 추가하지 않았다.

원본 분석/계약 검사는 `C:/Program Files (x86)/Steam/steamapps/common/Valheim/valheim_Data/Managed`를 사용했다. 기존 준비 자료와 필요한 Container 원본 분석만 참고했다. 원본을 publicize하거나 게임 자료를 전부 다시 수집하지 않았다.

빌드의 기존 AssemblyPublicizer.MSBuild 0.4.2 구성은 유지했다. 원본 Container의 `m_nview`, `m_inUse`, `m_lastRevision`, CheckAccess/CheckForChanges/Load/Save와 관련 RPC는 private이다. 이번 변경은 새 게임 멤버 접근을 만들지 않는다. 컴파일 성공·기존 접근 검사 특성의 존재와 실제 Mono 런타임 접근 가능성은 별개다.

선택적 의존성은 장비/제작/툴팁/권한별 경계를 유지했다. EpicLoot, MagicSupremacy, AdventureBackpacks 및 다른 백팩, HipLantern, CircletExtended, Jewelcrafting, VNEI, RecycleNReclaim, AzuCraftyBoxes, STUWard, 외부 MultiUserChest의 서로 다른 정책을 통합하지 않았다. 하드 비호환 인벤토리 모드 선언도 변경하지 않았다.

## 영역별 구조 판정

| 영역 | 판정 | 근거와 조치 |
| --- | --- | --- |
| 커스텀 장비 조회/투영 캐시 | 일부 과도한 분리 | 일반 조회가 BackpackCompat에 있고 MagicSupremacy/HipLantern도 이를 사용하며 API가 같은 검색을 다시 구현했다. 캐시 소유 파일로 모아 검색을 한 경로로 만들었다. |
| 장착·복구·무결성 감사 | 집중도가 높지만 분리 유지 | 자동 장착, 직접 이동, 사망/로드 복구는 롤백·보존 조건이 다르다. 일반 장착 엔진으로 합치지 않았다. |
| 설정/YAML/ClientState | 대체로 적절 | 저장/정규화/런타임 적용이 다른 실패·수명주기를 가진다. 새 migration이나 저장 추상화를 추가하지 않았다. |
| 단일 상자 실행 래퍼 | 과도한 추상화 | 모든 호출이 단일 상자인데 목록·generic core·여러 delegate·죽은 범위 FX 분기가 남아 있었다. 실제 이동 호출과 알림을 호출자에 배치하고 래퍼/core를 삭제했다. |
| 범위 소유권/공동 상자/권한 | 적절한 분리 | RPC·Unity 상태와 승인 상태기계, viewer/replay, native 응답 fence는 책임이 다르다. owner와 사용자 권한 확인을 통합하거나 완화하지 않았다. |
| QuickStack/Restock/TakeAll | 정책별 분리 적절 | 후보, 즐겨찾기, 목표 수량, leave-one, 빈 슬롯 처리가 다르다. 외형이 비슷한 이동 루프를 합치지 않았다. |
| 인벤토리 UI/HUD/입력 | 조정 코드의 집중도가 높으나 현상 유지 | 패널 배치·입력·닫기 애니메이션의 연결은 강하지만 파일 크기만으로 분리할 근거가 부족하다. 진행 중인 UI 변경과 섞지 않았다. |
| 제작 어댑터/정제/캐시 | 대체로 적절 | Vanilla/VNEI/Jewelcrafting 정책과 stamp 무효화 조건이 다르다. 하나의 범용 UI 캐시로 합치지 않았다. |
| 인벤토리/제작/채팅 툴팁 | 분리 유지 | 아이템 참조, 레시피 인덱스, 수신 snapshot은 서로 다른 갱신 기준이다. 입력 lease와 RPC nonce/TTL/session generation도 별도 책임이다. |
| 선택적 모드 연동 | 대체로 균형 | 공통 접근 도구는 공유하되 어댑터의 API 탐색·이벤트 해제·fallback을 각 모드가 소유하도록 유지했다. |

## 구현과 커밋

### 7213e76 — 장비 조회와 캐시 공동 배치

- `BackpackCompat.FindCustomEquippedItem`과 `EquipmentProjection.TryGetCustomEquippedItemForApi`를 `CustomEquipmentProjectionCache.cs`에 배치하고, API 연결부가 공통 검색에 위임하게 했다.
- 공개 API, 첫 일치 순서, null player/predicate, 실패 시 out=null, predicate 예외 전달을 보존했다. 기존 null item guard도 유지했다. 캐시는 null item을 넣지 않는다.
- snapshot이나 인덱스 열거로 바꾸지 않았다. 콜백이 캐시를 재구축할 때의 기존 열거 예외/즉시 반환 동작도 유지한다.
- `GetArmor`를 호출하지 않는 조회 경계, 캐시 무효화·player/inventory 전환·실패 후 초기화는 그대로다.
- 근거: `1e67f20`부터 공존하던 조회, `1d7f47b`의 캐시 확장, `8786872`의 AdventureBackpacks API 전환 뒤에도 일반 조회가 백팩 파일에 남았다. `ac19693`의 메뉴/접속 실패 시 방어력 지연 계산 수정을 회귀 기준으로 삼았다.
- 효과: 호환 어댑터·API에서 같은 조회를 찾기 위해 세 파일을 오갈 필요가 줄어든다. LINQ closure 제거는 예상 할당 감소이며 FPS 개선을 측정한 것은 아니다.
- 위험/검증: null·소유자 필터·첫 일치·예외·live invalidation 및 armor provider 미호출을 기존 실제 소스 링크 하네스에서 검증했다.

### 93ee834 — 사용되지 않는 범위 실행 추상화 제거

- `QuickStackIntoContainers`를 단일 상자 함수로 정리하고 사용되지 않는 includeArea를 제거했다. `RestockFromContainer`도 유일한 호출 정책인 CurrentContainerMatchingStacks를 명시한다.
- `ContainerActions.RunContainerTransferAcrossContainers` 및 production 호출이 그것뿐이었던 `ContainerTransferCore`를 삭제했다. 실제 범위 후보를 수집하는 GetActionContainers는 유지했다.
- 후보 선택/내림차순 정렬 → drag 해제 → player/container/inventory 재검사 → 실제 이동 → 하위 이동 함수의 container Changed → 양수 이동 시 player Changed → 제작 재료 캐시 무효화 → 결과 표시 순서를 보존했다.
- 예외를 새로 삼키거나 finally에서 알림을 추가하지 않았다. 기존 단일 상자 호출은 includeArea=false여서 FX를 내지 않았고 이 동작도 유지했다.
- 근거: `21bda73`이 범위 작업을 별도 승인 경로로 이전했다. 현재 QuickStack 두 호출과 Restock 한 호출은 모두 단일 상자다. `ef64fec`에는 InventoryActions에서 같은 종류의 래퍼를 제거한 선례가 있다.
- `ab0f9ec`의 공동 GUI replay, `dbf0fff`의 STUWard requester 권한 경계는 유지했다. 범위 승인/소유권 handoff/중복·지연 응답/FX 코드 자체는 변경하지 않았다.
- 효과: 단일 상자에서 불필요한 목록과 callback 연결을 만들지 않으며 실행/알림 순서를 해당 함수에서 바로 읽을 수 있다. 실제 프레임 시간은 측정하지 않았다.
- 위험/검증: 실제 두 진입 메서드를 Roslyn으로 추출 실행하는 하네스에서 0/부분 이동, drag 후 무효화, GUI 없음, 이동/저장 알림/캐시 callback 예외의 18개 시나리오를 검사했다. 실제 이동·저장·Unity는 test double 경계이며 결과값과 콜백 순서를 검증한다. 삭제된 generic 래퍼만 검증하던 두 검사와 fake 타입은 제거했다.

## 반복 경로와 유지한 후보

- Plugin Update는 YAML/공동 상자 설정 적용 뒤 dedicated server에서 UI 경로를 중단한다. 로딩 중 처리, 프레임 입력, 주기적 light/heavy audit의 경계를 유지했다.
- 장비 캐시는 player·inventory·player ID·version을 확인한다. 방어력 계산의 `ToArray`는 외부 provider 재진입 중 목록 변경을 견디기 위한 snapshot이므로 할당만 보고 제거하지 않았다.
- 장비/퀵 패널 목록은 이미 slot-definition version, 해금 signature/count로 재사용한다. 반환형이 List라는 이유만으로 매 프레임 할당한다고 판단하지 않았다.
- 제작 fast path는 화면·선택·가용 재료·검색·핀 상태 stamp와 dirty 조건을 확인하며, private 선택 variant 접근자는 static으로 캐시한다. 외부 제작 탭이나 스크롤/큐가 있을 때 full path로 돌아가는 정책을 유지했다.
- 채팅 UI는 차단/초점/핀 유무로 조기 종료한다. UI 루트는 부모가 바뀌거나 root가 없을 때 생성하고, 개별 hover·고정 패널은 필요할 때 생성·재사용한다. 입력·hover lease와 snapshot 수명주기 전체를 새 캐시로 덮지 않았다.
- `CraftingTooltipRecipeRows`의 제작대 판정은 signature와 표시부에 중복되어 있고 `78801dd`에서도 함께 바뀌었다. 또한 확보한 slotRect를 Configure 함수가 다시 찾는 부분이 있다. 후속 후보로 남겼다. 이번에는 원본 게임/외부 UI callback을 포함한 표시 계약 검증 범위를 추가하지 않고, 이미 검증 근거가 충분한 장비 조회·단일 상자 흐름에 한정했다.
- 새 production 캐시, 파일, 인터페이스, 서비스 계층은 추가하지 않았다.

## 검증 결과

| 검사 | 기준 → 변경 후 |
| --- | --- |
| Debug + DeployToGame | 기준 성공. 각 변경 후 성공, 최종 0 warning / 0 error. |
| InventorySlots.Tests | 184 → 183 통과. wrapper 전용 2개 삭제, 실제 entry의 18개 시나리오를 실행하는 등록 검사 1개 추가. 기존 client-state 검사는 보존했다. |
| EquipmentArmorTests | 75 → 90 assertion 통과. 실제 production 캐시/조회 코드를 링크하고 게임 경계는 double로 실행한다. |
| NativeContainerTests (InventorySlots) | 기준 143개 통과. 해당 native fence 소스는 이번 변경에서 그대로 유지했고, 변경 후 주 하네스의 area/shared handoff 검사도 통과했다. |
| CraftingFrameTests | 기준 entry 14, closing 22, frame routing 164개 통과. 해당 제작/닫기 코드는 수정하지 않았다. |
| 원본 DLL 정적 검사 | 전후 모두 게임 참조 1,148, 명시적 Harmony 대상 162, reflection 계약 51, 실패 0, 수동 확인 10. 보고서의 계약 목록·기존 비공개 접근 수·접근 특성이 동일하다. |
| Git diff | 변경별 diff/스테이징 검토와 whitespace 검사 통과. 읽기 전용 독립 리뷰에서 차단할 회귀를 발견하지 못했다. |

Debug 병합에서 Mono.Cecil NativePdbWriter.Close의 `0xC0000005`가 한 차례 발생했다. 실패 산출물을 별도로 복사하지 않았고, `dotnet build InventorySlots.csproj -c Debug -p:DeployToGame=true --no-incremental` 재실행으로 병합/배포를 완료했다. 빌드 도구나 심볼 설정을 변경하지 않았다. 주 테스트 재컴파일에서 기존 StuWardCompat의 nullable 경고 2개가 관찰되었으며 최종 모드 빌드에는 경고가 없다.

최종 DLL: `bin/Debug/InventorySlots.dll`. 배포 대상: `C:/Program Files (x86)/Steam/steamapps/common/Valheim/BepInEx/plugins/InventorySlots.dll`. 양쪽 SHA-256은 `D64E6605C509F4CF3F9606C9B541EB6A0AF685FBEAA0B5936FDF6BDF05426A15`로 일치했다.

정적 검사는 메타데이터 해석 검사이지 실제 Harmony 패치 실행이나 Mono 접근 검사 통과를 뜻하지 않는다. 게임·호스트·데디케이트 서버·크로스플레이를 실행하지 않았다. 실제로 남은 확인은 장비/외부 효과 조회, 메뉴 캐릭터·접속 실패 후 복구, 단일 상자 Stack/Take stacks의 0/부분 이동과 저장 후 재접속, 범위 작업의 다중 사용자 소유권 전환·접속 해제다. 측정된 FPS 개선은 주장하지 않는다.

## 별도로 남긴 결함·정책 후보

1. `TooltipOwnership`: 파괴된 tooltip/child CanvasGroup은 복원 루프에서 제외되어 static snapshot 사전에 남을 수 있다. 전체 정리 경로를 찾지 못했다. 실제 누적 규모·게임 영향은 미측정이며 수명주기 결함 수정으로 별도 검토해야 한다.
2. `ContainerAccessPolicy.CanProcessContainerSortRpc`: sender↔player, owner, CheckAccess는 확인하지만 area/shared 승인과 동일한 거리·ward 검사는 없다. 외부 MUC 원격 정렬의 권한 계약 검토 후보다. 실제 악용을 재현한 것은 아니며 이번에 권한 정책을 변경하지 않았다.
3. `LinkCompatibleFileWriter`: 임시 파일 정리의 File.Delete 예외가 원래 저장 실패를 가리거나 저장 후 실패로 보이게 할 수 있다. 9월 리뷰의 지적과 현재 코드를 대조했다. 저장 실패 정책 수정은 이번 구조 개선에서 제외했다.

## 기존 작업 보존과 최종 범위

시작 시 diff와 80개 파일 해시를 임시 감사 폴더에 보관했다. 기존 파일 78개는 바이트 단위로 동일했다. 이번과 겹친 `ContainerQuickStack.cs`, `InventorySlots.Tests/Program.cs`는 새 hunk만 선택적으로 스테이징했다. 두 커밋 후 남은 추적 diff의 추가/삭제 내용이 시작 diff와 정확히 같은지 비교했다(이동한 줄 번호·hunk 문맥 제외).

따라서 구현 후에도 기존 추적 변경 62개와 미추적 파일 18개는 남아 있다. 테스트/Debug DLL은 이 기존 작업을 포함한 현재 checkout을 검증한 결과이며, 기존 변경까지 커밋했다는 뜻이 아니다. 이번 구현 커밋은 위 두 개이며 이 문서는 별도 검토 기록 커밋으로 남긴다. 버전 변경, Release 빌드/ZIP, push, 업로더 조작이나 게시 확인은 수행하지 않았다.
