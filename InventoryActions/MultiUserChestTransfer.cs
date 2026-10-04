using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BepInEx.Bootstrap;
using HarmonyLib;
using UnityEngine;
using ItemData = ItemDrop.ItemData;

namespace InventoryActions;

public sealed partial class InventoryActionsPlugin
{
    private static MultiUserChestTransferApi? _mucTransferApi;
    private static MucTransferSession? _mucTransfer;
    private static MucTransferPending? _mucTransferPending;
    private static bool _mucTransferDispatching;

    // Only this adapter uses these hooks. It observes MUC's transport and never
    // takes ownership, refunds, releases MUC slots, or suppresses its responses.
    private sealed class MultiUserChestTransferApi
    {
        internal readonly Type RequestType, DepositRequestType;
        internal readonly MethodInfo Remove, Deposit, AddPackage, ApplyResponse, ApplyDepositResponse, GetBlock, AnyBlocked, FindChanges;
        internal readonly ConstructorInfo DepositConstructor;
        internal readonly PropertyInfo RequestId, Source, Target, ResponseId, Success, Amount;
        internal readonly FieldInfo From, To, DragAmount, ResponseItem, HasSwitched,
            DepositTo, DepositItem, AllowSwitch, RefundItem, RefundPosition;
        internal readonly object Changes;

        internal MultiUserChestTransferApi(Assembly assembly)
        {
            Type Type(string name) => assembly.GetType("MultiUserChest." + name, true)!;
            MethodInfo Method(Type type, string name, Type result, bool isStatic, params Type[] args) =>
                type.GetMethod(name, BindingFlags.Public | (isStatic ? BindingFlags.Static : BindingFlags.Instance),
                    null, args, null) is { } method && !method.ContainsGenericParameters && method.ReturnType == result
                    && method.GetParameters().Select(parameter => parameter.ParameterType).SequenceEqual(args)
                    ? method : throw new MissingMethodException(type.FullName, name);
            PropertyInfo Property(Type type, string name, Type value) =>
                type.GetProperty(name, BindingFlags.Public | BindingFlags.Instance) is { } property &&
                property.PropertyType == value && property.GetIndexParameters().Length == 0 &&
                property.GetGetMethod() is { IsStatic: false }
                    ? property : throw new MissingMemberException(type.FullName, name);
            FieldInfo Field(Type type, string name, Type value) =>
                type.GetField(name, BindingFlags.Public | BindingFlags.Instance) is { } field &&
                field.FieldType == value ? field : throw new MissingFieldException(type.FullName, name);

            RequestType = Type("RequestChestRemove");
            DepositRequestType = Type("RequestChestAdd");
            Type requestContract = Type("IRequest"), responseContract = Type("IResponse");
            Type response = Type("RequestChestRemoveResponse"), preview = Type("InventoryPreview"), block = Type("InventoryBlock");
            Type depositResponse = Type("RequestChestAddResponse");
            void RequireContract(Type concrete, Type contract)
            {
                if (!concrete.IsClass || concrete.IsAbstract || !contract.IsInterface || !contract.IsAssignableFrom(concrete))
                    throw new TypeLoadException($"MUC {concrete.FullName} must implement {contract.FullName} as a concrete class");
            }
            RequireContract(RequestType, requestContract);
            RequireContract(DepositRequestType, requestContract);
            RequireContract(response, responseContract);
            RequireContract(depositResponse, responseContract);
            Remove = Method(Type("ContainerHandler"), "RemoveItemFromChest", RequestType, true, typeof(Container), typeof(ItemData),
                typeof(Inventory), typeof(Vector2i), typeof(ZDOID), typeof(int), typeof(ItemData));
            Deposit = Method(Type("ContainerHandler"), "AddItemToChest", DepositRequestType, true, typeof(Container), typeof(ItemData),
                typeof(Inventory), typeof(Vector2i), typeof(ZDOID), typeof(int));
            Type[] depositArguments = { typeof(Vector2i), typeof(int), typeof(ItemData), typeof(Inventory), typeof(Inventory) };
            DepositConstructor = DepositRequestType.GetConstructor(depositArguments)
                ?? throw new MissingMethodException("MUC RequestChestAdd constructor");
            if (!DepositConstructor.GetParameters().Select(parameter => parameter.ParameterType).SequenceEqual(depositArguments))
                throw new MissingMethodException("MUC RequestChestAdd constructor parameters");
            AddPackage = Method(preview, "AddPackage", typeof(void), true, requestContract);
            ApplyResponse = Method(Type("InventoryHandler"), "RPC_RequestItemRemoveResponse", typeof(void), true, typeof(Inventory), response);
            ApplyDepositResponse = Method(Type("InventoryHandler"), "RPC_RequestItemAddResponse", typeof(void), true, typeof(Inventory), depositResponse);
            RequestId = Property(requestContract, "RequestID", typeof(int));
            Source = Property(requestContract, "SourceInventory", typeof(Inventory));
            Target = Property(requestContract, "TargetInventory", typeof(Inventory));
            From = Field(RequestType, "fromPos", typeof(Vector2i));
            To = Field(RequestType, "toPos", typeof(Vector2i));
            DragAmount = Field(RequestType, "dragAmount", typeof(int));
            ResponseId = Property(responseContract, "SourceID", typeof(int));
            Success = Property(responseContract, "Success", typeof(bool));
            Amount = Property(responseContract, "Amount", typeof(int));
            ResponseItem = Field(response, "responseItem", typeof(ItemData));
            HasSwitched = Field(response, "hasSwitched", typeof(bool));
            DepositTo = Field(DepositRequestType, "toPos", typeof(Vector2i));
            DepositItem = Field(DepositRequestType, "dragItem", typeof(ItemData));
            AllowSwitch = Field(DepositRequestType, "allowSwitch", typeof(bool));
            RefundItem = Field(depositResponse, "switchItem", typeof(ItemData));
            RefundPosition = Field(depositResponse, "inventoryPos", typeof(Vector2i));
            GetBlock = Method(block, "Get", block, true, typeof(Inventory));
            AnyBlocked = Method(block, "IsAnySlotBlocked", typeof(bool), false);
            Changes = preview.GetField("PackageChanges", BindingFlags.Public | BindingFlags.Static)?.GetValue(null)
                ?? throw new MissingFieldException("MUC InventoryPreview.PackageChanges");
            Type list = typeof(List<>).MakeGenericType(Type("IRequest"));
            FindChanges = Method(Changes.GetType(), "TryGetValue", typeof(bool), false, typeof(Inventory), list.MakeByRefType());
        }

