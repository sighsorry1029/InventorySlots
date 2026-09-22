# Controller dispatcher and button navigation harness

The source-linked `ControllerItemMenu.cs` checks cover standalone RStick tap/release,
retained held chords, source-grid identity/selection changes, modal/drag/mouse
invalidation, A/B consumption, and native shoulder escape. Sort focus checks cover
player/container edge entry, original-cell return, disabled/drag guards, and
same-frame movement. Rendering adapters are stubbed; these do not verify Unity
layout or actual device comfort.

The source-linked `FeatureGuideController.cs` checks cover LT + R3 toggling,
held-input repeat prevention, R3 release/menu suppression, button and non-grid
focus, disabled optional hotkeys, early input polling, and modal/lifecycle
guards. Guide rendering and persisted collapse state are adapter callbacks;
their real Unity presentation and disk writes are not simulated here.

This executable compiles the real `Shared/ItemRules/InventoryController.cs`
and `Shared/ItemRules/InventoryButtonNavigation.cs` against a small fake host,
for either plugin's preprocessor branch. It exercises dispatch, focus state,
action routing, button consumption, and input blocking without copying those
state machines into the test. Stub callbacks count invocations; they do not
execute inventory transfers, deletion, persistence, or the rule editor.

It also compiles the shared `ItemRuleControllerState.cs` directly. Panel cases
cover row/control navigation, absent Quantity or Remove controls, explicit
quantity editing, two-stage B behavior, A/X actions, empty lists and rebuilding
or resetting selection. These tests check requested effects and focus state;
the Unity editor executes the effects, saves quantities and renders highlights
separately and is not instantiated by this harness.

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

Same-frame regressions call the navigation prefix while the modifier and Y/B
are held but button-down state is empty, then populate button-down state before
the input-updated callback and inventory update prefix without advancing
`Time.frameCount`. They verify
that the rule callback sees the held-item context and close/use aliases are
consumed. Separate cases restore gamepad mode or grid focus after an early
navigation call, and repeat entry points after consumption to reject duplicate
actions. Previous-frame button-down state also must not dispatch before the
current input tick. These are controlled input-phase scenarios, not a simulation of
Unity's scheduler or a test of actual item-rule registration.

Button-row scenarios cover nearest visible entry, skipped disabled buttons,
vertical release before leaving the row, player/container return requests,
ordinary and special-row boundaries, direct navigation with hotkeys disabled,
held-item rule callbacks, existing Y/B chords, and focus after closing rules.
The focused-button helper must identify only the selected button, switch with
Left/Right and suspend its expansion request while the rules editor is open.
An early navigation poll followed by fresh shoulder input in the same frame
must release its reservation so native group switching remains available.
If native grid movement reaches the last player row or first container row,
later polls in that frame must not reuse the same direction edge to enter the
button row; another Up/Down press is required.
Trash scenarios verify that A opens a confirmation, Cancel is the default,
navigation follows the adapter's reported left/right button arrangement, and
A invokes the selected confirm/cancel callback once. Both Delete-on-right and
Delete-on-left arrangements are covered, while B always cancels and consumes
close aliases. Lifecycle changes and hiding all buttons restore the
stored player-cell selection.

`ButtonNavigationHost.cs` replaces the Unity adapter with visible/interactable
button masks, a displayed-row count, focus callbacks, a reported confirmation
button arrangement and a fake modal. The real
`InventoryButtonFocusUi.cs` is not compiled here. These checks do not validate
Unity focus/highlight rendering, button geometry, game group activation,
container coordinate mapping, hidden-cell recovery or real item protections.
Those remain separate original-DLL and in-game checks.
