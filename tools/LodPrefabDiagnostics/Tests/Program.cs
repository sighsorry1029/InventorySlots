using System.Reflection;
using LodPrefabDiagnostics;

int passed = 0;
void Check(bool condition, string name)
{
    if (!condition) throw new InvalidOperationException(name);
    Console.WriteLine("PASS " + name);
    passed++;
}

var membership = new LodMembership();
membership.Add(100, 10, 0);
membership.Add(100, 10, 1);
membership.Add(100, 10, 1);
Check(membership.Renderers[100].Count == 1, "one renderer in several levels of one group is not a duplicate owner");
Check(membership.Renderers[100][10].SetEquals(new[] { 0, 1 }), "repeated entries in a level are deduplicated without losing other levels");
membership.Add(100, 20, 0);
Check(membership.Renderers[100].Count == 2, "parent and child groups sharing a renderer are detected");
membership.Add(200, 30, 0);
membership.Add(201, 40, 0);
Check(membership.Renderers[200].Count == 1 && membership.Renderers[201].Count == 1,
    "distinct renderer instances remain separate even if names or shared meshes are identical");
Check(membership.Renderers.Where(entry => entry.Value.Count > 1).Select(entry => entry.Key).SequenceEqual(new[] { 100 }),
    "mixed healthy and duplicate ownership reports only the duplicate renderer");
Check(new LodMembership().Renderers.Count == 0, "each scene scan starts empty, so removed registrations cannot stay cached");

string gamePath = args.Length > 0 ? Path.GetFullPath(args[0]) : @"C:\Program Files (x86)\Steam\steamapps\common\Valheim";
string pluginPath = args.Length > 1 ? Path.GetFullPath(args[1]) : Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../bin/Debug/LodPrefabDiagnostics.dll"));
string[] folders = { Path.Combine(gamePath, "valheim_Data/Managed"), Path.Combine(gamePath, "BepInEx/core"), Path.GetDirectoryName(pluginPath)! };
AppDomain.CurrentDomain.AssemblyResolve += (_, eventArgs) =>
{
    foreach (string folder in folders)
    {
        string path = Path.Combine(folder, new AssemblyName(eventArgs.Name).Name + ".dll");
        if (File.Exists(path)) return Assembly.LoadFrom(path);
    }
    return null;
};
Assembly game = Assembly.LoadFrom(Path.Combine(folders[0], "assembly_valheim.dll"));
Assembly plugin = Assembly.LoadFrom(pluginPath);
Console.WriteLine("Original game assembly: " + game.Location);
Console.WriteLine("Diagnostic assembly: " + plugin.Location);
Type pluginType = plugin.GetType("LodPrefabDiagnostics.Plugin", true)!;
Type visEquipment = game.GetType("VisEquipment", true)!;
foreach (var (patchName, methodName) in new[] { ("ItemAttachmentPatch", "AttachItem"), ("ArmorAttachmentPatch", "AttachArmor") })
{
    Type patch = pluginType.GetNestedType(patchName, BindingFlags.NonPublic)!;
    var target = (MethodInfo)patch.GetMethod("TargetMethod", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, null)!;
    Check(target.DeclaringType == visEquipment && target.Name == methodName && target.IsPrivate,
        methodName + " resolves to the private overload in the original game DLL");
    MethodInfo postfix = patch.GetMethod("Postfix", BindingFlags.Static | BindingFlags.NonPublic)!;
    Check(postfix.ReturnType == typeof(void) && postfix.GetParameters().All(p => !p.ParameterType.IsByRef),
        methodName + " observation cannot replace the original result or input arguments");
    foreach (ParameterInfo parameter in postfix.GetParameters())
    {
        Type? expected = parameter.Name switch
        {
            "__instance" => visEquipment,
            "__result" => target.ReturnType,
            _ => target.GetParameters().SingleOrDefault(p => p.Name == parameter.Name)?.ParameterType
        };
        Check(expected == parameter.ParameterType, methodName + " postfix parameter contract: " + parameter.Name);
    }
}
foreach (var (typeName, name, parameterTypes) in new[]
{
    ("ObjectDB", "GetItemPrefab", new[] { typeof(int) }),
    ("ZNetView", "GetZDO", Type.EmptyTypes),
    ("ZDO", "GetPrefab", Type.EmptyTypes),
    ("ZNetScene", "GetPrefab", new[] { typeof(int) }),
    ("ZNet", "IsDedicated", Type.EmptyTypes)
})
{
    MethodInfo? method = game.GetType(typeName, true)!.GetMethod(name, BindingFlags.Public | BindingFlags.Instance, null, parameterTypes, null);
    Check(method != null, typeName + "." + name + " is public in the original game DLL");
}
Check(plugin.GetReferencedAssemblies().All(a => a.Name != "InventorySlots" && a.Name != "ServerSync"),
    "diagnostic DLL has no InventorySlots or ServerSync dependency");
Console.WriteLine($"{passed} checks passed. No Unity scene, native renderer registration or live Harmony attachment was executed.");