        internal bool HasPendingChanges(Inventory inventory)
        {
            object?[] args = { inventory, null };
            return ((bool)FindChanges.Invoke(Changes, args)! && ((IList)args[1]!).Count != 0) ||
                   (bool)AnyBlocked.Invoke(GetBlock.Invoke(null, new object[] { inventory }), null)!;
        }
    }

    private sealed class MucTransferSession
    {
        internal Player Player = null!;
        internal Inventory Inventory = null!;
        internal Container Anchor = null!;
        internal List<Container> Containers = null!;
        internal AreaContainerActionKind Action;
        internal readonly HashSet<ItemData> MovedSources = new();
        internal int Index, Moved;
    }

    private sealed class MucTransferPending
    {
        internal readonly MultiUserChestRequestGate Gate = new();
        internal MucTransferSession Session = null!;
        internal Container Container = null!;
        internal Inventory SourceInventory = null!, DestinationInventory = null!;
        internal ItemData Source = null!, OriginalSource = null!;
        internal ItemData? Target;
        internal Vector2i From, To;
        internal int Requested, Returned, Before, Applied, Refund;
        internal bool ValidResponse, TargetStillEligible, SeedEmpty, EmptyBeforeResponse, NoSwitchPrepared;
        internal bool Deposit => Session.Action == AreaContainerActionKind.QuickStack;
        internal float CompletedAt;
    }

