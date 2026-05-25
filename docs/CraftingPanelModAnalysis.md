# Crafting Panel Mod Analysis: MyLittleUI / ZenUI / AAA_Crafting

작성일: 2026-05-09

## Scope

이 문서는 아래 세 DLL의 crafting panel 관련 코드만 분석한다.

- `Libs/MyLittleUI.dll`
- `Libs/ZenUI.dll`
- `Libs/AzuAntiArthriticCrafting.dll`

디컴파일 참고 경로:

- `decompiled/MyLittleUI`
- `decompiled/ZenUI`
- `decompiled/AzuAntiArthriticCrafting`

InventorySlots의 crafting panel 재구성에 참고할 수 있는 구조, 안전한 패턴, 충돌 위험이 큰 패턴을 중심으로 정리했다.

## MyLittleUI

MyLittleUI는 crafting panel 전체를 교체하기보다는, vanilla crafting UI 위에 기능을 추가하는 방식이다. 핵심은 기존 recipe list를 유지한 상태에서 검색, 필터, 정렬, new mark, 요구 재료 보유량, multi craft UI를 덧붙이는 구조다.

### Crafting 관련 코드 맵

| 파일 | 역할 |
| --- | --- |
| `MyLittleUI/CraftFilter.cs` | crafting recipe 검색 입력창 추가, recipe filtering |
| `MyLittleUI/CraftSort.cs` | recipe category/filter/sort 패널 추가, recipe list 재정렬 |
| `MyLittleUI/MultiCraft.cs` | craft 버튼 옆 multi craft 수량 UI 추가 |
| `MyLittleUI.Crafting/CraftNew.cs` | 새로 제작할 가치가 있는 recipe에 mark 표시 |
| `MyLittleUI/MyLittleUI.cs` | requirement 보유량 표시, 관련 config |
| `MyLittleUI/ItemTooltip.cs` | recipe description font size 조정 |

### CraftFilter

패치 대상:

- `InventoryGui.Awake`
- `InventoryGui.Show`
- `InventoryGui.OnDestroy`
- `Chat.HasFocus`
- `Player.GetAvailableRecipes`
- `InventoryGui.UpdateRecipe`

동작 방식:

- `InventoryGui.instance.m_recipeListScroll.transform.parent` 근처에 `TextInput.instance.m_inputField`를 복제해서 `MLUI_FilterField`를 만든다.
- 입력값이 바뀌면 recipe cache를 만들고 `InventoryGui.instance.UpdateCraftingPanel(true)`를 지연 호출한다.
- 실제 filtering은 `Player.GetAvailableRecipes` postfix에서 `available.RemoveAll(...)`로 처리한다.
- 검색 대상 문자열은 recipe 이름, item prefab/name/type/set, localized name, tooltip, requirement item 정보까지 포함한다.

참고할 점:

- 검색 UI는 vanilla recipe list 위에 붙는 additive 구조라 비교적 충돌 위험이 작다.
- `Chat.HasFocus`를 patch해서 입력 필드 focus 중에는 다른 입력이 새지 않게 처리한다.
- recipe 검색 문자열 cache를 둬서 매 입력마다 tooltip 문자열을 다시 만들지 않으려 한다.

주의할 점:

- 검색 필드는 기존 recipe list 높이/anchor를 직접 조정한다.
- InventorySlots가 recipe list를 grid로 완전히 바꾸면 이 방식은 그대로 가져오기 어렵다. 검색 조건과 focus 처리만 참고하는 편이 좋다.

### CraftSort

패치 대상:

- `InventoryGui.UpdateRecipeList`
- `InventoryGui.UpdateCraftingPanel`
- `InventoryGui.Update`
- `InventoryGui.Show`
- `InventoryGui.OnDestroy`
- `Game.SpawnPlayer`

동작 방식:

