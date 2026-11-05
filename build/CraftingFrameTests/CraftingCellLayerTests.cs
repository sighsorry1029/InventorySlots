using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

internal static class CraftingCellLayerTests
{
    internal static void Run()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root != null && !File.Exists(Path.Combine(root.FullName, "CraftingListView.cs"))) root = root.Parent;
        if (root == null) throw new DirectoryNotFoundException("InventorySlots checkout");
        IEnumerable<MethodDeclarationSyntax> Methods(string file) => CSharpSyntaxTree.ParseText(
            File.ReadAllText(Path.Combine(root.FullName, file))).GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>();
        string members = Methods("CraftingListView.cs").Single(m => m.Identifier.ValueText == "ConfigureCraftingListRow").ToFullString();
        foreach (string name in new[] { "RestoreCraftingRecipeCellIconLayer", "ConfigureCraftingRecipeCellAmountText",
            "ConfigureCraftingRecipeCellQualityText", "ConfigureCraftingRecipeCellText" })
            members += Methods("CraftingGridInteraction.cs").Single(m => m.Identifier.ValueText == name).ToFullString();
        members += string.Concat(Methods("UiLayout.cs").Where(m => m.Identifier.ValueText is "SetRectLayout" or "SetTopLeftRectLayout")
            .Select(m => m.ToFullString()));
        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator)
            .Select(path => MetadataReference.CreateFromFile(path));
        var compilation = CSharpCompilation.Create("CraftingCellLayerRegression",
            new[] { CSharpSyntaxTree.ParseText(Host.Replace("/* PRODUCTION */", members)) }, references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));
        using var stream = new MemoryStream();
        var result = compilation.Emit(stream);
        if (!result.Success) throw new Exception(string.Join("\n", result.Diagnostics));
        try { Assembly.Load(stream.ToArray()).GetType("CellHost")!.GetMethod("Run")!.Invoke(null, null); }
        catch (TargetInvocationException error) { throw error.InnerException!; }
    }

    // Actual list layout, shared rect helpers, grid layer repair and numeric
    // text layout run against a sibling-order model, not a Unity renderer.
    private const string Host = """
using System;
using System.Collections.Generic;
using System.Linq;
public static class CellHost
{
    /* PRODUCTION */
    private const float CraftingListWidth = 200f;
    private const string CraftingRecipeStyleButtonName = "style";
    private static void ApplyDefaultFontAsset(TMP_Text text) { }
    private static bool IsUnityNull(object? value) => value == null;
    private static bool IsVeiledRecipeMasked(InventoryGui.RecipeDataPair pair) => false;
    private static string GetCraftingRecipeDisplayName(InventoryGui.RecipeDataPair pair) => "Snowball x10";
    private static RectTransform CreateTextRect(string name, Transform parent) => new(name, parent);
    private static int Checks;
    private static void Check(bool value, string name) { if (!value) throw new Exception(name); Checks++; }
    private static void Grid(CraftingRecipeGridCell cell)
    {
        RestoreCraftingRecipeCellIconLayer(cell);
        ConfigureCraftingRecipeCellAmountText(cell.Amount, "10", 64f);
        ConfigureCraftingRecipeCellQualityText(cell.Quality, "2", 64f);
        if (cell.Name != null) cell.Name.gameObject.SetActive(false);
    }
    private static void AboveIcon(CraftingRecipeGridCell cell, string label) => Check(
        cell.Icon!.rectTransform.GetSiblingIndex() < cell.Amount!.rectTransform.GetSiblingIndex() &&
        cell.Icon.rectTransform.GetSiblingIndex() < cell.Quality!.rectTransform.GetSiblingIndex(), label);
    public static void Run()
    {
        var cell = new CraftingRecipeGridCell();
        string NonIconOrder() => string.Join(",", cell.Rect.Children.Where(c => c != cell.Icon!.rectTransform).Select(c => c.name));
        var original = cell.Rect.Children.ToArray();
        Grid(cell); AboveIcon(cell, "New grid places numbers over icon");
        Check(Transform.Writes == 0 && original.SequenceEqual(cell.Rect.Children), "Normal grid needs no sibling writes");
        var pair = new InventoryGui.RecipeDataPair();
        for (int cycle = 0; cycle < 3; cycle++)
        {
            ConfigureCraftingListRow(cell, pair, 30f + cycle, cycle % 2 == 0);
            AboveIcon(cell, "List resizing must not raise icon over numeric labels");
            Check(!cell.Amount!.gameObject.activeSelf && !cell.Quality!.gameObject.activeSelf, "List hides numeric overlays");
            string before = NonIconOrder();
            Grid(cell); AboveIcon(cell, "Grid reuse preserves numeric layering");
            Check(cell.Amount.gameObject.activeSelf && cell.Amount.text == "10" && cell.Quality.gameObject.activeSelf && cell.Quality.text == "2", "Grid restores quantity and quality");
            Check(NonIconOrder() == before, "Grid does not reorder badges, buttons or labels");
        }
        cell.Icon!.rectTransform.SetAsLastSibling();
        cell.Amount!.gameObject.SetActive(false); cell.Quality!.gameObject.SetActive(false);
        string otherOrder = NonIconOrder();
        bool[] otherActive = cell.Rect.Children.Where(c => c != cell.Icon.rectTransform).Select(c => c.gameObject.activeSelf).ToArray();
        int writes = Transform.Writes;
        RestoreCraftingRecipeCellIconLayer(cell); AboveIcon(cell, "Legacy raised icon repaired against inactive labels");
        Check(Transform.Writes == writes + 1, "Repair moves only the icon once");
        Check(NonIconOrder() == otherOrder && otherActive.SequenceEqual(cell.Rect.Children.Where(c => c != cell.Icon.rectTransform).Select(c => c.gameObject.activeSelf)), "Repair preserves other order and visibility");
        Grid(cell); Grid(cell); Check(Transform.Writes == writes + 1, "Repeated grid refresh leaves correct order alone");
        cell.Icon.rectTransform.SetAsLastSibling(); cell.Amount = null;
        RestoreCraftingRecipeCellIconLayer(cell);
        Check(cell.Icon.rectTransform.GetSiblingIndex() < cell.Quality!.rectTransform.GetSiblingIndex(), "Quality alone is protected");
        var foreign = new RectTransform("foreign", null);
        cell.Quality.rectTransform.SetParent(foreign, false);
        writes = Transform.Writes;
        RestoreCraftingRecipeCellIconLayer(cell);
        Check(Transform.Writes == writes, "Foreign label hierarchy is not changed");
        cell.Icon.rectTransform.SetParent(foreign, false); RestoreCraftingRecipeCellIconLayer(cell);
        cell.Icon = null; RestoreCraftingRecipeCellIconLayer(cell);
        Check(Transform.Writes == writes, "Missing or reparented icon is ignored");
        Console.WriteLine($"Crafting cell layering: {Checks} checks passed (production layout, managed sibling model; no Unity rendering).");
    }
}
public class GameObject { public bool activeSelf = true; public void SetActive(bool active) => activeSelf = active; }
public class Transform
{
    public static int Writes;
    public string name;
    public GameObject gameObject = new();
    public Transform? parent;
    public List<Transform> Children = new();
    public int childCount => Children.Count;
    public Transform(string name, Transform? parent) { this.name=name; SetParent(parent, false); }
    public void SetParent(Transform? value, bool world) { parent?.Children.Remove(this); parent=value; parent?.Children.Add(this); }
    public int GetSiblingIndex() => parent?.Children.IndexOf(this) ?? 0;
    public void SetSiblingIndex(int index) { parent!.Children.Remove(this); parent.Children.Insert(Math.Clamp(index,0,parent.Children.Count),this); Writes++; }
    public void SetAsLastSibling() => SetSiblingIndex(parent!.childCount - 1);
    public Transform? Find(string name) => Children.FirstOrDefault(c => c.name == name);
}
public class RectTransform(string name, Transform? parent) : Transform(name, parent)
{
    public Vector2 anchorMin, anchorMax, pivot, anchoredPosition, sizeDelta;
    public Vector3 localScale;
    public Quaternion localRotation;
    public T GetComponent<T>() => (T)(object)new TMP_Text(this);
}
public record struct Vector2(float x, float y)
{
    public static Vector2 one => new(1,1);
    public static Vector2 operator -(Vector2 a, Vector2 b) => new(a.x-b.x,a.y-b.y);
    public float sqrMagnitude => x*x+y*y;
}
public record struct Vector3(float x, float y, float z) { public static Vector3 one => new(1,1,1); }
public record struct Quaternion { public static Quaternion identity => new(); }
public static class Mathf { public static float Clamp(float v,float min,float max)=>Math.Clamp(v,min,max); public static int Min(int a,int b)=>Math.Min(a,b); }
public record struct Color(float r,float g,float b) { public static Color white => new(1,1,1); }
public enum TextAlignmentOptions { MidlineLeft, Bottom, TopRight }
public enum TextWrappingModes { Normal, NoWrap }
public enum TextOverflowModes { Ellipsis }
public class TMP_Text(RectTransform rect)
{
    public RectTransform rectTransform = rect;
    public GameObject gameObject => rectTransform.gameObject;
    public string text = "";
    public float fontSize;
    public bool raycastTarget;
    public TextAlignmentOptions alignment;
    public TextWrappingModes textWrappingMode;
    public TextOverflowModes overflowMode;
    public Color color;
}
public class Image(RectTransform rect) { public RectTransform rectTransform = rect; public GameObject gameObject => rect.gameObject; }
public class ItemData { public int m_quality = 2; }
public class InventoryGui { public class RecipeDataPair { public ItemData? ItemData; } }
public class CraftingRecipeGridCell
{
    public RectTransform Rect = new("cell", null);
    public Image? Icon, Food;
    public TMP_Text? Amount, Quality, Name;
    public CraftingRecipeGridCell()
    {
        new RectTransform("selected", Rect);
        Icon = new(new("icon",Rect)); Food = new(new("foodicon",Rect));
        Amount = new(new("amount",Rect)); Quality = new(new("quality",Rect));
        new RectTransform("style",Rect); new RectTransform("favorite",Rect); new RectTransform("pin",Rect);
    }
}
""";
}