    private void InitializeMultiUserChestTransfer()
    {
        if (IsDedicatedServer || !Chainloader.PluginInfos.TryGetValue(ExternalMultiUserChestGuid, out var plugin) ||
            plugin.Instance == null) return;
        // Bind the required API instead of rejecting an otherwise compatible version.
        // Signatures cannot prove unchanged transfer semantics; keep runtime guards.
        MultiUserChestTransferApi? api = null;
        try
        {
            api = new MultiUserChestTransferApi(plugin.Instance.GetType().Assembly);
            _harmony.Patch(api.AddPackage, postfix: new HarmonyMethod(typeof(InventoryActionsPlugin), nameof(MucTransferPackageAdded)));
            _harmony.Patch(api.DepositConstructor,
                postfix: new HarmonyMethod(typeof(InventoryActionsPlugin), nameof(MucDepositRequestCreated)));
            _harmony.Patch(api.ApplyResponse,
                prefix: new HarmonyMethod(typeof(InventoryActionsPlugin), nameof(MucTransferResponseStarting)),
                finalizer: new HarmonyMethod(typeof(InventoryActionsPlugin), nameof(MucTransferResponseFinished)));
            _harmony.Patch(api.ApplyDepositResponse,
                prefix: new HarmonyMethod(typeof(InventoryActionsPlugin), nameof(MucDepositResponseStarting)),
                finalizer: new HarmonyMethod(typeof(InventoryActionsPlugin), nameof(MucDepositResponseFinished)));
            _mucTransferApi = api;
            Log.LogInfo($"MultiUserChest {plugin.Metadata.Version} area quick stack and favorite restock enabled (API checks passed; including configured empty favorites; one request at a time).");
        }
        catch (Exception error)
        {
            if (api != null)
            {
                _harmony.Unpatch(api.AddPackage, AccessTools.Method(typeof(InventoryActionsPlugin), nameof(MucTransferPackageAdded)));
                _harmony.Unpatch(api.ApplyResponse, AccessTools.Method(typeof(InventoryActionsPlugin), nameof(MucTransferResponseStarting)));
                _harmony.Unpatch(api.ApplyResponse, AccessTools.Method(typeof(InventoryActionsPlugin), nameof(MucTransferResponseFinished)));
                _harmony.Unpatch(api.DepositConstructor, AccessTools.Method(typeof(InventoryActionsPlugin), nameof(MucDepositRequestCreated)));
                _harmony.Unpatch(api.ApplyDepositResponse, AccessTools.Method(typeof(InventoryActionsPlugin), nameof(MucDepositResponseStarting)));
                _harmony.Unpatch(api.ApplyDepositResponse, AccessTools.Method(typeof(InventoryActionsPlugin), nameof(MucDepositResponseFinished)));
            }
            Log.LogWarning($"MultiUserChest {plugin.Metadata.Version} area transfers disabled: required API or hooks unavailable. {error.GetBaseException().Message}");
        }
    }

    private static bool CanUseMucTransferContainer(Player player, Container container, Container anchor, AreaContainerActionKind action)
    {
        if (_mucTransferApi == null || player == null || player.m_isLoading ||
            !IsAreaContainerEligible(container) || !IsAreaContainerEligible(anchor) ||
            container.GetType() != typeof(Container) || anchor.GetType() != typeof(Container) ||
            container.m_piece == null || anchor.m_piece == null ||
            !IsExternalMultiUserChestTransferContainer(container) || !IsExternalMultiUserChestTransferContainer(anchor) ||
            !container.m_nview.HasOwner() || !anchor.m_nview.HasOwner() ||
            container.m_nview.GetZDO().GetBool("MUC_Ignore", false) || anchor.m_nview.GetZDO().GetBool("MUC_Ignore", false) ||
            !HasContainerPlayerAccess(player, container) || !HasContainerPlayerAccess(player, anchor)) return false;
        if ((player.transform.position - anchor.transform.position).sqrMagnitude >
            AreaOwnershipMaximumAnchorDistance * AreaOwnershipMaximumAnchorDistance) return false;
        float range = GetAreaContainerRange(action);
        return container == anchor || range > 0 &&
            (container.transform.position - anchor.transform.position).sqrMagnitude <= range * range;
    }