- `InventoryGui.instance.m_repairPanel`을 복제해서 `MLUI_SortingPanels`를 만든다.
- filter icon은 `m_playerGrid.m_elementPrefab` 기반으로 만든다.
- food, armor, skills, bows, crossbows, magic, tools 같은 category filter를 구성한다.
- `UpdateRecipeList` prefix에서 recipe list를 필터링하고, 이후 `InventoryGui.instance.m_availableRecipes`를 정렬한다.
- 정렬 후 `RecipeDataPair.InterfaceElement`의 `anchoredPosition`을 다시 계산해서 vanilla recipe list 순서를 재배치한다.

참고할 점:

- vanilla recipe element를 유지하면서 `m_availableRecipes`와 `InterfaceElement`를 재정렬하는 접근은 보수적이다.
- category 판단에는 item type, skill type, attack animation, armor, food stat 등을 활용한다.
- 다른 crafting UI 모드가 있을 때 패널을 숨기는 compatibility guard가 있다.

주의할 점:

- InventorySlots의 icon grid UI에서는 `InterfaceElement` 기반 재배치보다 recipe item clone 기반 grid가 더 자연스럽다.
- category/filter 패널까지 가져오면 UI 복잡도가 빠르게 올라간다.

### MultiCraft

패치 대상:

- `InventoryGui.UpdateRecipe`
- `InventoryGui.Awake`
- `InventoryGui.OnDestroy`
- `InventoryGui.SetRecipe`
- `Player.ToggleNoPlacementCost`

동작 방식:

- craft button 옆에 `+`, `-`, amount label을 붙인다.
- craft 진행 완료를 감지하면 남은 수량만큼 `m_craftButton.onClick.Invoke()`를 반복 호출한다.
- 최대 제작 가능 수량은 `Player.HaveRequirements(recipe, false, 1, amount)`로 이분 탐색한다.
- `m_recipeRequirementList[i]/res_amount` 텍스트를 읽어 cache key로 사용한다.

참고할 점:

- 최대 craft 가능 수량 계산 방식은 가져올 만하다.
- 수량 입력 UI를 craft button parent 안에 넣고, 기존 button asset과 `ButtonSfx`를 재사용한다.

주의할 점:

- `m_craftButton.onClick.Invoke()`를 반복 호출하는 방식은 다른 craft 관련 patch와 충돌하기 쉽다.
- requirement label 텍스트를 cache key로 쓰는 방식은 UI 표시 변경과 강하게 결합된다.
- InventorySlots가 이미 queue형 craft를 지향한다면 MyLittleUI 방식보다 자체 queue state를 유지하는 쪽이 안정적이다.

### Requirement / New Mark

`InventoryGui.SetupRequirement` postfix에서 requirement UI의 `res_amount` 텍스트 뒤에 현재 보유 수량을 붙인다.

`CraftNew`는 `InventoryGui.UpdateRecipeList` postfix에서 recipe element의 `QualityLevel` 텍스트를 재사용해 `?` 같은 new mark를 표시한다. 표시 대상은 아직 알려지지 않은 material이고, 설정에 따라 다른 recipe/build piece의 재료로 쓰이는 item만 보여준다.

참고할 점:

- vanilla requirement element를 복제하거나 새로 만들기보다 기존 `SetupRequirement` 결과를 후처리한다.
- recipe element의 기존 child를 재사용하는 방식은 가볍지만, grid UI에서는 별도 overlay로 다시 설계하는 편이 낫다.

## ZenUI

ZenUI는 crafting panel을 더 적극적으로 교체한다. 핵심은 오른쪽 recipe description 영역에 `ContainerGrid`를 복제하고, 그 위에 custom `CraftingPanelGrid : InventoryGrid`를 붙여 recipe를 fake inventory item처럼 표시하는 방식이다.

### Crafting 관련 코드 맵

| 파일 | 역할 |
| --- | --- |
| `ZenUI.Section/CraftingPanel.cs` | crafting panel 전체 교체, recipe grouping/sorting, grid data 구성 |
| `ZenUI.Section/CraftingPanelGrid.cs` | `InventoryGrid` 상속 grid 구현, fake inventory 표시, scrollbar/tooltip 처리 |
| `ZenUI.Section/CraftingPanelSearch.cs` | crafting grid 검색창 |
| `ZenUI/Configs.cs` | crafting panel 관련 config |
| `ZenUI.Compatibility/OtherModUI.cs` | grid 표시 중 다른 모드 UI 숨김 |

