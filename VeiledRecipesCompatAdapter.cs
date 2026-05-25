using System;
using System.Reflection;
using Requirement = Piece.Requirement;

namespace InventorySlots;

public sealed partial class InventorySlotsPlugin
{
    private sealed class VeiledRecipesApi
    {
        private delegate bool ShouldMaskRecipePairDelegate(Player player, InventoryGui.RecipeDataPair pair);
        private delegate bool IsUnknownRecipePreviewDelegate(Player player, Recipe recipe);
        private delegate bool IsMaterialKnownDelegate(Player player, Requirement requirement);
        private delegate bool KnowsRecipeStationRequirementDelegate(Player player, Recipe recipe, int quality);

        private readonly ShouldMaskRecipePairDelegate _shouldMaskRecipePair;
        private readonly IsUnknownRecipePreviewDelegate? _isUnknownRecipePreview;
        private readonly IsMaterialKnownDelegate _isMaterialKnown;
        private readonly KnowsRecipeStationRequirementDelegate _knowsRecipeStationRequirement;
        private readonly PropertyInfo _unknownNameTextProperty;
        private readonly PropertyInfo _unknownDescriptionTextProperty;
        private readonly PropertyInfo _unknownRequirementTextProperty;
        private readonly PropertyInfo? _groupUnknownRecipePreviewsBelowKnownRecipesProperty;

        private VeiledRecipesApi(
            ShouldMaskRecipePairDelegate shouldMaskRecipePair,
            IsUnknownRecipePreviewDelegate? isUnknownRecipePreview,
            IsMaterialKnownDelegate isMaterialKnown,
            KnowsRecipeStationRequirementDelegate knowsRecipeStationRequirement,
            PropertyInfo unknownNameTextProperty,
            PropertyInfo unknownDescriptionTextProperty,
            PropertyInfo unknownRequirementTextProperty,
            PropertyInfo? groupUnknownRecipePreviewsBelowKnownRecipesProperty)
        {
            _shouldMaskRecipePair = shouldMaskRecipePair;
            _isUnknownRecipePreview = isUnknownRecipePreview;
            _isMaterialKnown = isMaterialKnown;
            _knowsRecipeStationRequirement = knowsRecipeStationRequirement;
            _unknownNameTextProperty = unknownNameTextProperty;
            _unknownDescriptionTextProperty = unknownDescriptionTextProperty;
            _unknownRequirementTextProperty = unknownRequirementTextProperty;
            _groupUnknownRecipePreviewsBelowKnownRecipesProperty = groupUnknownRecipePreviewsBelowKnownRecipesProperty;
        }

