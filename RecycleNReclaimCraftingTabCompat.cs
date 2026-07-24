using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace InventorySlots;

public sealed partial class InventorySlotsPlugin
{
    private const string RecycleNReclaimHudName = "InventorySlots_RecycleNReclaimHud";
    private const int RecycleNReclaimYieldColumn = 2;
    private const int RecycleNReclaimYieldColumns = CraftingRecipeGridColumns - RecycleNReclaimYieldColumn;

    private static readonly List<string> RecycleNReclaimHudImpediments = new();
    private static readonly List<RecycleNReclaimYieldTextEntry> RecycleNReclaimHudYields = new();
    private static RectTransform? _recycleNReclaimHudRect;
    private static TMP_Text? _recycleNReclaimHudText;
    private static string _recycleNReclaimHudSignature = "";
    private static object? _recycleNReclaimRecipeListSignatureListRef;
    private static int _recycleNReclaimRecipeListSignatureCount = -1;
    private static float _recycleNReclaimRecipeListSignatureExpiresAt;
    private static string _recycleNReclaimRecipeListSignature = "";

    private static void ApplyRecycleNReclaimCraftingRedesignVisibility(InventoryGui gui)
    {
        SetCraftingVanillaDetailVisible(gui, visible: false);
        _craftingVanillaDetailHidden = true;
        EnsureCraftingVanillaPanelBackgroundsHidden(gui);
    }

    private static bool ShouldIncludeRecycleNReclaimRecipeInView(InventoryGui.RecipeDataPair pair) =>
        pair.ItemData != null && RecipeMatchesCraftingSearch(pair);

    private static int CompareRecycleNReclaimRecipeViewEntries(CraftingRecipeViewEntry a, CraftingRecipeViewEntry b)
    {
        CraftingRecipeSortMode reclaimMode = _craftingRecipeSortMode?.Value ?? CraftingRecipeSortMode.TierThenGroup;
        int reclaimSortComparison = SortKeyComparerCore.Compare(a.SortKey, b.SortKey, reclaimMode);
        return reclaimSortComparison != 0 ? reclaimSortComparison : a.OriginalIndex.CompareTo(b.OriginalIndex);
    }

    private static bool TryGetRecycleNReclaimRecipeActionAvailable(int originalIndex, InventoryGui.RecipeDataPair pair, out bool available)
    {
        available = false;
        if (!IsRecycleNReclaimReclaimTabActive(InventoryGui.instance))
        {
            return false;
        }

        available = originalIndex >= 0 && TryGetRecycleNReclaimRecyclingImpedimentCount(originalIndex, out int impediments)
            ? impediments == 0
            : pair.CanCraft;
        return true;
    }

    private static string GetRecycleNReclaimRecipeDisplayName(InventoryGui.RecipeDataPair pair)
    {
        if (pair.ItemData == null)
        {
            return "";
        }

        string text = Localization.instance.Localize(pair.ItemData.m_shared.m_name);
        if (pair.ItemData.m_stack > 1 && pair.ItemData.m_shared.m_maxStackSize > 1)
        {
            text += $" x{pair.ItemData.m_stack}";
        }

        return text;
    }

    private static string GetRecycleNReclaimRecipeTooltip(InventoryGui.RecipeDataPair pair)
    {
        if (pair.ItemData == null)
        {
            return "";
        }

        int quality = Mathf.Max(1, pair.ItemData.m_quality);
        int amount = Mathf.Max(1, pair.ItemData.m_stack);
        return GetLocalizedStaticItemTooltip(pair.ItemData, quality, crafting: false, amount);
    }

    private static string GetRecycleNReclaimRecipeCellAmountText(InventoryGui gui, InventoryGui.RecipeDataPair pair)
    {
        return IsRecycleNReclaimReclaimTabActive(gui) && pair.ItemData != null && pair.ItemData.m_stack > 1
            ? pair.ItemData.m_stack.ToString()
            : "";
    }

