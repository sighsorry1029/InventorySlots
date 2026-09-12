# InventorySlots item rule buttons — 2026-09-13

Starting point: main, `427ba16`. This adds InventoryActions' restock-rule editor and
automatic pickup exclusions to InventorySlots. Existing uncommitted shared-container
work is outside this change. No version, Release package, network policy or game
support range changes are included.

## Player-facing behavior

- Restock and Auto Pickup Exclude use the same gray/gold icons, brighter controls
  and wooden downward dropdown as InventoryActions. Hover opens a temporary list;
  clicking pins it for editing. Long lists scroll. Near screen edges the dropdown
  is shortened/clamped so Save and Cancel remain reachable.
- Dropping a held local-inventory item on Restock opens a quantity editor. Save
  registers it in the existing `3 - Restock / Restock Target Stack Limits` config.
  Cancel leaves it unchanged. This retains the existing per-favorite-stack area
  restock policy, maximum-stack clamp and meaning of zero. It does not change
  current-container Take stacks or make nonfavorite items restock candidates.
- Dropping an item on Auto Pickup Exclude immediately registers its prefab name
  and opens its list. Repeated drops select the existing entry. Neither drop
  consumes, transfers, destroys or changes item data.
- `4 - Client / Auto Pickup Excluded Items` is an unsynced, empty-by-default list.
  It affects only the local player's automatic pickup; manual pickup remains
  available. It is shared by characters using that cfg, with no item/character
  metadata. Direct file edits still require the usual config reload/restart.
- `5 - Client UI / Show Restock Rules Button` and
  `Show Auto Pickup Exclude Button` are unsynced and default On. Changes apply
  live. Hiding a button closes its editor and discards an unsaved draft; saved
  rules stay active. There are no new button position settings.
- For an eight-column inventory, Restock is centered below column 6, Exclude below
  column 7 and the existing trash below column 8. Hiding Exclude moves Restock to
  column 7. Trash visibility does not reclaim the reserved trash column.

## Integration and contracts

`Shared/ItemRules` contains the existing editor, config text/store helpers and
automatic pickup filter as source shared by the two projects. Each compiles into
its own namespace/DLL; there is no shared runtime DLL or cross-mod config coupling.
This avoids maintaining a second copy of the editor and its persistence/cleanup
logic. The limited compile-time namespace aliases add a source-navigation cost;
host-specific settings, row policies and input hooks remain in their own mods.

- `ItemRuleIntegration.BindItemRuleConfigs` binds only the new Slots settings and
  subscribes to the exclusion change event. `ShutdownItemRules` closes/destroys the
  GUI editor, unsubscribes and clears the filter. `PluginLifecycle` invokes these.
- `InventoryUiController` passes its final grid origin and visible viewport rows
  through `UpdateInventoryActionPanels`; its panel-drag shortcut also updates the
  toolbar. The placement deliberately does not use full `Inventory.GetHeight()`:
  Slots has hidden/special storage and its own collapsed/expanded viewport policy.
  Existing vanilla purchased rows, recovery rows and wheel expansion still use
  that same display calculation. No ExtraSlots dependency is added to Slots.
- Container preview hides the rule UI and prevents reopening through its buttons.
  Native modal/hidden/loading/menu states discard drafts. The GUI owns generated
  textures/sprites and row listeners; native wood sprites/materials are borrowed.
- Pinned editing and the closing frame block Slots global shortcuts, crafting
  favorite clearing, socket opening and inventory gamepad handlers. A hover list
  owns wheel input only over the popup; pinned editing blocks background wheel use.
  Native `UIGamePad.ButtonPressed` is also blocked for the current InventoryGui's
  descendants. The latter fixes an independent background-button shortcut path in
  both consumers; normal InventoryGui.Update and network processing still run.
- The restock hint uses each mod's configured restock keyboard/controller display.
  InventoryActions keeps its own displayed-row/ExtraSlots integration and input
  policy. No ownership, authorization, RPC, quickstack or restock mutation changes.
