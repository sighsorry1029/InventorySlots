# InventorySlots

올인원 인벤토리 개편 모드임. 점진적 인벤토리 행, 커스텀 장비/퀵 슬롯, 다중 제작, 사망 시 유지 규칙, 즐겨찾기, 스크롤 가능한 툴팁, 제작 그리드/검색/정렬, 컨테이너 스택/보충/정렬, 아이템 비교, 컨트롤러 지원을 제공함. EpicLoot와 Jewelcrafting 호환도 지원됨.

## 기능 미리보기

### 안정적인 인벤토리 성장

![](https://i.ibb.co/QFFMsQgx/progressiveslotfinal.gif)

점진적 인벤토리 행과 퀵 슬롯은 시간이 지나며 해금됨. 저장된 아이템 위치를 뒤섞지 않음. 클라이언트에서 현재 보이는 인벤토리 행을 펼치거나 접을 수 있음. 장비 슬롯은 `InventorySlots/InventorySlots.yml`에서 정의됨.

![](https://i.ibb.co/B2RrkCH1/hotbarswitch.gif)

핫바 전환 키로 현재 표시되는 핫바 행을 순환할 수 있음. 내부 아이템 좌표를 옮기지 않으므로 인벤토리가 커져도 핫바를 실용적으로 사용할 수 있음.

`InventorySlots/InventorySlots.yml`의 `QuickSlots` 규칙에 맞는 새 스택은 자동 배치 시 핫바를 먼저 사용하고, 맞지 않는 아이템은 2~n행을 먼저 사용함. 기존 부분 스택 합치기와 직접 드래그 위치는 바뀌지 않음.

![](https://i.ibb.co/5XMjS4Cw/keyhint.png)

키 힌트에는 현재 설정된 즐겨찾기와 툴팁 단축키가 표시됨. 클라이언트 설정에 따라 마우스와 컨트롤러 입력도 반영됨.

### 제작 브라우저

![](https://i.ibb.co/pBJJ84sJ/craftingpanellook.png)

제작대가 아이콘 그리드 기반 브라우저로 재구성됨. 검색, 그룹 필터, 즐겨찾기, 정렬 버튼, 페이지 스크롤, hover/고정 툴팁을 지원함.

![](https://i.ibb.co/0yP8R3vf/gridsizefavorite.gif)

레시피를 즐겨찾기로 표시하고, 마우스 휠 줌으로 제작 그리드 크기를 조절할 수 있음.

![](https://i.ibb.co/Y7586d2w/sortingandmulticraft.gif)

레시피를 그룹과 자원 티어 기준으로 정렬하고, 같은 제작 흐름 안에서 여러 개를 제작할 수 있음.

![](https://i.ibb.co/bMjbRm73/recipeandgrid.gif)

제작대에서 그리드 크기를 바꾸거나 레시피를 탐색하는 동안에도 hover 툴팁과 고정 툴팁을 계속 사용할 수 있음.

![](https://i.ibb.co/tPDH0MPc/upgradebenefit.png)

업그레이드 화면에서 장비를 강화했을 때 증가하는 능력치를 확인할 수 있음. 업그레이드 탭의 즐겨찾기는 제작 탭의 즐겨찾기와 별도로 관리됨.

### 스크롤 가능한 툴팁과 비교

![](https://i.ibb.co/NdVdTFdk/jeweltooltiptest.gif)

InventorySlots 툴팁은 스크롤 가능한 패널로 확장될 수 있음. 여러 개의 고정 비교 슬롯도 지원됨.

![](https://i.ibb.co/JWzSGMcd/favoritecomparepotions.gif)

![](https://i.ibb.co/j9bNG7Ft/comparemeal.gif)

최대 세 개의 툴팁을 고정해 레시피, 음식, 포션, 장비를 비교할 수 있음. 현재 hover 중인 대상도 잃지 않음.

![](https://i.ibb.co/3Dv1Ct1/comparegears.png)

![](https://i.ibb.co/LXcfNxtG/comparemeals.png)

장비와 음식 비교도 같은 고정 툴팁 시스템을 사용함.

![](https://i.ibb.co/kVJ3fjJ2/Tooltipalpha.gif)

인벤토리/컨테이너 hover 툴팁과 제작 hover 툴팁의 배경 투명도는 클라이언트에서 설정할 수 있음.

### 컨테이너 도구

![](https://i.ibb.co/xtpGM34P/quickstackchest.png)

컨테이너에 hover하면 범위 퀵 스택과 범위 보충을 위한 홀드 액션이 표시됨. 범위는 상호작용한 컨테이너를 중심으로 계산됨.

![](https://i.ibb.co/rJYRL18/quickstack.gif)

`E`를 길게 누르면 즐겨찾기되지 않은 플레이어 아이템 중 일치하는 아이템을 hover한 컨테이너와 주변의 유효한 컨테이너에 빠르게 넣음.

즐겨찾기는 아이템 종류가 아니라 인벤토리 칸 자체에 적용됨. 즐겨찾기된 칸은 퀵 스택 대상에서 제외되며, 동시에 보충 대상 칸으로 등록됨.

![](https://i.ibb.co/kgqHWzbk/restock.gif)

기본 설정에서는 `Alt+E`를 길게 눌러 hover한 컨테이너와 주변의 유효한 컨테이너에서 즐겨찾기된 인벤토리 스택을 보충할 수 있음.

`Enable Multi User Chest`가 On이면 여러 플레이어가 일반 설치 상자의 창을 함께 열 수 있음. 직접 이동·스택/보충·모두 빼기·정렬은 현재 소유자의 승인과 최신 내용 도착을 기다린 뒤 실행됨. 대기 중 슬롯 내용이 달라지면 다른 아이템을 옮기지 않고 해당 클릭을 취소함. 범위 작업은 다른 사람이 보고 있는 공유 상자도 포함하여 하나씩 처리하며, 거절·시간 초과된 대상에서는 아이템을 이동하지 않음.

설정 기본값은 On이고 기존에 저장한 On/Off 값은 유지됨. Off이면 바닐라의 단독 열기와 사용 중이지 않은 상자의 승인식 범위 이동을 사용함. 설정을 바꾸면 인벤토리를 닫고 대기 중 작업을 취소한 뒤 다시 열어야 함. 이전 내장 구현의 원격 아이템 전송과 임시 보관·처리 기록은 사용하지 않으며 상자에 남은 기록도 삭제하지 않음. 외부 MultiUserChest 모드가 있으면 해당 모드가 우선하며 범위 작업에서 자신이 소유하지 않은 상자를 제외함.

보호석·상자 접근 권한과 특수 컨테이너의 기존 정책은 유지됨. 이 기능은 다른 모드가 직접 수정하는 인벤토리나 게임 강제 종료 시 서로 다른 캐릭터/월드 저장을 하나의 원자적 저장으로 만들지는 않음. 실제 데디케이트 서버 동시 조작은 추가 확인이 필요하며 검토·검증 범위는 `docs/SharedContainerReview.md`에 정리됨.

DLL을 교체하기 전에 진행 중인 아이템 이동을 마치고 게임을 정상 종료해야 함. 범위 작업의 소유권 요청 구현이 일치하도록 서버와 모든 클라이언트를 같은 빌드로 교체한 뒤 다시 시작해야 함.

스택 가져오기는 즐겨찾기되지 않았고, 플레이어 인벤토리와 컨테이너에 서로 일치하는 stackable 아이템만 가져옴.

![](https://i.ibb.co/yFQWpxjF/restocklimit.png)

클라이언트별 보충 제한을 설정하면 prefab별로 즐겨찾기 보충 목표 수량을 제한할 수 있음. 예를 들어 `Stone: 10` 또는 `Coins: 500`처럼 설정할 수 있음.

### 모드 호환 예시

![](https://i.ibb.co/JWPfnMWn/epiclootcompatible.png)

EpicLoot 아이템은 툴팁 정보를 유지함. `InventorySlots/InventorySlots.yml`에 설정하면 InventorySlots의 장비/커스텀 슬롯 라우팅을 사용할 수 있음.

![](https://i.ibb.co/4ZD7PW47/upgradeepicloot.png)

EpicLoot 장비도 업그레이드 화면에서 InventorySlots의 고정 툴팁으로 비교할 수 있음.

![](https://i.ibb.co/ymQvwRzd/comparejewel.png)

Jewelcrafting의 소켓과 보석 툴팁 내용은 InventorySlots의 툴팁과 비교 흐름에서 지원됨.

AzuCraftyBoxes의 선택적 API를 통해 주변 제작 재료 수량과 표시 색상을 반영함. 실제 재료 소비는 AzuCraftyBoxes가 처리하며, 이 표시 연동이 여러 플레이어의 동시 제작·건축을 중재하지는 않음.

## 구성 파일

`BepInEx/config` 아래에서 다음 파일을 사용함.

- `sighsorry.InventorySlots.cfg`: config 루트에 유지되는 BepInEx 설정 파일임. Configuration Manager에 표시되는 옵션을 저장함.
- `InventorySlots/InventorySlots.yml`: 서버 권한 설정임. `Slots`, `Groups`, `InventoryLimits`, `QuickSlots`, `KeepOnDeath`를 저장함.
- `InventorySlots/ResourceMap.yml`: 서버 권한 설정임. 자원 정렬 티어를 저장함.
- `InventorySlots/ClientState.yml`: 로컬 UI 상태를 자동으로 저장하는 파일임. 서버 설정으로 배포하지 않아도 됨.

`InventorySlots/ResourceMap.yml`은 티어 이름과 재료 목록을 직접 연결함. 위에서 아래 순서로 티어가 정해지며, 같은 재료가 여러 번 나오면 처음 나온 티어가 적용됨.

기본 맵은 Meadows부터 DeepNorth까지 정렬 티어를 제공함. 제작 비용이나 해금 조건은 바꾸지 않음. Writhan 재료는 Swamp, Hook은 Mistlands, 용암 Blob 트로피는 AshLands에 배치하며, Fader 처치 보상은 다음 지역 진입 재료로 보아 DeepNorth에 배치함.

모드를 업데이트해도 기존 `ResourceMap.yml`은 덮어쓰지 않음. 파일을 백업한 뒤 저장소의 `config/InventorySlots/ResourceMap.yml`에서 누락 항목만 해당 섹션에 병합하고 사용자 항목과 티어 순서를 유지해야 함. `DeepNorth`는 `AshLands` 뒤에 추가하며 섹션을 알파벳순으로 정렬하면 안 됨. 멀티플레이에서는 서버의 맵이 기준임. YAML 수정은 실행 중 다시 읽고 동기화하도록 되어 있으며, 제작 목록은 갱신 대상으로 표시되고 인벤토리·상자는 다음 정렬부터 새 티어를 사용함. 잘못된 YAML은 마지막 정상 맵을 유지함.

```yaml
Meadows:
  - Wood
  - Stone
BlackForest:
  - HardAntler
  - Bronze
```

이번 버전은 config 루트의 기존 `InventorySlots.yml`, `InventorySlots.Client.yml`, 또는 `InventorySlots.yml` 내부의 `resourceMap`을 읽거나 이전하지 않음. 기존 파일은 삭제하지 않고 그대로 둠. 커스텀 설정을 새 파일에 수동으로 다시 적용하고 서버와 모든 클라이언트를 함께 업데이트해야 함.
