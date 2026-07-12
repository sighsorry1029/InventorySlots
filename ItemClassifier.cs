using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;
using ItemData = ItemDrop.ItemData;
using AnimationState = ItemDrop.ItemData.AnimationState;
using ItemType = ItemDrop.ItemData.ItemType;

namespace InventorySlots;

public sealed partial class InventorySlotsPlugin
{
    private const int MaxItemClassifierCacheItems = 2048;
    private sealed class ItemClassification
    {
        public readonly Dictionary<string, bool> BuiltInGroupMatches = new(StringComparer.OrdinalIgnoreCase);
        public bool BigGroupResolved;
        public string BigGroupId = "";
    }

    private sealed class ItemClassifierRuntimeState
    {
        public readonly Dictionary<string, ItemClassification> Cache = new(StringComparer.OrdinalIgnoreCase);
        public readonly HashSet<string> FeastMaterialTokens = new(StringComparer.OrdinalIgnoreCase);
        public string FeastMaterialSignature = "";
        public int Version;
        public int AppliedVersion = -1;
    }

    private static readonly ItemClassifierRuntimeState ItemClassifierRuntime = new();
    private static readonly Dictionary<string, Func<ItemData, bool>> BuiltInGroupMatchers = new(StringComparer.OrdinalIgnoreCase)
    {
        ["all"] = _ => true,
        ["healthfood"] = MatchHealthFoodCategory,
        ["staminafood"] = MatchStaminaFoodCategory,
        ["eitrfood"] = MatchEitrFoodCategory,
        ["consumable"] = MatchConsumableCategory,
        ["mead"] = MatchMeadCategory,
        ["potion"] = MatchPotionCategory,
        ["meadbase"] = MatchMeadBaseCategory,
        ["feast"] = MatchFeastCategory,
        ["trophy"] = item => item.m_shared.m_itemType == ItemType.Trophy,
        ["valuable"] = item => item.m_shared.m_value > 0,
        ["melee"] = MatchMeleeCategory,
        ["ranged"] = MatchRangedCategory,
        ["magic"] = MatchMagicCategory,
        ["armor"] = MatchArmorCategory,
        ["equipment"] = MatchArmorCategory,
        ["food"] = MatchFoodCategory,
        ["misc"] = MatchMiscCategory,
        ["legs"] = MatchLegsCategory,
        ["chest"] = MatchChestCategory,
        ["helmet"] = MatchHelmetCategory,
        ["cape"] = MatchCapeCategory,
        ["utility"] = MatchUtilityCategory,
        ["trinket"] = MatchTrinketCategory,
        ["shield"] = MatchShieldCategory,
        ["axe"] = MatchAxeCategory,
        ["club"] = MatchClubCategory,
        ["knife"] = MatchKnifeCategory,
        ["pickaxe"] = MatchPickaxeCategory,
        ["polearm"] = MatchPolearmCategory,
        ["spear"] = MatchSpearCategory,
        ["sword"] = MatchSwordCategory,
        ["fists"] = MatchFistsCategory,
        ["bow"] = MatchBowWeaponCategory,
        ["crossbow"] = MatchCrossbowWeaponCategory,
        ["elementalmagic"] = MatchElementalMagicCategory,
        ["bloodmagic"] = MatchBloodMagicCategory,
        ["tool"] = MatchToolCategory,
        ["bomb"] = MatchBombCategory,
        ["arrow"] = MatchArrowAmmo,
        ["bolt"] = MatchBoltAmmo,
        ["ammo"] = MatchAmmoCategory
    };

