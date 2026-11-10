using System;
using System.Globalization;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using ItemData = ItemDrop.ItemData;

namespace InventorySlots;

public sealed partial class InventorySlotsPlugin
{
    private const string PovWalletCell = "purse.0";
    private static bool _povWalletWithdrawal;
    [ThreadStatic] private static Inventory? _walletLoadingInventory;
    private static WalletFrame? _walletFrame;

    internal static void BeginWalletInventoryLoad(Inventory inventory, out Inventory? previous)
    {
        previous = _walletLoadingInventory;
        if (_pov?.HasWallet == true) _walletLoadingInventory = inventory;
    }

    internal static void EndWalletInventoryLoad(Inventory? previous) => _walletLoadingInventory = previous;

    private static bool IsWalletInventoryLoading(Inventory inventory) =>
        ReferenceEquals(_walletLoadingInventory, inventory) || IsInventoryLoadPreserving(inventory);

    private static bool IsPathOfValheimanWalletSlot(SlotDefinition? slot) => slot?.PovCell == PovWalletCell;

    private static bool TryGetWalletPosition(Inventory inventory, out Vector2i position)
    {
        position = new Vector2i(-1, -1);
        return _pov != null && Player.m_localPlayer != null && inventory == Player.m_localPlayer.GetInventory() &&
               PovSlots.TryGetValue(PovWalletCell, out SlotDefinition? slot) && TryGetSlotGridPos(inventory, slot, out position);
    }

    private static bool IsWalletItem(Inventory? inventory, ItemData? item) =>
        item != null && inventory != null && TryGetWalletPosition(inventory, out Vector2i position) &&
        item.m_gridPos == position && ReferenceEquals(inventory.GetItemAt(position.x, position.y), item);

    internal static int WalletWithdrawalAmount(Inventory? inventory, ItemData? item, int requested) =>
        IsWalletWithdrawal(inventory, item) ? Math.Min(Math.Max(0, requested),
            Math.Min(item!.m_stack, Math.Min(ushort.MaxValue, Math.Max(1, item.m_shared.m_maxStackSize)))) : requested;

    private static bool IsWalletWithdrawal(Inventory? inventory, ItemData? item) =>
        IsWalletItem(inventory, item) || _pov?.HasWallet == true && item != null && IsPathOfValheimanCoin(item) &&
        item.m_stack > Math.Min(ushort.MaxValue, Math.Max(1, item.m_shared.m_maxStackSize));

    internal static bool RejectOversizedWalletInsert(Inventory target, ItemData item, int amount) =>
        !IsWalletInventoryLoading(target) && WalletWithdrawalAmount(Player.m_localPlayer?.GetInventory(), item, amount) < amount;

    private static bool ExceedsWalletCoinTotal(Inventory inventory, ItemData item, int amount)
    {
        if (_pov?.HasWallet != true || IsWalletInventoryLoading(inventory) || !TryGetWalletPosition(inventory, out _) ||
            !IsPathOfValheimanCoin(item) || ContainsExactItemReference(inventory, item)) return false;
        long total = amount;
        foreach (ItemData carried in inventory.GetAllItems())
            if (IsPathOfValheimanCoin(carried)) total += carried.m_stack;
        return total > int.MaxValue;
    }

    private static bool WalletStacksMatch(ItemData existing, ItemData incoming)
    {
        using var scope = PathOfValheimanStackCompatibility.Begin(incoming, existing);
        return existing.IsSameType(incoming) && existing.m_variant == incoming.m_variant &&
               existing.m_quality == incoming.m_quality && existing.m_worldLevel == incoming.m_worldLevel &&
               existing.m_cheated == incoming.m_cheated && PathOfValheimanStackCompatibility.MatchesIdentity(existing, incoming) &&
               StackMetadataPolicy.AreCompatible(existing.m_customData, incoming.m_customData);
    }

