using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using AnoMech.Core.Game.Party;

namespace AnoMech.Scenarios.Top.P3HelloWorld;

public enum TopP3TransitionStep { Outer, Inner, Resolve }

public static class TopP3TransitionRules
{
    // Geometry: awgil/ffxiv_bossmod, TOP/P3Intermission.cs. Coordinates are local XZ.
    public const float ArmRadius = 11f;
    public const float CannonRadius = 6f;
    public const float CenterRadius = 6f;

    private static readonly PartyRole[] Priority =
    [
        PartyRole.RegenHealer, PartyRole.MainTank, PartyRole.OffTank, PartyRole.MeleeDpsA,
        PartyRole.MeleeDpsB, PartyRole.PhysRangedDps, PartyRole.CasterDps, PartyRole.ShieldHealer,
    ];

    // Slots: two north stack pairs, then west / southwest / southeast / east spreads.
    // Angles leave room at both the 11y arm boundary and the 6y cannon boundary.
    private static readonly float[] SouthFirstWait = [-23, -23, 23, 23, -97, -143, 143, 97];
    private static readonly float[] SouthFirstResolve = [-45, -45, 45, 45, -75, -165, 165, 75];
    private static readonly float[] NorthFirstWait = [-38, -38, 38, 38, -82, -158, 158, 82];
    private static readonly float[] NorthFirstResolve = [-13, -13, 13, 13, -105, -135, 135, 105];

    public static PartyRole[] Assign(IReadOnlyList<PartyRole> shuffled, PartyRole? player = null, int? forcedSlot = null)
    {
        PartyRole[] Ordered(int start, int count) => shuffled.Skip(start).Take(count)
            .OrderBy(role => Array.IndexOf(Priority, role)).ToArray();
        var high = Ordered(0, 2);
        var unmarked = Ordered(2, 2);
        PartyRole[] result = [high[0], unmarked[0], high[1], unmarked[1], .. Ordered(4, 4)];
        if (player is { } role && forcedSlot is >= 0 and < 8)
        {
            var current = Array.IndexOf(result, role);
            (result[current], result[forcedSlot.Value]) = (result[forcedSlot.Value], result[current]);
        }
        return result;
    }

    public static Vector2 PositionFor(int slot, bool firstArmsSouth, TopP3TransitionStep step)
    {
        var angles = (firstArmsSouth, step == TopP3TransitionStep.Resolve) switch
        {
            (true, false) => SouthFirstWait,
            (true, true) => SouthFirstResolve,
            (false, false) => NorthFirstWait,
            _ => NorthFirstResolve,
        };
        return Polar(angles[slot], step == TopP3TransitionStep.Outer ? 19f : 16f);
    }

    public static Vector2 ArmPosition(bool south, int arm) => Polar((south ? 180f : 0f) + arm * 120f, 14f);

    public static bool RingContains(Vector2 position, int ring)
    {
        var distance = position.Length();
        return distance >= ring * 6f && distance <= (ring + 1) * 6f;
    }

    public static bool CenterContains(Vector2 position) => position.LengthSquared() <= CenterRadius * CenterRadius;

    // Freeze all eight positions before evaluating any circle: applying deaths first
    // would remove victims from subsequent simultaneous cannons and hide overlaps.
    public static bool[] FailedCannonSlots(IReadOnlyList<Vector2?> positions)
    {
        var hits = new int[8];
        var failed = new bool[8];
        for (var source = 0; source < 8; source++)
        {
            if (source is 1 or 3 || positions[source] is not { } center) continue;
            var victims = Enumerable.Range(0, 8).Where(i => positions[i] is { } position &&
                Vector2.DistanceSquared(center, position) <= CannonRadius * CannonRadius).ToArray();
            var expected = source is 0 or 2 ? 2 : 1;
            foreach (var victim in victims)
            {
                hits[victim]++;
                if (victims.Length != expected) failed[victim] = true;
            }
        }
        for (var i = 0; i < 8; i++)
            if (positions[i] != null && hits[i] != 1) failed[i] = true;
        return failed;
    }

    private static Vector2 Polar(float degrees, float radius)
    {
        var radians = degrees * MathF.PI / 180f;
        return new Vector2(MathF.Sin(radians), -MathF.Cos(radians)) * radius;
    }
}
