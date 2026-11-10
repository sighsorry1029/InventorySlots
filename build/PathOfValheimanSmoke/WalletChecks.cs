using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BepInEx.Configuration;
using HarmonyLib;
using ItemData = ItemDrop.ItemData;

internal static partial class Program
{
    private static Harmony InstallNativeWalletSerialization(Assembly pov)
    {
        var native = new Harmony("treextr.pathofvalheiman");
        foreach (string type in new[] { "BigStackSave", "BigStackLoad", "CoinLoad", "CoinLoadFlag" })
            native.CreateClassProcessor(pov.GetType("PassiveTree.Gear.Wallet+" + type, true)!).Patch();
        return native;
    }

    private static bool WalletPrefab(ItemData item, ref string __result)
    {
        if (item?.m_shared?.m_name != "Coins") return true;
        __result = "Coins"; return false;
    }

    private static void CheckCoinWallet(Assembly pov, Type plugin, Player player, Inventory bag, ConfigEntry<bool> enabled, object bridge)
    {
        object? Call(string name, params object?[] args) => plugin.GetMethod(name, Statics)!.Invoke(null, args);
        Check((bool)bridge.GetType().GetField("HasWallet", Instances)!.GetValue(bridge)!, "wallet serializer and ownership contracts enabled");
        Type definition = plugin.Assembly.GetType("InventorySlots.SlotDefinition", true)!;
        Type kind = plugin.Assembly.GetType("InventorySlots.SlotKind", true)!;
        var definitions = (IList)plugin.GetProperty("SlotDefinitions", Statics)!.GetValue(null)!;
        var mapped = (IDictionary)plugin.GetField("PovSlots", Statics)!.GetValue(null)!;
        object walletSlot = Activator.CreateInstance(definition, new object?[] { "pov.purse", "Coins", Enum.Parse(kind, "Quick"),
            new Func<ItemData, bool>(i => i?.m_shared?.m_name == "Coins"), -1, "purse.0" })!;
        definitions.Add(walletSlot); mapped.Add("purse.0", walletSlot);
        var items = (List<ItemData>)AccessTools.Field(typeof(Inventory), "m_inventory").GetValue(bag);
        ItemData[] savedItems = items.ToArray();
        Type wallet = pov.GetType("PassiveTree.Gear.Wallet", true)!;
        FieldInfo gate = wallet.GetField("Gate", Statics)!, free = wallet.GetField("Weightless", Statics)!;
        object? oldGate = gate.GetValue(null), oldFree = free.GetValue(null);
        var fixture = new Harmony("smoke.pov.wallet");
        fixture.Patch(AccessTools.Method(pov.GetType("PassiveTree.Gear.EquipmentApi", true)!, "Prefab"), prefix: new HarmonyMethod(typeof(Program), nameof(WalletPrefab)));
        fixture.Patch(AccessTools.Method(pov.GetType("PassiveTree.Gear.State", true)!, "Prefab"), prefix: new HarmonyMethod(typeof(Program), nameof(WalletPrefab)));
        fixture.Patch(AccessTools.Method(typeof(Inventory), "UpdateTotalWeight"), postfix:
            new HarmonyMethod(wallet.GetNestedType("Weight", BindingFlags.NonPublic)!.GetMethod("Postfix", Statics)!));
        fixture.Patch(AccessTools.Method(plugin, "NotifyPathOfValheimanStorageChanged"), prefix: new HarmonyMethod(typeof(Program), nameof(NoProgress)));
        // Install actual production transfer patches on original game methods.
        foreach (string patch in new[] { "InventoryAddItemDataPatch", "InventoryAddItemXyPatch", "InventoryMoveItemToThisPatch", "WalletAutomaticWithdrawalPatch", "WalletInventoryLoadPatch" })
            fixture.CreateClassProcessor(plugin.Assembly.GetType("InventorySlots." + patch, true)!).Patch();
        bool wasEnabled = enabled.Value;
        ItemData Coin(int count) => new ItemData { m_stack = count, m_gridPos = new Vector2i(0, 0),
            m_shared = new ItemData.SharedData { m_name = "Coins", m_maxStackSize = 999, m_weight = 1 } };
        bool Deposit(ItemData source, int amount) => (bool)Call("DepositWallet", bag, source, amount)!;
        ItemData? Read() => (ItemData?)Call("ReadPathOfValheimanItem", player, "purse.0");
        bool CanDeposit(ItemData source, int amount)
        {
            object?[] arguments = { bag, source, amount, null, null };
            return (bool)Call("CanDepositWallet", arguments)!;
        }
        try
        {
            bool open = true;
            gate.SetValue(null, new Func<Player, bool>(_ => open));
            free.SetValue(null, new Func<Player, bool>(_ => true));
            enabled.Value = true; items.Clear();
            Call("InvalidateSlotDefinitionCaches");
            ItemData source = Coin(70000);
            Check(CanDeposit(source, 70000) && Deposit(source, 70000), "incoming 70000 coins fit and deposit");
            ItemData balance = Read()!;
            Check(balance.m_stack == 70000 && source.m_stack == 0 && items.Count == 1 && !ReferenceEquals(balance, source), "deposit commits exact quantity once");
            Check(!Deposit(balance, 1) && balance.m_stack == 70000, "self-deposit rejects without duplication");
            var ordinary = new ItemData { m_stack = 2, m_gridPos = new Vector2i(2, 0), m_shared = new ItemData.SharedData { m_name = "Wood", m_weight = 3 } };
            items.Add(ordinary);
            AccessTools.Method(typeof(Inventory), "UpdateTotalWeight").Invoke(bag, null);
            Check(Math.Abs(bag.GetTotalWeight() - 6f) < .001f, "native plus IS weight hooks exempt purse only once");
            ItemData incompatible = Coin(2); incompatible.m_customData["foreign.quality"] = "different";
            Check(!CanDeposit(incompatible, 2) && !Deposit(incompatible, 2) && incompatible.m_stack == 2 && balance.m_stack == 70000, "foreign metadata rejection leaves both stacks intact");
            Check(!Deposit(ordinary, 1) && ordinary.m_stack == 2, "wallet rejects non-coins");
            ItemData more = Coin(23456);
            Check(Deposit(more, more.m_stack) && Read() == balance && balance.m_stack == 93456, "second deposit preserves wallet reference");
            open = false;
            Check(!CanDeposit(Coin(3), 3) && !(bool)Call("IsQuickSlotUnlocked", player, walletSlot)! && Read() == balance,
                "node removal closes deposits but preserves readable occupied wallet");
            Check((int)Call("WalletWithdrawalAmount", bag, balance, 93456)! == 999, "withdrawal bound is normal coin stack");
            int before = balance.m_stack;
            bag.MoveItemToThis(bag, balance, 93456, 0, 0);
            Check(balance.m_stack == before - 999 && bag.GetItemAt(0, 0)?.m_stack == 999, "actual positional withdrawal leaves remainder");
            var chest = new Inventory("Chest", null, 1, 1);
            chest.MoveItemToThis(bag, balance);
            Check(chest.GetItemAt(0, 0)?.m_stack == 999 && balance.m_stack == before - 1998, "actual automatic chest withdrawal is bounded");
            before = balance.m_stack; chest.MoveItemToThis(bag, balance);
            Check(balance.m_stack == before && chest.GetItemAt(0, 0)?.m_stack == 999, "full chest leaves wallet intact");
            open = true;
            before = balance.m_stack;
            balance.m_stack = int.MaxValue;
            ItemData excess = Coin(1);
            Check(!CanDeposit(excess, 1) && !bag.AddItem(excess) && excess.m_stack == 1,
                "wallet total overflow cannot fall back into an empty ordinary cell");
            balance.m_stack = before;
            ItemData arrival = Coin(3);
            Check(bag.AddItem(arrival) && balance.m_stack == before + 3 && arrival.m_stack == 0,
                "actual automatic AddItem routes incoming coins to wallet");
            // Registered composition must be used even with different values.
            Type policy = plugin.Assembly.GetType("InventorySlots.StackMetadataPolicy", true)!;
            policy.GetMethods(Statics).Single(m => m.Name == "Register" && m.GetParameters().Length == 2).Invoke(null,
                new object[] { "smoke.wallet.age", new Func<string?, string?, string?>((a, b) => string.CompareOrdinal(a, b) < 0 ? a : b) });
            balance.m_customData["smoke.wallet.age"] = "20";
            ItemData earlier = Coin(1); earlier.m_customData["smoke.wallet.age"] = "10";
            Check(Deposit(earlier, 1) && balance.m_customData["smoke.wallet.age"] == "10", "wallet applies registered metadata composer");
            balance.m_customData.Clear();
            enabled.Value = false;
            Check(!CanDeposit(Coin(1), 1) && Read() == balance, "equipment disabled retains withdrawal item but refuses deposits");
            enabled.Value = true;
            CheckWalletSerialization(plugin, pov);
        }
        finally
        {
            fixture.UnpatchSelf();
            items.Clear(); items.AddRange(savedItems);
            definitions.Remove(walletSlot); mapped.Remove("purse.0");
            gate.SetValue(null, oldGate); free.SetValue(null, oldFree); enabled.Value = wasEnabled;
            Call("InvalidateSlotDefinitionCaches");
        }
    }

