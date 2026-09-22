#if INVENTORY_SLOTS
namespace InventorySlots;
#else
namespace InventoryActions;
#endif

// Keeps row/column navigation separate from rule mutations and Unity focus.
internal sealed class ItemRuleControllerState
{
    internal enum Control { Mode, Quantity, Remove }
    internal enum Input { Up, Down, Left, Right, Submit, Cancel, QuickRemove }
    internal enum Effect { None, FocusChanged, Close, ToggleMode, Remove, IncreaseQuantity, DecreaseQuantity }

    internal int Row { get; set; }
    internal Control Column { get; private set; }
    internal bool EditingQuantity { get; private set; }

    internal void Reset()
    {
        Row = 0;
        Column = Control.Mode;
        EditingQuantity = false;
    }

    internal void EndEditing() => EditingQuantity = false;

    internal void Normalize(int rowCount, bool hasQuantity, bool hasRemove)
    {
        if (rowCount <= 0)
        {
            Reset();
            return;
        }

        if (Row < 0) Row = 0;
        else if (Row >= rowCount) Row = rowCount - 1;

        // A rebuilt row must never shift focus onto a destructive control.
        if (Column == Control.Quantity && !hasQuantity || Column == Control.Remove && !hasRemove)
            Column = Control.Mode;
        if (Column != Control.Quantity) EndEditing();
    }

    internal Effect Apply(Input input, int rowCount, bool hasQuantity, bool hasRemove)
    {
        Normalize(rowCount, hasQuantity, hasRemove);
        if (input == Input.Cancel)
        {
            if (!EditingQuantity) return Effect.Close;
            EndEditing();
            return Effect.FocusChanged;
        }
        if (rowCount <= 0) return Effect.None;
        if (input == Input.QuickRemove)
        {
            EndEditing();
            return Effect.Remove;
        }

        if (EditingQuantity)
        {
            switch (input)
            {
                case Input.Up: return Effect.IncreaseQuantity;
                case Input.Down: return Effect.DecreaseQuantity;
                case Input.Submit:
                    EndEditing();
                    return Effect.FocusChanged;
                default: return Effect.None;
            }
        }

        switch (input)
        {
            case Input.Up:
                if (Row == 0) return Effect.None;
                Row--;
                return Effect.FocusChanged;
            case Input.Down:
                if (Row == rowCount - 1) return Effect.None;
                Row++;
                return Effect.FocusChanged;
            case Input.Left:
                if (Column == Control.Mode) return Effect.None;
                Column = Column == Control.Remove && hasQuantity ? Control.Quantity : Control.Mode;
                return Effect.FocusChanged;
            case Input.Right:
                if (Column == Control.Mode && hasQuantity) Column = Control.Quantity;
                else if (Column != Control.Remove && hasRemove) Column = Control.Remove;
                else return Effect.None;
                return Effect.FocusChanged;
            case Input.Submit:
                if (Column == Control.Mode) return Effect.ToggleMode;
                if (Column == Control.Remove) return Effect.Remove;
                EditingQuantity = true;
                return Effect.FocusChanged;
            default: return Effect.None;
        }
    }
}
