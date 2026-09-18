# Valheim 1.0.15: InventorySlots startup patch

The reported `vvvv/BepInEx/LogOutput.log` identifies Valheim 1.0.15, not
1.0.14. InventorySlots 1.5.3 was built for the four-argument
`Inventory.FindFreeStackItem(string, int, float, bool)` in 1.0.14.
Original client build 25390630 restores the private three-argument method.
Harmony therefore aborted plugin initialization before this fix.

Original client evidence is preserved at
`C:/Users/blizz/.codex/references/valheim/snapshots/client-b25390630-windows-x64-20260918T131715Z`.
Its extracted `assembly_valheim/csharp/Inventory.cs` shows the three-argument
declaration and calls. No publicized DLL was used for analysis or validation.
All 141 managed assemblies extracted successfully; the 995-file original
snapshot passed post-extraction integrity verification.
The dedicated-server capture was rejected because Steam reported a non-idle
installation state; this is not a dedicated-server compatibility validation.

## Change and scope

- Bind the explicit Harmony target and prefix to the three-argument method.
- Remove the now-unavailable cheat-marker argument from the lookup helper.
- When an AddItem source is available, retain the existing identity and
  metadata checks. Without a source, do not invent a cheat-marker value:
  native 1.0.15 lookup also does not filter that marker.
- Keep locked-cell restrictions and existing Sort, Fill, QuickStack and
  Restock policies. InventoryActions runtime code is unchanged; only its
  shared isolated test harness is updated for the Slots helper signature.

This targets current 1.0.15, not simultaneous 1.0.14 execution support.
It does not skip the failed patch, suppress startup exceptions or alter
network ownership. The authorized release publishes the fix as 1.5.4.

## Validation (2026-09-18)

- Before: released DLL against original installed 1.0.15 assemblies reports
  one failure, precisely the missing FindFreeStackItem Harmony target.
- After: Debug build/deployment succeeds, zero warnings/errors; 1,025 member
  references, 135 explicit Harmony targets and 42 reflected contracts have
  zero failures (nine manual-review entries remain).
- 171 automated tests pass. Existing StuWardCompat nullable warnings remain
  in the test project.
- Isolated original-assembly stack/favorite checks: 32 assertions pass on
  Unity Editor Mono; process then exits -1073741819, the previously recorded
  standalone runner shutdown fault. This is not a clean process pass.
- The pre-release Debug DLL and Steam plugins copy had identical SHA-256:
  `419084f01516b33262335d4b68b82079d9daa70964edab91db76c636e4dc2582`.
- Final InventorySlots 1.5.4 Release DLL SHA-256:
  `5ce59304c1b1b997655afc1b556d79709313068d845f7459942d620554d5c09c`.
- Thunderstore ZIP SHA-256:
  `10719e0066f27395c4236874fc25d6b3b6f81f3594e5cda8d91a1211532dfd9f`.

Reports are ignored local files `obj/compatibility-1.0.15-before.json`,
`obj/compatibility-1.0.15-after.json`, and `obj/stack-smoke-1.0.15.log`.
Actual game startup, profile mod composition, pickup/stacking and multiplayer
have not been executed. The supplied Gale profile was not overwritten.
