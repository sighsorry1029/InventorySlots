# ValheimPlus inventory compatibility

Reviewed 2026-10-26 against InventorySlots 1.6.23 and the supplied Grantapher
ValheimPlus 10.4.0 DLL (plugin version 10.4.0.0), with original Valheim 1.0.20
Windows client assemblies. The earlier crafting-panel screenshot was withdrawn
as a reproduction; this patch does not diagnose or fix that screenshot.

## Responsibility

InventorySlots owns player inventory rows, including its hidden equipment and
quick-slot storage. ValheimPlus continues to own its chest/cart/ship size options.
The compatibility layer does not edit either mod's configuration.

Three V+ patch methods are skipped while their compatibility contract is verified:

- `Player_Load_InventorySize_Patch.Prefix(Player)` on `Player.Load(ZPackage)`.
- `Player_OnSpawned_InventorySize_Patch.Postfix(Player)` on `Player.OnSpawned(bool)`.
- `Player_SetInventorySize_Patch.Transpiler(...)` on `Player.SetInventorySize(int)`.

The spawn hook otherwise passes `Math.Max(inventory.GetHeight(), configuredRows)`
to the native inventory panel. InventorySlots' physical inventory height includes
reserved rows, so it must not be treated as the native visible-row count. The other
two hooks independently enforce V+'s minimum player row count.

`Inventory_MoveAll_Patch.Prefix(ref Inventory, ref Inventory)` is also skipped.
Its optional premerge directly changes item quantities, matching only item name
and quality. It bypasses InventorySlots' custom-data compatibility checks and
registered metadata composers (for example, expiry merging). This protection
covers every inventory pair to which that V+ prefix applies. The original
`Inventory.MoveAll` and all original `AddItem` bodies remain in use, together with
InventorySlots' existing hooks. This does not introduce new stack identity rules
or change the native positional/automatic transfer policies.

### Nearby quick stack

InventorySlots owns nearby inventory actions. The compatibility guard makes
`Inventory_StackAll_Patch.IsStackAllIntoOpenChest` return false, preventing V+'s
additional AutoStack sweep and its message takeover. The initial native
`Inventory.StackAll` transfer, its result/message behavior, and existing IS
metadata hooks remain in use. This does not replace native StackAll with IS's
nearby-action policy, or import V+'s range/exclusion settings into IS.

Both V+ Prefix and Postfix registrations, their shared `SweepStart` state and
the predicate's containing patch class are verified before installation. The
private `AutoStackSweep.running`, `stacking`, `Pending`, `Granted` and `CutOff`
fields must be idle/empty. Otherwise only this guard is refused with a restart
warning: no ongoing request, reservation, delayed reply or inventory is cleared.
V+'s sweep/RPC handlers and native StackAll exclusion transpiler are untouched.

### CraftFromChest resource display

The compact requirement strip and recipe tooltips share the existing availability
query. Source selection is AzuCraftyBoxes when its API is usable and not prevented,
then verified/enabled V+ CraftFromChest, then local inventory. Azu's valid zero
and leave-one results remain authoritative; overlapping provider counts are not
added together. The source is included in the existing 0.25-second cache key,
and the existing PoV availability adjustment remains in place.

The V+ display adapter calls its original private `ComputeItemQuantity` helper
through a cached delegate. This reuses V+'s current nearby-chest cache, range,
workbench origin, access checks and 1-10 second lookup interval. No new world scan
or reflection search is added per frame. The helper itself can allocate its
normal chest-item list and perform its ordinary expired-cache scan; this is not
a measured performance improvement.

The adapter reads the current configuration object through cached getters. It
also observes V+'s `Player_HaveRequirementItems_Transpiler` output at patch/repatch
time: the helper must actually be emitted, rather than merely registered or
enabled in config. This observes V+'s stage, not arbitrary later transpilers'
final output. The native requirements wrapper is rebuilt once to establish that
state. Changing configuration without V+ emitting its helper cannot falsely
activate the display adapter. V+'s own live backend reconfiguration behavior is
not changed here.

A temporary stock-query exception falls back to inventory-only display, logs
once per session and retries after one second. Its cache is not modified. A
configuration-getter/contract failure disables only this adapter. Crafting
eligibility, ingredient consumption and server permissions remain with native
Valheim/V+ and the already-installed mods; this is a display integration.

### Automatic pickup order

`AutoPickupCapacityPatch` runs before the actual V+ Harmony owner
`mod.valheim_plus`, as it already does for `sighsorry.AdminQoL`. IS moves the
original weight check before capacity evaluation first. V+ can then replace the
carry-limit call for `autoPickUpWhenEncumbered`, preserving that option's behavior.
The IS optimization still skips dedicated servers. No pickup timing, quantities,
ownership or V+ configuration values are changed.

## Installation and failure boundaries

- Soft dependency GUID: `org.bepinex.plugins.valheim_plus`.
- Native Harmony owner: `mod.valheim_plus` (different from the plugin GUID).
- Own guards: `sighsorry.InventorySlots.vplus.rows`, `.vplus.merge`,
  `.vplus.autostack` and `.vplus.crafting`.
- Installation runs before InventorySlots' ordinary `PatchAll`, after V+ loads.
- The four groups validate/install independently. Missing methods,
  changed relevant bodies, or incomplete native hook registration produce a
  warning for that group; they do not abort InventorySlots initialization.
- Only the specified V+ methods receive guards/observation. Other V+ patches are
  not unpatched. The already-transpiled `Player.SetInventorySize` and requirements
  wrappers are rebuilt without adding another native patch registration.
- V+'s owner-only `UnpatchSelf`/`PatchAll` configuration refresh cannot remove
  InventorySlots' guards. Configuration values remain intact for standalone V+.
