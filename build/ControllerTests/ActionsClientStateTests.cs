#if !INVENTORY_SLOTS
using System.IO;
using Plugin = InventoryActions.InventoryActionsPlugin;

internal static partial class Program
{
    private static void CheckActionsClientState()
    {
        Plugin.TestReset();
        Check(Path.GetFileName(Plugin.TestStateFile) == "InventoryActions.ClientState.yml", "Unambiguous unified client-state filename");
        string old = Plugin.TestWriteOldFavorites("alice");
        Plugin.TestLoadFavorites("alice");
        Check(Plugin.TestFavorite(2, 3) == null, "Old TXT favorites are not imported");
        Plugin.TestObserveFavorite(2, 3, "Wood"); Plugin.TestSaveFavoriteState();
        Check(!Plugin.TestSavePending, "Successful YAML save clears pending retry");
        Plugin.TestLoadFavorites("bob");
        Check(Plugin.TestFavorite(2, 3) == null && Plugin.TestMemoryPending, "Changing characters clears runtime favorite cache and requests observation");
        const string unicode = "Mod:木,材%Special";
        Plugin.TestObserveFavorite(2, 3, unicode); Plugin.TestSaveFavoriteState();
        // A guide write must preserve both characters and their remembered item identities.
        GuideKeyFrame(UnityEngine.KeyCode.F6);
        Plugin.TestRestartState();
        Plugin.TestLoadFavorites("alice");
        Check(Plugin.TestFavorite(2, 3) == "Wood" && Plugin.TestGuideCollapsed, "Guide save/restart preserves Alice and global collapsed preference");
        Plugin.TestLoadFavorites("bob");
        Check(Plugin.TestFavorite(2, 3) == unicode, "Same cell in another character retains Unicode prefab");
        Plugin.TestClearFavorites(); Plugin.TestRestartState(); Plugin.TestLoadFavorites("alice");
        Check(Plugin.TestFavorite(2, 3) == "Wood", "Clearing Bob never clears Alice");
        Plugin.TestLoadFavorites("bob");
        Check(Plugin.TestFavorite(2, 3) == null, "Cleared favorites stay empty after reload");
        Check(File.ReadAllText(old) == "2,3,Wood", "Old TXT is left untouched");

        Plugin.TestLoadFavorites("alice");
        using (FileStream locked = File.Open(Plugin.TestStateFile, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            Plugin.TestObserveFavorite(2, 3, "Stone"); Plugin.TestSaveFavoriteState();
            Check(Plugin.TestSavePending && Plugin.TestStateWarnings.Count > 0, "IO failure retains pending save");
            Plugin.TestObserveFavorite(2, 3, "Coal"); // New observation during the retry delay.
            Plugin.TestLoadFavorites("bob");
            Plugin.TestObserveFavorite(1, 1, "Resin"); Plugin.TestSaveFavoriteState();
            Check(Plugin.TestSavePending, "Changing characters does not discard pending file retry");
        }
        Player.m_localPlayer = null!;
        Plugin.TestFlushState();
        Check(!Plugin.TestSavePending, "Teardown can flush pending snapshots without a player object");
        Plugin.TestRestartState(); Plugin.TestLoadFavorites("alice");
        Check(Plugin.TestFavorite(2, 3) == "Coal", "Retry preserves the old character's latest observation before switch");
        Plugin.TestLoadFavorites("bob");
        Check(Plugin.TestFavorite(1, 1) == "Resin" && Plugin.TestFavorite(2, 3) == null, "Retry preserves the new character independently");

        Plugin.TestReset(); Plugin.TestLoadGuideYaml("players: [malformed\n");
        string broken = File.ReadAllText(Plugin.TestStateFile);
        GuideKeyFrame(UnityEngine.KeyCode.F6);
        Check(File.ReadAllText(Plugin.TestStateFile) == broken && Plugin.TestStateWarnings.Count > 0,
            "Malformed consolidated state is reported and never overwritten by guide changes");
    }
}
#endif
