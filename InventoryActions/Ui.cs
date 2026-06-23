using System;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using Object = UnityEngine.Object;
using ItemData = ItemDrop.ItemData;

namespace InventoryActions;

public sealed partial class InventoryActionsPlugin
{
    private const float SortButtonOutsideGap = 1f;
    private const float TrashPanelGap = 8f;
    private static readonly Vector2 InventorySortButtonFixedOffset = new(2f, 2f);
    private const string PlayerActionPanelName = "InventoryActions_PlayerActionPanel";
    private const string TrashPanelName = "InventoryActions_TrashPanel";
    private const string TrashButtonName = "InventoryActions_TrashButton";
    private const string TrashIconName = "InventoryActions_TrashIcon";
    private const string TrashConfirmDialogName = "InventoryActions_TrashConfirmDialog";

    internal static void UpdateInventoryActionsUi(InventoryGui gui)
    {
        if (gui == null || !InventoryGui.IsVisible())
        {
            HideInventoryActionPanels();
            return;
        }

        Player? player = Player.m_localPlayer;
        if (player == null || player.m_isLoading || gui.m_playerGrid == null)
        {
            HideInventoryActionPanels();
            return;
        }

        UpdateContainerActionButtons(gui);
        UpdatePlayerActionPanel(gui, gui.m_playerGrid, player);
        UpdateTrashPanel(gui, gui.m_playerGrid, player);
        UpdateFavoriteBorders(gui.m_playerGrid, player);
    }

    internal static void HideInventoryActionPanels()
    {
        SetActionPanelActive(Runtime.PlayerActionPanel, false);
        SetActionPanelActive(Runtime.TrashPanel, false);
        RestoreContainerActionButtonLayout();
        SetButtonActive(Runtime.ContainerStoreAllButton, false);
        SetButtonActive(Runtime.ContainerRestockButton, false);
        SetButtonActive(Runtime.ContainerSortButton, false);
        CloseInventoryTrashConfirmDialog();
    }

