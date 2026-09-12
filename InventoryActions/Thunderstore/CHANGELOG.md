# Changelog

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
