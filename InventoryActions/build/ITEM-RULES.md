# Item rule UI and automatic pickup exclusions — 2026-09-12

Scope: InventoryActions 1.0.11 on the existing main checkout, starting at
`dee1a157309f71b99cdbe841806d5a4f1df55f4f`. Existing InventorySlots/shared-container
working changes are outside this implementation. No version, dependency, Release
package or network protocol change was made.

`616aed2` adds the empty-by-default client exclusion setting, local automatic
pickup filter, config text editing core and initial checks. The subsequent UI
commit adds the GUI-owned editor, persistence failure recovery and validation.

## Behavior and ownership

- In this initial implementation, two icons were placed to the left of trash,
  following inventory height and the existing trash position offset. The 2026-09-13
  update below replaces that shared position offset. They remain independent of
  the trash enable setting.
- Restock drop reads an item from the local inventory and opens a quantity editor.
  Save registers the quantity; cancel leaves the config unchanged. Autopickup drop
  immediately registers the prefab and opens/scrolls to its entry. Duplicate drops
  select an existing rule. Neither operation consumes, moves or modifies the item.
- Hover waits 300 ms; leaving both icon and popup waits 250 ms. Clicking or editing
  pins the popup. Lists show up to six rows and scroll. A pinned backdrop blocks
  underlying clicks; dirty drafts require Save, Cancel or Escape.
- Restock uses the existing `3 - Restock / Restock Target Stack Limits` setting and
  its existing per-favorite-stack Alt+E policy. Current-container Take stacks remains
  unaffected. Zero means no restock, and the runtime still clamps to normal maximum.
- `2 - Client / Auto Pickup Excluded Items` uses prefab names and defaults to empty.
  Both configs are local and shared by characters using that cfg. No item metadata
  or character save format is introduced. ConfigEntry changes apply immediately;
  direct disk edits require reload/restart, as before.
- Text edits preserve unrelated entries, comments and separators. Existing restock
  aliases/normalization and last-duplicate precedence remain. A prefab registration
  updates the last matching normalized key; other aliases are retained. Removing one
  entry can expose a remaining alias rule.
- Save rejects a changed ConfigEntry snapshot. With autosave temporarily disabled,
  it applies the setting and saves once; on IO failure it restores the old setting
  and SettingChanged consumers, then restores the previous autosave option.

## Game and UI boundaries

Original Valheim client 1.0.12 / Steam 25253764 and dedicated server 1.0.12 / Steam
25253791 were used for analysis and static checks. Existing build publicizer/runtime
access setup remains; originals were not publicized or replaced for analysis/tests.

- `Player.AutoPickup(float)` remains a private Harmony target. The transpiler adds
  `dup → original m_autoPickup load → ldarg.0 → FilterAutoPickup` at the unique public
  field read. All original branches/calls remain. Unexpected match counts or a new
  exception boundary disable just this filter with a warning. Branch labels move to
  `dup`; input instructions are copied.
- The filter can only veto the local player's automatic pickup. Native false remains
  false. `ItemDrop.m_autoPickup`, Interact/Pickup, owner, RPCs and world items are untouched.
- One InventoryGui-owned component owns generated icons, toolbar, popup, draft,
  row listeners and temporary item-name lookup. Rows rebuild on opening/editing,
  not per frame. Lookups are discarded on close/reload. No measured performance claim.
- TMP inputs are configured while inactive, with a separate masked Text Area.
  Config is saved on explicit Save/submit, not on end-edit/deselection.
- Public `Chat.HasFocus` is extended during pinned editing and the closing frame;
  native InventoryGui and InventoryGrid gamepad handlers are also blocked then.
  Full InventoryGui.Update continues to run. Native SplitDialog and trash confirmation
  are kept mutually exclusive with the editor. Touch release uses UIDragHandler.
- Hide/loading/menu/console/GUI disable/destruction clear editor state. The cached Animator
  comes from the same public GetComponent call as vanilla Awake and prevents reopening
  during InventoryGui.IsVisible's delayed hidden-frame window. Generated sprites and
  textures are destroyed with the editor.

## Performed verification

- Baseline and final Debug build/deploy: zero warnings/errors.
- Final original client/server contract checks: each 465 direct references, 28 static
  Harmony targets, zero failures. Existing nonpublic reference count stayed 30;
  one existing ServerSync method-level declaration still requires manual review.
- Existing regression/source suite: 166 passed.
- Pure config edit/identity tests: 17 passed (comments, aliases, duplicates, zero,
  unknown mod items, deletion/reload/re-registration).
- Real BepInEx config tests: 13 passed, including failed file save, consumer rollback,
  stale snapshot rejection, retry and preservation of SaveOnConfigSet.
- Compiled transpiler on original client/server IL: 10 checks each, including original
  accessibility, unique anchor, input-label preservation, injected stack sequence,
  original opcode/operand preservation and absence of added world/item stores.
