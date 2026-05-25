using UnityEngine;
using UnityEngine.EventSystems;

namespace InventorySlots;

internal sealed class InventorySlotsSimpleTooltipHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    private string _topic = "";
    private string _text = "";
    private bool _hovering;

    public void Configure(string topic, string text)
    {
        _topic = topic ?? "";
        _text = text ?? "";
        enabled = !string.IsNullOrWhiteSpace(_topic) || !string.IsNullOrWhiteSpace(_text);
        if (!enabled && _hovering)
        {
            _hovering = false;
            InventorySlotsPlugin.HideSimpleNameTooltip(this);
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (!enabled)
        {
            return;
        }

        _hovering = true;
        InventorySlotsPlugin.ShowSimpleNameTooltip(this, _topic, _text);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (!_hovering)
        {
            return;
        }

        _hovering = false;
        InventorySlotsPlugin.HideSimpleNameTooltip(this);
    }

    private void Update()
    {
        if (_hovering)
        {
            InventorySlotsPlugin.ShowSimpleNameTooltip(this, _topic, _text);
        }
    }

    private void OnDisable()
    {
        if (_hovering)
        {
            _hovering = false;
            InventorySlotsPlugin.HideSimpleNameTooltip(this);
        }
    }
}