    private static bool CanDepositWallet(Inventory inventory, ItemData item, int amount, out Vector2i position, out ItemData? existing)
    {
        existing = null;
        position = new Vector2i(-1, -1);
        Player player = Player.m_localPlayer;
        if (_povWalletWithdrawal || _pov?.HasWallet != true || !PovCanMutate(player) ||
            IsWalletInventoryLoading(inventory) || amount <= 0 || !IsPathOfValheimanCoin(item) ||
            !TryGetWalletPosition(inventory, out position) || !_pov.Open(player, PovWalletCell)) return false;
        existing = inventory.GetItemAt(position.x, position.y);
        if (ReferenceEquals(existing, item)) return false;
        if (ExceedsWalletCoinTotal(inventory, item, amount)) return false;
        if (existing == null) return true;
        return existing.m_stack >= 0 && amount <= int.MaxValue - existing.m_stack &&
               WalletStacksMatch(existing, item);
    }

    // Same full-amount predicate is used by capacity queries and actual deposits.
    // A rejected merge leaves both quantities and metadata unchanged.
    private static bool DepositWallet(Inventory inventory, ItemData item, int amount)
    {
        if (amount > item.m_stack || !CanDepositWallet(inventory, item, amount, out Vector2i position, out ItemData? existing) ||
            !CanAddWithinInventoryLimits(inventory, item, amount, out _)) return false;
        ItemData prepared = (existing ?? item).Clone();
        if (existing != null) StackMetadataPolicy.MergeInto(prepared.m_customData, item.m_customData);
        prepared.m_gridPos = position;
        prepared.m_stack = (existing?.m_stack ?? 0) + amount;
        bool contained = ContainsExactItemReference(inventory, item);
        // Prepare clones/composed metadata before the quantity commit. Keep the
        // original wallet reference so trading and crafting see the same item.
        if (existing == null) inventory.m_inventory.Add(prepared);
        else
        {
            existing.m_customData = prepared.m_customData;
            existing.m_stack = prepared.m_stack;
        }
        item.m_stack -= amount;
        if (contained && item.m_stack == 0) inventory.m_inventory.Remove(item);
        try
        {
            inventory.Changed(true, item.m_cheated && !Achievements.IsCheatedAtAll());
            NotifyPathOfValheimanStorageChanged();
        }
        catch (Exception error) { Log.LogWarning("Coin wallet committed; refresh failed: " + error.GetBaseException().Message); }
        return true;
    }

    internal static bool TryWalletAutomaticDeposit(Inventory inventory, ItemData item, ref bool result)
    {
        if (IsWalletInventoryLoading(inventory)) return false;
        if (IsWalletItem(Player.m_localPlayer?.GetInventory(), item))
        {
            // Automatic AddItem callers remove the entire source on success.
            // Only MoveItemToThis's bounded withdrawal may consume a wallet.
            result = false;
            return true;
        }
        if (!ContainsExactItemReference(inventory, item) && DepositWallet(inventory, item, item.m_stack))
        { result = true; return true; }
        if (IsWalletWithdrawal(inventory, item)) { result = false; return true; }
        return false;
    }

    internal static bool TryWalletPositionalDeposit(Inventory inventory, ItemData item, int amount, int x, int y, ref bool result)
    {
        if (IsWalletInventoryLoading(inventory) || !TryGetWalletPosition(inventory, out Vector2i wallet) || wallet != new Vector2i(x, y)) return false;
        result = ReferenceEquals(inventory.GetItemAt(x, y), item) || DepositWallet(inventory, item, Math.Min(amount, item.m_stack));
        return true;
    }

    internal static bool TryWalletGridDrop(InventoryGrid grid, Inventory from, ItemData item, int amount, Vector2i position, ref bool result)
    {
        Inventory target = grid.GetInventory();
        if (TryGetWalletPosition(target, out Vector2i wallet) && wallet == position)
        {
            if (ReferenceEquals(target.GetItemAt(position.x, position.y), item)) { result = true; return true; }
            result = DepositWallet(target, item, Math.Min(amount, item.m_stack));
            if (result && from != target)
            {
                if (item.m_stack == 0) from.RemoveItem(item);
                else from.Changed();
            }
            return true;
        }
        // Never swap a non-coin into the wallet as the reverse half of a drag.
        if (IsWalletItem(from, item) && target.GetItemAt(position.x, position.y) is ItemData occupant &&
            !WalletStacksMatch(occupant, item))
        { result = false; return true; }
        return false;
    }