    private static void CheckWalletSerialization(Type plugin, Assembly pov)
    {
        Type itemVersion = typeof(Game).Assembly.GetType("Version+Item", true)!;
        object version = Enum.ToObject(itemVersion, Enum.GetValues(itemVersion).Cast<object>().Max(Convert.ToInt32));
        MethodInfo load = AccessTools.DeclaredMethod(typeof(ItemData), "Load", new[] { typeof(ZPackage), typeof(ItemData), itemVersion });
        foreach (int count in new[] { 65535, 65536, 123456, int.MaxValue })
        {
            var source = new ItemData { m_stack = count, m_gridPos = new Vector2i(3, 14), m_quality = 2, m_shared = new ItemData.SharedData { m_name = "Coins", m_maxStackSize = 999 } };
            source.m_customData["foreign"] = "preserve";
            var package = new ZPackage(); source.Save(package); package.SetPos(0);
            var copy = new ItemData(); load.Invoke(null, new[] { package, copy, version });
            Check(copy.m_stack == count && copy.m_gridPos == source.m_gridPos && copy.m_quality == 2 && copy.m_customData["foreign"] == "preserve" &&
                  !copy.m_customData.ContainsKey("pt.bigstack") && !source.m_customData.ContainsKey("pt.bigstack"), "original save/load roundtrip count " + count);
        }
        var failing = new ItemData { m_stack = 70000 };
        try { failing.Save(null!); } catch { }
        Check(!failing.m_customData.ContainsKey("pt.bigstack"), "native save finalizer cleans marker on exception");
        CheckWalletInventoryLoad(plugin, pov);
        MethodInfo save = AccessTools.DeclaredMethod(typeof(ItemData), "Save", new[] { typeof(ZPackage) });
        MethodInfo prefix = AccessTools.DeclaredMethod(pov.GetType("PassiveTree.Gear.Wallet+BigStackSave", true)!, "Prefix");
        var native = new Harmony("treextr.pathofvalheiman");
        native.Unpatch(save, prefix);
        bool rejected = false;
        try { plugin.GetMethod("VerifyPathOfValheimanWalletSerialization", Statics)!.Invoke(null, new object[] { pov }); }
        catch (TargetInvocationException) { rejected = true; }
        finally { native.Patch(save, prefix: new HarmonyMethod(prefix)); }
        Check(rejected, "partial large-stack serialization patch registration is rejected");
    }

