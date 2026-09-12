using Mono.Cecil;
using Mono.Cecil.Cil;
using System.Security.Cryptography;
using System.Text.Json;

// Checks compiled contracts against ORIGINAL assemblies, without loading Unity.
if (args.Length != 4) throw new ArgumentException("Usage: <mod.dll> <original Managed> <BepInEx core> <report.json>");
var resolver = new DefaultAssemblyResolver();
foreach (string dir in resolver.GetSearchDirectories()) resolver.RemoveSearchDirectory(dir);
resolver.AddSearchDirectory(Path.GetFullPath(args[1]));
resolver.AddSearchDirectory(Path.GetFullPath(args[2]));
resolver.AddSearchDirectory(Path.GetDirectoryName(Path.GetFullPath(args[0]))!);
resolver.AddSearchDirectory(Path.GetDirectoryName(typeof(object).Assembly.Location)!);
using var mod = AssemblyDefinition.ReadAssembly(args[0], new ReaderParameters { AssemblyResolver = resolver });
var failures = new List<string>();
var manual = new List<string>();
var patches = new List<string>();
var reflectedContracts = new List<string>();
var privateAccess = new HashSet<string>();
var references = new HashSet<string>();
IEnumerable<TypeDefinition> Types(IEnumerable<TypeDefinition> types) => types.SelectMany(t => new[] { t }.Concat(Types(t.NestedTypes)));
bool Game(TypeReference type) => type.Scope is AssemblyNameReference assembly &&
    (assembly.Name.StartsWith("assembly_") || assembly.Name.StartsWith("Unity") || assembly.Name is "gui_framework" or "SoftReferenceableAssets");
