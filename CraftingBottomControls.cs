using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using ItemData = ItemDrop.ItemData;
using Requirement = Piece.Requirement;

namespace InventorySlots;

public sealed partial class InventorySlotsPlugin
{
    private static void LayoutCraftingBottomControls(InventoryGui gui, RectTransform grid)
    {
        HideRecycleNReclaimHud();
        if (CraftingRecipes.View.Count == 0 || FindCraftingRecipeViewIndex(gui.GetSelectedRecipeIndex()) < 0)
        {
            HideCraftingRedesignBottomControls(gui);
            HideCraftingSocketWarning();
            if (_craftingCountInputRect != null)
            {
                _craftingCountInputRect.gameObject.SetActive(false);
            }

            CraftingController.StoreBottomControlsSignature(CraftingBottomControlsHiddenSignature);
            return;
        }

        bool jewelcraftingSocketTab = IsJewelcraftingSocketTabActive(gui);
        Vector2 offset = CraftingBottomControlsFixedOffset;
        Vector2 craftButtonSize = new(CraftingRecipeGridCellSize + CraftingRecipeGridCellSpace, 42f);
        Vector2 countInputSize = new(CraftingRecipeGridCellSize, 42f);
        Vector2 craftPosition = GetCraftingGridCenteredPosition(grid, 0, CraftingBottomControlRow, craftButtonSize, columnSpan: 2) + offset + CraftingCraftButtonFixedOffset;
        Vector2 countPosition = GetCraftingGridCenteredPosition(grid, 2, CraftingBottomControlRow, countInputSize) + offset + CraftingCountInputFixedOffset;
        Vector2 requirementPosition = GetCraftingGridCellPosition(grid, 3, CraftingBottomControlRow) + offset;
        Vector2 requiredStationPosition = GetCraftingGridCellPosition(grid, 7, CraftingBottomControlRow) + offset;
        RectTransform? countRect = jewelcraftingSocketTab ? null : EnsureCraftingCountInput(gui);
        RectTransform? upgradeProgressionRect = jewelcraftingSocketTab ? null : EnsureCraftingUpgradeProgression(gui);
        List<Requirement> visibleRequirements = GetVisibleCraftingRequirements(gui);
        string layoutSignature = GetCraftingBottomControlsLayoutSignature(gui, grid, countRect, upgradeProgressionRect, visibleRequirements.Count);
        bool updateLayout = CraftingController.NeedsBottomControlsLayout(layoutSignature);

        UpdateCraftingActionControls(gui, countRect, upgradeProgressionRect, craftPosition, countPosition, craftButtonSize, countInputSize, updateLayout);
        UpdateCraftingStationAndWarningControls(gui, grid, requiredStationPosition, updateLayout);
        UpdateCraftingRequirementStrip(gui, requirementPosition, visibleRequirements, updateLayout);

        CraftingController.StoreBottomControlsSignature(layoutSignature);
    }

    private static void HideCraftingRedesignBottomControls(InventoryGui gui)
    {
        SuppressCraftingRequiredStationLevelOriginalBackground(gui);

        if (gui.m_craftButton != null)
        {
            gui.m_craftButton.gameObject.SetActive(false);
        }

        if (gui.m_craftProgressPanel is RectTransform progressPanel)
        {
            progressPanel.gameObject.SetActive(false);
        }

        if (gui.m_minStationLevelIcon != null)
        {
            gui.m_minStationLevelIcon.gameObject.SetActive(false);
        }

        if (gui.m_minStationLevelText != null)
        {
            gui.m_minStationLevelText.gameObject.SetActive(false);
        }

        HideCraftingRequiredStationLevelTooltip();

        if (gui.m_itemCraftType != null)
        {
            gui.m_itemCraftType.gameObject.SetActive(false);
        }

        HideCraftingSocketWarning();

        if (_craftingUpgradeProgressionRect != null)
        {
            _craftingUpgradeProgressionRect.gameObject.SetActive(false);
        }

        HideCraftingVanillaRequirementSlots(gui);
        HideOwnedCraftingRequirementSlots();

        CraftingController.MarkBottomControlsDirty();
    }

    private static string GetCraftingBottomControlsLayoutSignature(InventoryGui gui, RectTransform grid, RectTransform? countRect, RectTransform? upgradeProgressionRect, int visibleRequirementCount)
    {
        return $"{gui.m_crafting.GetInstanceID()}|{grid.GetInstanceID()}|{grid.anchoredPosition.x:0.###}|{grid.anchoredPosition.y:0.###}|{GetUnityObjectId(gui.m_craftButton)}|{GetUnityObjectId(gui.m_craftProgressPanel as UnityEngine.Object)}|{GetUnityObjectId(countRect)}|{GetUnityObjectId(upgradeProgressionRect)}|{GetUnityObjectId(gui.m_minStationLevelIcon)}|{GetUnityObjectId(gui.m_minStationLevelText)}|{visibleRequirementCount}|ownedReq={CraftingRequirements.OwnedSlots.Count}|{CraftingRecipeGridCellSize:0.###}|{CraftingRecipeGridCellSpace:0.###}|socket={IsJewelcraftingSocketTabActive(gui)}";
    }

    private static List<Requirement> GetVisibleCraftingRequirements(InventoryGui gui)
    {
        CraftingRequirements.VisibleRequirements.Clear();
        CraftingRequirements.VisibleRequirementCandidates.Clear();
        Recipe? recipe = gui.m_selectedRecipe.Recipe;
        int quality = GetSelectedCraftingQuality(gui);
        if (recipe == null || recipe.m_resources == null)
        {
            return CraftingRequirements.VisibleRequirements;
        }

        if (IsJewelcraftingSocketTabActive(gui) && ShouldHideJewelcraftingSocketRequirements(gui.m_selectedRecipe))
        {
            return CraftingRequirements.VisibleRequirements;
        }

        Player? player = Player.m_localPlayer;
        foreach (Requirement requirement in recipe.m_resources)
        {
            if (requirement == null || requirement.m_resItem == null || requirement.GetAmount(quality) <= 0)
            {
                continue;
            }

            if (recipe.m_requireOnlyOneIngredient &&
                player != null &&
                requirement.m_resItem.m_itemData?.m_shared != null &&
                !player.IsKnownMaterial(requirement.m_resItem.m_itemData.m_shared.m_name))
            {
                continue;
            }

            CraftingRequirements.VisibleRequirementCandidates.Add(requirement);
        }

        int startIndex = 0;
        if (CraftingRequirements.VisibleRequirementCandidates.Count > CraftingVisibleRequirementSlots)
        {
            int pageCount = Mathf.CeilToInt(CraftingRequirements.VisibleRequirementCandidates.Count / (float)CraftingVisibleRequirementSlots);
            startIndex = (int)Time.fixedTime % Mathf.Max(1, pageCount) * CraftingVisibleRequirementSlots;
        }

        for (int i = startIndex; i < CraftingRequirements.VisibleRequirementCandidates.Count && CraftingRequirements.VisibleRequirements.Count < CraftingVisibleRequirementSlots; i++)
        {
            CraftingRequirements.VisibleRequirements.Add(CraftingRequirements.VisibleRequirementCandidates[i]);
        }

        return CraftingRequirements.VisibleRequirements;
    }

