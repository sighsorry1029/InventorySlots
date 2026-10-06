# Crafting frame routing checks

Run `dotnet run --project build/CraftingFrameTests -c Debug` from the repository root.

This harness links the production frame router and cache stamps. Counted boundary
calls check idle reuse, immediate invalidation, queue/foreign-tab fallback,
non-consuming scroll fallback, progress cleanup, socket suppression, pin repair,
and recovery when the grid is hidden, reparented, or destroyed.

Unity and crafting operations are doubles. These checks are not a frame-time
benchmark or a replacement for in-game crafting/refinement/controller testing.
