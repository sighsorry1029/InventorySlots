# UI localization fallback regression checks

This harness compiles the production `Shared/ItemRules/UiLocalization.cs` for
each plugin. The stub supplies unavailable, missing, empty and successful game
localization results; the fallback logic is not copied into the test.

The missing-word contract comes from the unmodified Valheim 1.0.14 client
(Steam build 25364265), `assembly_guiutils` `Localization.Localize` and
`Translate`: `$key` is returned as `[key]` when `m_translations` has no entry.
The preserved extraction is
`C:/Users/blizz/.codex/references/valheim/snapshots/client-b25364265-windows-x64-20260917T122143Z/derived/ilspy-9.1.0.7988-r1/assemblies/assembly_guiutils/csharp/Localization.cs`,
lines 388–416 and 449–468. This harness does not load Unity or validate rendering.

```powershell
dotnet run --project build/LocalizationTests/LocalizationTests.csproj -c Debug
dotnet run --project build/LocalizationTests/LocalizationTests.csproj -c Debug -p:LocalizationTarget=InventorySlots
```

Cases cover rule hints and controller guides, vanilla UI keys, the old missing
`pad_restock` key, preserved placeholders/Korean text, unchanged or empty
results and valid bracketed translations.
