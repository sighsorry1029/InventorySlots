using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using YamlDotNet.Serialization;

namespace LocalizationManager;

internal static class Localizer
{
    private static readonly string[] FileExtensions = { ".yml", ".json" };
    private static readonly ManualLogSource Log = BepInEx.Logging.Logger.CreateLogSource("InventorySlots.Localization");
    private static BaseUnityPlugin? _plugin;
    private static bool _loaded;

    public static event Action? OnLocalizationComplete;

    public static void Load(BaseUnityPlugin plugin)
    {
        _plugin = plugin;
        if (_loaded)
        {
            TryLoadCurrentLanguage();
            return;
        }

        _loaded = true;
        Harmony harmony = new(plugin.Info.Metadata.GUID + ".LocalizationManager");
        PatchLocalization(harmony);
        TryLoadCurrentLanguage();
    }

    private static void PatchLocalization(Harmony harmony)
    {
        MethodInfo? setupLanguage = AccessTools.DeclaredMethod(typeof(Localization), "SetupLanguage");
        if (setupLanguage != null)
        {
            harmony.Patch(setupLanguage, postfix: new HarmonyMethod(typeof(Localizer), nameof(LocalizationSetupLanguagePostfix)));
        }

        MethodInfo? setupGui = AccessTools.DeclaredMethod(typeof(FejdStartup), "SetupGui");
        if (setupGui != null)
        {
            harmony.Patch(setupGui, postfix: new HarmonyMethod(typeof(Localizer), nameof(FejdStartupSetupGuiPostfix)));
        }
    }

    private static void LocalizationSetupLanguagePostfix(Localization __instance, string language)
    {
        LoadLocalization(__instance, language);
    }

    private static void FejdStartupSetupGuiPostfix()
    {
        TryLoadCurrentLanguage();
    }

    private static void TryLoadCurrentLanguage()
    {
        if (Localization.instance == null)
        {
            return;
        }

        LoadLocalization(Localization.instance, Localization.instance.GetSelectedLanguage());
    }

    private static void LoadLocalization(Localization localization, string language)
    {
        if (_plugin == null || localization == null)
        {
            return;
        }

        Dictionary<string, string> translations = LoadLanguageDictionary("English");
        if (!string.Equals(language, "English", StringComparison.OrdinalIgnoreCase))
        {
            Merge(translations, LoadLanguageDictionary(language));
        }

        foreach (KeyValuePair<string, string> translation in translations)
        {
            localization.AddWord(translation.Key, translation.Value);
        }

        OnLocalizationComplete?.Invoke();
    }

    private static Dictionary<string, string> LoadLanguageDictionary(string language)
    {
        Dictionary<string, string> translations = new(StringComparer.OrdinalIgnoreCase);
        Merge(translations, LoadEmbeddedLanguage(language));
        foreach (string file in FindExternalTranslationFiles(language))
        {
            try
            {
                Merge(translations, ParseTranslations(File.ReadAllText(file), file));
            }
            catch (Exception ex)
            {
                Log.LogWarning($"Failed to load localization file {file}: {ex.Message}");
            }
        }

        return translations;
    }

    private static Dictionary<string, string> LoadEmbeddedLanguage(string language)
    {
        foreach (string extension in FileExtensions)
        {
            byte[]? bytes = ReadEmbeddedFileBytes("translations." + language + extension);
            if (bytes != null)
            {
                return ParseTranslations(Encoding.UTF8.GetString(bytes), language + extension);
            }
        }

        if (string.Equals(language, "English", StringComparison.OrdinalIgnoreCase))
        {
            Log.LogWarning("Found no embedded English localization for InventorySlots.");
        }

        return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    }

    private static IEnumerable<string> FindExternalTranslationFiles(string language)
    {
        if (_plugin == null)
        {
            yield break;
        }

        string modName = _plugin.Info.Metadata.Name;
        foreach (string root in GetExternalTranslationRoots())
        {
            if (!Directory.Exists(root))
            {
                continue;
            }

            foreach (string extension in FileExtensions)
            {
                string pattern = modName + "." + language + extension;
                foreach (string file in Directory.GetFiles(root, pattern, SearchOption.AllDirectories))
                {
                    yield return file;
                }
            }
        }
    }

    private static IEnumerable<string> GetExternalTranslationRoots()
    {
        if (!string.IsNullOrWhiteSpace(Paths.PluginPath))
        {
            yield return Paths.PluginPath;
        }

        if (_plugin != null)
        {
            yield return Path.Combine(Paths.ConfigPath, _plugin.Info.Metadata.GUID + "Translations");
        }
    }

    private static Dictionary<string, string> ParseTranslations(string text, string source)
    {
        try
        {
            Dictionary<string, string>? parsed = new DeserializerBuilder()
                .IgnoreFields()
                .Build()
                .Deserialize<Dictionary<string, string>>(text);
            return parsed ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }
        catch (Exception ex)
        {
            Log.LogWarning($"Failed to parse localization source {source}: {ex.Message}");
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }
    }

    private static void Merge(Dictionary<string, string> target, Dictionary<string, string> source)
    {
        foreach (KeyValuePair<string, string> entry in source)
        {
            target[entry.Key] = entry.Value;
        }
    }

    private static byte[]? ReadEmbeddedFileBytes(string resourceFileName)
    {
        if (_plugin == null)
        {
            return null;
        }

        Assembly assembly = _plugin.GetType().Assembly;
        string? resourceName = assembly.GetManifestResourceNames()
            .FirstOrDefault(name => name.EndsWith(resourceFileName, StringComparison.OrdinalIgnoreCase));
        if (resourceName == null)
        {
            return null;
        }

        using Stream? stream = assembly.GetManifestResourceStream(resourceName);
        if (stream == null)
        {
            return null;
        }

        using MemoryStream memory = new();
        stream.CopyTo(memory);
        return memory.Length == 0 ? null : memory.ToArray();
    }
}