    private static bool ShouldShowCraftingStatusHud(InventoryGui gui)
    {
        if (ShouldShowUpgradeCraftingStatusHud(gui))
        {
            return true;
        }

        if (IsJewelcraftingSocketTabActive(gui))
        {
            return true;
        }

        return IsJewelcraftingGemcutterStationActive() &&
               IsCraftingCraftTabSelected(gui) &&
               IsJewelcraftingGemCuttingRecipe(gui.m_selectedRecipe.Recipe);
    }

    private static bool ShouldShowUpgradeCraftingStatusHud(InventoryGui gui) =>
        gui.InUpradeTab() &&
        !IsJewelcraftingSocketTabActive(gui) &&
        !IsRecycleNReclaimReclaimTabActive(gui) &&
        gui.m_selectedRecipe.ItemData?.m_shared != null &&
        gui.m_selectedRecipe.Recipe != null;

    private static void LayoutCraftingStatusHud(InventoryGui gui, RectTransform grid, bool updateLayout)
    {
        string warning = GetCraftingStatusHudText(gui);
        if (string.IsNullOrWhiteSpace(warning))
        {
            HideCraftingSocketWarning();
            return;
        }

        RectTransform? warningRect = EnsureCraftingSocketWarning(gui);
        if (warningRect == null || CraftingUi.SocketWarningText == null)
        {
            return;
        }

        Vector2 position = GetCraftingGridCellPosition(grid, 0, CraftingSocketWarningRow) + CraftingBottomControlsFixedOffset;
        Vector2 size = new(
            CraftingRecipeGridColumns * CraftingRecipeGridCellSpace - (CraftingRecipeGridCellSpace - CraftingRecipeGridCellSize),
            CraftingSocketWarningHeight);
        CraftingStatusHudStamp stamp = new(
            gui.m_crafting.GetInstanceID(),
            grid.GetInstanceID(),
            position.x,
            position.y,
            size.x,
            size.y,
            warning);

        if (updateLayout || !CraftingController.CanReuseSocketWarning(stamp))
        {
            SetCraftingTopLeftRect(gui.m_crafting, warningRect, position, size);
            ConfigureCraftingSocketWarningText(CraftingUi.SocketWarningText, warning);
            CraftingController.StoreSocketWarningStamp(stamp);
        }

        warningRect.SetAsLastSibling();
        warningRect.gameObject.SetActive(true);
    }

    private static string GetCraftingStatusHudText(InventoryGui gui)
    {
        if (ShouldShowUpgradeCraftingStatusHud(gui))
        {
            return GetUpgradeCraftingStatusText(gui);
        }

        string text = gui.m_itemCraftType != null && !IsUnityNull(gui.m_itemCraftType)
            ? gui.m_itemCraftType.text ?? ""
            : "";

        text = StripRichText(text).Trim();
        return text;
    }

    private static string GetUpgradeCraftingStatusText(InventoryGui gui)
    {
        ItemData? item = gui.m_selectedRecipe.ItemData;
        if (item?.m_shared == null)
        {
            return "";
        }

        int currentQuality = Mathf.Max(1, item.m_quality);
        int maxQuality = Mathf.Max(currentQuality, item.m_shared.m_maxQuality);
        if (currentQuality >= maxQuality)
        {
            return Localization.instance != null
                ? Localization.instance.Localize("$inventory_maxquality")
                : "Max quality";
        }

        string itemName = GetLocalizedItemName(item);
        string nextQuality = (currentQuality + 1).ToString();
        return Localization.instance != null
            ? Localization.instance.Localize("$inventory_upgrade", new[] { itemName, nextQuality })
            : $"Upgrade {itemName} quality to {nextQuality}";
    }

    private static RectTransform? EnsureCraftingSocketWarning(InventoryGui gui)
    {
        if (gui.m_crafting == null)
        {
            return null;
        }

        if (CraftingUi.SocketWarningRect != null &&
            !IsUnityNull(CraftingUi.SocketWarningRect) &&
            CraftingUi.SocketWarningRect!.parent == gui.m_crafting)
        {
            return CraftingUi.SocketWarningRect;
        }

        Transform? existing = gui.m_crafting.Find(CraftingSocketWarningName);
        CraftingUi.SocketWarningRect = existing != null ? existing.GetComponent<RectTransform>() : null;
        if (CraftingUi.SocketWarningRect == null)
        {
            CraftingUi.SocketWarningRect = new GameObject(CraftingSocketWarningName, typeof(RectTransform)).GetComponent<RectTransform>();
        }

        CraftingUi.SocketWarningRect.SetParent(gui.m_crafting, false);
        if (CraftingUi.SocketWarningRect.TryGetComponent(out Image background))
        {
            background.enabled = false;
            background.raycastTarget = false;
        }

        Transform? textTransform = CraftingUi.SocketWarningRect.Find("Text");
        RectTransform textRect;
        if (textTransform != null && textTransform.GetComponent<TMP_Text>() is { } existingText)
        {
            textRect = (RectTransform)textTransform;
            CraftingUi.SocketWarningText = existingText;
        }
        else
        {
            textRect = CreateTextRect("Text", CraftingUi.SocketWarningRect, out TMP_Text socketWarningText);
            CraftingUi.SocketWarningText = socketWarningText;
        }

        SetStretchRectLayout(textRect, new Vector2(12f, 5f), new Vector2(-12f, -5f));
        TMP_Text warningText = CraftingUi.SocketWarningText!;
        ConfigureCraftingSocketWarningText(warningText, warningText.text);
        return CraftingUi.SocketWarningRect;
    }

    private static void ConfigureCraftingSocketWarningText(TMP_Text text, string warning)
    {
        if (text == null || IsUnityNull(text))
        {
            return;
        }

        ApplyDefaultFontAsset(text);
        text.text = warning;
        text.color = new Color(1f, 0.72f, 0.28f, 1f);
        text.alignment = TextAlignmentOptions.Center;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.overflowMode = TextOverflowModes.Truncate;
        text.enableAutoSizing = true;
        text.fontSizeMin = 12f;
        text.fontSizeMax = 18f;
        text.raycastTarget = false;
    }

