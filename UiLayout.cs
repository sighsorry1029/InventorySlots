using UnityEngine;

namespace InventorySlots;

internal sealed class RectTransformLayoutMarker : MonoBehaviour
{
    public string Signature { get; set; } = "";
}

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

    private static bool SetTopLeftRectLayout(RectTransform parent, RectTransform rect, Vector2 anchoredPosition, Vector2 size, bool setAsLastSibling = true)
    {
        bool changed = false;
        if (rect.parent != parent)
        {
            rect.SetParent(parent, false);
            changed = true;
        }

        changed |= SetTopLeftRectLayout(rect, anchoredPosition, size);
        if (setAsLastSibling && changed && rect.parent != null && rect.GetSiblingIndex() != rect.parent.childCount - 1)
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

    private static bool SetTopLeftRectLayoutCached(RectTransform rect, Vector2 anchoredPosition, Vector2 size, string key)
    {
        string signature = BuildRectLayoutSignature("top-left", key, parentId: 0, anchoredPosition, size);
        if (CanReuseRectLayout(rect, signature) &&
            RectLayoutMatches(rect, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), anchoredPosition, size))
        {
            return false;
        }

        bool changed = SetTopLeftRectLayout(rect, anchoredPosition, size);
        SetRectLayoutSignature(rect, signature);
        return changed;
    }

    private static bool SetTopLeftRectLayoutCached(RectTransform parent, RectTransform rect, Vector2 anchoredPosition, Vector2 size, string key, bool setAsLastSibling = true)
    {
        if (parent == null || IsUnityNull(parent))
        {
            return false;
        }

        int parentId = parent.GetInstanceID();
        string signature = BuildRectLayoutSignature("top-left-parent", key, parentId, anchoredPosition, size);
        bool siblingMatches = !setAsLastSibling ||
                              rect.parent != null &&
                              rect.GetSiblingIndex() == rect.parent.childCount - 1;
        if (CanReuseRectLayout(rect, signature) &&
            rect.parent == parent &&
            siblingMatches &&
            RectLayoutMatches(rect, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), anchoredPosition, size))
        {
            return false;
        }

        bool changed = SetTopLeftRectLayout(parent!, rect, anchoredPosition, size, setAsLastSibling);
        SetRectLayoutSignature(rect, signature);
        return changed;
    }

    private static bool SetStretchRectLayoutCached(RectTransform rect, Vector2 offsetMin, Vector2 offsetMax, string key)
    {
        string signature = BuildRectLayoutSignature("stretch", key, parentId: 0, offsetMin, offsetMax);
        if (CanReuseRectLayout(rect, signature) &&
            StretchRectLayoutMatches(rect, offsetMin, offsetMax))
        {
            return false;
        }

        bool changed = SetStretchRectLayout(rect, offsetMin, offsetMax);
        SetRectLayoutSignature(rect, signature);
        return changed;
    }

    private static bool CanReuseRectLayout(RectTransform rect, string signature)
    {
        if (rect == null || IsUnityNull(rect))
        {
            return false;
        }

        RectTransformLayoutMarker? marker = rect.GetComponent<RectTransformLayoutMarker>();
        return marker != null && !IsUnityNull(marker) && string.Equals(marker.Signature, signature, System.StringComparison.Ordinal);
    }

    private static bool RectLayoutMatches(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPosition, Vector2 sizeDelta)
    {
        if (rect == null || IsUnityNull(rect))
        {
            return false;
        }

        return VectorApproximatelyEqual(rect.anchorMin, anchorMin) &&
               VectorApproximatelyEqual(rect.anchorMax, anchorMax) &&
               VectorApproximatelyEqual(rect.pivot, pivot) &&
               VectorApproximatelyEqual(rect.anchoredPosition, anchoredPosition) &&
               VectorApproximatelyEqual(rect.sizeDelta, sizeDelta) &&
               rect.localScale == Vector3.one &&
               rect.localRotation == Quaternion.identity;
    }

    private static bool StretchRectLayoutMatches(RectTransform rect, Vector2 offsetMin, Vector2 offsetMax)
    {
        if (rect == null || IsUnityNull(rect))
        {
            return false;
        }

        return VectorApproximatelyEqual(rect.anchorMin, Vector2.zero) &&
               VectorApproximatelyEqual(rect.anchorMax, Vector2.one) &&
               VectorApproximatelyEqual(rect.pivot, new Vector2(0.5f, 0.5f)) &&
               VectorApproximatelyEqual(rect.offsetMin, offsetMin) &&
               VectorApproximatelyEqual(rect.offsetMax, offsetMax) &&
               rect.localScale == Vector3.one &&
               rect.localRotation == Quaternion.identity;
    }

    private static bool VectorApproximatelyEqual(Vector2 left, Vector2 right) =>
        (left - right).sqrMagnitude <= 0.0001f;

    private static void SetRectLayoutSignature(RectTransform rect, string signature)
    {
        if (rect == null || IsUnityNull(rect))
        {
            return;
        }

        RectTransformLayoutMarker marker = rect.GetComponent<RectTransformLayoutMarker>() ?? rect.gameObject.AddComponent<RectTransformLayoutMarker>();
        marker.Signature = signature;
    }

    private static string BuildRectLayoutSignature(string mode, string key, int parentId, Vector2 first, Vector2 second)
    {
        return string.Concat(
            mode,
            "|",
            key,
            "|",
            parentId.ToString(),
            "|",
            first.x.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture),
            ",",
            first.y.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture),
            "|",
            second.x.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture),
            ",",
            second.y.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture));
    }
}