### CraftingPanel

패치 대상:

- `InventoryGui.UpdateRecipeList`
- `InventoryGui.Update`
- `InventoryGui.SetRecipe`
- `InventoryGui.Awake`
- `InventoryGui.UpdateRecipe`
- `InventoryGui.Show`
- `InventoryGui.Hide`
- `InventoryGui.OnTabCraftPressed`
- `InventoryGrid.CreateItemTooltip`
- `InventoryGui.SetupRequirement`
- `Player.GetAvailableRecipes`
- `Player.ToggleNoPlacementCost`
- `Player.ResetCharacterKnownItems`
- `UITooltip.OnHoverStart`

동작 방식:

- `InventoryGui.UpdateRecipeList` prefix에서 craft tab이면 vanilla 원본 실행을 막고 `m_availableRecipes`를 직접 채운다.
- recipe를 Everything, Weapons, Ammo, Armor, Shields, Magic, Tools, Equipment, Resources, Food, Trophy, Misc 그룹으로 분류한다.
- 왼쪽 recipe list에는 recipe 자체가 아니라 group list를 vanilla `m_recipeElementPrefab`으로 표시한다.
- 오른쪽 `Decription` panel에는 `ContainerGrid`를 복제해 `ZenUI_CraftingGrid`를 만든다.
- 복제한 object의 기존 `InventoryGrid`를 제거하고 `CraftingPanelGrid`를 붙인다.
- craft button, craft progress bar, requirements panel, craft station text 위치를 재배치한다.
- vanilla recipe icon/name/description은 grid 모드에서 숨긴다.
- 선택된 grid item을 `InventoryGui.instance.m_selectedRecipe`와 `m_selectedVariant`에 다시 연결한다.

중요한 구조:

- recipe icon grid는 실제 recipe UI element가 아니라 cloned `ItemData` 목록을 가진 fake `Inventory`다.
- 각 recipe variant마다 item clone을 만들고 `m_gridPos = new Vector2i(index % width, index / width)`로 좌표를 넣는다.
- fake inventory height는 recipe 수에 따라 늘리고, `inventory.Changed()`로 grid update를 유도한다.

기본 grid 크기:

- width 5
- visible height 7

InventorySlots가 원하는 8x7 또는 8x8 crafting layout으로 확장할 때도 같은 원리를 쓸 수 있다. width 상수를 8로 두고, fake inventory height만 recipe 수에 따라 계산하면 vanilla grid 계열의 경계, icon, hover, scrollbar 처리를 상당 부분 재사용할 수 있다.

### CraftingPanelGrid

핵심 구조:

- `CraftingPanelGrid : InventoryGrid`
- `m_elementPrefab = InventoryGui.instance.m_playerGrid.m_elementPrefab`
- `m_elementSpace = InventoryGui.instance.m_playerGrid.m_elementSpace`
- tooltip anchor는 player grid tooltip anchor를 복제한다.
- scrollbar는 `InventoryGui.instance.ContainerGrid.m_scrollbar`를 복제한다.
- 내부 inventory는 `new Inventory("$hud_crafting", null, width, height)`로 만든다.

성능 관련:

- grid size별 root cache를 둔다.
- 같은 width/height일 때 element root를 재사용한다.
- `UpdateGuiOptimized()`에서 `m_gridPos -> ItemData` dictionary를 사용해 element를 채운다.
- update 시간이 3ms를 넘으면 log를 남긴다.
- 큰 grid를 고려해서 Canvas와 GraphicRaycaster를 grid object에 추가한다.

표시 처리:

- player inventory element prefab에서 icon, amount, quality, selected, no teleport, food icon만 유지하고 나머지는 제거한다.
- craft 가능 여부에 따라 icon brightness를 낮추거나 symbol을 표시한다.
- selected recipe는 `equiped` image를 재사용해 표시한다.
- hover/gamepad focus 시 `InventoryGrid.CreateItemTooltip` 경로를 사용한다.