    private static void HideCraftingSocketWarning()
    {
        if (CraftingUi.SocketWarningRect != null && !IsUnityNull(CraftingUi.SocketWarningRect))
        {
            CraftingUi.SocketWarningRect.gameObject.SetActive(false);
        }

        CraftingController.ResetSocketWarningStamp();
    }

    private static int GetSelectedCraftingQuality(InventoryGui gui)
    {
        if (IsJewelcraftingSocketTabActive(gui))
        {
            return 1;
        }

        return gui.m_selectedRecipe.ItemData == null ? 1 : gui.m_selectedRecipe.ItemData.m_quality + 1;
    }

    private static Vector2 GetCraftingGridCellPosition(RectTransform grid, int column, int row)
    {
        return grid.anchoredPosition + new Vector2(column * CraftingRecipeGridCellSpace, -row * CraftingRecipeGridCellSpace);
    }

    private static Vector2 GetCraftingGridCenteredPosition(RectTransform grid, int column, int row, Vector2 size, int columnSpan = 1)
    {
        float spanWidth = (Mathf.Max(1, columnSpan) - 1) * CraftingRecipeGridCellSpace + CraftingRecipeGridCellSize;
        Vector2 cellPosition = GetCraftingGridCellPosition(grid, column, row);
        return cellPosition + new Vector2((spanWidth - size.x) * 0.5f, -(CraftingRecipeGridCellSize - size.y) * 0.5f);
    }

    private static RectTransform? EnsureCraftingCountInput(InventoryGui gui)
    {
        if (_craftingCountInputRect != null && !IsUnityNull(_craftingCountInputRect) && _craftingCountInputRect!.parent == gui.m_crafting)
        {
            return _craftingCountInputRect;
        }

        Transform? existing = gui.m_crafting.Find(CraftingCountInputName);
        _craftingCountInputRect = existing != null ? existing.GetComponent<RectTransform>() : null;
        if (_craftingCountInputRect == null)
        {
            _craftingCountInputRect = new GameObject(CraftingCountInputName, typeof(RectTransform), typeof(Image), typeof(TMP_InputField)).GetComponent<RectTransform>();
            _craftingCountInputRect.SetParent(gui.m_crafting, false);

            Image background = _craftingCountInputRect.GetComponent<Image>();
            background.sprite = GetSolidUiSprite();
            background.color = new Color(0f, 0f, 0f, 0.46f);
            background.raycastTarget = true;

            RectTransform viewport = new GameObject("Text Area", typeof(RectTransform), typeof(RectMask2D)).GetComponent<RectTransform>();
            viewport.SetParent(_craftingCountInputRect, false);
            SetStretchRectLayout(viewport, new Vector2(22f, 3f), new Vector2(-4f, -3f));
            CraftingUi.CountInputViewport = viewport;

            RectTransform textRect = CreateTextRect("Text", viewport, out TMP_Text text);
            SetStretchRectLayout(textRect, Vector2.zero, Vector2.zero);

            text.fontSize = 22f;
            text.alignment = TextAlignmentOptions.Center;
            text.color = Color.white;
            text.textWrappingMode = TextWrappingModes.NoWrap;

            _craftingCountInput = _craftingCountInputRect.GetComponent<TMP_InputField>();
            _craftingCountInput.textViewport = viewport;
            _craftingCountInput.textComponent = text;
            _craftingCountInput.targetGraphic = background;
            _craftingCountInput.contentType = TMP_InputField.ContentType.IntegerNumber;
            _craftingCountInput.characterValidation = TMP_InputField.CharacterValidation.Integer;
            _craftingCountInput.lineType = TMP_InputField.LineType.SingleLine;
            _craftingCountInput.characterLimit = 2;
            _craftingCountInput.SetTextWithoutNotify("1");
            _craftingCountInput.onEndEdit.AddListener(value => SetCraftingCount(ParseCraftingCount(value)));
        }
        else
        {
            _craftingCountInput = _craftingCountInputRect.GetComponent<TMP_InputField>();
            CraftingUi.CountInputViewport = _craftingCountInput?.textViewport;
            if (CraftingUi.CountInputViewport == null || IsUnityNull(CraftingUi.CountInputViewport))
            {
                CraftingUi.CountInputViewport = _craftingCountInputRect.Find("Text Area") as RectTransform;
            }
        }

        EnsureCraftingCountWheelIcon(_craftingCountInputRect, locked: false);
        return _craftingCountInputRect;
    }

    private static RectTransform? EnsureCraftingUpgradeProgression(InventoryGui gui)
    {
        if (_craftingUpgradeProgressionRect != null && !IsUnityNull(_craftingUpgradeProgressionRect) && _craftingUpgradeProgressionRect!.parent == gui.m_crafting)
        {
            return _craftingUpgradeProgressionRect;
        }

        Transform? existing = gui.m_crafting.Find(CraftingUpgradeProgressionName);
        _craftingUpgradeProgressionRect = existing != null ? existing.GetComponent<RectTransform>() : null;
        if (_craftingUpgradeProgressionRect == null)
        {
            _craftingUpgradeProgressionRect = new GameObject(CraftingUpgradeProgressionName, typeof(RectTransform), typeof(Image), typeof(UITooltip)).GetComponent<RectTransform>();
            _craftingUpgradeProgressionRect.SetParent(gui.m_crafting, false);

            Image background = _craftingUpgradeProgressionRect.GetComponent<Image>();
            background.sprite = GetSolidUiSprite();
            background.color = new Color(0f, 0f, 0f, 0.42f);
            background.raycastTarget = true;

            RectTransform textRect = CreateTextRect("Text", _craftingUpgradeProgressionRect, out TMP_Text upgradeProgressionText);
            CraftingUi.UpgradeProgressionText = upgradeProgressionText;
            SetStretchRectLayout(textRect, new Vector2(4f, 3f), new Vector2(-4f, -3f));
            upgradeProgressionText.fontSize = 20f;
            upgradeProgressionText.alignment = TextAlignmentOptions.Center;
            upgradeProgressionText.color = new Color(1f, 0.84f, 0.42f, 1f);
            upgradeProgressionText.textWrappingMode = TextWrappingModes.NoWrap;
            upgradeProgressionText.overflowMode = TextOverflowModes.Overflow;
            upgradeProgressionText.raycastTarget = false;
        }
        else
        {
            CraftingUi.UpgradeProgressionText = _craftingUpgradeProgressionRect.Find("Text")?.GetComponent<TMP_Text>();
        }

        return _craftingUpgradeProgressionRect;
    }

