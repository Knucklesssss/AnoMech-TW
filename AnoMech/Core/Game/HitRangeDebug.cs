using System;
using System.Collections.Generic;
using System.Numerics;

namespace AnoMech.Core.Game;

public enum HitRangeShape { Circle, Ring, Cone, Rect, Cross }

// A/B: circle radius; ring inner/outer; cone half-angle/range; rect half-width/length; cross half-width/half-length.
public readonly record struct HitRange(HitRangeShape Shape, Vector3 Origin, float Rotation,
    float A, float B, float ExpiresAt, bool Hit, float Remaining = 0f, string Label = "");

public static class HitRangeDebug
{
    public const float DisplaySeconds = 2f;
    private static readonly List<HitRange> ranges = new(64);
    private static float clock;
    public static bool Enabled { get; set; }
    public static string NextLabel { get; set; } = "";

    public static void Tick(float deltaSeconds)
    {
        clock += deltaSeconds;
        ranges.RemoveAll(range => range.ExpiresAt <= clock);
    }

    public static void Clear()
    {
        ranges.Clear();
        NextLabel = "";
        clock = 0f;
    }

    public static IReadOnlyList<HitRange> Snapshot()
        => ranges.ConvertAll(range => range with { Remaining = range.ExpiresAt - clock });

    public static void Record(HitRangeShape shape, Vector3 origin, float rotation, float a, float b, bool hit)
    {
        if (!Enabled) return;
        if (ranges.Count >= 64) ranges.RemoveAt(0);
        ranges.Add(new(shape, origin, rotation, a, b, clock + DisplaySeconds, hit, Label: NextLabel));
    }
}
