using System;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Requirement = Piece.Requirement;

namespace InventorySlots;

public sealed partial class InventorySlotsPlugin
{
    private static RectTransform? EnsureOwnedCraftingRequirementSlot(InventoryGui gui, int index)
    {
        if (gui.m_crafting == null || IsUnityNull(gui.m_crafting))
        {
            return null;
        }

        while (CraftingRequirements.OwnedSlots.Count <= index)
        {
            CraftingRequirements.OwnedSlots.Add(null);
        }

        RectTransform? cached = CraftingRequirements.OwnedSlots[index];
        if (cached != null && !IsUnityNull(cached) && cached.parent == gui.m_crafting)
        {
            return cached;
        }

        string name = $"{CraftingRequirementSlotNamePrefix}{index}";
        RectTransform? existing = gui.m_crafting.Find(name) as RectTransform;
        RectTransform rect = existing != null && !IsUnityNull(existing)
            ? existing
            : CreateOwnedCraftingRequirementSlot(gui, name);
        rect.name = name;
        rect.SetParent(gui.m_crafting, false);
        CraftingRequirements.OwnedSlots[index] = rect;
        return rect;
    }

    private static RectTransform CreateOwnedCraftingRequirementSlot(InventoryGui gui, string name)
    {
        GameObject? template = GetCraftingRequirementTemplate(gui);
        GameObject slot = template != null && !IsUnityNull(template)
            ? UnityEngine.Object.Instantiate(template, gui.m_crafting, false)
            : CreateFallbackCraftingRequirementSlot(name, gui.m_crafting);
        slot.name = name;
        slot.SetActive(false);
        return slot.GetComponent<RectTransform>() ?? slot.AddComponent<RectTransform>();
    }

    private static GameObject? GetCraftingRequirementTemplate(InventoryGui gui)
    {
        if (gui.m_recipeRequirementList == null)
        {
            return null;
        }

        foreach (GameObject requirement in gui.m_recipeRequirementList)
        {
            if (requirement != null && !IsUnityNull(requirement))
            {
                return requirement;
            }
        }

        return null;
    }

