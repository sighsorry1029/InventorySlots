# Changelog

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
