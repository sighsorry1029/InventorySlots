using System;
using static InventorySlots.CraftingStampMath;

namespace InventorySlots;

internal static class CraftingStampMath
{
    public static int Quantize(float value) =>
        (int)Math.Round(value * 1000f, MidpointRounding.AwayFromZero);
}

internal readonly struct CraftingFrameFastPathStamp : IEquatable<CraftingFrameFastPathStamp>
{
    public CraftingFrameFastPathStamp(
        int guiId,
        int craftingPanelId,
        int gridId,
        CraftingTabAdapterKind adapterKind,
        int selectedRecipeIndex,
        string recipeViewSignature,
        int recipePage,
        int gridDimension,
        int availabilityVersion,
        bool hasNoCraftCost,
        string pinnedTooltipGridSignature,
        int recipeVariantVersion,
        int hoveredRecipeIndex,
        int screenWidth,
        int screenHeight)
    {
        IsValid = true;
        GuiId = guiId;
        CraftingPanelId = craftingPanelId;
        GridId = gridId;
        AdapterKind = adapterKind;
        SelectedRecipeIndex = selectedRecipeIndex;
        RecipeViewSignature = recipeViewSignature ?? "";
        RecipePage = recipePage;
        GridDimension = gridDimension;
        AvailabilityVersion = availabilityVersion;
        HasNoCraftCost = hasNoCraftCost;
        PinnedTooltipGridSignature = pinnedTooltipGridSignature ?? "";
        RecipeVariantVersion = recipeVariantVersion;
        HoveredRecipeIndex = hoveredRecipeIndex;
        ScreenWidth = screenWidth;
        ScreenHeight = screenHeight;
    }

    public bool IsValid { get; }
    private int GuiId { get; }
    private int CraftingPanelId { get; }
    private int GridId { get; }
    private CraftingTabAdapterKind AdapterKind { get; }
    private int SelectedRecipeIndex { get; }
    private string RecipeViewSignature { get; }
    private int RecipePage { get; }
    private int GridDimension { get; }
    private int AvailabilityVersion { get; }
    private bool HasNoCraftCost { get; }
    private string PinnedTooltipGridSignature { get; }
    private int RecipeVariantVersion { get; }
    private int HoveredRecipeIndex { get; }
    private int ScreenWidth { get; }
    private int ScreenHeight { get; }

    public bool Equals(CraftingFrameFastPathStamp other) =>
        IsValid == other.IsValid &&
        GuiId == other.GuiId &&
        CraftingPanelId == other.CraftingPanelId &&
        GridId == other.GridId &&
        AdapterKind == other.AdapterKind &&
        SelectedRecipeIndex == other.SelectedRecipeIndex &&
        string.Equals(RecipeViewSignature, other.RecipeViewSignature, StringComparison.Ordinal) &&
        RecipePage == other.RecipePage &&
        GridDimension == other.GridDimension &&
        AvailabilityVersion == other.AvailabilityVersion &&
        HasNoCraftCost == other.HasNoCraftCost &&
        string.Equals(PinnedTooltipGridSignature, other.PinnedTooltipGridSignature, StringComparison.Ordinal) &&
        RecipeVariantVersion == other.RecipeVariantVersion &&
        HoveredRecipeIndex == other.HoveredRecipeIndex &&
        ScreenWidth == other.ScreenWidth &&
        ScreenHeight == other.ScreenHeight;

    public override bool Equals(object? obj) =>
        obj is CraftingFrameFastPathStamp other && Equals(other);

    public override int GetHashCode()
    {
        unchecked
        {
            int hash = IsValid ? 1 : 0;
            hash = hash * 397 ^ GuiId;
            hash = hash * 397 ^ CraftingPanelId;
            hash = hash * 397 ^ GridId;
            hash = hash * 397 ^ (int)AdapterKind;
            hash = hash * 397 ^ SelectedRecipeIndex;
            hash = hash * 397 ^ StringComparer.Ordinal.GetHashCode(RecipeViewSignature ?? "");
            hash = hash * 397 ^ RecipePage;
            hash = hash * 397 ^ GridDimension;
            hash = hash * 397 ^ AvailabilityVersion;
            hash = hash * 397 ^ HasNoCraftCost.GetHashCode();
            hash = hash * 397 ^ StringComparer.Ordinal.GetHashCode(PinnedTooltipGridSignature ?? "");
            hash = hash * 397 ^ RecipeVariantVersion;
            hash = hash * 397 ^ HoveredRecipeIndex;
            hash = hash * 397 ^ ScreenWidth;
            hash = hash * 397 ^ ScreenHeight;
            return hash;
        }
    }
}