        public static bool TryCreate(Assembly assembly, out VeiledRecipesApi? api, out string detail)
        {
            api = null;
            Type? compatType = assembly.GetType("VeiledRecipes.VeiledRecipesCompat");
            MethodInfo? shouldMaskRecipePairMethod = compatType?.GetMethod(
                "ShouldMaskRecipePair",
                BindingFlags.Public | BindingFlags.Static,
                null,
                new[] { typeof(Player), typeof(InventoryGui.RecipeDataPair) },
                null);
            MethodInfo? isUnknownRecipePreviewMethod = compatType?.GetMethod(
                "IsUnknownRecipePreview",
                BindingFlags.Public | BindingFlags.Static,
                null,
                new[] { typeof(Player), typeof(Recipe) },
                null);
            MethodInfo? isMaterialKnownMethod = compatType?.GetMethod(
                "IsMaterialKnown",
                BindingFlags.Public | BindingFlags.Static,
                null,
                new[] { typeof(Player), typeof(Requirement) },
                null);
            MethodInfo? knowsRecipeStationRequirementMethod = compatType?.GetMethod(
                "KnowsRecipeStationRequirement",
                BindingFlags.Public | BindingFlags.Static,
                null,
                new[] { typeof(Player), typeof(Recipe), typeof(int) },
                null);
            PropertyInfo? unknownNameTextProperty = compatType?.GetProperty("UnknownNameText", BindingFlags.Public | BindingFlags.Static);
            PropertyInfo? unknownDescriptionTextProperty = compatType?.GetProperty("UnknownDescriptionText", BindingFlags.Public | BindingFlags.Static);
            PropertyInfo? unknownRequirementTextProperty = compatType?.GetProperty("UnknownRequirementText", BindingFlags.Public | BindingFlags.Static);
            PropertyInfo? groupUnknownRecipePreviewsBelowKnownRecipesProperty = compatType?.GetProperty("GroupUnknownRecipePreviewsBelowKnownRecipes", BindingFlags.Public | BindingFlags.Static);

            if (compatType == null ||
                shouldMaskRecipePairMethod == null ||
                isMaterialKnownMethod == null ||
                knowsRecipeStationRequirementMethod == null ||
                unknownNameTextProperty == null ||
                unknownDescriptionTextProperty == null ||
                unknownRequirementTextProperty == null)
            {
                detail = "VeiledRecipes compatibility API was not found";
                return false;
            }

            try
            {
                api = new VeiledRecipesApi(
                    (ShouldMaskRecipePairDelegate)Delegate.CreateDelegate(typeof(ShouldMaskRecipePairDelegate), shouldMaskRecipePairMethod),
                    isUnknownRecipePreviewMethod != null
                        ? (IsUnknownRecipePreviewDelegate)Delegate.CreateDelegate(typeof(IsUnknownRecipePreviewDelegate), isUnknownRecipePreviewMethod)
                        : null,
                    (IsMaterialKnownDelegate)Delegate.CreateDelegate(typeof(IsMaterialKnownDelegate), isMaterialKnownMethod),
                    (KnowsRecipeStationRequirementDelegate)Delegate.CreateDelegate(typeof(KnowsRecipeStationRequirementDelegate), knowsRecipeStationRequirementMethod),
                    unknownNameTextProperty,
                    unknownDescriptionTextProperty,
                    unknownRequirementTextProperty,
                    groupUnknownRecipePreviewsBelowKnownRecipesProperty);
                detail = "";
                return true;
            }
            catch (Exception ex)
            {
                detail = ex.Message;
                api = null;
                return false;
            }
        }

        public string UnknownNameText => GetStringProperty(_unknownNameTextProperty, "???");

        public string UnknownDescriptionText => GetStringProperty(_unknownDescriptionTextProperty, "Not enough info");

        public string UnknownRequirementText => GetStringProperty(_unknownRequirementTextProperty, "?");

        public bool GroupUnknownRecipePreviewsBelowKnownRecipes => GetBoolProperty(_groupUnknownRecipePreviewsBelowKnownRecipesProperty, fallback: false);

        public bool HasUnknownRecipePreviewApi => _isUnknownRecipePreview != null;

        public bool ShouldMaskRecipePair(Player player, InventoryGui.RecipeDataPair pair)
        {
            try
            {
                return _shouldMaskRecipePair(player, pair);
            }
            catch
            {
                return false;
            }
        }

        public bool IsUnknownRecipePreview(Player player, Recipe recipe)
        {
            if (_isUnknownRecipePreview == null)
            {
                return false;
            }

            try
            {
                return _isUnknownRecipePreview(player, recipe);
            }
            catch
            {
                return false;
            }
        }

        public bool IsMaterialKnown(Player player, Requirement requirement)
        {
            try
            {
                return _isMaterialKnown(player, requirement);
            }
            catch
            {
                return false;
            }
        }

        public bool KnowsRecipeStationRequirement(Player player, Recipe recipe, int quality)
        {
            try
            {
                return _knowsRecipeStationRequirement(player, recipe, quality);
            }
            catch
            {
                return false;
            }
        }

        private static string GetStringProperty(PropertyInfo property, string fallback)
        {
            try
            {
                return property.GetValue(null, null) as string ?? fallback;
            }
            catch
            {
                return fallback;
            }
        }

        private static bool GetBoolProperty(PropertyInfo? property, bool fallback)
        {
            if (property == null)
            {
                return fallback;
            }

            try
            {
                return property.GetValue(null, null) is bool value ? value : fallback;
            }
            catch
            {
                return fallback;
            }
        }
    }
}
