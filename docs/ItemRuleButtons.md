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

## Closing animation alignment (2026-09-14)

Both mods now retain the existing Trash and rule toolbar visuals beneath the
player grid while the native inventory animator closes. Hide/CloseContainer and
the later `IsVisible()` transition no longer independently remove these buttons
during a normal close. The rule editor closes immediately, its existing Animator
input guard prevents reopening, and Trash explicitly rejects interaction while
the animator's `visible` parameter is false. No separate button animation or timer
was added. Quickslot animation is unchanged.

Preview-only suppression, invalid/loading player cleanup, UI destruction and
explicit full hiding still remove the buttons. Retaining visuals never activates
an already-hidden toolbar. The shared UI implementation applies to both mods.

Both Debug builds/deployments succeeded. Existing InventorySlots checks (167) and
item rule configuration checks (37) passed; these do not execute Unity animation.
In-game checks remain: close/reopen with and without a container, a pinned editor
or held item, button visibility settings, and preview after closing.

## Independent slide-out buttons (2026-09-14)

Trash, Restock and Exclude each have their own clipped content transform below
the player grid in both mods. Each idle button exposes 7 UI units. Hovering that
button's visible area expands only that button over 0.15 seconds; leaving it for
0.3 seconds starts its retraction. Holding an item alone does not expand any
button: dragging and empty-handed hovering use the same pointer condition.
An open rule popup holds only its associated button expanded and follows that
button. Gamepad use alone keeps Auto buttons collapsed. Down from the last
visible player inventory row selects and expands one enabled button; Left/Right
switches to another enabled button, expanding only the newly selected Auto
button and retracting the previous one. Leaving the button row retracts the
selected Auto button. On buttons stay expanded; Off buttons stay hidden.
Enabled buttons pack from the right edge in Trash, Exclude, Restock order. With
Trash disabled, Exclude occupies column 8 and Restock column 7; if only one rule
button is enabled it occupies column 8. Live layout uses the same column offset
for the button, its clip and the popup anchor.

RectMask2D clips the translated content above the inventory's bottom edge. A
raycast filter restricts input to the currently exposed height, and hover checks
the foremost EventSystem hit so overlaid containers/dialogs win. No inventory
slot-sized invisible hover area is added. Disabled button settings still apply,
including the all-disabled case. The original row/column positioning remains
responsible for native and supported mod-added rows.

Each button's slide freezes during inventory closing so the three buttons leave
with their parent panel. Input is blocked and the editor closes immediately.
Opening again starts from the collapsed state. GUI destruction removes the
clips with their parents; per-button references are cleared on destruction.
Rule clips live under the rule toolbar so its hide/destroy behavior still owns
both buttons and their listeners. Each component performs no per-frame hierarchy
or reflection searches. Pointer data
and raycast result storage are reused (EventSystem itself may allocate).

Debug builds and existing automated checks do not verify Unity clipping,
raycasting or animation. Check the actual game for hover/drag, mouse movement
between buttons and popups, gamepad use, dynamic rows, all visibility-setting
combinations, container/split overlap, and close/reopen.

## Inventory Buttons configuration (2026-09-14)

Both mods use `3 - Inventory Buttons`, replacing their old `3 - Restock` section.
Configuration Manager order (highest Order first):

1. Restock Button = Auto (900)
2. Restock Target Stack Limits = empty (890)
3. Restock Leave One Item = On (880)
4. Auto Pickup Exclude Button = Auto (870)
5. Auto Pickup Excluded Items = empty (860)
6. Trash Button = Auto (850)

All six settings are client-only. Button modes are Off, Auto and On. Auto keeps
the independent hover slide behavior; On always exposes the whole button and
Off hides it. The existing synced `1 - General / Enable Inventory Trash Panel`
remains unchanged and overrides the local trash mode. Only effectively visible
buttons reserve columns. Hidden rule buttons leave saved rules active. Hiding
Trash also closes its confirmation and prevents starting a trash action.