    private static string GetRecycleNReclaimRecipeListSignature(InventoryGui? gui)
    {
        if (gui?.m_availableRecipes == null)
        {
            return "";
        }

        object listRef = gui.m_availableRecipes;
        int count = gui.m_availableRecipes.Count;
        if (ReferenceEquals(_recycleNReclaimRecipeListSignatureListRef, listRef) &&
            _recycleNReclaimRecipeListSignatureCount == count &&
            Time.unscaledTime < _recycleNReclaimRecipeListSignatureExpiresAt)
        {
            return _recycleNReclaimRecipeListSignature;
        }

        unchecked
        {
            int hash = 17;
            for (int i = 0; i < count; i++)
            {
                InventoryGui.RecipeDataPair pair = gui.m_availableRecipes[i];
                ItemDrop.ItemData? item = pair.ItemData;
                hash = hash * 31 + i;
                hash = hash * 31 + (pair.Recipe != null ? System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(pair.Recipe) : 0);
                hash = hash * 31 + (item != null ? System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(item) : 0);
                if (item == null)
                {
                    continue;
                }

                hash = hash * 31 + CleanPrefabName(GetItemPrefabName(item)).GetHashCode();
                hash = hash * 31 + item.m_quality;
                hash = hash * 31 + item.m_variant;
                hash = hash * 31 + item.m_stack;
                hash = hash * 31 + item.m_gridPos.x;
                hash = hash * 31 + item.m_gridPos.y;
                hash = hash * 31 + (item.m_equipped ? 1 : 0);
            }

            _recycleNReclaimRecipeListSignatureListRef = listRef;
            _recycleNReclaimRecipeListSignatureCount = count;
            _recycleNReclaimRecipeListSignatureExpiresAt = Time.unscaledTime + RecycleNReclaimSignatureCacheSeconds;
            _recycleNReclaimRecipeListSignature = $"{count}:{hash}";
            return _recycleNReclaimRecipeListSignature;
        }
    }

    private static void ClearRecycleNReclaimRecipeListSignatureCache()
    {
        _recycleNReclaimRecipeListSignatureListRef = null;
        _recycleNReclaimRecipeListSignatureCount = -1;
        _recycleNReclaimRecipeListSignatureExpiresAt = 0f;
        _recycleNReclaimRecipeListSignature = "";
    }

    private static void LayoutRecycleNReclaimBottomControls(InventoryGui gui, RectTransform grid)
    {
        if (CraftingRecipes.View.Count == 0 || FindCraftingRecipeViewIndex(gui.GetSelectedRecipeIndex()) < 0)
        {
            HideCraftingRedesignBottomControls(gui);
            HideRecycleNReclaimHud();
            if (_craftingCountInputRect != null)
            {
                _craftingCountInputRect.gameObject.SetActive(false);
            }

            CraftingController.StoreBottomControlsSignature(CraftingBottomControlsHiddenSignature);
            return;
        }

        Vector2 offset = CraftingBottomControlsFixedOffset;
        Vector2 craftButtonSize = new(CraftingRecipeGridCellSize + CraftingRecipeGridCellSpace, 42f);
        Vector2 craftPosition = GetCraftingGridCenteredPosition(grid, 0, CraftingBottomControlRow, craftButtonSize, columnSpan: 2) + offset + CraftingCraftButtonFixedOffset;
        string layoutSignature = GetRecycleNReclaimBottomControlsLayoutSignature(gui, grid);
        bool updateLayout = CraftingController.NeedsBottomControlsLayout(layoutSignature);

        if (_craftingCountInputRect != null)
        {
            _craftingCountInputRect.gameObject.SetActive(false);
        }

        if (_craftingUpgradeProgressionRect != null)
        {
            _craftingUpgradeProgressionRect.gameObject.SetActive(false);
        }

        HideCraftingSocketWarning();
        if (gui.m_minStationLevelIcon != null)
        {
            gui.m_minStationLevelIcon.gameObject.SetActive(false);
        }

        if (gui.m_minStationLevelText != null)
        {
            gui.m_minStationLevelText.gameObject.SetActive(false);
        }

        HideCraftingRequiredStationLevelTooltip();

        if (gui.m_craftButton != null && updateLayout)
        {
            RectTransform craftRect = (RectTransform)gui.m_craftButton.transform;
            SetCraftingTopLeftRect(gui.m_crafting, craftRect, craftPosition, craftButtonSize);
            SetActionButtonTextAutoSize(gui.m_craftButton);
        }

        if (gui.m_craftProgressPanel is RectTransform progressRect && updateLayout)
        {
            SetCraftingTopLeftRect(gui.m_crafting, progressRect, craftPosition, craftButtonSize);
            gui.m_craftProgressBar?.SetWidth(craftButtonSize.x);
        }

        bool hasSummary = TryPrepareRecycleNReclaimSelectedSummary(gui);
        LayoutRecycleNReclaimYieldIcons(gui, grid, hasSummary ? RecycleNReclaimHudYields : null);
        LayoutRecycleNReclaimHud(gui, grid, hasSummary, RecycleNReclaimHudImpediments, RecycleNReclaimHudYields);
        HideRecycleNReclaimVanillaDetailUi(gui);

        if (gui.m_craftButton != null)
        {
            ApplyCraftingActionButtonTextStateColor(gui.m_craftButton, gui.m_craftButton.interactable);
        }

        CraftingController.StoreBottomControlsSignature(layoutSignature);
    }

