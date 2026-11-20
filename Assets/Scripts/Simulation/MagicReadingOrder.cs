using System;
using System.Collections.Generic;

/// <summary>One slot's persisted reading rank. Slots, including empty slots, own layout policy.</summary>
public readonly struct SlotReadingRank
{
    public int SlotIndex { get; }
    public int Rank { get; }
    public SlotReadingRank(int slotIndex, int rank) { SlotIndex = slotIndex; Rank = rank; }
}

/// <summary>
/// Pure slot-to-reading-rank policy. Views capture their layout into this state; commands, scripts,
/// save/restore and battle sessions read the same state. Moving a magic changes its slot, not policy.
/// Missing/new slots use slot order until a presentation adapter commits a new layout policy.
/// </summary>
public sealed class MagicReadingOrder
{
    private readonly List<SlotReadingRank> slots = new List<SlotReadingRank>();
    public int SlotSpan { get; private set; }

    public int GetRank(int slotIndex)
    {
        for (int i = 0; i < slots.Count; i++) if (slots[i].SlotIndex == slotIndex) return slots[i].Rank;
        return slotIndex;
    }

    public void Set(IReadOnlyList<SlotReadingRank> values)
    {
        // Validate before replacing an active policy so malformed snapshots cannot partly mutate it.
        var copy = new List<SlotReadingRank>();
        var seen = new HashSet<int>();
        int span = 0;
        for (int i = 0; values != null && i < values.Count; i++)
        {
            SlotReadingRank value = values[i];
            if (value.SlotIndex < 0 || value.Rank < 0 || !seen.Add(value.SlotIndex))
                throw new ArgumentException("Reading policy requires unique nonnegative slots and ranks.", nameof(values));
            copy.Add(value);
            span = Math.Max(span, value.SlotIndex + 1);
        }
        copy.Sort((a, b) => a.SlotIndex.CompareTo(b.SlotIndex));
        slots.Clear(); slots.AddRange(copy); SlotSpan = span;
    }

    public IReadOnlyList<SlotReadingRank> Export() => slots.ToArray();

    public void ApplyTo(IReadOnlyList<MagicModel> magics)
    {
        for (int i = 0; magics != null && i < magics.Count; i++)
            if (magics[i] != null) magics[i].ReadingRank = GetRank(magics[i].SlotIndex);
    }

    public IReadOnlyList<int> Capture(IReadOnlyList<MagicModel> magics)
    {
        var result = new int[magics != null ? magics.Count : 0];
        for (int i = 0; i < result.Length; i++) result[i] = magics[i] != null ? GetRank(magics[i].SlotIndex) : i;
        return Array.AsReadOnly(result);
    }
}
