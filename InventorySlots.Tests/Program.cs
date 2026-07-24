using InventorySlots;

TestRunner.Run(
    ("Default YAML parses with expected sections", Tests.DefaultYamlParsesWithExpectedSections),
    ("Malformed YAML is rejected", Tests.MalformedYamlIsRejected),
    ("Null YAML slot entry is rejected", Tests.NullYamlSlotEntryIsRejected),
    ("Unknown YAML property is rejected", Tests.UnknownYamlPropertyIsRejected),
    ("Structured YAML group matcher is rejected", Tests.StructuredYamlGroupMatcherIsRejected),
    ("Inventory limits parse exact and group targets", Tests.InventoryLimitsParseExactAndGroupTargets),
    ("Inventory limits reject negative values", Tests.InventoryLimitsRejectNegativeValues),
    ("Slot id normalization preserves compat dots", Tests.SlotIdNormalizationPreservesCompatDots),
    ("Group id normalization removes punctuation", Tests.GroupIdNormalizationRemovesPunctuation),
    ("Localization token stripping preserves item identity", Tests.LocalizationTokenStrippingPreservesItemIdentity),
    ("Resource tier map normalizes tokens and keeps first tier", Tests.ResourceTierMapNormalizesTokensAndKeepsFirstTier),
    ("Built-in group section names normalize to ids", Tests.BuiltInGroupSectionNamesNormalizeToIds),
    ("Dominant food stat tie breaks are stable", Tests.DominantFoodStatTieBreaksAreStable),
    ("Dominant food stat ignores empty foods", Tests.DominantFoodStatIgnoresEmptyFoods),
    ("ColoredFork food stat copy mirrors InventorySlots behavior", Tests.ColoredForkFoodStatCopyMirrorsInventorySlotsBehavior),
    ("Crafting frame fast-path stamp tracks relevant fields", Tests.CraftingFrameFastPathStampTracksRelevantFields),
    ("Crafting grid stamp tracks pinned tooltip changes", Tests.CraftingGridStampTracksPinnedTooltipChanges),
    ("Crafting scrollbar stamp ignores sub-pixel jitter", Tests.CraftingScrollbarStampIgnoresSubPixelJitter),
    ("Crafting group rail stamp tracks selected group", Tests.CraftingGroupRailStampTracksSelectedGroup),
    ("Crafting search stamp tracks query and focus", Tests.CraftingSearchStampTracksQueryAndFocus),
    ("Crafting sort buttons stamp tracks mode", Tests.CraftingSortButtonsStampTracksMode),
    ("Crafting status HUD stamp tracks warning text", Tests.CraftingStatusHudStampTracksWarningText),
    ("Crafting text stamp separates text fields", Tests.CraftingTextStampSeparatesTextFields),
    ("Crafting text color stamp tracks color state", Tests.CraftingTextColorStampTracksColorState),
    ("Crafting simple tooltip stamp avoids delimiter collisions", Tests.CraftingSimpleTooltipStampAvoidsDelimiterCollisions),
    ("Tier sort mode prioritizes higher resource tier", Tests.TierSortModePrioritizesHigherResourceTier),
    ("Group sort mode prioritizes configured group order", Tests.GroupSortModePrioritizesConfiguredGroupOrder),
    ("Tier sort mode clusters equipment sets by slot", Tests.TierSortModeClustersEquipmentSetsBySlot),
    ("Sort key fallback uses localized name", Tests.SortKeyFallbackUsesLocalizedName),
    ("Crafting view favorites sort before craftable", Tests.CraftingViewFavoritesSortBeforeCraftable),
    ("Crafting view craftable sort before original order", Tests.CraftingViewCraftableSortBeforeOriginalOrder),
    ("Crafting view sort key falls back to original order", Tests.CraftingViewSortKeyFallsBackToOriginalOrder),
    ("Client state normalize creates missing roots", Tests.ClientStateNormalizeCreatesMissingRoots),
    ("Client state normalize trims players and lists", Tests.ClientStateNormalizeTrimsPlayersAndLists),
    ("Custom equipped item keeps stable slot identity during auto-adopt", Tests.CustomEquippedItemKeepsStableSlotIdentityDuringAutoAdopt),
    ("Unmarked item can auto-adopt matching grid slot", Tests.UnmarkedItemCanAutoAdoptMatchingGridSlot),
    ("Keep-on-death quickslot rows prefer their original slot", Tests.KeepOnDeathQuickslotRowsPreferOriginalSlot),
    ("Keep-on-death quickslot falls back to empty quickslot before inventory", Tests.KeepOnDeathQuickslotFallsBackToEmptyQuickslotBeforeInventory),
    ("Keep-on-death quickslot uses regular cell only after quickslots fail", Tests.KeepOnDeathQuickslotUsesRegularCellOnlyAfterQuickslotsFail),
    ("Keep-on-death full inventory preserves without overwriting", Tests.KeepOnDeathFullInventoryPreservesWithoutOverwriting),
    ("Keep-on-death regular item keeps original cell when available", Tests.KeepOnDeathRegularItemKeepsOriginalCellWhenAvailable),
    ("Keep-on-death preservation cell uses original when free", Tests.KeepOnDeathPreservationCellUsesOriginalWhenFree),
    ("Keep-on-death preservation cell uses overflow when original occupied", Tests.KeepOnDeathPreservationCellUsesOverflowWhenOriginalOccupied),
    ("Inventory first-free cell skips disallowed and occupied cells", Tests.InventoryFirstFreeCellSkipsDisallowedAndOccupiedCells),
    ("Inventory first-free cell reports no cell when full", Tests.InventoryFirstFreeCellReportsNoCellWhenFull),
    ("Inventory load preservation treats quickslot rows as tail cells", Tests.InventoryLoadPreservationTreatsQuickslotRowsAsTailCells),
    ("Inventory load preservation rejects hotbar cells", Tests.InventoryLoadPreservationRejectsHotbarCells),
    ("Inventory load preservation keeps free tail cell", Tests.InventoryLoadPreservationKeepsFreeTailCell),
    ("Inventory load preservation overflows occupied tail cell", Tests.InventoryLoadPreservationOverflowsOccupiedTailCell),
    ("Inventory load preservation never falls back into hotbar", Tests.InventoryLoadPreservationNeverFallsBackIntoHotbar),
    ("Inventory load preservation ignores out-of-width tail requests", Tests.InventoryLoadPreservationIgnoresOutOfWidthTailRequests),
    ("Inventory placement policy keeps containers top-first", Tests.InventoryPlacementPolicyKeepsContainersTopFirst),
    ("Inventory placement policy shares top-first empty count", Tests.InventoryPlacementPolicySharesTopFirstEmptyCount),
    ("Inventory automatic placement prefers regular rows before hotbar", Tests.InventoryAutomaticPlacementPrefersRegularRowsBeforeHotbar),
    ("Inventory automatic placement falls back to hotbar", Tests.InventoryAutomaticPlacementFallsBackToHotbar),
    ("Inventory limits accept the exact remaining amount", Tests.InventoryLimitsAcceptExactRemainingAmount),
    ("Inventory limits reject excess and additions to existing excess", Tests.InventoryLimitsRejectExcessAdditions),
    ("Action cell policy favorites include quickslots", Tests.ActionCellPolicyFavoritesIncludeQuickslots),
    ("Action cell policy keeps quickslots out of container action sources", Tests.ActionCellPolicyKeepsQuickslotsOutOfContainerActionSources),
    ("Action cell policy restock targets include hotbar and quickslots", Tests.ActionCellPolicyRestockTargetsIncludeHotbarAndQuickslots),
    ("Action cell policy trash allows regular inventory only", Tests.ActionCellPolicyTrashAllowsRegularInventoryOnly),
    ("InventoryActions action cell policy copy mirrors InventorySlots behavior", Tests.InventoryActionsActionCellPolicyCopyMirrorsInventorySlotsBehavior),
    ("Keep-on-death equipment prefers regular cell before unrelated special slot", Tests.KeepOnDeathEquipmentPrefersRegularCellBeforeUnrelatedSpecialSlot),
    ("Keep-on-death quickslot avoids unrelated special slot when packed", Tests.KeepOnDeathQuickslotAvoidsUnrelatedSpecialSlotWhenPacked),
    ("Keep-on-death equipment prefers same-kind special slot", Tests.KeepOnDeathEquipmentPrefersSameKindSpecialSlot),
    ("Upgrade favorite reuses existing item id", Tests.UpgradeFavoriteReusesExistingItemId),
    ("Upgrade favorite creates unique item id", Tests.UpgradeFavoriteCreatesUniqueItemId),
    ("Upgrade favorite set and remove trims id", Tests.UpgradeFavoriteSetAndRemoveTrimsId),
    ("Jewelcrafting native tooltip refresh skips stable same signature rows", Tests.JewelcraftingNativeTooltipRefreshSkipsStableSameSignatureRows),
    ("Jewelcrafting native tooltip refresh runs for changed or unstable state", Tests.JewelcraftingNativeTooltipRefreshRunsForChangedOrUnstableState),
    ("Jewelcrafting native tooltip signature tracks detail keys", Tests.JewelcraftingNativeTooltipSignatureTracksDetailKeys),
    ("Simple tooltip owner ignores stale hide", Tests.SimpleTooltipOwnerIgnoresStaleHide),
    ("Hover source VNEI uses owned renderer and crafting alpha", Tests.HoverSourceVneiUsesOwnedRendererAndCraftingAlpha),
    ("Hover source owned crafting only suppresses EpicLoot layout", Tests.HoverSourceOwnedCraftingOnlySuppressesEpicLootLayout),
    ("Tooltip source cache prunes matching entries", Tests.TooltipSourceCachePrunesMatchingEntries),
    ("Tooltip source cache trims oldest entries", Tests.TooltipSourceCacheTrimsOldestEntries),
    ("Container transfer sums moved amounts and callbacks", Tests.ContainerTransferSumsMovedAmountsAndCallbacks),
    ("Container transfer stays quiet when nothing moves", Tests.ContainerTransferStaysQuietWhenNothingMoves),
    ("InventoryActions container transfer core copy mirrors InventorySlots behavior", Tests.InventoryActionsContainerTransferCoreCopyMirrorsInventorySlotsBehavior),
    ("InventoryActions container action core copy mirrors InventorySlots behavior", Tests.InventoryActionsContainerActionCoreCopyMirrorsInventorySlotsBehavior),
    ("Restock target limits parse config entries", Tests.RestockTargetLimitsParseConfigEntries),
    ("Restock target limit resolves aliases and clamps to max stack", Tests.RestockTargetLimitResolvesAliasesAndClampsToMaxStack),
    ("Restock target limit resolves localized item names", Tests.RestockTargetLimitResolvesLocalizedItemNames),
    ("InventoryActions restock target limit copy mirrors InventorySlots behavior", Tests.InventoryActionsRestockTargetLimitCopyMirrorsInventorySlotsBehavior),
    ("Intentional InventoryActions source copies stay synchronized", Tests.IntentionalInventoryActionsSourceCopiesStaySynchronized),
    ("Release metadata versions stay synchronized", Tests.ReleaseMetadataVersionsStaySynchronized));

