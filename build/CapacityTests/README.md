# Pickup capacity regression checks

Run `dotnet run --project build/CapacityTests/CapacityTests.csproj -c Debug`.

The runner uses the SDK's Roslyn to extract and compile the actual capacity,
stack identity, failure lookup and empty-cell methods from
`InventoryPlacementPolicy.cs`. Counted boundary doubles represent slot policy,
progression context and optional metadata/EpicLoot callbacks. They verify rejected
mixed-item workloads, eligible stacks, restricted slots, identity changes, metadata
vetoes, live stack limits, cache invalidation and occupied-cell filtering.

`-- --baseline` runs the same behavior checks without the optimized call-count
assertions, for comparison with the unmodified source. Call counts demonstrate
work avoided, not a measured game FPS improvement. Native execution, full mod
callbacks and multiplayer still need actual game validation.