- English/Korean UI keys present; diff whitespace check passed.
- Final merged DLL and Steam plugins copy SHA-256:
  `342AD0862543395C5C3159C36D174E397942FFA36FE380CEA3A61CC5CA3939BF`.

Static reports are in ignored `artifacts/item-rules-20260912-final-{client,server}.json`.
No game, Unity/Mono UI, host/client multiplayer or controller session was run.

## Reproduction commands (repository root)

```powershell
dotnet build InventoryActions/InventoryActions.csproj -c Debug -p:DeployToGame=true
dotnet run --project InventorySlots.Tests/InventorySlots.Tests.csproj -c Debug
dotnet run --project InventoryActions/build/RuleTests/RuleTests.csproj -c Debug
dotnet build InventoryActions/build/RuleConfigSmoke/RuleConfigSmoke.csproj -c Debug
# RuleConfigSmoke.exe <BepInEx core> <test output directory>
dotnet build InventoryActions/build/RuleIlSmoke/RuleIlSmoke.csproj -c Debug
# dotnet RuleIlSmoke.dll <final InventoryActions.dll> <original Managed> <BepInEx core>
```

## Remaining in-game checks

1. Hover bridge, click pin, six-row scroll and last-row registration at different UI
   scales and purchased inventory heights; toggle trash visibility and position.
2. Restock drop → quantity entry → Save/Cancel/Escape; 0 and max; unchanged inventory
   quantities/quality/customData; limits only affect favorite Alt+E restock.
3. Exclude drop → immediate registration, repeated drop, remove/save, manual E pickup,
   another player's automatic pickup, and persistence after restarting/switching characters.
4. Enter/Escape/WASD/numbers, controller focus and touch drag release; no underlying
   item move/split/use; no cursor loss after closing or reopening inventory.
5. GUI teardown, death/loading, menu/console, config manager changes during a draft;
   optional UI/pickup mods may add their own input or Harmony paths and require live testing.

## Toolbar appearance and downward dropdown — 2026-09-13

Historical step: the column-layout update below supersedes its manual offsets.

Starting at `ff3921d`, this UI-only update keeps item registration, config rule text,
automatic pickup filtering and all inventory/network mutation policies unchanged.

- Restock is a parcel with two return arrows; excluded pickup is an upward arrow
  and tray crossed by a slash. Both are cached 64-pixel procedural white sprites,
  tinted and sized by the same helpers as trash. Their textures/sprites are still
  owned and destroyed by the GUI editor; no image dependency was added.
- Rule buttons now clone the same native Take All background as trash. Empty-handed
  buttons use the native disabled sprite and gray icon while remaining clickable;
  a valid held local-inventory item uses the normal sprite and golden icon. Native
  hover/pressed sprites remain enabled. Trash's own eligibility guards are unchanged.
  The clones' Take All click listeners and `UIGamePad` shortcuts/hints are disabled
  or replaced so they cannot claim vanilla JoyLStick input.
- The popup copies the active original 1.0.12 split-dialog background
  (`win_bkg/border (1)`: `woodpanel_512x512`, sliced, shared `litpanel` material).
  Player/crafting `Bkg` sprites are fallbacks. No native material or sprite is
  destroyed. The source lookup happens only during editor initialization.
- `2 - Client / Restock Rules Button Position` and
  `2 - Client / Auto Pickup Exclude Button Position` are separate, unsynced offsets,
  both defaulting to `x: 0 y: 0`. Positive X moves right and positive Y moves up.
  They use the existing config drawer/parser and independent value caches. Trash
  offset no longer shifts either rule button. In-game changes apply live; editing
  the cfg on disk still requires a reload/restart.
- The popup follows the actual selected button's bottom-right, opening downward.
  Limited space reduces the viewport (at most six rows). If even one row plus the
  editor controls cannot fit below an extreme button position, screen-edge clamping
  shifts the panel enough to keep Save/Cancel reachable. Geometry changes preserve
  the draft, input focus and valid scroll position without rebuilding rows.

Verification for this update (separate from the preceding implementation):

- Baseline and final Debug/deploy builds: zero warnings/errors.
- Extended compiled-button-offset smoke: 40 checks passed on original client DLLs
  with real BepInEx ConfigEntry instances, including independent live changes,
  invalid input, unbinding and rebinding. No user config file was written.
- Final original client and dedicated-server static checks: each 481 direct
  references, 28 Harmony targets, zero failures. One existing ServerSync declaration
  still needs manual review; these checks do not certify Unity/Mono execution.
- Rendered the actual procedural icon geometry at enlarged and HUD scales for
  visual inspection. This was a source rendering, not an in-game screenshot.
- Final merged Debug DLL and Steam plugins copy share SHA-256
  `52877501CF9C98B377CE189B0B914E4F1292D8BA5F30059E953E0448EDE30A20`.
- Static reports: ignored `artifacts/item-rule-ui-20260913-{client,server}.json`.
  No Release package, version bump or push was performed.