    private static bool StartMucTransfer(Player player, Inventory inventory, Container anchor, AreaContainerActionKind action)
    {
        if (_mucTransfer != null || _mucTransferPending != null || _areaContainerTransfer != null ||
            !CanUseMucTransferContainer(player, anchor, anchor, action)) return false;
        var containers = Runtime.KnownContainers.Where(c => c != null && c != anchor &&
                CanUseMucTransferContainer(player, c, anchor, action)).Distinct()
            .OrderBy(c => (c.transform.position - anchor.transform.position).sqrMagnitude).ToList();
        containers.Insert(0, anchor);
        _mucTransfer = new MucTransferSession { Player = player, Inventory = inventory, Anchor = anchor, Containers = containers, Action = action };
        return true;
    }

    private static void CancelMucTransfer()
    {
        _mucTransferPending?.Gate.Stop();
        _mucTransfer = null;
        // An in-flight MUC response may still deliver items. Retain its gate so
        // repeated hotkeys cannot overlap it, and leave MUC cleanup to MUC.
    }

    [HarmonyPatch(typeof(ZNet), "StopAll", typeof(bool))]
    private static class MucTransferNetworkStoppedPatch
    {
        private static void Postfix()
        {
            // Peers/RPCs have been shut down. A new network session must not
            // inherit an unresolved request from the disconnected world.
            CancelMucTransfer();
            _mucTransferPending = null;
            _mucUnconfirmedSeeds.Clear();
            _mucSeedInventory = null;
        }
    }

    private static void UpdateMucTransfer(Player? player)
    {
        if (_mucTransferApi == null) return;
        try { TickMucTransfer(player); }
        catch (Exception error)
        {
            CancelMucTransfer();
            Log.LogWarning($"MultiUserChest area transfer stopped; submitted requests remain with MUC: {error.Message}");
        }
    }

    private static void TickMucTransfer(Player? player)
    {
        PruneMucSeedMemoryProtection(player);
        var session = _mucTransfer;
        if (session != null && (player == null || session.Player != player || player.m_isLoading || player.IsDead() ||
            player.IsTeleporting() || ((Character)player).InCutscene() || InventoryGui.IsVisible() ||
            session.Inventory != GetPlayerInventory(player) || !CanUseMucTransferContainer(player, session.Anchor, session.Anchor, session.Action)))
        {
            CancelMucTransfer();
            session = null;
        }
        var pending = _mucTransferPending;
        if (pending != null)
        {
            if (pending.Gate.CheckTimeout(Time.unscaledTime))
            {
                CancelMucTransfer();
                Log.LogWarning("MultiUserChest area transfer timed out. No retry or refund; waiting for the original response before another transfer.");
            }
            if (!pending.Gate.Completed) return;
            if (session != pending.Session || !pending.Gate.ContinueBatch)
            {
                _mucTransferPending = null;
                if (session != null) { RecordMucTransfer(session, pending); FinishMucTransfer(session); }
                return;
            }
            // A remove response can precede the chest ZDO update. MUC retains
            // that source preview until Inventory.Changed sees the new contents.
            Inventory chestInventory = pending.Deposit ? pending.DestinationInventory : pending.SourceInventory;
            if (_mucTransferApi!.HasPendingChanges(chestInventory))
            {
                if (Time.unscaledTime - pending.CompletedAt < 10f) return;
                _mucTransferPending = null;
                RecordMucTransfer(session, pending);
                FinishMucTransfer(session);
                return;
            }
            RecordMucTransfer(session, pending);
            _mucTransferPending = null;
        }
        if (session == null) return;
        if (_mucTransferApi!.HasPendingChanges(session.Inventory)) { FinishMucTransfer(session); return; }
        while (session.Index < session.Containers.Count)
        {
            Container container = session.Containers[session.Index];
            if (CanUseMucTransferContainer(session.Player, container, session.Anchor, session.Action) &&
                !_mucTransferApi.HasPendingChanges(container.m_inventory) &&
                (session.Action == AreaContainerActionKind.QuickStack
                    ? TrySendMucQuickStack(session, container) : TrySendMucTransfer(session, container))) return;
            session.Index++;
        }
        FinishMucTransfer(session);
    }

