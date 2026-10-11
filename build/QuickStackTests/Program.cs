using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Reflection;

var root = new DirectoryInfo(AppContext.BaseDirectory);
while (root != null && !File.Exists(Path.Combine(root.FullName, "ContainerQuickStack.cs"))) root = root.Parent;
if (root == null) throw new DirectoryNotFoundException("InventorySlots checkout");
string Methods(string file, params string[] names)
{
    var syntax = CSharpSyntaxTree.ParseText(File.ReadAllText(Path.Combine(root.FullName, file))).GetRoot();
    return string.Join("\n", names.Select(name => syntax.DescendantNodes().OfType<MethodDeclarationSyntax>()
        .Single(method => method.Identifier.ValueText == name).ToFullString()));
}
string host = File.ReadAllText(Path.Combine(root.FullName, "build/QuickStackTests/Host.cs.txt"))
    .Replace("/* SLOTS */", Methods("ContainerQuickStack.cs", "ShouldQuickStackItem", "QuickStackItemsIntoContainer",
        "MoveItemToContainerTopFirst", "IsEquippedContainerMoveSource") +
        Methods("ContainerActions.cs", "CountMovedFromContainerSource"))
    .Replace("/* ACTIONS */", Methods("InventoryActions/Actions.cs", "ShouldQuickStackItem", "GetQuickStackCandidates",
        "QuickStackItemsIntoContainer", "MoveItemToInventoryTopFirst", "GetInventorySlotsTopFirst",
        "CanStackForTopFirstMove", "CanStackIntoTargetForTopFirstMove", "HasSameStackIdentity", "CountMovedFromContainerSource") +
        Methods("InventoryActions/MultiUserChestTransfer.cs", "TrySendMucQuickStack"))
    .Replace("/* CORE */", Methods("ContainerActionCore.cs", "CountMovedAmount"));
var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator)
    .Select(path => MetadataReference.CreateFromFile(path));
var compilation = CSharpCompilation.Create("QuickStackProbe", new[] { CSharpSyntaxTree.ParseText(host) }, references,
    new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));
using var stream = new MemoryStream();
var emitted = compilation.Emit(stream);
if (!emitted.Success) throw new Exception(string.Join("\n", emitted.Diagnostics));
try { Assembly.Load(stream.ToArray()).GetType("Checks")!.GetMethod("Run")!.Invoke(null, null); }
catch (TargetInvocationException error) { throw error.InnerException!; }
