# QuickStackStore Analysis for InventorySlots

## Scope

This document reviews `QuickStackStore` as a reference for possible native InventorySlots features:

- quick stack
- restock
- store all / improved take all
- sort
- favorite / trash support
- inventory and container action buttons

Sources reviewed:

- GitHub repository: https://github.com/Goldenrevolver/QuickStackStore
- Cloned source: `C:\Users\blizz\RiderProjects\InventorySlots\reference\QuickStackStore-src`
- Commit reviewed: `c18a7b9` (`2025-03-08`, version `1.4.13`)
- Local DLL: `C:\Users\blizz\AppData\Roaming\com.kesomannen.gale\valheim\profiles\asdf\BepInEx\plugins\Goldenrevolver-Quick_Stack_Store_Sort_Trash_Restock\QuickStackStore.dll`
- Decompiled DLL output: `C:\Users\blizz\RiderProjects\InventorySlots\decompiled\QuickStackStore`

The GitHub README describes the mod as providing quick stack, restock, store all, sort, trash, and favorite-based protections for Valheim inventories.

## Current Compatibility Failure

The reported exception:

```text
NullReferenceException
QuickStackStore.ButtonRenderer.RepositionMiniButton(...)
QuickStackStore.ButtonRenderer+MainButtonUpdate.UpdateInventoryGuiButtons(...)
QuickStackStore.ButtonUIPatches+PatchInventoryGui.Show_Postfix(...)
```

is consistent with a UI anchor conflict.

QuickStackStore chooses its mini-button anchor like this in `Source/UI/ButtonRenderer.cs`:

```csharp
Transform anchor;
if (HasPlugin(currencyPockets))
{
    anchor = __instance.m_player.transform.Find("CoinPocketUI");
}
else
{
    anchor = __instance.m_player.transform.Find("Weight");
}
...
RepositionMiniButton(__instance, button.transform, anchor, ...);
```

`RepositionMiniButton()` then dereferences `anchor.localPosition` without a null guard. InventorySlots currently moves the vanilla `Weight` UI out of `InventoryGui.m_player` and into `InventorySlots_PlayerStatPanelHost`. If a container is opened while the inventory UI is already active, QuickStackStore's `Show` postfix can run while `m_player.Find("Weight")` returns null.

That explains why `S` may remain visible while `R` and `Q` disappear: the sort button may have already been created or shown in a previous pass, then the later reposition path aborts before restock and quick-stack mini buttons are created or positioned.

Important detail: `InventorySlots_` host objects are internal wrapper objects created by InventorySlots. For example, `InventorySlots_PlayerStatPanelHost` exists only to hold moved vanilla/compat UI beside the equipment panel. Compatibility scans should not treat those objects as foreign UI, or the scan can recursively move its own host.

## Module Map

| Area | Main files | What it does | Notes for InventorySlots |
| --- | --- | --- | --- |
| Plugin boot | `QuickStackStorePlugin.cs` | Loads sprites, config, localization, ServerSync, Harmony patches. Soft-depends on AzuEPI and MultiUserChest. | Native InventorySlots should not depend on QSS. Borrow ideas only. |
| UI buttons | `Source/UI/ButtonRenderer.cs`, `ButtonUIPatches.cs` | Clones vanilla `Take All` button for big buttons and mini buttons. Anchors mini buttons to `Weight`, `Armor`, or `CoinPocketUI`. | Good feature set, fragile anchoring. InventorySlots should render its own action panel instead. |
| Quick stack | `Source/Modules/QuickStackModule.cs` | Moves stackable non-favorited player items into current/nearby containers that already contain matching item names. | Good current-container logic. Area logic needs multiplayer caution. |
| Restock | `Source/Modules/RestockModule.cs` | Pulls matching items from current/nearby containers to refill existing player stacks. Can limit to ammo/consumables/favorites. | Strong candidate, but must understand InventorySlots quick/equipment/custom slots. |
| Store all / take all | `Source/Modules/StoreTakeAllModule.cs` | Stores non-favorited player items into current container; optionally improves chest take-all order. | Useful, low-risk if special slot filters are correct. |
| Sort | `Source/Modules/SortModule.cs` | Sorts by type category/name/value/weight, optionally merges stacks, respects favorites and special slots. | Useful. Must replace QSS special-slot detection with InventorySlots slot classifier. |
| Area containers | `Source/ContainerFinder.cs`, `AreaStackRestockHelper.cs` | Tracks all containers and scans range on action. In MP, uses ownership/in-use/ward/privacy checks. | Good reference, but area actions should be phase 2. |
| Favorites | `Source/Config/UserConfig.cs`, `InventoryGridButtonHandlingPatches.cs`, `FavoritingMode.cs` | Stores favorited slots/item names per player in a binary file. Uses click patches and borders. | Useful concept, but BinaryFormatter and slot-coordinate favorites are not ideal for InventorySlots. |
| Trash | `Source/Modules/TrashModule.cs` | Clones `Armor` UI into a trash can panel; deletes held or trash-flagged items. | Destructive and UI-heavy. Defer unless explicitly wanted. |
| Compatibility | `Source/Modules/CompatibilitySupport.cs` | Detects AzuEPI, Randy, ComfyQuickSlots, BetterArchery, MUC, Jewelcrafting, etc. | Replace with native InventorySlots slot knowledge. Avoid reflection where possible. |

