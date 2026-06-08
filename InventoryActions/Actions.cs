using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using ItemData = ItemDrop.ItemData;

namespace InventoryActions;

public sealed partial class InventoryActionsPlugin
{
    private static readonly Dictionary<Type, SfxVolumeMemberCache> SfxVolumeMembersByType = new();

    private enum RestockMode
    {
        CurrentContainerMatchingStacks,
        AreaFavoriteRestock
    }

    internal static bool TryHandleVanillaPlaceStacks(InventoryGui gui)
    {
        if (gui == null || gui.m_currentContainer == null)
        {
            return false;
        }

        if (!CanMutateContainerDirectly(gui.m_currentContainer, allowLocalWithoutZNetView: true))
        {
            return false;
        }

        QuickStackCurrentContainer(Player.m_localPlayer);
        return true;
    }

    internal static bool TryHandleContainerStackAll(Container container)
    {
        Player? player = Player.m_localPlayer;
        if (player == null || player.m_isLoading || container == null || container.m_inventory == null)
        {
            return false;
        }

        if (!CanMutateContainerDirectly(container, allowLocalWithoutZNetView: true))
        {
            return false;
        }

        Inventory? playerInventory = GetPlayerInventory(player);
        if (playerInventory == null)
        {
            return false;
        }

        QuickStackIntoContainers(player, playerInventory, container, includeArea: true);
        return true;
    }

    internal static bool TryHandleSafeTakeAll(InventoryGui gui)
    {
        Player? player = Player.m_localPlayer;
        if (player == null || player.m_isLoading || gui == null || gui.m_currentContainer == null || gui.m_currentContainer.m_inventory == null)
        {
            return false;
        }

        Container container = gui.m_currentContainer;
        if (!CanMutateContainerDirectly(container, allowLocalWithoutZNetView: true))
        {
            player.Message(MessageHud.MessageType.Center, LocalizeUi("$inventoryactions_container_not_ready", "Container is not ready."), 0, null);
            return true;
        }

        Inventory? playerInventory = GetPlayerInventory(player);
        Inventory containerInventory = container.m_inventory;
        if (playerInventory == null || containerInventory == null)
        {
            return false;
        }

        TombStone? tombstone = container.GetComponent<TombStone>();
        gui.SetupDragItem(null, null, 0);
        int movedStacks = SafeTakeAllItems(player, playerInventory, containerInventory);
        if (movedStacks > 0)
        {
            playerInventory.Changed();
            containerInventory.Changed();
        }

        if (tombstone != null && containerInventory.NrOfItems() == 0)
        {
            tombstone.OnTakeAllSuccess();
        }

        ShowActionResult(player, LocalizeUi("$inventoryactions_action_take_all", "Take All"), movedStacks);
        return true;
    }

    internal static bool TryHandleTopFirstMoveSelectedItem(InventoryGui gui, InventoryGrid grid, ItemData item, InventoryGrid.Modifier mod)
    {
        if (gui == null ||
            grid == null ||
            item?.m_shared == null ||
            mod != InventoryGrid.Modifier.Move ||
            item.m_shared.m_questItem ||
            gui.m_dragGo != null)
        {
            return false;
        }

        Player? player = Player.m_localPlayer;
        Container? container = gui.m_currentContainer;
        if (player == null ||
            player.IsTeleporting() ||
            container == null ||
            container.m_inventory == null ||
            !CanMutateContainerDirectly(container, allowLocalWithoutZNetView: true))
        {
            return false;
        }

        Inventory? playerInventory = GetPlayerInventory(player);
        Inventory sourceInventory = grid.GetInventory();
        Inventory containerInventory = container.m_inventory;
        if (playerInventory == null || sourceInventory == null)
        {
            return false;
        }

        Inventory? targetInventory = null;
        if (sourceInventory == containerInventory)
        {
            targetInventory = playerInventory;
        }
        else if (sourceInventory == playerInventory)
        {
            targetInventory = containerInventory;
        }

        if (targetInventory == null)
        {
            return false;
        }

        if (!CanMoveItemToInventoryTopFirst(sourceInventory, targetInventory, item))
        {
            return true;
        }

        if (sourceInventory == playerInventory && ((Humanoid)player).IsItemEquiped(item))
        {
            player.RemoveEquipAction(item);
            ((Humanoid)player).UnequipItem(item, false);
        }

        int movedAmount = MoveItemToInventoryTopFirst(sourceInventory, targetInventory, item);
        if (movedAmount > 0)
        {
            playerInventory.Changed();
            containerInventory.Changed();
            gui.m_moveItemEffects.Create(gui.transform.position, Quaternion.identity);
        }

        return true;
    }

    private static int SafeTakeAllItems(Player player, Inventory playerInventory, Inventory containerInventory)
    {
        List<Vector2i> actionSlots = GetPlayerActionSlots(player, playerInventory, includeHotbar: false, blockFavorites: true);
        HashSet<Vector2i> allowedSlots = new(actionSlots);
        List<Vector2i> emptySlots = actionSlots
            .Where(slot => playerInventory.GetItemAt(slot.x, slot.y) == null)
            .ToList();

        List<ItemData> sourceItems = containerInventory.m_inventory
            .Where(item => item?.m_shared != null)
            .OrderBy(item => item.m_gridPos.y)
            .ThenBy(item => item.m_gridPos.x)
            .ToList();

        int movedStacks = 0;
        foreach (ItemData source in sourceItems)
        {
            if (!containerInventory.m_inventory.Contains(source))
            {
                continue;
            }

            int before = source.m_stack;
            int movedAmount = MoveContainerItemSafelyToPlayer(playerInventory, containerInventory, source, emptySlots, allowedSlots);
            if (movedAmount > 0 && (!containerInventory.m_inventory.Contains(source) || source.m_stack < before))
            {
                movedStacks++;
            }
        }

        return movedStacks;
    }