internal static class Tests
{
    public static void DefaultYamlParsesWithExpectedSections()
    {
        YamlRoot root = InventorySlotsConfigCore.ParseYaml(InventorySlotsPlugin.DefaultYaml);

        Assert.Contains(root.Slots.Select(slot => slot.Id), "helmet");
        Assert.Contains(root.Slots.Select(slot => slot.Id), "jewelcrafting.ring");
        Assert.Contains(root.Slots.Select(slot => slot.Id), "rustybags.quiver");
        Assert.Contains(root.QuickSlots, "Melee");
        Assert.False(root.QuickSlots.Contains("balancedfood", StringComparer.OrdinalIgnoreCase), "QuickSlots should not include the removed balancedfood group");
        Assert.Contains(root.QuickSlots, "mead");
        Assert.Contains(root.QuickSlots, "potion");
        Assert.False(root.Groups["Food"].Contains("balancedfood", StringComparer.OrdinalIgnoreCase), "Food should not expose the removed balancedfood group");
        Assert.False(root.Groups["Food"].Contains("uncooked", StringComparer.OrdinalIgnoreCase), "Food should not expose uncooked station inputs as a default subgroup");
        Assert.Equal("healthfood", root.Groups["Food"][0]);
        Assert.Equal("feast", root.Groups["Food"][root.Groups["Food"].Count - 1]);
        Assert.True(root.Groups["Consumable"].Contains("mead", StringComparer.OrdinalIgnoreCase), "Consumable should expose fermenter outputs as mead");
        Assert.True(root.Groups["Consumable"].Contains("potion", StringComparer.OrdinalIgnoreCase), "Consumable should expose direct status-effect consumables as potion");
        Assert.False(root.Groups["Melee"].Contains("torch", StringComparer.OrdinalIgnoreCase), "torch should be folded into tool rather than listed as its own subgroup");
        Assert.True(root.Groups["Melee"].IndexOf("tankards") > root.Groups["Melee"].IndexOf("tool"), "tankards should be a configurable subgroup after tool");
        Assert.Contains(root.Groups["tankards"], "Tankard");
        Assert.Contains(root.Groups["tankards"], "Tankard_dvergr");
        Assert.Contains(root.Groups["tankards"], "TankardAnniversary");
        Assert.Equal(1, root.InventoryLimits["FishingRod"]);
        Assert.Equal(3, root.InventoryLimits["tankards"]);
        Assert.Equal(3, root.InventoryLimits["FLG_TamingOrb"]);
        Assert.True(root.ResourceMap.Count >= 8, "default resourceMap should contain biome tiers");
    }

    public static void MalformedYamlIsRejected()
    {
        bool parsed = InventorySlotsConfigCore.TryParseYaml("Slots:\n  - id: [", out YamlRoot next, out Exception? error);

        Assert.False(parsed, "malformed YAML should not parse");
        Assert.True(error != null, "parse error should be reported");
        Assert.Equal(0, next.Slots.Count);
    }

    public static void NullYamlSlotEntryIsRejected()
    {
        bool parsed = InventorySlotsConfigCore.TryParseYaml("Slots: [null]", out YamlRoot next, out Exception? error);

        Assert.False(parsed, "null slot entries should fail parsing before configuration apply");
        Assert.True(error is InvalidDataException, "null slot entries should report an invalid data error");
        Assert.Equal(0, next.Slots.Count);
    }

    public static void UnknownYamlPropertyIsRejected()
    {
        string yaml = """
        Slots: []
        TypoRoot: true
        """;

        Assert.False(InventorySlotsConfigCore.TryParseYaml(yaml, out _, out Exception? error), "unknown root property should fail strict parsing");
        Assert.True(error != null, "unknown root property should report an error");
    }

    public static void StructuredYamlGroupMatcherIsRejected()
    {
        string yaml = """
        Groups:
          customWeapons:
            Prefabs:
              - SwordIron
        """;

        Assert.False(
            InventorySlotsConfigCore.TryParseYaml(yaml, out _, out Exception? error),
            "custom groups must remain simple prefab-name lists");
        Assert.True(error != null, "structured group syntax should report an error");
    }

    public static void InventoryLimitsParseExactAndGroupTargets()
    {
        YamlRoot root = InventorySlotsConfigCore.ParseYaml("""
        InventoryLimits:
          FishingRod: 1
          tankards: 3
        """);

        Dictionary<string, int> limits = InventorySlotsConfigCore.BuildInventoryLimits(root);

        Assert.Equal(1, limits["fishingrod"]);
        Assert.Equal(3, limits["TANKARDS"]);
    }

    public static void InventoryLimitsRejectNegativeValues()
    {
        bool parsed = InventorySlotsConfigCore.TryParseYaml("""
        InventoryLimits:
          FishingRod: -1
        """, out _, out Exception? error);

        Assert.False(parsed, "negative inventory limits should fail parsing");
        Assert.True(error != null, "negative inventory limits should report an error");
    }

    public static void SlotIdNormalizationPreservesCompatDots()
    {
        Assert.Equal("jewelcrafting.ring", InventorySlotsConfigCore.NormalizeSlotId(" Jewelcrafting.Ring! "));
        Assert.Equal("rustybags.quiver", InventorySlotsConfigCore.NormalizeSlotId("RustyBags.Quiver"));
        Assert.Equal("smoothbrainbackpacks.backpack", InventorySlotsConfigCore.NormalizeSlotId("smoothbrainbackpacks.backpack"));
    }

    public static void GroupIdNormalizationRemovesPunctuation()
    {
        Assert.Equal("magicsupremacybelt", InventorySlotsConfigCore.NormalizeGroupId("Magic-Supremacy.Belt"));
        Assert.Equal("healthfood", InventorySlotsConfigCore.NormalizeGroupId(" Health Food "));
    }

    public static void LocalizationTokenStrippingPreservesItemIdentity()
    {
        Assert.Equal("furbundlenorth_tw", InventorySlotsConfigCore.StripLocalizationToken("$item_furbundlenorth_tw"));
        Assert.Equal("meadbase_tasty", InventorySlotsConfigCore.StripLocalizationToken("$item_meadbase_tasty"));
        Assert.Equal("inventoryslots_group_food", InventorySlotsConfigCore.StripLocalizationToken("$inventoryslots_group_food"));
    }

    public static void ResourceTierMapNormalizesTokensAndKeepsFirstTier()
    {
        YamlRoot root = InventorySlotsConfigCore.ParseYaml("""
        resourceMap:
          - biome: Meadows
            materials:
              - Wood
              - $item_Stone
          - biome: BlackForest
            materials:
              - Wood
              - Bronze(Clone)
        """);

        Dictionary<string, int> tiers = InventorySlotsConfigCore.BuildResourceTierMap(root);

        Assert.Equal(0, tiers["wood"]);
        Assert.Equal(0, tiers["stone"]);
        Assert.Equal(1, tiers["bronze"]);
    }

    public static void BuiltInGroupSectionNamesNormalizeToIds()
    {
        Assert.True(ItemGroupRegistry.TryNormalizeSectionId("Melee", out string melee), "Melee section should normalize");
        Assert.Equal("melee", melee);
        Assert.True(ItemGroupRegistry.TryNormalizeSectionId("Meadbase", out string meadbase), "Meadbase section should normalize");
        Assert.Equal("meadbase", meadbase);
        Assert.False(ItemGroupRegistry.TryNormalizeSectionId("melee", out _), "section YAML names are intentionally case-sensitive");
    }

    public static void DominantFoodStatTieBreaksAreStable()
    {
        Assert.True(FoodStatCore.TryGetDominant(30f, 90f, 100f, out FoodStat ratatoskr), "Ratatoskr's Desire should classify");
        Assert.Equal(FoodStat.Eitr, ratatoskr);

        Assert.True(FoodStatCore.TryGetDominant(41f, 14f, 52f, out FoodStat squirrelStew), "Squirrel Stew should classify");
        Assert.Equal(FoodStat.Eitr, squirrelStew);

        Assert.True(FoodStatCore.TryGetDominant(22f, 22f, 0f, out FoodStat healthTie), "health/stamina tie should classify");
        Assert.Equal(FoodStat.Health, healthTie);

        Assert.True(FoodStatCore.TryGetDominant(0f, 30f, 30f, out FoodStat staminaTie), "stamina/eitr tie should classify");
        Assert.Equal(FoodStat.Stamina, staminaTie);

        Assert.True(FoodStatCore.TryGetDominant(50f, 50f, 50f, out FoodStat allTie), "all-stat tie should classify");
        Assert.Equal(FoodStat.Health, allTie);
    }

    public static void DominantFoodStatIgnoresEmptyFoods()
    {
        Assert.False(FoodStatCore.TryGetDominant(0f, 0f, 0f, out FoodStat stat), "zero-value food should not classify");
        Assert.Equal(FoodStat.None, stat);
    }

    public static void ColoredForkFoodStatCopyMirrorsInventorySlotsBehavior()
    {
        (float Health, float Stamina, float Eitr)[] cases =
        {
            (0f, 0f, 0f),
            (22f, 22f, 0f),
            (0f, 30f, 30f),
            (50f, 50f, 50f),
            (30f, 90f, 100f)
        };

        foreach ((float health, float stamina, float eitr) in cases)
        {
            bool inventorySlotsResult = FoodStatCore.TryGetDominant(health, stamina, eitr, out FoodStat inventorySlotsStat);
            bool coloredForkResult = ColoredFork.FoodStatCore.TryGetDominant(
                health,
                stamina,
                eitr,
                out ColoredFork.FoodStat coloredForkStat);

            Assert.Equal(inventorySlotsResult, coloredForkResult);
            Assert.Equal(inventorySlotsStat.ToString(), coloredForkStat.ToString());
        }
    }

    public static void CraftingFrameFastPathStampTracksRelevantFields()
    {
        CraftingFrameFastPathStamp baseline = FrameStamp(adapterKind: CraftingTabAdapterKind.Vanilla);
        CraftingFrameFastPathStamp same = FrameStamp(adapterKind: CraftingTabAdapterKind.Vanilla);
        CraftingFrameFastPathStamp differentAdapter = FrameStamp(adapterKind: CraftingTabAdapterKind.RecycleNReclaim);
        CraftingFrameFastPathStamp differentSelection = FrameStamp(adapterKind: CraftingTabAdapterKind.Vanilla, selectedRecipeIndex: 4);

        Assert.True(baseline.Equals(same), "same fast-path state should reuse the frame stamp");
        Assert.False(baseline.Equals(differentAdapter), "adapter changes must invalidate the frame stamp");
        Assert.False(baseline.Equals(differentSelection), "selection changes must invalidate the frame stamp");
    }