## Feature Behavior Details

### Quick Stack

QuickStack only considers player items where:

- stack size is greater than one
- the item is not favorited by name or slot
- hotbar is included only if config allows it
- known equipment/quick-slot cells are excluded

For the current container, it scans existing container items and moves matching player items into that container. Matching is primarily by `m_shared.m_name`; restricted dynamic storage containers can use drop prefab names. It intentionally lets `Inventory.AddItem()` decide stack compatibility details.

Nearby container mode scans `ContainerFinder.AllContainers`, checks distance, ownership, wards, privacy, physical/non-physical container rules, and whether the container is already in use. Without MultiUserChest, it claims ownership and temporarily marks containers in use.

### Restock

Restock works in reverse:

- it only refills existing player stacks
- it excludes items with custom data
- it matches container items by internal shared name and quality
- it can limit targets to ammo/consumables
- it can require favorited items/slots
- it can cap desired stack size by config

This is safer than a generic "take matching item" because it does not create new inventory stacks. For InventorySlots, restock should probably be allowed for quick slots, but not for equipment slots, unless the slot definition explicitly marks itself restockable.

### Store All and Improved Take All

Store All moves player items to the current container in slot order. It excludes:

- hotbar unless enabled
- equipped items unless enabled
- favorited item names/slots
- known equipment/quick-slot cells

Improved Take All replaces vanilla `InventoryGui.OnTakeAll` for non-tombstone containers and moves items in order. InventorySlots already has tombstone safeguards, so any native Take All override must explicitly preserve tombstone behavior.

### Sorting

Sorting uses:

- type-category sort
- translated name
- internal name
- value
- weight

Ties are broken by internal name, quality, and stack size. Optional stack merging groups by internal name and quality, excluding custom-data stacks.

QSS directly reassigns `item.m_gridPos` after computing allowed slots. That is fast, but for InventorySlots it must never target:

- locked progressive rows
- equipment slots
- YAML custom equipment slots
- quick slots, unless a feature explicitly targets quick slots
- hidden/non-visible UI-only cells

The safe native approach is to build allowed positions from InventorySlots' own slot classifier rather than infer from inventory height.

### Favorites

QSS supports two favorite concepts:

- favorited item names
- favorited grid slots

It stores them in `QuickStackStore_player_<playerId>.dat` with `BinaryFormatter`.

For InventorySlots, BinaryFormatter should not be copied. A safer native design would use:

- JSON/YAML in BepInEx config folder, or
- player custom data for character-bound state, depending on whether favorites should travel with the character.

Slot favorites are trickier with progressive rows and custom slots. If implemented, favorites should key by a stable logical cell id, not only `(x,y)`.

### Trash

Trash is intentionally destructive. QSS:

- clones the `Armor` UI as a trash can
- trash-flags item names
- quick-trashes all trash-flagged inventory items
- optionally prevents auto-pickup of trash-flagged items through a Player.AutoPickup transpiler

This should not be part of the first native port. It has the highest "oops, my item is gone" risk and adds several unrelated UI/tooltip paths.

## UI Compatibility Lessons

QuickStackStore's UI works by cloning existing vanilla objects:

- big container buttons are cloned from `m_takeAllButton`
- mini inventory buttons are cloned from `m_takeAllButton` and parented under `m_player`
- trash can is cloned from `m_player/Armor`
- mini buttons use `m_player/Weight` as anchor

This is the exact pattern that conflicts with InventorySlots moving armor/weight panels. If InventorySlots keeps offering QSS compatibility, there are three possible approaches:

1. Do not reparent vanilla `Armor` and `Weight`; only adjust their local position under `m_player`.
2. Leave proxy anchors named `Armor` and `Weight` under `m_player` when moving the real panels.
3. Stop supporting external QSS UI and provide native InventorySlots buttons.

The third option is best long term. The first option is simplest if external compatibility must remain.

## Recommended Native InventorySlots Design

### Phase 1: Safe Current-Container Actions

Implement first:

- sort player inventory
- sort current container
- store all to current container
- quick stack to current container
- restock from current container

Avoid first:

- area quick stack/restock
- trash
- auto-sort on open
- controller D-pad patches
- external compatibility reflection

### Phase 2: Optional Area Actions

Add later:

- nearby quick stack
- nearby restock
- server-synced area range
- server-synced permission for multiplayer without MultiUserChest
- container privacy/ward/in-use checks
- ship/container exclusions

Area actions are useful, but they are the only part that materially touches multiplayer safety.

### Phase 3: Favorites

Add later if needed:

- favorite item prefab/internal names
- favorite regular inventory cells
- optional favorite quick slots
- visual border overlay

For InventorySlots, favorite storage should use stable ids:

- regular inventory: `regular:x:y`
- hotbar: `hotbar:x`
- quick slot: `quick:index`
- equipment/custom slot: `slot:<slotId>`

### Phase 4: Trash

Only add if explicitly wanted. Recommended default: disabled. If added, require confirmation and never affect equipment/custom/quick slots unless explicitly configured.

## InventorySlots Slot Classifier Needed

Before porting any logic, InventorySlots should expose one internal classifier:

```csharp
internal enum InventoryCellKind
{
    Hotbar,
    RegularUnlocked,
    RegularLocked,
    Equipment,
    CustomEquipment,
    Quick,
    Outside
}
```

Every action should call the same classifier:

- quick stack can use `RegularUnlocked`, optionally `Hotbar`
- restock can use `RegularUnlocked`, optionally `Hotbar` and `Quick`
- sort can use `RegularUnlocked`, optionally `Hotbar`
- store all can use `RegularUnlocked`, optionally `Hotbar`
- take all should target regular inventory first and never force items into equipment/custom slots

This avoids the main weakness in QSS compatibility code: trying to infer special slots from third-party inventory height/row conventions.

## Button UI Recommendation

Do not clone QuickStackStore's button layout directly. InventorySlots already controls the equipment/quick/custom panels, so it should own a dedicated action strip.

Recommended layout:

- Player-side mini actions near equipment panel:
  - `S`: sort player inventory
  - `R`: restock from nearby/current
  - `Q`: quick stack to nearby/current
  - optional favorite toggle later
- Container-side actions near container panel:
  - `Q`: quick stack to current container
  - `Store`: store all
  - `R`: restock from current container
  - `S`: sort container

Use InventorySlots-created anchors, not `m_player.Find("Weight")`. This avoids the current null anchor failure completely.

## Config Recommendation

Server-synced:

- enable/disable container actions
- area quick stack/restock range
- allow area actions in multiplayer without MultiUserChest
- allow non-player-built containers
- allow non-physical containers
- suppress container sounds/visuals during area scan

Client-only:

- action button visibility
- action button positions
- hotkeys
- labels
- result message visibility

Potentially per-character/client:

- favorites
- trash flags

## Code Worth Borrowing

Good ideas to adapt:

- QSS current-container quick-stack matching
- QSS restock `RestockData` approach
- QSS sort comparison and category map
- QSS container range and ward/privacy checks
- QSS distinction between current container buttons and mini inventory buttons
- QSS result messages

Code to avoid copying directly:

- `m_player.Find("Weight")` / `m_player.Find("Armor")` UI anchoring
- direct assumptions about third-party equipment rows
- `BinaryFormatter` favorite storage
- destructive trash by default
- direct grid-position sorting without InventorySlots-aware allowed cells
- broad controller transpilers during the first native implementation

## Migration Strategy

If InventorySlots implements native QSS-like features, it should eventually declare QuickStackStore incompatible or warn when both are installed. Running both will duplicate hotkeys, buttons, container actions, and sort/stack mutations.

Short-term coexistence fix, if still desired:

- do not remove `Weight` from `m_player`; keep it as a valid anchor
- or create a stable proxy `Weight` RectTransform under `m_player`
- or delay moving stat panels until after QuickStackStore creates and positions mini buttons, then reparent the mini buttons as well

Long-term native implementation is cleaner and more stable.