No migration or legacy binding was added. Values from old section/key identities
are not imported into these bindings. Other section numbers are unchanged.

Both final Debug builds succeeded with zero warnings/errors and matched the
deployed Steam plugin DLL hashes. The existing 167-check suite passed. The
compiled InventoryActions `--button-modes` harness passed 271 checks covering
all server-permission/mode combinations with real BepInEx entries. InventorySlots
could not enter that additional harness: its existing static initialization
fails under Windows CLR (interface method loading) and under the experimental
.NET 9 runner (Harmony initialization). These are isolated-harness limitations,
not game execution results. Actual Configuration Manager presentation, live
UI mode transitions and game/network execution remain unverified.

### Retry with Unity's standalone Mono (2026-09-14)

Unity Editor installations provide `MonoBleedingEdge/bin/mono.exe` even though
Mono is not on PATH. The harness now initializes BepInEx.Paths through its
existing SetExecutablePath method before ConfigFile initialization, with a new
temporary BepInEx root. No user configuration or mod/game DLL is changed.

Using Unity 6000.0.46f1's Mono 6.13.0 (x86) and the current original client Managed
directory, InventorySlots completed all 271 `--button-modes` assertions against
the then-current 1.4.15 Debug DLL (SHA-256
`2F7AE7CC84FA5F328466456CBB47EA05494D8D79954E2473E3B8399058591701`).
This covers server permission, each Off/Auto/On setting, independent visibility
and packed columns. Logs: `artifacts/slots-button-modes-mono.log`.

However, the process subsequently exited with native access violation
`0xC0000005`. Unity 6000.0.61f1 behaved similarly; 2022.3.50f1 completed the same
assertions then exited with `0xC0000374`. InventoryActions as a control also
completed all 271 assertions then suffered the standalone Mono exit failure.
Windows Application events identify mono-2.0-sgen.dll/ntdll.dll for the access
violations. The underlying native fault has not been established. Disabling
finalization for the managed-only queue did not fix it and was not retained.

Thus the assertion results are now available, but this is **not** a clean
end-to-end harness run or actual Unity/game verification. No process termination
shortcut, altered game/mod assembly, or test-assertion bypass was used to mask
the failing exit status. Reproduction:

```powershell
dotnet build InventoryActions/build/CompatibilitySmoke/CompatibilitySmoke.csproj -c Debug
& 'C:/Program Files/Unity 6000.0.46f1/Editor/Data/MonoBleedingEdge/bin/mono.exe' `
  InventoryActions/build/CompatibilitySmoke/bin/Debug/net48/CompatibilitySmoke.exe `
  bin/Debug/InventorySlots.dll `
  'C:/Program Files (x86)/Steam/steamapps/common/Valheim/valheim_Data/Managed' `
  'C:/Program Files (x86)/Steam/steamapps/common/Valheim/BepInEx/core' --button-modes
# Check $LASTEXITCODE as well as the PASS summary.
```

## Empty favorite restock — 2026-09-20 (as released in 1.5.5 / 1.0.19)

Both mods now offer a per-item **Refill empty** checkbox in the Restock targets
panel and F1 rule editor. Unchecked by default. Panel rows use one line:
item icon/name, checkbox, quantity, delete. The checkbox retains its hover
explanation; item-name and checkbox tooltips use separate sibling hit areas.
The existing client setting stores the
quantity and flag together, e.g. `Wood: 30 | refill`; `Wood: 30` stays Off and
`Wood: 0 | refill` remembers On but disables all restocking. No new server
setting, character/item metadata, slot binding, RPC or consumption patch exists.

`Shared/ItemRules/EmptyFavoriteRestock.cs` runs only in the existing area-restock
executor after chest access/ownership and inventory-refresh checks. Existing
favorite stacks are filled first. An opted-in item missing from all eligible
favorite cells may seed one eligible empty favorite cell, scanning rows then
columns. Full stacks and incompatible variants of the same internal item name
still suppress seeding. Ordinary empty cells, locked/special/reserved cells
outside each mod's favorite-restock policy and incompatible quick slots are not
destinations. Rules are considered in config order within each chest; unavailable
items do not reserve cells ahead of later chests.

