using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using HarmonyLib;

internal static class MucTransferChecks
{
    // Bind against an unmodified external DLL, then patch its real method
    // bodies. No publicized games, fake MUC API, Unity scene or network is used.
    internal static int Run(Type plugin, string mucDll)
    {
        const BindingFlags fields = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
        const BindingFlags statics = BindingFlags.Static | BindingFlags.NonPublic;
        var muc = Assembly.LoadFrom(mucDll);
        var apiType = plugin.GetNestedType("MultiUserChestTransferApi", BindingFlags.NonPublic)!;
        object api = Activator.CreateInstance(apiType, fields, null, new object[] { muc }, null)!;
        MethodInfo Method(string field) => (MethodInfo)apiType.GetField(field, fields)!.GetValue(api)!;
        var harmony = new Harmony("inventory-tests.muc-transfer");
        var add = Method("AddPackage");
        var apply = Method("ApplyResponse");
        var applyDeposit = Method("ApplyDepositResponse");
        var depositConstructor = (ConstructorInfo)apiType.GetField("DepositConstructor", fields)!.GetValue(api)!;
        int checks = 0;
        void Check(string label, bool value)
        {
            if (!value) throw new Exception(label);
            checks++;
            System.Console.WriteLine("PASS " + label);
        }
        try
        {
            Check("optional MUC API binds against original DLL", api != null);
            harmony.Patch(add, postfix: new HarmonyMethod(plugin.GetMethod("MucTransferPackageAdded", statics)));
            harmony.Patch(apply,
                prefix: new HarmonyMethod(plugin.GetMethod("MucTransferResponseStarting", statics)),
                finalizer: new HarmonyMethod(plugin.GetMethod("MucTransferResponseFinished", statics)));
            harmony.Patch(applyDeposit,
                prefix: new HarmonyMethod(plugin.GetMethod("MucDepositResponseStarting", statics)),
                finalizer: new HarmonyMethod(plugin.GetMethod("MucDepositResponseFinished", statics)));
            harmony.Patch(depositConstructor, postfix: new HarmonyMethod(plugin.GetMethod("MucDepositRequestCreated", statics)));
            Check("real MUC registration body accepts observer", Harmony.GetPatchInfo(add)!.Postfixes.Any(p => p.owner == harmony.Id));
            Check("real MUC response body accepts state/finalizer hooks", Harmony.GetPatchInfo(apply)!.Finalizers.Any(p => p.owner == harmony.Id));
            Check("remove uses public seven-argument request API", Method("Remove").IsPublic && Method("Remove").GetParameters().Length == 7);
            Check("deposit uses public six-argument request API", Method("Deposit").IsPublic && Method("Deposit").GetParameters().Length == 6);
            Check("real MUC deposit response accepts state/finalizer hooks", Harmony.GetPatchInfo(applyDeposit)!.Finalizers.Any(p => p.owner == harmony.Id));

            // Exercise registration correlation using the actual request class
            // and compiled plugin observer. Inventories are managed game data.
            Inventory source = new Inventory("source", null, 4, 4);
            Inventory target = new Inventory("target", null, 4, 4);
            Type requestType = muc.GetType("MultiUserChest.RequestChestRemove", true)!;
            Type sessionType = plugin.GetNestedType("MucTransferSession", BindingFlags.NonPublic)!;
            Type pendingType = plugin.GetNestedType("MucTransferPending", BindingFlags.NonPublic)!;
            object session = Activator.CreateInstance(sessionType, true)!;
            object pending = Activator.CreateInstance(pendingType, true)!;
            void Set(object obj, string name, object value) => obj.GetType().GetField(name, fields)!.SetValue(obj, value);
            Set(session, "Inventory", target);
            var actionType = sessionType.GetField("Action", fields)!.FieldType;
            Set(session, "Action", Enum.Parse(actionType, "Restock"));
            Set(pending, "Session", session);
            Set(pending, "SourceInventory", source);
            Set(pending, "DestinationInventory", target);
            Set(pending, "From", new Vector2i(1, 1));
            Set(pending, "To", new Vector2i(2, 2));
            Set(pending, "Requested", 10);
            object gate = pendingType.GetField("Gate", fields)!.GetValue(pending)!;
            gate.GetType().GetMethod("Begin", fields)!.Invoke(gate, new object[] { 0f });
            plugin.GetField("_mucTransferApi", statics)!.SetValue(null, api);
            plugin.GetField("_mucTransferPending", statics)!.SetValue(null, pending);
            plugin.GetField("_mucTransferDispatching", statics)!.SetValue(null, true);
            var observe = plugin.GetMethod("MucTransferPackageAdded", statics)!;
            object Request(Inventory destination, int amount) => Activator.CreateInstance(requestType,
                new object?[] { new Vector2i(1, 1), new Vector2i(2, 2), amount, null, source, destination })!;
            bool Registered() => (bool)gate.GetType().GetProperty("Registered", fields)!.GetValue(gate)!;
            observe.Invoke(null, new[] { Request(source, 10) });
            Check("unrelated target is not registered", !Registered());
            observe.Invoke(null, new[] { Request(target, 0) });
            Check("zero amount rejection is not registered", !Registered());
            object request = Request(target, 3);
            requestType.GetProperty("RequestID")!.SetValue(request, -17);
            observe.Invoke(null, new[] { request });
            Check("registered amount is MUC's final clamped amount", Registered() && (int)pendingType.GetField("Requested", fields)!.GetValue(pending)! == 3);
            Check("negative request ID correlates", (bool)gate.GetType().GetMethod("BeginResponse", fields)!.Invoke(gate, new object[] { -17 })!);

            // This exercises the original constructor through Harmony, including
            // the readonly instance-field write, before any locks/removal/RPC.
            var wood = new ItemDrop.ItemData
            {
                m_stack = 10, m_gridPos = new Vector2i(1, 1),
                m_shared = new ItemDrop.ItemData.SharedData { m_name = "$item_wood", m_maxStackSize = 50 }
            };
            FieldInfo allowSwitch = (FieldInfo)apiType.GetField("AllowSwitch", fields)!.GetValue(api)!;
            FieldInfo depositItem = (FieldInfo)apiType.GetField("DepositItem", fields)!.GetValue(api)!;
            object Deposit(Inventory destination, int amount) => depositConstructor.Invoke(new object[]
                { new Vector2i(2, 2), amount, wood, source, destination });
            Check("unrelated manual full-stack request retains MUC swap behavior", (bool)allowSwitch.GetValue(Deposit(target, 10))!);
            Set(session, "Action", Enum.Parse(actionType, "QuickStack"));
            Set(session, "Inventory", source);
            pending = Activator.CreateInstance(pendingType, true)!;
            Set(pending, "Session", session);
            Set(pending, "SourceInventory", source);
            Set(pending, "DestinationInventory", target);
            Set(pending, "OriginalSource", wood);
            Set(pending, "From", wood.m_gridPos);
            Set(pending, "To", new Vector2i(2, 2));
            Set(pending, "Requested", 10);
            gate = pendingType.GetField("Gate", fields)!.GetValue(pending)!;
            gate.GetType().GetMethod("Begin", fields)!.Invoke(gate, new object[] { 0f });
            plugin.GetField("_mucTransferPending", statics)!.SetValue(null, pending);
            Check("unrelated deposit destination retains swap behavior", (bool)allowSwitch.GetValue(Deposit(source, 10))!);
            request = Deposit(target, 10);
            Check("scoped automatic full-stack deposit disables readonly swap flag", !(bool)allowSwitch.GetValue(request)! && wood.m_stack == 10);
            request.GetType().GetProperty("RequestID")!.SetValue(request, 0);
            observe.Invoke(null, new[] { request });
            ((ItemDrop.ItemData)depositItem.GetValue(request)!).m_stack = 4;
            Check("registered deposit quantity survives owner mutation into refund", Registered() && (int)pendingType.GetField("Requested", fields)!.GetValue(pending)! == 10);
            Check("zero deposit request ID correlates", (bool)gate.GetType().GetMethod("BeginResponse", fields)!.Invoke(gate, new object[] { 0 })!);
            plugin.GetField("_mucTransferDispatching", statics)!.SetValue(null, false);
            Check("outside dispatch manual deposit still allows swaps", (bool)allowSwitch.GetValue(Deposit(target, 10))!);
            var balance = plugin.GetMethod("ValidMucDepositAmounts", statics)!;
            bool Balanced(int sent, int accepted, int refund) => (bool)balance.Invoke(null, new object[] { sent, accepted, refund })!;
            Check("full, partial and rejected deposits balance", Balanced(10, 10, 0) && Balanced(10, 6, 4) && Balanced(10, 0, 10));
            Check("missing refund, excess acceptance and overflow fail balance", !Balanced(10, 6, 0) && !Balanced(10, 11, 0) && !Balanced(int.MaxValue, -1, int.MinValue));

            // Pending empty-slot delivery and unexpected delivered item must not
            // replace the remembered prefab. A later manual replacement may.
            pending = Activator.CreateInstance(pendingType, true)!;
            Set(pending, "Session", session);
            Set(pending, "SeedEmpty", true);
            Set(pending, "To", new Vector2i(2, 2));
            plugin.GetField("_mucTransferPending", statics)!.SetValue(null, pending);
            var defer = plugin.GetMethod("ShouldDeferMucFavoriteMemory", statics)!;
            bool Deferred(Inventory inventory, ItemDrop.ItemData item) => (bool)defer.Invoke(null,
                new object[] { inventory, new Vector2i(2, 2), item })!;
            Check("pending empty-slot response defers memory only in its inventory", Deferred(source, wood) && !Deferred(target, wood));
            plugin.GetField("_mucTransferPending", statics)!.SetValue(null, null);
            plugin.GetField("_mucSeedInventory", statics)!.SetValue(null, source);
            var seeds = (IDictionary)plugin.GetField("_mucUnconfirmedSeeds", statics)!.GetValue(null)!;
            seeds[new Vector2i(2, 2)] = wood;
            Check("unexpected delivered item keeps original memory", Deferred(source, wood));
            Check("manual replacement releases memory protection", !Deferred(source, wood.Clone()) && seeds.Count == 0);
            plugin.GetField("_mucTransferPending", statics)!.SetValue(null, pending);
            plugin.GetNestedType("MucTransferNetworkStoppedPatch", BindingFlags.NonPublic)!
                .GetMethod("Postfix", statics)!.Invoke(null, null);
            Check("network teardown forgets old-session tracking", plugin.GetField("_mucTransferPending", statics)!.GetValue(null) == null);
        }
        finally
        {
            harmony.UnpatchSelf();
            plugin.GetField("_mucTransferApi", statics)!.SetValue(null, null);
            plugin.GetField("_mucTransferPending", statics)!.SetValue(null, null);
            plugin.GetField("_mucTransferDispatching", statics)!.SetValue(null, false);
            ((IDictionary)plugin.GetField("_mucUnconfirmedSeeds", statics)!.GetValue(null)!).Clear();
            plugin.GetField("_mucSeedInventory", statics)!.SetValue(null, null);
        }
        return checks;
    }
}
