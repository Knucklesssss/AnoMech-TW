using System;
using System.Numerics;

namespace AnoMech.Scenarios.Top.P6AlphaOmega;

internal static class TopP6LimitBreakRules
{
    internal static bool IsTankLbActive(float completedAt, float now)
        => now >= completedAt && now - completedAt < 8f;

    internal static bool Hits(Vector2 point, Vector2 origin, float rotation, float range, float halfWidth, bool circle)
    {
        var offset = point - origin;
        if (circle) return offset.LengthSquared() <= range * range;
        var forward = new Vector2(MathF.Sin(rotation), MathF.Cos(rotation));
        var along = Vector2.Dot(offset, forward);
        return along >= 0f && along <= range &&
            MathF.Abs(offset.X * forward.Y - offset.Y * forward.X) <= halfWidth;
    }
}
