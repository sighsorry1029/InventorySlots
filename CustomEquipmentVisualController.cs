using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using ItemData = ItemDrop.ItemData;
using ItemType = ItemDrop.ItemData.ItemType;

namespace InventorySlots;

public sealed partial class InventorySlotsPlugin
{
    private const string CustomEquipmentVisualItemZdoPrefix = "InventorySlots_CustomEquipmentVisual_Item_";
    private const string CustomEquipmentVisualVariantZdoPrefix = "InventorySlots_CustomEquipmentVisual_Variant_";

    internal static void PrepareCustomEquipmentStatusEffects(Humanoid humanoid, Player player)
    {
        EquipmentVisuals.StatusEffects.Clear();
        EquipmentVisuals.StatusEffectLevels.Clear();

        if (humanoid == null || player == null)
        {
            return;
        }

        foreach (ItemData item in GetCustomEquippedItems(player))
        {
            PrepareCustomEquipmentStatusEffect(item.m_shared.m_equipStatusEffect, item.m_quality);
            if (humanoid.HaveSetEffect(item))
            {
                PrepareCustomEquipmentStatusEffect(item.m_shared.m_setStatusEffect, item.m_quality);
            }
        }
    }

    private static void PrepareCustomEquipmentStatusEffect(StatusEffect? statusEffect, int itemLevel)
    {
        if (IsUnityNull(statusEffect))
        {
            return;
        }

        EquipmentVisuals.StatusEffects.Add(statusEffect!);
        EquipmentVisuals.StatusEffectLevels.TryGetValue(statusEffect!, out int existingLevel);
        EquipmentVisuals.StatusEffectLevels[statusEffect!] = Math.Max(existingLevel, itemLevel);
    }

    internal static void ApplyPreparedCustomEquipmentStatusEffects(Humanoid humanoid)
    {
        if (humanoid == null)
        {
            ClearPreparedCustomEquipmentStatusEffects();
            return;
        }

        foreach (StatusEffect statusEffect in EquipmentVisuals.StatusEffects)
        {
            if (IsUnityNull(statusEffect))
            {
                continue;
            }

            EquipmentVisuals.StatusEffectLevels.TryGetValue(statusEffect, out int itemLevel);
            ((Character)humanoid).m_seman.AddStatusEffect(statusEffect, false, itemLevel, 0f);
            humanoid.m_equipmentStatusEffects.Add(statusEffect);
        }

        ClearPreparedCustomEquipmentStatusEffects();
    }

    internal static void ClearPreparedCustomEquipmentStatusEffects()
    {
        EquipmentVisuals.StatusEffects.Clear();
        EquipmentVisuals.StatusEffectLevels.Clear();
    }

    internal static bool ShouldPreventCustomEquipmentStatusRemoval(SEMan seMan, int nameHash)
    {
        if (nameHash == 0 || EquipmentVisuals.StatusEffects.Count == 0)
        {
            return false;
        }

        Player? player = Player.m_localPlayer;
        if (player == null || seMan != ((Character)player).m_seman)
        {
            return false;
        }

        return EquipmentVisuals.StatusEffects.Any(statusEffect => !IsUnityNull(statusEffect) && statusEffect.NameHash() == nameHash);
    }

    internal static float GetCustomEquipmentArmor(Player player)
    {
        return player == null ? 0f : GetCachedCustomEquipmentArmor(player);
    }

    internal static void ApplyCustomEquipmentDamageModifiers(Player player, ref HitData.DamageModifiers modifiers)
    {
        if (player == null)
        {
            return;
        }

        foreach (ItemData item in GetCustomEquippedItems(player))
        {
            if (item?.m_shared != null && item.m_shared.m_damageModifiers.Count > 0)
            {
                modifiers.Apply(item.m_shared.m_damageModifiers);
            }
        }
    }

