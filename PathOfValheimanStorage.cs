using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using BepInEx.Bootstrap;
using HarmonyLib;
using UnityEngine;
using ItemData = ItemDrop.ItemData;

namespace InventorySlots;

public sealed partial class InventorySlotsPlugin
{
    private const string PathOfValheimanGuid = "treextr.pathofvalheiman";
    private const string PovLayoutKey = "InventorySlots.PovLayout.v1";
    private const string PovNativeRowsKey = "valheiman.equipment.v1.rowStart";
    private static PathOfValheimanStorageBridge? _pov;
    private static readonly Harmony PovStoragePatches = new(ModGUID + ".pov.storage");
    private static readonly Dictionary<string, SlotDefinition> PovSlots = new(StringComparer.Ordinal);
    private static readonly HashSet<Player> PovLoading = new();
    private static readonly HashSet<Player> PovLoadFailed = new();
    private static int _povMutationDepth;
    private static int _povUnlockStamp;
    private static float _povNextPoll;
    private static bool _povNotificationActive;
    private static bool _povWasEnabled;
    private static bool _povCoinsWeightless;
    private static Inventory? _povCoinWeightInventory;
    private static ItemData? _povLastQuiver;

    private bool InitializePathOfValheimanStorage()
    {
        if (IsDedicatedServer || !Chainloader.PluginInfos.TryGetValue(PathOfValheimanGuid, out var plugin)) return true;
        try
        {
            if (plugin.Instance == null) throw new InvalidOperationException("PoV did not initialize");
            var bridge = new PathOfValheimanStorageBridge(plugin.Instance.GetType().Assembly);
            _pov = bridge;
            bridge.InstallFixes(PovStoragePatches);
            _povWasEnabled = bridge.Enabled();
            bridge.Register(ReadPathOfValheimanItem, TryPutPathOfValheimanItem, TakeOutPathOfValheimanItem);
            // Register before PoV's ObjectDB callback. Differently rolled items must
            // never become mergeable merely because both keys belong to PoV.
            var keys = bridge.Bind<Func<IEnumerable<string>>>("PassiveTree.InventorySlotsCompat", "OurKeys");
            foreach (string key in keys())
                StackMetadataPolicy.Register(key, (a, _) => a, (a, b) => string.Equals(a, b, StringComparison.Ordinal));
            Log.LogInfo("PoV storage provider registered; InventorySlots owns cells and discovery progression, PoV retains node requirements and effects.");
            return true;
        }
        catch (Exception error)
        {
            _pov?.Unregister();
            _pov = null;
            PovStoragePatches.UnpatchSelf();
            PovStoragePatches.Patch(AccessTools.Method(typeof(Game), "SpawnPlayer"),
                prefix: new HarmonyMethod(typeof(InventorySlotsPlugin), nameof(BlockUnsupportedPathOfValheimanLoad)) { priority = Priority.First });
            enabled = false;
            Log.LogError("InventorySlots was not activated: PoV storage compatibility could not be verified. " + error.GetBaseException().Message);
            return false;
        }
    }

    private static void BlockUnsupportedPathOfValheimanLoad() =>
        throw new InvalidOperationException("InventorySlots / PoV compatibility is unavailable. Character loading was blocked to preserve saved items; see the initialization error.");

