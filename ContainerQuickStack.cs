using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using ItemData = ItemDrop.ItemData;

namespace InventorySlots;

public sealed partial class InventorySlotsPlugin
{
    internal static bool TryHandleVanillaPlaceStacks(InventoryGui gui)
    {
        if (gui == null || gui.m_currentContainer == null)
        {
            return false;
        }

        if (IsMultiUserContainerAreaBatchActive())
        {
            ShowMultiUserContainerNotReady();
            return true;
        }

        ContainerAccessMode accessMode = GetContainerAccessMode(
            gui.m_currentContainer,
            allowLocalWithoutZNetView: true);
        if (accessMode == ContainerAccessMode.MultiUserChestRemote &&
            IsBuiltInMultiUserChestEnabled)
        {
            if (!TryStartMultiUserContainerPlaceStacksBatch(
                    gui.m_currentContainer))
            {
                ShowMultiUserContainerNotReady();
            }

            return true;
        }

        if (accessMode != ContainerAccessMode.DirectOwner)
        {
            return false;
        }

        QuickStackCurrentContainer(Player.m_localPlayer);
        return true;
    }

    internal static bool TryHandleContainerStackAll(Container container)
    {
        if (ShouldSuppressContainerStackAllForRestock(container))
        {
            return true;
        }

        Player? player = Player.m_localPlayer;
        if (player == null || player.m_isLoading || container == null || container.m_inventory == null)
        {
            return false;
        }

        ContainerAccessMode accessMode = GetContainerAccessMode(
            container,
            allowLocalWithoutZNetView: true);
        if (IsBuiltInMultiUserChestEnabled &&
            TryHandleMultiUserContainerAreaQuickStack(container))
        {
            return true;
        }

        if (accessMode == ContainerAccessMode.MultiUserChestRemote &&
            IsBuiltInMultiUserChestEnabled)
        {
            ShowMultiUserContainerNotReady();
            return true;
        }

        if (accessMode != ContainerAccessMode.DirectOwner)
        {
            return false;
        }

        Inventory playerInventory = ((Humanoid)player).GetInventory();
        if (playerInventory == null)
        {
            return false;
        }

        QuickStackIntoContainers(player, playerInventory, container, includeArea: true);
        return true;
    }

    private static void HandleContainerQuickStackHotkey(Player player)
    {
        HandleContainerHoldHotkey(
            player,
            InventoryPanels.ContainerQuickStackHold,
            TryGetContainerQuickStackHotkeyContext,
            TryQuickStackFromHoveredContainer);
    }

    private static bool TryGetContainerQuickStackHotkeyContext(Player player, out Container container)
    {
        container = null!;
        if (player == null ||
            InventoryGui.IsVisible() ||
            ShouldBlockGlobalHotkeys(player) ||
            IsContainerRestockShortcutHeld() ||
            !IsContainerQuickStackShortcutHeld())
        {
            return false;
        }

        Container? hovered = GetHoveredContainer(player);
        if (hovered == null ||
            player.m_isLoading ||
            hovered.m_inventory == null ||
            !CanHandleContainerAreaAction(player, hovered))
        {
            return false;
        }

        container = hovered;
        return true;
    }

    private static bool IsContainerQuickStackShortcutHeld() =>
        ZInput.GetButton("Use") || ZInput.GetButton("JoyUse");

    private static bool TryQuickStackFromHoveredContainer(Player player, Container container)
    {
        if (container == null ||
            container.m_inventory == null ||
            !CanHandleContainerAreaAction(player, container))
        {
            return false;
        }

        if (TryHandleMultiUserContainerAreaQuickStack(container))
        {
            return true;
        }

        if (!CanMutateContainerDirectly(
                container,
                allowLocalWithoutZNetView: true))
        {
            return false;
        }

        Inventory playerInventory = ((Humanoid)player).GetInventory();
        if (playerInventory == null)
        {
            return false;
        }

        QuickStackIntoContainers(player, playerInventory, container, includeArea: true);
        return true;
    }

