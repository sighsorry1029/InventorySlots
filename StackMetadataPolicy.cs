using System;
using System.Collections.Generic;
using System.Globalization;

namespace InventorySlots;

/// <summary>
/// Central policy for custom-data fields whose values may be combined when two
/// otherwise identical item stacks are merged. Unregistered fields always remain
/// part of the exact stack identity.
/// </summary>
internal static class StackMetadataPolicy
{
    internal const string BeingSpoiledExpiryWorldTicksKey =
        "sighsorry.BeingSpoiled.ExpiryWorldTicks";

    private static readonly object Sync = new();
    private static Dictionary<string, MergePolicy> MergePolicies =
        new(StringComparer.Ordinal)
        {
            [BeingSpoiledExpiryWorldTicksKey] = new MergePolicy(
                MergeBeingSpoiledClock,
                CanMergeBeingSpoiledClock,
                replaceableFallback: true)
        };
    private static Func<long?>? _worldTicksProvider;

    internal static bool Register(
        string key,
        Func<string?, string?, string?> mergeValues)
    {
        if (string.IsNullOrWhiteSpace(key) || mergeValues == null)
        {
            return false;
        }

        lock (Sync)
        {
            if (MergePolicies.TryGetValue(key, out MergePolicy? existing))
            {
                // BeingSpoiled can replace InventorySlots' load-order-safe
                // fallback with its authoritative implementation once it loads.
                // A real registration remains first-wins.
                if (existing.ReplaceableFallback)
                {
                    Dictionary<string, MergePolicy> updated =
                        new(MergePolicies, StringComparer.Ordinal);
                    updated[key] = new MergePolicy(
                        mergeValues,
                        existing.CanMergeValues,
                        replaceableFallback: false);
                    MergePolicies = updated;
                    return true;
                }

                return false;
            }

            Dictionary<string, MergePolicy> added =
                new(MergePolicies, StringComparer.Ordinal);
            added.Add(
                key,
                new MergePolicy(
                    mergeValues,
                    AlwaysCompatible,
                    replaceableFallback: false));
            MergePolicies = added;
            return true;
        }
    }