참고할 점:

- InventorySlots의 crafting grid 경계가 vanilla inventory grid처럼 보이게 하려면 ZenUI 방식이 가장 직접적인 참고 자료다.
- 직접 Image를 그려 grid를 흉내 내기보다 `m_playerGrid.m_elementPrefab`과 `ContainerGrid`/`InventoryGrid` 흐름을 재사용하는 쪽이 안전하다.

주의할 점:

- `InventoryGui.UpdateRecipeList` 원본을 막는 것은 강한 패치다.
- `InventoryGui.SetRecipe`도 craft tab에서는 vanilla 실행을 막는다.
- 다른 crafting UI 모드와 충돌하기 쉬우며, ZenUI도 `OtherModUI.Show(...)`로 오른쪽 panel의 다른 모드 UI를 숨긴다.
- InventorySlots가 다른 inventory 기능도 많이 가진 모드라면 crafting panel 교체는 config로 끄고 켤 수 있게 두는 것이 좋다.

### CraftingPanelSearch

동작 방식:

- `TextInput.instance.m_inputField`를 복제해 `ZenUI_Search`를 만든다.
- search text가 바뀌면 `CraftingPanel.UpdateRecipeList()`와 `CraftingPanel.SelectItem(null)`를 호출한다.
- 검색 대상은 item name, localized name, armor set name, resource requirement name이다.
- `Chat.HasFocus`를 patch해서 input focus를 보호한다.

참고할 점:

- 검색 UI 생성 방식은 MyLittleUI와 비슷하지만, 검색 적용은 ZenUI의 grid recipe data에 맞춰져 있다.
- InventorySlots가 icon grid를 유지한다면 ZenUI의 search application 방식이 더 맞다.

### ZenUI Configs

crafting panel 관련 config:

- `CraftPanelEnabled`
- `CraftPanelTooltipReq`
- `CraftPanelTooltipHover`
- `CraftPanelSearch`
- `CraftPanelSearchPosition`
- `CraftPanelSearchResources`
- `CraftPanelSearchArmorSets`
- `CraftPanelSortCraftable`
- `CraftPanelShowAnyKnownStation`
- `CraftPanelHideUncraftableVariants`
- `CraftPanelUncraftableBrightness`
- `CraftPanelUncraftableSymbol`
- `CraftPanelUncraftableSymbolColor`
- `CraftPanelUncraftableSymbolPosition`
- `CraftPanelInfoPosition`
- `CraftPanelScrollSensitivity`

참고할 점:

- scroll sensitivity, tooltip 위치, search 위치, uncraftable 표시 같은 UI tuning 값은 client-side config가 적합하다.
- recipe availability나 station rule을 바꾸는 옵션은 서버 운영 환경에서는 synced config로 보는 편이 안전하다.

## AAA_Crafting

AAA_Crafting(AzuAntiArthriticCrafting)은 vanilla crafting panel을 완전히 새 panel로 교체하기보다는, vanilla `RecipeList/Recipes/ListRoot`와 recipe element를 재사용하면서 grid, pagination, search/filter, craft amount UI를 얹는 구조다. InventorySlots의 현재 crafting redesign 목표와 비교하면 ZenUI보다 덜 침습적이고, MyLittleUI보다 grid/pagination 쪽 참고 가치가 크다.

### 주요 코드 위치

