using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Plain (non-ECS) singleton that turns a stream of resolved combat events into
/// per-encounter, per-source stats for the overlay to render. Fed by
/// DamageMeterReceiveSystem, which runs on the main thread, so no locking needed.
/// </summary>
public sealed class CombatLogAggregator
{
    private const float EncounterTimeoutSeconds = 10f;
    private const int MaxHistory = 20;

    private static CombatLogAggregator _instance;
    public static CombatLogAggregator Instance => _instance ??= new CombatLogAggregator();

    public EncounterRecord Current { get; private set; }
    public readonly List<EncounterRecord> History = new();

    public void RecordEvent(string sourceName, string victimName, int amount, bool wasKilled, bool victimIsBoss, bool sourceIsPlayer, bool victimIsPlayer)
    {
        // Dealt = damage the party did (source must be a player); Received = damage the
        // party took (victim must be a player). Matches how Skada/Details! scope their
        // "damage done"/"damage taken" tabs - neither tracks wild-enemy-on-enemy combat.
        if (amount == 0 || !(sourceIsPlayer || victimIsPlayer))
        {
            return;
        }

        float now = Time.unscaledTime;

        if (Current == null)
        {
            Current = new EncounterRecord
            {
                StartTime = now,
                LastEventTime = now,
                Name = victimIsBoss ? victimName : "Combat",
                InvolvesBoss = victimIsBoss,
            };
        }

        Current.LastEventTime = now;
        if (victimIsBoss)
        {
            Current.InvolvesBoss = true;
            Current.Name = victimName;
        }

        if (amount < 0)
        {
            long dmg = -amount;
            if (sourceIsPlayer)
            {
                EncounterRecord.GetOrAdd(Current.Dealt, sourceName).Add(dmg);
            }
            if (victimIsPlayer)
            {
                EncounterRecord.GetOrAdd(Current.Received, victimName).Add(dmg, sourceName);
            }
        }
        else if (sourceIsPlayer)
        {
            EncounterRecord.GetOrAdd(Current.Healing, sourceName).Add(amount);
        }

        if (wasKilled && victimIsBoss)
        {
            CloseCurrent();
        }
    }

    /// <summary>Call once a frame (from the overlay's Update) to auto-close a stale encounter.</summary>
    public void Tick()
    {
        if (Current != null && Time.unscaledTime - Current.LastEventTime > EncounterTimeoutSeconds)
        {
            CloseCurrent();
        }
    }

    public void ResetCurrent()
    {
        if (Current != null)
        {
            CloseCurrent();
        }
    }

    private void CloseCurrent()
    {
        Current.IsActive = false;
        History.Insert(0, Current);
        while (History.Count > MaxHistory)
        {
            History.RemoveAt(History.Count - 1);
        }
        Current = null;
    }
}
