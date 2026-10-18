# Compatibility smoke checks on original Valheim Mono

These checks load the final InventoryActions DLL and original game/BepInEx
assemblies in an isolated process. They do not start Unity, a world, the BepInEx
preloader, or networking. The MUC modes can also load the original optional
MultiUserChest DLL. Framework configuration paths are redirected to a temporary
directory by the managed harness.

## Run on Windows x64

Prerequisites: .NET SDK/net48 targeting support, Visual Studio C++ x64 build tools
(including the Windows SDK), and an original Windows x64 Valheim installation
with BepInEx. No Unity Editor installation is required.

From the repository root, first build/deploy the final mod using the normal Debug
workflow, then run the isolated checks:

```powershell
dotnet build InventoryActions/InventoryActions.csproj -c Debug -p:DeployToGame=true
powershell -NoProfile -ExecutionPolicy Bypass -File InventoryActions/build/CompatibilitySmoke/Run-MonoSmoke.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File InventoryActions/build/CompatibilitySmoke/Run-MonoSmoke.ps1 -Mode muc-restock -MucDll reference/MultiUserChest.dll
```

The default mode is `muc-session`. Other modes are listed in the script's
`ValidateSet`; `basic` runs the original default checks. `muc-api` and
`muc-restock` require `-MucDll`. Use `-ModDll` to select another final mod build.
The default game directory comes from `environment.props`; `-GamePath` selects
another complete installation and is also passed to the harness build.

The script builds only the managed harness and the small x64 `MonoHost.cpp`
executable, under ignored `bin/obj` directories. It does not deploy the host,
change any game/runtime/library files, build the mod, or create release ZIPs.
The host loads that game's `MonoBleedingEdge/EmbedRuntime/mono-2.0-bdwgc.dll`,
uses `valheim_Data/Managed` for its original core/game assemblies, executes the
test, calls `mono_jit_cleanup`, and returns the managed exit code unchanged.
Build failures and native failures remain failures. A PASS line alone is not
sufficient: check the process exit code as well.

## Path of Valheiman adapter checks

After building/deploying InventoryActions Debug, run:

```powershell
./InventoryActions/build/CompatibilitySmoke/Run-MonoSmoke.ps1 -Mode pov -PovDll '<original PathOfValheiman.dll>'
```

On 2026-10-18, the original PoV 5.0.8 DLL (SHA-256
`3E4AFE1A7DCF52612B93DCA2A2C70A27C3CD0DF19BF852D87F6365F81BA7E3DA`)
and the original Valheim 1.0.20 client assemblies/runtime passed 56 isolated
checks with process exit 0 and normal Mono cleanup. They cover original public
API delegate bindings, API major/capability rejection before hook installation,
retained native hooks, loading/delayed-release boundaries,
protected source and destination cells, original PoV Sort execution, empty
favorites and HarmonyX rejected/accepted/duplicate response state. Invalid row
values and query exceptions fail closed. API-reported external-provider and
suspended states block actions, then clear without a stale cached boundary.
The existing game access modifiers are preserved; the mod has no compile-time
PoV reference.

Release **1.1.16** was also checked against original PoV **5.0.9** (SHA-256
`6D01948786BF2E1527D6801670E4D14ADBEC89E5E3809F1D8F8840E56C7D8165`)
on 2026-10-18: **56 checks**, process exit 0 and normal Mono cleanup.
The final merged Release DLL SHA-256 is
`0BBA7F848C2FD4270FEA3E2420B64BA0CE356EC0A593552A0F118A44F2A2E81F`.
Use `-ModDll InventoryActions/bin/Release/InventoryActions.dll` to select it.
Release contract inspection reported 665 references, 51 Harmony targets and
seven reflected contracts, with zero failures and two manual targets. Debug
deployment and packaged Release DLL hashes matched their respective build outputs;
package metadata, changelog, README and translations were verified.

The fixture sets the real 5.0.8/5.0.9 `EquipRows.start` boundary and six hidden rows.
Increasing physical height alone must not increase allowed backpack capacity;
changing PoV's actual boundary does. The previous 5.0.5 fixture assumed four
hidden rows and failed this boundary assertion against 5.0.8 before the update.
That baseline assertion failure exited 1 with normal Mono cleanup, distinct from
the historical native shutdown failure described below.

