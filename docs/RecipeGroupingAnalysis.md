# Recipe Grouping Analysis: MyLittleUI / ZenUI / AAA_Crafting

작성일: 2026-05-09

## Scope

이 문서는 아래 세 모드의 recipe grouping / category / filtering 관련 코드만 분석한다.

- `Libs/MyLittleUI.dll`
- `Libs/ZenUI.dll`
- `Libs/AzuAntiArthriticCrafting.dll`

참고 디컴파일 경로:

- `decompiled/MyLittleUI`
- `decompiled/ZenUI`
- `decompiled/AzuAntiArthriticCrafting`

## Summary

MyLittleUI, ZenUI, AAA_Crafting은 모두 recipe를 종류별로 줄이거나 나누지만 목적과 구현 방식이 다르다.

| 모드 | 구조 | UI 의미 | Recipe 처리 |
| --- | --- | --- | --- |
| MyLittleUI | `FilteringPanel` + `FilteringState` | 기존 recipe list 옆에 category filter icon 추가 | 선택된 filter에 맞지 않는 recipe를 임시 제거하고 정렬 |
| ZenUI | `Recipe -> CraftGroup` dictionary | 왼쪽 recipe list를 group list로 교체 | recipe를 실제 group에 배정하고 선택 group만 grid에 표시 |
| AAA_Crafting | `ItemTypeMask` + advanced filter overlay | paged recipe grid/list 위에 quick filter와 검색 추가 | recipe list를 filter/sort/search한 뒤 pagination 적용 |

InventorySlots의 crafting grid에는 ZenUI의 group model이 더 직접적으로 맞는다. MyLittleUI는 recipe list를 유지하면서 빠르게 필터링하는 구조라, icon grid 기반 crafting panel에서는 category/filter 조건식과 sort 기준만 참고하는 편이 좋다. AAA_Crafting은 실제 group model보다는 pagination 전 단계의 search/filter/sort pipeline과 item type mask가 참고 가치가 크다.

## MyLittleUI Grouping

관련 파일:

- `MyLittleUI/CraftSort.cs`

MyLittleUI의 grouping은 실제 recipe group을 만드는 구조가 아니라, filter icon category를 만든 뒤 현재 선택한 filter에 맞는 recipe만 남기는 방식이다.

### Core Types

`FilteringPanel`

- 하나의 category panel이다.
- 예: `Food`, `Armor`, `Skills`, `Bows`, `Crossbows`, `Magic`, `Tools`
- 내부에 여러 `FilteringState`를 가진다.
- panel UI는 `InventoryGui.instance.m_repairPanel`을 복제해서 만든다.
- filter icon은 3열 단위로 배치된다.

`FilteringState`

- 실제 filter 하나를 의미한다.
- 예: `armor_helmet`, `food_stamina`, `skill_bows`, `tools_material`
- `filter: Func<ItemData, bool>`로 recipe item이 해당 filter에 들어가는지 판단한다.
- `sort: Comparison<RecipeDataPair>`로 해당 filter 선택 시 recipe 정렬 기준을 제공한다.
- `unique` 기본값이 `true`라 하나의 filter를 켜면 다른 filter는 꺼진다.

### 생성되는 Category

MyLittleUI는 `InitSortingPanel()`에서 아래 순서로 filter panel을 만든다.

| Panel | Filter |
| --- | --- |
| Food | health food, stamina food, eitr food |
| Armor | helmet, chest armor, legs, cape, utility, trinket |
| Skills | melee skill weapons, shields |
| Bows | bow weapons, arrows |
| Crossbows | crossbows, bolts |
| Magic | elemental magic, blood magic |
| Tools | tools/crafting, consumables, materials, fishing, misc, Jewelcrafting item |

Jewelcrafting이 설치되어 있으면 `tools_jewelcrafting` filter가 추가된다. 조건은 대략 `item.m_shared.m_name.StartsWith("$jc_")`이며 utility item type은 제외한다.

### Filter 조건

대표 조건:

- Food: item type food이면서 health/stamina/eitr 값이 있거나, material에 `appendToolTip`으로 food stat이 붙은 경우
- Armor: `m_itemType` 또는 `m_attachOverride`가 helmet/chest/legs/cape/utility/trinket에 해당하는 경우
- Skills: `m_skillType`과 attack animation/damage를 기준으로 weapon 계열 구분
- Bows/Crossbows: skill type과 ammo type으로 bow/arrow/bolt 구분
- Magic: elemental/blood magic skill type
- Tools:
  - tool item type
  - bomb throw animation
  - building/crafting 관련 skill type
  - mead ammo type
  - consumable status effect
  - plain material
  - fishing rod/bait/fishing hat
  - misc fallback

