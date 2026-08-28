using System.Collections.Generic;
using TMPro;
using UnityEngine;
using ItemData = ItemDrop.ItemData;

namespace InventorySlots;

public sealed partial class InventorySlotsPlugin
{
    private static bool IsInventoryPinnedTooltipSlotActive(int slot)
    {
        return slot >= 0 &&
               slot < PinnedTooltips.Inventory.Panels.Length &&
               PinnedTooltips.Inventory.Panels[slot] != null &&
               !IsUnityNull(PinnedTooltips.Inventory.Panels[slot]) &&
               PinnedTooltips.Inventory.Panels[slot]!.gameObject.activeSelf;
    }

    private static void ToggleInventoryItemTooltip(InventoryGui gui, InventoryGrid grid, Vector2i pos, ItemData item)
    {
        if (item?.m_shared == null)
        {
            return;
        }

        SetPinnedTooltipContext(PinnedTooltipContext.InventoryContainer);

        int existingSlot = FindInventoryPinnedTooltipSlot(grid, pos, item);
        if (existingSlot >= 0)
        {
            List<InventoryPinnedTooltipTarget> remaining = GetActiveInventoryPinnedTooltipTargets();
            remaining.RemoveAll(target => IsSameInventoryPinnedTooltipTarget(target, grid, pos, item));
            RebuildInventoryPinnedTooltips(gui, remaining);
            return;
        }

        List<InventoryPinnedTooltipTarget> targets = GetActiveInventoryPinnedTooltipTargets();
        int activeSlots = GetActivePinnedTooltipSlotCount();
        if (targets.Count >= activeSlots)
        {
            targets.Insert(0, new InventoryPinnedTooltipTarget(grid, pos, item));
            targets.RemoveAt(targets.Count - 1);
        }
        else
        {
            targets.Add(new InventoryPinnedTooltipTarget(grid, pos, item));
        }

        RebuildInventoryPinnedTooltips(gui, targets);
    }

    private static bool IsSameInventoryPinnedTooltipTarget(int slot, InventoryGrid grid, Vector2i pos, ItemData item)
    {
        if (slot < 0 || slot >= PinnedTooltips.Inventory.Panels.Length)
        {
            return false;
        }

        return PinnedTooltips.Inventory.Grids[slot] == grid &&
               PinnedTooltips.Inventory.Positions[slot] == pos &&
               (ReferenceEquals(PinnedTooltips.Inventory.Items[slot], item) ||
                PinnedTooltips.Inventory.Items[slot]?.m_shared?.m_name == item.m_shared?.m_name);
    }

    private static bool IsSameInventoryPinnedTooltipTarget(InventoryPinnedTooltipTarget target, InventoryGrid grid, Vector2i pos, ItemData item) =>
        target.Grid == grid &&
        target.Pos == pos &&
        (ReferenceEquals(target.Item, item) || target.Item.m_shared?.m_name == item.m_shared?.m_name);

    private static int FindInventoryPinnedTooltipSlot(InventoryGrid grid, Vector2i pos, ItemData item)
    {
        for (int slot = 0; slot < GetActivePinnedTooltipSlotCount(); slot++)
        {
            RectTransform? panel = PinnedTooltips.Inventory.Panels[slot];
            if (panel != null &&
                !IsUnityNull(panel) &&
                panel.gameObject.activeSelf &&
                IsSameInventoryPinnedTooltipTarget(slot, grid, pos, item))
            {
                return slot;
            }
        }

        return -1;
    }

    private static List<InventoryPinnedTooltipTarget> GetActiveInventoryPinnedTooltipTargets()
    {
        List<InventoryPinnedTooltipTarget> targets = new();
        for (int slot = 0; slot < GetActivePinnedTooltipSlotCount(); slot++)
        {
            RectTransform? panel = PinnedTooltips.Inventory.Panels[slot];
            ItemData? item = PinnedTooltips.Inventory.Items[slot];
            InventoryGrid? grid = PinnedTooltips.Inventory.Grids[slot];
            if (panel != null &&
                !IsUnityNull(panel) &&
                panel.gameObject.activeSelf &&
                grid != null &&
                item?.m_shared != null)
            {
                targets.Add(new InventoryPinnedTooltipTarget(grid, PinnedTooltips.Inventory.Positions[slot], item));
            }
        }

        return targets;
    }

    private static void RebuildInventoryPinnedTooltips(InventoryGui gui, List<InventoryPinnedTooltipTarget> targets)
    {
        HideInventoryPinnedTooltips();
        int activeSlots = GetActivePinnedTooltipSlotCount();
        for (int slot = 0; slot < targets.Count && slot < activeSlots; slot++)
        {
            InventoryPinnedTooltipTarget target = targets[slot];
            PinInventoryItemTooltip(gui, target.Grid, target.Pos, target.Item, slot);
        }

        RefreshInventoryPinnedTooltipBorders();
    }

