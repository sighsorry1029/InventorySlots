using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using ItemData = ItemDrop.ItemData;

namespace InventorySlots;

// All optional types stay behind reflection. PatchAll enumerates this assembly's
// types even without PoV installed, so a statically implemented external interface
// would make the otherwise optional dependency mandatory at type-load time.
internal sealed class PathOfValheimanStorageBridge
{
    internal const string Owner = "sighsorry.InventorySlots";
    internal readonly Assembly Assembly;
    internal readonly Func<bool> Enabled;
    internal readonly Func<bool> Suspended;
    internal readonly Func<string> ProviderId;
    internal readonly Func<Player, string, bool> Open;
    internal readonly Func<string, ItemData, bool> Accepts;
    internal readonly Func<string, string> KeyLabel;
    internal readonly Func<Player, string, bool> KeptOnDeath;
    internal readonly Func<Player, string, bool> Weightless;
    internal readonly Func<int> ExtraRows;
    internal readonly Func<Player, string, ItemData> Equipped;
    internal readonly Func<Player, string, ItemData, bool> Equip;
    internal readonly Action<Player, string> Unequip;
    private Func<Player, ItemData>? _quiverWhenDisabled;
    private Action<Player>? _removeQuiverWhenDisabled;
    internal readonly Action<string> Changed;
    internal readonly List<(string Id, int Ordinal)> Cells = new();
    internal readonly HashSet<int> UnsupportedOrdinals = new();
    internal readonly List<string> UnhostedUtilityCells = new();
    internal readonly List<string> UnhostedNativeCells = new();
    private static readonly Dictionary<string, (int Ordinal, string Kind, bool Worn, bool Storage)> SupportedCells = CreateSupportedCells();
    internal bool HasExtraTrinkets { get; private set; }
    internal bool HasRings { get; private set; }
    private readonly Dictionary<string, Func<Player, ItemData>> _trinketReads = new(StringComparer.Ordinal);
    internal IReadOnlyList<string> StorageCells { get; private set; } = Array.Empty<string>();
    private readonly MethodInfo _register;
    private readonly MethodInfo _unregister;
    private readonly Type _providerInterface;
    private object? _provider;

    internal PathOfValheimanStorageBridge(Assembly assembly)
    {
        Assembly = assembly;
        var atLeast = Bind<Func<int, int, bool>>("Valheiman.Api.Pov", "AtLeast");
        var has = Bind<Func<string, bool>>("Valheiman.Api.Pov", "Has");
        if (!atLeast(1, 0) || !has("storage") || !has("equipment.cells"))
            throw new NotSupportedException("PoV API 1.x storage/equipment.cells is required");
        _providerInterface = assembly.GetType("Valheiman.Api.IStorageProvider", true)!;
        _register = Require("Valheiman.Api.Storage", "Register", typeof(string), _providerInterface);
        _unregister = Require("Valheiman.Api.Storage", "Unregister", typeof(void), _providerInterface);
        Enabled = Bind<Func<bool>>("Valheiman.Api.Equipment", "get_On");
        Suspended = Bind<Func<bool>>("Valheiman.Api.Storage", "get_Suspended");
        ProviderId = Bind<Func<string>>("Valheiman.Api.Storage", "get_ProviderId");
        Open = Bind<Func<Player, string, bool>>("Valheiman.Api.Equipment", "IsOpen");
        Accepts = Bind<Func<string, ItemData, bool>>("Valheiman.Api.Equipment", "Accepts");
        KeyLabel = Bind<Func<string, string>>("Valheiman.Api.Equipment", "KeyLabel");
        KeptOnDeath = Bind<Func<Player, string, bool>>("Valheiman.Api.Storage", "KeptOnDeath");
        Weightless = Bind<Func<Player, string, bool>>("Valheiman.Api.Storage", "Weightless");
        Equipped = Bind<Func<Player, string, ItemData>>("Valheiman.Api.Equipment", "ItemIn");
        Equip = Bind<Func<Player, string, ItemData, bool>>("Valheiman.Api.Equipment", "Put");
        Unequip = Bind<Action<Player, string>>("Valheiman.Api.Equipment", "TakeOut");
        ExtraRows = Bind<Func<int>>("Valheiman.Api.Rows", "get_Extra");
        Changed = Bind<Action<string>>("Valheiman.Api.Storage", "Changed");
    }

