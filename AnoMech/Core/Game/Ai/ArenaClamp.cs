using System.Numerics;

namespace AnoMech.Core.Game.Ai;

// Jitter exists to keep the party from standing in unnaturally perfect formation, not to walk it into the
// arena fence. Strats author spots within a yalm of the fence, where a jitter roll outward is lethal, so the
// jittered destination is pulled back to a margin the fence cannot reach.
public static class ArenaClamp
{
    public const float Margin = 0.5f;

    // `radius` of 0 means the scenario enforces no fence, and the point is left alone.
    public static Vector2 Inside(Vector2 point, float radius)
    {
        var limit = radius - Margin;
        if (radius <= 0f || limit <= 0f) return point;
        var distance = point.Length();
        return distance <= limit ? point : point * (limit / distance);
    }
}
