using System;
using UnityEngine;
using ItemData = ItemDrop.ItemData;

namespace InventorySlots;

public sealed partial class InventorySlotsPlugin
{
    internal static bool TryGetMultiUserContainerProjection(
        Inventory realInventory,
        out Inventory projection)
    {
        projection = null!;
        PendingMultiUserContainerTransfer? pending = _pendingMultiUserContainerTransfer;
        if (pending == null ||
            pending.TerminalFailureReceived ||
            realInventory == null ||
            pending.Container == null ||
            IsUnityNull(pending.Container) ||
            pending.Container.GetInventory() != realInventory)
        {
            return false;
        }

        if (pending.AuthoritativeStateObserved)
        {
            return false;
        }

        if (pending.Projection != null)
        {
            projection = pending.Projection;
            return true;
        }

        Inventory projected;
        try
        {
            projected = new Inventory(
                realInventory.GetName(),
                null,
                realInventory.GetWidth(),
                realInventory.GetHeight());
            foreach (ItemData item in realInventory.GetAllItems())
            {
                if (item?.m_shared == null)
                {
                    return false;
                }

                projected.m_inventory.Add(item.Clone());
            }
        }
        catch
        {
            return false;
        }

        bool applied;
        switch (pending.Request.Operation)
        {
            case MultiUserContainerOperation.Add:
                applied = TryApplyMultiUserContainerAdd(
                    projected,
                    pending.Request.Item,
                    pending.Request.Amount,
                    pending.Request.TargetPosition,
                    pending.Request.ExpectedTargetStack);
                break;
            case MultiUserContainerOperation.Remove:
                applied = TryApplyMultiUserContainerRemove(
                    projected,
                    pending.Request.Item,
                    pending.Request.Amount,
                    pending.Request.SourcePosition,
                    out _);
                break;
            case MultiUserContainerOperation.Move:
                applied = TryApplyMultiUserContainerMove(
                    projected,
                    pending.Request.Item,
                    pending.Request.Amount,
                    pending.Request.SourcePosition,
                    pending.Request.TargetPosition,
                    pending.Request.ExpectedTargetStack);
                break;
            case MultiUserContainerOperation.Exchange:
                applied = TryApplyMultiUserContainerExchange(
                    projected,
                    pending.Request.Item,
                    pending.Request.CounterpartItem!,
                    pending.Request.SourcePosition,
                    out _);
                break;
            case MultiUserContainerOperation.Swap:
                applied = TryApplyMultiUserContainerSwap(
                    projected,
                    pending.Request.Item,
                    pending.Request.CounterpartItem!,
                    pending.Request.SourcePosition,
                    pending.Request.TargetPosition);
                break;
            default:
                applied = false;
                break;
        }

        if (!applied)
        {
            return false;
        }

        pending.Projection = projected;
        projection = pending.Projection;
        return true;
    }

    internal static void RestoreMultiUserContainerGridInventory(
        InventoryGrid grid,
        Inventory realInventory)
    {
        if (grid != null && !IsUnityNull(grid) && realInventory != null)
        {
            grid.m_inventory = realInventory;
        }
    }

    internal static bool ShouldBlockMultiUserContainerGamepadInput(
        InventoryGrid grid)
    {
        if (IsMultiUserContainerBatchInteractionBlocked(
                InventoryGui.instance))
        {
            return true;
        }

        PendingMultiUserContainerTransfer? pending =
            _pendingMultiUserContainerTransfer;
        if (pending == null ||
            grid == null ||
            IsUnityNull(grid))
        {
            return false;
        }

        Inventory? inventory = grid.GetInventory();
        return inventory == pending.Projection ||
               pending.Container != null &&
               !IsUnityNull(pending.Container) &&
               inventory == pending.Container.GetInventory();
    }

    internal static void OnMultiUserContainerInventoryLoaded(Inventory inventory)
    {
        PendingMultiUserContainerTransfer? pending = _pendingMultiUserContainerTransfer;
        if (pending != null &&
            pending.Container != null &&
            !IsUnityNull(pending.Container) &&
            pending.Container.GetInventory() == inventory)
        {
            pending.Projection = null;
            pending.AuthoritativeStateObserved =
                !pending.TerminalFailureReceived &&
                IsMultiUserContainerRequestVisibleInInventory(
                    pending,
                    inventory);
            if (pending.ResponseApplied &&
                !pending.LocalRecoveryPending &&
                !pending.AcknowledgementPending &&
                pending.AuthoritativeStateObserved &&
                _pendingMultiUserContainerTransfer == pending)
            {
                CompletePendingMultiUserContainerTransfer(
                    pending,
                    committedAndObserved: true);
            }
        }

        InventoryGui? gui = InventoryGui.instance;
        if (inventory == null ||
            gui == null ||
            IsUnityNull(gui) ||
            gui.m_dragItem == null ||
            gui.m_dragInventory != inventory ||
            gui.m_currentContainer == null ||
            gui.m_currentContainer.GetInventory() != inventory)
        {
            return;
        }

        ItemData previous = gui.m_dragItem;
        Vector2i position = previous.m_gridPos;
        ItemData? current = inventory.GetItemAt(position.x, position.y);
        int requiredStack = Math.Min(
            Math.Max(1, gui.m_dragAmount),
            Math.Max(1, previous.m_stack));
        if (current == null ||
            !IsExactMultiUserContainerItemMatch(previous, current, requiredStack))
        {
            gui.SetupDragItem(null, null, 1);
            return;
        }

        gui.m_dragItem = current;
        gui.m_dragAmount = Math.Min(current.m_stack, gui.m_dragAmount);
    }
}
