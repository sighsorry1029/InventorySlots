using InventorySlots;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

internal static class FavoriteMemoryPersistenceTests
{
    public static void RoundTripAndCharacterIsolation()
    {
        var serializer = new SerializerBuilder().WithNamingConvention(CamelCaseNamingConvention.Instance).Build();
        var deserializer = new DeserializerBuilder().WithNamingConvention(CamelCaseNamingConvention.Instance).IgnoreUnmatchedProperties().Build();
        const string oldYaml = "players:\n  playerA:\n    favoriteSlots:\n      - x: 1\n        y: 2\n";
        var oldState = ClientStateCore.Normalize(deserializer.Deserialize<InventorySlotsClientState>(oldYaml));
        if (oldState.Players["playerA"].FavoriteSlots.Single().Prefab != "") throw new Exception("Coordinate-only YAML must load without an item binding.");
        oldState.Players["playerA"].FavoriteSlots[0].Prefab = "Wood";
        oldState.Players["playerB"] = new InventorySlotsClientPlayerState
        {
            FavoriteSlots = new() { new() { X = 1, Y = 2, Prefab = "Stone" } }
        };
        var saved = ClientStateCore.Normalize(deserializer.Deserialize<InventorySlotsClientState>(serializer.Serialize(oldState)));
        if (saved.Players["playerA"].FavoriteSlots.Single().Prefab != "Wood" ||
            saved.Players["playerB"].FavoriteSlots.Single().Prefab != "Stone")
            throw new Exception("Remembered items must survive YAML and stay character-specific.");
        saved.Players["playerA"].FavoriteSlots.Clear();
        if (saved.Players["playerB"].FavoriteSlots.Count != 1) throw new Exception("Changing one character must not clear another's favorites.");
    }
}
