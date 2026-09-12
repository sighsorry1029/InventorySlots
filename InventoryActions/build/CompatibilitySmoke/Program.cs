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
        if (args.Length != 3 && (args.Length != 4 || (args[3] != "--button-offsets" && args[3] != "--ui-layout")))
            throw new ArgumentException("Usage: <final InventoryActions.dll> <original Managed> <BepInEx core> [--ui-layout]");
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
            // Supply only BepInEx's managed dispatch queue. ServerSync may enqueue
            // startup work, but this harness never executes it or constructs Unity objects.
            Type threading = Assembly.LoadFrom(Path.Combine(roots[2], "BepInEx.dll")).GetType("BepInEx.ThreadingHelper", true)!;
            object queue = FormatterServices.GetUninitializedObject(threading);
            threading.GetField("_invokeLock", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(queue, new object());
            threading.GetField("<Instance>k__BackingField", BindingFlags.NonPublic | BindingFlags.Static)!.SetValue(null, queue);
            plugin = Assembly.LoadFrom(Path.GetFullPath(args[0])).GetType("InventoryActions.InventoryActionsPlugin", true)!;
            if (args.Length == 4) RunUiLayoutChecks();
            else Run();
            System.Console.WriteLine($"PASS {checks} isolated checks against actual mod and original game assemblies. CLR {Environment.Version}; no Unity/game execution.");
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
        Type toggle = plugin.GetNestedType("Toggle")!;
        MethodInfo bind = typeof(ConfigFile).GetMethods().Single(m => m.Name == "Bind" && m.IsGenericMethod &&
            m.GetParameters().Length == 4 && m.GetParameters()[0].ParameterType == typeof(string) &&
            m.GetParameters()[3].ParameterType == typeof(ConfigDescription)).MakeGenericMethod(toggle);
        ConfigEntryBase restock = (ConfigEntryBase)bind.Invoke(config, new object[] { "test", "restock", Enum.Parse(toggle, "On"), new ConfigDescription("") })!;
        ConfigEntryBase exclude = (ConfigEntryBase)bind.Invoke(config, new object[] { "test", "exclude", Enum.Parse(toggle, "On"), new ConfigDescription("") })!;
        plugin.GetField("_showRestockRulesButton", BindingFlags.NonPublic | BindingFlags.Static)!.SetValue(null, restock);
        plugin.GetField("_showAutoPickupRulesButton", BindingFlags.NonPublic | BindingFlags.Static)!.SetValue(null, exclude);
        foreach (bool showRestock in new[] { false, true })
        foreach (bool showExclude in new[] { false, true })
        {
            restock.BoxedValue = Enum.Parse(toggle, showRestock ? "On" : "Off");
            exclude.BoxedValue = Enum.Parse(toggle, showExclude ? "On" : "Off");
            string state = $"restock {showRestock}, exclude {showExclude}";
            Check(state + ": restock visibility", (bool)Call("IsItemRuleButtonEnabled", true)! == showRestock);
            Check(state + ": exclude visibility", (bool)Call("IsItemRuleButtonEnabled", false)! == showExclude);
            Check(state + ": restock uses column " + (showExclude ? 6 : 7), (int)Call("GetItemRuleColumnsFromRight", true)! == (showExclude ? 2 : 1));
            Check(state + ": exclude reserves column 7", (int)Call("GetItemRuleColumnsFromRight", false)! == 1);
        }

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

    private static ItemDrop.ItemData Item(int stack, bool cheated) => new ItemDrop.ItemData
    {
        m_stack = stack,
        m_cheated = cheated,
        m_shared = new ItemDrop.ItemData.SharedData { m_name = "test-stone", m_maxStackSize = 50 },
        m_customData = new Dictionary<string, string>()
    };
}