    private static void AppendPathOfValheimanSlots()
    {
        PovSlots.Clear();
        if (_pov == null) return;
        // Preserve original ordinal positions, including every configured quick slot.
        for (int i = 0; i < SlotDefinitions.Count; i++)
        {
            SlotDefinition previous = SlotDefinitions[i];
            string? cell = previous.Id switch {
                "helmet" or "chest" or "legs" or "cape" or "trinket" => previous.Id,
                "utility" => "belt", "demister" => "utility.wisp", "wishbone" => "utility.wishbone", _ => null };
            if (cell == null) continue;
            bool storage = _pov.StorageCells.Contains(cell);
            var replacement = new SlotDefinition(previous.Id, cell == "belt" ? "Belt" : previous.Name,
                storage && _pov.Enabled() ? SlotKind.Quick : previous.Kind,
                item => item != null && (_pov.Enabled() ? _pov.Accepts(cell, item) : previous.Accepts(item)), povCell: cell);
            SlotDefinitions[i] = replacement;
            PovSlots[cell] = replacement;
        }
        foreach (string cell in _pov.StorageCells)
        {
            if (PovSlots.ContainsKey(cell) || IsUnsupportedPathOfValheimanCell(cell)) continue;
            string role = cell;
            var slot = new SlotDefinition("pov." + role, PovCellLabel(role), SlotKind.Quick,
                item => item != null && _pov.Accepts(role, item), povCell: role);
            SlotDefinitions.Add(slot);
            PovSlots.Add(role, slot);
        }
        var quiver = new SlotDefinition("pov.quiver", "Quiver", SlotKind.BuiltIn,
            item => item != null && _pov.Accepts("quiver", item), povCell: "quiver");
        SlotDefinitions.Add(quiver);
        PovSlots.Add("quiver", quiver);
        if (_pov.HasExtraTrinkets)
            foreach (string role in PovTrinketRoles.Skip(1))
            {
                var trinket = new SlotDefinition("pov." + role,
                    role == "trinket.hand" ? "Trinket (Hand)" : PovCellLabel(role), SlotKind.BuiltIn,
                    item => item != null && _pov.Accepts(role, item), povCell: role);
                SlotDefinitions.Add(trinket);
                PovSlots.Add(role, trinket);
            }
    }

    private static string PovCellLabel(string id)
    {
        int dot = id.LastIndexOf('.');
        if (dot > 0 && int.TryParse(id.Substring(dot + 1), out int index))
            return char.ToUpperInvariant(id[0]) + id.Substring(1, dot - 1) + " " + (index + 1);
        return id == "utility.wisp" ? "Wisplight" : id == "utility.wishbone" ? "Wishbone" : "Utility";
    }

    private static bool PovCanMutate(Player player) => _pov != null && player != null && player == Player.m_localPlayer &&
        !player.m_isLoading && !PovLoading.Contains(player) && !PovLoadFailed.Contains(player) &&
        _pov.ProviderId() == ModGUID && !_pov.Suspended() && _povMutationDepth == 0;

    private static bool IsUnsupportedPathOfValheimanCell(string cell) =>
        cell.StartsWith("tool.", StringComparison.Ordinal) || cell.StartsWith("key.", StringComparison.Ordinal) ||
        cell.StartsWith("purse.", StringComparison.Ordinal);

    private static bool AllowPathOfValheimanCell(Player __0, int __1, ref bool __result)
    {
        if (_pov == null || __0 != Player.m_localPlayer || _pov.ProviderId() != ModGUID ||
            !(__1 >= 16 && __1 <= 31 || __1 >= 35 && __1 <= 42)) return true;
        __result = false;
        return false;
    }

    // Closed/suspended cells reject insertion but must not trap their existing item.
    private static bool CanWithdrawPathOfValheimanQuiver(Player player, Inventory inventory, ItemData item, SlotDefinition slot) =>
        slot.PovCell == "quiver" && _pov != null && _pov.ProviderId() == ModGUID &&
        player == Player.m_localPlayer && !player.m_isLoading && !IsPathOfValheimanMutationPending(player) &&
        inventory == player.GetInventory() && ContainsExactItemReference(inventory, item) &&
        TryGetSlotGridPos(inventory, slot, out Vector2i pos) && item.m_gridPos == pos && inventory.GetItemAt(pos.x, pos.y) == item;

    private static bool HasPathOfValheimanCoinBenefits(Player player) =>
        _pov != null && player != null && player == Player.m_localPlayer &&
        _pov.ProviderId() == ModGUID && !_pov.Suspended();

    private static bool IsPathOfValheimanCoin(ItemData item) =>
        item?.m_shared != null && _pov != null && _pov.Accepts("purse.0", item);

    private static bool KeepPathOfValheimanCoinsOnDeath(Player player) =>
        HasPathOfValheimanCoinBenefits(player) && _pov!.KeptOnDeath(player, "purse.0");

