using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Runtime.InteropServices;

namespace InventorySlots;

internal sealed class MultiUserContainerItemSnapshot
{
    private readonly ReadOnlyCollection<KeyValuePair<string, string>> _customData;

    public MultiUserContainerItemSnapshot(
        string? prefabName,
        int quality,
        int variant,
        int worldLevel,
        long crafterId,
        string? crafterName,
        float durability,
        bool pickedUp,
        int stack,
        IEnumerable<KeyValuePair<string, string>>? customData)
    {
        PrefabName = prefabName;
        Quality = quality;
        Variant = variant;
        WorldLevel = worldLevel;
        CrafterId = crafterId;
        CrafterName = crafterName;
        Durability = durability;
        DurabilityBits = SingleBits.GetBits(durability);
        PickedUp = pickedUp;
        Stack = stack;

        List<KeyValuePair<string, string>> copiedCustomData =
            customData == null
                ? new List<KeyValuePair<string, string>>()
                : new List<KeyValuePair<string, string>>(customData);
        copiedCustomData.Sort(CompareCustomData);
        _customData = copiedCustomData.AsReadOnly();
    }

    public string? PrefabName { get; }
    public int Quality { get; }
    public int Variant { get; }
    public int WorldLevel { get; }
    public long CrafterId { get; }
    public string? CrafterName { get; }
    public float Durability { get; }
    public int DurabilityBits { get; }
    public bool PickedUp { get; }
    public int Stack { get; }
    public IReadOnlyList<KeyValuePair<string, string>> CustomData => _customData;

    private static int CompareCustomData(
        KeyValuePair<string, string> left,
        KeyValuePair<string, string> right)
    {
        int keyComparison = string.CompareOrdinal(left.Key, right.Key);
        return keyComparison != 0
            ? keyComparison
            : string.CompareOrdinal(left.Value, right.Value);
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct SingleBits
    {
        [FieldOffset(0)]
        private float _value;

        [FieldOffset(0)]
        private int _bits;

        public static int GetBits(float value)
        {
            SingleBits bits = new()
            {
                _value = value
            };
            return bits._bits;
        }
    }
}

internal static class MultiUserContainerTransferCore
{
    internal const int MaximumSerializedStack = 1_000_000;

    /// <summary>
    /// Accepts a nominal stack-sized transfer, or an existing intentional
    /// over-stack only when the entire source stack moves as one unit. This keeps
    /// 1:1 replacement stacks movable without allowing a partial transfer to
    /// create a new over-stack.
    /// </summary>
    public static bool CanTransferAmount(
        int sourceStack,
        int nominalMaxStack,
        int amount)
    {
        return sourceStack > 0 &&
               sourceStack <= MaximumSerializedStack &&
               nominalMaxStack > 0 &&
               amount > 0 &&
               amount <= sourceStack &&
               (amount <= nominalMaxStack || amount == sourceStack);
    }

    public static bool MatchesExpectedStackState(
        int expectedStack,
        int? actualStack)
    {
        return expectedStack >= 0 &&
               (expectedStack == 0
                   ? !actualStack.HasValue
                   : actualStack == expectedStack);
    }

    public static bool IsExactMatch(
        MultiUserContainerItemSnapshot? expected,
        MultiUserContainerItemSnapshot? actual,
        int requiredStack)
    {
        if (!HasMatchingSerializedIdentity(
                expected,
                actual,
                requiredStack) ||
            expected!.CustomData.Count != actual!.CustomData.Count)
        {
            return false;
        }

        for (int index = 0; index < expected.CustomData.Count; index++)
        {
            KeyValuePair<string, string> expectedEntry = expected.CustomData[index];
            KeyValuePair<string, string> actualEntry = actual.CustomData[index];
            if (!string.Equals(expectedEntry.Key, actualEntry.Key, StringComparison.Ordinal) ||
                !string.Equals(expectedEntry.Value, actualEntry.Value, StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }

    public static bool CanStackTogether(
        MultiUserContainerItemSnapshot? incoming,
        MultiUserContainerItemSnapshot? target,
        int requiredStack)
    {
        return HasMatchingSerializedIdentity(incoming, target, requiredStack) &&
               StackMetadataPolicy.AreCompatible(
                   incoming!.CustomData,
                   target!.CustomData);
    }

    private static bool HasMatchingSerializedIdentity(
        MultiUserContainerItemSnapshot? expected,
        MultiUserContainerItemSnapshot? actual,
        int requiredStack)
    {
        return expected != null &&
               actual != null &&
               requiredStack > 0 &&
               expected.Stack >= requiredStack &&
               actual.Stack >= requiredStack &&
               string.Equals(expected.PrefabName, actual.PrefabName, StringComparison.Ordinal) &&
               expected.Quality == actual.Quality &&
               expected.Variant == actual.Variant &&
               expected.WorldLevel == actual.WorldLevel &&
               expected.CrafterId == actual.CrafterId &&
               string.Equals(expected.CrafterName, actual.CrafterName, StringComparison.Ordinal) &&
               expected.DurabilityBits == actual.DurabilityBits &&
               expected.PickedUp == actual.PickedUp;
    }
}
