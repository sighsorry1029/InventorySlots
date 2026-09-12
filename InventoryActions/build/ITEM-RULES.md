# Item rule UI and automatic pickup exclusions — 2026-09-12

Scope: InventoryActions 1.0.11 on the existing main checkout, starting at
`dee1a157309f71b99cdbe841806d5a4f1df55f4f`. Existing InventorySlots/shared-container
working changes are outside this implementation. No version, dependency, Release
package or network protocol change was made.

`616aed2` adds the empty-by-default client exclusion setting, local automatic
pickup filter, config text editing core and initial checks. The subsequent UI
commit adds the GUI-owned editor, persistence failure recovery and validation.

## Behavior and ownership

- Two icons are placed to the left of trash, following inventory height and the
  existing trash position offset. They remain independent of the trash enable setting.
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
