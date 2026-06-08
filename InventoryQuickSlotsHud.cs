using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BepInEx;
using BepInEx.Configuration;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using ItemData = ItemDrop.ItemData;

namespace InventorySlots;

public sealed partial class InventorySlotsPlugin
{
    private static void UpdateQuickSlotsHud(Player player)
    {
        if (GetQuickSlotCount() <= 0 || Hud.instance == null || Hud.instance.m_rootObject == null)
        {
            ClearQuickSlotsHud();
            return;
        }

        List<SlotDefinition> quickSlots = GetQuickPanelSlots(player);
        if (quickSlots.Count == 0)
        {
            ClearQuickSlotsHud();
            return;
        }

        HotkeyBar? hotkeyBar = EnsureQuickSlotsHotkeyBar();
        if (hotkeyBar == null || InventoryPanels.QuickSlotsHotkeyBarRect == null)
        {
            return;
        }

        float elementSpace = GetQuickSlotHudElementSpace();
        int panelRows = GetQuickPanelRows(quickSlots.Count);
        InventoryPanels.QuickSlotsHotkeyBarRect.localPosition = GetQuickSlotHudPosition();
        InventoryPanels.QuickSlotsHotkeyBarRect.sizeDelta = new Vector2(QuickSlotPanelColumns * elementSpace, panelRows * elementSpace);
        hotkeyBar.m_elementSpace = elementSpace;
        EnsureQuickSlotsHotkeyBarElementCount(hotkeyBar, quickSlots.Count);

        Inventory inventory = ((Humanoid)player).GetInventory();
        for (int i = 0; i < quickSlots.Count; i++)
        {
            SlotDefinition slot = quickSlots[i];
            Vector2i gridPos = GetSlotGridPos(inventory, slot);
            ItemData? item = inventory.GetItemAt(gridPos.x, gridPos.y);
            UpdateQuickSlotsHotkeyBarElement(player, hotkeyBar.m_elements[i], i, slot, item, elementSpace);
        }
    }

    private static HotkeyBar? EnsureQuickSlotsHotkeyBar()
    {
        Transform? root = Hud.instance != null && Hud.instance.m_rootObject != null ? Hud.instance.m_rootObject.transform : null;
        if (root == null)
        {
            return null;
        }

        if (!IsUnityNull(InventoryPanels.QuickSlotsHotkeyBarRect) && InventoryPanels.QuickSlotsHotkeyBarRect!.parent == root)
        {
            InventoryPanels.QuickSlotsHotkeyBar = InventoryPanels.QuickSlotsHotkeyBarRect.GetComponent<HotkeyBar>();
            ConfigureQuickSlotsHotkeyBarRect(InventoryPanels.QuickSlotsHotkeyBarRect);
            InventoryPanels.QuickSlotsHotkeyBarRect.gameObject.SetActive(true);
            return InventoryPanels.QuickSlotsHotkeyBar;
        }

        Transform? existing = root.Find(QuickSlotsHotkeyBarName);
        InventoryPanels.QuickSlotsHotkeyBarRect = existing != null ? existing.GetComponent<RectTransform>() : null;
        if (InventoryPanels.QuickSlotsHotkeyBarRect == null)
        {
            Transform? vanillaBar = root.Find("HotKeyBar");
            RectTransform? vanillaRect = vanillaBar != null ? vanillaBar.GetComponent<RectTransform>() : null;
            if (vanillaRect == null)
            {
                return null;
            }

            InventoryPanels.QuickSlotsHotkeyBarRect = UnityEngine.Object.Instantiate(vanillaRect, root, false);
            InventoryPanels.QuickSlotsHotkeyBarRect.name = QuickSlotsHotkeyBarName;
            InventoryPanels.QuickSlotsHotkeyBarRect.SetSiblingIndex(vanillaRect.GetSiblingIndex() + 1);
        }

        InventoryPanels.QuickSlotsHotkeyBar = InventoryPanels.QuickSlotsHotkeyBarRect.GetComponent<HotkeyBar>();
        if (InventoryPanels.QuickSlotsHotkeyBar == null)
        {
            return null;
        }

        InventoryPanels.QuickSlotsHotkeyBar.enabled = false;
        InventoryPanels.QuickSlotsHotkeyBar.m_selected = -1;
        ClearQuickSlotsHotkeyBarChildren(InventoryPanels.QuickSlotsHotkeyBar);
        ConfigureQuickSlotsHotkeyBarRect(InventoryPanels.QuickSlotsHotkeyBarRect);
        InventoryPanels.QuickSlotsHotkeyBarRect.gameObject.SetActive(true);
        return InventoryPanels.QuickSlotsHotkeyBar;
    }

