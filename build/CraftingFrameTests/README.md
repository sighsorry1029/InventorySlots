# Crafting frame routing checks

Run `dotnet run --project build/CraftingFrameTests -c Debug` from the repository root.

This harness links the production frame router and cache stamps. Counted boundary
calls check idle reuse, immediate invalidation, queue/foreign-tab fallback,
non-consuming scroll fallback, progress cleanup, socket suppression, pin repair,
and recovery when the grid is hidden, reparented, or destroyed.

The entry regression also compiles the production `UpdateCraftingPanelRedesign`
entry statements up to the preflight boundary, together with the real
`IsInventoryPanelClosing` method and reason enum. It checks post-Hide ticks,
immediate reopening, explicit state/recipe-list callbacks, and the existing
owned-animator/loading fallbacks. Before the guard, its first post-Hide case
reproduces the unwanted entry into UI work. It does not execute the rest of the
redesign renderer or the native Hide animation.

Unity and crafting operations are doubles. These checks are not a frame-time
benchmark or a replacement for in-game crafting/refinement/controller testing.
