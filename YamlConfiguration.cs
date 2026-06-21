using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ServerSync;
using UnityEngine;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using ItemType = ItemDrop.ItemData.ItemType;

namespace InventorySlots;

public sealed partial class InventorySlotsPlugin
{
    private static void InitializeYamlSync()
    {
        _syncedYaml = new CustomSyncedValue<string>(ConfigSync, "InventorySlotsYaml", "");
        _syncedYaml.ValueChanged += delegate
        {
            ApplyYaml(_syncedYaml.Value, fromSync: true);
        };

        bool defaultYamlValid = EnsureBuiltInYamlValid();
        string yaml = ReadYamlFileOrDefault();
        if (ApplyYaml(yaml, fromSync: false))
        {
            _syncedYaml.AssignLocalValue(yaml);
        }
        else
        {
            if (defaultYamlValid)
            {
                ApplyYaml(DefaultYaml, fromSync: false);
                _syncedYaml.AssignLocalValue(DefaultYaml);
            }
            else
            {
                _syncedYaml.AssignLocalValue("");
            }
        }
        StartYamlWatcher();
    }

    private static bool EnsureBuiltInYamlValid()
    {
        if (InventorySlotsConfigCore.TryParseYaml(DefaultYaml, out _, out Exception? error))
        {
            return true;
        }

        Log.LogError($"Built-in InventorySlots YAML failed to parse. Falling back to built-in equipment slots only: {error}");
        _yamlConfig = new YamlRoot();
        RebuildPredefinedGroups();
        RebuildResourceMap();
        RebuildSlotDefinitions();
        ClearCraftingRecipeCaches();
        return false;
    }

    private static void StartYamlWatcher()
    {
        StopYamlWatcher();

        string? directory = Path.GetDirectoryName(YamlFilePath);
        if (string.IsNullOrWhiteSpace(directory))
        {
            return;
        }

        Directory.CreateDirectory(directory);
        _yamlWatcher = new FileSystemWatcher(directory, YamlFileName)
        {
            IncludeSubdirectories = false,
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.CreationTime
        };

        FileSystemEventHandler queueReload = (_, _) => QueueYamlReload();
        RenamedEventHandler queueRenameReload = (_, _) => QueueYamlReload();
        ErrorEventHandler queueErrorReload = (_, _) => QueueYamlReload();
        _yamlWatcher.Changed += queueReload;
        _yamlWatcher.Created += queueReload;
        _yamlWatcher.Deleted += queueReload;
        _yamlWatcher.Renamed += queueRenameReload;
        _yamlWatcher.Error += queueErrorReload;
        _yamlWatcher.EnableRaisingEvents = true;
    }

    private static void StopYamlWatcher()
    {
        if (_yamlWatcher == null)
        {
            return;
        }

        _yamlWatcher.EnableRaisingEvents = false;
        _yamlWatcher.Dispose();
        _yamlWatcher = null;
    }

    private static void QueueYamlReload()
    {
        lock (YamlReloadLock)
        {
            _yamlReloadQueued = true;
            _yamlReloadAfterUtc = DateTime.UtcNow.AddMilliseconds(250);
        }
    }

    private static void ProcessYamlHotReload()
    {
        lock (YamlReloadLock)
        {
            if (!_yamlReloadQueued)
            {
                return;
            }

            if (DateTime.UtcNow < _yamlReloadAfterUtc)
            {
                return;
            }

            _yamlReloadQueued = false;
        }

        if (!CanApplyLocalYamlChanges())
        {
            return;
        }

        try
        {
            string yaml = ReadYamlFileOrDefault();
            if (!ApplyYaml(yaml, fromSync: false))
            {
                return;
            }

            _syncedYaml.AssignLocalValue(yaml);
            Log.LogInfo("InventorySlots YAML hot-reloaded.");
        }
        catch (IOException ex)
        {
            Log.LogWarning($"InventorySlots YAML hot reload delayed because the file is still busy: {ex.Message}");
            QueueYamlReload();
        }
        catch (UnauthorizedAccessException ex)
        {
            Log.LogWarning($"InventorySlots YAML hot reload delayed because the file cannot be read yet: {ex.Message}");
            QueueYamlReload();
        }
    }

    private static bool CanApplyLocalYamlChanges()
    {
        return ZNet.instance == null || ZNet.IsSinglePlayer || ConfigSync.IsSourceOfTruth;
    }

    private static string ReadYamlFileOrDefault()
    {
        return File.Exists(YamlFilePath) ? File.ReadAllText(YamlFilePath) : DefaultYaml;
    }