    private static bool TrySendMucTransfer(MucTransferSession session, Container container)
    {
        EnsureFavoritesLoaded(session.Player);
        RememberFavoriteSlotItems(session.Player);
        foreach (ItemData target in GetRestockTargets(session.Player, session.Inventory, RestockMode.AreaFavoriteRestock))
        {
            // Do not reinforce an unexpected positional-race delivery on the
            // next hotkey press. Manual replacement/unfavoriting releases it.
            if (ShouldDeferMucFavoriteMemory(session.Inventory, target.m_gridPos, target)) continue;
            for (int i = container.m_inventory.m_inventory.Count - 1; i >= 0; i--)
            {
                ItemData source = container.m_inventory.m_inventory[i];
                if (!CanRestockFromContainerItem(target, source)) continue;
                int amount = GetRestockTransferAmount(container.m_inventory, source,
                    GetRestockTargetStack(target) - target.m_stack, RestockMode.AreaFavoriteRestock);
                if (amount <= 0) continue;
                return SendMucTransfer(session, container, source, target, target.m_gridPos, amount);
            }
        }
        foreach (string key in _emptyFavoriteRestockKeys)
        foreach (ItemData source in container.m_inventory.GetAllItems())
        {
            if (source?.m_shared == null || source.m_stack <= 0 || source.m_shared.m_maxStackSize <= 1 ||
                !CanUseContainerActionStacking(source) ||
                RestockTargetLimitCore.ResolveConfiguredKey(Runtime.RestockTargetStackLimits, GetRestockTargetLookupTokens(source)) != key ||
                !TryFindEmptyFavoriteRestockCell(session.Player, session.Inventory, source, out Vector2i cell)) continue;
            int amount = GetRestockTransferAmount(container.m_inventory, source, GetRestockTargetStack(source), RestockMode.AreaFavoriteRestock);
            if (amount > 0) return SendMucTransfer(session, container, source, null, cell, amount, seedEmpty: true);
        }
        return false;
    }

    private static bool TrySendMucQuickStack(MucTransferSession session, Container container)
    {
        Inventory chest = container.m_inventory;
        var accepted = new HashSet<string>(chest.GetAllItems().Where(i => i?.m_shared != null)
            .Select(i => i.m_shared.m_name), StringComparer.OrdinalIgnoreCase);
        foreach (ItemData source in GetQuickStackCandidates(session.Player, session.Inventory))
        {
            if (!accepted.Contains(source.m_shared.m_name)) continue;
            ItemData? target = chest.GetAllItems().Where(i => CanStackIntoTargetForTopFirstMove(i, source))
                .OrderBy(i => i.m_gridPos.y).ThenBy(i => i.m_gridPos.x).FirstOrDefault();
            if (target != null)
                return SendMucTransfer(session, container, source, target, target.m_gridPos,
                    Math.Min(source.m_stack, target.m_shared.m_maxStackSize - target.m_stack));
            foreach (Vector2i cell in GetInventorySlotsTopFirst(chest))
                if (chest.GetItemAt(cell.x, cell.y) == null)
                    return SendMucTransfer(session, container, source, null, cell, source.m_stack);
        }
        return false;
    }

