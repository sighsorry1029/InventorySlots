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

The closing-layout regression compiles the production Hide handler, pending
layout lifecycle, Show/Destroy and final native-tick gates, plus the stat-layout
entry. It checks immediate queue/pin/focus cleanup, pointer gating without
visual restoration, retaining the layout after logical visibility expires,
one-time restoration when the native root becomes inactive, rapid reopening,
repeated Hide, GUI replacement/destruction and an existing blocked CanvasGroup.
The native InventoryGui 1.0.17 source was inspected: Update caches visibility
before Hide and can still call UpdateInventory/UpdateRecipe in that frame.
These native methods' existing Harmony patches skip that final update while
the layout is pending. Other mods' postfixes still run.

The pinned-tooltip regression executes the production overlay follower and
crafting pin lifecycle with a managed hierarchy/translation/scale model. It
checks the shared raised layer, delayed sort-control creation, stable sibling
order, source visibility/opacity/input gates, coordinate preservation, tab
switches and teardown. Rotation is copied but is not simulated geometrically.
Native modal ordering is covered by the ControllerTests dialog-layer fixture.

Unity and crafting operations are doubles. These checks are not a frame-time
benchmark or a replacement for in-game crafting/refinement/controller testing.
Also verify rapid Tab, death/logout while an input is focused, chest preview,
and VNEI/Jewelcrafting/Reclaim tabs during closing in the game.
