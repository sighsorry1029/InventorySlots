#requires -Version 7.0
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot
function Read-PluginClass([string]$file) {
    $source = [IO.File]::ReadAllText((Join-Path $root $file))
    $start = $source.IndexOf('public sealed partial class InventorySlotsPlugin', [StringComparison]::Ordinal)
    if ($start -lt 0) { throw "Missing plugin class in $file" }
    return $source.Substring($start)
}
$adapter = Read-PluginClass 'MagicSupremacyCompatAdapter.cs'
$compat = Read-PluginClass 'MagicSupremacyCompat.cs'
function Read-SourceRange([string]$file, [string]$first, [string]$next) {
    $source = [IO.File]::ReadAllText((Join-Path $root $file))
    $start = $source.IndexOf($first, [StringComparison]::Ordinal)
    if ($start -lt 0) { throw "Missing start in $file : $first" }
    $end = $source.IndexOf($next, $start, [StringComparison]::Ordinal)
    if ($end -lt 0) { throw "Missing end in $file : $next" }
    return $source.Substring($start, $end - $start)
}
$slots = Read-SourceRange 'InventorySlotsModels.cs' 'internal sealed class SlotDefinition' 'internal sealed class InventoryPanelDragMarker'
$equip = Read-SourceRange 'SlotEquipController.cs' '    private static ItemData? FindItemForSlot(' '    internal static bool TryRouteInventoryUseToDedicatedSlot('
$routing = Read-SourceRange 'SlotEquipController.cs' '    internal static bool TryRouteHumanoidEquipToDedicatedSlot(' '    private static bool TryGetCachedDedicatedSlotRouteFailure('
$unequip = Read-SourceRange 'SlotEquipController.cs' '    internal static void UnequipInventorySlotsItem(' '    internal static void UnequipCustomEquipmentForDeathDrop('
$relocate = Read-SourceRange 'InventoryPlacementCore.cs' '    private static bool TryRelocateSlotEquipBlockingItem(' '    private static bool IsUsableRegularCell('
$reconcile = Read-SourceRange 'InventoryIntegrityValidation.cs' '    private static bool ReconcileMagicSupremacyTomeAssignments(' '    private static bool ClearDuplicateCustomEquipmentAssignments('
$release = Read-SourceRange 'InventoryIntegrityValidation.cs' '    private static bool TryReleaseItemToRegularInventory(' '    private static void WarnUnableToReleaseSlotItem('
$utility = Read-SourceRange 'YamlConfiguration.cs' '        bool hasMagicSupremacySlot =' '        SlotDefinitions.Add(new SlotDefinition("trinket"'
$equipPatch = Read-SourceRange 'EquipmentRoutingPatches.cs' '[HarmonyPatch(typeof(Humanoid), "EquipItem"' '[HarmonyPatch(typeof(Humanoid), "IsItemEquiped"'
$handler = Read-SourceRange 'SlotEquipPatchHandlers.cs' '    internal static bool TryOverrideHumanoidEquipItem(' '    internal static void OnHumanoidIsItemEquipped('
$plugin = [IO.File]::ReadAllText((Join-Path $root 'Plugin.cs'))
# Use the production constants, including the external slot ID, so the original
# belt/tome mismatch fails this test rather than being masked by a test constant.
$constants = ([regex]::Matches($plugin, 'private const string MagicSupremacy\w*SlotId = "[^"]+";') | ForEach-Object Value) -join "`n"
if (-not $constants) { throw 'Missing Magic Supremacy slot constants' }
$production = @"
#nullable enable
#pragma warning disable CS8600, CS8604, CS0649
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using ItemData = ItemDrop.ItemData;
using ItemType = ItemDrop.ItemData.ItemType;
namespace InventorySlots {
internal enum SlotKind { BuiltIn, CustomEquipment, Quick }
$slots
$adapter
$compat
$equipPatch
public sealed partial class InventorySlotsPlugin {
$constants
$equip
$routing
$unequip
$relocate
$reconcile
$release
$handler
private static void BuildUtilityDefinition() {
    var yamlSlots = new List<YamlSlot>();
    if (HasDedicatedYaml) yamlSlots.Add(new YamlSlot { Id = MagicSupremacyBeltSlotId });
$utility
}
}
}
"@
$fixture = @'
public sealed class ItemDrop {
    public sealed class ItemData {
        public readonly string Prefab;
        public bool m_equipped;
        public bool Equipped { get => m_equipped; set => m_equipped = value; }
        public Dictionary<string, string> m_customData = new();
        public Dictionary<string, string> Data => m_customData;
        public Vector2i m_gridPos;
        public SharedData m_shared = new();
        public sealed class SharedData { public ItemType m_itemType = ItemType.Utility; }
        public enum ItemType { Utility, Helmet, Chest, Legs, Shoulder }
        public ItemData(string prefab) => Prefab = prefab;
    }
}
public readonly struct Vector2i : IEquatable<Vector2i> {
    public readonly int x,y; public Vector2i(int x,int y) { this.x=x; this.y=y; }
    public bool Equals(Vector2i other) => x==other.x && y==other.y;
    public override bool Equals(object? other) => other is Vector2i cell && Equals(cell);
    public override int GetHashCode() => x*397^y;
    public static bool operator ==(Vector2i a,Vector2i b) => a.Equals(b);
    public static bool operator !=(Vector2i a,Vector2i b) => !a.Equals(b);
}
public static class DictionaryExtensions {
    public static TValue? GetValueOrDefault<TKey,TValue>(this Dictionary<TKey,TValue> values,TKey key) where TValue:class => values.TryGetValue(key,out var value)?value:null;
}
public class Inventory {
    public readonly List<ItemData> m_inventory = new();
    public int RegularCells = 2;
    public ItemData? GetItemAt(int x, int y) => m_inventory.FirstOrDefault(i => i.m_gridPos == new Vector2i(x,y));
    public bool ContainsItem(ItemData item) => m_inventory.Contains(item);
    public void Changed() { }
}
public class Humanoid {
    public readonly Dictionary<string, ItemData> NativeSlots = new();
    public ItemData? m_rightItem, m_leftItem, m_chestItem, m_legItem, m_ammoItem, m_helmetItem,
        m_shoulderItem, m_utilityItem, m_trinketItem, m_hiddenLeftItem, m_hiddenRightItem;
    public readonly Inventory Inventory = new();
    public Inventory GetInventory() => Inventory;
    public bool FailNextSetup;
    public bool IsItemEquiped(ItemData item) => ReferenceEquals(m_utilityItem, item) ||
        NativeSlots.Values.Contains(item) || item.m_equipped && item.Data.ContainsKey("slot");
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    public bool EquipItem(ItemData item, bool triggerEquipEffects) { m_utilityItem = item; item.m_equipped = true; return true; }
    public void UnequipItem(ItemData item, bool trigger) {
        if (m_utilityItem == item) m_utilityItem = null;
        if (NativeSlots.GetValueOrDefault("tome") == item) NativeSlots.Remove("tome");
        item.m_equipped = false;
    }
    public void SetupEquipment() { if (FailNextSetup) { FailNextSetup = false; throw new Exception("injected setup failure"); } }
}
public sealed class Player : Humanoid {
    public static Player? m_localPlayer;
    public bool m_isLoading;
    public readonly Dictionary<string, string> Saved = new();
    public List<ItemData> CustomItems => Inventory.m_inventory;
}
// Controlled boundary with the signatures/access levels and tome registry of
// the supplied, unmodified Magic Supremacy 3.1.3 DLL. No Unity plugin is loaded.
namespace Magic_Supremacy {
public static class CustomSlotSystem {
    private struct EquipPatchState { public bool Intercepted; }
    public static int NativePrefixCalls, NativePostfixCalls;
    [HarmonyPatch(typeof(Humanoid), nameof(Humanoid.EquipItem))]
    private static class Humanoid_EquipItem_CustomSlots {
        [HarmonyPrefix]
        private static void Prefix(Humanoid __instance, ItemData item, bool triggerEquipEffects, ref EquipPatchState __state) {
            __state = default;
            if (!Tomes.Contains(item.Prefab)) return;
            NativePrefixCalls++; __state.Intercepted=true;
        }
        [HarmonyPostfix, HarmonyPriority(800)]
        private static void Postfix(Humanoid __instance, ItemData item, ref bool __result, EquipPatchState __state) {
            if (__state.Intercepted) { NativePostfixCalls++; __result=true; __instance.NativeSlots["tome"]=item; }
        }
    }
    public static void Install(Harmony harmony) => harmony.CreateClassProcessor(typeof(Humanoid_EquipItem_CustomSlots)).Patch();
    public sealed class CustomSlotDefinition {
        public string SlotId;
        public string PlayerSaveKey => "DO_Equipped_" + SlotId;
        public CustomSlotDefinition(string id) => SlotId = id;
    }
    public static readonly HashSet<string> Tomes = new() {
        "ArmorMushroomcallerTome_DO", "ArmorDeathcallerTome_DO", "ArmorFrostcallerTome_DO",
        "ArmorFirecallerTome_DO", "ArmorStonecallerTome_DO", "ArmorLightcallerTome_DO", "ArmorStormcallerTome_DO"
    };
    private static readonly CustomSlotDefinition Tome = new("tome"), Other = new("belt");
    public static CustomSlotDefinition? GetMatchingSlotDefinition(ItemData item) =>
        Tomes.Contains(item.Prefab) ? Tome : item.Prefab == "OtherModBelt" ? Other : null;
    private static CustomSlotDefinition? GetDefinitionBySlotId(string id) => id == "tome" ? Tome : null;
    public static ItemData? GetEquippedItem(Humanoid player, string id) => player.NativeSlots.GetValueOrDefault(id);
    public static void SetEquippedItem(Humanoid player, string id, ItemData? item) {
        if (item == null) player.NativeSlots.Remove(id); else player.NativeSlots[id] = item;
    }
    private static string GetOrCreateItemGuid(ItemData item) {
        if (!item.Data.TryGetValue("DO_CustomSlotGuid", out var guid))
            item.Data["DO_CustomSlotGuid"] = guid = Guid.NewGuid().ToString("N");
        return guid;
    }
    private static void SetSavedEquippedGuid(Player player, CustomSlotDefinition definition, string guid) => player.Saved[definition.PlayerSaveKey] = guid;
    private static void ClearSavedEquippedGuid(Player player, CustomSlotDefinition definition) => player.Saved.Remove(definition.PlayerSaveKey);
}
}
namespace InventorySlots {
public sealed partial class InventorySlotsPlugin {
    private readonly Harmony _harmony = new("InventorySlots.SourceLinkedSmoke");
    private static readonly InventorySlotsPlugin _instance = new();
    private static bool TryBlockJewelcraftingUtilityGemEquip(Humanoid humanoid, ItemData item, ref bool result) => false;
    private static bool TryCompletePendingSlotEquip(Humanoid humanoid, ItemData item, out bool result) { result=false; return false; }
    private const string MagicSupremacyGuid = "Dreanegade.Magic_Supremacy";
    private static bool ModLoaded = true;
    private static int Checks;
    private sealed class YamlSlot { public string Id = "", Name = "MagicBelt"; public List<string> Items = new(); }
    private static bool HasDedicatedYaml = true;
    private static YamlSlot UtilityYaml = new();
    private const string SlotIdKey = "slot", EquippedByKey = "owner";
    private static class InventorySafety { public static bool SuppressSlotAutoEquip, RoutingEquipToDedicatedSlot; }
    private sealed class TestLog { public void LogError(string text) { } public void LogWarning(string text) { } }
    private static readonly TestLog Log = new();
    private static string GetPlayerId(Player player) => "local";
    private static string GetSlotName(List<YamlSlot> slots, string id, string fallback) => fallback;
    private static YamlSlot GetYamlSlot(string id) => UtilityYaml;
    private static bool IsJewelcraftingDedicatedJewelryItem(ItemData item) => false;
    private static bool IsJewelcraftingUtilityGemBlocked(ItemData item) => false;
    private static bool IsJewelcraftingUtilityGemBlockedForSlot(ItemData item, SlotDefinition slot) => false;
    private static void ShowJewelcraftingCannotEquipGemMessage(Player player) { }
    private static bool CanUseSpecialSlot(Player player, Inventory inventory, ItemData item, SlotDefinition slot) => slot.Accepts(item);
    private static bool IsQuickSlotUnlocked(Player player, SlotDefinition slot) => true;
    private static bool TryGetSlotGridPos(Inventory inventory, SlotDefinition slot, out Vector2i pos) {
        pos = new(slot.Id == "utility" ? 0 : 1, 1); return true;
    }
    private static bool TryPlaceQuickItemIntoSlot(Player player, Inventory inventory, ItemData item, SlotDefinition slot) => false;
    private static ItemData? CaptureHipLanternEquippedState(Player player) => null;
    private static bool RestoreHipLanternEquippedState(Player player, ItemData item) => false;
    private static bool RestoreCircletExtendedEquippedState(Player player, ItemData item) => false;
    private static bool SynchronizeCircletExtendedEquippedState(Player player, ItemData item) => false;
    private static void RefreshExternalEquipmentEffects(Player player) { }
    private static void UpdateCustomEquipmentVisuals(Player player) { } // intentionally unavailable during rollback
    private static bool IsInventorySlotsCustomEquipped(ItemData item) => item.m_equipped && item.Data.ContainsKey(SlotIdKey);
    private static void ClearItemSlot(ItemData item) { item.Data.Remove(SlotIdKey); item.Data.Remove(EquippedByKey); }
    private static void MarkItemSlot(Player player, ItemData item, SlotDefinition slot) { item.Data[SlotIdKey] = slot.Id; item.Data[EquippedByKey] = GetPlayerId(player); }
    private static bool ClearVanillaEquipmentReferences(Humanoid player, ItemData item) {
        if (player.m_utilityItem != item) return false; player.m_utilityItem = null; return true;
    }
    private static bool ForceClearEquipmentReference(Player player, ItemData item) => ClearVanillaEquipmentReferences(player, item);
    private static bool OnCustomEquipmentCompatEquipped(Player player, ItemData item) { OnMagicSupremacyBeltEquipped(player,item); return true; }
    private static bool OnCustomEquipmentCompatUnequipping(Player player, ItemData item) { OnMagicSupremacyBeltUnequipping(player,item); return true; }
    private static bool IsUsableRegularCell(Inventory inventory, Player player, Vector2i pos) => pos.y == 0 && pos.x >= 0 && pos.x < inventory.RegularCells;
    private static bool TryMoveToFirstFreeRegularCell(Player player, Inventory inventory, ItemData item) {
        for (int x=0; x<inventory.RegularCells; x++) if (inventory.GetItemAt(x,0) == null) { item.m_gridPos = new(x,0); return true; }
        return false;
    }
    private static bool TryGetCachedDedicatedSlotRouteFailure(Player player, Inventory inventory, ItemData item) => false;
    private static void CacheDedicatedSlotRouteFailure(Player player, Inventory inventory, ItemData item) { }
    private static bool TryEquipIntoDedicatedSlot(Player player, Inventory inventory, ItemData item, SlotDefinition slot) => TryEquipIntoSlot(player,inventory,item,slot);
    private static SlotDefinition? GetSlotFromItemMarker(ItemData item) => SlotDefinitions.FirstOrDefault(s=>s.Id==item.Data.GetValueOrDefault(SlotIdKey));
    private static bool IsValidCustomEquipmentAssignment(Player player, ItemData item, SlotDefinition slot) => slot.Accepts(item);
    private static void ClearSlotRecoveryWarning(ItemData item, string reason) { }
    private static void WarnUnableToReleaseSlotItem(ItemData item, string reason) { }
    private static bool TryMoveOverlappingItemToOverflowPreservationCell(Inventory inventory, ItemData item, bool preserveCurrentCell = true) {
        int y=2; while (inventory.GetItemAt(0,y)!=null) y++; item.m_gridPos=new(0,y); return true;
    }
    private static readonly List<SlotDefinition> SlotDefinitions = new();
    private static string NormalizeSlotId(string id) => id.Trim().ToLowerInvariant();
    private static List<string> GetSlotItems(YamlSlot slot) => slot.Items;
    private static bool ItemMatchesSlotItems(ItemData? item, List<string> items) => item != null && items.Contains(item.Prefab);
    private static ItemData? FindCustomEquippedItem(Player player, Func<ItemData, bool> predicate) => player.CustomItems.FirstOrDefault(item => IsInventorySlotsCustomEquipped(item) && predicate(item));
    private sealed class CompatApiRuntimeState<T> where T : class { public T? Api; }
    private static class CompatRuntime { public static readonly CompatApiRuntimeState<MagicSupremacyApi> MagicSupremacy = new(); }
    private delegate bool CompatApiFactory<T>(Assembly assembly, out T? api, out string detail) where T : class;
    private static bool TryGetCompatApi<T>(string guid, string capability, CompatApiRuntimeState<T> runtime, CompatApiFactory<T> factory, string warning, out T? api) where T : class {
        api = null;
        if (!ModLoaded) return false;
        if (runtime.Api != null) { api = runtime.Api; return true; }
        if (!factory(typeof(Magic_Supremacy.CustomSlotSystem).Assembly, out api, out _)) return false;
        runtime.Api = api;
        return true;
    }
    private static void Check(bool result, string message) { Checks++; if (!result) throw new Exception(message); }
    private static Player FreshSlots(bool dedicated = true, params string[] utilityItems) {
        ModLoaded = true; HasDedicatedYaml = dedicated; SlotDefinitions.Clear();
        UtilityYaml = new YamlSlot { Items = utilityItems.ToList() };
        _lastMagicSupremacyBeltCompatItem = null;
        BuildUtilityDefinition();
        if (dedicated) TryAddMagicSupremacyCompatSlot(new YamlSlot(), MagicSupremacyBeltSlotId);
        var player = new Player(); Player.m_localPlayer = player; return player;
    }
    private static SlotDefinition Slot(string id) => SlotDefinitions.Single(s => s.Id == id);
    private static ItemData Add(Player player, string prefab, int x, int y = 0) {
        var item = new ItemData(prefab) { m_gridPos = new(x,y) }; player.Inventory.m_inventory.Add(item); return item;
    }
    private static void CheckNative(Player player, ItemData? item, string message) =>
        Check(player.NativeSlots.GetValueOrDefault("tome") == item &&
              player.Saved.GetValueOrDefault("DO_Equipped_tome") == item?.Data.GetValueOrDefault("DO_CustomSlotGuid"), message);
    private static void RunEquipScenarios() {
        const string Frost = "ArmorFrostcallerTome_DO", Light = "ArmorLightcallerTome_DO", Wind = "ArmorWindcallerUtility_DO";
        var player = FreshSlots(true,Frost); var inventory=player.Inventory; var first=Add(player,Frost,0);
        Check(!Slot("utility").Accepts(first), "Dedicated entry excludes Tomes from Utility even with explicit utility.items");
        Check(TryFindDedicatedEquipmentSlot(player,inventory,first,out var route) && route!.Id==MagicSupremacyBeltSlotId, "Dedicated entry controls automatic destination");
        Check(!TryEquipIntoSlot(player,inventory,first,Slot("utility")) && !first.m_equipped, "Manual Utility placement cannot bypass dedicated entry");
        Check(TryRouteHumanoidEquipToDedicatedSlot(player,first,out bool handled) && handled, "Dedicated Tome route is handled");
        Check(first.Data[SlotIdKey]==MagicSupremacyBeltSlotId && player.m_utilityItem==null,"Tome does not also take vanilla Utility ownership");

        player=FreshSlots(false); inventory=player.Inventory; first=Add(player,Frost,0);
        Check(Magic_Supremacy.CustomSlotSystem.Tomes.All(p=>Slot("utility").Accepts(new ItemData(p))),"Removing entry enables all native Tomes in Utility without item lists");
        Check(TryFindDedicatedEquipmentSlot(player,inventory,first,out route) && route!.Id=="utility","No dedicated entry routes to Utility");
        Check(TryRouteHumanoidEquipToDedicatedSlot(player,first,out handled) && handled && first.Data[SlotIdKey]=="utility","Fallback Utility uses custom ownership");
        CheckNative(player,first,"Utility equip synchronizes native state and saved GUID");
        first.Data.Remove(SlotIdKey); first.Data.Remove(EquippedByKey); first.m_equipped=false;
        player.NativeSlots.Clear(); player.Saved.Clear();
        RestoreSlotEquipmentState(player,inventory,first,Slot("utility"));
        Check(first.Data[SlotIdKey]=="utility" && player.m_utilityItem==null,"Load/restore retains custom Utility ownership");
        CheckNative(player,first,"Restore works before visuals initialize");
        var second=Add(player,Light,0);
        Check(TryEquipIntoSlot(player,inventory,second,Slot("utility")),"Different Tome replaces existing Utility Tome");
        Check(!first.m_equipped && !first.Data.ContainsKey(SlotIdKey) && first.m_gridPos==new Vector2i(0,0),"Displaced Tome vacates equipment cell");
        CheckNative(player,second,"Replacement owns native slot");
        UnequipInventorySlotsItem(player,second); CheckNative(player,null,"Unequip clears native runtime and saved state");

        player=FreshSlots(); inventory=player.Inventory;
        var wind=Add(player,Wind,0); TryEquipIntoSlot(player,inventory,wind,Slot("utility"));
        var broad=new SlotDefinition(MagicSupremacyBeltSlotId,"MagicBelt",SlotKind.CustomEquipment,i=>IsMagicSupremacyBeltItem(i)||i?.Prefab==Wind);
        first=Add(player,Frost,0); TryEquipIntoSlot(player,inventory,first,broad);
        Check(wind.m_equipped && player.m_utilityItem==wind && first.m_equipped,"Windcaller in Utility can coexist with native Tome; overlapping whitelist does not unequip it");

        // Existing saved dedicated Tome after its YAML entry was removed. Replacing
        // occupied Utility must relocate both items or preserve the whole transaction.
        player=FreshSlots(false); inventory=player.Inventory;
        wind=Add(player,Wind,0); TryEquipIntoSlot(player,inventory,wind,Slot("utility"));
        second=Add(player,Light,1,1); second.m_equipped=true;
        MarkItemSlot(player,second,new SlotDefinition(MagicSupremacyBeltSlotId,"old",SlotKind.CustomEquipment,_=>true));
        OnMagicSupremacyBeltEquipped(player,second);
        first=Add(player,Frost,0); Add(player,"Filler",1);
        string oldGuid=player.Saved["DO_Equipped_tome"];
        Check(!TryRouteHumanoidEquipToDedicatedSlot(player,first,out handled) && handled,"Full inventory rejects two displacements and marks failure handled");
        Check(inventory.m_inventory.Count==4 && inventory.m_inventory.Select(i=>i.m_gridPos).Distinct().Count()==4,"Failure preserves all items and unique positions");
        Check(wind.m_equipped && player.m_utilityItem==wind && wind.m_gridPos==new Vector2i(0,1) && second.m_equipped && second.m_gridPos==new Vector2i(1,1),"Rollback restores both occupants");
        Check(!first.m_equipped && first.Data.Count==0 && second.Data[SlotIdKey]==MagicSupremacyBeltSlotId && player.Saved["DO_Equipped_tome"]==oldGuid,"Rollback restores metadata and stable GUID");
        CheckNative(player,second,"Rollback restores native state without visual sync");

        player=FreshSlots(false); inventory=player.Inventory; first=Add(player,Frost,0);
        player.FailNextSetup=true;
        Check(!TryEquipIntoSlot(player,inventory,first,Slot("utility")) && !first.m_equipped && first.Data.Count==0 && first.m_gridPos==new Vector2i(0,0),"Exception after native sync rolls back item ownership and metadata");
        CheckNative(player,null,"Failed first equip leaves no stale native item or GUID");
        var external=Add(player,Light,1); external.m_equipped=true;
        OnMagicSupremacyBeltEquipped(player,external);
        RestoreMagicSupremacyEquippedState(player,external);
        SyncMagicSupremacyCompatState(player);
        CheckNative(player,external,"Restoring external-only native Tome does not let a subsequent custom sync clear it");

        player=FreshSlots(); inventory=player.Inventory;
        first=Add(player,Frost,0,1); second=Add(player,Light,1,1);
        first.m_equipped=second.m_equipped=true;
        MarkItemSlot(player,first,Slot("utility")); MarkItemSlot(player,second,Slot(MagicSupremacyBeltSlotId));
        OnMagicSupremacyBeltEquipped(player,first); Add(player,"Filler",0); Add(player,"Filler",1);
        Check(ReconcileMagicSupremacyTomeAssignments(player,inventory),"Duplicate saved assignments recover after Utility becomes invalid");
        Check(second.m_equipped && !first.m_equipped && first.m_gridPos.y>=2 && inventory.m_inventory.Count==4,"Valid dedicated assignment wins and invalid Tome is preserved even when full");
        CheckNative(player,second,"Recovery synchronizes surviving valid owner");
        Check(!ReconcileMagicSupremacyTomeAssignments(player,inventory),"Recovery is stable on later projection");
    }
    private static void RunHarmonyScenarios() {
        var native=new Harmony(MagicSupremacyGuid);
        foreach (bool nativeFirst in new[] { true,false }) {
            try {
                if (nativeFirst) Magic_Supremacy.CustomSlotSystem.Install(native);
                InitializeMagicSupremacyCompatibility();
                Check(!_magicSupremacyEquipGuardFailed,"Native patch guard installs with actual HarmonyX");
                _instance._harmony.CreateClassProcessor(typeof(HumanoidEquipItemRouteToDedicatedSlotPatch)).Patch();
                if (!nativeFirst) Magic_Supremacy.CustomSlotSystem.Install(native);
                var player=FreshSlots(false); var item=Add(player,"ArmorFrostcallerTome_DO",0);
                Magic_Supremacy.CustomSlotSystem.NativePrefixCalls=Magic_Supremacy.CustomSlotSystem.NativePostfixCalls=0;
                Check(player.EquipItem(item,true),"Patched game equip accepts handled Tome");
                Check(Magic_Supremacy.CustomSlotSystem.NativePrefixCalls==0 && Magic_Supremacy.CustomSlotSystem.NativePostfixCalls==0,"Actual HarmonyX does not run external equip body/postfix for handled success");
                player=FreshSlots(false); item=Add(player,"ArmorFrostcallerTome_DO",0); player.FailNextSetup=true;
                Check(!player.EquipItem(item,true) && !item.m_equipped,"Actual HarmonyX preserves handled rollback result");
                Check(Magic_Supremacy.CustomSlotSystem.NativePrefixCalls==0 && Magic_Supremacy.CustomSlotSystem.NativePostfixCalls==0,"Failed equip cannot be resurrected by external postfix");
                CheckNative(player,null,"Guarded rollback has no stale native state");
                Player.m_localPlayer=null;
                Check(player.EquipItem(item,true) && Magic_Supremacy.CustomSlotSystem.NativePrefixCalls==1 && Magic_Supremacy.CustomSlotSystem.NativePostfixCalls==1,"Unhandled native equip remains available after finalizer resets guard");
                Check(_magicSupremacyHandledEquipItem==null,"No leaked equip suppression after outer call");
            } finally { ShutdownMagicSupremacyCompatibility(); _instance._harmony.UnpatchSelf(); native.UnpatchSelf(); }
        }
    }
    public static int RunMagicSupremacySmoke() {
        var player = new Player();
        var yaml = new YamlSlot();
        Check(TryAddMagicSupremacyCompatSlot(yaml, "magicsupremacy.belt") && SlotDefinitions.Single().Id == "magicsupremacy.belt", "Existing YAML/save slot ID is retained");
        var slot = SlotDefinitions.Single();
        foreach (string prefab in Magic_Supremacy.CustomSlotSystem.Tomes) {
            var item = new ItemData(prefab);
            Check(slot.Accepts(item) && IsMagicSupremacyBeltItem(item), "Native tome automatically accepted: " + prefab);
        }
        Check(!slot.Accepts(new ItemData("OtherModBelt")) && !slot.Accepts(new ItemData("BeltStrength")), "Unrelated native definitions and vanilla utilities stay excluded");
        var first = new ItemData("ArmorFrostcallerTome_DO") { Equipped = true };
        player.CustomItems.Add(first);
        MarkItemSlot(player, first, slot);
        OnMagicSupremacyBeltEquipped(player, first);
        Check(ReferenceEquals(player.NativeSlots.GetValueOrDefault("tome"), first) && !player.NativeSlots.ContainsKey("belt"), "Equip uses native tome runtime state");
        Check(player.Saved.GetValueOrDefault("DO_Equipped_tome") == first.Data.GetValueOrDefault("DO_CustomSlotGuid") && player.Saved.Count == 1, "Equip writes the native tome save key and item GUID");
        string guid = first.Data["DO_CustomSlotGuid"];
        player.NativeSlots.Clear(); player.Saved.Clear(); // Native state lost/recreated while item remains equipped.
        SyncMagicSupremacyCompatState(player);
        Check(ReferenceEquals(player.NativeSlots.GetValueOrDefault("tome"), first) && player.Saved.GetValueOrDefault("DO_Equipped_tome") == guid, "State refresh restores native slot with stable GUID");
        first.Equipped = false;
        var second = new ItemData("ArmorLightcallerTome_DO") { Equipped = true };
        player.CustomItems.Add(second);
        MarkItemSlot(player, second, slot);
        SyncMagicSupremacyCompatState(player);
        Check(ReferenceEquals(player.NativeSlots.GetValueOrDefault("tome"), second) && player.Saved.GetValueOrDefault("DO_Equipped_tome") == second.Data.GetValueOrDefault("DO_CustomSlotGuid"), "Switch replaces native item and saved GUID together");
        OnMagicSupremacyBeltUnequipping(player, first);
        Check(ReferenceEquals(player.NativeSlots.GetValueOrDefault("tome"), second) && player.Saved.Count == 1, "Delayed old-item unequip cannot clear replacement");
        OnMagicSupremacyBeltUnequipping(player, second);
        second.Equipped = false;
        Check(player.NativeSlots.Count == 0 && player.Saved.Count == 0, "Unequip clears native runtime and saved state");
        var wind = new ItemData("ArmorWindcallerUtility_DO") { Equipped = true };
        Check(!slot.Accepts(wind), "Windcaller is not an automatic native tome");
        yaml.Items.Add(wind.Prefab);
        Check(slot.Accepts(wind) && !IsMagicSupremacyBeltItem(wind), "Explicit YAML accepts Windcaller without reclassifying it");
        player.CustomItems.Add(wind);
        OnMagicSupremacyBeltEquipped(player, wind);
        SyncMagicSupremacyCompatState(player);
        OnMagicSupremacyBeltUnequipping(player, wind);
        Check(player.NativeSlots.Count == 0 && player.Saved.Count == 0 && wind.Data.Count == 0, "Windcaller never enters native tome state or save records");
        Magic_Supremacy.CustomSlotSystem.Tomes.Add("FutureNativeTome");
        Check(slot.Accepts(new ItemData("FutureNativeTome")), "Recognition follows external definitions rather than a copied prefab whitelist");
        ModLoaded = false; SlotDefinitions.Clear();
        TryAddMagicSupremacyCompatSlot(new YamlSlot(), "magicsupremacy.belt");
        Check(SlotDefinitions.Count == 0, "Missing optional mod with empty YAML creates no slot");
        TryAddMagicSupremacyCompatSlot(yaml, "magicsupremacy.belt");
        Check(SlotDefinitions.Single().Accepts(wind), "Explicit YAML remains usable without optional mod");
        RunEquipScenarios();
        RunHarmonyScenarios();
        return Checks;
    }
}
}
'@
$core = 'C:/Program Files (x86)/Steam/steamapps/common/Valheim/BepInEx/core'
$outDir = Join-Path $root 'obj/MagicSupremacySmoke'
[void][IO.Directory]::CreateDirectory($outDir)
$hostSource = @'
public static class SmokeProgram {
    public static int Main() {
        try { Console.WriteLine("PASS: " + InventorySlots.InventorySlotsPlugin.RunMagicSupremacySmoke() + " source-linked equip/state and actual HarmonyX assertions; no Unity/game session executed."); return 0; }
        catch(Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }
}
'@
[IO.File]::WriteAllText((Join-Path $outDir 'Program.cs'), $production + $fixture + $hostSource)
$referenceXml = (@('0Harmony.dll','Mono.Cecil.dll','MonoMod.Utils.dll','MonoMod.RuntimeDetour.dll') | ForEach-Object {
    '<Reference Include="' + [IO.Path]::GetFileNameWithoutExtension($_) + '"><HintPath>' + (Join-Path $core $_) + '</HintPath><Private>true</Private></Reference>'
}) -join "`n"
[IO.File]::WriteAllText((Join-Path $outDir 'Smoke.csproj'), @"
<Project Sdk="Microsoft.NET.Sdk">
<PropertyGroup><TargetFramework>net48</TargetFramework><OutputType>Exe</OutputType><LangVersion>latest</LangVersion><Nullable>enable</Nullable><NoWarn>CS8600;CS8604;CS0649</NoWarn></PropertyGroup>
<ItemGroup>$referenceXml</ItemGroup>
</Project>
"@)
& dotnet build (Join-Path $outDir 'Smoke.csproj') -c Debug -v quiet
if ($LASTEXITCODE -ne 0) { throw 'Magic Supremacy smoke host failed to build' }
& (Join-Path $outDir 'bin/Debug/net48/Smoke.exe')
if ($LASTEXITCODE -ne 0) { throw 'Magic Supremacy smoke host failed' }