### Filtering 흐름

패치 대상:

- `InventoryGui.UpdateRecipeList` prefix

흐름:

1. `FilterRecipes(recipes, inCraftTab, inUpgradeTab)` 실행
2. 각 `FilteringState`가 현재 recipe 목록에서 선택 가능한지 계산
3. 선택 가능한 filter icon만 표시
4. 활성화된 filter가 있으면 recipe list에서 해당 filter에 맞지 않는 recipe를 제거
5. vanilla `UpdateRecipeList`가 남은 recipe로 list UI를 만든다
6. postfix에서 `SortRecipes()`를 실행해 `m_availableRecipes`를 정렬
7. `RecipeDataPair.InterfaceElement`의 `anchoredPosition`을 다시 잡아 vanilla list 순서를 맞춘다

업그레이드 탭에서는 다음 조건을 추가로 본다.

- recipe item의 max quality가 1보다 커야 함
- 플레이어 인벤토리에 해당 item을 가지고 있어야 함

### Sorting

활성 filter가 있을 때만 정렬이 바뀐다.

주요 sort 함수:

- `ByDefault`: recipe list sort weight, item type, localized name
- `ByName`: localized item name
- `ByArmor`: armor, max durability, name
- `SortCape`: armor, durability, name
- `SortShield`: block power, timed block bonus, name
- `ByTotalDamage`: total damage
- `ByAdrenaline`: max adrenaline, name

여러 filter가 동시에 활성화될 가능성은 코드상 열려 있지만, `FilteringState.unique` 기본값이 `true`이고 `unique = false`로 설정한 filter가 보이지 않아 실제 사용은 거의 단일 filter 방식이다.

### 장점

- vanilla recipe list를 유지하므로 구조가 보수적이다.
- category 조건이 세분화되어 있어 item classification 참고 가치가 높다.
- filter icon UI가 현재 recipe 목록에 맞춰 자동으로 표시/숨김된다.
- upgrade tab까지 고려한다.

### 한계

- 실제 recipe group model이 아니라 “filter”다.
- 한 그룹을 선택해서 grid에 보여주는 구조와는 맞지 않는다.
- `InterfaceElement` 위치를 재배치하는 방식이라 icon grid에서는 재사용성이 낮다.
- filter 상태가 UI와 recipe list에 강하게 결합되어 있다.

## ZenUI Grouping

관련 파일:

- `ZenUI.Section/CraftingPanel.cs`
- `ZenUI/Configs.cs`

ZenUI는 recipe를 실제 `CraftGroup`에 배정하고, 왼쪽 recipe list를 group selector로 바꾼다. 선택한 group의 recipe만 오른쪽 icon grid에 표시한다.

### CraftGroup

ZenUI의 group enum:

- `Everything`
- `Weapons`
- `Ammo`
- `Armor`
- `Shields`
- `Magic`
- `Tools`
- `Equipment`
- `Resources`
- `Food`
- `Trophy`
- `Misc`

### Default Group Mapping

`CraftGroups` dictionary는 group마다 대표 icon prefab과 item type set을 가진다.

| Group | Icon prefab | 기본 기준 |
| --- | --- | --- |
| Everything | `Wisp` | 모든 item type |
| Weapons | `Battleaxe` | weapon 계열 item type |
| Ammo | `ArrowIron` | ammo 계열 item type |
| Armor | `HelmetCarapace` | helmet/chest/legs/cape 계열 item type |
| Shields | `ShieldIronBuckler` | shield item type |
| Magic | `StaffFireball` | magic weapon item type |
| Tools | `PickaxeIron` | tool/utility/misc tool 계열 item type |
| Equipment | `chest_hildir1` | equipment/customization 계열 item type |
| Resources | `piece_chest_barrel` | material item type |
| Food | `CookedMeat` | food/consumable 계열 item type |
| Trophy | `TrophyDeer` | trophy item type |
| Misc | `BoneFragments` | none/misc fallback |

디컴파일 결과 item type은 숫자로 보이지만, icon prefab과 주변 조건상 위와 같은 의도로 분류되어 있다.

### Explicit Group Assignment

ZenUI는 item type만으로 부족한 recipe를 prefab name pattern으로 보정한다.

기본 explicit assignment:

| Group | Pattern |
| --- | --- |
| Ammo | `Catapult_ammo`, `BombSiege` |
| Tools | `Pickaxe*`, `Scythe`, `FishingRod`, `Tankard*`, `KnifeButcher` |
| Resources | `FishingBait*` |
| Magic | `Staff*` |
| Food | `Feast*` |

