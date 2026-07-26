using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ServerSync;
using UnityEngine;
using ItemType = ItemDrop.ItemData.ItemType;

namespace InventorySlots;

public sealed partial class InventorySlotsPlugin
{
    private static YamlRoot _yamlConfig = new();

    private static void InitializeYamlSync()
    {
        _syncedYaml = new CustomSyncedValue<string>(ConfigSync, "InventorySlotsYaml", "");
        _syncedResourceMapYaml = new CustomSyncedValue<string>(ConfigSync, "InventorySlotsResourceMapYaml", "");
        _syncedYaml.ValueChanged += delegate
        {
            ApplyYaml(_syncedYaml.Value, fromSync: true);
        };
        _syncedResourceMapYaml.ValueChanged += delegate
        {
            ApplyResourceMapYaml(_syncedResourceMapYaml.Value, fromSync: true);
        };

        bool defaultYamlValid = EnsureBuiltInYamlValid();
        string yaml = ReadYamlFileOrDefault();
        if (ApplyYaml(yaml, fromSync: false))
        {
            _syncedYaml.AssignLocalValue(yaml);
        }
        else
        {
            if (defaultYamlValid && ApplyYaml(DefaultYaml, fromSync: false))
            {
                _syncedYaml.AssignLocalValue(DefaultYaml);
            }
            else
            {
                _syncedYaml.AssignLocalValue("");
            }
        }

        bool defaultResourceMapValid = EnsureBuiltInResourceMapYamlValid();
        string resourceMapYaml = ReadResourceMapFileOrDefault();
        if (ApplyResourceMapYaml(resourceMapYaml, fromSync: false))
        {
            _syncedResourceMapYaml.AssignLocalValue(resourceMapYaml);
        }
        else
        {
            if (defaultResourceMapValid && ApplyResourceMapYaml(DefaultResourceMapYaml, fromSync: false))
            {
                _syncedResourceMapYaml.AssignLocalValue(DefaultResourceMapYaml);
            }
            else
            {
                _syncedResourceMapYaml.AssignLocalValue("");
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
        RebuildInventoryLimits();
        RebuildSlotDefinitions();
        ClearCraftingRecipeCaches();
        return false;
    }

    private static bool EnsureBuiltInResourceMapYamlValid()
    {
        if (InventorySlotsConfigCore.TryParseResourceMapYaml(DefaultResourceMapYaml, out _, out Exception? error))
        {
            return true;
        }

        Log.LogError($"Built-in ResourceMap YAML failed to parse. Falling back to no resource tiers: {error}");
        ResourceTierByToken.Clear();
        try
        {
            ClearCraftingRecipeCaches();
        }
        catch (Exception ex)
        {
            Log.LogWarning($"ResourceMap fallback succeeded, but crafting cache refresh failed: {ex}");
        }

        return false;
    }

    private static void StartYamlWatcher()
    {
        StopYamlWatcher();

        Directory.CreateDirectory(ConfigDirectoryPath);
        _yamlWatcher = new FileSystemWatcher(ConfigDirectoryPath, "*.yml")
        {
            IncludeSubdirectories = false,
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.CreationTime
        };

        FileSystemEventHandler queueReload = (_, args) => QueueYamlReloadForFile(args.FullPath);
        RenamedEventHandler queueRenameReload = (_, args) =>
        {
            QueueYamlReloadForFile(args.OldFullPath);
            QueueYamlReloadForFile(args.FullPath);
        };
        ErrorEventHandler queueErrorReload = (_, _) => QueueYamlReload(reloadYaml: true, reloadResourceMap: true);
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

    private static void QueueYamlReloadForFile(string? path)
    {
        string fileName = Path.GetFileName(path);
        if (string.Equals(fileName, YamlFileName, StringComparison.OrdinalIgnoreCase))
        {
            QueueYamlReload(reloadYaml: true, reloadResourceMap: false);
        }
        else if (string.Equals(fileName, ResourceMapFileName, StringComparison.OrdinalIgnoreCase))
        {
            QueueYamlReload(reloadYaml: false, reloadResourceMap: true);
        }
    }

    private static void QueueYamlReload(bool reloadYaml, bool reloadResourceMap)
    {
        if (!reloadYaml && !reloadResourceMap)
        {
            return;
        }

        lock (YamlReloadLock)
        {
            _yamlReloadQueued |= reloadYaml;
            _resourceMapReloadQueued |= reloadResourceMap;
            _yamlReloadAfterUtc = DateTime.UtcNow.AddMilliseconds(250);
        }
    }

    private static void ProcessYamlHotReload()
    {
        bool reloadYaml;
        bool reloadResourceMap;
        lock (YamlReloadLock)
        {
            if (!_yamlReloadQueued && !_resourceMapReloadQueued)
            {
                return;
            }

            if (DateTime.UtcNow < _yamlReloadAfterUtc)
            {
                return;
            }

            reloadYaml = _yamlReloadQueued;
            reloadResourceMap = _resourceMapReloadQueued;
            _yamlReloadQueued = false;
            _resourceMapReloadQueued = false;
        }

        if (!CanApplyLocalYamlChanges())
        {
            return;
        }

        if (reloadYaml)
        {
            ProcessInventorySlotsYamlHotReload();
        }

        if (reloadResourceMap)
        {
            ProcessResourceMapYamlHotReload();
        }
    }

    private static void ProcessInventorySlotsYamlHotReload()
    {
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
            QueueYamlReload(reloadYaml: true, reloadResourceMap: false);
        }
        catch (UnauthorizedAccessException ex)
        {
            Log.LogWarning($"InventorySlots YAML hot reload delayed because the file cannot be read yet: {ex.Message}");
            QueueYamlReload(reloadYaml: true, reloadResourceMap: false);
        }
    }

    private static void ProcessResourceMapYamlHotReload()
    {
        try
        {
            string yaml = ReadResourceMapFileOrDefault();
            if (!ApplyResourceMapYaml(yaml, fromSync: false))
            {
                return;
            }

            _syncedResourceMapYaml.AssignLocalValue(yaml);
            Log.LogInfo("InventorySlots ResourceMap YAML hot-reloaded.");
        }
        catch (IOException ex)
        {
            Log.LogWarning($"ResourceMap YAML hot reload delayed because the file is still busy: {ex.Message}");
            QueueYamlReload(reloadYaml: false, reloadResourceMap: true);
        }
        catch (UnauthorizedAccessException ex)
        {
            Log.LogWarning($"ResourceMap YAML hot reload delayed because the file cannot be read yet: {ex.Message}");
            QueueYamlReload(reloadYaml: false, reloadResourceMap: true);
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

    private static string ReadResourceMapFileOrDefault()
    {
        return File.Exists(ResourceMapFilePath) ? File.ReadAllText(ResourceMapFilePath) : DefaultResourceMapYaml;
    }

    private static void EnsureDefaultYamlFiles()
    {
        Directory.CreateDirectory(ConfigDirectoryPath);
        if (!File.Exists(YamlFilePath))
        {
            File.WriteAllText(YamlFilePath, DefaultYaml);
        }

        if (!File.Exists(ResourceMapFilePath))
        {
            File.WriteAllText(ResourceMapFilePath, DefaultResourceMapYaml);
        }
    }

    private static bool ApplyYaml(string yaml, bool fromSync)
    {
        string source = fromSync ? "synced" : "local";
        YamlRoot nextConfig;
        try
        {
            nextConfig = InventorySlotsConfigCore.ParseYaml(yaml);
        }
        catch (Exception ex)
        {
            Log.LogWarning($"Failed to parse {source} InventorySlots YAML; keeping the last stable configuration: {ex}");
            return false;
        }

        YamlApplySnapshot snapshot = CreateYamlApplySnapshot();
        try
        {
            _yamlConfig = nextConfig;
            RebuildPredefinedGroups();
            RebuildInventoryLimits();
            RebuildSlotDefinitions();
        }
        catch (Exception ex)
        {
            RestoreYamlApplySnapshot(snapshot);
            Log.LogWarning($"Failed to apply {source} InventorySlots YAML; keeping the last stable configuration: {ex}");
            return false;
        }

        try
        {
            RebuildStationInputTokens(force: true);
        }
        catch (Exception ex)
        {
            Log.LogWarning($"Applied {source} InventorySlots YAML, but station token refresh failed: {ex}");
        }

        try
        {
            ClearCraftingRecipeCaches();
        }
        catch (Exception ex)
        {
            Log.LogWarning($"Applied {source} InventorySlots YAML, but crafting cache refresh failed: {ex}");
        }

        InvalidateCraftingRecipeView();

        try
        {
            Player? player = Player.m_localPlayer;
            if (!IsUnityNull(player))
            {
                EnsureInventoryState(player!, InventoryStateEnsureReason.YamlReload);
            }
        }
        catch (Exception ex)
        {
            Log.LogWarning($"Applied {source} InventorySlots YAML, but player inventory reconciliation failed: {ex}");
        }

        return true;
    }

    private static bool ApplyResourceMapYaml(string yaml, bool fromSync)
    {
        string source = fromSync ? "synced" : "local";
        Dictionary<string, int> nextResourceTiers;
        try
        {
            nextResourceTiers = InventorySlotsConfigCore.ParseResourceMapYaml(yaml);
        }
        catch (Exception ex)
        {
            Log.LogWarning($"Failed to parse {source} ResourceMap YAML; keeping the last stable resource tiers: {ex}");
            return false;
        }

        Dictionary<string, int> previousResourceTiers =
            new(ResourceTierByToken, StringComparer.OrdinalIgnoreCase);
        try
        {
            ResourceTierByToken.Clear();
            foreach (KeyValuePair<string, int> entry in nextResourceTiers)
            {
                ResourceTierByToken[entry.Key] = entry.Value;
            }
        }
        catch (Exception ex)
        {
            ResourceTierByToken.Clear();
            foreach (KeyValuePair<string, int> entry in previousResourceTiers)
            {
                ResourceTierByToken[entry.Key] = entry.Value;
            }

            Log.LogWarning($"Failed to apply {source} ResourceMap YAML; keeping the last stable resource tiers: {ex}");
            return false;
        }

        try
        {
            ClearCraftingRecipeCaches();
        }
        catch (Exception ex)
        {
            Log.LogWarning($"Applied {source} ResourceMap YAML, but crafting cache refresh failed: {ex}");
        }

        InvalidateCraftingRecipeView();
        return true;
    }

    private static YamlApplySnapshot CreateYamlApplySnapshot() =>
        new(
            _yamlConfig,
            SlotDefinitions.ToList(),
            PredefinedGroupDefinitions.ToDictionary(
                entry => entry.Key,
                entry => entry.Value.ToList(),
                StringComparer.OrdinalIgnoreCase),
            PredefinedGroupOrder.ToList(),
            PredefinedGroupOrders.ToDictionary(
                entry => entry.Key,
                entry => entry.Value.ToList(),
                StringComparer.OrdinalIgnoreCase));

    private static void RestoreYamlApplySnapshot(YamlApplySnapshot snapshot)
    {
        _yamlConfig = snapshot.Config;

        SlotDefinitions.Clear();
        SlotDefinitions.AddRange(snapshot.SlotDefinitions);
        InvalidateSlotDefinitionCaches();

        PredefinedGroupDefinitions.Clear();
        foreach (KeyValuePair<string, List<string>> entry in snapshot.PredefinedGroupDefinitions)
        {
            PredefinedGroupDefinitions[entry.Key] = entry.Value.ToList();
        }

        PredefinedGroupOrder.Clear();
        PredefinedGroupOrder.AddRange(snapshot.PredefinedGroupOrder);

        PredefinedGroupOrders.Clear();
        foreach (KeyValuePair<string, List<string>> entry in snapshot.PredefinedGroupOrders)
        {
            PredefinedGroupOrders[entry.Key] = entry.Value.ToList();
        }

        RebuildInventoryLimits();
    }

    private sealed class YamlApplySnapshot
    {
        public YamlApplySnapshot(
            YamlRoot config,
            List<SlotDefinition> slotDefinitions,
            Dictionary<string, List<string>> predefinedGroupDefinitions,
            List<string> predefinedGroupOrder,
            Dictionary<string, List<string>> predefinedGroupOrders)
        {
            Config = config;
            SlotDefinitions = slotDefinitions;
            PredefinedGroupDefinitions = predefinedGroupDefinitions;
            PredefinedGroupOrder = predefinedGroupOrder;
            PredefinedGroupOrders = predefinedGroupOrders;
        }

        public YamlRoot Config { get; }
        public List<SlotDefinition> SlotDefinitions { get; }
        public Dictionary<string, List<string>> PredefinedGroupDefinitions { get; }
        public List<string> PredefinedGroupOrder { get; }
        public Dictionary<string, List<string>> PredefinedGroupOrders { get; }
    }

    private static void RebuildPredefinedGroups()
    {
        PredefinedGroupDefinitions.Clear();
        PredefinedGroupOrder.Clear();
        PredefinedGroupOrders.Clear();

        AddBuiltInPredefinedGroupOrders();
        ApplyYamlGroups(_yamlConfig.Groups);
    }

    private static void RebuildInventoryLimits()
    {
        InventoryLimits.Clear();
        foreach (KeyValuePair<string, int> entry in InventorySlotsConfigCore.BuildInventoryLimits(_yamlConfig))
        {
            InventoryLimits[entry.Key] = entry.Value;
        }

        unchecked
        {
            InventoryDefinitions.InventoryLimitVersion++;
        }

        InvalidateInventoryPlacementCaches();
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

            PredefinedGroupDefinitions[id] = (entry.Value ?? new List<string>())
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value.Trim())
                .ToList();
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
