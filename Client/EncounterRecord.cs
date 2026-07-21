using System.Collections.Generic;

/// <summary>Aggregated stats for a single damage/heal source (or victim, for the "received" table).</summary>
public sealed class SourceStats
{
    public string Name;
    public long TotalAmount;
    public int HitCount;

    // Only populated on "received" rows: who dealt the damage this victim took.
    public Dictionary<string, long> BreakdownBySource;

    public void Add(long amount, string counterpartName = null)
    {
        TotalAmount += amount;
        HitCount++;

        if (counterpartName == null)
        {
            return;
        }

        BreakdownBySource ??= new Dictionary<string, long>();
        BreakdownBySource.TryGetValue(counterpartName, out long existing);
        BreakdownBySource[counterpartName] = existing + amount;
    }
}

public sealed class EncounterRecord
{
    public string Name = "Combat";
    public bool InvolvesBoss;
    public float StartTime;
    public float LastEventTime;
    public bool IsActive = true;

    public readonly Dictionary<string, SourceStats> Dealt = new();
    public readonly Dictionary<string, SourceStats> Received = new();
    public readonly Dictionary<string, SourceStats> Healing = new();

    public float Duration => LastEventTime - StartTime;

    public static SourceStats GetOrAdd(Dictionary<string, SourceStats> table, string name)
    {
        if (!table.TryGetValue(name, out SourceStats stats))
        {
            stats = new SourceStats { Name = name };
            table[name] = stats;
        }
        return stats;
    }
}