    public static void CraftingGridStampTracksPinnedTooltipChanges()
    {
        CraftingRecipeGridStamp baseline = new(4, 0, 2, 42, "pin=1", 12, 3);
        CraftingRecipeGridStamp same = new(4, 0, 2, 42, "pin=1", 12, 3);
        CraftingRecipeGridStamp changedPinnedTooltip = new(4, 0, 2, 42, "pin=2", 12, 3);

        Assert.True(baseline.Equals(same), "same grid state should reuse the grid stamp");
        Assert.False(baseline.Equals(changedPinnedTooltip), "pinned tooltip changes must invalidate the grid stamp");
    }

    public static void CraftingScrollbarStampIgnoresSubPixelJitter()
    {
        CraftingRecipeScrollbarStamp baseline = new(3, 1, visible: true, gridX: 10.0001f, gridY: -20.0001f);
        CraftingRecipeScrollbarStamp samePixel = new(3, 1, visible: true, gridX: 10.0002f, gridY: -20.0002f);
        CraftingRecipeScrollbarStamp moved = new(3, 1, visible: true, gridX: 10.004f, gridY: -20.0002f);

        Assert.True(baseline.Equals(samePixel), "tiny layout jitter should not invalidate the scrollbar stamp");
        Assert.False(baseline.Equals(moved), "meaningful grid movement should invalidate the scrollbar stamp");
    }

    public static void CraftingGroupRailStampTracksSelectedGroup()
    {
        CraftingGroupRailStamp baseline = new(1, 2, 10f, -20f, "food", 3, 0.85f, "available", "food,melee");
        CraftingGroupRailStamp same = new(1, 2, 10.0001f, -20.0001f, "food", 3, 0.8501f, "available", "food,melee");
        CraftingGroupRailStamp changedGroup = new(1, 2, 10.0001f, -20.0001f, "melee", 3, 0.8501f, "available", "food,melee");

        Assert.True(baseline.Equals(same), "tiny layout jitter should not invalidate the group rail stamp");
        Assert.False(baseline.Equals(changedGroup), "selected group changes must invalidate the group rail stamp");
    }

    public static void CraftingSearchStampTracksQueryAndFocus()
    {
        CraftingSearchInputStamp baseline = new(1, 2, 10f, 20f, 120f, 30f, "jam", 4, focused: false, 5, 6, 7);
        CraftingSearchInputStamp same = new(1, 2, 10.0001f, 20.0001f, 120.0001f, 30.0001f, "jam", 4, focused: false, 5, 6, 7);
        CraftingSearchInputStamp focused = new(1, 2, 10.0001f, 20.0001f, 120.0001f, 30.0001f, "jam", 4, focused: true, 5, 6, 7);
        CraftingSearchInputStamp query = new(1, 2, 10.0001f, 20.0001f, 120.0001f, 30.0001f, "soup", 4, focused: false, 5, 6, 7);

        Assert.True(baseline.Equals(same), "tiny layout jitter should not invalidate the search input stamp");
        Assert.False(baseline.Equals(focused), "focus changes must invalidate the search input stamp");
        Assert.False(baseline.Equals(query), "query changes must invalidate the search input stamp");
    }

    public static void CraftingSortButtonsStampTracksMode()
    {
        CraftingSortModeButtonsStamp baseline = new(1, 2, 10f, 20f, 70f, 30f, 30f, CraftingRecipeSortMode.GroupThenTier, 4, 5, 6, 7);
        CraftingSortModeButtonsStamp same = new(1, 2, 10.0001f, 20.0001f, 70.0001f, 30.0001f, 30.0001f, CraftingRecipeSortMode.GroupThenTier, 4, 5, 6, 7);
        CraftingSortModeButtonsStamp changedMode = new(1, 2, 10.0001f, 20.0001f, 70.0001f, 30.0001f, 30.0001f, CraftingRecipeSortMode.TierThenGroup, 4, 5, 6, 7);

        Assert.True(baseline.Equals(same), "tiny layout jitter should not invalidate the sort button stamp");
        Assert.False(baseline.Equals(changedMode), "sort mode changes must invalidate the sort button stamp");
    }

    public static void CraftingStatusHudStampTracksWarningText()
    {
        CraftingStatusHudStamp baseline = new(1, 2, 10f, -20f, 120f, 30f, "Need station|level");
        CraftingStatusHudStamp same = new(1, 2, 10.0001f, -20.0001f, 120.0001f, 30.0001f, "Need station|level");
        CraftingStatusHudStamp changedWarning = new(1, 2, 10.0001f, -20.0001f, 120.0001f, 30.0001f, "Need station");

        Assert.True(baseline.Equals(same), "tiny layout jitter should not invalidate the status HUD stamp");
        Assert.False(baseline.Equals(changedWarning), "warning text changes must invalidate the status HUD stamp");
    }

    public static void CraftingTextStampSeparatesTextFields()
    {
        CraftingTextStamp baseline = new("craft", "Run+[", "children=3", 18f);
        CraftingTextStamp same = new("craft", "Run+[", "children=3", 18.0001f);
        CraftingTextStamp changedText = new("craft", "Run+]", "children=3", 18.0001f);
        CraftingTextStamp changedScope = new("progress", "Run+[", "children=3", 18.0001f);
        CraftingTextStamp delimiterCollision = new("craft|Run+", "[", "children=3", 18.0001f);

        Assert.True(baseline.Equals(same), "tiny font-size jitter should not invalidate the text stamp");
        Assert.False(baseline.Equals(changedText), "label changes must invalidate the text stamp");
        Assert.False(baseline.Equals(changedScope), "cache scope changes must invalidate the text stamp");
        Assert.False(baseline.Equals(delimiterCollision), "field boundaries must not collapse through delimiter-like text");
    }

    public static void CraftingTextColorStampTracksColorState()
    {
        CraftingTextColorStamp baseline = new(true, 1f, 0.8f, 0.2f, 1f, "children=3");
        CraftingTextColorStamp same = new(true, 1f, 0.8001f, 0.2001f, 1f, "children=3");
        CraftingTextColorStamp changedInteractable = new(false, 1f, 0.8001f, 0.2001f, 1f, "children=3");
        CraftingTextColorStamp changedColor = new(true, 1f, 0.805f, 0.2001f, 1f, "children=3");

        Assert.True(baseline.Equals(same), "tiny color jitter should not invalidate the text color stamp");
        Assert.False(baseline.Equals(changedInteractable), "interactable changes must invalidate the text color stamp");
        Assert.False(baseline.Equals(changedColor), "visible color changes must invalidate the text color stamp");
    }

    public static void CraftingSimpleTooltipStampAvoidsDelimiterCollisions()
    {
        CraftingSimpleTooltipStamp baseline = new("a|b", "c");
        CraftingSimpleTooltipStamp same = new("a|b", "c");
        CraftingSimpleTooltipStamp delimiterCollision = new("a", "b|c");

        Assert.True(baseline.Equals(same), "same tooltip topic and body should reuse the tooltip stamp");
        Assert.False(baseline.Equals(delimiterCollision), "topic and body must remain separate fields");
    }

    public static void TierSortModePrioritizesHigherResourceTier()
    {
        SortKey lowerTierEarlierGroup = Key(resourceTier: 1, groupRank: 0, bigGroupRank: 0, name: "A");
        SortKey higherTierLaterGroup = Key(resourceTier: 4, groupRank: 9, bigGroupRank: 9, name: "B");

        Assert.GreaterThan(0, SortKeyComparerCore.Compare(lowerTierEarlierGroup, higherTierLaterGroup, CraftingRecipeSortMode.TierThenGroup));
        Assert.LessThan(0, SortKeyComparerCore.Compare(higherTierLaterGroup, lowerTierEarlierGroup, CraftingRecipeSortMode.TierThenGroup));
    }

    public static void GroupSortModePrioritizesConfiguredGroupOrder()
    {
        SortKey higherTierLaterGroup = Key(resourceTier: 4, groupRank: 0, bigGroupRank: 3, name: "A");
        SortKey lowerTierEarlierGroup = Key(resourceTier: 1, groupRank: 0, bigGroupRank: 1, name: "B");

        Assert.GreaterThan(0, SortKeyComparerCore.Compare(higherTierLaterGroup, lowerTierEarlierGroup, CraftingRecipeSortMode.GroupThenTier));
        Assert.LessThan(0, SortKeyComparerCore.Compare(lowerTierEarlierGroup, higherTierLaterGroup, CraftingRecipeSortMode.GroupThenTier));
    }

    public static void TierSortModeClustersEquipmentSetsBySlot()
    {
        SortKey chest = Key(resourceTier: 2, groupRank: 0, bigGroupRank: 0, setKey: "bronze", slot: 1, name: "Bronze Chest");
        SortKey helmet = Key(resourceTier: 2, groupRank: 9, bigGroupRank: 9, setKey: "bronze", slot: 0, name: "Bronze Helmet");

        Assert.GreaterThan(0, SortKeyComparerCore.Compare(chest, helmet, CraftingRecipeSortMode.TierThenGroup));
        Assert.LessThan(0, SortKeyComparerCore.Compare(helmet, chest, CraftingRecipeSortMode.TierThenGroup));
    }

    public static void SortKeyFallbackUsesLocalizedName()
    {
        SortKey queen = Key(name: "Queen Jam");
        SortKey carrot = Key(name: "Carrot Soup");

        Assert.GreaterThan(0, SortKeyComparerCore.Compare(queen, carrot, CraftingRecipeSortMode.TierThenGroup));
        Assert.LessThan(0, SortKeyComparerCore.Compare(carrot, queen, CraftingRecipeSortMode.TierThenGroup));
    }

    public static void CraftingViewFavoritesSortBeforeCraftable()
    {
        int comparison = CraftingRecipeViewCore.CompareWithSortKey(
            aIsFavorite: false,
            bIsFavorite: true,
            aCanCraft: true,
            bCanCraft: false,
            aSortKey: Key(),
            bSortKey: Key(),
            aOriginalIndex: 0,
            bOriginalIndex: 1,
            mode: CraftingRecipeSortMode.GroupThenTier);

        Assert.GreaterThan(0, comparison);
    }

    public static void CraftingViewCraftableSortBeforeOriginalOrder()
    {
        int comparison = CraftingRecipeViewCore.CompareWithSortKey(
            aIsFavorite: false,
            bIsFavorite: false,
            aCanCraft: false,
            bCanCraft: true,
            aSortKey: Key(),
            bSortKey: Key(),
            aOriginalIndex: 0,
            bOriginalIndex: 9,
            mode: CraftingRecipeSortMode.GroupThenTier);

        Assert.GreaterThan(0, comparison);
    }

