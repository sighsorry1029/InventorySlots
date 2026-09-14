using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using BepInEx.Configuration;
using UnityEngine;

internal static class Program
{
    private static Type plugin = null!;
    private static int checks;

    private static int Main(string[] args)
    {
        if (args.Length != 3 && (args.Length != 4 || (args[3] != "--button-offsets" && args[3] != "--ui-layout" && args[3] != "--restock-reserve" && args[3] != "--button-modes")))
            throw new ArgumentException("Usage: <final mod.dll> <original Managed> <BepInEx core> [--ui-layout|--restock-reserve|--button-modes]");
        string[] roots = { Path.GetDirectoryName(Path.GetFullPath(args[0]))!, Path.GetFullPath(args[1]), Path.GetFullPath(args[2]) };
        AppDomain.CurrentDomain.AssemblyResolve += (_, request) =>
        {
            string name = new AssemblyName(request.Name).Name + ".dll";
            foreach (string root in roots)
            {
                string path = Path.Combine(root, name);
                if (File.Exists(path)) return Assembly.LoadFrom(path);
            }
            return null;
        };
        try
        {
            // Standalone Mono does not run BepInEx's preloader. Initialize its
            // managed paths before ConfigFile's static constructor. Keep any
            // framework-created config/cache files outside the user's game.
            Assembly bep = Assembly.LoadFrom(Path.Combine(roots[2], "BepInEx.dll"));
            string isolatedRoot = Path.Combine(Path.GetTempPath(), "InventoryButtons-harness-" + Guid.NewGuid());
            bep.GetType("BepInEx.Paths", true)!.GetMethod("SetExecutablePath", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)!
                .Invoke(null, new object[] { Path.Combine(isolatedRoot, "valheim.exe"), Path.Combine(isolatedRoot, "BepInEx"), roots[1], roots });
            // Supply only BepInEx's managed dispatch queue. ServerSync may enqueue
            // startup work, but this harness never executes it or constructs Unity objects.
            Type threading = Assembly.LoadFrom(Path.Combine(roots[2], "BepInEx.dll")).GetType("BepInEx.ThreadingHelper", true)!;
            object queue = FormatterServices.GetUninitializedObject(threading);
            threading.GetField("_invokeLock", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(queue, new object());
            threading.GetField("<Instance>k__BackingField", BindingFlags.NonPublic | BindingFlags.Static)!.SetValue(null, queue);
            Assembly mod = Assembly.LoadFrom(Path.GetFullPath(args[0]));
            plugin = mod.GetType(mod.GetName().Name + "." + mod.GetName().Name + "Plugin", true)!;
            if (args.Length == 4 && args[3] == "--button-modes") RunButtonModeChecks();
            else if (args.Length == 4 && args[3] == "--restock-reserve") RunRestockReserveChecks();
            else if (args.Length == 4) RunUiLayoutChecks();
            else Run();
            System.Console.WriteLine($"PASS {checks} isolated checks against supplied DLL and original game assemblies. CLR {Environment.Version}; no Unity/game execution.");
            return 0;
        }
        catch (Exception error)
        {
            System.Console.Error.WriteLine(error);
            return 1;
        }
    }

    private static object? Call(string name, params object?[] args) =>
        plugin.GetMethod(name, BindingFlags.NonPublic | BindingFlags.Static)!.Invoke(null, args);

    private static void Check(string name, bool condition)
    {
        if (!condition) throw new InvalidOperationException(name);
        checks++;
        System.Console.WriteLine("PASS " + name);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void RunButtonModeChecks()
    {
        ConfigFile config = new ConfigFile(Path.Combine(Path.GetTempPath(), "button-modes-" + Guid.NewGuid() + ".cfg"), false) { SaveOnConfigSet = false };
        Type mode = plugin.GetNestedType("InventoryButtonMode", BindingFlags.NonPublic)!;
        Type toggle = plugin.GetNestedType("Toggle", BindingFlags.Public | BindingFlags.NonPublic)!;
        MethodInfo bind = typeof(ConfigFile).GetMethods().Single(m => m.Name == "Bind" && m.IsGenericMethod &&
            m.GetParameters().Length == 4 && m.GetParameters()[0].ParameterType == typeof(string) && m.GetParameters()[3].ParameterType == typeof(ConfigDescription));
        ConfigEntryBase Bind(string field, Type type, string value)
        {
            ConfigEntryBase entry = (ConfigEntryBase)bind.MakeGenericMethod(type).Invoke(config,
                new object[] { "test", field, Enum.Parse(type, value), new ConfigDescription("") })!;
            plugin.GetField(field, BindingFlags.NonPublic | BindingFlags.Static)!.SetValue(null, entry);
            return entry;
        }
        ConfigEntryBase restock = Bind("_restockButtonMode", mode, "Auto");
        ConfigEntryBase exclude = Bind("_autoPickupButtonMode", mode, "Auto");
        ConfigEntryBase trash = Bind("_trashButtonMode", mode, "Auto");
        ConfigEntryBase server = Bind("_enableInventoryTrashPanel", toggle, "On");
        Check("button mode choices", string.Join(",", Enum.GetNames(mode)) == "Off,Auto,On");
        foreach (string permission in new[] { "Off", "On" })
        foreach (string t in new[] { "Off", "Auto", "On" })
        foreach (string e in new[] { "Off", "Auto", "On" })
        foreach (string r in new[] { "Off", "Auto", "On" })
        {
            server.BoxedValue = Enum.Parse(toggle, permission);
            trash.BoxedValue = Enum.Parse(mode, t);
            exclude.BoxedValue = Enum.Parse(mode, e);
            restock.BoxedValue = Enum.Parse(mode, r);
            string state = $"server={permission}, trash={t}, exclude={e}, restock={r}";
            bool trashVisible = permission == "On" && t != "Off";
            Check(state + ": server permission and local trash visibility", (bool)Call("IsInventoryTrashButtonEnabled")! == trashVisible);
            Check(state + ": independent restock visibility", (bool)Call("IsItemRuleButtonEnabled", true)! == (r != "Off"));
            Check(state + ": independent exclude visibility", (bool)Call("IsItemRuleButtonEnabled", false)! == (e != "Off"));
            int[] packedColumns = trashVisible ? new[] { 1, 2 } : new[] { 0, 1 };
            Check(state + ": exclude fills the rightmost available column", (int)Call("GetItemRuleColumnsFromRight", false)! == packedColumns[0]);
            Check(state + ": restock follows enabled neighbors", (int)Call("GetItemRuleColumnsFromRight", true)! == packedColumns[e == "Off" ? 0 : 1]);
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void RunUiLayoutChecks()
    {
        CheckOffset("unbound sort", "GetSortButtonPositionOffset", 0f, 0f);

        // Exercise actual config entries and compiled getters without opening UI
        // or writing the user's configuration. No Unity objects are constructed.
        ConfigFile config = new ConfigFile(Path.Combine(Path.GetTempPath(), "InventoryActions-smoke-" + Guid.NewGuid() + ".cfg"), false)
        {
            SaveOnConfigSet = false
        };
        ConfigEntry<string> sort = config.Bind("test", "sort", "x: 1.25 y: -2", "");
        FieldInfo sortField = plugin.GetField("_sortButtonPositionOffset", BindingFlags.NonPublic | BindingFlags.Static)!;
        sortField.SetValue(null, sort);

        for (int i = 0; i < 3; i++)
        {
            CheckOffset("stable sort " + i, "GetSortButtonPositionOffset", 1.25f, -2f);
        }

        sort.Value = "X=-4 Y=2.5";
        CheckOffset("live config edit", "GetSortButtonPositionOffset", -4f, 2.5f);
        foreach (string raw in new[] { "invalid", "invalid", "x: 1 y: nope", "", "   " })
        {
            sort.Value = raw;
            CheckOffset("invalid or blank config returns zero", "GetSortButtonPositionOffset", 0f, 0f);
        }
        sort.Value = "x: 1.25 y: -2";
        CheckOffset("valid config after invalid input", "GetSortButtonPositionOffset", 1.25f, -2f);

        ConfigEntry<string> replacement = config.Bind("test", "replacement", sort.Value, "");
        sortField.SetValue(null, replacement);
        CheckOffset("same value after rebind", "GetSortButtonPositionOffset", 1.25f, -2f);
        replacement.Value = "0, 7";
        CheckOffset("new value after rebind", "GetSortButtonPositionOffset", 0f, 7f);
        sortField.SetValue(null, null);
        CheckOffset("unbind after populated value", "GetSortButtonPositionOffset", 0f, 0f);
        sortField.SetValue(null, replacement);
        CheckOffset("restore same entry after unbind", "GetSortButtonPositionOffset", 0f, 7f);

        // Real config entries exercise all live visibility combinations. They
        // do not touch saved item rules or require any Unity object instances.
        RunButtonModeChecks();

        // Exact column centers and the bottom edge for base/purchased/expanded
        // row counts. These values are GUI coordinates before the grid transform.
        foreach ((int rows, float bottom) in new[] { (4, -288f), (6, -428f), (9, -638f) })
        foreach ((int fromRight, float left) in new[] { (0, 500f), (1, 430f), (2, 360f) })
        {
            Vector3 actual = (Vector3)Call("CalculateInventoryBottomButtonPosition", 8, rows, 70f, 50f, fromRight)!;
            Check($"{rows} rows, column {8 - fromRight} center/bottom", actual.x == left && actual.y == bottom && actual.z == 0f);
        }
        Vector3 resized = (Vector3)Call("CalculateInventoryBottomButtonPosition", 8, 5, 90f, 58f, 0)!;
        Check("changed slot/button size stays centered", resized.x == 646f && resized.y == -458f);

        // EAQS 3.x appends hidden slot rows to the same Inventory. Exercise the
        // compiled boundary using a live delegate without loading that optional mod.
        FieldInfo eaqsRowsField = plugin.GetField("_equipmentAndQuickSlotsVisibleRows", BindingFlags.NonPublic | BindingFlags.Static)!;
        int visibleRows = 5;
        eaqsRowsField.SetValue(null, new Func<int>(() => visibleRows));
        Inventory eaqsInventory = new Inventory("eaqs", null, 8, 8);
        Check("EAQS visible row API bounds regular rows", (int)Call("GetRegularPlayerRowsOrInventoryHeight", eaqsInventory)! == 5);
        Check("EAQS last visible row can be favorited", (bool)Call("CanFavoriteCell", eaqsInventory, new Vector2i(7, 4))!);
        Check("EAQS hidden slot row cannot be favorited", !(bool)Call("CanFavoriteCell", eaqsInventory, new Vector2i(0, 5))!);
        Check("EAQS hidden slot row cannot be trashed", !(bool)Call("CanTrashCell", eaqsInventory, new Vector2i(0, 5))!);
        ItemDrop.ItemData regularItem = Item(10, false);
        regularItem.m_gridPos = new Vector2i(0, 4);
        ItemDrop.ItemData hiddenSlotItem = Item(10, false);
        hiddenSlotItem.m_gridPos = new Vector2i(0, 5);
        Check("EAQS visible item remains a regular action item", (bool)Call("IsRegularActionItem", eaqsInventory, regularItem)!);
        Check("EAQS hidden item is excluded from regular actions", !(bool)Call("IsRegularActionItem", eaqsInventory, hiddenSlotItem)!);
        visibleRows = 6;
        Check("EAQS live row changes are read without reinitializing", (int)Call("GetRegularPlayerRowsOrInventoryHeight", eaqsInventory)! == 6);
        Check("EAQS newly visible row becomes regular", (bool)Call("CanTrashCell", eaqsInventory, new Vector2i(0, 5))!);
        eaqsRowsField.SetValue(null, null);
        Check("inventory height remains the fallback without EAQS", (int)Call("GetRegularPlayerRowsOrInventoryHeight", eaqsInventory)! == 8);

        // AzuEPI exposes the first special slot as a live linear grid index. Keep
        // automatic actions above that boundary without loading the optional mod.
        FieldInfo azuSlotIndexField = plugin.GetField("_azuEpiGetSlotGridLinearIndex", BindingFlags.NonPublic | BindingFlags.Static)!;
        FieldInfo azuConfigField = plugin.GetField("_azuEpiConfig", BindingFlags.NonPublic | BindingFlags.Static)!;
        FieldInfo azuSeparatePanelEntryField = plugin.GetField("_azuEpiSeparatePanelEntry", BindingFlags.NonPublic | BindingFlags.Static)!;
        FieldInfo azuSeparatePanelField = plugin.GetField("_azuEpiDisplaysEquipmentInSeparatePanel", BindingFlags.NonPublic | BindingFlags.Static)!;
        int firstSpecialSlot = 40;
        azuSlotIndexField.SetValue(null, new Func<Inventory, int, int>((_, _) => firstSpecialSlot));
        Inventory azuInventory = new Inventory("azu-epi", null, 8, 8);
        Check("AzuEPI first special slot bounds regular rows", (int)Call("GetRegularPlayerRowsOrInventoryHeight", azuInventory)! == 5);
        Check("AzuEPI last regular row can be favorited", (bool)Call("CanFavoriteCell", azuInventory, new Vector2i(7, 4))!);
        Check("AzuEPI special row cannot be favorited", !(bool)Call("CanFavoriteCell", azuInventory, new Vector2i(0, 5))!);
        Check("AzuEPI special row cannot be trashed", !(bool)Call("CanTrashCell", azuInventory, new Vector2i(0, 5))!);
        ItemDrop.ItemData azuRegularItem = Item(10, false);
        azuRegularItem.m_gridPos = new Vector2i(0, 4);
        ItemDrop.ItemData azuSpecialItem = Item(10, false);
        azuSpecialItem.m_gridPos = new Vector2i(0, 5);
        Check("AzuEPI regular item remains an action item", (bool)Call("IsRegularActionItem", azuInventory, azuRegularItem)!);
        Check("AzuEPI special item is excluded from actions", !(bool)Call("IsRegularActionItem", azuInventory, azuSpecialItem)!);
        firstSpecialSlot = 48;
        Check("AzuEPI live row changes are read without reinitializing", (int)Call("GetRegularPlayerRowsOrInventoryHeight", azuInventory)! == 6);
        firstSpecialSlot = 64;
        Check("AzuEPI disabled equipment row uses full inventory height", (int)Call("GetRegularPlayerRowsOrInventoryHeight", azuInventory)! == 8);
        firstSpecialSlot = -1;
        Check("AzuEPI with no registered slots uses full inventory height", (int)Call("GetRegularPlayerRowsOrInventoryHeight", azuInventory)! == 8);

        firstSpecialSlot = 40;
        ConfigEntry<int> azuSeparatePanel = config.Bind("2 - Inventory", "Display Equipment in Separate Panel", 1, "");
        azuConfigField.SetValue(null, config);
        azuSeparatePanelEntryField.SetValue(null, azuSeparatePanel);
        Call("RefreshAzuEpiSeparatePanelSetting");
        EventHandler<SettingChangedEventArgs> azuSettingChanged =
            (EventHandler<SettingChangedEventArgs>)Delegate.CreateDelegate(
                typeof(EventHandler<SettingChangedEventArgs>),
                plugin.GetMethod("HandleAzuEpiSettingChanged", BindingFlags.NonPublic | BindingFlags.Static)!);
        config.SettingChanged += azuSettingChanged;
        Check("AzuEPI separate panel places buttons after regular rows", (int)Call("GetAzuEpiDisplayedPlayerRows", azuInventory)! == 5);
        azuSeparatePanel.Value = 0;
        Check("AzuEPI live inline setting places buttons after all rows", (int)Call("GetAzuEpiDisplayedPlayerRows", azuInventory)! == 8);
        azuSeparatePanel.Value = 1;
        Check("AzuEPI live separate setting restores regular-row layout", (int)Call("GetAzuEpiDisplayedPlayerRows", azuInventory)! == 5);
        config.SettingChanged -= azuSettingChanged;
        azuSlotIndexField.SetValue(null, null);
        azuConfigField.SetValue(null, null);
        azuSeparatePanelEntryField.SetValue(null, null);
        azuSeparatePanelField.SetValue(null, null);
        Check("inventory height remains the fallback without slot providers", (int)Call("GetRegularPlayerRowsOrInventoryHeight", azuInventory)! == 8);
    }
    private static void CheckOffset(string name, string getter, float x, float y)
    {
        Vector2 value = (Vector2)Call(getter)!;
        Check(name, value.x == x && value.y == y);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void Run()
    {
        // Invoke the compiled helper, including its cached private Harmony delegate.
        Inventory empty = new Inventory("notification", null, 8, 6);
        int notifications = 0;
        empty.m_onChanged += () => notifications++;
        Call("NotifyInventoryChanged", empty);
        Check("private Changed(bool,bool) notifies once", notifications == 1);
        Check("weight recomputation works on original Inventory", empty.GetTotalWeight() == 0);

        foreach (int rows in new[] { 4, 5, 6 })
        {
            Inventory inventory = new Inventory("pockets", null, 8, rows);
            Check($"{rows} rows: last cell can be favorited", (bool)Call("CanFavoriteCell", inventory, new Vector2i(7, rows - 1))!);
            Check($"{rows} rows: last cell can be trashed", (bool)Call("CanTrashCell", inventory, new Vector2i(7, rows - 1))!);
            Check($"{rows} rows: hotbar remains protected from trash", !(bool)Call("CanTrashCell", inventory, new Vector2i(0, 0))!);
            Check($"{rows} rows: next row is out of bounds", !(bool)Call("CanFavoriteCell", inventory, new Vector2i(0, rows))!);
            Check($"{rows} rows: next column is out of bounds", !(bool)Call("CanFavoriteCell", inventory, new Vector2i(8, rows - 1))!);
        }
        System.Console.WriteLine("NOT RUN: Player-dependent methods; the original Player interfaces require default-interface support absent from desktop .NET Framework. Verify in game/Mono.");

        Inventory mixed = new Inventory("markers", null, 8, 6);
        var normal = Item(20, false);
        var cheated = Item(30, true);
        mixed.GetAllItems().Add(normal);
        mixed.GetAllItems().Add(cheated);
        var items = new List<ItemDrop.ItemData>(mixed.GetAllItems());
        Call("MergeSortableStacks", items, mixed);
        Check("mixed cheat markers do not merge or disappear", normal.m_stack == 20 && cheated.m_stack == 30 && !normal.m_cheated && cheated.m_cheated && items.Count == 2);

        normal.m_cheated = true;
        normal.m_stack = 30;
        Call("MergeSortableStacks", items, mixed);
        Check("matching markers still consolidate and conserve quantity", normal.m_stack == 50 && cheated.m_stack == 10 && normal.m_cheated && cheated.m_cheated);

        normal.m_stack = 30;
        cheated.m_stack = 30;
        normal.m_customData["external"] = "preserve";
        Call("MergeSortableStacks", items, mixed);
        Check("external custom data retains stacking protection", normal.m_stack == 30 && cheated.m_stack == 30 && normal.m_customData["external"] == "preserve");
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void RunRestockReserveChecks()
    {
        // Exercise the final DLL's quantity policy with original Inventory/ItemData
        // and a real, unsaved BepInEx setting. Transfers below simulate success/failure;
        // native movement, Harmony patches and network ownership still need game tests.
        ConfigFile config = new ConfigFile(Path.Combine(Path.GetTempPath(), "Restock-reserve-" + Guid.NewGuid() + ".cfg"), false)
        {
            SaveOnConfigSet = false
        };
        Type toggle = plugin.GetNestedType("Toggle")!;
        MethodInfo bind = typeof(ConfigFile).GetMethods().Single(m => m.Name == "Bind" && m.IsGenericMethod &&
            m.GetParameters().Length == 4 && m.GetParameters()[0].ParameterType == typeof(string) &&
            m.GetParameters()[3].ParameterType == typeof(ConfigDescription)).MakeGenericMethod(toggle);
        ConfigEntryBase setting = (ConfigEntryBase)bind.Invoke(config, new object[] { "test", "leave one", Enum.Parse(toggle, "On"), new ConfigDescription("") })!;
        plugin.GetField("_restockLeaveOneItem", BindingFlags.NonPublic | BindingFlags.Static)!.SetValue(null, setting);
        Type mode = plugin.GetMethod("GetRestockTransferAmount", BindingFlags.NonPublic | BindingFlags.Static)!.GetParameters()[3].ParameterType;
        object favorite = Enum.Parse(mode, "AreaFavoriteRestock");
        object matching = Enum.Parse(mode, "CurrentContainerMatchingStacks");

        Inventory Chest(params ItemDrop.ItemData[] items)
        {
            Inventory chest = new Inventory("reserve", null, 8, 4);
            chest.GetAllItems().AddRange(items);
            return chest;
        }
        int Amount(Inventory chest, ItemDrop.ItemData item, int needed, object? operation = null) =>
            (int)Call("GetRestockTransferAmount", chest, item, needed, operation ?? favorite)!;
        int Withdraw(Inventory chest, ItemDrop.ItemData item, int needed, int actualLimit = int.MaxValue)
        {
            int moved = Math.Min(Amount(chest, item, needed), actualLimit);
            item.m_stack -= moved;
            if (item.m_stack == 0) chest.GetAllItems().Remove(item);
            return moved;
        }

        var single = Item(1, false);
        Inventory one = Chest(single);
        Check("last item is reserved", Amount(one, single, 50) == 0);
        Check("Take stacks can take the last item", Amount(one, single, 50, matching) == 1);
        setting.BoxedValue = Enum.Parse(toggle, "Off");
        Check("live Off allows full depletion", Amount(one, single, 50) == 1);
        setting.BoxedValue = Enum.Parse(toggle, "On");
        Check("live On restores reserve", Amount(one, single, 50) == 0);

        var five = Item(5, false);
        Inventory stock = Chest(five);
        Check("partial refill takes only needed quantity", Amount(stock, five, 2) == 2);
        Check("no shortage requests no transfer", Amount(stock, five, 0) == 0);
        Check("single stack supplies all but one", Withdraw(stock, five, 50) == 4 && five.m_stack == 1);

        var first = Item(2, false);
        var last = Item(3, false);
        stock = Chest(first, last);
        Check("earlier stack may be fully consumed", Withdraw(stock, last, 50) == 3 && !stock.GetAllItems().Contains(last));
        Check("next favorite sees one reserve across all stacks", Withdraw(stock, first, 50) == 1 && first.m_stack == 1);
        Check("further favorite cannot consume reserve", Withdraw(stock, first, 50) == 0);
        var secondChestItem = Item(2, false);
        Inventory secondChest = Chest(secondChestItem);
        Check("second chest keeps its own reserve", Withdraw(secondChest, secondChestItem, 50) == 1 && first.m_stack == 1 && secondChestItem.m_stack == 1);

        var source = Item(5, false);
        var other = Item(1, false);
        other.m_shared.m_name = "different-item";
        stock = Chest(source, other);
        Check("different item does not preserve source routing", Amount(stock, source, 50) == 4);
        other.m_shared.m_name = "TEST-STONE";
        other.m_quality = 2;
        other.m_customData["external"] = "keep";
        Check("routing uses case-insensitive name independently of merge identity", Amount(stock, source, 50) == 5);
        other.m_stack = 0;
        Check("zero stack does not count as a reserve", Amount(stock, source, 50) == 4);
        other.m_stack = 1;
        other.m_shared = null!;
        Check("invalid item does not count as a reserve", Amount(stock, source, 50) == 4);

        stock = Chest(source);
        Check("failed movement leaves full stock available", Withdraw(stock, source, 50, 0) == 0 && Amount(stock, source, 50) == 4);
        Check("partial movement uses actual remaining stock", Withdraw(stock, source, 50, 2) == 2 && Amount(stock, source, 50) == 2);
        Check("partial retry retains exactly one", Withdraw(stock, source, 50) == 2 && source.m_stack == 1);
        var added = Item(1, false);
        stock.GetAllItems().Add(added);
        Check("new live stack allows old reserve to move", Withdraw(stock, source, 50) == 1 && Amount(stock, added, 50) == 0);
        setting.BoxedValue = Enum.Parse(toggle, "Off");
        Check("turning Off releases final reserve", Withdraw(stock, added, 50) == 1 && stock.GetAllItems().Count == 0);
    }

    private static ItemDrop.ItemData Item(int stack, bool cheated) => new ItemDrop.ItemData
    {
        m_stack = stack,
        m_cheated = cheated,
        m_shared = new ItemDrop.ItemData.SharedData { m_name = "test-stone", m_maxStackSize = 50 },
        m_customData = new Dictionary<string, string>()
    };
}
