using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using ItemData = ItemDrop.ItemData;
using Requirement = Piece.Requirement;

namespace InventorySlots;

public sealed partial class InventorySlotsPlugin
{
    private static Sprite? _craftingFavoriteGroupIcon;

    private static void UpdateCraftingGroupRail(InventoryGui gui, RectTransform grid)
    {
        RectTransform? rail = EnsureCraftingGroupRail(gui);
        if (rail == null)
        {
            return;
        }

        IReadOnlyList<CraftingRecipeGroupFilter> filters = GetSelectableCraftingGroupFilters(gui);
        CraftingGroupRailStamp stamp = GetCraftingGroupRailStamp(gui, grid);
        if (CraftingController.CanReuseGroupRail(stamp))
        {
            return;
        }

        if (!string.IsNullOrEmpty(_selectedCraftingGroupId) && filters.All(filter => filter.Id != _selectedCraftingGroupId))
        {
            _selectedCraftingGroupId = "";
            _craftingRecipePage = 0;
            CraftingController.ClearHoveredRecipe();
            InvalidateCraftingRecipeView();
            filters = GetSelectableCraftingGroupFilters(gui);
            stamp = GetCraftingGroupRailStamp(gui, grid);
        }

        if (filters.Count == 0)
        {
            rail.gameObject.SetActive(false);
            CraftingController.StoreGroupRailStamp(stamp);
            return;
        }

        rail.gameObject.SetActive(true);
        Vector2 blockSize = GetCraftingGroupBlockSize(filters);
        rail.anchoredPosition = grid.anchoredPosition + CraftingGroupRailFixedOffset;
        rail.SetAsLastSibling();

        float tabWidth = blockSize.x;
        float tabHeight = blockSize.x;
        float panelHeight = blockSize.y;
        CraftingRecipeGroupPanel panel = EnsureCraftingGroupPanel(rail, 0);
        panel.Rect.gameObject.SetActive(true);
        panel.Rect.anchorMin = new Vector2(0f, 1f);
        panel.Rect.anchorMax = new Vector2(0f, 1f);
        panel.Rect.pivot = new Vector2(0f, 1f);
        panel.Rect.sizeDelta = new Vector2(tabWidth, panelHeight);
        panel.Rect.anchoredPosition = Vector2.zero;
        panel.Rect.localScale = Vector3.one;
        panel.Rect.localRotation = Quaternion.identity;
        if (panel.Background != null)
        {
            panel.Background.sprite = GetSolidUiSprite();
            panel.Background.color = Color.clear;
            panel.Background.raycastTarget = false;
        }

        for (int position = 0; position < filters.Count; position++)
        {
            CraftingRecipeGroupFilter filter = filters[position];
            CraftingRecipeGroupButton button = EnsureCraftingGroupButton(gui, panel.Rect, position);
            bool hasRecipes = CraftingGroupHasRecipes(gui, filter);
            ConfigureCraftingGroupButton(gui, button, filter, position, tabWidth, tabHeight, hasRecipes);
        }

        for (int i = 1; i < CraftingRecipes.GroupPanels.Count; i++)
        {
            RectTransform rect = CraftingRecipes.GroupPanels[i].Rect;
            if (!IsUnityNull(rect))
            {
                rect.gameObject.SetActive(false);
            }
        }

        for (int i = filters.Count; i < CraftingRecipes.GroupButtons.Count; i++)
        {
            GameObject go = CraftingRecipes.GroupButtons[i].Go;
            if (!IsUnityNull(go))
            {
                go.SetActive(false);
            }
        }

        rail.sizeDelta = new Vector2(tabWidth, panelHeight);
        CraftingController.StoreGroupRailStamp(stamp);
    }

    private static void HideCraftingGroupRail()
    {
        if (_craftingGroupRail != null && !IsUnityNull(_craftingGroupRail))
        {
            _craftingGroupRail.gameObject.SetActive(false);
        }

        CraftingController.StoreGroupRailStamp(default);
    }

    private static CraftingGroupRailStamp GetCraftingGroupRailStamp(InventoryGui gui, RectTransform grid)
    {
        return new CraftingGroupRailStamp(
            gui.m_crafting.GetInstanceID(),
            grid.GetInstanceID(),
            grid.anchoredPosition.x,
            grid.anchoredPosition.y,
            _selectedCraftingGroupId,
            _craftingFavoritesVersion,
            _craftingGroupAvailabilitySignature,
            _craftingSelectableGroupFilterIdsSignature);
    }

    private static RectTransform? EnsureCraftingGroupRail(InventoryGui gui)
    {
        if (gui.m_crafting == null)
        {
            return null;
        }

        if (_craftingGroupRail != null && !IsUnityNull(_craftingGroupRail) && _craftingGroupRail!.parent == gui.m_crafting)
        {
            ConfigureCraftingGroupRail(_craftingGroupRail);
            return _craftingGroupRail;
        }

        Transform? existing = gui.m_crafting.Find(CraftingGroupRailName);
        _craftingGroupRail = existing != null ? existing.GetComponent<RectTransform>() : null;
        if (_craftingGroupRail == null)
        {
            _craftingGroupRail = new GameObject(CraftingGroupRailName, typeof(RectTransform)).GetComponent<RectTransform>();
            _craftingGroupRail.SetParent(gui.m_crafting, false);
        }

        ConfigureCraftingGroupRail(_craftingGroupRail);
        return _craftingGroupRail;
    }

    private static void ConfigureCraftingGroupRail(RectTransform rail)
    {
        rail.anchorMin = new Vector2(0f, 1f);
        rail.anchorMax = new Vector2(0f, 1f);
        rail.pivot = new Vector2(0f, 1f);
        rail.localScale = Vector3.one;
        rail.localRotation = Quaternion.identity;
    }

    private static Vector2 GetCraftingGroupBlockSize(IReadOnlyList<CraftingRecipeGroupFilter> filters)
    {
        int tabCount = Mathf.Max(1, filters.Count);
        float size = CraftingGroupIconBlockFixedSize;
        return new Vector2(size, size * tabCount);
    }

    private static CraftingRecipeGroupPanel EnsureCraftingGroupPanel(RectTransform rail, int index)
    {
        while (CraftingRecipes.GroupPanels.Count <= index)
        {
            CraftingRecipes.GroupPanels.Add(CreateCraftingGroupPanel(rail, CraftingRecipes.GroupPanels.Count));
        }

        CraftingRecipeGroupPanel panel = CraftingRecipes.GroupPanels[index];
        if (IsUnityNull(panel.Rect))
        {
            panel = CreateCraftingGroupPanel(rail, index);
            CraftingRecipes.GroupPanels[index] = panel;
        }

        if (panel.Rect.parent != rail)
        {
            panel.Rect.SetParent(rail, false);
        }

        return panel;
    }

