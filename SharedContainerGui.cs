using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using ItemData = ItemDrop.ItemData;

namespace InventorySlots;

public sealed partial class InventorySlotsPlugin
{
    // These fields and callbacks are private in the original Valheim 1.0.12 DLL.
    private static readonly AccessTools.FieldRef<InventoryGui, InventoryGrid> SharedGuiContainerGrid =
        AccessTools.FieldRefAccess<InventoryGui, InventoryGrid>("m_containerGrid");
    private static readonly AccessTools.FieldRef<InventoryGui, ItemData> SharedGuiDragItem =
        AccessTools.FieldRefAccess<InventoryGui, ItemData>("m_dragItem");
    private static readonly AccessTools.FieldRef<InventoryGui, Inventory> SharedGuiDragInventory =
        AccessTools.FieldRefAccess<InventoryGui, Inventory>("m_dragInventory");
    private static readonly AccessTools.FieldRef<InventoryGui, GameObject> SharedGuiDragObject =
        AccessTools.FieldRefAccess<InventoryGui, GameObject>("m_dragGo");
    private static readonly AccessTools.FieldRef<InventoryGui, int> SharedGuiDragAmount =
        AccessTools.FieldRefAccess<InventoryGui, int>("m_dragAmount");
    private static readonly AccessTools.FieldRef<InventoryGui, ItemData> SharedGuiSplitItem =
        AccessTools.FieldRefAccess<InventoryGui, ItemData>("m_splitItem");
    private static readonly AccessTools.FieldRef<InventoryGui, Inventory> SharedGuiSplitInventory =
        AccessTools.FieldRefAccess<InventoryGui, Inventory>("m_splitInventory");
    private static readonly AccessTools.FieldRef<InventoryGui, bool> SharedGuiFirstContainerUpdate =
        AccessTools.FieldRefAccess<InventoryGui, bool>("m_firstContainerUpdate");
    private static readonly AccessTools.FieldRef<InventoryGui, float> SharedGuiHoldTime =
        AccessTools.FieldRefAccess<InventoryGui, float>("m_containerHoldTime");
    private static readonly AccessTools.FieldRef<InventoryGui, int> SharedGuiHoldState =
        AccessTools.FieldRefAccess<InventoryGui, int>("m_containerHoldState");
    private static readonly AccessTools.FieldRef<InventoryGui, float> SharedGuiStackDelay =
        AccessTools.FieldRefAccess<InventoryGui, float>("m_containerHoldPlaceStackDelay");
    private static readonly AccessTools.FieldRef<InventoryGui, float> SharedGuiExitDelay =
        AccessTools.FieldRefAccess<InventoryGui, float>("m_containerHoldExitDelay");
    private static readonly AccessTools.FieldRef<InventoryGui, bool> SharedGuiWaitForStack =
        AccessTools.FieldRefAccess<InventoryGui, bool>("m_waitForContainerStack");
    private static readonly Action<InventoryGui, ItemData?, Inventory?, int> SetupSharedGuiDrag =
        AccessTools.MethodDelegate<Action<InventoryGui, ItemData?, Inventory?, int>>(
            AccessTools.Method(typeof(InventoryGui), "SetupDragItem", new[] { typeof(ItemData), typeof(Inventory), typeof(int) }));
    private static readonly Action<InventoryGui> CloseSharedGuiContainer =
        AccessTools.MethodDelegate<Action<InventoryGui>>(
            AccessTools.Method(typeof(InventoryGui), "CloseContainer", Type.EmptyTypes));
    private static readonly Action<InventoryGui> CancelSharedGuiSplit =
        AccessTools.MethodDelegate<Action<InventoryGui>>(
            AccessTools.Method(typeof(InventoryGui), "OnSplitCancel", Type.EmptyTypes));
    private static readonly MethodInfo SharedGuiSelectMethod = AccessTools.Method(
        typeof(InventoryGui), "OnSelectedItem",
        new[] { typeof(InventoryGrid), typeof(ItemData), typeof(Vector2i), typeof(InventoryGrid.Modifier) });
    private static readonly MethodInfo SharedGuiRightClickMethod = AccessTools.Method(
        typeof(InventoryGui), "OnRightClickItem",
        new[] { typeof(InventoryGrid), typeof(ItemData), typeof(Vector2i) });
    private static readonly MethodInfo SharedGuiDropOutsideMethod = AccessTools.Method(
        typeof(InventoryGui), "OnDropOutside", Type.EmptyTypes);

