using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using ItemData = ItemDrop.ItemData;

namespace InventorySlots;

public sealed partial class InventorySlotsPlugin
{
    private static bool HandleCraftingCountWheel()
    {
        if (_craftingCountInputRect == null || !_craftingCountInputRect.gameObject.activeInHierarchy || !RectContainsScreenPoint(_craftingCountInputRect, GetUiMousePosition()))
        {
            return false;
        }

        if (IsCraftingCountInputLocked(InventoryGui.instance))
        {
            return false;
        }

        float wheel = GetUiScrollDelta(UiScrollInputMode.Discrete, allowGamepad: false);
        if (Mathf.Abs(wheel) < 0.01f)
        {
            return false;
        }

        SetCraftingCount(GetCraftingCount() + (wheel > 0f ? 1 : -1));
        return true;
    }

    private static int GetCraftingCount()
    {
        return ParseCraftingCount(_craftingCountInput != null ? _craftingCountInput.text : "1");
    }

    private static int GetEffectiveCraftingCount(InventoryGui? gui)
    {
        if (gui == null || gui.m_selectedRecipe.ItemData != null)
        {
            return 1;
        }

        if (IsVanillaMultiCraftModifierHeld())
        {
            return Mathf.Max(1, gui.m_multiCraftAmount);
        }

        return GetCraftingCount();
    }

    private static bool IsVanillaMultiCraftModifierHeld()
    {
        try
        {
            return ZInput.GetButton("AltPlace") || ZInput.GetButton("JoyLStick");
        }
        catch
        {
            return Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
        }
    }

    private static int ParseCraftingCount(string value)
    {
        return int.TryParse(value, out int count) ? Mathf.Clamp(count, 1, CraftingQueueMaxCount) : 1;
    }

    private static void SetCraftingCount(int count)
    {
        if (_craftingCountInput == null)
        {
            return;
        }

        _craftingCountInput.SetTextWithoutNotify(Mathf.Clamp(count, 1, CraftingQueueMaxCount).ToString());
    }

    private static bool CanContinueCraftingQueue(InventoryGui gui)
    {
        Player? player = Player.m_localPlayer;
        Recipe? recipe = gui.m_selectedRecipe.Recipe;
        if (player == null || recipe == null || _craftingQueueRecipe == null || recipe != _craftingQueueRecipe || gui.m_selectedVariant != _craftingQueueVariant || gui.m_selectedRecipe.ItemData != null)
        {
            return false;
        }

        int quality = 1;
        int needsOnlyOneIngredient;
        ItemData singleReqItem;
        int amount = recipe.GetAmount(quality, out needsOnlyOneIngredient, out singleReqItem, 1);
        bool noCost = player.NoCostCheat() || ZoneSystem.instance.GetGlobalKey(GlobalKeys.NoCraftCost);
        if (!noCost && !player.HaveRequirements(recipe, discover: false, quality, 1))
        {
            return false;
        }

        if (!player.GetInventory().CanAddItem(((Component)recipe.m_item).gameObject, amount))
        {
            return false;
        }

        CraftingStation requiredStation = recipe.GetRequiredStation(quality);
        int requiredStationLevel = recipe.GetRequiredStationLevel(quality);
        CraftingStation currentStation = player.GetCurrentCraftingStation();
        if (requiredStation != null)
        {
            if (currentStation == null || currentStation.m_name != requiredStation.m_name || currentStation.GetLevel() < requiredStationLevel)
            {
                return false;
            }

            if (!currentStation.CheckUsable(player, showMessage: false))
            {
                return false;
            }
        }

        return true;
    }

    private static bool CanAttemptJewelcraftingSocket(InventoryGui.RecipeDataPair pair)
    {
        return pair.Recipe != null &&
               pair.ItemData != null &&
               pair.Recipe.m_enabled &&
               pair.CanCraft &&
               CanAffordJewelcraftingSocketAttempt(pair);
    }

