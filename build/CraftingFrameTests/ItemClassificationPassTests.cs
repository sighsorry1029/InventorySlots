using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

internal static class ItemClassificationPassTests
{
    internal static void Run()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root != null && !File.Exists(Path.Combine(root.FullName, "ItemClassifier.cs"))) root = root.Parent;
        if (root == null) throw new DirectoryNotFoundException("InventorySlots checkout");
        var tree = CSharpSyntaxTree.ParseText(File.ReadAllText(Path.Combine(root.FullName, "ItemClassifier.cs"))).GetRoot();
        string members = string.Concat(new[] { "ItemClassification", "ItemClassifierRuntimeState", "ItemClassificationPass" }
            .Select(name => tree.DescendantNodes().OfType<ClassDeclarationSyntax>().Single(c => c.Identifier.ValueText == name).ToFullString()));
        members += string.Concat(new[] { "GetItemClassification", "ClearItemClassifierCaches", "EnsureItemClassifierCacheFresh" }
            .Select(name => tree.DescendantNodes().OfType<MethodDeclarationSyntax>().Single(m => m.Identifier.ValueText == name).ToFullString()));
        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator)
            .Select(path => MetadataReference.CreateFromFile(path));
        var compilation = CSharpCompilation.Create("ItemClassificationPassRegression",
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
using System.Runtime.CompilerServices;
public static class Host
{
    /* PRODUCTION */
    private static readonly ItemClassifierRuntimeState ItemClassifierRuntime = new();
    private static ItemClassificationPass _itemClassificationPass;
    private const int MaxItemClassifierCacheItems = 2;
    private static int Keys, Checks;
    // Boundary counts expensive full-content key construction, without duplicating
    // its 17 production fields. Classification/cache behavior above is production code.
    private static string GetItemClassifierCacheKey(ItemData item) { Keys++; return item.Value; }
    private static int GetItemClassifierCacheVersion() => ItemClassifierRuntime.Version;
    private static void Expect(bool ok, string label) { if (!ok) throw new Exception(label); Checks++; }
    public static void Check()
    {
        var item = new ItemData { Value = "food" };
        ItemClassification first;
        using (var pass = new ItemClassificationPass())
        {
            first = GetItemClassification(item);
            for (int i = 0; i < 1801; i++) Expect(ReferenceEquals(first, GetItemClassification(item)), "Repeated groups share one classification");
            Expect(Keys == 1, "A pass constructs the full key only once per item");
            var equalContent = new ItemData { Value = "food" };
            Expect(ReferenceEquals(first, GetItemClassification(equalContent)) && Keys == 2, "Different references still use original content cache");
            var other = new ItemData { Value = "weapon" };
            Expect(!ReferenceEquals(first, GetItemClassification(other)), "Item value equality cannot alias reference pass keys");
        }
        Expect(_itemClassificationPass == null, "Pass releases references on completion");
        item.Value = "changed";
        using (var pass = new ItemClassificationPass())
        {
            var changed = GetItemClassification(item);
            Expect(!ReferenceEquals(first, changed), "Next rebuild sees mutable item changes");
            int before = Keys;
            using (var nested = new ItemClassificationPass())
            {
                Expect(ReferenceEquals(changed, GetItemClassification(item)), "Nested pass keeps original content policy");
                ClearItemClassifierCaches();
                Expect(!ReferenceEquals(changed, GetItemClassification(item)), "Explicit clear invalidates inner hits");
            }
            Expect(!ReferenceEquals(changed, GetItemClassification(item)) && Keys == before + 3, "Parent cannot resurrect hits invalidated in a nested pass");
            var previous = GetItemClassification(item);
            ItemClassifierRuntime.Version++;
            Expect(!ReferenceEquals(previous, GetItemClassification(item)), "Policy version change invalidates a pass before its hit");
            try { using var nested = new ItemClassificationPass(); throw new InvalidOperationException(); }
            catch (InvalidOperationException) { }
            Expect(ReferenceEquals(_itemClassificationPass, pass), "Exception restores suspended parent");
            previous = GetItemClassification(item);
            GetItemClassification(new ItemData { Value = "other1" });
            GetItemClassification(new ItemData { Value = "other2" });
            Expect(ReferenceEquals(previous, GetItemClassification(item)), "Capacity eviction alone does not change semantic classification");
        }
        int unscoped = Keys;
        GetItemClassification(item); item.Value = "last"; GetItemClassification(item);
        Expect(Keys == unscoped + 2 && _itemClassificationPass == null, "Unscoped callers keep full content validation on every read");
        Console.WriteLine($"Item classification pass: {Checks} checks passed (key construction counts, mutable state and nested invalidation).");
    }
}
public class ItemData
{
    public string Value;
    public override bool Equals(object value) => value is ItemData;
    public override int GetHashCode() => 0;
}
""";
}
