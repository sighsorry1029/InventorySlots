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
    private static Vector3 GetCustomSlotPanelElementPosition(int index, float elementSpace)
    {
        return new Vector3((index / CustomSlotPanelRows) * elementSpace, -(index % CustomSlotPanelRows) * elementSpace, 0f);
    }

    private static Vector3 GetQuickSlotPanelElementPosition(int index, float elementSpace)
    {
        return new Vector3((index % QuickSlotPanelColumns) * elementSpace, -(index / QuickSlotPanelColumns) * elementSpace, 0f);
    }

    private static int GetCustomPanelColumns(int slotCount)
    {
        return slotCount <= 0 ? 0 : Mathf.CeilToInt(slotCount / (float)CustomSlotPanelRows);
    }

    private static int GetQuickPanelColumns(int slotCount)
    {
        return slotCount > 0 ? QuickSlotPanelColumns : 0;
    }

    private static int GetQuickPanelRows(int slotCount)
    {
        return slotCount <= 0 ? 0 : Mathf.CeilToInt(slotCount / (float)QuickSlotPanelColumns);
    }

    private static int GetInventoryViewportRows(int totalRegularRows)
    {
        int unlockedRows = Mathf.Clamp(totalRegularRows, BaseRows, BaseRows + MaxSupportedExtraRows);
        if (!UseExpandableInventoryRows())
        {
            return Mathf.Max(1, unlockedRows);
        }

        int rememberedRows = GetLastExpandableInventoryRows(unlockedRows);
        return Mathf.Clamp(Mathf.Min(unlockedRows, rememberedRows), 1, Mathf.Max(1, unlockedRows));
    }

    private static int UpdatePlayerInventoryScroll(InventoryGrid playerGrid, int viewportRows, int totalRegularRows)
    {
        InventoryPanels.PlayerInventoryMaxScroll = 0;
        InventoryPanels.PlayerInventoryScrollOffset = 0;
        StopPlayerInventoryRowsDrag();
        HidePlayerInventoryScrollbar();
        HidePlayerInventoryResizeHandle(stopDragging: false);

        if (UseExpandableInventoryRows())
        {
            HandlePlayerInventoryExpandableWheel(playerGrid, totalRegularRows);
            viewportRows = GetInventoryViewportRows(totalRegularRows);
        }

        return viewportRows;
    }

    private static bool UseExpandableInventoryRows()
    {
        return _inventoryRowsDisplayMode == null || _inventoryRowsDisplayMode.Value == InventoryRowsDisplayMode.Expandable;
    }

    private static void HandlePlayerInventoryExpandableWheel(InventoryGrid playerGrid, int totalRegularRows)
    {
        if (totalRegularRows <= BaseRows || !InventoryGui.IsVisible())
        {
            return;
        }

        if (ShouldSuppressInventoryContainerRowsWheel())
        {
            return;
        }

        if (!IsMouseOverPlayerInventory(playerGrid) && !IsGamepadUiScrollActive())
        {
            return;
        }

        float wheel = GetUiScrollDelta(UiScrollInputMode.Discrete);
        if (Mathf.Abs(wheel) < 0.01f)
        {
            return;
        }

        int direction = wheel < 0f ? 1 : -1;
        SetExpandableInventoryRows(GetLastExpandableInventoryRows(totalRegularRows) + direction, totalRegularRows);
    }

    private static int GetLastExpandableInventoryRows(int totalRegularRows)
    {
        EnsureLastExpandableInventoryRowsLoaded();
        int maxRows = Mathf.Clamp(totalRegularRows, BaseRows, BaseRows + MaxSupportedExtraRows);
        return Mathf.Clamp(InventoryPanels.LastExpandableInventoryRows, BaseRows, maxRows);
    }

    private static void SetExpandableInventoryRows(int rows, int totalRegularRows)
    {
        EnsureLastExpandableInventoryRowsLoaded();
        int maxRows = Mathf.Clamp(totalRegularRows, BaseRows, BaseRows + MaxSupportedExtraRows);
        int clampedRows = Mathf.Clamp(rows, BaseRows, maxRows);
        if (InventoryPanels.LastExpandableInventoryRows == clampedRows)
        {
            return;
        }

        InventoryPanels.LastExpandableInventoryRows = clampedRows;
        SaveLastExpandableInventoryRows();
    }

    private static void EnsureLastExpandableInventoryRowsLoaded()
    {
        if (InventoryPanels.LastExpandableInventoryRowsLoaded)
        {
            return;
        }

        InventoryPanels.LastExpandableInventoryRowsLoaded = true;
        try
        {
            EnsureClientStateLoaded();
            InventoryPanels.LastExpandableInventoryRows = Mathf.Clamp(InventoryClient.ClientState.Inventory.LastExpandableRows, BaseRows, BaseRows + MaxSupportedExtraRows);
        }
        catch (Exception)
        {
        }
    }

    private static void SaveLastExpandableInventoryRows()
    {
        try
        {
            EnsureClientStateLoaded();
            InventoryClient.ClientState.Inventory.LastExpandableRows = InventoryPanels.LastExpandableInventoryRows;
            SaveClientState();
        }
        catch (Exception)
        {
        }
    }

    private static void UpdatePlayerInventoryRowsDragging(int totalRegularRows, float elementSpace)
    {
        if (!InventoryPanels.PlayerInventoryRowsDragging)
        {
            return;
        }

        if (!InventoryGui.IsVisible() || !Input.GetMouseButton(0))
        {
            StopPlayerInventoryRowsDrag();
            return;
        }

        float rowHeight = Mathf.Max(1f, elementSpace);
        int rowDelta = Mathf.RoundToInt((InventoryPanels.PlayerInventoryRowsDragStartMouse.y - GetUiMousePosition().y) / rowHeight);
        SetExpandableInventoryRows(InventoryPanels.PlayerInventoryRowsDragStartRows + rowDelta, totalRegularRows);
    }

    private static void StartPlayerInventoryRowsDrag()
    {
        if (!InventoryGui.IsVisible())
        {
            return;
        }

        InventoryPanels.PlayerInventoryRowsDragging = true;
        InventoryPanels.PlayerInventoryRowsDragStartMouse = GetUiMousePosition();
        InventoryPanels.PlayerInventoryRowsDragStartRows = GetLastExpandableInventoryRows(BaseRows + MaxSupportedExtraRows);
    }

    private static void StopPlayerInventoryRowsDrag()
    {
        InventoryPanels.PlayerInventoryRowsDragging = false;
    }

    private static void HandlePlayerInventoryMouseWheel(InventoryGrid playerGrid)
    {
        if (InventoryPanels.PlayerInventoryMaxScroll <= 0 || !InventoryGui.IsVisible())
        {
            return;
        }

        if (ShouldSuppressInventoryContainerRowsWheel())
        {
            return;
        }

        if (!IsMouseOverPlayerInventory(playerGrid) && !IsGamepadUiScrollActive())
        {
            return;
        }

        float wheel = GetUiScrollDelta(UiScrollInputMode.Discrete);
        if (Mathf.Abs(wheel) < 0.01f)
        {
            return;
        }

        int direction = wheel > 0f ? -1 : 1;
        InventoryPanels.PlayerInventoryScrollOffset = Mathf.Clamp(InventoryPanels.PlayerInventoryScrollOffset + direction, 0, InventoryPanels.PlayerInventoryMaxScroll);
    }

    private static bool IsMouseOverPlayerInventory(InventoryGrid playerGrid)
    {
        if (playerGrid == null)
        {
            return false;
        }

        Vector2 mouse = GetUiMousePosition();
        if (playerGrid.m_gridRoot != null && RectContainsScreenPoint(playerGrid.m_gridRoot, mouse))
        {
            return true;
        }

        if (InventoryPanels.PlayerInventoryScrollbar != null && InventoryPanels.PlayerInventoryScrollbar.gameObject.activeInHierarchy && RectContainsScreenPoint(InventoryPanels.PlayerInventoryScrollbar, mouse))
        {
            return true;
        }

        if (InventoryPanels.PlayerInventoryResizeHandle != null && InventoryPanels.PlayerInventoryResizeHandle.gameObject.activeInHierarchy && RectContainsScreenPoint(InventoryPanels.PlayerInventoryResizeHandle, mouse))
        {
            return true;
        }

        return InventoryGui.instance != null && InventoryGui.instance.m_player != null && RectContainsScreenPoint(InventoryGui.instance.m_player, mouse);
    }

    private static bool RectContainsScreenPoint(RectTransform rectTransform, Vector2 screenPoint)
    {
        Vector2 localPoint = rectTransform.InverseTransformPoint(screenPoint);
        return rectTransform.rect.Contains(localPoint);
    }

    private static bool TryGetLocalPointInRect(RectTransform rectTransform, Vector2 screenPoint, out Vector2 localPoint)
    {
        localPoint = rectTransform.InverseTransformPoint(screenPoint);
        return true;
    }

    private static void HidePlayerInventoryScrollbar()
    {
        if (InventoryPanels.PlayerInventoryScrollbar != null)
        {
            InventoryPanels.PlayerInventoryScrollbar.gameObject.SetActive(false);
        }
    }

    private static void UpdatePlayerInventoryResizeHandle(InventoryGrid playerGrid, int viewportRows, int totalRegularRows)
    {
        if (totalRegularRows <= BaseRows)
        {
            HidePlayerInventoryResizeHandle();
            return;
        }

        RectTransform handle = EnsurePlayerInventoryResizeHandle(playerGrid);
        Vector3 origin = GetGridOrigin(playerGrid);
        float elementSpace = playerGrid.m_elementSpace;
        float width = playerGrid.m_width * elementSpace;
        float handleHeight = Mathf.Clamp(elementSpace * 0.18f, 12f, 18f);

        handle.localPosition = origin + new Vector3((playerGrid.m_width - 1) * elementSpace / 2f, -(viewportRows - 0.55f) * elementSpace, 0f);
        handle.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
        handle.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, handleHeight);
        handle.SetAsLastSibling();
        handle.gameObject.SetActive(true);

        if (InventoryPanels.PlayerInventoryResizeHandleLabel != null)
        {
            int hiddenRows = Mathf.Max(0, totalRegularRows - viewportRows);
            InventoryPanels.PlayerInventoryResizeHandleLabel.text = hiddenRows > 0 ? "..." : "---";
        }
    }

    private static RectTransform EnsurePlayerInventoryResizeHandle(InventoryGrid playerGrid)
    {
        if (InventoryPanels.PlayerInventoryResizeHandle != null && InventoryPanels.PlayerInventoryResizeHandle.parent == playerGrid.m_gridRoot)
        {
            return InventoryPanels.PlayerInventoryResizeHandle;
        }

        Transform? existing = playerGrid.m_gridRoot.Find("InventorySlots_PlayerInventoryResizeHandle");
        InventoryPanels.PlayerInventoryResizeHandle = existing != null ? existing.GetComponent<RectTransform>() : null;
        if (InventoryPanels.PlayerInventoryResizeHandle == null)
        {
            InventoryPanels.PlayerInventoryResizeHandle = new GameObject("InventorySlots_PlayerInventoryResizeHandle", typeof(RectTransform), typeof(Image), typeof(UIInputHandler), typeof(PlayerInventoryRowsHandleMarker)).GetComponent<RectTransform>();
            InventoryPanels.PlayerInventoryResizeHandle.SetParent(playerGrid.m_gridRoot, false);

            Image background = InventoryPanels.PlayerInventoryResizeHandle.GetComponent<Image>();
            background.color = new Color(0f, 0f, 0f, 0.35f);
            background.raycastTarget = true;

            RectTransform labelRect = CreateTextRect("label", InventoryPanels.PlayerInventoryResizeHandle, out TMP_Text labelText);
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;

            InventoryPanels.PlayerInventoryResizeHandleLabel = labelText;
            InventoryPanels.PlayerInventoryResizeHandleLabel.alignment = TextAlignmentOptions.Center;
            InventoryPanels.PlayerInventoryResizeHandleLabel.fontSize = 14f;
            InventoryPanels.PlayerInventoryResizeHandleLabel.color = new Color(0.75f, 0.86f, 1f, 0.9f);
            InventoryPanels.PlayerInventoryResizeHandleLabel.raycastTarget = false;
        }

        InventoryPanels.PlayerInventoryResizeHandle.SetParent(playerGrid.m_gridRoot, false);
        InventoryPanels.PlayerInventoryResizeHandle.anchorMin = new Vector2(0.5f, 0.5f);
        InventoryPanels.PlayerInventoryResizeHandle.anchorMax = new Vector2(0.5f, 0.5f);
        InventoryPanels.PlayerInventoryResizeHandle.pivot = new Vector2(0.5f, 0.5f);
        InventoryPanels.PlayerInventoryResizeHandle.localScale = Vector3.one;
        InventoryPanels.PlayerInventoryResizeHandle.localRotation = Quaternion.identity;

        PlayerInventoryRowsHandleMarker marker = InventoryPanels.PlayerInventoryResizeHandle.GetComponent<PlayerInventoryRowsHandleMarker>();
        if (!marker.Initialized)
        {
            UIInputHandler input = InventoryPanels.PlayerInventoryResizeHandle.GetComponent<UIInputHandler>();
            input.m_onLeftDown += _ => StartPlayerInventoryRowsDrag();
            input.m_onLeftUp += _ => StopPlayerInventoryRowsDrag();
            marker.Initialized = true;
        }

        InventoryPanels.PlayerInventoryResizeHandleLabel = InventoryPanels.PlayerInventoryResizeHandle.Find("label")?.GetComponent<TextMeshProUGUI>();
        ApplyDefaultFontAsset(InventoryPanels.PlayerInventoryResizeHandleLabel);
        return InventoryPanels.PlayerInventoryResizeHandle;
    }

    private static void HidePlayerInventoryResizeHandle(bool stopDragging = true)
    {
        if (stopDragging)
        {
            StopPlayerInventoryRowsDrag();
        }

        if (InventoryPanels.PlayerInventoryResizeHandle != null)
        {
            InventoryPanels.PlayerInventoryResizeHandle.gameObject.SetActive(false);
        }
    }

}