    private static bool CanAffordJewelcraftingSocketAttempt(InventoryGui.RecipeDataPair pair)
    {
        Player? player = Player.m_localPlayer;
        if (player == null || pair.Recipe == null)
        {
            return false;
        }

        if (ShouldHideJewelcraftingSocketRequirements(pair))
        {
            return true;
        }

        if (player.NoCostCheat() || ZoneSystem.instance.GetGlobalKey(GlobalKeys.NoCraftCost))
        {
            return true;
        }

        return player.HaveRequirementItems(pair.Recipe, false, 1, 1);
    }

    private static string GetSocketRecipeItemTooltip(InventoryGui.RecipeDataPair pair)
    {
        ItemData? item = pair.ItemData;
        if (item == null)
        {
            return "";
        }

        int quality = Mathf.Max(1, item.m_quality);
        int amount = pair.Recipe?.m_amount ?? 1;
        string tooltip = GetLocalizedStaticItemTooltip(item, quality, crafting: false, amount);
        return AppendItemRequiresSkillLevelCraftingTooltip(pair, tooltip);
    }

    private static string GetUpgradeRecipeComparisonTooltip(InventoryGui.RecipeDataPair pair)
    {
        ItemData? item = pair.ItemData;
        if (item == null)
        {
            return "";
        }

        int currentQuality = Mathf.Max(1, item.m_quality);
        int nextQuality = Mathf.Clamp(currentQuality + 1, 1, Mathf.Max(1, item.m_shared?.m_maxQuality ?? currentQuality + 1));
        int amount = pair.Recipe?.m_amount ?? 1;
        ItemData currentPreview = CreateUpgradeTooltipPreviewItem(item, currentQuality);
        ItemData upgradedPreview = CreateUpgradeTooltipPreviewItem(item, nextQuality);
        string currentTooltip = GetLocalizedStaticItemTooltip(currentPreview, currentQuality, crafting: false, amount);
        string upgradedTooltip = GetLocalizedStaticItemTooltip(upgradedPreview, nextQuality, crafting: false, amount);
        string tooltip = BuildUpgradeComparisonTooltip(currentTooltip, upgradedTooltip);
        return AppendItemRequiresSkillLevelCraftingTooltip(pair, tooltip);
    }

    private static ItemData CreateUpgradeTooltipPreviewItem(ItemData item, int quality)
    {
        ItemData preview = item.Clone();
        preview.m_quality = quality;
        return preview;
    }

    private static string GetLocalizedStaticItemTooltip(ItemData item, int quality, bool crafting, int amount)
    {
        string tooltip = ItemData.GetTooltip(item, quality, crafting, Game.m_worldLevel, amount);
        return Localization.instance != null ? Localization.instance.Localize(tooltip) : tooltip;
    }

    private static string BuildUpgradeComparisonTooltip(string currentTooltip, string upgradedTooltip)
    {
        if (string.IsNullOrWhiteSpace(currentTooltip) || string.IsNullOrWhiteSpace(upgradedTooltip))
        {
            return upgradedTooltip;
        }

        string[] currentLines = SplitTooltipLines(currentTooltip);
        string[] upgradedLines = SplitTooltipLines(upgradedTooltip);
        Dictionary<string, string> currentByLabel = new(StringComparer.OrdinalIgnoreCase);
        foreach (string line in currentLines)
        {
            if (TrySplitTooltipStatLine(line, out string label, out _))
            {
                string normalizedLabel = NormalizeTooltipStatLabel(label);
                if (!currentByLabel.ContainsKey(normalizedLabel))
                {
                    currentByLabel.Add(normalizedLabel, line);
                }
            }
        }

        for (int i = 0; i < upgradedLines.Length; i++)
        {
            if (!TrySplitTooltipStatLine(upgradedLines[i], out string upgradedLabel, out _) ||
                !currentByLabel.TryGetValue(NormalizeTooltipStatLabel(upgradedLabel), out string currentLine))
            {
                continue;
            }

            upgradedLines[i] = BuildUpgradeComparisonLine(currentLine, upgradedLines[i]);
        }

        return string.Join("\n", upgradedLines);
    }

    private static string[] SplitTooltipLines(string tooltip) =>
        tooltip.Replace("\r\n", "\n").Split('\n');

