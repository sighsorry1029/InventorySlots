using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

internal static class PinnedTooltipLayerTests
{
    internal static void Run()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root != null && !File.Exists(Path.Combine(root.FullName, "PinnedTooltipCrafting.cs"))) root = root.Parent;
        if (root == null) throw new DirectoryNotFoundException("InventorySlots checkout");
        SyntaxNode Read(string file) => CSharpSyntaxTree.ParseText(File.ReadAllText(Path.Combine(root.FullName, file))).GetRoot();
        MethodDeclarationSyntax Method(string file, string name) => Read(file).DescendantNodes().OfType<MethodDeclarationSyntax>()
            .Single(m => m.Identifier.ValueText == name);
        string members = Read("InventoryActionPanels.cs").DescendantNodes().OfType<ClassDeclarationSyntax>()
            .Single(t => t.Identifier.ValueText == "InventoryOverlayHost").ToFullString();
        foreach (string name in new[] { "EnsureInventoryOverlayHost", "ClearSortButtonLayer", "EnsureActionPanel" })
            members += Method("InventoryActionPanels.cs", name).ToFullString();
        // Execute creation through the actual parent/panel calls; text/icon work
        // is unrelated to ownership and remains outside this geometry fixture.
        var create = Method("PinnedTooltipCrafting.cs", "EnsureCraftingPinnedTooltipPanel");
        members += create.WithBody(SyntaxFactory.Block(create.Body!.Statements.Take(2)
            .Append(SyntaxFactory.ParseStatement("return panel;")))).ToFullString();
        foreach (string name in new[] { "HideCraftingPinnedTooltip", "HideCraftingPinnedTooltips" })
            members += Method("PinnedTooltipCrafting.cs", name).ToFullString();
        members += "private static class TooltipController {";
        foreach (string name in new[] { "SetPinnedContext", "SyncCraftingPinnedContext", "IsCraftingPinnedContext", "IsCraftingContext" })
            members += Method("TooltipState.cs", name).ToFullString();
        members += "}";
        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator)
            .Select(path => MetadataReference.CreateFromFile(path));
        var compilation = CSharpCompilation.Create("PinnedTooltipLayerRegression",
            new[] { CSharpSyntaxTree.ParseText(Host.Replace("/* PRODUCTION */", members)) }, references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));
        using var stream = new MemoryStream();
        var result = compilation.Emit(stream);
        if (!result.Success) throw new Exception(string.Join("\n", result.Diagnostics));
        try { Assembly.Load(stream.ToArray()).GetType("PinHost")!.GetMethod("Check")!.Invoke(null, null); }
        catch (TargetInvocationException error) { throw error.InnerException!; }
    }

    // Managed hierarchy/translation/scale model, not Unity rendering. Run the
    // production follower and lifecycle against state changes, not copied logic.
    private const string Host = """
using System;
using System.Collections.Generic;
using System.Linq;
using Vector2 = Vec;
using Vector3 = Vec;
public static class PinHost
{
    /* PRODUCTION */
    private const string CraftingPinnedTooltipNamePrefix = "Pin";
    private enum PinnedTooltipContext { None, InventoryContainer, CraftingCraft, CraftingUpgrade, CraftingPathOfValheimanSalvage }
    private static class PinnedTooltips
    {
        public static PinnedTooltipContext ActiveContext;
        public static class Crafting
        {
            public static InventoryOverlayHost Host = new();
            public static RectTransform?[] Panels = new RectTransform?[3], JewelcraftingTooltipRoots = new RectTransform?[3], GemIconRows = new RectTransform?[3];
            public static int[] RecipeIndices = { -1, -1, -1 };
        }
    }
    private static class InventoryPanels
    {
        public static RectTransform? SortButtonLayer, InventorySortPanel;
        public static object? ContainerSortButton;
        public static InventoryOverlayHost PlayerSortHost = new(), ContainerSortHost = new();
    }
    private static class CraftingController { public static void MarkRecipeGridLayoutDirty() { } }
    private static bool IsUnityNull(object? value) => value == null;
    private static int Checks, ScrollResets;
    private static void ResetPinnedTooltipTextScrollState(RectTransform p) => ScrollResets++;
    private static void HideInventoryPinnedTooltips() { }
    private static void SetActionPanelActive(RectTransform? rect, bool active) { if (rect != null) rect.gameObject.SetActive(active); }
    private static RectTransform EnsurePinnedTooltipPanel(RectTransform parent, string name, RectTransform? current)
    { var panel = current ?? new RectTransform(); panel.SetParent(parent, false); return panel; }
    private static void Expect(bool ok, string text) { if (!ok) throw new Exception(text); Checks++; }
    private static void Near(Vec a, Vec b, string text) => Expect((a - b).Magnitude < 0.001f, text);
    public static void Check()
    {
        var root = new RectTransform { localPosition = new(50,70), localScale = new(1.3f,1.3f,1) };
        var gui = new InventoryGui { m_inventoryRoot = root, m_crafting = new RectTransform() };
        var crafting = gui.m_crafting;
        crafting.SetParent(root, false);
        crafting.localPosition = new(700,100); crafting.localScale = new(.8f,.9f,1);
        crafting.pivot = new(.3f,.7f); crafting.sizeDelta = new(600,500);
        var rootGroup = root.gameObject.AddComponent<CanvasGroup>(); rootGroup.alpha = .4f;
        var group = crafting.gameObject.AddComponent<CanvasGroup>(); group.alpha = .6f;
        group.interactable = false; group.blocksRaycasts = false;
        EnsureInventoryOverlayHost(gui, InventoryPanels.PlayerSortHost, crafting, "PlayerSortHost");
        var card = EnsureCraftingPinnedTooltipPanel(gui, 0);
        PinnedTooltips.Crafting.Panels[0] = card;
        var host = PinnedTooltips.Crafting.Host;
        Expect(card.parent == host.Rect && host.Rect!.parent == InventoryPanels.SortButtonLayer &&
            InventoryPanels.SortButtonLayer!.parent == root, "Crafting pin leaves the crafting branch for the raised layer");
        Expect(host.Rect.pivot == crafting.pivot && host.Rect.sizeDelta == crafting.sizeDelta, "Preserves the layout frame dimensions/pivot");
        Near(host.Rect.TransformPoint(new(45,-180)), crafting.TransformPoint(new(45,-180)), "Preserves card center with different ancestor scale");
        Near(host.Rect.TransformPoint(new(240,-380)), crafting.TransformPoint(new(240,-380)), "Preserves card corners and size");
        Expect(host.Group!.alpha == .6f && !host.Group.interactable && !host.Group.blocksRaycasts,
            "Copies Crafting opacity/input gates without multiplying common root opacity twice");
        EnsureInventoryOverlayHost(gui, InventoryPanels.ContainerSortHost, crafting, "ContainerSortHost");
        Expect(host.Rect.GetSiblingIndex() < InventoryPanels.ContainerSortHost.Rect!.GetSiblingIndex(), "Fixture creates container sort after pin");
        host.Follow(keepOnTop: true);
        Expect(host.Rect.GetSiblingIndex() == host.Rect.parent!.childCount - 1, "Delayed container sort cannot cover the pin");
        int writes = Transform.SiblingWrites;
        for (int i = 0; i < 120; i++) host.Follow(keepOnTop: true);
        Expect(Transform.SiblingWrites == writes, "Stable frames do not rewrite sibling order");
        crafting.localPosition = new(320,40); crafting.localScale = new(1.15f,.75f,1);
        crafting.sizeDelta = new(730,420); crafting.pivot = new(.8f,.2f); crafting.rotation = new(0,0,1);
        group.alpha = .2f; group.ignoreParentGroups = true;
        host.Follow(true);
        Near(host.Rect.TransformPoint(new(-45,100)), crafting.TransformPoint(new(-45,100)), "Follows moved/scaled crafting frame");
        Expect(host.Rect.rotation == crafting.rotation && host.Rect.pivot == crafting.pivot && host.Rect.sizeDelta == crafting.sizeDelta,
            "Refresh preserves rotation/dimensions/pivot");
        Expect(host.Group.alpha == .2f && host.Group.ignoreParentGroups, "Live opacity and ignore-parent gate follow source");
        crafting.gameObject.SetActive(false); host.Follow(true);
        Expect(!card.gameObject.activeInHierarchy && card.gameObject.activeSelf, "Hiding Crafting hides pin without discarding its active state");
        crafting.gameObject.SetActive(true); host.Follow(true);
        Expect(card.gameObject.activeInHierarchy, "Reactivating Crafting restores visibility");
        root.gameObject.SetActive(false); host.Follow(true);
        Expect(!card.gameObject.activeInHierarchy, "Hidden inventory root hides pins");
        root.gameObject.SetActive(true); host.Follow(true);
        PinnedTooltips.ActiveContext = PinnedTooltipContext.CraftingCraft;
        PinnedTooltips.Crafting.RecipeIndices[0] = 7;
        TooltipController.SyncCraftingPinnedContext(PinnedTooltipContext.CraftingCraft);
        Expect(card.gameObject.activeSelf, "Same crafting tab retains pin");
        TooltipController.SyncCraftingPinnedContext(PinnedTooltipContext.CraftingUpgrade);
        Expect(!card.gameObject.activeSelf && PinnedTooltips.Crafting.RecipeIndices[0] == -1 && ScrollResets > 0,
            "Tab switch still hides card, resets scroll and clears recipe");
        foreach (var next in new[] { PinnedTooltipContext.None, PinnedTooltipContext.InventoryContainer })
        {
            PinnedTooltips.ActiveContext = PinnedTooltipContext.CraftingCraft; card.gameObject.SetActive(true);
            PinnedTooltips.Crafting.RecipeIndices[0] = 3;
            TooltipController.SetPinnedContext(next);
            Expect(!card.gameObject.activeSelf && PinnedTooltips.Crafting.RecipeIndices[0] == -1, "Leaving crafting context hides elevated pin");
        }
        card.gameObject.SetActive(true); HideCraftingPinnedTooltips();
        Expect(!card.gameObject.activeSelf, "Explicit close hide remains independent of parent");
        var oldLayer = InventoryPanels.SortButtonLayer;
        gui.m_inventoryRoot = new RectTransform(); crafting.SetParent(gui.m_inventoryRoot, false);
        var replacement = EnsureCraftingPinnedTooltipPanel(gui, 1);
        Expect(oldLayer!.gameObject.Destroyed && host.Rect!.parent == InventoryPanels.SortButtonLayer &&
            InventoryPanels.SortButtonLayer!.parent == gui.m_inventoryRoot, "GUI replacement destroys old shared layer and rebinds host");
        ClearSortButtonLayer();
        Expect(host.Rect == null && host.Source == null && host.Group == null && host.SourceGroups.Length == 0,
            "Teardown releases crafting source and CanvasGroup references");
        Console.WriteLine($"Crafting pinned tooltip host: {Checks} checks passed (managed model, no Unity rendering).");
    }
}
public sealed class InventoryGui { public RectTransform m_inventoryRoot = new(), m_crafting = new(); }
public sealed class CanvasGroup { public bool isActiveAndEnabled = true, interactable = true, blocksRaycasts = true, ignoreParentGroups; public float alpha = 1; }
public readonly record struct Vec(float x, float y, float z = 0)
{
    public static Vec one => new(1,1,1);
    public float Magnitude => MathF.Sqrt(x*x+y*y+z*z);
    public static Vec operator +(Vec a, Vec b) => new(a.x+b.x,a.y+b.y,a.z+b.z);
    public static Vec operator -(Vec a, Vec b) => new(a.x-b.x,a.y-b.y,a.z-b.z);
    public static Vec operator *(Vec a, Vec b) => new(a.x*b.x,a.y*b.y,a.z*b.z);
    public static Vec operator /(Vec a, Vec b) => new(a.x/b.x,a.y/b.y,a.z/b.z);
}
public static class Mathf { public static float Abs(float f) => Math.Abs(f); }
public static class Quaternion { public static Vec identity => default; }
public class Transform
{
    private Transform? Parent;
    private readonly List<Transform> Children = new();
    public readonly GameObject gameObject;
    public string name = "";
    public static int SiblingWrites;
    public Transform() { gameObject = new(this); }
    public Transform? parent { get => Parent; set { Parent?.Children.Remove(this); Parent=value; Parent?.Children.Add(this); } }
    public int childCount => Children.Count;
    public int GetSiblingIndex() => parent?.Children.IndexOf(this) ?? 0;
    public void SetAsLastSibling() { if (Parent == null) return; Parent.Children.Remove(this); Parent.Children.Add(this); SiblingWrites++; }
    public void SetParent(Transform value, bool worldPositionStays) => parent = value;
    public Transform? Find(string name) => Children.FirstOrDefault(c => c.name == name);
    public T[] GetComponents<T>() => gameObject.Components.OfType<T>().ToArray();
    public Vec localPosition, localScale = Vec.one, rotation, localRotation;
    public Vec lossyScale => (parent?.lossyScale ?? Vec.one) * localScale;
    public Vec position { get => (parent?.position ?? default) + localPosition * (parent?.lossyScale ?? Vec.one);
        set => localPosition = (value - (parent?.position ?? default)) / (parent?.lossyScale ?? Vec.one); }
    public Vec TransformPoint(Vec point) => position + point * lossyScale;
}
public sealed class RectTransform : Transform { public Vec pivot, sizeDelta, anchorMin, anchorMax; public (Vec size, float unused) rect => (sizeDelta,0); }
public sealed class Image { }
public sealed class GameObject
{
    public readonly Transform transform;
    public readonly List<object> Components = new();
    public bool activeSelf = true, Destroyed;
    public bool activeInHierarchy => activeSelf && !Destroyed && (transform.parent?.gameObject.activeInHierarchy ?? true);
    public GameObject(Transform transform) => this.transform = transform;
    public GameObject(string name, params Type[] components) { transform = new RectTransform(); transform.name=name; }
    public T AddComponent<T>() where T:new() { T value=new(); Components.Add(value!); return value; }
    public void SetActive(bool value) => activeSelf=value;
}
namespace UnityEngine { public static class Object { public static void Destroy(global::GameObject go) { go.Destroyed=true; go.SetActive(false); } } }
""";
}