    public static void CraftingViewSortKeyFallsBackToOriginalOrder()
    {
        SortKey sameA = Key(name: "Same");
        SortKey sameB = Key(name: "Same");
        int comparison = CraftingRecipeViewCore.CompareWithSortKey(
            aIsFavorite: false,
            bIsFavorite: false,
            aCanCraft: true,
            bCanCraft: true,
            sameA,
            sameB,
            aOriginalIndex: 5,
            bOriginalIndex: 2,
            CraftingRecipeSortMode.TierThenGroup);

        Assert.GreaterThan(0, comparison);
    }

    public static void ClientStateNormalizeCreatesMissingRoots()
    {
        InventorySlotsClientState normalized = ClientStateCore.Normalize(null);

        Assert.True(normalized.Inventory != null, "inventory root should be created");
        InventorySlotsClientInventoryState inventory = normalized.Inventory!;
        Assert.True(inventory.EquipmentSlotsPanelPosition != null, "equipment panel position should be created");
        Assert.True(inventory.QuickSlotsPanelPosition != null, "quick slot panel position should be created");
        Assert.True(inventory.QuickSlotsHudPosition != null, "quick slot HUD position should be created");
        Assert.Equal(-80f, inventory.EquipmentSlotsPanelPosition!.X);
        Assert.Equal(0f, inventory.EquipmentSlotsPanelPosition.Y);
        Assert.Equal(-80f, inventory.QuickSlotsPanelPosition!.X);
        Assert.Equal(-552f, inventory.QuickSlotsPanelPosition.Y);
        Assert.Equal(64f, inventory.QuickSlotsHudPosition!.X);
        Assert.Equal(-520f, inventory.QuickSlotsHudPosition.Y);
        Assert.Equal(70f, inventory.QuickSlotsHudElementSpace);
        Assert.True(normalized.Players != null, "players root should be created");
        Assert.Equal(1, normalized.Version);
    }

    public static void ClientStateNormalizeTrimsPlayersAndLists()
    {
        InventorySlotsClientState state = new()
        {
            Inventory = new InventorySlotsClientInventoryState
            {
                EquipmentSlotsPanelPosition = new InventorySlotsClientPanelPosition(12f, 34f),
                QuickSlotsPanelPosition = new InventorySlotsClientPanelPosition(-56f, -78f),
                QuickSlotsHudPosition = new InventorySlotsClientPanelPosition(90f, -123f),
                QuickSlotsHudElementSpace = 42f
            },
            Players = new Dictionary<string, InventorySlotsClientPlayerState>
            {
                [" "] = new InventorySlotsClientPlayerState(),
                [" PlayerOne "] = new InventorySlotsClientPlayerState
                {
                    FavoriteSlots = null!,
                    CraftingFavorites = null!,
                    UpgradeFavorites = null!
                },
                ["NullValue"] = null!
            }
        };

        InventorySlotsClientState normalized = ClientStateCore.Normalize(state);

        Assert.True(normalized.Inventory != null, "inventory root should be recreated");
        InventorySlotsClientInventoryState inventory = normalized.Inventory!;
        Assert.Equal(12f, inventory.EquipmentSlotsPanelPosition!.X);
        Assert.Equal(34f, inventory.EquipmentSlotsPanelPosition.Y);
        Assert.Equal(-56f, inventory.QuickSlotsPanelPosition!.X);
        Assert.Equal(-78f, inventory.QuickSlotsPanelPosition.Y);
        Assert.Equal(90f, inventory.QuickSlotsHudPosition!.X);
        Assert.Equal(-123f, inventory.QuickSlotsHudPosition.Y);
        Assert.Equal(42f, inventory.QuickSlotsHudElementSpace);
        Assert.False(normalized.Players.ContainsKey(" "), "blank player ids should be removed");
        Assert.True(normalized.Players.ContainsKey("PlayerOne"), "player ids should be trimmed");
        Assert.True(normalized.Players.ContainsKey("playerone"), "players dictionary should stay case-insensitive");
        Assert.True(normalized.Players["PlayerOne"].FavoriteSlots != null, "favorite slot list should be recreated");
        Assert.True(normalized.Players["PlayerOne"].CraftingFavorites != null, "crafting favorites list should be recreated");
        Assert.True(normalized.Players["PlayerOne"].UpgradeFavorites != null, "upgrade favorites list should be recreated");
        Assert.True(normalized.Players["NullValue"].FavoriteSlots != null, "null player state should be recreated");
    }

    public static void CustomEquippedItemKeepsStableSlotIdentityDuringAutoAdopt()
    {
        Assert.False(
            InventorySlotSafetyCore.CanAutoAdoptGridSlot(
                isInventorySlotsCustomEquipped: true,
                markedSlotId: "oldslot",
                candidateSlotId: "newslot"),
            "custom-equipped items should not be adopted into a different slot id just because their grid position now maps there");

        Assert.True(
            InventorySlotSafetyCore.CanAutoAdoptGridSlot(
                isInventorySlotsCustomEquipped: true,
                markedSlotId: "OldSlot",
                candidateSlotId: "oldslot"),
            "same slot id should remain valid regardless of case");
    }

    public static void UnmarkedItemCanAutoAdoptMatchingGridSlot()
    {
        Assert.True(
            InventorySlotSafetyCore.CanAutoAdoptGridSlot(
                isInventorySlotsCustomEquipped: false,
                markedSlotId: null,
                candidateSlotId: "quick1"),
            "regular items can still be adopted by the grid slot they are placed into");
    }

    public static void KeepOnDeathQuickslotRowsPreferOriginalSlot()
    {
        for (int row = 1; row <= 3; row++)
        {
            InventorySlotSafetyCore.KeepOnDeathRestorePlan plan = InventorySlotSafetyCore.SelectKeepOnDeathRestorePlan(
                new InventorySlotSafetyCore.KeepOnDeathRestoreOptions(
                    wasSpecialSlot: true,
                    wasQuickSlot: true,
                    originalSlotAvailable: true,
                    emptyQuickSlotAvailable: true,
                    emptySameSpecialKindSlotAvailable: false,
                    originalCellAvailable: true,
                    freeRegularCellAvailable: true,
                    emptyNonQuickSpecialSlotAvailable: true));

            Assert.Equal(InventorySlotSafetyCore.KeepOnDeathRestorePlan.OriginalSlot, plan);
        }
    }

    public static void KeepOnDeathQuickslotFallsBackToEmptyQuickslotBeforeInventory()
    {
        InventorySlotSafetyCore.KeepOnDeathRestorePlan plan = InventorySlotSafetyCore.SelectKeepOnDeathRestorePlan(
            new InventorySlotSafetyCore.KeepOnDeathRestoreOptions(
                wasSpecialSlot: true,
                wasQuickSlot: true,
                originalSlotAvailable: false,
                emptyQuickSlotAvailable: true,
                emptySameSpecialKindSlotAvailable: false,
                originalCellAvailable: true,
                freeRegularCellAvailable: true,
                emptyNonQuickSpecialSlotAvailable: true));

        Assert.Equal(InventorySlotSafetyCore.KeepOnDeathRestorePlan.EmptyQuickSlot, plan);
    }

    public static void KeepOnDeathQuickslotUsesRegularCellOnlyAfterQuickslotsFail()
    {
        InventorySlotSafetyCore.KeepOnDeathRestorePlan plan = InventorySlotSafetyCore.SelectKeepOnDeathRestorePlan(
            new InventorySlotSafetyCore.KeepOnDeathRestoreOptions(
                wasSpecialSlot: true,
                wasQuickSlot: true,
                originalSlotAvailable: false,
                emptyQuickSlotAvailable: false,
                emptySameSpecialKindSlotAvailable: false,
                originalCellAvailable: false,
                freeRegularCellAvailable: true,
                emptyNonQuickSpecialSlotAvailable: true));

        Assert.Equal(InventorySlotSafetyCore.KeepOnDeathRestorePlan.FirstFreeRegularCell, plan);
    }

    public static void KeepOnDeathFullInventoryPreservesWithoutOverwriting()
    {
        InventorySlotSafetyCore.KeepOnDeathRestorePlan plan = InventorySlotSafetyCore.SelectKeepOnDeathRestorePlan(
            new InventorySlotSafetyCore.KeepOnDeathRestoreOptions(
                wasSpecialSlot: true,
                wasQuickSlot: true,
                originalSlotAvailable: false,
                emptyQuickSlotAvailable: false,
                emptySameSpecialKindSlotAvailable: false,
                originalCellAvailable: false,
                freeRegularCellAvailable: false,
                emptyNonQuickSpecialSlotAvailable: false));

        Assert.Equal(InventorySlotSafetyCore.KeepOnDeathRestorePlan.PreserveWithoutOverwriting, plan);
    }

    public static void KeepOnDeathRegularItemKeepsOriginalCellWhenAvailable()
    {
        InventorySlotSafetyCore.KeepOnDeathRestorePlan plan = InventorySlotSafetyCore.SelectKeepOnDeathRestorePlan(
            new InventorySlotSafetyCore.KeepOnDeathRestoreOptions(
                wasSpecialSlot: false,
                wasQuickSlot: false,
                originalSlotAvailable: false,
                emptyQuickSlotAvailable: false,
                emptySameSpecialKindSlotAvailable: false,
                originalCellAvailable: true,
                freeRegularCellAvailable: true,
                emptyNonQuickSpecialSlotAvailable: true));

        Assert.Equal(InventorySlotSafetyCore.KeepOnDeathRestorePlan.OriginalCell, plan);
    }

    public static void KeepOnDeathPreservationCellUsesOriginalWhenFree()
    {
        InventorySlotSafetyCore.GridCell selected = InventorySlotSafetyCore.SelectNonOverlappingPreservationCell(
            inventoryWidth: 8,
            inventoryHeight: 7,
            preferredCell: new InventorySlotSafetyCore.GridCell(2, 5),
            isOccupied: (_, _) => false);

        Assert.Equal(2, selected.X);
        Assert.Equal(5, selected.Y);
    }

    public static void KeepOnDeathPreservationCellUsesOverflowWhenOriginalOccupied()
    {
        HashSet<(int X, int Y)> occupied = new()
        {
            (2, 5),
            (0, 7),
            (1, 7)
        };

        InventorySlotSafetyCore.GridCell selected = InventorySlotSafetyCore.SelectNonOverlappingPreservationCell(
            inventoryWidth: 8,
            inventoryHeight: 7,
            preferredCell: new InventorySlotSafetyCore.GridCell(2, 5),
            isOccupied: (x, y) => occupied.Contains((x, y)));

        Assert.Equal(2, selected.X);
        Assert.Equal(7, selected.Y);
    }