    private static bool SendMucTransfer(MucTransferSession session, Container container, ItemData source,
        ItemData? target, Vector2i to, int amount, bool seedEmpty = false)
    {
        bool deposit = session.Action == AreaContainerActionKind.QuickStack;
        var pending = new MucTransferPending
        {
            Session = session, Container = container,
            SourceInventory = deposit ? session.Inventory : container.m_inventory,
            DestinationInventory = deposit ? container.m_inventory : session.Inventory,
            Source = source.Clone(), OriginalSource = source, Target = target, From = source.m_gridPos,
            To = to, Requested = amount, SeedEmpty = seedEmpty
        };
        pending.Gate.Begin(Time.unscaledTime);
        _mucTransferPending = pending;
        _mucTransferDispatching = true;
        try
        {
            var api = _mucTransferApi!;
            // Withdraw never supplies a switch item. Deposit's constructor hook
            // disables swaps before MUC locks/removes any source items.
            object? result = deposit
                ? api.Deposit.Invoke(null, new object[] { container, source, session.Inventory, to, session.Player.GetZDOID(), amount })
                : api.Remove.Invoke(null, new object?[] { container, source, session.Inventory, to, session.Player.GetZDOID(), amount, null });
            if (result != null && !pending.Gate.Registered &&
                (deposit ? result.GetType() == api.DepositRequestType && api.DepositItem.GetValue(result) == null
                    : result.GetType() == api.RequestType && (int)api.DragAmount.GetValue(result)! == 0))
            {
                // Known explicit rejection before submission. Exceptions or a
                // missing callback are never treated as this no-send sentinel.
                _mucTransferPending = null;
                FinishMucTransfer(session);
            }
        }
        finally { _mucTransferDispatching = false; }
        return true;
    }

    private static void RecordMucTransfer(MucTransferSession session, MucTransferPending pending)
    {
        if (pending.Applied <= 0) return;
        if (pending.Deposit)
        {
            // Existing quick-stack feedback counts changed source stacks, not
            // item units. Splitting one source across requests counts it once.
            if (session.MovedSources.Add(pending.OriginalSource)) session.Moved++;
        }
        else session.Moved += pending.Applied;
    }

    private static void FinishMucTransfer(MucTransferSession session)
    {
        _mucTransfer = null;
        bool deposit = session.Action == AreaContainerActionKind.QuickStack;
        ShowContainerActionResult(session.Player, deposit ? "$inventoryactions_action_stack" : "$inventoryactions_action_take_stacks",
            deposit ? "Stack" : "Take stacks", session.Moved);
    }

    private static void MucDepositRequestCreated(object __instance, Vector2i __0, int __1, ItemData __2, Inventory __3, Inventory __4)
    {
        var api = _mucTransferApi;
        var pending = _mucTransferPending;
        if (!_mucTransferDispatching || api == null || pending == null || !pending.Deposit ||
            __0 != pending.To || __1 <= 0 || __1 > pending.Requested ||
            !ReferenceEquals(__2, pending.OriginalSource) || !ReferenceEquals(__3, pending.SourceInventory) ||
            !ReferenceEquals(__4, pending.DestinationInventory)) return;
        // Public instance field; readonly in the reviewed MUC implementations.
        // Failure must propagate BEFORE MUC blocks/removes/sends anything.
        api.AllowSwitch.SetValue(__instance, false);
        if ((bool)api.AllowSwitch.GetValue(__instance)!) throw new InvalidOperationException("MUC deposit could not disable item swapping");
        pending.NoSwitchPrepared = true;
    }

    private static void MucTransferPackageAdded(object __0)
    {
        var api = _mucTransferApi;
        var pending = _mucTransferPending;
        if (!_mucTransferDispatching || api == null || pending == null ||
            __0.GetType() != (pending.Deposit ? api.DepositRequestType : api.RequestType)) return;
        try
        {
            var sentItem = pending.Deposit ? api.DepositItem.GetValue(__0) as ItemData : null;
            int amount = pending.Deposit ? sentItem?.m_stack ?? 0 : (int)api.DragAmount.GetValue(__0)!;
            Vector2i from = pending.Deposit ? sentItem?.m_gridPos ?? default : (Vector2i)api.From.GetValue(__0)!;
            Vector2i to = (Vector2i)(pending.Deposit ? api.DepositTo : api.To).GetValue(__0)!;
            if (ReferenceEquals(api.Source.GetValue(__0), pending.SourceInventory) &&
                ReferenceEquals(api.Target.GetValue(__0), pending.DestinationInventory) && from == pending.From && to == pending.To &&
                (!pending.Deposit || pending.NoSwitchPrepared && !(bool)api.AllowSwitch.GetValue(__0)!) &&
                amount > 0 && amount <= pending.Requested &&
                pending.Gate.Register((int)api.RequestId.GetValue(__0)!))
                pending.Requested = amount;
        }
        catch (Exception error) { CancelMucTransfer(); Log.LogWarning($"MUC transfer request tracking failed: {error.Message}"); }
    }

