# Startup equipment visual initialization (issue #5)

Patched after InventorySlots 1.5.1 (`0b862ad`). No version, config, inventory data,
network contract, or release package changes are included.

## Problem and scope

The report at https://github.com/sighsorry1029/InventorySlots/issues/5 shows slot
backup restoration during main-menu character loading, followed by repeated
`MaterialMan.PropertyContainer.UpdateBlock` exceptions. The same installed plugin
versions ran successfully with the developer's own character, without entering
the reported backup-restoration path. The reporter's character has not been
obtained or reproduced locally.

Original Valheim 1.0.12 code publishes `MaterialMan.instance` in `Awake`, but creates
its private `MaterialPropertyBlock` in `Start`. Registering a visual before that
initialization can store a null block in a property container permanently.
`UpdateBlock` then throws before `MaterialMan.Update` can remove the pending entry.
InventorySlots can reach registration indirectly through `VisEquipment.AttachItem`
or `AttachArmor` when restoring custom equipment. This is a plausible trigger, not
a captured runtime stack proving the reporter's exact cause. The relevant mod
code was identical in 1.5.0 and 1.5.1.

## Change

- Check the current manager's actual block through a cached Harmony `FieldRef`.
  The original field is private; no direct publicized-field access or patch to
  the game's initialization/exception behavior is used.
- Defer player custom-visual updates until that block exists, before compatibility
  visual hooks or the successful-update cache are touched. Repeated requests for
  the same player are combined. Inventory/backup restoration still runs normally.
- Process deferred requests on the plugin's client Update, including the menu.
  Discard destroyed players, retain still-loading players, and clear pending
  references when the plugin is destroyed. Existing visual cleanup remains intact.
- Gate remote ZDO-driven custom visuals as well; their existing per-frame visual
  update retries naturally. No new network RPCs or ownership changes are involved.
- Read readiness from the current manager rather than retaining a scene-global
  initialized flag. Scene replacements must initialize their own block.

## Verification

- Debug build with `DeployToGame=true`: zero warnings/errors; deployed Steam
  plugins DLL SHA-256 matches the final merged Debug DLL.
- Existing automated suite: 171 tests passed.
- `pwsh -NoProfile -File InventorySlots.Tests/EquipmentVisualInitializationSmoke.ps1`:
  8 checks using production entry guards and deferred processing with Unity doubles.
  Covers missing manager, pre-Start manager, deduplication, successful retry,
  scene replacement, ongoing player load, destroyed preview and dedicated-server
  exclusion. This does not execute real visual attachment or game lifecycle events.
- Original-client DLL static contracts: 1,026 references, 135 Harmony targets,
  42 reflection contracts, zero failures; 9 existing manual checks remain.
- The shared `InventoryActions/build/CompatibilitySmoke` harness has a new
  `--material-access` mode for InventorySlots. InventoryActions plugin code was
  not changed. Against the final Debug DLL and original game assemblies, Unity
  6000.0.46f1 standalone Mono executed the real compiled FieldRef delegate and
  passed both original-private-field/access checks. The process then exited with
  `0xC0000005` (-1073741819). This is a successful isolated access assertion, not a
  clean end-to-end harness run or a game-runtime pass. No termination workaround
  was added. Log: `artifacts/issue-5-review/material-access-mono.log`.

Actual startup with the affected character, menu character switching, world entry,
remote players and logout remain unverified in-game. Test after restarting the
game; this patch prevents InventorySlots from registering too early and does not
repair an already-invalid material container created earlier in a running process.
