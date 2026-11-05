using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

internal static class CraftingScrollInputTests
{
    internal static void Run()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root != null && !File.Exists(Path.Combine(root.FullName, "CraftingGridInteraction.cs"))) root = root.Parent;
        if (root == null) throw new DirectoryNotFoundException("InventorySlots checkout");
        string Read(string file) => File.ReadAllText(Path.Combine(root.FullName, file));
        string Method(string file, string name) => CSharpSyntaxTree.ParseText(Read(file)).GetRoot()
            .DescendantNodes().OfType<MethodDeclarationSyntax>().Single(m => m.Identifier.ValueText == name).ToFullString();
        string source = Host.Replace("/* METHODS */",
            Method("CraftingGridInteraction.cs", "HandleCraftingRecipeGridWheel") +
            Method("CraftingGridInteraction.cs", "IsCraftingRecipePageScrollTargetActive") +
            Method("UiScrollInput.cs", "ConsumeMouseUiScrollForCurrentFrame") +
            Method("UiScrollInput.cs", "IsMouseUiScrollConsumedForCurrentFrame"));
        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator)
            .Select(path => MetadataReference.CreateFromFile(path));
        var trees = new[] { source, Read("CraftingViewCore.cs"), Read("CraftingTabAdapterModels.cs") }
            .Select(text => CSharpSyntaxTree.ParseText(text));
        var compilation = CSharpCompilation.Create("CraftingScrollInputRegression", trees, references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        using var stream = new MemoryStream();
        var result = compilation.Emit(stream);
        if (!result.Success) throw new Exception(string.Join("\n", result.Diagnostics));
        try { Assembly.Load(stream.ToArray()).GetType("ScrollHost")!.GetMethod("Check")!.Invoke(null, null); }
        catch (TargetInvocationException error) { throw error.InnerException!; }
    }

    // Production wheel handler and consumption guards; boundary doubles supply
    // input, sorted recipes and selection callbacks. No Unity/device simulation.
    private const string Host = """
using System;
using System.Linq;
using System.Collections.Generic;
using InventorySlots;
public static class ScrollHost
{
    /* METHODS */
    private static bool _craftingListViewActive, Modifier, OverGrid, OverScrollbar;
    private static int _craftingRecipePage, _craftingListRevealedSelection, _mouseUiScrollConsumedFrame = -1;
    private static Config<CraftingScrollMode> _craftingGridScrollMode, _craftingListScrollMode;
    private static int Checks, SelectionCalls;
    private static bool Center;
    private static Vector2 GetUiMousePosition() => new();
    private static bool IsCraftingRecipeScrollbarScrollTargetActive(Vector2 mouse) => OverScrollbar;
    private static bool IsCraftingRecipeGridZoomModifierHeld() => Modifier;
    private static bool RectContainsCraftingRecipeIconArea(RectTransform grid, Vector2 mouse) => OverGrid;
    private static bool IsGamepadUiScrollActive() => false;
    private enum UiScrollInputMode { Discrete }
    private static float GetUiScrollDelta(UiScrollInputMode mode) => IsMouseUiScrollConsumedForCurrentFrame() ? 0 : Input.mouseScrollDelta.y;
    private static int GetCraftingRecipeGridDimension() => _craftingListViewActive ? 1 : 6;
    private static int GetCraftingRecipeGridCapacity() => _craftingListViewActive ? 14 : 36;
    private static int GetCraftingRecipePageCount(InventoryGui gui) => CraftingViewCore.PageCount(CraftingRecipes.View.Count, GetCraftingRecipeGridCapacity(), GetCraftingRecipeGridDimension());
    private static int GetCraftingRecipePageStart() => CraftingViewCore.PageStart(_craftingRecipePage, GetCraftingRecipeGridDimension());
    private static int GetSelectedCraftingRecipeIndexSafe(InventoryGui gui, bool acceptOneLevelHigher)
    { if (acceptOneLevelHigher) throw new Exception("List must preserve the exact selection"); return gui.Selected; }
    private static void SetCraftingRecipeWithStoredVariant(InventoryGui gui, int index, bool center)
    { gui.Selected = index; Center = center; SelectionCalls++; }
    private static InventoryGui Begin(bool list, CraftingScrollMode mode, int count = 100)
    {
        _craftingListViewActive = list;
        // Give the other view the opposite setting to detect accidental sharing.
        CraftingScrollMode opposite = mode == CraftingScrollMode.Row ? CraftingScrollMode.Page : CraftingScrollMode.Row;
        _craftingGridScrollMode = new() { Value = list ? opposite : mode };
        _craftingListScrollMode = new() { Value = list ? mode : opposite };
        _craftingRecipePage = 0;
        _craftingListRevealedSelection = -1;
        CraftingRecipes.View = Enumerable.Range(0, count).Select(i => new Recipe { OriginalIndex = 1000 - i * 3 }).ToList();
        Modifier = OverScrollbar = false;
        OverGrid = true;
        SelectionCalls = CraftingController.Dirty = CraftingController.Hover = 0;
        Time.frameCount++;
        Input.mouseScrollDelta = new() { y = -1 };
        return new() { Selected = 777 };
    }
    private static void Expect(bool condition, string message)
    { if (!condition) throw new Exception(message); Checks++; }
    private static bool Wheel(InventoryGui gui) => HandleCraftingRecipeGridWheel(gui, new());
    public static void Check()
    {
        foreach (bool list in new[] { false, true })
        foreach (CraftingScrollMode mode in Enum.GetValues<CraftingScrollMode>())
        {
            InventoryGui gui = Begin(list, mode);
            int step = mode == CraftingScrollMode.Row ? 1 : list ? 14 : 6;
            Expect(Wheel(gui) && _craftingRecipePage == step, "Use the current view's independent scroll mode");
            Expect(CraftingController.Dirty == 1 && CraftingController.Hover == 1, "One layout/hover invalidation per scroll");
            Expect(list ? gui.Selected == 777 && SelectionCalls == 0 && _craftingListRevealedSelection == 777
                        : gui.Selected == 1000 - step * 6 * 3 && SelectionCalls == 1 && !Center,
                "List retains selection; Grid selects first displayed native identity without centering");
            Expect(!Wheel(gui) && _craftingRecipePage == step && CraftingController.Dirty == 1,
                "A repeated same-frame callback cannot consume the wheel twice");
        }
        foreach (bool list in new[] { false, true })
        foreach (string boundary in new[] { "top", "bottom", "short", "empty", "outside", "no-input" })
        {
            InventoryGui gui = Begin(list, CraftingScrollMode.Page, boundary == "short" ? 5 : boundary == "empty" ? 0 : 100);
            if (boundary == "top") Input.mouseScrollDelta.y = 1;
            if (boundary == "bottom") _craftingRecipePage = GetCraftingRecipePageCount(gui) - 1;
            if (boundary == "outside") OverGrid = false;
            if (boundary == "no-input") Input.mouseScrollDelta.y = 0;
            int row = _craftingRecipePage;
            Expect(!Wheel(gui) && row == _craftingRecipePage && gui.Selected == 777 && SelectionCalls == 0 && CraftingController.Dirty == 0,
                "Inactive/boundary input must have no selection or layout side effects: " + boundary);
        }
        var grid = Begin(false, CraftingScrollMode.Row);
        Modifier = true;
        Expect(!Wheel(grid) && _craftingRecipePage == 0, "Alt over grid remains reserved for zoom");
        OverScrollbar = true;
        Expect(Wheel(grid) && _craftingRecipePage == 1, "Alt over scrollbar still scrolls recipes");
        var listGui = Begin(true, CraftingScrollMode.Page);
        Modifier = true;
        Expect(Wheel(listGui) && _craftingRecipePage == 14, "List ignores the grid-only zoom modifier");
        Console.WriteLine($"Crafting scroll input: {Checks} checks passed.");
    }
}
class Config<T> { public T Value; }
class InventoryGui { public int Selected; }
class RectTransform { }
struct Vector2 { public float y; }
static class Mathf { public static float Abs(float value) => Math.Abs(value); }
static class Time { public static int frameCount; }
static class Input { public static Vector2 mouseScrollDelta; }
class Recipe { public int OriginalIndex; }
static class CraftingRecipes { public static List<Recipe> View = new(); }
static class CraftingController
{
    public static int Dirty, Hover;
    public static void ClearHoveredRecipeAndRequestMouseSync() => Hover++;
    public static void MarkRecipeGridLayoutDirty() => Dirty++;
}
""";
}
