using System;
using System.Numerics;
using AnoMech.Core.Game.Ai;

namespace AnoMech.Scenarios.Top.P6AlphaOmega;

// The eight-way assignment shared by both P6 scenarios and both strats.
// MT=4, H1=D, D1=3, D2=2, D3=A, D4=1 everywhere; the original puts ST on C
// (south) and H2 on B (east), and the moogle strat swaps exactly those two.
internal static class TopP6ClockSpots
{
    internal static IAiMove Clock(float radius, bool moogle)
    {
        var diagonal = radius / MathF.Sqrt(2f);
        Vector2 st = moogle ? new(radius, 0f) : new(0f, radius);
        Vector2 h2 = moogle ? new(0f, radius) : new(radius, 0f);
        return AiMove.Create(new(-diagonal, -diagonal), st,
            new(-radius, 0f), h2, new(-diagonal, diagonal),
            new(diagonal, diagonal), new(0f, -radius), new(diagonal, -diagonal)).NaturalOrder();
    }

    // Wave cannon stack. The original stacks on B (east), the moogle strat on C
    // (south); both tanks stand 2y inside the rest, on the boss side of the stack.
    internal static IAiMove Stack(bool moogle)
    {
        Vector2 tank = moogle ? new(0f, 11.63f) : new(11.63f, 0f);
        Vector2 rest = moogle ? new(0f, 13.63f) : new(13.63f, 0f);
        return AiMove.Create(tank, tank, rest, rest, rest, rest, rest, rest).NaturalOrder();
    }

    // Second-arrow quadrants: same assignment, squared into lanes between the strips.
    internal static IAiMove Arrow(float diagonal, float cardinal, float offset, bool moogle)
    {
        Vector2 st = moogle ? new(cardinal, offset) : new(-offset, cardinal);
        Vector2 h2 = moogle ? new(-offset, cardinal) : new(cardinal, offset);
        return AiMove.Create(new(-diagonal, -diagonal), st,
            new(-cardinal, -offset), h2, new(-diagonal, diagonal),
            new(diagonal, diagonal), new(offset, -cardinal), new(diagonal, -diagonal)).NaturalOrder();
    }
}
