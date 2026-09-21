# Optional server installation patch validation

2026-09-21; InventorySlots 1.5.7 / InventoryActions 1.1.1 development builds.

## Installation policy

Both mods allow a modded client to join a server without that mod. A server running the mod requires the same mod version on each connecting client. ServerSync configuration/YAML synchronization is retained when both sides install the mod; local configuration applies otherwise.

`OptionalServerSupport.cs` sends one version announcement before the normal connection handshake, validates it before accepting PeerInfo, and binds capability state to the current connection. ServerSync's `ModRequired` is false so it no longer imposes a symmetric requirement. The vendored library is unchanged: `Libs/ServerSync.dll`, SHA-256 `B4DD786997F4E90D770F09EF3E9D64154754FE7E8EDFB4841795751895B35846`, shared by both projects and merged into each final plugin.

## Container behavior

On a server without the mod, area quick stack/restock uses the existing game `RPC_RequestStack` ownership protocol. It waits for both approval and the expected owner revision, then loads the current inventory before moving anything. Favorite protection, restock limits and chest leave-one rules remain in the existing transfer code. A cancelled or timed-out request cannot cause the later untagged response to execute vanilla StackAll; the chest remains fenced until that response arrives or the connection ends. A new area action also waits until an ordinary native stack request is no longer outstanding.

The modded-server custom protocol is retained. InventorySlots disables its built-in shared-chest behavior in vanilla-server mode because that protocol requires participating owners; access is exclusive as in the game. Success effects are local in vanilla-server mode. Custom-slot equipment attachments require the mod on the viewer. InventoryActions and InventorySlots remain alternatives, not a supported simultaneous installation.

## Automated verification

- Both projects: Debug build with `DeployToGame=true`, zero warnings/errors; final merged DLL hashes match the copies in the Steam Valheim `BepInEx/plugins` directory.
- Existing InventorySlots suite: 172 passed. The existing FX routing assertion now covers both custom and native handoff branches.
- Item rules/favorite memory: 102 passed per mod.
- Controller dispatcher: 73 InventorySlots / 71 InventoryActions passed.
- Source-linked server policy host: 38 passed per mod; absent/mismatched/matching peers, reconnects and stale callbacks.
- Source-linked native handoff host: 143 passed per mod; reordered response/data, ownership races, load failures, cancellation, timeout, ordinary native requests, destruction and StopAll cleanup.
- Final merged DLL contracts against original Valheim 1.0.15 client and dedicated-server assemblies: zero unresolved contracts on either role. InventorySlots: 1,064 references, 149 static Harmony targets, 49 reflected contracts. InventoryActions: 609 references, 43 static Harmony targets, 7 reflected contracts. Existing dynamic/manual review entries remain 10/2; all newly added Harmony targets are statically checked.

The API checker explicitly verifies the new cached private accesses to `Container.m_nview`, `m_lastRevision`, `m_loading` and `Load()`. Existing compiler publicizer settings remain unchanged; analysis and contract verification use original assemblies. No original game DLL was modified.

Original reference snapshots under `C:/Users/blizz/.codex/references/valheim/snapshots/`:

- `client-b25390630-windows-x64-20260918T131715Z/original/valheim_Data/Managed`
- `dedicated-server-b25390671-windows-x64-20260918T185703Z-depot-restored/original/valheim_server_Data/Managed`

Local JSON contract reports are under ignored `artifacts/ClientOnlySupport/`. Debug DLL SHA-256:

- InventorySlots: `D50FF03E10AD1B710AB88066B7918D8EF311CA83B5A6724146F02A7824485228`
- InventoryActions: `A50386FFDB5E12627B2B11BDE1B7EBFAE94F79940CA89EE5609AAFB48E4CF5D1`

## Execution limits

The fake hosts do not execute Unity or install Harmony patches, and metadata checks do not prove network behavior. Actual Steam/PlayFab connections, an unmodded player owning a chest, concurrent chest access, and death/rejoin with extra slots still require in-game validation. No live dedicated server or crossplay session was run for this patch. No version bump, Release package, commit or push was performed.
