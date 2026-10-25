using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

internal static class PlayerInitializationTests
{
    public static void Run()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root != null && !File.Exists(Path.Combine(root.FullName, "InventoryPatchHandlers.cs"))) root = root.Parent;
        if (root == null) throw new DirectoryNotFoundException("InventorySlots checkout");
        string Read(string path) => File.ReadAllText(Path.Combine(root.FullName, path));
        string Methods(string file, params string[] names) => string.Join("\n", CSharpSyntaxTree.ParseText(Read(file))
            .GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>()
            .Where(method => names.Contains(method.Identifier.ValueText)).Select(method => method.ToFullString()));
        string host = Read("InventorySlots.Tests/PlayerInitializationHost.cs.txt").Replace("/* METHODS */",
            Methods("InventoryPatchHandlers.cs", "OnPlayerAwake") +
            Methods("InventorySlotLayout.cs", "GetStableUnlockedQuickSlotRows", "PreserveOccupiedQuickSlotRowsDuringLoad",
                "GetHighestOccupiedQuickSlotRow", "TryGetSlotAtGridPos", "GetFullHeightForWidth", "GetSlotTailRows", "GetFixedRegularRows"));
        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator)
            .Select(path => MetadataReference.CreateFromFile(path));
        var compilation = CSharpCompilation.Create("PlayerInitializationContract",
            new[] { CSharpSyntaxTree.ParseText(host), CSharpSyntaxTree.ParseText(Read("InventorySlotSafetyCore.cs")) }, references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        using var stream = new MemoryStream();
        var result = compilation.Emit(stream);
        if (!result.Success) throw new Exception(string.Join("\n", result.Diagnostics));
        try { Assembly.Load(stream.ToArray()).GetType("PlayerInitializationContract")!.GetMethod("Check")!.Invoke(null, null); }
        catch (TargetInvocationException error) { throw error.InnerException!; }
    }
}