    internal void InstallFixes(Harmony harmony)
    {
        Assembly assembly = Assembly;
        Type slotType = assembly.GetType("PassiveTree.Gear.Slot", true)!;
        // The public equipment API intentionally hides equipped state while Off.
        // Withdrawal still needs the registered quiver's own cleanup callback.
        var player = Expression.Parameter(typeof(Player), "player");
        var quiver = Expression.Constant(Enum.Parse(slotType, "Quiver"), slotType);
        _quiverWhenDisabled = Expression.Lambda<Func<Player, ItemData>>(Expression.Call(
            Require("PassiveTree.Gear.State", "Item", typeof(ItemData), typeof(Player), slotType), player, quiver), player).Compile();
        _removeQuiverWhenDisabled = Expression.Lambda<Action<Player>>(Expression.Call(
            Require("PassiveTree.Gear.State", "Clear", typeof(void), typeof(Player), slotType), player, quiver), player).Compile();
        // Check the enum rather than interpreting arbitrary future enum ordinals.
        if (Convert.ToInt32(Enum.Parse(slotType, "Utility")) != 9)
            throw new NotSupportedException("PoV utility slot ordinal changed");
        for (int i = 0; i < 8; i++)
            if (Convert.ToInt32(Enum.Parse(slotType, "Wallet" + i)) != 24 + i ||
                Convert.ToInt32(Enum.Parse(slotType, "Tool" + i)) != 16 + i ||
                Convert.ToInt32(Enum.Parse(slotType, "Key" + i)) != 35 + i)
                throw new NotSupportedException("PoV storage slot ordinals changed");
        harmony.Patch(Require("PassiveTree.Gear.State", "Enabled", typeof(bool), typeof(Player), slotType),
            prefix: new HarmonyMethod(typeof(InventorySlotsPlugin), "AllowPathOfValheimanCell"));
        harmony.Patch(Require("PassiveTree.Gear.UtilitySlots+Equip", "Prefix", typeof(bool), typeof(Humanoid),
                typeof(ItemData), typeof(bool).MakeByRefType()),
            prefix: new HarmonyMethod(typeof(InventorySlotsPlugin), "SkipUnhostedPathOfValheimanUtilityEquip"),
            postfix: new HarmonyMethod(typeof(InventorySlotsPlugin), "RejectUnsupportedPathOfValheimanUtilityEquip"));
        var equipmentCells = (IEnumerable)assembly.GetType("Valheiman.Api.Equipment")!.GetProperty("Cells")!.GetValue(null);
        StorageCells = Bind<Func<IReadOnlyList<string>>>("Valheiman.Api.Storage", "get_Cells")();

        Type cellInfo = assembly.GetType("Valheiman.Api.CellInfo", true)!;
        PropertyInfo id = cellInfo.GetProperty("Id")!;
        FieldInfo ordinal = AccessTools.DeclaredField(cellInfo, "Slot");
        var descriptors = new Dictionary<string, (int Ordinal, string Kind, bool Worn, bool Storage)>(StringComparer.Ordinal);
        var ordinals = new HashSet<int>();
        var storageIds = new HashSet<string>(StorageCells, StringComparer.Ordinal);
        if (storageIds.Count != StorageCells.Count) throw new NotSupportedException("Duplicate PoV storage IDs");
        foreach (object cell in equipmentCells)
        {
            string key = (string)id.GetValue(cell);
            int position = Convert.ToInt32(ordinal.GetValue(cell));
            string kind = cellInfo.GetProperty("Kind")!.GetValue(cell)!.ToString();
            bool worn = (bool)cellInfo.GetProperty("Worn")!.GetValue(cell);
            if (string.IsNullOrEmpty(key) || descriptors.ContainsKey(key) || position < 0 ||
                position == 4 || position == 5 || !ordinals.Add(position))
                throw new NotSupportedException("Duplicate or invalid PoV equipment coordinates");
            var contract = (position, kind, worn, storageIds.Contains(key));
            descriptors.Add(key, contract);
            if (SupportedCells.TryGetValue(key, out var expected))
            {
                if (contract != expected) throw new NotSupportedException("PoV cell contract changed: " + key);
            }
            else
            {
                // Storage cells need our backing store, so unhosted cells stay closed.
                // Native roles (for example belt.lantern) keep their original backpack
                // equipment behavior; a new descriptor alone must not block loading.
                // Purse remains the existing intentional omission, handled by coin benefits.
                bool purse = position >= 24 && position <= 31 && key == "purse." + (position - 24) &&
                             kind == "Purse" && worn && !contract.Item4;
                if (purse || contract.Item4) UnsupportedOrdinals.Add(position);
                else UnhostedNativeCells.Add(key);
                if (contract.Item4 && kind == "Utility" && key != "utility") UnhostedUtilityCells.Add(key);
            }
            // Retain every native coordinate for atomic recovery of saved items,
            // even when no physical InventorySlots cell will be created for it.
            Cells.Add((key, position));
        }
        if (storageIds.Any(key => !descriptors.ContainsKey(key)))
            throw new NotSupportedException("PoV storage refers to an unknown equipment cell");
        // 5.0.9 advertises API 1.1.0 but AtLeast(1, 1) still returns false.
        // Detect the actual cells, including their native saved coordinates.
        HasExtraTrinkets = Cells.Any(c => c.Id.StartsWith("trinket.", StringComparison.Ordinal) && SupportsCell(c.Id));
        if (HasExtraTrinkets)
        {
            foreach (var role in new[] { ("trinket.1", 45), ("trinket.2", 46), ("trinket.hand", 47) })
                if (!Cells.Contains(role) || StorageCells.Contains(role.Item1))
                    throw new NotSupportedException("PoV extra trinket cell contract changed");
            foreach (string role in InventorySlotsPlugin.PovTrinketRoles)
            {
                var native = Expression.Constant(Enum.ToObject(slotType, Cells.Single(c => c.Id == role).Ordinal), slotType);
                _trinketReads.Add(role, Expression.Lambda<Func<Player, ItemData>>(Expression.Call(
                    Require("PassiveTree.Gear.State", "Item", typeof(ItemData), typeof(Player), slotType), player, native), player).Compile());
            }
            // PoV restores extra trinket marks after Player.Load, in its spawn callback.
            harmony.Patch(Require("PassiveTree.Trinkets+Spawn", "Postfix", typeof(void), typeof(Player)),
                postfix: new HarmonyMethod(typeof(InventorySlotsPlugin), "OnPathOfValheimanTrinketsSpawned"));
        }
        HasRings = Cells.Any(c => c.Id.StartsWith("ring.", StringComparison.Ordinal) && SupportsCell(c.Id));
        if (HasRings)
        {
            Require("Valheiman.Api.Equipment", "IsRing", typeof(bool), typeof(ItemData));
            for (int i = 0; i < 10; i++)
                if (!Cells.Contains(("ring." + i, 48 + i)) || !StorageCells.Contains("ring." + i) ||
                    Convert.ToInt32(Enum.Parse(slotType, "Ring" + i)) != 48 + i)
                    throw new NotSupportedException("PoV ring cell contract changed");
        }
        foreach (string key in SupportedCells.Keys)
            if (!(key.StartsWith("ring.", StringComparison.Ordinal) && !HasRings) &&
                !(key.StartsWith("trinket.", StringComparison.Ordinal) && !HasExtraTrinkets) &&
                !descriptors.ContainsKey(key))
                throw new NotSupportedException("Required PoV cell is missing: " + key);

        if (HasRings)
        {
            // 5.1 arranges native side plates every frame. The provider's UI
            // already owns their layout; keep PoV's other side-plate work.
            Type plates = assembly.GetType("PassiveTree.UI.SidePlates", true)!;
            FieldInfo columned = AccessTools.DeclaredField(plates, "Columned");
            if (columned?.FieldType != typeof(bool) || !columned.IsStatic)
                throw new MissingFieldException(plates.FullName, "Columned");
            harmony.Patch(Require(plates.FullName!, "Column", typeof(void), typeof(InventoryGui), typeof(UnityEngine.RectTransform)),
                prefix: new HarmonyMethod(typeof(InventorySlotsPlugin), "KeepPathOfValheimanStatPanelLayout"));
        }
        // Coins stay in ordinary backpack cells. Public API queries retain PoV's
        // node/option policy; field injection respects the original private field.
        harmony.Patch(AccessTools.DeclaredMethod(typeof(Inventory), "UpdateTotalWeight", Type.EmptyTypes),
            postfix: new HarmonyMethod(typeof(InventorySlotsPlugin), "ApplyPathOfValheimanCoinWeight"));

        foreach (var target in new[] {
            Require("PassiveTree.Gear.State", "Assign", typeof(bool), typeof(Player), slotType, typeof(ItemData)),
            Require("PassiveTree.Gear.UtilitySlots", "Tick", typeof(void), typeof(Player)) })
        {
            InventorySlotsPlugin.FindPathOfValheimanUtilityUnequip(PatchProcessor.GetOriginalInstructions(target).ToList());
            if (HasRings && target.Name == "Tick")
                InventorySlotsPlugin.FindPathOfValheimanUtilityUnequip(PatchProcessor.GetOriginalInstructions(target).ToList(), "PutRing");
            harmony.Patch(target, transpiler: new HarmonyMethod(typeof(InventorySlotsPlugin), "DeferPathOfValheimanUtilityUnequip"));
        }
    }

