using System;
using System.Reflection;
#if INVENTORY_SLOTS
using InventorySlots;
using Plugin = InventorySlots.InventorySlotsPlugin;
#else
using InventoryActions;
using Plugin = InventoryActions.InventoryActionsPlugin;
#endif
using Handoff =
#if INVENTORY_SLOTS
    InventorySlots.NativeContainerHandoff;
#else
    InventoryActions.NativeContainerHandoff;
#endif

internal static class Program
{
    private static int _checks;
    private static Container Chest = null!;
    private static Player Player => global::Player.m_localPlayer;
    private static ZDO Data => Chest.View.Data;

    private static void Check(bool condition, string message)
    {
        _checks++;
        if (!condition) throw new Exception(message);
    }

    private static void Reset()
    {
        Handoff.ClearSession();
        ZNet.instance = new ZNet();
        ZNet.Session = 42;
        UnityEngine.Time.realtimeSinceStartup = 0;
        global::Player.m_localPlayer = new Player();
        Plugin.UsesVanillaContainerProtocol = true;
        Chest = new Container(1);
    }

    private static void Begin()
    {
        Check(Handoff.TryBegin(Chest, Player), "Request should begin");
        Check(Chest.View.RequestOwner == 77 && Chest.View.RequestPlayer == 123, "Request must capture owner/player");
    }

    private static void Reply(bool granted = true, long sender = 77)
    {
        Check(!Handoff.BeforeNativeStackResponse(Chest, sender, granted), "Adapter response must not fall through");
    }

    private static void GrantOwnership()
    {
        Data.Owner = 42;
        Data.OwnerRevision = 9;
    }

    private static void Expect(NativeContainerHandoffResult expected, string message) =>
        Check(Handoff.Poll(Chest) == expected, message);