    private static void ApplyPathOfValheimanCoinWeight(Inventory __instance, ref float ___m_totalWeight)
    {
        Player player = Player.m_localPlayer;
        if (!HasPathOfValheimanCoinBenefits(player) || player.GetInventory() != __instance ||
            !_pov!.Weightless(player, "purse.0")) return;
        foreach (ItemData item in __instance.GetAllItems())
            if (IsPathOfValheimanCoin(item)) ___m_totalWeight -= item.GetWeight();
        ___m_totalWeight = Mathf.Max(0, ___m_totalWeight);
    }

    private static void RefreshPathOfValheimanCoinWeight(Player player)
    {
        Inventory inventory = player.GetInventory();
        bool weightless = HasPathOfValheimanCoinBenefits(player) && _pov!.Weightless(player, "purse.0");
        if (_povCoinWeightInventory == inventory && _povCoinsWeightless == weightless) return;
        _povCoinWeightInventory = inventory;
        _povCoinsWeightless = weightless;
        // No purse cell exists in the unlock stamp. Recalculate on node changes
        // even when none of the ordinary inventory items moved.
        inventory.Changed();
    }

    private static ItemData? ReadPathOfValheimanItem(Player player, string cell)
    {
        if (player == null || player != Player.m_localPlayer || PovLoading.Contains(player) || PovLoadFailed.Contains(player) ||
            !PovSlots.TryGetValue(cell, out SlotDefinition? slot)) return null;
        Inventory inventory = player.GetInventory();
        return TryGetSlotGridPos(inventory, slot, out Vector2i pos) ? inventory.GetItemAt(pos.x, pos.y) : null;
    }

    private static bool TryPutPathOfValheimanItem(Player player, string cell, ItemData item)
    {
        if (!PovCanMutate(player) || item == null || !PovSlots.TryGetValue(cell, out SlotDefinition? slot) ||
            !_pov!.Open(player, cell) || !slot.Accepts(item)) return false;
        Inventory inventory = player.GetInventory();
        if (!IsSpecialSlotUnlocked(player, inventory, slot)) return false;
        if (!ContainsExactItemReference(inventory, item) || !TryGetSlotGridPos(inventory, slot, out Vector2i target)) return false;
        if (IsPathOfValheimanTrinketSlot(slot)) return TryEquipPathOfValheimanTrinket(player, inventory, item, slot);
        if (inventory.GetItemAt(target.x, target.y) == item && cell != "quiver") return true;
        // Empty destinations only for API calls. UI inventory moves retain their
        // existing stack/swap transaction; a rejected API call changes nothing.
        if (inventory.GetItemAt(target.x, target.y) is ItemData occupant && occupant != item) return false;
        Vector2i oldPosition = item.m_gridPos;
        var snapshots = CaptureSlotEquipItemSnapshots(player, inventory, item, slot, target);
        var equipment = CaptureHumanoidEquipmentSnapshot(player);
        ItemData? oldQuiver = cell == "quiver" ? _pov.Equipped(player, cell) : null;
        _povMutationDepth++;
        BeginSlotAutoEquipSuppression();
        bool routing = InventorySafety.RoutingEquipToDedicatedSlot;
        InventorySafety.RoutingEquipToDedicatedSlot = true;
        try
        {
            if (cell == "quiver")
            {
                if (_pov.Equipped(player, cell) != item && !_pov.Equip(player, cell, item))
                    throw new InvalidOperationException("Quiver equipment callback rejected the move");
                // The API can enqueue an equipment action. Do not publish a cell
                // until the actual quiver callback has committed the equipment.
                if (_pov.Equipped(player, cell) != item)
                {
                    if (player.IsEquipActionQueued(item)) return true;
                    throw new InvalidOperationException("Quiver action did not equip or queue the item");
                }
            }
            else if (cell.StartsWith("utility", StringComparison.Ordinal))
            {
                UnequipInventorySlotsItem(player, item);
            }
            ClearItemSlot(item);
            item.m_gridPos = target;
        }
        catch (Exception error)
        {
            try { RestoreSlotEquipMutationSnapshots(player, inventory, snapshots, equipment); }
            catch (Exception restoreError) { Log.LogWarning("PoV move equipment recovery failed: " + restoreError.GetBaseException().Message); }
            finally { item.m_gridPos = oldPosition; }
            if (cell == "quiver")
            {
                player.RemoveEquipAction(item);
                try
                {
                    _pov.RemoveQuiver(player);
                    if (oldQuiver != null) _pov.Equip(player, cell, oldQuiver);
                }
                catch (Exception restoreError) { Log.LogWarning("PoV quiver callback recovery failed: " + restoreError.GetBaseException().Message); }
            }
            Log.LogWarning("PoV cell move failed; original item position retained: " + error.GetBaseException().Message);
            return false;
        }
        finally { InventorySafety.RoutingEquipToDedicatedSlot = routing; CompleteSlotAutoEquipSuppression(); _povMutationDepth--; }
        // Once committed, notification failures cannot report "no change".
        NotifyPathOfValheimanCommit(inventory);
        return true;
    }

