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
    private static ItemData? _povLastBeltLantern;

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
            if (bridge.UnhostedNativeCells.Count > 0)
                Log.LogWarning("PoV native equipment roles have no InventorySlots panel cells; original backpack behavior is retained: " +
                               string.Join(", ", bridge.UnhostedNativeCells));
            // Register before PoV's ObjectDB callback. Differently rolled items must
            // never become mergeable merely because both keys belong to PoV.
            var keys = bridge.Bind<Func<IEnumerable<string>>>("PassiveTree.InventorySlotsCompat", "OurKeys");
            PathOfValheimanStackMetadata.Register(keys());
            string? stackingWarning = PathOfValheimanStackCompatibility.Install(bridge.Assembly, ModGUID);
            if (stackingWarning != null)
                Log.LogWarning("PoV metadata comparison changed; native restrictions remain in addition to InventorySlots slot rules: " + stackingWarning);
            InventoryCompatibility.PathOfValheimanActions.Install(bridge.Assembly, ModGUID);
            Log.LogInfo("PoV storage provider registered; InventorySlots owns cells and discovery progression, PoV retains node requirements and effects.");
            return true;
        }
        catch (Exception error)
        {
            _pov?.Unregister();
            _pov = null;
            PovStoragePatches.UnpatchSelf();
            PathOfValheimanStackCompatibility.Dispose();
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
        if (_pov.HasBeltLantern)
        {
            var lantern = new SlotDefinition("pov.belt.lantern", "Belt Lantern", SlotKind.BuiltIn,
                item => item != null && _pov.Accepts("belt.lantern", item), povCell: "belt.lantern");
            SlotDefinitions.Add(lantern);
            PovSlots.Add("belt.lantern", lantern);
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
        !PathOfValheimanStorageBridge.SupportsCell(cell);

    private static bool AllowPathOfValheimanCell(Player __0, int __1, ref bool __result)
    {
        if (_pov == null || __0 != Player.m_localPlayer || _pov.ProviderId() != ModGUID ||
            !_pov.UnsupportedOrdinals.Contains(__1)) return true;
        __result = false;
        return false;
    }

    private static bool SkipUnhostedPathOfValheimanUtilityEquip(Humanoid __0, ItemData __1, ref bool __result)
    {
        if (_pov == null || !_pov.Enabled() || __0 != Player.m_localPlayer || _pov.ProviderId() != ModGUID)
            return true;
        foreach (string cell in _pov.UnhostedUtilityCells)
        {
            if (!_pov.Accepts(cell, __1)) continue;
            // Return "continue EquipItem" from PoV's prefix, leaving the original
            // item/mod/YAML route intact. This does not report a successful equip.
            __result = true;
            return false;
        }
        return true;
    }

    private static void RejectUnsupportedPathOfValheimanUtilityEquip(Humanoid __0, ItemData __1, ref bool __2, bool __result)
    {
        // PoV's prefix reports success even when its provider Put was rejected.
        // Correct the EquipItem result, while still skipping vanilla equipment
        // so a disabled extra utility cannot replace the currently worn belt.
        if (!__result && __2 && _pov != null && __0 == Player.m_localPlayer &&
            _pov.ProviderId() == ModGUID && !PovSlots.ContainsKey("utility") && _pov.Accepts("utility", __1))
            __2 = false;
    }

    // Closed/suspended cells reject insertion but must not trap their existing item.
    private static bool IsPathOfValheimanNativeEquipmentSlot(SlotDefinition? slot) =>
        slot?.PovCell is "quiver" or "belt.lantern";

    private static bool CanWithdrawPathOfValheimanNativeEquipment(Player player, Inventory inventory, ItemData item, SlotDefinition slot) =>
        IsPathOfValheimanNativeEquipmentSlot(slot) && _pov != null && _pov.ProviderId() == ModGUID &&
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
        bool nativeEquipment = IsPathOfValheimanNativeEquipmentSlot(slot);
        if (inventory.GetItemAt(target.x, target.y) == item && !nativeEquipment) return true;
        // Empty destinations only for API calls. UI inventory moves retain their
        // existing stack/swap transaction; a rejected API call changes nothing.
        if (inventory.GetItemAt(target.x, target.y) is ItemData occupant && occupant != item) return false;
        Vector2i oldPosition = item.m_gridPos;
        var snapshots = CaptureSlotEquipItemSnapshots(player, inventory, item, slot, target);
        var equipment = CaptureHumanoidEquipmentSnapshot(player);
        ItemData? oldEquipment = nativeEquipment ? _pov.NativeItem(player, cell) : null;
        _povMutationDepth++;
        BeginSlotAutoEquipSuppression();
        bool routing = InventorySafety.RoutingEquipToDedicatedSlot;
        InventorySafety.RoutingEquipToDedicatedSlot = true;
        try
        {
            if (nativeEquipment)
            {
                // A lantern held before the mastery was learned is already
                // "equipped" to PoV's generic API. Clear that hand assignment
                // first, otherwise Assign skips the belt's native equip hook.
                if (cell == "belt.lantern" && _pov.NativeItem(player, cell) != item && player.IsItemEquiped(item))
                    player.UnequipItem(item, true);
                if (_pov.NativeItem(player, cell) != item && !_pov.Equip(player, cell, item))
                    throw new InvalidOperationException(cell + " equipment callback rejected the move");
                // The API can enqueue an equipment action. Do not publish a cell
                // until the native callback has committed the equipment.
                if (_pov.NativeItem(player, cell) != item)
                {
                    if (player.IsEquipActionQueued(item)) return true;
                    throw new InvalidOperationException(cell + " action did not equip or queue the item");
                }
            }
            else if (cell.StartsWith("utility", StringComparison.Ordinal) || IsPathOfValheimanRingSlot(slot))
            {
                UnequipInventorySlotsItem(player, item);
            }
            ClearItemSlot(item);
            item.m_gridPos = target;
        }
        catch (Exception error)
        {
            // BeltSlot.Take changes equipped metadata. Remove any partially
            // committed native state before restoring the hand/item snapshot.
            if (cell == "belt.lantern")
            {
                player.RemoveEquipAction(item);
                try { if (_pov.NativeItem(player, cell) != oldEquipment) _pov.RemoveNativeItem(player, cell); }
                catch (Exception cleanupError) { Log.LogWarning("PoV lantern cleanup failed: " + cleanupError.GetBaseException().Message); }
            }
            try { RestoreSlotEquipMutationSnapshots(player, inventory, snapshots, equipment); }
            catch (Exception restoreError) { Log.LogWarning("PoV move equipment recovery failed: " + restoreError.GetBaseException().Message); }
            finally { item.m_gridPos = oldPosition; }
            if (nativeEquipment)
            {
                player.RemoveEquipAction(item);
                try
                {
                    if (cell != "belt.lantern") _pov.RemoveNativeItem(player, cell);
                    if (oldEquipment != null && _pov.NativeItem(player, cell) != oldEquipment) _pov.Equip(player, cell, oldEquipment);
                }
                catch (Exception restoreError) { Log.LogWarning("PoV native equipment recovery failed: " + restoreError.GetBaseException().Message); }
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
        try
        {
            _pov.Changed("");
            // Late quiver registrations can reclassify utility prefabs without a
            // definition rebuild. Retain their existing refresh-on-change policy,
            // while ordinary armor/custom-slot discovery keeps its cached result.
            foreach (SlotDefinition slot in SlotDefinitions)
                if (slot.PovCell is "belt" or "quiver" or "utility" or "utility.wisp" or "utility.wishbone")
                    InventoryDefinitions.EquipmentSlotUnlockCache.Remove(slot.Id);
            InvalidateSlotStateCaches();
        }
        finally { _povNotificationActive = false; }
    }

    internal static int FindPathOfValheimanUtilityUnequip(List<CodeInstruction> instructions, string handoff = "Put")
    {
        MethodInfo original = AccessTools.Method(typeof(Humanoid), nameof(Humanoid.UnequipItem), new[] { typeof(ItemData), typeof(bool) });
        int match = -1;
        for (int i = 1; i < instructions.Count; i++)
        {
            // Only the utility hand-off suppresses effects and immediately calls Put.
            // Trinket replacement (effects=true) must still remove its predecessor.
            if (!instructions[i].Calls(original) || instructions[i - 1].opcode != OpCodes.Ldc_I4_0) continue;
            bool put = false;
            int start = i + 1;
            // 5.1.1 Tick checks the six ring cells before PutRing. Match that
            // bounded loop and its exits; do not search across arbitrary branches.
            if (handoff == "PutRing" && i >= 6 && i + 37 < instructions.Count &&
                instructions[i - 6].operand is MethodInfo ringCheck && ringCheck.Name == "IsRingItem" &&
                ringCheck.DeclaringType?.FullName == "PassiveTree.Gear.State" &&
                instructions[i + 3].operand is FieldInfo rings && rings.Name == "rings" &&
                rings.DeclaringType?.FullName == "PassiveTree.Gear.UtilitySlots" &&
                instructions[i + 15].operand is MethodInfo at && at.Name == "At" &&
                at.DeclaringType?.FullName == "PassiveTree.Gear.Cells" &&
                instructions[i + 35].operand is MethodInfo putRing && putRing.Name == "PutRing" &&
                putRing.DeclaringType?.FullName == "PassiveTree.Gear.UtilitySlots" &&
                instructions[i + 1].opcode == OpCodes.Ldc_I4_0 &&
                instructions[i + 5].opcode == OpCodes.Ldc_I4_0 &&
                instructions[i + 10].opcode == OpCodes.Ldelem_I4 &&
                instructions[i + 18].opcode == OpCodes.Ldc_I4_1 &&
                instructions[i + 22].opcode == OpCodes.Ldc_I4_1 &&
                instructions[i + 23].opcode == OpCodes.Add &&
                instructions[i + 27].opcode == OpCodes.Ldlen &&
                instructions[i + 28].opcode == OpCodes.Conv_I4 &&
                instructions[i + 36].opcode == OpCodes.Pop &&
                BranchTo(i - 5, i + 37) && BranchTo(i + 7, i + 25) &&
                BranchTo(i + 17, i + 21) && BranchTo(i + 20, i + 30) &&
                BranchTo(i + 29, i + 8) && BranchTo(i + 31, i + 37))
                start = i + 32;
            // 5.1 Assign adds "if (!External && !IntoCell(p,s,item)) return false"
            // before Put. Accept that exact branch, not arbitrary intervening code.
            if (handoff == "Put" && i + 13 < instructions.Count &&
                instructions[i + 1].operand is MethodInfo external && external.Name == "get_External" &&
                external.DeclaringType?.FullName == "PassiveTree.Gear.Cells" &&
                instructions[i + 2].Branches(out Label? externalTarget) &&
                instructions[i + 2].opcode.FlowControl == FlowControl.Cond_Branch &&
                instructions[i + 3].opcode == OpCodes.Ldarg_0 &&
                instructions[i + 4].opcode == OpCodes.Ldarg_1 &&
                instructions[i + 5].opcode == OpCodes.Ldarg_2 &&
                instructions[i + 6].operand is MethodInfo into && into.Name == "IntoCell" &&
                into.DeclaringType?.FullName == "PassiveTree.Gear.State" &&
                instructions[i + 7].Branches(out Label? successTarget) &&
                (instructions[i + 2].opcode == OpCodes.Brtrue || instructions[i + 2].opcode == OpCodes.Brtrue_S) &&
                (instructions[i + 7].opcode == OpCodes.Brtrue || instructions[i + 7].opcode == OpCodes.Brtrue_S) &&
                externalTarget.HasValue && successTarget.HasValue &&
                instructions[i + 10].labels.Contains(externalTarget.Value) &&
                instructions[i + 10].labels.Contains(successTarget.Value) &&
                instructions[i + 8].opcode == OpCodes.Ldc_I4_0 && instructions[i + 9].opcode == OpCodes.Ret &&
                instructions[i + 10].opcode == OpCodes.Ldarg_0 &&
                instructions[i + 11].opcode == OpCodes.Ldarg_1 &&
                instructions[i + 12].opcode == OpCodes.Ldarg_2)
                start = i + 10;
            for (int j = start; j < Math.Min(start + 11, instructions.Count); j++)
            {
                if (instructions[j].operand is MethodInfo method && method.Name == handoff &&
                    method.DeclaringType?.FullName == "PassiveTree.Gear.UtilitySlots") { put = true; break; }
                if (instructions[j].opcode.FlowControl is FlowControl.Branch or FlowControl.Cond_Branch or FlowControl.Return) break;
            }
            if (!put) continue;
            if (match >= 0) throw new InvalidOperationException("PoV utility hand-off is ambiguous");
            match = i;
        }
        if (match < 0) throw new InvalidOperationException("PoV utility hand-off contract changed");
        return match;

        bool BranchTo(int from, int to) => instructions[from].Branches(out Label? target) &&
            target.HasValue && instructions[to].labels.Contains(target.Value);
    }

    private static IEnumerable<CodeInstruction> DeferPathOfValheimanUtilityUnequip(IEnumerable<CodeInstruction> source, MethodBase __originalMethod)
    {
        var instructions = source.ToList();
        int index = FindPathOfValheimanUtilityUnequip(instructions);
        instructions[index].opcode = OpCodes.Call;
        instructions[index].operand = AccessTools.Method(typeof(InventorySlotsPlugin), nameof(PathOfValheimanUtilityUnequip));
        // Ring routing was added in 5.1. Detect the checked native hand-off,
        // not runtime provider state: Harmony can rebuild this patch later.
        if (__originalMethod.Name == "Tick" &&
            __originalMethod.DeclaringType?.Assembly.GetType("Valheiman.Api.Equipment")?.GetMethod("IsRing") != null)
        {
            int ring = FindPathOfValheimanUtilityUnequip(instructions, "PutRing");
            instructions[ring].opcode = OpCodes.Call;
            instructions[ring].operand = instructions[index].operand;
        }
        return instructions;
    }

    private static void PathOfValheimanUtilityUnequip(Humanoid humanoid, ItemData item, bool effects)
    {
        if (_pov != null && humanoid == Player.m_localPlayer && _pov.ProviderId() == ModGUID)
        {
            Player player = Player.m_localPlayer;
            // 5.1.1 skips PutRing when the same ring already occupies a cell.
            // Only that committed ring can clear its duplicate native equipment
            // here; all new hand-offs still wait for a successful provider Put.
            if (item == null || !TryGetSlotAtGridPos(player.GetInventory(), item.m_gridPos, out SlotDefinition? slot) ||
                slot == null || !IsPathOfValheimanRingSlot(slot) || ReadPathOfValheimanItem(player, slot.PovCell!) != item)
                return;
            _povMutationDepth++;
            BeginSlotAutoEquipSuppression();
            try { humanoid.UnequipItem(item, effects); }
            finally { CompleteSlotAutoEquipSuppression(); _povMutationDepth--; }
            return;
        }
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
        ItemData? lantern = _pov.NativeItem(player, "belt.lantern");
        if (!ReferenceEquals(quiver, _povLastQuiver) || !ReferenceEquals(lantern, _povLastBeltLantern))
        {
            _povLastQuiver = quiver;
            _povLastBeltLantern = lantern;
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