    private static ItemData.SharedData? _walletLoadShared;
    private static int _walletLoadMax;
    private static Action? _walletNestedLoad;

    private static bool WalletLoadShared(ref ItemData.SharedData __result)
    { __result = _walletLoadShared!; return false; }

    private static void WalletLoadPrefabIdentity(ref int __result) => __result = 123;

    // Substitute only prefab creation: original Inventory.Load, ItemData.Load,
    // native max-lift/finalizer and the actual patched positional AddItem run.
    private static bool WalletLoadPrefab(Inventory __instance, ItemData itemData, ref bool __result)
    {
        Action? nested = _walletNestedLoad; _walletNestedLoad = null; nested?.Invoke();
        _walletLoadMax = _walletLoadShared!.m_maxStackSize;
        itemData.m_shared = _walletLoadShared;
        itemData.m_stack = Math.Min(itemData.m_stack, _walletLoadMax);
        __result = (bool)AccessTools.DeclaredMethod(typeof(Inventory), "AddItem", new[] {
            typeof(ItemData), typeof(int), typeof(int), typeof(int), typeof(bool) }).Invoke(__instance,
            new object[] { itemData, itemData.m_stack, itemData.m_gridPos.x, itemData.m_gridPos.y, true });
        return false;
    }

    private static ZPackage WalletInventoryPackage(int count)
    {
        var saved = new Inventory("SavedCoins", null, 8, 15);
        var item = new ItemData { m_stack = count, m_gridPos = new Vector2i(3, 14),
            m_shared = new ItemData.SharedData { m_name = "Coins", m_maxStackSize = 999 } };
        item.m_customData["foreign"] = "preserve";
        ((List<ItemData>)AccessTools.Field(typeof(Inventory), "m_inventory").GetValue(saved)).Add(item);
        var package = new ZPackage(); saved.Save(package);
        package.SetPos(0);
        return package;
    }