    private static string GetRecycleNReclaimBottomControlsLayoutSignature(InventoryGui gui, RectTransform grid)
    {
        return $"{gui.m_crafting.GetInstanceID()}|{grid.GetInstanceID()}|{grid.anchoredPosition.x:0.###}|{grid.anchoredPosition.y:0.###}|{GetUnityObjectId(gui.m_craftButton)}|{GetUnityObjectId(gui.m_craftProgressPanel as UnityEngine.Object)}|{CraftingRecipeGridCellSize:0.###}|{CraftingRecipeGridCellSpace:0.###}|reclaim|ownedReq={CraftingRequirements.OwnedSlots.Count}";
    }

    private static void LayoutRecycleNReclaimHud(
        InventoryGui gui,
        RectTransform grid,
        bool hasSummary,
        IReadOnlyList<string> impediments,
        IReadOnlyList<RecycleNReclaimYieldTextEntry> yields)
    {
        string text = BuildRecycleNReclaimHudText(gui, hasSummary, impediments, yields);
        if (string.IsNullOrWhiteSpace(text))
        {
            HideRecycleNReclaimHud();
            return;
        }

        RectTransform? hud = EnsureRecycleNReclaimHud(gui);
        if (hud == null || _recycleNReclaimHudText == null)
        {
            return;
        }

        Vector2 position = GetCraftingGridCellPosition(grid, 0, CraftingSocketWarningRow) + CraftingBottomControlsFixedOffset;
        Vector2 size = new(
            CraftingRecipeGridColumns * CraftingRecipeGridCellSpace - (CraftingRecipeGridCellSpace - CraftingRecipeGridCellSize),
            CraftingSocketWarningHeight);
        string signature = $"{gui.m_crafting.GetInstanceID()}|{grid.GetInstanceID()}|{position.x:0.###}|{position.y:0.###}|{size.x:0.###}|{size.y:0.###}|{text}";
        if (!string.Equals(_recycleNReclaimHudSignature, signature, StringComparison.Ordinal))
        {
            SetCraftingTopLeftRect(gui.m_crafting, hud, position, size);
            ConfigureRecycleNReclaimHudText(_recycleNReclaimHudText, text);
            _recycleNReclaimHudSignature = signature;
        }

        hud.SetAsLastSibling();
        hud.gameObject.SetActive(true);
    }

    private static RectTransform? EnsureRecycleNReclaimHud(InventoryGui gui)
    {
        if (gui.m_crafting == null)
        {
            return null;
        }

        if (_recycleNReclaimHudRect != null &&
            !IsUnityNull(_recycleNReclaimHudRect) &&
            _recycleNReclaimHudRect!.parent == gui.m_crafting)
        {
            return _recycleNReclaimHudRect;
        }

        Transform? existing = gui.m_crafting.Find(RecycleNReclaimHudName);
        _recycleNReclaimHudRect = existing != null ? existing.GetComponent<RectTransform>() : null;
        if (_recycleNReclaimHudRect == null)
        {
            _recycleNReclaimHudRect = new GameObject(RecycleNReclaimHudName, typeof(RectTransform)).GetComponent<RectTransform>();
        }

        _recycleNReclaimHudRect.SetParent(gui.m_crafting, false);
        if (_recycleNReclaimHudRect.TryGetComponent(out Image background))
        {
            background.enabled = false;
            background.raycastTarget = false;
        }

        Transform? textTransform = _recycleNReclaimHudRect.Find("Text");
        RectTransform textRect;
        if (textTransform != null && textTransform.GetComponent<TMP_Text>() is { } existingText)
        {
            textRect = (RectTransform)textTransform;
            _recycleNReclaimHudText = existingText;
        }
        else
        {
            textRect = CreateTextRect("Text", _recycleNReclaimHudRect, out _recycleNReclaimHudText);
        }

        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(12f, 5f);
        textRect.offsetMax = new Vector2(-12f, -5f);
        textRect.localScale = Vector3.one;
        textRect.localRotation = Quaternion.identity;
        SetStretchRectLayout(textRect, new Vector2(12f, 5f), new Vector2(-12f, -5f));
        ConfigureRecycleNReclaimHudText(_recycleNReclaimHudText, _recycleNReclaimHudText.text);
        return _recycleNReclaimHudRect;
    }