    private static void EnsureDefaultYamlFile()
    {
        if (File.Exists(YamlFilePath))
        {
            return;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(YamlFilePath)!);
        File.WriteAllText(YamlFilePath, DefaultYaml);
    }

    private static bool ApplyYaml(string yaml, bool fromSync)
    {
        YamlApplySnapshot snapshot = CreateYamlApplySnapshot();
        try
        {
            YamlRoot nextConfig = ParseYaml(yaml);
            _yamlConfig = nextConfig;
            RebuildPredefinedGroups();
            RebuildResourceMap();
            RebuildStationInputTokens(force: true);
            RebuildSlotDefinitions();
            ClearCraftingRecipeCaches();
            Player? player = Player.m_localPlayer;
            if (!IsUnityNull(player))
            {
                EnsureInventoryState(player!, InventoryStateEnsureReason.YamlReload);
            }

            return true;
        }
        catch (Exception ex)
        {
            RestoreYamlApplySnapshot(snapshot);
            Log.LogWarning($"Failed to apply {(fromSync ? "synced" : "local")} InventorySlots YAML; keeping the last stable configuration: {ex}");
            return false;
        }
    }

    private static YamlApplySnapshot CreateYamlApplySnapshot() =>
        new(
            _yamlConfig,
            SlotDefinitions.ToList(),
            new Dictionary<string, YamlPredefinedGroup>(PredefinedGroupDefinitions, StringComparer.OrdinalIgnoreCase),
            PredefinedGroupOrder.ToList(),
            PredefinedGroupOrders.ToDictionary(
                entry => entry.Key,
                entry => entry.Value.ToList(),
                StringComparer.OrdinalIgnoreCase),
            new Dictionary<string, int>(ResourceTierByToken, StringComparer.OrdinalIgnoreCase),
            new HashSet<string>(CookingStationInputTokens, StringComparer.OrdinalIgnoreCase),
            new HashSet<string>(CraftingRecipeFoodInputTokens, StringComparer.OrdinalIgnoreCase),
            new HashSet<string>(CookingStationFoodInputTokens, StringComparer.OrdinalIgnoreCase),
            new HashSet<string>(FermenterInputTokens, StringComparer.OrdinalIgnoreCase),
            new HashSet<string>(FermenterOutputTokens, StringComparer.OrdinalIgnoreCase),
            new HashSet<string>(FermenterFoodInputTokens, StringComparer.OrdinalIgnoreCase),
            _stationInputTokensInitialized,
            _cachedStationInputObjectDbItemCount,
            _cachedStationInputPrefabCount,
            _cachedStationInputRecipeCount);

    private static void RestoreYamlApplySnapshot(YamlApplySnapshot snapshot)
    {
        _yamlConfig = snapshot.Config;

        SlotDefinitions.Clear();
        SlotDefinitions.AddRange(snapshot.SlotDefinitions);
        InvalidateSlotDefinitionCaches();

        PredefinedGroupDefinitions.Clear();
        foreach (KeyValuePair<string, YamlPredefinedGroup> entry in snapshot.PredefinedGroupDefinitions)
        {
            PredefinedGroupDefinitions[entry.Key] = entry.Value;
        }

        PredefinedGroupOrder.Clear();
        PredefinedGroupOrder.AddRange(snapshot.PredefinedGroupOrder);

        PredefinedGroupOrders.Clear();
        foreach (KeyValuePair<string, List<string>> entry in snapshot.PredefinedGroupOrders)
        {
            PredefinedGroupOrders[entry.Key] = entry.Value.ToList();
        }

        ResourceTierByToken.Clear();
        foreach (KeyValuePair<string, int> entry in snapshot.ResourceTierByToken)
        {
            ResourceTierByToken[entry.Key] = entry.Value;
        }

        CookingStationInputTokens.Clear();
        CookingStationInputTokens.UnionWith(snapshot.CookingStationInputTokens);

        CraftingRecipeFoodInputTokens.Clear();
        CraftingRecipeFoodInputTokens.UnionWith(snapshot.CraftingRecipeFoodInputTokens);

        CookingStationFoodInputTokens.Clear();
        CookingStationFoodInputTokens.UnionWith(snapshot.CookingStationFoodInputTokens);

        FermenterInputTokens.Clear();
        FermenterInputTokens.UnionWith(snapshot.FermenterInputTokens);

        FermenterOutputTokens.Clear();
        FermenterOutputTokens.UnionWith(snapshot.FermenterOutputTokens);

        FermenterFoodInputTokens.Clear();
        FermenterFoodInputTokens.UnionWith(snapshot.FermenterFoodInputTokens);

        _stationInputTokensInitialized = snapshot.StationInputTokensInitialized;
        _cachedStationInputObjectDbItemCount = snapshot.CachedStationInputObjectDbItemCount;
        _cachedStationInputPrefabCount = snapshot.CachedStationInputPrefabCount;
        _cachedStationInputRecipeCount = snapshot.CachedStationInputRecipeCount;
    }

