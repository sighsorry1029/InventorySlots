# Jewelcrafting Socket Cost Layout Analysis

작성일: 2026-05-16

## Scope

이 문서는 Jewelcrafting의 `Socket Cost` 설정 4가지가 실제 소켓 추가 흐름에서 어떻게 동작하는지 분석하고, InventorySlots가 Jewelcrafting table의 `Socket` 탭을 재배치할 때 어떤 요소를 어디에 보여주는 것이 좋은지 정리한다.

분석 기준 코드:

- `reference/Jewelcrafting-src/Jewelcrafting/Jewelcrafting.cs`
- `reference/Jewelcrafting-src/Jewelcrafting/GemStones.cs`
- `reference/Jewelcrafting-src/Jewelcrafting/Socketing.cs`
- `reference/Jewelcrafting-src/Jewelcrafting/Setup/Jewelcrafting.SocketCosts.yml`
- `CraftingBottomControls.cs`
- `CraftingGridInteraction.cs`
- `CraftingRecipeActions.cs`
- `CraftingRecipeView.cs`
- `JewelcraftingCraftingSocketUiCompatAdapter.cs`

## 핵심 결론

Jewelcrafting의 `Socket Cost`는 단순히 "비용을 보여줄지 말지"가 아니라 다음 세 가지를 동시에 바꾼다.

- 소켓 시도 전에 재료를 요구하는지
- 실패했을 때 아이템이 파괴되는지
- 재료가 시도 시점에 소비되는지, 성공 시점에만 소비되는지

따라서 InventorySlots의 socket tab은 4가지 모드를 같은 레이아웃으로 처리하면 안 된다. 특히 `BreakOrCost`는 UI상 재료가 필요하지만 실제 소비는 성공 후에만 일어나므로, `CostsItems`나 `BreakAndCost`와 같은 "시도 비용"으로 표시하면 플레이어가 손실 리스크를 잘못 이해할 수 있다.

권장 방향은 다음과 같다.

- socket tab의 왼쪽 grid는 모든 소켓 추가 대상 아이템을 보여준다.
- grid의 주황/활성 배경은 "지금 Socket 버튼을 눌러 시도 가능한 상태"를 기준으로 한다.
- 소켓 수 제한, YAML 비용 비활성화, 재료 부족, station level 부족은 서로 다른 비활성 사유로 tooltip에 구분한다.
- detail panel에는 선택 아이템, 현재 소켓 수, 성공률, 실패 페널티를 항상 보인다.
- bottom control row에는 `Socket` 버튼과 요구 재료를 둔다.
- 요구 재료가 없는 `ItemMayBreak`는 재료 영역을 비우고 위험 경고와 성공률을 더 넓게 사용한다.
- 요구 재료가 있는 3개 모드는 요구 재료를 항상 보여주되, `BreakOrCost`만 "성공 시 소비"임을 구분한다.

## Jewelcrafting의 Socket Tab 흐름

### 탭 생성과 활성 조건

`GemStones.AddSocketAddingTab`은 `InventoryGui.Awake`에서 vanilla upgrade tab을 복제해 `Socket` 탭을 만든다. 탭을 누르면 다른 탭 버튼은 다시 interactable로 두고 socket tab 버튼만 `interactable = false`로 만든 뒤 `InventoryGui.UpdateCraftingPanel()`을 호출한다.

socket tab open 판정은 다음과 같다.

```csharp
tab.gameObject.activeSelf && !tab.GetComponent<Button>().interactable
```

`LimitTabToGemCutterTable`은 `InventoryGui.UpdateCraftingPanel` prefix에서 현재 crafting station이 `op_transmution_table`이고 Jewelcrafting의 `socketingTab` config가 켜져 있을 때만 socket tab을 보이게 한다.

InventorySlots가 socket tab을 감지할 때는 이 판정과 동일해야 한다. 현재 `JewelcraftingCraftingSocketUiApi`는 `GemStones+AddSocketAddingTab.TabOpen()`과 `tab` field를 reflection으로 읽고 있으므로 방향은 맞다.