    private static bool TryPrepareRecycleNReclaimSelectedSummary(InventoryGui gui)
    {
        int selectedIndex = GetSelectedCraftingRecipeIndexSafe(gui);
        if (selectedIndex < 0)
        {
            RecycleNReclaimHudImpediments.Clear();
            RecycleNReclaimHudYields.Clear();
            return false;
        }

        if (TryGetRecycleNReclaimSummary(selectedIndex, RecycleNReclaimHudImpediments, RecycleNReclaimHudYields))
        {
            return true;
        }

        if (TryGetRecycleNReclaimApi(out RecycleNReclaimApi? api) && api != null && api.IsRecycleTabActive())
        {
            api.TryRefreshSelectedRecipeUi();
        }

        return TryGetRecycleNReclaimSummary(selectedIndex, RecycleNReclaimHudImpediments, RecycleNReclaimHudYields);
    }

    private static void LayoutRecycleNReclaimYieldIcons(InventoryGui gui, RectTransform grid, IReadOnlyList<RecycleNReclaimYieldTextEntry>? yields)
    {
        HideCraftingVanillaRequirementSlots(gui);

        if (yields != null)
        {
            int yieldIndex = 0;
            int slotCount = Mathf.Max(RecycleNReclaimYieldColumns, CraftingRequirements.OwnedSlots.Count);
            for (int i = 0; i < slotCount; i++)
            {
                if (yieldIndex >= yields.Count || i >= RecycleNReclaimYieldColumns)
                {
                    if (i < CraftingRequirements.OwnedSlots.Count &&
                        CraftingRequirements.OwnedSlots[i] is { } staleSlot &&
                        !IsUnityNull(staleSlot))
                    {
                        staleSlot.gameObject.SetActive(false);
                    }

                    continue;
                }

                RecycleNReclaimYieldTextEntry yield = yields[yieldIndex++];
                RectTransform? rect = EnsureOwnedCraftingRequirementSlot(gui, i);
                if (rect == null || IsUnityNull(rect))
                {
                    continue;
                }

                rect.gameObject.SetActive(true);
                int column = RecycleNReclaimYieldColumn + i;
                Vector2 position = GetCraftingGridCellPosition(grid, column, CraftingBottomControlRow) + CraftingBottomControlsFixedOffset;
                SetCraftingTopLeftRect(gui.m_crafting, rect, position, new Vector2(CraftingRecipeGridCellSize, CraftingRecipeGridCellSize));

                ConfigureRecycleNReclaimYieldIcon(rect, yield);
            }

            return;
        }

        HideOwnedCraftingRequirementSlots();
    }

