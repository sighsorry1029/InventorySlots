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

## Why the old standalone process failed

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