    internal static bool SupportsCell(string id) => SupportedCells.ContainsKey(id);

    private static Dictionary<string, (int Ordinal, string Kind, bool Worn, bool Storage)> CreateSupportedCells()
    {
        var cells = new Dictionary<string, (int, string, bool, bool)>(StringComparer.Ordinal)
        {
            ["helmet"] = (0, "Armour", true, false), ["chest"] = (1, "Armour", true, false),
            ["legs"] = (2, "Armour", true, false), ["cape"] = (3, "Armour", true, false),
            ["belt"] = (6, "Belt", true, false), ["quiver"] = (7, "Quiver", false, false),
            ["trinket"] = (8, "Trinket", true, false),
            ["utility.wisp"] = (43, "Utility", false, true),
            ["utility.wishbone"] = (44, "Utility", false, true),
            ["trinket.1"] = (45, "Trinket", true, false),
            ["trinket.2"] = (46, "Trinket", true, false),
            ["trinket.hand"] = (47, "Trinket", true, false)
        };
        for (int i = 0; i < 3; i++)
        {
            cells.Add("food." + i, (10 + i, "Food", false, true));
            cells.Add("arrow." + i, (13 + i, "Arrow", false, true));
            cells.Add("potion." + i, (32 + i, "Potion", false, true));
        }
        for (int i = 0; i < 10; i++) cells.Add("ring." + i, (48 + i, "Ring", false, true));
        return cells;
    }