Still requires actual game verification: native wood/gray/gold rendering, hover
bridge and pinning, independent offsets with purchased rows/UI scaling, downward
scrolling near screen edges, live offsets during quantity editing, controller
shortcuts and touch drops. The existing rule-persistence and multiplayer behavior
was not rerun in a game session for this appearance change.

## Automatic column layout and ExtraSlots — 2026-09-13

Starting at `a060ac1`, the three bottom buttons now share the same grid-relative
column/bottom calculation as InventorySlots' trash placement. Standard width 8
uses trash column 8, exclusions column 7, restock column 6. Hiding exclusions
moves restock to column 7; hiding trash does not change the reserved columns.
For other widths these remain the rightmost three columns. Sort placement is
separate. Changes to the number of regular rows move all bottom buttons together.

The old Trash/Restock Rules/Auto Pickup Exclude Button Position bindings and caches
are removed. Old keys left in a cfg by BepInEx are ignored, not migrated into item
rules or used for positioning. Sort Button Position is retained.
`2 - Client / Show Restock Rules Button` and `Show Auto Pickup Exclude Button` are
unsynced and default On. They control visibility only; saved exclusion filtering
and favorite-stack restock limits remain effective. Off closes the corresponding
popup, discards its draft and releases its input/backdrop state. Hidden buttons
are excluded from hover and click handling. Toggling the other icon repositions
an open popup without rebuilding its rows or discarding its draft.

Popup input fields and button faces now use opaque warm brown, with a gold input
border and brighter hover/focus. Native button frames and cream text are retained.
Nominal sRGB contrast from the code colors is approximately 9.0:1 normally and
6.0:1 on hover/focus; game rendering was not measured. Inset faces do not intercept
raycasts, and the outside-click backdrop remains transparent.

ExtraSlots analysis used the supplied original DLL (not publicized):

- Path: `C:/Users/blizz/AppData/Roaming/com.kesomannen.gale/valheim/profiles/inventorys/BepInEx/plugins/shudnal-ExtraSlots/ExtraSlots.dll`.
- Version 1.2.3.0; GUID `shudnal.ExtraSlots`; SHA-256
  `0DB53DDD5FDA6BE9C95E1BA13248A0E177CA400B28BF77D7DDCAFCDE28C71117`.
- `Slots.InventoryHeightFull` includes 40 reserved equipment/quick/food/ammo slots:
  five hidden storage rows at width 8. `Inventory.GetHeight()` alone therefore
  puts the buttons five rows too low. `API.GetInventoryHeightPlayer()` is public,
  static, returns int and takes no parameters; it reports only regular rows.
- `EquipmentPanel.InventoryGrid_UpdateGui_UpdateSlotsOnDirty` positions the special
  elements separately and restores grid height to regular rows. It runs during
  `InventoryGrid.UpdateGui`, before InventoryActions' `InventoryGui.Update` postfix.
  No extra Harmony ordering or patches to ExtraSlots are necessary.
- ILSpyCmd 9.1.0.7988 `--disable-updatecheck -t` on `ExtraSlots.API`, `Slots`,
  `EquipmentPanel`, `InventoryInteraction` and `ExtraSlots`, and `-il -t ExtraSlots.API`
  provided the evidence. Analysis was read from stdout; no game assets were recopied.

The implementation adds a soft dependency and resolves that public API once as a
`Func<int>`. The plugin instance and delegate, not the row value, are cached.
Layout reads the current value, only for the local player's inventory, clamped
to its actual height. Behaviour.enabled is deliberately not used: ExtraSlots'
static patches persist when its component is disabled. Destroyed/missing plugins
use the normal height; API failure logs once and also falls back. Dedicated servers
skip this UI integration. No ExtraSlots DLL is referenced at compile time or bundled.
Only sort/trash/rules UI placement changes; item eligibility, storage and network
ownership logic are outside this patch.

Verification for this step:

- Baseline and final Debug/deploy builds passed with zero warnings/errors.
- Compiled `CompatibilitySmoke --ui-layout`: 41 checks passed using original client
  DLLs and real ConfigEntry instances. Covers all four visibility combinations,
  reserved columns, explicit 4/6/9-row positions, changed cell/button size and
  retained sort offset behavior. No Unity objects or user config writes.
- Original client/server static checks: each 490 direct references, 28 Harmony
  targets, zero failures; the existing ServerSync manual-review entry remains.
- Final merged DLL and Steam plugins copy SHA-256:
  `FD8774B256D046E126DE4147A583DCF25BBCD271B735E2BD2B5B96CFD0C58B4F`.
- Reports: ignored `artifacts/item-rule-columns-20260913-{client,server}.json`.

Not executed: actual Unity/game rendering, live toggle during draft editing,
ExtraSlots absent/present sessions, purchased-row and ExtraSlots live-row changes,
UI scale/controller/touch checks, multiplayer. API signature and control flow
inspection are not a substitute for these runtime checks. No version bump,
Release package, ExtraSlots DLL modification or push was performed.