    private static int _sharedContainerGuiEpoch;
    internal static bool IsReplayingSharedContainerInteraction { get; private set; }

    internal static bool ReplaySharedContainerInteraction(Func<bool> action)
    {
        bool previous = IsReplayingSharedContainerInteraction;
        IsReplayingSharedContainerInteraction = true;
        try { return action(); }
        finally { IsReplayingSharedContainerInteraction = previous; }
    }

    internal enum SharedContainerGuiActionKind { Select, RightClick, DropOutside }

    internal static bool TryHandleSharedContainerGuiAction(
        InventoryGui gui,
        InventoryGrid? grid,
        ItemData? item,
        Vector2i position,
        InventoryGrid.Modifier modifier,
        SharedContainerGuiActionKind kind)
    {
        if (IsReplayingSharedContainerInteraction || gui == null) return false;
        Container? container = ContainerAreaGuiContainer(gui);
        if (container == null || !IsSharedContainerEnabled(container)) return false;
        if (ShouldBlockContainerPreviewInteraction(gui) || IsContainerAreaTransferActive()) return true;

        Player? player = Player.m_localPlayer;
        if (player == null || ContainerAreaPlayerLoading(player) || player.IsTeleporting()) return true;
        Inventory inventory = container.GetInventory();
        Inventory? gridInventory = grid != null ? grid.GetInventory() : null;
        bool dragging = SharedGuiDragObject(gui) != null;
        Inventory? dragInventory = dragging ? SharedGuiDragInventory(gui) : null;
        bool touchesContainer = kind switch
        {
            SharedContainerGuiActionKind.DropOutside => dragging && dragInventory == inventory,
            SharedContainerGuiActionKind.RightClick => item != null && gridInventory == inventory,
            _ => dragging
                ? dragInventory == inventory || gridInventory == inventory
                : item != null && (modifier == InventoryGrid.Modifier.Move ||
                    modifier == InventoryGrid.Modifier.Drop && gridInventory == inventory)
        };
        // Starting a drag or opening the split dialog only selects an item. Its
        // eventual drop is validated after the ownership handoff below.
        if (!touchesContainer) return false;
        Inventory playerInventory = ((Humanoid)player).GetInventory();
        if (gridInventory != null && gridInventory != inventory && gridInventory != playerInventory ||
            dragInventory != null && dragInventory != inventory && dragInventory != playerInventory)
            return true;

        SharedContainerGuiAction action;
        try
        {
            action = new SharedContainerGuiAction
            {
                Gui = gui, Container = container, Grid = grid, GridInventory = gridInventory,
                Player = player, PlayerInventory = playerInventory,
                Position = position, Item = item?.Clone(), Kind = kind, Modifier = modifier,
                DragInventory = dragInventory, DragItem = dragging ? SharedGuiDragItem(gui)?.Clone() : null,
                DragAmount = dragging ? SharedGuiDragAmount(gui) : 0,
                Epoch = _sharedContainerGuiEpoch
            };
        }
        catch (Exception exception)
        {
            Log.LogWarning($"Shared chest selection could not be captured: {exception.Message}");
            return true;
        }

        _ = TryStartSharedContainerInteraction(player, container, () =>
            ReplaySharedContainerGuiAction(action));
        return true;
    }

    private sealed class SharedContainerGuiAction
    {
        public InventoryGui Gui = null!;
        public Container Container = null!;
        public Player Player = null!;
        public Inventory PlayerInventory = null!;
        public InventoryGrid? Grid;
        public Inventory? GridInventory;
        public Vector2i Position;
        public ItemData? Item;
        public SharedContainerGuiActionKind Kind;
        public InventoryGrid.Modifier Modifier;
        public Inventory? DragInventory;
        public ItemData? DragItem;
        public int DragAmount;
        public int Epoch;
    }

