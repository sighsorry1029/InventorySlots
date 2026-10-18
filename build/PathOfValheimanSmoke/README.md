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

The runner builds its test executable and the repository's existing `InventoryActions/build/CompatibilitySmoke/MonoHost.cpp` with the Visual Studio C++ x64 tools. Logs and input hashes go under ignored `obj/MonoHost-x64`. Cold mode lets the bridge create the API caches; warm mode initializes them first. Both test cache restoration after unregistration/unpatching. The reference omission assertions describe PoV 5.0.8/5.0.9; a later PoV fix requires reviewing those expectations, not blindly updating them.

## Coverage and limits

- Optional dependency loading: final InventorySlots types enumerate before loading PoV, without a mandatory PoV assembly reference.
- Real reflection/delegate bindings, dynamic public storage-provider interface, guarded Harmony installation and the native HUD transpiler.
- Original eight-purse-cell omission, 43/46 cell descriptors and 36 storage cells, cached `Worn` correction, cold/warm rollback and forwarding of the exact player/item references.
- Actual provider registration, accepted/rejected placement, occupied destinations, full backpack rejection, closed-cell withdrawal, suspended/loading guards and coin placement in an unlocked purse.
- Native and provider save-coordinate conversion, same-reference conservation, missing-slot recovery, no-space rejection before mutation, failed-load protection and subsequent-load recovery.
- Public quiver registration callbacks: successful insertion/removal, UI right-click with a full/free backpack, Off-state cleanup through the checked native fallback, and exceptions after partial callback effects.
- Purse weight while On/Off, public affix/rarity bindings, repeated affix values, and the current salvage-return signature.
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

## Game checks still required

Use backed-up test characters to exercise first login from native PoV, full inventory, death/tombstone recovery, logout/reconnect, live node reset and Equipment On/Off. Check actual registered quivers, nearby crafting/quick stack, host/dedicated-server sessions and delayed/conflicting container operations. Visually check equipment/storage/HUD layouts across UI scales, craft/salvage/disenchant/VNEI tab changes and pinned/socket/rarity tooltips. No game or multiplayer pass is claimed by these isolated results.
