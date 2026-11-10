using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using ItemData = ItemDrop.ItemData;

namespace InventorySlots;

public sealed partial class InventorySlotsPlugin
{
    private static Func<ItemData, bool>? _povNativeQuiver;
    private static Func<Humanoid, ItemData>? _povNativeQuiverItem;
    private static Func<ItemData, string>? _povQuiverPrefab;
    private static IDictionary? _povExternalQuivers;
    private static bool _povQuiverRollback;
    private static bool _povQuiverSwapFailed;

    private sealed class PovQuiverSwap
    {
        internal Player Player = null!;
        internal Inventory Inventory = null!;
        internal SlotEquipItemSnapshot Incoming = null!, Outgoing = null!;
        internal bool Routing, Completed, Recovered;
    }

    private static void InstallPathOfValheimanQuiverSwap(Harmony harmony)
    {
        if (_pov == null) return;
        var native = _pov.Bind<Func<ItemData, bool>>("PassiveTree.Quivers", "Is");
        var equipped = _pov.Bind<Func<Humanoid, ItemData>>("PassiveTree.QuiverSlot", "Get");
        var prefab = _pov.Bind<Func<ItemData, string>>("PassiveTree.Gear.EquipmentApi", "Prefab");
        FieldInfo registry = AccessTools.DeclaredField(_pov.Assembly.GetType("PassiveTree.Gear.EquipmentApi", true), "Quivers");
        if (registry == null || !registry.IsStatic || !registry.IsInitOnly || registry.GetValue(null) is not IDictionary external)
            throw new MissingFieldException("PoV quiver registration table changed");
        MethodInfo equip = RequirePathOfValheimanMethod(_pov.Assembly.GetType("PassiveTree.QuiverSlot+Equip", true)!,
            "Prefix", typeof(bool), typeof(Humanoid), typeof(ItemData), typeof(bool).MakeByRefType());
        harmony.Patch(equip,
            prefix: new HarmonyMethod(typeof(InventorySlotsPlugin), nameof(BeginPathOfValheimanQuiverSwap)),
            postfix: new HarmonyMethod(typeof(InventorySlotsPlugin), nameof(EndPathOfValheimanQuiverSwap)),
            finalizer: new HarmonyMethod(typeof(InventorySlotsPlugin), nameof(FinalizePathOfValheimanQuiverSwap)));
        _povNativeQuiver = native; _povNativeQuiverItem = equipped; _povQuiverPrefab = prefab; _povExternalQuivers = external;
    }

    private static bool IsNativePathOfValheimanQuiver(ItemData item) =>
        _povNativeQuiver != null && _povNativeQuiver(item) && !_povExternalQuivers!.Contains(_povQuiverPrefab!(item));

    private static bool CanSwapPathOfValheimanQuiver(Player player, Inventory inventory, ItemData incoming, ItemData outgoing, SlotDefinition slot) =>
        !_povQuiverSwapFailed && _pov != null && slot.PovCell == "quiver" && IsNativePathOfValheimanQuiver(incoming) && IsNativePathOfValheimanQuiver(outgoing) &&
        ReferenceEquals(_povNativeQuiverItem!(player), outgoing) && ReferenceEquals(_pov.Equipped(player, "quiver"), outgoing) &&
        slot.Accepts(incoming) &&
        IsUsableRegularCell(inventory, player, incoming.m_gridPos) &&
        ReferenceEquals(inventory.GetItemAt(incoming.m_gridPos.x, incoming.m_gridPos.y), incoming) &&
        ContainsExactItemReference(inventory, outgoing) && !player.IsEquipActionQueued(outgoing) &&
        _pov.Open(player, "quiver") && IsSpecialSlotUnlocked(player, inventory, slot);

    // Wrap PoV's actual native equip prefix, not Humanoid's competing prefix list.
    // This is reached at completion of a queued right-click or API equip as well.
    private static bool BeginPathOfValheimanQuiverSwap(Humanoid __0, ItemData __1, ref bool __2,
        ref bool __result, out PovQuiverSwap? __state)
    {
        __state = null;
        if (_povQuiverRollback || _pov == null || __0 is not Player player || player != Player.m_localPlayer ||
            !_pov.Enabled() || _pov.ProviderId() != ModGUID || __1 == null || !IsNativePathOfValheimanQuiver(__1) ||
            !PovSlots.TryGetValue("quiver", out SlotDefinition? slot)) return true;
        Inventory inventory = player.GetInventory();
        ItemData? occupant = ReadPathOfValheimanItem(player, "quiver");
        if (occupant == null || ReferenceEquals(occupant, __1)) return true;
        if (player.m_isLoading || PovLoading.Contains(player) || PovLoadFailed.Contains(player) || _pov.Suspended() ||
            !CanSwapPathOfValheimanQuiver(player, inventory, __1, occupant, slot))
        { __2 = __result = false; return false; }
        __state = new PovQuiverSwap
        {
            Player = player, Inventory = inventory, Incoming = new SlotEquipItemSnapshot(__1, true),
            Outgoing = new SlotEquipItemSnapshot(occupant, true), Routing = InventorySafety.RoutingEquipToDedicatedSlot
        };
        _povMutationDepth++;
        BeginSlotAutoEquipSuppression();
        InventorySafety.RoutingEquipToDedicatedSlot = true;
        return true;
    }

