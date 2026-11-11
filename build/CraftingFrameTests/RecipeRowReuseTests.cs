using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

internal static class RecipeRowReuseTests
{
    internal static void Run()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root != null && !File.Exists(Path.Combine(root.FullName, "CraftingRecipeRowReuse.cs"))) root = root.Parent;
        if (root == null) throw new DirectoryNotFoundException("InventorySlots checkout");
        var source = CSharpSyntaxTree.ParseText(File.ReadAllText(Path.Combine(root.FullName, "CraftingRecipeRowReuse.cs"))).GetRoot();
        string code = source.DescendantNodes().OfType<ClassDeclarationSyntax>()
            .Single(c => c.Identifier.ValueText == "CraftingRecipeRowReuse").ToFullString();
        var refs = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator)
            .Select(path => MetadataReference.CreateFromFile(path));
        var compilation = CSharpCompilation.Create("RecipeRowReuseRegression",
            new[] { CSharpSyntaxTree.ParseText(Host.Replace("/* PRODUCTION */", code)) }, refs,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));
        using var stream = new MemoryStream();
        var result = compilation.Emit(stream);
        if (!result.Success) throw new Exception(string.Join("\n", result.Diagnostics));
        try { Assembly.Load(stream.ToArray()).GetType("Checks")!.GetMethod("Run")!.Invoke(null, null); }
        catch (TargetInvocationException error) { throw error.InnerException!; }
    }

    // Run the production lifetime/lease/listener code against counted Unity
    // boundaries. Native body transformation is checked separately on original IL.
    private const string Host = """
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using Object = UnityEngine.Object;
/* PRODUCTION */
internal static class CraftingRecipeRowReuseHooks { internal static bool Permit() => !Harmony.UnknownHook; }
public static class Checks
{
    private static int checks, clicks, prefabClicks;
    private static void Expect(bool value, string name) { checks++; if (!value) throw new Exception(name); }
    private static void PrefabClick() => prefabClicks++;
    private static Recipe R(string name) => new() { Name = name };
    private static GameObject Add(InventoryGui gui, Recipe recipe, ItemDrop.ItemData? item = null, bool fail = false, bool postfixFail = false)
    {
        var state = CraftingRecipeRowReuse.EnterRow(gui);
        Exception? error = null;
        try
        {
            var row = CraftingRecipeRowReuse.Rent(gui.m_recipeElementPrefab, gui.m_recipeListRoot, gui, recipe, item);
            row.SetActive(true);
            // Model fields assigned by the original AddRecipeToList body.
            row.Text = recipe.Name + ":" + recipe.Quantity;
            row.CanCraft = recipe.CanCraft;
            row.Quality = item?.Quality ?? 0;
            row.GetComponent<Image>().enabled = true;
            CraftingRecipeRowReuse.Bind(row.GetComponent<Button>().onClick, () => { gui.Clicked = row; clicks++; }, gui);
            if (fail) throw new InvalidOperationException("native initialization");
            gui.Rows.Add(row);
            CraftingRecipeRowReuse.CompleteRow(gui);
            if (postfixFail) throw new InvalidOperationException("foreign postfix");
            return row;
        }
        catch (Exception e) { error = e; throw; }
        finally { Expect(ReferenceEquals(CraftingRecipeRowReuse.LeaveRow(state, error), error), "row exception identity"); }
    }
    private static void Rebuild(InventoryGui gui, Action body, bool skip = false)
    {
        var state = CraftingRecipeRowReuse.Enter(gui);
        Exception? error = null;
        try
        {
            if (skip) { body(); return; }
            CraftingRecipeRowReuse.Begin(gui);
            foreach (var row in gui.Rows) CraftingRecipeRowReuse.Release(row, gui);
            gui.Rows.Clear();
            body();
            CraftingRecipeRowReuse.End(gui);
        }
        catch (Exception e) { error = e; throw; }
        finally { Expect(ReferenceEquals(CraftingRecipeRowReuse.Leave(state, error), error), "list exception identity"); }
    }
    public static void Run()
    {
        CraftingRecipeRowReuse.ListReady = CraftingRecipeRowReuse.RowReady = true;
        Player.m_localPlayer = new();
        var gui = new InventoryGui();
        gui.m_recipeElementPrefab.GetComponent<Button>().onClick.AddListener(PrefabClick);
        var recipes = Enumerable.Range(0, 1801).Select(n => R("Recipe " + n)).ToArray();
        Rebuild(gui, () => { foreach (var r in recipes) Add(gui, r); });
        var first = gui.Rows.ToArray();
        int created = Object.Created, destroyed = Object.Destroyed;
        for (int pass = 0; pass < 20; pass++)
        {
            int p = pass;
            foreach (var row in gui.Rows) { row.Selected.SetActive(true); row.GetComponent<Button>().interactable = false; row.GetComponent<Image>().enabled = false; }
            recipes[0].Name = "Localized " + p; recipes[0].Quantity++; recipes[0].CanCraft = p % 2 == 0;
            Rebuild(gui, () => { foreach (var r in Enumerable.Reverse(recipes)) Add(gui, r); });
            Expect(gui.Rows.SequenceEqual(Enumerable.Reverse(first)), "all 1801 recipe identities follow reordered results");
            var tested = gui.Rows[^1];
            Expect(tested.Text == "Localized " + p + ":" + recipes[0].Quantity && tested.CanCraft == (p % 2 == 0), "fresh native name/count/requirements");
            Expect(!tested.Selected.activeSelf && tested.GetComponent<Button>().interactable && tested.GetComponent<Image>().enabled, "fresh clone defaults restored");
            tested.GetComponent<Button>().onClick.Invoke();
            Expect(clicks == p + 1 && prefabClicks == p + 1 && ReferenceEquals(gui.Clicked, tested), "one native click plus preserved prefab listener");
        }
        Expect(Object.Created == created && Object.Destroyed == destroyed, "20 repeated 1801-row rebuilds create/destroy no rows");
        Rebuild(gui, () => Add(gui, recipes[0]));
        Expect(Object.Destroyed == destroyed + 1800 && gui.Rows[0] == first[0], "nocost shrink destroys unused rows, no spare pool");
        Rebuild(gui, () => { Add(gui, recipes[0]); Add(gui, recipes[1]); });
        Expect(Object.Created == created + 1 && gui.Rows[0] == first[0] && gui.Rows[1] != first[1], "removed recipe is newly constructed when it returns");

        var a = new ItemDrop.ItemData { Quality = 1 }; var b = new ItemDrop.ItemData { Quality = 2 };
        Rebuild(gui, () => { Add(gui, recipes[0], a); Add(gui, recipes[0], b); });
        var old = gui.Rows.ToArray(); a.Quality = 3;
        Rebuild(gui, () => { Add(gui, recipes[0], b); Add(gui, recipes[0], a); });
        Expect(gui.Rows[0] == old[1] && gui.Rows[1] == old[0] && gui.Rows[1].Quality == 3, "upgrade identity and changed quality preserved");
        Rebuild(gui, () => { Add(gui, recipes[0]); Add(gui, recipes[0]); });
        old = gui.Rows.ToArray(); created = Object.Created;
        Rebuild(gui, () => { Add(gui, recipes[0]); Add(gui, recipes[0]); });
        Expect(gui.Rows.Distinct().Count() == 2 && gui.Rows.All(r => old.Contains(r)) && Object.Created == created, "duplicate pair has distinct reusable rows");

        // A native/foreign tab round trip must not borrow a previously tracked row.
        InventorySlotsPlugin.Allowed = false; old = gui.Rows.ToArray();
        Rebuild(gui, () => Add(gui, recipes[0]));
        Expect(old.All(r => r.Dead) && !old.Contains(gui.Rows[0]), "foreign tab uses ordinary destruction/creation");
        InventorySlotsPlugin.Allowed = true; var foreign = gui.Rows[0];
        Rebuild(gui, () => Add(gui, recipes[0]));
        Expect(foreign.Dead && gui.Rows[0] != foreign, "foreign rows are never adopted into pool");
        var current = gui.Rows[0];
        Rebuild(gui, () => { }, skip: true);
        Expect(gui.Rows[0] == current && !current.Dead, "skipped original list has no pooling side effects");

        // Late unknown Harmony registration opts out without altering the hook.
        Harmony.UnknownHook = true;
        Rebuild(gui, () => Add(gui, recipes[0]));
        Expect(current.Dead && gui.Rows[0] != current, "late unknown hook falls back");
        Harmony.UnknownHook = false;
        foreach (bool listMissing in new[] { false, true })
        {
            current = gui.Rows[0];
            CraftingRecipeRowReuse.ListReady = !listMissing; CraftingRecipeRowReuse.RowReady = listMissing;
            Rebuild(gui, () => Add(gui, recipes[0]));
            Expect(current.Dead && gui.Rows[0] != current, "partial transpiler support cannot retain rows");
        }
        CraftingRecipeRowReuse.ListReady = CraftingRecipeRowReuse.RowReady = true;

        Rebuild(gui, () => Add(gui, recipes[0])); current = gui.Rows[0];
        try { Rebuild(gui, () => Add(gui, recipes[0], fail: true)); } catch (InvalidOperationException) { }
        Expect(current.Dead && gui.Rows.Count == 0, "failed rented row is destroyed and original exception preserved");
        try { Rebuild(gui, () => Add(gui, recipes[0], postfixFail: true)); } catch (InvalidOperationException) { }
        Expect(gui.Rows.Count == 1 && !gui.Rows[0].Dead, "committed row survives foreign postfix failure");
        current = gui.Rows[0];
        Rebuild(gui, () => Add(gui, recipes[0]));
        Expect(current.Dead, "exception invalidates cache before next rebuild");

        current = gui.Rows[0];
        Rebuild(gui, () => { Rebuild(gui, () => Add(gui, recipes[1])); Add(gui, recipes[0]); });
        Expect(current.Dead && gui.Rows.All(r => !r.Dead) && gui.Rows.Count == 2, "reentrant list preserves native results and clears suspended leases");
        old = gui.Rows.ToArray();
        Rebuild(gui, () => Add(gui, recipes[0]));
        Expect(old.All(r => r.Dead), "reentrant scope invalidates reuse ownership");

        foreach (Action replace in new Action[] { () => Player.m_localPlayer = new(),
            () => gui.m_recipeElementPrefab = new(), () => gui.m_recipeListRoot = new(),
            () => CraftingRecipeRowReuse.Clear(gui) })
        {
            current = gui.Rows[0]; replace(); Rebuild(gui, () => Add(gui, recipes[0]));
            Expect(current.Dead && gui.Rows[0] != current, "player/template/root/destruction invalidation");
        }
        var gui2 = new InventoryGui(); current = gui.Rows[0];
        Rebuild(gui2, () => Add(gui2, recipes[0]));
        CraftingRecipeRowReuse.Clear(gui); // Destroying an old GUI must not discard the new GUI cache.
        var second = gui2.Rows[0]; Rebuild(gui2, () => Add(gui2, recipes[0]));
        Expect(gui2.Rows[0] == second && !current.Dead, "GUI replacement does not destroy another GUI's active rows");
        current = gui2.Rows[0]; Object.Destroy(current);
        Rebuild(gui2, () => Add(gui2, recipes[0]));
        Expect(gui2.Rows[0] != current && !gui2.Rows[0].Dead, "foreign destruction/fake-null is detected");
        current = gui2.Rows[0]; current.transform.parent = new RectTransform();
        Rebuild(gui2, () => Add(gui2, recipes[0]));
        Expect(current.Dead && gui2.Rows[0] != current, "reparented row follows native destruction");
        CraftingRecipeRowReuse.Clear();
        Console.WriteLine($"Native recipe row reuse: {checks} checks passed (1801 rows x 20 rebuilds, listeners, state refresh, shrink, upgrades, tab/owner changes, exceptions, reentrancy; simulated Unity boundaries).");
    }
}
public static class InventorySlotsPlugin
{
    public static bool Allowed = true;
    public static bool CanReuseNativeCraftingRows(InventoryGui gui) => Allowed;
    public static Logger Log = new();
}
public class Logger { public void LogWarning(string text) { } }
public class Player { public static Player m_localPlayer = new(); }
public class Recipe { public string Name = ""; public int Quantity = 1; public bool CanCraft = true; }
public class ItemDrop { public class ItemData { public int Quality; } }
public class InventoryGui
{
    public GameObject m_recipeElementPrefab = new(); public RectTransform m_recipeListRoot = new();
    public List<GameObject> Rows = new(); public GameObject? Clicked;
    public void AddRecipeToList(Player p, Recipe r, ItemDrop.ItemData item, bool canCraft) { }
}
namespace UnityEngine
{
    public class Object
    {
        public bool Dead; public static int Created, Destroyed;
        public static T Instantiate<T>(T original, Transform parent) where T : Object
        {
            var from = (GameObject)(object)original;
            var go = new GameObject(); go.transform.parent = parent;
            go.Selected.activeSelf = from.Selected.activeSelf; go.Image.enabled = from.Image.enabled;
            go.Button.interactable = from.Button.interactable;
            go.Button.onClick.CopyFrom(from.Button.onClick);
            Created++; return (T)(object)go;
        }
        public static void Destroy(Object value) { if (!value.Dead) { value.Dead = true; Destroyed++; } }
        public static bool operator ==(Object? a, Object? b)
        { bool an = ReferenceEquals(a, null) || a.Dead, bn = ReferenceEquals(b, null) || b.Dead; return an || bn ? an == bn : ReferenceEquals(a,b); }
        public static bool operator !=(Object? a, Object? b) => !(a == b);
        public override bool Equals(object? other) => ReferenceEquals(this, other);
        public override int GetHashCode() => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(this);
    }
    public class Transform : Object
    {
        public Transform? parent; public GameObject gameObject = null!;
        public void SetAsLastSibling() { }
        public Transform? Find(string name) => name == "selected" ? gameObject.Selected.transform : name == "icon" ? this : null;
        public T GetComponent<T>() where T : class => gameObject.GetComponent<T>();
    }
    public class RectTransform : Transform { }
    public class GameObject : Object
    {
        public bool activeSelf; public Transform transform; public GameObject Selected;
        public Button Button = new(); public Image Image = new(); public string Text = ""; public bool CanCraft; public int Quality;
        public GameObject(bool child = false)
        { transform = new Transform { gameObject = this }; Selected = child ? this : new GameObject(true); }
        public void SetActive(bool value) => activeSelf = value;
        public T GetComponent<T>() where T : class => (typeof(T) == typeof(Button) ? (object)Button : Image) as T ?? throw new Exception("unknown component");
    }
}
namespace UnityEngine.Events
{
    public delegate void UnityAction();
    public class UnityEvent
    {
        private readonly List<UnityAction> listeners = new();
        public void AddListener(UnityAction action) => listeners.Add(action);
        public void RemoveListener(UnityAction action) => listeners.RemoveAll(a => a == action);
        public void Invoke() { foreach (var action in listeners.ToArray()) action(); }
        public void CopyFrom(UnityEvent other) => listeners.AddRange(other.listeners);
    }
}
namespace UnityEngine.UI
{
    public class Button : Object { public bool interactable = true; public ButtonClickedEvent onClick = new(); public class ButtonClickedEvent : UnityEvent { } }
    public class Image : Object { public bool enabled = true; }
}
namespace HarmonyLib
{
    public static class AccessTools { public static MethodInfo Method(Type t, string name, Type[] args) => t.GetMethod(name, args)!; }
    public class Patch { public string owner = "foreign"; public MethodInfo PatchMethod = typeof(Harmony).GetMethod("Foreign")!; }
    public class Patches { public Patch[] Prefixes = Array.Empty<Patch>(), Postfixes = Array.Empty<Patch>(), Transpilers = Array.Empty<Patch>(), Finalizers = Array.Empty<Patch>(); }
    public static class Harmony
    {
        public static bool UnknownHook;
        public static Patches? GetPatchInfo(MethodInfo m) => UnknownHook ? new Patches { Postfixes = new[] { new Patch() } } : null;
        public static void Foreign() { }
    }
}
""";
}