    internal static void QuickStackCurrentContainer(Player? player)
    {
        if (!TryGetActionContext(player, out Player localPlayer, out Inventory playerInventory, out Container container, out _))
        {
            return;
        }

        QuickStackIntoContainers(localPlayer, playerInventory, container, includeArea: false);
    }

    private static void QuickStackIntoContainers(Player localPlayer, Inventory playerInventory, Container container, bool includeArea)
    {
        List<ItemData> candidates = playerInventory.m_inventory
            .Where(item => ShouldQuickStackItem(localPlayer, playerInventory, item))
            .ToList();
        candidates.Sort((a, b) => -CompareGridOrder(a.m_gridPos, b.m_gridPos));

        InventoryGui.instance?.SetupDragItem(null, null, 0);
        int moved = RunContainerTransferAcrossContainers(
            localPlayer,
            container,
            includeArea,
            areaForQuickStack: true,
            targetContainer => QuickStackItemsIntoContainer(localPlayer, playerInventory, targetContainer.m_inventory, candidates),
            () =>
            {
                playerInventory.Changed();
                ClearCraftingRequirementAvailabilityCache();
            });

        ShowContainerActionResult(localPlayer, "$inventoryslots_action_stack", "Stack", moved);
    }

    private static bool ShouldQuickStackItem(Player player, Inventory inventory, ItemData item)
    {
        return item?.m_shared != null &&
               item.m_shared.m_maxStackSize > 1 &&
               IsRegularActionItem(player, inventory, item) &&
               !IsEquippedContainerMoveSource(player, item) &&
               !IsFavoriteProtected(player, inventory, item) &&
               CanUseContainerActionStacking(item);
    }

    private static int QuickStackItemsIntoContainer(Player player, Inventory playerInventory, Inventory containerInventory, List<ItemData> candidates)
    {
        if (containerInventory == null || candidates.Count == 0)
        {
            return 0;
        }

        HashSet<string> acceptedNames = new(containerInventory.m_inventory
            .Where(item => item?.m_shared != null)
            .Select(item => item.m_shared.m_name), System.StringComparer.OrdinalIgnoreCase);
        if (acceptedNames.Count == 0)
        {
            return 0;
        }

        int moved = 0;
        foreach (ItemData item in candidates)
        {
            if (!playerInventory.m_inventory.Contains(item) || item?.m_shared == null || !acceptedNames.Contains(item.m_shared.m_name))
            {
                continue;
            }

            if (MoveItemToContainerTopFirst(player, playerInventory, containerInventory, item) > 0)
            {
                moved++;
            }
        }

        if (moved > 0)
        {
            containerInventory.Changed();
        }

        return moved;
    }

    private static void StoreAllToCurrentContainer(Player? player)
    {
        if (!TryGetActionContext(player, out Player localPlayer, out Inventory playerInventory, out _, out Inventory containerInventory))
        {
            return;
        }

        List<ItemData> candidates = playerInventory.m_inventory
            .Where(item => ShouldStoreAllItem(localPlayer, playerInventory, item))
            .ToList();
        candidates.Sort((a, b) => CompareGridOrder(a.m_gridPos, b.m_gridPos));

        InventoryGui.instance.SetupDragItem(null, null, 0);
        int moved = 0;
        foreach (ItemData item in candidates)
        {
            if (!playerInventory.m_inventory.Contains(item))
            {
                continue;
            }

            if (MoveItemToContainerTopFirst(localPlayer, playerInventory, containerInventory, item) > 0)
            {
                moved++;
                if (item.m_equipped)
                {
                    localPlayer.RemoveEquipAction(item);
                    localPlayer.UnequipItem(item, false);
                }
            }
        }

        if (moved > 0)
        {
            playerInventory.Changed();
            containerInventory.Changed();
            ClearCraftingRequirementAvailabilityCache();
        }

    }