    private static void CheckWalletInventoryLoad(Type plugin, Assembly pov)
    {
        var fixture = new Harmony("smoke.pov.wallet.load-prefab");
        _walletLoadShared = new ItemData.SharedData { m_name = "Coins", m_maxStackSize = 999 };
        fixture.Patch(AccessTools.DeclaredPropertyGetter(pov.GetType("PassiveTree.Gear.Wallet", true)!, "CoinShared"),
            prefix: new HarmonyMethod(typeof(Program), nameof(WalletLoadShared)));
        // No Unity prefab exists in the fixture. Supply only its identity after
        // reading the original unmodified payload, allowing the AddItem branch.
        fixture.Patch(AccessTools.GetDeclaredMethods(typeof(ItemData)).Single(m => m.Name == "Load" && m.ReturnType == typeof(int)),
            postfix: new HarmonyMethod(typeof(Program), nameof(WalletLoadPrefabIdentity)));
        fixture.Patch(AccessTools.DeclaredMethod(typeof(Inventory), "AddItem", new[] { typeof(int), typeof(ItemData), typeof(bool) }),
            prefix: new HarmonyMethod(typeof(Program), nameof(WalletLoadPrefab)));
        try
        {
            foreach (bool flagged in new[] { false, true })
            {
                var container = new Inventory("Tombstone", null, 8, 15);
                var nested = new Inventory("NestedContainer", null, 8, 15);
                _walletNestedLoad = () => nested.Load(WalletInventoryPackage(70000), true);
                var package = WalletInventoryPackage(123456);
                if (flagged) container.Load(package, true); else container.Load(package);
                Check(container.GetItemAt(3, 14)?.m_stack == 123456 && nested.GetItemAt(3, 14)?.m_stack == 70000 &&
                      container.GetItemAt(3, 14)?.m_customData["foreign"] == "preserve",
                    "original container load retains large stacks through nested load and positional insertion; flag=" + flagged);
                Check(_walletLoadMax == int.MaxValue && _walletLoadShared.m_maxStackSize == 999,
                    "native load raises and restores ordinary Coins maximum; flag=" + flagged);
                bool threw = false;
                try { if (flagged) container.Load(new ZPackage(), true); else container.Load(new ZPackage()); }
                catch { threw = true; }
                Check(threw && _walletLoadShared.m_maxStackSize == 999 &&
                      plugin.GetField("_walletLoadingInventory", Statics)!.GetValue(null) == null,
                    "failed container load restores native maximum and IS load scope; flag=" + flagged);
            }
        }
        finally { fixture.UnpatchSelf(); _walletLoadShared = null; _walletNestedLoad = null; }
    }
}