    private static string GetInventoryItemBigGroupId(ItemData item)
    {
        if (item?.m_shared == null)
        {
            return "";
        }

        ItemClassification classification = GetItemClassification(item);
        if (classification.BigGroupResolved)
        {
            return classification.BigGroupId;
        }

        for (int i = 0; i < CraftingRecipeGroupFilters.Count; i++)
        {
            CraftingRecipeGroupFilter filter = CraftingRecipeGroupFilters[i];
            if (filter.Id != "favorite" && filter.Matches(item))
            {
                classification.BigGroupId = filter.Id;
                classification.BigGroupResolved = true;
                return classification.BigGroupId;
            }
        }

        classification.BigGroupId = "";
        classification.BigGroupResolved = true;
        return classification.BigGroupId;
    }

    private static bool ItemMatchesBuiltInPredefinedGroup(ItemData item, string groupId)
    {
        string id = NormalizeGroupId(groupId);
        if (item?.m_shared == null || string.IsNullOrWhiteSpace(id))
        {
            return false;
        }

        ItemClassification classification = GetItemClassification(item);
        if (classification.BuiltInGroupMatches.TryGetValue(id, out bool cached))
        {
            return cached;
        }

        bool result = ItemMatchesBuiltInPredefinedGroupUncached(item, id);
        classification.BuiltInGroupMatches[id] = result;
        return result;
    }

    private static bool ItemMatchesBuiltInPredefinedGroupUncached(ItemData item, string id)
    {
        return BuiltInGroupMatchers.TryGetValue(id, out Func<ItemData, bool> matcher) && matcher(item);
    }

    private static int GetItemClassifierCacheVersion()
    {
        EnsureFeastMaterialTokens();
        return ItemClassifierRuntime.Version;
    }

    private static string GetItemClassifierCacheKey(ItemData item)
    {
        return string.Join("|", new[]
        {
            GetItemPrefabName(item),
            GetSharedName(item),
            item.m_shared.m_itemType.ToString(),
            item.m_shared.m_skillType.ToString(),
            item.m_shared.m_attachOverride.ToString(),
            item.m_shared.m_animationState.ToString(),
            item.m_shared.m_attack.m_attackType.ToString(),
            GetAttackAnimation(item),
            GetAmmoType(item),
            item.m_shared.m_consumeStatusEffect != null ? item.m_shared.m_consumeStatusEffect.name : "",
            item.m_shared.m_maxStackSize.ToString(CultureInfo.InvariantCulture),
            item.m_shared.m_value.ToString(CultureInfo.InvariantCulture),
            FormatClassifierFloat(GetFoodHealth(item)),
            FormatClassifierFloat(GetFoodStamina(item)),
            FormatClassifierFloat(GetFoodEitr(item)),
            FormatClassifierFloat(GetTotalDamage(item)),
            item.m_shared.m_appendToolTip != null ? "append" : ""
        });
    }

    private static string FormatClassifierFloat(float value) =>
        value.ToString("R", CultureInfo.InvariantCulture);

    private static void ClearItemClassifierCaches()
    {
        ItemClassifierRuntime.Cache.Clear();
        ItemClassifierRuntime.AppliedVersion = ItemClassifierRuntime.Version;
    }

    private static ItemClassification GetItemClassification(ItemData item)
    {
        EnsureItemClassifierCacheFresh();
        string itemKey = GetItemClassifierCacheKey(item);
        if (ItemClassifierRuntime.Cache.TryGetValue(itemKey, out ItemClassification cached))
        {
            return cached;
        }

        if (ItemClassifierRuntime.Cache.Count >= MaxItemClassifierCacheItems)
        {
            ItemClassifierRuntime.Cache.Clear();
        }

        ItemClassification classification = new();
        ItemClassifierRuntime.Cache[itemKey] = classification;
        return classification;
    }

    private static void EnsureItemClassifierCacheFresh()
    {
        int version = GetItemClassifierCacheVersion();
        if (ItemClassifierRuntime.AppliedVersion == version)
        {
            return;
        }

        ItemClassifierRuntime.Cache.Clear();
        ItemClassifierRuntime.AppliedVersion = version;
    }

