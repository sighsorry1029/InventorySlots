# Quick-slot radial and panel snapping checks

Run from the repository root:

```powershell
dotnet run --project build/QuickSlotTests -c Debug
```

This executable links the production `QuickSlotRadial`, `QuickSlotInputController`
and `InventoryPanelSnapping` code against managed game/Unity doubles. It checks
main-menu-only registration, unlocked slot ordering, native action preservation,
stale-item and character guards, HUD use notification, keyboard duplicate-use
prevention, snap edges and tolerance, UI scale, background padding, and pulling
away from a snapped position without accumulated drift. Gap checks distinguish
same-edge alignment from adjacency, including the fixed 10-unit attraction
distance, -10..0 gap bounds, default -5 overlap, live settings and non-finite input.

Icon checks use the real embedded PNG with a managed Unity decoder double. They
check one-time loading, public loader resolution, failure fallback and texture
cleanup. They do not validate Unity's native PNG decoder or rendered appearance.

Panel parenting and drag/save lifecycle checks are in `build/CraftingFrameTests`.
These tests do not run Unity or emulate controller hardware. Verify native radial
selection/back/drop actions, 3/6/9-slot layouts, HUD fade, inventory animation and
panel dragging at different game UI scales in Valheim.
