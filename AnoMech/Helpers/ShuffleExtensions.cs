using System;
using System.Collections.Generic;
using System.Linq;

namespace AnoMech.Helpers;

// Stands in for .NET 10's Enumerable.Shuffle, which the net9.0 target of the
// API13 SDK does not have. Same contract as upstream: a new randomly-ordered
// sequence drawn from Random.Shared, source left untouched.
internal static class ShuffleExtensions
{
    public static IEnumerable<T> Shuffle<T>(this IEnumerable<T> source)
    {
        var buffer = source.ToArray();
        Random.Shared.Shuffle(buffer);
        return buffer;
    }
}