### 대상 목록 생성

socket tab이 열려 있으면 `GemStones.AddItemsToRecipeList`가 vanilla `UpdateRecipeList`를 막고 직접 recipe 목록을 만든다.

대상은 플레이어 인벤토리의 아이템 중 다음 조건이다.

- `Utils.IsSocketableItem(i)`
- 또는 `i.Data().Get<ItemContainer>() is { boxSealed: false }`

각 대상 아이템에 대해 임시 `Recipe`를 만들고, `RecipeDataPair.ItemData`에 실제 인벤토리 아이템을 넣는다. 이후 `AddRecipeToList`에 넘기는 `canCraft` 값은 최종적으로 아래 조건이다.

```csharp
recipe.Recipe.m_enabled && CanAddMoreSockets(recipe.ItemData)
```

즉 socket tab에서 `pair.CanCraft`는 "소켓 수 제한이나 YAML disabled 때문에 막혔는지"를 반영하지만, 재료 보유 여부는 직접 반영하지 않는다. 재료 보유 여부는 `UpdateRecipe`의 craft button 상태와 `DoCrafting` prefix에서 따로 검사한다.

InventorySlots에서 이 차이를 놓치면 다음 UI 문제가 생긴다.

- grid 정렬은 `pair.CanCraft` 기준이라 재료가 부족한 아이템도 craftable처럼 앞쪽에 남을 수 있다.
- cell icon color가 `pair.CanCraft`만 보면 재료 부족 아이템이 밝게 보일 수 있다.
- background만 `CanAttemptJewelcraftingSocket`을 보게 하면 icon과 background가 서로 다른 의미를 갖는다.

따라서 socket tab에서는 `pair.CanCraft`와 `CanAfford`를 합친 "attempt 가능 상태"를 별도 상태로 두고, 그 상태를 background, icon color, button state, sorting에 일관되게 사용해야 한다.

### 소켓 수 제한

`CanAddMoreSockets`는 다음 순서로 소켓 추가 가능 여부를 판단한다.

- 대상에 `ItemContainer`가 없으면 true
- item custom data에 `SocketSlotsLock`이 있으면 false
- `Limit number of Sockets`가 켜져 있고 현재 station이 gemcutter table이면 table level별 제한 사용
- 아니면 `Maximum number of Sockets` config 사용

여기서 `SocketsLock`과 `SocketSlotsLock`은 다르다. `SocketSlotsLock`은 소켓 슬롯 추가 자체를 막는다. `SocketsLock`은 주로 gem 제거/변경 쪽 제한에 쓰이며, 실패 시 gem 반환 조건에도 영향을 준다.

InventorySlots의 socket tab detail에는 가능하면 다음 정보를 보여주는 것이 좋다.

- 현재 소켓 수
- 이번 시도가 몇 번째 socket 추가인지
- 현재 table level 기준 최대 소켓 수
- 막힌 경우: `SocketSlotsLock`, table level 제한, maximum socket 제한 중 어떤 이유인지

## Socket Cost 설정

Jewelcrafting의 enum은 다음 4개다.

```csharp
public enum SocketCost
{
    ItemMayBreak,
    CostsItems,
    BreakOrCost,
    BreakAndCost,
}
```

config 설명상 의미는 다음과 같고, 코드 동작도 대체로 설명과 일치한다.

| Mode | 재료 요구 | 재료 소비 타이밍 | 실패 시 아이템 파괴 | 실패 시 재료 소비 | Jewelcrafting warning |
| --- | --- | --- | --- | --- | --- |
| `ItemMayBreak` | 없음 | 없음 | 예 | 없음 | 파괴 경고 |
| `CostsItems` | 있음 | 시도 전 | 아니오 | 예 | 확률만 표시 |
| `BreakOrCost` | 있음 | 성공 후 | 예 | 아니오 | 파괴 경고 |
| `BreakAndCost` | 있음 | 시도 전 | 예 | 예 | 파괴 경고 |