internal readonly struct CraftingRecipeGridStamp : IEquatable<CraftingRecipeGridStamp>
{
    public CraftingRecipeGridStamp(
        int dimension,
        int pageStart,
        int selectedIndex,
        int availabilityHash,
        string pinnedTooltipGridSignature,
        int viewCount,
        int recipeVariantVersion)
    {
        IsValid = true;
        Dimension = dimension;
        PageStart = pageStart;
        SelectedIndex = selectedIndex;
        AvailabilityHash = availabilityHash;
        PinnedTooltipGridSignature = pinnedTooltipGridSignature ?? "";
        ViewCount = viewCount;
        RecipeVariantVersion = recipeVariantVersion;
    }

    public bool IsValid { get; }
    private int Dimension { get; }
    private int PageStart { get; }
    private int SelectedIndex { get; }
    private int AvailabilityHash { get; }
    private string PinnedTooltipGridSignature { get; }
    private int ViewCount { get; }
    private int RecipeVariantVersion { get; }

    public bool Equals(CraftingRecipeGridStamp other) =>
        IsValid == other.IsValid &&
        Dimension == other.Dimension &&
        PageStart == other.PageStart &&
        SelectedIndex == other.SelectedIndex &&
        AvailabilityHash == other.AvailabilityHash &&
        string.Equals(PinnedTooltipGridSignature, other.PinnedTooltipGridSignature, StringComparison.Ordinal) &&
        ViewCount == other.ViewCount &&
        RecipeVariantVersion == other.RecipeVariantVersion;

    public override bool Equals(object? obj) =>
        obj is CraftingRecipeGridStamp other && Equals(other);

    public override int GetHashCode()
    {
        unchecked
        {
            int hash = IsValid ? 1 : 0;
            hash = hash * 397 ^ Dimension;
            hash = hash * 397 ^ PageStart;
            hash = hash * 397 ^ SelectedIndex;
            hash = hash * 397 ^ AvailabilityHash;
            hash = hash * 397 ^ StringComparer.Ordinal.GetHashCode(PinnedTooltipGridSignature ?? "");
            hash = hash * 397 ^ ViewCount;
            hash = hash * 397 ^ RecipeVariantVersion;
            return hash;
        }
    }
}

internal readonly struct CraftingRecipeScrollbarStamp : IEquatable<CraftingRecipeScrollbarStamp>
{
    public CraftingRecipeScrollbarStamp(int pageCount, int recipePage, bool visible, float gridX, float gridY)
    {
        IsValid = true;
        PageCount = pageCount;
        RecipePage = recipePage;
        Visible = visible;
        GridX = Quantize(gridX);
        GridY = Quantize(gridY);
    }

    public bool IsValid { get; }
    private int PageCount { get; }
    private int RecipePage { get; }
    private bool Visible { get; }
    private int GridX { get; }
    private int GridY { get; }

    public bool Equals(CraftingRecipeScrollbarStamp other) =>
        IsValid == other.IsValid &&
        PageCount == other.PageCount &&
        RecipePage == other.RecipePage &&
        Visible == other.Visible &&
        GridX == other.GridX &&
        GridY == other.GridY;

    public override bool Equals(object? obj) =>
        obj is CraftingRecipeScrollbarStamp other && Equals(other);

    public override int GetHashCode()
    {
        unchecked
        {
            int hash = IsValid ? 1 : 0;
            hash = hash * 397 ^ PageCount;
            hash = hash * 397 ^ RecipePage;
            hash = hash * 397 ^ Visible.GetHashCode();
            hash = hash * 397 ^ GridX;
            hash = hash * 397 ^ GridY;
            return hash;
        }
    }

}

