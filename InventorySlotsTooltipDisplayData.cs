using UnityEngine;

namespace InventorySlots;

internal sealed class InventorySlotsTooltipDisplayData : MonoBehaviour
{
    public string RouteToken { get; private set; } = "";
    public string DisplayTopic { get; private set; } = "";
    public string DisplayText { get; private set; } = "";
    public bool HasDisplayData { get; private set; }

    public void Configure(string routeToken, string displayTopic, string displayText)
    {
        RouteToken = routeToken ?? "";
        DisplayTopic = displayTopic ?? "";
        DisplayText = displayText ?? "";
        HasDisplayData = !string.IsNullOrWhiteSpace(DisplayTopic) || !string.IsNullOrWhiteSpace(DisplayText);
    }
}