| 파일 | 역할 |
| --- | --- |
| `AzuAntiArthriticCrafting.Patches/RecipeListFixer.cs` | vanilla recipe list root에 `GridLayoutGroup`, `ContentSizeFitter`, `ScrollRect`를 붙여 recipe list를 grid처럼 재배치 |
| `AzuAntiArthriticCrafting.Patches/PaginatorPatches.cs` | recipe 검색, 필터, 정렬, pagination, page wheel 이동, page cache |
| `AzuAntiArthriticCrafting.Patches/RecipeListPerfCache.cs` | page flip 시 sort/filter/search 결과 재사용 |
| `AzuAntiArthriticCrafting.Patches.Filtering/FilterManager.cs` | item type 기반 간단 filter mask |
| `AzuAntiArthriticCrafting.Patches.Filtering/AdvancedFiltersOverlay.cs` | station, station level, armor, damage, food stat 등 고급 filter overlay |
| `AzuAntiArthriticCrafting.Patches/AAACraft.cs` | craft 수량 입력 UI, plus/minus 버튼, craft 완료 후 반복 craft |
| `AzuAntiArthriticCrafting/ScrollableInputField.cs` | craft amount input wheel 조정, modifier key 처리 |
| `AzuAntiArthriticCrafting/MaxCraftAmount.cs` | 현재 recipe의 최대 제작 가능 수량 표시 |
| `InventoryGui_DoCrafting_Patch.cs`, `InventoryGuiUpdateRecipePatch.cs` | craft amount를 실제 제작 반복에 연결 |
| `AzuAntiArthriticCrafting.Patches/InventoryGuiSetupRequirementPatch.cs` | 표시되는 requirement 수량을 craft amount 기준으로 스케일 |

### Recipe Grid / Pagination

AAA_Crafting의 recipe grid는 ZenUI처럼 `InventoryGrid`를 새로 만들지 않고, vanilla recipe entry list를 grid layout으로 바꾼다.

- `RecipeListFixer.FixRecipeList`가 `ListRoot`에 `GridLayoutGroup`을 붙인다.
- grid preset은 Default/Medium/Large/Vanilla-like로 나뉘며, cell size, spacing, column count, page size를 config로 결정한다.
- vanilla recipe element clone을 계속 쓰기 때문에 selected state, localized name, button, hover 처리와의 연결이 비교적 자연스럽다.
- page 전환은 `PaginatorPatches`에서 recipe list 영역에 마우스가 있을 때 wheel로 처리한다.
- page flip만 하는 경우 `RecipeListPerfCache.PageFlipOnly`를 세워 전체 sort/filter/search를 다시 하지 않고 cached list를 재사용한다.

InventorySlots가 8x7 icon grid를 유지한다면 ZenUI식 `InventoryGrid` 기반 grid가 시각적으로 더 잘 맞지만, AAA_Crafting의 page cache와 page wheel 조건은 그대로 가져올 가치가 있다. 특히 지금처럼 recipe grid 안에 마우스가 있을 때만 wheel page 전환을 하고 싶다면 AAA_Crafting의 조건 분리가 좋은 참고 자료다.

### Search / Filter

AAA_Crafting은 search string을 아래처럼 해석한다.

- 일반 검색: localized item name, prefab name, description 검색
- `!term`: recipe 결과 아이템 이름 또는 requirement item 이름 검색
- `@mod`: prefab origin assembly 이름 기준 검색

기본 filter는 `ItemTypeMask`로 food/material/bow/armor/ammo/weapon/shield/tools/undefined를 분류한다. 고급 overlay는 기능이 강하지만 UI와 state가 크고, InventorySlots의 1차 crafting redesign에는 과한 편이다. 다만 station level, crafting station, damage type, food stat 같은 필터 기준은 나중에 recipe grouping/filter를 확장할 때 참고할 수 있다.

### Craft Amount / Multi Craft

AAA_Crafting은 craft 버튼 옆에 `-`, input, `+`를 붙이고, input field에서 mouse wheel로 수량을 조절한다. 최대 제작 가능 수량 계산은 현재 inventory와 craft-from-container 계열 compatibility까지 고려한다.

실제 제작 연결은 두 층이 있다.

- `AAACraft.UpdateRecipe`는 craft 완료 상태 전환을 감지해 남은 수량이 있으면 `m_craftButton.onClick.Invoke()`로 다음 craft를 시작한다.
- 별도 `InventoryGui_DoCrafting_Patch` / `InventoryGuiUpdateRecipePatch`는 `DoCrafting` 호출을 반복하는 batch성 경로도 가진다.

