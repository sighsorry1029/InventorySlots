using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace InventorySlots;

public sealed partial class InventorySlotsPlugin
{
    private static void ToggleCraftingRecipeTooltip(InventoryGui gui, int index)
    {
        if (!TryGetCraftingRecipePair(gui, index, out _))
        {
            return;
        }

        PinnedTooltipContext context = GetCurrentCraftingPinnedTooltipContext(gui);
        if (context == PinnedTooltipContext.None)
        {
            return;
        }

        SetPinnedTooltipContext(context);

        int existingSlot = FindCraftingPinnedTooltipSlot(index);
        if (existingSlot >= 0)
        {
            List<int> remaining = GetActiveCraftingPinnedTooltipIndices();
            remaining.RemoveAll(activeIndex => activeIndex == index);
            RebuildCraftingPinnedTooltips(gui, remaining);
            CraftingController.MarkRecipeGridLayoutDirty();
            return;
        }

        List<int> indices = GetActiveCraftingPinnedTooltipIndices();
        int activeSlots = GetActivePinnedTooltipSlotCount();
        if (indices.Count >= activeSlots)
        {
            indices.Insert(0, index);
            indices.RemoveAt(indices.Count - 1);
        }
        else
        {
            indices.Add(index);
        }

        RebuildCraftingPinnedTooltips(gui, indices);
        CraftingController.MarkRecipeGridLayoutDirty();
    }

    private static int FindCraftingPinnedTooltipSlot(int index)
    {
        for (int slot = 0; slot < PinnedTooltips.Crafting.RecipeIndices.Length; slot++)
        {
            RectTransform? panel = PinnedTooltips.Crafting.Panels[slot];
            if (panel != null &&
                !IsUnityNull(panel) &&
                panel.gameObject.activeSelf &&
                PinnedTooltips.Crafting.RecipeIndices[slot] == index)
            {
                return slot;
            }
        }

        return -1;
    }

    private static List<int> GetActiveCraftingPinnedTooltipIndices()
    {
        List<int> indices = new();
        int firstSlot = GetFirstActiveCraftingPinnedTooltipSlot();
        for (int i = PinnedTooltipSlotCount - 1; i >= firstSlot; i--)
        {
            RectTransform? panel = PinnedTooltips.Crafting.Panels[i];
            int index = PinnedTooltips.Crafting.RecipeIndices[i];
            if (panel != null && !IsUnityNull(panel) && panel.gameObject.activeSelf && index >= 0)
            {
                indices.Add(index);
            }
        }

        return indices;
    }

    private static void RebuildCraftingPinnedTooltips(InventoryGui gui, List<int> indices)
    {
        HideCraftingPinnedTooltips();
        int slot = PinnedTooltipSlotCount - 1;
        int firstSlot = GetFirstActiveCraftingPinnedTooltipSlot();
        foreach (int index in indices)
        {
            if (slot < firstSlot)
            {
                break;
            }

            PinCraftingRecipeTooltip(gui, index, slot--);
        }
    }

    private static void PinCraftingRecipeTooltip(InventoryGui gui, int index, int slot)
    {
        if (slot < 0 || slot >= PinnedTooltips.Crafting.Panels.Length || !TryGetCraftingRecipePair(gui, index, out InventoryGui.RecipeDataPair pair))
        {
            return;
        }

        PinnedTooltipContext context = GetCurrentCraftingPinnedTooltipContext(gui);
        if (context == PinnedTooltipContext.None)
        {
            return;
        }

        SetPinnedTooltipContext(context);
        RectTransform panel = EnsureCraftingPinnedTooltipPanel(gui, slot);
        HideInventoryPinnedTooltips();
        RectTransform parent = panel.parent as RectTransform ?? gui.m_crafting;
        Vector2 size = GetPinnedTooltipPanelSize(parent);
        Vector2 position = GetPinnedTooltipPosition(parent, slot, size, CraftingPinnedTooltipFixedOffset);
        SetCenteredRect(parent, panel, position, size);
        ConfigurePinnedTooltipPanelBackground(panel);
        panel.SetAsLastSibling();
        panel.gameObject.SetActive(true);
        PinnedTooltips.Crafting.RecipeIndices[slot] = index;
        bool veiledMasked = IsVeiledRecipeMasked(pair);

        if (PinnedTooltips.Crafting.Icons[slot] != null && pair.Recipe != null && pair.Recipe.m_item != null)
        {
            PinnedTooltips.Crafting.Icons[slot]!.sprite = GetCraftingRecipeIcon(pair);
            PinnedTooltips.Crafting.Icons[slot]!.color = veiledMasked ? Color.black : Color.white;
            PinnedTooltips.Crafting.Icons[slot]!.gameObject.SetActive(true);
        }

        if (PinnedTooltips.Crafting.Texts[slot] != null)
        {
            TMP_Text text = PinnedTooltips.Crafting.Texts[slot]!;
            ApplyDefaultFontAsset(text);
            ApplyTooltipSourceFont(text, "Text");
            text.enabled = true;
            text.gameObject.SetActive(true);
            text.color = Color.white;
            text.overflowMode = TextOverflowModes.Overflow;
            text.text = BuildCraftingPinnedTooltipText(pair);
        }

        bool hasJewelcraftingTooltip = !veiledMasked && UpdateCraftingJewelcraftingTooltip(panel, pair, ref PinnedTooltips.Crafting.JewelcraftingTooltipRoots[slot]);
        bool hasGemRow = false;
        if (hasJewelcraftingTooltip)
        {
            HideCraftingGemIconRow(ref PinnedTooltips.Crafting.GemIconRows[slot]);
        }
        else if (veiledMasked)
        {
            if (PinnedTooltips.Crafting.JewelcraftingTooltipRoots[slot] != null && !IsUnityNull(PinnedTooltips.Crafting.JewelcraftingTooltipRoots[slot]))
            {
                PinnedTooltips.Crafting.JewelcraftingTooltipRoots[slot]!.gameObject.SetActive(false);
            }

            HideCraftingGemIconRow(ref PinnedTooltips.Crafting.GemIconRows[slot]);
        }
        else
        {
            hasGemRow = UpdateCraftingGemIconRow(panel, pair, ref PinnedTooltips.Crafting.GemIconRows[slot], new Vector2(18f, 82f), iconSize: 28f, gap: 6f);
        }

        SetCraftingPinnedTooltipTextReservedSpace(panel, hasGemRow);
        ConfigureCraftingTooltipRecipeRow(panel, pair, new Vector2(18f, 18f), slotSize: 54f, gap: 8f);
        if (PinnedTooltips.Crafting.Texts[slot] != null)
        {
            float bottomReserved = hasGemRow ? 124f : 92f;
            ApplyPinnedTooltipDynamicTextLayout(panel, PinnedTooltips.Crafting.Texts[slot]!, slot, CraftingPinnedTooltipFixedOffset, topReserved: 102f, bottomReserved, maxTextViewportHeight: GetPinnedTooltipMaxTextViewportHeight(panel, 102f, bottomReserved));
        }

        CraftingController.MarkRecipeGridLayoutDirty();
    }