    private static void ConfigureRecycleNReclaimYieldIcon(RectTransform rect, RecycleNReclaimYieldTextEntry yield)
    {
        CraftingRequirementUiMarker marker = GetCraftingRequirementUiMarker(rect);
        HideCraftingRequirementSlotBackground(marker);
        const float iconSize = 44f;
        string layoutSignature = $"reclaim-yield|{CraftingRecipeGridCellSize:0.###}|{iconSize:0.###}";
        bool updateLayout = !string.Equals(marker.LayoutSignature, layoutSignature, StringComparison.Ordinal);

        if (marker.Name != null && !IsUnityNull(marker.Name))
        {
            marker.Name.gameObject.SetActive(false);
        }

        if (marker.Hitbox != null && !IsUnityNull(marker.Hitbox))
        {
            marker.Hitbox.gameObject.SetActive(false);
        }

        if (marker.Icon != null && !IsUnityNull(marker.Icon))
        {
            RectTransform icon = marker.Icon;
            icon.gameObject.SetActive(true);
            if (updateLayout)
            {
                icon.anchorMin = new Vector2(0f, 1f);
                icon.anchorMax = new Vector2(0f, 1f);
                icon.pivot = new Vector2(0f, 1f);
                icon.anchoredPosition = new Vector2((CraftingRecipeGridCellSize - iconSize) * 0.5f, -(CraftingRecipeGridCellSize - iconSize) * 0.5f);
                icon.sizeDelta = new Vector2(iconSize, iconSize);
                icon.localScale = Vector3.one;
            }

            if (marker.IconImage != null && !IsUnityNull(marker.IconImage))
            {
                if (yield.Item != null)
                {
                    marker.IconImage.sprite = yield.Item.GetIcon();
                }

                marker.IconImage.color = Color.white;
                marker.IconImage.raycastTarget = false;
            }

            ConfigureSimpleTooltip(icon.gameObject, LocalizeUi(yield.Name, yield.Name), enabled: true);
        }

        if (marker.Amount != null && !IsUnityNull(marker.Amount))
        {
            RectTransform amount = marker.Amount;
            amount.gameObject.SetActive(true);
            if (updateLayout)
            {
                amount.anchorMin = new Vector2(0.5f, 0f);
                amount.anchorMax = new Vector2(0.5f, 0f);
                amount.pivot = new Vector2(0.5f, 0f);
                amount.anchoredPosition = new Vector2(0f, 4f);
                amount.sizeDelta = new Vector2(CraftingRecipeGridCellSize - 4f, 20f);
                amount.localScale = Vector3.one;
            }

            if (marker.AmountText != null && !IsUnityNull(marker.AmountText))
            {
                ApplyDefaultFontAsset(marker.AmountText);
                marker.AmountText.text = yield.Amount.ToString();
                marker.AmountText.color = Color.white;
                if (updateLayout)
                {
                    marker.AmountText.alignment = TextAlignmentOptions.Bottom;
                    marker.AmountText.enableAutoSizing = true;
                    marker.AmountText.fontSizeMin = 10f;
                    marker.AmountText.fontSizeMax = 16f;
                    marker.AmountText.textWrappingMode = TextWrappingModes.NoWrap;
                    marker.AmountText.overflowMode = TextOverflowModes.Overflow;
                }
            }
        }

        RectTransform hitbox = EnsureCraftingRequirementHitbox(rect, marker);
        DisableCompetingCraftingRequirementTooltips(rect, hitbox, marker);
        ConfigureSimpleTooltip(hitbox.gameObject, LocalizeUi(yield.Name, yield.Name), enabled: true);
        marker.LayoutSignature = layoutSignature;
    }

    private static void ConfigureRecycleNReclaimHudText(TMP_Text text, string value)
    {
        if (text == null || IsUnityNull(text))
        {
            return;
        }

        ApplyDefaultFontAsset(text);
        text.text = value;
        text.color = new Color(1f, 0.86f, 0.55f, 1f);
        text.alignment = TextAlignmentOptions.Left;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.overflowMode = TextOverflowModes.Truncate;
        text.enableAutoSizing = true;
        text.fontSizeMin = 10f;
        text.fontSizeMax = 16f;
        text.maxVisibleLines = 2;
        text.raycastTarget = false;
    }

    private static string BuildRecycleNReclaimHudText(
        InventoryGui gui,
        bool hasSummary,
        IReadOnlyList<string> impediments,
        IReadOnlyList<RecycleNReclaimYieldTextEntry> yields)
    {
        int selectedIndex = GetSelectedCraftingRecipeIndexSafe(gui);
        if (selectedIndex < 0)
        {
            return "";
        }

        string status = BuildRecycleNReclaimStatusText(gui, hasSummary ? impediments : null);
        string detail = BuildRecycleNReclaimDetailText(gui);
        if (string.IsNullOrWhiteSpace(detail) && !HasVisibleRecycleNReclaimYieldIcons(gui))
        {
            detail = BuildRecycleNReclaimYieldText(hasSummary ? yields : null);
        }

        if (string.IsNullOrWhiteSpace(status))
        {
            return detail;
        }

        if (string.IsNullOrWhiteSpace(detail))
        {
            return status;
        }

        return $"{status}\n{detail}";
    }