    private static bool ReplaySharedContainerGuiAction(SharedContainerGuiAction action)
    {
        InventoryGui gui = action.Gui;
        if (gui == null || action.Container == null || action.Player != Player.m_localPlayer ||
            _sharedContainerGuiEpoch != action.Epoch ||
            ContainerAreaGuiContainer(gui) != action.Container ||
            !ContainerAreaGuiAnimator(gui).GetBool("visible") ||
            !IsSharedContainerEnabled(action.Container) ||
            ((Humanoid)action.Player).GetInventory() != action.PlayerInventory)
            return false;

        ItemData? current = null;
        bool valid = action.GridInventory == null ||
            action.Grid != null && action.Grid.GetInventory() == action.GridInventory &&
            MatchesSharedContainerSlot(action.GridInventory, action.Position, action.Item, out current);
        bool dragging = SharedGuiDragObject(gui) != null;
        if (action.DragItem != null)
        {
            valid = valid && dragging && SharedGuiDragInventory(gui) == action.DragInventory &&
                SharedGuiDragAmount(gui) == action.DragAmount && action.DragAmount > 0 &&
                action.DragAmount <= action.DragItem.m_stack;
            if (valid && action.DragInventory != null &&
                MatchesSharedContainerSlot(action.DragInventory, action.DragItem.m_gridPos,
                    action.DragItem, out ItemData? dragItem) &&
                SharedContainerItemsMatch(action.DragItem, SharedGuiDragItem(gui)))
                SharedGuiDragItem(gui) = dragItem!;
            else
                valid = false;
        }
        else if (dragging)
            valid = false;

        if (!valid)
        {
            SetupSharedGuiDrag(gui, null, null, 1);
            return false;
        }

        return ReplaySharedContainerInteraction(() =>
        {
            switch (action.Kind)
            {
                case SharedContainerGuiActionKind.Select:
                    SharedGuiSelectMethod.Invoke(gui, new object?[] { action.Grid, current, action.Position, action.Modifier });
                    break;
                case SharedContainerGuiActionKind.RightClick:
                    SharedGuiRightClickMethod.Invoke(gui, new object?[] { action.Grid, current, action.Position });
                    break;
                default:
                    SharedGuiDropOutsideMethod.Invoke(gui, null);
                    break;
            }
            return true;
        });
    }

    private static bool MatchesSharedContainerSlot(Inventory inventory, Vector2i position,
        ItemData? expected, out ItemData? current)
    {
        current = null;
        if (position.x < 0 || position.y < 0 || position.x >= inventory.GetWidth() || position.y >= inventory.GetHeight())
            return false;
        current = inventory.GetItemAt(position.x, position.y);
        return SharedContainerItemsMatch(expected, current);
    }

    private static bool SharedContainerItemsMatch(ItemData? expected, ItemData? current)
    {
        if (expected == null || current == null) return expected == null && current == null;
        ItemData actual;
        try { actual = current.Clone(); }
        catch { return false; }
        if (expected.m_dropPrefab == null || actual.m_dropPrefab == null ||
            !string.Equals(expected.m_dropPrefab.name, actual.m_dropPrefab.name, StringComparison.Ordinal) ||
            expected.m_gridPos != actual.m_gridPos || expected.m_stack != actual.m_stack ||
            expected.m_quality != actual.m_quality || expected.m_variant != actual.m_variant ||
            expected.m_worldLevel != actual.m_worldLevel || expected.m_crafterID != actual.m_crafterID ||
            expected.m_crafterID != 0 && !string.Equals(expected.m_crafterName, actual.m_crafterName, StringComparison.Ordinal) ||
            expected.m_equipped != actual.m_equipped || expected.m_pickedUp != actual.m_pickedUp ||
            expected.m_cheated != actual.m_cheated ||
            !SharedContainerDurabilityMatches(expected.m_durability, actual.m_durability) ||
            expected.m_customData == null || actual.m_customData == null ||
            expected.m_customData.Count != actual.m_customData.Count)
            return false;
        foreach (KeyValuePair<string, string> entry in expected.m_customData)
            if (!actual.m_customData.TryGetValue(entry.Key, out string? value) ||
                !string.Equals(entry.Value, value, StringComparison.Ordinal))
                return false;
        return true;
    }

    private static bool SharedContainerDurabilityMatches(float expected, float actual)
    {
        if (float.IsNaN(expected) || float.IsNaN(actual) ||
            float.IsInfinity(expected) || float.IsInfinity(actual) || expected < 0 || actual < 0)
            return false;
        // ItemData.Save stores hundredths as an integer; Load multiplies by
        // 0.01f. Accept that exact round trip, including float truncation at a
        // boundary, without an arbitrary epsilon or ignoring durability.
        return expected == actual || (int)(expected * 100f) == (int)(actual * 100f) ||
            expected == (int)(actual * 100f) * 0.01f ||
            actual == (int)(expected * 100f) * 0.01f;
    }

