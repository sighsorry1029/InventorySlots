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
    ("Default resource map parses with expected tier order", Tests.DefaultResourceMapParsesWithExpectedTierOrder),
    ("Resource tier map normalizes tokens and keeps first tier", Tests.ResourceTierMapNormalizesTokensAndKeepsFirstTier),
    ("Malformed resource maps are rejected", Tests.MalformedResourceMapsAreRejected),
    ("Legacy resource map schema is rejected", Tests.LegacyResourceMapSchemaIsRejected),
    ("Resource map rejects multiple documents", Tests.ResourceMapRejectsMultipleDocuments),
    ("Resource map rejects duplicate tier names", Tests.ResourceMapRejectsDuplicateTierNames),
    ("Main YAML rejects legacy inline resource map", Tests.MainYamlRejectsLegacyInlineResourceMap),
    ("Built-in group section names normalize to ids", Tests.BuiltInGroupSectionNamesNormalizeToIds),
    ("Dominant food stat tie breaks are stable", Tests.DominantFoodStatTieBreaksAreStable),
    ("Dominant food stat ignores empty foods", Tests.DominantFoodStatIgnoresEmptyFoods),
    ("Slot food fork rejects appended-tooltip materials", Tests.SlotFoodForkRejectsAppendedTooltipMaterials),
    ("Slot food fork accepts direct consumables", Tests.SlotFoodForkAcceptsDirectConsumables),
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
    ("Jewelcrafting socket actions refresh and guard the selected recipe pair", Tests.JewelcraftingSocketActionsRefreshAndGuardSelectedRecipePair),
    ("Client state normalize creates missing roots", Tests.ClientStateNormalizeCreatesMissingRoots),
    ("Client state normalize trims players and lists", Tests.ClientStateNormalizeTrimsPlayersAndLists),
    ("Custom equipped item keeps stable slot identity during auto-adopt", Tests.CustomEquippedItemKeepsStableSlotIdentityDuringAutoAdopt),
    ("Unmarked item can auto-adopt matching grid slot", Tests.UnmarkedItemCanAutoAdoptMatchingGridSlot),
    ("CircletExtended custom slot ownership fails closed", Tests.CircletExtendedCustomSlotOwnershipFailsClosed),
    ("HipLantern custom slot ownership fails closed", Tests.HipLanternCustomSlotOwnershipFailsClosed),
    ("Custom equipment visual is registered before attachment", Tests.CustomEquipmentVisualIsRegisteredBeforeAttachment),
    ("CircletExtended lifecycle guards stay ordered", Tests.CircletExtendedLifecycleGuardsStayOrdered),
    ("HipLantern lifecycle and native ownership stay wired", Tests.HipLanternLifecycleAndNativeOwnershipStayWired),
    ("Quickslot reset policy clears highest rows first", Tests.QuickslotResetPolicyClearsHighestRowsFirst),
    ("Quickslot reset policy stops at first blocked row", Tests.QuickslotResetPolicyStopsAtFirstBlockedRow),
    ("Quickslot reset policy respects naturally unlocked rows", Tests.QuickslotResetPolicyRespectsNaturallyUnlockedRows),
    ("Quickslot reset policy skips rows that cannot be reduced", Tests.QuickslotResetPolicySkipsRowsThatCannotBeReduced),
    ("Quickslot load preservation does not authorize reset", Tests.QuickslotLoadPreservationDoesNotAuthorizeReset),
    ("Progressive inventory row recovery waits for item lookup", Tests.ProgressiveInventoryRowRecoveryWaitsForItemLookup),
    ("Newly unlocked inventory rows reveal once", Tests.NewlyUnlockedInventoryRowsRevealOnce),
    ("Keep-on-death preparation and restoration retain every unconfirmed item", Tests.KeepOnDeathPreparationAndRestorationRetainEveryUnconfirmedItem),
    ("Keep-on-death finalizer directly preserves every remaining item", Tests.KeepOnDeathFinalizerDirectlyPreservesEveryRemainingItem),
    ("Slot auto-equip suppression remains balanced when scopes nest", Tests.SlotAutoEquipSuppressionRemainsBalancedWhenScopesNest),
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
    ("Quick-slot item automatic placement prefers hotbar", Tests.QuickSlotItemAutomaticPlacementPrefersHotbar),
    ("Quick-slot item automatic placement falls back to regular rows", Tests.QuickSlotItemAutomaticPlacementFallsBackToRegularRows),
    ("Inventory limits accept the exact remaining amount", Tests.InventoryLimitsAcceptExactRemainingAmount),
    ("Inventory limits reject excess and additions to existing excess", Tests.InventoryLimitsRejectExcessAdditions),
    ("Action cell policy favorites include quickslots", Tests.ActionCellPolicyFavoritesIncludeQuickslots),
    ("Action cell policy keeps quickslots out of container action sources", Tests.ActionCellPolicyKeepsQuickslotsOutOfContainerActionSources),
    ("Action cell policy restock targets include hotbar and quickslots", Tests.ActionCellPolicyRestockTargetsIncludeHotbarAndQuickslots),
    ("Action cell policy trash allows regular inventory only", Tests.ActionCellPolicyTrashAllowsRegularInventoryOnly),
    ("Inventory trash rejects quest items through final confirmation", Tests.InventoryTrashRejectsQuestItemsThroughFinalConfirmation),
    ("InventoryActions uses its fixed vanilla cell policy directly", Tests.InventoryActionsUsesFixedVanillaCellPolicyDirectly),
    ("InventoryActions tooltip guard owns only its buttons", Tests.InventoryActionsTooltipGuardOwnsOnlyItsButtons),
    ("Keep-on-death equipment prefers regular cell before unrelated special slot", Tests.KeepOnDeathEquipmentPrefersRegularCellBeforeUnrelatedSpecialSlot),
    ("Keep-on-death quickslot avoids unrelated special slot when packed", Tests.KeepOnDeathQuickslotAvoidsUnrelatedSpecialSlotWhenPacked),
    ("Keep-on-death equipment prefers same-kind special slot", Tests.KeepOnDeathEquipmentPrefersSameKindSpecialSlot),
    ("Upgrade favorite reuses existing item id", Tests.UpgradeFavoriteReusesExistingItemId),
    ("Upgrade favorite creates unique item id", Tests.UpgradeFavoriteCreatesUniqueItemId),
    ("Upgrade favorite set and remove trims id", Tests.UpgradeFavoriteSetAndRemoveTrimsId),
    ("Jewelcrafting native tooltip refresh skips stable same signature rows", Tests.JewelcraftingNativeTooltipRefreshSkipsStableSameSignatureRows),
    ("Jewelcrafting native tooltip refresh bounds stable rowless retries", Tests.JewelcraftingNativeTooltipRefreshBoundsStableRowlessRetries),
    ("Jewelcrafting native tooltip refresh runs for changed or unstable state", Tests.JewelcraftingNativeTooltipRefreshRunsForChangedOrUnstableState),
    ("Jewelcrafting native tooltip signature tracks detail keys", Tests.JewelcraftingNativeTooltipSignatureTracksDetailKeys),
    ("Simple tooltip owner ignores stale hide", Tests.SimpleTooltipOwnerIgnoresStaleHide),
    ("Hover source VNEI uses owned renderer and crafting alpha", Tests.HoverSourceVneiUsesOwnedRendererAndCraftingAlpha),
    ("Hover source owned crafting only suppresses EpicLoot layout", Tests.HoverSourceOwnedCraftingOnlySuppressesEpicLootLayout),
    ("EpicLoot public API lifecycle uses exact v1 contracts", Tests.EpicLootPublicApiLifecycleUsesExactV1Contracts),
    ("EpicLoot sacrifice and effect refresh fail safely", Tests.EpicLootSacrificeAndEffectRefreshFailSafely),
    ("EpicLoot query and HUD fallback stay authoritative", Tests.EpicLootQueryAndHudFallbackStayAuthoritative),
    ("Crafting hover wheel follows recipe-cell ownership", Tests.CraftingHoverWheelFollowsRecipeCellOwnership),
    ("Crafting tooltip wheel blocks only underlying crafting scroll rects", Tests.CraftingTooltipWheelBlocksOnlyUnderlyingCraftingScrollRects),
    ("Tooltip source cache prunes matching entries", Tests.TooltipSourceCachePrunesMatchingEntries),
    ("Tooltip source cache trims oldest entries", Tests.TooltipSourceCacheTrimsOldestEntries),
    ("Container transfer sums moved amounts and callbacks", Tests.ContainerTransferSumsMovedAmountsAndCallbacks),
    ("Container transfer stays quiet when nothing moves", Tests.ContainerTransferStaysQuietWhenNothingMoves),
    ("Direct container actions use positional ownership-safe moves", Tests.DirectContainerActionsUsePositionalOwnershipSafeMoves),
    ("Container action success FX stays bounded and once per action", Tests.ContainerActionSuccessFxStaysBoundedAndOncePerAction),
    ("Container action success FX uses transient Everybody RPC", Tests.ContainerActionSuccessFxUsesTransientEverybodyRpc),
    ("Container action success FX stays local guarded and self cleaning", Tests.ContainerActionSuccessFxStaysLocalGuardedAndSelfCleaning),
    ("InventoryActions success FX stays bounded and once per action", Tests.InventoryActionsContainerActionSuccessFxStaysBoundedAndOncePerAction),
    ("InventoryActions success FX uses transient Everybody RPC", Tests.InventoryActionsContainerActionSuccessFxUsesTransientEverybodyRpc),
    ("InventoryActions success FX stays local guarded and self cleaning", Tests.InventoryActionsContainerActionSuccessFxStaysLocalGuardedAndSelfCleaning),
    ("Multi-user item snapshot ignores custom data order", Tests.MultiUserItemSnapshotIgnoresCustomDataOrder),
    ("Multi-user item snapshot rejects socket data changes", Tests.MultiUserItemSnapshotRejectsSocketDataChanges),
    ("BeingSpoiled signed clocks remain stack compatible", Tests.BeingSpoiledSignedClocksRemainStackCompatible),
    ("Stack metadata policy preserves other custom-data identity", Tests.StackMetadataPolicyPreservesOtherCustomDataIdentity),
    ("BeingSpoiled signed clock merge preserves destination state", Tests.BeingSpoiledSignedClockMergePreservesDestinationState),
    ("BeingSpoiled signed clock validates missing and malformed values", Tests.BeingSpoiledSignedClockValidatesMissingAndMalformedValues),
    ("BeingSpoiled partial merge leaves source clock unchanged", Tests.BeingSpoiledPartialMergeLeavesSourceClockUnchanged),
    ("BeingSpoiled registration replaces only the fallback", Tests.BeingSpoiledRegistrationReplacesOnlyTheFallback),
    ("Multi-user item snapshot rejects insufficient stack", Tests.MultiUserItemSnapshotRejectsInsufficientStack),
    ("Multi-user item snapshot rejects identity field changes", Tests.MultiUserItemSnapshotRejectsIdentityFieldChanges),
    ("Multi-user transfer requires exact pre-mutation stack state", Tests.MultiUserTransferRequiresExactPreMutationStackState),
    ("Multi-user request preparation keeps escrow behind a published pending", Tests.MultiUserRequestPreparationKeepsEscrowBehindPublishedPending),
    ("InventoryActions current-container transfers notify only after movement", Tests.InventoryActionsCurrentContainerTransfersNotifyOnlyAfterMovement),
    ("InventoryActions container action core copy mirrors InventorySlots behavior", Tests.InventoryActionsContainerActionCoreCopyMirrorsInventorySlotsBehavior),
    ("Area ownership handoff executes a matching grant once", Tests.AreaOwnershipHandoffExecutesMatchingGrantOnce),
    ("Area ownership handoff ignores mismatched responses", Tests.AreaOwnershipHandoffIgnoresMismatchedResponses),
    ("Area ownership handoff rejects late responses", Tests.AreaOwnershipHandoffRejectsLateResponses),
    ("Area ownership handoff duplicate grant does not extend deadline", Tests.AreaOwnershipHandoffDuplicateGrantDoesNotExtendDeadline),
    ("Area ownership handoff fails closed on owner and token races", Tests.AreaOwnershipHandoffFailsClosedOnOwnerAndTokenRaces),
    ("Area ownership handoff fails closed on unload", Tests.AreaOwnershipHandoffFailsClosedOnUnload),
    ("Area ownership handoff enforces serial execution preconditions", Tests.AreaOwnershipHandoffEnforcesSerialExecutionPreconditions),
    ("InventoryActions area cleanup commits state before callbacks", Tests.InventoryActionsAreaCleanupCommitsStateBeforeCallbacks),
    ("Restock target limits parse config entries", Tests.RestockTargetLimitsParseConfigEntries),
    ("Restock target limit editor normalization preserves runtime meaning", Tests.RestockTargetLimitEditorNormalizationPreservesRuntimeMeaning),
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
        List<YamlSlot> hipLanternSlots = root.Slots
            .Where(slot => string.Equals(slot.Id, "hiplantern.lantern", StringComparison.OrdinalIgnoreCase))
            .ToList();
        Assert.Equal(1, hipLanternSlots.Count);
        YamlSlot hipLanternSlot = hipLanternSlots[0];
        Assert.Contains(hipLanternSlot.Items, "HipLantern");
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
        Dictionary<string, int> tiers = InventorySlotsConfigCore.ParseResourceMapYaml("""
        Meadows:
          - Wood
          - $item_Stone
        BlackForest:
          - Wood
          - Bronze(Clone)
        """);

        Assert.Equal(0, tiers["wood"]);
        Assert.Equal(0, tiers["stone"]);
        Assert.Equal(1, tiers["bronze"]);
    }

    public static void DefaultResourceMapParsesWithExpectedTierOrder()
    {
        Dictionary<string, int> tiers =
            InventorySlotsConfigCore.ParseResourceMapYaml(InventorySlotsPlugin.DefaultResourceMapYaml);

        Assert.Equal(0, tiers["wood"]);
        Assert.Equal(1, tiers["bronze"]);
        Assert.Equal(2, tiers["iron"]);
        Assert.Equal(3, tiers["chitin"]);
        Assert.Equal(4, tiers["silver"]);
        Assert.Equal(5, tiers["blackmetal"]);
        Assert.Equal(6, tiers["eitr"]);
        Assert.Equal(7, tiers["flametalnew"]);
        Assert.Equal(0, tiers["resin"]);
        Assert.Equal(0, tiers["bonefragments"]);
    }

    public static void MalformedResourceMapsAreRejected()
    {
        string[] malformedMaps =
        [
            "Meadows: Wood",
            "Meadows:\n  - prefab: Wood",
            "Meadows:\n  - \"\""
        ];

        foreach (string yaml in malformedMaps)
        {
            bool parsed = InventorySlotsConfigCore.TryParseResourceMapYaml(yaml, out _, out Exception? error);

            Assert.False(parsed, "malformed ResourceMap.yml should not parse");
            Assert.True(error != null, "malformed ResourceMap.yml should report an error");
        }
    }

    public static void LegacyResourceMapSchemaIsRejected()
    {
        string yaml = """
        - biome: Meadows
          materials:
            - Wood
        """;

        Assert.False(
            InventorySlotsConfigCore.TryParseResourceMapYaml(yaml, out _, out Exception? error),
            "the old biome/materials sequence schema should not parse");
        Assert.True(error != null, "the old ResourceMap schema should report an error");
    }

    public static void ResourceMapRejectsMultipleDocuments()
    {
        string yaml = """
        Meadows:
          - Wood
        ---
        BlackForest:
          - Bronze
        """;

        Assert.False(
            InventorySlotsConfigCore.TryParseResourceMapYaml(yaml, out _, out Exception? error),
            "ResourceMap.yml should contain exactly one YAML document");
        Assert.True(error != null, "multiple YAML documents should report an error");
    }

    public static void ResourceMapRejectsDuplicateTierNames()
    {
        string yaml = """
        Meadows:
          - Wood
        meadows:
          - Stone
        """;

        Assert.False(
            InventorySlotsConfigCore.TryParseResourceMapYaml(yaml, out _, out Exception? error),
            "tier names duplicated with different casing should not parse");
        Assert.True(error != null, "duplicate tier names should report an error");
    }

    public static void MainYamlRejectsLegacyInlineResourceMap()
    {
        string yaml = """
        Slots: []
        resourceMap:
          Meadows:
            - Wood
        """;

        Assert.False(
            InventorySlotsConfigCore.TryParseYaml(yaml, out _, out Exception? error),
            "resourceMap is no longer valid inside InventorySlots.yml");
        Assert.True(error != null, "legacy inline resourceMap should report an error");
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

    public static void SlotFoodForkRejectsAppendedTooltipMaterials()
    {
        Assert.False(
            FoodStatCore.TryGetSlotForkDominant(
                isConsumable: false,
                health: 35f,
                stamina: 10f,
                eitr: 0f,
                out FoodStat stat),
            "a material must not receive a fork even when an appended tooltip supplies food-like stats");
        Assert.Equal(FoodStat.None, stat);

        string repositoryRoot = FindRepositoryRoot();
        string classifierSource = File.ReadAllText(Path.Combine(repositoryRoot, "ItemClassifier.cs"));
        int methodStart = classifierSource.IndexOf(
            "internal static bool TryGetSlotForkDominantFoodStat",
            StringComparison.Ordinal);
        int methodEnd = methodStart >= 0
            ? classifierSource.IndexOf(
                "private static string GetAttackAnimation",
                methodStart,
                StringComparison.Ordinal)
            : -1;
        Assert.True(methodStart >= 0 && methodEnd > methodStart, "slot fork classifier should remain discoverable");
        string slotForkMethod = classifierSource[methodStart..methodEnd];
        Assert.True(
            slotForkMethod.Contains("shared.m_itemType == ItemType.Consumable", StringComparison.Ordinal),
            "slot fork classification must require the current item to be consumable");
        Assert.False(
            slotForkMethod.Contains("m_appendToolTip", StringComparison.Ordinal) ||
            slotForkMethod.Contains("GetFoodSharedData", StringComparison.Ordinal),
            "slot fork classification must not follow appended tooltip food data");
    }

    public static void SlotFoodForkAcceptsDirectConsumables()
    {
        Assert.True(
            FoodStatCore.TryGetSlotForkDominant(
                isConsumable: true,
                health: 20f,
                stamina: 65f,
                eitr: 10f,
                out FoodStat stat),
            "a direct consumable with food stats should receive a fork");
        Assert.Equal(FoodStat.Stamina, stat);

        Assert.False(
            FoodStatCore.TryGetSlotForkDominant(
                isConsumable: true,
                health: 0f,
                stamina: 0f,
                eitr: 0f,
                out FoodStat emptyStat),
            "a consumable without direct food stats should not receive a fork");
        Assert.Equal(FoodStat.None, emptyStat);
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
        CraftingGroupRailStamp baseline = new(1, 2, 10f, -20f, "food", 3, "available", "food,melee");
        CraftingGroupRailStamp same = new(1, 2, 10.0001f, -20.0001f, "food", 3, "available", "food,melee");
        CraftingGroupRailStamp changedGroup = new(1, 2, 10.0001f, -20.0001f, "melee", 3, "available", "food,melee");

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

    public static void JewelcraftingSocketActionsRefreshAndGuardSelectedRecipePair()
    {
        string repositoryRoot = FindRepositoryRoot();
        string viewSource = File.ReadAllText(Path.Combine(repositoryRoot, "CraftingRecipeView.cs"));
        string listUpdate = ReadSourceSection(
            viewSource,
            "internal static void OnCraftingRecipeListUpdated",
            "private static bool TryRefreshSelectedJewelcraftingSocketRecipePair");
        string refresh = ReadSourceSection(
            viewSource,
            "private static bool TryRefreshSelectedJewelcraftingSocketRecipePair",
            "private static bool TryFindLatestJewelcraftingSocketRecipePair");
        string latestPair = ReadSourceSection(
            viewSource,
            "private static bool TryFindLatestJewelcraftingSocketRecipePair",
            "private static string GetCraftingRecipeListChangeSignature");

        int refreshCall = listUpdate.IndexOf(
            "TryRefreshSelectedJewelcraftingSocketRecipePair(gui);",
            StringComparison.Ordinal);
        int signatureRead = listUpdate.IndexOf(
            "GetCraftingRecipeListChangeSignature(gui)",
            StringComparison.Ordinal);
        int signatureStore = listUpdate.IndexOf(
            "TryStoreRecipeListChangeSignature(signature)",
            StringComparison.Ordinal);
        Assert.True(
            refreshCall >= 0 && signatureRead > refreshCall && signatureStore > signatureRead,
            "Jewelcrafting's rebuilt pair must replace the stale selection before cache signatures and UI state are read");
        int latestPairCall = refresh.IndexOf("TryFindLatestJewelcraftingSocketRecipePair(", StringComparison.Ordinal);
        int pairAssignment = refresh.IndexOf("gui.m_selectedRecipe = pair;", StringComparison.Ordinal);
        Assert.True(
            refresh.Contains("IsJewelcraftingSocketTabActive(gui)", StringComparison.Ordinal) &&
            latestPairCall >= 0 && pairAssignment > latestPairCall,
            "only a fresh Jewelcrafting socket pair may replace the selected recipe");
        int recipeMatch = latestPair.IndexOf("ReferenceEquals(candidate.Recipe, recipe)", StringComparison.Ordinal);
        int itemMatch = latestPair.IndexOf("ReferenceEquals(candidate.ItemData, item)", StringComparison.Ordinal);
        int candidateAssignment = latestPair.IndexOf("pair = candidate;", StringComparison.Ordinal);
        Assert.True(
            latestPair.Contains("foreach (InventoryGui.RecipeDataPair candidate in gui.m_availableRecipes)", StringComparison.Ordinal) &&
            recipeMatch >= 0 && itemMatch > recipeMatch && candidateAssignment > itemMatch,
            "the latest pair must match both the exact Recipe and ItemData references before its CanCraft state is trusted");

        string actionSource = File.ReadAllText(Path.Combine(repositoryRoot, "CraftingRecipeActions.cs"));
        string guard = ReadSourceSection(
            actionSource,
            "internal static bool CanStartCraftingAction",
            "internal static bool CanCompleteCraftingAction");
        string completionGuard = ReadSourceSection(
            actionSource,
            "internal static bool CanCompleteCraftingAction",
            "private static bool CanAffordJewelcraftingSocketAttempt");
        int guardRefresh = guard.IndexOf("TryRefreshSelectedJewelcraftingSocketRecipePair(gui)", StringComparison.Ordinal);
        int canAttempt = guard.IndexOf("CanAttemptJewelcraftingSocket(gui.m_selectedRecipe)", StringComparison.Ordinal);
        Assert.True(
            guard.Contains("if (!IsJewelcraftingSocketTabActive(gui))", StringComparison.Ordinal) &&
            guard.Contains("return true;", StringComparison.Ordinal) &&
            guard.Contains("return TryRefreshSelectedJewelcraftingSocketRecipePair(gui)", StringComparison.Ordinal) &&
            guardRefresh >= 0 && canAttempt > guardRefresh,
            "normal crafting must remain unchanged while socket attempts fail closed against the latest pair");
        int completionRefresh = completionGuard.IndexOf("TryFindLatestJewelcraftingSocketRecipePair(", StringComparison.Ordinal);
        int completionAttempt = completionGuard.IndexOf("CanAttemptJewelcraftingSocket(pair)", StringComparison.Ordinal);
        Assert.True(
            completionGuard.Contains("if (!IsJewelcraftingSocketTabActive(gui))", StringComparison.Ordinal) &&
            completionGuard.Contains("gui.m_craftRecipe", StringComparison.Ordinal) &&
            completionGuard.Contains("gui.m_craftUpgradeItem", StringComparison.Ordinal) &&
            completionGuard.Contains("return TryFindLatestJewelcraftingSocketRecipePair(", StringComparison.Ordinal) &&
            completionRefresh >= 0 && completionAttempt > completionRefresh,
            "the delayed socket mutation must fail closed against the exact recipe and item captured when crafting started");

        string patches = File.ReadAllText(Path.Combine(repositoryRoot, "CraftingPatches.cs"));
        string startPatch = ReadSourceSection(
            patches,
            "[HarmonyPatch(typeof(InventoryGui), \"OnCraftPressed\")]",
            "internal static class InventoryGuiCraftingQueueCancelPatch");
        int actionGuard = startPatch.IndexOf("CanStartCraftingAction(__instance)", StringComparison.Ordinal);
        int noticeStart = startPatch.IndexOf("BeginCraftingInventoryLimitNotice()", StringComparison.Ordinal);
        int prepareQueue = startPatch.IndexOf("PrepareCraftingQueue(__instance)", StringComparison.Ordinal);
        Assert.True(
            startPatch.Contains("[HarmonyPriority(Priority.First)]", StringComparison.Ordinal) &&
            startPatch.Contains("[HarmonyBefore(new[] { \"org.bepinex.plugins.jewelcrafting\" })]", StringComparison.Ordinal) &&
            startPatch.Contains("private static bool Prefix", StringComparison.Ordinal) &&
            actionGuard >= 0 && noticeStart > actionGuard && prepareQueue > noticeStart &&
            startPatch.Contains("return false;", StringComparison.Ordinal),
            "the latest socket cap must block OnCraftPressed before Jewelcrafting or crafting queue side effects run");

        int completionPatchStart = patches.IndexOf(
            "[HarmonyPatch(typeof(InventoryGui), \"DoCrafting\")]",
            StringComparison.Ordinal);
        Assert.True(completionPatchStart >= 0, "DoCrafting must retain a final socket-cap guard");
        string completionPatch = patches[completionPatchStart..];
        int completionActionGuard = completionPatch.IndexOf("CanCompleteCraftingAction(__instance)", StringComparison.Ordinal);
        int stateStarted = completionPatch.IndexOf("__state = true;", StringComparison.Ordinal);
        int completionNoticeStart = completionPatch.IndexOf("BeginCraftingInventoryLimitNotice()", StringComparison.Ordinal);
        int captureFavorite = completionPatch.IndexOf("CaptureUpgradeFavoriteBeforeCrafting(__instance)", StringComparison.Ordinal);
        Assert.True(
            completionPatch.Contains("[HarmonyPriority(Priority.First)]", StringComparison.Ordinal) &&
            completionPatch.Contains("[HarmonyBefore(new[] { \"org.bepinex.plugins.jewelcrafting\" })]", StringComparison.Ordinal) &&
            completionPatch.Contains("private static bool Prefix(InventoryGui __instance, out bool __state)", StringComparison.Ordinal) &&
            completionActionGuard >= 0 && stateStarted > completionActionGuard &&
            completionNoticeStart > stateStarted && captureFavorite > completionNoticeStart &&
            completionPatch.Contains("if (!__state)", StringComparison.Ordinal) &&
            completionPatch.Contains("if (__state)", StringComparison.Ordinal),
            "DoCrafting must block Jewelcrafting before mutation and skip favorite/notice cleanup when no side effects started");
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

    public static void CircletExtendedCustomSlotOwnershipFailsClosed()
    {
        Assert.False(
            InventorySlotSafetyCore.ShouldDelegateCircletOwnership(
                pluginActive: true,
                compatReady: false,
                putOnTopEnabled: true,
                isCircletPrefab: true,
                isCircletCustomType: true),
            "an unknown CircletExtended API must not receive equipment ownership");
        Assert.False(
            InventorySlotSafetyCore.ShouldDelegateCircletOwnership(
                pluginActive: true,
                compatReady: true,
                putOnTopEnabled: false,
                isCircletPrefab: true,
                isCircletCustomType: true),
            "a disabled put-on-top option must not receive custom-slot ownership");
        Assert.False(
            InventorySlotSafetyCore.ShouldDelegateCircletOwnership(
                pluginActive: true,
                compatReady: true,
                putOnTopEnabled: true,
                isCircletPrefab: true,
                isCircletCustomType: false),
            "an upgrade-gated quality-one Circlet must stay on the built-in helmet path");
        Assert.True(
            InventorySlotSafetyCore.ShouldDelegateCircletOwnership(
                pluginActive: true,
                compatReady: true,
                putOnTopEnabled: true,
                isCircletPrefab: true,
                isCircletCustomType: true),
            "an upgraded Circlet should delegate ownership when the API is ready");

        Assert.True(
            InventorySlotSafetyCore.CanUseCustomCircletSlot(
                pluginActive: false,
                isCircletPrefab: true,
                delegatesOwnership: false,
                helmetCompatible: false),
            "InventorySlots should retain its standalone Circlet slot behavior when CircletExtended is absent");
        Assert.False(
            InventorySlotSafetyCore.CanUseCustomCircletSlot(
                pluginActive: true,
                isCircletPrefab: true,
                delegatesOwnership: false,
                helmetCompatible: true),
            "a detected CircletExtended with an unavailable owner API must fail closed");
        Assert.True(
            InventorySlotSafetyCore.CanUseCustomCircletSlot(
                pluginActive: true,
                isCircletPrefab: true,
                delegatesOwnership: true,
                helmetCompatible: true),
            "a compatible upgraded Circlet should use the InventorySlots custom slot with delegated ownership");
        Assert.False(
            InventorySlotSafetyCore.CanUseCustomCircletSlot(
                pluginActive: true,
                isCircletPrefab: true,
                delegatesOwnership: true,
                helmetCompatible: false),
            "InventorySlots must respect CircletExtended's helmet compatibility rule");
        Assert.True(
            InventorySlotSafetyCore.CanUseCustomCircletSlot(
                pluginActive: true,
                isCircletPrefab: false,
                delegatesOwnership: false,
                helmetCompatible: false),
            "CircletExtended failures must not block unrelated custom equipment");
    }

    public static void HipLanternCustomSlotOwnershipFailsClosed()
    {
        Assert.False(
            InventorySlotSafetyCore.ShouldDelegateHipLanternOwnership(
                pluginActive: false,
                compatReady: true,
                useUtilitySlot: false,
                isHipLanternPrefab: true,
                isHipLanternItem: true),
            "HipLantern ownership cannot be delegated when the plugin is absent");
        Assert.False(
            InventorySlotSafetyCore.ShouldDelegateHipLanternOwnership(
                pluginActive: true,
                compatReady: false,
                useUtilitySlot: false,
                isHipLanternPrefab: true,
                isHipLanternItem: true),
            "an unknown HipLantern API must not receive equipment ownership");
        Assert.False(
            InventorySlotSafetyCore.ShouldDelegateHipLanternOwnership(
                pluginActive: true,
                compatReady: true,
                useUtilitySlot: true,
                isHipLanternPrefab: true,
                isHipLanternItem: true),
            "HipLantern utility mode must stay on the native utility-slot path");
        Assert.False(
            InventorySlotSafetyCore.ShouldDelegateHipLanternOwnership(
                pluginActive: true,
                compatReady: true,
                useUtilitySlot: false,
                isHipLanternPrefab: false,
                isHipLanternItem: true),
            "an unrelated prefab must not receive HipLantern ownership");
        Assert.False(
            InventorySlotSafetyCore.ShouldDelegateHipLanternOwnership(
                pluginActive: true,
                compatReady: true,
                useUtilitySlot: false,
                isHipLanternPrefab: true,
                isHipLanternItem: false),
            "a prefab that HipLantern no longer recognizes must fail closed");
        Assert.True(
            InventorySlotSafetyCore.ShouldDelegateHipLanternOwnership(
                pluginActive: true,
                compatReady: true,
                useUtilitySlot: false,
                isHipLanternPrefab: true,
                isHipLanternItem: true),
            "HipLantern custom mode should delegate native ownership when the API is ready");

        Assert.True(
            InventorySlotSafetyCore.CanUseCustomHipLanternSlot(
                pluginActive: false,
                isHipLanternPrefab: true,
                delegatesOwnership: false),
            "HipLantern absence must not block an explicitly configured standalone item slot");
        Assert.False(
            InventorySlotSafetyCore.CanUseCustomHipLanternSlot(
                pluginActive: true,
                isHipLanternPrefab: true,
                delegatesOwnership: false),
            "a detected HipLantern with unavailable or utility-mode ownership must fail closed");
        Assert.True(
            InventorySlotSafetyCore.CanUseCustomHipLanternSlot(
                pluginActive: true,
                isHipLanternPrefab: true,
                delegatesOwnership: true),
            "a compatible HipLantern should use its conditional InventorySlots slot");
        Assert.True(
            InventorySlotSafetyCore.CanUseCustomHipLanternSlot(
                pluginActive: true,
                isHipLanternPrefab: false,
                delegatesOwnership: false),
            "HipLantern compatibility failures must not block unrelated custom equipment");
    }

    public static void CustomEquipmentVisualIsRegisteredBeforeAttachment()
    {
        string source = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "CustomEquipmentVisualController.cs"));
        string applySource = ReadSourceSection(
            source,
            "private static void ApplyCustomEquipmentVisualStates",
            "internal static void ClearCustomEquipmentVisuals()");
        int reentryShortCircuit = applySource.IndexOf("existing.Matches(visEquipment, state.PrefabName, state.Variant)", StringComparison.Ordinal);
        int registration = applySource.IndexOf("EquipmentVisuals.Visuals[key] = visual;", StringComparison.Ordinal);
        int attachment = applySource.IndexOf("TryInitializeCustomEquipmentVisual(visEquipment, state, visual)", StringComparison.Ordinal);

        Assert.True(reentryShortCircuit >= 0, "matching registered visuals should short-circuit nested updates");
        Assert.True(registration >= 0, "custom visual placeholder registration should be present");
        Assert.True(attachment >= 0, "custom visual attachment initialization should be present");
        Assert.True(reentryShortCircuit < attachment, "the matching placeholder check must run before attachment");
        Assert.True(registration < attachment, "the placeholder must be visible before AttachItem can re-enter visual updates");
    }

    public static void CircletExtendedLifecycleGuardsStayOrdered()
    {
        string repositoryRoot = FindRepositoryRoot();
        string routingSource = ReadSourceSection(
            File.ReadAllText(Path.Combine(repositoryRoot, "SlotEquipController.cs")),
            "internal static bool TryRouteHumanoidEquipToDedicatedSlot",
            "private static bool CanRouteEquipToDedicatedSlot");
        Assert.True(routingSource.Contains("TryEquipIntoDedicatedSlot(player, inventory, item, slot!)", StringComparison.Ordinal),
            "Humanoid equip routing must use the guarded dedicated-slot transaction");

        string dedicatedEquipSource = ReadSourceSection(
            File.ReadAllText(Path.Combine(repositoryRoot, "SlotEquipController.cs")),
            "private static bool TryEquipIntoDedicatedSlot",
            "private static bool TryPlaceQuickItemIntoSlot");
        int clearCurrent = dedicatedEquipSource.IndexOf("ClearCircletExtendedEquippedState(player, item)", StringComparison.Ordinal);
        int routeItem = dedicatedEquipSource.IndexOf("TryEquipIntoSlot(player, inventory, item, slot)", StringComparison.Ordinal);
        int restoreCurrent = dedicatedEquipSource.IndexOf("RestoreCircletExtendedEquippedState(player, item)", StringComparison.Ordinal);

        Assert.True(clearCurrent >= 0 && routeItem >= 0 && clearCurrent < routeItem,
            "an existing CircletExtended current item must be cleared before any dedicated-slot route can re-unequip it");
        Assert.True(restoreCurrent > routeItem,
            "a failed routed equip must restore the prior CircletExtended current item");

        string validationSource = ReadSourceSection(
            File.ReadAllText(Path.Combine(repositoryRoot, "InventoryIntegrityValidation.cs")),
            "private static bool ClearMissingCustomEquipment",
            "private static bool TryReleaseItemToRegularInventory");
        Assert.True(validationSource.Contains("CanUseCircletExtendedCustomSlot(player, item, slot)", StringComparison.Ordinal),
            "full validation must evict Circlets that CircletExtended can no longer own");
        string validationEntrySource = ReadSourceSection(
            File.ReadAllText(Path.Combine(repositoryRoot, "InventoryIntegrityValidation.cs")),
            "private static void ValidateAndProjectInventory",
            "private static bool TryGetCanonicalEquippedSlot");
        Assert.True(validationEntrySource.Contains("ReconcileCircletExtendedLegacyHelmetState(player, inventory)", StringComparison.Ordinal),
            "full validation must clear legacy CircletExtended vanilla-helmet ownership");

        string deathKeepSource = ReadSourceSection(
            File.ReadAllText(Path.Combine(repositoryRoot, "DeathKeep.cs")),
            "private static bool CanUseKeepOnDeathSpecialSlot",
            "private static bool ShouldKeepOnDeath");
        Assert.True(deathKeepSource.Contains("CanUseCircletExtendedCustomSlot(player, item, slot)", StringComparison.Ordinal),
            "keep-on-death fallback must not bypass CircletExtended slot eligibility");

        string placementSource = ReadSourceSection(
            File.ReadAllText(Path.Combine(repositoryRoot, "InventoryPlacementCore.cs")),
            "private static bool CanUseSpecialSlot",
            "private static bool CanUseEmptySpecialSlot");
        Assert.True(placementSource.Contains("CanUseCircletExtendedCustomSlot(player, item, slot)", StringComparison.Ordinal),
            "normal special-slot placement must enforce CircletExtended slot eligibility");

        string equipSource = ReadSourceSection(
            File.ReadAllText(Path.Combine(repositoryRoot, "SlotEquipController.cs")),
            "internal static bool TryEquipIntoSlot",
            "private sealed class SlotEquipItemSnapshot");
        int clearVanillaReference = equipSource.IndexOf("ClearVanillaEquipmentReferences((Humanoid)player, item)", StringComparison.Ordinal);
        int markCustomSlot = equipSource.IndexOf("MarkItemSlot(player, item, slot)", StringComparison.Ordinal);
        Assert.True(clearVanillaReference >= 0 && markCustomSlot > clearVanillaReference,
            "custom equipment must clear stale vanilla equipment ownership before marking the dedicated slot");

        string restoreSource = ReadSourceSection(
            File.ReadAllText(Path.Combine(repositoryRoot, "SlotEquipController.cs")),
            "private static bool RestoreSlotEquipmentState",
            "private static ItemData? FindCustomEquippedItemForSlot");
        int clearRestoredVanillaReference = restoreSource.IndexOf("ClearVanillaEquipmentReferences((Humanoid)player, item)", StringComparison.Ordinal);
        int synchronizeRestoredCirclet = restoreSource.IndexOf("SynchronizeCircletExtendedEquippedState(player, item)", StringComparison.Ordinal);
        Assert.True(clearRestoredVanillaReference >= 0 && synchronizeRestoredCirclet > clearRestoredVanillaReference,
            "restored custom equipment must clear stale vanilla ownership before synchronizing CircletExtended");

        string compatSource = ReadSourceSection(
            File.ReadAllText(Path.Combine(repositoryRoot, "CircletExtendedCompatAdapter.cs")),
            "private static bool CanUseCircletExtendedCustomSlot",
            "private static bool SynchronizeCircletExtendedEquippedState");
        Assert.True(compatSource.Contains("TryIsCircletCustomType(helmet", StringComparison.Ordinal),
            "a different legacy Circlet in the vanilla helmet reference must block custom Circlet routing");
    }

    public static void HipLanternLifecycleAndNativeOwnershipStayWired()
    {
        string repositoryRoot = FindRepositoryRoot();
        string dedicatedEquipSource = ReadSourceSection(
            File.ReadAllText(Path.Combine(repositoryRoot, "SlotEquipController.cs")),
            "private static bool TryEquipIntoDedicatedSlot",
            "private static bool TryPlaceQuickItemIntoSlot");
        int clearCurrent = dedicatedEquipSource.IndexOf("ClearHipLanternEquippedState(player, item)", StringComparison.Ordinal);
        int routeItem = dedicatedEquipSource.IndexOf("TryEquipIntoSlot(player, inventory, item, slot)", StringComparison.Ordinal);
        int restoreCurrent = dedicatedEquipSource.IndexOf("RestoreHipLanternEquippedState(player, item)", StringComparison.Ordinal);
        Assert.True(clearCurrent >= 0 && routeItem >= 0 && clearCurrent < routeItem,
            "the native HipLantern item must be cleared before its EquipItem postfix observes a routed custom-slot marker");
        Assert.True(restoreCurrent > routeItem,
            "a failed routed HipLantern equip must restore the previous native state");

        string equipTransactionSource = ReadSourceSection(
            File.ReadAllText(Path.Combine(repositoryRoot, "SlotEquipController.cs")),
            "internal static bool TryEquipIntoSlot",
            "private sealed class SlotEquipItemSnapshot");
        int capturePreviousNative = equipTransactionSource.IndexOf("CaptureHipLanternEquippedState(player)", StringComparison.Ordinal);
        int rollbackItems = equipTransactionSource.IndexOf("RestoreSlotEquipMutationSnapshots(player, inventory, itemSnapshots, equipmentSnapshot)", StringComparison.Ordinal);
        int restorePreviousNative = equipTransactionSource.IndexOf("RestoreHipLanternEquippedState(player, hipLanternStateSnapshot)", StringComparison.Ordinal);
        Assert.True(capturePreviousNative >= 0 && rollbackItems > capturePreviousNative && restorePreviousNative > rollbackItems,
            "a failed slot replacement must restore both item snapshots and the previously equipped native HipLantern");

        string restoreSource = ReadSourceSection(
            File.ReadAllText(Path.Combine(repositoryRoot, "SlotEquipController.cs")),
            "private static bool RestoreSlotEquipmentState",
            "private static ItemData? FindCustomEquippedItemForSlot");
        int syncRestoredNative = restoreSource.IndexOf("changed |= OnCustomEquipmentCompatEquipped(player, item)", StringComparison.Ordinal);
        int setupRestoredEquipment = restoreSource.IndexOf("((Humanoid)player).SetupEquipment()", StringComparison.Ordinal);
        Assert.True(syncRestoredNative >= 0 && setupRestoredEquipment > syncRestoredNative,
            "a restored marker must synchronize native HipLantern state and refresh equipment even when no marker changed");

        string compatHooksSource = File.ReadAllText(Path.Combine(repositoryRoot, "BackpackCompat.cs"));
        Assert.True(compatHooksSource.Contains("OnHipLanternCustomEquipmentEquipped(player, item)", StringComparison.Ordinal),
            "direct and restored custom equips must synchronize HipLantern's native state");
        Assert.True(compatHooksSource.Contains("ClearHipLanternEquippedState(player, item)", StringComparison.Ordinal),
            "custom unequip paths must clear HipLantern's native state");

        string placementSource = ReadSourceSection(
            File.ReadAllText(Path.Combine(repositoryRoot, "InventoryPlacementCore.cs")),
            "private static bool CanUseSpecialSlot",
            "private static bool CanUseEmptySpecialSlot");
        Assert.True(placementSource.Contains("CanUseHipLanternCustomSlot(item, slot)", StringComparison.Ordinal),
            "normal placement must reject HipLantern custom slots in utility mode or after API drift");
        string validationSource = ReadSourceSection(
            File.ReadAllText(Path.Combine(repositoryRoot, "InventoryIntegrityValidation.cs")),
            "private static bool ClearMissingCustomEquipment",
            "private static bool TryReleaseItemToRegularInventory");
        Assert.True(validationSource.Contains("CanUseHipLanternCustomSlot(item, slot)", StringComparison.Ordinal),
            "full validation must evict HipLantern items that can no longer use a custom slot");
        string deathKeepSource = ReadSourceSection(
            File.ReadAllText(Path.Combine(repositoryRoot, "DeathKeep.cs")),
            "private static bool CanUseKeepOnDeathSpecialSlot",
            "private static bool ShouldKeepOnDeath");
        Assert.True(deathKeepSource.Contains("CanUseHipLanternCustomSlot(item, slot)", StringComparison.Ordinal),
            "keep-on-death fallback must not bypass HipLantern slot eligibility");

        string visualSource = ReadSourceSection(
            File.ReadAllText(Path.Combine(repositoryRoot, "CustomEquipmentVisualController.cs")),
            "private static bool ShouldAttachCustomEquipmentVisual",
            "private static string GetCustomEquipmentVisualKey");
        Assert.True(visualSource.Contains("ShouldSuppressInventorySlotsHipLanternVisual(item)", StringComparison.Ordinal),
            "HipLantern must remain the single owner of its visual");
        string projectionSource = File.ReadAllText(Path.Combine(repositoryRoot, "EquipmentProjection.cs"));
        Assert.True(projectionSource.Contains("ShouldDelegateHipLanternWeight(player, item)", StringComparison.Ordinal),
            "HipLantern's native weight projection must not be counted twice");
        Assert.True(projectionSource.Contains("ShouldDelegateHipLanternDurability(player, item)", StringComparison.Ordinal),
            "HipLantern's fuel and heat durability updates must not run twice");

        string yamlSource = File.ReadAllText(Path.Combine(repositoryRoot, "YamlConfiguration.cs"));
        int conditionalHipSlot = yamlSource.IndexOf("TryAddHipLanternCompatSlot(slot, id)", StringComparison.Ordinal);
        int genericCustomSlot = conditionalHipSlot >= 0
            ? yamlSource.IndexOf("GetSlotItems(slot)", conditionalHipSlot, StringComparison.Ordinal)
            : -1;
        Assert.True(conditionalHipSlot >= 0 && genericCustomSlot > conditionalHipSlot,
            "the reserved HipLantern YAML entry must be consumed before generic custom-slot creation");
        string adapterSlotSource = ReadSourceSection(
            File.ReadAllText(Path.Combine(repositoryRoot, "HipLanternCompatAdapter.cs")),
            "private static bool TryAddHipLanternCompatSlot",
            "private static bool IsHipLanternCustomSlotEnabled");
        int disabledSlotGuard = adapterSlotSource.IndexOf("if (!IsHipLanternCustomSlotEnabled(out _))", StringComparison.Ordinal);
        int consumeDisabledSlot = disabledSlotGuard >= 0
            ? adapterSlotSource.IndexOf("return true;", disabledSlotGuard, StringComparison.Ordinal)
            : -1;
        Assert.True(disabledSlotGuard >= 0 && consumeDisabledSlot > disabledSlotGuard,
            "the reserved YAML id must not fall through to a generic slot when HipLantern custom mode is unavailable");
        string lifecycleSource = File.ReadAllText(Path.Combine(repositoryRoot, "PluginLifecycle.cs"));
        Assert.True(lifecycleSource.Contains("RefreshHipLanternCompatibilityState(player)", StringComparison.Ordinal),
            "runtime utility-mode changes and stale native state must be reconciled");
    }

    public static void QuickslotResetPolicyClearsHighestRowsFirst()
    {
        List<int> attemptedRows = new();
        int rows = InventorySlotSafetyCore.ResolveQuickSlotProgressionResetRows(
            configuredRows: 3,
            naturallyUnlockedRows: 1,
            tryClearRow: row =>
            {
                attemptedRows.Add(row);
                return true;
            });

        Assert.Equal("3,2", string.Join(",", attemptedRows));
        Assert.Equal(1, rows);
    }

    public static void QuickslotResetPolicyStopsAtFirstBlockedRow()
    {
        List<int> attemptedRows = new();
        int rows = InventorySlotSafetyCore.ResolveQuickSlotProgressionResetRows(
            configuredRows: 3,
            naturallyUnlockedRows: 1,
            tryClearRow: row =>
            {
                attemptedRows.Add(row);
                return row != 2;
            });

        Assert.Equal("3,2", string.Join(",", attemptedRows));
        Assert.Equal(2, rows);

        attemptedRows.Clear();
        rows = InventorySlotSafetyCore.ResolveQuickSlotProgressionResetRows(
            configuredRows: 3,
            naturallyUnlockedRows: 1,
            tryClearRow: row =>
            {
                attemptedRows.Add(row);
                return false;
            });

        Assert.Equal("3", string.Join(",", attemptedRows));
        Assert.Equal(3, rows);
    }

    public static void QuickslotResetPolicyRespectsNaturallyUnlockedRows()
    {
        List<int> attemptedRows = new();
        int rows = InventorySlotSafetyCore.ResolveQuickSlotProgressionResetRows(
            configuredRows: 3,
            naturallyUnlockedRows: 2,
            tryClearRow: row =>
            {
                attemptedRows.Add(row);
                return true;
            });

        Assert.Equal("3", string.Join(",", attemptedRows));
        Assert.Equal(2, rows);
    }

    public static void QuickslotResetPolicySkipsRowsThatCannotBeReduced()
    {
        int callbackCount = 0;
        int rows = InventorySlotSafetyCore.ResolveQuickSlotProgressionResetRows(
            configuredRows: 3,
            naturallyUnlockedRows: 3,
            tryClearRow: _ =>
            {
                callbackCount++;
                return false;
            });

        Assert.Equal(3, rows);
        Assert.Equal(0, callbackCount);

        rows = InventorySlotSafetyCore.ResolveQuickSlotProgressionResetRows(
            configuredRows: 0,
            naturallyUnlockedRows: 0,
            tryClearRow: _ =>
            {
                callbackCount++;
                return false;
            });

        Assert.Equal(0, rows);
        Assert.Equal(0, callbackCount);
    }

    public static void QuickslotLoadPreservationDoesNotAuthorizeReset()
    {
        string source = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "InventorySlotLayout.cs"));
        string naturalUnlock = ReadSourceSection(
            source,
            "private static int GetNaturallyUnlockedQuickSlotRows",
            "private static int GetStableUnlockedQuickSlotRows");
        string loadPreservation = ReadSourceSection(
            source,
            "private static void PreserveOccupiedQuickSlotRowsDuringLoad",
            "private static bool IsItemProgressionLookupReady");
        string explicitReset = ReadSourceSection(
            source,
            "internal static void OnPlayerProgressionReset",
            "private static bool HasPendingQuickSlotProgressionReset");
        string reconciliation = ReadSourceSection(
            source,
            "private static bool ReconcilePendingQuickSlotProgressionReset",
            "private static bool TryMoveQuickSlotProgressionRowToRegularCells");

        Assert.False(
            loadPreservation.Contains("QuickSlotProgressionResetPendingPlayerId", StringComparison.Ordinal),
            "normal inventory loading must preserve occupied quick-slot rows without authorizing destructive reset reconciliation");
        Assert.True(
            explicitReset.Contains("QuickSlotProgressionResetPendingPlayerId", StringComparison.Ordinal),
            "explicit character progression resets must still authorize quick-slot reconciliation");
        Assert.True(
            reconciliation.Contains("IsQuickSlotProgressionLookupReady", StringComparison.Ordinal),
            "quick-slot reset reconciliation must defer until item-name lookup data is ready");
        Assert.True(
            naturalUnlock.Contains("RefreshItemNameTokens", StringComparison.Ordinal),
            "cold-start quick-slot unlock checks must populate prefab-to-shared-name tokens before evaluating progression");
    }

    public static void ProgressiveInventoryRowRecoveryWaitsForItemLookup()
    {
        string layoutSource = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "InventorySlotLayout.cs"));
        string recoverySource = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "ForeignSlotRecovery.cs"));
        string unlockedRows = ReadSourceSection(
            layoutSource,
            "private static int CalculateUnlockedRows",
            "private static int GetMaxExtraRows");
        string lookupReadiness = ReadSourceSection(
            layoutSource,
            "private static bool IsItemProgressionLookupReady",
            "private static bool IsRegularRowProgressionLookupReady");
        string regularRowReadiness = ReadSourceSection(
            layoutSource,
            "private static bool IsRegularRowProgressionLookupReady",
            "private static bool IsQuickSlotProgressionLookupReady");
        string recoveryPolicy = ReadSourceSection(
            recoverySource,
            "private static bool ShouldRecoverForeignSlotItem",
            "private static bool ShouldPreserveForeignSlotHeight");

        Assert.True(
            unlockedRows.Contains("RefreshItemNameTokens", StringComparison.Ordinal),
            "cold-start regular-row unlock checks must populate prefab-to-shared-name tokens before evaluating progression");
        Assert.True(
            lookupReadiness.Contains("ObjectDB.instance", StringComparison.Ordinal) &&
            lookupReadiness.Contains("ItemNameTokens.Count > 0", StringComparison.Ordinal),
            "progression lookup readiness must require a populated ObjectDB-backed item-name lookup");
        Assert.True(
            regularRowReadiness.Contains("IsItemProgressionLookupReady", StringComparison.Ordinal),
            "progressive regular rows must use the shared item progression readiness check");
        Assert.True(
            recoveryPolicy.Contains("ShouldPreserveProgressiveRowsDuringLoad", StringComparison.Ordinal),
            "locked regular-row recovery must not run while inventory load preservation is active");
        Assert.True(
            recoveryPolicy.Contains("IsRegularRowProgressionLookupReady", StringComparison.Ordinal),
            "locked regular-row recovery must defer until item-name lookup data is ready");
        Assert.True(
            recoveryPolicy.IndexOf("ShouldPreserveForeignSlotHeight", StringComparison.Ordinal) <
            recoveryPolicy.IndexOf("IsRegularRowProgressionLookupReady", StringComparison.Ordinal) &&
            recoveryPolicy.IndexOf("IsLegacyExtraSlotsItem", StringComparison.Ordinal) <
            recoveryPolicy.IndexOf("IsRegularRowProgressionLookupReady", StringComparison.Ordinal),
            "lookup readiness must gate only RegularLocked recovery, not out-of-grid or legacy item recovery");
    }

    public static void NewlyUnlockedInventoryRowsRevealOnce()
    {
        string lifecycleSource = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "InventoryLifecyclePatches.cs"));
        string rowsSource = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "InventoryRows.cs"));
        string addKnownItemPatch = ReadSourceSection(
            lifecycleSource,
            "internal static class PlayerAddKnownItemInventorySlotsPatch",
            "internal static class HumanoidSetupEquipmentValidateInventoryPatch");
        string capture = ReadSourceSection(
            rowsSource,
            "internal static int CaptureRegularRowsBeforeKnownItem",
            "internal static void RevealRegularRowsAfterKnownItem");
        string reveal = ReadSourceSection(
            rowsSource,
            "internal static void RevealRegularRowsAfterKnownItem",
            "private static int GetInventoryViewportRows");

        Assert.True(
            addKnownItemPatch.Contains("out int __state", StringComparison.Ordinal) &&
            addKnownItemPatch.Contains("CaptureRegularRowsBeforeKnownItem(__instance, item)", StringComparison.Ordinal) &&
            addKnownItemPatch.Contains("[HarmonyPriority(Priority.Last)]", StringComparison.Ordinal) &&
            addKnownItemPatch.Contains("RevealRegularRowsAfterKnownItem(__instance, __state)", StringComparison.Ordinal),
            "AddKnownItem must carry the pre-discovery row count into a final post-discovery reveal check");
        Assert.True(
            addKnownItemPatch.Contains("ShouldSuppressKnownItemRediscovery", StringComparison.Ordinal) &&
            addKnownItemPatch.IndexOf("ShouldSuppressKnownItemRediscovery", StringComparison.Ordinal) <
            addKnownItemPatch.IndexOf("CaptureRegularRowsBeforeKnownItem", StringComparison.Ordinal),
            "suppressed rediscovery must not capture or reveal inventory rows");
        Assert.True(
            capture.Contains("player != Player.m_localPlayer", StringComparison.Ordinal) &&
            capture.Contains("player.m_isLoading", StringComparison.Ordinal) &&
            capture.Contains("!UseExpandableInventoryRows()", StringComparison.Ordinal) &&
            capture.Contains("player.m_knownMaterial.Contains(sharedName)", StringComparison.Ordinal),
            "row reveal capture must be limited to a new local known item outside loading in expandable mode");
        Assert.True(
            reveal.Contains("previousRows < BaseRows", StringComparison.Ordinal) &&
            reveal.Contains("currentRows <= previousRows", StringComparison.Ordinal) &&
            reveal.Contains("GetInventoryViewportRows(currentRows) >= currentRows", StringComparison.Ordinal) &&
            reveal.Contains("SetExpandableInventoryRows(currentRows, currentRows)", StringComparison.Ordinal),
            "row reveal must require a real unlock, preserve an already larger remembered viewport, and expand only to the new total");
    }

    public static void KeepOnDeathPreparationAndRestorationRetainEveryUnconfirmedItem()
    {
        string source = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "DeathKeep.cs"));
        string createPreparation = ReadSourceSection(
            source,
            "internal static TombStonePreparationState PrepareCreateTombStone",
            "internal static void CompleteCreateTombStone");
        string itemPreparation = ReadSourceSection(
            source,
            "internal static List<KeepOnDeathItemState> PrepareKeepOnDeathItems(Player player, out Inventory? sourceInventory)",
            "internal static void RestoreKeepOnDeathItems");
        string completion = ReadSourceSection(
            source,
            "internal static void CompleteCreateTombStone",
            "internal static List<KeepOnDeathItemState> PrepareKeepOnDeathItems(Player player, out Inventory? sourceInventory)");
        string restoration = ReadSourceSection(
            source,
            "internal static void RestoreKeepOnDeathItems",
            "private static void RollbackPreparedKeepOnDeathItems");
        string rollback = ReadSourceSection(
            source,
            "private static void RollbackPreparedKeepOnDeathItems",
            "private static bool RestoreKeepOnDeathItem");

        Assert.True(
            createPreparation.Contains("RollbackPreparedKeepOnDeathItems(player, sourceInventory, keptItems)", StringComparison.Ordinal) &&
            itemPreparation.Contains("RollbackPreparedKeepOnDeathItems(player, inventory, keptItems)", StringComparison.Ordinal),
            "both item collection and later death-drop preparation must roll back items already removed from the inventory");
        Assert.True(
            createPreparation.Contains("PrepareKeepOnDeathItems(player, out Inventory? sourceInventory)", StringComparison.Ordinal) &&
            itemPreparation.Contains("sourceInventory = inventory;", StringComparison.Ordinal),
            "collection, rollback, and final preservation must retain one exact source inventory reference");
        Assert.True(
            rollback.Contains("inventory.m_inventory.Add(item)", StringComparison.Ordinal) &&
            rollback.Contains("OriginalCustomData", StringComparison.Ordinal),
            "preparation rollback must restore both item ownership and the item state captured before unequip callbacks");
        Assert.True(
            restoration.Contains("keptItems.RemoveAt(index)", StringComparison.Ordinal) &&
            restoration.Contains("if (!safelyInInventory)", StringComparison.Ordinal) &&
            !restoration.Contains("keptItems.Clear()", StringComparison.Ordinal),
            "restoration must remove only items confirmed safe in the inventory and retain failed entries for retry");
        int restoreCall = restoration.IndexOf("_ = RestoreKeepOnDeathItem", StringComparison.Ordinal);
        int ownershipCheck = restoration.IndexOf("safelyInInventory = inventory.ContainsItem(item);", restoreCall, StringComparison.Ordinal);
        int stateRemoval = restoration.IndexOf("keptItems.RemoveAt(index);", StringComparison.Ordinal);
        Assert.True(
            restoreCall >= 0 && ownershipCheck > restoreCall && stateRemoval > ownershipCheck,
            "a callback success result must not discard escrow until the exact item reference is still owned by the source inventory");
        Assert.True(
            completion.Contains("state.KeptItems.Count == 0", StringComparison.Ordinal) &&
            !completion.Contains("state.Completed = true", StringComparison.Ordinal),
            "tombstone preparation state must complete only after every kept item is safe");
    }

    public static void KeepOnDeathFinalizerDirectlyPreservesEveryRemainingItem()
    {
        string repositoryRoot = FindRepositoryRoot();
        string source = File.ReadAllText(Path.Combine(repositoryRoot, "DeathKeep.cs"));
        string completion = ReadSourceSection(
            source,
            "internal static void CompleteCreateTombStone",
            "internal static List<KeepOnDeathItemState> PrepareKeepOnDeathItems(Player player, out Inventory? sourceInventory)");
        string fallback = ReadSourceSection(
            source,
            "private static void EmergencyPreserveKeepOnDeathItems",
            "private static bool RestoreKeepOnDeathItem");
        string normalOverflow = ReadSourceSection(
            source,
            "private static bool PreserveKeepOnDeathItemWithoutOverwriting",
            "private static bool CanRestoreKeepOnDeathItemAtCell");
        string patches = File.ReadAllText(
            Path.Combine(repositoryRoot, "DeathAndTombstonePatches.cs"));

        Assert.True(
            source.Contains("public Inventory? SourceInventory { get; }", StringComparison.Ordinal) &&
            completion.Contains("EmergencyPreserveKeepOnDeathItems(state.SourceInventory, state.KeptItems)", StringComparison.Ordinal),
            "the finalizer fallback must retain the exact readonly inventory that owned the items before tombstone creation");
        Assert.True(
            patches.Contains("finalAttempt: false", StringComparison.Ordinal) &&
            patches.Contains("finalAttempt: true", StringComparison.Ordinal),
            "only the synchronous Harmony finalizer may invoke the raw last-chance preservation path");

        int rawAdd = fallback.IndexOf("inventory.m_inventory.Add(item);", StringComparison.Ordinal);
        int confirmed = fallback.IndexOf("if (!inventory.m_inventory.Contains(item))", rawAdd + 1, StringComparison.Ordinal);
        int removeState = fallback.IndexOf("keptItems.RemoveAt(index);", StringComparison.Ordinal);
        Assert.True(
            fallback.Contains("SelectNonOverlappingPreservationCell", StringComparison.Ordinal) &&
            rawAdd >= 0 && confirmed > rawAdd && removeState > confirmed,
            "the fallback must keep failed items out of unusable cells and forget escrow only after raw ownership is confirmed");
        Assert.False(
            fallback.Contains("MoveItemToThis", StringComparison.Ordinal) ||
            fallback.Contains("TryFindFreeRegularCell", StringComparison.Ordinal),
            "the final fallback must not re-enter placement or equipment callbacks that caused normal restoration to fail");
        Assert.True(
            normalOverflow.Contains("item.m_equipped = false;", StringComparison.Ordinal) &&
            normalOverflow.Contains("ClearItemSlot(item);", StringComparison.Ordinal),
            "normal overflow preservation must not leave stale equipped or dedicated-slot markers outside a usable slot");
    }

    public static void SlotAutoEquipSuppressionRemainsBalancedWhenScopesNest()
    {
        string repositoryRoot = FindRepositoryRoot();
        string stateSource = File.ReadAllText(Path.Combine(repositoryRoot, "InventoryState.cs"));
        string controllerSource = File.ReadAllText(Path.Combine(repositoryRoot, "SlotEquipController.cs"));
        string handlersSource = File.ReadAllText(Path.Combine(repositoryRoot, "SlotEquipPatchHandlers.cs"));
        string suppressionMethods = ReadSourceSection(
            controllerSource,
            "internal static void BeginSlotAutoEquipSuppression",
            "internal static bool TryRouteHumanoidEquipToDedicatedSlot");

        Assert.True(
            stateSource.Contains("int SlotAutoEquipSuppressionDepth", StringComparison.Ordinal) &&
            stateSource.Contains("SuppressSlotAutoEquip => SlotAutoEquipSuppressionDepth > 0", StringComparison.Ordinal),
            "slot auto-equip suppression must be derived from a nesting depth instead of a mutable boolean");
        Assert.True(
            suppressionMethods.Contains("SlotAutoEquipSuppressionDepth++", StringComparison.Ordinal) &&
            suppressionMethods.Contains("SlotAutoEquipSuppressionDepth--", StringComparison.Ordinal),
            "suppression entry and completion must balance one nested scope at a time");
        Assert.False(
            (controllerSource + handlersSource).Contains("SuppressSlotAutoEquip =", StringComparison.Ordinal),
            "nested suppression scopes must not overwrite the outer scope with direct boolean assignment");
        Assert.True(
            handlersSource.Contains("BeginSlotAutoEquipSuppression();", StringComparison.Ordinal) &&
            handlersSource.Contains("CompleteSlotAutoEquipSuppression();", StringComparison.Ordinal),
            "death-drop and unequip callback scopes must use the balanced suppression operations");
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
        bool found = InventoryPlacementPolicyCore.TrySelectAutomaticPlacementCell(
            inventoryWidth: 3,
            rowCount: 3,
            preferHotbar: false,
            isAllowed: (_, _) => true,
            isOccupied: (_, _) => false,
            out InventorySlotSafetyCore.GridCell cell);

        Assert.True(found, "automatic placement should find an available regular inventory cell");
        Assert.Equal(0, cell.X);
        Assert.Equal(1, cell.Y);
    }

    public static void InventoryAutomaticPlacementFallsBackToHotbar()
    {
        bool found = InventoryPlacementPolicyCore.TrySelectAutomaticPlacementCell(
            inventoryWidth: 3,
            rowCount: 3,
            preferHotbar: false,
            isAllowed: (_, _) => true,
            isOccupied: (x, y) => y > 0 || x == 0,
            out InventorySlotSafetyCore.GridCell cell);

        Assert.True(found, "automatic placement should use hotbar when every regular inventory cell is occupied");
        Assert.Equal(1, cell.X);
        Assert.Equal(0, cell.Y);
    }

    public static void QuickSlotItemAutomaticPlacementPrefersHotbar()
    {
        bool found = InventoryPlacementPolicyCore.TrySelectAutomaticPlacementCell(
            inventoryWidth: 3,
            rowCount: 3,
            preferHotbar: true,
            isAllowed: (_, _) => true,
            isOccupied: (_, _) => false,
            out InventorySlotSafetyCore.GridCell cell);

        Assert.True(found, "quick-slot item automatic placement should find an available hotbar cell");
        Assert.Equal(0, cell.X);
        Assert.Equal(0, cell.Y);
    }

    public static void QuickSlotItemAutomaticPlacementFallsBackToRegularRows()
    {
        bool found = InventoryPlacementPolicyCore.TrySelectAutomaticPlacementCell(
            inventoryWidth: 3,
            rowCount: 3,
            preferHotbar: true,
            isAllowed: (_, _) => true,
            isOccupied: (_, y) => y == 0,
            out InventorySlotSafetyCore.GridCell cell);

        Assert.True(found, "quick-slot item automatic placement should use regular rows when the hotbar is full");
        Assert.Equal(0, cell.X);
        Assert.Equal(1, cell.Y);
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

    public static void InventoryTrashRejectsQuestItemsThroughFinalConfirmation()
    {
        string source = File.ReadAllText(
            Path.Combine(FindRepositoryRoot(), "InventoryTrashPanel.cs"));
        string policy = ReadSourceSection(
            source,
            "private static bool CanTrashInventoryItem",
            "private static bool CanTrashInventoryCell");
        Assert.True(
            policy.Contains("item.m_shared.m_questItem", StringComparison.Ordinal) &&
            policy.Contains("$inventoryslots_trash_quest_item", StringComparison.Ordinal),
            "the shared trash policy must reject quest items with a dedicated message");

        string confirmation = ReadSourceSection(
            source,
            "private static void ConfirmInventoryTrashDelete",
            "private static void ShowInventoryTrashMessage");
        int policyCheck = confirmation.IndexOf(
            "CanTrashInventoryItem(player, inventory, item, showMessage: true)",
            StringComparison.Ordinal);
        int fullStackRemoval = confirmation.IndexOf(
            "bool fullStack = amount >= item.m_stack;",
            StringComparison.Ordinal);
        Assert.True(
            policyCheck >= 0 && fullStackRemoval > policyCheck,
            "confirmation must rerun the quest-aware policy before deleting any amount");
    }

    public static void InventoryActionsUsesFixedVanillaCellPolicyDirectly()
    {
        string repositoryRoot = FindRepositoryRoot();
        string source = File.ReadAllText(
            Path.Combine(repositoryRoot, "InventoryActions", "ConfigAndUtility.cs"));
        string policy = ReadSourceSection(
            source,
            "private static bool CanFavoriteCell",
            "private static bool HasNoCustomData");

        Assert.True(
            CountSourceOccurrences(policy, "return IsSupportedPlayerCell(inventory, pos);") == 1,
            "favorites and favorite restock must share one supported vanilla-cell predicate");
        Assert.True(
            policy.Contains("return IsRegularPlayerCell(inventory, pos);", StringComparison.Ordinal) &&
            policy.Contains("IsSupportedPlayerCell(inventory, pos) && pos.y > 0", StringComparison.Ordinal),
            "trash and regular container actions must exclude the hotbar");
        Assert.True(
            policy.Contains("!IsOutOfBounds(inventory, pos)", StringComparison.Ordinal) &&
            policy.Contains("pos.y < Math.Min(VanillaPlayerRows, inventory.GetHeight())", StringComparison.Ordinal),
            "InventoryActions must limit its standalone policy to loaded vanilla player rows");
        Assert.False(
            policy.Contains("InventoryCellKind", StringComparison.Ordinal) ||
            policy.Contains("InventoryActionCellPolicyCore", StringComparison.Ordinal),
            "the standalone mod must not retain unreachable InventorySlots cell kinds");

        string actions = File.ReadAllText(
            Path.Combine(repositoryRoot, "InventoryActions", "Actions.cs"));
        string favoriteRestock = ReadSourceSection(
            actions,
            "private static bool ShouldRestockFavoriteItem",
            "private static bool ShouldTakeMatchingStackItem");
        Assert.True(
            favoriteRestock.Contains("CanFavoriteCell(inventory, item.m_gridPos)", StringComparison.Ordinal),
            "favorite restock must reuse the favorite-cell policy instead of defining another predicate");
    }

    public static void InventoryActionsTooltipGuardOwnsOnlyItsButtons()
    {
        string source = File.ReadAllText(
            Path.Combine(FindRepositoryRoot(), "InventoryActions", "Ui.cs"));
        string guard = ReadSourceSection(
            source,
            "internal static bool ShouldAllowTooltipHoverStart",
            "private static void CaptureRectTransformSnapshot");
        int nameCheck = guard.IndexOf(
            "name.StartsWith(\"InventoryActions_\"",
            StringComparison.Ordinal);
        int markerCheck = guard.IndexOf(
            "GetComponent<InventoryActionButtonMarker>() == null",
            StringComparison.Ordinal);
        int clearTopic = guard.IndexOf("tooltip.m_topic = \"\";", StringComparison.Ordinal);

        Assert.True(
            nameCheck >= 0 && markerCheck > nameCheck && clearTopic > markerCheck,
            "the null-prefab fallback must prove both InventoryActions identity markers before suppressing hover");
        Assert.False(
            source.Contains("SetActionButtonLabel(gui.m_takeAllButton", StringComparison.Ordinal) ||
            source.Contains("SetActionButtonLabel(gui.m_stackAllButton", StringComparison.Ordinal),
            "InventoryActions must not relabel or mark vanilla container buttons");
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
                previousHadSocketRows: true,
                previousRowlessRefreshAttempts: 0),
            "stable pinned socket rows should not call the native tooltip API again for the same signature");
    }

    public static void JewelcraftingNativeTooltipRefreshBoundsStableRowlessRetries()
    {
        Assert.True(
            JewelcraftingTooltipCore.ShouldRefreshNativeTooltip(
                previousSignature: "same",
                nextSignature: "same",
                previousVisible: true,
                previousHadSocketRows: false,
                previousRowlessRefreshAttempts: 1),
            "stable rowless tooltips should allow the first follow-up refresh");
        Assert.True(
            JewelcraftingTooltipCore.ShouldRefreshNativeTooltip(
                previousSignature: "same",
                nextSignature: "same",
                previousVisible: true,
                previousHadSocketRows: false,
                previousRowlessRefreshAttempts: 2),
            "stable rowless tooltips should allow a second follow-up refresh");
        Assert.False(
            JewelcraftingTooltipCore.ShouldRefreshNativeTooltip(
                previousSignature: "same",
                nextSignature: "same",
                previousVisible: true,
                previousHadSocketRows: false,
                previousRowlessRefreshAttempts: JewelcraftingTooltipCore.MaxRowlessRefreshAttempts),
            "stable interact-only tooltips should reuse the cache after the bounded retry budget");
    }

    public static void JewelcraftingNativeTooltipRefreshRunsForChangedOrUnstableState()
    {
        Assert.True(
            JewelcraftingTooltipCore.ShouldRefreshNativeTooltip(
                previousSignature: "old",
                nextSignature: "new",
                previousVisible: true,
                previousHadSocketRows: true,
                previousRowlessRefreshAttempts: JewelcraftingTooltipCore.MaxRowlessRefreshAttempts),
            "changed key/item/socket signature should call the native tooltip API");
        Assert.True(
            JewelcraftingTooltipCore.ShouldRefreshNativeTooltip(
                previousSignature: "same",
                nextSignature: "same",
                previousVisible: false,
                previousHadSocketRows: true,
                previousRowlessRefreshAttempts: JewelcraftingTooltipCore.MaxRowlessRefreshAttempts),
            "hidden tooltip state should refresh before reuse");
        Assert.True(
            JewelcraftingTooltipCore.ShouldRefreshNativeTooltip(
                previousSignature: "same",
                nextSignature: "same",
                previousVisible: true,
                previousHadSocketRows: false,
                previousRowlessRefreshAttempts: 0),
            "a newly rowless tooltip should retry while delayed socket rows may still appear");
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

    public static void EpicLootPublicApiLifecycleUsesExactV1Contracts()
    {
        string repositoryRoot = FindRepositoryRoot();
        string compatSource = File.ReadAllText(Path.Combine(repositoryRoot, "EpicLootCompat.cs"));
        string lifecycleSource = File.ReadAllText(Path.Combine(repositoryRoot, "PluginLifecycle.cs"));
        string resolution = ReadSourceSection(
            compatSource,
            "private static bool TryGetEpicLootPublicApi",
            "private sealed class EpicLootPublicApi");
        string apiContract = ReadSourceSection(
            compatSource,
            "private sealed class EpicLootPublicApi",
            "private static void ScheduleEpicLootRespawnRuntimeReload");

        Assert.True(
            resolution.Contains("pluginInfo.Instance.GetType().Assembly.GetType(\"EpicLoot.API\")", StringComparison.Ordinal) &&
            resolution.Contains("\"GetApiVersion\"", StringComparison.Ordinal) &&
            resolution.Contains("BindingFlags.Public | BindingFlags.Static", StringComparison.Ordinal) &&
            resolution.Contains("Type.EmptyTypes", StringComparison.Ordinal) &&
            resolution.Contains("getApiVersion.ReturnType != typeof(int)", StringComparison.Ordinal) &&
            resolution.Contains("apiVersion < 1", StringComparison.Ordinal),
            "the public bridge must resolve API v1 from the installed EpicLoot plugin assembly with the exact GetApiVersion contract");
        Assert.False(
            compatSource.Contains("RegisterEquipmentProvider", StringComparison.Ordinal) ||
            compatSource.Contains("EpicLoot.PlayerExtensions:GetEquipment", StringComparison.Ordinal),
            "InventorySlots must not add an equipment provider or revive EpicLoot's deprecated GetEquipment patch path");

        string[] exactDelegateContracts =
        [
            "Func<string, Func<ItemData, bool>, bool>? RegisterSacrificeFilter",
            "Func<string, bool>? UnregisterSacrificeFilter",
            "Action<Player>? InvalidatePlayerEffectCache",
            "Func<GameObject, GameObject, ItemData?, bool, bool>? ApplyMagicItemBackground",
            "Func<ItemData, bool>? IsShardStone",
            "Func<ItemData, bool>? IsMagicCraftingMaterial"
        ];
        foreach (string contract in exactDelegateContracts)
        {
            Assert.True(apiContract.Contains(contract, StringComparison.Ordinal), $"missing exact EpicLoot API delegate contract: {contract}");
        }

        Assert.True(
            apiContract.Contains("apiType.GetMethod(", StringComparison.Ordinal) &&
            apiContract.Contains("BindingFlags.Public | BindingFlags.Static", StringComparison.Ordinal) &&
            apiContract.Contains("parameterTypes", StringComparison.Ordinal) &&
            apiContract.Contains("method?.ReturnType == returnType", StringComparison.Ordinal) &&
            apiContract.Contains("Delegate.CreateDelegate(delegateType, method)", StringComparison.Ordinal),
            "every optional endpoint must bind by exact parameter and return types rather than method name alone");

        string initialize = ReadSourceSection(
            compatSource,
            "private static void InitializeEpicLootCompatibility",
            "private static void ShutdownEpicLootCompatibility");
        int rootCallback = initialize.IndexOf("_epicLootSacrificeFilter ??= CanSacrificeEpicLootItem;", StringComparison.Ordinal);
        int register = initialize.IndexOf("api.RegisterSacrificeFilter(ModGUID, _epicLootSacrificeFilter)", StringComparison.Ordinal);
        Assert.True(
            compatSource.Contains("private static Func<ItemData, bool>? _epicLootSacrificeFilter;", StringComparison.Ordinal) &&
            rootCallback >= 0 && register > rootCallback,
            "the callback must remain strongly rooted for the complete registration lifetime");

        string shutdown = ReadSourceSection(
            compatSource,
            "private static void ShutdownEpicLootCompatibility",
            "private static bool CanSacrificeEpicLootItem");
        int deactivate = shutdown.IndexOf("_epicLootSacrificeFilterRegistered = false;", StringComparison.Ordinal);
        int unregister = shutdown.IndexOf("api.UnregisterSacrificeFilter(ModGUID)", StringComparison.Ordinal);
        Assert.True(
            deactivate >= 0 && unregister > deactivate,
            "shutdown must make the callback inactive before the best-effort external unregistration call");
        Assert.True(
            lifecycleSource.Contains("InitializeEpicLootCompatibility();", StringComparison.Ordinal) &&
            lifecycleSource.Contains("ShutdownEpicLootCompatibility();", StringComparison.Ordinal),
            "plugin startup and destruction must own the EpicLoot registration lifecycle");
    }

    public static void EpicLootSacrificeAndEffectRefreshFailSafely()
    {
        string source = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "EpicLootCompat.cs"));
        string callback = ReadSourceSection(
            source,
            "private static bool CanSacrificeEpicLootItem",
            "private static void ResetEpicLootEquipmentEffectCache");
        int inactiveGuard = callback.IndexOf("if (!_epicLootSacrificeFilterRegistered)", StringComparison.Ordinal);
        int disabledGuard = callback.IndexOf("if (_epicLootSacrificeFilterCallbackDisabled)", StringComparison.Ordinal);
        int inspectItem = callback.IndexOf("Player? player = Player.m_localPlayer;", StringComparison.Ordinal);
        Assert.True(
            inactiveGuard >= 0 && disabledGuard > inactiveGuard && inspectItem > disabledGuard &&
            callback.Contains("return true;", StringComparison.Ordinal),
            "a callback retained after shutdown must be an inactive no-op, while a faulted active callback remains fail-closed");
        Assert.True(
            callback.Contains("slot?.Kind != SlotKind.Quick", StringComparison.Ordinal) &&
            callback.Contains("ReferenceEquals(inventory.GetItemAt(item.m_gridPos.x, item.m_gridPos.y), item)", StringComparison.Ordinal),
            "only the exact live item reference in a quick slot may be vetoed; repeated grid coordinates from other inventories must remain eligible");

        int catchStart = callback.LastIndexOf("catch (Exception ex)", StringComparison.Ordinal);
        Assert.True(catchStart >= 0, "the EpicLoot sacrifice callback must contain a bounded failure path");
        string callbackFailure = callback.Substring(catchStart);
        Assert.True(
            callbackFailure.Contains("_epicLootSacrificeFilterCallbackDisabled = true;", StringComparison.Ordinal) &&
            callbackFailure.Contains("failed closed", StringComparison.Ordinal) &&
            callbackFailure.Contains("return false;", StringComparison.Ordinal),
            "an unexpected active callback failure must veto the item and disable later unsafe evaluations");

        string refresh = ReadSourceSection(
            source,
            "private static void ResetEpicLootEquipmentEffectCache",
            "private static void TryApplyEpicLootMagicItemBackground");
        int publicInvalidate = refresh.IndexOf("api.InvalidatePlayerEffectCache(player);", StringComparison.Ordinal);
        int legacyResolve = refresh.IndexOf("ResolveEpicLootEquipmentEffectCacheResetMethod();", StringComparison.Ordinal);
        Assert.True(
            publicInvalidate >= 0 && legacyResolve > publicInvalidate &&
            refresh.Contains("_epicLootPublicCacheInvalidationDisabled", StringComparison.Ordinal) &&
            refresh.Contains("_epicLootEquipmentEffectCacheResetMethod.Invoke", StringComparison.Ordinal),
            "effect refresh must prefer EpicLoot API v1, then retain the existing legacy cache reset only as a bounded fallback");
        string publicRefreshPath = refresh.Substring(publicInvalidate, legacyResolve - publicInvalidate);
        Assert.True(
            publicRefreshPath.Contains("return;", StringComparison.Ordinal) &&
            publicRefreshPath.Contains("_epicLootPublicCacheInvalidationDisabled = true;", StringComparison.Ordinal),
            "a successful public refresh must not double-reset, while a thrown endpoint must be disabled before the legacy fallback");
    }

    public static void EpicLootQueryAndHudFallbackStayAuthoritative()
    {
        string repositoryRoot = FindRepositoryRoot();
        string compatSource = File.ReadAllText(Path.Combine(repositoryRoot, "EpicLootCompat.cs"));
        string query = ReadSourceSection(
            compatSource,
            "private static bool TryIsEpicLootStackableMaterialByApi",
            "private static bool TryGetEpicLootPublicApi");
        Assert.True(
            query.Contains("api.IsShardStone(item)", StringComparison.Ordinal) &&
            query.Contains("api.IsMagicCraftingMaterial(item)", StringComparison.Ordinal) &&
            query.Contains("result = shardStone || craftingMaterial;", StringComparison.Ordinal) &&
            query.Contains("return true;", StringComparison.Ordinal),
            "an available API result, including false, must be authoritative for EpicLoot shard stones and crafting materials");
        Assert.False(
            query.Contains("IsRunestone", StringComparison.Ordinal),
            "effect-bearing runestones must not be opted into automatic stacking through a classification-only API");
        int queryCatch = query.LastIndexOf("catch (Exception ex)", StringComparison.Ordinal);
        string queryFailure = queryCatch < 0 ? "" : query.Substring(queryCatch);
        Assert.True(
            queryFailure.Contains("_epicLootStackableMaterialQueryDisabled = true;", StringComparison.Ordinal) &&
            queryFailure.Contains("result = false;", StringComparison.Ordinal) &&
            queryFailure.Contains("return false;", StringComparison.Ordinal),
            "only an unavailable or failed API query may hand control to the name/ammo-type fallback");

        string containerSource = File.ReadAllText(Path.Combine(repositoryRoot, "ContainerActions.cs"));
        string materialPolicy = ReadSourceSection(
            containerSource,
            "private static bool IsEpicLootStackableMaterial",
            "private static bool IsEpicLootStackableMaterialToken");
        int apiQuery = materialPolicy.IndexOf("TryIsEpicLootStackableMaterialByApi(item, out bool isStackableMaterial)", StringComparison.Ordinal);
        int legacyToken = materialPolicy.IndexOf("string prefabName = GetItemPrefabName(item);", StringComparison.Ordinal);
        Assert.True(
            apiQuery >= 0 && legacyToken > apiQuery &&
            materialPolicy.Contains("return isStackableMaterial;", StringComparison.Ordinal) &&
            materialPolicy.Contains("m_ammoType", StringComparison.Ordinal) &&
            materialPolicy.Contains("EndsWith(\"ShardStone\", StringComparison.Ordinal)", StringComparison.Ordinal),
            "the caller must respect API false and reach the shard ammo-type fallback only when the API helper reports no answer");

        string hudSource = File.ReadAllText(Path.Combine(repositoryRoot, "InventoryQuickSlotsHud.cs"));
        string updateElement = ReadSourceSection(
            hudSource,
            "private static void UpdateQuickSlotsHotkeyBarElement",
            "private static void ConfigureQuickHudElementLayout");
        int applyBackground = updateElement.IndexOf("TryApplyEpicLootMagicItemBackground", StringComparison.Ordinal);
        int emptyBranch = updateElement.IndexOf("if (item == null)", StringComparison.Ordinal);
        Assert.True(
            applyBackground >= 0 && emptyBranch > applyBackground &&
            CountSourceOccurrences(updateElement, "TryApplyEpicLootMagicItemBackground") == 1 &&
            updateElement.Contains("element.m_go, element.m_equiped, item, inventoryGrid: false", StringComparison.Ordinal),
            "the custom quick HUD must use one common item/null API call before branching so empty slots clear stale EpicLoot backgrounds");

        string backgroundHelper = ReadSourceSection(
            compatSource,
            "private static void TryApplyEpicLootMagicItemBackground",
            "private static bool TryIsEpicLootStackableMaterialByApi");
        Assert.True(
            backgroundHelper.Contains("ItemData? item", StringComparison.Ordinal) &&
            backgroundHelper.Contains("api.ApplyMagicItemBackground(slotRoot, equippedOverlay, item, inventoryGrid)", StringComparison.Ordinal) &&
            backgroundHelper.Contains("_epicLootMagicItemBackgroundDisabled = true;", StringComparison.Ordinal),
            "the HUD bridge must forward nullable items and permanently bound repeated exceptions");

        string sortSource = File.ReadAllText(Path.Combine(repositoryRoot, "ContainerSort.cs"));
        string manualMerge = ReadSourceSection(
            sortSource,
            "private static bool MergeSortableStacks",
            "private static List<Vector2i> GetAllInventorySlots");
        Assert.True(
            manualMerge.Contains("CanUseStackMetadataAutomaticStacking(item)", StringComparison.Ordinal) &&
            !manualMerge.Contains("CanUseContainerActionStacking(item)", StringComparison.Ordinal) &&
            manualMerge.Contains("HasCompatibleStackMetadata(group[0], item)", StringComparison.Ordinal) &&
            manualMerge.Contains("MergeStackMetadata(target, source);", StringComparison.Ordinal),
            "manual Sort merging must be limited to metadata owned by InventorySlots; external custom-data items remain sortable but defer all stack merging to their own mod");
    }

    public static void CraftingHoverWheelFollowsRecipeCellOwnership()
    {
        string repositoryRoot = FindRepositoryRoot();
        string tooltipSource = File.ReadAllText(Path.Combine(repositoryRoot, "CraftingTooltipRecipeRows.cs"));
        string wheelHandler = ReadSourceSection(
            tooltipSource,
            "private static bool HandleCraftingHoverTooltipWheel",
            "private static bool HasCraftingHoverTooltipWheelOwner");
        Assert.True(wheelHandler.Contains("HasCraftingHoverTooltipWheelOwner(GetUiMousePosition(), gamepadScroll)", StringComparison.Ordinal),
            "crafting hover wheel must use the active recipe-cell ownership predicate");
        Assert.False(wheelHandler.Contains("IsUiScrollTargetActive(CraftingUi.HoverTooltipPanel)", StringComparison.Ordinal),
            "the cursor-aligned tooltip panel edge is not a valid hover ownership target");

        string owner = ReadSourceSection(
            tooltipSource,
            "private static bool HasCraftingHoverTooltipWheelOwner",
            "private static void PrepareCraftingTooltipScrollInput");
        Assert.True(owner.Contains("IsCraftingTooltipRecipeOverlayTargetValid(pointer)", StringComparison.Ordinal),
            "mouse ownership must remain tied to the hovered recipe cell");
        Assert.False(owner.Contains("IsUiScrollTargetActive(CraftingUi.HoverTooltipPanel)", StringComparison.Ordinal),
            "hover ownership must not reintroduce the cursor-aligned panel edge test");
        Assert.True(owner.Contains("CraftingUi.HoverTooltipMaxScroll > 1f", StringComparison.Ordinal),
            "short tooltips must not capture recipe-grid scrolling");
        string scrollOperation = ReadSourceSection(
            tooltipSource,
            "private static bool TryScrollCraftingHoverTooltip",
            "private static bool HasCraftingHoverTooltipWheelOwner");
        Assert.True(scrollOperation.Contains("ConsumeMouseUiScrollForCurrentFrame();", StringComparison.Ordinal),
            "a handled hover wheel must not be applied again by another crafting update hook in the same frame");

        string fastPathSource = File.ReadAllText(Path.Combine(repositoryRoot, "CraftingFrameFastPath.cs"));
        string redesignSource = File.ReadAllText(Path.Combine(repositoryRoot, "CraftingRedesign.cs"));
        Assert.True(fastPathSource.Contains("PrepareCraftingTooltipScrollInput(gui);", StringComparison.Ordinal) &&
                    redesignSource.Contains("PrepareCraftingTooltipScrollInput(gui);", StringComparison.Ordinal),
            "both crafting frame paths must measure the current tooltip before routing wheel input");
    }

    public static void CraftingTooltipWheelBlocksOnlyUnderlyingCraftingScrollRects()
    {
        string repositoryRoot = FindRepositoryRoot();
        string patchSource = File.ReadAllText(Path.Combine(repositoryRoot, "CraftingPatches.cs"));
        string guardPatch = ReadSourceSection(
            patchSource,
            "internal static class CraftingTooltipUnderlyingScrollRectGuardPatch",
            "[HarmonyPatch(typeof(InventoryGui), \"UpdateCraftingPanel\")]");
        Assert.True(patchSource.Contains("[HarmonyPatch(typeof(ScrollRect), nameof(ScrollRect.OnScroll))]", StringComparison.Ordinal),
            "the event guard must remain attached to ScrollRect.OnScroll");
        Assert.True(guardPatch.Contains("TryHandleCraftingPointerScroll(__instance, __0)", StringComparison.Ordinal) &&
                    guardPatch.Contains("__0.Use();", StringComparison.Ordinal) &&
                    guardPatch.Contains("return false;", StringComparison.Ordinal),
            "an owned tooltip wheel event must be consumed before the underlying ScrollRect runs");

        string inputSource = File.ReadAllText(Path.Combine(repositoryRoot, "UiScrollInput.cs"));
        string ownershipGuard = ReadSourceSection(
            inputSource,
            "internal static bool TryHandleCraftingPointerScroll",
            "private static Vector2 GetUiMousePosition");
        Assert.True(ownershipGuard.Contains("scrollRect.transform.IsChildOf(gui.m_crafting)", StringComparison.Ordinal),
            "the ScrollRect guard must stay inside the active crafting panel");
        int pointerRead = ownershipGuard.IndexOf("Vector2 pointer = eventData.position;", StringComparison.Ordinal);
        int pinnedPointerGuard = ownershipGuard.IndexOf("IsPointerOverActiveCraftingPinnedTooltip(pointer)", StringComparison.Ordinal);
        int prepareTooltip = ownershipGuard.IndexOf("UpdateCraftingTooltipRecipeOverlay(gui);", StringComparison.Ordinal);
        int checkOwner = ownershipGuard.IndexOf("HasCraftingHoverTooltipWheelOwner(pointer)", StringComparison.Ordinal);
        int applyScroll = ownershipGuard.IndexOf("TryScrollCraftingHoverTooltip(wheel)", StringComparison.Ordinal);
        Assert.False(ownershipGuard.Contains("HasActiveCraftingPinnedTooltip()", StringComparison.Ordinal),
            "an open pinned tooltip must not disable hover scrolling over a different recipe cell");
        Assert.True(pointerRead >= 0 && pinnedPointerGuard > pointerRead && prepareTooltip > pinnedPointerGuard,
            "only a pointer actually over an active pinned panel should leave through the existing pinned path");
        Assert.True(prepareTooltip >= 0 && checkOwner > prepareTooltip && applyScroll > checkOwner,
            "the pointer event must measure and immediately scroll only the owned hover tooltip");
        Assert.True(ownershipGuard.Contains("float wheel = eventData.scrollDelta.y * GetMouseUiScrollMultiplier();", StringComparison.Ordinal),
            "the immediate hover scroll must use the pointer event delta with the configured mouse multiplier");

        string deltaReader = ReadSourceSection(
            inputSource,
            "private static float GetUiScrollDelta",
            "private static bool HasUnconsumedUiScrollInput");
        string consumeHelper = ReadSourceSection(
            inputSource,
            "private static void ConsumeMouseUiScrollForCurrentFrame",
            "internal static bool TryHandleCraftingPointerScroll");
        Assert.True(deltaReader.Contains("_mouseUiScrollConsumedFrame == Time.frameCount", StringComparison.Ordinal) &&
                    consumeHelper.Contains("_mouseUiScrollConsumedFrame = Time.frameCount", StringComparison.Ordinal),
            "mouse wheel routing must remain idempotent across repeated crafting hooks in one frame");

        string pinnedPointerHelper = ReadSourceSection(
            inputSource,
            "private static bool IsPointerOverActiveCraftingPinnedTooltip",
            "private static Vector2 GetUiMousePosition");
        Assert.True(pinnedPointerHelper.Contains("PinnedTooltips.Crafting.Panels", StringComparison.Ordinal) &&
                    pinnedPointerHelper.Contains("panel.gameObject.activeInHierarchy", StringComparison.Ordinal) &&
                    pinnedPointerHelper.Contains("RectContainsScreenPoint(panel, pointer)", StringComparison.Ordinal),
            "hover isolation must yield only to a visible pinned panel under the current pointer");
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

    public static void DirectContainerActionsUsePositionalOwnershipSafeMoves()
    {
        string repositoryRoot = FindRepositoryRoot();
        string source = File.ReadAllText(Path.Combine(repositoryRoot, "ContainerQuickStack.cs"));
        string quickStack = ReadSourceSection(
            source,
            "private static int QuickStackItemsIntoContainer",
            "private static void StoreAllToCurrentContainer");
        string storeAll = ReadSourceSection(
            source,
            "private static void StoreAllToCurrentContainer",
            "private static int MoveItemToContainerTopFirst");
        string move = ReadSourceSection(
            source,
            "private static int MoveItemToContainerTopFirst",
            "private static bool ShouldStoreAllItem");
        string quickStackPolicy = ReadSourceSection(
            source,
            "private static bool ShouldQuickStackItem",
            "private static int QuickStackItemsIntoContainer");
        string storeAllPolicy = source.Substring(
            source.IndexOf("private static bool ShouldStoreAllItem", StringComparison.Ordinal));

        foreach (string action in new[] { quickStack, storeAll })
        {
            Assert.True(
                action.Contains("MoveItemToContainerTopFirst(", StringComparison.Ordinal),
                "direct quick-stack and store-all actions must use the ownership-safe positional move path");
            Assert.False(
                action.Contains(".AddItem(item)", StringComparison.Ordinal) ||
                action.Contains("RemoveItemIfStillOwned", StringComparison.Ordinal),
                "direct actions must not temporarily share one ItemData reference between inventories");
        }

        Assert.True(
            move.Contains("MoveItemToThis(", StringComparison.Ordinal) &&
            move.Contains("CountMovedFromContainerSource(", StringComparison.Ordinal),
            "the positional path must measure actual source-stack reduction, including partial moves");
        Assert.True(
            CountSourceOccurrences(move, "IsEquippedContainerMoveSource(player, source)") >= 3,
            "the positional path must recheck equipped state at mutation time after earlier Changed callbacks");
        Assert.True(
            move.Contains("OrderBy(target => target.m_gridPos.y)", StringComparison.Ordinal) &&
            move.Contains("for (int y = 0;", StringComparison.Ordinal),
            "normal stacking and empty-cell placement must retain top-first container ordering");

        string metadata = File.ReadAllText(
            Path.Combine(repositoryRoot, "StackMetadataInventoryIntegration.cs"));
        string positionalMerge = ReadSourceSection(
            metadata,
            "internal static bool TryPreparePositionalStackMetadataMerge",
            "internal static void CompletePositionalStackMetadataMerge");
        Assert.True(
            positionalMerge.Contains("IsTrustedCustomDataStackingItem(item)", StringComparison.Ordinal),
            "trusted custom-data mods must still receive the positional move so their Harmony validation can decide stack compatibility");
        foreach (string policy in new[] { quickStackPolicy, storeAllPolicy })
        {
            Assert.True(
                policy.Contains("!IsEquippedContainerMoveSource(player, item)", StringComparison.Ordinal),
                "container actions must never clone or remove an actively equipped item");
        }
    }

    public static void ContainerActionSuccessFxStaysBoundedAndOncePerAction()
    {
        string repositoryRoot = FindRepositoryRoot();
        string actionSource = File.ReadAllText(Path.Combine(repositoryRoot, "ContainerActions.cs"));
        Assert.True(
            actionSource.Contains("private const int ContainerActionSuccessVfxLimit = 10;", StringComparison.Ordinal),
            "area success VFX must stay bounded to ten changed containers");

        string directRun = ReadSourceSection(
            actionSource,
            "private static int RunContainerTransferAcrossContainers",
            "private static void ShowContainerActionResult");
        Assert.True(
            directRun.Contains("TryBroadcastChangedContainerActionSuccessVfx", StringComparison.Ordinal),
            "each directly changed container must use the bounded VFX broadcaster");
        Assert.Equal(1, CountSourceOccurrences(directRun, "ContainerActionSuccessSfxKind"));

        string limiter = ReadSourceSection(
            actionSource,
            "private static int TryBroadcastChangedContainerActionSuccessVfx",
            "private static void BroadcastContainerActionSuccessFx");
        Assert.True(
            limiter.Contains("played >= limit", StringComparison.Ordinal) &&
            limiter.Contains("ContainerActionSuccessVfxKind", StringComparison.Ordinal) &&
            limiter.Contains("return played + 1;", StringComparison.Ordinal),
            "the VFX broadcaster must stop at the limit and count only emitted targets");

        string batchSource = File.ReadAllText(
                Path.Combine(repositoryRoot, "MultiUserContainerBatchOperations.cs"))
            .Replace("\r\n", "\n", StringComparison.Ordinal);
        string movedTarget = ReadSourceSection(
            batchSource,
            "private static void MarkMultiUserContainerAreaBatchTargetMoved(\n        MultiUserContainerBatchState batch,\n        MultiUserContainerBatchTarget target)",
            "private static bool TryGetMultiUserContainerBatchContext");
        int alreadyMovedGuard = movedTarget.IndexOf("if (target.MovedAny)", StringComparison.Ordinal);
        int remoteVfx = movedTarget.IndexOf("TryBroadcastChangedContainerActionSuccessVfx", StringComparison.Ordinal);
        Assert.True(
            alreadyMovedGuard >= 0 && remoteVfx > alreadyMovedGuard,
            "remote retries must not emit a second VFX for an already changed target");

        int finishStart = batchSource.IndexOf("private static void FinishMultiUserContainerBatch", StringComparison.Ordinal);
        Assert.True(finishStart >= 0, "multi-user batch completion source must exist");
        string finish = batchSource.Substring(finishStart);
        int clearBatch = finish.IndexOf("_multiUserContainerBatch = null;", StringComparison.Ordinal);
        int remoteSfx = finish.IndexOf("ContainerActionSuccessSfxKind", StringComparison.Ordinal);
        Assert.True(
            clearBatch >= 0 && remoteSfx > clearBatch &&
            CountSourceOccurrences(finish, "ContainerActionSuccessSfxKind") == 1,
            "a completed remote area action must emit the anchor SFX exactly once after clearing batch state");
    }

    public static void ContainerActionSuccessFxUsesTransientEverybodyRpc()
    {
        string repositoryRoot = FindRepositoryRoot();
        string actionSource = File.ReadAllText(Path.Combine(repositoryRoot, "ContainerActions.cs"));
        string broadcaster = ReadSourceSection(
            actionSource,
            "private static void BroadcastContainerActionSuccessFx",
            "private static void RPC_ContainerActionSuccessFx");
        int localFallback = broadcaster.IndexOf(
            "RenderContainerActionSuccessFxLocal(container, effectKind);",
            StringComparison.Ordinal);
        int invokeEverybody = broadcaster.IndexOf("nview.InvokeRPC(", StringComparison.Ordinal);
        Assert.True(
            localFallback >= 0 && invokeEverybody > localFallback &&
            broadcaster.Contains("ZNetView.Everybody", StringComparison.Ordinal) &&
            broadcaster.Contains("ContainerActionSuccessFxRpc", StringComparison.Ordinal),
            "network containers must emit one transient event while local-only containers retain a fallback");
        Assert.Equal(
            1,
            CountSourceOccurrences(
                broadcaster,
                "RenderContainerActionSuccessFxLocal(container, effectKind);"));
        Assert.False(
            broadcaster.Contains("Object.Instantiate", StringComparison.Ordinal) ||
            broadcaster.Contains("GetZDO", StringComparison.Ordinal) ||
            broadcaster.Contains(".Set(", StringComparison.Ordinal),
            "the network sender must not instantiate or persist an effect object");

        string sortSource = File.ReadAllText(Path.Combine(repositoryRoot, "ContainerSort.cs"));
        string registration = ReadSourceSection(
            sortSource,
            "private static void RegisterContainerRpcs",
            "private static void RPC_RequestSort");
        Assert.True(
            registration.Contains("Unregister(ContainerActionSuccessFxRpc)", StringComparison.Ordinal) &&
            registration.Contains("Register<int>(", StringComparison.Ordinal) &&
            registration.Contains("RPC_ContainerActionSuccessFx(container, effectKind)", StringComparison.Ordinal),
            "every loaded container must replace and register the transient receive handler exactly once");

        string receiver = ReadSourceSection(
            actionSource,
            "private static void RPC_ContainerActionSuccessFx",
            "private static void RenderContainerActionSuccessFxLocal");
        Assert.True(
            receiver.Contains("RenderContainerActionSuccessFxLocal(container, effectKind)", StringComparison.Ordinal),
            "the RPC receiver must dispatch only to the local renderer");
        Assert.False(
            receiver.Contains("InvokeRPC", StringComparison.Ordinal) ||
            receiver.Contains("BroadcastContainerActionSuccessFx(", StringComparison.Ordinal),
            "the receive path must never rebroadcast recursively");
    }

    public static void ContainerActionSuccessFxStaysLocalGuardedAndSelfCleaning()
    {
        string source = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "ContainerActions.cs"));
        Assert.True(
            source.Contains("private const float ContainerActionSuccessFxLifetime = 5f;", StringComparison.Ordinal),
            "transient local effects must retain their five-second cleanup lifetime");

        string dispatch = ReadSourceSection(
            source,
            "private static void RenderContainerActionSuccessFxLocal",
            "private static bool CanRenderContainerActionSuccessFx");
        int guard = dispatch.IndexOf("CanRenderContainerActionSuccessFx", StringComparison.Ordinal);
        int receiveBudget = dispatch.IndexOf("TryConsumeContainerActionSuccessFxReceiveBudget", StringComparison.Ordinal);
        int renderVfx = dispatch.IndexOf("RenderContainerActionSuccessVfxLocal", StringComparison.Ordinal);
        int renderSfx = dispatch.IndexOf("RenderContainerActionSuccessSfxLocal", StringComparison.Ordinal);
        Assert.True(
            guard >= 0 && receiveBudget > guard &&
            renderVfx > receiveBudget && renderSfx > receiveBudget,
            "all local rendering must pass the shared receiver guard and bounded budget first");

        string receiverGuard = ReadSourceSection(
            source,
            "private static bool CanRenderContainerActionSuccessFx",
            "private static void RenderContainerActionSuccessVfxLocal");
        Assert.True(
            receiverGuard.Contains("ContainerActionSuccessVfxKind", StringComparison.Ordinal) &&
            receiverGuard.Contains("ContainerActionSuccessSfxKind", StringComparison.Ordinal) &&
            receiverGuard.Contains("IsDedicatedServer", StringComparison.Ordinal) &&
            receiverGuard.Contains("IsContainerActionSuccessFxEnabled()", StringComparison.Ordinal) &&
            receiverGuard.Contains("Player.m_localPlayer", StringComparison.Ordinal) &&
            receiverGuard.Contains("offset.sqrMagnitude", StringComparison.Ordinal) &&
            receiverGuard.Contains("ContainerActionSuccessFxReceiveRange", StringComparison.Ordinal),
            "receivers must validate effect kind, client role, local preference and distance");

        string receiveBudgetSource = ReadSourceSection(
            source,
            "private static bool TryConsumeContainerActionSuccessFxReceiveBudget",
            "private static void RenderContainerActionSuccessVfxLocal");
        Assert.True(
            source.Contains("private const int ContainerActionSuccessFxReceiveLimit = 32;", StringComparison.Ordinal) &&
            source.Contains("private const float ContainerActionSuccessFxReceiveWindow = 1f;", StringComparison.Ordinal) &&
            receiveBudgetSource.Contains("_containerActionSuccessFxReceivedInWindow >=", StringComparison.Ordinal) &&
            receiveBudgetSource.Contains("_containerActionSuccessFxReceivedInWindow++;", StringComparison.Ordinal),
            "a malformed peer must not create an unbounded number of local effect objects");

        string localVfx = ReadSourceSection(
            source,
            "private static void RenderContainerActionSuccessVfxLocal",
            "private static void RenderContainerActionSuccessSfxLocal");
        int disableNetworkInit = localVfx.IndexOf("ZNetView.m_forceDisableInit = true;", StringComparison.Ordinal);
        int instantiate = localVfx.IndexOf("UnityEngine.Object.Instantiate", StringComparison.Ordinal);
        int restoreNetworkInit = localVfx.IndexOf(
            "ZNetView.m_forceDisableInit = previousForceDisableInit;",
            StringComparison.Ordinal);
        int destroyVfx = localVfx.IndexOf(
            "UnityEngine.Object.Destroy(instance, ContainerActionSuccessFxLifetime);",
            StringComparison.Ordinal);
        Assert.True(
            disableNetworkInit >= 0 && instantiate > disableNetworkInit &&
            restoreNetworkInit > instantiate && destroyVfx > restoreNetworkInit,
            "VFX must remain a non-networked local object that is always cleaned up");
        Assert.True(
            localVfx.Contains("sfx.Stop();", StringComparison.Ordinal) &&
            localVfx.Contains("sfx.gameObject.SetActive(false);", StringComparison.Ordinal) &&
            !localVfx.Contains("InvokeRPC", StringComparison.Ordinal),
            "per-container VFX must stay silent and must not initiate network traffic");

        int localSfxStart = source.IndexOf("private static void RenderContainerActionSuccessSfxLocal", StringComparison.Ordinal);
        Assert.True(localSfxStart >= 0, "local SFX renderer source must exist");
        string localSfx = source.Substring(localSfxStart);
        Assert.True(
            localSfx.Contains(
                "UnityEngine.Object.Destroy(instance, ContainerActionSuccessFxLifetime);",
                StringComparison.Ordinal) &&
            !localSfx.Contains("InvokeRPC", StringComparison.Ordinal),
            "the one-shot SFX must stay local and self-cleaning");
    }

    public static void InventoryActionsContainerActionSuccessFxStaysBoundedAndOncePerAction()
    {
        string repositoryRoot = FindRepositoryRoot();
        string pluginSource = File.ReadAllText(
            Path.Combine(repositoryRoot, "InventoryActions", "Plugin.cs"));
        Assert.True(
            pluginSource.Contains(
                "private const int ContainerActionSuccessVfxLimit = 10;",
                StringComparison.Ordinal),
            "InventoryActions area success VFX must stay bounded to ten changed containers");

        string actionSource = File.ReadAllText(
            Path.Combine(repositoryRoot, "InventoryActions", "Actions.cs"));
        string limiter = ReadSourceSection(
            actionSource,
            "private static int TryBroadcastChangedContainerActionSuccessVfx",
            "private static void BroadcastContainerActionSuccessFx");
        Assert.True(
            limiter.Contains("played >= limit", StringComparison.Ordinal) &&
            limiter.Contains("ContainerActionSuccessVfxKind", StringComparison.Ordinal) &&
            limiter.Contains("return played + 1;", StringComparison.Ordinal),
            "InventoryActions must stop success VFX at the configured limit and count emitted targets");

        string ownershipSource = File.ReadAllText(
            Path.Combine(repositoryRoot, "InventoryActions", "AreaContainerOwnership.cs"));
        string record = ReadSourceSection(
            ownershipSource,
            "private static void RecordAreaContainerTransfer",
            "private static void FlushAreaTransferInventoriesAfterFailure");
        int movedGuard = record.IndexOf("if (moved <= 0)", StringComparison.Ordinal);
        int broadcast = record.IndexOf(
            "TryBroadcastChangedContainerActionSuccessVfx",
            StringComparison.Ordinal);
        Assert.True(
            movedGuard >= 0 && broadcast > movedGuard,
            "InventoryActions must broadcast VFX only after a positive confirmed move");
        Assert.Equal(
            2,
            CountSourceOccurrences(
                ownershipSource,
                "RecordAreaContainerTransfer(session, target, moved);"));

        string complete = ReadSourceSection(
            ownershipSource,
            "private static void CompleteAreaContainerTransfer",
            "private static void CancelAreaContainerTransfer");
        int clearSession = complete.IndexOf("_areaContainerTransfer = null;", StringComparison.Ordinal);
        int sfx = complete.IndexOf("ContainerActionSuccessSfxKind", StringComparison.Ordinal);
        Assert.True(
            clearSession >= 0 && sfx > clearSession &&
            CountSourceOccurrences(complete, "ContainerActionSuccessSfxKind") == 1,
            "InventoryActions must emit the anchor SFX exactly once after clearing the completed session");
    }

    public static void InventoryActionsContainerActionSuccessFxUsesTransientEverybodyRpc()
    {
        string repositoryRoot = FindRepositoryRoot();
        string actionSource = File.ReadAllText(
            Path.Combine(repositoryRoot, "InventoryActions", "Actions.cs"));
        string broadcaster = ReadSourceSection(
            actionSource,
            "private static void BroadcastContainerActionSuccessFx",
            "private static void RPC_ContainerActionSuccessFx");
        int localFallback = broadcaster.IndexOf(
            "RenderContainerActionSuccessFxLocal(container, effectKind);",
            StringComparison.Ordinal);
        int invokeEverybody = broadcaster.IndexOf("nview.InvokeRPC(", StringComparison.Ordinal);
        Assert.True(
            localFallback >= 0 && invokeEverybody > localFallback &&
            broadcaster.Contains("ZNetView.Everybody", StringComparison.Ordinal) &&
            broadcaster.Contains("ContainerActionSuccessFxRpc", StringComparison.Ordinal),
            "InventoryActions network containers must emit one transient event while local containers retain a fallback");
        Assert.Equal(
            1,
            CountSourceOccurrences(
                broadcaster,
                "RenderContainerActionSuccessFxLocal(container, effectKind);"));
        Assert.False(
            broadcaster.Contains("Object.Instantiate", StringComparison.Ordinal) ||
            broadcaster.Contains("GetZDO", StringComparison.Ordinal) ||
            broadcaster.Contains(".Set(", StringComparison.Ordinal),
            "InventoryActions FX sender must not instantiate or persist a network effect object");

        string pluginSource = File.ReadAllText(
            Path.Combine(repositoryRoot, "InventoryActions", "Plugin.cs"));
        Assert.True(
            pluginSource.Contains(
                "InventoryActions_ContainerActionTransientFxV1",
                StringComparison.Ordinal),
            "InventoryActions must use its own transient FX RPC namespace");

        string ownershipSource = File.ReadAllText(
            Path.Combine(repositoryRoot, "InventoryActions", "AreaContainerOwnership.cs"));
        string registration = ReadSourceSection(
            ownershipSource,
            "internal static void RegisterAreaOwnershipRpcs",
            "internal static void UnregisterAreaOwnershipRpcs");
        Assert.True(
            registration.Contains("Unregister(ContainerActionSuccessFxRpc)", StringComparison.Ordinal) &&
            registration.Contains("Register<int>(", StringComparison.Ordinal) &&
            registration.Contains(
                "RPC_ContainerActionSuccessFx(container, effectKind)",
                StringComparison.Ordinal),
            "every loaded InventoryActions container must replace and register the transient receive handler");
        string unregistration = ReadSourceSection(
            ownershipSource,
            "internal static void UnregisterAreaOwnershipRpcs",
            "private static void RPC_RequestAreaOwnership");
        Assert.True(
            unregistration.Contains(
                "Unregister(ContainerActionSuccessFxRpc)",
                StringComparison.Ordinal),
            "destroyed InventoryActions containers must unregister the transient receive handler");

        string receiver = ReadSourceSection(
            actionSource,
            "private static void RPC_ContainerActionSuccessFx",
            "private static void RenderContainerActionSuccessFxLocal");
        Assert.True(
            receiver.Contains(
                "RenderContainerActionSuccessFxLocal(container, effectKind)",
                StringComparison.Ordinal),
            "the InventoryActions RPC receiver must dispatch only to the local renderer");
        Assert.False(
            receiver.Contains("InvokeRPC", StringComparison.Ordinal) ||
            receiver.Contains("BroadcastContainerActionSuccessFx(", StringComparison.Ordinal),
            "the InventoryActions receive path must never rebroadcast recursively");
    }

    public static void InventoryActionsContainerActionSuccessFxStaysLocalGuardedAndSelfCleaning()
    {
        string repositoryRoot = FindRepositoryRoot();
        string pluginSource = File.ReadAllText(
            Path.Combine(repositoryRoot, "InventoryActions", "Plugin.cs"));
        Assert.True(
            pluginSource.Contains(
                "private const float ContainerActionSuccessFxLifetime = 5f;",
                StringComparison.Ordinal) &&
            pluginSource.Contains(
                "private const float ContainerActionSuccessFxReceiveRange = 64f;",
                StringComparison.Ordinal) &&
            pluginSource.Contains(
                "private const int ContainerActionSuccessFxReceiveLimit = 32;",
                StringComparison.Ordinal) &&
            pluginSource.Contains(
                "private const float ContainerActionSuccessFxReceiveWindow = 1f;",
                StringComparison.Ordinal),
            "InventoryActions transient FX must retain bounded range, rate and cleanup constants");

        string source = File.ReadAllText(
            Path.Combine(repositoryRoot, "InventoryActions", "Actions.cs"));
        string dispatch = ReadSourceSection(
            source,
            "private static void RenderContainerActionSuccessFxLocal",
            "private static bool CanRenderContainerActionSuccessFx");
        int guard = dispatch.IndexOf("CanRenderContainerActionSuccessFx", StringComparison.Ordinal);
        int receiveBudget = dispatch.IndexOf(
            "TryConsumeContainerActionSuccessFxReceiveBudget",
            StringComparison.Ordinal);
        int renderVfx = dispatch.IndexOf("RenderContainerActionSuccessVfxLocal", StringComparison.Ordinal);
        int renderSfx = dispatch.IndexOf("RenderContainerActionSuccessSfxLocal", StringComparison.Ordinal);
        Assert.True(
            guard >= 0 && receiveBudget > guard &&
            renderVfx > receiveBudget && renderSfx > receiveBudget,
            "InventoryActions local rendering must pass the shared guard and bounded budget first");

        string receiverGuard = ReadSourceSection(
            source,
            "private static bool CanRenderContainerActionSuccessFx",
            "private static bool TryConsumeContainerActionSuccessFxReceiveBudget");
        Assert.True(
            receiverGuard.Contains("ContainerActionSuccessVfxKind", StringComparison.Ordinal) &&
            receiverGuard.Contains("ContainerActionSuccessSfxKind", StringComparison.Ordinal) &&
            receiverGuard.Contains("IsDedicatedServer", StringComparison.Ordinal) &&
            receiverGuard.Contains("IsContainerActionSuccessFxEnabled()", StringComparison.Ordinal) &&
            receiverGuard.Contains("Player.m_localPlayer", StringComparison.Ordinal) &&
            receiverGuard.Contains("localPlayer.m_isLoading", StringComparison.Ordinal) &&
            receiverGuard.Contains("offset.sqrMagnitude", StringComparison.Ordinal) &&
            receiverGuard.Contains("ContainerActionSuccessFxReceiveRange", StringComparison.Ordinal),
            "InventoryActions receivers must validate kind, client role, preference, player state and distance");

        string receiveBudgetSource = ReadSourceSection(
            source,
            "private static bool TryConsumeContainerActionSuccessFxReceiveBudget",
            "private static void RenderContainerActionSuccessVfxLocal");
        Assert.True(
            receiveBudgetSource.Contains(
                "_containerActionSuccessFxReceivedInWindow >=",
                StringComparison.Ordinal) &&
            receiveBudgetSource.Contains(
                "_containerActionSuccessFxReceivedInWindow++;",
                StringComparison.Ordinal),
            "a malformed peer must not create unbounded InventoryActions effect objects");

        string localVfx = ReadSourceSection(
            source,
            "private static void RenderContainerActionSuccessVfxLocal",
            "private static void RenderContainerActionSuccessSfxLocal");
        int disableNetworkInit = localVfx.IndexOf(
            "ZNetView.m_forceDisableInit = true;",
            StringComparison.Ordinal);
        int instantiate = localVfx.IndexOf("UnityEngine.Object.Instantiate", StringComparison.Ordinal);
        int restoreNetworkInit = localVfx.IndexOf(
            "ZNetView.m_forceDisableInit = previousForceDisableInit;",
            StringComparison.Ordinal);
        int destroyVfx = localVfx.IndexOf(
            "UnityEngine.Object.Destroy(instance, ContainerActionSuccessFxLifetime);",
            StringComparison.Ordinal);
        Assert.True(
            disableNetworkInit >= 0 && instantiate > disableNetworkInit &&
            restoreNetworkInit > instantiate && destroyVfx > restoreNetworkInit,
            "InventoryActions VFX must remain non-networked locally and always be cleaned up");
        Assert.True(
            localVfx.Contains("sfx.Stop();", StringComparison.Ordinal) &&
            localVfx.Contains("sfx.gameObject.SetActive(false);", StringComparison.Ordinal) &&
            !localVfx.Contains("InvokeRPC", StringComparison.Ordinal),
            "InventoryActions per-container VFX must stay silent and initiate no network traffic");

        int localSfxStart = source.IndexOf(
            "private static void RenderContainerActionSuccessSfxLocal",
            StringComparison.Ordinal);
        Assert.True(localSfxStart >= 0, "InventoryActions local SFX renderer source must exist");
        string localSfx = source.Substring(localSfxStart);
        Assert.True(
            localSfx.Contains(
                "UnityEngine.Object.Destroy(instance, ContainerActionSuccessFxLifetime);",
                StringComparison.Ordinal) &&
            !localSfx.Contains("InvokeRPC", StringComparison.Ordinal),
            "InventoryActions one-shot SFX must stay local and self-cleaning");
    }

    public static void MultiUserItemSnapshotIgnoresCustomDataOrder()
    {
        MultiUserContainerItemSnapshot expected = CreateMultiUserItemSnapshot(
            stack: 3,
            customData:
            [
                new KeyValuePair<string, string>("socket-1", "Ruby"),
                new KeyValuePair<string, string>("socket-2", "Sapphire")
            ]);
        MultiUserContainerItemSnapshot actual = CreateMultiUserItemSnapshot(
            stack: 5,
            customData:
            [
                new KeyValuePair<string, string>("socket-2", "Sapphire"),
                new KeyValuePair<string, string>("socket-1", "Ruby")
            ]);

        Assert.True(
            MultiUserContainerTransferCore.IsExactMatch(expected, actual, requiredStack: 3),
            "custom data insertion order must not change item identity");
    }

    public static void MultiUserItemSnapshotRejectsSocketDataChanges()
    {
        MultiUserContainerItemSnapshot expected = CreateMultiUserItemSnapshot(
            customData:
            [
                new KeyValuePair<string, string>("Jewelcrafting.Sockets", "Ruby")
            ]);
        MultiUserContainerItemSnapshot actual = CreateMultiUserItemSnapshot(
            customData:
            [
                new KeyValuePair<string, string>("Jewelcrafting.Sockets", "Emerald")
            ]);

        Assert.False(
            MultiUserContainerTransferCore.IsExactMatch(expected, actual, requiredStack: 1),
            "socketed items with different custom data must not be treated as the same item");
    }

    public static void BeingSpoiledSignedClocksRemainStackCompatible()
    {
        StackMetadataPolicy.SetWorldTicksProvider(() => 1_000L);
        MultiUserContainerItemSnapshot incoming = CreateMultiUserItemSnapshot(
            stack: 3,
            customData:
            [
                new KeyValuePair<string, string>(
                    StackMetadataPolicy.BeingSpoiledExpiryWorldTicksKey,
                    "1600")
            ]);
        MultiUserContainerItemSnapshot target = CreateMultiUserItemSnapshot(
            stack: 5,
            customData:
            [
                new KeyValuePair<string, string>(
                    StackMetadataPolicy.BeingSpoiledExpiryWorldTicksKey,
                    "-400")
            ]);

        Assert.True(
            MultiUserContainerTransferCore.CanStackTogether(
                incoming,
                target,
                requiredStack: 1),
            "running and paused clocks must remain stack compatible when world time is available");
        Assert.False(
            MultiUserContainerTransferCore.IsExactMatch(
                incoming,
                target,
                requiredStack: 1),
            "optimistic concurrency snapshots must still compare the stored expiry exactly");

        StackMetadataPolicy.SetWorldTicksProvider(() => null);
        Assert.False(
            MultiUserContainerTransferCore.CanStackTogether(
                incoming,
                target,
                requiredStack: 1),
            "cross-state clocks must not merge before a common server clock is available");

        MultiUserContainerItemSnapshot anotherRunning = CreateMultiUserItemSnapshot(
            stack: 1,
            customData:
            [
                new KeyValuePair<string, string>(
                    StackMetadataPolicy.BeingSpoiledExpiryWorldTicksKey,
                    "1200")
            ]);
        Assert.True(
            MultiUserContainerTransferCore.CanStackTogether(
                incoming,
                anotherRunning,
                requiredStack: 1),
            "two running clocks remain comparable without reading world time");
    }

    public static void StackMetadataPolicyPreservesOtherCustomDataIdentity()
    {
        Dictionary<string, string> ruby = new(StringComparer.Ordinal)
        {
            [StackMetadataPolicy.BeingSpoiledExpiryWorldTicksKey] = "900",
            ["Jewelcrafting.Sockets"] = "Ruby"
        };
        Dictionary<string, string> emerald = new(StringComparer.Ordinal)
        {
            [StackMetadataPolicy.BeingSpoiledExpiryWorldTicksKey] = "400",
            ["Jewelcrafting.Sockets"] = "Emerald"
        };

        Assert.False(
            StackMetadataPolicy.AreCompatible(ruby, emerald),
            "only the BeingSpoiled expiry key may differ");

        MultiUserContainerItemSnapshot rubySnapshot = CreateMultiUserItemSnapshot(
            customData: ruby);
        MultiUserContainerItemSnapshot emeraldSnapshot = CreateMultiUserItemSnapshot(
            customData: emerald);
        Assert.False(
            MultiUserContainerTransferCore.CanStackTogether(
                rubySnapshot,
                emeraldSnapshot,
                requiredStack: 1),
            "multi-user stacking must not weaken socket/custom-data identity");
    }

    public static void BeingSpoiledSignedClockMergePreservesDestinationState()
    {
        StackMetadataPolicy.SetWorldTicksProvider(() => 1_000L);

        Dictionary<string, string> runningDestination = new(StringComparer.Ordinal)
        {
            [StackMetadataPolicy.BeingSpoiledExpiryWorldTicksKey] = "1600"
        };
        Dictionary<string, string> pausedSource = new(StringComparer.Ordinal)
        {
            [StackMetadataPolicy.BeingSpoiledExpiryWorldTicksKey] = "-400"
        };

        Assert.True(
            StackMetadataPolicy.MergeInto(runningDestination, pausedSource),
            "a shorter paused source must update a running destination");
        Assert.Equal(
            "1400",
            runningDestination[StackMetadataPolicy.BeingSpoiledExpiryWorldTicksKey]);

        Dictionary<string, string> pausedDestination = new(StringComparer.Ordinal)
        {
            [StackMetadataPolicy.BeingSpoiledExpiryWorldTicksKey] = "-600"
        };
        Dictionary<string, string> runningSource = new(StringComparer.Ordinal)
        {
            [StackMetadataPolicy.BeingSpoiledExpiryWorldTicksKey] = "1300"
        };
        StackMetadataPolicy.MergeInto(pausedDestination, runningSource);
        Assert.Equal(
            "-300",
            pausedDestination[StackMetadataPolicy.BeingSpoiledExpiryWorldTicksKey]);

        runningSource[StackMetadataPolicy.BeingSpoiledExpiryWorldTicksKey] = "900";
        StackMetadataPolicy.MergeInto(pausedDestination, runningSource);
        Assert.Equal(
            "1000",
            pausedDestination[StackMetadataPolicy.BeingSpoiledExpiryWorldTicksKey]);

        Dictionary<string, string> pausedPairDestination = new(StringComparer.Ordinal)
        {
            [StackMetadataPolicy.BeingSpoiledExpiryWorldTicksKey] = "-900"
        };
        StackMetadataPolicy.MergeInto(pausedPairDestination, pausedSource);
        Assert.Equal(
            "-400",
            pausedPairDestination[StackMetadataPolicy.BeingSpoiledExpiryWorldTicksKey]);

        Dictionary<string, string> runningPairDestination = new(StringComparer.Ordinal)
        {
            [StackMetadataPolicy.BeingSpoiledExpiryWorldTicksKey] = "1900"
        };
        Dictionary<string, string> runningPairSource = new(StringComparer.Ordinal)
        {
            [StackMetadataPolicy.BeingSpoiledExpiryWorldTicksKey] = "1400"
        };
        StackMetadataPolicy.MergeInto(runningPairDestination, runningPairSource);
        Assert.Equal(
            "1400",
            runningPairDestination[StackMetadataPolicy.BeingSpoiledExpiryWorldTicksKey]);
    }

    public static void BeingSpoiledSignedClockValidatesMissingAndMalformedValues()
    {
        StackMetadataPolicy.SetWorldTicksProvider(() => 1_000L);

        Dictionary<string, string> cleanDestination = new(StringComparer.Ordinal);
        Dictionary<string, string> pausedSource = new(StringComparer.Ordinal)
        {
            [StackMetadataPolicy.BeingSpoiledExpiryWorldTicksKey] = "-400"
        };
        StackMetadataPolicy.MergeInto(cleanDestination, pausedSource);
        Assert.Equal(
            "-400",
            cleanDestination[StackMetadataPolicy.BeingSpoiledExpiryWorldTicksKey]);

        Dictionary<string, string> malformedDestination = new(StringComparer.Ordinal)
        {
            [StackMetadataPolicy.BeingSpoiledExpiryWorldTicksKey] = "not-ticks"
        };
        Assert.False(
            StackMetadataPolicy.AreCompatible(malformedDestination, pausedSource),
            "a future or malformed destination format must not merge with a signed clock");
        StackMetadataPolicy.MergeInto(malformedDestination, pausedSource);
        Assert.Equal(
            "not-ticks",
            malformedDestination[StackMetadataPolicy.BeingSpoiledExpiryWorldTicksKey]);

        Dictionary<string, string> malformedSource = new(StringComparer.Ordinal)
        {
            [StackMetadataPolicy.BeingSpoiledExpiryWorldTicksKey] = "+400"
        };
        Dictionary<string, string> invalidSourceDestination = new(StringComparer.Ordinal);
        StackMetadataPolicy.MergeInto(invalidSourceDestination, malformedSource);
        Assert.False(
            invalidSourceDestination.ContainsKey(
                StackMetadataPolicy.BeingSpoiledExpiryWorldTicksKey),
            "non-canonical Int64 strings must not propagate to another stack");

        Dictionary<string, string> nonPositiveSource = new(StringComparer.Ordinal)
        {
            [StackMetadataPolicy.BeingSpoiledExpiryWorldTicksKey] = "0"
        };
        StackMetadataPolicy.MergeInto(invalidSourceDestination, nonPositiveSource);
        Assert.False(
            invalidSourceDestination.ContainsKey(
                StackMetadataPolicy.BeingSpoiledExpiryWorldTicksKey),
            "zero is not a valid BeingSpoiled expiry and must not propagate");

        nonPositiveSource[StackMetadataPolicy.BeingSpoiledExpiryWorldTicksKey] =
            long.MinValue.ToString(System.Globalization.CultureInfo.InvariantCulture);
        StackMetadataPolicy.MergeInto(invalidSourceDestination, nonPositiveSource);
        Assert.False(
            invalidSourceDestination.ContainsKey(
                StackMetadataPolicy.BeingSpoiledExpiryWorldTicksKey),
            "long.MinValue cannot be negated into paused remaining ticks");

        Dictionary<string, string> validDestination = new(StringComparer.Ordinal)
        {
            [StackMetadataPolicy.BeingSpoiledExpiryWorldTicksKey] = "-900"
        };
        StackMetadataPolicy.MergeInto(validDestination, nonPositiveSource);
        Assert.Equal(
            "-900",
            validDestination[StackMetadataPolicy.BeingSpoiledExpiryWorldTicksKey]);

        Assert.True(
            StackMetadataPolicy.TryParseCanonicalBeingSpoiledClock("-1", out long parsed) && parsed == -1L,
            "negative non-MinValue clocks are valid paused durations");
    }

    public static void BeingSpoiledPartialMergeLeavesSourceClockUnchanged()
    {
        StackMetadataPolicy.SetWorldTicksProvider(() => 1_000L);
        Dictionary<string, string> destination = new(StringComparer.Ordinal)
        {
            [StackMetadataPolicy.BeingSpoiledExpiryWorldTicksKey] = "1600"
        };
        Dictionary<string, string> partiallyConsumedSource = new(StringComparer.Ordinal)
        {
            [StackMetadataPolicy.BeingSpoiledExpiryWorldTicksKey] = "-400"
        };

        StackMetadataPolicy.MergeInto(destination, partiallyConsumedSource);

        Assert.Equal(
            "1400",
            destination[StackMetadataPolicy.BeingSpoiledExpiryWorldTicksKey]);
        Assert.Equal(
            "-400",
            partiallyConsumedSource[StackMetadataPolicy.BeingSpoiledExpiryWorldTicksKey]);
    }

    public static void BeingSpoiledRegistrationReplacesOnlyTheFallback()
    {
        Assert.True(
            StackMetadataPolicy.Register(
                StackMetadataPolicy.BeingSpoiledExpiryWorldTicksKey,
                (destinationValue, sourceValue) =>
                    destinationValue == null && sourceValue == null ? null : "-77"),
            "BeingSpoiled must be able to replace the built-in fallback regardless of load order");
        Assert.False(
            StackMetadataPolicy.Register(
                StackMetadataPolicy.BeingSpoiledExpiryWorldTicksKey,
                (destinationValue, sourceValue) =>
                    destinationValue == null && sourceValue == null ? null : "-55"),
            "the first authoritative registration must remain installed");

        Dictionary<string, string> destination = new(StringComparer.Ordinal)
        {
            [StackMetadataPolicy.BeingSpoiledExpiryWorldTicksKey] = "1200"
        };
        Dictionary<string, string> source = new(StringComparer.Ordinal)
        {
            [StackMetadataPolicy.BeingSpoiledExpiryWorldTicksKey] = "-400"
        };
        StackMetadataPolicy.MergeInto(destination, source);
        Assert.Equal(
            "-77",
            destination[StackMetadataPolicy.BeingSpoiledExpiryWorldTicksKey]);
    }

    public static void MultiUserItemSnapshotRejectsInsufficientStack()
    {
        MultiUserContainerItemSnapshot expected = CreateMultiUserItemSnapshot(stack: 5);
        MultiUserContainerItemSnapshot insufficientActual = CreateMultiUserItemSnapshot(stack: 2);
        MultiUserContainerItemSnapshot malformedExpected = CreateMultiUserItemSnapshot(stack: 2);
        MultiUserContainerItemSnapshot sufficientActual = CreateMultiUserItemSnapshot(stack: 5);

        Assert.False(
            MultiUserContainerTransferCore.IsExactMatch(expected, insufficientActual, requiredStack: 3),
            "the current item must still contain the requested amount");
        Assert.False(
            MultiUserContainerTransferCore.IsExactMatch(malformedExpected, sufficientActual, requiredStack: 3),
            "the captured item must have contained the requested amount");
    }

    public static void MultiUserItemSnapshotRejectsIdentityFieldChanges()
    {
        MultiUserContainerItemSnapshot expected = CreateMultiUserItemSnapshot();
        MultiUserContainerItemSnapshot[] mismatches =
        [
            CreateMultiUserItemSnapshot(prefabName: "ArmorFenringChest"),
            CreateMultiUserItemSnapshot(quality: 4),
            CreateMultiUserItemSnapshot(variant: 3),
            CreateMultiUserItemSnapshot(worldLevel: 2),
            CreateMultiUserItemSnapshot(crafterId: 99),
            CreateMultiUserItemSnapshot(crafterName: "Other crafter"),
            CreateMultiUserItemSnapshot(durability: 49.5f),
            CreateMultiUserItemSnapshot(pickedUp: false)
        ];

        foreach (MultiUserContainerItemSnapshot mismatch in mismatches)
        {
            Assert.False(
                MultiUserContainerTransferCore.IsExactMatch(expected, mismatch, requiredStack: 1),
                "every serialized identity field must participate in exact matching");
        }

        float negativeZero = BitConverter.Int32BitsToSingle(unchecked((int)0x80000000));
        MultiUserContainerItemSnapshot positiveZero = CreateMultiUserItemSnapshot(durability: 0f);
        MultiUserContainerItemSnapshot negativeZeroSnapshot = CreateMultiUserItemSnapshot(durability: negativeZero);
        Assert.False(
            MultiUserContainerTransferCore.IsExactMatch(positiveZero, negativeZeroSnapshot, requiredStack: 1),
            "durability must be compared by its serialized bit value");
    }

    public static void MultiUserTransferRequiresExactPreMutationStackState()
    {
        Assert.True(
            MultiUserContainerTransferCore.MatchesExpectedStackState(0, null),
            "an empty target must still be empty");
        Assert.False(
            MultiUserContainerTransferCore.MatchesExpectedStackState(0, 1),
            "a retry must not reuse a target populated by the first application");
        Assert.True(
            MultiUserContainerTransferCore.MatchesExpectedStackState(5, 5),
            "an unchanged populated stack must match");
        Assert.False(
            MultiUserContainerTransferCore.MatchesExpectedStackState(5, 3),
            "a partially consumed source must reject a repeated removal");
        Assert.False(
            MultiUserContainerTransferCore.MatchesExpectedStackState(5, 7),
            "a target changed by an earlier add must reject a repeated add");
        Assert.False(
            MultiUserContainerTransferCore.MatchesExpectedStackState(-1, null),
            "negative sentinels are not valid mutation preconditions");
    }

    public static void MultiUserRequestPreparationKeepsEscrowBehindPublishedPending()
    {
        string source = File.ReadAllText(
            Path.Combine(FindRepositoryRoot(), "MultiUserContainerOperations.cs"));
        string preparation = ReadSourceSection(
            source,
            "private static bool TryStartPreparedMultiUserContainerRequest",
            "private static MultiUserContainerRequest CreateMultiUserContainerRequest");

        int publishPending = preparation.IndexOf(
            "_pendingMultiUserContainerTransfer = pending;",
            StringComparison.Ordinal);
        int markPublished = preparation.IndexOf(
            "pendingPublished = true;",
            StringComparison.Ordinal);
        int send = preparation.IndexOf(
            "container.m_nview.InvokeRPC(",
            StringComparison.Ordinal);
        Assert.True(
            publishPending >= 0 && markPublished > publishPending && send > markPublished,
            "escrow must be represented by a published pending before the uncertain network send begins");

        Assert.True(
            preparation.Contains("if (!pendingPublished", StringComparison.Ordinal) &&
            CountSourceOccurrences(
                preparation,
                "RestoreMultiUserContainerLocalEscrow(") == 1,
            "all preparation failures must converge on one pre-publication escrow rollback");
        Assert.True(
            preparation.Contains(
                "initial request send failed; retrying",
                StringComparison.Ordinal) &&
            preparation.Contains("return true;", StringComparison.Ordinal),
            "an uncertain initial send must retain the pending for receipt polling and retry instead of restoring escrow");

        string runtime = ReadSourceSection(
            source,
            "internal static void UpdateMultiUserContainerRuntime",
            "internal static void ShutdownMultiUserContainerRuntime");
        Assert.True(
            runtime.Contains("request resend failed; retrying", StringComparison.Ordinal) &&
            runtime.Contains("new ZPackage(pending.RequestBytes)", StringComparison.Ordinal),
            "bounded resend failures must preserve the serialized pending request without aborting the Update cycle");
    }

    public static void InventoryActionsCurrentContainerTransfersNotifyOnlyAfterMovement()
    {
        string source = File.ReadAllText(
            Path.Combine(FindRepositoryRoot(), "InventoryActions", "Actions.cs"));
        string quickStack = ReadSourceSection(
            source,
            "private static void QuickStackIntoContainers",
            "private static bool ShouldQuickStackItem");
        string restock = ReadSourceSection(
            source,
            "private static void RestockFromContainer",
            "private static bool ShouldTakeStacksTarget");

        foreach (string transfer in new[] { quickStack, restock })
        {
            int movedGuard = transfer.IndexOf("if (moved", StringComparison.Ordinal);
            int changed = transfer.IndexOf("playerInventory.Changed();", StringComparison.Ordinal);
            Assert.True(
                movedGuard >= 0 && changed > movedGuard,
                "current-container transfers must notify the player inventory only after a positive move");
            Assert.False(
                transfer.Contains("ContainerTransferCore", StringComparison.Ordinal),
                "a one-container action must not route through the multi-container delegate wrapper");
        }

        string quickStackSelection = ReadSourceSection(
            source,
            "private static List<ItemData> GetQuickStackCandidates",
            "private static int QuickStackItemsIntoContainer");
        string restockSelection = ReadSourceSection(
            source,
            "private static List<ItemData> GetRestockTargets",
            "private static int RestockTargetsFromContainer");
        Assert.True(
            quickStackSelection.Contains("ShouldQuickStackItem", StringComparison.Ordinal) &&
            restockSelection.Contains("ShouldTakeStacksTarget", StringComparison.Ordinal),
            "current and area transfers must build candidates in shared selectors");

        string ownershipSource = File.ReadAllText(
            Path.Combine(FindRepositoryRoot(), "InventoryActions", "AreaContainerOwnership.cs"));
        string areaMutation = ReadSourceSection(
            ownershipSource,
            "private static int ExecuteAreaContainerTransfer",
            "private static void RecordAreaContainerTransfer");
        Assert.False(
            areaMutation.Contains(".Where(", StringComparison.Ordinal) ||
            areaMutation.Contains(".Sort(", StringComparison.Ordinal),
            "the area path must not duplicate candidate eligibility or ordering");
    }

    public static void InventoryActionsAreaCleanupCommitsStateBeforeCallbacks()
    {
        string source = File.ReadAllText(
            Path.Combine(FindRepositoryRoot(), "InventoryActions", "AreaContainerOwnership.cs"));
        string execute = ReadSourceSection(
            source,
            "private static void ExecuteGrantedAreaContainer",
            "private static bool CanExecuteGrantedAreaContainer");
        Assert.True(
            execute.IndexOf("AreaOwnershipHandoff.CompleteExecution();", StringComparison.Ordinal) <
            execute.IndexOf("ClearAreaOwnershipLeaseIfMatching(", StringComparison.Ordinal),
            "an executed handoff must become terminal before best-effort lease cleanup");

        string skipped = ReadSourceSection(
            source,
            "private static void FinishPendingAreaContainerWithoutMutation",
            "private static void CancelAreaContainerTransfer");
        Assert.True(
            skipped.IndexOf("AreaOwnershipHandoff.Cancel();", StringComparison.Ordinal) <
            skipped.IndexOf("ClearAreaOwnershipLeaseIfMatching(", StringComparison.Ordinal) &&
            skipped.IndexOf("session.NextTargetIndex++;", StringComparison.Ordinal) <
            skipped.IndexOf("ClearAreaOwnershipLeaseIfMatching(", StringComparison.Ordinal),
            "a skipped target must advance and clear pending state before external cleanup");

        string cancel = ReadSourceSection(
            source,
            "private static void CancelAreaContainerTransfer",
            "internal static void RegisterAreaOwnershipRpcs");
        int clearSession = cancel.IndexOf("_areaContainerTransfer = null;", StringComparison.Ordinal);
        int clearLease = cancel.IndexOf("ClearAreaOwnershipLeaseIfMatching(", StringComparison.Ordinal);
        int notifyInventory = cancel.IndexOf("changedInventory.Changed();", StringComparison.Ordinal);
        Assert.True(
            clearSession >= 0 && clearLease > clearSession && notifyInventory > clearSession,
            "cancellation must clear the session before lease or inventory callbacks can throw");
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

    public static void AreaOwnershipHandoffExecutesMatchingGrantOnce()
    {
        InventoryActions.AreaOwnershipHandoffCore core = new();
        InventoryActions.AreaOwnershipRequestIdentity identity =
            AreaOwnershipIdentity();

        Assert.True(
            core.TryBegin(identity, expectedResponderUid: 40L, responseDeadlineAt: 2f),
            "first handoff should begin");
        Assert.Equal(
            InventoryActions.AreaOwnershipHandoffDecision.None,
            core.Observe(
                now: 0.5f,
                loaded: true,
                InventoryActions.AreaOwnershipObservedOwner.LocalRequester,
                netViewIsOwner: true,
                InventoryActions.AreaOwnershipGrantTokenStatus.Missing,
                canExecute: true));
        Assert.Equal(
            InventoryActions.AreaOwnershipHandoffDecision.None,
            core.ReceiveResponse(
                identity,
                senderUid: 40L,
                granted: true,
                grantToken: 99L,
                now: 1f,
                ownershipDeadlineAt: 3f));
        Assert.Equal(
            InventoryActions.AreaOwnershipHandoffDecision.None,
            core.Observe(
                now: 1.1f,
                loaded: true,
                InventoryActions.AreaOwnershipObservedOwner.LocalRequester,
                netViewIsOwner: false,
                InventoryActions.AreaOwnershipGrantTokenStatus.Matching,
                canExecute: true));
        Assert.Equal(
            InventoryActions.AreaOwnershipHandoffDecision.Execute,
            core.Observe(
                now: 1.2f,
                loaded: true,
                InventoryActions.AreaOwnershipObservedOwner.LocalRequester,
                netViewIsOwner: true,
                InventoryActions.AreaOwnershipGrantTokenStatus.Matching,
                canExecute: true));
        Assert.Equal(
            InventoryActions.AreaOwnershipHandoffDecision.None,
            core.Observe(
                now: 1.3f,
                loaded: true,
                InventoryActions.AreaOwnershipObservedOwner.LocalRequester,
                netViewIsOwner: true,
                InventoryActions.AreaOwnershipGrantTokenStatus.Matching,
                canExecute: true));
        Assert.Equal(
            InventoryActions.AreaOwnershipHandoffDecision.None,
            core.ReceiveResponse(
                identity,
                senderUid: 40L,
                granted: true,
                grantToken: 99L,
                now: 1.4f,
                ownershipDeadlineAt: 10f));

        core.CompleteExecution();
        Assert.Equal(
            InventoryActions.AreaOwnershipHandoffPhase.Idle,
            core.Phase);
    }

    public static void AreaOwnershipHandoffIgnoresMismatchedResponses()
    {
        InventoryActions.AreaOwnershipHandoffCore core = new();
        InventoryActions.AreaOwnershipRequestIdentity identity =
            AreaOwnershipIdentity();
        InventoryActions.AreaOwnershipRequestIdentity wrongIdentity =
            new(
                identity.RequestId + 1,
                identity.ContainerUserId,
                identity.ContainerObjectId,
                identity.Action);
        Assert.True(
            core.TryBegin(identity, expectedResponderUid: 40L, responseDeadlineAt: 5f),
            "handoff should begin");

        Assert.Equal(
            InventoryActions.AreaOwnershipHandoffDecision.None,
            core.ReceiveResponse(
                wrongIdentity,
                senderUid: 40L,
                granted: false,
                grantToken: 0L,
                now: 1f,
                ownershipDeadlineAt: 2f));
        Assert.Equal(
            InventoryActions.AreaOwnershipHandoffDecision.None,
            core.ReceiveResponse(
                identity,
                senderUid: 41L,
                granted: false,
                grantToken: 0L,
                now: 1f,
                ownershipDeadlineAt: 2f));
        Assert.Equal(
            InventoryActions.AreaOwnershipHandoffPhase.AwaitingResponse,
            core.Phase);
        Assert.Equal(
            InventoryActions.AreaOwnershipHandoffDecision.Denied,
            core.ReceiveResponse(
                identity,
                senderUid: 40L,
                granted: false,
                grantToken: 0L,
                now: 1f,
                ownershipDeadlineAt: 2f));
        Assert.Equal(
            InventoryActions.AreaOwnershipHandoffPhase.Idle,
            core.Phase);
    }

    public static void AreaOwnershipHandoffRejectsLateResponses()
    {
        InventoryActions.AreaOwnershipRequestIdentity identity =
            AreaOwnershipIdentity();
        InventoryActions.AreaOwnershipHandoffCore responseTimeout = new();
        Assert.True(
            responseTimeout.TryBegin(
                identity,
                expectedResponderUid: 40L,
                responseDeadlineAt: 2f),
            "response timeout handoff should begin");
        Assert.Equal(
            InventoryActions.AreaOwnershipHandoffDecision.Timeout,
            responseTimeout.Observe(
                now: 2f,
                loaded: true,
                InventoryActions.AreaOwnershipObservedOwner.ExpectedResponder,
                netViewIsOwner: false,
                InventoryActions.AreaOwnershipGrantTokenStatus.Missing,
                canExecute: false));
        Assert.Equal(
            InventoryActions.AreaOwnershipHandoffDecision.None,
            responseTimeout.ReceiveResponse(
                identity,
                senderUid: 40L,
                granted: true,
                grantToken: 99L,
                now: 2.1f,
                ownershipDeadlineAt: 5f));

        InventoryActions.AreaOwnershipHandoffCore ownershipTimeout = new();
        Assert.True(
            ownershipTimeout.TryBegin(
                identity,
                expectedResponderUid: 40L,
                responseDeadlineAt: 2f),
            "ownership timeout handoff should begin");
        _ = ownershipTimeout.ReceiveResponse(
            identity,
            senderUid: 40L,
            granted: true,
            grantToken: 99L,
            now: 1f,
            ownershipDeadlineAt: 3f);
        Assert.Equal(
            InventoryActions.AreaOwnershipHandoffDecision.Timeout,
            ownershipTimeout.Observe(
                now: 3f,
                loaded: true,
                InventoryActions.AreaOwnershipObservedOwner.ExpectedResponder,
                netViewIsOwner: false,
                InventoryActions.AreaOwnershipGrantTokenStatus.Missing,
                canExecute: false));
    }

    public static void AreaOwnershipHandoffDuplicateGrantDoesNotExtendDeadline()
    {
        InventoryActions.AreaOwnershipHandoffCore core = new();
        InventoryActions.AreaOwnershipRequestIdentity identity =
            AreaOwnershipIdentity();
        Assert.True(
            core.TryBegin(identity, expectedResponderUid: 40L, responseDeadlineAt: 2f),
            "handoff should begin");
        _ = core.ReceiveResponse(
            identity,
            senderUid: 40L,
            granted: true,
            grantToken: 99L,
            now: 1f,
            ownershipDeadlineAt: 3f);
        Assert.Equal(
            InventoryActions.AreaOwnershipHandoffDecision.None,
            core.ReceiveResponse(
                identity,
                senderUid: 40L,
                granted: true,
                grantToken: 99L,
                now: 2f,
                ownershipDeadlineAt: 100f));
        Assert.Equal(
            InventoryActions.AreaOwnershipHandoffDecision.Timeout,
            core.Observe(
                now: 3f,
                loaded: true,
                InventoryActions.AreaOwnershipObservedOwner.ExpectedResponder,
                netViewIsOwner: false,
                InventoryActions.AreaOwnershipGrantTokenStatus.Missing,
                canExecute: false));
    }

    public static void AreaOwnershipHandoffFailsClosedOnOwnerAndTokenRaces()
    {
        InventoryActions.AreaOwnershipRequestIdentity identity =
            AreaOwnershipIdentity();
        InventoryActions.AreaOwnershipHandoffCore ownerChanged =
            GrantedAreaOwnershipCore(identity);
        Assert.Equal(
            InventoryActions.AreaOwnershipHandoffDecision.OwnerChanged,
            ownerChanged.Observe(
                now: 1.5f,
                loaded: true,
                InventoryActions.AreaOwnershipObservedOwner.Other,
                netViewIsOwner: false,
                InventoryActions.AreaOwnershipGrantTokenStatus.Missing,
                canExecute: false));

        InventoryActions.AreaOwnershipHandoffCore tokenChanged =
            GrantedAreaOwnershipCore(identity);
        Assert.Equal(
            InventoryActions.AreaOwnershipHandoffDecision.GrantReplaced,
            tokenChanged.Observe(
                now: 1.5f,
                loaded: true,
                InventoryActions.AreaOwnershipObservedOwner.LocalRequester,
                netViewIsOwner: true,
                InventoryActions.AreaOwnershipGrantTokenStatus.Other,
                canExecute: true));
    }

    public static void AreaOwnershipHandoffFailsClosedOnUnload()
    {
        InventoryActions.AreaOwnershipRequestIdentity identity =
            AreaOwnershipIdentity();
        InventoryActions.AreaOwnershipHandoffCore beforeResponse = new();
        Assert.True(
            beforeResponse.TryBegin(
                identity,
                expectedResponderUid: 40L,
                responseDeadlineAt: 5f),
            "handoff should begin");
        Assert.Equal(
            InventoryActions.AreaOwnershipHandoffDecision.Unloaded,
            beforeResponse.Observe(
                now: 1f,
                loaded: false,
                InventoryActions.AreaOwnershipObservedOwner.Unknown,
                netViewIsOwner: false,
                InventoryActions.AreaOwnershipGrantTokenStatus.Missing,
                canExecute: false));

        InventoryActions.AreaOwnershipHandoffCore afterGrant =
            GrantedAreaOwnershipCore(identity);
        Assert.Equal(
            InventoryActions.AreaOwnershipHandoffDecision.Unloaded,
            afterGrant.Observe(
                now: 1.5f,
                loaded: false,
                InventoryActions.AreaOwnershipObservedOwner.Unknown,
                netViewIsOwner: false,
                InventoryActions.AreaOwnershipGrantTokenStatus.Missing,
                canExecute: false));
    }

    public static void AreaOwnershipHandoffEnforcesSerialExecutionPreconditions()
    {
        InventoryActions.AreaOwnershipRequestIdentity identity =
            AreaOwnershipIdentity();
        InventoryActions.AreaOwnershipHandoffCore core =
            GrantedAreaOwnershipCore(identity);
        Assert.False(
            core.TryBegin(
                new InventoryActions.AreaOwnershipRequestIdentity(
                    2,
                    10L,
                    21U,
                    InventoryActions.AreaContainerActionKind.Restock),
                expectedResponderUid: 41L,
                responseDeadlineAt: 5f),
            "a second target must not begin while a grant is active");
        Assert.Equal(
            InventoryActions.AreaOwnershipHandoffDecision.Unavailable,
            core.Observe(
                now: 1.5f,
                loaded: true,
                InventoryActions.AreaOwnershipObservedOwner.LocalRequester,
                netViewIsOwner: true,
                InventoryActions.AreaOwnershipGrantTokenStatus.Matching,
                canExecute: false));
        Assert.True(
            core.TryBegin(
                new InventoryActions.AreaOwnershipRequestIdentity(
                    2,
                    10L,
                    21U,
                    InventoryActions.AreaContainerActionKind.Restock),
                expectedResponderUid: 41L,
                responseDeadlineAt: 5f),
            "the next target may begin only after the prior request terminates");
    }

    private static InventoryActions.AreaOwnershipRequestIdentity AreaOwnershipIdentity() =>
        new(
            requestId: 1,
            containerUserId: 10L,
            containerObjectId: 20U,
            InventoryActions.AreaContainerActionKind.QuickStack);

    private static InventoryActions.AreaOwnershipHandoffCore GrantedAreaOwnershipCore(
        InventoryActions.AreaOwnershipRequestIdentity identity)
    {
        InventoryActions.AreaOwnershipHandoffCore core = new();
        Assert.True(
            core.TryBegin(
                identity,
                expectedResponderUid: 40L,
                responseDeadlineAt: 2f),
            "handoff should begin");
        Assert.Equal(
            InventoryActions.AreaOwnershipHandoffDecision.None,
            core.ReceiveResponse(
                identity,
                senderUid: 40L,
                granted: true,
                grantToken: 99L,
                now: 1f,
                ownershipDeadlineAt: 3f));
        return core;
    }

    public static void RestockTargetLimitsParseConfigEntries()
    {
        Dictionary<string, int> limits = RestockTargetLimitCore.Parse("Stone: 10, Coins = 500; BadEntry; Wood: -5 # comment");

        Assert.Equal(3, limits.Count);
        Assert.Equal(10, limits["stone"]);
        Assert.Equal(500, limits["coins"]);
        Assert.Equal(0, limits["wood"]);
    }

    public static void RestockTargetLimitEditorNormalizationPreservesRuntimeMeaning()
    {
        string slotsAmount = RestockTargetLimitCore.NormalizeAmountForEditor(" -5 ");
        string actionsAmount = InventoryActions.RestockTargetLimitCore.NormalizeAmountForEditor(" -5 ");

        Assert.Equal("0", slotsAmount);
        Assert.Equal(slotsAmount, actionsAmount);
        Assert.Equal("7", RestockTargetLimitCore.NormalizeAmountForEditor(" +7 "));
        Assert.Equal("", RestockTargetLimitCore.NormalizeAmountForEditor("invalid"));
        Assert.Equal("", RestockTargetLimitCore.NormalizeAmountForEditor("2147483648"));

        Assert.Equal(
            RestockTargetLimitCore.Parse("Wood: -5")["wood"],
            RestockTargetLimitCore.Parse($"Wood: {slotsAmount}")["wood"]);
        Assert.Equal(
            InventoryActions.RestockTargetLimitCore.Parse("Wood: -5")["wood"],
            InventoryActions.RestockTargetLimitCore.Parse($"Wood: {actionsAmount}")["wood"]);
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
    }

    public static void IntentionalInventoryActionsSourceCopiesStaySynchronized()
    {
        string repositoryRoot = FindRepositoryRoot();
        (string Main, string Copy)[] copiedSources =
        {
            ("ContainerActionCore.cs", "InventoryActions/ContainerActionCore.cs"),
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

    private static string ReadSourceSection(string source, string startMarker, string endMarker)
    {
        int start = source.IndexOf(startMarker, StringComparison.Ordinal);
        int end = start < 0 ? -1 : source.IndexOf(endMarker, start, StringComparison.Ordinal);
        if (start < 0 || end <= start)
        {
            throw new InvalidOperationException($"Could not locate source section '{startMarker}'.");
        }

        return source.Substring(start, end - start);
    }

    private static int CountSourceOccurrences(string source, string token)
    {
        int count = 0;
        int offset = 0;
        while (offset < source.Length)
        {
            int index = source.IndexOf(token, offset, StringComparison.Ordinal);
            if (index < 0)
            {
                break;
            }

            count++;
            offset = index + token.Length;
        }

        return count;
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

    private static MultiUserContainerItemSnapshot CreateMultiUserItemSnapshot(
        string prefabName = "ArmorCarapaceChest",
        int quality = 3,
        int variant = 2,
        int worldLevel = 1,
        long crafterId = 42,
        string crafterName = "Crafter",
        float durability = 50f,
        bool pickedUp = true,
        int stack = 1,
        IEnumerable<KeyValuePair<string, string>>? customData = null) =>
        new(
            prefabName,
            quality,
            variant,
            worldLevel,
            crafterId,
            crafterName,
            durability,
            pickedUp,
            stack,
            customData);

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