`InitExplicitGroupConfigs()`는 각 group별 config를 만든다.

Config section:

- `Crafting Group Assignment`

지원 문법:

- comma-separated prefab names
- prefix wildcard: `Pickaxe*`
- suffix wildcard: `*Something`
- explicit removal: `-PrefabName`
- removal wildcard는 지원하지 않음

### Assignment Algorithm

`AssignRecipesToCraftGroups()`:

1. `ObjectDB.instance.m_recipes`를 순회한다.
2. `recipe.m_item`이 있는 recipe만 대상으로 한다.
3. 각 recipe에 `AssignRecipeToCraftGroups(recipe)`를 적용한다.
4. 결과는 `Dictionary<Recipe, HashSet<CraftGroup>> RecipeToGroups`에 저장한다.

`AssignRecipeToCraftGroups(recipe)`:

1. item prefab name을 얻는다.
2. item type을 얻는다.
3. 항상 `Everything` group에 추가한다.
4. 각 group에 대해 explicit removal이면 건너뛴다.
5. 현재 prefab이 explicit assignment pattern 중 하나와 매칭되면 item type mapping을 쓰지 않고, explicit matching group에만 추가한다.
6. explicit pattern과 매칭되지 않으면 item type mapping으로 group을 배정한다.
7. 어떤 group에도 들어가지 않으면 `Misc`에 추가한다.

중요한 점:

- explicit assignment는 단순 추가가 아니라 override 성격이 있다.
- 어떤 explicit pattern이 prefab에 매칭되면 기본 item type group 대신 explicit group만 사용한다.
- 그래도 `Everything`은 항상 붙는다.

### ActiveGroups

`InventoryGui.UpdateRecipeList` prefix에서 현재 이용 가능한 recipe들을 처리할 때:

1. `m_availableRecipes`를 직접 비운다.
2. recipe마다 craft 가능 여부를 계산한다.
3. `InventoryGui.instance.AddRecipeToList(...)`로 `RecipeDataPair`를 채운다.
4. `ActiveGroups.UnionWith(RecipeToGroups[recipe])`로 현재 UI에 표시할 group을 모은다.

`ActiveGroups.Count == 2`이면 `Everything`을 제거한다.

의도:

- 실제 group이 하나뿐이면 굳이 `Everything`을 보여주지 않는다.
- 여러 group이 있으면 `Everything`도 선택지로 남긴다.

### Group UI

`UpdateCraftingGroupsUI()`:

- vanilla `m_recipeElementPrefab`을 복제해서 왼쪽 recipe list root에 group item을 만든다.
- group 이름은 `$CraftGroup_{group}` localization key를 쓴다.
- icon은 `CraftGroupItemTypes.InitIcons()`에서 prefab icon으로 가져온다.
- 클릭하면 `SelectGroup(group)`를 호출한다.
- 선택 표시에는 vanilla recipe element의 `selected` child를 재사용한다.

즉 ZenUI에서는 왼쪽 recipe list가 recipe list가 아니라 group selector가 된다.

### ActiveRecipes

오른쪽 grid에 표시되는 recipe는 `ActiveRecipes`에서 나온다.

조건:

- 현재 선택된 group에 속해야 함
- search filter를 통과해야 함
- `SortCraftRecipes()` 결과 순서대로 표시

이후 recipe item clone을 fake inventory에 넣고 grid 좌표를 부여한다.

### Sorting Inside Group

ZenUI는 group 내부 sorting을 꽤 촘촘하게 한다.

정렬 순서:

1. craftable first, config가 켜진 경우
2. craft group order
3. skill type order
4. attack animation order
5. item type order
6. ammo type order
7. damage order
8. armor order
9. shield order
10. food type/stat order
11. consume status effect category
12. attack status effect category
13. equip status effect category
14. crafting station name
15. localized item name

참고할 점:

- Weapons/Magic은 attack animation과 skill type을 보정해서 비슷한 무기끼리 묶는다.
- Ammo는 ammo type 기준이 들어간다.
- Food는 health/stamina/eitr 성격을 반영한다.
- Armor/Shields는 실제 stat 기반으로 정렬한다.

### Cache / Refresh

ZenUI는 recipe group map을 `RecipeToGroups`에 cache한다.

재분류 조건:

- `ObjectDB.instance.m_recipes.Count`가 이전과 다르면 `AssignRecipesToCraftGroups()`를 다시 실행한다.

