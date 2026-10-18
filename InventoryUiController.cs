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
    private static InventoryGui? _inventoryClosingLayoutGui;
    private static CanvasGroup? _inventoryClosingInputGroup;
    private static bool _inventoryClosingBlocksRaycasts;

    internal static bool IsInventoryClosingLayoutPending(InventoryGui? gui) =>
        gui != null && _inventoryClosingLayoutGui == gui;

    internal static void CancelInventoryClosingLayout(InventoryGui gui)
    {
        if (_inventoryClosingLayoutGui == gui) ClearInventoryClosingLayout();
    }

    private static void ClearInventoryClosingLayout()
    {
        if (_inventoryClosingInputGroup != null)
            _inventoryClosingInputGroup.blocksRaycasts = _inventoryClosingBlocksRaycasts;
        _inventoryClosingInputGroup = null;
        _inventoryClosingLayoutGui = null;
    }

    private static void DeferInventoryClosingLayoutRestore()
    {
        // Hide stops input/crafting immediately, but the animator still renders
        // these panels. Restore their native layout only once the root is hidden.
        InventoryGui? gui = InventoryGui.instance;
        if (IsInventoryClosingLayoutPending(gui)) return;
        ClearInventoryClosingLayout();
        _inventoryClosingLayoutGui = gui;
        if (gui != null && gui.m_inventoryRoot != null && gui.m_inventoryRoot.gameObject.activeInHierarchy)
        {
            // Preserve visuals, not interaction. Reuse a root-local group without
            // changing alpha/interactable (which would tint the closing controls).
            _inventoryClosingInputGroup = gui.m_inventoryRoot.GetComponent<CanvasGroup>() ??
                gui.m_inventoryRoot.gameObject.AddComponent<CanvasGroup>();
            _inventoryClosingBlocksRaycasts = _inventoryClosingInputGroup.blocksRaycasts;
            _inventoryClosingInputGroup.blocksRaycasts = false;
        }
        CraftingUi.SearchInput?.DeactivateInputField();
        _craftingCountInput?.DeactivateInputField();
        UpdateInventoryClosingLayout();
    }

    private static void UpdateInventoryClosingLayout()
    {
        InventoryGui? gui = _inventoryClosingLayoutGui;
        if (gui == null || gui != InventoryGui.instance)
        {
            ClearInventoryClosingLayout();
            return;
        }
        if (gui.m_inventoryRoot != null && gui.m_inventoryRoot.gameObject.activeInHierarchy) return;

        ClearInventoryClosingLayout();
        RestorePlayerStatPanels();
        HideCraftingPanelRedesign();
    }

    internal static void UpdateInventoryGridUi(InventoryGrid playerGrid, Player player)
    {
        if (playerGrid == null || player == null || playerGrid.m_gridRoot == null || playerGrid.m_elements == null)
        {
            return;
        }

        if (IsInventoryClosingLayoutPending(InventoryGui.instance)) return;

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
        foreach (ItemData item in inventory.GetAllItems())
        {
            if (item.m_gridPos.y >= usableRows && item.m_gridPos.y < fixedRows)
            {
                totalRegularRows = InventorySlotSafetyCore.GetRecoveryDisplayRows(totalRegularRows, item.m_gridPos.y);
            }
        }
        int viewportRows = GetInventoryViewportRows(totalRegularRows);
        viewportRows = UpdatePlayerInventoryScroll(playerGrid, viewportRows, totalRegularRows);
        // Occupied locked rows must remain reachable; empty locked cells are
        // still hidden and all insertion policies use usableRows, not this extent.
        if (totalRegularRows > usableRows)
        {
            viewportRows = totalRegularRows;
        }
        if (playerGrid.m_elements.Count == 0)
        {
            HideInventorySideHints();
            return;
        }

        Vector3 origin = GetGridOrigin(playerGrid);
        List<SlotDefinition> quickPanelSlots = GetQuickPanelSlots(player);
        UpdatePathOfValheimanPanelSlots(player, inventory, quickPanelSlots.Count);
        float displayedRows = viewportRows;
        playerGrid.m_gridRoot.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, displayedRows * playerGrid.m_elementSpace);
        if (UpdateInventoryPanelDragging() && TryUpdateDraggedInventoryPanelPositionOnly(playerGrid, origin, width))
        {
            UpdateItemRuleUi(InventoryGui.instance, origin, viewportRows);
            return;
        }

        List<SlotDefinition> customPanelSlots = GetCustomPanelSlots(player, inventory);

        UpdatePlayerInventoryPanelBackground(viewportRows);
        UpdateContainerPanelPosition(viewportRows, playerGrid.m_elementSpace);
        RectTransform? customPanel = customPanelSlots.Count > 0 ? EnsureSlotPanel(playerGrid, CustomSlotPanelName, InventoryPanels.CustomSlotPanels) : DisableSlotPanel(playerGrid, CustomSlotPanelName, InventoryPanels.CustomSlotPanels);
        bool hasQuickPanel = quickPanelSlots.Count + PovVisibleActions.Count > 0;
        RectTransform? quickPanel = hasQuickPanel ? EnsureSlotPanel(playerGrid, QuickSlotPanelName, InventoryPanels.QuickSlotPanels) : DisableSlotPanel(playerGrid, QuickSlotPanelName, InventoryPanels.QuickSlotPanels);
        for (int i = 0; i < playerGrid.m_elements.Count; i++)
        {
            InventoryElement element = playerGrid.m_elements[i];
            if (IsUnityNull(element))
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
                element!.gameObject.SetActive(false);
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
                    element!.gameObject.SetActive(false);
                    element.m_used = true;
                    UpdateSlotBindingLabel(element, null);
                    HideFavoriteBorder(element);
                    HideInventoryPinnedTooltipBorder(element);
                    continue;
                }

                SlotDefinition slot = SlotDefinitions[slotIndex];
                bool equipmentPanelSlot = IsEquipmentPanelSlot(slot);
                bool povQuickSlot = slot.PovCell != null && !equipmentPanelSlot;
                RectTransform? targetPanel = equipmentPanelSlot ? customPanel : quickPanel;
                List<SlotDefinition> visibleSlots = equipmentPanelSlot ? customPanelSlots :
                    povQuickSlot ? PovVisibleActions : quickPanelSlots;
                int visibleIndex = visibleSlots.IndexOf(slot);
                if (targetPanel == null || visibleIndex < 0)
                {
                    element!.gameObject.SetActive(false);
                    element.m_used = true;
                    UpdateSlotBindingLabel(element, null);
                    HideFavoriteBorder(element);
                    HideInventoryPinnedTooltipBorder(element);
                    continue;
                }

                RectTransform elementRect = (RectTransform)element!.gameObject.transform;
                if (elementRect.parent != targetPanel)
                {
                    elementRect.SetParent(targetPanel, false);
                }
                elementRect.localScale = Vector3.one;
                elementRect.localRotation = Quaternion.identity;
                elementRect.localPosition = equipmentPanelSlot ? GetCustomSlotPanelElementPosition(visibleIndex, playerGrid.m_elementSpace) :
                    povQuickSlot ? GetPathOfValheimanActionPosition(visibleIndex, playerGrid.m_elementSpace) :
                    GetQuickSlotPanelElementPosition(visibleIndex, playerGrid.m_elementSpace);
                element.gameObject.SetActive(true);
                if (!element.m_used && element.m_tooltip != null)
                {
                    element.m_tooltip.m_topic = "";
                    element.m_tooltip.m_text = "";
                }

                bool equipmentSlotOccupied = UpdateEquipmentSlotElementState(element, player, inventory, slot, new Vector2i(x, y));
                if (equipmentPanelSlot && slot.Kind == SlotKind.Quick) equipmentSlotOccupied = IsOccupiedSlot(inventory, slot);
                UpdateSlotBindingLabel(element, slot, equipmentPanelSlot && equipmentSlotOccupied);
                UpdateFavoriteBorder(element, player, inventory, new Vector2i(x, y));
                UpdateInventoryPinnedTooltipBorder(playerGrid, element, new Vector2i(x, y));
            }
            else
            {
                RectTransform elementRect = (RectTransform)element!.gameObject.transform;
                if (elementRect.parent != playerGrid.m_gridRoot)
                {
                    elementRect.SetParent(playerGrid.m_gridRoot, false);
                }

                if (y >= viewportRows || y >= totalRegularRows)
                {
                    element.gameObject.SetActive(false);
                    element.m_used = true;
                    HideFavoriteBorder(element);
                    HideInventoryPinnedTooltipBorder(element);
                    continue;
                }

                elementRect.localPosition = origin + new Vector3(x * playerGrid.m_elementSpace, -y * playerGrid.m_elementSpace, 0f);
                element.gameObject.SetActive(true);
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
            UpdateVanillaPanelBackground(quickPanel, _povQuickColumns * playerGrid.m_elementSpace, _povQuickContentRows * playerGrid.m_elementSpace);
            quickPanel.gameObject.SetActive(true);
        }

        UpdateInventorySideHints(playerGrid, origin, playerGrid.m_elementSpace, totalRegularRows);
        UpdateInventoryActionPanels(playerGrid, player, origin, viewportRows);
        EnsureInventoryPinnedTooltipHandlers(InventoryGui.instance?.m_playerGrid);
        EnsureInventoryPinnedTooltipHandlers(InventoryGui.instance?.m_containerGrid);
        UpdateInventoryPinnedTooltipGridBorders(InventoryGui.instance?.m_containerGrid);
        UpdateContainerWeightPanelPosition();
    }

}
