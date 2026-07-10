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
    internal static void RestoreContainerPanelPosition()
    {
        if (InventoryPanels.TrackedContainerPanel != null && InventoryPanels.ContainerPanelBasePositionSet && !IsUnityNull(InventoryPanels.TrackedContainerPanel))
        {
            InventoryPanels.TrackedContainerPanel.localPosition = InventoryPanels.ContainerPanelBasePosition;
        }

        InventoryPanels.TrackedContainerPanel = null;
        InventoryPanels.ContainerPanelBasePosition = Vector3.zero;
        InventoryPanels.ContainerPanelAppliedOffset = Vector3.zero;
        InventoryPanels.ContainerPanelBasePositionSet = false;
    }

    internal static void RestoreContainerWeightPanelPosition()
    {
        if (InventoryPanels.TrackedContainerWeightPanel != null && InventoryPanels.ContainerWeightPanelBasePositionSet && !IsUnityNull(InventoryPanels.TrackedContainerWeightPanel))
        {
            InventoryPanels.TrackedContainerWeightPanel.localPosition = InventoryPanels.ContainerWeightPanelBasePosition;
        }

        InventoryPanels.TrackedContainerWeightPanel = null;
        InventoryPanels.ContainerWeightPanelBasePosition = Vector3.zero;
        InventoryPanels.ContainerWeightPanelAppliedYOffset = 0f;
        InventoryPanels.ContainerWeightPanelBasePositionSet = false;
    }

    private static void UpdateContainerPanelPosition(int viewportRows, float elementSpace)
    {
        InventoryGui? gui = InventoryGui.instance;
        if (gui == null || gui.m_container == null)
        {
            RestoreContainerPanelPosition();
            return;
        }

        RectTransform containerPanel = gui.m_container;
        if (!containerPanel.gameObject.activeSelf)
        {
            RestoreContainerPanelPosition();
            return;
        }

        if (InventoryPanels.TrackedContainerPanel != containerPanel)
        {
            InventoryPanels.TrackedContainerPanel = containerPanel;
            InventoryPanels.ContainerPanelBasePosition = containerPanel.localPosition;
            InventoryPanels.ContainerPanelAppliedOffset = Vector3.zero;
            InventoryPanels.ContainerPanelBasePositionSet = true;
        }
        else if (InventoryPanels.ContainerPanelBasePositionSet)
        {
            InventoryPanels.ContainerPanelBasePosition = containerPanel.localPosition - InventoryPanels.ContainerPanelAppliedOffset;
        }

        float extraHeight = Mathf.Max(0, viewportRows - BaseRows) * Mathf.Max(1f, elementSpace);
        float verticalDirection = 1f;
        if (gui.m_player != null)
        {
            verticalDirection = InventoryPanels.ContainerPanelBasePosition.y >= gui.m_player.localPosition.y ? 1f : -1f;
        }

        Vector3 offset = new(0f, verticalDirection * extraHeight, 0f);
        containerPanel.localPosition = InventoryPanels.ContainerPanelBasePosition + offset;
        InventoryPanels.ContainerPanelAppliedOffset = offset;
    }

    private static void UpdateContainerWeightPanelPosition()
    {
        InventoryGui? gui = InventoryGui.instance;
        if (gui == null || gui.m_container == null || gui.m_currentContainer == null || gui.m_containerWeight == null || !gui.m_container.gameObject.activeSelf)
        {
            RestoreContainerWeightPanelPosition();
            return;
        }

        RectTransform? weightPanel = GetContainerWeightPanelRoot(gui);
        if (weightPanel == null)
        {
            RestoreContainerWeightPanelPosition();
            return;
        }

        if (InventoryPanels.TrackedContainerWeightPanel != weightPanel)
        {
            InventoryPanels.TrackedContainerWeightPanel = weightPanel;
            InventoryPanels.ContainerWeightPanelBasePosition = weightPanel.localPosition;
            InventoryPanels.ContainerWeightPanelAppliedYOffset = 0f;
            InventoryPanels.ContainerWeightPanelBasePositionSet = true;
        }
        else if (InventoryPanels.ContainerWeightPanelBasePositionSet)
        {
            InventoryPanels.ContainerWeightPanelBasePosition = weightPanel.localPosition - new Vector3(0f, InventoryPanels.ContainerWeightPanelAppliedYOffset, 0f);
        }

        float yOffset = ContainerWeightPanelFixedYOffset;
        weightPanel.localPosition = InventoryPanels.ContainerWeightPanelBasePosition + new Vector3(0f, yOffset, 0f);
        InventoryPanels.ContainerWeightPanelAppliedYOffset = yOffset;
    }

    private static bool UpdateEquipmentSlotElementState(InventoryGrid.Element element, Player player, Inventory inventory, SlotDefinition slot, Vector2i pos)
    {
        if (slot.Kind == SlotKind.Quick || inventory == null || IsOutOfBounds(inventory, pos))
        {
            return false;
        }

        ItemData? item = inventory.GetItemAt(pos.x, pos.y);
        if (item?.m_shared == null)
        {
            ResetEquipmentSlotTooltipCache(element);
            if (element.m_tooltip != null)
            {
                element.m_tooltip.m_topic = "";
                element.m_tooltip.m_text = "";
            }

            if (element.m_equiped != null)
            {
                element.m_equiped.enabled = false;
            }

            return false;
        }

        element.m_used = true;
        UpdateEquipmentSlotTooltip(element, item);
        if (element.m_equiped != null)
        {
            element.m_equiped.enabled = IsSlotItemEquippedForDisplay(player, item, slot);
        }

        return true;
    }

    private static void UpdateEquipmentSlotTooltip(InventoryGrid.Element element, ItemData item)
    {
        if (element?.m_tooltip == null || item?.m_shared == null)
        {
            return;
        }

        InventoryGridElementUiCache? cache = GetInventoryGridElementUiCache(element);
        string signature = GetEquipmentSlotTooltipSignature(item);
        bool forceJewelcraftingRefresh = ShouldForceJewelcraftingEquipmentTooltipRefresh(item);
        if (cache != null &&
            !forceJewelcraftingRefresh &&
            string.Equals(cache.EquipmentTooltipSignature, signature, StringComparison.Ordinal) &&
            !string.IsNullOrWhiteSpace(element.m_tooltip.m_text))
        {
            return;
        }

        try
        {
            InventoryGui? gui = InventoryGui.instance;
            if (gui?.m_playerGrid != null)
            {
                gui.m_playerGrid.CreateItemTooltip(item, element.m_tooltip);
                if (string.IsNullOrWhiteSpace(element.m_tooltip.m_text))
                {
                    SetDirectEquipmentSlotTooltip(element, item);
                }

                if (cache != null)
                {
                    cache.EquipmentTooltipSignature = signature;
                }

                return;
            }
        }
        catch (Exception)
        {
        }

        SetDirectEquipmentSlotTooltip(element, item);

        if (cache != null)
        {
            cache.EquipmentTooltipSignature = signature;
        }
    }

    private static bool ShouldForceJewelcraftingEquipmentTooltipRefresh(ItemData item)
    {
        return HasJewelcraftingActive &&
               TryGetJewelcraftingGemApi(out JewelcraftingGemApi? api) &&
               api != null &&
               api.HasSocketContainer(item);
    }

    private static void SetDirectEquipmentSlotTooltip(InventoryGrid.Element element, ItemData item)
    {
        if (element.m_tooltip == null || item?.m_shared == null)
        {
            return;
        }

        EnsureTooltipPrefab(element.m_tooltip);
        element.m_tooltip.Set(
            item.m_shared.m_name,
            item.GetTooltip(-1),
            InventoryGui.instance?.m_playerGrid != null ? InventoryGui.instance.m_playerGrid.m_tooltipAnchor : null,
            default);
    }

    internal static void EnsureInventoryGridItemTooltipText(ItemData item, UITooltip tooltip)
    {
        if (tooltip == null ||
            IsUnityNull(tooltip) ||
            item?.m_shared == null ||
            !string.IsNullOrWhiteSpace(tooltip.m_text))
        {
            return;
        }

        string fallbackText = item.GetTooltip(-1);
        if (string.IsNullOrWhiteSpace(fallbackText))
        {
            return;
        }

        EnsureTooltipPrefab(tooltip);
        if (string.IsNullOrWhiteSpace(tooltip.m_topic))
        {
            tooltip.m_topic = item.m_shared.m_name;
        }

        tooltip.m_text = fallbackText;
    }

    private static void ResetEquipmentSlotTooltipCache(InventoryGrid.Element element)
    {
        InventoryGridElementUiCache? cache = GetInventoryGridElementUiCache(element);
        if (cache != null)
        {
            cache.EquipmentTooltipSignature = "";
        }
    }

    private static string GetEquipmentSlotTooltipSignature(ItemData item)
    {
        string prefab = GetItemPrefabName(item);
        string customData = item.m_customData is { Count: > 0 }
            ? string.Join(";", item.m_customData
                .OrderBy(pair => pair.Key, StringComparer.Ordinal)
                .Select(pair => $"{pair.Key}={pair.Value}"))
            : "";
        int durability = Mathf.RoundToInt(item.m_durability * 1000f);
        return string.Join(
            "|",
            prefab,
            item.m_shared?.m_name ?? "",
            item.m_gridPos.x,
            item.m_gridPos.y,
            item.m_quality,
            item.m_variant,
            item.m_stack,
            durability,
            item.m_equipped,
            customData);
    }

    private static bool IsSlotItemEquippedForDisplay(Player player, ItemData item, SlotDefinition slot)
    {
        if (item == null || slot.Kind == SlotKind.Quick)
        {
            return false;
        }

        if (slot.Kind == SlotKind.CustomEquipment)
        {
            return IsInventorySlotsCustomEquipped(item);
        }

        try
        {
            return ((Humanoid)player).IsItemEquiped(item) || item.m_equipped;
        }
        catch
        {
            return item.m_equipped;
        }
    }

    private static RectTransform? GetContainerWeightPanelRoot(InventoryGui gui)
    {
        if (gui.m_containerWeight == null)
        {
            return null;
        }

        RectTransform? textRect = gui.m_containerWeight.GetComponent<RectTransform>();
        if (textRect == null)
        {
            return null;
        }

        if (gui.m_container == null || !gui.m_containerWeight.transform.IsChildOf(gui.m_container))
        {
            return textRect;
        }

        RectTransform? candidate = textRect;
        Transform cursor = gui.m_containerWeight.transform;
        while (cursor.parent != null && cursor.parent != gui.m_container)
        {
            cursor = cursor.parent;
            if (cursor is RectTransform rect)
            {
                candidate = rect;
            }
        }

        return candidate;
    }

}