Native positional movement clones the actual source. The source must pass the
existing stackability and metadata checks; non-stackable items remain excluded.
The same target/max-stack cap, leave-one calculation and actual moved-quantity
accounting apply. Other compatible source stacks can finish the new target through
the ordinary restock routine. The opened-container Take stacks button, Sort and
automatic material consumption keep their previous behavior.

Verification:

- Both final Debug/deploy builds passed with zero warnings/errors. InventorySlots'
  first attempt failed in ILRepack's native PDB writer; the normal retry succeeded
  without changing build settings. Deployed Steam DLL hashes matched both outputs.
- Existing InventorySlots suite: 171 checks passed; the test project retains two
  nullable warnings in unchanged `StuWardCompat.cs`.
- Source-linked rule tests: 64 checks for each mod, including default Off, zero,
  duplicate/alias precedence, max-stack clamping, toggle/quantity persistence and
  live text-span rebasing.
- Compiled `CompatibilitySmoke --empty-favorite`: 28 assertions completed for each
  mod against original game assemblies (config refresh/F1 serialization and the
  existing leave-one helper). InventoryActions passed under desktop CLR with exit
  0. InventorySlots required Mono and still faulted during process shutdown after
  all assertions; this is not a clean end-to-end harness pass.
- A broader selection/movement harness attempt could not enter Unity-dependent
  code because `UnityEngine.Object` needs native initialization. It is not counted
  as passed, and the retained harness explicitly reports this scope as NOT RUN.
- Original client 1.0.15 static contract checks: InventorySlots 1028 references,
  135 Harmony targets, 42 reflected contracts; InventoryActions 564 references,
  29 Harmony targets. Zero failures. Existing dynamic/manual-review entries remain.

No actual game/UI/multiplayer run was performed. In-game checks remain for empty
favorite restoration after consumption, partial supply across multiple chests,
repeated Alt+E, existing full/incompatible favorites, no eligible vacancy,
locked/quick/third-party reserved cells, leave-one behavior, live/reloaded rules,
and non-owner multiplayer transfers. The implementation verification above did
not include a version bump, Release package or push.

### Authorized release — InventorySlots 1.5.5 / InventoryActions 1.0.19

- Bumped both plugin/assembly and manifest versions; added matching changelog
  entries for optional empty-favorite refilling and the single-line checkbox UI.
- Both final Debug/deploy and ordinary Release/package builds completed with zero
  warnings/errors. Steam plugin copies matched the final Debug DLL hashes.
- The 171-check suite and both 64-check rule suites passed. Final Release static
  contract scans against original Valheim 1.0.15 client assemblies reported zero
  failures with the same reference/target counts listed above. The InventoryActions
  Release DLL passed all 28 `--empty-favorite` isolated checks under desktop CLR.
- Verified both Thunderstore ZIPs' six entries against their source DLL, README,
  changelog, manifest, icon and English translation. Assembly and manifest versions
  match, and BepInEx dependency remains 5.4.2350. The InventorySlots Nexus ZIP's
  sole DLL matches the Release output. ZIPs are ignored build outputs.
- Actual game/UI/multiplayer execution and site publication were not verified.

Final Release DLL SHA-256:

| Mod | SHA-256 |
| --- | --- |
| InventorySlots 1.5.5 | `95308D234AE56DE23CB659C3378D29339AB82E746819F143562A6FD901E1EA75` |
| InventoryActions 1.0.19 | `0F6185C173F98FF7CB20022ED1BEE64C15163C52BD272BE065003E7E51178EE2` |

## Remember favorite destinations — 2026-09-20 (unreleased)

The checkbox/config format described in this section was subsequently replaced
by the three-state mode described below; the slot-memory behavior remains.

