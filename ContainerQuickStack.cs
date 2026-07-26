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
        if (accessMode == ContainerAccessMode.MultiUserChestRemote &&
            IsBuiltInMultiUserChestEnabled)
        {
            InventoryGui? gui = InventoryGui.instance;
            if (gui == null ||
                IsUnityNull(gui) ||
                gui.m_currentContainer != container ||
                !TryStartMultiUserContainerPlaceStacksBatch(container))
            {
                ShowMultiUserContainerNotReady();
            }

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
            !CanMutateContainerDirectly(hovered, allowLocalWithoutZNetView: true))
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
        if (container == null || container.m_inventory == null || !CanMutateContainerDirectly(container, allowLocalWithoutZNetView: true))
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
            .Where(item => ShouldQuickStackItem(localPlayer, playerInventory, item, includeHotbar: false))
            .ToList();
        candidates.Sort((a, b) => -CompareGridOrder(a.m_gridPos, b.m_gridPos));

        InventoryGui.instance?.SetupDragItem(null, null, 0);
        int moved = RunContainerTransferAcrossContainers(
            localPlayer,
            container,
            includeArea,
            areaForQuickStack: true,
            targetContainer => QuickStackItemsIntoContainer(playerInventory, targetContainer.m_inventory, candidates),
            () =>
            {
                playerInventory.Changed();
                ClearCraftingRequirementAvailabilityCache();
            });

        ShowContainerActionResult(localPlayer, "$inventoryslots_action_stack", "Stack", moved);
    }

    private static bool ShouldQuickStackItem(Player player, Inventory inventory, ItemData item, bool includeHotbar)
    {
        return item?.m_shared != null &&
               item.m_shared.m_maxStackSize > 1 &&
               IsRegularActionItem(player, inventory, item, includeHotbar) &&
               !IsFavoriteProtected(player, inventory, item) &&
               CanUseContainerActionStacking(item);
    }

    private static int QuickStackItemsIntoContainer(Inventory playerInventory, Inventory containerInventory, List<ItemData> candidates)
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

            if (containerInventory.AddItem(item))
            {
                RemoveItemIfStillOwned(playerInventory, item);
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
            .Where(item => ShouldStoreAllItem(localPlayer, playerInventory, item, includeHotbar: false, includeEquipped: false))
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

            if (containerInventory.AddItem(item))
            {
                RemoveItemIfStillOwned(playerInventory, item);
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

    private static bool ShouldStoreAllItem(Player player, Inventory inventory, ItemData item, bool includeHotbar, bool includeEquipped)
    {
        return item?.m_shared != null &&
               IsRegularActionItem(player, inventory, item, includeHotbar) &&
               !IsFavoriteProtected(player, inventory, item) &&
               (includeEquipped || !item.m_equipped);
    }
}
