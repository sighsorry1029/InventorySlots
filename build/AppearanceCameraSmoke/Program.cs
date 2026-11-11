using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using HarmonyLib;

internal static class Program
{
    private static int Main(string[] args)
    {
        // Final mod DLL, ORIGINAL game Managed, BepInEx core. No publicized DLLs.
        string[] folders = { args[1], args[2], Path.GetDirectoryName(args[0])! };
        AppDomain.CurrentDomain.AssemblyResolve += (_, e) => {
            string name = new AssemblyName(e.Name).Name + ".dll";
            foreach (string folder in folders) { string p = Path.Combine(folder, name); if (File.Exists(p)) return Assembly.LoadFrom(p); }
            return null;
        };
        return Checks.Run(args);
    }
}

internal static class Checks
{
    private const BindingFlags Static = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
    [DllImport("mono-2.0-bdwgc.dll", CallingConvention = CallingConvention.Cdecl)]
    private static extern void mono_add_internal_call(string name, IntPtr function);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int HashCallback(IntPtr value);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int IntCallback();
    private static readonly HashCallback Hash = _ => 0;
    private static readonly IntCallback Offset = () => 0;
    private static bool SkipNative() => false;

    internal static int Run(string[] args)
    {
        // Minimal loader/Unity boundary fixtures; no game world or native camera
        // body executes. Actual hook signatures/detours use original game Mono.
        var bep = Assembly.LoadFrom(Path.Combine(args[2], "BepInEx.dll"));
        string isolated = Path.Combine(Path.GetTempPath(), "appearance-camera-smoke-" + Guid.NewGuid());
        bep.GetType("BepInEx.Paths", true)!.GetMethod("SetExecutablePath", Static)!.Invoke(null,
            new object[] { Path.Combine(isolated, "valheim.exe"), Path.Combine(isolated, "BepInEx"), args[1], new[] { args[1], args[2] } });
        Type threading = bep.GetType("BepInEx.ThreadingHelper", true)!;
        object queue = FormatterServices.GetUninitializedObject(threading);
        threading.GetField("_invokeLock", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(queue, new object());
        threading.GetField("<Instance>k__BackingField", Static)!.SetValue(null, queue);
        mono_add_internal_call("UnityEngine.Animator::StringToHash_Injected", Marshal.GetFunctionPointerForDelegate(Hash));
        mono_add_internal_call("UnityEngine.Object::GetOffsetOfInstanceIDInCPlusPlusObject", Marshal.GetFunctionPointerForDelegate(Offset));

        var mod = Assembly.LoadFrom(args[0]);
        var game = Assembly.LoadFrom(Path.Combine(args[1], "assembly_valheim.dll"));
        var unity = Assembly.LoadFrom(Path.Combine(args[1], "UnityEngine.CoreModule.dll"));
        Type camera = game.GetType("GameCamera", true)!;
        Type vector = unity.GetType("UnityEngine.Vector3", true)!;
        Type rotation = unity.GetType("UnityEngine.Quaternion", true)!;
        Type plugin = mod.GetType("InventorySlots.InventorySlotsPlugin", true)!;
        MethodInfo target = camera.GetMethod("GetCameraPosition", BindingFlags.Instance | BindingFlags.NonPublic,
            null, new[] { typeof(float), vector.MakeByRefType(), rotation.MakeByRefType() }, null)!;
        if (target == null || !target.IsPrivate || camera.GetField("m_camera", BindingFlags.Instance | BindingFlags.NonPublic) == null)
            throw new Exception("Expected original private camera contract");
        var owner = new Harmony("appearance-camera.contract-smoke");
        var foreign = new Harmony("appearance-camera.foreign-prefix");
        try
        {
            // Install before and after another camera owner; it must not claim a
            // skipped original, nor fail field injection / out-parameter binding.
            for (int order = 0; order < 2; order++)
            {
                if (order == 1) foreign.Patch(target, prefix: new HarmonyMethod(typeof(Checks), nameof(SkipNative)));
                plugin.GetMethod("InstallPathOfValheimanAppearanceCamera", Static)!.Invoke(null, new object[] { owner });
                if (order == 0) foreign.Patch(target, prefix: new HarmonyMethod(typeof(Checks), nameof(SkipNative)));
                if (!Harmony.GetPatchInfo(target).Postfixes.Any(p => p.owner == owner.Id)) throw new Exception("Missing camera postfix");
                object cameraInstance = FormatterServices.GetUninitializedObject(camera);
                object v = Activator.CreateInstance(vector, 4f, 5f, 6f)!;
                object q = Activator.CreateInstance(rotation, 0f, 0f, 0f, 1f)!;
                object[] values = { 0.02f, v, q };
                target.Invoke(cameraInstance, values);
                if ((float)vector.GetField("x")!.GetValue(values[1])! != 4f ||
                    (float)rotation.GetField("w")!.GetValue(values[2])! != 1f)
                    throw new Exception("Changed foreign camera output");
                owner.UnpatchSelf(); foreign.UnpatchSelf();
            }
            Console.WriteLine("PASS: original private camera contract, Harmony field/out binding, both install orders, skipped-native output and cleanup.");
            return 0;
        }
        finally { owner.UnpatchSelf(); foreign.UnpatchSelf(); }
    }
}