    internal static void UpdateCustomEquipmentVisuals(Player player)
    {
        if (IsDedicatedServer || IsUnityNull(player) || player!.m_isLoading)
        {
            if (!IsUnityNull(player?.m_visEquipment))
            {
                ClearCustomEquipmentVisuals(player!.m_visEquipment);
            }
            return;
        }

        VisEquipment visEquipment = player.m_visEquipment;
        if (IsUnityNull(visEquipment) || IsUnityNull(ObjectDB.instance))
        {
            if (!IsUnityNull(visEquipment))
            {
                ClearCustomEquipmentVisuals(visEquipment);
            }
            return;
        }

        Inventory inventory = ((Humanoid)player).GetInventory();
        if (inventory == null)
        {
            ClearCustomEquipmentVisuals(visEquipment);
            return;
        }

        SyncBackpackCompatState(player);
        SyncMagicSupremacyCompatState(player);
        List<CustomEquipmentVisualState> states = BuildCustomEquipmentVisualStatesFromInventory(player, inventory, visEquipment);
        SuppressJewelcraftingNativeVisualSlots(player, inventory);
        ApplyCustomEquipmentVisualStates(visEquipment, states);
    }

    internal static void UpdateCustomEquipmentVisualsFromZdo(VisEquipment visEquipment)
    {
        if (IsDedicatedServer || IsUnityNull(visEquipment) || IsUnityNull(ObjectDB.instance))
        {
            return;
        }

        ZDO? zdo = visEquipment.m_nview != null && visEquipment.m_nview.IsValid() ? visEquipment.m_nview.GetZDO() : null;
        if (zdo == null)
        {
            ClearCustomEquipmentVisuals(visEquipment);
            return;
        }

        List<CustomEquipmentVisualState> states = new();
        foreach (SlotDefinition slot in SlotDefinitions.Where(slot => slot.Kind == SlotKind.CustomEquipment))
        {
            int itemHash = zdo.GetInt(GetCustomEquipmentVisualItemZdoKey(slot.Id));
            if (itemHash == 0)
            {
                continue;
            }

            GameObject itemPrefab = ObjectDB.instance.GetItemPrefab(itemHash);
            if (IsUnityNull(itemPrefab))
            {
                continue;
            }

            ItemDrop itemDrop = itemPrefab.GetComponent<ItemDrop>();
            if (IsUnityNull(itemDrop) || itemDrop.m_itemData?.m_shared == null || !ShouldAttachCustomEquipmentVisual(itemDrop.m_itemData.m_shared.m_itemType))
            {
                continue;
            }

            int variant = zdo.GetInt(GetCustomEquipmentVisualVariantZdoKey(slot.Id));
            states.Add(new CustomEquipmentVisualState(slot.Id, itemHash, variant, itemPrefab.name, itemDrop.m_itemData.m_shared.m_itemType));
        }

        ApplyCustomEquipmentVisualStates(visEquipment, states);
    }

    private static List<CustomEquipmentVisualState> BuildCustomEquipmentVisualStatesFromInventory(Player player, Inventory inventory, VisEquipment visEquipment)
    {
        List<CustomEquipmentVisualState> states = new();
        foreach (SlotDefinition slot in SlotDefinitions.Where(slot => slot.Kind == SlotKind.CustomEquipment))
        {
            ItemData? item = FindItemForSlot(player, inventory, slot);
            int itemHash = 0;
            int variant = 0;
            if (item != null && ShouldAttachCustomEquipmentVisual(item))
            {
                itemHash = StringExtensionMethods.GetStableHashCode(item.m_dropPrefab.name);
                variant = item.m_variant;
                states.Add(new CustomEquipmentVisualState(slot.Id, itemHash, variant, item.m_dropPrefab.name, item.m_shared.m_itemType));
            }

            SetCustomEquipmentVisualZdoValue(visEquipment, slot.Id, itemHash, variant);
        }

        return states;
    }

    private static void ApplyCustomEquipmentVisualStates(VisEquipment visEquipment, IEnumerable<CustomEquipmentVisualState> states)
    {
        HashSet<string> desiredKeys = new(StringComparer.Ordinal);
        bool visualsChanged = false;
        foreach (CustomEquipmentVisualState state in states)
        {
            string key = GetCustomEquipmentVisualKey(visEquipment, state);
            desiredKeys.Add(key);
            if (EquipmentVisuals.Visuals.TryGetValue(key, out CustomEquipmentVisual? existing) && existing.Matches(visEquipment, state.PrefabName, state.Variant))
            {
                continue;
            }

            existing?.Destroy();
            EquipmentVisuals.Visuals[key] = CreateCustomEquipmentVisual(visEquipment, state, key);
            visualsChanged = true;
        }

        foreach (string key in EquipmentVisuals.Visuals.Keys.ToList())
        {
            if (EquipmentVisuals.Visuals[key].IsOwnedBy(visEquipment) && !desiredKeys.Contains(key))
            {
                EquipmentVisuals.Visuals[key].Destroy();
                EquipmentVisuals.Visuals.Remove(key);
                visualsChanged = true;
            }
        }

        if (visualsChanged)
        {
            RefreshVisEquipmentLodGroup(visEquipment);
        }
    }

