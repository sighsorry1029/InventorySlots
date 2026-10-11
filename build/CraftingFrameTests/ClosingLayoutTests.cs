using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

internal static class ClosingLayoutTests
{
    internal static void Run()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root != null && !File.Exists(Path.Combine(root.FullName, "InventoryUiController.cs"))) root = root.Parent;
        if (root == null) throw new DirectoryNotFoundException("InventorySlots checkout");
        SyntaxNode Read(string path) => CSharpSyntaxTree.ParseText(File.ReadAllText(Path.Combine(root.FullName, path))).GetRoot();
        MethodDeclarationSyntax Method(string file, string name) => Read(file).DescendantNodes().OfType<MethodDeclarationSyntax>()
            .Single(m => m.Identifier.ValueText == name);
        string PatchEntry(string file, string type, string name) => Read(file).DescendantNodes().OfType<ClassDeclarationSyntax>()
            .Single(c => c.Identifier.ValueText == type).Members.OfType<MethodDeclarationSyntax>()
            .Single(m => m.Identifier.ValueText == "Prefix").WithIdentifier(SyntaxFactory.Identifier(name))
            .WithoutTrivia().ToFullString();

        var ui = Read("InventoryUiController.cs");
        string members = string.Concat(ui.DescendantNodes().OfType<FieldDeclarationSyntax>()
            .Where(f => f.Declaration.Variables.Any(v => v.Identifier.ValueText.StartsWith("_inventoryClosing"))).Select(f => f.ToFullString()));
        foreach (string name in new[] { "IsInventoryClosingLayoutPending", "CancelInventoryClosingLayout", "ClearInventoryClosingLayout", "DeferInventoryClosingLayoutRestore", "UpdateInventoryClosingLayout" })
            members += Method("InventoryUiController.cs", name).ToFullString();
        members += Method("InventoryPatchHandlers.cs", "OnInventoryGuiHide").ToFullString();
        var craft = Method("CraftingRecipeActions.cs", "CanStartCraftingAction");
        members += craft.WithBody(SyntaxFactory.Block(craft.Body!.Statements.Take(1)
            .Append(SyntaxFactory.ParseStatement("return true;")))).ToFullString();
        // Execute the real stat-panel entry until the first layout construction.
        var stats = Method("InventoryStatPanels.cs", "UpdatePlayerStatPanels");
        members += stats.WithBody(SyntaxFactory.Block(stats.Body!.Statements.TakeWhile(s => !s.ToString().StartsWith("RectTransform host ="))
            .Append(SyntaxFactory.ParseStatement("StatLayouts++;")))).ToFullString();
        members += PatchEntry("ContainerGuiPatches.cs", "InventoryGuiShowValidateInventoryPatch", "ShowPrefix");
        members += PatchEntry("CraftingPatches.cs", "InventoryGuiCraftingViewDestroyPatch", "DestroyPrefix");
        members += PatchEntry("CraftingPatches.cs", "InventoryGuiCraftingRecipeRedesignPatch", "RecipePrefix");
        members += PatchEntry("InventoryInteractionPatches.cs", "InventoryGuiUpdateInventoryPatch", "InventoryPrefix");
        string source = Host.Replace("/* PRODUCTION */", members);
        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator)
            .Select(path => MetadataReference.CreateFromFile(path));
        var compilation = CSharpCompilation.Create("ClosingLayoutRegression", new[] { CSharpSyntaxTree.ParseText(source) }, references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));
        using var stream = new MemoryStream();
        var result = compilation.Emit(stream);
        if (!result.Success) throw new Exception(string.Join("\n", result.Diagnostics));
        try { Assembly.Load(stream.ToArray()).GetType("InventorySlotsPlugin")!.GetMethod("Check")!.Invoke(null, null); }
        catch (TargetInvocationException error) { throw error.InnerException!; }
    }

    private const string Host = """
using System;
public static class InventorySlotsPlugin
{
    /* PRODUCTION */
    private static int Checks, StatRestores, CraftRestores, QueueClears, PinHides, HoverClears, StatLayouts;
    private static InputField? _craftingCountInput = new();
    private static class CraftingUi { public static InputField SearchInput = new(); }
    private static void Expect(bool condition, string label)
    {
        if (!condition) throw new Exception(label);
        Checks++;
    }
    private static InventoryGui Fresh(bool active = true)
    {
        ClearInventoryClosingLayout();
        StatRestores = CraftRestores = QueueClears = PinHides = HoverClears = StatLayouts = 0;
        var gui = new InventoryGui();
        gui.m_inventoryRoot!.gameObject.activeInHierarchy = active;
        InventoryGui.instance = gui;
        return gui;
    }
    public static void Check()
    {
        var gui = Fresh();
        gui.m_animator.Visible = false; // Native Hide has already stopped crafting/input.
        OnInventoryGuiHide();
        Expect(QueueClears == 1 && PinHides == 1 && HoverClears == 1, "Hide immediately cancels queue, pins and hover");
        Expect(StatRestores == 0 && CraftRestores == 0, "Hide must not expose native layouts during the animation");
        Expect(!RecipePrefix(gui) && !InventoryPrefix(gui), "The last same-frame native ticks must not reset the frozen layout");
        Expect(!CanStartCraftingAction(gui), "Closing cannot restart crafting through controller/button callbacks");
        Expect(!gui.m_inventoryRoot!.gameObject.Group!.blocksRaycasts && !CraftingUi.SearchInput.Focused && !_craftingCountInput!.Focused,
            "Visible closing controls accept neither pointer nor text input");
        UpdatePlayerStatPanels(new RectTransform(), 2, 70);
        Expect(StatRestores == 0 && StatLayouts == 0, "A late stat update must not restore or relayout closing panels");
        InventoryGui.Visible = false; // IsVisible grace ends before the actual animation.
        for (int frame = 0; frame < 5; frame++) UpdateInventoryClosingLayout();
        Expect(StatRestores == 0 && CraftRestores == 0, "Logical visibility cannot complete visual cleanup early");
        gui.m_inventoryRoot!.gameObject.activeInHierarchy = false;
        UpdateInventoryClosingLayout();
        Expect(StatRestores == 1 && CraftRestores == 1, "Hidden root restores layouts exactly once");
        Expect(gui.m_inventoryRoot!.gameObject.Group!.blocksRaycasts, "Hidden cleanup restores previous pointer state");
        UpdateInventoryClosingLayout();
        Expect(StatRestores == 1 && CraftRestores == 1 && !IsInventoryClosingLayoutPending(gui), "Closed idle frames perform no further restoration");

        gui = Fresh();
        gui.m_animator.Visible = false;
        OnInventoryGuiHide();
        ShowPrefix(gui, out _); // Must cancel before native Show/SetupCrafting callbacks.
        Expect(RecipePrefix(gui) && InventoryPrefix(gui), "Rapid reopen allows native Show callbacks immediately");
        Expect(gui.m_inventoryRoot!.gameObject.Group!.blocksRaycasts && CanStartCraftingAction(gui), "Rapid reopen restores interaction");
        gui.m_animator.Visible = true;
        UpdateInventoryClosingLayout();
        UpdatePlayerStatPanels(new RectTransform(), 2, 70);
        Expect(StatRestores == 0 && CraftRestores == 0 && StatLayouts == 1, "Old close cannot restore over reopened layout");
        gui.m_animator.Visible = false;
        OnInventoryGuiHide();
        gui.m_inventoryRoot!.gameObject.activeInHierarchy = false;
        UpdateInventoryClosingLayout();
        Expect(StatRestores == 1 && CraftRestores == 1, "A subsequent close still cleans up normally");

        gui = Fresh();
        OnInventoryGuiHide();
        var replacement = new InventoryGui();
        InventoryGui.instance = replacement;
        UpdateInventoryClosingLayout();
        Expect(StatRestores == 0 && CraftRestores == 0 && !IsInventoryClosingLayoutPending(replacement), "Old owner cannot restore new GUI state");
        OnInventoryGuiHide();
        DestroyPrefix(gui);
        Expect(IsInventoryClosingLayoutPending(replacement), "Destroying old GUI cannot cancel a newer close");
        DestroyPrefix(replacement);
        Expect(!IsInventoryClosingLayoutPending(replacement), "Destroy cancels its own pending cleanup");

        gui = Fresh(false);
        OnInventoryGuiHide();
        Expect(StatRestores == 1 && CraftRestores == 1, "Already hidden root restores immediately");
        gui = Fresh();
        gui.m_inventoryRoot = null;
        OnInventoryGuiHide();
        Expect(StatRestores == 1 && CraftRestores == 1, "Missing root cannot leave a pending cleanup");
        gui = Fresh();
        OnInventoryGuiHide();
        InventoryGui.instance = null;
        UpdateInventoryClosingLayout();
        Expect(_inventoryClosingLayoutGui == null, "Scene teardown releases the pending GUI");
        Expect(gui.m_inventoryRoot!.gameObject.Group!.blocksRaycasts, "Owner replacement releases the old pointer gate");
        gui = Fresh();
        gui.m_inventoryRoot!.gameObject.Group = new CanvasGroup { blocksRaycasts = false };
        OnInventoryGuiHide();
        OnInventoryGuiHide();
        ShowPrefix(gui, out _);
        Expect(!gui.m_inventoryRoot.gameObject.Group.blocksRaycasts, "Repeated Hide preserves an existing noninteractive group");
        Console.WriteLine($"Closing layout lifecycle: {Checks} checks passed.");
    }
    private static bool IsInventoryPanelClosing(InventoryGui? gui) => gui != null && !gui.m_animator.Visible;
    private static void RestorePlayerStatPanels() => StatRestores++;
    private static void HideCraftingPanelRedesign() => CraftRestores++;
    private static void ClearCraftingQueue() => QueueClears++;
    private static void HideCraftingPinnedTooltips() => PinHides++;
    private static class CraftingController { public static void ClearHoveredRecipe() => HoverClears++; }
    private static void OnInventoryGuiHidden() { }
    private static void StartQuickSlotPanelOutroAnimation() { }
    private static void PrunePendingSlotActions() { }
    private static void RestoreContainerUiState() { }
    private static void HideInventoryPinnedTooltips() { }
    private static void HideInventoryOwnedHoverTooltips() { }
    private static void ClearInventoryHoverTooltipSources() { }
    private static void BeforeRealInventoryGuiShown() { }
    private static void DestroyCraftingListViewUi(InventoryGui gui) { }
}
public sealed class InventoryGui
{
    public static InventoryGui? instance;
    public static bool Visible = true;
    public static bool IsVisible() => Visible;
    public RectTransform? m_inventoryRoot = new();
    public object? m_armor = new(), m_weight = new();
    public Animator m_animator = new();
}
public sealed class Animator
{
    public bool Visible = true;
    public bool GetBool(string key) => key == "visible" ? Visible : throw new Exception("Unexpected animation parameter");
}
public sealed class RectTransform
{
    public GameObject gameObject = new();
    public T? GetComponent<T>() where T : class => gameObject.Group as T;
}
public sealed class GameObject
{
    public bool activeInHierarchy = true;
    public CanvasGroup? Group;
    public T AddComponent<T>() where T : new() { var value = new T(); Group = value as CanvasGroup; return value; }
}
public sealed class CanvasGroup { public bool blocksRaycasts = true; }
public sealed class InputField { public bool Focused = true; public void DeactivateInputField() => Focused = false; }
""";
}
