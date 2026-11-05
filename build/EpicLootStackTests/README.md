# EpicLoot stacking regression checks

Run the shared production stacking and favorite-fill code for both mods:

```powershell
dotnet run --project build/EpicLootStackTests/EpicLootStackTests.csproj -c Debug -- <original-EpicLoot.dll>
dotnet run --project build/EpicLootStackTests/EpicLootStackTests.csproj -c Debug -p:StackTarget=InventorySlots -- <original-EpicLoot.dll>

# Modern fixtures omit the legacy types and expose the internal bool predicate.
dotnet run --project build/EpicLootStackTests/EpicLootStackTests.csproj -c Debug -p:EpicLootApiFlavor=Modern -- <original-modern-EpicLoot.dll>
dotnet run --project build/EpicLootStackTests/EpicLootStackTests.csproj -c Debug -p:EpicLootApiFlavor=Modern -p:StackTarget=InventorySlots -- <original-modern-EpicLoot.dll>
```

The fake native inventory host tests eligibility, exact identity, EpicLoot-owned
and unknown data, Slots-registered metadata, native veto/partial progress,
favorite ordering/position/source protection, donor removal, repeated sorting,
optional-dependency absence and API failure. Reflection binding uses fixture
legacy public APIs or the modern internal predicate with the real signatures.
The modern fixture additionally verifies destination/source ordering, no claimed
merge keys and continued owner-policy/exact-data restrictions. Synthetic API
assemblies check unrelated version/MVID changes and reject missing/malformed or
partial contracts. The optional DLL argument separately
checks those signatures and access levels in the supplied original with Cecil;
it does not execute EpicLoot or Unity.

For real original-EpicLoot calls in either final merged mod DLL, use the existing
Mono host (the mod must already be built):

```powershell
./InventoryActions/build/CompatibilitySmoke/Run-MonoSmoke.ps1 -Mode epicloot-stacking -ModDll ./bin/Debug/InventorySlots.dll -EpicLootDll <original-modern-EpicLoot.dll>
./InventoryActions/build/CompatibilitySmoke/Run-MonoSmoke.ps1 -Mode epicloot-stacking -ModDll ./InventoryActions/bin/Debug/InventoryActions.dll -EpicLootDll <original-modern-EpicLoot.dll>
```

This checks the internal delegate against original game access restrictions and
the real modern EpicLoot lookup prefix in both HarmonyX registration orders,
with an explicit rejecting boundary stand-in. It does not run plugin Awake,
classification (which can reload/canonicalize EpicLoot data), the full Slots
placement implementation, or gameplay. The runner's default game/core/runtime
paths come from `environment.props`; supply `-GamePath` for another original
game installation. Dedicated snapshot arguments can also be passed directly to
the built MonoHost/CompatibilitySmoke executable as documented in its runner.

The existing final-DLL harness also supports `--epicloot-overlap` for
InventoryActions. It invokes the compiled `Start` method with actual Harmony
patch fixtures, checks the 0.8.4 version boundary, removal of only the three
redundant stacking hooks, retention of unrelated/tracker patches, and repeat
cleanup. It does not run the actual AdventureTools tracker or Unity lifecycle.

Actual game tests remain necessary for native transfer hooks, rendered UI,
profile mod combinations, and multiplayer synchronization.