    private static bool IsCraftingRecipeTooltipPinned(int index)
    {
        if (index < 0)
        {
            return false;
        }

        for (int i = 0; i < PinnedTooltips.Crafting.Panels.Length; i++)
        {
            RectTransform? panel = PinnedTooltips.Crafting.Panels[i];
            if (panel != null &&
                !IsUnityNull(panel) &&
                panel.gameObject.activeSelf &&
                PinnedTooltips.Crafting.RecipeIndices[i] == index)
            {
                return true;
            }
        }

        return false;
    }

    private static RectTransform EnsureCraftingPinnedTooltipPanel(InventoryGui gui, int slot)
    {
        RectTransform parent = gui.m_crafting;
        RectTransform panel = EnsurePinnedTooltipPanel(parent, CraftingPinnedTooltipNamePrefix + slot, PinnedTooltips.Crafting.Panels[slot]);
        PinnedTooltips.Crafting.Icons[slot] = EnsurePinnedTooltipIcon(panel);
        PinnedTooltips.Crafting.Texts[slot] = EnsurePinnedTooltipBodyText(panel, 16f);
        PinnedTooltips.Crafting.JewelcraftingTooltipRoots[slot] = panel.Find(InventoryPinnedJewelcraftingTooltipRootName)?.GetComponent<RectTransform>();
        PinnedTooltips.Crafting.GemIconRows[slot] = panel.Find(CraftingGemIconRowName)?.GetComponent<RectTransform>();

        PinnedTooltips.Crafting.Panels[slot] = panel;
        return panel;
    }

    private static void HideCraftingPinnedTooltips()
    {
        for (int i = 0; i < PinnedTooltips.Crafting.Panels.Length; i++)
        {
            HideCraftingPinnedTooltip(i);
        }
    }

    private static void HideCraftingPinnedTooltip(int slot)
    {
        if (slot < 0 || slot >= PinnedTooltips.Crafting.Panels.Length)
        {
            return;
        }

        if (PinnedTooltips.Crafting.Panels[slot] != null && !IsUnityNull(PinnedTooltips.Crafting.Panels[slot]))
        {
            ResetPinnedTooltipTextScrollState(PinnedTooltips.Crafting.Panels[slot]!);
            PinnedTooltips.Crafting.Panels[slot]!.gameObject.SetActive(false);
        }

        if (PinnedTooltips.Crafting.JewelcraftingTooltipRoots[slot] != null && !IsUnityNull(PinnedTooltips.Crafting.JewelcraftingTooltipRoots[slot]))
        {
            PinnedTooltips.Crafting.JewelcraftingTooltipRoots[slot]!.gameObject.SetActive(false);
        }

        if (PinnedTooltips.Crafting.GemIconRows[slot] != null && !IsUnityNull(PinnedTooltips.Crafting.GemIconRows[slot]))
        {
            PinnedTooltips.Crafting.GemIconRows[slot]!.gameObject.SetActive(false);
        }

        PinnedTooltips.Crafting.RecipeIndices[slot] = -1;
        CraftingController.MarkRecipeGridLayoutDirty();
    }

    private static string BuildCraftingPinnedTooltipText(InventoryGui.RecipeDataPair pair)
    {
        string displayName = GetCraftingRecipeDisplayName(pair);
        string itemTooltip = GetCraftingRecipeTooltip(pair);
        return $"<size=28><color=#FFD36A>{displayName}</color></size>\n{itemTooltip}";
    }
}
