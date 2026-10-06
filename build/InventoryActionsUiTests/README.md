# InventoryActions UI regression checks

Run `dotnet run --project build/InventoryActionsUiTests -c Debug`.

The harness compiles the production runtime models and extracts the unchanged method bodies for container buttons and favorite border refresh. It counts geometry writes, permission checks, caption/tooltip refreshes and native coordinate lookups. Extraction fails if selected methods disappear. Keep the small block extractor in sync if these methods gain braces in string literals.

Checks cover stable-frame layout reuse, resize/external edits, destroyed/replaced controls, hide/reopen, owner loss/regain, GUI replacement/destruction, native grid width, patched coordinate fallback and live favorite eligibility. Unity and Harmony boundaries are simulated; this does not verify Unity layout timing, actual Harmony detours, frame rate or in-game multiplayer behavior. Original-game metadata and the compiled mod are checked separately.
