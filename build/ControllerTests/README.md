# Controller dispatcher harness

This executable compiles the real `Shared/ItemRules/InventoryController.cs`
against a small fake host, for either plugin's preprocessor branch. It exercises
dispatch, action routing, button consumption, and input blocking without
copying the dispatcher into the test. Stub callbacks count invocations; they do
not execute inventory transfers, persistence, or the rule editor.

```powershell
dotnet run --project build/ControllerTests/ControllerTests.csproj -c Debug
dotnet run --project build/ControllerTests/ControllerTests.csproj -c Debug -p:ControllerTarget=InventorySlots
```

The fake `ZInput.ResetButtonStatus` clears the held/down state, matching the
relevant part of the original game's reset contract. World tests exercise
semantic `JoyAltKeys` and `JoyUse`, not fabricated physical layout mappings.
Harmony entry-point methods are invoked directly. This does not validate
Harmony installation, Unity/EventSystem update ordering, gamepad hardware,
rendering, networking, or actual gameplay. The original DLL compatibility scan
and manual controller testing remain separate checks.