    internal sealed class SharedContainerDragRefresh
    {
        internal InventoryGui Gui = null!;
        internal Inventory Inventory = null!;
        internal ItemData Item = null!;
        internal int Amount;
    }

    internal static SharedContainerDragRefresh? CaptureSharedContainerDragBeforeLoad(Inventory inventory)
    {
        InventoryGui? gui = InventoryGui.instance;
        if (gui == null || SharedGuiDragObject(gui) == null || SharedGuiDragInventory(gui) != inventory)
            return null;
        Container? container = ContainerAreaGuiContainer(gui);
        ItemData? item = SharedGuiDragItem(gui);
        if (container == null || !IsSharedContainerEnabled(container) ||
            container.GetInventory() != inventory || item == null)
            return null;
        try
        {
            return new SharedContainerDragRefresh
            {
                Gui = gui, Inventory = inventory, Item = item.Clone(), Amount = SharedGuiDragAmount(gui)
            };
        }
        catch
        {
            SetupSharedGuiDrag(gui, null, null, 1);
            return null;
        }
    }

    internal static void RestoreSharedContainerDragAfterLoad(SharedContainerDragRefresh? state, bool succeeded)
    {
        if (state == null || state.Gui == null || SharedGuiDragInventory(state.Gui) != state.Inventory) return;
        if (succeeded && SharedGuiDragAmount(state.Gui) == state.Amount &&
            MatchesSharedContainerSlot(state.Inventory, state.Item.m_gridPos, state.Item, out ItemData? current))
            SharedGuiDragItem(state.Gui) = current!;
        else
            SetupSharedGuiDrag(state.Gui, null, null, 1);
    }

    internal static bool PrepareSharedContainerSplit(InventoryGui gui)
    {
        Container? container = ContainerAreaGuiContainer(gui);
        Inventory? inventory = SharedGuiSplitInventory(gui);
        if (container == null || inventory == null || !IsSharedContainerEnabled(container) ||
            inventory != container.GetInventory())
            return true;
        if (IsContainerAreaTransferActive()) return false;
        ItemData? expected = SharedGuiSplitItem(gui);
        if (expected != null &&
            MatchesSharedContainerSlot(inventory, expected.m_gridPos, expected, out ItemData? current))
        {
            SharedGuiSplitItem(gui) = current!;
            return true;
        }
        // The split dialog keeps its own item reference across Inventory.Load.
        // Reject a changed slot before it can become a misleading drag preview.
        CancelSharedGuiSplit(gui);
        return false;
    }

    internal static bool TryUpdateSharedContainerGui(InventoryGui gui, Player player)
    {
        if (gui == null || player == null) return false;
        Container? container = ContainerAreaGuiContainer(gui);
        if (container == null || !IsSharedContainerEnabled(container)) return false;
        if (!ContainerAreaGuiAnimator(gui).GetBool("visible")) return true;
        if ((container.transform.position - player.transform.position).sqrMagnitude >
            gui.m_autoCloseDistance * gui.m_autoCloseDistance)
        {
            CloseSharedGuiContainer(gui);
            return true;
        }

        container.SetInUse(true);
        RefreshContainerAreaInventory(container);
        gui.m_container.gameObject.SetActive(true);
        Inventory inventory = container.GetInventory();
        InventoryGrid grid = SharedGuiContainerGrid(gui);
        grid.UpdateInventory(inventory, null, SharedGuiDragItem(gui));
        gui.m_containerName.text = Localization.instance.Localize(inventory.GetName());
        if (SharedGuiFirstContainerUpdate(gui))
        {
            grid.ResetView();
            SharedGuiFirstContainerUpdate(gui) = false;
            SharedGuiHoldTime(gui) = 0;
            SharedGuiHoldState(gui) = 0;
        }
        if (ZInput.GetButton("Use") || ZInput.GetButton("JoyUse"))
        {
            SharedGuiHoldTime(gui) += Time.deltaTime;
            if (SharedGuiHoldTime(gui) > SharedGuiStackDelay(gui) && SharedGuiHoldState(gui) == 0)
            {
                SharedGuiHoldState(gui) = 1;
                container.StackAll();
            }
            else if (SharedGuiHoldTime(gui) > SharedGuiStackDelay(gui) + SharedGuiExitDelay(gui) &&
                SharedGuiHoldState(gui) == 1)
                gui.Hide();
        }
        else if (SharedGuiHoldState(gui) >= 0)
        {
            SharedGuiHoldState(gui) = -1;
            SharedGuiWaitForStack(gui) = false;
        }
        return true;
    }

