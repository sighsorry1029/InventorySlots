using System;
using ItemData = ItemDrop.ItemData;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace InventorySlots;

public sealed partial class InventorySlotsPlugin
{
    private static readonly Color CraftingRecipeUnavailableBackgroundColor = new(0.055f, 0.035f, 0.025f, 0.42f);
    private static readonly Color CraftingRecipeDefaultCraftableBackgroundColor = new(1f, 0.58f, 0.16f, 0.44f);
    private static readonly Color CraftingRecipeSelectedBackgroundColor = new(0.42f, 0.68f, 0.92f, 0.62f);

    private static bool HandleCraftingRecipeGridWheel(InventoryGui gui, RectTransform grid)
    {
        bool modifierHeld = IsCraftingRecipeGridZoomModifierHeld();
        if (modifierHeld)
        {
            return false;
        }

        int pageCount = GetCraftingRecipePageCount(gui);
        bool targetActive = IsCraftingRecipeGridScrollTargetActive(grid);
        float wheel = GetUiScrollDelta(UiScrollInputMode.Discrete);
        if (pageCount <= 1 || !targetActive)
        {
            return false;
        }

        if (Mathf.Abs(wheel) < 0.01f)
        {
            return false;
        }

        int direction = wheel < 0f ? 1 : -1;
        int oldPage = _craftingRecipePage;
        _craftingRecipePage = Mathf.Clamp(_craftingRecipePage + direction, 0, pageCount - 1);
        if (_craftingRecipePage == oldPage)
        {
            return false;
        }

        CraftingController.ClearHoveredRecipe();
        InvalidateCraftingRecipeGridLayout();
        int pageStart = _craftingRecipePage * GetCraftingRecipeGridCapacity();
        if (pageStart >= 0 && pageStart < CraftingRecipes.View.Count)
        {
            SetCraftingRecipeWithStoredVariant(gui, CraftingRecipes.View[pageStart].OriginalIndex, center: false);
        }

        return true;
    }

    private static bool HandleCraftingRecipeGridZoomWheel(InventoryGui gui, RectTransform grid)
    {
        bool targetActive = IsCraftingRecipeGridScrollTargetActive(grid);
        bool modifierHeld = IsCraftingRecipeGridZoomModifierHeld();
        float wheel = GetUiScrollDelta(UiScrollInputMode.Discrete);
        if (!targetActive)
        {
            return false;
        }

        if (!modifierHeld || Mathf.Abs(wheel) < 0.01f)
        {
            return false;
        }

        int current = GetCraftingRecipeGridDimension();
        int next = Mathf.Clamp(current + (wheel > 0f ? -1 : 1), CraftingRecipeGridMinDimension, CraftingRecipeGridMaxDimension);
        if (next == current)
        {
            return true;
        }

        if (_craftingRecipeGridSize != null)
        {
            _craftingRecipeGridSize.Value = next;
        }

        int selectedIndex = gui.GetSelectedRecipeIndex();
        int viewIndex = FindCraftingRecipeViewIndex(selectedIndex);
        if (viewIndex >= 0)
        {
            _craftingRecipePage = Mathf.Clamp(viewIndex / GetCraftingRecipeGridCapacity(), 0, Mathf.Max(0, GetCraftingRecipePageCount(gui) - 1));
        }
        else
        {
            ClampCraftingRecipePage(gui);
        }

        EnsureCraftingRecipeCells(gui, grid);
        InvalidateCraftingRecipeGridLayout();
        MarkCraftingRecipeScrollbarDirty();
        CraftingController.ClearHoveredRecipe();
        return true;
    }

    private static bool IsCraftingRecipeGridScrollTargetActive(RectTransform grid) =>
        RectContainsCraftingRecipeIconArea(grid, GetUiMousePosition()) ||
        IsGamepadUiScrollActive();

    private static bool IsCraftingRecipeGridZoomModifierHeld() =>
        _craftingRecipeGridZoomModifier != null && IsShortcutHeldAllowingAltPair(_craftingRecipeGridZoomModifier.Value) ||
        IsGamepadCraftingRecipeGridZoomModifierHeld();

    private static bool IsGamepadCraftingRecipeGridZoomModifierHeld()
    {
        if (!IsGamepadActiveSafe())
        {
            return false;
        }

        try
        {
            return IsControllerHotkeyHeld(_controllerCraftingGridZoomModifierButton);
        }
        catch
        {
            return false;
        }
    }

    private static void InvalidateCraftingRecipeGridZoomHint()
    {
        CraftingController.InvalidateRecipeGridZoomHint();
    }

