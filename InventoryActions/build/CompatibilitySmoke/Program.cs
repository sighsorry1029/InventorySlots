using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;

internal static class Program
{
    private static Type plugin = null!;
    private static int checks;

    private static int Main(string[] args)
    {
        if (args.Length != 3) throw new ArgumentException("Usage: <final InventoryActions.dll> <original Managed> <BepInEx core>");
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
            Run();
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