주의할 점은 `BreakOrCost`다. 이 모드는 "재료 또는 아이템 손실" 구조다.

- 성공하면 재료가 소비되고 아이템은 살아남으며 빈 소켓이 추가된다.
- 실패하면 재료는 소비되지 않고 아이템이 파괴된다.

따라서 UI에서 `BreakOrCost`의 재료를 `Cost`로만 표시하면 실제보다 더 가혹한 "실패해도 재료가 사라지는 비용"처럼 보인다. 이 모드는 요구 재료 영역에 `success cost`, `성공 시 소비`, 또는 tooltip 설명을 붙이는 편이 좋다.

### 비용 recipe 생성

`GemStones.AddItemsToRecipeList.UpdateRecipeSocketingCosts`는 `socketCost != ItemMayBreak`일 때만 비용을 recipe에 넣는다.

흐름:

1. `Socketing.EnsureCostsCache()`
2. 대상 item prefab 이름으로 `EquipmentDrops.biomeAssignments`에서 biome 조회
3. biome이 없으면 Meadows 사용
4. 현재 소켓 수를 기준으로 `Socketing.SocketRequirements[biome][sockets]` 선택
5. 해당 index가 없으면 `recipe.m_enabled = false`

즉 `SocketCosts.yml`에서 특정 socket 번호를 `disabled`로 두거나, 설정된 배열 범위를 넘으면 그 아이템은 socket tab에는 남을 수 있지만 `recipe.m_enabled = false`가 되어 시도할 수 없다.

### SocketCosts.yml 규칙

`Socketing.Parse`와 `Socketing.Apply` 기준으로 YAML은 biome -> socket number -> resources 구조다.

기본 파일은 `reference/Jewelcrafting-src/Jewelcrafting/Setup/Jewelcrafting.SocketCosts.yml`에 있다.

중요 규칙:

- socket number는 1부터 시작한다.
- 어떤 번호를 생략하면 직전 socket cost를 이어받는다.
- `3: disabled`처럼 지정하면 해당 biome의 3번째 socket부터 비활성화할 수 있다.
- resource 이름은 Loot yaml의 resource map / item name 해석에 의존한다.
- 비용 모드가 `ItemMayBreak`이면 이 YAML은 socket adding UI에 사용되지 않는다.

InventorySlots는 cost mode가 있는 경우 요구 재료가 4개를 넘을 수 있음을 고려해야 한다. 현재 `CraftingVisibleRequirementSlots = 4`라서 custom YAML이 5개 이상 재료를 요구하면 일부가 보이지 않는다. 기본 config는 socket당 1~2개 정도라 문제가 작지만, 호환성을 생각하면 socket tab에서는 `+N` 표시나 requirement tooltip 전체 목록이 필요하다.

## 성공률 계산

Jewelcrafting은 display와 실제 시도 모두 같은 방식으로 성공률을 계산한다.

```csharp
socketNumber = Math.Min(currentSocketCount, 9);
successChance = socketAddingChances[socketNumber] / 100f;
skillChance = JewelcraftingSkillFactor * upgradeChanceIncrease / 100f;

if (additiveSkillBonus == Off)
    successChance *= 1 + skillChance;
else
    successChance += skillChance;

successChanceInt = RoundToInt(successChance * 100);
```

기본 `socketAddingChances`는 1번째 socket 80%, 2번째 70%, 3번째 60%, 4번째 50%, 5번째 40%, 6번째 30%, 이후 25/20/15/10% 계열이다. 실제 config는 `Socket Adding Chances` section에서 서버 설정에 따라 바뀔 수 있다.

InventorySlots가 성공률을 직접 표시하려면 reflection으로 다음 값을 읽어야 한다.

- `Jewelcrafting.socketAddingChances`
- `Jewelcrafting.upgradeChanceIncrease`
- `Jewelcrafting.additiveSkillBonus`
- `Player.m_localPlayer.GetSkillFactor("Jewelcrafting")`