- Automatic pickup retains the single-anchor `Player.AutoPickup(float)` transpiler
  and fail-safe warning when another patch changes its expected shape. Original
  false stays false; no writes to world items or `ItemDrop.m_autoPickup` are added.
- Existing text comments/separators, duplicate precedence, stale-draft rejection,
  config save failure rollback and event consumer restoration are unchanged.

## Verification performed

Analyzed original Valheim 1.0.12 client Steam 25253764 and dedicated server Steam
25253791. The existing compile-time publicizer/runtime access configuration was
retained; analyzed originals were not publicized. Static checks do not establish
runtime access or Unity behavior.

- Baseline and final Debug/deploy builds passed with zero warnings/errors for both
  mods. No Release ZIP was generated.
- Existing InventorySlots regression suite: 166 passed. The working tree includes
  preexisting shared-container code/tests, which were not added to this commit.
- Shared rule parsing/editing tests: 17 passed for each namespace/host parser.
- Slots-namespaced real BepInEx config tests: 13 passed, including stale snapshot,
  failed save, consumer rollback and retry. No user config was written by tests.
- Final compiled pickup transpiler: 10 checks per mod per original client/server
  assembly. Includes unique anchor, labels, injected stack sequence, original
  opcode/operand preservation and no extra item/world stores.
- Final static checks: Slots 1006 direct references, 133 Harmony targets, 40
  reflected contracts, zero failures, eight manual-review entries per role;
  Actions 490 references, 29 Harmony targets, zero failures, one manual entry.
  Manual entries concern existing dynamic optional-mod/ServerSync hooks and the
  pending shared-container load patch, not the new rule targets.
- Actions compiled UI/config geometry smoke: 41 passed after sharing the source.
  Slots could not run this whole-plugin desktop CLR harness: .NET Framework fails
  on original Player interface metadata; a .NET 9 attempt fails in the installed
  Harmony AccessTools initializer. These are harness limitations, not a passing
  Slots UI test or evidence of a game regression. The experimental harness changes
  were discarded. Slots UI/input lifecycle received a separate read-only review.
- Final merged DLLs match their Steam plugins copies by SHA-256:
  - Slots: `C81DAF3975D8EE3DB41939DCBC099604A3110198DF0B98B7005B29DD566296B7`
  - Actions: `B3DB70CD80E2ECB1310B98243BDF0BEE5EE11763509359C159218AB1DAD7ED92`

Static reports: ignored `artifacts/slots-item-rules-20260913/*-final.json`.

Useful commands from the repository root:

```powershell
dotnet build InventorySlots.csproj -c Debug -p:DeployToGame=true
dotnet build InventoryActions/InventoryActions.csproj -c Debug -p:DeployToGame=true
dotnet run --project InventorySlots.Tests/InventorySlots.Tests.csproj -c Debug
dotnet run --project InventoryActions/build/RuleTests/RuleTests.csproj -c Debug -p:RuleTarget=InventorySlots
dotnet run --project InventoryActions/build/RuleTests/RuleTests.csproj -c Debug -p:RuleTarget=InventoryActions
dotnet build InventoryActions/build/RuleConfigSmoke/RuleConfigSmoke.csproj -c Debug -p:RuleTarget=InventorySlots
# RuleConfigSmoke.exe <BepInEx core> <isolated test directory>
# dotnet RuleIlSmoke.dll <final mod.dll> <original Managed> <BepInEx core>
```

## Not executed: actual game checks

1. Normal/purchased/recovery rows, collapse/expand and panel dragging at different
   UI scales; all button toggle combinations and lower-screen dropdown clamping.
2. Drop registration, repeat registration, quantity/save/cancel/remove, and config
   persistence after restart. Confirm inventory quantities and custom data stay
   unchanged and restock still follows existing favorite eligibility.
3. Auto pickup exclusion and manual pickup, including optional pickup mods and a
   second player's independent settings.
4. Preview/normal inventory transitions, GUI teardown, death/loading, menus,
   input focus, wheel, controller/touch, live config change during an unsaved draft.
5. Client/host/dedicated-server multiplayer behavior. No game/Unity/Mono or
   multiplayer session was executed, and no performance improvement was measured.
