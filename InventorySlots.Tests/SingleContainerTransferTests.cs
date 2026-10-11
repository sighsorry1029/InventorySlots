using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

internal static class SingleContainerTransferTests
{
    public static void Run()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root != null && !File.Exists(Path.Combine(root.FullName, "ContainerQuickStack.cs"))) root = root.Parent;
        if (root == null) throw new DirectoryNotFoundException("InventorySlots checkout");
        string Method(string file, string name) => CSharpSyntaxTree.ParseText(
            File.ReadAllText(Path.Combine(root.FullName, file))).GetRoot()
            .DescendantNodes().OfType<MethodDeclarationSyntax>().Single(m => m.Identifier.ValueText == name).ToFullString();
        string source = Host.Replace("/* METHODS */",
            Method("ContainerQuickStack.cs", "QuickStackIntoContainer") +
            Method("ContainerRestock.cs", "RestockFromContainer"));
        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator)
            .Select(path => MetadataReference.CreateFromFile(path));
        var compilation = CSharpCompilation.Create("SingleContainerContract", [CSharpSyntaxTree.ParseText(source)], references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        using var stream = new MemoryStream();
        var result = compilation.Emit(stream);
        if (!result.Success) throw new Exception(string.Join("\n", result.Diagnostics));
        try { Assembly.Load(stream.ToArray()).GetType("Host")!.GetMethod("Check")!.Invoke(null, null); }
        catch (TargetInvocationException error) { throw error.InnerException!; }
    }

    // Real entry methods, fake movement/storage/UI boundaries. Tests assert the
    // observable order, cancellation after drag callbacks, and failure propagation.
    // They do not simulate Unity destruction, item movement or network ownership.
    private const string Host = """
using System;
using System.Collections.Generic;
using System.Linq;
public static class Host
{
    public static readonly List<string> Events = new();
    private static readonly Exception Failure = new Exception("callback failed");
    private static string FailAt;
    private static int Moved;
    private enum ContainerTakeStacksMode { CurrentContainerMatchingStacks, AreaFavoriteRestock }
    /* METHODS */
    public static void Record(string name)
    {
        Events.Add(name);
        if (FailAt == name) throw Failure;
    }
    private static bool IsUnityNull(Container chest) => chest.Destroyed;
    private static int CompareGridOrder(int a, int b) => a.CompareTo(b);
    private static bool ShouldQuickStackItem(Player p, Inventory inv, ItemData item) => item.Eligible;
    private static bool ShouldTakeStacksTarget(Player p, Inventory inv, ItemData item, ContainerTakeStacksMode mode)
    {
        if (mode != ContainerTakeStacksMode.CurrentContainerMatchingStacks) throw new Exception("Area policy leaked into direct action");
        return item.Eligible;
    }
    private static int Transfer(Inventory chest, List<ItemData> items)
    {
        if (string.Join(",", items.Select(item => item.m_gridPos)) != "3,1")
            throw new Exception("Source selection or bottom-first processing changed");
        Record("move");
        if (Moved > 0) chest.Changed();
        return Moved;
    }
    private static int QuickStackItemsIntoContainer(Player p, Inventory inv, Inventory chest, List<ItemData> items) => Transfer(chest, items);
    private static int RestockTargetsFromContainer(Inventory inv, Inventory chest, List<ItemData> items, ContainerTakeStacksMode mode)
    {
        if (mode != ContainerTakeStacksMode.CurrentContainerMatchingStacks) throw new Exception("Wrong restock mode");
        return Transfer(chest, items);
    }
    private static void ClearCraftingRequirementAvailabilityCache() => Record("invalidate");
    private static void ShowContainerActionResult(Player player, string token, string fallback, int moved) => Record("result:" + moved);
    public static void Check()
    {
        foreach (bool restock in new[] { false, true })
        foreach (string scenario in new[] { "zero", "partial", "lost-inventory", "destroyed", "no-gui", "move", "chest", "player", "invalidate" })
        {
            Events.Clear();
            FailAt = scenario;
            Moved = scenario == "zero" ? 0 : 3;
            var player = new Player();
            var inv = new Inventory { Name = "player", m_inventory = new List<ItemData>
                { new ItemData(1, true), new ItemData(2, false), new ItemData(3, true) } };
            var chest = new Container();
            InventoryGui.instance = scenario == "no-gui" ? null : new InventoryGui();
            if (InventoryGui.instance != null) InventoryGui.instance.AfterDrag = () =>
            {
                if (scenario == "lost-inventory") chest.m_inventory = null;
                if (scenario == "destroyed") chest.Destroyed = true;
            };
            Exception error = null;
            try
            {
                if (restock) RestockFromContainer(player, inv, chest);
                else QuickStackIntoContainer(player, inv, chest);
            }
            catch (Exception actual) { error = actual; }
            bool failed = new[] { "move", "chest", "player", "invalidate" }.Contains(scenario);
            if (failed ? !ReferenceEquals(error, Failure) : error != null) throw new Exception("Exception contract: " + scenario, error);
            string expected = scenario switch
            {
                "zero" => "drag,move,result:0",
                "lost-inventory" or "destroyed" => "drag,result:0",
                "no-gui" => "move,chest,player,invalidate,result:3",
                "move" => "drag,move",
                "chest" => "drag,move,chest",
                "player" => "drag,move,chest,player",
                "invalidate" => "drag,move,chest,player,invalidate",
                _ => "drag,move,chest,player,invalidate,result:3"
            };
            string actualOrder = string.Join(",", Events);
            if (expected != actualOrder) throw new Exception($"{restock}/{scenario}: expected {expected}, got {actualOrder}");
        }
    }
}
public sealed class Player { }
public sealed class ItemData
{
    public int m_gridPos;
    public bool Eligible;
    public ItemData(int position, bool eligible) { m_gridPos = position; Eligible = eligible; }
}
public sealed class Inventory
{
    public string Name;
    public List<ItemData> m_inventory = new();
    public void Changed() => Host.Record(Name);
}
public sealed class Container
{
    public bool Destroyed;
    public Inventory m_inventory = new Inventory { Name = "chest" };
}
public sealed class InventoryGui
{
    public static InventoryGui instance;
    public Action AfterDrag;
    public void SetupDragItem(object item, object inventory, int amount) { Host.Record("drag"); AfterDrag?.Invoke(); }
}
""";
}