    internal static void ClearCustomEquipmentVisuals()
    {
        List<VisEquipment> owners = EquipmentVisuals.Visuals.Values
            .Select(visual => visual.Owner)
            .Where(owner => !IsUnityNull(owner))
            .Distinct()
            .ToList();

        foreach (CustomEquipmentVisual visual in EquipmentVisuals.Visuals.Values)
        {
            visual.Destroy();
        }

        EquipmentVisuals.Visuals.Clear();

        foreach (VisEquipment owner in owners)
        {
            RefreshVisEquipmentLodGroup(owner);
        }
    }

    private static void ClearCustomEquipmentVisuals(VisEquipment visEquipment)
    {
        bool changed = false;
        foreach (string key in EquipmentVisuals.Visuals.Keys.ToList())
        {
            if (!EquipmentVisuals.Visuals[key].IsOwnedBy(visEquipment))
            {
                continue;
            }

            EquipmentVisuals.Visuals[key].Destroy();
            EquipmentVisuals.Visuals.Remove(key);
            changed = true;
        }

        if (changed)
        {
            RefreshVisEquipmentLodGroup(visEquipment);
        }
    }

    private static void RefreshVisEquipmentLodGroup(VisEquipment visEquipment)
    {
        if (!IsUnityNull(visEquipment) && !IsUnityNull(visEquipment.m_lodGroup))
        {
            visEquipment.UpdateLodgroup();
        }
    }

    private static bool ShouldAttachCustomEquipmentVisual(ItemData item)
    {
        if (item?.m_shared == null || IsUnityNull(item.m_dropPrefab))
        {
            return false;
        }

        if (IsSmoothbrainBackpackItem(item))
        {
            return false;
        }

        if (IsRustyBagItem(item) || IsRustyQuiverItem(item))
        {
            return false;
        }

        if (IsMagicSupremacyBeltItem(item))
        {
            return false;
        }

        return item.m_shared.m_itemType is ItemType.Helmet or ItemType.Shoulder or ItemType.Utility or ItemType.Trinket;
    }

    private static bool ShouldAttachCustomEquipmentVisual(ItemType itemType)
    {
        return itemType is ItemType.Helmet or ItemType.Shoulder or ItemType.Utility or ItemType.Trinket;
    }

    private static string GetCustomEquipmentVisualKey(VisEquipment visEquipment, CustomEquipmentVisualState state)
    {
        return $"{visEquipment.GetInstanceID()}:{state.SlotId}:{state.ItemHash}";
    }

    private static CustomEquipmentVisual CreateCustomEquipmentVisual(VisEquipment visEquipment, CustomEquipmentVisualState state, string key)
    {
        CustomEquipmentVisual visual = new(key, visEquipment, state.PrefabName, state.Variant);
        try
        {
            switch (state.ItemType)
            {
                case ItemType.Helmet:
                    if (!IsUnityNull(visEquipment.m_helmet))
                    {
                        GameObject instance = visEquipment.AttachItem(state.ItemHash, state.Variant, visEquipment.m_helmet);
                        visual.Add(instance);
                    }
                    break;
                case ItemType.Shoulder:
                case ItemType.Utility:
                case ItemType.Trinket:
                    List<GameObject> instances = visEquipment.AttachArmor(state.ItemHash, state.Variant);
                    visual.AddRange(instances);
                    break;
            }
        }
        catch (Exception ex)
        {
            Log.LogWarning($"Failed to attach custom equipment visual for {state.PrefabName}: {ex.Message}");
        }

        return visual;
    }

