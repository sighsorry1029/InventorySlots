# InventorySlots / Path of Valheiman isolated checks

These checks load the final merged InventorySlots DLL and the **original** PoV and Valheim assemblies in Valheim's Windows x64 Mono runtime. They do not start Unity or a game session. No game assembly is publicized or rewritten, and no plugin/config is installed by this runner.

Build/deploy InventorySlots separately:

```powershell
dotnet build InventorySlots.csproj -c Debug -p:DeployToGame=true
```

Run both modes, supplying the original PoV DLL and the directory containing its Jotunn dependency. `GamePath` defaults to this repository's `environment.props`; `ModDll` defaults to `bin/Debug/InventorySlots.dll`.

```powershell
./build/PathOfValheimanSmoke/Run-MonoSmoke.ps1 -PovDll '<original PathOfValheiman.dll>' -OptionalDependencyDirectory '<directory containing Jotunn.dll>' -Mode cold
./build/PathOfValheimanSmoke/Run-MonoSmoke.ps1 -PovDll '<original PathOfValheiman.dll>' -OptionalDependencyDirectory '<directory containing Jotunn.dll>' -Mode warm
```

The runner builds its test executable and the repository's existing `InventoryActions/build/CompatibilitySmoke/MonoHost.cpp` with the Visual Studio C++ x64 tools. Logs and input hashes go under ignored `obj/MonoHost-x64`. Cold mode lets the bridge create the API caches; warm mode initializes them first. Both check that the bridge preserves the original classifications and cached object identities. The reference omission assertions describe PoV 5.0.8/5.0.9; a later PoV fix requires reviewing those expectations, not blindly updating them.

## Coverage and limits

- Optional dependency loading: final InventorySlots types enumerate before loading PoV, without a mandatory PoV assembly reference.
- Real reflection/delegate bindings, dynamic public storage-provider interface, guarded Harmony installation and the native HUD transpiler.
- Original eight-purse-cell omission, 43/46 cell descriptors and 28 storage cells, unchanged cached `Worn` values, cold/warm cache identity and forwarding of the exact player/item references.
- Actual provider registration, accepted/rejected placement, occupied destinations, full backpack rejection, closed-cell withdrawal and suspended/loading guards.
- Native and provider save-coordinate conversion, same-reference conservation, missing-slot recovery, no-space rejection before mutation, failed-load protection and subsequent-load recovery.
- Public quiver registration callbacks: successful insertion/removal, UI right-click with a full/free backpack, Off-state cleanup through the checked native fallback, and exceptions after partial callback effects.
- No Tool/Key/Purse definitions, disabled native roles, equipment/quick bank grouping without changing physical coordinates, and hidden/available/carried quiver visibility.
- Ordinary-inventory coin weight/death benefits through separate public policies: locked/unlocked/Off, container and remote exclusion, live weight recalculation, overlap with YAML, YAML Off, repeated restoration and a full backpack with a no-op death body. Native PoV's weight postfix also runs to check for double subtraction.
- Shared Delete UI guard: original Build/Enter/Press and original InventoryGui postfix call site, cleanup request, and selective patch teardown. Cursor and exit effects are managed fixture substitutes; no cursor rendering is tested.
- Public affix/rarity bindings, repeated affix values, and the current salvage-return signature.
- Extra trinket ID mapping and native ordinals 45/46/47, original spawn restoration, full-backpack promotion/node reset, dynamic hand seats, closed/Off withdrawal, queued equipment completion, synchronous external removal, and upgrade result/rollback restoration without evicting peers.

The 5.0.9 trinket fixtures run the original public API, `State.Assign`, `Trinkets.Worn/Refresh`, spawn and equipment Harmony patches together with InventorySlots routing. Progression getters, world-effect refresh and vanilla Humanoid equipment bodies are managed stand-ins; this checks compatibility ordering and item ownership, not actual equipment effects or animation timing.

