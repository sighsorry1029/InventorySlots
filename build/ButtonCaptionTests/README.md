# Inventory button caption tests

These executable regression tests compile the actual shared
`InventoryButtonTextUi.cs` helper with a small fake Unity hierarchy. They cover
native `UIGamePad`/`UIInputHint` ownership, active and inactive hint descendants,
TMP and legacy captions, external references and the button root, deferred
destruction/event delivery, retained hint ownership, and click-listener survival.
They do not replace in-game validation of Unity lifecycle or rendered layout.

Run both conditional plugin variants from the repository root:

```powershell
dotnet run --project build/ButtonCaptionTests/ButtonCaptionTests.csproj -c Debug -p:ButtonCaptionTarget=InventoryActions
dotnet run --project build/ButtonCaptionTests/ButtonCaptionTests.csproj -c Debug -p:ButtonCaptionTarget=InventorySlots
```