    private static bool IsWeaponItemType(ItemType itemType) =>
        itemType is ItemType.OneHandedWeapon or ItemType.TwoHandedWeapon or ItemType.TwoHandedWeaponLeft or ItemType.Bow or ItemType.Attach_Atgeir or ItemType.Torch;

    private static bool ItemTypeTokenMatches(ItemType itemType, string token)
    {
        if (Enum.TryParse(token, ignoreCase: true, out ItemType parsedType))
        {
            return itemType == parsedType;
        }

        return NormalizeGroupId(token) switch
        {
            "legs" => itemType == ItemType.Legs,
            "cape" => itemType == ItemType.Shoulder,
            "armor" => itemType is ItemType.Helmet or ItemType.Chest or ItemType.Legs or ItemType.Shoulder or ItemType.Utility or ItemType.Trinket,
            "weapon" => IsWeaponItemType(itemType),
            "ammo" => itemType == ItemType.Ammo || itemType == ItemType.AmmoNonEquipable,
            _ => false
        };
    }

    private static bool SkillTypeTokenMatches(Skills.SkillType skillType, string token)
    {
        if (Enum.TryParse(token, ignoreCase: true, out Skills.SkillType parsedType))
        {
            return skillType == parsedType;
        }

        return NormalizeGroupId(token) switch
        {
            "axe" => skillType == Skills.SkillType.Axes,
            "club" => skillType == Skills.SkillType.Clubs,
            "knife" => skillType == Skills.SkillType.Knives,
            "pickaxe" => skillType == Skills.SkillType.Pickaxes,
            "polearm" => skillType == Skills.SkillType.Polearms,
            "spear" => skillType == Skills.SkillType.Spears,
            "sword" => skillType == Skills.SkillType.Swords,
            "fists" => skillType == Skills.SkillType.Unarmed,
            "bow" => skillType == Skills.SkillType.Bows,
            "crossbow" => skillType == Skills.SkillType.Crossbows,
            "elementalmagic" => skillType == Skills.SkillType.ElementalMagic,
            "bloodmagic" => skillType == Skills.SkillType.BloodMagic,
            _ => false
        };
    }

    private static bool MatchMeleeCategory(ItemData item) =>
        !MatchNativeRangedCategory(item) &&
        !MatchNativeMagicCategory(item) &&
        (MatchNativeMeleeCategory(item) ||
         MatchToolCategory(item) ||
         MatchConfiguredCustomGroupInCraftingGroup(item, "melee"));

    private static bool MatchRangedCategory(ItemData item) =>
        MatchNativeRangedCategory(item) ||
        MatchConfiguredCustomGroupInCraftingGroup(item, "ranged");

    private static bool MatchMagicCategory(ItemData item) =>
        MatchNativeMagicCategory(item) ||
        MatchConfiguredCustomGroupInCraftingGroup(item, "magic");

    private static bool MatchNativeMeleeCategory(ItemData item) =>
        MatchSwordCategory(item) ||
        MatchKnifeCategory(item) ||
        MatchClubCategory(item) ||
        MatchPolearmCategory(item) ||
        MatchSpearCategory(item) ||
        MatchAxeCategory(item) ||
        MatchFistsCategory(item) ||
        MatchShieldCategory(item);

    private static bool MatchNativeRangedCategory(ItemData item) =>
        MatchBowWeaponCategory(item) ||
        MatchCrossbowWeaponCategory(item) ||
        MatchArrowAmmo(item) ||
        MatchBoltAmmo(item) ||
        MatchRangedGenericAmmo(item) ||
        MatchBombCategory(item);

    private static bool MatchNativeMagicCategory(ItemData item) =>
        item.m_shared.m_skillType == Skills.SkillType.ElementalMagic ||
        item.m_shared.m_skillType == Skills.SkillType.BloodMagic;

