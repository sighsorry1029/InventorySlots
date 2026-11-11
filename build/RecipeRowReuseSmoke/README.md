# Native recipe row reuse validation

`RecipeRowReuseSmoke` targets net48 and runs in the game's bundled Mono through
the existing `InventoryActions/build/CompatibilitySmoke/MonoHost.cpp` launcher.
It does not start Unity or a game session. BepInEx paths/threading and two engine
initialization calls are isolated boundaries; game and mod method IL is original.

Build the mod with `dotnet build InventorySlots.csproj -c Debug -p:DeployToGame=true`,
then build this test with `dotnet build build/RecipeRowReuseSmoke/RecipeRowReuseSmoke.csproj -c Debug`.
Run the launcher with: Mono runtime DLL, runtime Managed directory, Mono etc
directory, test EXE, final InventorySlots DLL, **original** game Managed directory,
BepInEx core directory, `test`, then optional PoV / VeiledRecipes /
ServerDevcommands / WarfareTweaks DLL paths (all four for the combination check),
then optionally the current InteropFixes DLL to exercise its actual lookup transpiler.
`shapes` instead of `test` prints the relevant normalized body fingerprints.
`build/PathOfValheimanSmoke/Run-MonoSmoke.ps1` documents compilation of that same
native launcher; no additional launcher implementation is required.

Checks:

- Native Destroy/Instantiate/listener call substitutions leave all other native
  instructions, operands and branch destinations intact.
- Unrecognized native IL is left unchanged, with row reuse disabled.
- Harmony compiles both replacements on game Mono with unmodified game DLLs;
  the private recipe list is never directly accessed by the reuse implementation.
- Actual current hooks are registered incrementally. Unknown late list hooks,
  changed helper bodies/wrappers and a known method registered under the wrong
  patch kind disable reuse only. Removing those patches restores eligibility.
- Only helpers belonging to registered hooks are checked. An unrelated helper
  change does not block reuse; registering its parent hook rechecks that helper.
- A body-preserving transpiler from an unlisted owner remains eligible. The actual
  InteropFixes optional type/field lookup substitutions are normalized only in
  PoV's reviewed cleanup method, after verifying the lookup helpers themselves.
  The remaining cleanup body, branches and exception regions must still match.
- InteropFixes registered before/after the reuse patches remains eligible. Removing
  row destruction, skipping the cleanup callback or changing lookup helpers is
  rejected. Helper patch/unpatch invalidates eligibility even when caller patches
  have not changed. Unchanged registrations do not repeat IL inspection.
- NOP differences do not change the normalized body shape; a changed UI write does.

Validated inputs (2026-11-11): Valheim 1.0.21 Windows client build 26249040,
PoV 6.0.8, VeiledRecipes 1.1.7, Server devcommands 1.116, WarfareTweaks 1.0.5.
Run logs and original DLL SHA-256 values are in
`artifacts/recipe-row-reuse-20261111`; the InteropFixes follow-up is in
`artifacts/recipe-row-guard-20261111`. Method fingerprints intentionally omit
assembly version, MVID and metadata tokens; they are conservative contract guards,
not proof of semantic equivalence. Only the reviewed row/list hooks and direct
UI/pairing dependencies are inspected, not every integration in an assembly.
Unknown native row/list decorators still require review; this is not a global
allow-list for InteropFixes or arbitrary transpilers. Arbitrary outside changes
to row templates or unobserved code cannot be certified by this check.

Original bodies/helper references are cached for the loaded assembly's lifetime.
At each rebuild, patch method/owner/order metadata is checked for callers and
dependencies. Changed registrations invalidate the cached transformed body check.
Harmony's `GetCurrentInstructions` reruns transpilers for that inspection; it does
not prove an arbitrary state-dependent transpiler produced the same installed
body. The reviewed InteropFixes transform depends on incoming IL. Runtime DLL
replacement and unregistered/state-dependent code rewriting are not supported.

The managed `CraftingFrameTests` exercises row lifecycle and click correctness
with simulated Unity objects. The Mono smoke checks do **not** measure FPS or
verify actual rendering. In game, compare normal/nocost and List/Grid; separate
the first build from repeated opens. Also check selection/click once, crafting,
upgrades after durability/quality changes, unknown-to-known recipes and language,
station changes, VNEI/Salvage/Runic Anvil tabs, character/logout and rapid Tab.

No unused row reserve survives a rebuild. Native rows still represent the full
current recipe list; this change is not virtualization and does not remove the
cost of discovery, requirements, row field refresh, or custom visible UI work.