    private static string BuildRecycleNReclaimStatusText(InventoryGui gui, IReadOnlyList<string>? impediments)
    {
        string description = gui.m_recipeDecription != null && !IsUnityNull(gui.m_recipeDecription)
            ? SanitizeRecycleNReclaimHudText(gui.m_recipeDecription.text ?? "")
            : "";
        if (!string.IsNullOrWhiteSpace(description))
        {
            return description;
        }

        if (impediments != null && impediments.Count > 0)
        {
            return $"{LocalizeUi("$inventoryslots_reclaim_blocked", "Blocked")}: {JoinRecycleNReclaimTextParts(impediments.Select(SanitizeRecycleNReclaimHudText), 3)}";
        }

        return LocalizeUi("$azumatt_recycle_n_reclaim_requirements_fulfilled", "Recycling requirements fulfilled");
    }

    private static string BuildRecycleNReclaimDetailText(InventoryGui gui)
    {
        string detail = gui.m_itemCraftType != null && !IsUnityNull(gui.m_itemCraftType)
            ? SanitizeRecycleNReclaimHudText(gui.m_itemCraftType.text ?? "")
            : "";
        return detail;
    }

    private static string BuildRecycleNReclaimYieldText(IReadOnlyList<RecycleNReclaimYieldTextEntry>? yields)
    {
        IEnumerable<string> parts = yields != null && yields.Count > 0
            ? yields.Select(entry => $"{LocalizeUi(entry.Name, entry.Name)} x{entry.Amount}")
            : Enumerable.Empty<string>();
        string joined = JoinRecycleNReclaimTextParts(parts, 5);
        return string.IsNullOrWhiteSpace(joined)
            ? ""
            : $"{LocalizeUi("$azumatt_recycle_n_reclaim_tooltip_yield_header", "Yield")}: {joined}";
    }

    private static bool HasVisibleRecycleNReclaimYieldIcons(InventoryGui gui)
    {
        return CraftingRequirements.OwnedSlots.Any(slot => slot != null && !IsUnityNull(slot) && slot.gameObject.activeSelf);
    }

    private static string JoinRecycleNReclaimTextParts(IEnumerable<string> parts, int maxParts)
    {
        List<string> values = parts
            .Where(part => !string.IsNullOrWhiteSpace(part))
            .Select(part => part.Trim())
            .ToList();
        if (values.Count == 0)
        {
            return "";
        }

        int take = Mathf.Clamp(maxParts, 1, values.Count);
        string text = string.Join("; ", values.Take(take));
        int remaining = values.Count - take;
        return remaining > 0 ? $"{text}; +{remaining}" : text;
    }

    private static string SanitizeRecycleNReclaimHudText(string text)
    {
        string plain = StripRichText(text)
            .Replace("\r\n", "\n")
            .Replace('\r', '\n');
        string[] lines = plain
            .Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Trim())
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .ToArray();
        return string.Join("; ", lines);
    }

    private static void HideRecycleNReclaimVanillaDetailUi(InventoryGui gui)
    {
        if (gui.m_recipeIcon != null)
        {
            gui.m_recipeIcon.gameObject.SetActive(false);
        }

        if (gui.m_recipeName != null)
        {
            gui.m_recipeName.gameObject.SetActive(false);
        }

        if (gui.m_recipeDecription != null)
        {
            gui.m_recipeDecription.gameObject.SetActive(false);
        }

        if (gui.m_itemCraftType != null)
        {
            gui.m_itemCraftType.gameObject.SetActive(false);
        }

        if (gui.m_variantButton != null)
        {
            gui.m_variantButton.gameObject.SetActive(false);
        }

        if (gui.m_qualityPanel != null)
        {
            gui.m_qualityPanel.gameObject.SetActive(false);
        }
    }

    private static void HideRecycleNReclaimHud()
    {
        if (_recycleNReclaimHudRect != null && !IsUnityNull(_recycleNReclaimHudRect))
        {
            _recycleNReclaimHudRect.gameObject.SetActive(false);
        }

        _recycleNReclaimHudSignature = "";
    }
}