다만 Jewelcrafting 원본이 이미 `m_itemCraftType.text`를 갱신하므로, 구현 난이도를 낮추려면 처음에는 원본 텍스트를 재배치하는 방식이 안전하다. 단, 모드별로 `BreakOrCost`와 `BreakAndCost`가 같은 `$jc_socket_adding_warning`을 쓰기 때문에 "재료 소비 타이밍"은 InventorySlots가 별도 문구로 보강하는 것이 좋다.

## 실제 DoCrafting 동작

`GemStones.AddSocketToItem.Prefix`는 socket tab이 열려 있을 때 vanilla crafting을 막고 자체 처리한다.

공통 흐름:

1. selected recipe index 저장
2. `socketingItemsExperience`가 켜져 있으면 Jewelcrafting skill 상승
3. 선택 아이템과 현재 socket count 확인
4. 비용 모드에 따라 resource check / consume 준비
5. 성공률 계산
6. 실패하면 모드별 손실 처리
7. 성공하면 빈 socket 추가, item data save, crafting panel 갱신

비용 모드별 실제 소비:

```csharp
if (socketCost != ItemMayBreak)
{
    if (!NoCostCheat && !NoCraftCost)
    {
        if (!player.HaveRequirementItems(m_craftRecipe, false, 1))
            return false;

        if (socketCost == BreakOrCost)
            consumeResources = DoConsume; // 성공 후 호출
        else
            DoConsume();                  // 시도 전에 소비
    }
}
```

실패 시:

- `Blessed Item`이 있으면 blessed flag만 제거되고 아이템은 파괴되지 않는다.
- `socketCost != CostsItems`이면 아이템을 unequip 후 inventory에서 제거한다.
- `CostsItems`이면 아이템은 유지되고 `$jc_socket_adding_fail_costsonly` 메시지만 나온다.

성공 시:

- `Blessed Item` flag가 있으면 제거한다.
- `BreakOrCost`에서만 여기서 재료가 소비된다.
- `Sockets` data가 없으면 추가하고, 이미 있으면 빈 `SocketItem("")`을 append한다.

## InventorySlots 현재 구현 영향

현재 InventorySlots에는 socket tab 처리를 위한 주요 코드가 이미 있다.

- `CraftingRecipeView.UpdateCraftingRecipeView`: socket tab이면 `ItemData == null`인 pair를 제외하고 grid에 보여준다.
- `CraftingRecipeActions.CanAttemptJewelcraftingSocket`: `Recipe != null`, `ItemData != null`, `Recipe.m_enabled`, `pair.CanCraft`, `CanAfford`를 모두 요구한다.
- `CraftingBottomControls.UpdateJewelcraftingSocketCraftButtonState`: Socket 버튼을 `CanAttemptJewelcraftingSocket` 기준으로 켠다.
- `CraftingBottomControls.GetSelectedCraftingQuality`: socket tab에서는 requirement amount 계산 quality를 1로 고정한다.
- `CraftingGridInteraction.ConfigureCraftingRecipeCell`: cell background는 `IsCraftingRecipeActionAvailable`을 통해 socket attempt 가능 여부를 본다.
- `CraftingGridInteraction.ConfigureCraftingRecipeCellIcon`: icon color는 아직 `pair.CanCraft`만 본다.
- `CraftingRecipeView.CompareCraftingRecipeViewEntries`: 정렬도 아직 `pair.CanCraft` 중심이다.

따라서 다음 보강이 필요하다.

- socket tab에서는 icon color도 `CanAttemptJewelcraftingSocket` 기준으로 맞춘다.
- socket tab sort에서 craftable 우선순위를 `CanAttemptJewelcraftingSocket` 기준으로 바꾼다.
- `CanAffordJewelcraftingSocketAttempt`는 mode를 알 수 있으면 `ItemMayBreak`에서는 true를 즉시 반환한다. 현재는 empty requirements에 기대도 대체로 동작하지만, 의도를 명확히 하는 편이 좋다.
- cost mode별 UI 문구를 위해 `Jewelcrafting.socketCost`를 reflection adapter에 추가한다.
- 가능하면 socket chance와 max socket 제한 정보도 adapter에 추가한다.

