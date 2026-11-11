using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

internal static class RecipeMetadataWorkTests
{
    internal static void Run()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root != null && !File.Exists(Path.Combine(root.FullName, "ItemClassifier.cs"))) root = root.Parent;
        if (root == null) throw new DirectoryNotFoundException("InventorySlots checkout");
        SyntaxNode Read(string file) => CSharpSyntaxTree.ParseText(File.ReadAllText(Path.Combine(root.FullName, file))).GetRoot();
        string Methods(string file, params string[] names) => string.Concat(Read(file).DescendantNodes()
            .OfType<MethodDeclarationSyntax>().Where(m => names.Contains(m.Identifier.ValueText)).Select(m => m.ToFullString()));
        string members = Methods("ItemClassifier.cs", "MatchToolCategory", "MatchPotionCategory", "GetItemIdentityTokens",
            "EnsureFeastMaterialTokens", "ClearItemClassifierCaches");
        foreach (string name in new[] { "ItemClassification", "ItemClassifierRuntimeState" })
            members += Read("ItemClassifier.cs").DescendantNodes().OfType<ClassDeclarationSyntax>()
                .Single(c => c.Identifier.ValueText == name).ToFullString();
        members += Read("CraftingRecipeView.cs").DescendantNodes().OfType<ClassDeclarationSyntax>()
            .Single(c => c.Identifier.ValueText == "CraftingResourceTierPass").ToFullString();
        members += Methods("CraftingRecipeView.cs", "GetCraftingRecipeResourceTier", "TryGetResourceTier", "GetResourceTokens");
        members += Methods("InventorySlotsConfigCore.cs", "CleanPrefabName", "StripLocalizationToken");
        var normalize = Read("InventorySlotsConfigCore.cs").DescendantNodes().OfType<MethodDeclarationSyntax>()
            .Single(m => m.Identifier.ValueText == "NormalizeResourceToken");
        members += normalize.WithBody(normalize.Body!.WithStatements(normalize.Body.Statements.Insert(0,
            SyntaxFactory.ParseStatement("Normalizations++;")))).ToFullString();
        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator)
            .Select(path => MetadataReference.CreateFromFile(path));
        var compilation = CSharpCompilation.Create("RecipeMetadataWorkRegression",
            new[] { CSharpSyntaxTree.ParseText(Host.Replace("/* PRODUCTION */", members)) }, references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));
        using var stream = new MemoryStream();
        var result = compilation.Emit(stream);
        if (!result.Success) throw new Exception(string.Join("\n", result.Diagnostics));
        try { Assembly.Load(stream.ToArray()).GetType("Host")!.GetMethod("Check")!.Invoke(null, null); }
        catch (TargetInvocationException error) { throw error.InnerException!; }
    }

    // Production methods above run against counted metadata/Unity boundaries.
    // This validates work avoided and policy, not native rendering or frame times.
    private const string Host = """
using System;
using System.Collections.Generic;
using System.Linq;
public static class Host
{
    /* PRODUCTION */
    private static readonly ItemClassifierRuntimeState ItemClassifierRuntime = new();
    private static CraftingResourceTierPass? _craftingResourceTierPass;
    private static readonly Dictionary<string, int> ResourceTierByToken = new(StringComparer.OrdinalIgnoreCase);
    private static int Checks, Expensive, Normalizations, PrefabReads;
    public static int Scans;
    private static bool MatchPrimaryFoodCategory(ItemData item) { Expensive++; return item.Food; }
    private static bool MatchConsumableCategory(ItemData item) { Expensive++; return item.Consumable; }
    private static bool MatchMeadCategory(ItemData item) { Expensive++; return item.Mead; }
    private static bool MatchMeadBaseCategory(ItemData item) { Expensive++; return item.MeadBase; }
    private static bool MatchTorchCategory(ItemData item) => item.Torch;
    private static bool MatchConfiguredCustomGroupInCraftingGroup(ItemData item, string group) => item.CustomTool;
    private static string GetItemPrefabName(ItemData item) { PrefabReads++; return CleanPrefabName(item.Prefab); }
    private static bool IsUnityNull(object? obj) => obj == null;
    private static void AddItemIdentityTokens(HashSet<string> tokens, ItemDrop? item) { if (item != null) tokens.Add(item.name); }
    private static void Expect(bool value, string label) { Checks++; if (!value) throw new Exception(label); }
    private static ItemDrop Material(string name, string prefab = "", string shared = "") =>
        new() { name = name, m_itemData = new() { Prefab = prefab, m_shared = new() { m_name = shared } } };
    private static Recipe RecipeOf(params ItemDrop[] materials) => new() {
        m_resources = materials.Select(m => new Requirement { m_resItem = m, m_amount = 1 }).ToArray() };

    public static void Check()
    {
        var ordinary = new ItemData();
        Expect(!MatchToolCategory(ordinary) && Expensive == 0, "Non-tool avoids food/mead classification");
        Expect(!MatchPotionCategory(ordinary) && Expensive == 0, "Non-potion avoids food/mead classification");
        ordinary.m_shared.m_itemType = ItemType.Material;
        Expect(!MatchPotionCategory(ordinary) && Expensive == 0, "Material without effect avoids classification");
        foreach (var type in new[] { ItemType.Material, ItemType.Consumable })
        {
            ordinary.m_shared.m_itemType = type; ordinary.m_shared.m_consumeStatusEffect = new();
            Expect(MatchPotionCategory(ordinary), "Effect carrier can be a potion");
            ordinary.Food = true; Expect(!MatchPotionCategory(ordinary), "Food is not a potion"); ordinary.Food = false;
            ordinary.Mead = true; Expect(!MatchPotionCategory(ordinary), "Mead stays distinct from potion"); ordinary.Mead = false;
            ordinary.MeadBase = true; Expect(!MatchPotionCategory(ordinary), "Mead base is not potion"); ordinary.MeadBase = false;
        }
        var tools = new[] {
            new ItemData { m_shared = new() { m_itemType = ItemType.Tool } },
            new ItemData { Torch = true }, new ItemData { CustomTool = true },
            new ItemData { m_shared = new() { m_skillType = Skills.SkillType.Pickaxes } },
            new ItemData { m_shared = new() { m_skillType = Skills.SkillType.Fishing } },
            new ItemData { m_shared = new() { m_skillType = Skills.SkillType.Farming } }
        };
        foreach (var item in tools)
        {
            Expect(MatchToolCategory(item), "Supported native/custom tool");
            item.Food = true; Expect(!MatchToolCategory(item), "Food excludes even custom tools"); item.Food = false;
            item.Consumable = true; Expect(!MatchToolCategory(item), "Consumable excludes tool"); item.Consumable = false;
            item.MeadBase = true; Expect(!MatchToolCategory(item), "Mead base excludes tool"); item.MeadBase = false;
        }

        var wood = Material("Wood", "OtherPrefab", "$item_shared");
        ResourceTierByToken["wood"] = 1; ResourceTierByToken["otherprefab"] = 8;
        var recipe = RecipeOf(wood);
        Normalizations = PrefabReads = Localization.Calls = 0;
        using (var pass = new CraftingResourceTierPass())
        {
            for (int i = 0; i < 1801; i++) Expect(GetCraftingRecipeResourceTier(recipe) == 1, "First material token wins");
            Expect(Normalizations == 1 && PrefabReads == 0 && Localization.Calls == 0,
                "1801 shared-material recipes normalize once, with no later identity or localization work");
            var missing = Material("Missing", "Unknown", "$item_no_tier");
            Expect(!TryGetResourceTier(missing, out int miss) && miss == 0, "Unknown material lookup fails");
            int before = Normalizations;
            Expect(!TryGetResourceTier(missing, out miss) && miss == 0 && Normalizations == before, "Negative lookup reused");
            using (var nested = new CraftingResourceTierPass())
            {
                ResourceTierByToken["wood"] = 3; ClearItemClassifierCaches();
                Expect(GetCraftingRecipeResourceTier(recipe) == 3, "Reload visible inside nested pass");
            }
            Expect(GetCraftingRecipeResourceTier(recipe) == 3, "Suspended parent invalidates after nested reload");
            try { using var nested = new CraftingResourceTierPass(); throw new InvalidOperationException(); }
            catch (InvalidOperationException) { }
            Expect(ReferenceEquals(_craftingResourceTierPass, pass), "Exception restores parent resource scope");
        }
        Expect(_craftingResourceTierPass == null, "Resource references released after rebuild");
        ResourceTierByToken["wood"] = 4;
        using (var pass = new CraftingResourceTierPass()) Expect(GetCraftingRecipeResourceTier(recipe) == 4, "Next rebuild sees live tier changes");
        wood.name = "Changed";
        Expect(GetCraftingRecipeResourceTier(recipe) == 8, "Unscoped item rename observed");
        wood.m_itemData.Prefab = ""; ResourceTierByToken["shared"] = 6;
        Expect(GetCraftingRecipeResourceTier(recipe) == 6, "Shared-name fallback observed");
        ResourceTierByToken.Remove("shared"); ResourceTierByToken["translated"] = 2;
        Expect(GetCraftingRecipeResourceTier(recipe) == 2, "Localized-name fallback preserved");
        Localization.Text = "newlanguage"; ResourceTierByToken["newlanguage"] = 7;
        Expect(GetCraftingRecipeResourceTier(recipe) == 7, "Changed language observed next lookup");
        var zero = Material("Zero"); ResourceTierByToken["zero"] = 0;
        using (var pass = new CraftingResourceTierPass())
        {
            Expect(TryGetResourceTier(zero, out int tier) && tier == 0, "Zero is a successful tier");
            Expect(TryGetResourceTier(zero, out tier) && tier == 0, "Cached zero is not a miss");
            Expect(GetCraftingRecipeResourceTier(RecipeOf(zero, wood)) == 7, "Maximum direct material tier retained");
            Expect(GetCraftingRecipeResourceTier(new Recipe { m_resources = new[] {
                new Requirement { m_resItem = wood, m_amount = 0 },
                new Requirement { m_resItem = zero, m_amount = 1 } } }) == 0, "Zero amount excluded");
        }
        Expect(GetCraftingRecipeResourceTier(null) == int.MinValue, "Missing recipe policy unchanged");
        Expect(GetCraftingRecipeResourceTier(RecipeOf(Material("unmapped"))) == int.MinValue, "No known materials policy unchanged");
        ResourceTierByToken.Clear();
        Expect(GetCraftingRecipeResourceTier(null) == 0, "Empty resource map policy unchanged");

        var identities = new ItemData { Prefab = "Some_Wood(Clone)", m_shared = new() { m_name = "$item_wood" } };
        Normalizations = 0;
        Expect(GetItemIdentityTokens(identities).First() == "Some_Wood" && Normalizations == 0, "Exact identity avoids normalization");
        Expect(GetItemIdentityTokens(identities).SequenceEqual(new[] { "Some_Wood", "somewood", "$item_wood" }), "Full identity order unchanged");

        CheckFeastFreshness();
        Console.WriteLine($"Recipe metadata work: {Checks} checks passed (classification, tier reuse, invalidation, counted work; no game timing).");
    }

    private static void CheckFeastFreshness()
    {
        ZNetScene.instance = null; EnsureFeastMaterialTokens();
        Expect(ItemClassifierRuntime.FeastMaterialStampInitialized && Scans == 0, "First absent-scene check initializes stamp");
        ZNetScene.instance = new() { Id = 0 }; EnsureFeastMaterialTokens();
        Expect(ItemClassifierRuntime.FeastMaterialPrefabCount == 0, "Empty scene distinguished from absent scene");
        var prefab = new GameObject { Feasts = new[] { new Feast { m_foodItem = Material("FeastFood"),
            Piece = new() { m_resources = new[] { new Piece.Requirement { m_resItem = Material("FeastMaterial") } } } } } };
        ZNetScene.instance.m_prefabs!.Add(prefab); EnsureFeastMaterialTokens();
        Expect(Scans == 1 && ItemClassifierRuntime.FeastMaterialTokens.SetEquals(new[] { "FeastFood", "FeastMaterial" }), "Prefab count change rebuilds feast tokens");
        int version = ItemClassifierRuntime.Version;
        for (int i = 0; i < 1801; i++) EnsureFeastMaterialTokens();
        Expect(Scans == 1 && ItemClassifierRuntime.Version == version, "Stable scene avoids rescanning and version churn");
        ZNetScene.instance = new() { Id = 2, m_prefabs = new() { prefab } }; EnsureFeastMaterialTokens();
        Expect(Scans == 2 && ItemClassifierRuntime.Version == version, "New scene scans even at same count; equal token set keeps version");
        ZNetScene.instance.m_prefabs = null; EnsureFeastMaterialTokens();
        Expect(ItemClassifierRuntime.FeastMaterialTokens.Count == 0 && ItemClassifierRuntime.Version == version + 1, "Missing prefab list clears prior tokens");
        ZNetScene.instance = null; EnsureFeastMaterialTokens();
        Expect(ItemClassifierRuntime.Version == version + 1, "Repeated absent state does not churn version");
        ZNetScene.instance = new() { Id = 2, m_prefabs = new() { prefab } }; EnsureFeastMaterialTokens();
        Expect(Scans == 3 && ItemClassifierRuntime.FeastMaterialTokens.Count == 2, "Returning scene repopulates tokens");
    }
}
public enum ItemType { Other, Material, Consumable, Tool }
public static class Skills { public enum SkillType { None, Pickaxes, Fishing, Farming } }
public class ItemData
{
    public Shared m_shared = new(); public string Prefab = "";
    public bool Food, Consumable, Mead, MeadBase, Torch, CustomTool;
    public class Shared { public ItemType m_itemType; public Skills.SkillType m_skillType; public object? m_consumeStatusEffect; public string m_name = ""; }
}
public class ItemDrop { public string name = ""; public ItemData m_itemData = new(); }
public class Requirement { public ItemDrop? m_resItem; public int m_amount; }
public class Recipe { public Requirement[]? m_resources; }
public class Localization
{
    public static Localization? instance = new(); public static int Calls; public static string Text = "translated";
    public string Localize(string text) { Calls++; return text == "$item_shared" ? Text : text; }
}
public static class Mathf { public static int Max(int a, int b) => Math.Max(a, b); }
public class ZNetScene { public static ZNetScene? instance; public int Id; public List<GameObject>? m_prefabs = new(); public int GetInstanceID() => Id; }
public class GameObject { public Feast[] Feasts = Array.Empty<Feast>(); public T[] GetComponentsInChildren<T>(bool includeInactive) { Host.Scans++; return Feasts.Cast<T>().ToArray(); } }
public class Feast { public ItemDrop? m_foodItem; public Piece? Piece; public T? GetComponent<T>() where T : class => Piece as T; public T? GetComponentInParent<T>() where T : class => null; }
public class Piece { public Requirement[]? m_resources; public class Requirement : global::Requirement { } }
""";
}