- Guards share the process lifetime of InventorySlots' native hooks. Its existing
  `OnDestroy` leaves those native hooks installed, so restoring V+ row handling
  there would be unsafe. Runtime plugin/DLL replacement is unsupported. The
  internal `Dispose` is used by isolated tests after removing IS native hooks;
  production installation failures roll back only their own feature group.
- Rollback failures are logged separately and do not masquerade as successful
  cleanup. Restart after resolving the reported conflicting patch.

Contract checks compare declared signatures, native patch kind/owner, and a
normalized IL digest of each relevant patch class, including nested helpers,
constructors, static bindings, locals and exception blocks. Assembly versions,
MVID, metadata tokens, NOPs, raw branch offsets and platform line endings do not
determine acceptance. These checks run at installation, not per frame. A digest
match is not a proof about arbitrary external callees; a meaningful restructuring
may require review even when its behavior is equivalent.

## Validation

Inputs (identification only, not runtime allowlists):

- V+ SHA-256: `9F6070177A0C58A2EDB598B78A7E444DAB533D87855C5ECC476E2BE244AAAE6B`
- Original game `assembly_valheim.dll` SHA-256:
  `94998885B103D6E5F5480123EAEE0A093E82AF2DDEEEE37536C15BF555F24F3A`

The original DLLs were analyzed without publicization. The existing mod compiler
reference/publicizer configuration was retained. The isolated smoke executable
compiles against and runs with original, access-restricted game assemblies on the
game's Mono runtime; it loads the final merged InventorySlots DLL.

Reproduction after the normal Debug/deploy build:

```powershell
build/ValheimPlusSmoke/Run-MonoSmoke.ps1 -ValheimPlusDll '<original ValheimPlus.dll>'
build/ValheimPlusSmoke/Run-MonoSmoke.ps1 -ValheimPlusDll '<original ValheimPlus.dll>' -Mode rebuilt
build/ValheimPlusSmoke/Run-MonoSmoke.ps1 -ValheimPlusDll '<original ValheimPlus.dll>' -Mode changed-rows
build/ValheimPlusSmoke/Run-MonoSmoke.ps1 -ValheimPlusDll '<original ValheimPlus.dll>' -Mode changed-merge
build/ValheimPlusSmoke/Run-MonoSmoke.ps1 -ValheimPlusDll '<original ValheimPlus.dll>' -Mode changed-stack
build/ValheimPlusSmoke/Run-MonoSmoke.ps1 -ValheimPlusDll '<original ValheimPlus.dll>' -Mode changed-crafting
```

The first two modes pass 62 checks each. `rebuilt` modifies only an in-memory V+
copy's assembly identity/MVID and adds NOPs. Each changed-body mode passes three
checks confirming rejection of only the affected feature group. No game or
installed V+ DLL is rewritten. Every process exits 0, including Mono cleanup.

Covered behavior:

- Warmed native Load/OnSpawned wrappers run V+ row handling before guards and
  stop it after guards. The original resize transpiler has two V+ helper calls
  before protection and none afterward, including with the real IS transpiler.
- Native MoveAll reproduces V+'s differing-metadata premerge before protection.
  Afterward, real IS AddItem hooks preserve different metadata, allow matching
  metadata, compose registered metadata, and retain quantities/data through
  partial merges and full destinations.
- Owner-only V+ repatch preserves the protections; repeated installation adds
  no duplicate native patch; teardown restores original V+ behavior.
- Missing native hook registrations and changed relevant method bodies reject
  only their respective group; configuration values remain unchanged.
- A warmed original StackAll executes its initial transfer before and after the
  guard; additional V+ sweep start/message takeover is removed, survives repatch
  and returns on teardown. Busy and pending-reply states refuse only this group
  and remain untouched.
- The original private stock helper counts local plus cached chest items. The
  production display path preserves Azu zero/priority, avoids double counting,
  changes cache source on disable, follows replacement configuration objects,
  requires actual helper emission, and recovers from transient query failure.
- Actual final AutoPickup IL retains weight-before-capacity order and the correct
  static/instance call stack with both V+ option values and both registration
  orders. The full Unity pickup loop is not executed in the fixture.
- Core tests: 191 passed before and after. Final static checker: 1,157 references,
  164 Harmony targets and 52 reflected contracts, zero failures (10 manual items).
- Debug build/merge/deploy: zero warnings/errors.

The smoke fixtures stub engine-only logging/time/object-equality/gameObject
services and the native player Load/OnSpawned bodies. V+ hooks still execute,
with UI row requests captured instead of creating a Unity scene. The additional
sweep start is counted without starting networking, and the stock helper reads
a seeded V+ chest cache without Unity physics. Azu availability/counts are fixture
inputs; the actual Azu DLL is not run here. Transfer tests use real managed
inventories, not a connected player/container session. The standalone host emits
an unresolved engine gameObject internal-call warning while preparing the fixture
detour; the managed checks and Mono cleanup still exit 0. This is **not** evidence
of an installed game/runtime mismatch, nor an in-game/dedicated/multiplayer pass.

Live follow-up should check joining/rejoining, V+ server settings synchronization,
tombstone recovery/take-all, inventory row progression, independently enlarged
containers, local/chest resource counts at and away from workbenches, opening a
different chest, nearby actions, and pickup with the encumbrance option on/off.

## Out of scope

CraftFromChest eligibility/consumption, ongoing AutoStack reply handling,
unrelated UI patches and InventoryActions are unchanged. V+'s entire codebase
and other mods' Harmony combinations were not runtime-tested. The compatibility
layer introduces no new options, save format or RPC protocol. These changes are
included in InventorySlots 1.6.24.