    private static int MoveContainerItemSafelyToPlayer(Inventory playerInventory, Inventory containerInventory, ItemData source, List<Vector2i> emptySlots, HashSet<Vector2i> allowedSlots)
    {
        int movedAmount = 0;
        if (CanMergeForSafeMove(source))
        {
            List<ItemData> stackTargets = playerInventory.m_inventory
                .Where(target => target?.m_shared != null &&
                                 allowedSlots.Contains(target.m_gridPos) &&
                                 CanMergeForSafeMove(target) &&
                                 HasSameStackIdentity(target, source) &&
                                 target.m_stack < target.m_shared.m_maxStackSize)
                .OrderBy(target => target.m_gridPos.y)
                .ThenBy(target => target.m_gridPos.x)
                .ToList();

            foreach (ItemData target in stackTargets)
            {
                if (!containerInventory.m_inventory.Contains(source) || source.m_stack <= 0)
                {
                    break;
                }

                int amount = Math.Min(target.m_shared.m_maxStackSize - target.m_stack, source.m_stack);
                if (amount <= 0)
                {
                    continue;
                }

                int before = source.m_stack;
                bool movedOk = playerInventory.MoveItemToThis(containerInventory, source, amount, target.m_gridPos.x, target.m_gridPos.y);
                movedAmount += CountMovedFromContainerSource(containerInventory, source, before, amount, movedOk);
            }
        }

        while (containerInventory.m_inventory.Contains(source) && source.m_stack > 0 && emptySlots.Count > 0)
        {
            Vector2i slot = emptySlots[0];
            emptySlots.RemoveAt(0);
            int before = source.m_stack;
            int requestedAmount = source.m_stack;
            bool movedOk = playerInventory.MoveItemToThis(containerInventory, source, requestedAmount, slot.x, slot.y);
            int moved = CountMovedFromContainerSource(containerInventory, source, before, requestedAmount, movedOk);
            movedAmount += moved;
            if (moved == 0)
            {
                break;
            }
        }

        return movedAmount;
    }

    private static bool CanMergeForSafeMove(ItemData item)
    {
        return item?.m_shared != null && item.m_shared.m_maxStackSize > 1 && CanUseContainerActionStacking(item);
    }