internal readonly struct CraftingGroupRailStamp : IEquatable<CraftingGroupRailStamp>
{
    public CraftingGroupRailStamp(
        int craftingPanelId,
        int gridId,
        float gridX,
        float gridY,
        string selectedGroupId,
        int favoritesVersion,
        string availabilitySignature,
        string selectableGroupIdsSignature)
    {
        IsValid = true;
        CraftingPanelId = craftingPanelId;
        GridId = gridId;
        GridX = Quantize(gridX);
        GridY = Quantize(gridY);
        SelectedGroupId = selectedGroupId ?? "";
        FavoritesVersion = favoritesVersion;
        AvailabilitySignature = availabilitySignature ?? "";
        SelectableGroupIdsSignature = selectableGroupIdsSignature ?? "";
    }

    public bool IsValid { get; }
    private int CraftingPanelId { get; }
    private int GridId { get; }
    private int GridX { get; }
    private int GridY { get; }
    private string SelectedGroupId { get; }
    private int FavoritesVersion { get; }
    private string AvailabilitySignature { get; }
    private string SelectableGroupIdsSignature { get; }

    public bool Equals(CraftingGroupRailStamp other) =>
        IsValid == other.IsValid &&
        CraftingPanelId == other.CraftingPanelId &&
        GridId == other.GridId &&
        GridX == other.GridX &&
        GridY == other.GridY &&
        string.Equals(SelectedGroupId, other.SelectedGroupId, StringComparison.Ordinal) &&
        FavoritesVersion == other.FavoritesVersion &&
        string.Equals(AvailabilitySignature, other.AvailabilitySignature, StringComparison.Ordinal) &&
        string.Equals(SelectableGroupIdsSignature, other.SelectableGroupIdsSignature, StringComparison.Ordinal);

    public override bool Equals(object? obj) =>
        obj is CraftingGroupRailStamp other && Equals(other);

    public override int GetHashCode()
    {
        unchecked
        {
            int hash = IsValid ? 1 : 0;
            hash = hash * 397 ^ CraftingPanelId;
            hash = hash * 397 ^ GridId;
            hash = hash * 397 ^ GridX;
            hash = hash * 397 ^ GridY;
            hash = hash * 397 ^ StringComparer.Ordinal.GetHashCode(SelectedGroupId ?? "");
            hash = hash * 397 ^ FavoritesVersion;
            hash = hash * 397 ^ StringComparer.Ordinal.GetHashCode(AvailabilitySignature ?? "");
            hash = hash * 397 ^ StringComparer.Ordinal.GetHashCode(SelectableGroupIdsSignature ?? "");
            return hash;
        }
    }

}

internal readonly struct CraftingSearchInputStamp : IEquatable<CraftingSearchInputStamp>
{
    public CraftingSearchInputStamp(
        int craftingPanelId,
        int inputId,
        float positionX,
        float positionY,
        float sizeX,
        float sizeY,
        string searchQuery,
        int localizationVersion,
        bool focused,
        int tabImageId,
        int tabSpriteId,
        int tabMaterialId)
    {
        IsValid = true;
        CraftingPanelId = craftingPanelId;
        InputId = inputId;
        PositionX = Quantize(positionX);
        PositionY = Quantize(positionY);
        SizeX = Quantize(sizeX);
        SizeY = Quantize(sizeY);
        SearchQuery = searchQuery ?? "";
        LocalizationVersion = localizationVersion;
        Focused = focused;
        TabImageId = tabImageId;
        TabSpriteId = tabSpriteId;
        TabMaterialId = tabMaterialId;
    }

    public bool IsValid { get; }
    private int CraftingPanelId { get; }
    private int InputId { get; }
    private int PositionX { get; }
    private int PositionY { get; }
    private int SizeX { get; }
    private int SizeY { get; }
    private string SearchQuery { get; }
    private int LocalizationVersion { get; }
    private bool Focused { get; }
    private int TabImageId { get; }
    private int TabSpriteId { get; }
    private int TabMaterialId { get; }

    public bool Equals(CraftingSearchInputStamp other) =>
        IsValid == other.IsValid &&
        CraftingPanelId == other.CraftingPanelId &&
        InputId == other.InputId &&
        PositionX == other.PositionX &&
        PositionY == other.PositionY &&
        SizeX == other.SizeX &&
        SizeY == other.SizeY &&
        string.Equals(SearchQuery, other.SearchQuery, StringComparison.Ordinal) &&
        LocalizationVersion == other.LocalizationVersion &&
        Focused == other.Focused &&
        TabImageId == other.TabImageId &&
        TabSpriteId == other.TabSpriteId &&
        TabMaterialId == other.TabMaterialId;

    public override bool Equals(object? obj) =>
        obj is CraftingSearchInputStamp other && Equals(other);

    public override int GetHashCode()
    {
        unchecked
        {
            int hash = IsValid ? 1 : 0;
            hash = hash * 397 ^ CraftingPanelId;
            hash = hash * 397 ^ InputId;
            hash = hash * 397 ^ PositionX;
            hash = hash * 397 ^ PositionY;
            hash = hash * 397 ^ SizeX;
            hash = hash * 397 ^ SizeY;
            hash = hash * 397 ^ StringComparer.Ordinal.GetHashCode(SearchQuery ?? "");
            hash = hash * 397 ^ LocalizationVersion;
            hash = hash * 397 ^ Focused.GetHashCode();
            hash = hash * 397 ^ TabImageId;
            hash = hash * 397 ^ TabSpriteId;
            hash = hash * 397 ^ TabMaterialId;
            return hash;
        }
    }

}

