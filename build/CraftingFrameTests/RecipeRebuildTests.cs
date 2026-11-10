using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

internal static class RecipeRebuildTests
{
    internal static void Run()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root != null && !File.Exists(Path.Combine(root.FullName, "CraftingVanillaBridge.cs"))) root = root.Parent;
        if (root == null) throw new DirectoryNotFoundException("InventorySlots checkout");
        SyntaxNode Read(string file) => CSharpSyntaxTree.ParseText(File.ReadAllText(Path.Combine(root.FullName, file))).GetRoot();
        MethodDeclarationSyntax Method(string file, string name) => Read(file).DescendantNodes()
            .OfType<MethodDeclarationSyntax>().Single(m => m.Identifier.ValueText == name);
        string members = "";
        foreach (string name in new[] { "PrepareCraftingRecipeListRebuild", "RestoreFailedCraftingRecipeListRebuild",
                     "EnsureCraftingVanillaRecipeElementsHidden", "SetCraftingVanillaRecipeListRootVisible",
                     "SetCraftingVanillaRecipeElementsVisible", "InvalidateCraftingVanillaHiddenState" })
            members += Method("CraftingVanillaBridge.cs", name).ToFullString();
        foreach (string name in new[] { "HideCraftingPanelRedesign", "RestoreCraftingVanillaState",
                     "ForceRestoreForeignCraftingVanillaState", "HasActiveForeignCraftingTab", "IsCraftingTabLocallyActive" })
            members += Method("CraftingRedesign.cs", name).ToFullString();
        foreach (string name in new[] { "GetCraftingRecipeBigGroupId", "GetItemPredefinedGroupRank" })
            members += Method("CraftingRecipeView.cs", name).ToFullString();
        var order = Method("CraftingRecipeView.cs", "GetPredefinedGroupOrderForCraftingGroup");
        members += order.WithBody(order.Body!.WithStatements(order.Body.Statements.Insert(0,
            SyntaxFactory.ParseStatement("OrderBuilds++;")))).ToFullString();
        // Keep the original precedence guard as an independent behavioral oracle.
        members += Method("CraftingRecipeView.cs", "IsFirstMatchingCraftingGroup").ToFullString();
        var finalizer = Read("CraftingPatches.cs").DescendantNodes().OfType<ClassDeclarationSyntax>()
            .Single(c => c.Identifier.ValueText == "InventoryGuiJewelcraftingRecipeListPrimaryTabPreflightPatch")
            .Members.OfType<MethodDeclarationSyntax>().Single(m => m.Identifier.ValueText == "Finalizer");
        members += finalizer.WithAttributeLists(default).ToFullString();
        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator)
            .Select(path => MetadataReference.CreateFromFile(path));
        var compilation = CSharpCompilation.Create("RecipeRebuildRegression", new[] { CSharpSyntaxTree.ParseText(Host.Replace("/* PRODUCTION */", members)) }, references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));
        using var stream = new MemoryStream();
        var result = compilation.Emit(stream);
        if (!result.Success) throw new Exception(string.Join("\n", result.Diagnostics));
        try { Assembly.Load(stream.ToArray()).GetType("InventorySlotsPlugin")!.GetMethod("Check")!.Invoke(null, null); }
        catch (TargetInvocationException error) { throw error.InnerException!; }
    }

    // Counts activation boundaries; this does not simulate Unity native rendering.
    private const string Host = """
using System;
using System.Linq;
using System.Collections.Generic;
public static class InventorySlotsPlugin
{
    /* PRODUCTION */
    static bool IsDedicatedServer, Primary = true, Salvage, Jewel, Reclaim, Foreign, Preserve;
    static bool _craftingVanillaRecipeElementsHidden, _craftingVanillaDetailHidden,
        _craftingVanillaRecipeScrollbarsHidden, _craftingVanillaPanelBackgroundsHidden, _craftingRedesignApplied;
    static List<int> CraftingVanillaPanelBackgroundStates = new();
    static int Checks, Details, Scrollbars, Backgrounds, OrderBuilds;
    static class Log { public static int Warnings; public static void LogWarning(string text) => Warnings++; }
    static bool IsUnityNull(object? value) => value == null;
    static bool IsCraftingPrimaryTabSelected(InventoryGui gui) => Primary;
    static bool IsPathOfValheimanSalvageTab(InventoryGui gui) => Salvage;
    static bool IsJewelcraftingCraftingRedesignTab(InventoryGui gui) => Jewel;
    static bool IsRecycleNReclaimReclaimTabActive(InventoryGui gui) => Reclaim;
    static bool IsJewelcraftingSocketTabButton(Button b) => b.Jewel;
    struct CraftingTabAdapterState { public bool IsForeign; }
    static CraftingTabAdapterState GetCraftingTabAdapterState(InventoryGui? gui) => new() { IsForeign = Foreign };
    static bool ShouldPreserveForeignCraftingControls(InventoryGui gui) => Preserve;
    static void HideOwnedCraftingRedesignUi() { }
    static void UpdateMyLittleUICraftingObjectSuppression(InventoryGui? gui, bool shouldSuppress, CraftingTabAdapterState adapter) { }
    static void RestoreCraftingPanelHeightExtension(InventoryGui gui) { }
    static void RestoreCraftingStationLevelLayout(InventoryGui gui) { }
    static void RestoreJewelcraftingCraftingSocketUiForVanilla(InventoryGui gui) { }
    static void HideCraftingRedesignBottomControls(InventoryGui gui) { }
    static void SetCraftingVanillaDetailVisible(InventoryGui gui, bool visible) => Details++;
    static void SetCraftingVanillaRecipeScrollbarsVisible(InventoryGui gui, bool visible) => Scrollbars++;
    static void SetCraftingVanillaPanelBackgroundsVisible(InventoryGui gui, bool visible) {
        Backgrounds++; _craftingVanillaPanelBackgroundsHidden = false; CraftingVanillaPanelBackgroundStates.Clear();
    }
    static class CraftingController { public static void ClearHoveredRecipe() { } public static void ResetRecipeListChangeSignature() { } }
    static class CraftingRecipes {
        public static List<int> View = new(), ViewIndexByOriginal = new();
        public static Dictionary<CraftingRecipePairCacheKey, Dictionary<string, bool>> GroupMatchCache = new();
    }
    static void InvalidateCraftingRecipeView() { }
    static void ClearCraftingQueue() { }
    static List<CraftingRecipeGroupFilter> CraftingRecipeGroupFilters = new();
    static Dictionary<string, List<string>> PredefinedGroupOrders = new();
    static string NormalizeGroupId(string value) => value.ToLowerInvariant();
    static ItemData? GetCraftingRecipeItemData(InventoryGui.RecipeDataPair pair) => pair.Item;
    record struct CraftingRecipePairCacheKey(int Id) { public bool IsValid => Id > 0; }
    static CraftingRecipePairCacheKey GetCraftingRecipePairCacheKey(InventoryGui.RecipeDataPair pair) => new(pair.Item?.Id ?? 0);
    static bool ItemMatchesPredefinedGroup(ItemData item, string group) => item.Groups.Contains(group);
    static void Expect(bool ok, string label) { if (!ok) throw new Exception(label); Checks++; }
    static InventoryGui Fresh() {
        IsDedicatedServer = Salvage = Jewel = Reclaim = Foreign = Preserve = false;
        Primary = true; Player.m_localPlayer = new();
        _craftingVanillaRecipeElementsHidden = _craftingVanillaDetailHidden =
            _craftingVanillaRecipeScrollbarsHidden = _craftingVanillaPanelBackgroundsHidden = false;
        var gui = new InventoryGui(); InventoryGui.instance = gui;
        return gui;
    }
    static void BuildRows(InventoryGui gui, int count) {
        gui.m_availableRecipes.Clear();
        for (int i=0; i<count; i++) gui.m_availableRecipes.Add(new() { InterfaceElement = new GameObject(gui.m_recipeListRoot!.gameObject), Item = new() });
    }
    public static void Check() {
        var gui = Fresh();
        // Show calls SetupCrafting before the animator activates the inventory.
        gui.m_inventoryRoot!.gameObject.SetActive(false);
        var changed = PrepareCraftingRecipeListRebuild(gui);
        Expect(changed != null && !changed.activeSelf && !_craftingVanillaRecipeElementsHidden, "Preflight hides only root, not future rows");
        BuildRows(gui, 1801);
        gui.m_inventoryRoot.gameObject.SetActive(true);
        Expect(gui.m_availableRecipes.Count == 1801 && gui.m_availableRecipes.All(p => !p.InterfaceElement!.activeInHierarchy), "Native data retained; first render cannot activate native rows");
        InvalidateCraftingVanillaHiddenState(); EnsureCraftingVanillaRecipeElementsHidden(gui);
        Expect(gui.m_availableRecipes.All(p => !p.InterfaceElement!.activeSelf), "Managed redraw suppresses newly created rows");
        int calls = gui.m_availableRecipes.Sum(p => p.InterfaceElement!.SetCalls);
        gui.m_inventoryRoot.gameObject.SetActive(false);
        _craftingVanillaDetailHidden = _craftingVanillaRecipeScrollbarsHidden = _craftingVanillaPanelBackgroundsHidden = true;
        HideCraftingPanelRedesign();
        Expect(_craftingVanillaRecipeElementsHidden && !gui.m_recipeListRoot!.gameObject.activeSelf &&
            calls == gui.m_availableRecipes.Sum(p => p.InterfaceElement!.SetCalls), "Closed cleanup leaves 1801 rows untouched");
        Expect(Details == 1 && Scrollbars == 1 && Backgrounds == 1, "Other native state still restores on close");
        Expect(PrepareCraftingRecipeListRebuild(gui) == null, "Reopen does not claim an already hidden root");
        BuildRows(gui, 20); InvalidateCraftingVanillaHiddenState(); EnsureCraftingVanillaRecipeElementsHidden(gui);
        Expect(gui.m_availableRecipes.All(p => !p.InterfaceElement!.activeSelf), "Reopen handles replacement rows");
        Primary = false;
        Expect(PrepareCraftingRecipeListRebuild(gui) == null && !_craftingVanillaRecipeElementsHidden &&
            gui.m_recipeListRoot.gameObject.activeSelf && gui.m_availableRecipes.All(p => p.InterfaceElement!.activeSelf), "Foreign preflight restores previous suppression before its rebuild");
        foreach (string tab in new[] { "salvage", "jewel", "reclaim" }) {
            gui = Fresh(); Salvage = tab == "salvage"; Jewel = tab == "jewel"; Reclaim = tab == "reclaim";
            Expect(PrepareCraftingRecipeListRebuild(gui) == null && gui.m_recipeListRoot!.gameObject.activeSelf, "Special adapter is not pre-hidden: " + tab);
        }
        gui = Fresh(); gui.m_inventoryRoot!.gameObject.SetActive(false);
        var foreignTab = new Button(gui.TabRoot) { interactable = false };
        Expect(!HasActiveForeignCraftingTab(gui) && HasActiveForeignCraftingTab(gui, true), "Local tab selection works during closed Show hierarchy");
        Expect(PrepareCraftingRecipeListRebuild(gui) == null, "Selected foreign tab wins over stale native selection");
        foreignTab.gameObject.SetActive(false);
        Expect(!HasActiveForeignCraftingTab(gui, true), "Inactive foreign tab is ignored");
        foreignTab.gameObject.SetActive(true); foreignTab.Jewel = true;
        Expect(!HasActiveForeignCraftingTab(gui, true), "Existing stale Jewelcrafting selection exception remains");
        gui = Fresh(); BuildRows(gui, 2); EnsureCraftingVanillaRecipeElementsHidden(gui);
        HideCraftingPanelRedesign();
        Expect(gui.m_recipeListRoot!.gameObject.activeSelf && !_craftingVanillaRecipeElementsHidden, "Visible native handoff restores rows");
        gui = Fresh(); changed = PrepareCraftingRecipeListRebuild(gui);
        var error = new InvalidOperationException("original");
        Expect(ReferenceEquals(Finalizer(gui, changed, error), error) && changed!.activeSelf, "Failure restores exact root and preserves exception");
        gui = Fresh(); changed = PrepareCraftingRecipeListRebuild(gui);
        gui.m_recipeListRoot = new Transform(new GameObject());
        Finalizer(gui, changed, error);
        Expect(!changed!.activeSelf, "Replacement root is not touched");
        gui = Fresh(); changed = PrepareCraftingRecipeListRebuild(gui); InventoryGui.instance = new();
        Finalizer(gui, changed, error);
        Expect(!changed!.activeSelf, "Replacement GUI is not touched");
        gui = Fresh(); gui.m_recipeListRoot!.gameObject.SetActive(false);
        changed = PrepareCraftingRecipeListRebuild(gui); Finalizer(gui, changed, error);
        Expect(!gui.m_recipeListRoot.gameObject.activeSelf, "Originally hidden root stays hidden on failure");
        gui = Fresh(); changed = PrepareCraftingRecipeListRebuild(gui); changed!.ThrowOnSet = true;
        Expect(ReferenceEquals(Finalizer(gui, changed, error), error) && Log.Warnings == 1, "Cleanup failure cannot replace original exception");
        gui = Fresh(); IsDedicatedServer = true;
        Expect(PrepareCraftingRecipeListRebuild(gui) == null, "No dedicated server UI work");
        IsDedicatedServer = false; Player.m_localPlayer = null;
        Expect(PrepareCraftingRecipeListRebuild(gui) == null, "No UI mutation without local player");

        CraftingRecipeGroupFilters.AddRange(new[] {
            new CraftingRecipeGroupFilter("favorite", _ => true),
            new CraftingRecipeGroupFilter("equipment", item => item.Groups.Contains("weapon")),
            new CraftingRecipeGroupFilter("food", item => item.Groups.Contains("food")),
            new CraftingRecipeGroupFilter("other", item => item.Groups.Contains("other")) });
        for (int bits = 0; bits < 8; bits++) {
            var item = new ItemData();
            for(int i=0; i<3; i++) if ((bits & (1<<i)) != 0) item.Groups.Add(new[] { "weapon", "food", "other" }[i]);
            string expected = CraftingRecipeGroupFilters.Where(f => f.Id != "favorite")
                .FirstOrDefault(f => f.Matches(item) && IsFirstMatchingCraftingGroup(item, f))?.Id ?? "";
            Expect(GetCraftingRecipeBigGroupId(new() { Item = item }) == expected, "Group precedence including overlapping custom matches");
        }
        Expect(GetCraftingRecipeBigGroupId(new()) == "", "Missing recipe item remains unclassified");
        var overlap = new ItemData(); overlap.Groups.UnionWith(new[] { "weapon", "food", "other" });
        var pair = new InventoryGui.RecipeDataPair { Item = overlap };
        var matches = new Dictionary<string, bool> { ["equipment"] = false };
        CraftingRecipes.GroupMatchCache[GetCraftingRecipePairCacheKey(pair)] = matches;
        Expect(GetCraftingRecipeBigGroupId(pair) == "food", "Partial cache preserves prior false and resolves next missing match");
        matches.Clear(); matches["food"] = true;
        Expect(GetCraftingRecipeBigGroupId(pair) == "equipment", "Earlier uncached match wins over later cached match");
        matches["equipment"] = false; matches["food"] = false; matches["other"] = true;
        Expect(GetCraftingRecipeBigGroupId(pair) == "other" && matches.Count == 3, "Warm result preserved without cache writes");
        overlap.Id = 0;
        Expect(GetCraftingRecipeBigGroupId(pair) == "equipment", "Invalid cache key uses direct first match");
        PredefinedGroupOrders["food"] = new() { "custom", "food", "custom" };
        PredefinedGroupOrders["global"] = new() { "food", "other" };
        var orders = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        var food = new ItemData(); food.Groups.UnionWith(new[] { "custom", "food" });
        for(int i=0; i<1801; i++) GetItemPredefinedGroupRank(food, "food", 2, orders);
        Expect(OrderBuilds == 1 && orders.Count == 1 && orders["food"].SequenceEqual(new[] { "custom", "food", "other" }), "1801 recipes build one order list; section precedes global");
        Expect(GetItemPredefinedGroupRank(food, "food", 2, orders) == 0, "Custom group remains first");
        PredefinedGroupOrders["food"] = new() { "other", "food", "custom" };
        var next = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        Expect(GetItemPredefinedGroupRank(food, "food", 2, next) == 1, "Next rebuild observes changed YAML order");
        Expect(GetItemPredefinedGroupRank(food, "food", 2) == 1, "Inventory sort retains uncached path");
        Expect(GetItemPredefinedGroupRank(new(), "unknown", 4, next) == 1004, "Unmatched fallback rank retained");
        Expect(GetItemPredefinedGroupRank(null, "food", 2, next) == int.MaxValue, "Missing item fallback retained");
        Console.WriteLine($"Recipe rebuild lifecycle and grouping: {Checks} checks passed.");
    }
}
public class ItemData { static int Next; public int Id = ++Next; public object m_shared = new(); public HashSet<string> Groups = new(); }
public class CraftingRecipeGroupFilter {
    public string Id; public Func<ItemData, bool> Matches;
    public CraftingRecipeGroupFilter(string id, Func<ItemData,bool> matches) { Id=id; Matches=matches; }
}
public class Player { public static Player? m_localPlayer = new(); }
public class InventoryGui {
    public static InventoryGui? instance;
    public Transform? m_recipeListRoot, m_inventoryRoot, m_crafting;
    public Transform TabRoot;
    public Button m_tabCraft, m_tabUpgrade;
    public List<RecipeDataPair> m_availableRecipes = new();
    public InventoryGui() {
        m_inventoryRoot = new(new GameObject()); m_crafting = new(new GameObject(m_inventoryRoot.gameObject));
        m_recipeListRoot = new(new GameObject(m_inventoryRoot.gameObject));
        TabRoot = new(new GameObject(m_inventoryRoot.gameObject));
        m_tabCraft = new(TabRoot); m_tabUpgrade = new(TabRoot);
    }
    public struct RecipeDataPair { public GameObject? InterfaceElement; public ItemData? Item; }
}
public class GameObject {
    public bool activeSelf = true, ThrowOnSet;
    public int SetCalls;
    public GameObject? Parent;
    public GameObject(GameObject? parent = null) { Parent = parent; }
    public bool activeInHierarchy => activeSelf && (Parent?.activeInHierarchy ?? true);
    public void SetActive(bool value) { if (ThrowOnSet) throw new Exception("destroyed"); SetCalls++; activeSelf=value; }
}
public class Transform {
    public GameObject gameObject; public Transform? parent;
    public List<Button> Buttons = new();
    public Transform(GameObject obj) { gameObject=obj; }
    public T[] GetComponentsInChildren<T>(bool includeInactive) => Buttons.Cast<T>().ToArray();
}
public class Button {
    public Transform transform; public GameObject gameObject => transform.gameObject;
    public bool interactable = true, Jewel;
    public Button(Transform parent) { transform=new(new GameObject(parent.gameObject)) { parent=parent }; parent.Buttons.Add(this); }
}
""";
}
