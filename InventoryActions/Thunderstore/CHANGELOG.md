# Changelog

## 1.1.3

- Added direct controller navigation for Restock, Auto Pickup Exclude, Trash, and player/container Sort. Move down from the final inventory row or right from an edge slot, then use A to activate and B or a return direction to restore the original cell. Auto buttons expand only while focused.
- Added a short right-stick click menu for Favorite and Sort without picking up an item. Existing optional modifier chords remain available, while menu/button focus suppresses conflicting pickup, close, submit, and world actions.
- Reworked controller rule editing: up/down selects an item, left/right selects Mode, Quantity, or Remove, A activates or edits, X removes, and B closes or finishes quantity editing. Added focused-control highlights, safe trash confirmation navigation, and localized live help.
- Added **LT + right-stick click** to collapse or expand the quick guide while the inventory is open. The controller chord appears in the guide header and does not open the item action menu on release.
- Moved the open-inventory quick guide into the free upper area between the player and crafting panels. It adapts to active panels and UI scale, keeps clear of the inventory and crafting UI, and hides when no readable space remains.
- Fixed raw localization tokens and duplicated labels in controller help and confirmation buttons. Controller help follows the selected inventory button closely and moves above it near the screen edge.
- Reworked the package README into a shorter visual overview and added linked guides for inventory actions, controllers, and multiplayer compatibility.

## 1.1.2

- Allowed client-only installation when joining vanilla servers, using local settings. If the server installs InventoryActions, all connecting clients must still use the same version and receive its synced settings.
- Added vanilla chest ownership requests for area quick stack and favorite restock on unmodded servers. Transfers run one chest at a time, preserve favorites and restock rules, skip occupied or inaccessible chests, and guard against stale replies and duplicate actions. Container action effects remain local on vanilla servers.
- Added checkboxes to Auto pickup exclusions: uncheck to allow pickup while keeping the entry, or remove it to delete the rule. Changes save immediately; duplicate prefab entries are handled together, and controller A toggles the selected checkbox.
- Added the client-only Show Rule Tooltips setting under 3 - Inventory Buttons, enabled by default. It controls help in both rule panels and the F1 restock editor without hiding item information or Configuration Manager setting descriptions.

## 1.1.1

- Added configurable controller inventory actions: hold the right-stick click by default, then press A to toggle the selected player slot's favorite protection, X to sort the focused inventory, Y to open Restock targets, or B to open Auto Pickup Exclude. Opening a rules panel with a picked-up player item registers that item.
- Added controller favorite restock by holding the game's alternate-action modifier with Use while looking at a chest. This follows the selected controller layout and preserves target quantities, restock modes, remembered empty slots, chest access checks, and the leave-one-item setting. Holding Use alone continues to quick stack.
- Added controller navigation to both rules panels: up/down selects an entry, left/right adjusts its quantity, A changes its mode, X removes it, and B closes the panel. Changes save immediately, and the quick guide and chest hints show the active bindings.
- Prevented controller actions from also triggering native pickup, use, submit, close, or Stack All commands. Input dialogs block the new shortcuts, and cloned action buttons no longer inherit Take All shortcuts.
- Made the Configuration Manager restock editor fit the available width with compact item, mode, quantity, and remove controls. Narrow views wrap without forcing horizontal scrolling, and long item names stay inside their fields.

## 1.1.0

- Remembered favorite-slot item types per character in `InventoryActions.Favorites.<playerId>.txt`. IncludeEmpty restock restores items to their remembered empty slots instead of assigning them by chest order, including multiple remembered slots of the same item. Occupied slots and slots remembered for other items are never replaced. Replacing an item updates its association; removing favorite protection clears it.
- Replaced the Refill empty checkbox with three modes: Off preserves the target quantity without restocking, Existing tops up remaining favorite stacks, and IncludeEmpty also restores empty favorite slots. New entries default to Existing. Quantity editing now clamps to `1..current max stack` and does not change the selected mode.
- Added compact yellow mode icons: a dash for Off, circular arrows for Existing, and the same arrows with a central box for IncludeEmpty. Both the panel and F1 editor support the modes, and visible panel tooltips update immediately when a mode changes.
- **Configuration change:** rules now require a positive quantity and explicit mode, for example `Wood: 30 | IncludeEmpty`. Old numeric-only rules, zero targets, and `| refill` are ignored without migration. Re-register old rules or edit their config text. Without a valid rule, existing favorite stacks refill to the normal maximum, so an old zero rule no longer disables restocking.
- Favorite memory records only items observed in eligible slots; it cannot identify items consumed before they were observed. Unassigned empty favorite fallback, current stack compatibility, special-slot exclusions, chest access/ownership checks, and Restock Leave One Item remain in effect.

## 1.0.19

