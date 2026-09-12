# Valheim 1.0.7 compatibility patch

Prepared 2026-09-10 for Windows x64 client build 25185596 and dedicated server build 25185644. Released as InventorySlots 1.4.7 after separate release authorization.

## Behavior

- Normal rows are vanilla character rows plus 0-3 InventorySlots progression rows, capped at the existing nine-row regular storage boundary. Defaults are HardAntler, CryptKey and Wishbone, sequentially. DragonTear/YagluthDrop no longer unlock mod rows. Existing first-three configuration keys keep their meaning; Maximum Extra Rows is capped at 3.
- Haldor purchases remain vanilla transactions, with vanilla world unlocks, prices, unique purchase keys and `invrows` values. No new purchase RPC or save format was added. The mod does not turn discovered boss drops into free purchases.
- Equipment/quick-slot storage still starts at y=9. The native resize patch changes only the physical height and UI calls within Player.SetInventorySize. Its input and vanilla `invrows` write remain native; invalid-coordinate cleanup remains enabled. Missing/duplicated interception sites abort patch installation.
- Native row state is cached per Player with weak ownership, reset before loading, and refreshed after loading/resizing/character resets. The per-frame row getter does not repeatedly split the unique-key strings. The UI reveals newly purchased rows and keeps occupied locked cells accessible for recovery; insertion/stacking/sorting still use the actual unlocked extent. No blanket opening of old locked rows or data deletion occurs.
- New InventoryElement components replace the removed InventoryGrid.Element wrapper. Destroyed Unity objects are checked before accessing gameObject. Favorite mouse clicks intercept OnLeftDown/OnRightDown at the new input phase. Vanilla touch-only OnLeftClick and controller selection paths remain separate.
- Trash confirmation clones the new SplitDialog, uses its public acceptance/cancellation events and slider/icon APIs, and caches reflection access to its two private button references. The clone owns and cleans up its listeners with its own lifetime.
- The native ten-argument positional AddItem and five-argument private positional overload are targeted explicitly. Existing upgrade Prefix/Postfix/Finalizer state and Harmony priorities remain. The original skipValidPositionCheck argument is not forced on; normal placement restrictions and load preservation are kept.
- TakeAllResponse spelling and tombstone revision invalidation match the new Container API. Tombstone reload still respects the native in-use guard and requires a valid network view.
- Multi-user container persistence compares the native ZDO byte array with the serialized inventory bytes, preserving owner checks, rollback and duplicate receipt handling. Item codec version is 2 and outer protocol version is 4; old/malformed wire versions remain rejected. `m_cheated` is transmitted, copied into snapshots and checked for exact identity. Stack merges propagate it with the native bypass condition, separately from exact request identity. Local receipt invokes the native change notification with the new flags.
- Custom equipment visual quality is passed to the new attachment signatures via cached delegates and included in visual cache identities and owner-authorized ZDO updates. Other mods' visual ownership checks remain.
- The vendored ServerSync is the shared `valheim-1.0.7-r1` baseline; see [provenance](../Libs/ServerSync.md). It is internalized into the final plugin DLL.

## Compilation and deployment

The previous project used stale, manually publicized game files. It now uses SDK-style MSBuild and BepInEx.AssemblyPublicizer.MSBuild **0.4.2** to derive compiler-only references from the installed originals under obj. This preserves the existing broad private-UI access design instead of rewriting it throughout this compatibility patch. Analyses and validation resolve against preserved originals; original game DLLs are not publicized in place, overwritten, copied into plugins, or packaged.