    private static int MoveItemToContainerTopFirst(
        Player player,
        Inventory sourceInventory,
        Inventory targetInventory,
        ItemData source)
    {
        if (sourceInventory == null ||
            targetInventory == null ||
            ReferenceEquals(sourceInventory, targetInventory) ||
            source?.m_shared == null ||
            !sourceInventory.m_inventory.Contains(source) ||
            IsEquippedContainerMoveSource(player, source))
        {
            return 0;
        }

        int movedAmount = 0;
        if (source.m_shared.m_maxStackSize > 1 &&
            CanUseContainerActionStacking(source))
        {
            IEnumerable<ItemData> compatibleTargets = targetInventory.m_inventory
                .Where(target =>
                    target?.m_shared != null &&
                    target.m_stack < target.m_shared.m_maxStackSize &&
                    CanShareInventoryStack(target, source));
            if (!IsTrustedCustomDataStackingItem(source))
            {
                compatibleTargets = compatibleTargets
                    .OrderBy(target => target.m_gridPos.y)
                    .ThenBy(target => target.m_gridPos.x);
            }

            // Trusted custom-data mods patch the vanilla stack search and use
            // inventory insertion order to choose and merge their stack data.
            List<ItemData> stackTargets = compatibleTargets.ToList();

            foreach (ItemData target in stackTargets)
            {
                if (!sourceInventory.m_inventory.Contains(source) ||
                    source.m_stack <= 0 ||
                    IsEquippedContainerMoveSource(player, source))
                {
                    break;
                }

                if (!targetInventory.m_inventory.Contains(target) ||
                    target?.m_shared == null ||
                    !CanShareInventoryStack(target, source))
                {
                    continue;
                }

                int amount = Math.Min(
                    target.m_shared.m_maxStackSize - target.m_stack,
                    source.m_stack);
                if (amount <= 0)
                {
                    continue;
                }

                int before = source.m_stack;
                bool movedOk = targetInventory.MoveItemToThis(
                    sourceInventory,
                    source,
                    amount,
                    target.m_gridPos.x,
                    target.m_gridPos.y);
                movedAmount += CountMovedFromContainerSource(
                    sourceInventory,
                    source,
                    before,
                    amount,
                    movedOk);
            }
        }

        for (int y = 0;
             y < targetInventory.GetHeight() &&
             sourceInventory.m_inventory.Contains(source) &&
             source.m_stack > 0;
             y++)
        {
            for (int x = 0; x < targetInventory.GetWidth(); x++)
            {
                if (targetInventory.GetItemAt(x, y) != null)
                {
                    continue;
                }

                if (IsEquippedContainerMoveSource(player, source))
                {
                    return movedAmount;
                }

                int before = source.m_stack;
                int requestedAmount = source.m_stack;
                bool movedOk = targetInventory.MoveItemToThis(
                    sourceInventory,
                    source,
                    requestedAmount,
                    x,
                    y);
                int moved = CountMovedFromContainerSource(
                    sourceInventory,
                    source,
                    before,
                    requestedAmount,
                    movedOk);
                movedAmount += moved;
                if (moved == 0)
                {
                    return movedAmount;
                }

                if (!sourceInventory.m_inventory.Contains(source) ||
                    source.m_stack <= 0)
                {
                    return movedAmount;
                }
            }
        }

        return movedAmount;
    }

    private static bool ShouldStoreAllItem(Player player, Inventory inventory, ItemData item)
    {
        return item?.m_shared != null &&
               IsRegularActionItem(player, inventory, item) &&
               !IsFavoriteProtected(player, inventory, item) &&
               !IsEquippedContainerMoveSource(player, item);
    }

    private static bool IsEquippedContainerMoveSource(Player player, ItemData item)
    {
        if (player == null || item == null || item.m_equipped)
        {
            return true;
        }

        try
        {
            return player.IsItemEquiped(item);
        }
        catch
        {
            return true;
        }
    }
}