    private static void QuickStackCurrentContainer(Player? player)
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
            () => playerInventory.Changed());

        ShowActionResult(localPlayer, LocalizeUi("$inventoryactions_action_stack", "Stack"), moved);
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
            .Select(item => item.m_shared.m_name), StringComparer.OrdinalIgnoreCase);
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

            if (MoveItemToInventoryTopFirst(playerInventory, containerInventory, item) > 0)
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
            .Where(item => ShouldStoreAllItem(localPlayer, playerInventory, item, includeHotbar: false, includeEquipped: false))
            .ToList();
        candidates.Sort((a, b) => CompareGridOrder(a.m_gridPos, b.m_gridPos));

        InventoryGui.instance?.SetupDragItem(null, null, 0);
        int moved = 0;
        foreach (ItemData item in candidates)
        {
            if (!playerInventory.m_inventory.Contains(item))
            {
                continue;
            }

            if (MoveItemToInventoryTopFirst(playerInventory, containerInventory, item) > 0)
            {
                moved++;
            }
        }

        if (moved > 0)
        {
            playerInventory.Changed();
            containerInventory.Changed();
        }

        ShowActionResult(localPlayer, LocalizeUi("$inventoryactions_action_place_all", "Place All"), moved);
    }

    private static bool ShouldStoreAllItem(Player player, Inventory inventory, ItemData item, bool includeHotbar, bool includeEquipped)
    {
        return item?.m_shared != null &&
               IsRegularActionItem(player, inventory, item, includeHotbar) &&
               !IsFavoriteProtected(player, inventory, item) &&
               (includeEquipped || !item.m_equipped);
    }

    private static void RestockFromCurrentContainer(Player? player)
    {
        if (!TryGetActionContext(player, out Player localPlayer, out Inventory playerInventory, out Container container, out _))
        {
            return;
        }

        RestockFromContainer(localPlayer, playerInventory, container, RestockMode.CurrentContainerMatchingStacks);
    }

    private static void RefreshRestockTargetStackLimits()
    {
        Runtime.RestockTargetStackLimits = RestockTargetLimitCore.Parse(_restockTargetStackLimitsConfig?.Value);
    }

    private static void RestockFromContainer(Player localPlayer, Inventory playerInventory, Container container, RestockMode mode)
    {
        if (localPlayer == null || playerInventory == null || container == null || container.m_inventory == null)
        {
            return;
        }

        bool includeArea = mode == RestockMode.AreaFavoriteRestock;
        List<ItemData> targets = playerInventory.m_inventory
            .Where(item => ShouldTakeStacksTarget(localPlayer, playerInventory, item, includeHotbar: false, mode))
            .ToList();
        targets.Sort((a, b) => -CompareGridOrder(a.m_gridPos, b.m_gridPos));

        InventoryGui.instance?.SetupDragItem(null, null, 0);
        int movedAmount = RunContainerTransferAcrossContainers(
            localPlayer,
            container,
            includeArea,
            areaForQuickStack: false,
            sourceContainer => RestockTargetsFromContainer(playerInventory, sourceContainer.m_inventory, targets, mode),
            () => playerInventory.Changed());

        ShowActionResult(localPlayer, LocalizeUi("$inventoryactions_action_take_stacks", "Take stacks"), movedAmount);
    }

    private static bool ShouldTakeStacksTarget(Player player, Inventory inventory, ItemData item, bool includeHotbar, RestockMode mode)
    {
        return mode == RestockMode.AreaFavoriteRestock
            ? ShouldRestockFavoriteItem(player, inventory, item)
            : ShouldTakeMatchingStackItem(player, inventory, item, includeHotbar);
    }

    private static bool ShouldRestockFavoriteItem(Player player, Inventory inventory, ItemData item)
    {
        if (item?.m_shared == null ||
            !IsPlayerActionCell(inventory, item.m_gridPos, includeHotbar: true) ||
            !IsFavoriteSlot(player, item.m_gridPos) ||
            !CanUseContainerActionStacking(item))
        {
            return false;
        }

        int targetStack = GetRestockTargetStack(item);
        return item.m_shared.m_maxStackSize > 1 && item.m_stack < targetStack;
    }

    private static bool ShouldTakeMatchingStackItem(Player player, Inventory inventory, ItemData item, bool includeHotbar)
    {
        return item?.m_shared != null &&
               IsRegularActionItem(player, inventory, item, includeHotbar) &&
               !IsFavoriteSlot(player, item.m_gridPos) &&
               CanUseContainerActionStacking(item) &&
               item.m_shared.m_maxStackSize > 1 &&
               item.m_stack < item.m_shared.m_maxStackSize;
    }

    private static int GetRestockTargetStack(ItemData item)
    {
        return item?.m_shared == null
            ? 0
            : RestockTargetLimitCore.ResolveTargetStackLimit(
                Runtime.RestockTargetStackLimits,
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
        if (!IsUnityNull(item.m_dropPrefab))
        {
            yield return item.m_dropPrefab.name;
        }

        string sharedName = item.m_shared?.m_name ?? "";
        yield return sharedName;
        yield return RestockTargetLimitCore.StripLocalizationToken(sharedName);
        if (Localization.instance != null && !string.IsNullOrWhiteSpace(sharedName))
        {
            yield return Localization.instance.Localize(sharedName);
        }
    }

    private static int RestockTargetsFromContainer(Inventory playerInventory, Inventory containerInventory, List<ItemData> targets, RestockMode mode)
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

            int wanted = mode == RestockMode.AreaFavoriteRestock
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

    private static bool CanRestockFromContainerItem(ItemData target, ItemData source)
    {
        return target?.m_shared != null &&
               source?.m_shared != null &&
               CanUseContainerActionStacking(target) &&
               CanUseContainerActionStacking(source) &&
               HasSameStackIdentity(target, source);
    }

    private static void SortCurrentContainer(Player? player)
    {
        if (player == null || player.m_isLoading || InventoryGui.instance == null)
        {
            return;
        }

        Container container = InventoryGui.instance.m_currentContainer;
        if (container == null || container.m_inventory == null)
        {
            ShowActionResult(player, LocalizeUi("$inventoryactions_action_container", "Container"), 0);
            return;
        }

        if (!CanMutateContainerDirectly(container, allowLocalWithoutZNetView: true))
        {
            player.Message(MessageHud.MessageType.Center, LocalizeUi("$inventoryactions_container_not_ready", "Container is not ready."), 0, null);
            return;
        }

        int moved = SortContainerInventory(container);
        ShowActionResult(player, LocalizeUi("$inventoryactions_action_sort", "Sort"), moved);
    }

    private static int SortContainerInventory(Container container)
    {
        if (container == null || container.m_inventory == null)
        {
            return 0;
        }

        List<Vector2i> allowedSlots = GetAllInventorySlots(container.m_inventory);
        return SortInventoryInternal(container.m_inventory, allowedSlots, item => item?.m_shared != null);
    }

    private static void SortPlayerInventory(Player? player)
    {
        if (player == null || player.m_isLoading)
        {
            return;
        }

        Inventory? inventory = GetPlayerInventory(player);
        if (inventory == null)
        {
            return;
        }

        List<Vector2i> allowedSlots = GetPlayerActionSlots(player, inventory, includeHotbar: false, blockFavorites: true);
        HashSet<Vector2i> allowedSet = new(allowedSlots);
        InventoryGui.instance?.SetupDragItem(null, null, 0);
        int moved = SortInventoryInternal(inventory, allowedSlots, item => item?.m_shared != null && allowedSet.Contains(item.m_gridPos) && !IsFavoriteProtected(player, inventory, item));
        ShowActionResult(player, LocalizeUi("$inventoryactions_action_sort_inventory", "Sort Inv"), moved);
    }

    private static int SortInventoryInternal(Inventory inventory, List<Vector2i> allowedSlots, Func<ItemData, bool> shouldSort)
    {
        if (inventory == null || allowedSlots.Count == 0)
        {
            return 0;
        }

        List<ItemData> toSort = inventory.m_inventory.Where(shouldSort).ToList();
        if (toSort.Count == 0)
        {
            return 0;
        }

        bool changed = MergeSortableStacks(toSort, inventory);
        int inventoryWidth = Mathf.Max(1, inventory.GetWidth());
        Dictionary<ItemData, int> originalIndices = toSort.ToDictionary(item => item, item => item.m_gridPos.y * inventoryWidth + item.m_gridPos.x);
        toSort.Sort((a, b) => CompareItemsForSort(a, b, originalIndices[a], originalIndices[b]));

        int moved = 0;
        int count = Math.Min(toSort.Count, allowedSlots.Count);
        for (int i = 0; i < count; i++)
        {
            if (toSort[i].m_gridPos != allowedSlots[i])
            {
                moved++;
                changed = true;
                toSort[i].m_gridPos = allowedSlots[i];
            }
        }

        if (changed)
        {
            inventory.Changed();
        }

        return moved;
    }

    private static bool MergeSortableStacks(List<ItemData> toMerge, Inventory inventory)
    {
        bool changed = false;
        List<List<ItemData>> grouped = toMerge
            .Where(item => item?.m_shared != null && item.m_stack < item.m_shared.m_maxStackSize && CanUseContainerActionStacking(item))
            .GroupBy(item => new { item.m_shared.m_name, item.m_quality, item.m_worldLevel })
            .Select(grouping => grouping.ToList())
            .ToList();

        foreach (List<ItemData> group in grouped)
        {
            if (group.Count <= 1)
            {
                continue;
            }

            int total = group.Sum(item => item.m_stack);
            int maxStack = group[0].m_shared.m_maxStackSize;
            foreach (ItemData item in group.ToList())
            {
                if (total <= 0)
                {
                    if (item.m_stack != 0)
                    {
                        item.m_stack = 0;
                        changed = true;
                    }

                    inventory.RemoveItem(item);
                    toMerge.Remove(item);
                    changed = true;
                    continue;
                }

                int nextStack = Math.Min(maxStack, total);
                if (item.m_stack != nextStack)
                {
                    item.m_stack = nextStack;
                    changed = true;
                }

                total -= item.m_stack;
            }
        }

        return changed;
    }

    private static int CompareItemsForSort(ItemData a, ItemData b, int aOriginalIndex, int bOriginalIndex)
    {
        int comparison = GetItemSortCategory(a).CompareTo(GetItemSortCategory(b));
        if (comparison != 0)
        {
            return comparison;
        }

        comparison = string.Compare(GetLocalizedItemName(a), GetLocalizedItemName(b), StringComparison.OrdinalIgnoreCase);
        if (comparison != 0)
        {
            return comparison;
        }

        comparison = -a.m_quality.CompareTo(b.m_quality);
        if (comparison != 0)
        {
            return comparison;
        }

        comparison = -a.m_stack.CompareTo(b.m_stack);
        return comparison != 0 ? comparison : aOriginalIndex.CompareTo(bOriginalIndex);
    }

    private static int GetItemSortCategory(ItemData item)
    {
        string type = item?.m_shared?.m_itemType.ToString() ?? "";
        return type switch
        {
            "OneHandedWeapon" or "TwoHandedWeapon" or "TwoHandedWeaponLeft" or "Bow" or "Torch" or "Tool" or "Shield" => 10,
            "Ammo" or "AmmoNonEquipable" => 20,
            "Helmet" or "Chest" or "Legs" or "Shoulder" or "Utility" => 30,
            "Consumable" or "Fish" => 40,
            "Material" => 50,
            "Trophie" => 60,
            _ => 90
        };
    }

    private static List<Vector2i> GetPlayerActionSlots(Player player, Inventory inventory, bool includeHotbar, bool blockFavorites = false)
    {
        List<Vector2i> slots = new();
        int rows = Math.Min(VanillaPlayerRows, inventory.GetHeight());
        EnsureFavoritesLoaded(player);
        for (int y = 0; y < rows; y++)
        {
            for (int x = 0; x < inventory.GetWidth(); x++)
            {
                Vector2i pos = new(x, y);
                if (blockFavorites && Runtime.FavoriteSlots.Contains(pos))
                {
                    continue;
                }

                if (IsPlayerActionCell(inventory, pos, includeHotbar))
                {
                    slots.Add(pos);
                }
            }
        }

        return slots;
    }

    private static List<Vector2i> GetAllInventorySlots(Inventory inventory)
    {
        List<Vector2i> slots = new();
        for (int y = 0; y < inventory.GetHeight(); y++)
        {
            for (int x = 0; x < inventory.GetWidth(); x++)
            {
                slots.Add(new Vector2i(x, y));
            }
        }

        return slots;
    }

    private static bool TryGetActionContext(Player? player, out Player localPlayer, out Inventory playerInventory, out Container container, out Inventory containerInventory)
    {
        localPlayer = null!;
        playerInventory = null!;
        container = null!;
        containerInventory = null!;

        if (player == null || player.m_isLoading || InventoryGui.instance == null)
        {
            return false;
        }

        Container currentContainer = InventoryGui.instance.m_currentContainer;
        if (currentContainer == null || currentContainer.m_inventory == null)
        {
            ShowActionResult(player, LocalizeUi("$inventoryactions_action_container", "Container"), 0);
            return false;
        }

        if (!CanMutateContainerDirectly(currentContainer, allowLocalWithoutZNetView: true))
        {
            player.Message(MessageHud.MessageType.Center, LocalizeUi("$inventoryactions_container_not_ready", "Container is not ready."), 0, null);
            return false;
        }

        Inventory? inventory = GetPlayerInventory(player);
        if (inventory == null)
        {
            return false;
        }

        localPlayer = player;
        playerInventory = inventory;
        container = currentContainer;
        containerInventory = currentContainer.m_inventory;
        return true;
    }

    private static void HandleHoverActions(Player player)
    {
        if (InventoryGui.IsVisible() || ShouldBlockGlobalHotkeys(player))
        {
            ResetContainerHold(Runtime.AreaQuickStackHold);
            ResetContainerHold(Runtime.AreaRestockHold);
            return;
        }

        HandleContainerHoldHotkey(
            player,
            Runtime.AreaQuickStackHold,
            IsContainerQuickStackShortcutHeld() && !IsContainerRestockShortcutHeld(),
            TryGetHoverQuickStackContext,
            TryQuickStackFromHoveredContainer);

        HandleContainerHoldHotkey(
            player,
            Runtime.AreaRestockHold,
            IsContainerRestockShortcutHeld(),
            TryGetHoverRestockContext,
            TryRestockFromHoveredContainer);
    }

    private static void HandleContainerHoldHotkey(
        Player player,
        ContainerHoldActionState hold,
        bool shortcutHeld,
        Func<Player, Container?> getContext,
        Func<Player, Container, bool> executeAction)
    {
        Container? container = shortcutHeld ? getContext(player) : null;
        if (container == null)
        {
            ResetContainerHold(hold);
            return;
        }

        if (IsUnityNull(hold.Container) || hold.Container != container)
        {
            hold.Container = container;
            hold.StartTime = Time.time;
            hold.Triggered = false;
        }

        if (hold.Triggered || Time.time - hold.StartTime < Mathf.Clamp(_containerHoverHoldDuration.Value, ContainerHoverHoldDurationMin, ContainerHoverHoldDurationMax))
        {
            return;
        }

        if (executeAction(player, container))
        {
            hold.Triggered = true;
        }
        else
        {
            ResetContainerHold(hold);
        }
    }

    private static void ResetContainerHold(ContainerHoldActionState hold)
    {
        hold.Container = null;
        hold.StartTime = -1f;
        hold.Triggered = false;
    }

    private static Container? TryGetHoverQuickStackContext(Player player)
    {
        Container? hovered = GetHoveredContainer(player);
        if (hovered == null || player.m_isLoading || hovered.m_inventory == null || !CanHandleContainerAction(player, hovered))
        {
            return null;
        }

        return hovered;
    }

    private static Container? TryGetHoverRestockContext(Player player)
    {
        Container? hovered = GetHoveredContainer(player);
        if (hovered == null || !CanHandleContainerAction(player, hovered))
        {
            return null;
        }

        return hovered;
    }

    private static bool TryQuickStackFromHoveredContainer(Player player, Container container)
    {
        Inventory? playerInventory = GetPlayerInventory(player);
        if (playerInventory == null || !CanHandleContainerAction(player, container))
        {
            return false;
        }

        QuickStackIntoContainers(player, playerInventory, container, includeArea: true);
        return true;
    }

    private static bool TryRestockFromHoveredContainer(Player player, Container container)
    {
        Inventory? playerInventory = GetPlayerInventory(player);
        if (playerInventory == null || !CanHandleContainerAction(player, container))
        {
            return false;
        }

        RestockFromContainer(player, playerInventory, container, RestockMode.AreaFavoriteRestock);
        return true;
    }

    private static Container? GetHoveredContainer(Player player)
    {
        GameObject? hoverObject = player != null ? player.GetHoverObject() : null;
        return IsUnityNull(hoverObject) ? null : hoverObject!.GetComponentInParent<Container>();
    }

    internal static bool ShouldSuppressContainerInteractForRestock(Container container, Humanoid character)
    {
        Player? player = Player.m_localPlayer;
        if (player == null ||
            character != (Humanoid)player ||
            InventoryGui.IsVisible() ||
            !IsContainerRestockShortcutHeld())
        {
            return false;
        }

        Container? hovered = GetHoveredContainer(player);
        return hovered == container && CanHandleContainerAction(player, container);
    }

    internal static void AppendContainerActionHoverText(Container container, ref string text)
    {
        Player? player = Player.m_localPlayer;
        if (player == null || player.m_isLoading || string.IsNullOrWhiteSpace(text) || !CanHandleContainerAction(player, container))
        {
            return;
        }

        string restockKey = GetContainerRestockKeyDisplayText();
        if (!string.IsNullOrWhiteSpace(restockKey))
        {
            text += $"\n[<color=yellow><b>{restockKey}</b></color>] ({LocalizeUi("$inventoryactions_hold_to_restock", "Hold to restock")})";
        }
    }

    internal static void RegisterContainer(Container container)
    {
        if (container != null && !Runtime.KnownContainers.Contains(container))
        {
            Runtime.KnownContainers.Add(container);
        }
    }

    internal static void UnregisterContainer(Container container)
    {
        if (container != null)
        {
            Runtime.KnownContainers.Remove(container);
        }
    }

    private static int RunContainerTransferAcrossContainers(Player localPlayer, Container anchorContainer, bool includeArea, bool areaForQuickStack, Func<Container, int> transfer, Action onMoved)
    {
        if (localPlayer == null || anchorContainer == null || transfer == null)
        {
            return 0;
        }

        List<Container> containers = includeArea
            ? GetActionContainers(localPlayer, anchorContainer, areaForQuickStack)
            : new List<Container> { anchorContainer };

        int fxMode = includeArea ? GetContainerActionSuccessFxMode() : 0;
        int changedContainerFxCount = 0;
        int moved = 0;
        foreach (Container container in containers)
        {
            if (container == null || IsUnityNull(container) || container.m_inventory == null)
            {
                continue;
            }

            int containerMoved = transfer(container);
            if (containerMoved <= 0)
            {
                continue;
            }

            moved += containerMoved;
            changedContainerFxCount = TryPlayChangedContainerActionSuccessFx(localPlayer, container, fxMode, changedContainerFxCount);
        }

        if (moved > 0)
        {
            onMoved?.Invoke();
            if (fxMode == 1)
            {
                PlayContainerActionSuccessFx(localPlayer, anchorContainer);
            }
        }

        return moved;
    }

    private static bool IsContainerQuickStackShortcutHeld() =>
        ZInput.GetButton("Use") || ZInput.GetButton("JoyUse");

    private static bool IsContainerRestockShortcutHeld() =>
        _containerRestockKey != null &&
        _containerRestockKey.Value.MainKey != KeyCode.None &&
        IsShortcutHeldAllowingAltPair(_containerRestockKey.Value);

    private static string GetContainerRestockKeyDisplayText() =>
        _containerRestockKey != null ? GetShortcutDisplayText(_containerRestockKey.Value) : "";

    private static List<Container> GetActionContainers(Player player, Container currentContainer, bool areaForQuickStack)
    {
        List<Container> containers = new();
        HashSet<Container> seen = new();
        if (currentContainer != null && currentContainer.m_inventory != null && seen.Add(currentContainer))
        {
            containers.Add(currentContainer);
        }

        float range = areaForQuickStack ? _areaQuickStackRange.Value : _areaRestockRange.Value;
        if (range <= 0f || currentContainer == null || IsUnityNull(currentContainer))
        {
            return containers;
        }

        Vector3 origin = currentContainer.transform.position;
        float rangeSq = range * range;
        List<(Container Container, float DistanceSq)> areaContainers = new();
        for (int i = Runtime.KnownContainers.Count - 1; i >= 0; i--)
        {
            Container container = Runtime.KnownContainers[i];
            if (container == null || IsUnityNull(container))
            {
                Runtime.KnownContainers.RemoveAt(i);
                continue;
            }

            if (seen.Contains(container))
            {
                continue;
            }

            if (IsAreaContainerAllowed(player, container, currentContainer, origin, rangeSq, out float distanceSq))
            {
                areaContainers.Add((container, distanceSq));
                seen.Add(container);
            }
        }

        areaContainers.Sort((left, right) => left.DistanceSq.CompareTo(right.DistanceSq));
        foreach ((Container container, _) in areaContainers)
        {
            containers.Add(container);
        }

        return containers;
    }

    private static bool CanHandleContainerAction(Player player, Container container)
    {
        return player != null &&
               !player.m_isLoading &&
               container != null &&
               container.m_inventory != null &&
               CanMutateContainerDirectly(container, allowLocalWithoutZNetView: true) &&
               HasContainerPlayerAccess(player, container, flashGuardStone: false);
    }

    private static bool IsAreaContainerAllowed(Player player, Container container, Container? currentContainer, Vector3 origin, float rangeSq, out float distanceSq)
    {
        distanceSq = float.MaxValue;
        if (player == null ||
            container == null ||
            container == currentContainer ||
            container.m_inventory == null ||
            container.m_nview == null ||
            !container.m_nview.IsValid() ||
            !container.m_nview.IsOwner())
        {
            return false;
        }

        distanceSq = (container.transform.position - origin).sqrMagnitude;
        if (distanceSq > rangeSq)
        {
            return false;
        }

        if (container.GetComponent<TombStone>() != null ||
            container.GetComponentInParent<TombStone>() != null ||
            container.m_nview.GetComponent<Player>() != null ||
            container.transform.root.GetComponentInChildren<Ship>() != null)
        {
            return false;
        }

        if (container.m_piece != null && !container.m_piece.IsPlacedByPlayer())
        {
            return false;
        }

        return !IsContainerInUse(container) && HasContainerPlayerAccess(player, container, flashGuardStone: true);
    }

    private static bool HasContainerPlayerAccess(Player player, Container container, bool flashGuardStone)
    {
        if (player == null || container == null)
        {
            return false;
        }

        if (container.m_checkGuardStone && !PrivateArea.CheckAccess(container.transform.position, 0f, flashGuardStone, false))
        {
            return false;
        }

        return container.CheckAccess(player.GetPlayerID());
    }

    private static bool IsContainerInUse(Container container)
    {
        if (container == null)
        {
            return true;
        }

        if (container.IsInUse() || container.m_wagon != null && container.m_wagon.InUse())
        {
            return true;
        }

        ZDO? zdo = container.m_nview != null ? container.m_nview.GetZDO() : null;
        return zdo != null && zdo.GetInt("InUse", 0) == 1;
    }

    private static bool CanMutateContainerDirectly(Container container, bool allowLocalWithoutZNetView = false)
    {
        if (container == null)
        {
            return false;
        }

        if (container.m_nview == null || !container.m_nview.IsValid())
        {
            return allowLocalWithoutZNetView;
        }

        return container.m_nview.IsOwner();
    }

    private static int GetContainerActionSuccessFxMode() =>
        Mathf.Clamp(_containerActionSuccessFxMode != null ? _containerActionSuccessFxMode.Value : 1, 0, ContainerActionSuccessFxMaxMode);

    private static float GetContainerActionSuccessFxVolume() =>
        Mathf.Clamp01(_containerActionSuccessFxVolume != null ? _containerActionSuccessFxVolume.Value : 1f);

    private static int TryPlayChangedContainerActionSuccessFx(Player player, Container container, int mode, int played)
    {
        if (mode < 2 || played >= mode)
        {
            return played;
        }

        PlayContainerActionSuccessFx(player, container);
        return played + 1;
    }

    private static void PlayContainerActionSuccessFx(Player player, Container container)
    {
        if (player == null ||
            IsUnityNull(player) ||
            container == null ||
            IsUnityNull(container) ||
            ZNetScene.instance == null)
        {
            return;
        }

        GameObject? prefab = ZNetScene.instance.GetPrefab(ContainerActionSuccessFxPrefabName);
        if (prefab == null || IsUnityNull(prefab))
        {
            return;
        }

        GameObject instance = UnityEngine.Object.Instantiate(prefab, container.transform.position, container.transform.rotation);
        ApplyContainerActionSuccessFxVolume(instance);
    }

    private static void ApplyContainerActionSuccessFxVolume(GameObject instance)
    {
        float volumeScale = GetContainerActionSuccessFxVolume();
        if (instance == null || IsUnityNull(instance) || volumeScale >= 0.999f)
        {
            return;
        }

        foreach (Component component in instance.GetComponentsInChildren<Component>(includeInactive: true))
        {
            if (component != null && !IsUnityNull(component))
            {
                ScaleSfxComponentVolume(component, volumeScale);
            }
        }
    }

    private static void ScaleSfxComponentVolume(Component component, float volumeScale)
    {
        Type type = component.GetType();
        if (IsUnityAudioSource(type))
        {
            ScaleUnityAudioSourceVolume(component, type, volumeScale);
            return;
        }

        if (!IsLikelySfxComponent(type))
        {
            return;
        }

        SfxVolumeMemberCache members = GetSfxVolumeMemberCache(type);
        foreach (FieldInfo field in members.Fields)
        {
            try
            {
                field.SetValue(component, Mathf.Max(0f, (float)field.GetValue(component) * volumeScale));
            }
            catch
            {
                // Best-effort support for Unity-backed SFX wrappers.
            }
        }

        foreach (PropertyInfo property in members.Properties)
        {
            try
            {
                property.SetValue(component, Mathf.Max(0f, (float)property.GetValue(component) * volumeScale));
            }
            catch
            {
                // Best-effort support for writable volume properties.
            }
        }
    }

    private static SfxVolumeMemberCache GetSfxVolumeMemberCache(Type type)
    {
        if (SfxVolumeMembersByType.TryGetValue(type, out SfxVolumeMemberCache cache))
        {
            return cache;
        }

        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        cache = new SfxVolumeMemberCache(
            type.GetFields(flags)
                .Where(field =>
                    field.FieldType == typeof(float) &&
                    IsSfxVolumeMemberName(field.Name) &&
                    !IsUnsupportedSfxVolumeMemberName(field.Name))
                .ToArray(),
            type.GetProperties(flags)
                .Where(property =>
                    property.PropertyType == typeof(float) &&
                    property.CanRead &&
                    property.CanWrite &&
                    property.GetIndexParameters().Length == 0 &&
                    IsSfxVolumeMemberName(property.Name) &&
                    !IsUnsupportedSfxVolumeMemberName(property.Name))
                .ToArray());
        SfxVolumeMembersByType[type] = cache;
        return cache;
    }

    private static bool IsLikelySfxComponent(Type type)
    {
        string name = type.Name;
        return name.IndexOf("SFX", StringComparison.OrdinalIgnoreCase) >= 0 ||
               name.IndexOf("Audio", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static bool IsSfxVolumeMemberName(string name)
    {
        return name.IndexOf("volume", StringComparison.OrdinalIgnoreCase) >= 0 ||
               name.IndexOf("vol", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static bool IsUnsupportedSfxVolumeMemberName(string name)
    {
        return string.Equals(name, "minVolume", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(name, "maxVolume", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsUnityAudioSource(Type type) =>
        string.Equals(type.FullName, "UnityEngine.AudioSource", StringComparison.Ordinal);

    private static void ScaleUnityAudioSourceVolume(Component component, Type type, float volumeScale)
    {
        PropertyInfo? volumeProperty = type.GetProperty("volume", BindingFlags.Instance | BindingFlags.Public);
        if (volumeProperty == null || !volumeProperty.CanRead || !volumeProperty.CanWrite)
        {
            return;
        }

        try
        {
            volumeProperty.SetValue(component, Mathf.Max(0f, (float)volumeProperty.GetValue(component) * volumeScale));
        }
        catch
        {
            // Best-effort volume scaling for Unity audio sources.
        }
    }

    private static int MoveItemToInventoryTopFirst(Inventory sourceInventory, Inventory targetInventory, ItemData source)
    {
        if (sourceInventory == null || targetInventory == null || source?.m_shared == null || !sourceInventory.m_inventory.Contains(source))
        {
            return 0;
        }

        int movedAmount = 0;
        if (CanStackForTopFirstMove(source))
        {
            List<ItemData> stackTargets = targetInventory.m_inventory
                .Where(target => CanStackIntoTargetForTopFirstMove(target, source))
                .OrderBy(target => target.m_gridPos.y)
                .ThenBy(target => target.m_gridPos.x)
                .ToList();

            foreach (ItemData target in stackTargets)
            {
                if (!sourceInventory.m_inventory.Contains(source) || source.m_stack <= 0)
                {
                    break;
                }

                int amount = Math.Min(target.m_shared.m_maxStackSize - target.m_stack, source.m_stack);
                if (amount <= 0)
                {
                    continue;
                }

                int before = source.m_stack;
                bool movedOk = targetInventory.MoveItemToThis(sourceInventory, source, amount, target.m_gridPos.x, target.m_gridPos.y);
                movedAmount += CountMovedFromContainerSource(sourceInventory, source, before, amount, movedOk);
            }
        }

        foreach (Vector2i slot in GetInventorySlotsTopFirst(targetInventory))
        {
            if (!sourceInventory.m_inventory.Contains(source) || source.m_stack <= 0)
            {
                break;
            }

            if (targetInventory.GetItemAt(slot.x, slot.y) != null)
            {
                continue;
            }

            int before = source.m_stack;
            int requestedAmount = source.m_stack;
            bool movedOk = targetInventory.MoveItemToThis(sourceInventory, source, requestedAmount, slot.x, slot.y);
            int moved = CountMovedFromContainerSource(sourceInventory, source, before, requestedAmount, movedOk);
            movedAmount += moved;
            if (moved == 0)
            {
                break;
            }
        }

        return movedAmount;
    }

    private static bool CanMoveItemToInventoryTopFirst(Inventory sourceInventory, Inventory targetInventory, ItemData source)
    {
        if (sourceInventory == null || targetInventory == null || source?.m_shared == null || !sourceInventory.m_inventory.Contains(source))
        {
            return false;
        }

        if (CanStackForTopFirstMove(source) && targetInventory.m_inventory.Any(target => CanStackIntoTargetForTopFirstMove(target, source)))
        {
            return true;
        }

        return GetInventorySlotsTopFirst(targetInventory).Any(slot => targetInventory.GetItemAt(slot.x, slot.y) == null);
    }

    private static IEnumerable<Vector2i> GetInventorySlotsTopFirst(Inventory inventory)
    {
        int width = Math.Max(1, inventory.GetWidth());
        int height = Math.Max(0, inventory.GetHeight());
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                yield return new Vector2i(x, y);
            }
        }
    }

    private static bool CanStackForTopFirstMove(ItemData item)
    {
        return item?.m_shared != null && item.m_shared.m_maxStackSize > 1 && CanUseContainerActionStacking(item);
    }

    private static bool CanStackIntoTargetForTopFirstMove(ItemData target, ItemData source)
    {
        return target?.m_shared != null &&
               source?.m_shared != null &&
               CanStackForTopFirstMove(target) &&
               CanStackForTopFirstMove(source) &&
               HasSameStackIdentity(target, source) &&
               target.m_stack < target.m_shared.m_maxStackSize;
    }

    private static bool HasSameStackIdentity(ItemData left, ItemData right)
    {
        return left?.m_shared != null &&
               right?.m_shared != null &&
               string.Equals(left.m_shared.m_name, right.m_shared.m_name, StringComparison.OrdinalIgnoreCase) &&
               left.m_quality == right.m_quality &&
               (float)left.m_worldLevel == (float)right.m_worldLevel;
    }

    private static string GetItemPrefabName(ItemData item)
    {
        return RestockTargetLimitCore.CleanPrefabNameForLookup(item?.m_dropPrefab != null ? item.m_dropPrefab.name : "");
    }

    private static int CountMovedFromContainerSource(Inventory sourceInventory, ItemData sourceItem, int before, int requestedAmount, bool moveSucceeded)
    {
        int after = sourceInventory.m_inventory.Contains(sourceItem) ? sourceItem.m_stack : 0;
        int moved = Math.Max(0, before - after);
        return moved == 0 && moveSucceeded ? requestedAmount : moved;
    }

    private static int CompareGridOrder(Vector2i a, Vector2i b)
    {
        int y = a.y.CompareTo(b.y);
        return y != 0 ? y : a.x.CompareTo(b.x);
    }

    private readonly struct SfxVolumeMemberCache
    {
        public SfxVolumeMemberCache(FieldInfo[] fields, PropertyInfo[] properties)
        {
            Fields = fields;
            Properties = properties;
        }

        public FieldInfo[] Fields { get; }
        public PropertyInfo[] Properties { get; }
    }
}