internal readonly struct CraftingSortModeButtonsStamp : IEquatable<CraftingSortModeButtonsStamp>
{
    public CraftingSortModeButtonsStamp(
        int craftingPanelId,
        int groupId,
        float positionX,
        float positionY,
        float sizeX,
        float sizeY,
        float buttonSize,
        CraftingRecipeSortMode mode,
        int localizationVersion,
        int tabImageId,
        int tabSpriteId,
        int tabMaterialId)
    {
        IsValid = true;
        CraftingPanelId = craftingPanelId;
        GroupId = groupId;
        PositionX = Quantize(positionX);
        PositionY = Quantize(positionY);
        SizeX = Quantize(sizeX);
        SizeY = Quantize(sizeY);
        ButtonSize = Quantize(buttonSize);
        Mode = mode;
        LocalizationVersion = localizationVersion;
        TabImageId = tabImageId;
        TabSpriteId = tabSpriteId;
        TabMaterialId = tabMaterialId;
    }

    public bool IsValid { get; }
    private int CraftingPanelId { get; }
    private int GroupId { get; }
    private int PositionX { get; }
    private int PositionY { get; }
    private int SizeX { get; }
    private int SizeY { get; }
    private int ButtonSize { get; }
    private CraftingRecipeSortMode Mode { get; }
    private int LocalizationVersion { get; }
    private int TabImageId { get; }
    private int TabSpriteId { get; }
    private int TabMaterialId { get; }

    public bool Equals(CraftingSortModeButtonsStamp other) =>
        IsValid == other.IsValid &&
        CraftingPanelId == other.CraftingPanelId &&
        GroupId == other.GroupId &&
        PositionX == other.PositionX &&
        PositionY == other.PositionY &&
        SizeX == other.SizeX &&
        SizeY == other.SizeY &&
        ButtonSize == other.ButtonSize &&
        Mode == other.Mode &&
        LocalizationVersion == other.LocalizationVersion &&
        TabImageId == other.TabImageId &&
        TabSpriteId == other.TabSpriteId &&
        TabMaterialId == other.TabMaterialId;

    public override bool Equals(object? obj) =>
        obj is CraftingSortModeButtonsStamp other && Equals(other);

    public override int GetHashCode()
    {
        unchecked
        {
            int hash = IsValid ? 1 : 0;
            hash = hash * 397 ^ CraftingPanelId;
            hash = hash * 397 ^ GroupId;
            hash = hash * 397 ^ PositionX;
            hash = hash * 397 ^ PositionY;
            hash = hash * 397 ^ SizeX;
            hash = hash * 397 ^ SizeY;
            hash = hash * 397 ^ ButtonSize;
            hash = hash * 397 ^ (int)Mode;
            hash = hash * 397 ^ LocalizationVersion;
            hash = hash * 397 ^ TabImageId;
            hash = hash * 397 ^ TabSpriteId;
            hash = hash * 397 ^ TabMaterialId;
            return hash;
        }
    }

}

internal readonly struct CraftingStatusHudStamp : IEquatable<CraftingStatusHudStamp>
{
    public CraftingStatusHudStamp(
        int craftingPanelId,
        int gridId,
        float positionX,
        float positionY,
        float sizeX,
        float sizeY,
        string warning)
    {
        IsValid = true;
        CraftingPanelId = craftingPanelId;
        GridId = gridId;
        PositionX = Quantize(positionX);
        PositionY = Quantize(positionY);
        SizeX = Quantize(sizeX);
        SizeY = Quantize(sizeY);
        Warning = warning ?? "";
    }

    public bool IsValid { get; }
    private int CraftingPanelId { get; }
    private int GridId { get; }
    private int PositionX { get; }
    private int PositionY { get; }
    private int SizeX { get; }
    private int SizeY { get; }
    private string Warning { get; }

    public bool Equals(CraftingStatusHudStamp other) =>
        IsValid == other.IsValid &&
        CraftingPanelId == other.CraftingPanelId &&
        GridId == other.GridId &&
        PositionX == other.PositionX &&
        PositionY == other.PositionY &&
        SizeX == other.SizeX &&
        SizeY == other.SizeY &&
        string.Equals(Warning, other.Warning, StringComparison.Ordinal);

    public override bool Equals(object? obj) =>
        obj is CraftingStatusHudStamp other && Equals(other);

    public override int GetHashCode()
    {
        unchecked
        {
            int hash = IsValid ? 1 : 0;
            hash = hash * 397 ^ CraftingPanelId;
            hash = hash * 397 ^ GridId;
            hash = hash * 397 ^ PositionX;
            hash = hash * 397 ^ PositionY;
            hash = hash * 397 ^ SizeX;
            hash = hash * 397 ^ SizeY;
            hash = hash * 397 ^ StringComparer.Ordinal.GetHashCode(Warning ?? "");
            return hash;
        }
    }

}

