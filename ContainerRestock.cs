using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Configuration;
using UnityEngine;
using ItemData = ItemDrop.ItemData;

namespace InventorySlots;

public sealed partial class InventorySlotsPlugin
{
    private enum ContainerTakeStacksMode
    {
        CurrentContainerMatchingStacks,
        AreaFavoriteRestock
    }

    private static void RefreshRestockTargetStackLimits()
    {
        InventoryDefinitions.RestockTargetStackLimits = RestockTargetLimitCore.Parse(_restockTargetStackLimitsConfig?.Value);
    }

    private static void RestockFromCurrentContainer(Player? player)
    {
        if (!TryGetActionContext(player, out Player localPlayer, out Inventory playerInventory, out Container container, out _))
        {
            return;
        }

        RestockFromContainer(localPlayer, playerInventory, container, ContainerTakeStacksMode.CurrentContainerMatchingStacks);
    }

    private static void HandleContainerRestockHotkey(Player player)
    {
        HandleContainerHoldHotkey(
            player,
            InventoryPanels.ContainerRestockHold,
            TryGetContainerRestockHotkeyContext,
            TryRestockFromHoveredContainer);
    }

    internal static bool ShouldSuppressContainerInteractForRestock(Container container, Humanoid character)
    {
        Player? player = Player.m_localPlayer;
        if (player == null || character != (Humanoid)player || InventoryGui.IsVisible() || !IsContainerRestockShortcutHeld())
        {
            return false;
        }

        Container? hovered = GetHoveredContainer(player);
        return hovered == container && CanHandleContainerRestock(player, container);
    }

    private static bool ShouldSuppressContainerStackAllForRestock(Container container)
    {
        Player? player = Player.m_localPlayer;
        if (player == null || player.m_isLoading || InventoryGui.IsVisible() || !IsContainerRestockShortcutHeld())
        {
            return false;
        }

        Container? hovered = GetHoveredContainer(player);
        return hovered == container;
    }