    private static void UpdateCraftingUpgradeProgression(InventoryGui gui)
    {
        if (_craftingUpgradeProgressionRect == null)
        {
            return;
        }

        ItemData? item = gui.m_selectedRecipe.ItemData;
        Recipe? recipe = gui.m_selectedRecipe.Recipe;
        bool visible = item?.m_shared != null && recipe != null;
        _craftingUpgradeProgressionRect.gameObject.SetActive(visible);
        if (!visible)
        {
            return;
        }

        int currentQuality = Mathf.Max(1, item!.m_quality);
        int maxQuality = Mathf.Max(currentQuality, item.m_shared.m_maxQuality);
        int nextQuality = Mathf.Min(currentQuality + 1, maxQuality);
        string label = currentQuality < maxQuality ? $"{currentQuality} > {nextQuality}" : LocalizeUi("$inventoryslots_max", "Max");
        if (CraftingUi.UpgradeProgressionText != null)
        {
            CraftingTextCacheState cache = GetCraftingTextCache(_craftingUpgradeProgressionRect.gameObject);
            CraftingTextStamp stamp = new("upgrade", label, fontSizeMax: 20f);
            if (!cache.LastTextStamp.Equals(stamp))
            {
                ApplyDefaultFontAsset(CraftingUi.UpgradeProgressionText);
                CraftingUi.UpgradeProgressionText.text = label;
                CraftingUi.UpgradeProgressionText.enableAutoSizing = true;
                CraftingUi.UpgradeProgressionText.fontSizeMin = 10f;
                CraftingUi.UpgradeProgressionText.fontSizeMax = 20f;
                cache.LastTextStamp = stamp;
            }
        }

        ConfigureSimpleTooltip(_craftingUpgradeProgressionRect.gameObject, $"{GetLocalizedItemName(item)} {label}", enabled: true);
    }

    private static void EnsureCraftingCountWheelIcon(RectTransform countRect, bool locked)
    {
        RectTransform icon = EnsureHintImage(countRect, CraftingCountWheelIconName);
        icon.anchorMin = new Vector2(0f, 0.5f);
        icon.anchorMax = new Vector2(0f, 0.5f);
        icon.pivot = new Vector2(0f, 0.5f);
        icon.anchoredPosition = new Vector2(5f, 0f);
        icon.sizeDelta = new Vector2(13f, 19f);
        icon.localScale = Vector3.one;
        icon.localRotation = Quaternion.identity;
        icon.SetAsLastSibling();

        Image image = icon.GetComponent<Image>();
        image.sprite = GetMouseWheelHintSprite();
        image.type = Image.Type.Simple;
        image.preserveAspect = true;
        image.raycastTarget = false;
        image.color = locked ? new Color(0.68f, 0.88f, 1f, 0.32f) : new Color(0.68f, 0.88f, 1f, 0.82f);

        RectTransform? viewport = CraftingUi.CountInputViewport;
        if (viewport == null || IsUnityNull(viewport) || viewport.parent != countRect)
        {
            viewport = countRect.Find("Text Area") as RectTransform;
            CraftingUi.CountInputViewport = viewport;
        }

        if (viewport != null && !IsUnityNull(viewport))
        {
            SetStretchRectLayout(viewport, new Vector2(22f, 3f), new Vector2(-4f, -3f));
        }
    }

    private static void UpdateCraftingRequiredStationLevel(InventoryGui gui, Vector2 anchoredPosition, bool updateLayout)
    {
        Recipe? recipe = gui.m_selectedRecipe.Recipe;
        if (gui.m_minStationLevelIcon == null || gui.m_minStationLevelText == null)
        {
            return;
        }

        RectTransform iconRect = (RectTransform)gui.m_minStationLevelIcon.transform;
        RectTransform textRect = (RectTransform)gui.m_minStationLevelText.transform;
        SuppressCraftingRequiredStationLevelOriginalBackground(gui, iconRect, textRect);

        if (recipe == null)
        {
            gui.m_minStationLevelIcon.gameObject.SetActive(false);
            gui.m_minStationLevelText.gameObject.SetActive(false);
            HideCraftingRequiredStationLevelTooltip();
            return;
        }

        int quality = gui.m_selectedRecipe.ItemData == null ? 1 : gui.m_selectedRecipe.ItemData.m_quality + 1;
        bool allowedQuality = quality <= recipe.m_item.m_itemData.m_shared.m_maxQuality;
        CraftingStation requiredStation = recipe.GetRequiredStation(quality);
        if (requiredStation == null || !allowedQuality)
        {
            gui.m_minStationLevelIcon.gameObject.SetActive(false);
            gui.m_minStationLevelText.gameObject.SetActive(false);
            HideCraftingRequiredStationLevelTooltip();
            return;
        }

        int requiredLevel = recipe.GetRequiredStationLevel(quality);
        bool veiledMasked = IsVeiledRecipeMasked(gui.m_selectedRecipe);
        bool stationRequirementKnown = !veiledMasked || KnowsVeiledRecipeStationRequirement(recipe, quality);
        const float iconSize = 44f;
        Vector2 iconPosition = anchoredPosition + new Vector2((CraftingRecipeGridCellSize - iconSize) * 0.5f, -(CraftingRecipeGridCellSize - iconSize) * 0.5f);
        if (updateLayout)
        {
            SetCraftingTopLeftRect(gui.m_crafting, iconRect, iconPosition, new Vector2(iconSize, iconSize));
            SetCraftingTopLeftRect(gui.m_crafting, textRect, iconPosition + new Vector2(0f, -(iconSize - 20f)), new Vector2(iconSize, 20f));
        }

        if (requiredStation.m_icon != null)
        {
            gui.m_minStationLevelIcon.sprite = requiredStation.m_icon;
            gui.m_minStationLevelIcon.overrideSprite = requiredStation.m_icon;
        }

        gui.m_minStationLevelIcon.color = Color.white;
        gui.m_minStationLevelIcon.gameObject.SetActive(true);

        ApplyDefaultFontAsset(gui.m_minStationLevelText);
        gui.m_minStationLevelText.text = stationRequirementKnown ? requiredLevel.ToString() : GetVeiledRecipeUnknownRequirementText();
        gui.m_minStationLevelText.alignment = TextAlignmentOptions.BottomRight;
        gui.m_minStationLevelText.enableAutoSizing = true;
        gui.m_minStationLevelText.fontSizeMin = 12f;
        gui.m_minStationLevelText.fontSizeMax = 18f;
        CraftingStation? currentStation = Player.m_localPlayer != null ? Player.m_localPlayer.GetCurrentCraftingStation() : null;
        bool missingLevel = stationRequirementKnown && (currentStation == null || currentStation.GetLevel() < requiredLevel);
        gui.m_minStationLevelText.color = missingLevel && !ZoneSystem.instance.GetGlobalKey(GlobalKeys.NoCraftCost) && Mathf.Sin(Time.time * 10f) > 0f
            ? Color.red
            : gui.m_minStationLevelBasecolor;
        gui.m_minStationLevelText.gameObject.SetActive(true);

        RectTransform? hitbox = EnsureCraftingRequiredStationLevelHitbox(gui, iconPosition, iconSize);
        if (hitbox != null)
        {
            ConfigureSimpleTooltip(hitbox.gameObject, stationRequirementKnown ? GetCraftingStationDisplayName(requiredStation) : GetVeiledRecipeUnknownNameText(), enabled: true);
        }
    }

