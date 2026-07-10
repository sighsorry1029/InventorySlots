using System;
using System.IO;

namespace InventorySlots;

public sealed partial class InventorySlotsPlugin
{
    private const string DefaultYamlResourceName = "InventorySlots.Default.yml";
    private static readonly Lazy<string> DefaultYamlResource = new(LoadDefaultYaml);

    internal static string DefaultYaml => DefaultYamlResource.Value;

    private static string LoadDefaultYaml()
    {
        using Stream? stream = typeof(InventorySlotsPlugin).Assembly.GetManifestResourceStream(DefaultYamlResourceName);
        if (stream == null)
        {
            throw new InvalidOperationException($"Embedded default YAML resource '{DefaultYamlResourceName}' was not found.");
        }

        using StreamReader reader = new(stream);
        return reader.ReadToEnd();
    }
}