## 공통 레이아웃 권장안

InventorySlots의 crafting redesign은 8 columns x 9 rows 구조이며, 마지막 row를 bottom controls로 사용한다.

권장 배치는 다음과 같다.

```text
┌──────────────────────────────────────────────────────────────┐
│ tabs                                                         │
├──────────────────────────────┬───────────────────────────────┤
│ socketable item grid          │ selected item detail          │
│ 8x8 area, paged/zoomable      │ name, tooltip, socket icons   │
│                              │ chance/risk/cost explanation  │
├──────────────────────────────┴───────────────────────────────┤
│ [ Socket button ] [mode/chance label] [requirements...] [lvl] │
└──────────────────────────────────────────────────────────────┘
```

공통 원칙:

- `Craft` 버튼 텍스트는 항상 `$jc_add_socket_button` 또는 fallback `Socket`.
- 일반 craft count input과 upgrade progression은 socket tab에서는 숨긴다.
- selected item detail에는 기존 Jewelcrafting socket icons를 재배치해 현재 socket 상태를 보여준다.
- `AddSocketIcons.socketingButton`은 `Socket` 시도 버튼과 혼동되지 않도록 detail panel의 socket icon 옆 secondary action으로 둔다. 이 버튼은 "소켓 관리/보석 장착 컨테이너 열기" 성격이다.
- bottom requirements는 재료가 있는 모드에서만 사용한다.
- warning/chance text는 vanilla `m_itemCraftType`를 재사용하되, InventorySlots panel 안에서 잘리지 않게 별도 line 또는 warning band로 배치한다.

## 모드별 배치

### ItemMayBreak

동작:

- socket cost YAML을 사용하지 않는다.
- 요구 재료가 없다.
- 실패하면 아이템이 파괴된다. 단, `Blessed Item`이면 blessed flag가 제거되고 아이템은 살아남는다.

권장 UI:

- bottom row:
  - column 0~1: `Socket` button
  - column 2~6: 성공률 + 아이템 파괴 경고
  - requirements area는 숨김
- selected item detail:
  - 현재 socket count와 다음 socket chance를 크게 표시
  - `실패 시 아이템 파괴`를 명확하게 표시
- grid cell:
  - `CanAddMoreSockets`가 true면 활성 배경
  - max socket / lock 때문에 불가능하면 회색

이 모드는 비용이 없기 때문에 플레이어가 잃을 수 있는 것은 아이템 자체다. UI는 재료칸보다 risk notice가 더 중요하다.

### CostsItems

동작:

- socket cost YAML을 사용한다.
- 재료가 부족하면 시도할 수 없다.
- 재료는 시도 전에 소비된다.
- 실패해도 아이템은 파괴되지 않는다.
- 실패해도 이미 소비한 재료는 돌아오지 않는다.

권장 UI:

- bottom row:
  - column 0~1: `Socket` button
  - column 2: 성공률 compact label
  - column 3~6: required resources
  - column 7: station level / lock reason slot
- selected item detail:
  - `실패해도 아이템 유지`
  - `재료는 시도 시 소비`
- grid cell:
  - 재료 보유 + 소켓 추가 가능이면 활성 배경
  - 재료 부족이면 회색 또는 낮은 alpha
  - tooltip에 missing requirements

이 모드에서는 아이템 파괴 경고가 나오면 안 된다. Jewelcrafting도 `$jc_socket_adding_warning_costsonly`를 사용하므로 InventorySlots도 같은 의미를 유지해야 한다.

### BreakOrCost

동작:

- socket cost YAML을 사용한다.
- 재료가 부족하면 시도할 수 없다.
- 성공하면 재료가 소비된다.
- 실패하면 재료는 소비되지 않고 아이템이 파괴된다.

