using System;
using System.Collections.Generic;
using System.Linq;
using AnoMech.Core.Game;

namespace AnoMech.Helpers;

// Stands in for .NET 10's Enumerable.Shuffle, which the net9.0 target of the
// API13 SDK does not have. Same contract as upstream: a new randomly-ordered
// sequence drawn from SimRandom.Current, source left untouched.
internal static class ShuffleExtensions
{
    public static IEnumerable<T> Shuffle<T>(this IEnumerable<T> source)
    {
        var buffer = source.ToArray();
        SimRandom.Current.Shuffle(buffer);
        return buffer;
    }
}