    private sealed class YamlApplySnapshot
    {
        public YamlApplySnapshot(
            YamlRoot config,
            List<SlotDefinition> slotDefinitions,
            Dictionary<string, YamlPredefinedGroup> predefinedGroupDefinitions,
            List<string> predefinedGroupOrder,
            Dictionary<string, List<string>> predefinedGroupOrders,
            Dictionary<string, int> resourceTierByToken,
            HashSet<string> cookingStationInputTokens,
            HashSet<string> craftingRecipeFoodInputTokens,
            HashSet<string> cookingStationFoodInputTokens,
            HashSet<string> fermenterInputTokens,
            HashSet<string> fermenterOutputTokens,
            HashSet<string> fermenterFoodInputTokens,
            bool stationInputTokensInitialized,
            int cachedStationInputObjectDbItemCount,
            int cachedStationInputPrefabCount,
            int cachedStationInputRecipeCount)
        {
            Config = config;
            SlotDefinitions = slotDefinitions;
            PredefinedGroupDefinitions = predefinedGroupDefinitions;
            PredefinedGroupOrder = predefinedGroupOrder;
            PredefinedGroupOrders = predefinedGroupOrders;
            ResourceTierByToken = resourceTierByToken;
            CookingStationInputTokens = cookingStationInputTokens;
            CraftingRecipeFoodInputTokens = craftingRecipeFoodInputTokens;
            CookingStationFoodInputTokens = cookingStationFoodInputTokens;
            FermenterInputTokens = fermenterInputTokens;
            FermenterOutputTokens = fermenterOutputTokens;
            FermenterFoodInputTokens = fermenterFoodInputTokens;
            StationInputTokensInitialized = stationInputTokensInitialized;
            CachedStationInputObjectDbItemCount = cachedStationInputObjectDbItemCount;
            CachedStationInputPrefabCount = cachedStationInputPrefabCount;
            CachedStationInputRecipeCount = cachedStationInputRecipeCount;
        }

        public YamlRoot Config { get; }
        public List<SlotDefinition> SlotDefinitions { get; }
        public Dictionary<string, YamlPredefinedGroup> PredefinedGroupDefinitions { get; }
        public List<string> PredefinedGroupOrder { get; }
        public Dictionary<string, List<string>> PredefinedGroupOrders { get; }
        public Dictionary<string, int> ResourceTierByToken { get; }
        public HashSet<string> CookingStationInputTokens { get; }
        public HashSet<string> CraftingRecipeFoodInputTokens { get; }
        public HashSet<string> CookingStationFoodInputTokens { get; }
        public HashSet<string> FermenterInputTokens { get; }
        public HashSet<string> FermenterOutputTokens { get; }
        public HashSet<string> FermenterFoodInputTokens { get; }
        public bool StationInputTokensInitialized { get; }
        public int CachedStationInputObjectDbItemCount { get; }
        public int CachedStationInputPrefabCount { get; }
        public int CachedStationInputRecipeCount { get; }
    }

    private static YamlRoot ParseYaml(string yaml)
    {
        return InventorySlotsConfigCore.ParseYaml(yaml);
    }

    private static void RebuildPredefinedGroups()
    {
        PredefinedGroupDefinitions.Clear();
        PredefinedGroupOrder.Clear();
        PredefinedGroupOrders.Clear();

        AddBuiltInPredefinedGroupOrders();
        ApplyYamlGroups(_yamlConfig.Groups);
    }

    private static void AddBuiltInPredefinedGroupOrders()
    {
        foreach (BuiltInItemGroupSection section in ItemGroupRegistry.Sections)
        {
            AddPredefinedGroupOrder(section.Id, section.Subgroups.ToArray());
        }

        AddPredefinedGroupOrder("global", ItemGroupRegistry.GlobalSubgroupOrder().ToArray());
    }