    private static void MucTransferResponseStarting(Inventory __0, object __1, out MucTransferPending? __state)
    {
        __state = null;
        var api = _mucTransferApi;
        var pending = _mucTransferPending;
        if (api == null || pending == null || pending.Deposit || !ReferenceEquals(__0, pending.Session.Inventory)) return;
        try
        {
            if (!pending.Gate.BeginResponse((int)api.ResponseId.GetValue(__1)!)) return;
            __state = pending;
            pending.EmptyBeforeResponse = __0.GetItemAt(pending.To.x, pending.To.y) == null;
            pending.Before = pending.Target?.m_stack ?? 0;
            pending.Returned = (int)api.Amount.GetValue(__1)!;
            var item = api.ResponseItem.GetValue(__1) as ItemData;
            pending.ValidResponse = (bool)api.Success.GetValue(__1)! && !(bool)api.HasSwitched.GetValue(__1)! &&
                pending.Returned > 0 && pending.Returned <= pending.Requested && item != null &&
                ReferenceEquals(__0.GetItemAt(pending.To.x, pending.To.y), pending.Target) &&
                MatchesMucTransferredItem(pending.Source, item) &&
                (pending.SeedEmpty || CanRestockFromContainerItem(pending.Target!, item));
            pending.TargetStillEligible = _mucTransfer == pending.Session &&
                (pending.SeedEmpty ? IsMucEmptyTargetStillEligible(pending) :
                    ShouldRestockFavoriteItem(pending.Session.Player, __0, pending.Target!) &&
                    GetRestockTargetStack(pending.Target!) - pending.Before >= pending.Returned);
        }
        catch (Exception error) { CancelMucTransfer(); Log.LogWarning($"MUC restock response tracking failed: {error.Message}"); }
    }

    private static Exception? MucTransferResponseFinished(Exception? __exception, MucTransferPending? __state)
    {
        if (__state == null) return __exception;
        // Finalizer runs after MUC applies the response (or throws). Never hide
        // its exception or interpret a removed package/slot lock as completion.
        var pending = __state;
        ItemData? target = pending.Session.Inventory.GetItemAt(pending.To.x, pending.To.y);
        bool sameTarget = pending.SeedEmpty
            ? pending.EmptyBeforeResponse && target != null && MatchesMucTransferredItem(pending.Source, target)
            : ReferenceEquals(target, pending.Target);
        pending.Applied = pending.ValidResponse && sameTarget && target != null && __exception == null
            ? Math.Max(0, Math.Min(pending.Returned, target.m_stack - pending.Before)) : 0;
        if (pending.SeedEmpty && pending.EmptyBeforeResponse && target != null &&
            (!pending.ValidResponse || !sameTarget || __exception != null))
        {
            _mucSeedInventory = pending.Session.Inventory;
            _mucUnconfirmedSeeds[pending.To] = target;
        }
        pending.Gate.FinishResponse(__exception == null && pending.ValidResponse && pending.TargetStillEligible &&
            pending.Applied == pending.Requested);
        pending.CompletedAt = Time.unscaledTime;
        return __exception;
    }

    private static bool IsMucEmptyTargetStillEligible(MucTransferPending pending)
    {
        string? key = RestockTargetLimitCore.ResolveConfiguredKey(Runtime.RestockTargetStackLimits, GetRestockTargetLookupTokens(pending.Source));
        return key != null && _emptyFavoriteRestockKeys.Contains(key) && GetRestockTargetStack(pending.Source) >= pending.Returned &&
            TryFindEmptyFavoriteRestockCell(pending.Session.Player, pending.Session.Inventory, pending.Source, out Vector2i cell) && cell == pending.To;
    }

    private static bool MatchesMucTransferredItem(ItemData expected, ItemData actual) =>
        CanRestockFromContainerItem(expected, actual) &&
        string.Equals(GetFavoriteMemoryPrefab(expected), GetFavoriteMemoryPrefab(actual), StringComparison.Ordinal);