    internal static void SetWorldTicksProvider(Func<long?>? worldTicksProvider)
    {
        lock (Sync)
        {
            _worldTicksProvider = worldTicksProvider;
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
        return AreCompatibleOneWay(left, right, policies) &&
               AreCompatibleOneWay(right, left, policies);
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

    /// <summary>
    /// Load-order-safe fallback for BeingSpoiled's signed single-key clock.
    /// Positive values are absolute world deadlines; negative values are frozen
    /// remaining ticks. The shorter effective duration wins, but the destination
    /// keeps its running or paused state.
    /// </summary>
    internal static string? MergeBeingSpoiledClock(
        string? destinationValue,
        string? sourceValue)
    {
        bool hasDestination = TryParseCanonicalBeingSpoiledClock(
            destinationValue,
            out long destinationClock);
        bool hasSource = TryParseCanonicalBeingSpoiledClock(
            sourceValue,
            out long sourceClock);

        if (!hasDestination)
        {
            // A missing destination inherits a valid source. A malformed or
            // unknown destination is preserved so a future format is never
            // overwritten by this fallback.
            return destinationValue == null && hasSource
                ? FormatClock(sourceClock)
                : destinationValue;
        }

        if (!hasSource)
        {
            return FormatClock(destinationClock);
        }

        bool destinationPaused = destinationClock < 0L;
        bool sourcePaused = sourceClock < 0L;
        if (destinationPaused == sourcePaused)
        {
            return destinationPaused
                ? FormatClock(Math.Max(destinationClock, sourceClock))
                : FormatClock(Math.Min(destinationClock, sourceClock));
        }

        if (!TryGetWorldTicks(out long nowTicks))
        {
            // Cross-state values cannot be compared without a common server
            // clock. Compatibility rejects this merge; keep this defensive
            // fallback non-destructive if a caller invokes MergeInto directly.
            return FormatClock(destinationClock);
        }

        long destinationRemaining = GetRemainingTicks(destinationClock, nowTicks);
        long sourceRemaining = GetRemainingTicks(sourceClock, nowTicks);
        long shorterRemaining = Math.Min(destinationRemaining, sourceRemaining);

        if (shorterRemaining <= 0L)
        {
            // Cold must never rescue a running clock that has already expired.
            // Switch even a paused destination back to a due running deadline so
            // BeingSpoiled expires it on its next authoritative update.
            return FormatClock(nowTicks);
        }

        if (destinationPaused)
        {
            return FormatClock(-shorterRemaining);
        }

        if (shorterRemaining == destinationRemaining)
        {
            return FormatClock(destinationClock);
        }

        return FormatClock(AddSaturating(nowTicks, shorterRemaining));
    }

    internal static bool CanMergeBeingSpoiledClock(
        string? leftValue,
        string? rightValue)
    {
        bool hasLeft = TryParseCanonicalBeingSpoiledClock(leftValue, out long leftClock);
        bool hasRight = TryParseCanonicalBeingSpoiledClock(rightValue, out long rightClock);

        if (!hasLeft || !hasRight)
        {
            if (leftValue == null)
            {
                return rightValue == null || hasRight;
            }

            if (rightValue == null)
            {
                return hasLeft;
            }

            // Equal unknown values can remain exact identity. Different invalid
            // or future formats must not be collapsed into one stack.
            return string.Equals(leftValue, rightValue, StringComparison.Ordinal);
        }

        return (leftClock < 0L) == (rightClock < 0L) || TryGetWorldTicks(out _);
    }

    internal static bool TryParseCanonicalBeingSpoiledClock(
        string? value,
        out long parsed)
    {
        if (value != null &&
            long.TryParse(
                value,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out parsed) &&
            parsed != 0L &&
            parsed != long.MinValue &&
            string.Equals(
                value,
                parsed.ToString(CultureInfo.InvariantCulture),
                StringComparison.Ordinal))
        {
            return true;
        }

        parsed = 0L;
        return false;
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
            if (policies.TryGetValue(entry.Key, out MergePolicy? policy))
            {
                string? policyRightValue = null;
                right?.TryGetValue(entry.Key, out policyRightValue);
                try
                {
                    if (!policy.CanMergeValues(entry.Value, policyRightValue))
                    {
                        return false;
                    }
                }
                catch
                {
                    return false;
                }

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

    private static bool TryGetWorldTicks(out long ticks)
    {
        Func<long?>? provider;
        lock (Sync)
        {
            provider = _worldTicksProvider;
        }

        try
        {
            long? provided = provider?.Invoke();
            if (provided is > 0L)
            {
                ticks = provided.Value;
                return true;
            }
        }
        catch
        {
            // A clock provider must not break an inventory mutation.
        }

        ticks = 0L;
        return false;
    }

    private static long GetRemainingTicks(long clock, long nowTicks) =>
        clock < 0L ? -clock : clock - nowTicks;

    private static long AddSaturating(long left, long right) =>
        right > long.MaxValue - left ? long.MaxValue : left + right;

    private static string FormatClock(long clock) =>
        clock.ToString(CultureInfo.InvariantCulture);

    private static bool AlwaysCompatible(string? _, string? __) => true;

    private sealed class MergePolicy
    {
        internal MergePolicy(
            Func<string?, string?, string?> mergeValues,
            Func<string?, string?, bool> canMergeValues,
            bool replaceableFallback)
        {
            MergeValues = mergeValues;
            CanMergeValues = canMergeValues;
            ReplaceableFallback = replaceableFallback;
        }

        internal Func<string?, string?, string?> MergeValues { get; }
        internal Func<string?, string?, bool> CanMergeValues { get; }
        internal bool ReplaceableFallback { get; }
    }
}