- Added a per-item Refill empty checkbox to Restock targets, unchecked by default. When enabled, Alt+E can refill one eligible empty favorite slot up to the target quantity after that item's favorite stack has been completely consumed. Existing favorite stacks keep their positions and prevent additional stacks from being created.
- Condensed Restock targets into single-line rows with a checkbox immediately left of the quantity field. Checkbox hover explanations are separate from item-name tooltips, long names use ellipsis, and changes save immediately. The F1 rule editor also supports the option.
- Saved the option with each client-side target rule, for example `Wood: 30 | refill`. A quantity of 0 still disables restocking. Current maximum-stack limits, item compatibility, special-slot exclusions, chest access/ownership checks, and Restock Leave One Item continue to apply.

## 1.0.18

- Updated stack identity handling for Valheim 1.0.14. Quick stack and restock no longer combine normal items with otherwise matching cheat-marked items, matching the game's new native stacking rule.
- Retained matching-origin sorting and favorite filling, compatible custom-data protection, container authorization, ownership leases, saved favorites, settings, and network contracts.

## 1.0.17

- Player inventory Sort now fills existing favorite stacks from compatible non-favorite stacks in the sortable inventory area before merging and sorting the remaining ordinary stacks. Favorites are filled from top to bottom, left to right, up to the current maximum stack size.
- Favorite stacks stay in their original slots and never donate to or merge with each other. This behavior is always enabled; restock limits do not apply. Items moved into favorite slots gain the existing favorite quick-stack protection. Container sorting, quick stack, and restock rules are unchanged.

## 1.0.16

- Added independent Auto slide-out behavior for the Restock, Auto Pickup Exclude, and Trash buttons. Each button now shows only its bottom edge until hovered, stays open with its own editor, follows the inventory closing animation, and no longer expands merely because an item is being dragged.
- Grouped the related client settings under `3 - Inventory Buttons`. Each button now supports `Off`, `Auto`, and `On`, defaults to `Auto`, and enabled buttons fill empty positions from the right. The existing synchronized `Enable Inventory Trash Panel` setting still controls whether trash is permitted. No legacy setting migration is included.
- Kept button input disabled as soon as the inventory starts closing while allowing all three controls to leave with the same player-inventory animation.

## 1.0.15

- Added the client-only `Restock Leave One Item` option, enabled by default. Favorite restock (Alt+E by default) leaves one item per kind in each source chest, counting all stacks with the same internal item name together, so the chest remains a quick-stack destination.
- Turning the option Off allows restock to take the last item. In-game setting changes apply to subsequent transfers immediately. `Take stacks`, `Take All`, and manual moves retain their existing behavior.
- Each client controls its own restock setting; another player or mod may still remove the last item. Existing ownership, access, and stack-compatibility checks are unchanged.

## 1.0.14

- Added an optional compact InventoryActions quick guide beside the hotbar. It displays the active favorite, Use, and Restock bindings, adapts to available screen space, can be collapsed, and stores its visibility and collapsed state as client-only settings.
- Moved Restock and Auto Pickup Exclude controls into the player-grid render hierarchy used by Trash, so container panels and native dialogs remain above all three controls. External toolbar listeners and objects are also released with the inventory UI lifecycle.
- Changed the Restock limit editor to keep an in-progress numeric buffer and clamp it to `0..max stack` when editing ends. For example, an item with a maximum stack of 30 now saves both `40` and `230` as `30` instead of restoring `4` or `23`.
- Removed the redundant automatic-save status and empty-hand Trash hover tooltip, and added a localized title to the delete confirmation dialog.

## 1.0.13

- Added optional Equipment and Quick Slots 3.x and AzuExtendedPlayerInventory 2.4.14 compatibility. Automatic sorting, quick stack, restock, favorites, trash, and Safe Take All now stay within regular player inventory rows instead of treating equipment, quick, reserved, or custom-slot rows as storage.
- Kept bottom action buttons aligned with each provider's visible inventory rows. AzuEPI's live separate-panel setting switches between the regular inventory bottom and the full inline grid without restarting.
- Removed the Favorite Border Color configuration option and fixed favorite borders to the previous default blue color.

## 1.0.12

- Added client-side Restock and Auto Pickup Exclude controls below the player inventory. Dropping an item registers its rule without moving it; valid restock quantities and removals save immediately, while excluded items remain available through manual pickup.
- Positioned Restock, Auto Pickup Exclude, and Trash under the last regular inventory row as vanilla pocket upgrades or the optional ExtraSlots mod add rows. The two new controls can be hidden independently, and Restock shifts toward Trash when exclusions are hidden.
- Added wooden downward-opening rule lists with scrolling and screen-edge clamping. The panels use Valheim's Craft button and inventory-slot visuals, remain below native dialogs, and no longer require Save or Cancel buttons.
- Fixed repeated missing LiberationSans font warnings by assigning an initialized Valheim font before activating generated text. Invalid quantities restore the last saved value, while config conflicts and save failures leave the previous rule active.
- Reduced redundant per-frame UI work and retained existing favorite, quick stack, restock, sorting, trash, ownership-handoff, and optional MultiUserChest policies.

## 1.0.11

