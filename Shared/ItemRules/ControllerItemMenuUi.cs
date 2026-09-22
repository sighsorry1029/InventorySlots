using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

#if INVENTORY_SLOTS
namespace InventorySlots;
public sealed partial class InventorySlotsPlugin
#else
namespace InventoryActions;
public sealed partial class InventoryActionsPlugin
#endif
{
    private static InventoryGui? _controllerMenuUiOwner;
    private static RectTransform? _controllerMenuPanel;
    private static TMP_Text? _controllerMenuTitle, _controllerMenuHelp;
    private static readonly TMP_Text?[] ControllerMenuLabels = new TMP_Text?[2];
    private static readonly Image?[] ControllerMenuRows = new Image?[2];
    private static readonly Vector3[] ControllerMenuCorners = new Vector3[4];

    private static void ShowControllerItemMenu(InventoryGui gui, InventoryGrid grid, bool offerFavorite, bool favorite, int choice)
    {
        if (_controllerMenuUiOwner != gui) DestroyControllerItemMenu();
        if (_controllerMenuPanel == null)
        {
            TMP_Text? template = GetInventoryButtonCaptionTexts<TMP_Text>(gui.m_takeAllButton).FirstOrDefault();
            if (template == null || template.font == null) { ResetControllerItemMenu(); return; }
            _controllerMenuUiOwner = gui;
            _controllerMenuPanel = new GameObject(ModName + "_ControllerActions", typeof(RectTransform)).GetComponent<RectTransform>();
            Transform parent = gui.m_inventoryRoot != null ? gui.m_inventoryRoot : gui.transform;
            _controllerMenuPanel.SetParent(parent, false);
            if (gui.m_splitDialog != null && gui.m_splitDialog.transform.parent == parent)
                _controllerMenuPanel.SetSiblingIndex(gui.m_splitDialog.transform.GetSiblingIndex());
            _controllerMenuPanel.pivot = new Vector2(0, 1);
            _controllerMenuPanel.anchorMin = _controllerMenuPanel.anchorMax = new Vector2(0.5f, 0.5f);
            Image background = _controllerMenuPanel.gameObject.AddComponent<Image>();
            Image? source = gui.m_splitDialog != null ? gui.m_splitDialog.transform.Find("win_bkg/border (1)")?.GetComponent<Image>() : null;
            if (source == null || source.sprite == null) source = gui.m_player.Find("Bkg")?.GetComponent<Image>();
            if (source != null && source.sprite != null)
            {
                background.sprite = source.sprite;
                background.type = source.type;
                background.material = source.material;
                background.color = source.color;
                background.pixelsPerUnitMultiplier = source.pixelsPerUnitMultiplier;
            }
            else background.color = new Color(0.12f, 0.09f, 0.06f, 0.98f);
            background.raycastTarget = false;
            _controllerMenuTitle = MenuText("Title", template, _controllerMenuPanel, 19);
            PlaceMenuRect(_controllerMenuTitle.rectTransform, 12, -8, 250, 26);
            _controllerMenuTitle.color = new Color(1f, 0.8f, 0.36f, 1f);
            for (int i = 0; i < 2; i++)
            {
                Image row = new GameObject("Choice", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image)).GetComponent<Image>();
                row.transform.SetParent(_controllerMenuPanel, false);
                PlaceMenuRect(row.rectTransform, 10, -38 - i * 34, 254, 32);
                row.raycastTarget = false;
                ControllerMenuRows[i] = row;
                TMP_Text label = ControllerMenuLabels[i] = MenuText("Label", template, row.transform, 17);
                PlaceMenuRect(label.rectTransform, 8, 0, 238, 32);
            }
            _controllerMenuHelp = MenuText("Help", template, _controllerMenuPanel, 14);
        }

        string prefix = "$" + ModName.ToLowerInvariant() + "_controller_";
        _controllerMenuTitle!.text = LocalizeUi(prefix + "menu_title", "Inventory actions");
        int count = offerFavorite ? 2 : 1;
        for (int i = 0; i < 2; i++)
        {
            ControllerMenuRows[i]!.gameObject.SetActive(i < count);
            ControllerMenuRows[i]!.color = i == choice ? new Color(1f, 0.68f, 0.16f, 0.5f) : new Color(0, 0, 0, 0.2f);
            ControllerMenuLabels[i]!.text = offerFavorite && i == 0
                ? LocalizeUi(prefix + (favorite ? "menu_unfavorite" : "menu_favorite"), favorite ? "Remove favorite" : "Favorite slot")
                : LocalizeUi(prefix + "menu_sort", "Sort inventory");
        }
        float height = 38 + count * 34 + 48;
        _controllerMenuPanel.sizeDelta = new Vector2(274, height);
        PlaceMenuRect(_controllerMenuHelp!.rectTransform, 12, -40 - count * 34, 250, 42);
        _controllerMenuHelp.text = LocalizeUi(prefix + "menu_help", "↑↓: select\n{submit}: apply · {back}: back")
            .Replace("{submit}", GetInventoryControllerActionDisplay("JoyButtonA"))
            .Replace("{back}", GetInventoryControllerActionDisplay("JoyButtonB"));

        RectTransform? cell = grid.GetGamepadSelectedElement();
        RectTransform? parentRect = _controllerMenuPanel.parent as RectTransform;
        if (parentRect != null)
        {
            Vector3 position;
            if (cell != null)
            {
                cell.GetWorldCorners(ControllerMenuCorners);
                position = parentRect.InverseTransformPoint(ControllerMenuCorners[2]) + new Vector3(5, 0, 0);
            }
            else position = parentRect.rect.center;
            Rect bounds = parentRect.rect;
            position.x = Mathf.Clamp(position.x, bounds.xMin + 8, Mathf.Max(bounds.xMin + 8, bounds.xMax - 282));
            position.y = Mathf.Clamp(position.y, Mathf.Min(bounds.yMax - 8, bounds.yMin + height + 8), bounds.yMax - 8);
            position.z = 0;
            _controllerMenuPanel.localPosition = position;
        }
        _controllerMenuPanel.gameObject.SetActive(true);
    }

    private static TMP_Text MenuText(string name, TMP_Text template, Transform parent, float size)
    {
        GameObject go = new(name, typeof(RectTransform));
        go.SetActive(false);
        go.transform.SetParent(parent, false);
        TextMeshProUGUI text = go.AddComponent<TextMeshProUGUI>();
        text.font = template.font;
        text.fontSharedMaterial = template.fontSharedMaterial;
        text.spriteAsset = template.spriteAsset;
        text.fontSize = size;
        text.color = new Color(1f, 0.94f, 0.8f, 1f);
        text.alignment = TextAlignmentOptions.MidlineLeft;
        text.raycastTarget = false;
        go.SetActive(true);
        return text;
    }

    private static void PlaceMenuRect(RectTransform rect, float x, float y, float width, float height)
    {
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
        rect.anchoredPosition = new Vector2(x, y);
        rect.sizeDelta = new Vector2(width, height);
    }

    private static void HideControllerItemMenu()
    {
        if (_controllerMenuPanel != null) _controllerMenuPanel.gameObject.SetActive(false);
    }

    private static void DestroyControllerItemMenu(InventoryGui? owner = null)
    {
        if (owner != null && _controllerMenuUiOwner != owner) return;
        if (_controllerMenuPanel != null) Object.Destroy(_controllerMenuPanel.gameObject);
        _controllerMenuPanel = null;
        _controllerMenuUiOwner = null;
        _controllerMenuTitle = _controllerMenuHelp = null;
        for (int i = 0; i < 2; i++) { ControllerMenuLabels[i] = null; ControllerMenuRows[i] = null; }
    }
}
