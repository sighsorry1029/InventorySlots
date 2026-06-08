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
    private static float GetAdvancedConfigFloat(ConfigEntry<float>? config, float fallback) =>
        config?.Value ?? fallback;

    private static void UpdatePlayerStatPanels(RectTransform equipmentPanel, int equipmentColumns, float elementSpace)
    {
        InventoryGui? gui = InventoryGui.instance;
        if (gui == null || equipmentPanel == null)
        {
            RestorePlayerStatPanels();
            return;
        }

        if (gui.m_armor == null || gui.m_weight == null || !equipmentPanel.gameObject.activeInHierarchy)
        {
            RestorePlayerStatPanels();
            return;
        }

        RectTransform host = EnsurePlayerStatPanelHost(equipmentPanel);
        float width = Mathf.Max(1, equipmentColumns) * elementSpace;
        host.localPosition = new Vector3(width + SidePanelBackgroundPadding + PlayerStatPanelGap, -SidePanelBackgroundPadding, 0f) + (Vector3)PlayerStatPanelsFixedOffset;
        host.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, elementSpace * 1.35f);
        host.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, elementSpace * 1.6f);
        host.gameObject.SetActive(true);
        host.SetAsFirstSibling();

        MovePlayerStatPanelGroup(gui, host);
        LayoutPlayerStatPanels(elementSpace);
        host.SetAsFirstSibling();
    }

    private static RectTransform EnsurePlayerStatPanelHost(RectTransform equipmentPanel)
    {
        if (InventoryPanels.PlayerStatPanelHost != null && !IsUnityNull(InventoryPanels.PlayerStatPanelHost) && InventoryPanels.PlayerStatPanelHost!.parent == equipmentPanel)
        {
            return InventoryPanels.PlayerStatPanelHost;
        }

        Transform? existing = equipmentPanel.Find(PlayerStatPanelHostName);
        InventoryPanels.PlayerStatPanelHost = existing != null ? existing.GetComponent<RectTransform>() : null;
        if (InventoryPanels.PlayerStatPanelHost == null)
        {
            InventoryPanels.PlayerStatPanelHost = new GameObject(PlayerStatPanelHostName, typeof(RectTransform)).GetComponent<RectTransform>();
        }

        InventoryPanels.PlayerStatPanelHost.SetParent(equipmentPanel, false);
        InventoryPanels.PlayerStatPanelHost.anchorMin = new Vector2(0f, 1f);
        InventoryPanels.PlayerStatPanelHost.anchorMax = new Vector2(0f, 1f);
        InventoryPanels.PlayerStatPanelHost.pivot = new Vector2(0f, 1f);
        InventoryPanels.PlayerStatPanelHost.localScale = Vector3.one;
        InventoryPanels.PlayerStatPanelHost.localRotation = Quaternion.identity;
        return InventoryPanels.PlayerStatPanelHost;
    }

    private static void MovePlayerStatPanelGroup(InventoryGui gui, RectTransform host)
    {
        RectTransform? armorRoot = GetPlayerStatPanelRoot(gui, gui.m_armor);
        RectTransform? weightRoot = GetPlayerStatPanelRoot(gui, gui.m_weight);
        if (armorRoot == null || weightRoot == null)
        {
            return;
        }

        if (armorRoot.parent != null && armorRoot.parent == weightRoot.parent)
        {
            Transform sourceParent = armorRoot.parent;
            int armorIndex = armorRoot.GetSiblingIndex();
            int weightIndex = weightRoot.GetSiblingIndex();
            int start = Mathf.Min(armorIndex, weightIndex);
            int end = Mathf.Max(armorIndex, weightIndex);
            List<RectTransform> roots = new();
            for (int i = start; i <= end && i < sourceParent.childCount; i++)
            {
                if (sourceParent.GetChild(i) is RectTransform sibling && ShouldMovePlayerStatSibling(gui, sibling))
                {
                    roots.Add(sibling);
                }
            }

            for (int i = 0; i < roots.Count; i++)
            {
                PlayerStatPanelKind kind = roots[i] == armorRoot ? PlayerStatPanelKind.Armor : roots[i] == weightRoot ? PlayerStatPanelKind.Weight : IsSynergyStatPanel(roots[i]) ? PlayerStatPanelKind.Synergy : PlayerStatPanelKind.Between;
                MovePlayerStatPanelRoot(roots[i], kind, host, i);
            }

            MoveNamedPlayerStatPanelExtras(gui, host, roots.Count);
            return;
        }

        MovePlayerStatPanelRoot(armorRoot, PlayerStatPanelKind.Armor, host, 0);
        MovePlayerStatPanelRoot(weightRoot, PlayerStatPanelKind.Weight, host, 1);
        MoveNamedPlayerStatPanelExtras(gui, host, 2);
    }

    private static void MoveNamedPlayerStatPanelExtras(InventoryGui gui, RectTransform host, int nextSortOrder)
    {
        Transform playerPanel = gui.m_player;
        foreach (string name in PlayerStatPanelExtraNames)
        {
            if (playerPanel.Find(name) is RectTransform extra && ShouldMovePlayerStatSibling(gui, extra))
            {
                MovePlayerStatPanelRoot(extra, IsSynergyStatPanel(extra) ? PlayerStatPanelKind.Synergy : PlayerStatPanelKind.Between, host, nextSortOrder++);
            }
        }

        int quickButtonOrder = Math.Max(nextSortOrder, 100);
        foreach (string name in QuickStackStoreMiniButtonNames)
        {
            if (playerPanel.Find(name) is RectTransform quickButton)
            {
                MovePlayerStatPanelRoot(quickButton, PlayerStatPanelKind.QuickStackMiniButton, host, quickButtonOrder++);
            }
        }
    }

    private static void MovePlayerStatPanelRoot(RectTransform root, PlayerStatPanelKind kind, RectTransform host, int sortOrder)
    {
        MovedPlayerStatPanel? moved = InventoryPanels.MovedPlayerStatPanels.FirstOrDefault(panel => panel.Rect == root && !IsUnityNull(panel.Rect));
        if (moved == null)
        {
            moved = new MovedPlayerStatPanel(kind, root, sortOrder);
            InventoryPanels.MovedPlayerStatPanels.Add(moved);
        }
        else
        {
            moved.Kind = kind;
            moved.SortOrder = sortOrder;
        }

        root.SetParent(host, false);
    }

    private static bool ShouldMovePlayerStatSibling(InventoryGui gui, RectTransform sibling)
    {
        if (sibling == null ||
            sibling == InventoryPanels.PlayerStatPanelHost ||
            sibling.name.StartsWith("InventorySlots_", StringComparison.Ordinal) ||
            string.Equals(sibling.name, CurrencyPocketPanelName, StringComparison.Ordinal))
        {
            return false;
        }

        if (gui.m_pvp != null && (sibling.transform == gui.m_pvp.transform || gui.m_pvp.transform.IsChildOf(sibling)))
        {
            return false;
        }

        return true;
    }

    private static bool IsSynergyStatPanel(RectTransform rect)
    {
        return rect != null && rect.name.IndexOf("Synergy", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static RectTransform? GetPlayerStatPanelRoot(InventoryGui gui, TMP_Text text)
    {
        if (text == null)
        {
            return null;
        }

        MovedPlayerStatPanel? moved = InventoryPanels.MovedPlayerStatPanels.FirstOrDefault(panel => !IsUnityNull(panel.Rect) && text.transform.IsChildOf(panel.Rect));
        if (moved != null)
        {
            return moved.Rect;
        }

        RectTransform? textRect = text.GetComponent<RectTransform>();
        RectTransform? candidate = textRect;
        Transform cursor = text.transform;
        while (cursor.parent != null && cursor.parent != gui.m_infoPanel && cursor.parent != gui.m_player)
        {
            cursor = cursor.parent;
            if (cursor is RectTransform rect)
            {
                candidate = rect;
            }
        }

        return candidate;
    }

    private static void LayoutPlayerStatPanels(float elementSpace)
    {
        int row = 0;
        int quickButtonIndex = 0;
        foreach (MovedPlayerStatPanel panel in InventoryPanels.MovedPlayerStatPanels.OrderBy(panel => panel.SortOrder).ThenBy(panel => panel.Kind))
        {
            if (IsUnityNull(panel.Rect))
            {
                continue;
            }

            panel.Rect.anchorMin = new Vector2(0f, 1f);
            panel.Rect.anchorMax = new Vector2(0f, 1f);
            panel.Rect.pivot = new Vector2(0f, 1f);
            panel.Rect.localScale = Vector3.one;
            panel.Rect.localRotation = Quaternion.identity;
            if (panel.Kind == PlayerStatPanelKind.QuickStackMiniButton)
            {
                panel.Rect.localPosition = new Vector3(quickButtonIndex * 40f, -row * elementSpace * 0.72f, 0f);
                quickButtonIndex++;
            }
            else
            {
                panel.Rect.localPosition = new Vector3(0f, -row * elementSpace * 0.72f, 0f) + GetPlayerStatPanelOffset(panel.Kind);
                row++;
            }

            panel.Rect.SetAsLastSibling();
        }
    }

    private static Vector3 GetPlayerStatPanelOffset(PlayerStatPanelKind kind)
    {
        return kind switch
        {
            PlayerStatPanelKind.Armor => ArmorPanelFixedOffset,
            PlayerStatPanelKind.Weight => WeightPanelFixedOffset,
            PlayerStatPanelKind.Synergy => SynergyPanelFixedOffset,
            _ => Vector3.zero
        };
    }

    internal static void RestorePlayerStatPanels()
    {
        for (int i = InventoryPanels.MovedPlayerStatPanels.Count - 1; i >= 0; i--)
        {
            InventoryPanels.MovedPlayerStatPanels[i].Restore();
        }

        InventoryPanels.MovedPlayerStatPanels.Clear();
        if (InventoryPanels.PlayerStatPanelHost != null && !IsUnityNull(InventoryPanels.PlayerStatPanelHost))
        {
            InventoryPanels.PlayerStatPanelHost!.gameObject.SetActive(false);
        }
    }

}