internal readonly struct CraftingTextStamp : IEquatable<CraftingTextStamp>
{
    public CraftingTextStamp(string scope, string text, string childSignature = "", float fontSizeMax = 0f)
    {
        IsValid = true;
        Scope = scope ?? "";
        Text = text ?? "";
        ChildSignature = childSignature ?? "";
        FontSizeMax = Quantize(fontSizeMax);
    }

    public bool IsValid { get; }
    private string Scope { get; }
    private string Text { get; }
    private string ChildSignature { get; }
    private int FontSizeMax { get; }

    public bool Equals(CraftingTextStamp other) =>
        IsValid == other.IsValid &&
        string.Equals(Scope, other.Scope, StringComparison.Ordinal) &&
        string.Equals(Text, other.Text, StringComparison.Ordinal) &&
        string.Equals(ChildSignature, other.ChildSignature, StringComparison.Ordinal) &&
        FontSizeMax == other.FontSizeMax;

    public override bool Equals(object? obj) =>
        obj is CraftingTextStamp other && Equals(other);

    public override int GetHashCode()
    {
        unchecked
        {
            int hash = IsValid ? 1 : 0;
            hash = hash * 397 ^ StringComparer.Ordinal.GetHashCode(Scope ?? "");
            hash = hash * 397 ^ StringComparer.Ordinal.GetHashCode(Text ?? "");
            hash = hash * 397 ^ StringComparer.Ordinal.GetHashCode(ChildSignature ?? "");
            hash = hash * 397 ^ FontSizeMax;
            return hash;
        }
    }

}

internal readonly struct CraftingTextColorStamp : IEquatable<CraftingTextColorStamp>
{
    public CraftingTextColorStamp(bool interactable, float colorR, float colorG, float colorB, float colorA, string childSignature)
    {
        IsValid = true;
        Interactable = interactable;
        ColorR = Quantize(colorR);
        ColorG = Quantize(colorG);
        ColorB = Quantize(colorB);
        ColorA = Quantize(colorA);
        ChildSignature = childSignature ?? "";
    }

    public bool IsValid { get; }
    private bool Interactable { get; }
    private int ColorR { get; }
    private int ColorG { get; }
    private int ColorB { get; }
    private int ColorA { get; }
    private string ChildSignature { get; }

    public bool Equals(CraftingTextColorStamp other) =>
        IsValid == other.IsValid &&
        Interactable == other.Interactable &&
        ColorR == other.ColorR &&
        ColorG == other.ColorG &&
        ColorB == other.ColorB &&
        ColorA == other.ColorA &&
        string.Equals(ChildSignature, other.ChildSignature, StringComparison.Ordinal);

    public override bool Equals(object? obj) =>
        obj is CraftingTextColorStamp other && Equals(other);

    public override int GetHashCode()
    {
        unchecked
        {
            int hash = IsValid ? 1 : 0;
            hash = hash * 397 ^ Interactable.GetHashCode();
            hash = hash * 397 ^ ColorR;
            hash = hash * 397 ^ ColorG;
            hash = hash * 397 ^ ColorB;
            hash = hash * 397 ^ ColorA;
            hash = hash * 397 ^ StringComparer.Ordinal.GetHashCode(ChildSignature ?? "");
            return hash;
        }
    }

}

internal readonly struct CraftingSimpleTooltipStamp : IEquatable<CraftingSimpleTooltipStamp>
{
    public CraftingSimpleTooltipStamp(string topic, string text)
    {
        IsValid = true;
        Topic = topic ?? "";
        Text = text ?? "";
    }

    public bool IsValid { get; }
    private string Topic { get; }
    private string Text { get; }

    public bool Equals(CraftingSimpleTooltipStamp other) =>
        IsValid == other.IsValid &&
        string.Equals(Topic, other.Topic, StringComparison.Ordinal) &&
        string.Equals(Text, other.Text, StringComparison.Ordinal);

    public override bool Equals(object? obj) =>
        obj is CraftingSimpleTooltipStamp other && Equals(other);

    public override int GetHashCode()
    {
        unchecked
        {
            int hash = IsValid ? 1 : 0;
            hash = hash * 397 ^ StringComparer.Ordinal.GetHashCode(Topic ?? "");
            hash = hash * 397 ^ StringComparer.Ordinal.GetHashCode(Text ?? "");
            return hash;
        }
    }
}
