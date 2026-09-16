# Crafting Grid/List views

Implemented on main after `83ba1cf` (2026-09-16), included in InventorySlots 1.5.0. Game support and InventoryActions are unchanged.

## User behavior

- The Grid/List button to the right of the G/T sort buttons changes the view live and remembers the last choice locally. It shares their height and 4-unit spacing. Default is `Grid`. The existing `5 - Client UI` / `Crafting View Mode` config key remains for persistence and existing selections, but is hidden from ConfigurationManager/F1 via `Browsable = false`.
- List reuses the filtered/sorted recipe model and click/favorite handlers. It shows 14 rows with names/icons on the left and selected-item details on the right. Long descriptions scroll independently; list scrolling does not select another recipe.
- The List recipe scrollbar is 8 UI units wide; its detail scrollbar is 4. Grid keeps its 16-unit recipe scrollbar. Screen pixel widths depend on UI scaling.
- The recipe list stays 196 UI units wide. Its 8-unit scrollbar has equal 2-unit gaps to the list and detail panel. The detail panel extends to the same right edge and retains its inner text padding.
- When the selected item has Jewelcrafting gems, the detail header shows their icons using the same gem data and icon renderer as Grid. The body moves below the icon row; items without gems reserve no space for it. These compact icons do not open an automatic hover popup.
- The detail viewport receives pointer raycasts so Unity's ScrollRect handles wheel and body dragging. This is enabled only for the List detail pane; passive hover tooltips retain their existing input behavior.
- Craft, Upgrade, Jewelcrafting's Socket tab and Recycle N Reclaim's Reclaim tab use the same selected view and live toggle. Foreign crafting panels such as EpicLoot/VNEI retain their existing layouts.
- Socket/Reclaim reuse their existing adapters for selection, item descriptions, sorting, action availability and bottom controls. Socket costs/risk warnings and Reclaim return materials/blocking reasons stay below the list/detail area. List row text uses the same availability as its background, including socket affordability and Reclaim impediments. Changing adapter refreshes the selected details even if an item/recipe is reused.
- Search, group filter, recipe favorites, selected recipe/style, count, bottom controls and crafting queue are shared. Switching views does not call the teardown path that clears the queue. Grid size is retained for returning to Grid.
- List suppresses the automatic recipe hover popup; hovering any row and pressing the pin key still pins that recipe without selecting it or changing the right-side details. Hover targets are refreshed after scrolling under a stationary pointer. Style selection moves from the grid cell to the detail header.

## Implementation boundaries

- `CraftingViewCore.cs`: mode eligibility and scroll-window calculations, source-linked into the tests.
- `CraftingListView.cs`: owned view button/detail objects, list-row presentation, localization and independent detail scrolling. Selection and localization changes refresh immediately; unchanged selected details are rechecked at most four times per second. Gem data is cached separately from the transient hover overlay and refreshed on selection, localization or item-data signature changes, using the existing Grid signature. The cache clears on empty selection and is released with the detail UI. Text layout is recalculated only when content/selection/localization/gems change. This is not a measured performance claim.
- Existing grid, scrollbar, hover and pinned-cell mapping use the current view's window start. Grid retains its existing paging behavior. List reveals a selected row after a selection/filter/view change while retaining a visible window where possible.
- The new UI roots are recognized by the existing ownership checks, including the vanilla scrollbar suppression pass. The roots follow InventoryGui destruction and plugin shutdown. The setting handler is unsubscribed on shutdown.
- Original 1.0.12 `InventoryGui.GetSelectedRecipeIndex(bool)` is **private instance int**. The existing safe selector now uses a cached Harmony delegate; List requests the original `false` behavior and previous callers retain `true`. `InventoryGui.OnDestroy` is a private, parameterless instance method targeted by a cleanup Prefix.
- Existing compiler publicizer/ILRepack settings remain. Original installed game DLLs are used for API validation; compiler-publicized copies are not runtime evidence. No networking, item mutation, save schema, optional dependencies or InventoryActions code changes are part of this feature.

## Verification

- Baseline Debug build and 167 existing checks passed before the new view tests.
- Debug build with `DeployToGame=true`: final merged InventorySlots.dll copied to the Steam BepInEx/plugins folder; source/deployed SHA-256 checked.
- Automated suite: 171 checks, including List adapter boundaries, complete scroll coverage/no blank tail, minimal selection reveal, and stable filtered recipe identity across Grid/List transitions.
- Original-DLL contract checker covers compiled references/Harmony targets and the cached selected-index accessor. Reports and build/test output are in ignored `artifacts/crafting-list-*` files. Optional/dynamic targets listed for manual review by that tool are not runtime-tested.
- Optional-mod extension was compared with the supplied original DLLs: Recycle N Reclaim 1.4.4 (`454415FE539D79DCCB8B102D3138B96EE7567BB41224A9B5A4B27CC6A776B366`) and Jewelcrafting 2.0.9 (`FD5E9D8C27E5A29C31E476B06F5E91447E9DCF5C2A79126D0CBE6062FD195FC8`), from Gale profile `asdfasdf`. Versions are the BepInPlugin versions, hashes are SHA-256. Bounded decompilation of `StationRecyclingTabHolder` and `GemStones` confirmed native recipe/item selection and status paths. All 13 checked reflection member contracts passed against their unchanged metadata; this does not certify the mods' full game compatibility.
- No Unity game, host/dedicated or optional-mod UI session was launched. Automated geometry/model and metadata checks do not verify rendering, input event order or detour execution.

## Remaining game checks

1. Switch both ways in Craft/Upgrade with search, a category and favorites active; check the selected recipe, count and retained Grid zoom. Repeat during queued crafting and cancellation.
2. Click/list-scroll/drag scrollbar through a long list. Selection should stay put while scrolling. Select A, hover B and press the pin key: pin B while the right-side details remain A and no automatic hover popup appears. Scroll under the stationary pointer and pin the newly visible row; move outside the list and check there is no stale pin target. Scroll a long right-side description without moving the list; try controller scrolling and pinned comparisons.
3. Select different styles, unavailable recipes, veiled recipes, upgrade gear and long translated/modded names. With Jewelcrafting, select gear with several gems, gear with no gems, then gemmed gear again. Change its gems, switch tabs/views and check that icons refresh, disappear when empty and leave room for the scrollable body. Clear a search with no results. Check the detail icon/name/body and lower requirements update together.
4. Close/reopen the inventory, switch stations and leave/re-enter the world. Test UI scales/resolutions and a variant dialog above the new detail panel.
5. In Reclaim and Socket, switch Grid/List with an item selected, scroll and pin a hovered row, then perform the native action. Verify refreshed/removed items and preserved return materials, blocked-recycling reasons, socket costs and failure warnings. Repeat with no eligible items and with insufficient materials. Verify gem cutting's existing warning controls in Craft as well.
6. With optional mods installed, visit VNEI and EpicLoot's Enchant/augment/result dialogs, then return to Craft/Upgrade/Socket/Reclaim. Foreign layouts should be preserved and the configured supported view restored.