    private static bool TakeOutPathOfValheimanItem(Player player, string cell)
    {
        if (!PovCanMutate(player)) return false;
        ItemData? item = ReadPathOfValheimanItem(player, cell);
        if (item == null) return true;
        Inventory inventory = player.GetInventory();
        if (!TryFindFreeRegularCell(player, inventory, out Vector2i destination)) return false;
        _povMutationDepth++;
        try { item.m_gridPos = destination; }
        finally { _povMutationDepth--; }
        NotifyPathOfValheimanCommit(inventory);
        return true;
    }

    private static void NotifyPathOfValheimanCommit(Inventory inventory)
    {
        try { inventory.Changed(); NotifyPathOfValheimanStorageChanged(); }
        catch (Exception error) { Log.LogWarning("PoV cell committed; refresh failed: " + error.GetBaseException().Message); }
    }

    private static void NotifyPathOfValheimanStorageChanged()
    {
        if (_pov == null || _povNotificationActive || _povMutationDepth > 0) return;
        _povNotificationActive = true;
        try { _pov.Changed(""); InvalidateSlotDefinitionCaches(); }
        finally { _povNotificationActive = false; }
    }

    internal static int FindPathOfValheimanUtilityUnequip(List<CodeInstruction> instructions)
    {
        MethodInfo original = AccessTools.Method(typeof(Humanoid), nameof(Humanoid.UnequipItem), new[] { typeof(ItemData), typeof(bool) });
        int match = -1;
        for (int i = 1; i < instructions.Count; i++)
        {
            // Only the utility hand-off suppresses effects and immediately calls Put.
            // Trinket replacement (effects=true) must still remove its predecessor.
            if (!instructions[i].Calls(original) || instructions[i - 1].opcode != OpCodes.Ldc_I4_0) continue;
            bool put = false;
            for (int j = i + 1; j < Math.Min(i + 12, instructions.Count); j++)
            {
                if (instructions[j].operand is MethodInfo method && method.Name == "Put" &&
                    method.DeclaringType?.FullName == "PassiveTree.Gear.UtilitySlots") { put = true; break; }
                if (instructions[j].opcode.FlowControl is FlowControl.Branch or FlowControl.Cond_Branch or FlowControl.Return) break;
            }
            if (!put) continue;
            if (match >= 0) throw new InvalidOperationException("PoV utility hand-off is ambiguous");
            match = i;
        }
        if (match < 0) throw new InvalidOperationException("PoV utility hand-off contract changed");
        return match;
    }

    private static IEnumerable<CodeInstruction> DeferPathOfValheimanUtilityUnequip(IEnumerable<CodeInstruction> source)
    {
        var instructions = source.ToList();
        int index = FindPathOfValheimanUtilityUnequip(instructions);
        instructions[index].opcode = OpCodes.Call;
        instructions[index].operand = AccessTools.Method(typeof(InventorySlotsPlugin), nameof(PathOfValheimanUtilityUnequip));
        return instructions;
    }

