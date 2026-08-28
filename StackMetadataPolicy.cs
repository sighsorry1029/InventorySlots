using System;
using System.Collections.Generic;

namespace InventorySlots;

/// <summary>
/// Central policy for custom-data fields whose values may be combined when two
/// otherwise identical item stacks are merged. Unregistered fields always remain
/// part of the exact stack identity.
/// </summary>
internal static class StackMetadataPolicy
{
    private static readonly object Sync = new();
    private static Dictionary<string, MergePolicy> MergePolicies =
        new(StringComparer.Ordinal);

    internal static bool Register(
        string key,
        Func<string?, string?, string?> mergeValues) =>
        Register(key, mergeValues, static (_, _) => true);

    internal static bool Register(
        string key,
        Func<string?, string?, string?> mergeValues,
        Func<string?, string?, bool> canMerge)
    {
        if (string.IsNullOrWhiteSpace(key) || mergeValues == null || canMerge == null)
        {
            return false;
        }

        lock (Sync)
        {
            if (MergePolicies.ContainsKey(key))
            {
                return false;
            }

            Dictionary<string, MergePolicy> added =
                new(MergePolicies, StringComparer.Ordinal);
            added.Add(key, new MergePolicy(mergeValues, canMerge));
            MergePolicies = added;
            return true;
        }
    }

    /// <summary>
    /// Returns true only when every custom-data field is governed by a merge
    /// policy. This is used by conservative automatic inventory actions that did
    /// not historically stack arbitrary custom-data items.
    /// </summary>
    internal static bool CanParticipateInAutomaticStacking(
        IDictionary<string, string>? customData)
    {
        if (customData == null || customData.Count == 0)
        {
            return true;
        }

        Dictionary<string, MergePolicy> policies = GetPoliciesSnapshot();
        foreach (string key in customData.Keys)
        {
            if (!policies.ContainsKey(key))
            {
                return false;
            }
        }

        return true;
    }

    internal static bool HasMergeMetadata(
        IDictionary<string, string>? customData)
    {
        if (customData == null || customData.Count == 0)
        {
            return false;
        }

        Dictionary<string, MergePolicy> policies = GetPoliciesSnapshot();
        foreach (string key in customData.Keys)
        {
            if (policies.ContainsKey(key))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Compares all unregistered metadata exactly. Registered fields are ignored
    /// only when their policy confirms that the two raw values can be merged.
    /// </summary>
    internal static bool AreCompatible(
        IDictionary<string, string>? left,
        IDictionary<string, string>? right)
    {
        if ((left == null || left.Count == 0) &&
            (right == null || right.Count == 0))
        {
            return true;
        }

        Dictionary<string, MergePolicy> policies = GetPoliciesSnapshot();
        if (!AreCompatibleOneWay(left, right, policies) ||
            !AreCompatibleOneWay(right, left, policies))
        {
            return false;
        }

        foreach (KeyValuePair<string, MergePolicy> policy in policies)
        {
            string? leftValue = null;
            string? rightValue = null;
            bool hasLeft = left != null && left.TryGetValue(policy.Key, out leftValue);
            bool hasRight = right != null && right.TryGetValue(policy.Key, out rightValue);
            if (!hasLeft && !hasRight)
            {
                continue;
            }

            if (!CanMerge(policy.Value, leftValue, rightValue) ||
                !CanMerge(policy.Value, rightValue, leftValue))
            {
                return false;
            }
        }

        return true;
    }

    internal static bool AreCompatible(
        IReadOnlyList<KeyValuePair<string, string>>? left,
        IReadOnlyList<KeyValuePair<string, string>>? right)
    {
        Dictionary<string, string>? leftDictionary = ToDictionary(left);
        Dictionary<string, string>? rightDictionary = ToDictionary(right);
        return leftDictionary != null &&
               rightDictionary != null &&
               AreCompatible(leftDictionary, rightDictionary);
    }

    /// <summary>
    /// Applies every registered merge policy to the destination without changing
    /// the source. This makes partial-stack transfers safe: a remaining source
    /// stack retains its original metadata.
    /// </summary>
    internal static bool MergeInto(
        IDictionary<string, string>? destination,
        IDictionary<string, string>? source)
    {
        if (destination == null)
        {
            return false;
        }

        Dictionary<string, MergePolicy> policies = GetPoliciesSnapshot();
        bool changed = false;
        foreach (KeyValuePair<string, MergePolicy> policy in policies)
        {
            destination.TryGetValue(policy.Key, out string? destinationValue);
            string? sourceValue = null;
            source?.TryGetValue(policy.Key, out sourceValue);

            string? mergedValue;
            try
            {
                mergedValue = policy.Value.MergeValues(destinationValue, sourceValue);
            }
            catch
            {
                // A third-party policy must not break an inventory mutation. Its
                // field remains unchanged for this merge.
                continue;
            }

            if (mergedValue == null)
            {
                if (destination.Remove(policy.Key))
                {
                    changed = true;
                }

                continue;
            }

            if (!destination.TryGetValue(policy.Key, out string? currentValue) ||
                !string.Equals(currentValue, mergedValue, StringComparison.Ordinal))
            {
                destination[policy.Key] = mergedValue;
                changed = true;
            }
        }

        return changed;
    }

    private static bool AreCompatibleOneWay(
        IDictionary<string, string>? left,
        IDictionary<string, string>? right,
        IReadOnlyDictionary<string, MergePolicy> policies)
    {
        if (left == null || left.Count == 0)
        {
            return true;
        }

        foreach (KeyValuePair<string, string> entry in left)
        {
            if (policies.ContainsKey(entry.Key))
            {
                continue;
            }

            if (right == null ||
                !right.TryGetValue(entry.Key, out string? exactRightValue) ||
                !string.Equals(entry.Value, exactRightValue, StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }

    private static bool CanMerge(
        MergePolicy policy,
        string? leftValue,
        string? rightValue)
    {
        try
        {
            return policy.CanMerge(leftValue, rightValue);
        }
        catch
        {
            // Third-party validation is fail-closed: invalid or throwing
            // metadata must never relax stack identity.
            return false;
        }
    }

    private static Dictionary<string, string>? ToDictionary(
        IReadOnlyList<KeyValuePair<string, string>>? entries)
    {
        Dictionary<string, string> result = new(StringComparer.Ordinal);
        if (entries == null)
        {
            return result;
        }

        foreach (KeyValuePair<string, string> entry in entries)
        {
            if (result.ContainsKey(entry.Key))
            {
                return null;
            }

            result.Add(entry.Key, entry.Value);
        }

        return result;
    }

    private static Dictionary<string, MergePolicy> GetPoliciesSnapshot()
    {
        lock (Sync)
        {
            // Registrations use copy-on-write, so the returned dictionary is an
            // immutable snapshot and callbacks can run without holding Sync.
            return MergePolicies;
        }
    }

    private sealed class MergePolicy
    {
        internal MergePolicy(
            Func<string?, string?, string?> mergeValues,
            Func<string?, string?, bool> canMerge)
        {
            MergeValues = mergeValues;
            CanMerge = canMerge;
        }

        internal Func<string?, string?, string?> MergeValues { get; }
        internal Func<string?, string?, bool> CanMerge { get; }
    }
}
