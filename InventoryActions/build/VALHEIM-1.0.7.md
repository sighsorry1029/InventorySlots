# InventoryActions Valheim 1.0.7 compatibility

Applied 2026-09-10 and released as InventoryActions 1.0.9 after separate release authorization.

## Problem and changes

The supplied client log repeatedly throws `MissingMethodException: void Inventory.Changed()` from InventoryActions.Update. The original 1.0.7 Inventory exposes only private `Changed(bool success = false, bool cheatedStateChanged = false)`. Notification call sites now use a cached Harmony open delegate with the explicit two-boolean signature. The existing flushes pass false/false; native item transfers retain their own success/cheat notifications and ownership checks. Failure cleanup still clears pending session/lease state before callbacks.

Additional version boundaries:

- `InventoryGrid.Element`/`m_go` replaced by original `InventoryElement`/`gameObject`, preserving Unity destroyed-object guards and cached favorite borders.
- Favorites intercept `OnLeftDown`, where desktop item selection now happens. The touch-specific `OnLeftClick` implementation is left intact.
- Trash confirmation clones the new `InventoryGui.m_splitDialog`, uses its public icon/slider/events, and caches explicit accessors for the two private buttons. The dialog owns its native button listeners. Closing unsubscribes the mod events, clears pending references, and destroys the clone; plugin teardown also closes it. Existing item/drag/favorite revalidation remains before deletion.
- Area ownership leases reject open/take-all requests using current registered `RPC_OpenResponse` and `RPC_TakeAllResponse`. `RPC_StackResponse`, lease tokens, permissions, timeouts, duplicate suppression, and MultiUserChest restrictions are unchanged.
- Player action bounds and button placement follow the loaded inventory height, including native purchased rows. Favorites can cover the hotbar; trash, quick stack and player sorting continue to exclude it.
- Direct sort consolidation separates `m_cheated` groups to preserve the new marker. External custom-data stack protection stays in place; native transfer semantics are retained.
- Settings keys, favorite coordinate text files, mod IDs/version negotiation and optional MultiUserChest dependency remain unchanged. InventorySlots/Quick Stack Store incompatibilities remain declared. BepInEx manifest dependency corrected to 5.4.2350.

## Build and reference strategy

The previous non-SDK project used pre-generated publicized game DLL paths and did not provide reliable `dotnet build`/ILRepack integration. The project now uses the existing InventorySlots SDK build pattern. `ReadModAssemblyVersion` avoids the SDK's `GetAssemblyVersion` target name, and Debug deployment runs after final DLL merging.

Build inputs come from installed `CorlibPath` original DLLs, verified equal to the saved 1.0.7 client snapshot. Existing direct private UI accesses remain: BepInEx.AssemblyPublicizer.MSBuild 0.4.2 generates compiler-only references under obj and runtime access attributes for assembly_valheim/assembly_utils/assembly_guiutils. **Compilation uses these generated references; it is not an all-public original-API build.** Original game files are never overwritten or packaged. Static verification uses the original client/server DLLs. New Changed and SplitDialog button access is explicitly cached through Harmony.

ServerSync is the already pinned parent `Libs/ServerSync.dll`, common `valheim-1.0.7-r1`, SHA-256 `b4dd786997f4e90d770f09ef3e9d64154754fe7e8edfb4841795751895b35846`. It is merged into the final DLL; no standalone shared plugin is installed. See `../../Libs/ServerSync.md`.

## Verification

Run from the InventorySlots repository root:

```powershell
dotnet build .\InventoryActions\InventoryActions.csproj -c Debug -p:DeployToGame=true
dotnet run --project .\InventorySlots.Tests\InventorySlots.Tests.csproj -c Debug
dotnet build .\InventoryActions\build\CompatibilitySmoke\CompatibilitySmoke.csproj -c Debug
```

Results: Debug 0 warnings/errors, existing test suite 160 passed. The three existing source-contract assertions were updated for dynamic purchased rows and the explicit notification helper, retaining callback-order/movement guards.

`build/CompatibilityCheck` accepts `<final mod.dll> <original Managed> <BepInEx core> <report.json>`. The final DLL passes against both targets: 385 unique direct game references and 23 explicit Harmony targets, zero failures. The remaining method-level ServerSync.VersionCheck declaration is covered by the pinned common library review, not claimed as an automatically checked patch here. The two dynamic localization patch targets, private Changed signature, private SplitDialog buttons and native response registrations were separately checked against original metadata/IL.

`InventoryActions/build/CompatibilitySmoke/bin/Debug/net48/CompatibilitySmoke.exe` accepts `<final mod.dll> <original Managed> <BepInEx core>`. Twenty isolated checks pass per client/server target using actual compiled private mod methods and unmodified original game assemblies: notification callback/weight update, 4/5/6-row bounds and hotbar exclusion, mixed cheat-marker preservation, same-marker consolidation/quantity conservation, and external custom-data protection.

The smoke harness supplies only an uninitialized BepInEx managed dispatch queue; ServerSync startup callbacks are queued but never executed. Unity objects and native networking are not constructed. Desktop .NET Framework lacks default-interface support needed by original Player interfaces, so Player-dependent action execution is explicitly not run. The harness's local loadFromRemoteSources setting permits loading the known BepInEx test inputs without changing their download metadata. These results are **not Unity/Mono or live game verification**.

Final DLL SHA-256 (build and plugins deployment match): `2eff31c16ed557168e24f6a697b2602460ea95f009f9aea0fcb39f57f39f98d6`.

## Remaining game checks

Restart the game to load the new DLL. Verify startup/idle no longer logs the missing-method error; Alt favorite clicks do not start a drag; trash confirm/cancel/Enter/Escape, inventory hide and teardown release pending state; purchased rows participate in actions with hotbar/favorites protected. On host and dedicated-server sessions, check area handoff, busy/unauthorized rejection, timeout/disconnect cleanup, duplicate requests and external MultiUserChest restrictions. Compare item counts, positions, qualities, custom data and cheat markers through moves, sorting, death recovery and reconnect. New metadata/static checks do not prove all 29 existing nonpublic call paths work on target Mono or that other mods' patch composition is safe.

Full immutable game snapshot paths, original input fingerprints, pre-fix log/DLL, build/test logs and final reports are in `C:/Users/blizz/.codex/references/valheim/comparisons/0.221.12--1.0.7-windows-x64/inventoryactions-compatibility-20260910`.
