using System;
using System.Collections.Generic;
using System.Reflection;

// Only external game/loader behavior is simulated. The localizers, translation files,
// YAML parser and UI helper are the real production sources. This is not a Unity run.
internal static class PlatformInitializer
{
    public static bool PlatformInitialized;
}

internal sealed class Localization
{
    private static Localization? _instance;
    internal static int GetterCalls, PreferenceReads, CreatedInstances;
    internal static string SelectedLanguage = "English";
    internal readonly Dictionary<string, string> Words = new();

    public static Localization instance
    {
        get
        {
            GetterCalls++;
            RequirePlatform();
            return _instance ??= new Localization();
        }
    }

    public Localization()
    {
        CreatedInstances++;
        if (CreatedInstances > 8) throw new InvalidOperationException("Recursive singleton construction");
        // Like Valheim, SetupLanguage fires before the singleton assignment completes.
        SetupLanguage("English");
        if (SelectedLanguage != "English") SetupLanguage(SelectedLanguage);
    }

    internal static void Reset()
    {
        _instance = null;
        GetterCalls = PreferenceReads = CreatedInstances = 0;
        SelectedLanguage = "English";
    }

    private static void RequirePlatform()
    {
        if (!PlatformInitializer.PlatformInitialized)
            throw new InvalidOperationException("Steamworks is not initialized.");
    }

    public string GetSelectedLanguage()
    {
        PreferenceReads++;
        RequirePlatform();
        return SelectedLanguage;
    }

    public void SetupLanguage(string language)
    {
        Words.Clear();
        HarmonyLib.Harmony.Dispatch(typeof(Localization), nameof(SetupLanguage), this, language);
    }

    public void AddWord(string key, string value) => Words[key] = value;
    public string Localize(string token) => Words.TryGetValue(token.TrimStart('$'), out string? value) ? value : "[" + token.TrimStart('$') + "]";
}

internal sealed class FejdStartup
{
    public void SetupGui() => HarmonyLib.Harmony.Dispatch(typeof(FejdStartup), nameof(SetupGui));
}

namespace BepInEx
{
    internal sealed class BaseUnityPlugin(string name)
    {
        public PluginInfo Info { get; } = new(name);
    }
    internal sealed class PluginInfo(string name)
    {
        public PluginMetadata Metadata { get; } = new(name);
    }
    internal sealed class PluginMetadata(string name)
    {
        public string Name => name;
        public string GUID => "sighsorry." + name;
    }
    internal static class Paths
    {
        public static string PluginPath = "", ConfigPath = "";
    }
}

namespace BepInEx.Logging
{
    internal sealed class ManualLogSource
    {
        public void LogWarning(string message) => throw new InvalidOperationException("Unexpected localization warning: " + message);
    }
    internal static class Logger
    {
        public static ManualLogSource CreateLogSource(string name) => new();
    }
}

namespace HarmonyLib
{
    internal static class AccessTools
    {
        public static MethodInfo? DeclaredMethod(Type type, string name) => type.GetMethod(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly);
    }
    internal sealed class HarmonyMethod(Type type, string name)
    {
        internal readonly MethodInfo Method = AccessTools.DeclaredMethod(type, name)!;
    }
    internal sealed class Harmony(string id)
    {
        internal static readonly Dictionary<MethodInfo, MethodInfo> Postfixes = new();
        public void Patch(MethodInfo original, HarmonyMethod postfix)
        {
            if (string.IsNullOrEmpty(id)) throw new InvalidOperationException("Missing Harmony ID");
            Postfixes.Add(original, postfix.Method);
        }
        internal static void Dispatch(Type type, string name, params object[] args)
        {
            if (Postfixes.TryGetValue(AccessTools.DeclaredMethod(type, name)!, out MethodInfo? postfix))
                postfix.Invoke(null, args);
        }
    }
}