    public static void InventoryFirstFreeCellSkipsDisallowedAndOccupiedCells()
    {
        HashSet<(int X, int Y)> occupied = new()
        {
            (1, 0),
            (2, 0)
        };

        bool found = InventorySlotSafetyCore.TrySelectFirstFreeCell(
            inventoryWidth: 4,
            rowCount: 2,
            isAllowed: (x, y) => !(x == 0 && y == 0),
            isOccupied: (x, y) => occupied.Contains((x, y)),
            out InventorySlotSafetyCore.GridCell selected);

        Assert.True(found, "first free cell should be found after disallowed and occupied cells are skipped");
        Assert.Equal(3, selected.X);
        Assert.Equal(0, selected.Y);
    }

    public static void InventoryFirstFreeCellReportsNoCellWhenFull()
    {
        bool found = InventorySlotSafetyCore.TrySelectFirstFreeCell(
            inventoryWidth: 2,
            rowCount: 2,
            isAllowed: (_, _) => true,
            isOccupied: (_, _) => true,
            out InventorySlotSafetyCore.GridCell selected);

        Assert.False(found, "full candidate range should report no available cell");
        Assert.Equal(-1, selected.X);
        Assert.Equal(-1, selected.Y);
    }

    public static void InventoryLoadPreservationTreatsQuickslotRowsAsTailCells()
    {
        for (int y = 9; y <= 11; y++)
        {
            Assert.True(
                InventorySlotSafetyCore.IsInventorySlotsTailCell(
                    inventoryWidth: 8,
                    fixedRegularRows: 9,
                    cell: new InventorySlotSafetyCore.GridCell(0, y)),
                $"quickslot/equipment tail row {y} must be preserved during load instead of being sent to top-first regular slots");
        }
    }

    public static void InventoryLoadPreservationRejectsHotbarCells()
    {
        Assert.False(
            InventorySlotSafetyCore.IsInventorySlotsTailCell(
                inventoryWidth: 8,
                fixedRegularRows: 9,
                cell: new InventorySlotSafetyCore.GridCell(0, 0)),
            "hotbar cells must remain regular inventory cells during load preservation");
    }

    public static void InventoryLoadPreservationKeepsFreeTailCell()
    {
        bool selected = InventorySlotSafetyCore.TrySelectLoadPreservationTailCell(
            inventoryWidth: 8,
            inventoryHeight: 12,
            fixedRegularRows: 9,
            requestedCell: new InventorySlotSafetyCore.GridCell(2, 10),
            isOccupied: (_, _) => false,
            out InventorySlotSafetyCore.GridCell cell);

        Assert.True(selected, "tail cell requests should be handled by the load preservation policy");
        Assert.Equal(2, cell.X);
        Assert.Equal(10, cell.Y);
    }

    public static void InventoryLoadPreservationOverflowsOccupiedTailCell()
    {
        HashSet<(int X, int Y)> occupied = new()
        {
            (2, 10),
            (0, 12),
            (1, 12)
        };

        bool selected = InventorySlotSafetyCore.TrySelectLoadPreservationTailCell(
            inventoryWidth: 8,
            inventoryHeight: 12,
            fixedRegularRows: 9,
            requestedCell: new InventorySlotSafetyCore.GridCell(2, 10),
            isOccupied: (x, y) => occupied.Contains((x, y)),
            out InventorySlotSafetyCore.GridCell cell);

        Assert.True(selected, "occupied tail cell requests should overflow instead of using regular inventory cells");
        Assert.Equal(2, cell.X);
        Assert.Equal(12, cell.Y);
    }

    public static void InventoryLoadPreservationNeverFallsBackIntoHotbar()
    {
        bool selected = InventorySlotSafetyCore.TrySelectLoadPreservationTailCell(
            inventoryWidth: 8,
            inventoryHeight: 12,
            fixedRegularRows: 9,
            requestedCell: new InventorySlotSafetyCore.GridCell(2, 10),
            isOccupied: (_, y) => y >= 10 && y < 56,
            out InventorySlotSafetyCore.GridCell cell);

        Assert.True(selected, "tail cell requests should be handled by the load preservation policy");
        Assert.True(cell.Y >= 12, "overflow fallback must stay outside regular inventory/hotbar rows");
    }

    public static void InventoryLoadPreservationIgnoresOutOfWidthTailRequests()
    {
        bool selected = InventorySlotSafetyCore.TrySelectLoadPreservationTailCell(
            inventoryWidth: 8,
            inventoryHeight: 12,
            fixedRegularRows: 9,
            requestedCell: new InventorySlotSafetyCore.GridCell(8, 10),
            isOccupied: (_, _) => false,
            out InventorySlotSafetyCore.GridCell cell);

        Assert.False(selected, "out-of-width cells should not be treated as InventorySlots tail cells");
        Assert.Equal(-1, cell.X);
        Assert.Equal(-1, cell.Y);
    }

    public static void InventoryPlacementPolicyKeepsContainersTopFirst()
    {
        HashSet<(int X, int Y)> occupied = new()
        {
            (0, 0),
            (1, 0),
            (0, 1)
        };

        bool found = InventorySlotSafetyCore.TrySelectFirstFreeCell(
            inventoryWidth: 3,
            rowCount: 2,
            isAllowed: (_, _) => true,
            isOccupied: (x, y) => occupied.Contains((x, y)),
            out InventorySlotSafetyCore.GridCell cell);

        Assert.True(found, "container placement should fill the first open top row before lower rows");
        Assert.Equal(2, cell.X);
        Assert.Equal(0, cell.Y);
    }

    public static void InventoryPlacementPolicySharesTopFirstEmptyCount()
    {
        HashSet<(int X, int Y)> occupied = new()
        {
            (0, 0),
            (1, 0),
            (0, 1),
            (1, 1)
        };

        bool found = InventorySlotSafetyCore.TrySelectFirstFreeCell(
            inventoryWidth: 2,
            rowCount: 2,
            isAllowed: (_, _) => true,
            isOccupied: (x, y) => occupied.Contains((x, y)),
            out InventorySlotSafetyCore.GridCell cell);
        int emptySlots = InventoryPlacementPolicyCore.CountTopFirstPolicyEmptyCells(
            inventoryWidth: 2,
            rowCount: 2,
            isAllowed: (_, _) => true,
            isOccupied: (x, y) => occupied.Contains((x, y)));

        Assert.False(found, "full container placement policy should report no available cell");
        Assert.Equal(-1, cell.X);
        Assert.Equal(-1, cell.Y);
        Assert.Equal(0, emptySlots);
    }

    public static void InventoryAutomaticPlacementPrefersRegularRowsBeforeHotbar()
    {
        bool found = InventoryPlacementPolicyCore.TrySelectRegularBeforeHotbarCell(
            inventoryWidth: 3,
            rowCount: 3,
            isAllowed: (_, _) => true,
            isOccupied: (_, _) => false,
            out InventorySlotSafetyCore.GridCell cell);

        Assert.True(found, "automatic placement should find an available regular inventory cell");
        Assert.Equal(0, cell.X);
        Assert.Equal(1, cell.Y);
    }

    public static void InventoryAutomaticPlacementFallsBackToHotbar()
    {
        bool found = InventoryPlacementPolicyCore.TrySelectRegularBeforeHotbarCell(
            inventoryWidth: 3,
            rowCount: 3,
            isAllowed: (_, _) => true,
            isOccupied: (x, y) => y > 0 || x == 0,
            out InventorySlotSafetyCore.GridCell cell);

        Assert.True(found, "automatic placement should use hotbar when every regular inventory cell is occupied");
        Assert.Equal(1, cell.X);
        Assert.Equal(0, cell.Y);
    }

    public static void InventoryLimitsAcceptExactRemainingAmount()
    {
        Assert.True(
            InventoryPlacementPolicyCore.CanAcceptInventoryLimit(currentAmount: 1, incomingAmount: 2, maxAmount: 3),
            "an addition that reaches the limit exactly should be allowed");
        Assert.True(
            InventoryPlacementPolicyCore.CanAcceptInventoryLimit(currentAmount: 3, incomingAmount: 0, maxAmount: 3),
            "a non-positive addition should not be blocked");
    }

    public static void InventoryLimitsRejectExcessAdditions()
    {
        Assert.False(
            InventoryPlacementPolicyCore.CanAcceptInventoryLimit(currentAmount: 2, incomingAmount: 2, maxAmount: 3),
            "an addition above the limit should be rejected");
        Assert.False(
            InventoryPlacementPolicyCore.CanAcceptInventoryLimit(currentAmount: 4, incomingAmount: 1, maxAmount: 3),
            "existing excess should be preserved without allowing more items");
        Assert.False(
            InventoryPlacementPolicyCore.CanAcceptInventoryLimit(currentAmount: 0, incomingAmount: 1, maxAmount: 0),
            "a zero limit should block new additions");
    }

    public static void ActionCellPolicyFavoritesIncludeQuickslots()
    {
        Assert.True(InventoryActionCellPolicyCore.CanFavoriteSlot(InventoryCellKind.RegularUnlocked), "regular cells should be favoriteable");
        Assert.True(InventoryActionCellPolicyCore.CanFavoriteSlot(InventoryCellKind.Hotbar), "hotbar cells should be favoriteable");
        Assert.True(InventoryActionCellPolicyCore.CanFavoriteSlot(InventoryCellKind.Quick), "quickslots should be favoriteable");

        Assert.False(InventoryActionCellPolicyCore.CanFavoriteSlot(InventoryCellKind.RegularLocked), "locked regular cells should not be favoriteable");
        Assert.False(InventoryActionCellPolicyCore.CanFavoriteSlot(InventoryCellKind.Equipment), "equipment slots should not be inventory favorites");
        Assert.False(InventoryActionCellPolicyCore.CanFavoriteSlot(InventoryCellKind.EquipmentLocked), "locked equipment slots should not be inventory favorites");
        Assert.False(InventoryActionCellPolicyCore.CanFavoriteSlot(InventoryCellKind.CustomEquipment), "custom equipment slots should not be inventory favorites");
        Assert.False(InventoryActionCellPolicyCore.CanFavoriteSlot(InventoryCellKind.QuickLocked), "locked quickslots should not be favoriteable");
        Assert.False(InventoryActionCellPolicyCore.CanFavoriteSlot(InventoryCellKind.Outside), "outside cells should not be favoriteable");
    }

