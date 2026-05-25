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
    private static void UpdateInventoryActionPanels(InventoryGrid playerGrid, Player player, Vector3 gridOrigin, int viewportRows)
    {
        InventoryGui gui = InventoryGui.instance;
        if (gui == null || !InventoryGui.IsVisible())
        {
            HideInventoryActionPanels();
            return;
        }

        UpdateContainerActionPanel(gui);
        UpdateInventorySortPanel(gui, playerGrid, player, gridOrigin, viewportRows);
        UpdateInventoryTrashPanel(gui, playerGrid, player, gridOrigin, viewportRows);
        UpdateCurrencyPocketPanel(gui, playerGrid, gridOrigin);
        RaiseInventorySortPanel();
    }

    private static void UpdateContainerActionPanel(InventoryGui gui)
    {
        if (gui.m_currentContainer == null || gui.m_takeAllButton == null || gui.m_stackAllButton == null)
        {
            RestoreContainerActionButtonLayout();
            HideContainerActionButtons();
            return;
        }

        Container currentContainer = gui.m_currentContainer;
        RectTransform? parent = gui.m_takeAllButton.transform.parent as RectTransform;
        RectTransform? stackParent = gui.m_stackAllButton.transform.parent as RectTransform;
        if (parent == null || stackParent == null)
        {
            RestoreContainerActionButtonLayout();
            HideContainerActionButtons();
            return;
        }

        CaptureRectTransformSnapshot(ref InventoryPanels.TakeAllButtonOriginal, (RectTransform)gui.m_takeAllButton.transform);
        CaptureRectTransformSnapshot(ref InventoryPanels.StackAllButtonOriginal, (RectTransform)gui.m_stackAllButton.transform);
        RestoreContainerActionButtonLayout();

        Button template = gui.m_takeAllButton;
        RectTransform takeAllRect = (RectTransform)gui.m_takeAllButton.transform;
        RectTransform stackAllRect = (RectTransform)gui.m_stackAllButton.transform;
        bool canMutateDirectly = CanMutateContainerDirectly(currentContainer, allowLocalWithoutZNetView: true);
        bool canRequestSort = canMutateDirectly || CanUseContainerThroughOwnerOrMultiUserChest(currentContainer);
        SetActionButtonLabel(gui.m_takeAllButton, LocalizeUi("$inventoryslots_button_take_all", "Take all"));
        SetActionButtonLabel(gui.m_stackAllButton, LocalizeUi("$inventoryslots_button_place_stacks", "Place stacks"));

        if (!canMutateDirectly)
        {
            HideContainerMutationButtons();
            InventoryPanels.ContainerSortButton = EnsureContainerSortButton(stackAllRect, template, GetContainerSortButtonSize(gui), (Vector3)ContainerSortButtonFixedOffset);
            SetButtonActive(InventoryPanels.ContainerSortButton, canRequestSort);
            SetButtonInteractable(InventoryPanels.ContainerSortButton, canRequestSort);
            return;
        }

        AlignContainerActionButtonRows(takeAllRect, stackAllRect);
        float buttonHeight = Mathf.Clamp(Mathf.Max(GetRectHeight(takeAllRect), GetRectHeight(stackAllRect)), 24f, 36f);
        const float gap = 4f;
        Vector3 offset = Vector3.zero;
        float takeAllWidth = GetRectWidth(takeAllRect);
        float stackAllWidth = GetRectWidth(stackAllRect);

        InventoryPanels.ContainerStoreAllButton = EnsureActionButton(parent, template, "InventorySlots_StoreAllButton", LocalizeUi("$inventoryslots_button_place_all", "Place all"), () => StoreAllToCurrentContainer(Player.m_localPlayer));
        LayoutButtonPair(takeAllRect, InventoryPanels.ContainerStoreAllButton, takeAllWidth, buttonHeight, gap, true, offset);

        InventoryPanels.ContainerRestockButton = EnsureActionButton(stackParent, gui.m_stackAllButton, "InventorySlots_RestockButton", LocalizeUi("$inventoryslots_button_take_stacks", "Take stacks"), () => RestockFromCurrentContainer(Player.m_localPlayer));
        LayoutButtonPair(stackAllRect, InventoryPanels.ContainerRestockButton, stackAllWidth, buttonHeight, gap, false, offset);

        InventoryPanels.ContainerSortButton = EnsureContainerSortButton(stackAllRect, template, buttonHeight, offset + (Vector3)ContainerSortButtonFixedOffset);
        SetButtonActive(InventoryPanels.ContainerStoreAllButton, true);
        SetButtonActive(InventoryPanels.ContainerRestockButton, true);
        SetButtonActive(InventoryPanels.ContainerSortButton, true);
        SetButtonInteractable(InventoryPanels.ContainerStoreAllButton, true);
        SetButtonInteractable(InventoryPanels.ContainerRestockButton, true);
        SetButtonInteractable(InventoryPanels.ContainerSortButton, true);
    }

    private static void AlignContainerActionButtonRows(RectTransform takeAllRect, RectTransform stackAllRect)
    {
        if (takeAllRect == null || stackAllRect == null)
        {
            return;
        }

        float rowY = Mathf.Max(takeAllRect.localPosition.y, stackAllRect.localPosition.y);
        takeAllRect.localPosition = new Vector3(takeAllRect.localPosition.x, rowY, takeAllRect.localPosition.z);
        stackAllRect.localPosition = new Vector3(stackAllRect.localPosition.x, rowY, stackAllRect.localPosition.z);
    }

    private static void UpdateInventorySortPanel(InventoryGui gui, InventoryGrid playerGrid, Player player, Vector3 gridOrigin, int viewportRows)
    {
        if (playerGrid.m_gridRoot == null || gui.m_takeAllButton == null)
        {
            SetActionPanelActive(InventoryPanels.InventorySortPanel, false);
            return;
        }

        InventoryPanels.InventorySortPanel = EnsureActionPanel(playerGrid.m_gridRoot, InventorySortPanelName, InventoryPanels.InventorySortPanel);
        if (InventoryPanels.InventorySortPanel == null)
        {
            return;
        }

        float buttonHeight = GetContainerSortButtonSize(gui);
        float buttonWidth = buttonHeight;
        const float gap = 6f;
        int buttonCount = 1;
        float totalWidth = buttonCount * buttonWidth + Mathf.Max(0, buttonCount - 1) * gap;
        InventoryPanels.InventorySortPanel.sizeDelta = new Vector2(totalWidth, buttonHeight);
        if (InventoryPanels.InventorySortPanel.localScale != Vector3.one)
        {
            InventoryPanels.InventorySortPanel.localScale = Vector3.one;
        }

        if (InventoryPanels.InventorySortPanel.localRotation != Quaternion.identity)
        {
            InventoryPanels.InventorySortPanel.localRotation = Quaternion.identity;
        }

        Vector3 sortPanelPosition =
            gridOrigin + new Vector3(InventoryWidth * playerGrid.m_elementSpace + SortButtonOutsideGap, -Mathf.Max(1, viewportRows) * playerGrid.m_elementSpace + buttonHeight, 0f) + (Vector3)InventorySortButtonFixedOffset;
        if ((InventoryPanels.InventorySortPanel.localPosition - sortPanelPosition).sqrMagnitude > 0.0001f)
        {
            InventoryPanels.InventorySortPanel.localPosition = sortPanelPosition;
        }

        DisableActionPanelChildren(InventoryPanels.InventorySortPanel);

        int index = 0;
        Button? sortButton = EnsureActionButton(InventoryPanels.InventorySortPanel, gui.m_takeAllButton, "InventorySlots_PlayerSortButton", "S", () => SortPlayerInventory(Player.m_localPlayer));
        LayoutActionButton(sortButton, index, buttonWidth, buttonHeight, gap);

        SetActionPanelActive(InventoryPanels.InventorySortPanel, true);
    }

    private static void RaiseInventorySortPanel()
    {
        if (InventoryPanels.InventorySortPanel == null || IsUnityNull(InventoryPanels.InventorySortPanel) || !InventoryPanels.InventorySortPanel.gameObject.activeInHierarchy)
        {
            return;
        }

        if (InventoryPanels.InventorySortPanel.GetSiblingIndex() != InventoryPanels.InventorySortPanel.parent.childCount - 1)
        {
            InventoryPanels.InventorySortPanel.SetAsLastSibling();
        }
    }

    private static void UpdateCurrencyPocketPanel(InventoryGui gui, InventoryGrid playerGrid, Vector3 gridOrigin)
    {
        if (!HasPlugin(CurrencyPocketGuid) ||
            playerGrid.m_gridRoot == null)
        {
            return;
        }

        RectTransform? pocket = FindCurrencyPocketPanel(gui, playerGrid);
        if (pocket == null)
        {
            return;
        }

        if (pocket.parent != playerGrid.m_gridRoot)
        {
            pocket.SetParent(playerGrid.m_gridRoot, false);
        }

        pocket.anchorMin = new Vector2(0f, 1f);
        pocket.anchorMax = new Vector2(0f, 1f);
        pocket.pivot = new Vector2(0f, 1f);
        pocket.localScale = Vector3.one;
        pocket.localRotation = Quaternion.identity;

        int inventoryWidth = playerGrid.m_inventory != null ? playerGrid.m_inventory.GetWidth() : playerGrid.m_width;
        float elementSpace = Mathf.Max(1f, playerGrid.m_elementSpace);
        float pocketHeight = Mathf.Max(1f, GetRectHeight(pocket));
        int rowIndex = Mathf.Max(0, CurrencyPocketInventoryRow - 1);
        float centeredRowOffset = Mathf.Max(0f, (elementSpace - pocketHeight) * 0.5f);
        pocket.localPosition = gridOrigin + new Vector3(
            Mathf.Max(1, inventoryWidth) * elementSpace + CurrencyPocketOutsideGap,
            -rowIndex * elementSpace - centeredRowOffset,
            0f);
        pocket.SetAsLastSibling();
        pocket.gameObject.SetActive(true);
    }

    private static RectTransform? FindCurrencyPocketPanel(InventoryGui gui, InventoryGrid playerGrid)
    {
        if (InventoryPanels.CurrencyPocketPanel != null && !IsUnityNull(InventoryPanels.CurrencyPocketPanel))
        {
            return InventoryPanels.CurrencyPocketPanel;
        }

        InventoryPanels.CurrencyPocketPanel =
            FindCurrencyPocketPanelUnder(playerGrid.m_gridRoot) ??
            FindCurrencyPocketPanelUnder(gui.m_player) ??
            FindCurrencyPocketPanelUnder(InventoryPanels.PlayerStatPanelHost);

        return InventoryPanels.CurrencyPocketPanel;
    }

    private static RectTransform? FindCurrencyPocketPanelUnder(Transform? parent)
    {
        if (parent == null || IsUnityNull(parent))
        {
            return null;
        }

        Transform child = parent.Find(CurrencyPocketPanelName);
        return child != null && !IsUnityNull(child) ? child.GetComponent<RectTransform>() : null;
    }

    private static RectTransform? LayoutActionButton(Button? button, int index, float buttonWidth, float buttonHeight, float gap)
    {
        if (button == null)
        {
            return null;
        }

        RectTransform rect = (RectTransform)button.transform;
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.sizeDelta = new Vector2(buttonWidth, buttonHeight);
        rect.anchoredPosition = new Vector2(index * (buttonWidth + gap), 0f);
        rect.localScale = Vector3.one;
        rect.localRotation = Quaternion.identity;
        button.gameObject.SetActive(true);
        return rect;
    }

    private static void LayoutButtonPair(RectTransform vanillaButton, Button? addedButton, float originalWidth, float buttonHeight, float gap, bool addedButtonOnRight, Vector3 offset)
    {
        if (vanillaButton == null || addedButton == null)
        {
            return;
        }

        RectTransform addedRect = (RectTransform)addedButton.transform;
        CopyRectTransformFrame(vanillaButton, addedRect);
        float width = Mathf.Max(1f, (originalWidth - gap) * 0.5f) * ContainerActionPairButtonWidthMultiplier;
        float shift = (width + gap) * 0.5f;
        Vector3 origin = vanillaButton.localPosition + offset;
        Vector3 vanillaPosition = origin + new Vector3(addedButtonOnRight ? -shift : shift, 0f, 0f);
        Vector3 addedPosition = origin + new Vector3(addedButtonOnRight ? shift : -shift, 0f, 0f);

        SetActionButtonTextAutoSize(vanillaButton.GetComponent<Button>());
        SetActionButtonTextAutoSize(addedButton);
        LayoutButtonRect(vanillaButton, width, buttonHeight, vanillaPosition);
        LayoutButtonRect(addedRect, width, buttonHeight, addedPosition);
    }

    private static Button? EnsureContainerSortButton(RectTransform stackButton, Button template, float buttonHeight, Vector3 offset)
    {
        RectTransform? parent = stackButton?.parent as RectTransform ?? template.transform.parent as RectTransform;
        if (parent == null)
        {
            return null;
        }

        Button? sortButton = EnsureActionButton(parent, template, "InventorySlots_SortButton", "S", () => SortCurrentContainer(Player.m_localPlayer));
        if (sortButton == null)
        {
            return null;
        }

        RectTransform sortRect = (RectTransform)sortButton.transform;
        float sortWidth = buttonHeight;
        RectTransform anchor = stackButton ?? (RectTransform)template.transform;
        CopyRectTransformFrame(anchor, sortRect);
        LayoutButtonRect(sortRect, sortWidth, buttonHeight, anchor.localPosition + new Vector3(GetRectWidth(anchor) * 0.5f + SortButtonOutsideGap + sortWidth * 0.5f, 0f, 0f) + offset);
        return sortButton;
    }

    private static float GetContainerSortButtonSize(InventoryGui gui)
    {
        RectTransform? takeAllRect = gui.m_takeAllButton != null ? gui.m_takeAllButton.transform as RectTransform : null;
        RectTransform? stackAllRect = gui.m_stackAllButton != null ? gui.m_stackAllButton.transform as RectTransform : null;
        if (takeAllRect == null && stackAllRect == null)
        {
            return 32f;
        }

        float takeAllHeight = takeAllRect != null ? GetRectHeight(takeAllRect) : 0f;
        float stackAllHeight = stackAllRect != null ? GetRectHeight(stackAllRect) : 0f;
        return Mathf.Clamp(Mathf.Max(takeAllHeight, stackAllHeight), 24f, 36f);
    }

    private static void LayoutButtonRect(RectTransform rect, float width, float height, Vector3 localPosition)
    {
        rect.sizeDelta = new Vector2(width, height);
        rect.localPosition = localPosition;
        rect.localScale = Vector3.one;
        rect.localRotation = Quaternion.identity;
    }

    private static void CopyRectTransformFrame(RectTransform source, RectTransform target)
    {
        target.anchorMin = source.anchorMin;
        target.anchorMax = source.anchorMax;
        target.pivot = source.pivot;
        target.offsetMin = source.offsetMin;
        target.offsetMax = source.offsetMax;
        target.localScale = Vector3.one;
        target.localRotation = Quaternion.identity;
        target.localPosition = source.localPosition;
    }

    private static void DisableActionPanelChildren(RectTransform panel)
    {
        for (int i = 0; i < panel.childCount; i++)
        {
            Transform child = panel.GetChild(i);
            if (child.name.StartsWith("InventorySlots_", StringComparison.Ordinal))
            {
                child.gameObject.SetActive(false);
            }
        }
    }

    private static RectTransform? EnsureActionPanel(RectTransform parent, string name, RectTransform? cached)
    {
        RectTransform? panel = cached;
        if (panel == null)
        {
            Transform existing = parent.Find(name);
            panel = existing as RectTransform;
        }

        if (panel == null)
        {
            GameObject go = new(name, typeof(RectTransform));
            panel = (RectTransform)go.transform;
        }

        if (panel.parent != parent)
        {
            panel.SetParent(parent, false);
        }

        panel.name = name;
        panel.anchorMin = new Vector2(0f, 1f);
        panel.anchorMax = new Vector2(0f, 1f);
        panel.pivot = new Vector2(0f, 1f);
        panel.localScale = Vector3.one;
        panel.localRotation = Quaternion.identity;
        return panel;
    }

    private static Button? EnsureActionButton(RectTransform panel, Button template, string name, string label, UnityAction action)
    {
        Transform existing = panel.Find(name);
        Button? button = existing != null ? existing.GetComponent<Button>() : null;
        if (button == null)
        {
            button = UnityEngine.Object.Instantiate(template, panel, false);
            button.name = name;
            InventoryActionButtonMarker marker = button.gameObject.GetComponent<InventoryActionButtonMarker>() ?? button.gameObject.AddComponent<InventoryActionButtonMarker>();
            marker.Initialized = false;
        }

        InventoryActionButtonMarker buttonMarker = button.gameObject.GetComponent<InventoryActionButtonMarker>() ?? button.gameObject.AddComponent<InventoryActionButtonMarker>();
        if (!buttonMarker.Initialized)
        {
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(action);
            buttonMarker.Initialized = true;
        }

        SetActionButtonLabel(button, label);
        return button;
    }

    private static void SetActionButtonLabel(Button button, string label)
    {
        InventoryActionButtonMarker marker = button.gameObject.GetComponent<InventoryActionButtonMarker>() ?? button.gameObject.AddComponent<InventoryActionButtonMarker>();
        string signature = $"{label}|{_uiLocalizationVersion}";
        if (marker.AutoSizeInitialized && string.Equals(marker.LabelSignature, signature, StringComparison.Ordinal))
        {
            return;
        }

        SetActionButtonTextAutoSize(button);
        foreach (TMP_Text text in button.GetComponentsInChildren<TMP_Text>(true))
        {
            ApplyDefaultFontAsset(text);
            if (text.text != label)
            {
                text.text = label;
            }
        }

        foreach (UnityEngine.UI.Text text in button.GetComponentsInChildren<UnityEngine.UI.Text>(true))
        {
            if (text.text != label)
            {
                text.text = label;
            }
        }

        marker.LabelSignature = signature;
    }

    private static void SetActionButtonTextAutoSize(Button? button)
    {
        if (button == null)
        {
            return;
        }

        InventoryActionButtonMarker marker = button.gameObject.GetComponent<InventoryActionButtonMarker>() ?? button.gameObject.AddComponent<InventoryActionButtonMarker>();
        if (marker.AutoSizeInitialized)
        {
            return;
        }

        foreach (TMP_Text text in button.GetComponentsInChildren<TMP_Text>(true))
        {
            ApplyDefaultFontAsset(text);
            text.enableAutoSizing = true;
            text.fontSizeMin = 10f;
            text.fontSizeMax = Mathf.Min(Mathf.Max(text.fontSize, 14f), 16f);
            text.alignment = TextAlignmentOptions.Center;
        }

        marker.AutoSizeInitialized = true;
    }

    private static void SetActionPanelActive(RectTransform? panel, bool active)
    {
        if (panel != null && panel.gameObject.activeSelf != active)
        {
            panel.gameObject.SetActive(active);
        }
    }

    internal static void HideInventoryActionPanels()
    {
        SetActionPanelActive(InventoryPanels.InventorySortPanel, false);
        SetActionPanelActive(_inventoryTrashPanel, false);
        CloseInventoryTrashConfirmDialog();
        HideInventorySideHints();
        RestoreContainerActionButtonLayout();
        HideContainerActionButtons();
    }












































    private static string CleanPrefabName(string name)
    {
        return InventorySlotsConfigCore.CleanPrefabName(name);
    }

















    internal static Sprite? GetItemPrefabIcon(string prefabName)
    {
        if (string.IsNullOrWhiteSpace(prefabName))
        {
            return null;
        }

        GameObject? prefab = ObjectDB.instance != null ? ObjectDB.instance.GetItemPrefab(prefabName) : null;
        if (prefab != null && prefab.TryGetComponent(out ItemDrop itemDrop))
        {
            return itemDrop.m_itemData.GetIcon();
        }

        prefab = ZNetScene.instance != null ? ZNetScene.instance.GetPrefab(prefabName) : null;
        if (prefab == null)
        {
            return null;
        }

        if (prefab.TryGetComponent(out ItemDrop sceneItemDrop))
        {
            return sceneItemDrop.m_itemData.GetIcon();
        }

        if (prefab.TryGetComponent(out Piece piece))
        {
            return piece.m_icon;
        }

        return null;
    }

    internal static Sprite? GetSkillIcon(int skillType)
    {
        Player? player = Player.m_localPlayer;
        if (player == null)
        {
            return null;
        }

        try
        {
            Skills.SkillDef skillDef = player.GetSkills().GetSkillDef((Skills.SkillType)skillType);
            return skillDef != null ? skillDef.m_icon : null;
        }
        catch
        {
            return null;
        }
    }

    private static void CaptureRectTransformSnapshot(ref RectTransformSnapshot? snapshot, RectTransform rect)
    {
        if (rect == null)
        {
            return;
        }

        if (snapshot == null || snapshot.Rect != rect)
        {
            snapshot = new RectTransformSnapshot(rect);
        }
    }

    private static void RestoreContainerActionButtonLayout()
    {
        InventoryPanels.TakeAllButtonOriginal?.Restore();
        InventoryPanels.StackAllButtonOriginal?.Restore();
    }

    private static void HideContainerActionButtons()
    {
        SetButtonActive(InventoryPanels.ContainerRestockButton, false);
        SetButtonActive(InventoryPanels.ContainerStoreAllButton, false);
        SetButtonActive(InventoryPanels.ContainerSortButton, false);
    }

    private static void HideContainerMutationButtons()
    {
        SetButtonActive(InventoryPanels.ContainerRestockButton, false);
        SetButtonActive(InventoryPanels.ContainerStoreAllButton, false);
    }

    private static void SetButtonActive(Button? button, bool active)
    {
        if (button != null && button.gameObject.activeSelf != active)
        {
            button.gameObject.SetActive(active);
        }
    }

    private static void SetButtonInteractable(Button? button, bool interactable)
    {
        if (button != null && button.interactable != interactable)
        {
            button.interactable = interactable;
        }
    }

    private static float GetRectWidth(RectTransform rect)
    {
        if (rect.rect.width > 0f)
        {
            return rect.rect.width;
        }

        return rect.sizeDelta.x > 0f ? rect.sizeDelta.x : 120f;
    }

    private static float GetRectHeight(RectTransform rect)
    {
        if (rect.rect.height > 0f)
        {
            return rect.rect.height;
        }

        return rect.sizeDelta.y > 0f ? rect.sizeDelta.y : 30f;
    }

}