    private static void UpdateCraftingRecipeGridZoomHint(InventoryGui gui, RectTransform grid)
    {
        if (gui?.m_crafting == null ||
            grid == null ||
            _showCraftingRecipeGridZoomHint == null ||
            _showCraftingRecipeGridZoomHint.Value.IsOff() ||
            _craftingRecipeGridZoomModifier == null ||
            _craftingRecipeGridZoomModifier.Value.MainKey == KeyCode.None)
        {
            CraftingController.InvalidateRecipeGridZoomHint();
            SetHintActive(CraftingUi.RecipeGridZoomHint, false);
            return;
        }

        string modifierText = GetCraftingRecipeGridZoomModifierDisplayText();
        if (string.IsNullOrWhiteSpace(modifierText))
        {
            CraftingController.InvalidateRecipeGridZoomHint();
            SetHintActive(CraftingUi.RecipeGridZoomHint, false);
            return;
        }

        CraftingUi.RecipeGridZoomHint = EnsureInventoryHintLabel(gui.m_crafting, CraftingRecipeGridZoomHintName, ref CraftingUi.RecipeGridZoomHintText);
        if (CraftingUi.RecipeGridZoomHint == null || CraftingUi.RecipeGridZoomHintText == null)
        {
            return;
        }

        float textSize = CraftingRecipeGridZoomHintFixedSize;
        float iconHeight = Mathf.Clamp(textSize * 1.35f, 14f, 42f);
        float iconWidth = Mathf.Max(10f, iconHeight * 0.72f);
        float gap = CraftingRecipeGridZoomHintFixedTextIconGap;
        string label = $"{modifierText}+";
        float height = Mathf.Max(iconHeight, textSize * 1.45f);
        float textWidth = Mathf.Ceil(Mathf.Max(textSize * 1.4f, CraftingUi.RecipeGridZoomHintText.GetPreferredValues(label, 1000f, iconHeight).x + 1f));
        float width = textWidth + gap + iconWidth;
        Vector2 size = new(width, height);
        Vector2 position = GetCraftingRecipeGridZoomHintPosition(grid, size);
        CraftingRecipeGridZoomHintStamp stamp = new(
            gui.m_crafting.GetInstanceID(),
            grid.GetInstanceID(),
            grid.anchoredPosition.x,
            grid.anchoredPosition.y,
            label,
            textSize,
            iconWidth,
            iconHeight,
            gap,
            position.x,
            position.y,
            InventoryWheelHintColor.r,
            InventoryWheelHintColor.g,
            InventoryWheelHintColor.b,
            InventoryWheelHintColor.a);
        if (CraftingUi.RecipeGridZoomHint.gameObject.activeSelf &&
            CraftingUi.RecipeGridZoomHintStamp.Equals(stamp) &&
            CraftingUi.RecipeGridZoomHint.parent == gui.m_crafting &&
            RectLayoutMatches(CraftingUi.RecipeGridZoomHint, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), position, size))
        {
            CraftingUi.RecipeGridZoomHint.SetAsLastSibling();
            return;
        }

        CraftingUi.RecipeGridZoomHintText.text = label;
        CraftingUi.RecipeGridZoomHintText.fontSize = textSize;

        if (CraftingUi.RecipeGridZoomHint.parent != gui.m_crafting)
        {
            CraftingUi.RecipeGridZoomHint.SetParent(gui.m_crafting, false);
        }

        SetCenteredRectLayout(CraftingUi.RecipeGridZoomHint, position, size);
        CraftingUi.RecipeGridZoomHint.SetAsLastSibling();

        RectTransform textRect = (RectTransform)CraftingUi.RecipeGridZoomHintText.transform;
        if (textRect != CraftingUi.RecipeGridZoomHint)
        {
            textRect.anchorMin = new Vector2(0f, 0.5f);
            textRect.anchorMax = new Vector2(0f, 0.5f);
            textRect.pivot = new Vector2(0f, 0.5f);
            textRect.anchoredPosition = Vector2.zero;
            textRect.sizeDelta = new Vector2(textWidth, height);
            textRect.localScale = Vector3.one;
            textRect.localRotation = Quaternion.identity;
        }

        CraftingUi.RecipeGridZoomHintText.alignment = TextAlignmentOptions.Left;
        CraftingUi.RecipeGridZoomHintText.textWrappingMode = TextWrappingModes.NoWrap;
        CraftingUi.RecipeGridZoomHintText.overflowMode = TextOverflowModes.Overflow;
        CraftingUi.RecipeGridZoomHintText.color = InventoryWheelHintColor;
        CraftingUi.RecipeGridZoomHintText.raycastTarget = false;

        RectTransform icon = EnsureHintImage(CraftingUi.RecipeGridZoomHint, "MouseWheelIcon");
        icon.anchorMin = new Vector2(0f, 0.5f);
        icon.anchorMax = new Vector2(0f, 0.5f);
        icon.pivot = new Vector2(0f, 0.5f);
        icon.anchoredPosition = new Vector2(textWidth + gap, 0f);
        icon.sizeDelta = new Vector2(iconWidth, iconHeight);
        icon.localScale = Vector3.one;
        icon.localRotation = Quaternion.identity;
        icon.SetAsLastSibling();

        Image image = icon.GetComponent<Image>();
        image.sprite = GetMouseWheelHintSprite();
        image.type = Image.Type.Simple;
        image.preserveAspect = true;
        image.raycastTarget = false;
        image.color = InventoryWheelHintColor;