    public static void ActionCellPolicyKeepsQuickslotsOutOfContainerActionSources()
    {
        Assert.True(InventoryActionCellPolicyCore.CanUseContainerActionSource(InventoryCellKind.RegularUnlocked, includeHotbar: false), "regular cells should be container action sources");
        Assert.False(InventoryActionCellPolicyCore.CanUseContainerActionSource(InventoryCellKind.Hotbar, includeHotbar: false), "hotbar should stay excluded unless explicitly included");
        Assert.True(InventoryActionCellPolicyCore.CanUseContainerActionSource(InventoryCellKind.Hotbar, includeHotbar: true), "hotbar should be opt-in for container action sources");

        Assert.False(InventoryActionCellPolicyCore.CanUseContainerActionSource(InventoryCellKind.Quick, includeHotbar: true), "quickslots should not be quickstack/store/sort sources");
        Assert.False(InventoryActionCellPolicyCore.CanUseContainerActionSource(InventoryCellKind.Equipment, includeHotbar: true), "equipment slots should not be container action sources");
        Assert.False(InventoryActionCellPolicyCore.CanUseContainerActionSource(InventoryCellKind.EquipmentLocked, includeHotbar: true), "locked equipment slots should not be container action sources");
        Assert.False(InventoryActionCellPolicyCore.CanUseContainerActionSource(InventoryCellKind.RegularLocked, includeHotbar: true), "locked regular cells should not be container action sources");
    }

    public static void ActionCellPolicyRestockTargetsIncludeHotbarAndQuickslots()
    {
        Assert.True(InventoryActionCellPolicyCore.CanUseFavoriteRestockTarget(InventoryCellKind.RegularUnlocked), "regular favorites should be restock targets");
        Assert.True(InventoryActionCellPolicyCore.CanUseFavoriteRestockTarget(InventoryCellKind.Hotbar), "hotbar favorites should be restock targets");
        Assert.True(InventoryActionCellPolicyCore.CanUseFavoriteRestockTarget(InventoryCellKind.Quick), "quickslot favorites should be restock targets");

        Assert.False(InventoryActionCellPolicyCore.CanUseFavoriteRestockTarget(InventoryCellKind.RegularLocked), "locked regular cells should not be restock targets");
        Assert.False(InventoryActionCellPolicyCore.CanUseFavoriteRestockTarget(InventoryCellKind.Equipment), "equipment slots should not be restock targets");
        Assert.False(InventoryActionCellPolicyCore.CanUseFavoriteRestockTarget(InventoryCellKind.EquipmentLocked), "locked equipment slots should not be restock targets");
        Assert.False(InventoryActionCellPolicyCore.CanUseFavoriteRestockTarget(InventoryCellKind.CustomEquipment), "custom equipment slots should not be restock targets");
        Assert.False(InventoryActionCellPolicyCore.CanUseFavoriteRestockTarget(InventoryCellKind.QuickLocked), "locked quickslots should not be restock targets");
        Assert.False(InventoryActionCellPolicyCore.CanUseFavoriteRestockTarget(InventoryCellKind.Outside), "outside cells should not be restock targets");
    }

    public static void ActionCellPolicyTrashAllowsRegularInventoryOnly()
    {
        Assert.True(InventoryActionCellPolicyCore.CanTrashSlot(InventoryCellKind.RegularUnlocked), "regular cells should be trashable");

        Assert.False(InventoryActionCellPolicyCore.CanTrashSlot(InventoryCellKind.Hotbar), "hotbar cells should not be trashable");
        Assert.False(InventoryActionCellPolicyCore.CanTrashSlot(InventoryCellKind.RegularLocked), "locked regular cells should not be trashable");
        Assert.False(InventoryActionCellPolicyCore.CanTrashSlot(InventoryCellKind.Equipment), "equipment slots should not be trashable");
        Assert.False(InventoryActionCellPolicyCore.CanTrashSlot(InventoryCellKind.EquipmentLocked), "locked equipment slots should not be trashable");
        Assert.False(InventoryActionCellPolicyCore.CanTrashSlot(InventoryCellKind.CustomEquipment), "custom equipment slots should not be trashable");
        Assert.False(InventoryActionCellPolicyCore.CanTrashSlot(InventoryCellKind.Quick), "quickslots should not be trashable");
        Assert.False(InventoryActionCellPolicyCore.CanTrashSlot(InventoryCellKind.QuickLocked), "locked quickslots should not be trashable");
        Assert.False(InventoryActionCellPolicyCore.CanTrashSlot(InventoryCellKind.Outside), "outside cells should not be trashable");
        Assert.False(InventoryActionCellPolicyCore.CanTrashSlot(InventoryCellKind.ExternalReserved), "external reserved cells should not be trashable");
    }

    public static void InventoryActionsActionCellPolicyCopyMirrorsInventorySlotsBehavior()
    {
        string[] slotsKindNames = Enum.GetNames(typeof(InventoryCellKind));
        string[] actionsKindNames = Enum.GetNames(typeof(InventoryActions.InventoryCellKind));
        Assert.Equal(string.Join(",", slotsKindNames), string.Join(",", actionsKindNames));

        foreach (string name in slotsKindNames)
        {
            InventoryCellKind slotsKind = Enum.Parse<InventoryCellKind>(name);
            InventoryActions.InventoryCellKind actionsKind = Enum.Parse<InventoryActions.InventoryCellKind>(name);

            Assert.Equal((int)slotsKind, (int)actionsKind);
            Assert.Equal(
                InventoryActionCellPolicyCore.CanFavoriteSlot(slotsKind),
                InventoryActions.InventoryActionCellPolicyCore.CanFavoriteSlot(actionsKind));
            Assert.Equal(
                InventoryActionCellPolicyCore.CanUseFavoriteRestockTarget(slotsKind),
                InventoryActions.InventoryActionCellPolicyCore.CanUseFavoriteRestockTarget(actionsKind));
            Assert.Equal(
                InventoryActionCellPolicyCore.CanTrashSlot(slotsKind),
                InventoryActions.InventoryActionCellPolicyCore.CanTrashSlot(actionsKind));
            Assert.Equal(
                InventoryActionCellPolicyCore.CanUseContainerActionSource(slotsKind, includeHotbar: false),
                InventoryActions.InventoryActionCellPolicyCore.CanUseContainerActionSource(actionsKind, includeHotbar: false));
            Assert.Equal(
                InventoryActionCellPolicyCore.CanUseContainerActionSource(slotsKind, includeHotbar: true),
                InventoryActions.InventoryActionCellPolicyCore.CanUseContainerActionSource(actionsKind, includeHotbar: true));
        }
    }

    public static void KeepOnDeathEquipmentPrefersRegularCellBeforeUnrelatedSpecialSlot()
    {
        InventorySlotSafetyCore.KeepOnDeathRestorePlan plan = InventorySlotSafetyCore.SelectKeepOnDeathRestorePlan(
            new InventorySlotSafetyCore.KeepOnDeathRestoreOptions(
                wasSpecialSlot: true,
                wasQuickSlot: false,
                originalSlotAvailable: false,
                emptyQuickSlotAvailable: false,
                emptySameSpecialKindSlotAvailable: false,
                originalCellAvailable: false,
                freeRegularCellAvailable: true,
                emptyNonQuickSpecialSlotAvailable: true));

        Assert.Equal(InventorySlotSafetyCore.KeepOnDeathRestorePlan.FirstFreeRegularCell, plan);
    }

    public static void KeepOnDeathQuickslotAvoidsUnrelatedSpecialSlotWhenPacked()
    {
        InventorySlotSafetyCore.KeepOnDeathRestorePlan plan = InventorySlotSafetyCore.SelectKeepOnDeathRestorePlan(
            new InventorySlotSafetyCore.KeepOnDeathRestoreOptions(
                wasSpecialSlot: true,
                wasQuickSlot: true,
                originalSlotAvailable: false,
                emptyQuickSlotAvailable: false,
                emptySameSpecialKindSlotAvailable: false,
                originalCellAvailable: false,
                freeRegularCellAvailable: false,
                emptyNonQuickSpecialSlotAvailable: true));

        Assert.Equal(InventorySlotSafetyCore.KeepOnDeathRestorePlan.PreserveWithoutOverwriting, plan);
    }

    public static void KeepOnDeathEquipmentPrefersSameKindSpecialSlot()
    {
        InventorySlotSafetyCore.KeepOnDeathRestorePlan plan = InventorySlotSafetyCore.SelectKeepOnDeathRestorePlan(
            new InventorySlotSafetyCore.KeepOnDeathRestoreOptions(
                wasSpecialSlot: true,
                wasQuickSlot: false,
                originalSlotAvailable: false,
                emptyQuickSlotAvailable: false,
                emptySameSpecialKindSlotAvailable: true,
                originalCellAvailable: false,
                freeRegularCellAvailable: true,
                emptyNonQuickSpecialSlotAvailable: true));

        Assert.Equal(InventorySlotSafetyCore.KeepOnDeathRestorePlan.EmptySameSpecialKindSlot, plan);
    }

    public static void UpgradeFavoriteReusesExistingItemId()
    {
        Dictionary<string, string> customData = new()
        {
            ["favorite"] = " existing-id "
        };
        int created = 0;

        string id = UpgradeFavoriteCore.GetOrCreateItemId(customData, "favorite", new HashSet<string>(), () =>
        {
            created++;
            return "new-id";
        });

        Assert.Equal("existing-id", id);
        Assert.Equal(0, created);
        Assert.Equal(" existing-id ", customData["favorite"]);
    }

    public static void UpgradeFavoriteCreatesUniqueItemId()
    {
        Dictionary<string, string> customData = new();
        HashSet<string> existing = new(StringComparer.OrdinalIgnoreCase) { "taken" };
        Queue<string> ids = new(new[] { " taken ", "", "fresh" });

        string id = UpgradeFavoriteCore.GetOrCreateItemId(customData, "favorite", existing, ids.Dequeue);

        Assert.Equal("fresh", id);
        Assert.Equal("fresh", customData["favorite"]);
    }

    public static void UpgradeFavoriteSetAndRemoveTrimsId()
    {
        Dictionary<string, string> customData = new();

        UpgradeFavoriteCore.SetItemId(customData, "favorite", " id-1 ");
        Assert.Equal("id-1", UpgradeFavoriteCore.GetItemId(customData, "favorite"));
        Assert.True(UpgradeFavoriteCore.RemoveItemId(customData, "favorite"), "remove should report removed key");
        Assert.Equal("", UpgradeFavoriteCore.GetItemId(customData, "favorite"));
    }

    public static void JewelcraftingNativeTooltipRefreshSkipsStableSameSignatureRows()
    {
        Assert.False(
            JewelcraftingTooltipCore.ShouldRefreshNativeTooltip(
                previousSignature: "same",
                nextSignature: "same",
                previousVisible: true,
                previousHadSocketRows: true),
            "stable pinned socket rows should not call the native tooltip API again for the same signature");
    }