InventorySlots에는 첫 번째 방식, 즉 craft 완료 후 다음 1개를 queue에서 시작하는 방식만 가져오는 편이 좋다. 사용자가 원하는 것도 “n개가 한 번에 제작되는 것”이 아니라 “1개씩 n번 queue로 제작되는 것”이므로, `DoCrafting`을 같은 tick에서 반복 호출하는 방식은 compatibility 위험이 크다.

### InventorySlots에 가져올 만한 점

- recipe grid 안에 마우스가 있을 때만 wheel page 전환
- page flip 전용 cache로 sort/filter/search 재계산 줄이기
- craft amount input에 mouse wheel과 modifier key 지원
- max craft amount 계산을 별도 helper로 분리하고 inventory 변경 시에만 invalidate
- requirement 표시를 craft amount 기준으로 `needed/available` 형태로 직접 갱신
- search prefix `!`, `@` 같은 확장 문법은 나중에 옵션으로 도입 가능

### 그대로 가져오지 않는 편이 좋은 점

- `DoCrafting`을 반복 호출하는 batch craft 경로
- `InventoryGui.SetupRequirement` transpiler로 requirement 수량을 전역 변경하는 방식
- 큰 advanced overlay UI를 crafting redesign 초기부터 포함하는 것
- vanilla recipe list를 grid로 바꾸는 방식 자체는 InventorySlots의 현재 fake grid/8x8 layout과 섞으면 상태가 복잡해질 수 있다.

## 비교

| 항목 | MyLittleUI | ZenUI | AAA_Crafting |
| --- | --- | --- | --- |
| 접근 방식 | vanilla crafting UI에 기능 추가 | crafting panel을 icon grid 중심으로 재구성 | vanilla recipe list를 grid/paginator로 재배치 |
| recipe list | vanilla list 유지 | 왼쪽은 group list, 오른쪽은 fake inventory grid | vanilla recipe entry를 grid cell처럼 표시 |
| grid 구현 | 없음 | `ContainerGrid` clone + `InventoryGrid` 상속 | `ListRoot`에 `GridLayoutGroup`/`ScrollRect` 추가 |
| search | `Player.GetAvailableRecipes` 결과 filtering | grid recipe data filtering | name/prefab/description, requirement, mod source 검색 |
| multi craft | craft button 반복 invoke | vanilla multi craft multiplier 일부 활용 | 수량 input + queue성 반복 + batch성 경로 |
| compatibility | 상대적으로 보수적 | 더 강한 patch, 충돌 가능성 큼 | 중간, transpiler/batch craft 사용 시 상승 |
| InventorySlots 참고 가치 | 검색, focus, requirement 후처리, max craft 계산 | grid/scrollbar/tooltip/recipe clone 구조 | page cache, wheel paging, amount input, max craft helper |

## InventorySlots에 대한 적용 제안

### 1. Recipe grid는 ZenUI 패턴을 우선 참고

현재 목표가 8x7 recipe icon grid와 vanilla inventory grid 같은 경계라면, ZenUI의 `CraftingPanelGrid : InventoryGrid` 방식이 가장 맞다.

권장 구조:

- `ContainerGrid` 또는 player grid prefab을 복제한다.
- 기존 `InventoryGrid`를 제거하거나 별도 component로 대체한다.
- fake `Inventory`를 만들고 recipe별 cloned `ItemData`를 넣는다.
- item clone의 `m_gridPos`로 8열 배치를 지정한다.
- tooltip, selected, no teleport, amount 표시는 `InventoryGrid` element 구조를 재사용한다.

이렇게 하면 수동으로 grid 배경과 칸 경계를 그리는 것보다 vanilla와 비슷한 결과를 얻기 쉽다.

### 2. Station level / requirements / craft button은 재구현보다 이동

사용자가 원하는 것은 기존 vanilla 제작 레벨 표시와 requirement block을 새 위치로 옮기는 것이다. ZenUI도 `m_itemCraftType`, `Decription/requirements`, `m_craftButton`, `m_craftProgressPanel`을 새 위치로 이동한다.

