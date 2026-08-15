using UnityEngine;

namespace InventorySlots;

public sealed partial class InventorySlotsPlugin
{
    private static bool SetRectLayout(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPosition, Vector2 sizeDelta)
    {
        if (rect == null || IsUnityNull(rect))
        {
            return false;
        }

        bool changed = false;
        if ((rect.anchorMin - anchorMin).sqrMagnitude > 0.0001f)
        {
            rect.anchorMin = anchorMin;
            changed = true;
        }

        if ((rect.anchorMax - anchorMax).sqrMagnitude > 0.0001f)
        {
            rect.anchorMax = anchorMax;
            changed = true;
        }

        if ((rect.pivot - pivot).sqrMagnitude > 0.0001f)
        {
            rect.pivot = pivot;
            changed = true;
        }

        if ((rect.anchoredPosition - anchoredPosition).sqrMagnitude > 0.0001f)
        {
            rect.anchoredPosition = anchoredPosition;
            changed = true;
        }

        if ((rect.sizeDelta - sizeDelta).sqrMagnitude > 0.0001f)
        {
            rect.sizeDelta = sizeDelta;
            changed = true;
        }

        if (rect.localScale != Vector3.one)
        {
            rect.localScale = Vector3.one;
            changed = true;
        }

        if (rect.localRotation != Quaternion.identity)
        {
            rect.localRotation = Quaternion.identity;
            changed = true;
        }

        return changed;
    }

    private static bool SetCenteredRectLayout(RectTransform rect, Vector2 anchoredPosition, Vector2 size)
    {
        return SetRectLayout(rect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), anchoredPosition, size);
    }

    private static bool SetTopLeftRectLayout(RectTransform rect, Vector2 anchoredPosition, Vector2 size)
    {
        return SetRectLayout(rect, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), anchoredPosition, size);
    }

    private static bool SetTopLeftRectLayout(RectTransform parent, RectTransform rect, Vector2 anchoredPosition, Vector2 size)
    {
        bool changed = false;
        if (rect.parent != parent)
        {
            rect.SetParent(parent, false);
            changed = true;
        }

        changed |= SetTopLeftRectLayout(rect, anchoredPosition, size);
        if (changed && rect.parent != null && rect.GetSiblingIndex() != rect.parent.childCount - 1)
        {
            rect.SetAsLastSibling();
        }

        return changed;
    }

    private static bool SetStretchRectLayout(RectTransform rect, Vector2 offsetMin, Vector2 offsetMax)
    {
        bool changed = SetRectLayout(rect, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), rect.anchoredPosition, rect.sizeDelta);
        if ((rect.offsetMin - offsetMin).sqrMagnitude > 0.0001f)
        {
            rect.offsetMin = offsetMin;
            changed = true;
        }

        if ((rect.offsetMax - offsetMax).sqrMagnitude > 0.0001f)
        {
            rect.offsetMax = offsetMax;
            changed = true;
        }

        return changed;
    }

    private static bool RectContainsScreenPoint(RectTransform rectTransform, Vector2 screenPoint)
    {
        Vector2 localPoint = rectTransform.InverseTransformPoint(screenPoint);
        return rectTransform.rect.Contains(localPoint);
    }

    internal static Sprite GetSolidUiSprite()
    {
        if (TooltipUi.SolidUiSprite != null)
        {
            return TooltipUi.SolidUiSprite;
        }

        Texture2D texture = new(1, 1, TextureFormat.RGBA32, false);
        texture.SetPixel(0, 0, Color.white);
        texture.Apply();
        TooltipUi.SolidUiSprite = Sprite.Create(texture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f));
        return TooltipUi.SolidUiSprite;
    }

}