    private static void UpdateContainerActionButtons(InventoryGui gui)
    {
        if (gui.m_currentContainer == null ||
            gui.m_takeAllButton == null ||
            gui.m_stackAllButton == null)
        {
            RestoreContainerActionButtonLayout();
            HideContainerActionButtons();
            return;
        }

        Container currentContainer = gui.m_currentContainer;
        RectTransform? takeAllRect = gui.m_takeAllButton.transform as RectTransform;
        RectTransform? stackRect = gui.m_stackAllButton.transform as RectTransform;
        RectTransform? takeAllParent = takeAllRect?.parent as RectTransform;
        RectTransform? stackParent = stackRect?.parent as RectTransform;
        if (takeAllRect == null || stackRect == null || takeAllParent == null || stackParent == null)
        {
            RestoreContainerActionButtonLayout();
            HideContainerActionButtons();
            return;
        }

        CaptureRectTransformSnapshot(ref Runtime.TakeAllButtonOriginal, takeAllRect);
        CaptureRectTransformSnapshot(ref Runtime.StackAllButtonOriginal, stackRect);
        RestoreContainerActionButtonLayout();
        SetActionButtonLabel(gui.m_takeAllButton, LocalizeUi("$inventoryactions_button_take_all", "Take all"));
        SetActionButtonLabel(gui.m_stackAllButton, LocalizeUi("$inventoryactions_button_place_stacks", "Place stacks"));

        if (!CanMutateContainerDirectly(currentContainer, allowLocalWithoutZNetView: true))
        {
            HideContainerActionButtons();
            return;
        }

        AlignContainerActionButtonRows(takeAllRect, stackRect);
        float buttonHeight = Mathf.Clamp(Mathf.Max(GetRectHeight(takeAllRect), GetRectHeight(stackRect)), 24f, 36f);
        const float gap = 4f;
        float takeAllWidth = GetRectWidth(takeAllRect);
        float stackAllWidth = GetRectWidth(stackRect);

        Runtime.ContainerStoreAllButton = EnsureActionButton(takeAllParent, gui.m_takeAllButton, "InventoryActions_StoreAllButton", LocalizeUi("$inventoryactions_button_place_all", "Place all"), () => StoreAllToCurrentContainer(Player.m_localPlayer));
        LayoutButtonPair(takeAllRect, Runtime.ContainerStoreAllButton, takeAllWidth, buttonHeight, gap, addedButtonOnRight: true);
        SetTooltip(Runtime.ContainerStoreAllButton, LocalizeUi("$inventoryactions_button_place_all", "Place all"), "Move all non-favorited regular inventory items into the current container.");

        Runtime.ContainerRestockButton = EnsureActionButton(stackParent, gui.m_stackAllButton, "InventoryActions_RestockButton", LocalizeUi("$inventoryactions_button_take_stacks", "Take stacks"), () => RestockFromCurrentContainer(Player.m_localPlayer));
        LayoutButtonPair(stackRect, Runtime.ContainerRestockButton, stackAllWidth, buttonHeight, gap, addedButtonOnRight: false);
        SetTooltip(Runtime.ContainerRestockButton, LocalizeUi("$inventoryactions_button_take_stacks", "Take stacks"), "Fill matching non-favorited partial stacks from the current container.");

        Runtime.ContainerSortButton = EnsureContainerSortButton(stackRect, gui.m_takeAllButton, buttonHeight);
        SetTooltip(Runtime.ContainerSortButton, LocalizeUi("$inventoryactions_action_sort", "Sort"), "Sort the current container.");
        SetButtonActive(Runtime.ContainerStoreAllButton, true);
        SetButtonActive(Runtime.ContainerRestockButton, true);
        SetButtonActive(Runtime.ContainerSortButton, true);
        SetButtonInteractable(Runtime.ContainerStoreAllButton, true);
        SetButtonInteractable(Runtime.ContainerRestockButton, true);
        SetButtonInteractable(Runtime.ContainerSortButton, true);
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

    private static void UpdatePlayerActionPanel(InventoryGui gui, InventoryGrid playerGrid, Player player)
    {
        if (playerGrid.m_gridRoot == null || gui.m_takeAllButton == null)
        {
            SetActionPanelActive(Runtime.PlayerActionPanel, false);
            return;
        }

        Runtime.PlayerActionPanel = EnsureActionPanel(playerGrid.m_gridRoot, PlayerActionPanelName, Runtime.PlayerActionPanel);
        if (Runtime.PlayerActionPanel == null)
        {
            return;
        }

        float buttonSize = GetContainerSortButtonSize(gui);
        const float gap = 6f;
        int rows = GetDisplayedPlayerRows(playerGrid);
        Runtime.PlayerActionPanel.sizeDelta = new Vector2(buttonSize, buttonSize);
        Runtime.PlayerActionPanel.localScale = Vector3.one;
        Runtime.PlayerActionPanel.localRotation = Quaternion.identity;

        Vector3 position = GetInventorySortPanelPosition(playerGrid, buttonSize, rows) + (Vector3)GetSortButtonPositionOffset();
        Runtime.PlayerActionPanel.localPosition = position;
        DisableActionPanelChildren(Runtime.PlayerActionPanel);

        Button? sortButton = EnsureActionButton(Runtime.PlayerActionPanel, gui.m_takeAllButton, "InventoryActions_PlayerSortButton", "S", () => SortPlayerInventory(Player.m_localPlayer));
        LayoutActionButton(sortButton, 0, buttonSize, buttonSize, gap);
        SetTooltip(sortButton, "Sort inventory", "Sort non-favorited player inventory slots outside the hotbar.");
        SetActionPanelActive(Runtime.PlayerActionPanel, true);
    }

    private static void UpdateTrashPanel(InventoryGui gui, InventoryGrid playerGrid, Player player)
    {
        if (!_enableInventoryTrashPanel.Value.IsOn() ||
            gui == null ||
            playerGrid == null ||
            playerGrid.m_gridRoot == null ||
            gui.m_takeAllButton == null ||
            !InventoryGui.IsVisible())
        {
            SetActionPanelActive(Runtime.TrashPanel, false);
            return;
        }

        Runtime.TrashPanel = EnsureActionPanel(playerGrid.m_gridRoot, TrashPanelName, Runtime.TrashPanel);
        if (Runtime.TrashPanel == null)
        {
            return;
        }

        float elementSpace = Mathf.Max(1f, playerGrid.m_elementSpace);
        float buttonSize = Mathf.Clamp(elementSpace * 0.72f, 42f, 58f);
        Runtime.TrashPanel.sizeDelta = new Vector2(buttonSize, buttonSize);
        Runtime.TrashPanel.localScale = Vector3.one;
        Runtime.TrashPanel.localRotation = Quaternion.identity;

        int rows = GetDisplayedPlayerRows(playerGrid);
        float sortButtonSize = GetContainerSortButtonSize(gui);
        Vector3 sortPanelPosition = GetInventorySortPanelPosition(playerGrid, sortButtonSize, rows);
        Vector3 gridOrigin = GetGridOrigin(playerGrid);
        Runtime.TrashPanel.localPosition = gridOrigin + new Vector3(
            sortPanelPosition.x - gridOrigin.x + (sortButtonSize - buttonSize) * 0.5f,
            -Mathf.Max(1, rows) * elementSpace - TrashPanelGap,
            0f) + (Vector3)GetTrashButtonPositionOffset();

        DisableActionPanelChildren(Runtime.TrashPanel);

        Button? trashButton = EnsureActionButton(Runtime.TrashPanel, gui.m_takeAllButton, TrashButtonName, "", TryClickInventoryTrashPanel);
        RectTransform? trashRect = LayoutActionButton(trashButton, 0, buttonSize, buttonSize, 0f);
        if (trashButton != null && trashRect != null)
        {
            ConfigureInventoryTrashButton(trashButton, buttonSize);
            bool canTrash = CanStartInventoryTrash(gui, player, showMessage: false);
            SetButtonInteractable(trashButton, HasHeldTrashCandidate(gui));
            SetInventoryTrashButtonVisual(trashButton, canTrash);
        }

        SetActionPanelActive(Runtime.TrashPanel, true);
        if (Runtime.TrashPanel.parent != null && Runtime.TrashPanel.GetSiblingIndex() != Runtime.TrashPanel.parent.childCount - 1)
        {
            Runtime.TrashPanel.SetAsLastSibling();
        }
    }

    private static Vector3 GetGridOrigin(InventoryGrid grid)
    {
        if (grid == null)
        {
            return Vector3.zero;
        }

        if (grid.transform is RectTransform gridTransform)
        {
            float width = grid.m_inventory != null ? grid.m_inventory.GetWidth() : grid.m_width;
            return new Vector3(gridTransform.rect.width / 2f - width * grid.m_elementSpace / 2f, 0f, 0f);
        }

        return new Vector3(
            -((grid.m_inventory != null ? grid.m_inventory.GetWidth() : PlayerInventoryWidth) - 1) * grid.m_elementSpace * 0.5f,
            0f,
            0f);
    }

    private static int GetDisplayedPlayerRows(InventoryGrid playerGrid) =>
        Mathf.Max(1, Math.Min(VanillaPlayerRows, playerGrid.m_inventory != null ? playerGrid.m_inventory.GetHeight() : VanillaPlayerRows));

    private static int GetInventoryGridWidth(InventoryGrid playerGrid) =>
        Mathf.Max(1, playerGrid.m_inventory != null ? playerGrid.m_inventory.GetWidth() : PlayerInventoryWidth);

    private static Vector3 GetInventorySortPanelPosition(InventoryGrid playerGrid, float buttonSize, int rows)
    {
        float elementSpace = Mathf.Max(1f, playerGrid.m_elementSpace);
        return GetGridOrigin(playerGrid) + new Vector3(
            GetInventoryGridWidth(playerGrid) * elementSpace + SortButtonOutsideGap,
            -Mathf.Max(1, rows) * elementSpace + buttonSize,
            0f) + (Vector3)InventorySortButtonFixedOffset;
    }

    private static void LayoutButtonPair(RectTransform vanillaButton, Button? addedButton, float originalWidth, float buttonHeight, float gap, bool addedButtonOnRight)
    {
        if (vanillaButton == null || addedButton == null)
        {
            return;
        }

        RectTransform addedRect = (RectTransform)addedButton.transform;
        CopyRectTransformFrame(vanillaButton, addedRect);
        float width = Mathf.Max(1f, (originalWidth - gap) * 0.5f);
        float shift = (width + gap) * 0.5f;
        Vector3 origin = vanillaButton.localPosition;
        Vector3 vanillaPosition = origin + new Vector3(addedButtonOnRight ? -shift : shift, 0f, 0f);
        Vector3 addedPosition = origin + new Vector3(addedButtonOnRight ? shift : -shift, 0f, 0f);

        LayoutButtonRect(vanillaButton, width, buttonHeight, vanillaPosition);
        LayoutButtonRect(addedRect, width, buttonHeight, addedPosition);
    }

    private static Button? EnsureContainerSortButton(RectTransform stackButton, Button template, float buttonHeight)
    {
        RectTransform? parent = stackButton?.parent as RectTransform ?? template.transform.parent as RectTransform;
        if (parent == null)
        {
            return null;
        }

        Button? sortButton = EnsureActionButton(parent, template, "InventoryActions_ContainerSortButton", "S", () => SortCurrentContainer(Player.m_localPlayer));
        if (sortButton == null)
        {
            return null;
        }

        RectTransform sortRect = (RectTransform)sortButton.transform;
        float sortWidth = buttonHeight;
        RectTransform anchor = stackButton ?? (RectTransform)template.transform;
        CopyRectTransformFrame(anchor, sortRect);
        LayoutButtonRect(sortRect, sortWidth, buttonHeight, anchor.localPosition + new Vector3(GetRectWidth(anchor) * 0.5f + SortButtonOutsideGap + sortWidth * 0.5f, 0f, 0f));
        return sortButton;
    }

    private static void LayoutButtonRect(RectTransform rect, float width, float height, Vector3 localPosition)
    {
        rect.sizeDelta = new Vector2(width, height);
        rect.localPosition = localPosition;
        rect.localScale = Vector3.one;
        rect.localRotation = Quaternion.identity;
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

    private static Button? EnsureActionButton(RectTransform panel, Button template, string name, string label, UnityAction action)
    {
        Transform existing = panel.Find(name);
        Button? button = existing != null ? existing.GetComponent<Button>() : null;
        if (button == null)
        {
            button = Object.Instantiate(template, panel, false);
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

    private static RectTransform? EnsureActionPanel(RectTransform parent, string name, RectTransform? cached)
    {
        RectTransform? panel = cached;
        if (panel == null || IsUnityNull(panel))
        {
            Transform existing = parent.Find(name);
            panel = existing as RectTransform;
        }

        if (panel == null || IsUnityNull(panel))
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

    private static void SetActionButtonLabel(Button button, string label)
    {
        InventoryActionButtonMarker marker = button.gameObject.GetComponent<InventoryActionButtonMarker>() ?? button.gameObject.AddComponent<InventoryActionButtonMarker>();
        string signature = $"{label}|{Runtime.UiLocalizationVersion}";
        if (marker.AutoSizeInitialized && string.Equals(marker.LabelSignature, signature, StringComparison.Ordinal))
        {
            return;
        }

        foreach (TMP_Text text in button.GetComponentsInChildren<TMP_Text>(true))
        {
            text.enableAutoSizing = true;
            text.fontSizeMin = 10f;
            text.fontSizeMax = Mathf.Min(Mathf.Max(text.fontSize, 14f), 16f);
            text.alignment = TextAlignmentOptions.Center;
            text.text = label;
            text.enabled = true;
        }

        foreach (UnityEngine.UI.Text text in button.GetComponentsInChildren<UnityEngine.UI.Text>(true))
        {
            text.text = label;
            text.enabled = true;
            text.alignment = TextAnchor.MiddleCenter;
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = 10;
            text.resizeTextMaxSize = 16;
        }

        marker.AutoSizeInitialized = true;
        marker.LabelSignature = signature;
    }

    private static void SetTooltip(Button? button, string topic, string text)
    {
        if (button == null)
        {
            return;
        }

        UITooltip tooltip = button.GetComponent<UITooltip>() ?? button.gameObject.AddComponent<UITooltip>();
        EnsureTooltipPrefab(tooltip);
        tooltip.m_topic = topic;
        tooltip.m_text = text;
    }

    private static void EnsureTooltipPrefab(UITooltip? tooltip)
    {
        if (tooltip == null || tooltip.m_tooltipPrefab != null)
        {
            return;
        }

        UITooltip? source = InventoryGui.instance?.m_playerGrid?.m_elementPrefab != null
            ? InventoryGui.instance.m_playerGrid.m_elementPrefab.GetComponent<UITooltip>()
            : null;
        if (source?.m_tooltipPrefab != null)
        {
            tooltip.m_tooltipPrefab = source.m_tooltipPrefab;
            return;
        }

        InventoryGrid? playerGrid = InventoryGui.instance?.m_playerGrid;
        if (playerGrid == null)
        {
            return;
        }

        foreach (InventoryGrid.Element element in playerGrid.m_elements)
        {
            source = element?.m_go != null ? element.m_go.GetComponent<UITooltip>() : null;
            if (source?.m_tooltipPrefab != null)
            {
                tooltip.m_tooltipPrefab = source.m_tooltipPrefab;
                return;
            }
        }
    }

    internal static bool ShouldAllowTooltipHoverStart(UITooltip tooltip)
    {
        if (tooltip == null || IsUnityNull(tooltip) || tooltip.m_tooltipPrefab != null)
        {
            return true;
        }

        if (UITooltip.m_current == tooltip)
        {
            UITooltip.m_current = null;
        }

        if (UITooltip.m_hovered == tooltip.gameObject)
        {
            UITooltip.m_hovered = null;
        }

        tooltip.m_topic = "";
        tooltip.m_text = "";
        return false;
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
            if (child.name.StartsWith("InventoryActions_", StringComparison.Ordinal))
            {
                child.gameObject.SetActive(false);
            }
        }
    }

    private static void SetActionPanelActive(RectTransform? panel, bool active)
    {
        if (panel != null && !IsUnityNull(panel) && panel.gameObject.activeSelf != active)
        {
            panel.gameObject.SetActive(active);
        }
    }

    private static void HideContainerActionButtons()
    {
        SetButtonActive(Runtime.ContainerStoreAllButton, false);
        SetButtonActive(Runtime.ContainerRestockButton, false);
        SetButtonActive(Runtime.ContainerSortButton, false);
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
        Runtime.TakeAllButtonOriginal?.Restore();
        Runtime.StackAllButtonOriginal?.Restore();
    }

    private static void SetButtonActive(Button? button, bool active)
    {
        if (button != null && !IsUnityNull(button) && button.gameObject.activeSelf != active)
        {
            button.gameObject.SetActive(active);
        }
    }

    private static void SetButtonInteractable(Button? button, bool interactable)
    {
        if (button != null && !IsUnityNull(button) && button.interactable != interactable)
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

    private static float GetContainerSortButtonSize(InventoryGui gui)
    {
        RectTransform? takeAllRect = gui != null && gui.m_takeAllButton != null ? gui.m_takeAllButton.transform as RectTransform : null;
        RectTransform? stackAllRect = gui != null && gui.m_stackAllButton != null ? gui.m_stackAllButton.transform as RectTransform : null;
        if (takeAllRect == null && stackAllRect == null)
        {
            return 32f;
        }

        float takeAllHeight = takeAllRect != null ? GetRectHeight(takeAllRect) : 0f;
        float stackAllHeight = stackAllRect != null ? GetRectHeight(stackAllRect) : 0f;
        return Mathf.Clamp(Mathf.Max(takeAllHeight, stackAllHeight), 24f, 36f);
    }

    private static bool HasHeldTrashCandidate(InventoryGui gui) =>
        gui != null && gui.m_dragGo != null && gui.m_dragItem != null && gui.m_dragInventory != null;

    private static void ConfigureInventoryTrashButton(Button button, float buttonSize)
    {
        InventoryTrashButtonMarker marker = button.GetComponent<InventoryTrashButtonMarker>() ?? button.gameObject.AddComponent<InventoryTrashButtonMarker>();
        if (!marker.TextSuppressed)
        {
            foreach (TMP_Text text in button.GetComponentsInChildren<TMP_Text>(true))
            {
                text.text = "";
                text.enabled = false;
            }

            foreach (UnityEngine.UI.Text text in button.GetComponentsInChildren<UnityEngine.UI.Text>(true))
            {
                text.text = "";
                text.enabled = false;
            }

            marker.TextSuppressed = true;
        }

        if (marker.Icon == null || IsUnityNull(marker.Icon))
        {
            Transform existing = button.transform.Find(TrashIconName);
            marker.Icon = existing != null ? existing.GetComponent<Image>() : null;
            if (marker.Icon == null || IsUnityNull(marker.Icon))
            {
                GameObject iconGo = new(TrashIconName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                RectTransform iconRect = (RectTransform)iconGo.transform;
                iconRect.SetParent(button.transform, false);
                marker.Icon = iconGo.GetComponent<Image>();
            }
        }

        Sprite sprite = GetInventoryTrashIconSprite();
        float iconSize = Mathf.Max(18f, buttonSize * 0.58f);
        string signature = $"{buttonSize:0.###}|{iconSize:0.###}|{sprite.GetInstanceID()}|{Runtime.UiLocalizationVersion}";
        if (string.Equals(marker.LayoutSignature, signature, StringComparison.Ordinal))
        {
            return;
        }

        RectTransform rect = (RectTransform)marker.Icon!.transform;
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(iconSize, iconSize);
        rect.localScale = Vector3.one;
        rect.localRotation = Quaternion.identity;
        marker.Icon.sprite = sprite;
        marker.Icon.preserveAspect = true;
        marker.Icon.raycastTarget = false;

        SetTooltip(button, LocalizeUi("$inventoryactions_trash_title", "Trash"), LocalizeUi("$inventoryactions_trash_tooltip", "Drop a held inventory item here to delete it after confirmation."));
        marker.LayoutSignature = signature;
    }

    private static void SetInventoryTrashButtonVisual(Button button, bool canTrash)
    {
        InventoryTrashButtonMarker? marker = button.GetComponent<InventoryTrashButtonMarker>();
        if (marker?.Icon != null && !IsUnityNull(marker.Icon))
        {
            if (marker.HasVisualState && marker.LastCanTrash == canTrash)
            {
                return;
            }

            marker.Icon.color = canTrash ? new Color(1f, 0.82f, 0.55f, 1f) : new Color(0.75f, 0.75f, 0.75f, 0.65f);
            marker.LastCanTrash = canTrash;
            marker.HasVisualState = true;
        }
    }

    private static Sprite GetInventoryTrashIconSprite()
    {
        if (Runtime.TrashIconSprite != null && !IsUnityNull(Runtime.TrashIconSprite))
        {
            return Runtime.TrashIconSprite;
        }

        const int size = 64;
        Texture2D texture = new(size, size, TextureFormat.RGBA32, false)
        {
            name = "InventoryActions_TrashIconTexture",
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };

        Color[] pixels = new Color[size * size];
        for (int i = 0; i < pixels.Length; i++)
        {
            pixels[i] = new Color(0f, 0f, 0f, 0f);
        }

        Color line = Color.white;
        DrawTrashLine(pixels, size, 19, 18, 45, 18, 3, line);
        DrawTrashLine(pixels, size, 25, 13, 39, 13, 3, line);
        DrawTrashLine(pixels, size, 23, 22, 27, 50, 3, line);
        DrawTrashLine(pixels, size, 41, 22, 37, 50, 3, line);
        DrawTrashLine(pixels, size, 27, 50, 37, 50, 3, line);
        DrawTrashLine(pixels, size, 30, 26, 30, 45, 2, line);
        DrawTrashLine(pixels, size, 34, 26, 34, 45, 2, line);

        texture.SetPixels(pixels);
        texture.Apply(updateMipmaps: false, makeNoLongerReadable: true);
        Runtime.TrashIconSprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
        Runtime.TrashIconSprite.name = "InventoryActions_TrashIcon";
        return Runtime.TrashIconSprite;
    }

    private static void DrawTrashLine(Color[] pixels, int textureSize, int x0, int y0, int x1, int y1, int thickness, Color color)
    {
        int dx = Math.Abs(x1 - x0);
        int sx = x0 < x1 ? 1 : -1;
        int dy = -Math.Abs(y1 - y0);
        int sy = y0 < y1 ? 1 : -1;
        int err = dx + dy;
        while (true)
        {
            DrawTrashPoint(pixels, textureSize, x0, y0, thickness, color);
            if (x0 == x1 && y0 == y1)
            {
                break;
            }

            int e2 = 2 * err;
            if (e2 >= dy)
            {
                err += dy;
                x0 += sx;
            }

            if (e2 <= dx)
            {
                err += dx;
                y0 += sy;
            }
        }
    }

    private static void DrawTrashPoint(Color[] pixels, int textureSize, int x, int y, int thickness, Color color)
    {
        int radius = Math.Max(1, thickness);
        for (int yy = y - radius; yy <= y + radius; yy++)
        {
            for (int xx = x - radius; xx <= x + radius; xx++)
            {
                if (xx < 0 || xx >= textureSize || yy < 0 || yy >= textureSize)
                {
                    continue;
                }

                int pixelY = textureSize - 1 - yy;
                pixels[pixelY * textureSize + xx] = color;
            }
        }
    }

    private static void TryClickInventoryTrashPanel()
    {
        InventoryGui gui = InventoryGui.instance;
        Player player = Player.m_localPlayer;
        if (!CanStartInventoryTrash(gui, player, showMessage: true))
        {
            return;
        }

        int amount = Mathf.Clamp(gui.m_dragAmount, 1, gui.m_dragItem.m_stack);
        ShowInventoryTrashConfirmDialog(gui, gui.m_dragInventory, gui.m_dragItem, amount);
    }

    private static bool CanStartInventoryTrash(InventoryGui? gui, Player? player, bool showMessage)
    {
        if (!_enableInventoryTrashPanel.Value.IsOn())
        {
            return false;
        }

        if (gui == null || player == null || !InventoryGui.IsVisible())
        {
            return false;
        }

        if (player.m_isLoading || player.IsTeleporting())
        {
            return false;
        }

        if (gui.m_dragGo == null || gui.m_dragItem == null || gui.m_dragInventory == null)
        {
            if (showMessage)
            {
                ShowInventoryTrashMessage(player, "$inventoryactions_trash_no_item", "Pick up an item first.");
            }

            return false;
        }

        Inventory? playerInventory = GetPlayerInventory(player);
        if (playerInventory == null || gui.m_dragInventory != playerInventory)
        {
            if (showMessage)
            {
                ShowInventoryTrashMessage(player, "$inventoryactions_trash_player_inventory_only", "Only player inventory items can be trashed.");
            }

            return false;
        }

        return CanTrashInventoryItem(player, playerInventory, gui.m_dragItem, showMessage);
    }

    private static bool CanTrashInventoryItem(Player player, Inventory inventory, ItemData item, bool showMessage)
    {
        if (inventory == null || item == null || !inventory.ContainsItem(item))
        {
            if (showMessage)
            {
                ShowInventoryTrashMessage(player, "$inventoryactions_trash_item_missing", "That item is no longer available.");
            }

            return false;
        }

        if (!CanTrashCell(inventory, item.m_gridPos))
        {
            if (showMessage)
            {
                ShowInventoryTrashMessage(player, "$inventoryactions_trash_hotbar_item", "Only regular inventory items can be trashed.");
            }

            return false;
        }

        if (IsFavoriteProtected(player, inventory, item))
        {
            if (showMessage)
            {
                ShowInventoryTrashMessage(player, "$inventoryactions_trash_favorite_item", "Favorite items cannot be trashed.");
            }

            return false;
        }

        return true;
    }

    private static void ShowInventoryTrashConfirmDialog(InventoryGui gui, Inventory inventory, ItemData item, int amount)
    {
        CloseInventoryTrashConfirmDialog();
        if (gui == null || gui.m_splitPanel == null || inventory == null || item == null || amount <= 0)
        {
            return;
        }

        Runtime.TrashPendingInventory = inventory;
        Runtime.TrashPendingItem = item;
        Runtime.TrashPendingAmount = amount;

        Runtime.TrashConfirmDialog = Object.Instantiate(gui.m_splitPanel.gameObject, gui.transform);
        Runtime.TrashConfirmDialog.name = TrashConfirmDialogName;

        Button? okButton = FindInventoryTrashConfirmButton(Runtime.TrashConfirmDialog, "win_bkg/Button_ok");
        Button? cancelButton = FindInventoryTrashConfirmButton(Runtime.TrashConfirmDialog, "win_bkg/Button_cancel");
        if (okButton == null || cancelButton == null)
        {
            CloseInventoryTrashConfirmDialog();
            return;
        }

        okButton.onClick.RemoveAllListeners();
        okButton.onClick.AddListener(new UnityAction(ConfirmInventoryTrashDelete));
        SetInventoryTrashConfirmButtonText(okButton, LocalizeUi("$inventoryactions_trash_delete", "Delete"), new Color(1f, 0.25f, 0.12f, 1f));

        cancelButton.onClick.RemoveAllListeners();
        cancelButton.onClick.AddListener(new UnityAction(CloseInventoryTrashConfirmDialog));
        SetInventoryTrashConfirmButtonText(cancelButton, LocalizeUi("$menu_cancel", "Cancel"), Color.white);

        Transform? slider = Runtime.TrashConfirmDialog.transform.Find("win_bkg/Slider");
        if (slider != null)
        {
            slider.gameObject.SetActive(false);
        }

        TMP_Text? text = Runtime.TrashConfirmDialog.transform.Find("win_bkg/Text")?.GetComponent<TMP_Text>();
        if (text != null)
        {
            string itemName = LocalizeUi(item.m_shared.m_name, item.m_shared.m_name);
            string format = LocalizeUi("$inventoryactions_trash_confirm_format", "Delete {item}?");
            text.text = format.Replace("{item}", itemName);
        }

        TMP_Text? amountText = Runtime.TrashConfirmDialog.transform.Find("win_bkg/amount")?.GetComponent<TMP_Text>();
        if (amountText != null)
        {
            amountText.text = $"{amount}/{Mathf.Max(1, item.m_shared.m_maxStackSize)}";
        }

        Image? icon = Runtime.TrashConfirmDialog.transform.Find("win_bkg/Icon_bkg/Icon")?.GetComponent<Image>();
        if (icon != null)
        {
            icon.sprite = item.GetIcon();
            icon.preserveAspect = true;
        }

        Runtime.TrashConfirmDialog.SetActive(true);
    }

    private static Button? FindInventoryTrashConfirmButton(GameObject dialog, string path)
    {
        Transform transform = dialog.transform.Find(path);
        return transform != null ? transform.GetComponent<Button>() : null;
    }

    private static void SetInventoryTrashConfirmButtonText(Button button, string label, Color color)
    {
        foreach (TMP_Text text in button.GetComponentsInChildren<TMP_Text>(true))
        {
            text.text = label;
            text.color = color;
        }

        foreach (UnityEngine.UI.Text text in button.GetComponentsInChildren<UnityEngine.UI.Text>(true))
        {
            text.text = label;
            text.color = color;
        }
    }

    internal static void UpdateInventoryTrashConfirmDialogInput()
    {
        if (Runtime.TrashConfirmDialog == null || IsUnityNull(Runtime.TrashConfirmDialog) || !Runtime.TrashConfirmDialog.activeSelf)
        {
            return;
        }

        if (ZInput.GetButtonDown("JoyButtonB") || ZInput.GetKeyDown(KeyCode.Escape, true))
        {
            CloseInventoryTrashConfirmDialog();
            return;
        }

        if (ZInput.GetKeyDown(KeyCode.Return, true) || ZInput.GetKeyDown(KeyCode.KeypadEnter, true))
        {
            ConfirmInventoryTrashDelete();
        }
    }

    private static void ConfirmInventoryTrashDelete()
    {
        InventoryGui gui = InventoryGui.instance;
        Player player = Player.m_localPlayer;
        Inventory? inventory = Runtime.TrashPendingInventory;
        ItemData? item = Runtime.TrashPendingItem;
        int amount = Runtime.TrashPendingAmount;
        CloseInventoryTrashConfirmDialog();

        if (gui == null || player == null || inventory == null || item == null)
        {
            return;
        }

        Inventory? playerInventory = GetPlayerInventory(player);
        if (inventory != playerInventory || gui.m_dragInventory != inventory || gui.m_dragItem != item || !CanTrashInventoryItem(player, inventory, item, showMessage: true))
        {
            gui.SetupDragItem(null, null, 0);
            return;
        }

        amount = Mathf.Clamp(amount, 1, item.m_stack);
        bool fullStack = amount >= item.m_stack;
        if (fullStack)
        {
            if (((Humanoid)player).IsItemEquiped(item))
            {
                player.RemoveEquipAction(item);
                ((Humanoid)player).UnequipItem(item, false);
            }

            inventory.RemoveItem(item);
        }
        else
        {
            inventory.RemoveItem(item, amount);
        }

        gui.SetupDragItem(null, null, 0);
        gui.UpdateCraftingPanel(false);
        inventory.Changed();
        gui.m_moveItemEffects.Create(gui.transform.position, Quaternion.identity);
    }

    private static void ShowInventoryTrashMessage(Player player, string token, string fallback)
    {
        ((Character)player).Message(MessageHud.MessageType.Center, LocalizeUi(token, fallback), 0, null);
    }

    private static void CloseInventoryTrashConfirmDialog()
    {
        Runtime.TrashPendingInventory = null;
        Runtime.TrashPendingItem = null;
        Runtime.TrashPendingAmount = 0;
        if (Runtime.TrashConfirmDialog != null && !IsUnityNull(Runtime.TrashConfirmDialog))
        {
            Object.Destroy(Runtime.TrashConfirmDialog);
        }

        Runtime.TrashConfirmDialog = null;
    }
}