    private static void PinInventoryItemTooltip(InventoryGui gui, InventoryGrid grid, Vector2i pos, ItemData item, int slot)
    {
        if (gui == null || item?.m_shared == null || slot < 0 || slot >= PinnedTooltips.Inventory.Panels.Length)
        {
            return;
        }

        SetPinnedTooltipContext(PinnedTooltipContext.InventoryContainer);
        HideCraftingPinnedTooltips();
        RectTransform panel = EnsureInventoryPinnedTooltipPanel(gui, slot);
        RectTransform parent = panel.parent as RectTransform ?? ResolveInventoryPinnedTooltipParent(gui);
        Vector2 size = GetPinnedTooltipPanelSize(parent);
        Vector2 position = GetPinnedTooltipPosition(parent, slot, size, InventoryPinnedTooltipFixedOffset);
        SetCenteredRectLayout(panel, position, size);
        ConfigurePinnedTooltipPanelBackground(panel);
        panel.gameObject.SetActive(true);
        RaiseInventoryPinnedTooltips();
        PinnedTooltips.Inventory.Items[slot] = item;
        PinnedTooltips.Inventory.Grids[slot] = grid;
        PinnedTooltips.Inventory.Positions[slot] = pos;
        UpdateInventoryPinnedJewelcraftingTooltip(panel, item, grid);
        SetInventoryPinnedTooltipTextReservedSpace(panel);

        if (PinnedTooltips.Inventory.Icons[slot] != null)
        {
            PinnedTooltips.Inventory.Icons[slot]!.sprite = item.GetIcon();
            PinnedTooltips.Inventory.Icons[slot]!.color = Color.white;
            PinnedTooltips.Inventory.Icons[slot]!.gameObject.SetActive(true);
        }

        if (PinnedTooltips.Inventory.Texts[slot] != null)
        {
            TMP_Text text = PinnedTooltips.Inventory.Texts[slot]!;
            ApplyDefaultFontAsset(text);
            ApplyTooltipSourceFont(text, "Text");
            text.enabled = true;
            text.gameObject.SetActive(true);
            text.color = Color.white;
            text.overflowMode = TextOverflowModes.Overflow;
            text.text = BuildInventoryPinnedTooltipText(item);
            ApplyPinnedTooltipDynamicTextLayout(panel, text, slot, InventoryPinnedTooltipFixedOffset, topReserved: 102f, bottomReserved: 18f);
        }
    }

    private static RectTransform EnsureInventoryPinnedTooltipPanel(InventoryGui gui, int slot)
    {
        RectTransform parent = ResolveInventoryPinnedTooltipParent(gui);
        string name = InventoryPinnedTooltipNamePrefix + slot;
        RectTransform panel = EnsurePinnedTooltipPanel(
            parent,
            name,
            PinnedTooltips.Inventory.Panels[slot],
            () => gui.m_player != null && !IsUnityNull(gui.m_player) && gui.m_player != parent
                ? gui.m_player.Find(name)?.GetComponent<RectTransform>()
                : null);
        PinnedTooltips.Inventory.Icons[slot] = EnsurePinnedTooltipIcon(panel);
        PinnedTooltips.Inventory.Texts[slot] = EnsurePinnedTooltipBodyText(panel, 16f);
        PinnedTooltips.Inventory.JewelcraftingTooltipRoots[slot] = FindJewelcraftingTooltipRoot(panel);

        PinnedTooltips.Inventory.Panels[slot] = panel;
        return panel;
    }

    private static RectTransform ResolveInventoryPinnedTooltipParent(InventoryGui gui)
    {
        if (gui != null &&
            gui.m_inventoryRoot != null &&
            !IsUnityNull(gui.m_inventoryRoot))
        {
            RectTransform? root = gui.m_inventoryRoot as RectTransform ?? gui.m_inventoryRoot.GetComponent<RectTransform>();
            if (root != null && !IsUnityNull(root))
            {
                return root;
            }
        }

        return gui!.m_player!;
    }

    private static void RaiseInventoryPinnedTooltips()
    {
        for (int slot = 0; slot < PinnedTooltips.Inventory.Panels.Length; slot++)
        {
            RectTransform? panel = PinnedTooltips.Inventory.Panels[slot];
            if (panel != null && !IsUnityNull(panel) && panel.gameObject.activeInHierarchy)
            {
                panel.SetAsLastSibling();
            }
        }
    }

    internal static void HideInventoryPinnedTooltips()
    {
        for (int i = 0; i < PinnedTooltips.Inventory.Panels.Length; i++)
        {
            HideInventoryPinnedTooltip(i);
        }

        RefreshInventoryPinnedTooltipBorders();
    }

    private static void HideInventoryPinnedTooltip(int slot)
    {
        if (slot < 0 || slot >= PinnedTooltips.Inventory.Panels.Length)
        {
            return;
        }

        if (PinnedTooltips.Inventory.Panels[slot] != null && !IsUnityNull(PinnedTooltips.Inventory.Panels[slot]))
        {
            ResetPinnedTooltipTextScrollState(PinnedTooltips.Inventory.Panels[slot]!);
            PinnedTooltips.Inventory.Panels[slot]!.gameObject.SetActive(false);
        }

        PinnedTooltips.Inventory.Items[slot] = null;
        PinnedTooltips.Inventory.Grids[slot] = null;
        PinnedTooltips.Inventory.Positions[slot] = new Vector2i(-1, -1);
    }

    private static string BuildInventoryPinnedTooltipText(ItemData item)
    {
        string displayName = LocalizeUi(item.m_shared.m_name, item.m_shared.m_name);
        string tooltip = Localization.instance != null
            ? Localization.instance.Localize(item.GetTooltip())
            : item.GetTooltip();
        return $"<size=28><color=#FFD36A>{displayName}</color></size>\n{tooltip}";
    }
}