    internal static void AppendContainerRestockHoverText(Container container, ref string text)
    {
        Player? player = Player.m_localPlayer;
        if (player == null || player.m_isLoading || string.IsNullOrWhiteSpace(text) || !IsContainerRestockKeyConfigured() || !CanHandleContainerRestock(player, container))
        {
            return;
        }

        string hint = LocalizeUi("$inventoryslots_hold_to_restock", "Hold to restock");
        if (text.IndexOf("Hold to restock", StringComparison.OrdinalIgnoreCase) >= 0 ||
            text.IndexOf(hint, StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return;
        }

        string keyText = GetContainerRestockKeyDisplayText();
        if (string.IsNullOrWhiteSpace(keyText))
        {
            return;
        }

        text += $"\n[<color=yellow><b>{keyText}</b></color>] ({hint})";
    }

    private static bool TryGetContainerRestockHotkeyContext(Player player, out Container container)
    {
        container = null!;
        if (player == null || InventoryGui.IsVisible() || ShouldBlockGlobalHotkeys(player) || !IsContainerRestockShortcutHeld())
        {
            return false;
        }

        Container? hovered = GetHoveredContainer(player);
        if (hovered == null || !CanHandleContainerRestock(player, hovered))
        {
            return false;
        }

        container = hovered;
        return true;
    }

    private static bool TryRestockFromHoveredContainer(Player player, Container container)
    {
        if (!CanHandleContainerRestock(player, container))
        {
            return false;
        }

        if (TryHandleMultiUserContainerAreaRestock(container))
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

        RestockFromContainer(player, playerInventory, container, ContainerTakeStacksMode.AreaFavoriteRestock);
        return true;
    }

    private static void RestockFromContainer(Player localPlayer, Inventory playerInventory, Container container, ContainerTakeStacksMode mode)
    {
        if (localPlayer == null || playerInventory == null || container == null || container.m_inventory == null)
        {
            return;
        }

        bool includeArea = mode == ContainerTakeStacksMode.AreaFavoriteRestock;
        List<ItemData> targets = playerInventory.m_inventory
            .Where(item => ShouldTakeStacksTarget(localPlayer, playerInventory, item, mode))
            .ToList();
        targets.Sort((a, b) => -CompareGridOrder(a.m_gridPos, b.m_gridPos));

        InventoryGui.instance?.SetupDragItem(null, null, 0);
        int movedAmount = RunContainerTransferAcrossContainers(
            localPlayer,
            container,
            includeArea,
            areaForQuickStack: false,
            sourceContainer => RestockTargetsFromContainer(playerInventory, sourceContainer.m_inventory, targets, mode),
            () =>
            {
                playerInventory.Changed();
                ClearCraftingRequirementAvailabilityCache();
            });

        ShowContainerActionResult(localPlayer, "$inventoryslots_action_take_stacks", "Take stacks", movedAmount);
    }

    private static bool IsContainerRestockKeyConfigured() =>
        _containerRestockKey != null && _containerRestockKey.Value.MainKey != KeyCode.None ||
        IsControllerHotkeyConfigured(_controllerContainerRestockButton);

    private static bool IsContainerRestockShortcutHeld() =>
        _containerRestockKey != null &&
        _containerRestockKey.Value.MainKey != KeyCode.None &&
        IsShortcutHeldAllowingAltPair(_containerRestockKey.Value) ||
        IsControllerHotkeyHeld(_controllerContainerRestockButton);

    private static string GetContainerRestockKeyDisplayText() =>
        JoinShortcutDisplayTexts(
            _containerRestockKey != null ? _containerRestockKey.Value.GetCompactDisplayText() : "",
            GetControllerHotkeyDisplayText(_controllerContainerRestockButton));

    private static bool ShouldTakeStacksTarget(Player player, Inventory inventory, ItemData item, ContainerTakeStacksMode mode)
    {
        return mode == ContainerTakeStacksMode.AreaFavoriteRestock
            ? ShouldRestockItem(player, inventory, item)
            : ShouldTakeMatchingStackItem(player, inventory, item);
    }

    private static bool ShouldRestockItem(Player player, Inventory inventory, ItemData item)
    {
        if (item?.m_shared == null || !IsFavoriteRestockActionItem(player, inventory, item) || !IsFavoriteRestockTarget(player, item) || !CanUseContainerActionStacking(item))
        {
            return false;
        }

        int itemMaxStack = item.m_shared.m_maxStackSize;
        int targetStack = GetRestockTargetStack(item);
        return itemMaxStack > 1 && item.m_stack < targetStack;
    }

    private static bool ShouldTakeMatchingStackItem(Player player, Inventory inventory, ItemData item)
    {
        return item?.m_shared != null &&
               IsRegularActionItem(player, inventory, item) &&
               !IsFavoriteRestockTarget(player, item) &&
               CanUseContainerActionStacking(item) &&
               item.m_shared.m_maxStackSize > 1 &&
               item.m_stack < item.m_shared.m_maxStackSize;
    }

    private static bool IsFavoriteRestockActionItem(Player player, Inventory inventory, ItemData item)
    {
        InventoryCellKind kind = GetInventoryCellKind(player, inventory, item.m_gridPos);
        return InventoryActionCellPolicyCore.CanUseFavoriteRestockTarget(kind);
    }

    private static bool IsFavoriteRestockTarget(Player player, ItemData item)
    {
        return IsFavoriteSlot(player, item.m_gridPos);
    }

    private static int GetRestockTargetStack(ItemData item)
    {
        return item?.m_shared == null
            ? 0
            : RestockTargetLimitCore.ResolveTargetStackLimit(
                InventoryDefinitions.RestockTargetStackLimits,
                GetRestockTargetLookupTokens(item),
                item.m_shared.m_maxStackSize);
    }

    private static IEnumerable<string?> GetRestockTargetLookupTokens(ItemData item)
    {
        if (item == null)
        {
            yield break;
        }

        yield return GetItemPrefabName(item);
        string sharedName = item.m_shared?.m_name ?? "";
        yield return sharedName;
        if (Localization.instance != null && !string.IsNullOrWhiteSpace(sharedName))
        {
            yield return Localization.instance.Localize(sharedName);
        }
    }

    private static bool CanRestockFromContainerItem(ItemData target, ItemData source)
    {
        return target?.m_shared != null &&
               source?.m_shared != null &&
               CanUseContainerActionStacking(target) &&
               CanUseContainerActionStacking(source) &&
               CanShareInventoryStack(target, source);
    }

    private static int RestockTargetsFromContainer(Inventory playerInventory, Inventory containerInventory, List<ItemData> targets, ContainerTakeStacksMode mode)
    {
        if (containerInventory == null || targets.Count == 0)
        {
            return 0;
        }

        int movedAmount = 0;
        foreach (ItemData playerItem in targets)
        {
            if (!playerInventory.m_inventory.Contains(playerItem))
            {
                continue;
            }

            int wanted = mode == ContainerTakeStacksMode.AreaFavoriteRestock
                ? GetRestockTargetStack(playerItem)
                : playerItem.m_shared.m_maxStackSize;
            int potential = playerItem.m_stack;
            for (int i = containerInventory.m_inventory.Count - 1; i >= 0 && potential < wanted; i--)
            {
                ItemData containerItem = containerInventory.m_inventory[i];
                if (!CanRestockFromContainerItem(playerItem, containerItem))
                {
                    continue;
                }

                int amount = Math.Min(wanted - potential, containerItem.m_stack);
                if (amount <= 0)
                {
                    continue;
                }

                int before = containerItem.m_stack;
                bool movedOk = playerInventory.MoveItemToThis(containerInventory, containerItem, amount, playerItem.m_gridPos.x, playerItem.m_gridPos.y);
                int moved = CountMovedFromContainerSource(containerInventory, containerItem, before, amount, movedOk);
                potential += moved;
                movedAmount += moved;
            }
        }

        if (movedAmount > 0)
        {
            containerInventory.Changed();
        }

        return movedAmount;
    }
}