    internal static void InvalidateSharedContainerGuiActions() =>
        _sharedContainerGuiEpoch = unchecked(_sharedContainerGuiEpoch + 1);
}

[HarmonyPatch(typeof(InventoryGui), "OnSelectedItem")]
internal static class InventoryGuiSharedContainerSelectedPatch
{
    [HarmonyPriority(Priority.First + 100)]
    private static bool Prefix(InventoryGui __instance, InventoryGrid grid, ItemData? item,
        Vector2i pos, InventoryGrid.Modifier mod) =>
        !InventorySlotsPlugin.TryHandleSharedContainerGuiAction(__instance, grid, item, pos, mod,
            InventorySlotsPlugin.SharedContainerGuiActionKind.Select);
}

[HarmonyPatch(typeof(InventoryGui), "OnRightClickItem")]
internal static class InventoryGuiSharedContainerRightClickPatch
{
    [HarmonyPriority(Priority.First + 100)]
    private static bool Prefix(InventoryGui __instance, InventoryGrid grid, ItemData? item, Vector2i pos) =>
        !InventorySlotsPlugin.TryHandleSharedContainerGuiAction(__instance, grid, item, pos, default,
            InventorySlotsPlugin.SharedContainerGuiActionKind.RightClick);
}

[HarmonyPatch(typeof(InventoryGui), "OnDropOutside")]
internal static class InventoryGuiSharedContainerDropOutsidePatch
{
    [HarmonyPriority(Priority.First + 100)]
    private static bool Prefix(InventoryGui __instance) =>
        !InventorySlotsPlugin.TryHandleSharedContainerGuiAction(__instance, null, null, default, default,
            InventorySlotsPlugin.SharedContainerGuiActionKind.DropOutside);
}

[HarmonyPatch(typeof(InventoryGui), "UpdateContainer")]
internal static class InventoryGuiSharedContainerUpdatePatch
{
    private static bool Prefix(InventoryGui __instance, Player player) =>
        !InventorySlotsPlugin.TryUpdateSharedContainerGui(__instance, player);
}

[HarmonyPatch]
internal static class InventorySharedContainerDragLoadPatch
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        yield return AccessTools.Method(typeof(Inventory), nameof(Inventory.Load), new[] { typeof(ZPackage) });
        yield return AccessTools.Method(typeof(Inventory), nameof(Inventory.Load), new[] { typeof(ZPackage), typeof(bool) });
    }

    private static void Prefix(Inventory __instance, out InventorySlotsPlugin.SharedContainerDragRefresh? __state) =>
        __state = InventorySlotsPlugin.CaptureSharedContainerDragBeforeLoad(__instance);

    private static Exception? Finalizer(InventorySlotsPlugin.SharedContainerDragRefresh? __state, Exception? __exception)
    {
        InventorySlotsPlugin.RestoreSharedContainerDragAfterLoad(__state, __exception == null);
        return __exception;
    }
}

[HarmonyPatch(typeof(InventoryGui), "OnSplitOk")]
internal static class InventoryGuiSharedContainerSplitPatch
{
    [HarmonyPriority(Priority.First + 100)]
    private static bool Prefix(InventoryGui __instance) =>
        InventorySlotsPlugin.PrepareSharedContainerSplit(__instance);
}

[HarmonyPatch(typeof(InventoryGui), "Hide")]
internal static class InventoryGuiSharedContainerHidePatch
{
    private static void Postfix() => InventorySlotsPlugin.InvalidateSharedContainerGuiActions();
}

[HarmonyPatch(typeof(InventoryGui), "CloseContainer")]
internal static class InventoryGuiSharedContainerClosePatch
{
    private static void Postfix() => InventorySlotsPlugin.InvalidateSharedContainerGuiActions();
}

[HarmonyPatch(typeof(InventoryGui), "Show")]
internal static class InventoryGuiSharedContainerShowPatch
{
    private static void Postfix() => InventorySlotsPlugin.InvalidateSharedContainerGuiActions();
}