This update supersedes the anonymous-slot-only selection described above. Both
mods now remember the last observed item prefab at each eligible favorite cell,
per character. Empty cells retain their binding; another observed item replaces
it, and unfavoriting deletes it. Slots adds `prefab` to the existing `favoriteSlots`
records in `config/InventorySlots/ClientState.yml`. Actions extends its existing
`InventoryActions.Favorites.<playerId>.txt` rows from `x,y` to
`x,y,<URI-escaped-prefab>`; coordinate-only records still load without a binding.
Memory stores item kind, not quantity, quality or a serialized item instance.

With `refill` enabled and a positive target, Alt+E restores eligible remembered
empty cells for that prefab before considering an unassigned cell. Other items'
remembered cells remain reserved even when their source chest is processed later
or has no stock. Occupied/locked/out-of-bounds/incompatible original cells are not
replaced and do not cause fallback to another slot. Multiple remembered cells for
one prefab may be restored even if another favorite stack already exists. Each
new target is filled from compatible sources in that chest before the next cell.
Without any remembered cell or existing favorite stack, the old single anonymous
favorite fallback remains. Unobserved items consumed before this update cannot be
recovered from history. Existing access, ownership, stack metadata, transfer
accounting, target-zero and chest leave-one policies remain in the transfer path.
The panel still has only its checkbox, quantity and remove controls.

`Shared/ItemRules/FavoriteSlotMemory.cs` marks observations pending from
`Player.OnInventoryChanged` and captures after synchronous changes in LateUpdate.
Slots additionally waits for pending inventory maintenance, slot equip/unequip,
backup restoration, loading, progression reset and initial config synchronization.
`Player.Save` and plugin teardown flush safe observations. Changed associations
are persisted; quantity-only changes do not rewrite the file. Failed writes stay
pending for retry (five seconds) and explicit save/teardown can retry immediately.
The original private `Player.m_isLoading` is accessed through a cached Harmony
FieldRef in a lazy nested class, not directly through publicized visibility.

Verification:

- Baseline and final Debug/deploy builds of both mods: zero warnings/errors.
- Existing 171-check suite passed; the added character-separated YAML round-trip
  test brings it to 172. The suite retains the pre-existing nullable warnings in
  `StuWardCompat.cs` when recompiled.
- Shared source-linked rule/memory tests: 90 checks for each mod. Scenarios include
  opposite chest order, original-slot preference, unavailable/reserved cells,
  multiple remembered cells, replacement/unfavorite, no-op quantity changes,
  and coordinate/prefab persistence including escaped mod-prefab names.
- Original 1.0.15 client static contracts: Slots 1028 references / 137 Harmony
  targets / 42 reflected contracts; Actions 564 / 31 / 0, zero failures. Existing
  manual-review entries remain. The new Player loading field was checked against
  its original private declaration; static checks do not prove Unity execution.
- Actions compiled `--empty-favorite` config/F1/reserve harness: 28 checks and
  clean desktop CLR exit. An initial eager Player accessor broke that standalone
  initialization with a game default-interface TypeLoadException; lazy resolution
  removed it. This does not exercise live Player access or actual item moves.
- Steam plugin copies match the final merged Debug DLLs by SHA-256.

Not run: actual game UI, reconnect/death/slot-layout changes, modded player
inventories and host/dedicated multiplayer transfers. These need in-game checks;
pure selection/storage tests are not a claim that those sessions were exercised.
No version bump, Release ZIP, commit, push or site publication was performed.

## Three-state restock mode — 2026-09-21 (unreleased)

Both mods replace the refill checkbox with one compact icon button. It cycles
Off (dash), Existing (circular arrow), IncludeEmpty (arrow with plus), then Off.
New items default to Existing. Hover text explains the mode and its cycle. The
quantity and remove controls remain; no separate enable checkbox is added.
The in-game panel and F1 editor use the same three modes. Off preserves the
configured quantity, and editing that quantity does not enable the rule.

Rules now require an explicit positive target and mode:

```text
Wood: 30 | Off
Stone: 20 | Existing
Coins: 500 | IncludeEmpty
```