    private static bool MatchArmorCategory(ItemData item) =>
        MatchHelmetCategory(item) ||
        MatchChestCategory(item) ||
        MatchLegsCategory(item) ||
        MatchCapeCategory(item) ||
        MatchUtilityCategory(item) ||
        MatchTrinketCategory(item) ||
        MatchConfiguredCustomGroupInCraftingGroup(item, "armor");

    private static bool MatchFoodCategory(ItemData item) =>
        MatchFeastCategory(item) ||
        IsNativeFoodCategory(item) ||
        MatchStationFoodInputCategory(item) ||
        MatchConfiguredCustomGroupInCraftingGroup(item, "food");

    private static bool MatchHealthFoodCategory(ItemData item) =>
        !MatchFeastCategory(item) &&
        IsNativeFoodCategory(item) &&
        TryGetDominantFoodStat(item, out FoodStat stat) &&
        stat == FoodStat.Health;

    private static bool MatchStaminaFoodCategory(ItemData item) =>
        !MatchFeastCategory(item) &&
        IsNativeFoodCategory(item) &&
        TryGetDominantFoodStat(item, out FoodStat stat) &&
        stat == FoodStat.Stamina;

    private static bool MatchEitrFoodCategory(ItemData item) =>
        !MatchFeastCategory(item) &&
        IsNativeFoodCategory(item) &&
        TryGetDominantFoodStat(item, out FoodStat stat) &&
        stat == FoodStat.Eitr;

    private static bool IsNativeFoodCategory(ItemData item) =>
        HasFoodCarrier(item) &&
        (GetFoodHealth(item) > 0f || GetFoodStamina(item) > 0f || GetFoodEitr(item) > 0f);

    private static bool MatchStationFoodInputCategory(ItemData item) =>
        ItemMatchesStationInput(CraftingRecipeFoodInputTokens, item) ||
        ItemMatchesStationInput(CookingStationFoodInputTokens, item) ||
        ItemMatchesStationInput(FermenterFoodInputTokens, item);

    private static bool ItemMatchesStationInput(HashSet<string> stationInputTokens, ItemData item)
    {
        if (stationInputTokens.Count == 0 || item?.m_shared == null)
        {
            return false;
        }

        foreach (string token in GetItemIdentityTokens(item))
        {
            if (stationInputTokens.Contains(token))
            {
                return true;
            }
        }

        return false;
    }