    private static CraftingRecipeGroupPanel CreateCraftingGroupPanel(RectTransform rail, int index)
    {
        RectTransform rect = new GameObject(CraftingGroupPanelNamePrefix + index, typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
        rect.SetParent(rail, false);
        return new CraftingRecipeGroupPanel(rect);
    }

    private static CraftingRecipeGroupButton EnsureCraftingGroupButton(InventoryGui gui, RectTransform panel, int index)
    {
        while (CraftingRecipes.GroupButtons.Count <= index)
        {
            CraftingRecipes.GroupButtons.Add(CreateCraftingGroupButton(panel, CraftingRecipes.GroupButtons.Count));
        }

        CraftingRecipeGroupButton groupButton = CraftingRecipes.GroupButtons[index];
        if (IsUnityNull(groupButton.Go) || IsUnityNull(groupButton.Rect))
        {
            groupButton = CreateCraftingGroupButton(panel, index);
            CraftingRecipes.GroupButtons[index] = groupButton;
        }

        if (groupButton.Rect.parent != panel)
        {
            groupButton.Rect.SetParent(panel, false);
        }

        return groupButton;
    }

    private static CraftingRecipeGroupButton CreateCraftingGroupButton(RectTransform panel, int index)
    {
        GameObject go = CreateFallbackCraftingGroupButton(panel);

        go.name = CraftingGroupButtonNamePrefix + index;
        go.SetActive(true);

        Button? button = go.GetComponent<Button>();
        if (button != null)
        {
            button.onClick.RemoveAllListeners();
            button.interactable = false;
        }

        return new CraftingRecipeGroupButton(go);
    }

    private static GameObject CreateFallbackCraftingGroupButton(RectTransform parent)
    {
        GameObject go = new(CraftingGroupButtonNamePrefix + "fallback", typeof(RectTransform), typeof(Image), typeof(UIInputHandler), typeof(UITooltip));
        go.SetActive(false);
        go.transform.SetParent(parent, false);

        Image background = go.GetComponent<Image>();
        background.sprite = GetSolidUiSprite();
        background.color = new Color(0.05f, 0.035f, 0.02f, 0.72f);
        background.raycastTarget = true;

        RectTransform icon = new GameObject("icon", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
        icon.SetParent(go.transform, false);

        CreateTextRect("Label", go.transform);

        return go;
    }

    private static void ConfigureCraftingGroupButton(InventoryGui gui, CraftingRecipeGroupButton button, CraftingRecipeGroupFilter filter, int position, float tabWidth, float tabHeight, bool hasRecipes)
    {
        button.Go.SetActive(false);
        button.Rect.anchorMin = new Vector2(0f, 1f);
        button.Rect.anchorMax = new Vector2(0f, 1f);
        button.Rect.pivot = new Vector2(0f, 1f);
        button.Rect.sizeDelta = new Vector2(tabWidth, tabHeight);
        button.Rect.anchoredPosition = new Vector2(0f, -position * tabHeight);
        button.Rect.localScale = Vector3.one;
        button.Rect.localRotation = Quaternion.identity;

        bool selected = string.Equals(_selectedCraftingGroupId, filter.Id, StringComparison.Ordinal);
        bool isFavorite = string.Equals(filter.Id, "favorite", StringComparison.OrdinalIgnoreCase);
        bool visuallyAvailable = hasRecipes || isFavorite;
        if (button.Background != null)
        {
            button.Background.sprite = GetSolidUiSprite();
            button.Background.color = selected
                ? new Color(1f, 0.55f, 0.04f, visuallyAvailable ? 0.92f : 0.62f)
                : new Color(0.08f, 0.05f, 0.03f, 0f);
            button.Background.raycastTarget = true;
        }

        TMP_Text label = EnsureCraftingGroupButtonLabel(button.Rect);
        ApplyDefaultFontAsset(label);
        Sprite? icon = GetCraftingGroupIcon(gui, filter);
        if (button.Icon != null)
        {
            button.Icon.gameObject.SetActive(icon != null);
            button.Icon.sprite = icon;
            button.Icon.color = visuallyAvailable ? Color.white : new Color(1f, 1f, 1f, 0.42f);
            button.Icon.preserveAspect = true;
            button.Icon.raycastTarget = false;
            RectTransform iconRect = button.Icon.rectTransform;
            float padding = Mathf.Clamp(CraftingGroupIconFixedPadding, 0f, Mathf.Min(tabWidth, tabHeight) * 0.45f);
            float iconSize = Mathf.Max(4f, Mathf.Min(tabWidth, tabHeight) - padding * 2f);
            iconRect.anchorMin = new Vector2(0.5f, 0.5f);
            iconRect.anchorMax = new Vector2(0.5f, 0.5f);
            iconRect.pivot = new Vector2(0.5f, 0.5f);
            iconRect.sizeDelta = new Vector2(iconSize, iconSize);
            iconRect.anchoredPosition = Vector2.zero;
            iconRect.localScale = Vector3.one;
            iconRect.localRotation = Quaternion.identity;
            iconRect.SetAsLastSibling();
        }

        label.gameObject.SetActive(icon == null);
        label.text = GetCraftingGroupLabel(filter);
        label.color = selected ? new Color(0.12f, 0.07f, 0.035f, 1f) : visuallyAvailable ? Color.white : new Color(1f, 1f, 1f, 0.45f);
        label.fontSize = Mathf.Clamp(tabHeight * 0.42f, 8f, 18f);
        label.fontStyle = selected ? FontStyles.Bold : FontStyles.Normal;
        label.alignment = TextAlignmentOptions.Midline;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.overflowMode = TextOverflowModes.Ellipsis;
        label.raycastTarget = false;
        RectTransform labelRect = label.rectTransform;
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;
        labelRect.localScale = Vector3.one;
        labelRect.localRotation = Quaternion.identity;
        labelRect.SetAsLastSibling();

        foreach (TMP_Text extraText in button.Rect.GetComponentsInChildren<TMP_Text>(includeInactive: true))
        {
            if (extraText != label)
            {
                extraText.gameObject.SetActive(false);
            }
        }

        if (button.Icon != null && icon != null)
        {
            button.Icon.rectTransform.SetAsLastSibling();
        }

        if (button.ActiveOverlay != null)
        {
            button.ActiveOverlay.gameObject.SetActive(false);
        }

        button.Go.SetActive(true);
        button.Marker.FilterId = filter.Id;
        if (!button.Marker.Initialized)
        {
            button.Input.m_onLeftClick += handler =>
            {
                string filterId = handler.GetComponent<CraftingGroupButtonMarker>()?.FilterId ?? "";
                ToggleCraftingGroupFilter(filterId);
            };
            button.Marker.Initialized = true;
        }

        string tooltip = GetCraftingGroupTooltip(filter);
        string tooltipText = isFavorite ? GetCraftingClearFavoritesTooltipText() : "";
        ConfigureSimpleTooltip(button.Go, tooltip, tooltipText, enabled: true);
    }

    private static string GetCraftingGroupTooltip(CraftingRecipeGroupFilter filter)
    {
        string fallback = string.Equals(filter.Id, "favorite", StringComparison.OrdinalIgnoreCase) ? "Favorite" : filter.Tooltip;
        return LocalizeUi(GetCraftingGroupTooltipToken(filter.Id), fallback);
    }

    private static string GetCraftingGroupLabel(CraftingRecipeGroupFilter filter) =>
        LocalizeUi(GetCraftingGroupLabelToken(filter.Id), filter.Label);

    private static string GetCraftingClearFavoritesTooltipText()
    {
        string key = GetCraftingClearFavoritesKeyDisplayText();
        if (string.IsNullOrWhiteSpace(key))
        {
            return "";
        }

        string format = LocalizeUi("$inventoryslots_clear_favorites_hint_format", "{key}: clear favorites");
        return format.Replace("{key}", key);
    }

    private static string GetCraftingClearFavoritesKeyDisplayText() =>
        JoinShortcutDisplayTexts(
            _craftingClearFavoritesKey != null ? _craftingClearFavoritesKey.Value.GetDisplayText() : KeyCode.Mouse2.GetDisplayText(),
            GetControllerHotkeyDisplayText(_controllerClearCraftingFavoritesButton));

    private static string GetCraftingGroupTooltipToken(string id) =>
        NormalizeGroupId(id) switch
        {
            "favorite" => "$inventoryslots_group_favorite",
            "melee" => "$inventoryslots_group_melee",
            "ranged" => "$inventoryslots_group_ranged",
            "magic" => "$inventoryslots_group_magic",
            "armor" => "$inventoryslots_group_equipment",
            "food" => "$inventoryslots_group_food",
            "consumable" => "$inventoryslots_group_consumable",
            "meadbase" => "$inventoryslots_group_meadbase",
            "tool" => "$inventoryslots_group_tool",
            "misc" => "$inventoryslots_group_misc",
            _ => "$inventoryslots_group_" + NormalizeGroupId(id)
        };

    private static string GetCraftingGroupLabelToken(string id) =>
        NormalizeGroupId(id) switch
        {
            "favorite" => "$inventoryslots_group_favorite_short",
            "ranged" => "$inventoryslots_group_ranged_short",
            "armor" => "$inventoryslots_group_equipment_short",
            "consumable" => "$inventoryslots_group_consumable_short",
            "meadbase" => "$inventoryslots_group_meadbase_short",
            _ => GetCraftingGroupTooltipToken(id)
        };

    private static void HandleCraftingGroupFavoriteClearShortcut()
    {
        if (!IsCraftingClearFavoritesHotkeyDown() ||
            !TryGetHoveredCraftingGroupButton(out CraftingRecipeGroupButton? button))
        {
            return;
        }

        CraftingRecipeGroupButton hoveredButton = button!;
        if (!string.Equals(hoveredButton.Marker.FilterId, "favorite", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        ClearCraftingFavoritesFromGroup(hoveredButton.Marker.FilterId);
    }

    private static bool IsCraftingClearFavoritesHotkeyDown()
    {
        bool keyboard =
            _craftingClearFavoritesKey != null &&
            _craftingClearFavoritesKey.Value.MainKey != KeyCode.None &&
            IsShortcutDownAllowingAltPair(_craftingClearFavoritesKey.Value);
        return keyboard || IsControllerHotkeyDown(_controllerClearCraftingFavoritesButton);
    }

    private static bool TryGetHoveredCraftingGroupButton(out CraftingRecipeGroupButton? button)
    {
        Vector2 mouse = GetUiMousePosition();
        foreach (CraftingRecipeGroupButton candidate in CraftingRecipes.GroupButtons)
        {
            if (candidate.Go == null ||
                IsUnityNull(candidate.Go) ||
                !candidate.Go.activeInHierarchy ||
                candidate.Rect == null ||
                IsUnityNull(candidate.Rect) ||
                !RectContainsScreenPoint(candidate.Rect, mouse))
            {
                continue;
            }

            button = candidate;
            return true;
        }

        button = null;
        return false;
    }

    private static TMP_Text EnsureCraftingGroupButtonLabel(RectTransform button)
    {
        Transform? existing = button.Find("Label");
        if (existing != null && existing.TryGetComponent(out TMP_Text existingLabel))
        {
            existingLabel.gameObject.SetActive(true);
            return existingLabel;
        }

        RectTransform rect = CreateTextRect("Label", button);
        return rect.GetComponent<TMP_Text>();
    }

    private static void ToggleCraftingGroupFilter(string filterId)
    {
        if (string.IsNullOrEmpty(filterId))
        {
            return;
        }

        _selectedCraftingGroupId = string.Equals(_selectedCraftingGroupId, filterId, StringComparison.Ordinal) ? "" : filterId;
        _craftingRecipePage = 0;
        CraftingController.ClearHoveredRecipe();
        InvalidateCraftingRecipeView();
        ClearCraftingQueue();

        InventoryGui? gui = InventoryGui.instance;
        if (gui != null && ShouldShowCraftingPanelRedesign(gui))
        {
            UpdateCraftingPanelRedesign(gui, CraftingPanelUpdateReason.StateChanged);
        }
    }

    private static void ClearCraftingFavoritesFromGroup(string filterId)
    {
        if (!string.Equals(filterId, "favorite", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        InventoryGui? gui = InventoryGui.instance;
        if (gui == null || !ClearCraftingFavorites(Player.m_localPlayer, gui.InUpradeTab()))
        {
            return;
        }

        if (string.Equals(_selectedCraftingGroupId, "favorite", StringComparison.OrdinalIgnoreCase))
        {
            _selectedCraftingGroupId = "";
        }

        InvalidateCraftingRecipeView();
    }

    private static IReadOnlyList<CraftingRecipeGroupFilter> GetSelectableCraftingGroupFilters(InventoryGui gui)
    {
        EnsureCraftingGroupAvailabilityCache(gui);
        return CraftingRecipes.SelectableGroupFilterCache;
    }

    private static bool CraftingGroupHasRecipes(InventoryGui gui, CraftingRecipeGroupFilter filter)
    {
        EnsureCraftingGroupAvailabilityCache(gui);
        return CraftingRecipes.GroupHasRecipesCache.TryGetValue(filter.Id, out bool hasRecipes) && hasRecipes;
    }

    private static void EnsureCraftingGroupAvailabilityCache(InventoryGui gui)
    {
        object? recipeListRef = gui.m_availableRecipes;
        int recipeCount = gui.m_availableRecipes?.Count ?? -1;
        string contextSignature = GetCraftingRecipeListContextSignature(gui);
        if (_craftingGroupAvailabilityBuiltVersion == _craftingGroupAvailabilityVersion &&
            ReferenceEquals(_craftingGroupAvailabilityRecipeListRef, recipeListRef) &&
            _craftingGroupAvailabilityRecipeCount == recipeCount &&
            string.Equals(_craftingGroupAvailabilityContextSignature, contextSignature, StringComparison.Ordinal))
        {
            return;
        }

        CraftingRecipes.SelectableGroupFilterCache.Clear();
        CraftingRecipes.GroupHasRecipesCache.Clear();
        _craftingSelectableGroupFilterIdsSignature = "";

        if (gui.m_availableRecipes == null)
        {
            foreach (CraftingRecipeGroupFilter filter in CraftingRecipeGroupFilters)
            {
                bool isFavorite = string.Equals(filter.Id, "favorite", StringComparison.OrdinalIgnoreCase);
                CraftingRecipes.GroupHasRecipesCache[filter.Id] = false;
                if (isFavorite)
                {
                    CraftingRecipes.SelectableGroupFilterCache.Add(filter);
                }
            }

            StoreCraftingGroupAvailabilityCacheStamp(recipeListRef, recipeCount, contextSignature);
            return;
        }

        foreach (CraftingRecipeGroupFilter filter in CraftingRecipeGroupFilters)
        {
            bool isFavorite = string.Equals(filter.Id, "favorite", StringComparison.OrdinalIgnoreCase);
            bool hasRecipes = gui.m_availableRecipes.Any(pair => RecipeMatchesCraftingGroup(pair, filter));
            CraftingRecipes.GroupHasRecipesCache[filter.Id] = hasRecipes;
            if (isFavorite || hasRecipes)
            {
                CraftingRecipes.SelectableGroupFilterCache.Add(filter);
            }
        }

        StoreCraftingGroupAvailabilityCacheStamp(recipeListRef, recipeCount, contextSignature);
    }

    private static void StoreCraftingGroupAvailabilityCacheStamp(object? recipeListRef, int recipeCount, string contextSignature)
    {
        _craftingGroupAvailabilityRecipeListRef = recipeListRef;
        _craftingGroupAvailabilityRecipeCount = recipeCount;
        _craftingGroupAvailabilityContextSignature = contextSignature;
        _craftingGroupAvailabilityBuiltVersion = _craftingGroupAvailabilityVersion;
        _craftingGroupAvailabilitySignature = $"{_craftingGroupAvailabilityBuiltVersion}|{recipeCount}|{CraftingRecipeGroupFilters.Count}|{PredefinedGroupDefinitions.Count}|{ResourceTierByToken.Count}|{CraftingRecipes.SelectableGroupFilterCache.Count}";
        _craftingSelectableGroupFilterIdsSignature = string.Join(",", CraftingRecipes.SelectableGroupFilterCache.Select(filter => filter.Id));
    }

    private static void ClearCraftingGroupAvailabilityCache()
    {
        unchecked
        {
            _craftingGroupAvailabilityVersion++;
        }

        _craftingGroupAvailabilityBuiltVersion = -1;
        _craftingGroupAvailabilityRecipeListRef = null;
        _craftingGroupAvailabilityRecipeCount = -1;
        _craftingGroupAvailabilitySignature = "";
        _craftingGroupAvailabilityContextSignature = "";
        _craftingSelectableGroupFilterIdsSignature = "";
        CraftingRecipes.SelectableGroupFilterCache.Clear();
        CraftingRecipes.GroupHasRecipesCache.Clear();
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

    private static Sprite? GetCraftingGroupIcon(InventoryGui gui, CraftingRecipeGroupFilter filter)
    {
        if (string.Equals(filter.Id, "favorite", StringComparison.OrdinalIgnoreCase))
        {
            return GetFavoriteCraftingGroupIcon();
        }

        Sprite? icon = filter.GetIcon();
        if (icon != null)
        {
            return icon;
        }

        if (gui.m_availableRecipes == null)
        {
            return null;
        }

        foreach (InventoryGui.RecipeDataPair pair in gui.m_availableRecipes)
        {
            if (!RecipeMatchesCraftingGroup(pair, filter))
            {
                continue;
            }

            ItemData? item = GetCraftingRecipeItemData(pair);
            if (item != null)
            {
                return item.GetIcon();
            }
        }

        return null;
    }

    private static Sprite GetFavoriteCraftingGroupIcon()
    {
        if (_craftingFavoriteGroupIcon != null && !IsUnityNull(_craftingFavoriteGroupIcon))
        {
            return _craftingFavoriteGroupIcon;
        }

        const int size = 96;
        Texture2D texture = new(size, size, TextureFormat.RGBA32, false)
        {
            name = "InventorySlots_FavoriteGroupIconTexture",
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };

        Color32[] pixels = Enumerable.Repeat(new Color32(0, 0, 0, 0), size * size).ToArray();
        DrawRect(pixels, size, 19, 28, 77, 56, new Color32(86, 48, 24, 255));
        DrawRect(pixels, size, 22, 32, 74, 53, new Color32(128, 78, 34, 255));
        DrawRect(pixels, size, 18, 54, 78, 70, new Color32(118, 66, 31, 255));
        DrawRect(pixels, size, 22, 58, 74, 67, new Color32(174, 102, 42, 255));
        DrawRect(pixels, size, 18, 52, 78, 57, new Color32(38, 22, 14, 255));
        DrawRect(pixels, size, 18, 25, 78, 31, new Color32(38, 22, 14, 255));
        DrawRect(pixels, size, 17, 28, 23, 56, new Color32(38, 22, 14, 255));
        DrawRect(pixels, size, 73, 28, 79, 56, new Color32(38, 22, 14, 255));
        DrawRect(pixels, size, 20, 42, 76, 48, new Color32(219, 157, 55, 255));
        DrawRect(pixels, size, 45, 27, 53, 56, new Color32(215, 148, 42, 255));
        DrawRect(pixels, size, 42, 37, 56, 49, new Color32(42, 28, 16, 255));
        DrawRect(pixels, size, 46, 39, 52, 47, new Color32(243, 192, 70, 255));
        DrawRect(pixels, size, 26, 60, 44, 64, new Color32(235, 167, 68, 255));
        DrawRect(pixels, size, 55, 34, 70, 38, new Color32(182, 105, 40, 255));
        DrawRect(pixels, size, 28, 34, 38, 38, new Color32(73, 39, 21, 180));

        texture.SetPixels32(pixels);
        texture.Apply(updateMipmaps: false, makeNoLongerReadable: true);
        _craftingFavoriteGroupIcon = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
        _craftingFavoriteGroupIcon.name = "InventorySlots_FavoriteGroupIcon";
        return _craftingFavoriteGroupIcon;
    }

    private static void DrawRect(Color32[] pixels, int textureSize, int xMin, int yMin, int xMax, int yMax, Color32 color)
    {
        xMin = Mathf.Clamp(xMin, 0, textureSize - 1);
        xMax = Mathf.Clamp(xMax, 0, textureSize - 1);
        yMin = Mathf.Clamp(yMin, 0, textureSize - 1);
        yMax = Mathf.Clamp(yMax, 0, textureSize - 1);
        for (int y = yMin; y <= yMax; y++)
        {
            int row = y * textureSize;
            for (int x = xMin; x <= xMax; x++)
            {
                pixels[row + x] = color;
            }
        }
    }

    private static void UpdateCraftingSearchInput(InventoryGui gui, RectTransform grid)
    {
        RectTransform? inputRect = EnsureCraftingSearchInput(gui);
        if (inputRect == null)
        {
            return;
        }

        Vector2 size = GetCraftingSearchInputSize(gui);
        Vector2 position = GetCraftingSearchInputPosition(gui, grid, ref size) +
                           CraftingSearchInputFixedOffset +
                           GetCraftingSearchCompatOffset(gui, size);
        CraftingSearchInputStamp stamp = GetCraftingSearchInputStamp(gui, inputRect, position, size);
        inputRect.gameObject.SetActive(true);
        if (inputRect.GetSiblingIndex() != inputRect.parent.childCount - 1)
        {
            inputRect.SetAsLastSibling();
        }

        if (CraftingController.CanReuseSearchInput(stamp))
        {
            return;
        }

        SetCraftingTopLeftRect(gui.m_crafting, inputRect, position, size);
        ConfigureCraftingSearchInputStyle(gui, inputRect);
        CraftingController.StoreSearchInputStamp(stamp);
    }

    private static CraftingSearchInputStamp GetCraftingSearchInputStamp(InventoryGui gui, RectTransform inputRect, Vector2 position, Vector2 size)
    {
        Image? tabImage = gui.m_tabCraft != null && !IsUnityNull(gui.m_tabCraft) ? gui.m_tabCraft.image : null;
        bool focused = CraftingUi.SearchInput != null && CraftingUi.SearchInput.isFocused;
        return new CraftingSearchInputStamp(
            gui.m_crafting.GetInstanceID(),
            inputRect.GetInstanceID(),
            position.x,
            position.y,
            size.x,
            size.y,
            _craftingSearchQuery,
            _uiLocalizationVersion,
            focused,
            GetUnityObjectId(tabImage),
            GetUnityObjectId(tabImage?.sprite),
            GetUnityObjectId(tabImage?.material));
    }

    private static RectTransform? EnsureCraftingSearchInput(InventoryGui gui)
    {
        if (gui.m_crafting == null)
        {
            return null;
        }

        if (CraftingUi.SearchInputRect != null && !IsUnityNull(CraftingUi.SearchInputRect) && CraftingUi.SearchInputRect!.parent == gui.m_crafting)
        {
            return CraftingUi.SearchInputRect;
        }

        Transform? existing = gui.m_crafting.Find(CraftingSearchInputName);
        CraftingUi.SearchInputRect = existing != null ? existing.GetComponent<RectTransform>() : null;
        if (CraftingUi.SearchInputRect == null)
        {
            CraftingUi.SearchInputRect = new GameObject(CraftingSearchInputName, typeof(RectTransform), typeof(Image), typeof(TMP_InputField)).GetComponent<RectTransform>();
            CraftingUi.SearchInputRect.SetParent(gui.m_crafting, false);
            CraftingController.MarkSearchInputDirty();

            Image background = CraftingUi.SearchInputRect.GetComponent<Image>();
            background.raycastTarget = true;

            RectTransform viewport = new GameObject("Text Area", typeof(RectTransform), typeof(RectMask2D)).GetComponent<RectTransform>();
            viewport.SetParent(CraftingUi.SearchInputRect, false);
            viewport.anchorMin = Vector2.zero;
            viewport.anchorMax = Vector2.one;
            viewport.offsetMin = new Vector2(12f, 4f);
            viewport.offsetMax = new Vector2(-12f, -4f);

            RectTransform placeholderRect = CreateTextRect("Placeholder", viewport, out TMP_Text placeholder);
            placeholder.text = GetCraftingSearchPlaceholderText();
            placeholder.fontSize = 18f;
            placeholder.color = new Color(1f, 0.86f, 0.52f, 0.55f);
            placeholder.alignment = TextAlignmentOptions.MidlineLeft;
            placeholderRect.anchorMin = Vector2.zero;
            placeholderRect.anchorMax = Vector2.one;
            placeholderRect.offsetMin = Vector2.zero;
            placeholderRect.offsetMax = Vector2.zero;

            RectTransform textRect = CreateTextRect("Text", viewport, out TMP_Text text);
            text.fontSize = 18f;
            text.color = Color.white;
            text.alignment = TextAlignmentOptions.MidlineLeft;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            CraftingUi.SearchInput = CraftingUi.SearchInputRect.GetComponent<TMP_InputField>();
            CraftingUi.SearchInput.textViewport = viewport;
            CraftingUi.SearchInput.textComponent = text;
            CraftingUi.SearchInput.placeholder = placeholder;
            CraftingUi.SearchInput.targetGraphic = background;
            CraftingUi.SearchInput.contentType = TMP_InputField.ContentType.Standard;
            CraftingUi.SearchInput.lineType = TMP_InputField.LineType.SingleLine;
            CraftingUi.SearchInput.characterLimit = 48;
            CraftingUi.SearchInput.restoreOriginalTextOnEscape = false;
            CraftingUi.SearchInput.onValueChanged.AddListener(UpdateCraftingSearchQuery);
        }
        else
        {
            CraftingUi.SearchInput = CraftingUi.SearchInputRect.GetComponent<TMP_InputField>();
            CraftingController.MarkSearchInputDirty();
        }

        if (CraftingUi.SearchInput != null && CraftingUi.SearchInput.text != _craftingSearchQuery)
        {
            CraftingUi.SearchInput.SetTextWithoutNotify(_craftingSearchQuery);
        }

        if (CraftingUi.SearchInput?.placeholder is TMP_Text placeholderText)
        {
            ApplyDefaultFontAsset(placeholderText);
            placeholderText.text = GetCraftingSearchPlaceholderText();
        }

        return CraftingUi.SearchInputRect;
    }

    private static Vector2 GetCraftingSearchInputSize(InventoryGui gui)
    {
        float tabHeight = 32f;
        if (gui.m_tabCraft != null && gui.m_tabCraft.transform is RectTransform tabRect)
        {
            tabHeight = Mathf.Clamp(GetRectHeight(tabRect), 28f, 42f);
        }

        return new Vector2(CraftingSearchInputWidth, tabHeight);
    }

    private static void ConfigureCraftingSearchInputStyle(InventoryGui gui, RectTransform inputRect)
    {
        Image? background = inputRect.GetComponent<Image>();
        if (background != null)
        {
            Image? tabImage = gui.m_tabCraft != null && !IsUnityNull(gui.m_tabCraft) ? gui.m_tabCraft.image : null;
            if (tabImage != null)
            {
                background.sprite = tabImage.sprite;
                background.type = tabImage.type;
                background.material = tabImage.material;
                background.pixelsPerUnitMultiplier = tabImage.pixelsPerUnitMultiplier;
            }
            else
            {
                background.sprite = GetSolidUiSprite();
                background.type = Image.Type.Sliced;
            }

            background.color = CraftingUi.SearchInput != null && CraftingUi.SearchInput.isFocused
                ? new Color(0.18f, 0.12f, 0.07f, 0.96f)
                : new Color(0.05f, 0.035f, 0.025f, 0.92f);
            background.raycastTarget = true;
        }

        if (CraftingUi.SearchInput != null)
        {
            CraftingUi.SearchInput.targetGraphic = background;
        }
    }

    private static Vector2 GetCraftingSearchInputPosition(InventoryGui gui, RectTransform grid, ref Vector2 size)
    {
        size.x = CraftingSearchInputWidth;
        float x = grid.anchoredPosition.x + (CraftingRecipeGridColumns - 2) * CraftingRecipeGridCellSpace;
        float y = grid.anchoredPosition.y + 58f;

        if (TryGetCraftingPrimaryTabBounds(gui, out CraftingTabRowBounds tabRow))
        {
            size.y = tabRow.Height;
            return new Vector2(x, tabRow.Top);
        }

        return new Vector2(x, y);
    }

    private static Vector2 GetCraftingSearchCompatOffset(InventoryGui gui, Vector2 searchSize)
    {
        return ShouldOffsetCraftingSearchForRecycleNReclaim(gui)
            ? new Vector2(0f, Mathf.Max(0f, searchSize.y))
            : Vector2.zero;
    }

    private static bool ShouldOffsetCraftingSearchForRecycleNReclaim(InventoryGui gui)
    {
        return HasPlugin(RecycleNReclaimGuid) && HasVisibleRecycleNReclaimCraftingTab(gui);
    }

    private static bool HasVisibleRecycleNReclaimCraftingTab(InventoryGui gui)
    {
        if (gui == null || IsUnityNull(gui) || gui.m_crafting == null || IsUnityNull(gui.m_crafting))
        {
            return false;
        }

        int guiId = gui.GetInstanceID();
        int frame = Time.frameCount;
        if (_visibleRecycleNReclaimTabFrame == frame && _visibleRecycleNReclaimTabGuiId == guiId)
        {
            return _visibleRecycleNReclaimTabValue;
        }

        _visibleRecycleNReclaimTabFrame = frame;
        _visibleRecycleNReclaimTabGuiId = guiId;
        _visibleRecycleNReclaimTabValue = false;
        foreach (Transform child in gui.m_crafting.GetComponentsInChildren<Transform>(includeInactive: false))
        {
            if (child == null || IsUnityNull(child) || child == gui.m_crafting)
            {
                continue;
            }

            string name = child.name ?? "";
            if (IsRecycleNReclaimUiToken(name))
            {
                _visibleRecycleNReclaimTabValue = true;
                return true;
            }

            foreach (Component component in child.GetComponents<Component>())
            {
                string typeName = component != null && !IsUnityNull(component)
                    ? component.GetType().FullName ?? component.GetType().Name
                    : "";
                if (IsRecycleNReclaimUiToken(typeName))
                {
                    _visibleRecycleNReclaimTabValue = true;
                    return true;
                }
            }

            TMP_Text? text = child.GetComponent<TMP_Text>();
            if (text != null && !IsUnityNull(text) && IsRecycleNReclaimUiToken(text.text ?? ""))
            {
                _visibleRecycleNReclaimTabValue = true;
                return true;
            }
        }

        return false;
    }

    private static bool IsRecycleNReclaimUiToken(string value)
    {
        return value.IndexOf("reclaim", StringComparison.OrdinalIgnoreCase) >= 0 ||
               value.IndexOf("recycle", StringComparison.OrdinalIgnoreCase) >= 0 ||
               value.IndexOf("recycling", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static string GetCraftingSearchPlaceholderText()
    {
        return LocalizeUi("$inventoryslots_search", "Search");
    }

    private static void UpdateCraftingSortModeButtons(InventoryGui gui, RectTransform grid)
    {
        RectTransform? group = EnsureCraftingSortModeButtonGroup(gui);
        if (group == null || gui.m_crafting == null)
        {
            return;
        }

        Vector2 searchSize = GetCraftingSearchInputSize(gui);
        Vector2 searchPosition = GetCraftingSearchInputPosition(gui, grid, ref searchSize) +
                                 CraftingSearchInputFixedOffset +
                                 GetCraftingSearchCompatOffset(gui, searchSize);
        float buttonSize = Mathf.Clamp(searchSize.y, 26f, 34f);
        Vector2 size = new(buttonSize * 2f + CraftingSortModeButtonGap, buttonSize);
        Vector2 position = searchPosition +
                           new Vector2(0f, buttonSize + CraftingSortModeButtonGap) +
                           CraftingSortModeButtonsFixedOffset;
        CraftingSortModeButtonsStamp stamp = GetCraftingSortModeButtonsStamp(gui, group, position, size, buttonSize);

        group.gameObject.SetActive(true);
        if (group.GetSiblingIndex() != group.parent.childCount - 1)
        {
            group.SetAsLastSibling();
        }

        if (CraftingController.CanReuseSortModeButtons(stamp))
        {
            return;
        }

        SetCraftingTopLeftRect(gui.m_crafting, group, position, size);
        ConfigureCraftingSortModeButton(gui, group, CraftingGroupFirstSortButtonName, 0, buttonSize, CraftingRecipeSortMode.GroupThenTier);
        ConfigureCraftingSortModeButton(gui, group, CraftingTierFirstSortButtonName, 1, buttonSize, CraftingRecipeSortMode.TierThenGroup);
        CraftingController.StoreSortModeButtonsStamp(stamp);
    }

    private static CraftingSortModeButtonsStamp GetCraftingSortModeButtonsStamp(InventoryGui gui, RectTransform group, Vector2 position, Vector2 size, float buttonSize)
    {
        Image? tabImage = gui.m_tabCraft != null && !IsUnityNull(gui.m_tabCraft) ? gui.m_tabCraft.image : null;
        CraftingRecipeSortMode mode = GetCraftingRecipeSortMode();
        return new CraftingSortModeButtonsStamp(
            gui.m_crafting.GetInstanceID(),
            group.GetInstanceID(),
            position.x,
            position.y,
            size.x,
            size.y,
            buttonSize,
            mode,
            _uiLocalizationVersion,
            GetUnityObjectId(tabImage),
            GetUnityObjectId(tabImage?.sprite),
            GetUnityObjectId(tabImage?.material));
    }

    private static RectTransform? EnsureCraftingSortModeButtonGroup(InventoryGui gui)
    {
        if (gui.m_crafting == null)
        {
            return null;
        }

        if (_craftingSortModeButtonGroup != null && !IsUnityNull(_craftingSortModeButtonGroup) && _craftingSortModeButtonGroup!.parent == gui.m_crafting)
        {
            return _craftingSortModeButtonGroup;
        }

        Transform? existing = gui.m_crafting.Find(CraftingSortModeButtonGroupName);
        _craftingSortModeButtonGroup = existing != null ? existing.GetComponent<RectTransform>() : null;
        if (_craftingSortModeButtonGroup == null)
        {
            _craftingSortModeButtonGroup = new GameObject(CraftingSortModeButtonGroupName, typeof(RectTransform)).GetComponent<RectTransform>();
            _craftingSortModeButtonGroup.SetParent(gui.m_crafting, false);
        }

        CraftingController.ResetSortModeButtonsStamp();
        return _craftingSortModeButtonGroup;
    }

    private static void ConfigureCraftingSortModeButton(InventoryGui gui, RectTransform parent, string name, int index, float buttonSize, CraftingRecipeSortMode mode)
    {
        RectTransform button = EnsureCraftingSortModeButton(parent, name, mode);
        button.gameObject.SetActive(true);
        button.anchorMin = new Vector2(0f, 1f);
        button.anchorMax = new Vector2(0f, 1f);
        button.pivot = new Vector2(0f, 1f);
        button.anchoredPosition = new Vector2(index * (buttonSize + CraftingSortModeButtonGap), 0f);
        button.sizeDelta = new Vector2(buttonSize, buttonSize);
        button.localScale = Vector3.one;
        button.localRotation = Quaternion.identity;

        bool selected = GetCraftingRecipeSortMode() == mode;
        if (button.GetComponent<Image>() is { } image)
        {
            ApplyVanillaButtonImage(gui.m_variantButton, image);
            image.color = selected
                ? new Color(1f, 0.55f, 0.04f, 0.92f)
                : new Color(0.05f, 0.035f, 0.025f, 0.92f);
            image.raycastTarget = true;
        }

        TMP_Text? label = button.Find("Label")?.GetComponent<TMP_Text>();
        if (label != null)
        {
            ApplyDefaultFontAsset(label);
            label.text = GetCraftingSortModeButtonLabel(mode);
            label.alignment = TextAlignmentOptions.Center;
            label.enableAutoSizing = true;
            label.fontSizeMin = 10f;
            label.fontSizeMax = Mathf.Clamp(buttonSize * 0.62f, 13f, 18f);
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.overflowMode = TextOverflowModes.Truncate;
            label.fontStyle = selected ? FontStyles.Bold : FontStyles.Normal;
            label.color = selected ? Color.white : new Color(1f, 0.86f, 0.52f, 0.95f);
            label.raycastTarget = false;
            RectTransform labelRect = label.rectTransform;
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;
            labelRect.localScale = Vector3.one;
            labelRect.localRotation = Quaternion.identity;
        }

        UITooltip? tooltip = button.GetComponent<UITooltip>();
        if (tooltip != null)
        {
            EnsureTooltipPrefab(tooltip);
            tooltip.enabled = true;
            tooltip.Set(GetCraftingSortModeButtonTopic(mode), GetCraftingSortModeButtonText(mode), gui.m_playerGrid != null ? gui.m_playerGrid.m_tooltipAnchor : null, default);
        }

        InventorySlotsSimpleTooltipHover? simpleTooltip = button.GetComponent<InventorySlotsSimpleTooltipHover>();
        if (simpleTooltip != null && !IsUnityNull(simpleTooltip))
        {
            simpleTooltip.Configure("", "");
        }
    }

    private static RectTransform EnsureCraftingSortModeButton(RectTransform parent, string name, CraftingRecipeSortMode mode)
    {
        Transform? existing = parent.Find(name);
        RectTransform? button = existing != null ? existing.GetComponent<RectTransform>() : null;
        if (button != null)
        {
            return button;
        }

        button = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(UIInputHandler), typeof(UITooltip)).GetComponent<RectTransform>();
        button.SetParent(parent, false);
        CreateTextRect("Label", button);
        UIInputHandler input = button.GetComponent<UIInputHandler>();
        input.m_onLeftClick += _ => SetCraftingRecipeSortMode(mode);
        return button;
    }

    private static void SetCraftingRecipeSortMode(CraftingRecipeSortMode mode)
    {
        if (_craftingRecipeSortMode != null && _craftingRecipeSortMode.Value != mode)
        {
            _craftingRecipeSortMode.Value = mode;
        }

        CraftingController.ResetSortModeButtonsStamp();
        InvalidateCraftingRecipeView();
    }

    private static CraftingRecipeSortMode GetCraftingRecipeSortMode() =>
        _craftingRecipeSortMode?.Value ?? CraftingRecipeSortMode.TierThenGroup;

    private static string GetCraftingSortModeButtonLabel(CraftingRecipeSortMode mode) =>
        mode == CraftingRecipeSortMode.GroupThenTier ? "G" : "T";

    private static string GetCraftingSortModeButtonTopic(CraftingRecipeSortMode mode) =>
        mode == CraftingRecipeSortMode.GroupThenTier
            ? LocalizeUi("$inventoryslots_sort_group_first", "Group first")
            : LocalizeUi("$inventoryslots_sort_tier_first", "Tier first");

    private static string GetCraftingSortModeButtonText(CraftingRecipeSortMode mode) =>
        mode == CraftingRecipeSortMode.GroupThenTier
            ? LocalizeUi("$inventoryslots_sort_group_first_tooltip", "Sort by category, then resource tier.")
            : LocalizeUi("$inventoryslots_sort_tier_first_tooltip", "Sort by resource tier, then category.");

    private static bool TryGetCraftingPrimaryTabBounds(InventoryGui gui, out CraftingTabRowBounds bounds)
    {
        bounds = default;
        if (gui.m_crafting == null || gui.m_tabCraft == null || gui.m_tabCraft.transform is not RectTransform craftTab)
        {
            return false;
        }

        Vector3[] corners = new Vector3[4];
        craftTab.GetWorldCorners(corners);
        Vector3 localBottomLeft = gui.m_crafting.InverseTransformPoint(corners[0]);
        Vector3 localTopLeft = gui.m_crafting.InverseTransformPoint(corners[1]);
        Vector3 localTopRight = gui.m_crafting.InverseTransformPoint(corners[2]);
        if (localTopRight.x <= localTopLeft.x || localTopLeft.y <= localBottomLeft.y)
        {
            return false;
        }

        bounds = new CraftingTabRowBounds(localTopLeft.x, localTopRight.x, localTopLeft.y, localBottomLeft.y);
        return true;
    }

    private static void UpdateCraftingSearchQuery(string value)
    {
        string normalized = value.Trim();
        if (string.Equals(_craftingSearchQuery, normalized, StringComparison.Ordinal))
        {
            return;
        }

        _craftingSearchQuery = normalized;
        _craftingRecipePage = 0;
        CraftingController.ClearHoveredRecipe();
        CraftingController.MarkSearchInputDirty();
        InvalidateCraftingRecipeView();
    }

    internal static bool IsCraftingSearchFocused()
    {
        return CraftingUi.SearchInput != null && CraftingUi.SearchInput.isFocused ||
               _craftingCountInput != null && _craftingCountInput.isFocused;
    }

    private static bool RecipeMatchesCraftingSearch(InventoryGui.RecipeDataPair pair)
    {
        if (string.IsNullOrWhiteSpace(_craftingSearchQuery))
        {
            return true;
        }

        string haystack = GetCraftingRecipeSearchText(pair);
        foreach (string token in _craftingSearchQuery.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries))
        {
            if (!haystack.Contains(token.ToLowerInvariant()))
            {
                return false;
            }
        }

        return true;
    }

    private static string GetCraftingRecipeSearchText(InventoryGui.RecipeDataPair pair)
    {
        CraftingRecipePairCacheKey cacheKey = GetCraftingRecipePairCacheKey(pair);
        if (cacheKey.IsValid && CraftingRecipes.SearchTextCache.TryGetValue(cacheKey, out string cached))
        {
            return cached;
        }

        ItemData? item = GetCraftingRecipeItemData(pair);
        if (pair.Recipe == null || item == null)
        {
            return "";
        }

        List<string> parts = new()
        {
            pair.Recipe.m_item != null ? pair.Recipe.m_item.name : ""
        };

        AddCraftingSearchLocalizedNameParts(parts, item.m_shared.m_name ?? "");

        if (pair.Recipe.m_resources != null)
        {
            foreach (Requirement requirement in pair.Recipe.m_resources)
            {
                if (requirement?.m_resItem == null)
                {
                    continue;
                }

                parts.Add(requirement.m_resItem.name);
                AddCraftingSearchLocalizedNameParts(parts, requirement.m_resItem.m_itemData.m_shared.m_name ?? "");
            }
        }

        string text = string.Join(" ", parts).ToLowerInvariant();
        if (cacheKey.IsValid)
        {
            CraftingRecipes.SearchTextCache[cacheKey] = text;
        }

        return text;
    }

    private static void AddCraftingSearchLocalizedNameParts(List<string> parts, string token)
    {
        AddCraftingSearchTextPart(parts, Localization.instance != null ? Localization.instance.Localize(token) : token);
        AddCraftingSearchTextPart(parts, GetEnglishLocalizedText(token));
    }

    private static void AddCraftingSearchTextPart(List<string> parts, string text)
    {
        if (string.IsNullOrWhiteSpace(text) || IsMissingLocalizationText(text))
        {
            return;
        }

        parts.Add(text);
    }

    private static bool IsMissingLocalizationText(string text)
    {
        return text.Length >= 2 && text[0] == '[' && text[text.Length - 1] == ']';
    }

    private static string GetEnglishLocalizedText(string token)
    {
        if (string.IsNullOrWhiteSpace(token) || token[0] != '$')
        {
            return token;
        }

        string key = token.TrimStart('$');
        if (CraftingRecipes.EnglishLocalizationCache.TryGetValue(key, out string cached))
        {
            return cached;
        }

        string english = TryGetIndexedEnglishLocalization(key);
        CraftingRecipes.EnglishLocalizationCache[key] = english;
        return english;
    }

    private static string TryGetIndexedEnglishLocalization(string key)
    {
        EnsureCraftingEnglishLocalizationIndex();
        return CraftingRecipes.EnglishLocalizationIndex.TryGetValue(key, out string english) ? english : "";
    }

    private static void EnsureCraftingEnglishLocalizationIndex()
    {
        if (_craftingEnglishLocalizationIndexBuilt)
        {
            return;
        }

        _craftingEnglishLocalizationIndexBuilt = true;
        CraftingRecipes.EnglishLocalizationIndex.Clear();

        try
        {
            BuildCraftingEnglishLocalizationIndex();
        }
        catch
        {
            CraftingRecipes.EnglishLocalizationIndex.Clear();
        }
    }

    private static void BuildCraftingEnglishLocalizationIndex()
    {
        Localization localization = Localization.instance;
        if (Localization.m_localizationSettings?.Localizations == null)
        {
            return;
        }

        foreach (TextAsset file in Localization.m_localizationSettings.Localizations)
        {
            if (file == null || string.IsNullOrEmpty(file.text))
            {
                continue;
            }

            using StringReader reader = new(file.text);
            string? header = reader.ReadLine();
            if (string.IsNullOrEmpty(header))
            {
                continue;
            }

            string[] columns = header.Split(new[] { ',' }, StringSplitOptions.None);
            int englishColumn = -1;
            for (int i = 0; i < columns.Length; i++)
            {
                if (string.Equals(localization.StripCitations(columns[i]), "English", StringComparison.Ordinal))
                {
                    englishColumn = i;
                    break;
                }
            }

            if (englishColumn < 0)
            {
                continue;
            }

            foreach (List<string> row in localization.DoQuoteLineSplit(reader))
            {
                if (row.Count == 0 || string.IsNullOrWhiteSpace(row[0]) || row.Count <= englishColumn)
                {
                    continue;
                }

                string text = row[englishColumn].Trim();
                if ((string.IsNullOrEmpty(text) || text[0] == '\r') && row.Count > 1)
                {
                    text = row[1].Trim();
                }

                if (!string.IsNullOrWhiteSpace(text) && !CraftingRecipes.EnglishLocalizationIndex.ContainsKey(row[0]))
                {
                    CraftingRecipes.EnglishLocalizationIndex[row[0]] = text;
                }
            }
        }
    }

    private static void ClearCraftingEnglishLocalizationCaches()
    {
        CraftingRecipes.EnglishLocalizationCache.Clear();
        CraftingRecipes.EnglishLocalizationIndex.Clear();
        _craftingEnglishLocalizationIndexBuilt = false;
    }
}