    private static bool ValidMucDepositAmounts(int sent, int accepted, int refund) =>
        sent > 0 && accepted >= 0 && accepted <= sent && refund == sent - accepted;

    private static void MucDepositResponseStarting(Inventory __0, object __1, out MucTransferPending? __state)
    {
        __state = null;
        var api = _mucTransferApi;
        var pending = _mucTransferPending;
        if (api == null || pending == null || !pending.Deposit || !ReferenceEquals(__0, pending.Session.Inventory)) return;
        try
        {
            if (!pending.Gate.BeginResponse((int)api.ResponseId.GetValue(__1)!)) return;
            __state = pending;
            pending.Returned = (int)api.Amount.GetValue(__1)!;
            var refund = api.RefundItem.GetValue(__1) as ItemData;
            pending.Refund = refund?.m_stack ?? 0;
            var remaining = __0.GetItemAt(pending.From.x, pending.From.y);
            pending.Before = remaining?.m_stack ?? 0;
            pending.ValidResponse = (Vector2i)api.RefundPosition.GetValue(__1)! == pending.From &&
                ValidMucDepositAmounts(pending.Requested, pending.Returned, pending.Refund) &&
                ((bool)api.Success.GetValue(__1)! == (pending.Returned > 0)) &&
                (refund == null || MatchesMucTransferredItem(pending.Source, refund)) &&
                (remaining == null || MatchesMucTransferredItem(pending.Source, remaining));
        }
        catch (Exception error) { CancelMucTransfer(); Log.LogWarning($"MUC deposit response tracking failed: {error.Message}"); }
    }

    private static Exception? MucDepositResponseFinished(Exception? __exception, MucTransferPending? __state)
    {
        if (__state == null) return __exception;
        var pending = __state;
        var remaining = pending.Session.Inventory.GetItemAt(pending.From.x, pending.From.y);
        bool refundApplied = pending.Refund == 0 || remaining != null && MatchesMucTransferredItem(pending.Source, remaining) &&
            remaining.m_stack - pending.Before == pending.Refund;
        pending.Applied = pending.ValidResponse && __exception == null ? pending.Returned : 0;
        pending.Gate.FinishResponse(__exception == null && pending.ValidResponse && refundApplied && pending.Applied == pending.Requested);
        pending.CompletedAt = Time.unscaledTime;
        return __exception;
    }

    // Keep the old remembered prefab if a positional MUC race delivered a
    // different item. Ordinary manual replacement/unfavoriting removes this
    // protection; it never restores or fabricates inventory contents.
    private static readonly Dictionary<Vector2i, ItemData> _mucUnconfirmedSeeds = new();
    private static Inventory? _mucSeedInventory;

    private static bool ShouldDeferMucFavoriteMemory(Inventory inventory, Vector2i cell, ItemData? item)
    {
        var pending = _mucTransferPending;
        if (pending?.SeedEmpty == true && !pending.Gate.Completed && ReferenceEquals(inventory, pending.Session.Inventory) && cell == pending.To)
            return true;
        if (!ReferenceEquals(inventory, _mucSeedInventory) || !_mucUnconfirmedSeeds.TryGetValue(cell, out var unexpected)) return false;
        if (ReferenceEquals(item, unexpected)) return true;
        _mucUnconfirmedSeeds.Remove(cell);
        return false;
    }

    private static void PruneMucSeedMemoryProtection(Player? player)
    {
        if (_mucUnconfirmedSeeds.Count == 0) return;
        Inventory? inventory = player == null ? null : GetPlayerInventory(player);
        if (!ReferenceEquals(inventory, _mucSeedInventory)) { _mucUnconfirmedSeeds.Clear(); _mucSeedInventory = null; return; }
        foreach (var entry in _mucUnconfirmedSeeds.ToArray())
            if (!ReferenceEquals(inventory!.GetItemAt(entry.Key.x, entry.Key.y), entry.Value) ||
                !IsEligibleFavoriteRestockCell(player!, inventory, entry.Key)) _mucUnconfirmedSeeds.Remove(entry.Key);
    }
}