Existing tops up remaining favorite stacks. IncludeEmpty additionally restores
remembered empty favorite destinations under the selection policy above. The
quantity field clamps to 1 through the current item maximum when editing ends.
Only the runtime target for Off is zero; zero is not a saved target quantity.

At the user's request there is no legacy rule interpretation or migration:
numeric-only entries, zero targets and the old `| refill` suffix are invalid.
Runtime parsing and both editors ignore those entries instead of converting
them. Re-register them in the panel or edit their config text. With no valid
rule, existing favorite stacks use the normal maximum-stack default, so an old
disabled rule no longer disables restocking. Unedited unsupported raw text may
remain on disk; retaining raw text does not make it an active rule. This change
is confined to Restock rule syntax, not unrelated character persistence.

`RestockTargetLimitCore` owns strict parsing, mode cycling and serialization;
`ItemRuleConfigCore` supplies those same entries to both editors. Mode icons are
three lazily created sprites, independent of font glyphs, released at plugin
teardown. Mode-save failures restore the previous UI state. Existing favorite
memory, chest access, ownership, transfer accounting and leave-one rules remain.

Verification:

- Both final Debug/deploy builds succeeded with zero warnings/errors. The
  installed Steam plugin copies match their final merged DLLs by SHA-256.
- 172 main tests and 102 source-linked rule/memory checks for each mod passed.
  Coverage includes rejecting old rules, mode/quantity independence, mode
  cycling, duplicate/alias precedence, repeated edits and remembered slots.
- InventoryActions' final compiled DLL passed 31 isolated config/F1/reserve
  checks against original game assemblies with a clean desktop CLR exit.
- Original Valheim 1.0.15 static contract scans reported zero failures:
  Slots 1038 references / 137 Harmony targets / 42 reflected contracts;
  Actions 574 / 31 / 0. Existing manual-review entries remain (9 / 1).
- Read-only code review found no blocking issue; `git diff --check` passed.

Not run: actual Unity icon appearance, hover/click/F1 interaction, item transfers
or host/dedicated multiplayer. Builds and isolated tests do not validate these.
No version bump, Release ZIP, commit, push or site publication was performed.

### Icon orientation and live tooltip follow-up — 2026-09-21

The drawing helper already converts top-down coordinates to texture pixels.
The original circular-arrow math used the opposite Y convention, placing its
gap and arrowhead at the bottom. Both active mode icons now open at the top with
the arrowhead at the upper left; IncludeEmpty puts the plus in the center of the
ring, following the supplied sketch. Their existing gold color and button style
are retained. A preview rendered from the actual pixel-drawing code was visually
checked at enlarged and 28-pixel sizes; this is not a Unity screenshot.

Changing `m_topic`/`m_text` alone left the visible tooltip's text elements stale.
Mode clicks now call the original public `UITooltip.Set(string, string,
RectTransform, Vector2)` API, which updates the current tooltip without hiding
it or restarting the hover delay. No private accessor or new Harmony patch is
needed. Disabled tooltips lacking a prefab do not call Set, avoiding its automatic
hover-start branch. The rule controls remain outside the owned item-tooltip
scrolling/pinning paths.

Baseline and final Debug/deploy builds passed with zero warnings/errors for both
mods. Final DLLs match their Steam plugin copies by SHA-256. Original 1.0.15
static checks passed: Slots 1038 references / 137 Harmony targets / 42 reflected
contracts, Actions 576 / 31 / 0, zero failures (existing manual entries 9 / 1).
Read-only tooltip review and `git diff --check` passed. Actual game hover/click
behavior still requires checking in Unity; no Release build or commit was made.

### Shared parcel symbol follow-up — 2026-09-21

The rule mode icons now reuse the main Restock button's two circular arrows:
Existing has arrows only, IncludeEmpty adds its central outlined parcel, and
Off remains a dash. This supersedes the plus icon described above. Mode icons
retain their warm yellow color; the main button retains its existing tint.
Both symbols use the original thin line geometry so the parcel faces remain
distinct at 28 pixels. `DrawRestockSymbol` is shared by both sprite factories;
their separate caches and destruction paths are unchanged. Click behavior and
live `UITooltip.Set` updates are unchanged.