The harness constructs managed player/inventory fixtures, controls PoV progression/option delegates, and substitutes unavailable Unity services: object identity, time, animator hashes, localization singleton, color parsing and message display. Two fixture prefab names stand in for Unity objects. Registered quiver callbacks are deliberately simple managed stand-ins, not real BetterArchery/RustyBags execution. Production item movement, inventory contents, bridge methods and the loaded original API implementations are exercised. There is no UI rendering, physics, live networking or complete BepInEx/PoV startup.

## Implementation checkpoint — 2026-10-18

| Input | Version / SHA-256 |
| --- | --- |
| PathOfValheiman.dll | BepInEx version **5.0.9** / `6D01948786BF2E1527D6801670E4D14ADBEC89E5E3809F1D8F8840E56C7D8165` |
| Previous PathOfValheiman.dll | BepInEx version **5.0.8** / `3E4AFE1A7DCF52612B93DCA2A2C70A27C3CD0DF19BF852D87F6365F81BA7E3DA` |
| Original assembly_valheim.dll | Valheim **1.0.20**, Windows client build **25909357** / `94998885B103D6E5F5480123EAEE0A093E82AF2DDEEEE37536C15BF555F24F3A` |
| Original mono-2.0-bdwgc.dll | `35FD9D8065EE84D0C9312B4B3BB19ECA3C1196C952E66D74DC8895CFFF0AA028` |
| Final merged InventorySlots.dll | Debug **1.6.7**, before release version bump / `1615C36CD0E72675811E0FFCCE3E0BD1A76E0997AB2F7106E2CE5D1959FD072B` |

PoV 5.0.9: cold **169 checks**, warm **171 checks**. PoV 5.0.8 regression: cold **140 checks**. All managed/native process exits were **0** with Mono cleanup completed. The repository unit runner also passed **189 tests**. The compiled-contract check resolved 1,151 references, 163 declared Harmony targets and 52 reflected contracts with zero failures; ten dynamic targets remain explicitly manual rather than automatically proven. The final Debug build had zero warnings/errors and the deployed DLL hash matched.

During the earlier 5.0.8 work, one incremental Debug build hit a native Cecil PDB-writer crash during ILRepack and a fresh sequential `Rebuild` succeeded. The 5.0.9 patch builds did not reproduce it; its cause remains unconfirmed.

## Release validation — 1.6.8, 2026-10-18

The final merged Release DLL (`12CFD43B622845316B0CB58122927B864F6F81BA7D40A3DF7B3F4A88361D6794`) passed the same **169 cold / 171 warm** checks with the original PoV 5.0.9 and Valheim inputs above. Both process exits were 0 and Mono cleanup completed. Run with `-ModDll bin/Release/InventorySlots.dll` to select this configuration.

The unit runner passed **189 tests**. Release contract inspection reported 1,151 references, 163 Harmony targets and 52 reflected contracts, with zero failures and ten manual targets. Debug/Release builds had zero warnings/errors. The Debug deployment hash matched, and Thunderstore/Nexus package DLLs matched the final Release DLL. Package versions, changelog, README and translations were checked. These are isolated/static checks; the game checks below remain outstanding.

## Panel and coin-benefit update — 2026-10-19

Debug InventorySlots **1.6.8** (`39C94381A9F5F5D74BF822F81185C51326E47AD0744D25607B82846089EB3CC9`)
passed **262 cold / 264 warm** checks against original PoV 5.0.9 and **227 cold**
checks against the original 5.0.8 input above. All process exits were 0 and Mono
cleanup completed. These replace the prior purse-cell expectations: no purse
capacity/classification-cache mutation is performed. Ordinary Coins use public
weight/death policies, including recalculation after a same-inventory reload or
inventory replacement. The unit runner passed **189 tests**. Contract inspection
resolved 1,152 references, 163 declared Harmony targets and 52 reflected contracts
with zero failures; ten dynamic targets remain manual. The new weight postfix
was executed against the original private Inventory field through Harmony
injection in the Mono checks. Debug built with zero warnings/errors and its
deployment hash matched. No version increment or release package was made.

