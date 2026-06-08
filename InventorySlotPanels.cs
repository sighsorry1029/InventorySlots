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
    private readonly struct PlayerInventoryPanelBackgroundSnapshot
    {
        public PlayerInventoryPanelBackgroundSnapshot(
            bool activeSelf,
            bool imageEnabled,
            Sprite? sprite,
            Image.Type type,
            float pixelsPerUnitMultiplier,
            Material? material,
            Color color,
            Vector2 anchorMax,
            Vector2 pivot,
            Vector2 offsetMin,
            Vector2 offsetMax,
            Vector2 anchoredPosition,
            Vector2 sizeDelta,
            Vector3 localScale,
            Quaternion localRotation)
        {
            ActiveSelf = activeSelf;
            ImageEnabled = imageEnabled;
            Sprite = sprite;
            Type = type;
            PixelsPerUnitMultiplier = pixelsPerUnitMultiplier;
            Material = material;
            Color = color;
            AnchorMax = anchorMax;
            Pivot = pivot;
            OffsetMin = offsetMin;
            OffsetMax = offsetMax;
            AnchoredPosition = anchoredPosition;
            SizeDelta = sizeDelta;
            LocalScale = localScale;
            LocalRotation = localRotation;
        }

        public bool ActiveSelf { get; }
        public bool ImageEnabled { get; }
        public Sprite? Sprite { get; }
        public Image.Type Type { get; }
        public float PixelsPerUnitMultiplier { get; }
        public Material? Material { get; }
        public Color Color { get; }
        public Vector2 AnchorMax { get; }
        public Vector2 Pivot { get; }
        public Vector2 OffsetMin { get; }
        public Vector2 OffsetMax { get; }
        public Vector2 AnchoredPosition { get; }
        public Vector2 SizeDelta { get; }
        public Vector3 LocalScale { get; }
        public Quaternion LocalRotation { get; }
    }

    private static readonly Dictionary<string, PlayerInventoryPanelBackgroundSnapshot> PlayerInventoryPanelBackgroundSnapshots = new(StringComparer.Ordinal);

    private static void UpdatePlayerInventoryPanelBackground(int visibleRegularRows)
    {
        if (InventoryGui.instance == null || InventoryGui.instance.m_player == null)
        {
            return;
        }

        int extraRows = Mathf.Max(0, visibleRegularRows - BaseRows);
        Vector2 anchorMin = new(0f, -1f * ((float)extraRows / BaseRows - 0.01f * Mathf.Max(extraRows - 1, 0)));
        foreach (string childName in new[] { "Bkg", "Darken" })
        {
            RectTransform? rect = GetOrRestorePlayerInventoryPanelBackgroundChild(InventoryGui.instance.m_player, childName);
            if (rect != null)
            {
                CapturePlayerInventoryPanelBackgroundSnapshot(childName, rect);
                RestorePlayerInventoryPanelBackgroundVisual(childName, rect);
                if ((rect.anchorMin - anchorMin).sqrMagnitude > 0.0001f)
                {
                    rect.anchorMin = anchorMin;
                }
            }
        }
    }

    private static RectTransform? GetOrRestorePlayerInventoryPanelBackgroundChild(RectTransform playerPanel, string childName)
    {
        Transform child = playerPanel.Find(childName);
        RectTransform? rect = child != null ? child.GetComponent<RectTransform>() : null;
        if (rect != null)
        {
            return rect;
        }

        if (!PlayerInventoryPanelBackgroundSnapshots.ContainsKey(childName))
        {
            return null;
        }

        rect = new GameObject(childName, typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
        rect.SetParent(playerPanel, false);
        rect.name = childName;
        if (string.Equals(childName, "Darken", StringComparison.Ordinal))
        {
            rect.SetAsFirstSibling();
        }
        else
        {
            rect.SetSiblingIndex(Mathf.Min(1, playerPanel.childCount - 1));
        }

        return rect;
    }

    private static void CapturePlayerInventoryPanelBackgroundSnapshot(string childName, RectTransform rect)
    {
        if (PlayerInventoryPanelBackgroundSnapshots.ContainsKey(childName))
        {
            return;
        }

        Image? image = rect.GetComponent<Image>();
        if (image == null ||
            IsUnityNull(image) ||
            image.sprite == null ||
            IsUnityNull(image.sprite) ||
            !rect.gameObject.activeSelf ||
            !image.enabled ||
            image.color.a <= 0.05f)
        {
            return;
        }

        PlayerInventoryPanelBackgroundSnapshots[childName] = new PlayerInventoryPanelBackgroundSnapshot(
            rect.gameObject.activeSelf,
            image.enabled,
            image.sprite,
            image.type,
            image.pixelsPerUnitMultiplier,
            image.material,
            image.color,
            rect.anchorMax,
            rect.pivot,
            rect.offsetMin,
            rect.offsetMax,
            rect.anchoredPosition,
            rect.sizeDelta,
            rect.localScale,
            rect.localRotation);
    }

    private static void RestorePlayerInventoryPanelBackgroundVisual(string childName, RectTransform rect)
    {
        if (!PlayerInventoryPanelBackgroundSnapshots.TryGetValue(childName, out PlayerInventoryPanelBackgroundSnapshot snapshot))
        {
            return;
        }

        if (!rect.gameObject.activeSelf && snapshot.ActiveSelf)
        {
            rect.gameObject.SetActive(true);
        }

        if ((rect.anchorMax - snapshot.AnchorMax).sqrMagnitude > 0.0001f)
        {
            rect.anchorMax = snapshot.AnchorMax;
        }

        if ((rect.pivot - snapshot.Pivot).sqrMagnitude > 0.0001f)
        {
            rect.pivot = snapshot.Pivot;
        }

        if ((rect.offsetMin - snapshot.OffsetMin).sqrMagnitude > 0.0001f)
        {
            rect.offsetMin = snapshot.OffsetMin;
        }

        if ((rect.offsetMax - snapshot.OffsetMax).sqrMagnitude > 0.0001f)
        {
            rect.offsetMax = snapshot.OffsetMax;
        }

        if ((rect.anchoredPosition - snapshot.AnchoredPosition).sqrMagnitude > 0.0001f)
        {
            rect.anchoredPosition = snapshot.AnchoredPosition;
        }

        if ((rect.sizeDelta - snapshot.SizeDelta).sqrMagnitude > 0.0001f)
        {
            rect.sizeDelta = snapshot.SizeDelta;
        }

        if (rect.localScale != snapshot.LocalScale)
        {
            rect.localScale = snapshot.LocalScale;
        }

        if (rect.localRotation != snapshot.LocalRotation)
        {
            rect.localRotation = snapshot.LocalRotation;
        }

        Image? image = rect.GetComponent<Image>();
        if (image == null || IsUnityNull(image))
        {
            image = rect.gameObject.AddComponent<Image>();
        }

        if (image.enabled != snapshot.ImageEnabled)
        {
            image.enabled = snapshot.ImageEnabled;
        }

        if (image.sprite != snapshot.Sprite)
        {
            image.sprite = snapshot.Sprite;
        }

        if (image.type != snapshot.Type)
        {
            image.type = snapshot.Type;
        }

        if (Math.Abs(image.pixelsPerUnitMultiplier - snapshot.PixelsPerUnitMultiplier) > 0.0001f)
        {
            image.pixelsPerUnitMultiplier = snapshot.PixelsPerUnitMultiplier;
        }

        if (image.material != snapshot.Material)
        {
            image.material = snapshot.Material;
        }

        if (image.color != snapshot.Color)
        {
            image.color = snapshot.Color;
        }

        if (image.raycastTarget)
        {
            image.raycastTarget = false;
        }
    }

    private static void UpdateSlotBindingLabel(InventoryGrid.Element element, SlotDefinition? slot, bool hideEquipmentSlotName = false)
    {
        if (IsUnityNull(element?.m_go))
        {
            return;
        }

        InventoryGridElementUiCache? cache = GetInventoryGridElementUiCache(element!);
        TMP_Text? binding = cache != null && cache.BindingText != null && !IsUnityNull(cache.BindingText)
            ? cache.BindingText
            : null;
        if (binding == null)
        {
            Transform bindingTransform = element!.m_go.transform.Find("binding");
            if (bindingTransform == null)
            {
                return;
            }

            binding = bindingTransform.GetComponent<TMP_Text>();
            if (binding == null)
            {
                return;
            }

            if (cache != null)
            {
                cache.BindingText = binding;
            }
        }

        string text = slot == null || hideEquipmentSlotName ? "" : slot.Kind == SlotKind.Quick ? GetQuickSlotBindingText(slot) : slot.Name;
        string signature = $"{slot?.Id ?? ""}|{slot?.Kind.ToString() ?? ""}|{hideEquipmentSlotName}|{text}";
        if (cache != null && string.Equals(cache.BindingSignature, signature, StringComparison.Ordinal))
        {
            return;
        }

        ApplyDefaultFontAsset(binding);
        RectTransform bindingRect = binding.GetComponent<RectTransform>();
        if (bindingRect != null)
        {
            bindingRect.anchorMin = Vector2.zero;
            bindingRect.anchorMax = Vector2.one;
            bindingRect.offsetMin = new Vector2(4f, 1f);
            bindingRect.offsetMax = new Vector2(-4f, -1f);
            bindingRect.anchoredPosition = Vector2.zero;
            bindingRect.sizeDelta = Vector2.zero;
        }

        binding.enableAutoSizing = true;
        binding.fontSizeMin = slot?.Kind == SlotKind.Quick ? 12f : 7f;
        binding.fontSizeMax = slot?.Kind == SlotKind.Quick ? 16f : 12f;
        binding.textWrappingMode = TextWrappingModes.NoWrap;
        binding.overflowMode = TextOverflowModes.Ellipsis;
        binding.alignment = slot?.Kind == SlotKind.Quick ? TextAlignmentOptions.TopLeft : TextAlignmentOptions.Center;
        binding.color = new Color(0.68f, 0.88f, 1f, 1f);
        binding.text = text;
        binding.enabled = !string.IsNullOrWhiteSpace(text);
        if (cache != null)
        {
            cache.BindingSignature = signature;
        }
    }

    private static string GetQuickSlotBindingText(SlotDefinition slot)
    {
        if (slot.QuickSlotIndex < 0)
        {
            return "";
        }

        if (_quickSlotHotkeyDisplayTexts != null && slot.QuickSlotIndex < _quickSlotHotkeyDisplayTexts.Length)
        {
            string custom = (_quickSlotHotkeyDisplayTexts[slot.QuickSlotIndex].Value ?? "").Trim();
            if (!string.IsNullOrWhiteSpace(custom))
            {
                return custom;
            }
        }

        string keyboard = _quickSlotHotkeys != null && slot.QuickSlotIndex < _quickSlotHotkeys.Length
            ? _quickSlotHotkeys[slot.QuickSlotIndex].Value.GetCompactDisplayText()
            : "";
        string controller = _controllerQuickSlotButtons != null && slot.QuickSlotIndex < _controllerQuickSlotButtons.Length
            ? GetControllerHotkeyDisplayText(_controllerQuickSlotButtons[slot.QuickSlotIndex])
            : "";
        return JoinShortcutDisplayTexts(keyboard, controller);
    }

    private static Vector3 GetGridOrigin(InventoryGrid playerGrid)
    {
        RectTransform gridTransform = (RectTransform)playerGrid.transform;
        if (gridTransform != null)
        {
            float width = playerGrid.m_inventory != null ? playerGrid.m_inventory.GetWidth() : playerGrid.m_width;
            return new Vector3(gridTransform.rect.width / 2f - width * playerGrid.m_elementSpace / 2f, 0f, 0f);
        }

        if (playerGrid.m_elements.Count == 0 || IsUnityNull(playerGrid.m_elements[0]?.m_go))
        {
            return Vector3.zero;
        }

        Transform originTransform = playerGrid.m_elements[0].m_go.transform;
        if (originTransform.parent != playerGrid.m_gridRoot)
        {
            originTransform.SetParent(playerGrid.m_gridRoot, false);
        }

        return originTransform.localPosition;
    }

    private static RectTransform? EnsureSlotPanel(InventoryGrid playerGrid, string panelName, Dictionary<int, RectTransform> cache)
    {
        int key = playerGrid.GetInstanceID();
        if (cache.TryGetValue(key, out RectTransform? cached) && !IsUnityNull(cached))
        {
            if (panelName != QuickSlotPanelName && cached!.parent != playerGrid.m_gridRoot)
            {
                cached.SetParent(playerGrid.m_gridRoot, false);
            }

            cached.gameObject.SetActive(true);
            return cached;
        }

        Transform? existing = playerGrid.m_gridRoot.Find(panelName);
        if (existing == null && string.Equals(panelName, QuickSlotPanelName, StringComparison.Ordinal))
        {
            RectTransform? stableParent = GetQuickSlotPanelStableParent(playerGrid);
            existing = stableParent != null ? stableParent.Find(panelName) : null;
        }

        RectTransform panel = existing != null ? existing.GetComponent<RectTransform>() : null!;
        if (panel == null)
        {
            panel = new GameObject(panelName, typeof(RectTransform), typeof(Image), typeof(UIInputHandler), typeof(InventoryPanelDragMarker)).GetComponent<RectTransform>();
            panel.SetParent(playerGrid.m_gridRoot, false);
        }

        panel.name = panelName;
        Image image = panel.GetComponent<Image>() ?? panel.gameObject.AddComponent<Image>();
        image.color = Color.clear;
        image.raycastTarget = true;
        UIInputHandler input = panel.GetComponent<UIInputHandler>() ?? panel.gameObject.AddComponent<UIInputHandler>();
        InventoryPanelDragMarker marker = panel.GetComponent<InventoryPanelDragMarker>() ?? panel.gameObject.AddComponent<InventoryPanelDragMarker>();
        marker.PanelName = panelName;
        if (!marker.Initialized)
        {
            marker.Initialized = true;
            input.m_onLeftDown += handler => StartInventoryPanelDrag(handler.GetComponent<InventoryPanelDragMarker>());
            input.m_onLeftUp += _ => StopInventoryPanelDrag();
        }

        panel.anchorMin = new Vector2(0.5f, 0.5f);
        panel.anchorMax = new Vector2(0.5f, 0.5f);
        panel.pivot = new Vector2(0f, 1f);
        panel.localScale = Vector3.one;
        panel.localRotation = Quaternion.identity;
        panel.gameObject.SetActive(true);
        cache[key] = panel;
        return panel;
    }

    private static RectTransform? DisableSlotPanel(InventoryGrid playerGrid, string panelName, Dictionary<int, RectTransform> cache)
    {
        int key = playerGrid.GetInstanceID();
        if (cache.TryGetValue(key, out RectTransform? cached) && !IsUnityNull(cached))
        {
            if (InventoryPanels.DraggedInventoryPanel == cached)
            {
                StopInventoryPanelDrag();
            }

            cached!.gameObject.SetActive(false);
            return null;
        }

        Transform? existing = playerGrid.m_gridRoot != null ? playerGrid.m_gridRoot.Find(panelName) : null;
        if (existing != null)
        {
            if (InventoryPanels.DraggedInventoryPanel == existing.GetComponent<RectTransform>())
            {
                StopInventoryPanelDrag();
            }

            existing.gameObject.SetActive(false);
        }

        return null;
    }

    private static void StartInventoryPanelDrag(InventoryPanelDragMarker? marker)
    {
        if (marker == null || !InventoryGui.IsVisible())
        {
            return;
        }

        bool isQuickSlotsPanel = marker.PanelName == QuickSlotPanelName;
        bool isEquipmentSlotsPanel = marker.PanelName == CustomSlotPanelName;
        if (!isQuickSlotsPanel && !isEquipmentSlotsPanel)
        {
            return;
        }

        RectTransform panel = marker.GetComponent<RectTransform>();
        if (panel == null)
        {
            return;
        }

        InventoryPanels.DraggedInventoryPanel = panel;
        InventoryPanels.InventoryPanelDragStartMouse = Input.mousePosition;
        InventoryPanels.InventoryPanelDragStartOffset = isQuickSlotsPanel ? InventoryPanels.QuickSlotsPanelRuntimeOffset : InventoryPanels.EquipmentSlotsPanelRuntimeOffset;
        InventoryPanels.DraggingQuickSlotsPanelOffset = isQuickSlotsPanel;
        InventoryPanels.DraggingEquipmentSlotsPanelOffset = isEquipmentSlotsPanel;
    }

    private static void StopInventoryPanelDrag()
    {
        CommitDraggedInventoryPanelOffset();
        InventoryPanels.DraggedInventoryPanel = null;
        InventoryPanels.DraggingQuickSlotsPanelOffset = false;
        InventoryPanels.DraggingEquipmentSlotsPanelOffset = false;
    }

    private static void LoadInventoryPanelPositionsFromClientState()
    {
        EnsureClientStateLoaded();
        InventoryPanels.EquipmentSlotsPanelRuntimeOffset = GetClientPanelPosition(InventoryClient.ClientState.Inventory.EquipmentSlotsPanelPosition, EquipmentSlotsPanelFixedOffset);
        InventoryPanels.QuickSlotsPanelRuntimeOffset = GetClientPanelPosition(InventoryClient.ClientState.Inventory.QuickSlotsPanelPosition, QuickSlotsPanelFixedOffset);
        InventoryPanels.QuickSlotHudAnchoredPosition = GetClientPanelPosition(InventoryClient.ClientState.Inventory.QuickSlotsHudPosition, QuickSlotsHudFallbackPosition);
        InventoryPanels.QuickSlotHudElementSpace = Mathf.Max(1f, InventoryClient.ClientState.Inventory.QuickSlotsHudElementSpace);
        InventoryPanels.QuickSlotHudAnchorValid = true;
    }

    private static Vector2 GetClientPanelPosition(InventorySlotsClientPanelPosition? position, Vector2 fallback) =>
        position == null ? fallback : new Vector2(position.X, position.Y);

    private static void CommitDraggedInventoryPanelOffset()
    {
        if (InventoryPanels.DraggingQuickSlotsPanelOffset)
        {
            SaveInventoryPanelPosition(isQuickSlotsPanel: true, InventoryPanels.QuickSlotsPanelRuntimeOffset);
            if (_quickSlotHudFollowsPanel.Value == Toggle.On)
            {
                SaveQuickSlotHudAnchor();
            }
        }
        else if (InventoryPanels.DraggingEquipmentSlotsPanelOffset)
        {
            SaveInventoryPanelPosition(isQuickSlotsPanel: false, InventoryPanels.EquipmentSlotsPanelRuntimeOffset);
        }
    }

    private static void SaveInventoryPanelPosition(bool isQuickSlotsPanel, Vector2 value)
    {
        EnsureClientStateLoaded();
        InventorySlotsClientPanelPosition target = isQuickSlotsPanel
            ? InventoryClient.ClientState.Inventory.QuickSlotsPanelPosition
            : InventoryClient.ClientState.Inventory.EquipmentSlotsPanelPosition;
        if (Mathf.Abs(target.X - value.x) <= 0.01f && Mathf.Abs(target.Y - value.y) <= 0.01f)
        {
            return;
        }

        target.X = value.x;
        target.Y = value.y;
        SaveClientState();
    }

    private static void SaveQuickSlotHudAnchor()
    {
        if (!InventoryPanels.QuickSlotHudAnchorValid)
        {
            return;
        }

        EnsureClientStateLoaded();
        InventorySlotsClientPanelPosition target = InventoryClient.ClientState.Inventory.QuickSlotsHudPosition;
        Vector3 value = InventoryPanels.QuickSlotHudAnchoredPosition;
        float elementSpace = Mathf.Max(1f, InventoryPanels.QuickSlotHudElementSpace);
        if (Mathf.Abs(target.X - value.x) <= 0.01f &&
            Mathf.Abs(target.Y - value.y) <= 0.01f &&
            Mathf.Abs(InventoryClient.ClientState.Inventory.QuickSlotsHudElementSpace - elementSpace) <= 0.01f)
        {
            return;
        }

        target.X = value.x;
        target.Y = value.y;
        InventoryClient.ClientState.Inventory.QuickSlotsHudElementSpace = elementSpace;
        SaveClientState();
    }

    private static void UpdateInventoryPanelDragging()
    {
        if (InventoryPanels.DraggedInventoryPanel == null || (!InventoryPanels.DraggingQuickSlotsPanelOffset && !InventoryPanels.DraggingEquipmentSlotsPanelOffset))
        {
            return;
        }

        if (!Input.GetMouseButton(0) || !InventoryPanels.DraggedInventoryPanel.gameObject.activeInHierarchy)
        {
            StopInventoryPanelDrag();
            return;
        }

        Vector3 delta = Input.mousePosition - InventoryPanels.InventoryPanelDragStartMouse;
        Vector2 nextOffset = new(InventoryPanels.InventoryPanelDragStartOffset.x + delta.x, InventoryPanels.InventoryPanelDragStartOffset.y + delta.y);
        if (InventoryPanels.DraggingQuickSlotsPanelOffset)
        {
            InventoryPanels.QuickSlotsPanelRuntimeOffset = nextOffset;
            return;
        }

        if (InventoryPanels.DraggingEquipmentSlotsPanelOffset)
        {
            InventoryPanels.EquipmentSlotsPanelRuntimeOffset = nextOffset;
            return;
        }
    }

    private static void UpdateVanillaPanelBackground(RectTransform panel, float width, float height)
    {
        if (panel == null)
        {
            return;
        }

        if (IsVanillaPlayerInventoryPanel(panel))
        {
            return;
        }

        if (Math.Abs(panel.rect.width - width) > 0.01f && Math.Abs(panel.sizeDelta.x - width) > 0.01f)
        {
            panel.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
        }

        if (Math.Abs(panel.rect.height - height) > 0.01f && Math.Abs(panel.sizeDelta.y - height) > 0.01f)
        {
            panel.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
        }
        SetPanelBackgroundVisible(panel, visible: true);
        RectTransform background = EnsurePanelBackgroundChild(panel, "Bkg", fallbackColor: new Color(0f, 0f, 0f, 0.45f));
        RectTransform darken = EnsurePanelBackgroundChild(panel, "Darken", fallbackColor: new Color(0f, 0f, 0f, 0.2f));
        if (IsVanillaPlayerInventoryPanelBackground(background) || IsVanillaPlayerInventoryPanelBackground(darken))
        {
            return;
        }

        ConfigureBackgroundRect(background, width, height);
        ConfigureBackgroundRect(darken, width, height);
        if (darken.GetSiblingIndex() != 0)
        {
            darken.SetAsFirstSibling();
        }

        int backgroundSiblingIndex = Mathf.Min(1, background.parent.childCount - 1);
        if (background.GetSiblingIndex() != backgroundSiblingIndex)
        {
            background.SetSiblingIndex(backgroundSiblingIndex);
        }
        if (string.Equals(panel.name, QuickSlotPanelName, StringComparison.Ordinal) || string.Equals(panel.name, CustomSlotPanelName, StringComparison.Ordinal))
        {
            ConfigureSlotPanelDragBorder(panel, width, height, panel.name);
        }
    }

    private static void SetPanelBackgroundVisible(RectTransform panel, bool visible)
    {
        if (panel == null)
        {
            return;
        }

        foreach (string childName in new[] { "Bkg", "Darken", SlotPanelDragBorderName })
        {
            Transform? child = panel.Find(childName);
            if (child != null)
            {
                child.gameObject.SetActive(visible);
            }
        }
    }

    private static RectTransform EnsurePanelBackgroundChild(RectTransform panel, string childName, Color fallbackColor)
    {
        Transform? existing = panel.Find(childName);
        RectTransform rect = existing != null ? existing.GetComponent<RectTransform>() : null!;
        if (rect != null)
        {
            if (IsVanillaPlayerInventoryPanelBackground(rect))
            {
                rect = null!;
            }
            else
            {
                return rect;
            }
        }

        if (rect != null)
        {
            return rect;
        }

        Transform? template = InventoryGui.instance != null && InventoryGui.instance.m_player != null ? InventoryGui.instance.m_player.Find(childName) : null;
        if (template != null)
        {
            rect = UnityEngine.Object.Instantiate(template, panel, false).GetComponent<RectTransform>();
            rect.name = childName;
            if (rect.parent != panel || IsVanillaPlayerInventoryPanelBackground(rect))
            {
                rect = CreateFallbackPanelBackgroundChild(panel, childName, fallbackColor);
            }
        }
        else
        {
            rect = CreateFallbackPanelBackgroundChild(panel, childName, fallbackColor);
        }

        foreach (Graphic graphic in rect.GetComponentsInChildren<Graphic>(includeInactive: true))
        {
            graphic.raycastTarget = false;
        }

        return rect;
    }

    private static RectTransform CreateFallbackPanelBackgroundChild(RectTransform panel, string childName, Color fallbackColor)
    {
        RectTransform rect = new GameObject(childName, typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
        rect.SetParent(panel, false);
        Image image = rect.GetComponent<Image>();
        image.color = fallbackColor;
        return rect;
    }

    private static void ConfigureSlotPanelDragBorder(RectTransform panel, float width, float height, string panelName)
    {
        RectTransform border = EnsureSlotPanelDragBorder(panel, panelName);
        ConfigureBackgroundRect(border, width, height);
        border.SetSiblingIndex(Mathf.Min(2, panel.childCount - 1));
        border.gameObject.SetActive(true);
    }

    private static RectTransform EnsureSlotPanelDragBorder(RectTransform panel, string panelName)
    {
        Transform? existing = panel.Find(SlotPanelDragBorderName);
        RectTransform border = existing != null ? existing.GetComponent<RectTransform>() : null!;
        if (border == null)
        {
            border = new GameObject(SlotPanelDragBorderName, typeof(RectTransform), typeof(Image), typeof(UIInputHandler), typeof(InventoryPanelDragMarker)).GetComponent<RectTransform>();
            border.SetParent(panel, false);
        }

        Image image = border.GetComponent<Image>() ?? border.gameObject.AddComponent<Image>();
        image.sprite = GetSolidUiSprite();
        image.color = Color.clear;
        image.raycastTarget = true;

        InventoryPanelDragMarker marker = border.GetComponent<InventoryPanelDragMarker>() ?? border.gameObject.AddComponent<InventoryPanelDragMarker>();
        marker.PanelName = panelName;

        UIInputHandler input = border.GetComponent<UIInputHandler>() ?? border.gameObject.AddComponent<UIInputHandler>();
        if (!marker.Initialized)
        {
            marker.Initialized = true;
            input.m_onLeftDown += handler => StartInventoryPanelDrag(handler.GetComponent<InventoryPanelDragMarker>());
            input.m_onLeftUp += _ => StopInventoryPanelDrag();
        }

        return border;
    }

    private static void ConfigureBackgroundRect(RectTransform rect, float width, float height)
    {
        if (IsVanillaPlayerInventoryPanelBackground(rect))
        {
            return;
        }

        SetRectLayout(
            rect,
            new Vector2(0f, 1f),
            new Vector2(0f, 1f),
            new Vector2(0.5f, 0.5f),
            new Vector2(width / 2f, -height / 2f),
            new Vector2(width + SidePanelBackgroundPadding * 2f, height + SidePanelBackgroundPadding * 2f));
    }

    private static bool IsVanillaPlayerInventoryPanel(RectTransform rect)
    {
        InventoryGui? gui = InventoryGui.instance;
        return gui?.m_player != null &&
               !IsUnityNull(gui.m_player) &&
               rect == gui.m_player;
    }

    private static bool IsVanillaPlayerInventoryPanelBackground(RectTransform? rect)
    {
        if (rect == null || IsUnityNull(rect))
        {
            return false;
        }

        InventoryGui? gui = InventoryGui.instance;
        return gui?.m_player != null &&
               !IsUnityNull(gui.m_player) &&
               rect.parent == gui.m_player &&
               (string.Equals(rect.name, "Bkg", StringComparison.Ordinal) ||
                string.Equals(rect.name, "Darken", StringComparison.Ordinal));
    }

}