    private static void Main()
    {
        Reset();
        Begin();
        Reply();
        Expect(NativeContainerHandoffResult.Pending, "Response cannot prove data/ownership");
        Check(Chest.LoadCount == 0, "Never load remotely owned inventory");
        Data.SerializedItems = 83;
        Data.DataRevision = 35;
        GrantOwnership();
        Expect(NativeContainerHandoffResult.Granted, "Fresh transferred snapshot grants");
        Check(Chest.Inventory!.Items == 83 && Chest.LoadCount == 1, "Load latest inventory before grant");
        Check(!Handoff.IsRequestBlocked(Chest) && Handoff.CanBegin, "Granted state retires");
        Handoff.Cancel(Chest);
        Check(!Handoff.IsRequestBlocked(Chest), "Cancel after grant cannot create a new fence");
        Expect(NativeContainerHandoffResult.Failed, "Grant may be consumed only once");

        Reset();
        Begin();
        GrantOwnership();
        Expect(NativeContainerHandoffResult.Pending, "Owner before response must wait");
        Reply();
        Expect(NativeContainerHandoffResult.Granted, "Unchanged data revision remains valid");

        Reset();
        Begin();
        Reply(false);
        Expect(NativeContainerHandoffResult.Failed, "Denied response fails");
        Check(!Handoff.IsRequestBlocked(Chest), "Drained denial releases chest");

        Reset();
        Begin();
        Reply(sender: 88);
        GrantOwnership();
        Expect(NativeContainerHandoffResult.Pending, "Unrelated sender cannot approve request");
        Reply();
        Expect(NativeContainerHandoffResult.Granted, "Expected responder still accepted");

        Reset();
        Begin();
        Handoff.Cancel(Chest);
        Check(Handoff.IsRequestBlocked(Chest), "Cancellation must keep late response fence");
        Check(!Handoff.TryBegin(Chest, Player), "Cannot reuse untagged request before reply");
        Check(!Handoff.BeforeNativeStackAll(Chest, true), "Fence also blocks native StackAll");
        Reply();
        Check(!Handoff.IsRequestBlocked(Chest), "Late response drains cancellation fence");
        Begin();
        Expect(NativeContainerHandoffResult.Pending, "New request cannot inherit old approval");

        Reset();
        Begin();
        UnityEngine.Time.realtimeSinceStartup = 6;
        Expect(NativeContainerHandoffResult.Failed, "Timeout releases pending action");
        Check(Handoff.IsRequestBlocked(Chest), "Timeout must not erase response fence");
        Check(Handoff.CanBegin, "Timeout allows another chest");
        UnityEngine.Time.realtimeSinceStartup = 100000;
        Check(Handoff.IsRequestBlocked(Chest), "Elapsed time cannot identify stale untagged response");
        Reply(false);
        Check(!Handoff.IsRequestBlocked(Chest), "Late denied response drains fence");

        Reset();
        Begin();
        Reply();
        UnityEngine.Time.realtimeSinceStartup = 6;
        Expect(NativeContainerHandoffResult.Failed, "Owner propagation timeout fails");
        Check(!Handoff.IsRequestBlocked(Chest), "Already drained response needs no fence");

        Reset();
        Begin();
        GrantOwnership();
        Data.OwnerRevision = 11;
        Reply();
        Expect(NativeContainerHandoffResult.Failed, "Returning ownership is a different grant");
        Check(Chest.LoadCount == 0, "Rejected ownership cannot load or mutate inventory");

        Reset();
        Begin();
        Data.Owner = 99;
        Data.OwnerRevision = 9;
        Expect(NativeContainerHandoffResult.Failed, "Concurrent owner reassignment fails closed");
        Reply();
        Check(!Handoff.IsRequestBlocked(Chest), "Abandoned generation still consumes late response");

        Reset();
        Begin();
        GrantOwnership();
        Data.DataRevision = 29;
        Reply();
        Expect(NativeContainerHandoffResult.Failed, "Regressed snapshot cannot grant");

        Reset();
        Begin();
        Data.DataRevision = 31;
        Reply();
        Expect(NativeContainerHandoffResult.Pending, "New data without ownership is insufficient");

        Reset();
        Begin();
        Chest.View.Data = new ZDO { m_uid = Data.m_uid, Owner = 42, OwnerRevision = 9 };
        Reply();
        Expect(NativeContainerHandoffResult.Failed, "Recreated ZDO cannot adopt prior grant");

        Reset();
        Begin();
        GrantOwnership();
        Chest.InUse = true;
        Reply();
        Expect(NativeContainerHandoffResult.Pending, "Respect Load refusal while in-use");
        Check(Chest.LoadCount == 0 && Chest.Inventory!.Items == 0, "No forced in-use bypass");
        Chest.InUse = false;
        Expect(NativeContainerHandoffResult.Granted, "Load may complete after in-use ends");

        Reset();
        Begin();
        GrantOwnership();
        Reply();
        Chest.DuringLoad = () => Data.Owner = 99;
        Expect(NativeContainerHandoffResult.Failed, "Ownership changes during load invalidate grant");

        Reset();
        Begin();
        GrantOwnership();
        Reply();
        Chest.DuringLoad = () => Data.DataRevision++;
        Expect(NativeContainerHandoffResult.Failed, "Changing snapshot during load invalidates grant");

        Reset();
        Begin();
        GrantOwnership();
        Reply();
        Chest.FailLoad = true;
        Expect(NativeContainerHandoffResult.Pending, "Malformed load never grants");
        Expect(NativeContainerHandoffResult.Pending, "lastRevision written before exception cannot fake complete load");
        UnityEngine.Time.realtimeSinceStartup = 6;
        Expect(NativeContainerHandoffResult.Failed, "Failed load eventually retires");

        Reset();
        Begin();
        GrantOwnership();
        Reply();
        Chest.Inventory = null;
        Expect(NativeContainerHandoffResult.Failed, "Missing inventory cannot grant");

        Reset();
        Begin();
        global::Player.m_localPlayer = new Player();
        Expect(NativeContainerHandoffResult.Failed, "Changing player cancels operation");
        Reply();

        Reset();
        Begin();
        Check(!Handoff.TryBegin(new Container(2), Player), "Adapter requests are serialized");
        Check(!Handoff.BeforeNativeStackAll(new Container(2), true), "Native request cannot overlap adapter");
        Handoff.BeforeContainerDestroyed(Chest);
        Check(Handoff.IsRequestBlocked(Chest), "Destruction releases references but retains late response fence");
        Chest.View.Valid = false;
        Reply();
        Check(Handoff.CanBegin, "Destroyed-view late reply drains its weakly associated fence");

        Reset();
        Check(Handoff.BeforeNativeStackAll(Chest, false), "Skipped original requires no extra suppression");
        Check(!Handoff.IsRequestBlocked(Chest), "Never track request skipped by another mod prefix");
        Check(Handoff.BeforeNativeStackAll(Chest, true), "Ordinary StackAll may proceed");
        Check(!Handoff.CanBegin && !Handoff.TryBegin(new Container(2), Player), "Wait for ordinary in-flight response");
        Check(Handoff.BeforeNativeStackResponse(Chest, 77, true), "Normal response retains vanilla behavior");
        Check(Handoff.CanBegin, "Ordinary response releases adapter");
        Begin();

        Reset();
        Check(Handoff.BeforeNativeStackAll(Chest, true), "Ordinary timeout setup");
        UnityEngine.Time.realtimeSinceStartup = 6;
        Check(Handoff.CanBegin, "Expired ordinary request releases active operation");
        Check(!Handoff.BeforeNativeStackResponse(Chest, 77, true), "Late ordinary response cannot mutate during later operation");
        Check(!Handoff.IsRequestBlocked(Chest), "Expired native reply drains its fence");

        Reset();
        Plugin.UsesVanillaContainerProtocol = false;
        Check(Handoff.BeforeNativeStackAll(Chest, true), "Modded-server vanilla behavior unchanged");
        Check(!Handoff.IsRequestBlocked(Chest), "Do not track native originals outside compatibility mode");

        Reset();
        Begin();
        Handoff.Cancel();
        Plugin.UsesVanillaContainerProtocol = false;
        Check(Handoff.IsRequestBlocked(Chest), "Changing compatibility mode cannot drop late-response fence");
        Reply();

        Reset();
        Begin();
        Handoff.Cancel();
        Handoff.ClearSession();
        Check(!Handoff.IsRequestBlocked(Chest), "Shutdown clears session-owned fences");
        Begin();
        ZNet.instance = new ZNet();
        Check(!Handoff.IsRequestBlocked(Chest), "New network with same UID still clears old session");
        Begin();
        ZNet.Session = 43;
        Check(!Handoff.IsRequestBlocked(Chest), "New session UID clears pending operation");

        Reset();
        Begin();
        Handoff.Cancel();
        ZNet existingNetwork = ZNet.instance;
        long existingSession = ZNet.Session;
        ZNet.instance.ShutdownWithoutSave(suspending: true);
        // No Harmony installation is simulated; invoke the source postfix itself.
        typeof(Handoff).GetNestedType("StopAllPatch", BindingFlags.NonPublic)!
            .GetMethod("Postfix", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, null);
        Check(ReferenceEquals(existingNetwork, ZNet.instance) && existingSession == ZNet.Session,
            "Lifecycle test must retain network and session identity");
        Check(!Handoff.IsRequestBlocked(Chest), "StopAll clears fences even without identity change");

        Reset();
        Data.Owner = 0;
        Check(!Handoff.TryBegin(Chest, Player), "No request to zero owner");
        Data.Owner = 42;
        Check(!Handoff.TryBegin(Chest, Player), "Already-owned chest needs no handoff");
        Check(Handoff.RefreshInventory(Chest), "Local refresh is available without a request");
        Data.Owner = 77;
        Check(!Handoff.RefreshInventory(Chest), "Remote inventory cannot be refreshed for mutation");
        Data.OwnerRevision = ushort.MaxValue;
        Check(!Handoff.TryBegin(Chest, Player), "Wrapped owner generation cannot be proven");
        Check(Chest.View.Requests == 0, "Rejected starts never dispatch an RPC");

        Console.WriteLine($"{_checks} native container handoff checks passed ({typeof(Plugin).Namespace}).");
    }
}
