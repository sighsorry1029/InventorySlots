using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

internal static class CraftingQueueTests
{
    internal static void Run()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root != null && !File.Exists(Path.Combine(root.FullName, "CraftingRedesign.cs"))) root = root.Parent;
        if (root == null) throw new DirectoryNotFoundException("InventorySlots checkout");
        SyntaxNode Read(string file) => CSharpSyntaxTree.ParseText(File.ReadAllText(Path.Combine(root.FullName, file))).GetRoot();
        string Methods(string file, params string[] names) => string.Join("\n", names.Select(name =>
            Read(file).DescendantNodes().OfType<MethodDeclarationSyntax>().Single(m => m.Identifier.ValueText == name).ToFullString()));
        var state = Read("CraftingState.cs").DescendantNodes().OfType<ClassDeclarationSyntax>()
            .Single(c => c.Identifier.ValueText == "CraftingQueueRuntimeState");
        string source = "using System; using System.Collections.Generic; using ItemData = ItemDrop.ItemData;\n" + Host
            .Replace("/* STATE */", state.ToFullString())
            .Replace("/* METHODS */", Methods("CraftingRedesign.cs", "TryContinueCraftingQueue", "ClearCraftingQueue", "ClearCraftingProgressLabelCount") +
                Methods("CraftingRecipeActions.cs", "CanContinueCraftingQueue", "BeginCraftingInventoryLimitNotice", "EndCraftingInventoryLimitNotice"));
        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator)
            .Select(path => MetadataReference.CreateFromFile(path));
        var compilation = CSharpCompilation.Create("CraftingQueueRegression", new[] { CSharpSyntaxTree.ParseText(source) }, references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));
        using var stream = new MemoryStream();
        var result = compilation.Emit(stream);
        if (!result.Success) throw new Exception(string.Join("\n", result.Diagnostics));
        try { Assembly.Load(stream.ToArray()).GetType("QueueHost")!.GetMethod("Check")!.Invoke(null, null); }
        catch (TargetInvocationException error) { throw error.InnerException!; }
    }

    // Run production queue/notice methods. Boundary doubles inject native-style
    // missing-ingredient failures and external-patch failures, not Unity rendering
    // or actual resource consumption. In particular, selecting an ingredient is
    // counted because chest integrations can reserve resources on that call.
    private const string Host = """
public static class QueueHost
{
    /* STATE */
    /* METHODS */
    private static CraftingQueueRuntimeState CraftingQueue = new();
    private static readonly TestLog Log = new();
    private static int Checks;
    private static void ShowInventoryLimitReached(Player player, ItemData item, int maxAllowed, bool force) { }
    private static void Expect(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
        Checks++;
    }
    private static InventoryGui Begin(bool single = false)
    {
        Player.m_localPlayer = new Player();
        ZoneSystem.instance = new ZoneSystem();
        Log.Messages.Clear();
        var recipe = new Recipe { m_requireOnlyOneIngredient = single };
        CraftingQueue = new CraftingQueueRuntimeState { QueueRecipe = recipe, QueueRemaining = 65, QueueTotal = 66,
            ProgressLabelRecipe = recipe, ProgressLabelCount = 66 };
        return new InventoryGui { m_selectedRecipe = new InventoryGui.Pair { Recipe = recipe } };
    }
    private static void ExpectStopped(InventoryGui gui, int warnings)
    {
        Expect(CraftingQueue.QueueRemaining == 0 && CraftingQueue.QueueTotal == 0 && CraftingQueue.QueueRecipe == null,
            "Remaining queue must be cleared");
        Expect(CraftingQueue.ProgressLabelCount == 0 && CraftingQueue.ProgressLabelRecipe == null,
            "Stopped queue must not leave a stale count label");
        Expect(!CraftingQueue.ContinuingQueue && gui.m_multiCraftAmount == 66, "Temporary start state must be restored");
        Expect(CraftingQueue.InventoryLimitNoticeDepth == 0, "Capacity notice scope must be balanced");
        int requirements = Player.m_localPlayer.RequirementCalls, amount = gui.m_selectedRecipe.Recipe.AmountCalls, starts = gui.Starts;
        for (int i = 0; i < 10; i++) TryContinueCraftingQueue(gui);
        Expect(requirements == Player.m_localPlayer.RequirementCalls && amount == gui.m_selectedRecipe.Recipe.AmountCalls && starts == gui.Starts,
            "Later UI ticks must not retry a stopped queue");
        Expect(Log.Messages.Count == warnings, "Only the failed queue attempt should warn; material shortage is normal");
    }
    public static void Check()
    {
        foreach (bool single in new[] { false, true })
        {
            var gui = Begin(single);
            Player.m_localPlayer.InventoryItems = 0;
            TryContinueCraftingQueue(gui);
            Expect(gui.m_selectedRecipe.Recipe.AmountCalls == 0 && gui.Starts == 0,
                "Exhausted materials must stop before output calculation, including one-ingredient recipes");
            ExpectStopped(gui, 0);
        }

        var batch = Begin(true);
        Player.m_localPlayer.InventoryItems = 2;
        for (int i = 0; i < 2; i++)
        {
            TryContinueCraftingQueue(batch);
            Expect(batch.Starts == i + 1 && CraftingQueue.QueueRemaining == 64 - i && batch.m_craftTimer == 0,
                "An affordable step starts exactly once and decrements the remaining queue");
            TryContinueCraftingQueue(batch);
            Expect(batch.Starts == i + 1, "An active craft must not start another step");
            // Model the game completing a craft and consuming its last input.
            Player.m_localPlayer.InventoryItems--;
            batch.m_craftTimer = -1;
        }
        TryContinueCraftingQueue(batch);
        Expect(batch.m_selectedRecipe.Recipe.AmountCalls == 2, "Excess requested count must not calculate a third, unaffordable step");
        ExpectStopped(batch, 0);

        var chest = Begin(true);
        Player.m_localPlayer.InventoryItems = 0;
        Player.m_localPlayer.ChestItems = 1;
        chest.m_selectedRecipe.Recipe.OutputAmount = 7;
        TryContinueCraftingQueue(chest);
        Expect(chest.Starts == 1 && chest.m_selectedRecipe.Recipe.IngredientSelections == 1,
            "Queue preflight may use chest material without an extra ingredient-selection probe");
        Expect(Player.m_localPlayer.Inventory.LastAmount == 7, "Capacity must use the patched output amount");

        foreach (bool global in new[] { false, true })
        {
            var free = Begin();
            Player.m_localPlayer.InventoryItems = 0;
            Player.m_localPlayer.NoCost = !global;
            ZoneSystem.instance.NoCost = global;
            Player.m_localPlayer.RequirementsAllowed = false;
            TryContinueCraftingQueue(free);
            Expect(free.Starts == 1 && Player.m_localPlayer.RequirementCalls == 0, "No-cost ordinary crafting must retain its requirements bypass");

            free = Begin(true);
            Player.m_localPlayer.NoCost = !global;
            ZoneSystem.instance.NoCost = global;
            Player.m_localPlayer.RequirementsAllowed = false;
            TryContinueCraftingQueue(free);
            Expect(free.Starts == 1 && Player.m_localPlayer.RequirementCalls == 0, "No-cost with an actual ingredient must not add station/DLC requirements");

            free = Begin(true);
            Player.m_localPlayer.InventoryItems = 0;
            Player.m_localPlayer.NoCost = !global;
            ZoneSystem.instance.NoCost = global;
            TryContinueCraftingQueue(free);
            ExpectStopped(free, 1);
        }

        foreach (string stage in new[] { "requirements", "amount", "capacity", "start", "started" })
        {
            var gui = Begin(true);
            var failure = new InvalidOperationException("Injected " + stage);
            if (stage == "requirements")
            {
                Player.m_localPlayer.Failure = failure;
                gui.m_multiCrafting = true;
            }
            if (stage == "amount") gui.m_selectedRecipe.Recipe.Failure = failure;
            if (stage == "capacity") Player.m_localPlayer.Inventory.Failure = failure;
            if (stage == "start" || stage == "started") gui.OnStart = () =>
            {
                Expect(gui.m_multiCraftAmount == 1 && CraftingQueue.ContinuingQueue, "Temporary state must cover start callbacks");
                gui.m_multiCrafting = true;
                if (stage == "started") gui.m_craftTimer = 0;
                throw failure;
            };
            TryContinueCraftingQueue(gui);
            ExpectStopped(gui, 1);
            Expect(Log.Messages[0].Contains(failure.ToString()), "Unexpected failures must retain diagnostic exception detail");
            Expect(gui.m_multiCrafting == (stage == "requirements"), "Only a start entered by this queue may clear native multicrafting on failure");
            Expect(gui.m_craftTimer == (stage == "started" ? 0 : -1), "Queue cleanup must not cancel or restart a craft already started");
            Expect(Player.m_localPlayer.InventoryItems == 1, "Queue failure must not synthesize consumption or refunds");
            // A new manual request is allowed after the failed queue is cleared.
            Player.m_localPlayer.Failure = null;
            Player.m_localPlayer.Inventory.Failure = null;
            gui.m_selectedRecipe.Recipe.Failure = null;
            gui.OnStart = null;
            gui.m_craftTimer = -1;
            CraftingQueue.QueueRecipe = gui.m_selectedRecipe.Recipe;
            CraftingQueue.QueueRemaining = 1;
            int before = gui.Starts;
            TryContinueCraftingQueue(gui);
            Expect(gui.Starts == before + 1, "Failure must not latch future manual queues off");
        }

        var declined = Begin();
        declined.OnStart = () => { };
        TryContinueCraftingQueue(declined);
        ExpectStopped(declined, 0);

        var full = Begin();
        full.m_multiCrafting = true;
        Player.m_localPlayer.Inventory.HasSpace = false;
        TryContinueCraftingQueue(full);
        Expect(full.Starts == 0, "Full inventory must not start a queued craft");
        Expect(full.m_multiCrafting, "A declined preflight must not alter native state before this queue starts a craft");
        ExpectStopped(full, 0);

        var changed = Begin();
        changed.m_selectedRecipe.ItemData = new ItemData();
        TryContinueCraftingQueue(changed);
        Expect(changed.Starts == 0 && changed.m_selectedRecipe.Recipe.AmountCalls == 0, "Switching to an upgrade cancels the creation queue");
        ExpectStopped(changed, 0);

        var reentrant = Begin();
        reentrant.OnStart = () => { TryContinueCraftingQueue(reentrant); reentrant.m_craftTimer = 0; };
        TryContinueCraftingQueue(reentrant);
        Expect(reentrant.Starts == 1 && CraftingQueue.QueueRemaining == 64, "Reentrant start callbacks must not consume two queue entries");
        Console.WriteLine($"Crafting queue: {Checks} checks passed (production methods; managed boundary doubles, no game execution).");
    }
}
public sealed class TestLog
{
    public readonly List<string> Messages = new();
    public void LogWarning(object message) => Messages.Add(message.ToString()!);
}
public class GameObject { }
public class Component { public GameObject gameObject = new(); }
public class ItemDrop : Component { public class ItemData { public object m_shared = new(); } }
public sealed class Recipe
{
    public bool m_requireOnlyOneIngredient;
    public ItemDrop m_item = new();
    public int AmountCalls, IngredientSelections, OutputAmount = 1;
    public Exception? Failure;
    public int GetAmount(int quality, out int need, out ItemData singleReqItem, int multiplier)
    {
        AmountCalls++;
        need = 0;
        singleReqItem = null!;
        if (Failure != null) throw Failure;
        if (m_requireOnlyOneIngredient)
        {
            IngredientSelections++;
            if (Player.m_localPlayer.InventoryItems + Player.m_localPlayer.ChestItems == 0)
                throw new NullReferenceException("Native Recipe.GetAmount requires an actual selected ingredient");
            singleReqItem = new ItemData();
        }
        return OutputAmount * multiplier;
    }
    public CraftingStation GetRequiredStation(int quality) => null!;
    public int GetRequiredStationLevel(int quality) => 1;
}
public sealed class CraftingStation
{
    public string m_name = "station";
    public int GetLevel() => 1;
    public bool CheckUsable(Player player, bool showMessage) => true;
}
public sealed class Player
{
    public static Player m_localPlayer = null!;
    public int InventoryItems = 1, ChestItems, RequirementCalls;
    public bool NoCost, RequirementsAllowed = true;
    public Exception? Failure;
    public Inventory Inventory = new();
    public bool NoCostCheat() => NoCost;
    public bool HaveRequirements(Recipe recipe, bool discover, int quality, int amount)
    {
        RequirementCalls++;
        if (Failure != null) throw Failure;
        return RequirementsAllowed && InventoryItems + ChestItems >= amount;
    }
    public Inventory GetInventory() => Inventory;
    public CraftingStation GetCurrentCraftingStation() => null!;
}
public sealed class Inventory
{
    public bool HasSpace = true;
    public int LastAmount;
    public Exception? Failure;
    public bool CanAddItem(GameObject item, int amount)
    {
        LastAmount = amount;
        if (Failure != null) throw Failure;
        return HasSpace;
    }
}
public enum GlobalKeys { NoCraftCost }
public sealed class ZoneSystem
{
    public static ZoneSystem instance = null!;
    public bool NoCost;
    public bool GetGlobalKey(GlobalKeys key) => NoCost;
}
public sealed class InventoryGui
{
    public sealed class Pair { public Recipe Recipe = null!; public ItemData? ItemData; }
    public Pair m_selectedRecipe = new();
    public int m_selectedVariant, m_multiCraftAmount = 66, Starts;
    public float m_craftTimer = -1;
    public bool m_multiCrafting;
    public Action? OnStart;
    public void OnCraftPressed()
    {
        Starts++;
        if (OnStart != null) OnStart();
        else { m_craftTimer = 0; m_multiCrafting = true; }
    }
}
""";
}
