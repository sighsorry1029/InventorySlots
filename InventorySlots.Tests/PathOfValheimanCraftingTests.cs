using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

internal static class PathOfValheimanCraftingTests
{
    public static void Run()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root != null && !File.Exists(Path.Combine(root.FullName, "PathOfValheimanCraftingCompat.cs"))) root = root.Parent;
        if (root == null) throw new DirectoryNotFoundException("InventorySlots checkout");
        var source = CSharpSyntaxTree.ParseText(File.ReadAllText(Path.Combine(root.FullName, "PathOfValheimanCraftingCompat.cs"))).GetRoot();
        string[] methods = { "IsPathOfValheimanSalvageTab", "GetPathOfValheimanCraftingStock", "GetPathOfValheimanPreview",
            "ClearPathOfValheimanCraftingPreviews", "SuppressPathOfValheimanCraftCategories", "SkipPathOfValheimanQueueDisplay" };
        var plugin = source.DescendantNodes().OfType<ClassDeclarationSyntax>().First(c => c.Identifier.ValueText == "InventorySlotsPlugin");
        string production = string.Join("\n", plugin.Members.Where(m => m is FieldDeclarationSyntax or DelegateDeclarationSyntax or ClassDeclarationSyntax ||
            m is MethodDeclarationSyntax method && methods.Contains(method.Identifier.ValueText)).Select(m => m.ToFullString()));
        string host = File.ReadAllText(Path.Combine(root.FullName, "InventorySlots.Tests/PathOfValheimanCraftingHost.cs.txt"))
            .Replace("/* PRODUCTION */", production);
        var refs = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator).Select(p => MetadataReference.CreateFromFile(p));
        var compilation = CSharpCompilation.Create("PoVCraftingBehavior", new[] { CSharpSyntaxTree.ParseText(host) }, refs,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));
        using var stream = new MemoryStream();
        var result = compilation.Emit(stream);
        if (!result.Success) throw new Exception(string.Join("\n", result.Diagnostics));
        try { Assembly.Load(stream.ToArray()).GetType("InventorySlots.InventorySlotsPlugin")!.GetMethod("Check")!.Invoke(null, null); }
        catch (TargetInvocationException error) { throw error.InnerException!; }
    }
}
