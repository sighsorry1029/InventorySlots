# Feature guide placement checks

Source-links the shared production placement code for both plugin branches.
The geometry/Animator doubles model the original Valheim 1.0.16 GUI's Player
and Info vertical slides and Crafting horizontal slide. Cases cover startup
before the first opening, intermediate and interrupted animations, settled
layout edits, live sizes, transformed ancestors, resolution/UI scale changes,
descendant stat/rail panels, independent quick-slot motion, absent animators,
and GUI destruction/recreation. The guide does not write native transforms.

```powershell
dotnet run --project build/GuidePlacementTests -c Debug
dotnet run --project build/GuidePlacementTests -c Debug -p:GuideTarget=InventorySlots
```

These are isolated geometry/lifecycle checks, not a Unity rendering or actual
gameplay test. Verify both guides in game with rapid Tab, F6 states and UI scale.
