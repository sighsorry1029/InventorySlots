# EpicLoot stacking regression checks

Run the shared production stacking and favorite-fill code for both mods:

```powershell
dotnet run --project build/EpicLootStackTests/EpicLootStackTests.csproj -c Debug -- <original-EpicLoot.dll>
dotnet run --project build/EpicLootStackTests/EpicLootStackTests.csproj -c Debug -p:StackTarget=InventorySlots -- <original-EpicLoot.dll>
```

The fake native inventory host tests eligibility, exact identity, EpicLoot-owned
and unknown data, Slots-registered metadata, native veto/partial progress,
favorite ordering/position/source protection, donor removal, repeated sorting,
optional-dependency absence and API failure. Reflection binding uses fixture
public APIs with the real signatures. The optional DLL argument separately
checks those signatures and access levels in the supplied original with Cecil;
it does not execute EpicLoot or Unity.

The existing final-DLL harness also supports `--epicloot-overlap` for
InventoryActions. It invokes the compiled `Start` method with actual Harmony
patch fixtures, checks the 0.8.4 version boundary, removal of only the three
redundant stacking hooks, retention of unrelated/tracker patches, and repeat
cleanup. It does not run the actual AdventureTools tracker or Unity lifecycle.

Actual game tests remain necessary for native transfer hooks, rendered UI,
profile mod combinations, and multiplayer synchronization.