    private static GameObject CreateFallbackCraftingRequirementSlot(string name, Transform parent)
    {
        GameObject slot = new(name, typeof(RectTransform), typeof(Image));
        slot.transform.SetParent(parent, false);
        Image background = slot.GetComponent<Image>();
        background.enabled = false;
        background.raycastTarget = false;

        RectTransform icon = new GameObject("res_icon", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
        icon.SetParent(slot.transform, false);
        Image iconImage = icon.GetComponent<Image>();

        RectTransform nameRect = CreateTextRect("res_name", slot.transform, active: false);
        RectTransform amountRect = CreateTextRect("res_amount", slot.transform, out TMP_Text amountText);
        CraftingRequirementUiMarker marker = slot.AddComponent<CraftingRequirementUiMarker>();
        marker.ChildSignature = slot.transform.childCount.ToString();
        marker.Name = nameRect;
        marker.Icon = icon;
        marker.IconImage = iconImage;
        marker.Amount = amountRect;
        marker.AmountText = amountText;
        marker.BackgroundImages = new[] { background };
        return slot;
    }

    private static void HideOwnedCraftingRequirementSlots()
    {
        foreach (RectTransform? slot in CraftingRequirements.OwnedSlots)
        {
            if (slot != null && !IsUnityNull(slot))
            {
                slot.gameObject.SetActive(false);
            }
        }
    }

    private static void HideCraftingVanillaRequirementSlots(InventoryGui gui)
    {
        if (gui.m_recipeRequirementList == null)
        {
            return;
        }

        foreach (GameObject requirement in gui.m_recipeRequirementList)
        {
            if (requirement != null && !IsUnityNull(requirement))
            {
                requirement.SetActive(false);
            }
        }
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

    private static string FormatCompactRequirementAmount(int available, int required)
    {
        return $"{available}/{required}";
    }

    private static Color GetCompactRequirementAmountColor(bool availableEnough)
    {
        if (availableEnough)
        {
            return Color.white;
        }

        if (TryGetAzuCraftyBoxesRequirementFlashColor(out Color azuColor))
        {
            return azuColor;
        }

        return Mathf.Sin(Time.time * 10f) > 0f ? Color.red : Color.white;
    }

    private static bool RequirementAmountTextMatches(TMP_Text text, string label, Color color)
    {
        return string.Equals(text.text, label, StringComparison.Ordinal) &&
               ColorsApproximatelyEqual(text.color, color);
    }

    private static bool ColorsApproximatelyEqual(Color left, Color right)
    {
        const float epsilon = 0.001f;
        return Mathf.Abs(left.r - right.r) <= epsilon &&
               Mathf.Abs(left.g - right.g) <= epsilon &&
               Mathf.Abs(left.b - right.b) <= epsilon &&
               Mathf.Abs(left.a - right.a) <= epsilon;
    }

    private static CraftingRequirementUiMarker GetCraftingRequirementUiMarker(RectTransform rect)
    {
        CraftingRequirementUiMarker marker = rect.GetComponent<CraftingRequirementUiMarker>() ?? rect.gameObject.AddComponent<CraftingRequirementUiMarker>();
        string childSignature = rect.childCount.ToString();
        if (string.Equals(marker.ChildSignature, childSignature, StringComparison.Ordinal) &&
            marker.Name != null &&
            marker.Icon != null &&
            marker.Amount != null &&
            !IsUnityNull(marker.Name) &&
            !IsUnityNull(marker.Icon) &&
            !IsUnityNull(marker.Amount))
        {
            return marker;
        }

        marker.Name = rect.Find("res_name");
        marker.Icon = rect.Find("res_icon") as RectTransform;
        marker.IconImage = marker.Icon != null && !IsUnityNull(marker.Icon) ? marker.Icon.GetComponent<Image>() : null;
        marker.Amount = rect.Find("res_amount") as RectTransform;
        marker.AmountText = marker.Amount != null && !IsUnityNull(marker.Amount) ? marker.Amount.GetComponent<TMP_Text>() : null;
        marker.Hitbox = rect.Find(CraftingRequirementHitboxName) as RectTransform;
        marker.BackgroundImages = FindCraftingRequirementBackgroundImages(rect);
        marker.CompetingTooltipSignature = "";
        marker.CompetingTooltips = Array.Empty<UITooltip>();
        marker.ChildSignature = childSignature;
        marker.LayoutSignature = "";
        marker.AmountSignature = "";
        return marker;
    }

    private static Image[] FindCraftingRequirementBackgroundImages(RectTransform rect)
    {
        return rect.GetComponentsInChildren<Image>(includeInactive: true)
            .Where(image => IsCraftingRequirementBackgroundImage(rect, image))
            .ToArray();
    }

    private static bool IsCraftingRequirementBackgroundImage(RectTransform root, Image image)
    {
        if (image == null || IsUnityNull(image))
        {
            return false;
        }

        Transform transform = image.transform;
        string name = transform.name.ToLowerInvariant();
        if (name == "res_icon" ||
            name == "res_amount" ||
            string.Equals(transform.name, CraftingRequirementHitboxName, StringComparison.Ordinal))
        {
            return false;
        }

        return transform == root ||
               name.Contains("bkg") ||
               name.Contains("background") ||
               name.Contains("border") ||
               name.Contains("frame");
    }

    private static void HideCraftingRequirementSlotBackground(CraftingRequirementUiMarker marker)
    {
        foreach (Image image in marker.BackgroundImages)
        {
            if (image == null || IsUnityNull(image))
            {
                continue;
            }

            image.enabled = false;
            image.raycastTarget = false;
        }
    }

    private static RectTransform EnsureCraftingRequirementHitbox(RectTransform rect, CraftingRequirementUiMarker marker)
    {
        RectTransform? cached = marker.Hitbox;
        if (cached == null || IsUnityNull(cached))
        {
            cached = rect.Find(CraftingRequirementHitboxName) as RectTransform;
        }

        RectTransform hitbox = cached != null && !IsUnityNull(cached)
            ? cached
            : new GameObject(CraftingRequirementHitboxName, typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
        if (hitbox.parent != rect)
        {
            hitbox.SetParent(rect, false);
        }

        marker.Hitbox = hitbox;

        SetStretchRectLayout(hitbox, Vector2.zero, Vector2.zero);
        hitbox.SetAsLastSibling();

        Image image = hitbox.GetComponent<Image>();
        image.sprite = GetSolidUiSprite();
        image.color = new Color(0f, 0f, 0f, 0f);
        image.raycastTarget = true;
        return hitbox;
    }

    private static void ConfigureCraftingRequirementTooltip(InventoryGui gui, GameObject target, Requirement requirement, int quality, int craftMultiplier)
    {
        if (target == null || IsUnityNull(target) || requirement.m_resItem == null)
        {
            return;
        }

        bool veiledMasked = gui != null && IsVeiledRecipeMasked(gui.m_selectedRecipe);
        ConfigureSimpleTooltip(
            target,
            veiledMasked && !IsVeiledRecipeRequirementKnown(requirement) ? GetVeiledRecipeUnknownNameText() : GetRequirementDisplayName(requirement),
            enabled: true);
    }

    private static void DisableCompetingCraftingRequirementTooltips(RectTransform root, RectTransform allowed, CraftingRequirementUiMarker marker)
    {
        string signature = $"{root.childCount}|{allowed.GetInstanceID()}";
        if (!string.Equals(marker.CompetingTooltipSignature, signature, StringComparison.Ordinal))
        {
            marker.CompetingTooltips = root
                .GetComponentsInChildren<UITooltip>(includeInactive: true)
                .Where(tooltip => tooltip != null && !IsUnityNull(tooltip) && tooltip.transform != allowed)
                .ToArray();
            marker.CompetingTooltipSignature = signature;
        }

        foreach (UITooltip tooltip in marker.CompetingTooltips)
        {
            if (tooltip == null || IsUnityNull(tooltip))
            {
                continue;
            }

            tooltip.m_topic = "";
            tooltip.m_text = "";
            tooltip.enabled = false;
            if (tooltip.TryGetComponent(out CraftingTooltipState state))
            {
                state.Stamp = default;
            }
        }
    }

    private static int GetAvailableCraftingRequirementAmount(Requirement requirement)
    {
        Player? player = Player.m_localPlayer;
        if (player == null || requirement.m_resItem == null || requirement.m_resItem.m_itemData?.m_shared == null)
        {
            return 0;
        }

        string sharedName = requirement.m_resItem.m_itemData.m_shared.m_name;
        if (string.IsNullOrWhiteSpace(sharedName))
        {
            return 0;
        }

        string cacheKey = GetCraftingRequirementAvailabilityCacheKey(player, requirement, sharedName);
        float now = Time.unscaledTime;
        if (CraftingRequirements.AvailabilityCache.TryGetValue(cacheKey, out CraftingRequirementAvailabilityCacheEntry cached) &&
            cached.ExpiresAt >= now)
        {
            return cached.Amount;
        }

        Inventory inventory = player.GetInventory();
        int amount = inventory?.CountItems(sharedName, -1, true) ?? 0;
        amount += GetAzuCraftyBoxesAvailableCraftingRequirementAmount(requirement, sharedName);
        CraftingRequirements.AvailabilityCache[cacheKey] = new CraftingRequirementAvailabilityCacheEntry(
            amount,
            now + CraftingRequirementAvailabilityCacheSeconds);
        return amount;
    }

    private static string GetCraftingRequirementAvailabilityCacheKey(Player player, Requirement requirement, string sharedName)
    {
        string prefabName = requirement.m_resItem != null ? GetPrefabNameForAzuCraftyBoxes(requirement.m_resItem.name) : "";
        long playerId = player != null ? player.GetPlayerID() : 0L;
        return $"{playerId}|{prefabName}|{sharedName}";
    }

    private static void ClearCraftingRequirementAvailabilityCache()
    {
        CraftingRequirements.AvailabilityCache.Clear();
        unchecked
        {
            CraftingRequirements.AvailabilityVersion++;
        }
    }

    private static bool HasNoCraftCost()
    {
        return Player.m_localPlayer != null && Player.m_localPlayer.NoCostCheat() ||
               ZoneSystem.instance != null && ZoneSystem.instance.GetGlobalKey(GlobalKeys.NoCraftCost);
    }
}
