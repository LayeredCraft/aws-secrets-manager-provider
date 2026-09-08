using System;
using System.Collections.Generic;

namespace MyLib.Core;

/// <summary>Caches values keyed by a runtime-provided type.</summary>
public static class GenericCache
{
    private static readonly Dictionary<Type, object> Slots = new();

    public static void RegisterSlot<T>(Type valueType)
    {
        var slotType = typeof(ContextSlot<>).MakeGenericType(valueType);
        var slot = Activator.CreateInstance(slotType, typeof(T))!;
        Slots[slotType] = slot;
    }
}

public class ContextSlot<T>
{
    public Type Bound { get; }
    public ContextSlot(Type bound) => Bound = bound;
}