The [publicizer's documented runtime strategy](https://github.com/BepInEx/BepInEx.AssemblyPublicizer) enables unsafe compilation for Mono and emits IgnoresAccessChecksTo attributes. The attributes for the three existing game assemblies are verified in the final merged DLL. This is **not** proof that every private access works in the game's Unity/Mono runtime. Newly changed private attachment/button/change-notification paths use explicit cached reflection accessors/delegates.

`ReadModAssemblyVersion` uses a distinct name from the SDK's built-in GetAssemblyVersion target, ensuring the final merged DLL is copied after ILRepack. Normal invocation:

```powershell
dotnet build InventorySlots.csproj -c Debug -p:DeployToGame=true
dotnet run --project InventorySlots.Tests/InventorySlots.Tests.csproj -c Debug
```

Only InventorySlots.dll is deployed to the configured game plugins directory. Its hash must match bin/Debug/InventorySlots.dll. No Release ZIP, version bump, or uploader action is part of this patch.

## Automated verification

- Debug compilation and ILRepack: zero warnings/errors.
- Existing/new tests: 160 passed, including all twelve ordinary native/mod row combinations, old row-cap constraints, occupied-row recovery bounds and cheated-state identity versus stacking behavior. Some existing regression tests assert source wiring; they do not simulate Unity.
- [CompatibilityCheck](CompatibilityCheck/Program.cs): the compiled plugin is resolved against each original client/server Managed directory. Each check resolves 977 distinct game references and 116 explicit Harmony targets, including named argument/result/field-injection types. Both passed with no confirmed contract failures. It rejects runtime loads of literal fields and verifies merged ServerSync and runtime access attributes.
- Six optional external-mod dynamic targets retain their Prepare guards and were reviewed in source; those external implementations were not run. ServerSync's method-level VersionCheck declaration is covered by the shared baseline verification rather than this class-level scanner. The seven entries are retained in reports as manual-review boundaries.
- [HarmonySmoke](HarmonySmoke/Program.cs): six checks per original target execute the compiled row transpiler on the original Player.SetInventorySize IL. They check both interception sites, retained native state write/cleanup, ordering, and rejection of an incompatible method body. The harness runs on .NET 9 and reads the original 59-byte method directly; it does not install all Harmony patches or initialize Unity. Older HarmonyX/MonoMod's runtime IL copier is not used in this harness.

Local reports/logs are under `artifacts/valheim-1.0.7` (git-ignored). These tools never use the publicized compiler copies as compatibility evidence. For another machine, pass its original Managed and BepInEx core directories explicitly:

```powershell
dotnet run --project build/CompatibilityCheck -c Debug -- bin/Debug/InventorySlots.dll <original-Managed> <BepInEx-core> artifacts/valheim-1.0.7/api-check.json
dotnet run --project build/HarmonySmoke -c Debug -- bin/Debug/InventorySlots.dll <original-Managed> <BepInEx-core>
```

## Required game checks

No game, host or dedicated-server session was launched for this patch. Validate on copies of a character/world before using a main save:

1. New character 4 rows; each mod unlock; each pocket alone and both pockets; maximum 9 rows. Check progressive Off and Maximum Extra Rows=0 independently of purchases. Each purchase must charge once, record its native key once, and add one row.
2. Existing character with occupied rows 8-9 and no purchases: every item remains present and can be withdrawn; empty locked cells reject insertions. Check full inventory, worn equipment/quick slots, save/reload, another world, character reset, death/tombstone take-all and keep-on-death modes.
3. Mouse favorite modifier must not also drag/use the item. Check controller/touch paths, pinned/hover upgrade tooltips, crafting/upgrade/socket panels, trash accept/cancel/escape/reopen, UI destruction and recreation, and quick-slot HUD hiding.
4. Full-inventory equipped upgrades, failures before/after AddItem, optional Jewelcrafting/EpicLoot/Recycle_N_Reclaim/backpack paths, quality-dependent visuals locally and on a remote player.
5. Host and dedicated server with two clients: concurrent chest operations, revision/owner changes, permission rejection, duplicate request/ACK loss, rollback, disconnect/reconnect and persisted byte payloads. Verify counts, coordinates, metadata and cheated flags after transfer/merge/split/reload.
6. ServerSync initial/large config sync, non-admin rejection, admin-file changes, version mismatch, reconnect and Steam/PlayFab sessions with multiple embedded ServerSync copies.

Static checks and isolated transpiler tests do not establish these runtime results, optional-mod compatibility, non-Windows support, or complete indirect-reflection coverage.