    internal static bool WithdrawWalletAutomatically(Inventory target, Inventory from, ItemData item)
    {
        if (!IsWalletWithdrawal(from, item) || !ContainsExactItemReference(from, item)) return false;
        int amount = WalletWithdrawalAmount(from, item, item.m_stack);
        bool wasWithdrawing = _povWalletWithdrawal;
        _povWalletWithdrawal = true;
        try
        {
            // One normal stack per operation; preserve remainder and reject if
            // no ordinary cell can receive it. Use the existing patched transfer.
            for (int y = 0; y < target.GetHeight(); y++) for (int x = 0; x < target.GetWidth(); x++)
            {
                Vector2i cell = new(x, y);
                if (target == from && (!CanUseCell(Player.m_localPlayer, target, item, cell) || TryGetSlotAtGridPos(target, cell, out _))) continue;
                ItemData? occupant = target.GetItemAt(x, y);
                if (occupant != null && (occupant.m_stack >= occupant.m_shared.m_maxStackSize || !WalletStacksMatch(occupant, item))) continue;
                int before = item.m_stack;
                target.MoveItemToThis(from, item, amount, x, y);
                if (item.m_stack != before) return true;
            }
        }
        finally { _povWalletWithdrawal = wasWithdrawing; }
        return true;
    }

    // InventorySlots already owns the all-carried-Coins benefit and death escrow.
    // Suppress only these native callbacks for our own active provider/player.
    private static bool RunNativeWalletWeight(Inventory __0) =>
        !HasPathOfValheimanCoinBenefits(Player.m_localPlayer) || __0 != Player.m_localPlayer.GetInventory();

    private static bool RunNativeWalletDeath(Player __0) => !HasPathOfValheimanCoinBenefits(__0);

    private static void PrepareWalletFrame(InventoryGrid grid)
    {
        if (_walletFrame == null) return;
        if (TryGetWalletPosition(grid.GetInventory(), out Vector2i position))
        {
            int index = position.y * grid.GetInventory().GetWidth() + position.x;
            if (index < grid.m_elements.Count && grid.m_elements[index].gameObject == _walletFrame.gameObject) return;
        }
        _walletFrame.Restore();
        _walletFrame = null;
    }

    private static void LayoutPathOfValheimanWallet(InventoryGrid grid, InventoryElement element, Player player,
        Inventory inventory, SlotDefinition slot, Vector3 origin, int rows)
    {
        bool occupied = TryGetSlotGridPos(inventory, slot, out Vector2i cell) && inventory.GetItemAt(cell.x, cell.y) is ItemData;
        bool visible = occupied || IsQuickSlotUnlocked(player, slot);
        element.gameObject.SetActive(visible);
        element.m_used = true;
        if (!visible) return;
        var rect = (RectTransform)element.transform;
        rect.SetParent(grid.m_gridRoot, false);
        float size = Mathf.Clamp(grid.m_elementSpace * .72f, 42f, 58f);
        float scale = size / grid.m_elementSpace;
        rect.localScale = Vector3.one * scale;
        rect.localRotation = Quaternion.identity;
        rect.localPosition = origin + CalculateInventoryBottomButtonPosition(inventory.GetWidth(), rows, grid.m_elementSpace, size, inventory.GetWidth() - 1);
        WalletFrame frame = element.GetComponent<WalletFrame>() ?? element.gameObject.AddComponent<WalletFrame>();
        frame.Configure(element);
        _walletFrame = frame;
        ItemData? coin = inventory.GetItemAt(cell.x, cell.y);
        if (coin == null && ObjectDB.instance != null)
        {
            var prefab = ObjectDB.instance.GetItemPrefab("Coins");
            if (prefab != null) coin = prefab.GetComponent<ItemDrop>()?.m_itemData;
        }
        if (element.m_icon != null && coin != null)
        {
            element.m_icon.sprite = coin.GetIcon();
            element.m_icon.enabled = true;
            element.m_icon.color = occupied ? Color.white : new Color(1f, 1f, 1f, .4f);
        }
        element.m_amount.text = occupied ? inventory.GetItemAt(cell.x, cell.y).m_stack.ToString("N0", CultureInfo.InvariantCulture) : "0";
        element.m_amount.gameObject.SetActive(true);
        element.m_amount.enabled = true;
        element.m_amount.enableAutoSizing = true;
        element.m_amount.fontSizeMin = 11;
        element.m_amount.fontSizeMax = 20;
        if (!occupied && element.m_tooltip != null)
        {
            InventoryHoverTooltipItems.Remove(element.m_tooltip);
            element.m_tooltip.m_topic = Localization.instance.Localize(coin?.m_shared.m_name ?? "$item_coins");
            element.m_tooltip.m_text = "";
        }
        UpdateSlotBindingLabel(element, null);
        HideFavoriteBorder(element);
        UpdateInventoryPinnedTooltipBorder(grid, element, cell);
    }