    private static void PathOfValheimanUtilityUnequip(Humanoid humanoid, ItemData item, bool effects)
    {
        if (_pov != null && humanoid == Player.m_localPlayer && _pov.ProviderId() == ModGUID) return;
        humanoid.UnequipItem(item, effects);
    }

    private static void BeginPathOfValheimanLoad(Player player)
    {
        if (_pov == null) return;
        _povCoinWeightInventory = null;
        PovLoadFailed.Remove(player);
        _povTrinketsReadyPlayer = null;
        PovLoading.Add(player);
    }
    private static void EndPathOfValheimanLoad(Player player, Exception? error)
    {
        // A native Load failure before our conversion leaves raw coordinates.
        // Do not normalize or save that partial inventory on subsequent updates.
        if (error != null && PovLoading.Contains(player)) PovLoadFailed.Add(player);
        PovLoading.Remove(player);
    }
    private static bool IsPathOfValheimanMutationPending(Player player) =>
        _pov != null && (_povMutationDepth > 0 || PovLoading.Contains(player) || PovLoadFailed.Contains(player));

    private static bool CompletePathOfValheimanLoad(Player player)
    {
        if (_pov == null) return false;
        Inventory inventory = player.GetInventory();
        try
        {
            var oldSlots = new Dictionary<Vector2i, string>();
            if (!player.m_customData.ContainsKey(PovNativeRowsKey) && player.m_customData.TryGetValue(PovLayoutKey, out string layout))
            {
                string[] ids = layout.Split('\n');
                for (int i = 0; i < ids.Length; i++) oldSlots[new Vector2i(i % InventoryWidth, GetFixedRegularRows() + i / InventoryWidth)] = ids[i];
            }
            else if (player.m_customData.TryGetValue(PovNativeRowsKey, out string raw))
            {
                if (!int.TryParse(raw, out int rows) || rows < BaseRows || rows > GetFixedRegularRows() || inventory.GetWidth() != InventoryWidth)
                    throw new InvalidOperationException("Invalid original PoV row boundary");
                foreach (var cell in _pov.Cells)
                    oldSlots[new Vector2i(cell.Ordinal % InventoryWidth, rows + cell.Ordinal / InventoryWidth)] =
                        PovSlots.TryGetValue(cell.Id, out SlotDefinition? slot) ? slot.Id : "pov." + cell.Id;
            }
            if (oldSlots.Count > 0) ReprojectPathOfValheimanLayout(inventory, oldSlots);
            // Clear old custom-equipment identity for the two roles now owned by PoV.
            foreach (SlotDefinition slot in PovSlots.Values)
                if (slot.Kind == SlotKind.Quick && TryGetSlotGridPos(inventory, slot, out Vector2i pos))
                {
                    ItemData? item = inventory.GetItemAt(pos.x, pos.y);
                    if (item != null && IsInventorySlotsCustomEquipped(item))
                    { item.m_equipped = false; ClearItemSlot(item); }
                }
            player.m_customData.Remove(PovNativeRowsKey);
            SavePathOfValheimanLayout(player);
            PovLoading.Remove(player);
            _povUnlockStamp = 0;
            // A positional backup from a different layout can be stale even when
            // all live items now fit in regular rows. Do not replay its copies.
            return oldSlots.Count > 0;
        }
        catch (Exception error)
        {
            PovLoadFailed.Add(player);
            Log.LogError("PoV layout could not be converted; inventory normalization paused, original item coordinates retained. " + error.Message);
            throw;
        }
    }

    private static void ReprojectPathOfValheimanLayout(Inventory inventory, Dictionary<Vector2i, string> previous)
    {
        var plan = new Dictionary<ItemData, Vector2i>();
        var occupied = new HashSet<Vector2i>();
        var recover = new List<ItemData>();
        foreach (ItemData item in inventory.m_inventory)
        {
            Vector2i target = item.m_gridPos;
            if (previous.TryGetValue(target, out string oldId))
            {
                if (!TryGetSlotById(oldId, out SlotDefinition? slot) || !TryGetSlotGridPos(inventory, slot, out target))
                { recover.Add(item); continue; }
            }
            else if (target.y >= GetFixedRegularRows()) { recover.Add(item); continue; }
            if (target.x < 0 || target.x >= InventoryWidth || target.y < 0 || !occupied.Add(target))
            { recover.Add(item); continue; }
            plan.Add(item, target);
        }
        foreach (ItemData item in recover)
        {
            bool found = false;
            for (int y = 0; y < GetFixedRegularRows() && !found; y++)
                for (int x = 0; x < InventoryWidth; x++)
                {
                    Vector2i pos = new(x, y);
                    if (!occupied.Add(pos)) continue;
                    plan.Add(item, pos); found = true; break;
                }
            if (!found) throw new InvalidOperationException("Not enough recovery space for the previous slot layout");
        }
        foreach (var pair in plan) pair.Key.m_gridPos = pair.Value;
    }