권장 UI:

- bottom row:
  - column 0~1: `Socket` button
  - column 2: 성공률 + `실패 시 파괴`
  - column 3~6: required resources
  - requirements tooltip 또는 header: `성공 시 소비`
- selected item detail:
  - `성공: 재료 소비 + 소켓 추가`
  - `실패: 아이템 파괴, 재료 유지`
- grid cell:
  - 재료가 없으면 시도 불가 회색
  - 재료가 있으면 활성 배경이지만 risk marker를 둘 수 있다

이 모드는 가장 오해하기 쉽다. 요구 재료가 표시되지만 실제로는 "시도 비용"이 아니라 "성공 비용"이다. UI에 이 차이를 반드시 남기는 것이 좋다.

### BreakAndCost

동작:

- socket cost YAML을 사용한다.
- 재료가 부족하면 시도할 수 없다.
- 재료는 시도 전에 소비된다.
- 실패하면 아이템도 파괴된다.

권장 UI:

- bottom row:
  - column 0~1: `Socket` button
  - column 2: 성공률 + 강한 risk label
  - column 3~6: required resources
  - requirements tooltip 또는 header: `시도 시 소비`
- selected item detail:
  - `실패: 아이템 파괴 + 재료 소비`
  - `Blessed Item`이 있으면 item break는 막을 수 있지만 재료 소비는 이미 발생할 수 있음을 tooltip에 적는 것이 좋다.
- grid cell:
  - 재료 보유 + 소켓 추가 가능이면 활성 배경
  - risk marker를 가장 강하게 표시

이 모드는 손실이 가장 크므로 UI도 가장 강한 경고가 필요하다.

## 상태 판정 모델

socket tab에서 cell과 button 상태를 안정적으로 유지하려면 내부 상태를 다음처럼 나누는 것이 좋다.

```csharp
enum SocketAttemptBlockReason
{
    None,
    NoRecipe,
    NoItem,
    RecipeDisabledBySocketCostYaml,
    SocketSlotsLocked,
    MaxSocketsReached,
    StationLevelTooLow,
    MissingRequirements,
}
```

최소 구현에서는 `RecipeDisabledBySocketCostYaml`, `MaxSocketsReached`, `StationLevelTooLow`, `SocketSlotsLocked`를 모두 `pair.CanCraft == false`로 묶어도 된다. 그러나 사용자 tooltip을 제대로 만들려면 Jewelcrafting의 `CanAddMoreSockets`와 관련 config를 reflection으로 읽거나, 그와 동일한 판정을 InventorySlots에서 재현해야 한다.

UI state는 다음처럼 분리한다.

| State | 조건 | 배경 | 아이콘 | 버튼 |
| --- | --- | --- | --- | --- |
| Attemptable | `Recipe.m_enabled && pair.CanCraft && canAfford` | 활성 | white | enabled |
| Missing Cost | cost mode != `ItemMayBreak`, 재료 부족 | 비활성 | dim | disabled |
| Socket Limited | `pair.CanCraft == false` | 비활성 | dim | disabled |
| YAML Disabled | `Recipe.m_enabled == false` | 비활성 | dim | disabled |

이렇게 해야 socket tab에서 "선택은 가능하지만 버튼은 비활성"인 상태가 자연스럽다. 아이템을 클릭하면 detail panel은 보여주되, 왜 시도할 수 없는지 button tooltip과 detail warning에 표시한다.

## Reflection Adapter 권장 확장

현재 `JewelcraftingCraftingSocketUiApi`는 UI 요소 위주로만 접근한다.

추가하면 좋은 값:

- `Jewelcrafting.socketCost.Value`
- `Jewelcrafting.socketAddingChances`
- `Jewelcrafting.upgradeChanceIncrease.Value`
- `Jewelcrafting.additiveSkillBonus.Value`
- `Jewelcrafting.maximumNumberSockets.Value`
- `Jewelcrafting.limitSocketsByTableLevel.Value`
- `Jewelcrafting.maxSocketsTableLevel`
- 가능하면 `GemStones.CanAddMoreSockets(ItemData)` private method