    private static string BuildUpgradeComparisonLine(string currentLine, string upgradedLine)
    {
        string currentPlain = StripRichText(currentLine).Trim();
        string upgradedPlain = StripRichText(upgradedLine).Trim();
        if (!TrySplitTooltipStatLine(currentPlain, out string currentLabel, out string currentValue) ||
            !TrySplitTooltipStatLine(upgradedPlain, out string upgradedLabel, out string upgradedValue) ||
            !string.Equals(NormalizeTooltipStatLabel(currentLabel), NormalizeTooltipStatLabel(upgradedLabel), StringComparison.OrdinalIgnoreCase) ||
            string.Equals(NormalizeTooltipStatValue(currentValue), NormalizeTooltipStatValue(upgradedValue), StringComparison.OrdinalIgnoreCase) ||
            !ContainsDigit(currentValue) ||
            !ContainsDigit(upgradedValue))
        {
            return upgradedLine;
        }

        if (IsDurabilityTooltipLabel(upgradedLabel) &&
            TryExtractDurabilityMaxValue(currentValue, out string currentDurabilityMax) &&
            TryExtractDurabilityMaxValue(upgradedValue, out string upgradedDurabilityMax))
        {
            return $"{upgradedLabel}: <color=orange>{currentDurabilityMax} > {upgradedDurabilityMax}</color>";
        }

        return $"{upgradedLabel}: <color=orange>{currentValue.Trim()} > {upgradedValue.Trim()}</color>";
    }

    private static bool IsDurabilityTooltipLabel(string label)
    {
        string normalized = NormalizeTooltipStatLabel(label);
        if (normalized.IndexOf("durability", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return true;
        }

        string localized = Localization.instance != null ? Localization.instance.Localize("$item_durability") : "";
        return !string.IsNullOrWhiteSpace(localized) &&
               string.Equals(normalized, NormalizeTooltipStatLabel(localized), StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryExtractDurabilityMaxValue(string value, out string maxValue)
    {
        maxValue = "";
        string plain = StripRichText(value).Trim();
        int slash = plain.LastIndexOf('/');
        if (slash >= 0 && TryReadNumberToken(plain, slash + 1, out maxValue))
        {
            return true;
        }

        return TryReadLastNumberToken(plain, out maxValue);
    }

    private static bool TryReadLastNumberToken(string text, out string number)
    {
        number = "";
        for (int i = text.Length - 1; i >= 0; i--)
        {
            if (!char.IsDigit(text[i]))
            {
                continue;
            }

            int start = i;
            while (start > 0 && IsNumberTokenChar(text[start - 1]))
            {
                start--;
            }

            number = text.Substring(start, i - start + 1);
            return true;
        }

        return false;
    }

    private static bool TryReadNumberToken(string text, int startIndex, out string number)
    {
        number = "";
        int start = startIndex;
        while (start < text.Length && char.IsWhiteSpace(text[start]))
        {
            start++;
        }

        int end = start;
        while (end < text.Length && IsNumberTokenChar(text[end]))
        {
            end++;
        }

        number = text.Substring(start, end - start).Trim();
        return number.Any(char.IsDigit);
    }

    private static bool IsNumberTokenChar(char c) =>
        char.IsDigit(c) || c == '.' || c == ',';

    private static bool TrySplitTooltipStatLine(string line, out string label, out string value)
    {
        label = "";
        value = "";
        string plain = StripRichText(line).Trim();
        int colon = plain.IndexOf(':');
        if (colon <= 0 || colon >= plain.Length - 1)
        {
            return false;
        }

        label = plain.Substring(0, colon).Trim();
        value = plain.Substring(colon + 1).Trim();
        return !string.IsNullOrWhiteSpace(label) && !string.IsNullOrWhiteSpace(value);
    }

    private static string NormalizeTooltipStatLabel(string label) =>
        new(label.Where(c => !char.IsWhiteSpace(c)).ToArray());

    private static string NormalizeTooltipStatValue(string value) =>
        new(value.Where(c => !char.IsWhiteSpace(c)).ToArray());

    private static bool ContainsDigit(string value)
    {
        foreach (char c in value)
        {
            if (char.IsDigit(c))
            {
                return true;
            }
        }

        return false;
    }
}
