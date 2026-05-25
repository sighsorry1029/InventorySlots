using System;
using System.Text;
using Requirement = Piece.Requirement;

namespace InventorySlots;

public sealed partial class InventorySlotsPlugin
{
    private static bool TryGetVeiledRecipesApi(out VeiledRecipesApi? api)
    {
        const string capability = "VeiledRecipes recipe masking";
        return TryGetCompatApi(
            VeiledRecipesGuid,
            capability,
            CompatRuntime.VeiledRecipes,
            VeiledRecipesApi.TryCreate,
            "VeiledRecipes recipe masking compatibility disabled",
            out api);
    }

    private static bool IsVeiledRecipeMasked(InventoryGui.RecipeDataPair pair)
    {
        Player? player = Player.m_localPlayer;
        return player != null &&
               TryGetVeiledRecipesApi(out VeiledRecipesApi? api) &&
               api != null &&
               api.ShouldMaskRecipePair(player, pair);
    }

    private static bool IsVeiledRecipePreview(InventoryGui.RecipeDataPair pair)
    {
        Player? player = Player.m_localPlayer;
        if (player == null ||
            pair.Recipe == null ||
            !TryGetVeiledRecipesApi(out VeiledRecipesApi? api) ||
            api == null)
        {
            return false;
        }

        return api.HasUnknownRecipePreviewApi
            ? api.IsUnknownRecipePreview(player, pair.Recipe)
            : api.ShouldMaskRecipePair(player, pair);
    }

    private static int CompareVeiledRecipeMaskGrouping(bool aIsVeiledRecipePreview, bool bIsVeiledRecipePreview)
    {
        if (!ShouldGroupVeiledRecipePreviewsBelowKnownRecipes())
        {
            return 0;
        }

        return aIsVeiledRecipePreview == bIsVeiledRecipePreview ? 0 : aIsVeiledRecipePreview ? 1 : -1;
    }

    private static bool ShouldGroupVeiledRecipePreviewsBelowKnownRecipes()
    {
        return TryGetVeiledRecipesApi(out VeiledRecipesApi? api) &&
               api != null &&
               api.GroupUnknownRecipePreviewsBelowKnownRecipes;
    }

    private static string GetVeiledRecipeGroupingSignature()
    {
        return ShouldGroupVeiledRecipePreviewsBelowKnownRecipes() ? "veiledgroup:1" : "veiledgroup:0";
    }

    private static bool IsVeiledRecipeRequirementKnown(Requirement requirement)
    {
        Player? player = Player.m_localPlayer;
        return player != null &&
               TryGetVeiledRecipesApi(out VeiledRecipesApi? api) &&
               api != null &&
               api.IsMaterialKnown(player, requirement);
    }

    private static bool KnowsVeiledRecipeStationRequirement(Recipe recipe, int quality)
    {
        Player? player = Player.m_localPlayer;
        return player != null &&
               TryGetVeiledRecipesApi(out VeiledRecipesApi? api) &&
               api != null &&
               api.KnowsRecipeStationRequirement(player, recipe, quality);
    }

    private static string GetVeiledRecipeUnknownNameText()
    {
        return TryGetVeiledRecipesApi(out VeiledRecipesApi? api) && api != null ? api.UnknownNameText : "???";
    }

    private static string GetVeiledRecipeUnknownDescriptionText()
    {
        return TryGetVeiledRecipesApi(out VeiledRecipesApi? api) && api != null ? api.UnknownDescriptionText : "Not enough info";
    }

    private static string GetVeiledRecipeUnknownRequirementText()
    {
        return TryGetVeiledRecipesApi(out VeiledRecipesApi? api) && api != null ? api.UnknownRequirementText : "?";
    }

    private static string GetVeiledRecipeDisplaySignature(InventoryGui.RecipeDataPair pair)
    {
        if (!IsVeiledRecipeMasked(pair))
        {
            return "veiled:known";
        }

        StringBuilder builder = new();
        builder
            .Append("veiled:masked|")
            .Append(GetVeiledRecipeUnknownNameText())
            .Append('|')
            .Append(GetVeiledRecipeUnknownDescriptionText())
            .Append('|')
            .Append(GetVeiledRecipeUnknownRequirementText());

        Recipe? recipe = pair.Recipe;
        if (recipe == null)
        {
            return builder.ToString();
        }

        int quality = pair.ItemData == null ? 1 : pair.ItemData.m_quality + 1;
        if (recipe.m_resources != null)
        {
            foreach (Requirement requirement in recipe.m_resources)
            {
                if (requirement == null || requirement.m_resItem == null || requirement.GetAmount(quality) <= 0)
                {
                    continue;
                }

                builder
                    .Append('|')
                    .Append(requirement.m_resItem.GetInstanceID())
                    .Append(':')
                    .Append(IsVeiledRecipeRequirementKnown(requirement) ? '1' : '0');
            }
        }

        builder
            .Append("|station:")
            .Append(KnowsVeiledRecipeStationRequirement(recipe, quality) ? '1' : '0');
        return builder.ToString();
    }
}
