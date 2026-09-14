using System;

namespace AnoMech.Core.Game;

// Single source of randomness for simulation code. Solo play draws from Random.Shared as before.
// In a multiplayer run every machine reseeds from the host's seed at each fixed tick, so the same
// code draws the same numbers everywhere. AI runs only on the host, so its draws go through a
// HostOnly scope and never advance the shared deterministic stream.
public static class SimRandom
{
    private static Random? deterministic;
    private static int hostOnlyDepth;

    public static Random Current => deterministic != null && hostOnlyDepth == 0 ? deterministic : Random.Shared;

    public static bool InHostOnly => hostOnlyDepth > 0;

    public static void Reseed(ulong seed, long tick) => deterministic = new Random(Mix(seed, tick));

    public static void Disable() => deterministic = null;

    public static HostOnlyScope HostOnly()
    {
        hostOnlyDepth++;
        return new HostOnlyScope();
    }

    public readonly struct HostOnlyScope : IDisposable
    {
        public void Dispose() => hostOnlyDepth--;
    }

    private static int Mix(ulong seed, long tick)
    {
        var z = seed + 0x9E3779B97F4A7C15UL * (ulong)(tick + 1);
        z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
        z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
        z ^= z >> 31;
        return (int)(z & 0x7FFFFFFF);
    }
}
