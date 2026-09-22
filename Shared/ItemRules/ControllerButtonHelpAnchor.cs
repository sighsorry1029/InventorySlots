using TMPro;
using UnityEngine;

#if INVENTORY_SLOTS
namespace InventorySlots;
#else
namespace InventoryActions;
#endif

// Run after the Auto buttons' slide so the gap stays constant during animation.
[DefaultExecutionOrder(1000)]
internal sealed class ControllerButtonHelpAnchor : MonoBehaviour
{
    private const float Gap = 6f;
    private const float ScreenMargin = 8f;
    private readonly Vector3[] _corners = new Vector3[4];
    private RectTransform? _target;
    private TMP_Text _label = null!;
    private RectTransform _rect = null!;
    private string? _measuredText;
    private float _measuredWidth = -1f;

    private void Awake()
    {
        _label = GetComponent<TMP_Text>();
        _rect = _label.rectTransform;
        _rect.anchorMin = _rect.anchorMax = new Vector2(0.5f, 0.5f);
        _rect.pivot = new Vector2(1, 1);
        _label.alignment = TextAlignmentOptions.TopRight;
        _label.margin = Vector4.zero;
        _label.enableAutoSizing = false;
        _label.textWrappingMode = TextWrappingModes.Normal;
        _label.overflowMode = TextOverflowModes.Overflow;
    }

    internal void Follow(RectTransform target)
    {
        _target = target;
        PositionHelp();
    }

    private void LateUpdate() => PositionHelp();

    private void PositionHelp()
    {
        if (_target == null || !_target.gameObject.activeInHierarchy || _rect.parent is not RectTransform parent)
        {
            _label.enabled = false;
            return;
        }

        Canvas? canvas = parent.GetComponentInParent<Canvas>();
        Camera? camera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, Vector2.zero, camera, out Vector2 bottomLeft) ||
            !RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, new Vector2(Screen.width, Screen.height), camera, out Vector2 topRight))
        {
            _label.enabled = false;
            return;
        }
        Rect screen = Rect.MinMaxRect(bottomLeft.x + ScreenMargin, bottomLeft.y + ScreenMargin,
            topRight.x - ScreenMargin, topRight.y - ScreenMargin);
        if (screen.width <= 0 || screen.height <= 0) { _label.enabled = false; return; }

        _target.GetWorldCorners(_corners);
        Vector2 min = parent.InverseTransformPoint(_corners[0]);
        Vector2 max = min;
        for (int i = 1; i < _corners.Length; i++)
        {
            Vector2 point = parent.InverseTransformPoint(_corners[i]);
            min = Vector2.Min(min, point);
            max = Vector2.Max(max, point);
        }
        Rect target = Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        if (!screen.Overlaps(target)) { _label.enabled = false; return; }

        float maxWidth = Mathf.Min(500f, screen.width);
        if (_measuredText != _label.text || !Mathf.Approximately(_measuredWidth, maxWidth))
        {
            _measuredText = _label.text;
            _measuredWidth = maxWidth;
            Vector2 preferred = _label.GetPreferredValues(_label.text, maxWidth, 0f);
            _rect.sizeDelta = new Vector2(Mathf.Min(maxWidth, Mathf.Max(1f, preferred.x)), Mathf.Max(1f, preferred.y));
        }

        Vector2 size = _rect.sizeDelta;
        float right = Mathf.Clamp(target.xMax, screen.xMin + size.x, screen.xMax);
        float top = target.yMin - Gap;
        if (top - size.y < screen.yMin) top = target.yMax + Gap + size.y;
        // Very small viewports or an off-screen animated button may leave no
        // room above/below. Keep the selected button unobstructed in that case.
        if (top > screen.yMax || top - size.y < screen.yMin)
        {
            _label.enabled = false;
            return;
        }
        _rect.localPosition = new Vector3(right, top, 0);
        _label.enabled = true;
    }
}
