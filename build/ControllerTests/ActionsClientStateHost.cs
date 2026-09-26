#if !INVENTORY_SLOTS
using System.IO;
namespace InventoryActions;
public sealed partial class InventoryActionsPlugin
{
    public static string TestStateFile => ClientStateFilePath;
    public static bool TestSavePending => _favoriteMemorySavePending;
    public static bool TestMemoryPending => _favoriteMemoryPending;
    public static void TestLoadFavorites(string id)
    {
        Player.m_localPlayer = new Player { Id = id };
        EnsureFavoritesLoaded(Player.m_localPlayer);
    }
    public static void TestObserveFavorite(int x, int y, string prefab)
    {
        Vector2i cell = new(x, y);
        Runtime.FavoriteSlots.Add(cell);
        FavoriteSlotItems[cell] = prefab;
        _favoriteMemorySavePending = true;
    }
    public static string? TestFavorite(int x, int y) =>
        !Runtime.FavoriteSlots.Contains(new Vector2i(x, y)) ? null :
        FavoriteSlotItems.TryGetValue(new Vector2i(x, y), out string? prefab) ? prefab : "";
    public static void TestSaveFavoriteState() => SaveFavorites(Player.m_localPlayer);
    public static void TestFlushState() => FlushPendingClientState();
    public static void TestClearFavorites() { Runtime.FavoriteSlots.Clear(); FavoriteSlotItems.Clear(); SaveFavorites(Player.m_localPlayer); }
    public static void TestRestartState()
    {
        TestReloadGuide();
        Runtime.LoadedFavoritesPlayerId = "";
        Runtime.FavoriteSlots.Clear(); FavoriteSlotItems.Clear();
        _favoriteMemorySavePending = false;
    }
    public static string TestWriteOldFavorites(string id)
    {
        string path = Path.Combine(BepInEx.Paths.ConfigPath, "InventoryActions.Favorites." + id + ".txt");
        Directory.CreateDirectory(BepInEx.Paths.ConfigPath);
        File.WriteAllText(path, "2,3,Wood");
        return path;
    }
}
#endif
