using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Reflection;

// Compile the actual policy methods with counted boundary doubles. This avoids
// copying their implementation or initializing Unity in the test process.
var root = new DirectoryInfo(AppContext.BaseDirectory);
while (root != null && !File.Exists(Path.Combine(root.FullName, "InventoryPlacementPolicy.cs"))) root = root.Parent;
if (root == null) throw new DirectoryNotFoundException("InventorySlots checkout");
var policy = CSharpSyntaxTree.ParseText(File.ReadAllText(Path.Combine(root.FullName, "InventoryPlacementPolicy.cs"))).GetRoot();
var names = new[] { "CountStackSpaceForIncomingItem", "CanStackIncomingItemInto", "CanShareInventoryStack",
    "TryGetCachedCanAddItemFailure", "CountUsableRegularEmptyCells" };
var methods = names.Select(name => policy.DescendantNodes().OfType<MethodDeclarationSyntax>().Single(m =>
    m.Identifier.ValueText == name && (name != "CountUsableRegularEmptyCells" || m.ParameterList.Parameters.Count == 3)));
string host = File.ReadAllText(Path.Combine(root.FullName, "build/CapacityTests/Host.cs.txt"));
host = host.Replace("/* PRODUCTION METHODS */", string.Join("\n", methods.Select(m => m.ToFullString())));
var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator)
    .Select(path => MetadataReference.CreateFromFile(path));
var compilation = CSharpCompilation.Create("CapacityProbe", new[] { CSharpSyntaxTree.ParseText(host) }, references,
    new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));
using var stream = new MemoryStream();
var emitted = compilation.Emit(stream);
if (!emitted.Success) throw new Exception(string.Join("\n", emitted.Diagnostics));
var assembly = Assembly.Load(stream.ToArray());
try { assembly.GetType("CapacityProbe")!.GetMethod("Run")!.Invoke(null, new object[] { args.Contains("--baseline") }); }
catch (TargetInvocationException error) { throw error.InnerException!; }
