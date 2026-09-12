# InventorySlots item rule buttons — 2026-09-13

Starting point: main, `427ba16`. This adds InventoryActions' restock-rule editor and
automatic pickup exclusions to InventorySlots. Existing uncommitted shared-container
work is outside this change. No version, Release package, network policy or game
support range changes are included.

These sections record successive changes. The final "Native controls and live
editing" section supersedes the earlier Save/Cancel workflow and parchment colors.

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

## Dialog layer correction — 2026-09-13

The original 1.0.12 prefab puts Player, Info and Crafting before SplitDialog and
the other inventory dialogs under `m_inventoryRoot`. ItemRules had instead been
created directly under the GUI canvas and raised to its last sibling every frame,
which rendered the toolbar above the complete native subtree. Both mods now create
it under `m_inventoryRoot`, immediately before SplitDialog, and keep that order.
The original parent rectangles have identical full-stretch bounds, preserving
button coordinates and dropdown screen-edge clamping. The existing trash panel
already belongs to PlayerGrid and needs no layer change; its cloned confirmation
still renders above the controls.

Native inventory dialogs and the mod's trash confirmation also close an open rule
popup and prevent coordinate-based hover/click from reopening it underneath them.
The toolbar stays in its normal layer; dialog activation/confirmation and item
mutation are untouched. Both Debug/deploy builds passed with zero warnings/errors.
Original client/server static checks passed: Slots 1007 references / 133 targets /
40 reflected contracts, Actions 496 / 29 / 0; existing manual entries remain 8/1.
Actual split/variant/trash confirmation overlaps, hover suppression and closing
transitions still need an in-game check in each mod.

## Muted control colors — 2026-09-13

Both consumers use low-saturation parchment inputs and tan buttons with dark brown
text. Input borders/carets are dark, and text selection uses translucent blue-gray.
Hover/focus gently lightens each surface. Font, size, layout, wood background,
native materials and sprites are unchanged. ControlFace stays white; only the
Selectable tint supplies the surface color, avoiding a second multiplication.
The transparent outside-click backdrop is unchanged.

Nominal sRGB text contrast calculated from the actual source colors is 7.30:1 for
normal buttons and 11.34:1 for normal inputs. All defined control states exceed
4.5:1 (minimum 5.37:1). This is a color calculation, not a measurement of game
rendering or comfort. Actual hover/focus/selection appearance still needs a game
check, along with the dialog overlaps listed above.

Final Debug/deploy builds passed with zero warnings/errors. Original client/server
static checks passed: Slots 1008 references / 133 targets / 40 reflected contracts;
Actions 497 / 29 / 0. Existing manual entries remain 8/1. Final merged DLLs and
Steam plugins copies match by SHA-256:

- Slots: `B1A43806A2E1AEFC8929360E4A6A3BC0ED3C8D80F7E7C35D121FA29DB0A61DA3`
- Actions: `B605612C1983DB234B3A1F0E86C9E1FD5D5E231934FCFF964A832FB4F117F0B8`

Reports: ignored `artifacts/item-rule-colors-20260913-*-*.json`.
No Release package, version change, push or actual game session was performed.

## Native controls and live editing — 2026-09-13

Starting point: main, `9605d2f`. Both mods compile this change from the same shared
source. The existing uncommitted shared-container work remains separate.

- Fix the reported LiberationSans warnings by adding TMP components while their
  objects are inactive, assigning the existing game font/shared material, then
  activating them. If the Take All font is unavailable, use another initialized
  InventoryGui text; defer construction if no game font is ready. No default TMP
  font, embedded font asset or shared native material is modified.
- Panel buttons borrow Craft's image, SpriteSwap states, font and material.
  They retain their own actions and enabled text color, even when Craft itself
  is disabled by missing ingredients. No crafting or controller behavior is cloned.
- Quantity inputs borrow the inventory slot Image and its Button ColorTint.
  This includes the normal gray-black tint and approximately 0.502 alpha; copying
  only the original white Image would not reproduce the grid's appearance.
- Maximum panel width is 300 UI units (previously 360). The 40-unit footer area
  and Cancel/Save buttons are removed. Existing row height, tooltip names,
  downward positioning, screen clamping and dialog layer are preserved.
- Valid quantities save and notify existing config consumers on each edit.
  Incomplete/invalid input is never placed into the saved model; losing focus
  restores the last saved quantity. Escape closes without undoing valid edits.
  Selecting text and saving do not rebuild rows or replace their Entry objects.
- Drop registration immediately saves a new entry using the existing restock
  target; Restock then focuses its quantity editor, Exclude shows its list.
  Repeated registration preserves the existing configured value. Removal saves
  immediately. Registration still does not move or consume inventory items.
- Every successful save updates the text spans held by existing row objects.
  This prevents repeated typing, deletions and appends from overwriting adjacent
  rules. Surviving keys/order are validated before writing; duplicates, comments,
  aliases and unrelated invalid entries retain their existing behavior.
- External config changes reject stale edits. Failed saves restore the config,
  its consumers and the edited field; failed removals keep the row. Valid rules
  remain active when the editor closes or its button is hidden. English/Korean
  guide text, status text and setting descriptions reflect automatic saving.

Validation:

- Both baseline and final Debug builds with DeployToGame=true succeeded with
  zero warnings/errors; final merged DLLs match their Steam plugins copies.
- Shared rule core: 37 checks passed for each host namespace, including 9/10/1000/2
  size changes, Entry identity, duplicate keys, deletion then adjacent editing,
  append/edit/delete/re-register, CRLF/comments and untouched invalid rows.
- Real BepInEx config store: 13 checks passed for each namespace, including stale
  snapshots, IO failure, consumer rollback and retry, using isolated test configs.
- Original 1.0.12 client/server static checks: Slots 1013 references, 133 Harmony
  targets, 40 reflected contracts; Actions 512 references, 29 targets. Zero failures
  for each role; existing manual-review counts remain 8/1. Reports are in ignored
  `artifacts/item-rule-live-20260913`.
- Final DLL SHA-256: Slots `879E1E001190B5DE3BEB676B82400EA956EE201CA14BE7E27371D0D1F0BB30AB`;
  Actions `C525D938D789B215947CB2E09B2583E649927393533DEF4BB6F1B850E0DF72CB`.

Actual Unity/game execution was not performed. Check both mods for font-warning
recurrence, Korean/fallback text, Craft disabled while rule actions remain usable,
input contrast over wood, continuous typing/selection/Escape, config persistence
after restart, screen-edge dropdowns and split-dialog overlap. No performance
measurement, version change, Release package or push was performed.
