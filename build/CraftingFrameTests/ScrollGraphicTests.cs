using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

internal static class ScrollGraphicTests
{
    internal static void Run()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root != null && !File.Exists(Path.Combine(root.FullName, "CraftingScrollbar.cs"))) root = root.Parent;
        string method = CSharpSyntaxTree.ParseText(File.ReadAllText(Path.Combine(root!.FullName, "CraftingScrollbar.cs")))
            .GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>()
            .Single(m => m.Identifier.ValueText == "ShouldControlVanillaCraftingScrollGraphic").ToFullString();
        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator)
            .Select(path => MetadataReference.CreateFromFile(path));
        var compilation = CSharpCompilation.Create("ScrollGraphicRegression",
            new[] { CSharpSyntaxTree.ParseText(Host.Replace("/* PRODUCTION */", method)) }, references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));
        using var stream = new MemoryStream();
        var result = compilation.Emit(stream);
        if (!result.Success) throw new Exception(string.Join("\n", result.Diagnostics));
        try { Assembly.Load(stream.ToArray()).GetType("ScrollHost")!.GetMethod("Run")!.Invoke(null, null); }
        catch (TargetInvocationException error) { throw error.InnerException!; }
    }

    private const string Host = """
using System;
public static class ScrollHost
{
    /* PRODUCTION */
    static int checks, foreignQueries;
    static bool IsOwnedCraftingUiTransform(Transform t) => t.Owned;
    static bool IsForeignCraftingUiTransform(InventoryGui g, Transform t) { foreignQueries++; return t.Foreign; }
    static void Check(bool ok, string name) { if (!ok) throw new Exception(name); checks++; }
    public static void Run()
    {
        var gui = new InventoryGui();
        var rows = new Transform("ListRoot", gui.m_crafting);
        for (int i=0; i<1801; i++)
            Check(!ShouldControlVanillaCraftingScrollGraphic(gui, new Transform("icon", new Transform("row", rows))), "recipe icons are not scrollbar graphics");
        Check(foreignQueries == 0, "1801 ordinary recipe graphics never trigger recipe-wide foreign-UI classification");
        var native = new Transform("Handle", gui.m_recipeListScroll!.transform);
        var named = new Transform("Handle", new Transform("ScRoLlArEa", gui.m_crafting));
        foreach (var t in new[] { native, named })
        {
            Check(ShouldControlVanillaCraftingScrollGraphic(gui, t), "native reference and legacy named scroll graphics remain controlled");
            t.Foreign = true;
            Check(!ShouldControlVanillaCraftingScrollGraphic(gui, t), "foreign scroll graphic remains protected");
            t.Foreign = false; t.Owned = true;
            Check(!ShouldControlVanillaCraftingScrollGraphic(gui, t), "owned redesigned scroll graphic remains protected");
        }
        Check(!ShouldControlVanillaCraftingScrollGraphic(gui, new Transform("scroll", null)), "outside crafting is never controlled");
        gui.m_recipeListScroll = null;
        named.Owned = false;
        Check(ShouldControlVanillaCraftingScrollGraphic(gui, named), "name fallback remains available without native scrollbar");
        gui.m_crafting = null;
        Check(!ShouldControlVanillaCraftingScrollGraphic(gui, named), "destroyed crafting root is harmless");
        Console.WriteLine($"Crafting scroll graphic routing: {checks} checks passed (production method, managed hierarchy; no Unity rendering).");
    }
}
public class Transform(string name, Transform? parent)
{
    public string name = name;
    public Transform? parent = parent;
    public bool Owned, Foreign;
    public bool IsChildOf(Transform root) { for (Transform? t=this; t!=null; t=t.parent) if(t==root)return true; return false; }
}
public class Scrollbar(Transform t) { public Transform transform = t; }
public class InventoryGui
{
    public Transform? m_crafting = new("crafting", null);
    public Scrollbar? m_recipeListScroll;
    public InventoryGui() { m_recipeListScroll = new(new("bar", m_crafting)); }
}
""";
}