    public static void JewelcraftingNativeTooltipRefreshRunsForChangedOrUnstableState()
    {
        Assert.True(
            JewelcraftingTooltipCore.ShouldRefreshNativeTooltip(
                previousSignature: "old",
                nextSignature: "new",
                previousVisible: true,
                previousHadSocketRows: true),
            "changed key/item/socket signature should call the native tooltip API");
        Assert.True(
            JewelcraftingTooltipCore.ShouldRefreshNativeTooltip(
                previousSignature: "same",
                nextSignature: "same",
                previousVisible: false,
                previousHadSocketRows: true),
            "hidden tooltip state should refresh before reuse");
        Assert.True(
            JewelcraftingTooltipCore.ShouldRefreshNativeTooltip(
                previousSignature: "same",
                nextSignature: "same",
                previousVisible: true,
                previousHadSocketRows: false),
            "interact-only or empty state should keep trying until socket rows are captured");
    }

    public static void JewelcraftingNativeTooltipSignatureTracksDetailKeys()
    {
        string baseSignature = JewelcraftingTooltipCore.BuildNativeTooltipUpdateSignature(
            showInteract: true,
            advancedPressed: false,
            prophecyPressed: false,
            localizationVersion: 1,
            equipmentSignature: "equipment",
            openSocketSignature: "sockets");
        string advancedSignature = JewelcraftingTooltipCore.BuildNativeTooltipUpdateSignature(
            showInteract: true,
            advancedPressed: true,
            prophecyPressed: false,
            localizationVersion: 1,
            equipmentSignature: "equipment",
            openSocketSignature: "sockets");
        string prophecySignature = JewelcraftingTooltipCore.BuildNativeTooltipUpdateSignature(
            showInteract: true,
            advancedPressed: false,
            prophecyPressed: true,
            localizationVersion: 1,
            equipmentSignature: "equipment",
            openSocketSignature: "sockets");

        Assert.NotEqual(baseSignature, advancedSignature);
        Assert.NotEqual(baseSignature, prophecySignature);
        Assert.NotEqual(advancedSignature, prophecySignature);
    }

    public static void SimpleTooltipOwnerIgnoresStaleHide()
    {
        object first = new();
        object second = new();
        SimpleTooltipOwnershipCore owner = new();

        owner.Show(first);
        owner.Show(second);

        Assert.False(owner.Hide(first), "previous owner should not hide the currently visible simple tooltip");
        Assert.True(owner.Visible, "tooltip should remain visible after stale hide");
        Assert.True(ReferenceEquals(second, owner.Owner), "current owner should remain second");
        Assert.True(owner.Hide(second), "current owner should be able to hide the tooltip");
        Assert.False(owner.Visible, "tooltip should be hidden after current owner hides it");
    }

    public static void HoverSourceVneiUsesOwnedRendererAndCraftingAlpha()
    {
        HoverTooltipSourceKind kind = HoverTooltipSourceCore.Classify(
            inventoryContainer: false,
            inventorySlotsCrafting: false,
            vneiCrafting: true);

        Assert.Equal(HoverTooltipSourceKind.VneiCrafting, kind);
        Assert.True(HoverTooltipSourceCore.UsesInventorySlotsOwnedHoverTooltip(kind), "VNEI crafting tooltips should use InventorySlots' owned hover renderer");
        Assert.True(HoverTooltipSourceCore.SuppressesVanillaHoverStart(kind), "VNEI crafting tooltips should skip vanilla hover-start rendering");
        Assert.True(HoverTooltipSourceCore.SuppressesVanillaLateUpdate(kind), "VNEI crafting tooltips should skip vanilla late-update rendering");
        Assert.True(HoverTooltipSourceCore.UsesCraftingHoverTooltipBackgroundAlpha(kind), "VNEI crafting tooltips should follow crafting hover alpha");
        Assert.True(HoverTooltipSourceCore.SuppressesEpicLootTooltipLayout(kind), "VNEI crafting tooltips should suppress EpicLoot tooltip layout artifacts");
    }

    public static void HoverSourceOwnedCraftingOnlySuppressesEpicLootLayout()
    {
        HoverTooltipSourceKind kind = HoverTooltipSourceCore.Classify(
            inventoryContainer: false,
            inventorySlotsCrafting: true,
            vneiCrafting: false);

        Assert.Equal(HoverTooltipSourceKind.InventorySlotsCrafting, kind);
        Assert.False(HoverTooltipSourceCore.UsesInventorySlotsOwnedHoverTooltip(kind), "owned crafting HUD tooltips should keep vanilla HUD rendering");
        Assert.False(HoverTooltipSourceCore.SuppressesVanillaHoverStart(kind), "owned crafting HUD tooltips should keep vanilla hover-start rendering");
        Assert.False(HoverTooltipSourceCore.SuppressesVanillaLateUpdate(kind), "owned crafting HUD tooltips should keep vanilla late-update rendering");
        Assert.True(HoverTooltipSourceCore.SuppressesEpicLootTooltipLayout(kind), "owned crafting tooltips should still suppress EpicLoot layout artifacts");
    }

    public static void TooltipSourceCachePrunesMatchingEntries()
    {
        TooltipSourceCacheCore<string, int> cache = new(maxEntries: 8, StringComparer.OrdinalIgnoreCase);
        cache.Set("keep", 1);
        cache.Set("drop", 2);

        int removed = cache.RemoveWhere((_, value) => value == 2);

        Assert.Equal(1, removed);
        Assert.True(cache.TryGet("KEEP", out int kept), "cache should honor its comparer");
        Assert.Equal(1, kept);
        Assert.False(cache.TryGet("drop", out _), "matching entry should be pruned");
    }

    public static void TooltipSourceCacheTrimsOldestEntries()
    {
        TooltipSourceCacheCore<string, int> cache = new(maxEntries: 2);
        cache.Set("a", 1);
        cache.Set("b", 2);
        cache.Set("c", 3);

        Assert.Equal(2, cache.Count);
        Assert.False(cache.TryGet("a", out _), "oldest entry should be trimmed when capacity is exceeded");
        Assert.True(cache.TryGet("b", out int b), "newer entry should remain");
        Assert.True(cache.TryGet("c", out int c), "newest entry should remain");
        Assert.Equal(2, b);
        Assert.Equal(3, c);
    }

    public static void ContainerTransferSumsMovedAmountsAndCallbacks()
    {
        FakeTransferContainer anchor = new("anchor", valid: true, moved: 2);
        FakeTransferContainer invalid = new("invalid", valid: false, moved: 7);
        FakeTransferContainer empty = new("empty", valid: true, moved: 0);
        FakeTransferContainer nearby = new("nearby", valid: true, moved: 3);
        List<string> visited = new();
        List<string> changed = new();
        int anyMovedCount = 0;

        int moved = ContainerTransferCore.Run(
            new FakeTransferContainer?[] { anchor, null, invalid, empty, nearby },
            container => container.Valid,
            container =>
            {
                visited.Add(container.Name);
                return container.Moved;
            },
            (container, amount) => changed.Add($"{container.Name}:{amount}"),
            () => anyMovedCount++);

        Assert.Equal(5, moved);
        Assert.Equal("anchor,empty,nearby", string.Join(",", visited));
        Assert.Equal("anchor:2,nearby:3", string.Join(",", changed));
        Assert.Equal(1, anyMovedCount);
    }

    public static void ContainerTransferStaysQuietWhenNothingMoves()
    {
        int changedCount = 0;
        int anyMovedCount = 0;

        int moved = ContainerTransferCore.Run(
            new[] { new FakeTransferContainer("empty", valid: true, moved: 0) },
            container => container.Valid,
            container => container.Moved,
            (_, _) => changedCount++,
            () => anyMovedCount++);

        Assert.Equal(0, moved);
        Assert.Equal(0, changedCount);
        Assert.Equal(0, anyMovedCount);
    }

    public static void InventoryActionsContainerTransferCoreCopyMirrorsInventorySlotsBehavior()
    {
        FakeTransferContainer?[] containers =
        {
            new("anchor", valid: true, moved: 2),
            null,
            new("invalid", valid: false, moved: 7),
            new("empty", valid: true, moved: 0),
            new("nearby", valid: true, moved: 3)
        };
        List<string> slotsChanged = new();
        List<string> actionsChanged = new();
        int slotsAnyMoved = 0;
        int actionsAnyMoved = 0;

        int slotsMoved = ContainerTransferCore.Run(
            containers,
            container => container.Valid,
            container => container.Moved,
            (container, amount) => slotsChanged.Add($"{container.Name}:{amount}"),
            () => slotsAnyMoved++);
        int actionsMoved = InventoryActions.ContainerTransferCore.Run(
            containers,
            container => container.Valid,
            container => container.Moved,
            (container, amount) => actionsChanged.Add($"{container.Name}:{amount}"),
            () => actionsAnyMoved++);

        Assert.Equal(slotsMoved, actionsMoved);
        Assert.Equal(string.Join(",", slotsChanged), string.Join(",", actionsChanged));
        Assert.Equal(slotsAnyMoved, actionsAnyMoved);
    }

    public static void InventoryActionsContainerActionCoreCopyMirrorsInventorySlotsBehavior()
    {
        (int Before, int After, int Requested, bool MoveSucceeded, bool UseFallback)[] movedCases =
        {
            (10, 3, 7, true, false),
            (10, 3, 0, true, true),
            (10, 10, 4, true, true),
            (10, 10, 4, true, false),
            (10, 12, 4, true, true),
            (10, 10, 4, false, true),
            (10, 3, 4, false, true),
            (0, 0, 5, true, true),
            (10, 10, -4, true, true)
        };

        foreach ((int before, int after, int requested, bool moveSucceeded, bool useFallback) in movedCases)
        {
            Assert.Equal(
                ContainerActionCore.CountMovedAmount(before, after, requested, moveSucceeded, useFallback),
                InventoryActions.ContainerActionCore.CountMovedAmount(before, after, requested, moveSucceeded, useFallback));
        }

        (int LeftX, int LeftY, int RightX, int RightY)[] gridCases =
        {
            (0, 0, 1, 0),
            (1, 0, 0, 1),
            (4, 3, 4, 3),
            (7, 2, 0, 2)
        };

        foreach ((int leftX, int leftY, int rightX, int rightY) in gridCases)
        {
            Assert.Equal(
                ContainerActionCore.CompareGridOrder(leftX, leftY, rightX, rightY),
                InventoryActions.ContainerActionCore.CompareGridOrder(leftX, leftY, rightX, rightY));
        }
    }

    public static void RestockTargetLimitsParseConfigEntries()
    {
        Dictionary<string, int> limits = RestockTargetLimitCore.Parse("Stone: 10, Coins = 500; BadEntry; Wood: -5 # comment");

        Assert.Equal(3, limits.Count);
        Assert.Equal(10, limits["stone"]);
        Assert.Equal(500, limits["coins"]);
        Assert.Equal(0, limits["wood"]);
    }

