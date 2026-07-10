using System;
using System.Collections.Generic;

namespace InventoryActions;

internal static class ContainerTransferCore
{
    public static int Run<TContainer>(
        IEnumerable<TContainer?>? containers,
        Func<TContainer, bool> canUse,
        Func<TContainer, int> transfer,
        Action<TContainer, int>? onContainerMoved,
        Action? onAnyMoved)
        where TContainer : class
    {
        if (containers == null || canUse == null || transfer == null)
        {
            return 0;
        }

        int moved = 0;
        foreach (TContainer? container in containers)
        {
            if (container == null || !canUse(container))
            {
                continue;
            }

            int containerMoved = transfer(container);
            moved += containerMoved;
            if (containerMoved > 0)
            {
                onContainerMoved?.Invoke(container, containerMoved);
            }
        }

        if (moved > 0)
        {
            onAnyMoved?.Invoke();
        }

        return moved;
    }
}