주의:

- recipe 개수가 그대로인 상태에서 다른 모드가 recipe item type이나 prefab assignment만 바꾸면 cache가 늦게 갱신될 수 있다.
- group assignment config의 `SettingChanged`는 pattern set을 갱신하지만, 디컴파일된 코드상 즉시 `RecipeToGroups` 전체를 rebuild하는 호출은 보이지 않는다. 실제 live update를 기대하기보다는 UI refresh 또는 재접속/재시작 전제에 가깝게 보는 것이 안전하다.

## AAA_Crafting Filtering / Grouping

관련 파일:

- `AzuAntiArthriticCrafting.Patches.Filtering/FilterManager.cs`
- `AzuAntiArthriticCrafting.Patches.Filtering/AdvancedFiltersOverlay.cs`
- `AzuAntiArthriticCrafting.Patches/PaginatorPatches.cs`
- `AzuAntiArthriticCrafting.Patches/RecipeListPerfCache.cs`

AAA_Crafting은 ZenUI처럼 recipe를 영구적인 group에 배정하지 않는다. 대신 `Player.GetAvailableRecipes` 결과를 search, quick filter, advanced filter, sort 순서로 줄인 뒤 page 단위로 잘라서 recipe grid/list에 넘긴다. 따라서 구조적으로는 grouping이라기보다 filtered paged list에 가깝다.

### Basic Item Type Mask

`FilterManager`는 recipe 결과 아이템의 `ItemDrop.ItemData.SharedData`를 보고 bitmask category를 계산한다.

- Undefined
- Food
- Material
- Bow
- Armor
- Ammo
- Weapon
- Shield
- Tools
- All

분류는 비교적 보수적이다. food는 consumable과 food stat을 보고, weapon은 `IsWeapon()`을 보고, armor/shield/ammo/tool은 vanilla item type과 일부 numeric range를 본다. MyLittleUI나 ZenUI처럼 prefab name pattern으로 세밀하게 보정하지는 않는다.

### Advanced Filters

`AdvancedFiltersOverlay`는 basic category보다 훨씬 세부적인 조건을 제공한다.

- required crafting station
- required station level
- armor value
- block power
- damage type
- food health/stamina/eitr
- recipe craftability

기능은 강하지만 overlay UI와 state가 크다. InventorySlots에서는 당장 가져오기보다는, 나중에 recipe filter를 확장할 때 조건식만 참고하는 편이 안전하다.

### Search Syntax

`PaginatorPatches`는 검색 문자열에 prefix 의미를 둔다.

- 일반 문자열: localized item name, prefab name, description 검색
- `!term`: recipe 결과 아이템 또는 requirement item 검색
- `@mod`: prefab을 로드한 assembly/mod 이름 검색

InventorySlots가 recipe grouping을 YAML 또는 config로 확장한다면, 이 search prefix 방식은 group selector 없이도 원하는 recipe를 빠르게 찾는 보조 기능으로 유용하다.

### Pagination Cache

AAA_Crafting의 가장 실용적인 부분은 grouping 자체보다 page cache다.

- search/filter/sort 결과 전체를 cache한다.
- 단순 page flip이면 cached list를 재사용한다.
- recipe list 영역 위에서 wheel을 굴릴 때만 page를 바꾼다.

InventorySlots도 8x7 grid에서 page 단위 wheel 이동을 쓰고 있으므로, recipe data cache와 page-only refresh flag를 분리하는 패턴은 그대로 적용할 가치가 있다.

### 장단점

장점:

- 실제 group model이 아니라서 implementation이 가볍다.
- recipe list를 page 단위로 다룰 때 performance 부담이 작다.
- search/filter/sort/pagination의 순서가 명확하다.

단점:

- group selector UX는 제공하지 않는다.
- item type mask가 단순해서 modded recipe의 분류 정확도는 ZenUI보다 낮을 수 있다.
- advanced overlay까지 가져오면 UI 복잡도가 급격히 오른다.

## InventorySlots에 대한 적용 제안

### 1. Crafting grid에는 ZenUI식 `Recipe -> Group` 모델이 더 적합

InventorySlots가 8x7 recipe icon grid를 쓰려면 recipe 목록을 group 단위로 먼저 나누고, 선택 group의 recipe만 fake grid에 넣는 구조가 깔끔하다.

권장 data model:

```csharp
enum CraftRecipeGroup
{
    Everything,
    Weapons,
    Ammo,
    Armor,
    Shields,
    Magic,
    Tools,
    Equipment,
    Resources,
    Food,
    Trophy,
    Misc
}

Dictionary<Recipe, HashSet<CraftRecipeGroup>> recipeGroups;
SortedSet<CraftRecipeGroup> activeGroups;
```