The same-day compact-width follow-up passed **274 cold checks** against PoV
5.0.9 (Debug SHA-256 `F1A10837636C6297D73612CF88AB52A12A8E8B98F1A39D85D7988646C85658BB`).
It checks partially filled banks, three-column wrapping, an absent bank,
closed occupied cells and shrinkage after withdrawal. A three-slot generic
bank plus one PoV slot uses four columns instead of six. Debug built without
warnings/errors, Mono exited 0, and the deployed DLL hash matched. These layout
checks inspect coordinates/dimensions; rendered UI still requires a game check.

## Release 1.6.9 verification — 2026-10-19

The final merged Release DLL (`4B1CE1F21FF3F001792C855D62D5A5A90EBAB6752B8DA37A5666A7858F901A25`)
passed **274 cold / 276 warm** checks against the original PoV 5.0.9 input above.
Both processes exited 0 and completed Mono cleanup. The unit runner passed
**189 tests**; static contracts reported zero failures (1,152 references,
163 Harmony targets, 52 reflected contracts; ten manual targets remain).
Debug and Release built without warnings/errors. Debug deployment hashes matched;
Thunderstore/Nexus DLL hashes matched Release. Plugin/assembly/manifest versions,
merged dependencies, package entries and packaged changelog were verified.

## Discovery progression follow-up (2026-10-19)

The Debug DLL now combines InventorySlots' discovery rows with PoV's row grant
using the larger extra-row count. Equipment discovery is checked after PoV's
node/option gate, including the Quick-kind utility roles. The isolated fixture
initializes the real progression options and supplies a stable player ID in
place of a network view. It simulates row grants through the original public
API getter; it does not download or run a live server's tree.

PoV **5.0.9** passed **302 cold / 304 warm** checks; **5.0.8** passed **266 cold**
checks. Every process exited 0 and completed Mono cleanup. Added checks cover
HardAntler's 4-to-5-row discovery/reveal, non-additive grants and node removal,
the fixed nine-row boundary, undiscovered base/utility cells, carried equipment,
discovery and Equipment On/Off, immediate panel-cache refresh on discovery-option
changes, node-locked trinkets and closed-cell withdrawal.
The metadata-only YAML suite additionally checks lasting discovery and cached
discovery under a closed PoV gate. The unit runner passed **189 tests** and the
static contract checker found **zero failures** (1,152 references, 163 Harmony
targets, 52 reflected contracts; ten manual targets remain). Debug build and
Steam plugin deployment succeeded with matching DLL hashes. This implementation
checkpoint used Debug only; the subsequent Release checks are recorded below.

## Release 1.6.10 verification — 2026-10-19

The final merged Release DLL (`7E412F35D4C849E1E43A357BDF5CF781E57043123BDC11FA54C0AA6F7BC0AD8C`)
passed **302 cold / 304 warm** checks against original PoV 5.0.9 and **266 cold**
checks against original PoV 5.0.8. All processes exited 0 and completed Mono
cleanup. The unit runner passed **189 tests**; Release contract inspection found
zero failures (1,152 references, 163 Harmony targets, 52 reflected contracts;
ten dynamic targets remain manual). Debug and Release built without warnings
or errors. Debug deployment hashes matched; Thunderstore/Nexus package DLLs
matched Release. Assembly/plugin/manifest versions, merged dependencies,
package entries and packaged source files were verified. Actual game and
multiplayer execution remain untested.

## Unified quick-slot HUD — 2026-10-19

Debug InventorySlots **1.6.10** (`11FDE0E9BE380B53B1D816C8D867FE0D947588455BFA41FA7576E04972DDB109`)
passed **323 cold / 325 warm** checks with original PoV 5.0.9 and **287 cold**
checks with original PoV 5.0.8. All managed/native exits were 0 and Mono cleanup
completed. No version bump or Release package was made.

`HudChecks.cs` exercises the real native Eat/Drink/CycleArrows methods and the
merged mod's provider, HUD list and notification hooks. Managed fixtures supply
capacity grants, permanent-potion status, final game use/equip bodies and message
services. Checks cover one use dispatch, both native-hotkey and HUD entry points,
permanent and closed potion/food restrictions, ammo cycling past empty slots and
failed equip, player loading, all four display modes, PoV-only layouts, cached
capacity reads, and preservation of reveal timers across inventory invalidation.
Native display fallback selection is checked with sentinel renderer state;
actual GameObjects, rendering and mouse events are not executed.

