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
    internal static void UpdateInventoryGridUi(InventoryGrid playerGrid, Player player)
    {
        if (playerGrid == null || player == null || playerGrid.m_gridRoot == null || playerGrid.m_elements == null)
        {
            return;
        }

        if (!InventoryGui.IsVisible())
        {
            UpdateQuickSlotInventoryPanelsWhileHidden();
            return;
        }

        int usableRows = GetUsableRegularRows(player);
        int fixedRows = GetFixedRegularRows();
        Inventory inventory = ((Humanoid)player).GetInventory();
        if (inventory == null)
        {
            return;
        }

        int width = inventory.GetWidth();
        const bool hideLockedRows = true;
        int totalRegularRows = usableRows;
        int viewportRows = GetInventoryViewportRows(totalRegularRows);
        viewportRows = UpdatePlayerInventoryScroll(playerGrid, viewportRows, totalRegularRows);
        if (playerGrid.m_elements.Count == 0)
        {
            HideInventorySideHints();
            return;
        }

        Vector3 origin = GetGridOrigin(playerGrid);
        float displayedRows = viewportRows;
        playerGrid.m_gridRoot.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, displayedRows * playerGrid.m_elementSpace);
        if (UpdateInventoryPanelDragging() && TryUpdateDraggedInventoryPanelPositionOnly(playerGrid, origin, width))
        {
            return;
        }

        List<SlotDefinition> customPanelSlots = GetCustomPanelSlots(player, inventory);
        List<SlotDefinition> quickPanelSlots = GetQuickPanelSlots(player);

        UpdatePlayerInventoryPanelBackground(viewportRows);
        UpdateContainerPanelPosition(viewportRows, playerGrid.m_elementSpace);
        RectTransform? customPanel = customPanelSlots.Count > 0 ? EnsureSlotPanel(playerGrid, CustomSlotPanelName, InventoryPanels.CustomSlotPanels) : DisableSlotPanel(playerGrid, CustomSlotPanelName, InventoryPanels.CustomSlotPanels);
        RectTransform? quickPanel = quickPanelSlots.Count > 0 ? EnsureSlotPanel(playerGrid, QuickSlotPanelName, InventoryPanels.QuickSlotPanels) : DisableSlotPanel(playerGrid, QuickSlotPanelName, InventoryPanels.QuickSlotPanels);
        for (int i = 0; i < playerGrid.m_elements.Count; i++)
        {
            InventoryGrid.Element element = playerGrid.m_elements[i];
            if (IsUnityNull(element?.m_go))
            {
                continue;
            }

            int y = i / width;
            int x = i - y * width;
            Vector2i gridPos = new(x, y);
            bool hideLockedCell = hideLockedRows &&
                                  !IsExternalReservedCell(gridPos, includeRestockableSlots: true) &&
                                  y >= usableRows &&
                                  y < fixedRows &&
                                  !ShouldShowLockedInventoryCellForRecovery(player, inventory, gridPos);
            if (hideLockedCell)
            {
                element!.m_go.SetActive(false);
                element.m_used = true;
                HideFavoriteBorder(element);
                HideInventoryPinnedTooltipBorder(element);
                continue;
            }

            if (y >= fixedRows)
            {
                int slotIndex = (y - fixedRows) * width + x;
                if (slotIndex >= SlotDefinitions.Count)
                {
                    element!.m_go.SetActive(false);
                    element.m_used = true;
                    UpdateSlotBindingLabel(element, null);
                    HideFavoriteBorder(element);
                    HideInventoryPinnedTooltipBorder(element);
                    continue;
                }

                SlotDefinition slot = SlotDefinitions[slotIndex];
                bool quickSlot = slot.Kind == SlotKind.Quick;
                RectTransform? targetPanel = quickSlot ? quickPanel : customPanel;
                List<SlotDefinition> visibleSlots = quickSlot ? quickPanelSlots : customPanelSlots;
                int visibleIndex = visibleSlots.IndexOf(slot);
                if (targetPanel == null || visibleIndex < 0)
                {
                    element!.m_go.SetActive(false);
                    element.m_used = true;
                    UpdateSlotBindingLabel(element, null);
                    HideFavoriteBorder(element);
                    HideInventoryPinnedTooltipBorder(element);
                    continue;
                }

                RectTransform elementRect = (RectTransform)element!.m_go.transform;
                elementRect.SetParent(targetPanel, false);
                elementRect.localScale = Vector3.one;
                elementRect.localRotation = Quaternion.identity;
                elementRect.localPosition = quickSlot ? GetQuickSlotPanelElementPosition(visibleIndex, playerGrid.m_elementSpace) : GetCustomSlotPanelElementPosition(visibleIndex, playerGrid.m_elementSpace);
                element.m_go.SetActive(true);
                if (!element.m_used && element.m_tooltip != null)
                {
                    element.m_tooltip.m_topic = "";
                    element.m_tooltip.m_text = "";
                }

                bool equipmentSlotOccupied = UpdateEquipmentSlotElementState(element, player, inventory, slot, new Vector2i(x, y));
                UpdateSlotBindingLabel(element, slot, !quickSlot && equipmentSlotOccupied);
                UpdateFavoriteBorder(element, player, inventory, new Vector2i(x, y));
                UpdateInventoryPinnedTooltipBorder(playerGrid, element, new Vector2i(x, y));
            }
            else
            {
                RectTransform elementRect = (RectTransform)element!.m_go.transform;
                if (elementRect.parent != playerGrid.m_gridRoot)
                {
                    elementRect.SetParent(playerGrid.m_gridRoot, false);
                }

                if (y >= viewportRows || y >= totalRegularRows)
                {
                    element.m_go.SetActive(false);
                    element.m_used = true;
                    HideFavoriteBorder(element);
                    HideInventoryPinnedTooltipBorder(element);
                    continue;
                }

                elementRect.localPosition = origin + new Vector3(x * playerGrid.m_elementSpace, -y * playerGrid.m_elementSpace, 0f);
                element.m_go.SetActive(true);
                UpdateFavoriteBorder(element, player, inventory, new Vector2i(x, y));
                UpdateInventoryPinnedTooltipBorder(playerGrid, element, new Vector2i(x, y));
            }
        }

        if (customPanel != null)
        {
            int columns = GetCustomPanelColumns(customPanelSlots.Count);
            Vector3 sidePanelBasePosition = GetSidePanelBasePosition(origin, width, playerGrid.m_elementSpace);
            customPanel.localPosition = sidePanelBasePosition + (Vector3)InventoryPanels.EquipmentSlotsPanelRuntimeOffset;
            UpdateVanillaPanelBackground(customPanel, columns * playerGrid.m_elementSpace, CustomSlotPanelRows * playerGrid.m_elementSpace);
            customPanel.gameObject.SetActive(customPanelSlots.Count > 0);
            UpdatePlayerStatPanels(customPanel, columns, playerGrid.m_elementSpace);
        }
        else
        {
            RestorePlayerStatPanels();
        }

        if (quickPanel != null)
        {
            Vector3 sidePanelBasePosition = GetSidePanelBasePosition(origin, width, playerGrid.m_elementSpace);
            Vector3 quickPanelTargetPosition =
                sidePanelBasePosition +
                (Vector3)InventoryPanels.QuickSlotsPanelRuntimeOffset;
            PositionQuickSlotPanel(playerGrid, quickPanel, quickPanelTargetPosition, playerGrid.m_elementSpace);
            int quickRows = GetQuickPanelRows(quickPanelSlots.Count);
            UpdateVanillaPanelBackground(quickPanel, QuickSlotPanelColumns * playerGrid.m_elementSpace, quickRows * playerGrid.m_elementSpace);
            quickPanel.gameObject.SetActive(quickPanelSlots.Count > 0);
        }

        UpdateInventorySideHints(playerGrid, origin, playerGrid.m_elementSpace, totalRegularRows);
        UpdateInventoryActionPanels(playerGrid, player, origin, viewportRows);
        EnsureInventoryPinnedTooltipHandlers(InventoryGui.instance?.m_playerGrid);
        EnsureInventoryPinnedTooltipHandlers(InventoryGui.instance?.m_containerGrid);
        UpdateInventoryPinnedTooltipGridBorders(InventoryGui.instance?.m_containerGrid);
        UpdateContainerWeightPanelPosition();
    }

}