    private static void ConfigureQuickSlotsHotkeyBarRect(RectTransform rect)
    {
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.localScale = Vector3.one;
        rect.localRotation = Quaternion.identity;
        rect.localPosition = GetQuickSlotHudPosition();
    }

    private static void ClearQuickSlotsHotkeyBarChildren(HotkeyBar hotkeyBar)
    {
        foreach (HotkeyBar.ElementData element in hotkeyBar.m_elements)
        {
            if (!IsUnityNull(element.m_go))
            {
                UnityEngine.Object.Destroy(element.m_go);
            }
        }

        hotkeyBar.m_elements.Clear();
        hotkeyBar.m_items.Clear();

        RectTransform rect = (RectTransform)hotkeyBar.transform;
        GameObject? prefab = hotkeyBar.m_elementPrefab;
        for (int i = rect.childCount - 1; i >= 0; i--)
        {
            GameObject child = rect.GetChild(i).gameObject;
            if (child == prefab)
            {
                child.SetActive(false);
                continue;
            }

            child.SetActive(false);
            UnityEngine.Object.Destroy(child);
        }
    }

    private static void EnsureQuickSlotsHotkeyBarElementCount(HotkeyBar hotkeyBar, int count)
    {
        while (hotkeyBar.m_elements.Count > count)
        {
            HotkeyBar.ElementData last = hotkeyBar.m_elements[hotkeyBar.m_elements.Count - 1];
            if (!IsUnityNull(last.m_go))
            {
                UnityEngine.Object.Destroy(last.m_go);
            }

            hotkeyBar.m_elements.RemoveAt(hotkeyBar.m_elements.Count - 1);
        }

        while (hotkeyBar.m_elements.Count < count)
        {
            HotkeyBar.ElementData element = new();
            element.m_go = CreateCleanQuickHudElement(hotkeyBar.transform, hotkeyBar.m_elements.Count);
            element.m_icon = element.m_go.transform.Find("icon")?.GetComponent<Image>()!;
            element.m_durability = element.m_go.transform.Find("durability")?.GetComponent<GuiBar>()!;
            element.m_amount = element.m_go.transform.Find("amount")?.GetComponent<TMP_Text>()!;
            element.m_equiped = element.m_go.transform.Find("equiped")?.gameObject!;
            element.m_queued = element.m_go.transform.Find("queued")?.gameObject!;
            element.m_selection = element.m_go.transform.Find("selected")?.gameObject!;
            element.m_used = false;
            hotkeyBar.m_elements.Add(element);

            QuickHudSlotMarker marker = element.m_go.GetComponent<QuickHudSlotMarker>() ?? element.m_go.AddComponent<QuickHudSlotMarker>();
            marker.Index = hotkeyBar.m_elements.Count - 1;
            UIInputHandler input = element.m_go.GetComponent<UIInputHandler>() ?? element.m_go.AddComponent<UIInputHandler>();
            input.m_onLeftClick += handler => SelectQuickHudSlot(handler.GetComponentInParent<QuickHudSlotMarker>());
            input.m_onRightClick += handler => UseQuickHudSlot(handler.GetComponentInParent<QuickHudSlotMarker>());
        }
    }