    private static void SavePathOfValheimanLayout(Player player)
    {
        if (_pov != null && !PovLoadFailed.Contains(player))
            player.m_customData[PovLayoutKey] = string.Join("\n", SlotDefinitions.Select(s => s.Id));
    }

    private static List<SlotDefinition>? CapturePathOfValheimanLayout() =>
        _pov != null && Player.m_localPlayer != null && !Player.m_localPlayer.m_isLoading && SlotDefinitions.Count > 0
            ? new List<SlotDefinition>(SlotDefinitions) : null;

    private static void RestorePathOfValheimanLayoutAfterRebuild(List<SlotDefinition>? previous)
    {
        Player? player = Player.m_localPlayer;
        if (previous == null || player == null) return;
        var layout = new Dictionary<Vector2i, string>();
        for (int i = 0; i < previous.Count; i++)
            layout.Add(new Vector2i(i % InventoryWidth, GetFixedRegularRows() + i / InventoryWidth), previous[i].Id);
        try
        {
            ReprojectPathOfValheimanLayout(player.GetInventory(), layout);
            SavePathOfValheimanLayout(player);
        }
        catch (Exception error)
        {
            // No coordinates were changed: keep the prior definitions until there
            // is enough room. A live YAML edit must not silently strand items.
            SlotDefinitions.Clear();
            SlotDefinitions.AddRange(previous);
            PovSlots.Clear();
            foreach (SlotDefinition slot in previous) if (slot.PovCell != null) PovSlots.Add(slot.PovCell, slot);
            Log.LogWarning("PoV slot layout change deferred; previous layout retained: " + error.Message);
        }
    }

    private static void PollPathOfValheimanStorage(Player player)
    {
        if (_pov == null || Time.unscaledTime < _povNextPoll) return;
        _povNextPoll = Time.unscaledTime + 0.25f;
        if (ProjectPathOfValheimanTrinkets(player, player.GetInventory()))
            NotifyPathOfValheimanCommit(player.GetInventory());
        ItemData? quiver = _pov.QuiverItem(player);
        if (!ReferenceEquals(quiver, _povLastQuiver))
        {
            _povLastQuiver = quiver;
            RequestLocalInventoryState(InventoryStateEnsureReason.EquipmentChanged, InventoryStateAuditLevel.FullIntegrity);
        }
        if (_povWasEnabled != _pov.Enabled())
        {
            _povWasEnabled = _pov.Enabled();
            RebuildSlotDefinitions();
            CompletePathOfValheimanLoad(player);
            InvalidateCustomEquipmentProjectionCache();
            NotifyPathOfValheimanCommit(player.GetInventory());
            RequestLocalInventoryState(InventoryStateEnsureReason.EquipmentChanged, InventoryStateAuditLevel.FullIntegrity);
        }
        int stamp = _pov.ExtraRows();
        RefreshPathOfValheimanCoinWeight(player);
        foreach (var pair in PovSlots) unchecked { stamp = stamp * 31 + (_pov.Open(player, pair.Key) ? 1 : 0); }
        if (stamp == _povUnlockStamp) return;
        _povUnlockStamp = stamp;
        InvalidateInventoryPlacementCaches();
        InvalidateSlotDefinitionCaches();
        RequestLocalInventoryState(InventoryStateEnsureReason.EquipmentChanged, InventoryStateAuditLevel.SlotLight);
    }
}