The unit runner passed **190 tests**. The layout fixture passed **33 HUD / 71
guide checks**, including six-column and PoV-only viewport bounds. Contract
inspection resolved 1,153 references, 163 declared Harmony targets and 52
reflected contracts with zero failures; ten dynamic targets remain manual.
Debug build had zero warnings/errors and the Steam plugins DLL hash matched.
Actual game checks must include AllOnUse/UsedSlotOnUse after PoV key use,
right-click food/potion and arrow cycling, belt/quiver changes, HUD hiding,
Follows Panel toggling, UI scaling and reconnect. No rendered-game pass or
measured frame-rate improvement is claimed.

## Release 1.6.11 verification — 2026-10-19

The final merged Release DLL
(`0CC35A1D4A2271DEC83B9191ED9A477F7849331E659E3503399E53E7C6EBF635`)
passed **323 cold / 325 warm** checks against original PoV **5.0.10**
(`252B60914EFC9F402A977A8C235E28BCB363D7BB7CEEBDF5F22A228518282E74`).
Both processes exited 0 and completed Mono cleanup. The same Debug build also
passed 323 cold checks. These are the managed fixtures described above, not a
complete game startup or gameplay test.

The unit runner passed **190 tests** and the layout fixture passed **33 HUD / 71
guide checks**. Release contract inspection found zero failures (1,153 references,
163 declared Harmony targets and 52 reflected contracts; ten dynamic targets
remain manual). Debug and Release builds completed with zero warnings/errors;
the Debug DLL matched the Steam plugins copy. Assembly/plugin/manifest version
1.6.11, merged dependencies and all Thunderstore/Nexus ZIP entries were verified
against their source files, including the final Release DLL. Actual game and
multiplayer execution remain untested.

## Separate Utility cell disabled — 2026-10-19

The post-release Debug DLL
(`560C093E095931547DD321534AF6CF6D781B9408B2BE597500B36158EE9F2C9A`)
passed **328 cold / 330 warm** checks against the original PoV 5.0.10 DLL above.
New cases cover omission of the exact `utility` role, preservation of the configured
Belt/Wishbone/Demister roles, native `State.Enabled` and public `Equipment.Put`
rejection, and the original utility equip prefix reporting failure without
replacing the belt. Saved native/provider Utility items recover by reference,
including shifted later slots; a full recovery grid rejects the entire move plan.
Both processes exited 0 and completed Mono cleanup. The unit runner passed 190
tests and static contract inspection found zero failures (ten manual targets).
Debug build had zero warnings/errors and the deployed Steam DLL hash matched.
No new version, Release package or actual game verification was performed.

## Release 1.6.12 verification — 2026-10-19

The final merged Release DLL
(`8A6392FC4D0E29EDDA93CA4201026F451754BAC72B1BBF2028C14824F180D0AF`)
passed **328 cold / 330 warm** checks against the original PoV 5.0.10 DLL above.
Both processes exited 0 and completed Mono cleanup. The unit runner passed 190
tests; Release contract inspection found zero failures (1,153 references, 163
declared Harmony targets, 52 reflected contracts and ten manual targets).
Debug and Release builds had zero warnings/errors. The Debug deployment hash
matched the Steam plugin copy. Assembly/plugin/manifest version 1.6.12, merged
dependencies and every Thunderstore/Nexus ZIP entry were verified against the
source files and final Release DLL. Actual game and multiplayer remain untested.

## Game checks still required

Use backed-up test characters to exercise first login from native PoV, full inventory, death/tombstone recovery, logout/reconnect, live node reset and Equipment On/Off. Check actual registered quivers, nearby crafting/quick stack, host/dedicated-server sessions and delayed/conflicting container operations. Visually check equipment/storage/HUD layouts across UI scales, craft/salvage/disenchant/VNEI tab changes and pinned/socket/rarity tooltips. No game or multiplayer pass is claimed by these isolated results.