    private static GameObject CreateCleanQuickHudElement(Transform parent, int index)
    {
        GameObject go = new($"InventorySlots_QuickHotkeySlot{index + 1}", typeof(RectTransform), typeof(Image), typeof(UIInputHandler), typeof(UITooltip), typeof(QuickHudSlotMarker));
        go.transform.SetParent(parent, false);

        Image background = go.GetComponent<Image>();
        background.sprite = GetSolidUiSprite();
        background.color = new Color(0.055f, 0.035f, 0.025f, QuickSlotsHudSlotBackgroundAlpha);
        background.raycastTarget = true;

        CreateTopLeftImageChild("equiped", go.transform, new Color(0.42f, 0.68f, 0.92f, 0.34f), active: false);
        CreateTopLeftImageChild("queued", go.transform, new Color(1f, 0.68f, 0.18f, 0.28f), active: false);
        CreateTopLeftImageChild("selected", go.transform, new Color(1f, 1f, 1f, 0.16f), active: false);

        RectTransform icon = CreateTopLeftImageChild("icon", go.transform, Color.white, active: false);
        icon.GetComponent<Image>().raycastTarget = false;

        RectTransform durability = CreateTopLeftImageChild("durability", go.transform, new Color(0f, 0f, 0f, 0.7f), active: false);
        RectTransform fill = CreateTopLeftImageChild("fill", durability, Color.white, active: true);
        fill.anchorMin = Vector2.zero;
        fill.anchorMax = new Vector2(1f, 1f);
        fill.pivot = new Vector2(0f, 0.5f);
        fill.offsetMin = Vector2.zero;
        fill.offsetMax = Vector2.zero;
        QuickHudSlotMarker marker = go.GetComponent<QuickHudSlotMarker>();
        marker.DurabilityObject = durability.gameObject;
        marker.DurabilityFill = fill.GetComponent<Image>();

        CreateTextRect("amount", go.transform, out TMP_Text amount);
        amount.raycastTarget = false;
        amount.alignment = TextAlignmentOptions.Bottom;
        amount.textWrappingMode = TextWrappingModes.NoWrap;

        CreateTextRect("binding", go.transform, out TMP_Text binding);
        binding.raycastTarget = false;
        binding.alignment = TextAlignmentOptions.TopLeft;
        binding.textWrappingMode = TextWrappingModes.NoWrap;
        marker.BindingText = binding;

        return go;
    }

    private static void UpdateQuickSlotsHotkeyBarElement(Player player, HotkeyBar.ElementData element, int index, SlotDefinition slot, ItemData? item, float elementSpace)
    {
        if (IsUnityNull(element.m_go))
        {
            return;
        }

        element.m_go.transform.localPosition = GetQuickSlotPanelElementPosition(index, elementSpace);
        element.m_go.transform.localScale = Vector3.one;
        element.m_go.SetActive(true);
        element.m_used = item != null;

        QuickHudSlotMarker marker = element.m_go.GetComponent<QuickHudSlotMarker>() ?? element.m_go.AddComponent<QuickHudSlotMarker>();
        marker.Index = index;
        ConfigureQuickHudElementLayout(element, marker, elementSpace);

        ConfigureQuickSlotsHotkeyBarElementBackground(element);
        UpdateQuickSlotsHotkeyBarBinding(element, marker, slot);

        element.m_selection?.SetActive(false);
        element.m_equiped?.SetActive(false);
        element.m_queued?.SetActive(false);

        if (item == null)
        {
            element.m_icon?.gameObject.SetActive(false);
            SetQuickHudDurability(element, marker, null);
            element.m_amount?.gameObject.SetActive(false);
            marker.TooltipHash = 0;
            SetQuickHotkeyBarTooltip(element, marker, $"empty|{slot.Id}|{slot.Name}", slot.Name, "");
            return;
        }

        element.m_icon?.gameObject.SetActive(true);
        if (element.m_icon != null)
        {
            element.m_icon.sprite = item.GetIcon();
            element.m_icon.color = Color.white;
        }

        SetQuickHudDurability(element, marker, item);

        bool showAmount = item.m_shared.m_maxStackSize > 1;
        element.m_amount?.gameObject.SetActive(showAmount);
        if (showAmount && element.m_amount != null)
        {
            element.m_amount.text = $"{item.m_stack}/{item.m_shared.m_maxStackSize}";
        }

        element.m_equiped?.SetActive(IsQuickSlotHudItemEquipped(player, item));
        int tooltipHash = GetQuickHudTooltipHash(item);
        if (marker.TooltipHash != tooltipHash)
        {
            marker.TooltipHash = tooltipHash;
            SetQuickHotkeyBarTooltip(element, marker, "item|" + tooltipHash.ToString("X8"), item.m_shared.m_name, item.GetTooltip());
        }
    }