TypeReference Unwrap(TypeReference type) => type is TypeSpecification specification ? Unwrap(specification.ElementType) : type;
string ParameterType(TypeReference type) => type is ByReferenceType reference ? reference.ElementType.FullName : type.FullName;
void CheckType(TypeReference type)
{
    if (type is GenericInstanceType generic) foreach (var argument in generic.GenericArguments) CheckType(argument);
    type = Unwrap(type);
    if (!Game(type)) return;
    if (type.Resolve() == null) failures.Add("Unresolved type: " + type.FullName);
}
foreach (var type in Types(mod.MainModule.Types))
{
    foreach (var method in type.Methods.Where(m => m.HasBody))
    foreach (var instruction in method.Body.Instructions)
    {
        try
        {
            if (instruction.Operand is TypeReference referencedType) CheckType(referencedType);
            if (instruction.Operand is not MemberReference member || member.DeclaringType == null || !Game(member.DeclaringType)) continue;
            references.Add(member.FullName);
            if (member is MethodReference mr)
            {
                var target = mr.Resolve();
                if (target == null) failures.Add($"Missing method: {member.FullName} from {method.FullName}");
                else if (!target.IsPublic || !target.DeclaringType.IsPublic && !target.DeclaringType.IsNestedPublic) privateAccess.Add(target.FullName);
            }
            else if (member is FieldReference fr)
            {
                var target = fr.Resolve();
                if (target == null) failures.Add($"Missing field: {member.FullName}");
                else
                {
                    if (!target.IsPublic) privateAccess.Add(target.FullName);
                    if (target.IsLiteral && instruction.OpCode.Code is Code.Ldsfld or Code.Ldsflda or Code.Stsfld)
                        failures.Add($"Literal used as runtime storage: {member.FullName}");
                }
            }
        }
        catch (Exception error) { failures.Add($"Resolve {method.FullName}: {error.Message}"); }
    }

    var declarations = type.CustomAttributes.Where(a => a.AttributeType.FullName == "HarmonyLib.HarmonyPatch").ToArray();
    if (declarations.Length == 0) continue;
    if (type.Methods.Any(m => m.Name is "TargetMethod" or "TargetMethods"))
    {
        manual.Add("Dynamic Harmony targets: " + type.FullName);
        continue;
    }
    TypeReference? declaring = null;
    string? name = null;
    string[]? arguments = null;
    int methodType = 0;
    foreach (var attribute in declarations)
    foreach (var argument in attribute.ConstructorArguments)
    {
        if (argument.Type.FullName == "System.Type") declaring = (TypeReference)argument.Value;
        else if (argument.Type.FullName == "System.String") name = (string)argument.Value;
        else if (argument.Type.FullName == "System.Type[]") arguments = ((CustomAttributeArgument[])argument.Value).Select(a => ((TypeReference)a.Value).FullName).ToArray();
        else if (argument.Type.FullName == "HarmonyLib.MethodType") methodType = Convert.ToInt32(argument.Value);
    }
    if (declaring == null || name == null)
    {
        manual.Add("Incomplete/method-level Harmony declaration: " + type.FullName);
        continue;
    }
    name = methodType switch { 1 => "get_" + name, 2 => "set_" + name, 3 => ".ctor", 4 => ".cctor", _ => name };
    var owner = declaring.Resolve();
    var candidates = owner.Methods.Where(m => m.Name == name &&
        (arguments == null || m.Parameters.Select(p => p.ParameterType.FullName).SequenceEqual(arguments))).ToArray();
    if (candidates.Length != 1)
    {
        failures.Add($"Harmony {type.FullName}: {declaring.FullName}.{name} resolves to {candidates.Length} methods");
        continue;
    }
    var original = candidates[0];
    patches.Add(type.FullName + " -> " + original.FullName);
    foreach (var patch in type.Methods.Where(m => m.Name is "Prefix" or "Postfix" or "Finalizer"))
    foreach (var parameter in patch.Parameters)
    {
        if (parameter.Name.StartsWith("___"))
        {
            var fieldOwner = owner;
            FieldDefinition? field = null;
            while (fieldOwner != null && field == null)
            {
                field = fieldOwner.Fields.FirstOrDefault(f => f.Name == parameter.Name[3..]);
                fieldOwner = fieldOwner.BaseType?.Resolve();
            }
            if (field == null) failures.Add($"Harmony field injection {patch.FullName}: {parameter.Name}");
            else if (ParameterType(parameter.ParameterType) != field.FieldType.FullName && ParameterType(parameter.ParameterType) != "System.Object")
                failures.Add($"Harmony field type {patch.FullName}: {parameter.Name} {parameter.ParameterType.FullName} != {field.FieldType.FullName}");
        }
        else if (!parameter.Name.StartsWith("__") && !original.Parameters.Any(p => p.Name == parameter.Name))
            failures.Add($"Harmony argument {patch.FullName}: '{parameter.Name}' absent from original");
        else if (!parameter.Name.StartsWith("__"))
        {
            var argument = original.Parameters.Single(p => p.Name == parameter.Name);
            if (ParameterType(parameter.ParameterType) != ParameterType(argument.ParameterType))
                failures.Add($"Harmony argument type {patch.FullName}: {parameter.Name} {parameter.ParameterType.FullName} != {argument.ParameterType.FullName}");
        }
        else if (parameter.Name == "__result" && ParameterType(parameter.ParameterType) != original.ReturnType.FullName && ParameterType(parameter.ParameterType) != "System.Object")
            failures.Add($"Harmony result type {patch.FullName}: {parameter.ParameterType.FullName} != {original.ReturnType.FullName}");
    }
}
// String-resolved AccessTools members do not appear as direct game references.
// Check the bounded area-handoff contract only in candidates containing that runtime.
if (Types(mod.MainModule.Types).Any(t => t.FullName == "InventorySlots.InventorySlotsPlugin" &&
    t.Fields.Any(f => f.Name == "ContainerAreaView")))
{
    using var originalGame = AssemblyDefinition.ReadAssembly(
        Path.Combine(args[1], "assembly_valheim.dll"),
        new ReaderParameters { AssemblyResolver = resolver });
    var originalTypes = Types(originalGame.MainModule.Types).ToDictionary(t => t.FullName);

    TypeDefinition? ReflectedOwner(string ownerName)
    {
        if (originalTypes.TryGetValue(ownerName, out var owner)) return owner;
        failures.Add("Area handoff reflection: missing original type " + ownerName);
        return null;
    }

    void CheckReflectedField(string ownerName, string name, string fieldType, bool isStatic)
    {
        var owner = ReflectedOwner(ownerName);
        if (owner == null) return;
        var candidates = owner.Fields.Where(f => f.Name == name).ToArray();
        string expected = $"{ownerName}.{name}: {(isStatic ? "static" : "instance")} {fieldType}";
        if (candidates.Length != 1)
        {
            failures.Add($"Area handoff reflection: {expected} resolves to {candidates.Length} fields");
            return;
        }
        var field = candidates[0];
        if (field.FieldType.FullName != fieldType || field.IsStatic != isStatic || field.IsLiteral)
        {
            failures.Add($"Area handoff reflection: expected {expected}; found {field.FullName} [{field.Attributes}]");
            return;
        }
        reflectedContracts.Add(expected + $" [{field.Attributes}]");
    }

    void CheckReflectedMethod(string ownerName, string name, string returnType, bool isStatic,
        params string[] parameters)
    {
        var owner = ReflectedOwner(ownerName);
        if (owner == null) return;
        var candidates = owner.Methods.Where(m => m.Name == name && !m.HasGenericParameters &&
            m.Parameters.Select(p => p.ParameterType.FullName).SequenceEqual(parameters)).ToArray();
        string expected = $"{ownerName}.{name}({string.Join(", ", parameters)}): {(isStatic ? "static" : "instance")} {returnType}";
        if (candidates.Length != 1)
        {
            failures.Add($"Area handoff reflection: {expected} resolves to {candidates.Length} methods");
            return;
        }
        var method = candidates[0];
        if (method.ReturnType.FullName != returnType || method.IsStatic != isStatic)
        {
            failures.Add($"Area handoff reflection: expected {expected}; found {method.FullName} [{method.Attributes}]");
            return;
        }
        reflectedContracts.Add(expected + $" [{method.Attributes}]");
    }

    CheckReflectedField("Container", "m_nview", "ZNetView", false);
    CheckReflectedField("Container", "m_lastRevision", "System.UInt32", false);
    CheckReflectedField("Player", "m_isLoading", "System.Boolean", false);
    CheckReflectedField("InventoryGui", "m_currentContainer", "Container", false);
    CheckReflectedField("InventoryGui", "m_animator", "UnityEngine.Animator", false);
    CheckReflectedField("PrivateArea", "m_allAreas", "System.Collections.Generic.List`1<PrivateArea>", true);
    CheckReflectedMethod("Container", "CheckForChanges", "System.Void", false);
    CheckReflectedMethod("Container", "CheckAccess", "System.Boolean", false, "System.Int64");
    CheckReflectedMethod("Inventory", "Changed", "System.Void", false, "System.Boolean", "System.Boolean");
    CheckReflectedMethod("PrivateArea", "IsEnabled", "System.Boolean", false);
    CheckReflectedMethod("PrivateArea", "IsInside", "System.Boolean", false, "UnityEngine.Vector3", "System.Single");
    CheckReflectedMethod("PrivateArea", "GetPermittedPlayers",
        "System.Collections.Generic.List`1<System.Collections.Generic.KeyValuePair`2<System.Int64,System.String>>", false);
}
string Hash(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();
var attributes = mod.CustomAttributes.Where(a => a.AttributeType.Name == "IgnoresAccessChecksToAttribute").Select(a => (string)a.ConstructorArguments[0].Value).ToArray();
foreach (string required in new[] { "assembly_valheim", "assembly_utils", "assembly_guiutils" })
    if (!attributes.Contains(required)) failures.Add("Missing runtime access attribute: " + required);
if (mod.MainModule.AssemblyReferences.Any(a => a.Name == "ServerSync")) failures.Add("ServerSync was not merged");
var fingerprints = new[] { "assembly_valheim.dll", "assembly_utils.dll", "assembly_guiutils.dll" }
    .Select(name => new { name, sha256 = Hash(Path.Combine(args[1], name)) }).ToArray();
var report = new
{
    candidate = Path.GetFullPath(args[0]), sha256 = Hash(args[0]), originalManaged = Path.GetFullPath(args[1]),
    fingerprints, directGameReferences = references.Count, patchCount = patches.Count, patches,
    reflectedGameContractCount = reflectedContracts.Count, reflectedContracts,
    existingNonpublicAccessCount = privateAccess.Count, runtimeAccessAttributes = attributes,
    failures = failures.Distinct().ToArray(), manual,
    limitations = new[] { "Static contract check; no game/Unity/Mono or multiplayer execution.",
        "Existing nonpublic calls still use the publicizer runtime strategy and require target-runtime validation.",
        "Reflection checks cover the listed area-handoff contracts only; other dynamic targets/reflection and other mods' patch composition require separate review." }
};
Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(args[3]))!);
File.WriteAllText(args[3], JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
Console.WriteLine($"References: {references.Count}; Harmony targets: {patches.Count}; reflected contracts: {reflectedContracts.Count}; failures: {failures.Distinct().Count()}; manual: {manual.Count}");
foreach (string failure in failures.Distinct()) Console.WriteLine(failure);
Environment.ExitCode = failures.Count == 0 ? 0 : 1;
