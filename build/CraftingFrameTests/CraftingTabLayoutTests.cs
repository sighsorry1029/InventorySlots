using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

internal static class CraftingTabLayoutTests
{
    internal static void Run()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root != null && !File.Exists(Path.Combine(root.FullName, "PathOfValheimanCraftingCompat.cs"))) root = root.Parent;
        if (root == null) throw new DirectoryNotFoundException("InventorySlots checkout");
        MethodDeclarationSyntax Read(string file, string name) => CSharpSyntaxTree.ParseText(
            File.ReadAllText(Path.Combine(root.FullName, file))).GetRoot().DescendantNodes()
            .OfType<MethodDeclarationSyntax>().Single(m => m.Identifier.ValueText == name);
        string members = string.Join("\n", new[] { "LayoutPathOfValheimanCraftingTab", "GetCraftingTabEdge",
            "PlaceCraftingTab", "RestorePathOfValheimanCraftingTabs" }.Select(name => Read("PathOfValheimanCraftingCompat.cs", name).ToFullString()));
        members += Read("PluginLifecycle.cs", "UpdateInventoryDialogLayering").ToFullString();
        foreach (string name in new[] { "CaptureCraftingStationLevelLayout", "LayoutCraftingStationLevel", "RestoreCraftingStationLevelLayout" })
            members += Read("CraftingRedesign.cs", name).ToFullString();
        members += Read("CraftingGroupsAndSearch.cs", "GetCraftingHeaderButtonLayout").ToFullString();
        members += Read("CraftingGroupsAndSearch.cs", "ToggleCraftingRecipeSortMode").ToFullString();
        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator)
            .Select(path => MetadataReference.CreateFromFile(path));
        var compilation = CSharpCompilation.Create("CraftingTabLayoutRegression",
            new[] { CSharpSyntaxTree.ParseText(Host.Replace("/* PRODUCTION */", members)) }, references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));
        using var stream = new MemoryStream();
        var result = compilation.Emit(stream);
        if (!result.Success) throw new Exception(string.Join("\n", result.Diagnostics));
        try { Assembly.Load(stream.ToArray()).GetType("TabHost")!.GetMethod("Check")!.Invoke(null, null); }
        catch (TargetInvocationException error) { throw error.InnerException!; }
    }

    // Execute the actual layout and its LateUpdate caller. The host models 2D
    // translation, scale, pivots and unlike anchor origins, not Unity rendering.
    private const string Host = """
using System;
using System.Collections.Generic;
using System.Linq;
public static class TabHost
{
    /* PRODUCTION */
    private sealed class Instance { public bool isActiveAndEnabled = true; }
    private static Instance? _instance = new();
    private static Func<Button>? _povSalvageTab;
    private static InventoryGui? _povCraftingGui;
    private static RectTransformSnapshot? _povTabOriginal, _povVneiTabOriginal;
    private static int Checks;
    private const float CraftingSortModeButtonGap = 4f;
    private enum CraftingRecipeSortMode { GroupThenTier, TierThenGroup }
    private static CraftingRecipeSortMode SortMode;
    private static CraftingRecipeSortMode GetCraftingRecipeSortMode() => SortMode;
    private static void SetCraftingRecipeSortMode(CraftingRecipeSortMode mode) => SortMode = mode;
    private static class CraftingUi
    {
        public static InventoryGui? StationLevelOwner;
        public static Transform? StationLevelParent;
        public static RectTransformSnapshot? StationLevelSnapshot;
    }
    private static bool IsDedicatedServer;
    private static void WarnPathOfValheimanCrafting(Exception error) => throw new Exception("Unexpected compatibility warning", error);
    private static bool IsInventoryPanelClosing(InventoryGui gui) => gui.Closing;
    private static void SetActionPanelActive(RectTransform? layer, bool active) { }
    private static void UpdatePathOfValheimanTooltipLayer() { }
    private static class PathOfValheimanTooltipLayer { public static void Restore() { } }
    private sealed class Follower { public void Follow() { } }
    private sealed class PinFollower { public void Follow(bool keepOnTop = false) { } }
    private static class PinnedTooltips { public static class Crafting { public static PinFollower Host = new(); } }
    private static class InventoryPanels
    {
        public static RectTransform? SortButtonLayer;
        public static Follower PlayerSortHost = new(), ContainerSortHost = new();
        public static Dictionary<int, RectTransform> QuickSlotPanels = new();
    }
    private static class InventoryDialogLayering { public static void Update(InventoryGui g, RectTransform? q, RectTransform? s) { } }
    private static RectTransform Tab(Transform parent, string name, float left, float top, float width, bool button = true, float pivot = 0f, float scale = 1f)
    {
        var tab = new RectTransform { Name = name, parent = parent, Width = width, Pivot = pivot, Scale = new(scale, scale, 1f) };
        tab.localPosition = new(left + width * pivot * scale, top - 30f * (1f - pivot) * scale, 0f);
        if (button) tab.Button = new Button(tab);
        return tab;
    }
    private static void Expect(bool ok, string name) { if (!ok) throw new Exception(name); Checks++; }
    private static void At(RectTransform tab, float x, float y, string name)
    {
        Expect(Math.Abs(tab.localPosition.x - tab.Width * tab.Pivot * tab.Scale.x - x) < 0.01f &&
            Math.Abs(tab.localPosition.y + 30f * (1f - tab.Pivot) * tab.Scale.y - y) < 0.01f, name);
    }
    public static void Check()
    {
        var parent = new RectTransform { Scale = new(1.6f, 1.6f, 1f), localPosition = new(400f, 50f, 0f) };
        var craft = Tab(parent, "Craft", 10f, 200f, 100f);
        var upgrade = Tab(parent, "Upgrade", 114f, 200f, 100f);
        upgrade.gameObject.Active = false;
        var salvage = Tab(parent, "PT_TabSalvage", 620f, 150f, 116f, pivot: 0.5f);
        salvage.gameObject.Active = false;
        var vnei = Tab(parent, "VNEI", 800f, 100f, 100f, pivot: 0.5f, scale: 1.2f);
        vnei.AnchorOrigin = new(400f, 80f, 0f);
        Tab(parent, "Decoration", 900f, 200f, 300f, button: false);
        Tab(parent, "OffRowButton", 700f, 140f, 150f);
        var gui = new InventoryGui { m_tabCraft = craft.Button! };
        InventoryGui.instance = gui;
        _povSalvageTab = () => salvage.Button!;

        // Hand-crafting: no upgrade/salvage tabs, arbitrary visual/control siblings.
        LayoutPathOfValheimanCraftingTab(gui);
        At(vnei, 114f, 200f, "VNEI follows craft when salvage is hidden; ignores non-tabs and off-row controls");
        Expect(vnei.AnchorOrigin.x == 400f && vnei.Scale.x == 1.2f && vnei.Width == 100f && vnei.Button!.interactable,
            "Preserves anchor, scale, size and tab selection state");
        salvage.gameObject.Active = true;
        LayoutPathOfValheimanCraftingTab(gui);
        At(salvage, 114f, 200f, "Salvage aligns to craft's top edge with a different pivot");
        At(vnei, 234f, 200f, "VNEI immediately follows corrected salvage position");
        upgrade.gameObject.Active = true;
        LayoutPathOfValheimanCraftingTab(gui);
        At(salvage, 218f, 200f, "Visible upgrade remains between craft and salvage");
        At(vnei, 338f, 200f, "VNEI follows the wider salvage tab, not a fixed 107-pixel step");
        var foreign = Tab(parent, "OtherModTab", 218f, 200f, 90f);
        LayoutPathOfValheimanCraftingTab(gui);
        At(foreign, 218f, 200f, "Other mod tab is not moved");
        At(salvage, 312f, 200f, "Other visible tab is respected");
        At(vnei, 432f, 200f, "VNEI follows all visible tabs");

        int writes = Transform.PositionWrites + Transform.SiblingWrites;
        for (int i = 0; i < 120; i++) UpdateInventoryDialogLayering();
        Expect(Transform.PositionWrites + Transform.SiblingWrites == writes, "Stable LateUpdate frames do not rewrite tab geometry/order");
        // Simulate VNEI's own click/SetupCrafting anchoredPosition assignment
        // occurring after InventorySlots' UpdateCraftingPanel postfix.
        vnei.anchoredPosition = new(750f, -50f, 0f);
        vnei.Button!.interactable = false;
        UpdateInventoryDialogLayering();
        At(vnei, 432f, 200f, "LateUpdate repairs VNEI's later placement without reopening the native tab");
        Expect(!vnei.Button.interactable, "VNEI remains selected after final layout");
        salvage.gameObject.Active = false; upgrade.gameObject.Active = false; foreign.gameObject.Active = false;
        UpdateInventoryDialogLayering();
        At(vnei, 114f, 200f, "Leaving station closes gaps immediately");
        vnei.localPosition = new(950f, 20f, 0f);
        writes = Transform.PositionWrites;
        gui.Closing = true; UpdateInventoryDialogLayering(); gui.Closing = false;
        InventoryGui.Visible = false; UpdateInventoryDialogLayering(); InventoryGui.Visible = true;
        IsDedicatedServer = true; UpdateInventoryDialogLayering(); IsDedicatedServer = false;
        _instance!.isActiveAndEnabled = false; LayoutPathOfValheimanCraftingTab(gui); _instance.isActiveAndEnabled = true;
        _povSalvageTab = null; LayoutPathOfValheimanCraftingTab(gui);
        Expect(Transform.PositionWrites == writes, "Closing, hidden, dedicated, disabled and unavailable PoV paths leave positions alone");
        _povSalvageTab = () => salvage.Button!;
        RestorePathOfValheimanCraftingTabs();
        At(vnei, 800f, 100f, "Restores original VNEI placement");
        At(salvage, 620f, 150f, "Restores original PoV placement");

        // VNEI setting changes destroy and recreate the tab, even under the same GUI.
        vnei.parent = null;
        vnei = Tab(parent, "VNEI", 900f, 120f, 100f);
        UpdateInventoryDialogLayering();
        At(vnei, 114f, 200f, "Replacement tab is adopted");
        RestorePathOfValheimanCraftingTabs();
        At(vnei, 900f, 120f, "Replacement gets its own original snapshot");
        UpdateInventoryDialogLayering();
        var popup = new Transform(); vnei.parent = popup;
        writes = Transform.PositionWrites + Transform.SiblingWrites;
        RestorePathOfValheimanCraftingTabs();
        Expect(vnei.parent == popup && Transform.PositionWrites + Transform.SiblingWrites == writes,
            "External reparent is not reversed during cleanup");
        var iconParent = new Transform { parent = parent, localPosition = new(20f, 65f, 0f), Scale = new(1.2f, 0.8f, 1f) };
        var icon = new RectTransform { parent = iconParent, Width = 40f, Height = 60f, Pivot = 0.5f, localPosition = new(30f, -20f, 0f) };
        var level = new RectTransform { parent = parent, Width = 32f, Height = 32f, Pivot = 0.5f, localPosition = new(500f, 100f, 0f), Scale = new(0.9f, 0.9f, 1f) };
        level.gameObject.Active = false;
        gui.m_craftingStationIcon = new Image { rectTransform = icon };
        gui.m_craftingStationLevelRoot = level;
        Vector3 original = level.localPosition;
        CaptureCraftingStationLevelLayout(gui);
        level.localPosition += new Vector3(0f, 25f, 0f); // Simulate panel height protection after capture.
        LayoutCraftingStationLevel(gui);
        Vector3 expected = parent.InverseTransformPoint(icon.TransformPoint(new Vector3(icon.rect.xMax, icon.rect.center.y, 0f))) + new Vector3(4f, 0f, 0f);
        Vector3 actual = parent.InverseTransformPoint(level.TransformPoint(new Vector3(level.rect.xMin, level.rect.center.y, 0f)));
        Expect(Math.Abs(actual.x - expected.x) < 0.01f && Math.Abs(actual.y - expected.y) < 0.01f,
            "Station badge follows icon's right center across parent scales/pivots");
        Expect(!level.gameObject.Active && level.Width == 32f && level.Scale.x == 0.9f, "Native badge visibility/size/scale retained");
        writes = Transform.PositionWrites;
        for (int i = 0; i < 120; i++) LayoutCraftingStationLevel(gui);
        Expect(writes == Transform.PositionWrites, "Stable header performs no position writes");
        RestoreCraftingStationLevelLayout(new InventoryGui());
        Expect(CraftingUi.StationLevelOwner == gui, "Other GUI destruction cannot restore this badge");
        RestoreCraftingStationLevelLayout(gui);
        Expect(level.localPosition == original && CraftingUi.StationLevelSnapshot == null, "Header restores pre-extension coordinates");
        writes = Transform.PositionWrites;
        RestoreCraftingStationLevelLayout(gui);
        Expect(writes == Transform.PositionWrites, "Repeated header cleanup is inert");
        CaptureCraftingStationLevelLayout(gui);
        LayoutCraftingStationLevel(gui);
        level.parent = new Transform();
        writes = Transform.PositionWrites;
        RestoreCraftingStationLevelLayout();
        Expect(writes == Transform.PositionWrites && level.parent != parent, "Foreign reparent is respected");
        level.parent = parent;
        CaptureCraftingStationLevelLayout(gui);
        var second = new InventoryGui { m_craftingStationLevelRoot = new RectTransform { parent = parent } };
        CaptureCraftingStationLevelLayout(second);
        Expect(CraftingUi.StationLevelOwner == second && CraftingUi.StationLevelSnapshot!.Rect == second.m_craftingStationLevelRoot,
            "GUI replacement adopts only the new native badge");
        RestoreCraftingStationLevelLayout();

        foreach (float width in new[] { 136f, 192f, 240f })
        {
            var search = new RectTransform { Width = width, Height = 26f, AnchorOrigin = new(100f, 50f, 0f) };
            search.anchoredPosition = new(200f, -85f, 0f);
            GetCraftingHeaderButtonLayout(search, false, out Vector2 left, out Vector2 leftSize);
            GetCraftingHeaderButtonLayout(search, true, out Vector2 right, out Vector2 rightSize);
            Expect(leftSize == rightSize && leftSize.x * 2f + 4f == width, "Header buttons equally divide search width");
            Expect(left.x == 200f && right.x + rightSize.x == 200f + width && left.y == -55f && right.y == left.y,
                "Both header buttons follow the actual search position with one gap above it");
        }
        SortMode = CraftingRecipeSortMode.TierThenGroup;
        ToggleCraftingRecipeSortMode();
        Expect(SortMode == CraftingRecipeSortMode.GroupThenTier, "First click changes sort preference");
        ToggleCraftingRecipeSortMode();
        Expect(SortMode == CraftingRecipeSortMode.TierThenGroup, "Repeated click reads live preference instead of captured creation mode");
        Console.WriteLine($"Crafting tab/header layout: {Checks} checks passed (production methods; affine fake host, no Unity/game execution).");
    }
}
public class InventoryGui
{
    public static InventoryGui? instance;
    public static bool Visible = true;
    public static bool IsVisible() => Visible;
    public bool Closing;
    public Button m_tabCraft = null!;
    public Grid? m_playerGrid;
    public Image? m_craftingStationIcon;
    public RectTransform? m_craftingStationLevelRoot;
}
public sealed class Image { public RectTransform rectTransform = null!; }
public sealed class Grid { public int GetInstanceID() => 1; }
public sealed class Button { public Button(RectTransform rect) { transform = rect; } public RectTransform transform; public bool interactable = true; }
public class GameObject { public bool Active = true; public bool activeInHierarchy => Active; }
public record struct Vector3(float x, float y, float z)
{
    public static Vector3 operator +(Vector3 a, Vector3 b) => new(a.x+b.x, a.y+b.y, a.z+b.z);
    public static Vector3 operator -(Vector3 a, Vector3 b) => new(a.x-b.x, a.y-b.y, a.z-b.z);
}
public record struct Vector2(float x, float y)
{
    public static implicit operator Vector3(Vector2 v) => new(v.x, v.y, 0f);
    public static implicit operator Vector2(Vector3 v) => new(v.x, v.y);
}
public static class Mathf { public static float Abs(float f) => Math.Abs(f); public static float Max(float a, float b) => Math.Max(a,b); }
public class Transform
{
    public static int PositionWrites, SiblingWrites;
    public string Name = "";
    private Transform? Parent;
    private readonly List<Transform> Children = new();
    private Vector3 Position;
    public GameObject gameObject = new();
    public Vector3 Scale = new(1f, 1f, 1f), AnchorOrigin;
    public Vector3 localPosition { get => Position; set { Position = value; PositionWrites++; } }
    public Vector3 anchoredPosition { get => Position - AnchorOrigin; set => localPosition = value + AnchorOrigin; }
    public Transform? parent { get => Parent; set { Parent?.Children.Remove(this); Parent = value; Parent?.Children.Add(this); } }
    public int childCount => Children.Count;
    public Transform GetChild(int i) => Children[i];
    public Transform? Find(string name) => Children.FirstOrDefault(t => t.Name == name);
    public int GetSiblingIndex() => Parent?.Children.IndexOf(this) ?? 0;
    public void SetSiblingIndex(int i) { Parent!.Children.Remove(this); Parent.Children.Insert(Math.Clamp(i, 0, Parent.Children.Count), this); SiblingWrites++; }
    public Vector3 TransformPoint(Vector3 p)
    {
        var v = new Vector3(p.x * Scale.x, p.y * Scale.y, p.z * Scale.z) + Position;
        return Parent?.TransformPoint(v) ?? v;
    }
    public Vector3 InverseTransformPoint(Vector3 p)
    {
        var v = (Parent?.InverseTransformPoint(p) ?? p) - Position;
        return new(v.x / Scale.x, v.y / Scale.y, v.z / Scale.z);
    }
    public Button? Button;
    public T? GetComponent<T>() where T:class => Button as T;
}
public sealed class RectTransform : Transform
{
    public float Width, Pivot, Height = 30f;
    public (float xMin, float xMax, float yMax, float width, float height, Vector2 center) rect =>
        (-Width * Pivot, Width * (1f - Pivot), Height * (1f - Pivot), Width, Height, new(Width * (0.5f - Pivot), Height * (0.5f - Pivot)));
}
public sealed class RectTransformSnapshot
{
    public RectTransform Rect;
    private Transform? Parent;
    private Vector3 Position;
    private int Sibling;
    public RectTransformSnapshot(RectTransform rect) { Rect = rect; Parent = rect.parent; Position = rect.localPosition; Sibling = rect.GetSiblingIndex(); }
    public void Restore() { Rect.parent = Parent; Rect.localPosition = Position; Rect.SetSiblingIndex(Sibling); }
}
""";
}
