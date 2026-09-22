using System;
#if INVENTORY_SLOTS
using RuleState = InventorySlots.ItemRuleControllerState;
#else
using RuleState = InventoryActions.ItemRuleControllerState;
#endif

internal static partial class Program
{
    private static void CheckItemRuleControllerState()
    {
        CheckRuleRowAndColumnNavigation();
        CheckRuleControlAvailability();
        CheckRuleQuantityEditing();
        CheckRuleRebuildAndReset();
    }

    private static void CheckRuleRowAndColumnNavigation()
    {
        RuleState state = new();
        Check(state.Row == 0 && state.Column == RuleState.Control.Mode && !state.EditingQuantity,
            "Rules open on the first row's mode control");
        Check(state.Apply(RuleState.Input.Up, 3, true, true) == RuleState.Effect.None && state.Row == 0,
            "Rules do not wrap above the first row");
        Check(state.Apply(RuleState.Input.Left, 3, true, true) == RuleState.Effect.None,
            "Rules do not wrap left of Mode");
        Check(state.Apply(RuleState.Input.Right, 3, true, true) == RuleState.Effect.FocusChanged &&
            state.Column == RuleState.Control.Quantity, "Right selects Quantity without editing it");
        Check(state.Apply(RuleState.Input.Down, 3, true, true) == RuleState.Effect.FocusChanged &&
            state.Row == 1 && state.Column == RuleState.Control.Quantity && !state.EditingQuantity,
            "Down on an unactivated quantity control selects the next row");
        Check(state.Apply(RuleState.Input.Right, 3, true, true) == RuleState.Effect.FocusChanged &&
            state.Column == RuleState.Control.Remove, "Right from Quantity selects Remove");
        Check(state.Apply(RuleState.Input.Right, 3, true, true) == RuleState.Effect.None &&
            state.Column == RuleState.Control.Remove, "Right stops at Remove");
        Check(state.Apply(RuleState.Input.Down, 3, true, true) == RuleState.Effect.FocusChanged &&
            state.Row == 2 && state.Column == RuleState.Control.Remove, "Down preserves the selected control");
        Check(state.Apply(RuleState.Input.Down, 3, true, true) == RuleState.Effect.None && state.Row == 2,
            "Rules do not wrap past the last row");
        Check(state.Apply(RuleState.Input.Submit, 3, true, true) == RuleState.Effect.Remove &&
            state.Row == 2 && !state.EditingQuantity, "A on Remove requests removal of the focused row");
        Check(state.Apply(RuleState.Input.Up, 3, true, true) == RuleState.Effect.FocusChanged && state.Row == 1,
            "Up returns to the previous row");
        Check(state.Apply(RuleState.Input.Left, 3, true, true) == RuleState.Effect.FocusChanged &&
            state.Column == RuleState.Control.Quantity, "Left from Remove returns to Quantity");
        Check(state.Apply(RuleState.Input.Left, 3, true, true) == RuleState.Effect.FocusChanged &&
            state.Column == RuleState.Control.Mode, "Left from Quantity returns to Mode");
        Check(state.Apply(RuleState.Input.Submit, 3, true, true) == RuleState.Effect.ToggleMode &&
            state.Row == 1 && !state.EditingQuantity, "A on Mode requests a toggle without changing focus");
    }

    private static void CheckRuleControlAvailability()
    {
        RuleState state = new();
        Check(state.Apply(RuleState.Input.Right, 2, false, true) == RuleState.Effect.FocusChanged &&
            state.Column == RuleState.Control.Remove, "Exclusion rows skip the absent Quantity control");
        Check(state.Apply(RuleState.Input.Left, 2, false, true) == RuleState.Effect.FocusChanged &&
            state.Column == RuleState.Control.Mode, "Exclusion rows return directly from Remove to Mode");

        Check(state.Apply(RuleState.Input.Right, 1, true, false) == RuleState.Effect.FocusChanged &&
            state.Column == RuleState.Control.Quantity, "Registration rows can select Quantity");
        Check(state.Apply(RuleState.Input.Right, 1, true, false) == RuleState.Effect.None &&
            state.Column == RuleState.Control.Quantity, "Registration rows cannot select absent Remove");
        state.Reset();
        Check(state.Apply(RuleState.Input.Right, 1, false, false) == RuleState.Effect.None &&
            state.Column == RuleState.Control.Mode, "Single-control rows keep Mode selected");
        Check(state.Apply(RuleState.Input.Submit, 1, false, false) == RuleState.Effect.ToggleMode,
            "Single-control registration rows still activate Mode");
        Check(state.Apply(RuleState.Input.QuickRemove, 1, false, false) == RuleState.Effect.Remove,
            "X retains the registration shortcut even without a visible Remove control");
    }