    // Owned by the native cell; destruction/recreation follows InventoryGrid.
    private sealed class WalletFrame : MonoBehaviour
    {
        private Image? _frame;
        private InventoryElement? _element;
        private float _min, _max;
        private bool _auto;
        internal void Configure(InventoryElement element)
        {
            if (_frame != null) { _frame.gameObject.SetActive(true); return; }
            _element = element;
            _min = element.m_amount.fontSizeMin; _max = element.m_amount.fontSizeMax;
            _auto = element.m_amount.enableAutoSizing;
            var source = InventoryGui.instance?.m_takeAllButton?.image;
            if (source == null) return;
            var go = new GameObject("WalletFrame", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(element.transform, false);
            go.transform.SetAsFirstSibling();
            _frame = go.GetComponent<Image>();
            _frame.sprite = source.sprite;
            _frame.type = source.type;
            _frame.color = source.color;
            _frame.raycastTarget = false;
            var rect = (RectTransform)go.transform;
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }
        internal void Restore()
        {
            if (_frame != null) _frame.gameObject.SetActive(false);
            if (_element == null) return;
            _element.m_amount.fontSizeMin = _min; _element.m_amount.fontSizeMax = _max;
            _element.m_amount.enableAutoSizing = _auto;
            _element.m_icon.color = Color.white;
        }
    }

    private static bool NavigateWallet(InventoryGui gui, InventoryGrid grid, Vector2i cell, int rows,
        bool up, bool down, bool left, bool right)
    {
        if (grid != gui.m_playerGrid || !TryGetWalletPosition(grid.GetInventory(), out Vector2i wallet)) return false;
        int index = wallet.y * grid.GetInventory().GetWidth() + wallet.x;
        if (index >= grid.m_elements.Count || !grid.m_elements[index].gameObject.activeInHierarchy) return false;
        if (cell != wallet)
        {
            if (cell.x != 0 || cell.y != rows - 1 || !down || up || left || right) return false;
            grid.SetGamepadSelection(wallet);
            grid.m_elements[index].m_button.Select();
        }
        else
        {
            if (!up && !down && !left && !right) return false;
            if (up)
            {
                grid.SetGamepadSelection(new Vector2i(0, rows - 1));
                grid.m_elements[(rows - 1) * grid.GetInventory().GetWidth()].m_button.Select();
            }
            else if (down && HasControllerContainer(gui)) FocusControllerInventoryGrid(gui, new Vector2i(0, 0), true);
            else if (right && FindControllerButton(0, grid.GetInventory().GetWidth()) is InventorySlideButton button)
            {
                _controllerButtonOwner = gui;
                _controllerButtonReturnCell = new Vector2i(0, rows - 1);
                _controllerButtonFocus = button;
                _controllerButtonWaitForVerticalRelease = true;
                FocusControllerInventoryButtonRow(gui);
                ShowControllerButtonFocus(button);
            }
        }
        _controllerButtonFrame = _controllerReservedFrame = Time.frameCount;
        return true;
    }

