using System;

namespace InventorySlots;

internal static class JewelcraftingTooltipCore
{
    public const int MaxRowlessRefreshAttempts = 3;

    public static bool ShouldRefreshNativeTooltip(
        string? previousSignature,
        string? nextSignature,
        bool previousVisible,
        bool previousHadSocketRows,
        int previousRowlessRefreshAttempts)
    {
        if (!string.Equals(previousSignature ?? "", nextSignature ?? "", StringComparison.Ordinal))
        {
            return true;
        }

        if (!previousVisible)
        {
            return true;
        }

        return !previousHadSocketRows &&
               previousRowlessRefreshAttempts < MaxRowlessRefreshAttempts;
    }

    public static bool HasVisibleText(string? text) =>
        !string.IsNullOrWhiteSpace(StripRichText(text));

    public static string BuildNativeTooltipUpdateSignature(
        bool showInteract,
        bool advancedPressed,
        bool prophecyPressed,
        int localizationVersion,
        string? equipmentSignature,
        string? openSocketSignature)
    {
        return string.Join(
            "|",
            showInteract,
            advancedPressed,
            prophecyPressed,
            localizationVersion,
            equipmentSignature ?? "",
            openSocketSignature ?? "");
    }

    public static string StripRichText(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return "";
        }

        string value = text!;
        int depth = 0;
        char[] buffer = new char[value.Length];
        int count = 0;
        foreach (char c in value)
        {
            if (c == '<')
            {
                depth++;
                continue;
            }

            if (c == '>' && depth > 0)
            {
                depth--;
                continue;
            }

            if (depth == 0)
            {
                buffer[count++] = c;
            }
        }

        return new string(buffer, 0, count);
    }
}