권장:

- 제작대 레벨 표시는 새 TMP를 만들지 말고 기존 `m_itemCraftType` 또는 관련 vanilla object를 이동한다.
- requirement list도 기존 `Decription/requirements`를 옮긴다.
- craft button과 progress panel도 기존 object를 유지한다.

이 방식이 font, localization, tooltip, craft 가능 상태, modded requirement 표시와 더 잘 맞는다.

### 3. Multi craft는 MyLittleUI/AAA_Crafting 방식을 선별해서 가져오는 편이 좋음

MyLittleUI와 AAA_Crafting 모두 craft 완료 후 다음 craft를 시작하는 방식이 있다. 이 방향은 queue형 제작에 잘 맞지만, AAA_Crafting의 별도 batch craft 경로처럼 `DoCrafting`을 같은 흐름에서 여러 번 반복 호출하는 방식은 다른 craft-from-container, craft-cost, station patch와 충돌할 수 있다.

InventorySlots는 사용자가 말한 queue형 제작이 목적이므로:

- craft queue state를 InventorySlots가 직접 가진다.
- 한 번에 하나씩 vanilla craft cycle을 진행한다.
- 다음 craft 시작 시점만 제어한다.
- max craft amount 계산은 `Player.HaveRequirements(recipe, false, 1, amount)` 이분 탐색 패턴을 참고한다.
- amount input과 max craft 계산은 AAA_Crafting처럼 helper로 분리하되, 실제 제작은 1개씩 순차 처리한다.

### 4. Search/filter는 grid data 단계에서 적용

InventorySlots가 recipe icon grid를 사용한다면 `Player.GetAvailableRecipes` 결과 자체를 제거하기보다, grid에 넣을 `RecipeDataPair` 목록을 만들 때 filtering하는 편이 좋다.

참고 패턴:

- MyLittleUI: recipe/resource/tooltip 문자열 cache
- ZenUI: item name, armor set, resource requirement 검색
- AAA_Crafting: page flip 때 filtered/sorted recipe list cache 재사용

### 5. Strong patch는 config로 보호

ZenUI 방식은 강력하지만 `UpdateRecipeList`, `SetRecipe`, `UpdateRecipe`, `Update`에 깊게 들어간다. InventorySlots에 crafting panel redesign을 넣는다면 다음 config가 있는 편이 좋다.

- crafting panel redesign enable
- recipe grid offset
- craft bar offset
- info tooltip offset
- recipe grid scroll sensitivity
- uncraftable icon brightness/alpha

UI 위치/시각 옵션은 client config, 실제 recipe availability나 station rule 변경은 server synced config가 적합하다.

## 결론

MyLittleUI는 vanilla crafting UI를 살리면서 기능을 얹는 모드라 검색, focus 처리, requirement 후처리, max craft 계산을 참고하기 좋다.

ZenUI는 crafting panel을 grid UI로 재구성하는 모드라 InventorySlots의 현재 crafting panel 목표에 더 직접적인 참고가 된다. 특히 `ContainerGrid` clone, `InventoryGrid` 상속, fake inventory item clone, scrollbar clone, tooltip anchor 재사용 구조는 InventorySlots가 vanilla 느낌의 8x7 recipe grid를 안정적으로 구현하는 데 가장 유용하다.

AAA_Crafting은 ZenUI보다 덜 침습적인 grid/pagination/search/multi-craft 참고 자료다. 특히 page wheel 조건, page cache, craft amount input, max craft helper는 InventorySlots에 바로 녹이기 좋다.

다만 ZenUI처럼 vanilla `UpdateRecipeList` 원본을 막는 방식이나 AAA_Crafting처럼 `DoCrafting`을 반복 호출하는 batch craft 방식은 충돌 위험이 크다. InventorySlots에서는 가능한 한 vanilla object를 이동/재사용하고, recipe grid와 queue state만 별도 layer로 구성하는 절충안이 안정성과 유지보수성 면에서 좋아 보인다.
