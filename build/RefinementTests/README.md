# Refinement regression checks

Run `dotnet run --project build/RefinementTests -c Debug`.

Links the production refinement observer, special-slot upgrade transaction and
crafting favorite transfer code. In-memory game boundaries cover full ordinary
inventory, quick/equipment/custom slots, success, downgrade (including native
quality zero), destruction with refunds and later exceptions, pre-decision
recovery, failed adds before cost consumption, exact favorite identity, foreign
AddItem prefixes and missing/ambiguous IL markers.

The injected replacement gate is also emitted into a DynamicMethod and executed
against the same source-linked observer to check the stack and early cost return.
Unexpected new result quality/variant/prefab must be cleaned up on rollback;
pre-existing items must remain untouched.

These checks do not execute Unity, EpicLoot, or a game session. The separate
`build/HarmonySmoke` checks apply the transpiler to original game IL.