    private static void ApplyYamlGroups(Dictionary<string, List<string>>? rawGroups)
    {
        if (rawGroups == null || rawGroups.Count == 0)
        {
            return;
        }

        foreach (KeyValuePair<string, List<string>> entry in rawGroups)
        {
            string id = NormalizeGroupId(entry.Key);
            if (string.IsNullOrWhiteSpace(id) || TryNormalizeGroupSectionId(entry.Key, out _))
            {
                continue;
            }

            if (IsBuiltInPredefinedGroupId(id))
            {
                Log.LogWarning($"InventorySlots YAML group '{entry.Key}' conflicts with a built-in group id and was ignored. Choose a custom group name such as custom{entry.Key}.");
                continue;
            }

            PredefinedGroupDefinitions[id] = new YamlPredefinedGroup
            {
                Id = id,
                Match = new YamlGroupMatch
                {
                    Prefabs = (entry.Value ?? new List<string>()).Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value.Trim()).ToList()
                }
            };
        }

        foreach (KeyValuePair<string, List<string>> entry in rawGroups)
        {
            if (!TryNormalizeGroupSectionId(entry.Key, out string craftingGroupId))
            {
                continue;
            }

            List<string> order = (entry.Value ?? new List<string>())
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(NormalizeGroupId)
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .ToList();
            if (string.Equals(craftingGroupId, "food", StringComparison.OrdinalIgnoreCase) &&
                !order.Contains("feast", StringComparer.OrdinalIgnoreCase))
            {
                order.Add("feast");
            }

            if (order.Count > 0)
            {
                SetPredefinedGroupOrder(craftingGroupId, order);
            }
        }
    }

    private static bool TryNormalizeGroupSectionId(string? rawId, out string craftingGroupId)
    {
        craftingGroupId = "";
        if (string.IsNullOrWhiteSpace(rawId))
        {
            return false;
        }

        return ItemGroupRegistry.TryNormalizeSectionId(rawId, out craftingGroupId);
    }

    private static bool IsBuiltInPredefinedGroupId(string groupId)
    {
        return ItemGroupRegistry.IsBuiltInGroupId(groupId);
    }

    private static void SetPredefinedGroupOrder(string craftingGroupId, IEnumerable<string> groupIds)
    {
        string normalizedCraftingGroupId = NormalizeGroupId(craftingGroupId);
        if (string.IsNullOrWhiteSpace(normalizedCraftingGroupId))
        {
            return;
        }

        PredefinedGroupOrders[normalizedCraftingGroupId] = new List<string>();
        AddPredefinedGroupOrder(normalizedCraftingGroupId, groupIds.ToArray());
    }

    private static void AddPredefinedGroupOrder(string craftingGroupId, params string[] groupIds)
    {
        string normalizedCraftingGroupId = NormalizeGroupId(craftingGroupId);
        if (string.IsNullOrWhiteSpace(normalizedCraftingGroupId))
        {
            normalizedCraftingGroupId = "global";
        }

        if (!PredefinedGroupOrders.TryGetValue(normalizedCraftingGroupId, out List<string> order))
        {
            order = new List<string>();
            PredefinedGroupOrders[normalizedCraftingGroupId] = order;
        }

        foreach (string groupId in groupIds)
        {
            string normalizedGroupId = NormalizeGroupId(groupId);
            if (string.IsNullOrWhiteSpace(normalizedGroupId) || order.Contains(normalizedGroupId))
            {
                continue;
            }

            order.Add(normalizedGroupId);
            if (normalizedCraftingGroupId == "global" && !PredefinedGroupOrder.Contains(normalizedGroupId))
            {
                PredefinedGroupOrder.Add(normalizedGroupId);
            }
        }
    }

    private static void RebuildResourceMap()
    {
        ResourceTierByToken.Clear();
        foreach (KeyValuePair<string, int> entry in InventorySlotsConfigCore.BuildResourceTierMap(_yamlConfig))
        {
            ResourceTierByToken[entry.Key] = entry.Value;
        }
    }

    private static void RebuildSlotDefinitions()
    {
        SlotDefinitions.Clear();

        List<YamlSlot> yamlSlots = _yamlConfig.Slots ?? new List<YamlSlot>();

        SlotDefinitions.Add(new SlotDefinition("helmet", GetSlotName(yamlSlots, "helmet", "Helmet"), SlotKind.BuiltIn, item => item?.m_shared?.m_itemType == ItemType.Helmet));
        SlotDefinitions.Add(new SlotDefinition("chest", GetSlotName(yamlSlots, "chest", "Chest"), SlotKind.BuiltIn, item => item?.m_shared?.m_itemType == ItemType.Chest));
        SlotDefinitions.Add(new SlotDefinition("legs", GetSlotName(yamlSlots, "legs", "Legs"), SlotKind.BuiltIn, item => item?.m_shared?.m_itemType == ItemType.Legs));
        SlotDefinitions.Add(new SlotDefinition("cape", GetSlotName(yamlSlots, "cape", "Cape"), SlotKind.BuiltIn, item => item?.m_shared?.m_itemType == ItemType.Shoulder));
        SlotDefinitions.Add(new SlotDefinition("utility", GetSlotName(yamlSlots, "utility", "Utility"), SlotKind.BuiltIn, item => item?.m_shared?.m_itemType == ItemType.Utility && !IsJewelcraftingDedicatedJewelryItem(item) && !IsJewelcraftingUtilityGemBlocked(item)));
        SlotDefinitions.Add(new SlotDefinition("trinket", GetSlotName(yamlSlots, "trinket", "Trinket"), SlotKind.BuiltIn, item => item?.m_shared?.m_itemType == ItemType.Trinket));

        HashSet<string> seenJewelcraftingSlots = new(StringComparer.OrdinalIgnoreCase);
        foreach (YamlSlot slot in yamlSlots)
        {
            string id = NormalizeSlotId(slot.Id);
            if (string.IsNullOrWhiteSpace(id))
            {
                continue;
            }

            if (IsBuiltInEquipmentSlotId(id))
            {
                continue;
            }

            if (ShouldSuppressYamlSlotForJewelcraftingGem(id))
            {
                continue;
            }

            if (IsJewelcraftingReservedSlotId(id))
            {
                seenJewelcraftingSlots.Add(id);
                AddJewelcraftingSlot(slot, id);
                continue;
            }

            if (TryAddBackpackCompatSlot(slot, id))
            {
                continue;
            }

            if (TryAddMagicSupremacyCompatSlot(slot, id))
            {
                continue;
            }

            if (SlotDefinitions.Any(existing => existing.Id == id))
            {
                continue;
            }

            List<string> items = GetSlotItems(slot);
            if (items.Count == 0)
            {
                continue;
            }

            string name = string.IsNullOrWhiteSpace(slot.Name) ? id : slot.Name.Trim();
            SlotDefinitions.Add(new SlotDefinition(id, name, SlotKind.CustomEquipment, item => ItemMatchesSlotItems(item, items)));
        }

        if (!seenJewelcraftingSlots.Contains(JewelcraftingNecklaceSlotId))
        {
            AddJewelcraftingSlot(null, JewelcraftingNecklaceSlotId);
        }

        if (!seenJewelcraftingSlots.Contains(JewelcraftingRingSlotId))
        {
            AddJewelcraftingSlot(null, JewelcraftingRingSlotId);
        }

        for (int i = 0; i < GetQuickSlotCount(); i++)
        {
            int quickSlotIndex = i;
            int displayIndex = i + 1;
            string name = LocalizeUi("$inventoryslots_quick_slot_format", "Quick {index}").Replace("{index}", displayIndex.ToString());
            SlotDefinitions.Add(new SlotDefinition($"quick{displayIndex}", name, SlotKind.Quick, QuickSlotAcceptsItem, quickSlotIndex));
        }

        InvalidateSlotDefinitionCaches();
        Log.LogInfo($"InventorySlots slot definitions rebuilt: {SlotDefinitions.Count} special slots, {PredefinedGroupDefinitions.Count} YAML custom groups.");
    }

    private static string GetSlotName(IEnumerable<YamlSlot> slots, string id, string fallback)
    {
        YamlSlot? slot = slots.FirstOrDefault(entry => string.Equals(NormalizeSlotId(entry.Id), id, StringComparison.OrdinalIgnoreCase));
        return slot == null || string.IsNullOrWhiteSpace(slot.Name) ? fallback : slot.Name.Trim();
    }

    private static YamlSlot? GetYamlSlot(string id)
    {
        string normalizedId = NormalizeSlotId(id);
        return (_yamlConfig.Slots ?? new List<YamlSlot>())
            .FirstOrDefault(slot => string.Equals(NormalizeSlotId(slot.Id), normalizedId, StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsBuiltInEquipmentSlotId(string id) =>
        id is "helmet" or "chest" or "legs" or "cape" or "utility" or "trinket";

    private static bool IsJewelcraftingReservedSlotId(string id) =>
        id is JewelcraftingRingSlotId or JewelcraftingNecklaceSlotId;

    private static List<string> GetSlotItems(YamlSlot? slot) =>
        (slot?.Items ?? new List<string>())
        .Where(value => !string.IsNullOrWhiteSpace(value))
        .Select(value => value.Trim())
        .ToList();
}
