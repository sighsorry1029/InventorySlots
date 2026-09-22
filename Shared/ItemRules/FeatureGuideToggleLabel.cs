using TMPro;
using UnityEngine;

#if INVENTORY_SLOTS
namespace InventorySlots;
public sealed partial class InventorySlotsPlugin
#else
namespace InventoryActions;
public sealed partial class InventoryActionsPlugin
#endif
{
    private static TMP_Text? _measuredGuideToggleLabel;
    private static TMP_FontAsset? _measuredGuideToggleFont;
    private static string _measuredGuideToggleText = "";
    private static float _guideToggleLabelWidth = 72f;

    private static float ConfigureControllerFeatureGuideLabel(TMP_Text label)
    {
        string chord = GetControllerFeatureGuideToggleDisplay();
        label.text = chord;
        label.fontSize = 12f;
        label.lineSpacing = 0f;
        label.alignment = TextAlignmentOptions.Center;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.enableAutoSizing = false;
        label.raycastTarget = false;
        if (_measuredGuideToggleLabel != label || _measuredGuideToggleFont != label.font || _measuredGuideToggleText != chord)
        {
            float width = label.GetPreferredValues(chord).x;
            _guideToggleLabelWidth = float.IsNaN(width) || float.IsInfinity(width) ? 72f : Mathf.Max(64f, Mathf.Ceil(width) + 8f);
            _measuredGuideToggleLabel = label;
            _measuredGuideToggleFont = label.font;
            _measuredGuideToggleText = chord;
        }
        return _guideToggleLabelWidth;
    }
}
