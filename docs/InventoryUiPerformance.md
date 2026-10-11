# Inventory UI refresh boundaries

The October 2026 review uses the original Valheim 1.0.16 Windows client
(Steam build 25527674). In that build, `InventoryGui.UpdateRecipe` runs every
visible frame; `UpdateCraftingPanel` is a structural refresh. Inventory validation
and item operations are not skipped. The closing-layout fix below also skips
the final native visual refresh after Hide.

## Changes

- Quick-slot HUD anchor capture waits for both the custom intro and the native
  `inventory_show` animation to finish. Closing, transitions, and rapid reopen
  cannot persist intermediate positions. A changed settled anchor still saves;
  drag commit and configuration changes retain their existing save paths.
  Computing the target corner no longer temporarily moves the panel or allocates
  a corner array on every capture.
- Sort/trash buttons remain active across refreshes. Slot parents and stat
  sibling order are changed only when needed. Backgrounds reserve the stat host's
  index, preventing the two layout routines from repeatedly moving each other.
  Stat restoration waits until the closing inventory root is inactive.
- Wheel hint sprites and constrained guide overflow modes are no longer reset
  before their final value is applied each frame.
- The existing crafting fast path is used for idle vanilla Craft/Upgrade frames.
  Structural changes still refresh fully. Visible availability, selection,
  variant, search focus, view, page, requirements, pins, and screen dimensions
  participate in invalidation. Dirty state, scroll input, crafting queues, and
  foreign adapters retain the full path. Scroll input is checked without consuming
  it, preventing a fast-path attempt from applying the same input twice.
  Progress cleanup, requirements, bottom controls, tooltip updates, and optional
  Jewelcrafting socket UI suppression continue on reused frames.

## Closing layout preservation

The 2026-10-11 fix checks the installed original Valheim 1.0.17 client.
`InventoryGui.Update` caches visibility before handling Tab, so its Hide frame
can still run `UpdateInventory` and `UpdateRecipe`. Restoring vanilla crafting
and stat layouts immediately in Hide exposed them during the remaining slide.
The mod now retains those layouts until `m_inventoryRoot` is inactive and skips
the final native inventory/recipe visual tick and its own layout refresh.
Queue cancellation, pins, hover and text focus are cleared immediately. A
root-local CanvasGroup temporarily blocks pointer input without changing alpha
or interactable colors; its prior raycast state is restored on completion,
reopening, GUI replacement/destruction or plugin shutdown. Show cancels pending
cleanup before native crafting initialization, including rapid reopening.

The original `SoftRef/Bundles/d59cfac` contains the IngameGui prefab and inventory
animation clips (SHA-256
`b8b5e179949f81f914a7ddcaf2381142175f2a0a6fbd2e2476456017da0beb89`).
UnityPy 1.25.3 inspection confirms `m_inventoryRoot` references GameObject `root`.
`inventory_hide` animates `root.m_IsActive` from 1 at 0 seconds to 0 at 0.5 seconds;
`inventory_hidden` keeps it at 0. Cleanup uses actual root visibility, not a
hard-coded timer or the shorter `IsVisible()` frame grace. This is resource
inspection, not an in-game animation test. Other mods' Harmony postfixes still
run and require in-game combination testing.

## Verification and limits

The source-linked `build/CraftingFrameTests` checks routing/call counts. Shared
`build/GuidePlacementTests` checks native animation states and UI lifetime;
`InventorySlots.Tests` covers existing gameplay and UI contracts. Debug builds
also merge dependencies and deploy the final plugin through `DeployToGame=true`.

No FPS improvement has been measured in game. Reduced file writes, hierarchy
changes, and full-layout work are expected effects, not a claimed frame-rate gain.
Native recipe-list creation on opening and full GUI recovery remain intact.

## InventoryActions

InventoryActions does not have the custom equipment/stat panels or crafting
redesign, so those InventorySlots changes are not copied wholesale.

- Container action buttons retain their last applied geometry instead of
  restoring the native controls and splitting them again each frame. All five
  controls are checked for replacement, parent/sibling/frame/scale changes and
  resolved bounds (including stretched-parent resizing). Current controls are
  resolved before matching, including replacements caused by external renaming.
  Owner loss restores native geometry and hides actions immediately. Hide,
  close, GUI replacement/destruction and plugin teardown release the snapshots.
  Ownership, labels, tooltips and controller registration remain live.
- Favorite border refresh maps each element's current list index using the
  actual grid width, avoiding the native repeated linear search. Cached method
  metadata and an explicit Harmony field accessor handle the original private
  API. If either coordinate mapper has Harmony patches, refresh retains the
  native lookup. Patch presence is checked again each refresh; conservative
  fallback may remain after unpatching. Slot eligibility and click mapping are
  unchanged.
- The user-supplied ComfyQuickSlots 1.10.1, AzuExtendedPlayerInventory 2.6.1,
  ExtraSlots 1.2.17 and EquipmentAndQuickSlots 3.1.3 DLLs preserve the native
  mapping in the reviewed paths. Original copies, source paths and hashes are
  stored locally in `refer/InventoryActions/2026-10-06/`; they are not compiled
  or packaged. This is a scoped code review, not a runtime compatibility claim.

`build/InventoryActionsUiTests` exercises production method bodies with counted
Unity/Harmony boundaries: stable updates, resize, replacements, restoration,
ownership changes, GUI lifetime and favorite mapping/eligibility. It does not
simulate Unity's layout engine or actual Harmony detours. This cache is only
in memory; no configuration-folder cache file is created.

Manual InventoryActions checks still needed: open/close inventory and chests,
transfer ownership in multiplayer, resize UI, switch language/controller, and
exercise the four optional inventory mods separately. Measure frame times in
the same scenario before attributing an FPS change to this work.

Manual checks: repeated and rapid Tab, chest opening, equipment/quick-slot drag,
HUD follows on/off, resolution changes, Jewelcrafting stats, recipe search and
sorting, mouse/controller wheel input, single/batch crafting, upgrades/refinement,
and switching to Jewelcrafting/Reclaim/VNEI tabs. Compare frame times with the
same character, station, recipe count, resolution, and other mods.
