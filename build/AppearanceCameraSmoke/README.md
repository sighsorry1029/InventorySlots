# Appearance camera contract check

Build the mod with `dotnet build InventorySlots.csproj -c Debug -p:DeployToGame=true`.
Build this isolated test with `dotnet build build/AppearanceCameraSmoke/AppearanceCameraSmoke.csproj -c Debug`.
Use the repository's native Mono host (`InventoryActions/build/CompatibilitySmoke/MonoHost.cpp`,
build instructions in `build/PathOfValheimanSmoke/Run-MonoSmoke.ps1`):

```powershell
& $monoHost $originalMono $originalManaged $monoEtc $testExe $modDll $originalManaged $bepInExCore
```

`GetCameraPosition(float, out Vector3, out Quaternion)` and `m_camera` must retain
their original private access. The test installs the production hook before and
after a competing prefix, compiles field/out/ref/`__runOriginal` injection on
game Mono, invokes the patched wrapper with the native engine body skipped,
checks unchanged output and removes both owners. Loader/Unity startup services
are fixtures; no game world, collision or rendered camera is exercised here.

2026-11-11: passed with original Valheim 1.0.21 client build 26249040. The managed
`CraftingFrameTests` also passed 106 camera checks. Logs/contract report and final
Debug deployment hashes are in `artifacts/pov-appearance-camera-20261111`.
The whole project retains its existing publicizer build configuration; this new
hook binds private access explicitly through Harmony and was checked against
unmodified originals. No Armoire dependency is added.

Game checks still needed: front/full-body framing with narrow and ultrawide
screens and moved panels; left-drag only in empty space; left-click item drop/transfer, right-click equip and
cancel-item-drag; indoor walls and slopes; tab/dialog switches and rapid reopen;
death, teleport, attachment, logout, focus loss and other camera mods. Verify
unchanged zoom/facing and normal camera restoration. These automated checks do
not establish game/multiplayer execution or FPS improvements.
