using System;
using System.Collections.Generic;

namespace AnoMech.Core.Game;

// Time-based event queue. Owned by Game; scenarios schedule actions via the
// world.Events passthrough (world.Events.Add(offset, ...)) and SimObjects that
// need to schedule receive the instance via constructor injection. Game.Tick
// advances the scheduler (scaled by Game.EventTimeScale) before World.Tick;
// Game.ResetInternal clears it between scenarios.
public sealed class EventScheduler
{
    private readonly List<Entry> entries = new();
    private float elapsed;

    public void Add(float offset, Action action)
    {
        var time = elapsed + MathF.Max(0f, offset);
        var index = entries.FindIndex(e => e.Time > time);
        var entry = new Entry(time, action, SimRandom.InHostOnly);
        if (index < 0) entries.Add(entry);
        else entries.Insert(index, entry);
    }

    public void Tick(float deltaSeconds)
    {
        elapsed += deltaSeconds;
        while (entries.Count > 0 && entries[0].Time <= elapsed)
        {
            var entry = entries[0];
            entries.RemoveAt(0);
            if (!entry.HostOnly)
            {
                entry.Action();
                continue;
            }
            using var scope = SimRandom.HostOnly();
            entry.Action();
        }
    }

    public void Clear()
    {
        entries.Clear();
        elapsed = 0f;
    }

    // HostOnly: scheduled from AI, which clients never run; see SimRandom.
    private readonly record struct Entry(float Time, Action Action, bool HostOnly);
}
