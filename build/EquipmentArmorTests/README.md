# Equipment armor regression harness

Run `dotnet run --project build/EquipmentArmorTests/EquipmentArmorTests.csproj`.

This links the production `EquipmentArmor.cs` and
`CustomEquipmentProjectionCache.cs` into a minimal managed game host. It checks
slot opt-in behavior, live cache invalidation, quality-aware armor, unchanged
non-armor projections, invalid ownership/slot references, native accessory opt-in,
and native armor/custom-item double counting guards.

The harness also injects provider failures and reentrant callbacks. Menu-style
equipment/weight/set queries must not request armor; armor failures must leave
the complete non-armor projection usable and retry the full armor total. A
callback may rebuild equipment, change player or replace inventory without
invalidating iteration or publishing an old total into the new context. Failed
general projections clear partial state, and in-flight invalidation is retained.

The game objects and existing slot-lookup helpers are test doubles. This does not
run Unity, Harmony patches, server sync, or third-party equipment mods and is not
an in-game compatibility result. YAML parsing/default checks run separately in
`InventorySlots.Tests`.
