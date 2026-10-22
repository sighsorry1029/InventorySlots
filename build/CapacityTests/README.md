# Pickup capacity regression checks

Run `dotnet run --project build/CapacityTests/CapacityTests.csproj -c Debug`.

The runner uses the SDK's Roslyn to extract and compile the actual capacity,
stack identity, failure lookup and empty-cell methods from
`InventoryPlacementPolicy.cs`. Counted boundary doubles represent slot policy,
progression context and optional metadata/EpicLoot callbacks. They verify rejected
mixed-item workloads, eligible stacks, restricted slots, identity changes, metadata
vetoes, live stack limits, cache invalidation and occupied-cell filtering.

The runner also extracts the special-slot search and auto-adoption guards from
`SlotEquipController.cs`, using the real `InventorySlotSafetyCore`. It checks
ordinary Quick rejection reuse, later available cells after occupied/locked
cells, PoV/custom acceptance, live changes between queries, equipped markers and
PoV trinket exclusion. Rejection is reused only inside one capacity query;
metadata failure-cache eligibility and persistent cache lifetimes are unchanged.
Accepted items retain the existing per-cell checks (with at most one additional
ordinary Quick classification precheck).

For the full-inventory mixed-drop workload reported on 2026-10-22, the extracted
baseline search performs 9 ordinary Quick acceptance calls for a rejected item;
the updated search performs 1. With the reported eight YAML group tokens, this
reduces their repeated classifier-key construction from 72 to 8. The main
`InventorySlots.Tests` YAML checks additionally exercise the real
`QuickSlotAcceptsItem` and verify shared item normalization, null/empty lists,
live aliases, custom groups and mutable item identities.

`-- --baseline` runs the same behavior checks without the optimized call-count
assertions, for comparison with the unmodified source. Call counts demonstrate
work avoided, not a measured game FPS improvement. Native execution, full mod
callbacks and multiplayer still need actual game validation.
