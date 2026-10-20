using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

internal static class YamlMatchingTests
{
    public static void Run()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root != null && !File.Exists(Path.Combine(root.FullName, "YamlItemMatcher.cs"))) root = root.Parent;
        if (root == null) throw new DirectoryNotFoundException("InventorySlots checkout");
        string Read(string path) => File.ReadAllText(Path.Combine(root.FullName, path));
        string Methods(string file, params string[] names) => string.Join("\n", CSharpSyntaxTree.ParseText(Read(file))
            .GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>()
            .Where(method => names.Contains(method.Identifier.ValueText)).Select(method => method.ToFullString()));
        string host = Read("InventorySlots.Tests/YamlMatchingHost.cs.txt").Replace("/* METHODS */",
            Methods("ItemClassifier.cs", "ItemMatchesBuiltInPredefinedGroup", "ItemMatchesBuiltInPredefinedGroupUncached") +
            Methods("PathOfValheimanStoragePanel.cs", "IsEquipmentPanelSlot", "IsPathOfValheimanRingSlot") +
            Methods("InventorySlotLayout.cs", "IsEquipmentSlotUnlocked", "IsEquipmentSlotDiscovered", "InventoryContainsAcceptedSlotItem",
                "PlayerHasAcceptedEquippedItem", "RefreshEquipmentSlotUnlockCache", "GetEquipmentSlotUnlockCacheSignature", "InvalidateEquipmentSlotUnlockCache",
                "GetKnownMaterialHash", "PlayerKnowsAcceptedSlotItem", "PlayerKnowsItem", "PlayerKnowsMaterialToken", "PlayerKnowsMaterial"));
        var sources = new[] { "YamlItemMatcher.cs", "InventorySlotsConfigCore.cs", "InventorySlotsYamlModels.cs", "ItemGroupRegistry.cs" }
            .Select(file => CSharpSyntaxTree.ParseText(Read(file))).Append(CSharpSyntaxTree.ParseText(host));
        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator)
            .Append(typeof(YamlDotNet.Serialization.YamlMemberAttribute).Assembly.Location).Distinct()
            .Select(path => MetadataReference.CreateFromFile(path));
        var compilation = CSharpCompilation.Create("YamlMatchingContract", sources, references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary,
                optimizationLevel: OptimizationLevel.Release, nullableContextOptions: NullableContextOptions.Enable));
        using var stream = new MemoryStream();
        var result = compilation.Emit(stream);
        if (!result.Success) throw new Exception(string.Join("\n", result.Diagnostics));
        try { Assembly.Load(stream.ToArray()).GetType("InventorySlots.InventorySlotsPlugin")!.GetMethod("Check")!.Invoke(null, null); }
        catch (TargetInvocationException error) { throw error.InnerException!; }
    }
}
