using System;
using System.IO;

namespace InventorySlots;

public sealed partial class InventorySlotsPlugin
{
    private const string DefaultYamlResourceName = "InventorySlots.Default.yml";
    private const string DefaultResourceMapYamlResourceName = "InventorySlots.ResourceMap.Default.yml";
    private static readonly Lazy<string> DefaultYamlResource = new(LoadDefaultYaml);
    private static readonly Lazy<string> DefaultResourceMapYamlResource = new(LoadDefaultResourceMapYaml);

    internal static string DefaultYaml => DefaultYamlResource.Value;
    internal static string DefaultResourceMapYaml => DefaultResourceMapYamlResource.Value;

    private static string LoadDefaultYaml() =>
        LoadEmbeddedYaml(DefaultYamlResourceName);

    private static string LoadDefaultResourceMapYaml() =>
        LoadEmbeddedYaml(DefaultResourceMapYamlResourceName);

    private static string LoadEmbeddedYaml(string resourceName)
    {
        using Stream? stream = typeof(InventorySlotsPlugin).Assembly.GetManifestResourceStream(resourceName);
        if (stream == null)
        {
            throw new InvalidOperationException($"Embedded default YAML resource '{resourceName}' was not found.");
        }

        using StreamReader reader = new(stream);
        return reader.ReadToEnd();
    }
}