    internal static bool TryGetCustomEquipmentVisualRootsForApi(VisEquipment visEquipment, ItemData item, List<GameObject> roots)
    {
        roots.Clear();
        if (IsUnityNull(visEquipment) || item == null || IsUnityNull(item.m_dropPrefab))
        {
            return false;
        }

        string prefabName = item.m_dropPrefab.name;
        foreach (CustomEquipmentVisual visual in EquipmentVisuals.Visuals.Values)
        {
            if (!visual.Matches(visEquipment, prefabName, item.m_variant))
            {
                continue;
            }

            visual.AddActiveInstancesTo(roots);
        }

        return roots.Count > 0;
    }

    private static void SetCustomEquipmentVisualZdoValue(VisEquipment visEquipment, string slotId, int itemHash, int variant)
    {
        if (IsUnityNull(visEquipment) || visEquipment.m_nview == null || !visEquipment.m_nview.IsValid() || !visEquipment.m_nview.IsOwner())
        {
            return;
        }

        ZDO zdo = visEquipment.m_nview.GetZDO();
        if (zdo == null)
        {
            return;
        }

        int itemKey = GetCustomEquipmentVisualItemZdoKey(slotId);
        int variantKey = GetCustomEquipmentVisualVariantZdoKey(slotId);
        if (zdo.GetInt(itemKey) != itemHash)
        {
            zdo.Set(itemKey, itemHash);
        }

        if (zdo.GetInt(variantKey) != variant)
        {
            zdo.Set(variantKey, variant);
        }
    }

    private static int GetCustomEquipmentVisualItemZdoKey(string slotId)
    {
        return StringExtensionMethods.GetStableHashCode(CustomEquipmentVisualItemZdoPrefix + slotId);
    }

    private static int GetCustomEquipmentVisualVariantZdoKey(string slotId)
    {
        return StringExtensionMethods.GetStableHashCode(CustomEquipmentVisualVariantZdoPrefix + slotId);
    }

    private sealed class CustomEquipmentVisualState
    {
        public CustomEquipmentVisualState(string slotId, int itemHash, int variant, string prefabName, ItemType itemType)
        {
            SlotId = slotId;
            ItemHash = itemHash;
            Variant = variant;
            PrefabName = prefabName;
            ItemType = itemType;
        }

        public string SlotId { get; }
        public int ItemHash { get; }
        public int Variant { get; }
        public string PrefabName { get; }
        public ItemType ItemType { get; }
    }

    private sealed class CustomEquipmentVisual
    {
        private readonly List<GameObject> _instances = new();

        public CustomEquipmentVisual(string key, VisEquipment owner, string prefabName, int variant)
        {
            Key = key;
            Owner = owner;
            PrefabName = prefabName;
            Variant = variant;
        }

        public string Key { get; }
        public VisEquipment Owner { get; }
        public string PrefabName { get; }
        public int Variant { get; }

        public bool Matches(VisEquipment owner, string prefabName, int variant)
        {
            return !IsUnityNull(Owner) && Owner == owner && string.Equals(PrefabName, prefabName, StringComparison.Ordinal) && Variant == variant;
        }

        public bool IsOwnedBy(VisEquipment owner)
        {
            return !IsUnityNull(Owner) && Owner == owner;
        }

        public void Add(GameObject? instance)
        {
            if (!IsUnityNull(instance))
            {
                _instances.Add(instance!);
            }
        }

        public void AddRange(IEnumerable<GameObject>? instances)
        {
            if (instances == null)
            {
                return;
            }

            foreach (GameObject instance in instances)
            {
                Add(instance);
            }
        }

        public void AddActiveInstancesTo(List<GameObject> roots)
        {
            foreach (GameObject instance in _instances)
            {
                if (!IsUnityNull(instance))
                {
                    roots.Add(instance);
                }
            }
        }

        public void Destroy()
        {
            foreach (GameObject instance in _instances)
            {
                if (IsUnityNull(instance))
                {
                    continue;
                }

                if (!IsUnityNull(Owner) && !IsUnityNull(Owner.m_lodGroup))
                {
                    Utils.RemoveFromLodgroup(Owner.m_lodGroup, instance);
                }

                UnityEngine.Object.Destroy(instance);
            }

            _instances.Clear();
        }
    }
}