Checked enlarged and 28-pixel previews rendered from the actual drawing code.
Both baseline and final Debug/deploy builds succeeded with zero warnings/errors;
both installed Steam DLLs match the final outputs by SHA-256. Read-only review
and `git diff --check` passed. No behavior tests were added for this drawing-only
change. Actual Unity rendering remains unverified. No Release build, version
change, commit or push was performed.

## Release 1.5.6 / 1.1.0 — 2026-09-21

The favorite-destination memory, three-state restock rules, strict rule format,
live tooltips and final yellow parcel icons described in the development
checkpoints above are included in InventorySlots 1.5.6 and InventoryActions
1.1.0. Changelogs explicitly explain re-registering old target rules and the
maximum-stack default when an old numeric/zero/refill rule is ignored.

- Debug/deploy and ordinary Release/package builds passed with zero warnings
  or errors for both mods. Installed Steam plugins match final Debug outputs.
- Main suite: 172 checks. Source-linked rule/memory suite: 102 per mod.
- Final Release static checks against original Valheim 1.0.15 client assemblies:
  Slots 1038 references / 137 Harmony targets / 42 reflected contracts; Actions
  576 / 31 / 0. Zero failures; existing manual-review entries remain (9 / 1).
- Actions Release config/F1/reserve isolation harness: 31 checks, clean desktop
  CLR exit. This does not execute Unity, real inventory transfers or networking.
- Both Thunderstore ZIPs contain exactly the expected six files, all matching
  the source DLL, README, changelog, manifest, icon and English translation.
  The InventorySlots Nexus ZIP contains its matching Release DLL only. DLL and
  manifest versions agree; BepInEx dependency remains 5.4.2350.
- Final diff and independent read-only review found no blocking issue. Actual
  game/UI/multiplayer and site publication were not verified.

| Final Release DLL | SHA-256 |
| --- | --- |
| InventorySlots 1.5.6 | `261ADA8965906643CC4A3F2CD59D1467866E02D2026BC1AE1DEE1D85CFAE7D5E` |
| InventoryActions 1.1.0 | `3936AF80DEFDBC74196DB8697959162911CBB0C3C08EA36EBABEE14DB2FEF65D` |

## Exclusion checkboxes and rule hover help

Both mods retain registered pickup rules when their checkbox is unchecked.
Checked means automatic pickup is excluded; unchecked means pickup is allowed.
The state saves immediately. Manual pickup remains available, and the remove
button deletes the entry instead of merely disabling it. Config entries accept
`Wood` or `Wood | On` for enabled exclusion and `Wood | Off` for a retained,
disabled entry. Controller A toggles the exclusion checkbox when that control
is selected.

The client-only `3 - Inventory Buttons / Show Rule Tooltips` setting defaults to
On. It controls hover help within both rule panels and F1 restock-entry controls,
and updates an open panel immediately. Item information tooltips and the
Configuration Manager's setting descriptions remain available. Disabling help
does not change saved rules. The new checkbox, remove-button help and exclusion
controller hints have English and Korean text.

Duplicate prefab entries written by hand are displayed as one exclusion row.
Its checkmark reflects the effective pickup rule; toggling or removing it updates
all matching entries together. A failed save restores each entry's prior state.
Re-registering a disabled item enables its exclusion again. Item names stay fully
readable when unchecked; only their icons are dimmed.

Validation: both final Debug/deploy builds passed with zero warnings/errors and
matching installed DLL hashes. The existing main suite passed 172 checks; rule
and favorite-memory checks passed 135 per mod, including the new exclusion-state
round trips, comment/span preservation, deletion and rollback cases. Controller
dispatcher checks passed 73/71 for Slots/Actions. All four English/Korean files
parsed and contained the new keys. Original 1.0.15 client contract checks passed
with zero failures (Slots: 1066 references / 149 Harmony targets / 49 reflected;
Actions: 612 / 43 / 7; existing manual entries 10/2). Reports are in ignored
`artifacts/RuleControls/`. UI review verified native `UITooltip.OnDisable` only
hides that tooltip if it is current. Actual in-game UI/gamepad interaction was
not executed. No Release package, version change, commit or push was made.

