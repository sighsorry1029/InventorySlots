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
    private static void UpdatePlayerInventoryScrollbar(InventoryGrid playerGrid, int viewportRows, int totalRegularRows)
    {
        if (InventoryPanels.PlayerInventoryMaxScroll <= 0)
        {
            if (InventoryPanels.PlayerInventoryScrollbar != null)
            {
                InventoryPanels.PlayerInventoryScrollbar.gameObject.SetActive(false);
            }

            return;
        }

        RectTransform scrollbarRect = EnsurePlayerInventoryScrollbar(playerGrid);
        Scrollbar scrollbar = InventoryPanels.PlayerInventoryScrollbarComponent!;
        Vector3 origin = GetGridOrigin(playerGrid);
        float height = viewportRows * playerGrid.m_elementSpace;
        scrollbarRect.localPosition = origin + new Vector3(playerGrid.m_width * playerGrid.m_elementSpace + 12f, -height / 2f + playerGrid.m_elementSpace / 2f, 0f);
        scrollbarRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 12f);
        scrollbarRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
        scrollbar.size = Mathf.Clamp01(viewportRows / (float)Mathf.Max(viewportRows, totalRegularRows));

        InventoryPanels.UpdatingPlayerInventoryScrollbar = true;
        scrollbar.value = InventoryPanels.PlayerInventoryMaxScroll == 0 ? 1f : 1f - InventoryPanels.PlayerInventoryScrollOffset / (float)InventoryPanels.PlayerInventoryMaxScroll;
        InventoryPanels.UpdatingPlayerInventoryScrollbar = false;
        scrollbarRect.gameObject.SetActive(true);
    }

    private static RectTransform EnsurePlayerInventoryScrollbar(InventoryGrid playerGrid)
    {
        if (InventoryPanels.PlayerInventoryScrollbar != null && InventoryPanels.PlayerInventoryScrollbar.parent == playerGrid.m_gridRoot)
        {
            return InventoryPanels.PlayerInventoryScrollbar;
        }

        Transform? existing = playerGrid.m_gridRoot.Find("InventorySlots_PlayerInventoryScrollbar");
        InventoryPanels.PlayerInventoryScrollbar = existing != null ? existing.GetComponent<RectTransform>() : null;
        if (InventoryPanels.PlayerInventoryScrollbar == null)
        {
            InventoryPanels.PlayerInventoryScrollbar = new GameObject("InventorySlots_PlayerInventoryScrollbar", typeof(RectTransform), typeof(Image), typeof(Scrollbar)).GetComponent<RectTransform>();
            InventoryPanels.PlayerInventoryScrollbar.SetParent(playerGrid.m_gridRoot, false);

            Image background = InventoryPanels.PlayerInventoryScrollbar.GetComponent<Image>();
            background.color = new Color(0f, 0f, 0f, 0.45f);
            background.raycastTarget = true;

            RectTransform slidingArea = new GameObject("Sliding Area", typeof(RectTransform)).GetComponent<RectTransform>();
            slidingArea.SetParent(InventoryPanels.PlayerInventoryScrollbar, false);
            slidingArea.anchorMin = Vector2.zero;
            slidingArea.anchorMax = Vector2.one;
            slidingArea.offsetMin = new Vector2(2f, 2f);
            slidingArea.offsetMax = new Vector2(-2f, -2f);

            RectTransform handle = new GameObject("Handle", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
            handle.SetParent(slidingArea, false);
            handle.anchorMin = Vector2.zero;
            handle.anchorMax = Vector2.one;
            handle.offsetMin = Vector2.zero;
            handle.offsetMax = Vector2.zero;
            Image handleImage = handle.GetComponent<Image>();
            handleImage.color = new Color(0.75f, 0.86f, 1f, 0.85f);
            handleImage.raycastTarget = true;

            Scrollbar scrollbar = InventoryPanels.PlayerInventoryScrollbar.GetComponent<Scrollbar>();
            scrollbar.direction = Scrollbar.Direction.BottomToTop;
            scrollbar.targetGraphic = handleImage;
            scrollbar.handleRect = handle;
            scrollbar.onValueChanged.AddListener(OnPlayerInventoryScrollbarChanged);
        }

        InventoryPanels.PlayerInventoryScrollbar.SetParent(playerGrid.m_gridRoot, false);
        InventoryPanels.PlayerInventoryScrollbar.anchorMin = new Vector2(0.5f, 0.5f);
        InventoryPanels.PlayerInventoryScrollbar.anchorMax = new Vector2(0.5f, 0.5f);
        InventoryPanels.PlayerInventoryScrollbar.pivot = new Vector2(0f, 0.5f);
        InventoryPanels.PlayerInventoryScrollbar.localScale = Vector3.one;
        InventoryPanels.PlayerInventoryScrollbar.localRotation = Quaternion.identity;
        InventoryPanels.PlayerInventoryScrollbar.SetAsLastSibling();
        InventoryPanels.PlayerInventoryScrollbarComponent = InventoryPanels.PlayerInventoryScrollbar.GetComponent<Scrollbar>();
        return InventoryPanels.PlayerInventoryScrollbar;
    }

    private static void OnPlayerInventoryScrollbarChanged(float value)
    {
        if (InventoryPanels.UpdatingPlayerInventoryScrollbar || InventoryPanels.PlayerInventoryMaxScroll <= 0)
        {
            return;
        }

        InventoryPanels.PlayerInventoryScrollOffset = Mathf.Clamp(Mathf.RoundToInt((1f - value) * InventoryPanels.PlayerInventoryMaxScroll), 0, InventoryPanels.PlayerInventoryMaxScroll);
    }

    private static Vector3 GetSidePanelBasePosition(Vector3 origin, int inventoryWidth, float elementSpace)
    {
        return origin + new Vector3((inventoryWidth + SidePanelGapColumns) * elementSpace, 0f, 0f);
    }

}
