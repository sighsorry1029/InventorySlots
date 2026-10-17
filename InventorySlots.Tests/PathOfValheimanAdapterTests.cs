using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

internal static class PathOfValheimanAdapterTests
{
    public static void Run()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root != null && !File.Exists(Path.Combine(root.FullName, "PathOfValheimanCompat.cs"))) root = root.Parent;
        if (root == null) throw new DirectoryNotFoundException("InventorySlots checkout");
        string Read(string file) => File.ReadAllText(Path.Combine(root.FullName, file));
        string Methods(string file, params string[] names) => string.Join("\n", CSharpSyntaxTree.ParseText(Read(file)).GetRoot()
            .DescendantNodes().OfType<MethodDeclarationSyntax>().Where(m => names.Contains(m.Identifier.ValueText)).Select(m => m.ToFullString()));
        string fields = string.Join("\n", CSharpSyntaxTree.ParseText(Read("PathOfValheimanCompat.cs")).GetRoot()
            .DescendantNodes().OfType<FieldDeclarationSyntax>().Where(f => f.Declaration.Variables.Any(v =>
                v.Identifier.ValueText.Contains("Tooltip") || v.Identifier.ValueText == "_pathOfValheimanTreeOpen")).Select(f => f.ToFullString()));
        string host = Read("InventorySlots.Tests/PathOfValheimanHost.cs.txt").Replace("/* PRODUCTION */", fields +
            Methods("PathOfValheimanCompat.cs", "TryYieldToPathOfValheimanTooltip", "HasPathOfValheimanTooltipBody",
                "ClearPathOfValheimanTooltipState", "FilterPathOfValheimanMovableItems", "SortPathOfValheimanInventory", "IsPathOfValheimanTreeOpen") +
            Methods("InventoryHoverTooltipScroll.cs", "OnVanillaTooltipHidden") + Methods("ContainerSort.cs", "SortPlayerInventory"));
        var refs = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator).Select(p => MetadataReference.CreateFromFile(p));
        var compilation = CSharpCompilation.Create("PoVAdapterBehavior", new[] { CSharpSyntaxTree.ParseText(host) }, refs,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));
        using var stream = new MemoryStream();
        var result = compilation.Emit(stream);
        if (!result.Success) throw new Exception(string.Join("\n", result.Diagnostics));
        try { Assembly.Load(stream.ToArray()).GetType("InventorySlots.InventorySlotsPlugin")!.GetMethod("Check")!.Invoke(null, null); }
        catch (TargetInvocationException error) { throw error.InnerException!; }
    }
}
