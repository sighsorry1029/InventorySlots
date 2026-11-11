# Crafting frame routing checks

Run `dotnet run --project build/CraftingFrameTests -c Debug` from the repository root.

This harness links the production frame router and cache stamps. Counted boundary
calls check idle reuse, immediate invalidation, queue/foreign-tab fallback,
non-consuming scroll fallback, progress cleanup, socket suppression, pin repair,
and recovery when the grid is hidden, reparented, or destroyed.

The scroll-input regression compiles the production recipe wheel handler and
mouse-scroll consumption guards with `CraftingViewCore`. It checks independent
Grid/List Row/Page settings, sorted recipe selection versus List selection
preservation, range/pointer/input guards, Alt zoom precedence, and repeated
same-frame callbacks. UI/input boundaries are doubles; this does not verify
Unity rendering, native mod tabs, or actual mouse/controller input.

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
It also executes the production search-query setter from Hide, checking model
reset without changing the visible input during the outro, idempotent clearing
and a rapid reopen with the cleared query.
The native InventoryGui 1.0.17 source was inspected: Update caches visibility
before Hide and can still call UpdateInventory/UpdateRecipe in that frame.
These native methods' existing Harmony patches skip that final update while
the layout is pending. Other mods' postfixes still run.

The tab/header regression executes the actual station-badge placement and
snapshot lifecycle with an affine hierarchy model (translation, scale and
pivots). It checks pre-extension restoration, stable-frame writes, GUI ownership,
external reparenting, equal header-button widths/search offsets and repeated
sort toggles. The snapshot double covers parent/position/sibling restoration;
it is not a complete Unity RectTransform or font/rendering simulation.

The pinned-tooltip regression executes the production overlay follower and
crafting pin lifecycle with a managed hierarchy/translation/scale model. It
checks the shared raised layer, delayed sort-control creation, stable sibling
order, source visibility/opacity/input gates, coordinate preservation, tab
switches and teardown. Rotation is copied but is not simulated geometrically.
Native modal ordering is covered by the ControllerTests dialog-layer fixture.

The cell-layer regression runs the production list layout, shared rectangle
helpers, grid icon repair and numeric-text layout against a sibling-order model.
It covers repeated List/Grid reuse, quantity and quality visibility, an already
raised icon, inactive labels, missing/reparented elements and preserving the
relative order of food/style/favorite/pin overlays. Actual pixel overlap and
Canvas rendering still require an in-game check.

The pinned-header regression extracts the production content setter, header
layout and shared-heading parser, together with the actual item-link wire code.
It checks wrapped names, title-only color changes, share/close space, hints,
narrow-panel icon recovery, complete body preservation and wire-format
roundtrips with nested colors and linebreaks. Text measurement is a boundary
double; actual font metrics, clipping and mouse interaction require game checks.

The panel-drag regression extracts the production drag, save, position and PoV
default-placement methods. It checks shared grid parenting, animation gates,
no-motion clicks, snap commit, independent equipment placement, HUD following,
and preserving an explicitly placed quick panel even at the default coordinates.

The recipe-metadata regression executes production classification predicates,
identity/tier token enumeration, the scoped resource-tier cache and feast-token
freshness check. It covers native/custom tool exclusions, potion eligibility,
first-token tier precedence, direct-requirement maxima, zero and missing tiers,
mutable names/language between rebuilds, nested policy invalidation/exception
cleanup and scene replacement. Counted lookups verify shared-material reuse and
deferred normalization/localization; these counts are not game timing results.

Unity and crafting operations are doubles. These checks are not a frame-time
benchmark or a replacement for in-game crafting/refinement/controller testing.
Also verify rapid Tab, death/logout while an input is focused, chest preview,
and VNEI/Jewelcrafting/Reclaim tabs during closing in the game.

The Appearance camera regression runs the production session/input/pose code
with game/physics/UI boundary doubles. It checks framing across aspect ratios,
tab/player/GUI/camera replacement and closing/focus/freefly states, modal and
item-drag cancellation, left-only rotation, panel-drag isolation, button release, single input consumption per frame, exact outside-drop
surface exemption, exact native hover-label exemption and HUD replacement,
other UI hits, wall constraints and feature-local failures.
Harmony private-field/out-parameter binding runs separately in
`build/AppearanceCameraSmoke` against the original game DLLs on game Mono.

The native recipe-row reuse regression extracts the production ownership,
lease, reset and cleanup code. A counted Unity boundary exercises 1,801 rows
over 20 rebuilds with changing order/name/count/requirements, one click callback,
preserved prefab listeners, duplicate keys, distinct upgrade items and quality,
nocost list shrink, special-tab round trips, partial patch readiness, failed
initialization/postfix, reentrancy, foreign destruction/reparenting and GUI,
player, template/root replacement. Hook/IL checks run separately in
`build/RecipeRowReuseSmoke` against original game/mod DLLs on game Mono.
