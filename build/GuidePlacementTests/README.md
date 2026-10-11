# Feature guide placement checks

Source-links the shared production placement code for both plugin branches.
The geometry/Animator doubles model the original Valheim 1.0.16 GUI's Player
and Info vertical slides and Crafting horizontal slide. Cases cover startup
before the first opening, intermediate and interrupted animations, panel
movement/resizing/overlap, transformed inventory ancestors, resolution/UI scale
changes, descendant stat/rail panels, independent quick-slot motion, absent
animators and GUI destruction/recreation. The guide is anchored to the HUD
viewport independently of all panels, even when the inventory GUI is absent.
The native-pose cache remains available for the quick-slot HUD and chat tooltips.
The guide does not write native transforms.

```powershell
dotnet run --project build/GuidePlacementTests -c Debug
dotnet run --project build/GuidePlacementTests -c Debug -p:GuideTarget=InventorySlots
```

These are isolated geometry/lifecycle checks, not a Unity rendering or actual
gameplay test. Verify both guides in game with rapid Tab, F6 states and UI scale.

The InventorySlots target also source-links `InventoryQuickSlotHudLayout.cs`.
HUD cases cover placement before the first Tab, interrupted native slides,
late layout readiness, GUI recreation, different canvas scales, saved positions
with panel following disabled, resolution/root-pivot changes and full multi-row
screen bounds. The test host supplies the inventory origin and side-panel base;
it does not create actual Unity inventory widgets or validate rendering.
In game, also check fresh/existing ClientState.yml, HUD hiding, dragging and
`Quick Slot HUD Follows Panel` On/Off at more than one resolution/UI scale.

Fresh HUD placement also covers delayed GUI scaling, 16:9, ultrawide and 4:3
viewports, insufficient space and returning to normal scale, with no inventory
geometry and without writing a saved position. `ClientStateCore` and its models
are source-linked for these cases. Existing saved positions keep their original
HUD-local coordinate system.

The default offsets were checked against the original Valheim 1.0.17 client
`IngameGui.prefab` in SoftRef bundle `d59cfac`: Player `(40,-40)`, size `570x287`,
grid spacing `70`, Container below Player by `30`, size `570x340`, Weight anchored
at its bottom-right `(34,60)`, size `80x60`. InventorySlots moves the chest down
`5*70` for nine rows and its weight panel up `200`. That puts the weight panel's
bottom at `817` units below the viewport top. HUD top `821` leaves a four-unit
gap; left `628.5` retains the HUD's independent horizontal reference.
The inventory equipment/quick-panel X offsets are both `-82`. Equipment Y stays
`0` to align its slot rows with the player grid. Their `14`-unit background inset
matches the native first-slot top (`44`) minus its background top (`30`). The
two-unit X adjustment preserves the adjacent background edges: the native wood
sprite has transparent side margins, so those Rect edges deliberately overlap.
The quick-panel Y offset `-235` includes three `70`-unit equipment rows and both
`14`-unit background margins, with three units of Rect overlap to absorb the
wood sprite's transparent top/bottom edges. These values describe the
reference layout, not a runtime dependency on those other panels. Original sprite
geometry was checked in bundle `9fe0899c`; actual visual spacing still needs a
game check, especially with other UI mods.