        CraftingUi.RecipeGridZoomHintStamp = stamp;
        SetHintActive(CraftingUi.RecipeGridZoomHint, true);
    }

    private static Vector2 GetCraftingRecipeGridZoomHintPosition(RectTransform grid, Vector2 size)
    {
        return CraftingRecipeGridZoomHintFixedOffset;
    }

    private static string GetCraftingRecipeGridZoomModifierDisplayText()
    {
        if (_craftingRecipeGridZoomModifier == null)
        {
            return "";
        }

        string text = _craftingRecipeGridZoomModifier.Value.GetDisplayText();
        if (string.IsNullOrWhiteSpace(text))
        {
            return "";
        }

        return text;
    }

    private static void SyncCraftingRecipePageToSelected(InventoryGui gui)
    {
        int selectedIndex = gui.GetSelectedRecipeIndex();
        if (selectedIndex < 0)
        {
            int previousPage = _craftingRecipePage;
            _craftingRecipePage = 0;
            if (_craftingRecipePage != previousPage)
            {
                InvalidateCraftingRecipeGridLayout();
            }
            return;
        }

        int viewIndex = FindCraftingRecipeViewIndex(selectedIndex);
        int oldPage = _craftingRecipePage;
        _craftingRecipePage = viewIndex < 0
            ? 0
            : Mathf.Clamp(viewIndex / GetCraftingRecipeGridCapacity(), 0, Mathf.Max(0, GetCraftingRecipePageCount(gui) - 1));
        if (_craftingRecipePage != oldPage)
        {
            InvalidateCraftingRecipeGridLayout();
        }
    }

    private static void ClampCraftingRecipePage(InventoryGui gui)
    {
        int oldPage = _craftingRecipePage;
        _craftingRecipePage = Mathf.Clamp(_craftingRecipePage, 0, Mathf.Max(0, GetCraftingRecipePageCount(gui) - 1));
        if (_craftingRecipePage != oldPage)
        {
            InvalidateCraftingRecipeGridLayout();
        }
    }

    private static int GetCraftingRecipePageCount(InventoryGui gui)
    {
        int count = CraftingRecipes.View.Count;
        return count <= 0 ? 1 : Mathf.CeilToInt(count / (float)GetCraftingRecipeGridCapacity());
    }

    private static void LayoutCraftingRecipeGrid(InventoryGui gui, RectTransform grid)
    {
        int dimension = GetCraftingRecipeGridDimension();
        int pageStart = _craftingRecipePage * GetCraftingRecipeGridCapacity();
        int selectedIndex = gui.GetSelectedRecipeIndex();
        int availabilityHash = GetCraftingRecipeGridAvailabilityHash(gui, pageStart);
        CraftingRecipeGridStamp stamp = new(
            dimension,
            pageStart,
            selectedIndex,
            availabilityHash,
            GetCraftingPinnedTooltipGridSignature(),
            CraftingRecipes.View.Count,
            _craftingRecipeVariantVersion);
        if (!_craftingRecipeGridDirty && _craftingRecipeGridStamp.Equals(stamp))
        {
            return;
        }

        EnsureCraftingRecipeCells(gui, grid);

        for (int slotIndex = 0; slotIndex < CraftingRecipes.GridCells.Count; slotIndex++)
        {
            CraftingRecipeGridCell cell = CraftingRecipes.GridCells[slotIndex];
            int viewIndex = pageStart + slotIndex;
            if (viewIndex < 0 || viewIndex >= CraftingRecipes.View.Count)
            {
                SetCraftingRecipeCellVisible(cell, false);
                continue;
            }

            CraftingRecipeViewEntry entry = CraftingRecipes.View[viewIndex];
            ConfigureCraftingRecipeCell(gui, cell, entry.Pair, entry.OriginalIndex, slotIndex, IsCraftingRecipeTooltipPinned(entry.OriginalIndex), entry.OriginalIndex == selectedIndex);
        }

        _craftingRecipeGridStamp = stamp;
        _craftingRecipeGridDirty = false;
    }

    private static void SetCraftingRecipeCellVisible(CraftingRecipeGridCell cell, bool visible)
    {
        if (!visible)
        {
            ClearCurrentCraftingRecipeTooltip(cell.Tooltip);
        }

        if (cell.Go != null && !IsUnityNull(cell.Go))
        {
            cell.Go.SetActive(visible);
        }
    }

    private static void ConfigureCraftingRecipeCell(InventoryGui gui, CraftingRecipeGridCell cell, InventoryGui.RecipeDataPair pair, int index, int slotIndex, bool tooltipPinned, bool craftingSelected)
    {
        if (cell.Go == null || IsUnityNull(cell.Go))
        {
            return;
        }

        cell.Go.SetActive(true);
        RectTransform rect = cell.Rect;
        int dimension = GetCraftingRecipeGridDimension();
        float cellSpace = GetCraftingRecipeDynamicCellSpace();
        float cellSize = GetCraftingRecipeDynamicCellSize(cellSpace);
        int column = slotIndex % dimension;
        int row = slotIndex / dimension;
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.sizeDelta = new Vector2(cellSize, cellSize);
        rect.anchoredPosition = new Vector2(column * cellSpace, -row * cellSpace);
        rect.localScale = Vector3.one;
        rect.localRotation = Quaternion.identity;
        ConfigureCraftingRecipeCellOverlays(cell, cellSize);

        if (cell.Background != null)
        {
            bool veiledMasked = IsVeiledRecipeMasked(pair);
            bool actionAvailable = !veiledMasked && IsCraftingRecipeActionAvailable(gui, pair, index);
            cell.Background.raycastTarget = true;
            cell.Background.color = craftingSelected
                ? CraftingRecipeSelectedBackgroundColor
                : actionAvailable
                    ? GetCraftingRecipeCraftableBackgroundColor()
                    : CraftingRecipeUnavailableBackgroundColor;
        }

        ConfigureCraftingRecipeCellIcon(cell.Icon, pair, cellSize);
        ConfigureCraftingRecipeCellAmountText(cell.Amount, GetCraftingRecipeCellAmountText(gui, pair), cellSize);
        ConfigureCraftingRecipeCellQualityText(cell.Quality, !IsVeiledRecipeMasked(pair) && pair.ItemData != null ? pair.ItemData.m_quality.ToString() : "", cellSize);
        ConfigureCraftingRecipeStyleButton(gui, cell, pair, index, cellSize);
        SetCraftingRecipeCellChild(cell.Selected, false);
        SetCraftingRecipeCellChild(cell.Equipped, false);
        SetCraftingRecipeCellChild(cell.Queued, false);
        SetCraftingRecipeCellChild(cell.NoTeleport, false);
        SetCraftingRecipeCellChild(cell.Food, false);
        SetCraftingRecipeCellChild(cell.Durability, false);
        SetCraftingRecipeFavoriteBorder(cell, IsFavoriteCraftingRecipe(pair), IsUpgradeFavoritePair(pair));
        SetCraftingRecipeSelectedBorder(cell, false);
        SetCraftingRecipePinnedTooltipMarker(cell, tooltipPinned, cellSize);
        ConfigureCraftingRecipeVneiTooltip(cell.Tooltip, pair);

        CraftingRecipeGridMarker marker = cell.Marker;
        marker.Index = index;
        marker.Tooltip = cell.Tooltip;
        if (!marker.Initialized)
        {
            UIInputHandler input = cell.Input;
            input.m_onPointerEnter += handler =>
            {
                CraftingRecipeGridMarker? marker = handler.GetComponent<CraftingRecipeGridMarker>();
                SetCraftingRecipeHover(marker?.Index ?? -1, marker?.Tooltip);
            };
            input.m_onPointerExit += handler =>
            {
                CraftingRecipeGridMarker? marker = handler.GetComponent<CraftingRecipeGridMarker>();
                ClearCraftingRecipeHover(marker?.Index ?? -1, marker?.Tooltip);
            };
            input.m_onLeftClick += handler =>
            {
                CraftingRecipeGridMarker? marker = handler.GetComponent<CraftingRecipeGridMarker>();
                int recipeIndex = marker?.Index ?? -1;
                ClearCraftingRecipeHover(recipeIndex, marker?.Tooltip);
                if (TryToggleCraftingRecipeFavorite(recipeIndex))
                {
                    return;
                }

                SelectCraftingRecipeFromGrid(recipeIndex);
            };
            input.m_onRightClick += handler =>
            {
                CraftingRecipeGridMarker? marker = handler.GetComponent<CraftingRecipeGridMarker>();
                int recipeIndex = marker?.Index ?? -1;
                ClearCraftingRecipeHover(recipeIndex, marker?.Tooltip);
                if (TryToggleCraftingRecipeFavorite(recipeIndex))
                {
                    return;
                }

                SelectCraftingRecipeFromGrid(recipeIndex);
            };
            marker.Initialized = true;
        }

        cell.Tooltip.enabled = false;
    }

    private static string GetCraftingRecipeCellAmountText(InventoryGui gui, InventoryGui.RecipeDataPair pair)
    {
        if (IsVeiledRecipeMasked(pair))
        {
            return "";
        }

        string recycleNReclaimAmount = GetRecycleNReclaimRecipeCellAmountText(gui, pair);
        if (!string.IsNullOrEmpty(recycleNReclaimAmount))
        {
            return recycleNReclaimAmount;
        }

        return pair.Recipe != null && pair.Recipe.m_amount > 1 ? pair.Recipe.m_amount.ToString() : "";
    }

    private static Color GetCraftingRecipeCraftableBackgroundColor() =>
        _craftingRecipeCraftableBackgroundColor?.Value ?? CraftingRecipeDefaultCraftableBackgroundColor;

    private static void SelectCraftingRecipeFromGrid(int index)
    {
        InventoryGui? gui = InventoryGui.instance;
        if (gui == null || !TryGetCraftingRecipePair(gui, index, out _))
        {
            return;
        }

        SetCraftingRecipeWithStoredVariant(gui, index, center: false);
    }

    private static void SetCraftingRecipeHover(int index, UITooltip? tooltip)
    {
        if (CraftingController.IsHoveredRecipe(index))
        {
            SetCurrentCraftingRecipeTooltip(tooltip);
            return;
        }

        CraftingController.SetHoveredRecipe(index);
        SetCurrentCraftingRecipeTooltip(tooltip);
    }

    private static void ClearCraftingRecipeHover(int index, UITooltip? tooltip)
    {
        if (!CraftingController.IsHoveredRecipe(index))
        {
            ClearCurrentCraftingRecipeTooltip(tooltip);
            return;
        }

        CraftingController.ClearHoveredRecipe();
        ClearCurrentCraftingRecipeTooltip(tooltip);
        HideCraftingTooltipRecipeOverlay();
    }

    private static void SetCurrentCraftingRecipeTooltip(UITooltip? tooltip)
    {
        if (tooltip == null || IsUnityNull(tooltip) || string.IsNullOrWhiteSpace(tooltip.m_topic))
        {
            return;
        }

        UITooltip.m_current = tooltip;
    }

    private static void ClearCurrentCraftingRecipeTooltip(UITooltip? tooltip)
    {
        if (tooltip != null && !IsUnityNull(tooltip) && UITooltip.m_current == tooltip)
        {
            UITooltip.m_current = null;
        }
    }

    private static void ConfigureCraftingRecipeVneiTooltip(UITooltip tooltip, InventoryGui.RecipeDataPair pair)
    {
        if (IsVeiledRecipeMasked(pair))
        {
            string topic = GetVeiledRecipeUnknownNameText();
            string text = GetVeiledRecipeUnknownDescriptionText();
            tooltip.m_topic = topic;
            tooltip.m_text = text;

            InventorySlotsTooltipDisplayData maskedDisplayData = tooltip.GetComponent<InventorySlotsTooltipDisplayData>() ?? tooltip.gameObject.AddComponent<InventorySlotsTooltipDisplayData>();
            maskedDisplayData.Configure(topic, text, "");
            return;
        }

        string itemToken = GetCraftingRecipeVneiItemToken(pair);
        string displayText = GetCraftingRecipeVneiDisplayText(pair, itemToken);
        tooltip.m_topic = itemToken;
        tooltip.m_text = displayText;

        InventorySlotsTooltipDisplayData displayData = tooltip.GetComponent<InventorySlotsTooltipDisplayData>() ?? tooltip.gameObject.AddComponent<InventorySlotsTooltipDisplayData>();
        displayData.Configure(itemToken, displayText, "");
    }

    private static string GetCraftingRecipeVneiItemToken(InventoryGui.RecipeDataPair pair)
    {
        if (pair.ItemData != null)
        {
            string prefab = GetItemPrefabName(pair.ItemData);
            return !string.IsNullOrWhiteSpace(prefab) ? prefab : pair.ItemData.m_shared?.m_name ?? "";
        }

        ItemDrop? recipeItem = pair.Recipe?.m_item;
        if (recipeItem == null)
        {
            return "";
        }

        string prefabName = recipeItem.m_itemData.m_dropPrefab != null ? recipeItem.m_itemData.m_dropPrefab.name : recipeItem.name;
        return !string.IsNullOrWhiteSpace(prefabName)
            ? CleanPrefabName(prefabName)
            : recipeItem.m_itemData.m_shared?.m_name ?? "";
    }

    private static string GetCraftingRecipeVneiDisplayText(InventoryGui.RecipeDataPair pair, string fallback)
    {
        string sharedName = pair.ItemData?.m_shared?.m_name ?? pair.Recipe?.m_item?.m_itemData.m_shared?.m_name ?? "";
        if (string.IsNullOrWhiteSpace(sharedName))
        {
            return fallback;
        }

        return Localization.instance != null ? Localization.instance.Localize(sharedName) : sharedName;
    }

    private static string StripRichText(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return "";
        }

        int depth = 0;
        char[] buffer = new char[text.Length];
        int count = 0;
        foreach (char c in text)
        {
            if (c == '<')
            {
                depth++;
                continue;
            }

            if (c == '>' && depth > 0)
            {
                depth--;
                continue;
            }

            if (depth == 0)
            {
                buffer[count++] = c;
            }
        }

        return new string(buffer, 0, count);
    }

    private static void ConfigureCraftingRecipeCellOverlays(CraftingRecipeGridCell cell, float cellSize)
    {
        foreach (Transform child in cell.Rect)
        {
            string name = child.name.ToLowerInvariant();
            if (!name.Contains("bkg") &&
                !name.Contains("background") &&
                !name.Contains("border") &&
                !name.Contains("frame") &&
                name is not ("selected" or "equiped" or "queued" or "noteleport" or "foodicon" or "durability"))
            {
                continue;
            }

            if (child is not RectTransform rect)
            {
                continue;
            }

            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(cellSize, cellSize);
            rect.localScale = Vector3.one;
            rect.localRotation = Quaternion.identity;
        }
    }

    private static void ConfigureCraftingRecipeCellIcon(Image? icon, InventoryGui.RecipeDataPair pair, float cellSize)
    {
        if (icon == null)
        {
            return;
        }

        ItemDrop? recipeItem = pair.Recipe != null ? pair.Recipe.m_item : null;
        icon.gameObject.SetActive(recipeItem != null);
        if (!icon.gameObject.activeSelf)
        {
            return;
        }

        RectTransform rect = icon.rectTransform;
        float padding = Mathf.Clamp(cellSize * 0.08f, 5f, 12f);
        float iconSize = Mathf.Max(16f, cellSize - padding * 2f);
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(padding, -padding);
        rect.sizeDelta = new Vector2(iconSize, iconSize);
        rect.localScale = Vector3.one;
        rect.localRotation = Quaternion.identity;
        icon.sprite = GetCraftingRecipeIcon(pair);
        icon.color = IsVeiledRecipeMasked(pair) ? Color.black : pair.CanCraft ? Color.white : new Color(0.68f, 0.68f, 0.68f, 0.72f);
        icon.raycastTarget = false;
    }

    private static void ConfigureCraftingRecipeStyleButton(InventoryGui gui, CraftingRecipeGridCell cell, InventoryGui.RecipeDataPair pair, int originalIndex, float cellSize)
    {
        RectTransform? button = EnsureCraftingRecipeStyleButton(gui, cell);
        if (button == null)
        {
            return;
        }

        int variantCount = GetCraftingRecipeVariantCount(pair);
        bool visible = variantCount > 1 && pair.ItemData == null && !IsVeiledRecipeMasked(pair);
        button.gameObject.SetActive(visible);
        if (!visible)
        {
            return;
        }

        float height = Mathf.Clamp(cellSize * 0.224f, 14f, 21f);
        float width = Mathf.Clamp(cellSize * 0.476f, 31f, 50f);
        button.anchorMin = new Vector2(0f, 1f);
        button.anchorMax = new Vector2(0f, 1f);
        button.pivot = new Vector2(0f, 1f);
        button.anchoredPosition = new Vector2(cellSize - width - 2f, -2f);
        button.sizeDelta = new Vector2(width, height);
        button.localScale = Vector3.one;
        button.localRotation = Quaternion.identity;
        button.SetAsLastSibling();

        if (button.GetComponent<Image>() is { } image)
        {
            ApplyVanillaButtonImage(gui.m_variantButton, image);
            image.raycastTarget = true;
        }

        TMP_Text? label = button.Find("Label")?.GetComponent<TMP_Text>();
        if (label != null)
        {
            ApplyDefaultFontAsset(label);
            label.text = LocalizeUi("$inventoryslots_style", "Style");
            label.alignment = TextAlignmentOptions.Center;
            label.enableAutoSizing = true;
            label.fontSizeMin = 7f;
            label.fontSizeMax = Mathf.Clamp(height * 0.66f, 9f, 12f);
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.overflowMode = TextOverflowModes.Truncate;
            label.color = GetVanillaButtonTextColor(gui.m_variantButton);
            label.raycastTarget = false;
            RectTransform labelRect = label.rectTransform;
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;
            labelRect.localScale = Vector3.one;
            labelRect.localRotation = Quaternion.identity;
        }

        CraftingRecipeStyleButtonMarker marker = button.GetComponent<CraftingRecipeStyleButtonMarker>() ?? button.gameObject.AddComponent<CraftingRecipeStyleButtonMarker>();
        marker.Index = originalIndex;
        marker.Gui = gui;
    }

    private static RectTransform? EnsureCraftingRecipeStyleButton(InventoryGui gui, CraftingRecipeGridCell cell)
    {
        if (cell.Rect == null || IsUnityNull(cell.Rect))
        {
            return null;
        }

        Transform? existing = cell.Rect.Find(CraftingRecipeStyleButtonName);
        RectTransform? button = existing != null ? existing.GetComponent<RectTransform>() : null;
        if (button == null)
        {
            button = new GameObject(CraftingRecipeStyleButtonName, typeof(RectTransform), typeof(Image), typeof(UIInputHandler), typeof(CraftingRecipeStyleButtonMarker)).GetComponent<RectTransform>();
            button.SetParent(cell.Rect, false);
            CreateTextRect("Label", button);

            UIInputHandler input = button.GetComponent<UIInputHandler>();
            input.m_onLeftClick += handler => ShowCraftingRecipeStyleDialog(handler.GetComponent<CraftingRecipeStyleButtonMarker>());
        }

        return button;
    }

    private static void ApplyVanillaButtonImage(Button? source, Image target)
    {
        Image? sourceImage = source != null && !IsUnityNull(source) ? source.image : null;
        if (sourceImage != null)
        {
            target.sprite = sourceImage.sprite;
            target.type = sourceImage.type;
            target.material = sourceImage.material;
            target.pixelsPerUnitMultiplier = sourceImage.pixelsPerUnitMultiplier;
            target.color = sourceImage.color;
            return;
        }

        target.sprite = GetSolidUiSprite();
        target.type = Image.Type.Sliced;
        target.color = new Color(0.02f, 0.015f, 0.01f, 0.76f);
    }

    private static Color GetVanillaButtonTextColor(Button? button)
    {
        if (button != null && !IsUnityNull(button) && button.GetComponentInChildren<TMP_Text>(true) is { } text)
        {
            return text.color;
        }

        return new Color(1f, 0.84f, 0.32f, 1f);
    }

    private static void ShowCraftingRecipeStyleDialog(CraftingRecipeStyleButtonMarker? marker)
    {
        InventoryGui? gui = marker != null && marker.Gui != null && !IsUnityNull(marker.Gui) ? marker.Gui : InventoryGui.instance;
        if (gui == null || !TryGetCraftingRecipePair(gui, marker?.Index ?? -1, out InventoryGui.RecipeDataPair pair) || pair.Recipe == null)
        {
            return;
        }

        int variantCount = GetCraftingRecipeVariantCount(pair);
        if (variantCount <= 1)
        {
            return;
        }

        SetCraftingRecipeWithStoredVariant(gui, marker!.Index, center: false);
        gui.OnShowVariantSelection();
    }

    internal static void OnCraftingRecipeVariantSelected(InventoryGui gui, int variant)
    {
        if (gui == null || !ShouldShowCraftingPanelRedesign(gui))
        {
            return;
        }

        InventoryGui.RecipeDataPair pair = gui.m_selectedRecipe;
        Recipe? recipe = pair.Recipe;
        if (recipe == null || pair.ItemData != null || GetCraftingRecipeVariantCount(pair) <= 1)
        {
            return;
        }

        SetCraftingRecipeVariant(recipe, variant);
        InvalidateCraftingRecipeGridLayout();
        UpdateCraftingPanelRedesign(gui, CraftingPanelUpdateReason.StateChanged);
    }

    private static void SetCraftingRecipeWithStoredVariant(InventoryGui gui, int index, bool center)
    {
        gui.SetRecipe(index, center);
        ApplyCraftingRecipeVariantToGui(gui, index);
    }

    private static void ApplyCraftingRecipeVariantToGui(InventoryGui gui, int index)
    {
        if (gui == null || !TryGetCraftingRecipePair(gui, index, out InventoryGui.RecipeDataPair pair) || pair.Recipe == null || pair.ItemData != null)
        {
            return;
        }

        int variantCount = GetCraftingRecipeVariantCount(pair);
        if (variantCount <= 1)
        {
            gui.m_selectedVariant = 0;
            return;
        }

        gui.m_selectedVariant = GetCraftingRecipeVariant(pair);
    }

    private static int GetCraftingRecipeVariant(InventoryGui.RecipeDataPair pair)
    {
        if (pair.ItemData != null)
        {
            return Mathf.Clamp(pair.ItemData.m_variant, 0, Mathf.Max(0, GetCraftingRecipeVariantCount(pair) - 1));
        }

        if (pair.Recipe == null || GetCraftingRecipeVariantCount(pair) <= 1)
        {
            return 0;
        }

        int variant = CraftingRecipes.Variants.TryGetValue(pair.Recipe, out int stored) ? stored : 0;
        return Mathf.Clamp(variant, 0, GetCraftingRecipeVariantCount(pair) - 1);
    }

    private static void SetCraftingRecipeVariant(Recipe recipe, int variant)
    {
        if (recipe == null)
        {
            return;
        }

        int variantCount = GetRecipeVariantCount(recipe);
        int clamped = Mathf.Clamp(variant, 0, Mathf.Max(0, variantCount - 1));
        if (CraftingRecipes.Variants.TryGetValue(recipe, out int current) && current == clamped)
        {
            return;
        }

        CraftingRecipes.Variants[recipe] = clamped;
        _craftingRecipeVariantVersion++;
    }

    private static int GetCraftingRecipeVariantCount(InventoryGui.RecipeDataPair pair) =>
        pair.Recipe != null ? GetRecipeVariantCount(pair.Recipe) : 1;

    private static int GetRecipeVariantCount(Recipe recipe)
    {
        ItemData? item = recipe?.m_item != null ? recipe.m_item.m_itemData : null;
        if (item?.m_shared == null || item.m_shared.m_icons == null)
        {
            return 1;
        }

        int icons = Mathf.Max(1, item.m_shared.m_icons.Length);
        int variants = Mathf.Max(1, item.m_shared.m_variants);
        return Mathf.Min(icons, variants);
    }

    private static Sprite? GetCraftingRecipeIcon(InventoryGui.RecipeDataPair pair)
    {
        if (pair.ItemData != null)
        {
            return pair.ItemData.GetIcon();
        }

        if (pair.Recipe?.m_item == null)
        {
            return null;
        }

        ItemData item = pair.Recipe.m_item.m_itemData;
        int variant = GetCraftingRecipeVariant(pair);
        if (item.m_shared?.m_icons != null && variant >= 0 && variant < item.m_shared.m_icons.Length)
        {
            return item.m_shared.m_icons[variant];
        }

        return item.GetIcon();
    }

    private static void ConfigureCraftingRecipeCellAmountText(TMP_Text? text, string value, float cellSize)
    {
        ConfigureCraftingRecipeCellText(text, value, cellSize, TextAlignmentOptions.Bottom, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 1f));
    }

    private static void ConfigureCraftingRecipeCellQualityText(TMP_Text? text, string value, float cellSize)
    {
        ConfigureCraftingRecipeCellText(text, value, cellSize, TextAlignmentOptions.TopRight, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-1f, -1f));
    }

    private static void ConfigureCraftingRecipeCellText(TMP_Text? text, string value, float cellSize, TextAlignmentOptions alignment, Vector2 anchor, Vector2 pivot, Vector2 direction)
    {
        if (text == null)
        {
            return;
        }

        ApplyDefaultFontAsset(text);
        text.text = value;
        text.gameObject.SetActive(!string.IsNullOrEmpty(value));
        text.alignment = alignment;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.raycastTarget = false;
        text.fontSize = Mathf.Clamp(cellSize * 0.28f, 16f, 34f);
        RectTransform rect = text.rectTransform;
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = pivot;
        float margin = Mathf.Clamp(cellSize * 0.06f, 4f, 10f);
        float height = Mathf.Clamp(cellSize * 0.32f, 20f, 42f);
        rect.anchoredPosition = new Vector2(direction.x * margin, direction.y * margin);
        rect.sizeDelta = new Vector2(cellSize - margin * 2f, height);
        rect.localScale = Vector3.one;
        rect.localRotation = Quaternion.identity;
    }

    private static void SetCraftingRecipeCellChild(GameObject? child, bool active)
    {
        if (child == null)
        {
            return;
        }

        if (string.Equals(child.name, "selected", StringComparison.OrdinalIgnoreCase) &&
            child.TryGetComponent(out Image image))
        {
            if (!child.activeSelf)
            {
                child.SetActive(true);
            }

            image.enabled = true;
            Color color = image.color;
            color.a = active ? Mathf.Max(color.a, 0.38f) : 0f;
            image.color = color;
            return;
        }

        if (child.activeSelf != active)
        {
            child.SetActive(active);
        }
    }

    private static void SetCraftingRecipeFavoriteBorder(CraftingRecipeGridCell cell, bool active, bool upgradeFavorite)
    {
        RectTransform? border = EnsureCraftingRecipeFavoriteBorder(cell);
        if (border == null)
        {
            return;
        }

        foreach (Image image in border.GetComponentsInChildren<Image>(includeInactive: true))
        {
            image.color = upgradeFavorite
                ? new Color(0.2f, 0.95f, 0.38f, 0.96f)
                : new Color(1f, 0.82f, 0.1f, 0.96f);
        }

        border.gameObject.SetActive(active);
        if (active)
        {
            border.SetAsLastSibling();
        }
    }

    private static void SetCraftingRecipeSelectedBorder(CraftingRecipeGridCell cell, bool active)
    {
        RectTransform? border = active
            ? EnsureCraftingRecipeSelectedBorder(cell)
            : cell.Rect != null && !IsUnityNull(cell.Rect) && cell.Rect.Find(CraftingSelectedRecipeBorderName) is RectTransform existing
                ? existing
                : null;
        if (border == null)
        {
            return;
        }

        foreach (Image image in border.GetComponentsInChildren<Image>(includeInactive: true))
        {
            image.color = new Color(1f, 1f, 1f, 0.98f);
        }

        border.gameObject.SetActive(active);
        if (active)
        {
            border.SetAsLastSibling();
        }
    }

    private static void SetCraftingRecipePinnedTooltipMarker(CraftingRecipeGridCell cell, bool active, float cellSize)
    {
        RectTransform? marker = EnsureCraftingRecipePinnedTooltipMarker(cell);
        if (marker == null)
        {
            return;
        }

        ConfigureCraftingRecipePinnedTooltipMarker(marker, cellSize);
        marker.gameObject.SetActive(active);
        if (active)
        {
            marker.SetAsLastSibling();
        }
    }

    private static RectTransform? EnsureCraftingRecipePinnedTooltipMarker(CraftingRecipeGridCell cell)
    {
        if (cell.Rect == null || IsUnityNull(cell.Rect))
        {
            return null;
        }

        Transform existing = cell.Rect.Find(CraftingPinnedTooltipMarkerName);
        RectTransform? marker = existing != null ? existing.GetComponent<RectTransform>() : null;
        if (marker == null)
        {
            marker = CreateTextRect(CraftingPinnedTooltipMarkerName, cell.Rect, out TMP_Text text, active: false);
            text.text = "T";
        }

        return marker;
    }

    private static void ConfigureCraftingRecipePinnedTooltipMarker(RectTransform marker, float cellSize)
    {
        if (marker == null || IsUnityNull(marker))
        {
            return;
        }

        float margin = Mathf.Clamp(cellSize * 0.06f, 4f, 8f);
        float markerSize = Mathf.Clamp(cellSize * 0.34f, 20f, 28f);
        float fontSize = Mathf.Clamp(cellSize * 0.28f, 16f, 24f);
        string signature = $"{margin:0.###}|{markerSize:0.###}|{fontSize:0.###}";
        CraftingPinnedTooltipMarkerState state = marker.GetComponent<CraftingPinnedTooltipMarkerState>() ?? marker.gameObject.AddComponent<CraftingPinnedTooltipMarkerState>();
        if (string.Equals(state.LayoutSignature, signature, StringComparison.Ordinal))
        {
            return;
        }

        SetRectLayout(marker, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-margin, margin), new Vector2(markerSize, markerSize));

        TMP_Text? text = marker.GetComponent<TMP_Text>();
        if (text == null)
        {
            state.LayoutSignature = signature;
            return;
        }

        ApplyDefaultFontAsset(text);
        text.text = "T";
        text.alignment = TextAlignmentOptions.BottomRight;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.fontSize = fontSize;
        text.color = Color.white;
        text.outlineColor = new Color32(0, 0, 0, 230);
        text.outlineWidth = 0.18f;
        text.raycastTarget = false;
        state.LayoutSignature = signature;
    }

    private static RectTransform? EnsureCraftingRecipeSelectedBorder(CraftingRecipeGridCell cell)
    {
        if (cell.Rect == null || IsUnityNull(cell.Rect))
        {
            return null;
        }

        Transform existing = cell.Rect.Find(CraftingSelectedRecipeBorderName);
        if (existing is RectTransform existingRect)
        {
            return existingRect;
        }

        GameObject go = new(CraftingSelectedRecipeBorderName, typeof(RectTransform));
        RectTransform border = go.GetComponent<RectTransform>();
        border.SetParent(cell.Rect, false);
        border.anchorMin = Vector2.zero;
        border.anchorMax = Vector2.one;
        border.offsetMin = Vector2.zero;
        border.offsetMax = Vector2.zero;
        border.localScale = Vector3.one;
        border.localRotation = Quaternion.identity;
        CreateFavoriteBorderSide(border, "Top", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, FavoriteBorderThickness), Vector2.zero);
        CreateFavoriteBorderSide(border, "Bottom", new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, FavoriteBorderThickness), Vector2.zero);
        CreateFavoriteBorderSide(border, "Left", new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(FavoriteBorderThickness, 0f), Vector2.zero);
        CreateFavoriteBorderSide(border, "Right", new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(1f, 0.5f), new Vector2(FavoriteBorderThickness, 0f), Vector2.zero);
        go.SetActive(false);
        return border;
    }

    private static RectTransform? EnsureCraftingRecipeFavoriteBorder(CraftingRecipeGridCell cell)
    {
        if (cell.Rect == null || IsUnityNull(cell.Rect))
        {
            return null;
        }

        Transform existing = cell.Rect.Find(CraftingFavoriteBorderName);
        if (existing is RectTransform existingRect)
        {
            return existingRect;
        }

        GameObject go = new(CraftingFavoriteBorderName, typeof(RectTransform));
        RectTransform border = go.GetComponent<RectTransform>();
        border.SetParent(cell.Rect, false);
        border.anchorMin = Vector2.zero;
        border.anchorMax = Vector2.one;
        border.offsetMin = Vector2.zero;
        border.offsetMax = Vector2.zero;
        border.localScale = Vector3.one;
        border.localRotation = Quaternion.identity;
        CreateFavoriteBorderSide(border, "Top", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, FavoriteBorderThickness), Vector2.zero);
        CreateFavoriteBorderSide(border, "Bottom", new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, FavoriteBorderThickness), Vector2.zero);
        CreateFavoriteBorderSide(border, "Left", new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(FavoriteBorderThickness, 0f), Vector2.zero);
        CreateFavoriteBorderSide(border, "Right", new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(1f, 0.5f), new Vector2(FavoriteBorderThickness, 0f), Vector2.zero);
        go.SetActive(false);
        return border;
    }

    private static bool RectContainsCraftingRecipeIconArea(RectTransform grid, Vector2 screenPoint)
    {
        if (!TryGetLocalPointInRect(grid, screenPoint, out Vector2 localPoint))
        {
            return false;
        }

        Rect recipeIconArea = new(
            0f,
            -CraftingRecipeIconRows * CraftingRecipeGridCellSpace,
            CraftingRecipeGridColumns * CraftingRecipeGridCellSpace,
            CraftingRecipeIconRows * CraftingRecipeGridCellSpace);
        return recipeIconArea.Contains(localPoint);
    }
}