    public static void RestockTargetLimitResolvesAliasesAndClampsToMaxStack()
    {
        Dictionary<string, int> limits = RestockTargetLimitCore.Parse("Stone: 250, Coins: 500");
        int stoneLimit = RestockTargetLimitCore.ResolveTargetStackLimit(
            limits,
            new[] { "$item_stone", "Stone" },
            itemMaxStack: 99);
        int coinLimit = RestockTargetLimitCore.ResolveTargetStackLimit(
            limits,
            new[] { "Coins" },
            itemMaxStack: 999);
        int fallbackLimit = RestockTargetLimitCore.ResolveTargetStackLimit(
            limits,
            new[] { "Wood" },
            itemMaxStack: 50);

        Assert.Equal(99, stoneLimit);
        Assert.Equal(500, coinLimit);
        Assert.Equal(50, fallbackLimit);
    }

    public static void RestockTargetLimitResolvesLocalizedItemNames()
    {
        Dictionary<string, int> limits = RestockTargetLimitCore.Parse("수지: 20");
        int localizedLimit = RestockTargetLimitCore.ResolveTargetStackLimit(
            limits,
            new[] { "$item_resin", "resin", "수지" },
            itemMaxStack: 50);

        Assert.Equal(20, localizedLimit);
    }

    public static void InventoryActionsRestockTargetLimitCopyMirrorsInventorySlotsBehavior()
    {
        string raw = "Stone: 250, $item_Coins = 500; Resin(Clone): 20; BadEntry; Wood: -5 # comment";
        Dictionary<string, int> slotsLimits = RestockTargetLimitCore.Parse(raw);
        Dictionary<string, int> actionsLimits = InventoryActions.RestockTargetLimitCore.Parse(raw);

        Assert.Equal(SerializeLimits(slotsLimits), SerializeLimits(actionsLimits));
        Assert.Equal(
            RestockTargetLimitCore.ResolveTargetStackLimit(slotsLimits, new[] { "$item_stone", "Stone" }, itemMaxStack: 99),
            InventoryActions.RestockTargetLimitCore.ResolveTargetStackLimit(actionsLimits, new[] { "$item_stone", "Stone" }, itemMaxStack: 99));
        Assert.Equal(
            RestockTargetLimitCore.ResolveTargetStackLimit(slotsLimits, new[] { "$item_coins", "Coins" }, itemMaxStack: 999),
            InventoryActions.RestockTargetLimitCore.ResolveTargetStackLimit(actionsLimits, new[] { "$item_coins", "Coins" }, itemMaxStack: 999));
        Assert.Equal(
            RestockTargetLimitCore.ResolveTargetStackLimit(slotsLimits, new[] { "$item_resin", "Resin(Clone)", "Resin" }, itemMaxStack: 50),
            InventoryActions.RestockTargetLimitCore.ResolveTargetStackLimit(actionsLimits, new[] { "$item_resin", "Resin(Clone)", "Resin" }, itemMaxStack: 50));
        Assert.Equal(
            RestockTargetLimitCore.ResolveTargetStackLimit(slotsLimits, new[] { "Wood" }, itemMaxStack: 50),
            InventoryActions.RestockTargetLimitCore.ResolveTargetStackLimit(actionsLimits, new[] { "Wood" }, itemMaxStack: 50));
        Assert.Equal(
            RestockTargetLimitCore.ResolveTargetStackLimit(slotsLimits, new[] { "Unknown" }, itemMaxStack: 50),
            InventoryActions.RestockTargetLimitCore.ResolveTargetStackLimit(actionsLimits, new[] { "Unknown" }, itemMaxStack: 50));
        Assert.Equal(
            InventorySlotsConfigCore.StripLocalizationToken("$item_resin"),
            InventoryActions.RestockTargetLimitCore.StripLocalizationToken("$item_resin"));
    }

    public static void IntentionalInventoryActionsSourceCopiesStaySynchronized()
    {
        string repositoryRoot = FindRepositoryRoot();
        (string Main, string Copy)[] copiedSources =
        {
            ("InventoryCellKind.cs", "InventoryActions/InventoryCellKind.cs"),
            ("InventoryActionCellPolicyCore.cs", "InventoryActions/InventoryActionCellPolicyCore.cs"),
            ("ContainerActionCore.cs", "InventoryActions/ContainerActionCore.cs"),
            ("ContainerTransferCore.cs", "InventoryActions/ContainerTransferCore.cs"),
            ("RestockTargetLimitConfigDrawer.cs", "InventoryActions/RestockTargetLimitConfigDrawer.cs")
        };

        foreach ((string main, string copy) in copiedSources)
        {
            string mainSource = NormalizeCopiedSource(Path.Combine(repositoryRoot, main));
            string copySource = NormalizeCopiedSource(Path.Combine(repositoryRoot, copy));
            if (!string.Equals(mainSource, copySource, StringComparison.Ordinal))
            {
                throw new InvalidOperationException($"InventoryActions source copy '{copy}' has drifted from '{main}'.");
            }
        }
    }

    public static void ReleaseMetadataVersionsStaySynchronized()
    {
        string repositoryRoot = FindRepositoryRoot();
        AssertReleaseVersionContract(
            repositoryRoot,
            "PluginMetadata.cs",
            "Thunderstore/manifest.json",
            "Thunderstore/CHANGELOG.md");
        AssertReleaseVersionContract(
            repositoryRoot,
            "InventoryActions/Plugin.cs",
            "InventoryActions/Thunderstore/manifest.json",
            "InventoryActions/Thunderstore/CHANGELOG.md");
    }

    private static void AssertReleaseVersionContract(
        string repositoryRoot,
        string sourcePath,
        string manifestPath,
        string changelogPath)
    {
        string sourceVersion = ReadVersion(
            Path.Combine(repositoryRoot, sourcePath),
            @"ModVersion\s*=\s*""(?<version>[^""]+)""",
            "source");
        string manifestVersion = ReadVersion(
            Path.Combine(repositoryRoot, manifestPath),
            @"""version_number""\s*:\s*""(?<version>[^""]+)""",
            "manifest");
        string changelogVersion = ReadVersion(
            Path.Combine(repositoryRoot, changelogPath),
            @"(?m)^##\s+(?<version>\d+\.\d+\.\d+)\s*$",
            "changelog");

        Assert.Equal(sourceVersion, manifestVersion);
        Assert.Equal(sourceVersion, changelogVersion);
    }

    private static string ReadVersion(string path, string pattern, string source)
    {
        System.Text.RegularExpressions.Match match =
            System.Text.RegularExpressions.Regex.Match(File.ReadAllText(path), pattern);
        if (!match.Success)
        {
            throw new InvalidOperationException($"Could not find a version in {source} file '{path}'.");
        }

        return match.Groups["version"].Value;
    }

    private static string FindRepositoryRoot()
    {
        foreach (string startPath in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
        {
            DirectoryInfo? directory = new(startPath);
            while (directory != null)
            {
                if (File.Exists(Path.Combine(directory.FullName, "InventorySlots.csproj")) &&
                    Directory.Exists(Path.Combine(directory.FullName, "InventoryActions")))
                {
                    return directory.FullName;
                }

                directory = directory.Parent;
            }
        }

        throw new InvalidOperationException("Could not locate the InventorySlots repository root.");
    }

    private static string NormalizeCopiedSource(string path) =>
        File.ReadAllText(path)
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace("namespace InventoryActions;", "namespace InventorySlots;", StringComparison.Ordinal)
            .Replace("InventoryActionsPlugin", "InventorySlotsPlugin", StringComparison.Ordinal)
            .TrimEnd();

    private static string SerializeLimits(Dictionary<string, int> limits) =>
        string.Join(",", limits.OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase).Select(pair => $"{pair.Key}:{pair.Value}"));

    private static CraftingFrameFastPathStamp FrameStamp(CraftingTabAdapterKind adapterKind, int selectedRecipeIndex = 2) =>
        new(
            guiId: 1,
            craftingPanelId: 2,
            gridId: 3,
            adapterKind: adapterKind,
            selectedRecipeIndex: selectedRecipeIndex,
            recipeViewSignature: "view",
            recipePage: 1,
            gridDimension: 4,
            availabilityVersion: 7,
            hasNoCraftCost: false,
            pinnedTooltipGridSignature: "pin",
            recipeVariantVersion: 8,
            hoveredRecipeIndex: 2,
            screenWidth: 1920,
            screenHeight: 1080);

    private sealed class FakeTransferContainer
    {
        public FakeTransferContainer(string name, bool valid, int moved)
        {
            Name = name;
            Valid = valid;
            Moved = moved;
        }

        public string Name { get; }
        public bool Valid { get; }
        public int Moved { get; }
    }

    private static SortKey Key(
        int resourceTier = 0,
        int groupRank = 0,
        int bigGroupRank = 0,
        string setKey = "",
        int slot = 99,
        string name = "") =>
        new(resourceTier, groupRank, bigGroupRank, setKey, slot, name);
}

internal static class TestRunner
{
    public static void Run(params (string Name, Action Body)[] tests)
    {
        int failures = 0;
        foreach ((string name, Action body) in tests)
        {
            try
            {
                body();
                Console.WriteLine($"PASS {name}");
            }
            catch (Exception ex)
            {
                failures++;
                Console.WriteLine($"FAIL {name}");
                Console.WriteLine(ex.Message);
            }
        }

        if (failures > 0)
        {
            throw new InvalidOperationException($"{failures} test(s) failed.");
        }

        Console.WriteLine($"{tests.Length} test(s) passed.");
    }
}

internal static class Assert
{
    public static void True(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    public static void False(bool condition, string message)
    {
        if (condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    public static void Equal<T>(T expected, T actual)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
        {
            throw new InvalidOperationException($"Expected '{expected}', got '{actual}'.");
        }
    }

    public static void NotEqual<T>(T left, T right)
    {
        if (EqualityComparer<T>.Default.Equals(left, right))
        {
            throw new InvalidOperationException($"Expected '{left}' and '{right}' to differ.");
        }
    }

    public static void LessThan(int threshold, int actual)
    {
        if (actual >= threshold)
        {
            throw new InvalidOperationException($"Expected value less than {threshold}, got {actual}.");
        }
    }

    public static void GreaterThan(int threshold, int actual)
    {
        if (actual <= threshold)
        {
            throw new InvalidOperationException($"Expected value greater than {threshold}, got {actual}.");
        }
    }

    public static void Contains(IEnumerable<string> values, string expected)
    {
        if (!values.Contains(expected, StringComparer.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Expected collection to contain '{expected}'.");
        }
    }
}