### 2. MyLittleUI의 filter 조건식은 보조 참고로 좋음

MyLittleUI의 장점은 Valheim item data를 섬세하게 읽는 조건식이다.

가져올 만한 조건:

- armor는 `m_itemType`뿐 아니라 `m_attachOverride`도 확인
- food는 `m_appendToolTip` food stat도 확인
- fishing, mead, material, consumable, Jewelcrafting 같은 특수 케이스
- misc fallback을 따로 두는 방식

### 3. AAA_Crafting의 search/filter/page pipeline은 grid refresh에 유용

InventorySlots가 실제 group model을 쓰더라도, 최종 grid에 표시할 recipe list는 아래 순서로 만드는 편이 좋다.

1. 전체 known/available recipe 수집
2. ZenUI식 group assignment 적용
3. 선택 group filter 적용
4. AAA_Crafting식 search/filter/sort 적용
5. page 단위로 잘라 grid cell에 배치

이렇게 나누면 group 선택과 검색이 서로 엉키지 않고, page flip은 cached filtered list에서 index만 바꾸는 식으로 가볍게 처리할 수 있다.

### 4. Explicit group override는 InventorySlots에도 유용

Modded recipe가 item type만으로 잘못 분류될 가능성이 높기 때문에 prefab pattern 기반 override가 있으면 좋다.

권장:

- YAML 또는 config에서 group assignment 지원
- `Pickaxe*` 같은 wildcard 지원
- `-PrefabName` 제거 지원
- server synced config로 운영

단, ZenUI처럼 “explicit pattern이 매칭되면 기본 item type 분류를 완전히 override”할지, 아니면 “기본 group + explicit group 추가”로 할지는 명확히 정해야 한다.

추천은 기본값을 conservative하게 두는 것이다.

- 기본: item type group + explicit group 추가
- 옵션: explicit assignment overrides default group

이렇게 하면 사용자가 실수로 pattern을 추가했을 때 recipe가 예상 밖의 group에서 사라지는 위험이 줄어든다.

### 5. Group cache rebuild 조건은 더 넓게 잡는 것이 좋음

ZenUI는 recipe count 변화만 주로 본다. InventorySlots는 다음 상황에서 group map을 rebuild하는 편이 안전하다.

- ObjectDB recipe count 변경
- config/YAML group assignment 변경
- language/localization 변경은 sorting name cache만 갱신
- known recipes 변경
- crafting station 변경
- modded recipe registration 이후 첫 InventoryGui open

### 6. UI 관점 추천

InventorySlots의 crafting panel에는 다음 구조가 가장 자연스럽다.

- 왼쪽 또는 상단 좁은 영역: group selector icon list
- 오른쪽/중앙: 8x7 recipe icon grid
- 하단 8x1: craft button, count input, station level, requirements
- hover/click item info HUD는 선택 recipe 기준

ZenUI처럼 vanilla recipe element를 group selector로 재사용하면 localization, selected state, button sound를 쉽게 얻을 수 있다. 다만 사용자가 원하는 icon grid 중심 UI라면 group selector도 작은 icon-only tab처럼 별도 구현하는 편이 화면 효율은 더 좋다.

## 결론

MyLittleUI의 recipe grouping은 실질적으로 category filter다. vanilla recipe list를 유지하면서 특정 조건에 맞는 recipe만 보여주고 정렬하는 구조라 안정적이지만, InventorySlots의 icon grid redesign에는 직접 맞지 않는다.

ZenUI는 recipe를 실제 group에 배정하고, 선택 group만 grid에 보여주는 구조다. InventorySlots가 crafting panel을 본격적으로 재구성한다면 ZenUI의 `RecipeToGroups`, `ActiveGroups`, explicit prefab assignment, selected group grid update 구조를 참고하는 것이 가장 좋다.

AAA_Crafting은 group model 자체보다는 search/filter/sort/pagination pipeline 참고 가치가 크다. 특히 page flip cache와 recipe grid 안에서만 wheel page 전환을 허용하는 구조는 InventorySlots의 8x7 grid에 잘 맞는다.

다만 ZenUI의 cache rebuild 조건과 explicit override 동작은 InventorySlots에서 조금 더 안전하게 다듬는 편이 좋다. 특히 YAML hot reload와 modded recipe 대응까지 생각하면 group map rebuild를 명시적으로 호출할 수 있는 구조가 필요하다.
