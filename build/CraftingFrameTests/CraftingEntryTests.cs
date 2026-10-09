using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

internal static class CraftingEntryTests
{
    internal static void Run()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root != null && !File.Exists(Path.Combine(root.FullName, "CraftingRedesign.cs"))) root = root.Parent;
        if (root == null) throw new DirectoryNotFoundException("InventorySlots checkout");
        SyntaxNode Read(string path) => CSharpSyntaxTree.ParseText(File.ReadAllText(Path.Combine(root.FullName, path))).GetRoot();
        var method = Read("CraftingRedesign.cs").DescendantNodes().OfType<MethodDeclarationSyntax>()
            .Single(m => m.Identifier.ValueText == "UpdateCraftingPanelRedesign");
        // Execute the real entry up to the first UI boundary. This intentionally
        // does not emulate rendering; the existing linked router tests cover the
        // fast path after entry. A guard after preflight cannot pass this test.
        var preflight = method.Body!.Statements.Single(s => s is ExpressionStatementSyntax expression &&
            expression.Expression is InvocationExpressionSyntax invocation && invocation.Expression.ToString() == "PrepareCraftingTabAdapterPreflight");
        var entry = method.WithBody(SyntaxFactory.Block(method.Body.Statements.TakeWhile(s => s != preflight)
            .Append(SyntaxFactory.ParseStatement("ReachedPreflight++;"))));
        var closing = Read("Shared/ItemRules/ItemRuleUi.cs").DescendantNodes().OfType<MethodDeclarationSyntax>()
            .Single(m => m.Identifier.ValueText == "IsInventoryPanelClosing");
        var reasons = Read("CraftingState.cs").DescendantNodes().OfType<EnumDeclarationSyntax>()
            .Single(e => e.Identifier.ValueText == "CraftingPanelUpdateReason");
        string source = "using System;\n" + reasons.ToFullString() + "\n" + Host
            .Replace("/* ENTRY */", entry.ToFullString()).Replace("/* CLOSING */", closing.ToFullString());
        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator)
            .Select(path => MetadataReference.CreateFromFile(path));
        var compilation = CSharpCompilation.Create("CraftingEntryRegression", new[] { CSharpSyntaxTree.ParseText(source) }, references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));
        using var stream = new MemoryStream();
        var result = compilation.Emit(stream);
        if (!result.Success) throw new Exception(string.Join("\n", result.Diagnostics));
        try { Assembly.Load(stream.ToArray()).GetType("EntryHost")!.GetMethod("Check")!.Invoke(null, null); }
        catch (TargetInvocationException error) { throw error.InnerException!; }
    }

    private const string Host = """
public static class EntryHost
{
    private static int ReachedPreflight, Checks;
    private static Editor? _itemRuleEditor;
    /* ENTRY */
    /* CLOSING */
    private static void Expect(InventoryGui gui, CraftingPanelUpdateReason reason, bool reaches, string label)
    {
        int before = ReachedPreflight;
        UpdateCraftingPanelRedesign(gui, reason);
        if (ReachedPreflight != before + (reaches ? 1 : 0)) throw new Exception(label);
        Checks++;
    }
    public static void Check()
    {
        Player.m_localPlayer = new Player();
        var gui = new InventoryGui();
        // Native Hide sets the animator false while the panel is still active
        // and InventoryGui.IsVisible can still be true during its grace period.
        gui.Animator.Visible = false;
        Expect(gui, CraftingPanelUpdateReason.FrameTick, false, "Post-Hide tick must stop before preflight, including optional-tab routing");
        Expect(gui, CraftingPanelUpdateReason.FrameTick, false, "Repeated closed ticks must not restart UI construction");
        gui.Animator.Visible = true;
        Expect(gui, CraftingPanelUpdateReason.FrameTick, true, "Reopen immediately permits updates without a frame-number latch");
        Expect(gui, CraftingPanelUpdateReason.StateChanged, true, "Show initialization remains allowed before visibility grace updates");
        gui.Animator.Visible = false;
        Expect(gui, CraftingPanelUpdateReason.StateChanged, true, "Explicit state changes retain existing behavior");
        Expect(gui, CraftingPanelUpdateReason.RecipeListChanged, true, "Recipe-list invalidation must not be discarded");
        Player.m_localPlayer.m_isLoading = true;
        Expect(gui, CraftingPanelUpdateReason.FrameTick, true, "Existing loading fallback remains intact");
        Player.m_localPlayer = null;
        Expect(gui, CraftingPanelUpdateReason.FrameTick, true, "Existing no-player fallback remains intact");
        Player.m_localPlayer = new Player();
        _itemRuleEditor = new Editor { Owner = gui, _animator = new Animator { Visible = false } };
        gui.Animator.Visible = true;
        Expect(gui, CraftingPanelUpdateReason.FrameTick, false, "Owned editor animator remains the established source of closing state");
        _itemRuleEditor._animator.Visible = true;
        Expect(gui, CraftingPanelUpdateReason.FrameTick, true, "Owned animator reopening permits the same-frame update");
        _itemRuleEditor.Owner = new InventoryGui();
        _itemRuleEditor._animator.Visible = false;
        Expect(gui, CraftingPanelUpdateReason.FrameTick, true, "An unrelated editor must not block this inventory");
        gui.Animator.Visible = false;
        Expect(gui, CraftingPanelUpdateReason.FrameTick, false, "Fallback reads this inventory's animator");
        Console.WriteLine($"Crafting entry lifecycle: {Checks} checks passed.");
    }
    private sealed class Editor { internal InventoryGui Owner = null!; internal Animator _animator = null!; }
}
public sealed class Animator
{
    public bool Visible;
    public bool GetBool(string key) => key == "visible" ? Visible : throw new Exception("Unexpected animator parameter");
}
public sealed class InventoryGui
{
    public Animator Animator = new();
    public T? GetComponent<T>() where T : class => Animator as T;
}
public sealed class Player { public static Player? m_localPlayer; public bool m_isLoading; }
""";
}