    private static IEnumerable<string> GetItemIdentityTokens(ItemData item)
    {
        string prefabName = GetItemPrefabName(item);
        string sharedName = item.m_shared.m_name ?? "";
        foreach (string value in new[] { prefabName, NormalizeResourceToken(prefabName), sharedName })
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                yield return value;
            }
        }
    }

    private static bool MatchConsumableCategory(ItemData item) =>
        !MatchMeadBaseCategory(item) &&
        !MatchStationFoodInputCategory(item) &&
        (MatchMeadCategory(item) ||
         MatchPotionCategory(item) ||
         MatchConfiguredCustomGroupInCraftingGroup(item, "consumable"));

    private static bool MatchMeadCategory(ItemData item) =>
        !MatchMeadBaseCategory(item) &&
        !MatchStationFoodInputCategory(item) &&
        ItemMatchesStationInput(FermenterOutputTokens, item);

    private static bool MatchPotionCategory(ItemData item) =>
        !MatchFoodCategory(item) &&
        !MatchMeadCategory(item) &&
        !MatchMeadBaseCategory(item) &&
        (item.m_shared.m_itemType == ItemType.Material || item.m_shared.m_itemType == ItemType.Consumable) &&
        item.m_shared.m_consumeStatusEffect != null;

    private static bool MatchMeadBaseCategory(ItemData item) =>
        !MatchStationFoodInputCategory(item) &&
        (ItemMatchesStationInput(FermenterInputTokens, item) ||
         MatchConfiguredCustomGroupInCraftingGroup(item, "meadbase"));

    private static bool MatchToolCategory(ItemData item) =>
        !MatchFoodCategory(item) &&
        !MatchConsumableCategory(item) &&
        !MatchMeadBaseCategory(item) &&
        (item.m_shared.m_itemType == ItemType.Tool ||
         MatchTorchCategory(item) ||
         item.m_shared.m_skillType == Skills.SkillType.Pickaxes ||
         item.m_shared.m_skillType == Skills.SkillType.Fishing ||
         item.m_shared.m_skillType == Skills.SkillType.Farming ||
         MatchConfiguredCustomGroupInCraftingGroup(item, "tool"));

    private static bool MatchMiscCategory(ItemData item) =>
        !MatchMeleeCategory(item) &&
        !MatchRangedCategory(item) &&
        !MatchMagicCategory(item) &&
        !MatchArmorCategory(item) &&
        !MatchFoodCategory(item) &&
        !MatchConsumableCategory(item) &&
        !MatchMeadBaseCategory(item) &&
        !MatchToolCategory(item);

    private static bool MatchArrowAmmo(ItemData item) =>
        !HasAttackAnimation(item) &&
        (string.Equals(GetAmmoType(item), "$ammo_arrows", StringComparison.Ordinal) ||
         ItemIdentityStartsWith(item, "Arrow")) &&
        GetTotalDamage(item) > 0f;

    private static bool MatchBoltAmmo(ItemData item) =>
        !HasAttackAnimation(item) &&
        (string.Equals(GetAmmoType(item), "$ammo_bolts", StringComparison.Ordinal) ||
         ItemIdentityStartsWith(item, "Bolt")) &&
        GetTotalDamage(item) > 0f;

    private static bool MatchRangedGenericAmmo(ItemData item) =>
        MatchAmmoCategory(item) &&
        !MatchArrowAmmo(item) &&
        !MatchBoltAmmo(item) &&
        !IsNonCombatAmmo(item);

    private static bool MatchAmmoCategory(ItemData item) =>
        item.m_shared.m_itemType == ItemType.Ammo ||
        item.m_shared.m_itemType == ItemType.AmmoNonEquipable;

    private static bool MatchHelmetCategory(ItemData item) =>
        IsItemTypeOrAttach(item, ItemType.Helmet);

    private static bool MatchChestCategory(ItemData item) =>
        IsItemTypeOrAttach(item, ItemType.Chest);

    private static bool MatchLegsCategory(ItemData item) =>
        IsItemTypeOrAttach(item, ItemType.Legs);

    private static bool MatchCapeCategory(ItemData item) =>
        IsItemTypeOrAttach(item, ItemType.Shoulder);

    private static bool MatchUtilityCategory(ItemData item) =>
        IsItemTypeOrAttach(item, ItemType.Utility);

    private static bool MatchTrinketCategory(ItemData item) =>
        IsItemTypeOrAttach(item, ItemType.Trinket);

    private static bool MatchSwordCategory(ItemData item) =>
        IsSkillAttack(item, Skills.SkillType.Swords);

    private static bool MatchAxeCategory(ItemData item) =>
        IsSkillAttack(item, Skills.SkillType.Axes);

    private static bool MatchClubCategory(ItemData item) =>
        IsSkillAttack(item, Skills.SkillType.Clubs);

    private static bool MatchKnifeCategory(ItemData item) =>
        IsSkillAttack(item, Skills.SkillType.Knives);

    private static bool MatchSpearCategory(ItemData item) =>
        IsSkillAttack(item, Skills.SkillType.Spears);

    private static bool MatchPolearmCategory(ItemData item) =>
        IsSkillAttack(item, Skills.SkillType.Polearms) ||
        IsItemTypeOrAttach(item, ItemType.Attach_Atgeir);

    private static bool MatchPickaxeCategory(ItemData item) =>
        item.m_shared.m_skillType == Skills.SkillType.Pickaxes;

    private static bool MatchBowWeaponCategory(ItemData item) =>
        (item.m_shared.m_skillType != Skills.SkillType.Crossbows &&
         item.m_shared.m_skillType != Skills.SkillType.ElementalMagic &&
         item.m_shared.m_skillType != Skills.SkillType.BloodMagic &&
         item.m_shared.m_itemType == ItemType.Bow) ||
        IsSkillAttack(item, Skills.SkillType.Bows);

    private static bool MatchCrossbowWeaponCategory(ItemData item) =>
        !MatchAmmoCategory(item) &&
        item.m_shared.m_skillType == Skills.SkillType.Crossbows &&
        (item.m_shared.m_itemType == ItemType.Bow || HasAttackAnimation(item));

    private static bool MatchTorchCategory(ItemData item) =>
        IsItemTypeOrAttach(item, ItemType.Torch);

    private static bool IsNonCombatAmmo(ItemData item)
    {
        string ammoType = GetAmmoType(item);
        return string.Equals(ammoType, "$item_fishingbait", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(ammoType, "mead", StringComparison.OrdinalIgnoreCase) ||
               ammoType.IndexOf("spell", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static bool ItemIdentityStartsWith(ItemData item, string prefix)
    {
        foreach (string value in new[] { GetItemPrefabName(item), StripLocalizationToken(GetSharedName(item)) })
        {
            if (value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static bool MatchFeastCategory(ItemData item)
    {
        return MatchFeastMaterialIdentity(item) ||
               ItemMatchesFeastMaterialToken(item);
    }

    private static bool MatchFeastMaterialIdentity(ItemData item)
    {
        foreach (string value in new[] { GetItemPrefabName(item), StripLocalizationToken(GetSharedName(item)) })
        {
            string clean = CleanPrefabName(value);
            if (clean.StartsWith("Feast", StringComparison.OrdinalIgnoreCase) &&
                clean.IndexOf("Material", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }
        }

        return false;
    }

    private static bool ItemMatchesFeastMaterialToken(ItemData item)
    {
        EnsureFeastMaterialTokens();
        return ItemMatchesStationInput(ItemClassifierRuntime.FeastMaterialTokens, item);
    }

    private static void EnsureFeastMaterialTokens()
    {
        string signature = GetFeastMaterialTokenSignature();
        if (string.Equals(ItemClassifierRuntime.FeastMaterialSignature, signature, StringComparison.Ordinal))
        {
            return;
        }

        ItemClassifierRuntime.FeastMaterialSignature = signature;
        HashSet<string> tokens = new(StringComparer.OrdinalIgnoreCase);
        if (IsUnityNull(ZNetScene.instance) || ZNetScene.instance!.m_prefabs == null)
        {
            if (ItemClassifierRuntime.FeastMaterialTokens.Count > 0)
            {
                ItemClassifierRuntime.FeastMaterialTokens.Clear();
                ItemClassifierRuntime.Version++;
            }

            return;
        }

        foreach (GameObject prefab in ZNetScene.instance.m_prefabs)
        {
            if (IsUnityNull(prefab))
            {
                continue;
            }

            foreach (Feast feast in prefab.GetComponentsInChildren<Feast>(includeInactive: true))
            {
                if (IsUnityNull(feast))
                {
                    continue;
                }

                AddItemIdentityTokens(tokens, feast.m_foodItem);
                Piece? piece = feast.GetComponent<Piece>() ?? feast.GetComponentInParent<Piece>();
                if (piece?.m_resources == null)
                {
                    continue;
                }

                foreach (Piece.Requirement requirement in piece.m_resources)
                {
                    AddItemIdentityTokens(tokens, requirement.m_resItem);
                }
            }
        }

        if (ItemClassifierRuntime.FeastMaterialTokens.SetEquals(tokens))
        {
            return;
        }

        ItemClassifierRuntime.FeastMaterialTokens.Clear();
        ItemClassifierRuntime.FeastMaterialTokens.UnionWith(tokens);
        ItemClassifierRuntime.Version++;
    }

    private static string GetFeastMaterialTokenSignature()
    {
        if (IsUnityNull(ZNetScene.instance) || ZNetScene.instance!.m_prefabs == null)
        {
            return "none";
        }

        return $"{ZNetScene.instance.GetInstanceID()}|{ZNetScene.instance.m_prefabs.Count}";
    }

    private static bool MatchBombCategory(ItemData item) =>
        item.m_shared.m_itemType == ItemType.OneHandedWeapon &&
        item.m_shared.m_animationState == AnimationState.Unarmed &&
        item.m_shared.m_attack.m_attackType == Attack.AttackType.Projectile &&
        string.Equals(GetAttackAnimation(item), "throw_bomb", StringComparison.Ordinal);

    private static bool MatchElementalMagicCategory(ItemData item) =>
        IsSkillAttack(item, Skills.SkillType.ElementalMagic);

    private static bool MatchBloodMagicCategory(ItemData item) =>
        item.m_shared.m_skillType == Skills.SkillType.BloodMagic &&
        HasAttackAnimation(item);

    private static bool MatchFistsCategory(ItemData item) =>
        IsSkillAttack(item, Skills.SkillType.Unarmed);

    private static bool MatchShieldCategory(ItemData item) =>
        IsItemTypeOrAttach(item, ItemType.Shield) ||
        item.m_shared.m_skillType == Skills.SkillType.Blocking;

    private static bool MatchConfiguredCustomGroupInCraftingGroup(ItemData item, string craftingGroupId)
    {
        if (item?.m_shared == null || !PredefinedGroupOrders.TryGetValue(NormalizeGroupId(craftingGroupId), out List<string> order))
        {
            return false;
        }

        foreach (string groupId in order)
        {
            if (PredefinedGroupDefinitions.ContainsKey(groupId) && ItemMatchesPredefinedGroup(item, groupId))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsSkillAttack(ItemData item, Skills.SkillType skillType) =>
        item.m_shared.m_skillType == skillType &&
        HasAttackAnimation(item) &&
        GetTotalDamage(item) > 0f;

    private static bool IsItemTypeOrAttach(ItemData item, ItemType itemType) =>
        item.m_shared.m_itemType == itemType ||
        item.m_shared.m_attachOverride == itemType;

    private static bool HasFoodCarrier(ItemData item) =>
        item.m_shared.m_itemType == ItemType.Consumable ||
        item.m_shared.m_itemType == ItemType.Material && item.m_shared.m_appendToolTip != null;

    private static float GetFoodHealth(ItemData item) => GetFoodSharedData(item).m_food;

    private static float GetFoodStamina(ItemData item) => GetFoodSharedData(item).m_foodStamina;

    private static float GetFoodEitr(ItemData item) => GetFoodSharedData(item).m_foodEitr;

    private static ItemData.SharedData GetFoodSharedData(ItemData item)
    {
        return item.m_shared.m_appendToolTip?.m_itemData?.m_shared ?? item.m_shared;
    }

    internal static bool TryGetDominantFoodStat(ItemData item, out FoodStat stat)
    {
        return FoodStatCore.TryGetDominant(GetFoodHealth(item), GetFoodStamina(item), GetFoodEitr(item), out stat);
    }

    private static string GetAttackAnimation(ItemData item) => item.m_shared.m_attack.m_attackAnimation ?? "";
    private static string GetAmmoType(ItemData item) => item.m_shared.m_ammoType ?? "";
    private static string GetSharedName(ItemData item) => item.m_shared.m_name ?? "";
    private static string GetItemPrefabName(ItemData item) => CleanPrefabName(item.m_dropPrefab != null ? item.m_dropPrefab.name : "");
    private static bool HasAttackAnimation(ItemData item) => !string.IsNullOrEmpty(GetAttackAnimation(item));
    private static float GetTotalDamage(ItemData item) => item.m_shared.m_damages.GetTotalDamage();
}