    private static void CheckRuleQuantityEditing()
    {
        RuleState state = new() { Row = 1 };
        state.Apply(RuleState.Input.Right, 3, true, true);
        Check(state.Apply(RuleState.Input.Submit, 3, true, true) == RuleState.Effect.FocusChanged && state.EditingQuantity,
            "A activates quantity editing explicitly");
        Check(state.Apply(RuleState.Input.Up, 3, true, true) == RuleState.Effect.IncreaseQuantity &&
            state.Row == 1 && state.Column == RuleState.Control.Quantity && state.EditingQuantity,
            "Editing Up requests increase without moving to another row");
        Check(state.Apply(RuleState.Input.Down, 3, true, true) == RuleState.Effect.DecreaseQuantity &&
            state.Row == 1 && state.Column == RuleState.Control.Quantity && state.EditingQuantity,
            "Editing Down requests decrease without moving to another row");
        foreach (RuleState.Input input in new[] { RuleState.Input.Left, RuleState.Input.Right })
            Check(state.Apply(input, 3, true, true) == RuleState.Effect.None && state.Row == 1 &&
                state.Column == RuleState.Control.Quantity && state.EditingQuantity,
                "Quantity editing isolates horizontal navigation: " + input);

        Check(state.Apply(RuleState.Input.Cancel, 3, true, true) == RuleState.Effect.FocusChanged &&
            !state.EditingQuantity && state.Column == RuleState.Control.Quantity,
            "First B finishes quantity editing and keeps the panel's focus");
        Check(state.Apply(RuleState.Input.Cancel, 3, true, true) == RuleState.Effect.Close,
            "Second B closes the rules panel");
        state.Apply(RuleState.Input.Submit, 3, true, true);
        Check(state.Apply(RuleState.Input.Submit, 3, true, true) == RuleState.Effect.FocusChanged &&
            !state.EditingQuantity, "A also finishes quantity editing");
        Check(state.Apply(RuleState.Input.Down, 3, true, true) == RuleState.Effect.FocusChanged && state.Row == 2,
            "Finishing quantity editing restores vertical row navigation");

        state.Apply(RuleState.Input.Submit, 3, true, false);
        Check(state.Apply(RuleState.Input.QuickRemove, 3, true, false) == RuleState.Effect.Remove &&
            !state.EditingQuantity && state.Row == 2, "X exits editing before requesting removal");
        state.Apply(RuleState.Input.Submit, 3, true, true);
        state.EndEditing();
        Check(!state.EditingQuantity && state.Row == 2 && state.Column == RuleState.Control.Quantity,
            "External edit completion preserves the focused row and control");
    }

    private static void CheckRuleRebuildAndReset()
    {
        RuleState state = new() { Row = 4 };
        state.Apply(RuleState.Input.Right, 5, true, true);
        state.Apply(RuleState.Input.Submit, 5, true, true);
        state.Normalize(2, true, true);
        Check(state.Row == 1 && state.Column == RuleState.Control.Quantity && state.EditingQuantity,
            "Rebuild clamps a removed last row while retaining available quantity focus");
        state.Normalize(2, false, true);
        Check(state.Row == 1 && state.Column == RuleState.Control.Mode && !state.EditingQuantity,
            "A disappearing quantity control returns safely to Mode and ends editing");
        state.Apply(RuleState.Input.Right, 2, false, true);
        state.Normalize(2, true, false);
        Check(state.Column == RuleState.Control.Mode && !state.EditingQuantity,
            "A disappearing Remove control returns to Mode");
        state.Row = -10;
        state.Normalize(2, true, true);
        Check(state.Row == 0, "Rebuild repairs negative row selection");
        state.Row = int.MaxValue;
        state.Apply(RuleState.Input.Down, 2, true, true);
        Check(state.Row == 1, "Input clamps externally stale selection before moving");

        foreach (int count in new[] { 0, -1 })
        {
            foreach (RuleState.Input input in Enum.GetValues<RuleState.Input>())
            {
                state.Row = 1;
                state.Apply(RuleState.Input.Right, 2, true, true);
                state.Apply(RuleState.Input.Submit, 2, true, true);
                RuleState.Effect expected = input == RuleState.Input.Cancel ? RuleState.Effect.Close : RuleState.Effect.None;
                Check(state.Apply(input, count, true, true) == expected && state.Row == 0 &&
                    state.Column == RuleState.Control.Mode && !state.EditingQuantity,
                    "Empty panel safely handles " + input + " with row count " + count);
            }
        }
        state.Normalize(3, true, true);
        Check(state.Apply(RuleState.Input.Submit, 3, true, true) == RuleState.Effect.ToggleMode && state.Row == 0,
            "Repopulated panel starts with the first row's Mode control");
        state.Row = 2;
        state.Apply(RuleState.Input.Right, 3, true, true);
        state.Apply(RuleState.Input.Submit, 3, true, true);
        state.Reset();
        Check(state.Row == 0 && state.Column == RuleState.Control.Mode && !state.EditingQuantity,
            "Reset clears selection and editing when the panel is reopened");
    }
}
