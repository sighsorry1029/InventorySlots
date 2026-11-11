using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

internal static class CharacterNavigationTests
{
    internal static void Run()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root != null && !File.Exists(Path.Combine(root.FullName, "PathOfValheimanCharacterUi.cs"))) root = root.Parent;
        if (root == null) throw new DirectoryNotFoundException("InventorySlots checkout");
        MethodDeclarationSyntax Read(string file, string name) => CSharpSyntaxTree.ParseText(File.ReadAllText(Path.Combine(root.FullName, file)))
            .GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>().Single(m => m.Identifier.ValueText == name);
        string members = string.Concat(new[] { "SetPathOfValheimanCharacter", "SuspendPathOfValheimanCharacterUi",
            "ClosePathOfValheimanCharacterUi", "ClearPathOfValheimanCharacterUi", "FailPathOfValheimanCharacterTab" }
            .Select(name => Read("PathOfValheimanCharacterUi.cs", name).ToFullString()));
        var update = Read("PathOfValheimanCharacterUi.cs", "UpdatePathOfValheimanCharacterUiCore");
        // Exercise the real lifecycle/router up to layout. Native rendering and PoV panels are counted boundaries.
        members += update.WithBody(SyntaxFactory.Block(update.Body!.Statements.TakeWhile(s => !s.ToString().StartsWith("RectTransform craft =")))).ToFullString();
        var show = Read("InventoryPatchHandlers.cs", "OnInventoryGuiShow");
        members += show.WithBody(SyntaxFactory.Block(show.Body!.Statements.Take(1))).ToFullString();
        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator)
            .Select(path => MetadataReference.CreateFromFile(path));
        var compilation = CSharpCompilation.Create("CharacterNavigationRegression",
            new[] { CSharpSyntaxTree.ParseText(Host.Replace("/* PRODUCTION */", members)) }, references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        using var stream = new MemoryStream();
        var result = compilation.Emit(stream);
        if (!result.Success) throw new Exception(string.Join("\n", result.Diagnostics));
        try { Assembly.Load(stream.ToArray()).GetType("Host")!.GetMethod("Check")!.Invoke(null, null); }
        catch (TargetInvocationException error) { throw error.InnerException!; }
    }

    private const string Host = """
using System;
using System.Collections.Generic;
public static class Host
{
    /* PRODUCTION */
    private static PovCharacterView _povCharacterView;
    private static bool IsDedicatedServer, Available = true, Preview;
    private static bool Anvil;
    private static bool IsPathOfValheimanAnvilActive(InventoryGui gui) => Anvil;
    private static int Checks, LookRefreshes;
    private static int PoseFollows, BackgroundFollows;
    private static void CopyPovCharacterRect(RectTransform source, RectTransform target)
    { target.Position = source.Position; PoseFollows++; }
    private static void SyncPathOfValheimanCharacterBackground(PovCharacterView view) => BackgroundFollows++;
    private static Func<bool> _povEnchantOpen = () => false;
    private static Action _povCloseEnchant = () => { };
    private static Factory _povSkillsFactory = new(), _povStatsFactory = new();
    private static bool CanShowPathOfValheimanCharacter(Player p) => p != null && Available;
    private static bool IsInventoryPanelClosing(InventoryGui gui) => gui.Closing;
    private static object _povAppearanceCameraFailedView;
    private static void ResetPathOfValheimanAppearanceCamera() { }
    private static void ValidatePathOfValheimanAppearanceCamera() { }
    private static float PovCharacterCraftTimer(InventoryGui gui) => gui.Timer;
    private static object PovCharacterDragItem(InventoryGui gui) => gui.Drag;
    private static void ClearPathOfValheimanLookPreview() => Preview = false;
    private static void PovSelectCraftTab(InventoryGui gui) { }
    private static void ClearCraftingQueue() { }
    private static void HideCraftingPinnedTooltips() { }
    private static void LayoutPathOfValheimanCharacter(PovCharacterView view) { }
    private static void RefreshPathOfValheimanLooks(PovCharacterView view) => LookRefreshes++;
    private static PovCharacterView CreatePathOfValheimanCharacterView(InventoryGui gui, Player player) => new() { Gui = gui, Player = player };
    private static class CraftingController { internal static void ClearHoveredRecipe() { } }
    private static class Log { internal static void LogWarning(string text) { } }
    private sealed class PovCharacterView
    {
        internal InventoryGui Gui;
        internal Player Player;
        internal bool Active, Closing, CharacterSelected, OwnGroup, OriginalInteractable, OriginalRaycasts;
        internal float OriginalAlpha;
        internal int Tab, Language;
        internal object Size, Target, Picked, CurrentLook, CurrentLookTarget;
        internal string CurrentLookKey;
        internal List<object> Looks = new();
        internal RectTransform Root = new();
        internal Button Toggle = new(), Title = new();
        internal CanvasGroup CraftingGroup = new();
        internal bool[] Failed = new bool[3];
        internal Panel[] Native = new Panel[3];
        internal RectTransform[] Pages = { new(), new(), new() };
        internal Button[] Tabs = { new(), new(), new() };
    }
    private static void Expect(bool value, string label)
    { if (!value) throw new Exception(label); Checks++; }
    private static PovCharacterView Fresh()
    {
        Available = true; InventoryGui.Visible = true;
        InventoryGui.instance = new(); Player.m_localPlayer = new();
        return _povCharacterView = CreatePathOfValheimanCharacterView(InventoryGui.instance, Player.m_localPlayer);
    }
    private static void Hide(bool finish)
    {
        InventoryGui.instance.Closing = true; InventoryGui.Visible = false;
        SuspendPathOfValheimanCharacterUi();
        UpdatePathOfValheimanCharacterUiCore();
        if (!finish) return;
        InventoryGui.instance.m_inventoryRoot.gameObject.SetActive(false);
        UpdatePathOfValheimanCharacterUiCore();
    }
    private static void Show()
    {
        InventoryGui.instance.Closing = false;
        OnInventoryGuiShow(false); // Existing real Show cleanup must not erase navigation.
        InventoryGui.Visible = true;
        InventoryGui.instance.m_inventoryRoot.gameObject.SetActive(true);
        UpdatePathOfValheimanCharacterUiCore();
    }
    public static void Check()
    {
        for (int tab = 0; tab < 3; tab++)
        {
            var view = Fresh(); SetPathOfValheimanCharacter(view, true, tab);
            var native = view.Native[tab];
            // The omitted open-frame renderer masks Crafting before the user closes it.
            view.CraftingGroup.alpha = 0;
            view.CraftingGroup.interactable = view.CraftingGroup.blocksRaycasts = false;
            Preview = true; view.Target = view.Picked = new object(); view.Looks.Add(new());
            Hide(false);
            Expect(view.Active && view.Closing && !Preview, "Outro retains rendered page but cancels preview immediately");
            int beforePose = PoseFollows, beforeBackground = BackgroundFollows, beforeRefresh = LookRefreshes;
            for (int frame = 1; frame <= 3; frame++)
            {
                InventoryGui.instance.m_crafting.Position = -100 * frame;
                UpdatePathOfValheimanCharacterUiCore();
                Expect(view.Root.Position == InventoryGui.instance.m_crafting.Position,
                    "Visible Character outro follows each animated crafting pose");
                Expect(view.Active && view.Closing && view.Tab == tab && !view.Toggle.gameObject.activeInHierarchy &&
                    view.CraftingGroup.alpha == 0 && !view.CraftingGroup.blocksRaycasts && !Preview,
                    "Following outro cannot restore Crafting, controls or appearance preview");
            }
            Expect(PoseFollows == beforePose + 3 && BackgroundFollows == beforeBackground + 3 && LookRefreshes == beforeRefresh,
                "Closing follows geometry without refreshing Character content");
            InventoryGui.instance.m_inventoryRoot.gameObject.SetActive(false);
            UpdatePathOfValheimanCharacterUiCore();
            Expect(!view.Active && view.CharacterSelected && view.Tab == tab, "Hidden UI retains only navigation choice");
            Expect(view.Target == null && view.Picked == null && view.Looks.Count == 0 && !view.Root.gameObject.activeInHierarchy,
                "Hide releases appearance targets and hides UI");
            int refreshes = LookRefreshes;
            for (int frame = 0; frame < 20; frame++) UpdatePathOfValheimanCharacterUiCore();
            Expect(!view.Active && LookRefreshes == refreshes && (native == null || !native.Visible),
                "Closed idle frames do not reopen or refresh the remembered page");
            Show();
            Expect(view.Active && view.Tab == tab && view.Pages[tab].gameObject.activeInHierarchy && !Preview,
                "Reopen returns to the same Character tab without restoring preview");
            Expect(ReferenceEquals(native, view.Native[tab]), "Reopen reuses the existing native view");
            if (native != null) Expect(native.Visible && native.Closures > 0, "Native page is closed then shown again");
            OnInventoryGuiShow(true); UpdatePathOfValheimanCharacterUiCore();
            Expect(view.Active && view.Tab == tab, "Repeated Show while visible keeps the selection");

            SetPathOfValheimanCharacter(view, false, tab); Hide(true); Show();
            Expect(!view.Active && !view.CharacterSelected && view.Tab == tab, "Explicit Crafting selection persists too");
            SetPathOfValheimanCharacter(view, true, view.Tab);
            Expect(view.Active && view.Tab == tab, "Returning from Crafting remembers the Character subtab");
            Hide(false); Show();
            Expect(view.Active && !view.Closing && view.Tab == tab, "Rapid reopen during outro restores the same page");
            Anvil = true; Preview = true;
            UpdatePathOfValheimanCharacterUiCore();
            Expect(!view.Active && view.CharacterSelected && view.Tab == tab && !Preview &&
                view.CraftingGroup.alpha == 1 && view.CraftingGroup.blocksRaycasts,
                "Anvil releases Character alpha/raycast ownership and preview but retains navigation");
            SetPathOfValheimanCharacter(view, true, tab);
            Expect(!view.Active, "Character callbacks cannot reopen over an active anvil");
            Anvil = false; UpdatePathOfValheimanCharacterUiCore();
            Expect(view.Active && view.Tab == tab, "Leaving anvil restores remembered Character page");
        }
        var current = Fresh(); SetPathOfValheimanCharacter(current, true, 2); Hide(true);
        InventoryGui.instance.Timer = 1; Show();
        Expect(!current.Active && current.CharacterSelected, "Restoration does not interrupt ongoing crafting");
        InventoryGui.instance.Timer = -1; InventoryGui.instance.Drag = new(); UpdatePathOfValheimanCharacterUiCore();
        Expect(!current.Active, "Restoration does not interrupt item dragging");
        InventoryGui.instance.Drag = null; UpdatePathOfValheimanCharacterUiCore();
        Expect(current.Active && current.Tab == 2, "Restoration resumes at a safe boundary");
        FailPathOfValheimanCharacterTab(current, 2, new Exception("fixture"));
        UpdatePathOfValheimanCharacterUiCore();
        Expect(!current.Active && !current.CharacterSelected && current.Failed[2], "Failed page is not reopened every frame");

        current = Fresh(); SetPathOfValheimanCharacter(current, true, 1);
        Player.m_localPlayer = new(); UpdatePathOfValheimanCharacterUiCore();
        Expect(!ReferenceEquals(current, _povCharacterView) && !_povCharacterView.CharacterSelected && _povCharacterView.Tab == 0,
            "Changing player discards remembered navigation");
        current = Fresh(); SetPathOfValheimanCharacter(current, true, 2);
        InventoryGui.instance = new(); UpdatePathOfValheimanCharacterUiCore();
        Expect(!ReferenceEquals(current, _povCharacterView) && !_povCharacterView.CharacterSelected,
            "Replacing GUI discards old selection and panel ownership");
        ClearPathOfValheimanCharacterUi();
        Expect(_povCharacterView == null && !Preview, "Disposal releases all state");
        Console.WriteLine($"Character navigation: {Checks} checks passed (managed UI boundaries, no rendered game).");
    }
}
public class Player { public static Player m_localPlayer; }
public class InventoryGui
{
    public static InventoryGui instance;
    public static bool Visible;
    public static bool IsVisible() => Visible;
    public bool Closing;
    public float Timer = -1;
    public object Drag;
    public RectTransform m_inventoryRoot = new(), m_crafting = new();
}
public class GameObject { public bool activeInHierarchy = true; public void SetActive(bool value) => activeInHierarchy = value; }
public class RectTransform { public GameObject gameObject = new(); public Rect rect = new(); public float Position; }
public class Rect { public object size = new(); }
public class Button { public bool interactable; public GameObject gameObject = new(); public object font; }
public class CanvasGroup { public float alpha = 1; public bool interactable = true, blocksRaycasts = true; }
public class Panel
{
    public bool Visible; public int Closures;
    public Action Closed;
    public Panel() { Closed = () => Closures++; }
    public void Show(bool value) => Visible = value;
    public void Fit(object pos, object size) { }
    public void Dispose() { }
}
public class Factory { public Panel Create(InventoryGui gui, RectTransform root, object font) => new(); }
public static class Vector2 { public static object zero = new(); }
namespace UnityEngine { public static class Object { public static void Destroy(object value) { } } }
""";
}
