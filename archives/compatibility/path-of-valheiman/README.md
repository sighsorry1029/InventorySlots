# Retired Path of Valheiman integration

Archived on 2026-10-18 when InventorySlots declared `treextr.pathofvalheiman` incompatible. These are source-history patches, not installable mods or a supported fallback. They are outside all plugin/test compile inputs and release package inputs. No PoV or game DLL is included.

## Preserved work

| File | Original commit | Scope |
| --- | --- | --- |
| `0001-pov-base-integration.patch` | `18ab553` | InventorySlots 1.6.5: Big Bag rows, protected Tidy Pack, exact metadata stacking, native hover cards and tree input; tests, smoke probe and documentation. |
| `0002-pov-crafting-integration.patch` | `ef82d63` | InventorySlots 1.6.6: Salvage/Disenchant, List/Grid and tooltip integration, material counts, layout and patch ordering; tests, smoke probe and documentation. |

The full-index Git patches apply in order to the historical base `e571d15` and reproduce the tracked source tree at `ef82d63bea4324c2d5999d15ca73e37fd4ec5d6f`. They also preserve the original version, manifest and changelog edits. They must not be applied blindly to the current branch; use the original base in a separate recovery location when revisiting the work.

SHA-256:

```text
0B2733ACB768DAE8778FB9A383DA728C1AFF7B029B2FCACD407E66981F141C48  0001-pov-base-integration.patch
5D91148F750F683915937A2F0BB65439F2C3EA8F063D3360AEB8EED6FEDEAC5C  0002-pov-crafting-integration.patch
```

## Historical verification and retirement reason

The original integration was reviewed against PoV 5.0.4 (DLL SHA-256 `36E45B65B44667AD596446CAC2AE989041669948C11710B05185E4FE16697DB4`) and Valheim client 1.0.20. The historical record includes 194 automated tests, 60 isolated Mono checks, and a static compatibility check with 0 failures and 10 manual-review items. Fifteen Mono checks used instrumented Harmony bodies. These results did not establish actual game or multiplayer compatibility and do not apply to PoV 5.0.5.

The supplied PoV 5.0.5 DLL has SHA-256 `FEE675080C260958D16831A611B6658B434E8D587492FFF3F2383AB97E471B6C`. Its new Gear subsystem attaches four physical inventory rows, moves equipped items into cells derived from the current inventory height, and handles its own storage, weight and death policies. InventorySlots owns a different special-slot layout. PoV's `Player.Load` path still attaches and later releases rows with Equipment system Off. That setting is local, not server-synchronized. The two independent row owners are the reason for the incompatibility policy.

PoV 5.0.5 also changed `CraftSalvage.Returns(ItemData, Recipe)` from pairs to triples containing lost-material counts, with additional probabilistic recovery. The archived crafting adapter does not support that contract. No 5.0.5 integration or save migration was implemented.

Public stack metadata registration, general foreign-slot preservation and unrelated mod adapters remain in the active project. PoV-specific code, invocation sites and tests were retired. No saved `pt.*` item metadata or character data is rewritten by this change.