    private static void EndPathOfValheimanQuiverSwap(ref bool __2, ref bool __result, PovQuiverSwap? __state)
    {
        if (__state == null) return;
        bool committed = !__result && __2 && ReferenceEquals(_povNativeQuiverItem!(__state.Player), __state.Incoming.Item) &&
            ReferenceEquals(__state.Inventory.GetItemAt(__state.Incoming.GridPos.x, __state.Incoming.GridPos.y), __state.Incoming.Item) &&
            ReferenceEquals(__state.Inventory.GetItemAt(__state.Outgoing.GridPos.x, __state.Outgoing.GridPos.y), __state.Outgoing.Item);
        if (committed)
        {
            ClearItemSlot(__state.Outgoing.Item);
            ClearItemSlot(__state.Incoming.Item);
            __state.Outgoing.Item.m_gridPos = __state.Incoming.GridPos;
            __state.Incoming.Item.m_gridPos = __state.Outgoing.GridPos;
        }
        else { __2 = false; __result = false; }
        CompletePathOfValheimanQuiverSwap(__state, committed);
    }

    private static Exception? FinalizePathOfValheimanQuiverSwap(Exception? __exception, PovQuiverSwap? __state, ref bool __2, ref bool __result)
    {
        if (__state is { Completed: false }) CompletePathOfValheimanQuiverSwap(__state, committed: false);
        if (__exception != null && __state is { Recovered: true })
        {
            __2 = __result = false;
            Log.LogWarning("PoV quiver replacement failed and was restored: " + __exception.GetBaseException().Message);
            return null;
        }
        return __exception;
    }

    private static void CompletePathOfValheimanQuiverSwap(PovQuiverSwap swap, bool committed)
    {
        if (swap.Completed) return;
        swap.Completed = true;
        try
        {
            if (!committed)
            {
                _povQuiverRollback = true;
                bool recovered = false;
                try
                {
                    // Native TakeOut API can queue unequipping; these calls run
                    // the native hooks immediately, before restoring metadata.
                    ItemData current = _povNativeQuiverItem!(swap.Player);
                    if (current != null && !ReferenceEquals(current, swap.Outgoing.Item)) swap.Player.UnequipItem(current, true);
                    if (!ReferenceEquals(_povNativeQuiverItem(swap.Player), swap.Outgoing.Item)) swap.Player.EquipItem(swap.Outgoing.Item, true);
                    recovered = ReferenceEquals(_povNativeQuiverItem(swap.Player), swap.Outgoing.Item);
                    swap.Recovered = recovered;
                }
                catch (Exception error) { Log.LogWarning("PoV quiver recovery failed: " + error.GetBaseException().Message); }
                finally
                {
                    foreach (var snapshot in new[] { swap.Incoming, swap.Outgoing })
                    {
                        snapshot.Item.m_gridPos = snapshot.GridPos;
                        snapshot.Item.m_equipped = snapshot.Equipped;
                        snapshot.Item.m_customData = new Dictionary<string, string>(snapshot.CustomData);
                    }
                    _povQuiverRollback = false;
                }
                if (!recovered)
                {
                    _povQuiverSwapFailed = true;
                    ItemData current = _povNativeQuiverItem!(swap.Player);
                    foreach (ItemData item in new[] { swap.Incoming.Item, swap.Outgoing.Item })
                    {
                        item.m_equipped = ReferenceEquals(current, item);
                        if (item.m_equipped) item.m_customData["pt.quiver"] = "1";
                        else item.m_customData.Remove("pt.quiver");
                    }
                    Log.LogWarning("PoV quiver replacement disabled for this session: native equipment recovery was rejected. Both inventory items were retained.");
                }
            }
        }
        finally
        {
            InventorySafety.RoutingEquipToDedicatedSlot = swap.Routing;
            CompleteSlotAutoEquipSuppression();
            _povMutationDepth--;
        }
        if (committed) NotifyPathOfValheimanCommit(swap.Inventory);
    }
}