    internal static InventoryElement? GetWalletUnderPointer(InventoryGrid grid, Vector2 pointer)
    {
        if (!TryGetWalletPosition(grid.GetInventory(), out Vector2i position)) return null;
        int index = position.y * grid.GetInventory().GetWidth() + position.x;
        if (index < 0 || index >= grid.m_elements.Count) return null;
        InventoryElement element = grid.m_elements[index];
        return element.gameObject.activeInHierarchy && RectTransformUtility.RectangleContainsScreenPoint(
            element.GetElementRectTransform(), pointer) ? element : null;
    }
}

[HarmonyPatch]
internal static class WalletInventoryLoadPatch
{
    private static System.Collections.Generic.IEnumerable<System.Reflection.MethodBase> TargetMethods()
    {
        yield return AccessTools.DeclaredMethod(typeof(Inventory), nameof(Inventory.Load), new[] { typeof(ZPackage) });
        yield return AccessTools.DeclaredMethod(typeof(Inventory), nameof(Inventory.Load), new[] { typeof(ZPackage), typeof(bool) });
    }

    // Restoring saved quantities is not a withdrawal. Include chests/tombstones,
    // which intentionally do not use the player's slot-preservation scope.
    private static void Prefix(Inventory __instance, out Inventory? __state) =>
        InventorySlotsPlugin.BeginWalletInventoryLoad(__instance, out __state);

    private static void Finalizer(Inventory? __state) => InventorySlotsPlugin.EndWalletInventoryLoad(__state);
}

[HarmonyPatch(typeof(Inventory), nameof(Inventory.MoveItemToThis), typeof(Inventory), typeof(ItemData))]
internal static class WalletAutomaticWithdrawalPatch
{
    private static bool Prefix(Inventory __instance, Inventory fromInventory, ItemData item) =>
        !InventorySlotsPlugin.WithdrawWalletAutomatically(__instance, fromInventory, item);
}

[HarmonyPatch(typeof(InventoryGui), "SetupDragItem")]
internal static class WalletDragAmountPatch
{
    private static void Prefix(ItemData item, Inventory inventory, ref int amount) =>
        amount = InventorySlotsPlugin.WalletWithdrawalAmount(inventory, item, amount);
}

[HarmonyPatch(typeof(InventoryGui), "ShowSplitDialog")]
internal static class WalletSplitAmountPatch
{
    private static void Postfix(InventoryGui __instance, ItemData item, Inventory fromIventory)
    {
        int amount = InventorySlotsPlugin.WalletWithdrawalAmount(fromIventory, item, item.m_stack);
        if (amount != item.m_stack) __instance.m_splitDialog.UpdateLimits(amount, false);
    }
}

[HarmonyPatch(typeof(Humanoid), nameof(Humanoid.DropItem), typeof(Inventory), typeof(ItemData), typeof(int))]
internal static class WalletWorldDropAmountPatch
{
    private static void Prefix(Inventory inventory, ItemData item, ref int amount) =>
        amount = InventorySlotsPlugin.WalletWithdrawalAmount(inventory, item, amount < 0 ? item.m_stack : amount);
}

[HarmonyPatch(typeof(InventoryGrid), "GetHoveredElement")]
internal static class WalletHoveredElementPatch
{
    private static bool Prefix(InventoryGrid __instance, ref InventoryElement __result)
    {
        InventoryElement? wallet = InventorySlotsPlugin.GetWalletUnderPointer(__instance, ZInput.pointerPosition);
        if (wallet == null) return true;
        __result = wallet;
        return false;
    }
}

[HarmonyPatch(typeof(InventoryGrid), nameof(InventoryGrid.GetItem))]
internal static class WalletHoveredItemPatch
{
    private static bool Prefix(InventoryGrid __instance, Vector2i cursorPosition, ref ItemData __result)
    {
        InventoryElement? wallet = InventorySlotsPlugin.GetWalletUnderPointer(__instance, cursorPosition.ToVector2());
        if (wallet == null) return true;
        int index = __instance.m_elements.IndexOf(wallet), width = __instance.GetInventory().GetWidth();
        __result = __instance.GetInventory().GetItemAt(index % width, index / width);
        return false;
    }
}
