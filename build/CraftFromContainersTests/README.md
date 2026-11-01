# CraftFromContainers requirement counts

Run `dotnet run --project build/CraftFromContainersTests` from the repository root.

The tests compile the production reflection adapter with isolated game/BepInEx stubs.
The requirement-display fixture is checked against the exact production method before
testing provider priority, caching, stock aggregation, disabled/prevent-key behavior,
duplicate containers and saturated totals. They do not run a Unity game session.

Manual verification with CraftFromContainers 3.5.8: place recipe materials only in an
accessible nearby chest, then check counts at the workbench, forge and cauldron,
the upgrade tab, and recipe hover tooltips. Move/consume materials and check that
counts refresh. Test again with CFC disabled and with its prevent key held.
All these standard recipe views use the same availability method.

The adapter counts ordinary CFC containers, not ItemDrawers. It only displays stock;
CraftFromContainers continues to own availability checks and resource consumption.
