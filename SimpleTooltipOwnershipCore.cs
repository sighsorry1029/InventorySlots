namespace InventorySlots;

internal sealed class SimpleTooltipOwnershipCore
{
    public object? Owner { get; private set; }
    public bool Visible { get; private set; }

    public void Show(object owner)
    {
        Owner = owner;
        Visible = true;
    }

    public bool Hide(object owner)
    {
        if (!Visible || !ReferenceEquals(Owner, owner))
        {
            return false;
        }

        ForceHide();
        return true;
    }

    public void ForceHide()
    {
        Owner = null;
        Visible = false;
    }
}
