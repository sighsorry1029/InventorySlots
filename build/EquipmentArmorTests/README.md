# Equipment armor regression harness

Run `dotnet run --project build/EquipmentArmorTests/EquipmentArmorTests.csproj`.

This links the production `EquipmentArmor.cs` and
`CustomEquipmentProjectionCache.cs` into a minimal managed game host. It checks
slot opt-in behavior, live cache invalidation, quality-aware armor, unchanged
non-armor projections, invalid ownership/slot references, native accessory opt-in,
and native armor/custom-item double counting guards.

The game objects and existing slot-lookup helpers are test doubles. This does not
run Unity, Harmony patches, server sync, or third-party equipment mods and is not
an in-game compatibility result. YAML parsing/default checks run separately in
`InventorySlots.Tests`.