    private static RectTransform? EnsureCraftingRequiredStationLevelHitbox(InventoryGui gui, Vector2 iconPosition, float iconSize)
    {
        if (gui.m_crafting == null)
        {
            return null;
        }

        RectTransform? hitbox = CraftingUi.RequiredStationLevelHitbox;
        if (hitbox == null || IsUnityNull(hitbox) || hitbox.parent != gui.m_crafting)
        {
            hitbox = gui.m_crafting.Find(CraftingRequiredStationHitboxName) as RectTransform;
            if (hitbox == null || IsUnityNull(hitbox))
            {
                hitbox = new GameObject(CraftingRequiredStationHitboxName, typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
            }

            CraftingUi.RequiredStationLevelHitbox = hitbox;
        }

        SetCraftingTopLeftRect(gui.m_crafting, hitbox, iconPosition, new Vector2(iconSize, iconSize));
        hitbox.gameObject.SetActive(true);

        Image image = hitbox.GetComponent<Image>() ?? hitbox.gameObject.AddComponent<Image>();
        image.sprite = GetSolidUiSprite();
        image.color = new Color(0f, 0f, 0f, 0f);
        image.raycastTarget = true;

        if (hitbox.parent != null && hitbox.GetSiblingIndex() != hitbox.parent.childCount - 1)
        {
            hitbox.SetAsLastSibling();
        }

        return hitbox;
    }

    private static void HideCraftingRequiredStationLevelTooltip()
    {
        RectTransform? hitbox = CraftingUi.RequiredStationLevelHitbox;
        if (hitbox == null || IsUnityNull(hitbox))
        {
            CraftingUi.RequiredStationLevelHitbox = null;
            return;
        }

        ConfigureSimpleTooltip(hitbox.gameObject, "", enabled: false);
        hitbox.gameObject.SetActive(false);
    }

    private static void SuppressCraftingRequiredStationLevelOriginalBackground(InventoryGui gui)
    {
        if (gui.m_minStationLevelIcon == null || gui.m_minStationLevelText == null)
        {
            return;
        }

        SuppressCraftingRequiredStationLevelOriginalBackground(
            gui,
            (RectTransform)gui.m_minStationLevelIcon.transform,
            (RectTransform)gui.m_minStationLevelText.transform);
    }

    private static void SuppressCraftingRequiredStationLevelOriginalBackground(InventoryGui gui, RectTransform iconRect, RectTransform textRect)
    {
        HideCraftingRequiredStationLevelOriginalBackground(gui, iconRect, textRect);
    }

    private static void HideCraftingRequiredStationLevelOriginalBackground(InventoryGui gui, RectTransform iconRect, RectTransform textRect)
    {
        if (gui.m_crafting == null)
        {
            return;
        }

        if (CraftingUi.RequiredStationLevelOriginalRoot == null || IsUnityNull(CraftingUi.RequiredStationLevelOriginalRoot))
        {
            CraftingUi.RequiredStationLevelOriginalRoot = FindCraftingRequiredStationLevelOriginalRoot(gui, iconRect, textRect);
        }

        Transform? root = CraftingUi.RequiredStationLevelOriginalRoot;
        if (root == null || IsUnityNull(root) || root == gui.m_crafting || !root.IsChildOf(gui.m_crafting))
        {
            CraftingUi.RequiredStationLevelOriginalRoot = FindCraftingRequiredStationLevelOriginalRoot(gui, iconRect, textRect);
            root = CraftingUi.RequiredStationLevelOriginalRoot;
            if (root == null || IsUnityNull(root) || root == gui.m_crafting || !root.IsChildOf(gui.m_crafting))
            {
                CraftingUi.RequiredStationLevelOriginalRoot = null;
                return;
            }
        }

        foreach (Image image in root.GetComponentsInChildren<Image>(includeInactive: true))
        {
            if (image == null ||
                IsUnityNull(image) ||
                image == gui.m_minStationLevelIcon ||
                IsOwnedCraftingUiTransform(image.transform))
            {
                continue;
            }

            if (!CraftingVanillaPanelBackgroundStates.ContainsKey(image))
            {
                CraftingVanillaPanelBackgroundStates[image] = image.enabled;
            }

            image.enabled = false;
            image.raycastTarget = false;
        }
    }

    private static Transform? FindCraftingRequiredStationLevelOriginalRoot(InventoryGui gui, RectTransform iconRect, RectTransform textRect)
    {
        Transform? iconParent = iconRect.parent;
        Transform? textParent = textRect.parent;
        if (iconParent != null && iconParent != gui.m_crafting && textParent != null && (textParent == iconParent || textParent.IsChildOf(iconParent)))
        {
            return iconParent;
        }

        if (textParent != null && textParent != gui.m_crafting && iconParent != null && iconParent.IsChildOf(textParent))
        {
            return textParent;
        }

        return iconParent != null && iconParent != gui.m_crafting ? iconParent : null;
    }

    private static void UpdateCraftingCountInputState(InventoryGui gui)
    {
        if (_craftingCountInput == null || _craftingCountInputRect == null)
        {
            return;
        }

        bool locked = IsCraftingCountInputLocked(gui);
        _craftingCountInput.interactable = !locked;
        _craftingCountInput.readOnly = locked;
        if (_craftingCountInputRect.TryGetComponent(out Image image))
        {
            image.color = locked ? new Color(0f, 0f, 0f, 0.28f) : new Color(0f, 0f, 0f, 0.46f);
        }

        EnsureCraftingCountWheelIcon(_craftingCountInputRect, locked);
    }

    private static void UpdateCraftingCraftButtonLabel(InventoryGui gui)
    {
        if (gui.m_craftButton == null || gui.m_selectedRecipe.Recipe == null)
        {
            return;
        }

        string label = IsJewelcraftingSocketTabActive(gui)
            ? LocalizeUi("$jc_add_socket_button", "Socket")
            : gui.m_selectedRecipe.ItemData != null
            ? LocalizeUi("$inventory_upgradebutton", "Upgrade")
            : LocalizeUi("$inventory_craftbutton", "Craft");

        int count = GetEffectiveCraftingCount(gui);
        if (gui.m_selectedRecipe.ItemData == null && count > 1)
        {
            label = FormatCraftingCountLabel("$inventoryslots_craft_button_count_format", "{label} x{count}", label, count);
        }

        CraftingTextCacheState cache = GetCraftingTextCache(gui.m_craftButton.gameObject);
        CraftingTextStamp stamp = new("craft", label, cache.ChildSignature);
        if (cache.LastTextStamp.Equals(stamp) &&
            TextCacheMatches(cache, label))
        {
            ApplyCraftingActionButtonTextStateColor(gui.m_craftButton, gui.m_craftButton.interactable);
            return;
        }

        foreach (TMP_Text text in cache.TmpTexts)
        {
            if (text == null || IsUnityNull(text))
            {
                continue;
            }

            ApplyDefaultFontAsset(text);
            if (text.text != label)
            {
                text.text = label;
            }
        }

        foreach (Text text in cache.LegacyTexts)
        {
            if (text != null && !IsUnityNull(text) && text.text != label)
            {
                text.text = label;
            }
        }

        cache.LastTextStamp = stamp;
        SetActionButtonTextAutoSize(gui.m_craftButton);
        ApplyCraftingActionButtonTextStateColor(gui.m_craftButton, gui.m_craftButton.interactable);
    }

    private static void UpdateJewelcraftingSocketCraftButtonState(InventoryGui gui)
    {
        if (!IsJewelcraftingSocketTabActive(gui) || gui.m_craftButton == null)
        {
            return;
        }

        bool canAfford = CanAffordJewelcraftingSocketAttempt(gui.m_selectedRecipe);
        bool canAttempt = CanAttemptJewelcraftingSocket(gui.m_selectedRecipe);
        gui.m_craftButton.interactable = canAttempt;
        ApplyCraftingActionButtonTextStateColor(gui.m_craftButton, canAttempt);

        UITooltip? tooltip = gui.m_craftButton.GetComponent<UITooltip>();
        if (tooltip == null || IsUnityNull(tooltip))
        {
            return;
        }

        tooltip.m_text = canAttempt || canAfford
            ? ""
            : LocalizeUi("$msg_missingrequirement", "Missing requirement");
    }

    private static void ApplyCraftingActionButtonTextStateColor(Button button, bool interactable)
    {
        Color color = GetCraftingActionButtonTextStateColor(button, interactable);
        CraftingTextCacheState cache = GetCraftingTextCache(button.gameObject);
        CraftingTextColorStamp stamp = new(interactable, color.r, color.g, color.b, color.a, cache.ChildSignature);
        if (cache.LastColorStamp.Equals(stamp) &&
            TextColorCacheMatches(cache, color))
        {
            return;
        }

        foreach (TMP_Text text in cache.TmpTexts)
        {
            if (text == null || IsUnityNull(text))
            {
                continue;
            }

            if (!ColorsApproximatelyEqual(text.color, color))
            {
                text.color = color;
            }
        }

        foreach (Text text in cache.LegacyTexts)
        {
            if (text == null || IsUnityNull(text))
            {
                continue;
            }

            if (!ColorsApproximatelyEqual(text.color, color))
            {
                text.color = color;
            }
        }

        cache.LastColorStamp = stamp;
    }

    private static Color GetCraftingActionButtonTextStateColor(Button button, bool interactable)
    {
        ColorBlock colors = button.colors;
        Color color = interactable ? colors.normalColor : colors.disabledColor;
        if (interactable && Mathf.Max(color.r, color.g, color.b) < 0.65f)
        {
            color = Color.white;
        }

        if (color.a < 0.25f)
        {
            color.a = 1f;
        }

        return color;
    }

    private static bool TextCacheMatches(CraftingTextCacheState cache, string label)
    {
        bool foundText = false;
        foreach (TMP_Text text in cache.TmpTexts)
        {
            if (text == null || IsUnityNull(text))
            {
                continue;
            }

            foundText = true;
            if (!string.Equals(text.text, label, StringComparison.Ordinal))
            {
                return false;
            }
        }

        foreach (Text text in cache.LegacyTexts)
        {
            if (text == null || IsUnityNull(text))
            {
                continue;
            }

            foundText = true;
            if (!string.Equals(text.text, label, StringComparison.Ordinal))
            {
                return false;
            }
        }

        return foundText;
    }

    private static bool TextColorCacheMatches(CraftingTextCacheState cache, Color color)
    {
        bool foundText = false;
        foreach (TMP_Text text in cache.TmpTexts)
        {
            if (text == null || IsUnityNull(text))
            {
                continue;
            }

            foundText = true;
            if (!ColorsApproximatelyEqual(text.color, color))
            {
                return false;
            }
        }

        foreach (Text text in cache.LegacyTexts)
        {
            if (text == null || IsUnityNull(text))
            {
                continue;
            }

            foundText = true;
            if (!ColorsApproximatelyEqual(text.color, color))
            {
                return false;
            }
        }

        return foundText;
    }

    private static void UpdateCraftingProgressLabel(InventoryGui gui)
    {
        if (gui.m_craftProgressPanel is not RectTransform progressPanel || !progressPanel.gameObject.activeInHierarchy)
        {
            return;
        }

        CraftingTextCacheState cache = GetCraftingProgressTextCache(progressPanel);
        string label = GetCraftingProgressBaseLabel(cache);
        if (TryGetCraftingProgressLabelCount(gui, out int count))
        {
            label = FormatCraftingCountLabel("$inventoryslots_crafting_progress_count_format", "{label} x{count}", label, count);
        }

        CraftingTextStamp stamp = new("progress", label, cache.ChildSignature);
        if (cache.LastTextStamp.Equals(stamp) &&
            TextCacheMatches(cache, label))
        {
            return;
        }

        foreach (TMP_Text text in cache.TmpTexts)
        {
            if (text == null || IsUnityNull(text))
            {
                continue;
            }

            ApplyDefaultFontAsset(text);
            if (text.text != label)
            {
                text.text = label;
            }

            text.enableAutoSizing = true;
            text.fontSizeMin = 10f;
            text.fontSizeMax = Mathf.Min(Mathf.Max(text.fontSizeMax, 14f), 18f);
            text.alignment = TextAlignmentOptions.Center;
        }

        foreach (Text text in cache.LegacyTexts)
        {
            if (text != null && !IsUnityNull(text) && text.text != label)
            {
                text.text = label;
            }
        }

        cache.LastTextStamp = stamp;
    }

    private static bool TryGetCraftingProgressLabelCount(InventoryGui gui, out int count)
    {
        count = 0;
        if (gui.m_craftTimer < 0f || gui.m_selectedRecipe.Recipe == null || gui.m_selectedRecipe.ItemData != null)
        {
            return false;
        }

        if (CraftingQueue.QueueTotal > 1 && gui.m_selectedRecipe.Recipe == CraftingQueue.QueueRecipe && gui.m_selectedVariant == CraftingQueue.QueueVariant)
        {
            int activeCraft = gui.m_craftTimer >= 0f ? 1 : 0;
            count = Mathf.Clamp(CraftingQueue.QueueRemaining + activeCraft, 1, CraftingQueue.QueueTotal);
            return true;
        }

        if (gui.m_multiCrafting && gui.m_multiCraftAmount > 1)
        {
            count = Mathf.Clamp(gui.m_multiCraftAmount, 1, CraftingQueueMaxCount);
            return true;
        }

        if (CraftingQueue.ProgressLabelCount > 1 &&
            gui.m_selectedRecipe.Recipe == CraftingQueue.ProgressLabelRecipe &&
            gui.m_selectedVariant == CraftingQueue.ProgressLabelVariant)
        {
            count = Mathf.Clamp(CraftingQueue.ProgressLabelCount, 1, CraftingQueueMaxCount);
            return true;
        }

        int inputCount = GetCraftingCount();
        if (inputCount <= 1)
        {
            return false;
        }

        count = inputCount;
        return true;
    }

    private static CraftingTextCacheState GetCraftingProgressTextCache(RectTransform progressPanel)
    {
        CraftingTextCacheState cache = GetCraftingTextCache(progressPanel.gameObject);
        TMP_Text? directTmpText = progressPanel.Find("Text")?.GetComponent<TMP_Text>();
        Text? directLegacyText = progressPanel.Find("Text")?.GetComponent<Text>();

        bool changed = false;
        if (directTmpText != null && !IsUnityNull(directTmpText) && !cache.TmpTexts.Contains(directTmpText))
        {
            cache.TmpTexts = cache.TmpTexts.Concat(new[] { directTmpText }).ToArray();
            changed = true;
        }

        if (directLegacyText != null && !IsUnityNull(directLegacyText) && !cache.LegacyTexts.Contains(directLegacyText))
        {
            cache.LegacyTexts = cache.LegacyTexts.Concat(new[] { directLegacyText }).ToArray();
            changed = true;
        }

        if (cache.TmpTexts.Length == 0 && cache.LegacyTexts.Length == 0)
        {
            TMP_Text? created = EnsureCraftingProgressLabel(progressPanel);
            if (created != null && !IsUnityNull(created))
            {
                cache.TmpTexts = new[] { created };
                changed = true;
            }
        }

        if (changed)
        {
            cache.LastTextStamp = default;
            cache.ProgressBaseLabel = "";
        }

        return cache;
    }

    private static TMP_Text? EnsureCraftingProgressLabel(RectTransform progressPanel)
    {
        Transform? existing = progressPanel.Find("Text");
        if (existing != null)
        {
            TMP_Text? existingText = existing.GetComponent<TMP_Text>();
            if (existingText != null && !IsUnityNull(existingText))
            {
                return existingText;
            }
        }

        RectTransform rect = existing as RectTransform ?? new GameObject("Text", typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(progressPanel, false);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.localScale = Vector3.one;

        TMP_Text label = rect.GetComponent<TMP_Text>() ?? rect.gameObject.AddComponent<TextMeshProUGUI>();
        ApplyDefaultFontAsset(label);
        label.text = LocalizeUi("$inventory_crafting", "Crafting");
        label.alignment = TextAlignmentOptions.Center;
        label.enableAutoSizing = true;
        label.fontSizeMin = 10f;
        label.fontSizeMax = 18f;
        label.raycastTarget = false;
        return label;
    }

    private static string GetCraftingProgressBaseLabel(CraftingTextCacheState cache)
    {
        if (!string.IsNullOrWhiteSpace(cache.ProgressBaseLabel))
        {
            return cache.ProgressBaseLabel;
        }

        foreach (TMP_Text text in cache.TmpTexts)
        {
            if (text == null || IsUnityNull(text))
            {
                continue;
            }

            string label = StripCraftingProgressCounter(text.text);
            if (!string.IsNullOrWhiteSpace(label))
            {
                cache.ProgressBaseLabel = label;
                return cache.ProgressBaseLabel;
            }
        }

        foreach (Text text in cache.LegacyTexts)
        {
            if (text == null || IsUnityNull(text))
            {
                continue;
            }

            string label = StripCraftingProgressCounter(text.text);
            if (!string.IsNullOrWhiteSpace(label))
            {
                cache.ProgressBaseLabel = label;
                return cache.ProgressBaseLabel;
            }
        }

        cache.ProgressBaseLabel = LocalizeUi("$inventory_crafting", "Crafting");
        return cache.ProgressBaseLabel;
    }

    private static string FormatCraftingCountLabel(string token, string fallback, string label, int count)
    {
        return LocalizeUi(token, fallback)
            .Replace("{label}", label)
            .Replace("{count}", count.ToString());
    }

    private static string StripCraftingProgressCounter(string value)
    {
        value = value.Trim();
        int lastSpace = value.LastIndexOf(' ');
        if (lastSpace <= 0)
        {
            return value;
        }

        string suffix = value.Substring(lastSpace + 1);
        if (IsCraftingProgressCounterSuffix(suffix))
        {
            return value.Substring(0, lastSpace);
        }

        return value;
    }

    private static bool IsCraftingProgressCounterSuffix(string suffix)
    {
        int slashIndex = suffix.IndexOf('/');
        if (slashIndex > 0 && slashIndex < suffix.Length - 1)
        {
            return int.TryParse(suffix.Substring(0, slashIndex), out _) && int.TryParse(suffix.Substring(slashIndex + 1), out _);
        }

        return suffix.Length > 1 && (suffix[0] == 'x' || suffix[0] == 'X') && int.TryParse(suffix.Substring(1), out _);
    }

    private static string GetRequirementDisplayName(Requirement requirement)
    {
        if (requirement.m_resItem == null)
        {
            return "";
        }

        ItemData item = requirement.m_resItem.m_itemData;
        return LocalizeUi(item.m_shared.m_name, item.m_shared.m_name);
    }

    private static string GetCraftingStationDisplayName(CraftingStation station) =>
        station == null ? "" : LocalizeUi(station.m_name, station.m_name);

    private static void ConfigureSimpleTooltip(GameObject target, string topic, bool enabled)
    {
        ConfigureSimpleTooltip(target, topic, "", enabled);
    }

    private static void ConfigureSimpleTooltip(GameObject target, string topic, string text, bool enabled)
    {
        if (target == null || IsUnityNull(target))
        {
            return;
        }

        UITooltip? tooltip = target.GetComponent<UITooltip>();
        CraftingTooltipState state = target.GetComponent<CraftingTooltipState>() ?? target.AddComponent<CraftingTooltipState>();
        if (!enabled || string.IsNullOrWhiteSpace(topic))
        {
            if (!state.Stamp.IsValid && tooltip == null)
            {
                return;
            }

            if (tooltip != null)
            {
                tooltip.m_topic = "";
                tooltip.m_text = "";
                tooltip.enabled = false;
            }

            InventorySlotsSimpleTooltipHover? hover = target.GetComponent<InventorySlotsSimpleTooltipHover>();
            if (hover != null && !IsUnityNull(hover))
            {
                hover.Configure("", "");
            }

            state.Stamp = default;
            return;
        }

        if (tooltip != null)
        {
            tooltip.m_topic = "";
            tooltip.m_text = "";
            tooltip.enabled = false;
        }

        CraftingSimpleTooltipStamp stamp = new(topic, text);
        if (state.Stamp.Equals(stamp))
        {
            return;
        }

        InventorySlotsSimpleTooltipHover simpleTooltip = target.GetComponent<InventorySlotsSimpleTooltipHover>() ?? target.AddComponent<InventorySlotsSimpleTooltipHover>();
        simpleTooltip.Configure(topic, text);
        state.Stamp = stamp;
    }

    private static CraftingTextCacheState GetCraftingTextCache(GameObject root)
    {
        CraftingTextCacheState cache = root.GetComponent<CraftingTextCacheState>() ?? root.AddComponent<CraftingTextCacheState>();
        string childSignature = root.transform.childCount.ToString();
        if (string.Equals(cache.ChildSignature, childSignature, StringComparison.Ordinal) &&
            !HasInvalidTextCache(cache))
        {
            return cache;
        }

        cache.ChildSignature = childSignature;
        cache.LastTextStamp = default;
        cache.LastColorStamp = default;
        cache.ProgressBaseLabel = "";
        cache.TmpTexts = root.GetComponentsInChildren<TMP_Text>(true);
        cache.LegacyTexts = root.GetComponentsInChildren<Text>(true);
        return cache;
    }

    private static bool HasInvalidTextCache(CraftingTextCacheState cache)
    {
        return cache.TmpTexts.Any(text => text == null || IsUnityNull(text)) ||
               cache.LegacyTexts.Any(text => text == null || IsUnityNull(text));
    }

    private static bool IsCraftingCountInputLocked(InventoryGui? gui)
    {
        return CraftingQueue.QueueRemaining > 0 || (gui != null && gui.m_craftTimer >= 0f);
    }

    private static void SetCraftingTopLeftRect(RectTransform parent, RectTransform rect, Vector2 anchoredPosition, Vector2 size)
    {
        SetTopLeftRectLayout(parent, rect, anchoredPosition, size);
    }

    private static void ConfigureCompactCraftingRequirement(InventoryGui gui, RectTransform rect, Requirement requirement, int quality, int craftMultiplier)
    {
        CraftingRequirementUiMarker marker = GetCraftingRequirementUiMarker(rect);
        HideCraftingRequirementSlotBackground(marker);
        string layoutSignature = $"{CraftingRecipeGridCellSize:0.###}";
        bool updateLayout = !string.Equals(marker.LayoutSignature, layoutSignature, StringComparison.Ordinal);
        bool veiledMasked = IsVeiledRecipeMasked(gui.m_selectedRecipe);
        bool requirementKnown = !veiledMasked || IsVeiledRecipeRequirementKnown(requirement);

        if (marker.Name != null && !IsUnityNull(marker.Name))
        {
            marker.Name.gameObject.SetActive(false);
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
                const float iconSize = 44f;
                icon.anchoredPosition = new Vector2((CraftingRecipeGridCellSize - iconSize) * 0.5f, -(CraftingRecipeGridCellSize - iconSize) * 0.5f);
                icon.sizeDelta = new Vector2(iconSize, iconSize);
                icon.localScale = Vector3.one;
            }

            if (marker.IconImage != null && !IsUnityNull(marker.IconImage))
            {
                marker.IconImage.sprite = requirement.m_resItem.m_itemData.GetIcon();
                marker.IconImage.color = requirementKnown ? Color.white : Color.black;
                marker.IconImage.raycastTarget = false;
            }

            ConfigureSimpleTooltip(icon.gameObject, "", enabled: false);
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
                TMP_Text text = marker.AmountText;
                int required = Mathf.Max(0, requirement.GetAmount(quality) * Mathf.Max(1, craftMultiplier));
                int available = requirementKnown ? GetAvailableCraftingRequirementAmount(requirement) : 0;
                bool noCost = HasNoCraftCost();
                bool availableEnough = requirementKnown && (noCost || available >= required);
                string label = requirementKnown ? FormatCompactRequirementAmount(available, required) : GetVeiledRecipeUnknownRequirementText();
                Color color = requirementKnown ? GetCompactRequirementAmountColor(availableEnough) : Color.white;
                string amountSignature = $"{label}|{color}";
                if (!string.Equals(marker.AmountSignature, amountSignature, StringComparison.Ordinal) ||
                    !RequirementAmountTextMatches(text, label, color))
                {
                    ApplyDefaultFontAsset(text);
                    text.text = label;
                    text.color = color;
                    marker.AmountSignature = amountSignature;
                }

                if (updateLayout)
                {
                    text.alignment = TextAlignmentOptions.Bottom;
                    text.enableAutoSizing = true;
                    text.fontSizeMin = 10f;
                    text.fontSizeMax = 16f;
                    text.textWrappingMode = TextWrappingModes.NoWrap;
                    text.overflowMode = TextOverflowModes.Overflow;
                }
            }
        }

        RectTransform hitbox = EnsureCraftingRequirementHitbox(rect, marker);
        DisableCompetingCraftingRequirementTooltips(rect, hitbox, marker);
        ConfigureCraftingRequirementTooltip(gui, hitbox.gameObject, requirement, quality, craftMultiplier);
        marker.LayoutSignature = layoutSignature;
    }

}