## Release 1.5.8 / 1.1.2 — 2026-09-21

The exclusion checkboxes and rule-tooltip setting above are included in this
release together with optional client-only server installation and native chest
handoffs. InventorySlots also restores the Armor, Weight, Jewelcrafting Synergy
panel order and removes space reserved by hidden stat panels.

- Debug/deploy and ordinary Release/package builds passed with zero warnings
  or errors for both mods. Installed Steam plugins match the final Debug DLLs.
- Main suite: 172 checks. Rule/favorite-memory suites: 135 per mod. Existing
  controller dispatcher suites: 73 Slots / 71 Actions. Source-linked server
  policy and native handoff hosts: 38 and 143 checks respectively per mod.
- Final Release checks against original Valheim 1.0.15 client and dedicated
  server DLLs: zero failures; Slots 1066 references / 149 Harmony targets / 49
  reflected contracts, Actions 612 / 43 / 7. Existing manual entries remain
  10/2. Compiled AutoPickup transpiler checks on original client IL: 10 per mod.
- Both Thunderstore ZIPs contain the expected six files and match their source
  DLL, README, changelog, manifest, icon and English translation by SHA-256.
  The InventorySlots Nexus ZIP contains only its matching Release DLL. Assembly
  and manifest versions agree; BepInEx dependency remains 5.4.2350.
- Actual Unity UI, live multiplayer/crossplay and site publication were not
  checked. Source-linked hosts and original-assembly checks do not prove those
  execution paths.

| Final Release DLL | SHA-256 |
| --- | --- |
| InventorySlots 1.5.8 | `BAF5633F70AB7632F8ECE050A908FF5A99C2370AD180EBD2EA3F9F8B6451B056` |
| InventoryActions 1.1.2 | `E2F3463428543B121A5C7F5BA88462405A795FA05FD2DDAF86F23998DFE8EF70` |

## Controller navigation within rule panels

Both mods share the same rule-panel controls with the inventory action modifier
released. Up/Down selects an item row. Left/Right selects Mode, Quantity or Remove
in a restock row, or Checkbox or Remove in an exclusion row. A activates the
selected control: cycle the restock mode, toggle the exclusion checkbox, remove
the entry, or enter quantity editing.

During quantity editing, Up increases the quantity and Down decreases it. A or B
ends editing and returns to the row controls. Changes save immediately, so B
retains the saved quantity. Outside quantity editing, B closes the panel. X
continues to remove the selected entry as a shortcut.

## Sequential controller actions — 2026-09-22

Both mods now offer a short standalone right-stick click/release menu at the
selected inventory cell. It uses the original favorite eligibility and sort
entry points: favorite/unfavorite for eligible player cells, and sort for the
selected player/container inventory. Up/Down selects, A applies, B dismisses.
Dragged items, modal UI, mouse input, loading, changed selection/inventory, and
closing the inventory invalidate the menu. A held chord or direction cancels the
pending tap; a hold longer than 0.45 seconds does not open the menu on release.
Old configurable held chords remain unchanged.

Existing S buttons are controller focus targets beside the bottom-right visible
player cell and the top-right container cell. Additional Right enters the button;
A uses its existing listener/interactability checks, and B/Left/Up/Down returns to
the source cell. Sorting while carrying a dragged item remains blocked. Normal
grid hints, selected-button Open/Register/Back hints and the EN/KO guide explain
these controls without adding configuration entries. These UI paths remain usable
when optional controller hotkeys are disabled.

Input/navigation tests compile the production state machines for both namespaces
with stub UI adapters. Actual popup placement, device input, and visual focus still
require an in-game check; test success does not establish those results.