private method reflection이 부담스럽다면 처음에는 다음까지만 해도 충분하다.

- `socketCost`
- `socketAddingChances`
- `upgradeChanceIncrease`
- `additiveSkillBonus`

나머지 block reason은 `pair.CanCraft`, `Recipe.m_enabled`, `Recipe.m_resources`, `HaveRequirementItems` 조합으로 대략 표현한다.

## 구현 우선순위

1. `JewelcraftingCraftingSocketUiApi`에 `socketCost` reader를 추가한다.
2. socket tab에서 `CraftingRecipeView` sort의 craftable flag를 `CanAttemptJewelcraftingSocket` 기준으로 바꾼다.
3. `ConfigureCraftingRecipeCellIcon`도 socket tab에서는 `CanAttemptJewelcraftingSocket` 기준으로 dim 처리한다.
4. bottom control row에 mode-specific label을 추가한다.
5. `ItemMayBreak`에서는 requirement list parent를 확실히 숨기고, warning/chance area를 넓게 쓴다.
6. `BreakOrCost`와 `BreakAndCost`는 같은 resource list를 쓰되 label/tooltip을 다르게 둔다.
7. custom YAML에서 requirement가 4개를 넘는 경우 `+N` 또는 full tooltip을 제공한다.
8. debug log에 socket mode, selected item, recipe enabled, pair canCraft, canAfford, visible requirement count를 추가한다.

## 검증 체크리스트

각 mode마다 같은 아이템으로 다음을 확인한다.

- 재료가 충분할 때 grid cell, detail panel, Socket button이 모두 enabled 의미로 보이는가
- 재료가 부족할 때 grid cell, button tooltip, requirement 표시가 모두 missing requirement를 가리키는가
- max socket 제한에 걸린 아이템이 재료 부족과 다른 이유로 표시되는가
- `SocketCosts.yml`에서 특정 socket number가 `disabled`인 경우 recipe disabled로 보이는가
- `Blessed Item`이 있는 아이템의 실패 설명이 과장되지 않는가
- `NoCostCheat` 또는 `NoCraftCost`일 때 요구 재료 부족이 button을 막지 않는가
- socket tab에서 craft count input이나 upgrade progression이 나타나지 않는가
- socket tab을 닫고 다른 crafting station으로 이동해도 Jewelcrafting socket UI가 남지 않는가

## 최종 권장안

InventorySlots의 socket tab은 "업그레이드 UI를 살짝 고치는 것"보다 "소켓 시도 전용 action panel"로 보는 것이 맞다. Jewelcrafting 원본은 vanilla crafting panel에 억지로 socket adding을 끼워 넣는 구조라 비용 모드별 의미가 `m_itemCraftType`, `m_recipeRequirementList`, `m_craftButton`에 흩어져 있다.

InventorySlots가 재배치할 때는 다음 규칙을 기준으로 삼는 것이 가장 안정적이다.

- 시도 가능 여부는 `Recipe.m_enabled && pair.CanCraft && canAfford`
- 재료 표시는 `socketCost != ItemMayBreak`
- 아이템 파괴 경고는 `socketCost != CostsItems`
- 재료 소비 타이밍 표시는 `BreakOrCost`만 "성공 시 소비", `CostsItems`와 `BreakAndCost`는 "시도 시 소비"
- `ItemMayBreak`는 요구 재료 UI를 숨기고 risk/chance를 넓게 표시
- `BreakAndCost`는 아이템과 재료를 모두 잃을 수 있음을 가장 강하게 표시

이 구조로 가면 4가지 Socket Cost config를 모두 정확하게 설명하면서도, InventorySlots의 grid 기반 crafting panel 안에서 버튼, 요구 재료, 경고 문구가 서로 같은 의미를 갖게 된다.
