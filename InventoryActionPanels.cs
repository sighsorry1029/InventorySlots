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
        if (gui == null || !InventoryGui.IsVisible() || IsInventoryPanelClosing(gui))
        {
            HideInventoryActionPanels(preservePlayerButtons: IsInventoryPanelClosing(gui));
            return;
        }

        UpdateContainerActionPanel(gui);
        UpdateInventorySortPanel(gui, playerGrid, player, gridOrigin, viewportRows);
        UpdateInventoryTrashPanel(gui, playerGrid, player, gridOrigin, viewportRows);
        UpdateItemRuleUi(gui, gridOrigin, viewportRows);
        UpdateCurrencyPocketPanel(gui, playerGrid, gridOrigin);
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
        ContainerAccessMode accessMode = GetContainerAccessMode(currentContainer, allowLocalWithoutZNetView: true);
        bool canMutateDirectly = accessMode == ContainerAccessMode.DirectOwner;
        bool canRequestSort = accessMode != ContainerAccessMode.Unavailable;
        SetActionButtonLabel(gui.m_takeAllButton, LocalizeUi("$inventoryslots_button_take_all", "Take all"));
        SetActionButtonLabel(gui.m_stackAllButton, LocalizeUi("$inventoryslots_button_place_stacks", "Place stacks"));

        if (!canMutateDirectly)
        {
            HideContainerMutationButtons();
            InventoryPanels.ContainerSortButton = EnsureContainerSortButton(stackAllRect, template, GetContainerSortButtonSize(gui), (Vector3)ContainerSortButtonFixedOffset);
            RegisterControllerSortButton(gui, true, InventoryPanels.ContainerSortButton);
            SetButtonActive(InventoryPanels.ContainerSortButton, canRequestSort);
            SetButtonInteractable(InventoryPanels.ContainerSortButton, canRequestSort && !IsContainerSortRequestPending(currentContainer));
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
        RegisterControllerSortButton(gui, true, InventoryPanels.ContainerSortButton);
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

        RectTransform host = EnsureSortButtonHost(gui, InventoryPanels.PlayerSortHost, playerGrid.m_gridRoot, "PlayerSortHost");
        InventoryPanels.InventorySortPanel = EnsureActionPanel(host, InventorySortPanelName, InventoryPanels.InventorySortPanel);
        if (InventoryPanels.InventorySortPanel == null)
        {
            return;
        }

        float buttonHeight = GetContainerSortButtonSize(gui);
        float buttonWidth = buttonHeight;
        InventoryPanels.InventorySortPanel.sizeDelta = new Vector2(buttonWidth, buttonHeight);
        if (InventoryPanels.InventorySortPanel.localScale != Vector3.one)
        {
            InventoryPanels.InventorySortPanel.localScale = Vector3.one;
        }

        if (InventoryPanels.InventorySortPanel.localRotation != Quaternion.identity)
        {
            InventoryPanels.InventorySortPanel.localRotation = Quaternion.identity;
        }

        float outsideOffset = SortButtonOutsideGap + InventorySortButtonFixedOffset.x;
        float sortX = SortButtonsOnLeft ? -buttonWidth - outsideOffset : InventoryWidth * playerGrid.m_elementSpace + outsideOffset;
        Vector3 sortPanelPosition = gridOrigin + new Vector3(sortX,
            -Mathf.Max(1, viewportRows) * playerGrid.m_elementSpace + buttonHeight + InventorySortButtonFixedOffset.y, 0f);
        sortPanelPosition.x = GetInventorySortPanelX(gui, playerGrid.m_gridRoot, buttonWidth, sortPanelPosition.x);
        if ((InventoryPanels.InventorySortPanel.localPosition - sortPanelPosition).sqrMagnitude > 0.0001f)
        {
            InventoryPanels.InventorySortPanel.localPosition = sortPanelPosition;
        }

        DisableActionPanelChildren(InventoryPanels.InventorySortPanel, "InventorySlots_PlayerSortButton");

        Button? sortButton = EnsureActionButton(InventoryPanels.InventorySortPanel, gui.m_takeAllButton, "InventorySlots_PlayerSortButton", "S", () => SortPlayerInventory(Player.m_localPlayer));
        RegisterControllerSortButton(gui, false, sortButton);
        LayoutActionButton(sortButton, buttonWidth, buttonHeight);

        SetActionPanelActive(InventoryPanels.InventorySortPanel, true);
    }

    private static float GetInventorySortPanelX(InventoryGui gui, RectTransform gridRoot, float buttonWidth, float fallback)
    {
        Button anchorButton = SortButtonsOnLeft ? gui.m_takeAllButton : gui.m_stackAllButton;
        if (gui.m_player == null || gui.m_container == null || anchorButton == null) return fallback;
        RectTransform anchor = (RectTransform)anchorButton.transform;
        if (anchor.parent == null) return fallback;

        // Match the container's edge inset, not its animated world position. The
        // native anchors exist while the chest is closed, and splitting its action
        // buttons preserves the outer edges, so opening a chest does not move S.
        Vector3 containerCenter = GetContainerSortButtonPosition(anchor, buttonWidth, (Vector3)ContainerSortButtonFixedOffset);
        float containerX = gui.m_container.InverseTransformPoint(anchor.parent.TransformPoint(containerCenter)).x;
        float containerEdge = SortButtonsOnLeft ? gui.m_container.rect.xMin : gui.m_container.rect.xMax;
        float playerEdge = SortButtonsOnLeft ? gui.m_player.rect.xMin : gui.m_player.rect.xMax;
        Vector3 playerCenter = gui.m_player.TransformPoint(new Vector3(playerEdge + containerX - containerEdge, 0f, 0f));
        float x = gridRoot.InverseTransformPoint(playerCenter).x - buttonWidth * 0.5f;
        return float.IsNaN(x) || float.IsInfinity(x) ? fallback : x;
    }

    private static void OnSortButtonSideChanged(object sender, EventArgs args)
    {
        ResetInventoryButtonNavigation();
        // Normal visible-grid layout applies the new side on the next frame.
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

    private static RectTransform? LayoutActionButton(Button? button, float buttonWidth, float buttonHeight)
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
        rect.anchoredPosition = Vector2.zero;
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
        RectTransform anchor = SortButtonsOnLeft ? (RectTransform)template.transform : stackButton;
        RectTransform? parent = anchor.parent as RectTransform;
        if (parent == null)
        {
            return null;
        }

        RectTransform host = EnsureSortButtonHost(InventoryGui.instance, InventoryPanels.ContainerSortHost, parent, "ContainerSortHost");
        Button? sortButton = EnsureActionButton(host, template, "InventorySlots_SortButton", "S", () => SortCurrentContainer(Player.m_localPlayer));
        if (sortButton == null)
        {
            return null;
        }

        RectTransform sortRect = (RectTransform)sortButton.transform;
        float sortWidth = buttonHeight;
        CopyRectTransformFrame(anchor, sortRect);
        LayoutButtonRect(sortRect, sortWidth, buttonHeight, GetContainerSortButtonPosition(anchor, sortWidth, offset));
        return sortButton;
    }

    private static Vector3 GetContainerSortButtonPosition(RectTransform anchor, float width, Vector3 offset)
    {
        float direction = SortButtonsOnLeft ? -1f : 1f;
        return anchor.localPosition + new Vector3(direction * (GetRectWidth(anchor) * 0.5f + SortButtonOutsideGap + width * 0.5f), 0f, 0f) + offset;
    }

    // Only the sort controls leave their original hierarchy. These two hosts
    // preserve its coordinate system, animation, visibility and CanvasGroup input
    // gates while allowing sibling ordering above the separate quick-slot panel.
    private sealed class SortButtonHost
    {
        internal RectTransform? Rect;
        internal RectTransform? Source;
        internal Transform? SourceParent;
        internal CanvasGroup? Group;
        internal CanvasGroup[] SourceGroups = Array.Empty<CanvasGroup>();

        internal void Follow()
        {
            if (Rect == null) return;
            bool active = Source != null && Source.gameObject.activeInHierarchy;
            SetActionPanelActive(Rect, active);
            if (!active) return;
            if (Rect.pivot != Source!.pivot) Rect.pivot = Source.pivot;
            if (Rect.sizeDelta != Source.rect.size) Rect.sizeDelta = Source.rect.size;
            if (Rect.position != Source.position) Rect.position = Source.position;
            if (Rect.rotation != Source.rotation) Rect.rotation = Source.rotation;
            Vector3 sourceScale = Source.lossyScale, parentScale = Rect.parent.lossyScale;
            Vector3 scale = new(
                Mathf.Abs(parentScale.x) > 0.0001f ? sourceScale.x / parentScale.x : 1f,
                Mathf.Abs(parentScale.y) > 0.0001f ? sourceScale.y / parentScale.y : 1f,
                Mathf.Abs(parentScale.z) > 0.0001f ? sourceScale.z / parentScale.z : 1f);
            if (Rect.localScale != scale) Rect.localScale = scale;
            float alpha = 1f;
            bool interactable = true, raycasts = true, ignoreParents = false;
            foreach (CanvasGroup sourceGroup in SourceGroups)
            {
                if (sourceGroup == null || !sourceGroup.isActiveAndEnabled) continue;
                alpha *= sourceGroup.alpha;
                interactable &= sourceGroup.interactable;
                raycasts &= sourceGroup.blocksRaycasts;
                if (sourceGroup.ignoreParentGroups) { ignoreParents = true; break; }
            }
            if (Group!.alpha != alpha) Group.alpha = alpha;
            if (Group.interactable != interactable) Group.interactable = interactable;
            if (Group.blocksRaycasts != raycasts) Group.blocksRaycasts = raycasts;
            if (Group.ignoreParentGroups != ignoreParents) Group.ignoreParentGroups = ignoreParents;
        }

        internal void Clear()
        {
            Rect = Source = null;
            SourceParent = null;
            Group = null;
            SourceGroups = Array.Empty<CanvasGroup>();
        }
    }

    private static RectTransform EnsureSortButtonHost(InventoryGui gui, SortButtonHost host, RectTransform source, string name)
    {
        if (InventoryPanels.SortButtonLayer == null || InventoryPanels.SortButtonLayer.parent != gui.m_inventoryRoot)
        {
            ClearSortButtonLayer();
            InventoryPanels.SortButtonLayer = EnsureActionPanel((RectTransform)gui.m_inventoryRoot, "InventorySlots_SortButtons", null);
        }
        if (host.Rect == null)
        {
            host.Rect = EnsureActionPanel(InventoryPanels.SortButtonLayer!, name, null);
            host.Group = host.Rect!.gameObject.AddComponent<CanvasGroup>();
        }
        if (host.Source != source || host.SourceParent != source.parent)
        {
            host.Source = source;
            host.SourceParent = source.parent;
            var groups = new List<CanvasGroup>();
            // The common inventory root is still inherited; do not multiply its
            // opacity twice. Rebind only when the source hierarchy is replaced.
            for (Transform node = source; node != null && node != gui.m_inventoryRoot; node = node.parent)
                groups.AddRange(node.GetComponents<CanvasGroup>());
            host.SourceGroups = groups.ToArray();
        }
        // Synchronize dimensions before assigning child local positions. LateUpdate
        // follows any subsequent native animation; unchanged values cause no writes.
        host.Follow();
        return host.Rect!;
    }

    private static void ClearSortButtonLayer()
    {
        if (InventoryPanels.SortButtonLayer != null) UnityEngine.Object.Destroy(InventoryPanels.SortButtonLayer.gameObject);
        InventoryPanels.SortButtonLayer = InventoryPanels.InventorySortPanel = null;
        InventoryPanels.ContainerSortButton = null;
        InventoryPanels.PlayerSortHost.Clear();
        InventoryPanels.ContainerSortHost.Clear();
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

    private static void DisableActionPanelChildren(RectTransform panel, string activeButtonName)
    {
        for (int i = 0; i < panel.childCount; i++)
        {
            Transform child = panel.GetChild(i);
            if (child.name.StartsWith("InventorySlots_", StringComparison.Ordinal) &&
                !string.Equals(child.name, activeButtonName, StringComparison.Ordinal))
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
            RemoveClonedControllerShortcuts(button);
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
        foreach (TMP_Text text in GetInventoryButtonCaptionTexts<TMP_Text>(button))
        {
            Localization.instance?.RemoveTextFromCache(text);
            ApplyDefaultFontAsset(text);
            if (text.text != label)
            {
                text.text = label;
            }
        }

        foreach (UnityEngine.UI.Text text in GetInventoryButtonCaptionTexts<UnityEngine.UI.Text>(button))
        {
            Localization.instance?.RemoveTextFromCache(text);
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

        foreach (TMP_Text text in GetInventoryButtonCaptionTexts<TMP_Text>(button))
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

    internal static void HideInventoryActionPanels(bool preservePlayerButtons = false)
    {
        if (preservePlayerButtons) _itemRuleEditor?.Close();
        else _itemRuleEditor?.Hide();
        SetActionPanelActive(InventoryPanels.InventorySortPanel, false);
        if (!preservePlayerButtons) SetActionPanelActive(_inventoryTrashPanel, false);
        CloseInventoryTrashConfirmDialog();
        HideInventorySideHints();
        RestoreContainerActionButtonLayout();
        HideContainerActionButtons();
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
