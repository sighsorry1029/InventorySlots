# Localization startup regression checks

Run from the repository root, sequentially (both variants share build outputs):

```powershell
dotnet run --project build/LocalizationStartupTests -c Debug -p:LocalizationTarget=InventoryActions
dotnet run --no-build --project build/LocalizationStartupTests -c Debug -- early-ui
dotnet run --project build/LocalizationStartupTests -c Debug -p:LocalizationTarget=InventorySlots
dotnet run --no-build --project build/LocalizationStartupTests -c Debug -- early-ui
```

These tests compile each production localizer and the shared UI localization helper,
and use the real embedded English/Korean resources and YAML parser. Game and loader
APIs are simulated: the singleton getter throws before platform initialization,
and constructor-time language callbacks run before singleton assignment. Harmony
registration/dispatch is simulated, not a runtime patch installation test.

Covered: early and repeated plugin loads, early UI fallback, supplied-instance
localization with no client platform initialization (server case), constructor
reentrancy, later menu initialization, late plugin registration, live language
changes, English fallback, and external YAML translation overrides.

On the pre-fix 1.5.12/1.1.5 sources, both normal and `early-ui` runs fail with
`Steamworks is not initialized`. Passing these checks does not replace a real
Steam client run with no saved language preference or a dedicated-server run.