    private static void ConfigureQuickHudElementLayout(HotkeyBar.ElementData element, QuickHudSlotMarker marker, float elementSpace)
    {
        if (element.m_go.transform is not RectTransform root)
        {
            return;
        }

        float cellSize = Mathf.Max(24f, elementSpace - 8f);
        string signature = cellSize.ToString("0.###");
        if (string.Equals(marker.LayoutSignature, signature, StringComparison.Ordinal))
        {
            return;
        }

        root.anchorMin = new Vector2(0f, 1f);
        root.anchorMax = new Vector2(0f, 1f);
        root.pivot = new Vector2(0f, 1f);
        root.sizeDelta = new Vector2(cellSize, cellSize);
        root.localRotation = Quaternion.identity;

        ConfigureQuickHudFullCellChild(root.Find("equiped"), cellSize);
        ConfigureQuickHudFullCellChild(root.Find("queued"), cellSize);
        ConfigureQuickHudFullCellChild(root.Find("selected"), cellSize);

        if (root.Find("icon") is RectTransform icon)
        {
            float padding = Mathf.Clamp(cellSize * 0.08f, 4f, 9f);
            icon.anchorMin = new Vector2(0f, 1f);
            icon.anchorMax = new Vector2(0f, 1f);
            icon.pivot = new Vector2(0f, 1f);
            icon.anchoredPosition = new Vector2(padding, -padding);
            icon.sizeDelta = new Vector2(Mathf.Max(1f, cellSize - padding * 2f), Mathf.Max(1f, cellSize - padding * 2f));
            icon.localScale = Vector3.one;
            icon.localRotation = Quaternion.identity;
        }

        if (root.Find("durability") is RectTransform durability)
        {
            float margin = Mathf.Clamp(cellSize * 0.08f, 4f, 8f);
            durability.anchorMin = new Vector2(0f, 0f);
            durability.anchorMax = new Vector2(1f, 0f);
            durability.pivot = new Vector2(0.5f, 0f);
            durability.offsetMin = new Vector2(margin, margin);
            durability.offsetMax = new Vector2(-margin, margin + Mathf.Clamp(cellSize * 0.08f, 4f, 7f));
            durability.localScale = Vector3.one;
            durability.localRotation = Quaternion.identity;
        }

        ConfigureQuickHudText(root.Find("amount")?.GetComponent<TMP_Text>(), cellSize, TextAlignmentOptions.Bottom, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 1f));
        ConfigureQuickHudText(root.Find("binding")?.GetComponent<TMP_Text>(), cellSize, TextAlignmentOptions.TopLeft, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(2f, -2f));
        marker.LayoutSignature = signature;
    }

    private static void ConfigureQuickHudFullCellChild(Transform? child, float cellSize)
    {
        if (child is not RectTransform rect)
        {
            return;
        }

        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(cellSize, cellSize);
        rect.localScale = Vector3.one;
        rect.localRotation = Quaternion.identity;
    }

    private static void ConfigureQuickHudText(TMP_Text? text, float cellSize, TextAlignmentOptions alignment, Vector2 anchor, Vector2 pivot, Vector2 anchoredPosition)
    {
        if (text == null)
        {
            return;
        }

        ApplyDefaultFontAsset(text);
        text.alignment = alignment;
        text.enableAutoSizing = true;
        text.fontSizeMin = 8f;
        text.fontSizeMax = Mathf.Clamp(cellSize * 0.24f, 12f, 16f);
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Overflow;
        text.raycastTarget = false;
        RectTransform rect = text.rectTransform;
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = pivot;
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = new Vector2(cellSize, Mathf.Clamp(cellSize * 0.32f, 18f, 26f));
        rect.localScale = Vector3.one;
        rect.localRotation = Quaternion.identity;
    }

    private static void SetQuickHudDurability(HotkeyBar.ElementData element, QuickHudSlotMarker marker, ItemData? item)
    {
        GameObject? durabilityGo = marker.DurabilityObject;
        if (durabilityGo == null || IsUnityNull(durabilityGo))
        {
            Transform? durabilityTransform = element.m_go.transform.Find("durability");
            durabilityGo = durabilityTransform != null ? durabilityTransform.gameObject : null;
            marker.DurabilityObject = durabilityGo;
        }

        bool showDurability = item != null && item.m_shared.m_useDurability && item.m_durability < item.GetMaxDurability();
        durabilityGo?.SetActive(showDurability);
        if (!showDurability || item == null)
        {
            return;
        }

        if (element.m_durability != null && !IsUnityNull(element.m_durability))
        {
            if (item.m_durability <= 0f)
            {
                element.m_durability.SetValue(1f);
                element.m_durability.SetColor(Mathf.Sin(Time.time * 10f) > 0f ? Color.red : Color.clear);
            }
            else
            {
                element.m_durability.SetValue(item.GetDurabilityPercentage());
                element.m_durability.ResetColor();
            }

            return;
        }

        Image? fill = marker.DurabilityFill;
        if (fill == null || IsUnityNull(fill))
        {
            return;
        }

        float value = item.m_durability <= 0f ? 1f : Mathf.Clamp01(item.GetDurabilityPercentage());
        Color color = item.m_durability <= 0f && Mathf.Sin(Time.time * 10f) <= 0f ? Color.clear : Color.white;
        fill.color = color;
        RectTransform fillRect = fill.rectTransform;
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = new Vector2(value, 1f);
        fillRect.offsetMin = Vector2.zero;
        fillRect.offsetMax = Vector2.zero;
    }

    private static bool IsQuickSlotHudItemEquipped(Player player, ItemData item)
    {
        try
        {
            return ((Humanoid)player).IsItemEquiped(item) || item.m_equipped;
        }
        catch
        {
            return item.m_equipped;
        }
    }

    private static void ConfigureQuickSlotsHotkeyBarElementBackground(HotkeyBar.ElementData element)
    {
        Image image = element.m_go.GetComponent<Image>();
        if (image == null)
        {
            return;
        }

        Color color = image.color;
        color.a = QuickSlotsHudSlotBackgroundAlpha;
        image.color = color;
        image.raycastTarget = true;
    }

    private static void UpdateQuickSlotsHotkeyBarBinding(HotkeyBar.ElementData element, QuickHudSlotMarker marker, SlotDefinition slot)
    {
        TMP_Text? binding = marker.BindingText != null && !IsUnityNull(marker.BindingText)
            ? marker.BindingText
            : null;
        if (binding == null)
        {
            Transform? bindingTransform = element.m_go.transform.Find("binding");
            binding = bindingTransform != null ? bindingTransform.GetComponent<TMP_Text>() : null;
            marker.BindingText = binding;
        }

        if (binding == null)
        {
            return;
        }

        string text = GetQuickSlotBindingText(slot);
        string signature = $"{slot.Id}|{text}";
        if (string.Equals(marker.BindingSignature, signature, StringComparison.Ordinal))
        {
            return;
        }

        ApplyDefaultFontAsset(binding);
        binding.text = text;
        binding.enabled = true;
        binding.enableAutoSizing = true;
        binding.fontSizeMin = 12f;
        binding.fontSizeMax = 16f;
        binding.alignment = TextAlignmentOptions.TopLeft;
        binding.textWrappingMode = TextWrappingModes.NoWrap;
        binding.overflowMode = TextOverflowModes.Overflow;
        binding.color = new Color(0.68f, 0.88f, 1f, 1f);
        marker.BindingSignature = signature;
    }

    private static void SetQuickHotkeyBarTooltip(HotkeyBar.ElementData element, QuickHudSlotMarker marker, string signature, string topic, string text)
    {
        if (string.Equals(marker.TooltipSignature, signature, StringComparison.Ordinal) &&
            string.Equals(marker.TooltipTopic, topic, StringComparison.Ordinal) &&
            string.Equals(marker.TooltipText, text ?? "", StringComparison.Ordinal))
        {
            return;
        }

        UITooltip tooltip = element.m_go.GetComponent<UITooltip>() ?? element.m_go.AddComponent<UITooltip>();
        EnsureTooltipPrefab(tooltip);
        tooltip.m_topic = topic;
        tooltip.m_text = text ?? "";
        marker.TooltipSignature = signature;
        marker.TooltipTopic = topic;
        marker.TooltipText = text ?? "";
    }

    private static int GetQuickHudTooltipHash(ItemData item)
    {
        if (item?.m_shared == null)
        {
            return 0;
        }

        unchecked
        {
            int hash = 17;
            hash = hash * 31 + StringComparer.Ordinal.GetHashCode(GetItemPrefabName(item));
            hash = hash * 31 + StringComparer.Ordinal.GetHashCode(item.m_shared.m_name ?? "");
            hash = hash * 31 + item.m_quality;
            hash = hash * 31 + item.m_variant;
            hash = hash * 31 + item.m_stack;
            hash = hash * 31 + Mathf.RoundToInt(item.m_durability * 1000f);
            hash = hash * 31 + (item.m_equipped ? 1 : 0);
            if (item.m_customData != null)
            {
                foreach (KeyValuePair<string, string> pair in item.m_customData)
                {
                    hash = hash * 31 + StringComparer.Ordinal.GetHashCode(pair.Key ?? "");
                    hash = hash * 31 + StringComparer.Ordinal.GetHashCode(pair.Value ?? "");
                }
            }

            return hash;
        }
    }

    private static float GetQuickSlotHudElementSpace()
    {
        if (InventoryPanels.QuickSlotHudAnchorValid)
        {
            return Mathf.Max(1f, InventoryPanels.QuickSlotHudElementSpace);
        }

        if (InventoryGui.instance != null && InventoryGui.instance.m_playerGrid != null)
        {
            return Mathf.Max(1f, InventoryGui.instance.m_playerGrid.m_elementSpace);
        }

        return GetHudElementSpace();
    }

    private static Vector3 GetQuickSlotHudPosition()
    {
        return InventoryPanels.QuickSlotHudAnchorValid ? InventoryPanels.QuickSlotHudAnchoredPosition : (Vector3)QuickSlotsHudFallbackPosition;
    }

    private static float GetHudElementSpace()
    {
        Transform? hotKeyBarTransform = Hud.instance != null && Hud.instance.m_rootObject != null ? Hud.instance.m_rootObject.transform.Find("HotKeyBar") : null;
        HotkeyBar hotKeyBar = hotKeyBarTransform != null ? hotKeyBarTransform.GetComponent<HotkeyBar>() : null!;
        return hotKeyBar != null ? hotKeyBar.m_elementSpace : 70f;
    }

    internal static void EnsureTooltipPrefab(UITooltip? tooltip)
    {
        if (tooltip == null || tooltip.m_tooltipPrefab != null)
        {
            return;
        }

        UITooltip? source = FindTooltipPrefabSource();
        if (source != null && source.m_tooltipPrefab != null)
        {
            tooltip.m_tooltipPrefab = source.m_tooltipPrefab;
        }
    }

    private static UITooltip? FindTooltipPrefabSource()
    {
        InventoryGui? gui = InventoryGui.instance;
        UITooltip? source = gui?.m_playerGrid?.m_elementPrefab != null ? gui.m_playerGrid.m_elementPrefab.GetComponent<UITooltip>() : null;
        if (source?.m_tooltipPrefab != null)
        {
            return source;
        }

        if (gui?.m_playerGrid?.m_elements != null)
        {
            foreach (InventoryGrid.Element element in gui.m_playerGrid.m_elements)
            {
                source = !IsUnityNull(element?.m_go) ? element!.m_go.GetComponent<UITooltip>() : null;
                if (source?.m_tooltipPrefab != null)
                {
                    return source;
                }
            }
        }

        Transform? hotKeyBarTransform = Hud.instance != null && Hud.instance.m_rootObject != null ? Hud.instance.m_rootObject.transform.Find("HotKeyBar") : null;
        HotkeyBar hotKeyBar = hotKeyBarTransform != null ? hotKeyBarTransform.GetComponent<HotkeyBar>() : null!;
        source = hotKeyBar?.m_elementPrefab != null ? hotKeyBar.m_elementPrefab.GetComponent<UITooltip>() : null;
        if (source?.m_tooltipPrefab != null)
        {
            return source;
        }

        if (hotKeyBar?.m_elements != null)
        {
            foreach (HotkeyBar.ElementData element in hotKeyBar.m_elements)
            {
                source = element?.m_go != null ? element.m_go.GetComponent<UITooltip>() : null;
                if (source?.m_tooltipPrefab != null)
                {
                    return source;
                }
            }
        }

        return null;
    }

    private static void SelectQuickHudSlot(QuickHudSlotMarker? marker)
    {
        if (marker == null || !InventoryGui.IsVisible() || InventoryGui.instance == null || InventoryGui.instance.m_playerGrid == null || Player.m_localPlayer == null)
        {
            return;
        }

        Inventory inventory = ((Humanoid)Player.m_localPlayer).GetInventory();
        if (!TryGetQuickSlotDefinition(marker.Index, out SlotDefinition? slot))
        {
            return;
        }

        Vector2i pos = GetSlotGridPos(inventory, slot!);
        ItemData? item = inventory.GetItemAt(pos.x, pos.y);
        InventoryGui.instance.OnSelectedItem(InventoryGui.instance.m_playerGrid, item, pos, InventoryGrid.Modifier.Select);
    }

    private static void UseQuickHudSlot(QuickHudSlotMarker? marker)
    {
        if (marker == null || Player.m_localPlayer == null)
        {
            return;
        }

        Inventory inventory = ((Humanoid)Player.m_localPlayer).GetInventory();
        if (!TryGetQuickSlotDefinition(marker.Index, out SlotDefinition? slot))
        {
            return;
        }

        Vector2i pos = GetSlotGridPos(inventory, slot!);
        ItemData? item = inventory.GetItemAt(pos.x, pos.y);
        if (item != null && slot!.Accepts(item))
        {
            Player.m_localPlayer.UseItem(inventory, item, fromInventoryGui: InventoryGui.IsVisible());
            inventory.Changed();
        }
    }

    private static void ClearQuickSlotsHud()
    {
        if (!IsUnityNull(InventoryPanels.QuickSlotsHotkeyBarRect))
        {
            UnityEngine.Object.Destroy(InventoryPanels.QuickSlotsHotkeyBarRect!.gameObject);
        }

        InventoryPanels.QuickSlotsHotkeyBarRect = null;
        InventoryPanels.QuickSlotsHotkeyBar = null;
    }


}