- Fixed vanilla Hold E quick stack skipping the container being interacted with. The opened container is now processed first, then any remaining eligible items are extended to nearby containers.
- If an area transfer cannot start, InventoryActions now leaves Valheim's original `StackAll` path available instead of suppressing the current-container action. Nearby containers that are in use remain excluded, and the existing access, ownership-lease, revision, timeout, and duplicate-processing guards are retained.

## 1.0.10

- Maintenance release verifying that area ownership-lease rejection replies use Valheim 1.0.7's registered `RPC_OpenResponse` and `RPC_TakeAllResponse` methods in the final package. Access, ownership, timeout, revision, and duplicate-request behavior is unchanged from 1.0.9.

## 1.0.9

- Updated InventoryActions for Valheim 1.0.7 and fixed the repeated `MissingMethodException` caused by the changed private inventory notification signature.
- Added support for Valheim's purchased inventory rows. Favorites, sorting, quick stack, restock, trash positioning, and action-button layout now follow the loaded inventory height while continuing to protect the hotbar.
- Updated favorite input and grid element handling for the current inventory UI, moved trash confirmation to the current Split Dialog lifecycle, and corrected open/take-all rejection response RPC names while preserving access, ownership-lease, revision, timeout, and duplicate-request checks.
- Preserved the cheated-item marker when sort consolidates compatible stacks, while retaining the existing fail-closed behavior for unrecognized external custom item data.
- Updated the build to use the current original Valheim assemblies with compiler-only publicized references, retained the reviewed ServerSync compatibility build, and corrected the BepInEx package dependency to 5.4.2350.

## 1.0.8

- Reduced redundant UI updates by keeping the active Sort and Trash buttons enabled instead of toggling them off and on during each refresh.
- Optimized favorite borders by reusing loaded favorite state and cached UI components, updating colors and layout only when needed, and recovering replaced border components.
- Removed per-refresh trash-icon layout strings and redundant visual-state caches. The icon now checks its actual layout and color, preserving recovery from external UI changes.
- Multiplayer servers and clients must update to the same InventoryActions version, as before.

## 1.0.7

- Hardened area quick stack/restock completion and cancellation by finalizing handoff state before ownership-lease cleanup and inventory callbacks, preventing callback failures from leaving an action stuck or eligible for duplicate continuation.
- Fixed the tooltip fallback so it handles only InventoryActions-owned buttons, leaving vanilla and other-mod tooltips untouched, and stopped relabeling or tagging Valheim's built-in Take All and Place Stacks buttons.
- Fixed the Restock Target Limits editor changing negative values such as `-5` into `5`; comments, separators, and negative amounts now use the same parsing rules as runtime, where negative limits normalize to `0`.
- Ignored stale favorite coordinates outside the supported vanilla player rows so they no longer remain protected or display favorite borders.
- Unified current-container and area candidate selection and removed unused cell-policy, single-container transfer, hold-action, localization-cache, and UI paths without intended feature changes.

## 1.0.6

- Shared hover hold area quick stack/restock success effects with nearby players who also enable the client setting, while retaining VFX at up to 10 changed containers and one SFX at the interacted container.
- Kept shared effects transient and locally rendered with dedicated-server, distance, and receive-rate guards so completed actions are not replayed to players entering the zone later.

## 1.0.5

- Fixed area quick stack/restock on dedicated servers so nearby players can use eligible closed containers even when another peer owns them, processing containers one at a time through validated ownership handoffs.
- Added lease, revision, access, range, idle-state, timeout, and vanilla request guards to prevent stale or concurrent container mutations during ownership handoff.
- Disabled area quick stack/restock while MultiUserChest is active and delegated non-owner Take All to MultiUserChest 0.6.1 or newer.
- Required the same InventoryActions version on the dedicated server and clients for multiplayer container actions.
- Made area action success effects local-only and short-lived so completed effects are not replayed to players entering the zone later.

## 1.0.4

- Simplified container action success effects to one On/Off option and fixed the hover hold duration at 0.5 seconds.
- Area quick stack/restock now shows VFX at up to 10 changed containers while playing its SFX only once at the interacted container.

## 1.0.3

- Fixed container action buttons potentially restoring stale positions after closing and reopening the inventory or container UI.
- Unified Safe Take All and top-first item movement through one path while preserving hotbar and favorite protections.
- Centralized cross-container transfer bookkeeping and added source/behavior parity tests with InventorySlots to reduce drift.
- Fixed container action success sound volume scaling to apply the configured value once, removed unused localization tokens, and simplified project source discovery.

## 1.0.2

- Kept the center-screen result counts for container quick stack and container restock actions.
- Removed the other action success/result messages, including take all, place all, sort, and trash, while keeping failure and warning messages.
- Restored only the quick stack/restock result localization tokens and removed unused result tokens for the quieter actions.

## 1.0.1

- Mirrored InventorySlots action-cell and container action helper policies with tests to reduce drift risk.
- Refactored favorite/restock eligibility and container move/grid-order helpers without intended behavior changes.
- Added client config options to adjust the player inventory Sort and Trash button positions live with x/y text values.

## 1.0.0

- Initial standalone InventoryActions release.