This mode supplies inert animation-hash/object-offset internal calls, managed
object identity and a test character ID, and skips localization resource loading.
It uses uninitialized managed Player/Container sentinels, never native Unity
objects. Public Storage getters are stubbed to report registered/suspended
provider states; this does not validate a real external provider or its item
storage. The public `AtLeast` wrapper can report an unsupported major, and the
in-memory feature list temporarily omits storage to test capability rejection.
The RPC body is an observer, so its checks establish wrapper state and
prefix ordering, **not** sender validation, network transfers or multiplayer safety.
Completed PoV replies are fenced until another permitted native request (or a
network stop). Vanilla's RPC has no request ID: an old reply arriving after a new
request cannot be distinguished by this fence alone.
No game files are modified by these stubs. Actual UI, node/key input, equipment
settlement, BigBag changes, full inventories, host/remote chests, delayed responses,
MUC combinations and save/death/reconnect still require game testing.

The unchanged `muc-restock` mode also passed 22 checks with exit 0 during this
update, without PoV loaded. The generic `basic` mode remains unable to initialize
Player without Unity's `Animator.StringToHash_Injected` internal call: it exited
1 with normal cleanup against both the pre-change 1.1.15 Release DLL and the
updated Debug DLL. This is a limitation of that standalone mode, not a passed
regression check or the old Mono shutdown crash. The PoV mode supplies its
explicit inert engine stubs as described above.

## Why the old standalone process failed (historical)

On 2026-10-11, the previous Unity Editor 6000.0.61f1 `bin/mono.exe` was found to
be **x86**, despite the installed Editor and game being x64. The process printed
all assertions and failed during shutdown. Capturing the native exit directly
showed `0xC0000005` (access violation), rather than only the earlier command
wrapper's generic exit 1.

Minimal probes isolated the failure:

- A plain managed entry point, BepInEx path setup, the managed dispatch queue,
  and loading the plugin type without initializing it exited normally.
- Initializing `HarmonyLib.AccessTools` alone reproduced the shutdown failure;
  no InventoryActions action or MUC request was needed.
- Inspection of the installed Harmony/MonoMod code found the path through
  `AccessTools.PrepForRemoting`, `MethodInvoker.GetHandler`, and the generated
  `ILGenerator` proxy to `SetMonoCorlibInternal`. That helper writes to a native
  Mono assembly structure using assumed layout offsets. This is a plausible
  runtime/architecture mismatch mechanism, not proof of the precise corrupted
  field. Switching DMD generators did not avoid this initialization path.
- Both an x64 Editor embedding probe and the original game x64 runtime completed
  the same initialization and normal Mono cleanup. The checked-in runner uses
  the game's matching runtime and original managed assemblies.

The managed entry point now rejects x86 Mono before loading game/Harmony code.
It does not change production compatibility policy. No Harmony/MonoMod binary
patch, swallowed assertion, forced successful exit, or skipped cleanup is used.

## Verification on 2026-10-11

Inputs: InventoryActions 1.1.13, installed Valheim client 1.0.17 / Steam build
25730771, and the existing original `reference/MultiUserChest.dll`.

| Check | Result |
| --- | --- |
| `muc-session` | 10 checks, normal cleanup, process exit 0; repeated via Windows PowerShell 5.1 from an unrelated working directory |
| `muc-restock` | 22 checks using real MUC/Harmony methods, normal cleanup, process exit 0 |
| Original x86 Editor Mono invocation | Clear x64 requirement, clean exit 2 before Harmony initialization |
| Valid assembly without the expected plugin type | Managed exception, normal cleanup, script exit 1 |
| Small managed probe returning 7, located in a Unicode path with spaces | Normal cleanup, native host exit 7 |
| Actions Debug build/deploy | Full rebuild succeeded with 0 warnings/errors; final and installed DLL SHA-256 matched |

The first incremental mod build hit a separate native PDB writer failure in
ILRepack (`Mono.Cecil.Pdb.ISymUnmanagedWriter2.Close`). An unchanged full Debug
rebuild succeeded; no build configuration change was made. This is distinct
from the standalone Mono shutdown failure and is not claimed fixed here.

Verified SHA-256 values:

- Original game Mono runtime:
  `35FD9D8065EE84D0C9312B4B3BB19ECA3C1196C952E66D74DC8895CFFF0AA028`
- Original `assembly_valheim.dll`:
  `25A0A107DCE4D834C44C2B72D0EAFD5CB7793933BDA81816ACCFA1EA9543DACE`
- Final rebuilt Debug InventoryActions DLL and installed plugins copy:
  `4EB31EE6EFB54AD445725BC69AE891DAA099D1C26EBAAEE73C6092E858A56D60`

These results establish isolated managed checks and clean process teardown on
the tested runtime. They do not validate Unity internal calls, actual UI,
multiplayer transfers/refunds, disconnect/reconnect, dedicated servers, or
crossplay. Other harness modes and other runtime versions need their own checks.
