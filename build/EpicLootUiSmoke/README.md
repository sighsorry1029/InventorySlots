# EpicLoot dialog visibility regression checks

Run `dotnet run --project build/EpicLootUiSmoke/EpicLootUiSmoke.csproj -c Debug` from the repository root.

The test compiles the actual `EpicLootCraftingUiCompat.cs` with managed hierarchy doubles. It exercises inactive cloned elements, already-visible dialogs, scrollbar graphics/raycast recovery, repeated opening, missing elements, and references outside the dialog. It verifies that text, rarity visibility policy, listeners, source templates, unrelated objects, and dialog lifetime are preserved.

These checks do **not** run Unity rendering, native layout, Harmony detours, EpicLoot item processing, or multiplayer. Match patch method/field signatures against the actual optional EpicLoot DLL separately, then check Enchant and Rune result dialogs, Augment choices, long-text mouse/gamepad scrolling, closing/reopening, and returning to ordinary crafting in game.