    internal ItemData? TrinketItem(Player player, string cell) =>
        Enabled() ? Equipped(player, cell) : _trinketReads.TryGetValue(cell, out var read) ? read(player) : null;

    internal ItemData? QuiverItem(Player player) => Enabled() ? Equipped(player, "quiver") : _quiverWhenDisabled?.Invoke(player);

    internal void RemoveQuiver(Player player)
    {
        if (Enabled()) Unequip(player, "quiver");
        else _removeQuiverWhenDisabled?.Invoke(player);
    }

    internal MethodInfo Require(string type, string name, Type result, params Type[] args)
    {
        MethodInfo? method = Assembly.GetType(type)?.GetMethod(name,
            BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic, null, args, null);
        if (method == null || method.ReturnType != result ||
            (type.StartsWith("Valheiman.Api.", StringComparison.Ordinal) && !method.IsPublic))
            throw new MissingMethodException(type, name);
        return method;
    }

    internal T Bind<T>(string type, string name) where T : Delegate
    {
        MethodInfo invoke = typeof(T).GetMethod("Invoke")!;
        return (T)Delegate.CreateDelegate(typeof(T), Require(type, name, invoke.ReturnType,
            invoke.GetParameters().Select(p => p.ParameterType).ToArray()));
    }

    internal void Register(Func<Player, string, ItemData?> read, Func<Player, string, ItemData, bool> put,
        Func<Player, string, bool> take)
    {
        _provider = CreateProvider(_providerInterface, read, put, take);
        string? error = (string?)_register.Invoke(null, new[] { _provider });
        if (error != null) { _provider = null; throw new InvalidOperationException(error); }
    }

    internal void Unregister()
    {
        if (_provider != null) _unregister.Invoke(null, new[] { _provider });
        _provider = null;
    }

    internal static object CreateProvider(Type contract, Delegate read, Delegate put, Delegate take)
    {
        var assembly = AssemblyBuilder.DefineDynamicAssembly(new AssemblyName("InventorySlots.PovStorage"), AssemblyBuilderAccess.Run);
        var type = assembly.DefineDynamicModule("Storage").DefineType("Provider", TypeAttributes.Public | TypeAttributes.Sealed);
        type.AddInterfaceImplementation(contract);
        var callbacks = new Dictionary<string, Delegate> { ["ItemIn"] = read, ["Put"] = put, ["TakeOut"] = take };
        foreach (MethodInfo member in contract.GetMethods())
        {
            Type[] parameters = member.GetParameters().Select(p => p.ParameterType).ToArray();
            var method = type.DefineMethod(member.Name, MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.Final,
                member.ReturnType, parameters);
            var il = method.GetILGenerator();
            if (member.Name == "get_Id") il.Emit(OpCodes.Ldstr, Owner);
            else if (member.Name == "get_Contract") il.Emit(OpCodes.Ldc_I4_1);
            else
            {
                if (!callbacks.TryGetValue(member.Name, out Delegate callback)) throw new NotSupportedException(member.Name);
                var field = type.DefineField(member.Name + "Callback", callback.GetType(), FieldAttributes.Public);
                il.Emit(OpCodes.Ldarg_0);
                il.Emit(OpCodes.Ldfld, field);
                for (int i = 0; i < parameters.Length; i++) il.Emit(OpCodes.Ldarg, i + 1);
                il.Emit(OpCodes.Callvirt, callback.GetType().GetMethod("Invoke")!);
            }
            il.Emit(OpCodes.Ret);
            type.DefineMethodOverride(method, member);
        }
        Type generated = type.CreateType()!;
        object instance = Activator.CreateInstance(generated);
        foreach (var entry in callbacks) generated.GetField(entry.Key + "Callback")!.SetValue(instance, entry.Value);
        return instance;
    }
}
