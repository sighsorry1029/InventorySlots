# Favorite stack filling on player Sort

Implemented in InventorySlots 1.4.17 and InventoryActions 1.0.17.

The player Sort button now fills existing favorite stacks before its existing
ordinary-stack merge and position sort. This is unconditional, with no new setting.
Restock limits and all container actions retain their previous meaning.

- Only ordinary items already eligible for player sorting can donate. Hotbar,
  favorite, locked, equipment, and reserved slot exclusions are preserved.
- Targets are existing stacks in valid favorite cells, including eligible hotbar
  cells (and InventorySlots quick cells). External reserved rows remain excluded.
- Targets are visited by row, then column. Each is filled to its current maximum;
  overfull targets are left alone. No item or favorite marker changes position.
- Favorites cannot donate, so 25 + 25 favorite stacks stay separate without an
  ordinary donor. 20 + 35 favorites and an ordinary 40 become 50 + 45 favorites.
- Exhausted ordinary donors are removed through Inventory.RemoveItem. The final
  inventory change notification also runs when filling leaves nothing to sort.
- Newly filled favorite quantities gain the existing quick-stack protection.
- InventorySlots uses its existing conservative automatic metadata policy and
  metadata merge helper. InventoryActions excludes custom-data stacks and preserves
  the cheat-marker separation used by its ordinary Sort merge. Neither path uses a
  trusted external-mod stacking shortcut to perform a direct quantity merge.

The quantity loop is source-linked from Shared/Sorting/FavoriteStackFill.cs into
both plugins. Each host selects cells according to its own existing slot policy.
No Harmony targets, RPCs, saved favorites, or configuration keys were changed.

## Validation (2026-09-15)

- Both baseline and modified Debug builds succeeded with zero warnings/errors.
  DeployToGame=true copied the merged plugin DLLs to Steam Valheim plugins;
  source/destination SHA-256 hashes matched.
- Both Release builds succeeded with zero warnings/errors. Each generated ZIP
  contains the expected version, BepInEx 5.4.2350 dependency, documentation/icon,
  and a DLL whose SHA-256 equals the final merged Release DLL.
- InventorySlots.Tests: 167 checks passed before and after the change.
- Added --favorite-fill to the existing CompatibilitySmoke harness. It invokes
  the compiled FillFavoriteStackAmounts method with original game ItemData,
  checking quantity conservation, partial/full donors, row/column priority,
  fixed target positions, favorites-only no-op, repeat no-op, quality/world/item
  mismatch, custom-data exclusions, and zero/overfull targets.
- InventoryActions Debug and Release: all 17 quantity-stage checks passed with a
  clean desktop CLR exit. InventorySlots Debug and Release: all 16 checks passed
  under Unity Mono 6.13, followed by
  the previously observed native runner shutdown exit 0xC0000005. This is not a
  clean full-process test result.
- Initial attempts to invoke the complete fill operation could not execute
  Inventory.RemoveItem/Changed outside Unity: desktop CLR rejected a game default
  interface method, and Mono reached Player/ZSyncAnimation native initialization.
  The quantity stage is therefore tested separately without replacing game DLLs
  or stubbing the production stacking policy.
- Game execution remains unverified: player-dependent cell selection, button
  input, exhausted-donor removal and callbacks, UI/weight refresh, actual external
  mod combinations, and multiplayer. These are not covered by the isolated checks.
