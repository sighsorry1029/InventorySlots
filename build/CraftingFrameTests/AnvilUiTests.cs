using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

internal static class AnvilUiTests
{
    internal static void Run()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root != null && !File.Exists(Path.Combine(root.FullName, "PathOfValheimanAnvilUi.cs"))) root = root.Parent;
        if (root == null) throw new DirectoryNotFoundException("InventorySlots checkout");
        SyntaxNode Read(string file) => CSharpSyntaxTree.ParseText(File.ReadAllText(Path.Combine(root.FullName, file))).GetRoot();
        var type = Read("PathOfValheimanAnvilUi.cs").DescendantNodes().OfType<ClassDeclarationSyntax>().Single();
        string members = string.Concat(type.Members.Where(m => m is not MethodDeclarationSyntax method ||
            method.Identifier.ValueText != "InstallPathOfValheimanAnvilUi").Select(m => m.ToFullString()));
        members += Read("CraftingRecipeActions.cs").DescendantNodes().OfType<MethodDeclarationSyntax>()
            .Single(m => m.Identifier.ValueText == "CanStartCraftingAction").ToFullString();
        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator)
            .Select(path => MetadataReference.CreateFromFile(path));
        var compilation = CSharpCompilation.Create("AnvilUiRegression",
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
public static class Host
{
    /* PRODUCTION */
    private static bool IsDedicatedServer;
    private static int Checks, Restores, Rebuilds, Warnings;
    private static bool NativeRestored;
    private static void WarnPathOfValheimanCrafting(Exception error) => Warnings++;
    private static void ClosePathOfValheimanCharacterUi() { }
    private static void HideCraftingPanelRedesign() => Restores++;
    private static void RestorePathOfValheimanRadiusButton(InventoryGui gui) { }
    private static void HideOwnedCraftingRedesignUi() { }
    private static void HidePathOfValheimanReturns() { }
    private static void HideCraftingRedesignBottomControls(InventoryGui gui) { }
    private static bool IsInventoryPanelClosing(InventoryGui gui) => gui.Closing;
    private static bool IsInventoryClosingLayoutPending(InventoryGui gui) => gui.Closing;
    private static bool IsJewelcraftingSocketTabActive(InventoryGui gui) => false;
    private static bool TryRefreshSelectedJewelcraftingSocketRecipePair(InventoryGui gui) => true;
    private static bool CanAttemptJewelcraftingSocket(object pair) => true;
    private static void UpdateCraftingPanelRedesign(InventoryGui gui, CraftingPanelUpdateReason reason)
    { if (!NativeRestored) throw new Exception("Ordinary UI rebuilt before native Restore"); Rebuilds++; }
    private enum CraftingPanelUpdateReason { StateChanged }
    private static void Expect(bool value, string label) { if (!value) throw new Exception(label); Checks++; }
    public static void Check()
    {
        var gui = InventoryGui.instance = new InventoryGui();
        var anvil = new Container { Anvil = true };
        Expect(!IsPathOfValheimanAnvilActive(gui) && CanStartCraftingAction(gui), "Without PoV ordinary crafting is unchanged");
        _povIsAnvil = c => c.Anvil;
        _povIsAnvilCache = c => c.Cache;
        _povViewingAnvil = c => c != null && c.Anvil && !c.Cache && c.Viewable;
        BeginPathOfValheimanAnvilShow(gui, anvil, out var show);
        Expect(gui.m_currentContainer == null && IsPathOfValheimanAnvilActive(gui), "Show routes before native assigns container");
        Expect(TryYieldCraftingToPathOfValheimanAnvil(gui) && Restores == 1 && !CanStartCraftingAction(gui),
            "Anvil restores geometry once and blocks hidden ordinary recipe button");
        gui.m_currentContainer = anvil;
        Expect(EndPathOfValheimanAnvilShow(gui, show, null) == null && _povAnvilOpeningGui == null, "Show restores its scoped state");
        BeforePathOfValheimanAnvilRefresh(gui); AfterPathOfValheimanAnvilRefresh(gui);
        Expect(IsPathOfValheimanAnvilActive(gui) && Restores == 1 && Rebuilds == 0, "Viewable anvil stays native without requiring owner");
        anvil.Viewable = false;
        Expect(IsPathOfValheimanAnvilActive(gui), "Lost viewing waits for native Restore");
        BeforePathOfValheimanAnvilRefresh(gui);
        Expect(Rebuilds == 0, "Prefix cannot reclaim UI before native restore");
        NativeRestored = true; AfterPathOfValheimanAnvilRefresh(gui);
        Expect(Rebuilds == 1 && !IsPathOfValheimanAnvilActive(gui) && CanStartCraftingAction(gui), "Postfix returns UI after native restore");
        AfterPathOfValheimanAnvilRefresh(gui);
        Expect(Rebuilds == 1, "Ordinary idle frames do not rebuild repeatedly");

        var cache = new Container { Anvil = true, Cache = true };
        BeginPathOfValheimanAnvilShow(gui, cache, out show);
        Expect(!IsPathOfValheimanAnvilActive(gui), "Anvil material cache keeps ordinary container UI");
        EndPathOfValheimanAnvilShow(gui, show, null);
        BeginPathOfValheimanAnvilShow(gui, anvil, out show);
        BeginPathOfValheimanAnvilShow(gui, cache, out var nested);
        Expect(_povAnvilOpeningGui == null, "Nested ordinary Show does not inherit opening argument");
        EndPathOfValheimanAnvilShow(gui, nested, null);
        Expect(_povAnvilOpeningGui == gui, "Nested Show restores outer opening scope");
        TryYieldCraftingToPathOfValheimanAnvil(gui);
        var error = new InvalidOperationException("native show failed");
        Expect(ReferenceEquals(EndPathOfValheimanAnvilShow(gui, show, error), error) && _povAnvilGui == null && _povAnvilOpeningGui == null,
            "Failure preserves native exception and releases newly acquired UI owner");
        anvil.Viewable = true;
        BeforePathOfValheimanAnvilRefresh(gui);
        gui.Closing = true; anvil.Viewable = false; AfterPathOfValheimanAnvilRefresh(gui);
        Expect(_povAnvilGui == null && Rebuilds == 1, "Close releases owner without reopening UI");
        gui.Closing = false;
        IsDedicatedServer = true;
        BeginPathOfValheimanAnvilShow(gui, anvil, out show);
        Expect(!IsPathOfValheimanAnvilActive(gui), "Dedicated server never acquires UI");
        EndPathOfValheimanAnvilShow(gui, show, null); IsDedicatedServer = false;
        _povViewingAnvil = c => throw new Exception("API failure");
        Expect(!IsPathOfValheimanAnvilActive(gui) && Warnings == 1, "Predicate failure leaves ordinary behavior available");
        _povViewingAnvil = c => false;
        _povAnvilGui = new InventoryGui(); ClearPathOfValheimanAnvilUi(gui);
        Expect(_povAnvilGui != null, "Destroying a different GUI cannot clear current owner");
        ClearPathOfValheimanAnvilUi();
        Expect(_povAnvilGui == null && _povAnvilOpeningGui == null, "Shutdown releases ownership");
        Console.WriteLine($"Runic Anvil UI handoff: {Checks} checks passed (managed lifecycle boundaries, no rendered game).");
    }
}
public class InventoryGui
{
    public static InventoryGui instance;
    public static bool IsVisible() => true;
    public Container m_currentContainer;
    public bool Closing;
    public object m_selectedRecipe;
}
public class Container { public bool Anvil, Cache, Viewable = true; }
""";
}
