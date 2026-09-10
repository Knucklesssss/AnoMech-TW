using System;
using System.Collections.Generic;
using System.Numerics;
using AnoMech.Core;
using AnoMech.Core.Game.Ai;
using AnoMech.Core.Game.Party;
using AnoMech.Core.SimObjects;
using static AnoMech.Scenarios.Top.TopConstants;

namespace AnoMech.Scenarios.Top.P3HelloWorld;

public sealed class TopP3HelloWorldAi : IScenarioAi<TopP3HelloWorldState>
{
    public string Name => "tuuufless";

    private TopP3HelloWorldState state = null!;
    private SimWorld world = null!;
    private readonly bool[] towerBugSeen = new bool[8];

    private const float WaymarkRing = 13.63f;

    private static readonly float[] TowersAppear = [53.4f, 74.5f, 95.5f, 116.6f];
    private static readonly float[] Resolve = [63.2f, 84.2f, 105.2f, 126.2f];

    private static readonly PartyRole[] LeftToRight =
    [
        PartyRole.RegenHealer, PartyRole.MainTank, PartyRole.OffTank, PartyRole.MeleeDpsA,
        PartyRole.MeleeDpsB, PartyRole.PhysRangedDps, PartyRole.CasterDps, PartyRole.ShieldHealer,
    ];

    private enum Job { Defamation, Stack, NearTether, FarTether }

    private static readonly Job[][] JobForSlot =
    [
        [Job.Stack,      Job.Defamation, Job.FarTether,  Job.NearTether],
        [Job.NearTether, Job.FarTether,  Job.Stack,      Job.Defamation],
        [Job.Defamation, Job.Stack,      Job.NearTether, Job.FarTether],
        [Job.FarTether,  Job.NearTether, Job.Defamation, Job.Stack],
    ];

    private static readonly Vector2[][] DefamationTowers =
    [
        [new(14f, 0f), new(0f, -14f)],
        [new(0f, -14f), new(-14f, 0f)],
        [new(9.9f, 9.9f), new(9.9f, -9.9f)],
        [new(-9.9f, -9.9f), new(-9.9f, 9.9f)],
    ];

    private static readonly Vector2[][] StackTowers =
    [
        [new(0f, 14f), new(-14f, 0f)],
        [new(14f, 0f), new(0f, 14f)],
        [new(-9.9f, 9.9f), new(-9.9f, -9.9f)],
        [new(9.9f, -9.9f), new(9.9f, 9.9f)],
    ];

    public void Run(TopP3HelloWorldState s, SimWorld world)
    {
        state = s;
        this.world = world;
        var ai = new AiManager(world);

        // Ley lines sweep outward twice (7.0/14.1/16.1/18.2 and 15.1/22.2/24.3/26.3) with the
        // two arm groups going off at 22.1 and 24.6, so the spread has to be held early and
        // then pulled in between the last two rings.
        ai.Move(7f, () => TransitionSpread(0), arrivalTime: 13f);
        ai.Move(22.6f, () => TransitionSpread(1), arrivalTime: 25.5f);
        ai.Move(27f, GatherOnTheBoss, arrivalTime: 42f);

        for (var i = 0; i < 4; i++)
        {
            var round = i;
            // The debuff clears six to ten seconds after Hello World resolves and the next
            // towers spawn roughly eleven after, so the pass has that gap and no more. Polled
            // rather than fired once because the clear time drifts by up to three seconds
            // between rounds; it runs right up to the next TakeTowers, which then takes over.
            var until = round < 3 ? TowersAppear[round + 1] : Resolve[round] + 12f;
            ai.Move(TowersAppear[round], () => TakeTowers(round), arrivalTime: Resolve[round] - 3f);
            for (var at = Resolve[round] - 3f; at < until; at += 0.5f)
                ai.Move(at, () => ReachForThePass(round), jitter: 0f);
        }
    }

    // Transition: the two high-powered cannons pair up with the two undebuffed players on
    // the north edge, the four plain cannons spread over the middle and south. The guide
    // leaves the left/right split open, so this follows the party's own HTDH order.
    // `sample` picks which of the two recorded snapshots to walk to.
    // Medians of where this party actually stood across the thirteen recorded P3 pulls,
    // sampled once the spread is set (13s) and again just before the cannons land (25.5s).
    // The two samples barely differ: the party takes its spots early and only trims.
    private static readonly Vector2[][] TransitionStacks =
    [
        [new(-9.9f, -14.2f), new(11.1f, -14.1f)],
        [new(-8.3f, -15.9f), new(7.9f, -14.8f)],
    ];

    // Left to right, which is the order the party's HTDH call fills them in.
    private static readonly Vector2[][] TransitionSpreads =
    [
        [new(-17.7f, 0f), new(-8.6f, 14.4f), new(7.3f, 14.9f), new(17.4f, 0f)],
        [new(-16.7f, -1.6f), new(-9.1f, 15.2f), new(9.9f, 15.0f), new(18.2f, -1.2f)],
    ];

    // Who is marked is fixed by the slot roll: member 0 of slots 1 and 3 takes a shared
    // cannon, slot 0 goes unmarked, and the remaining four spread.
    private IAiMove TransitionSpread(int sample)
    {
        var coords = new Vector2?[8];
        var roles = new PartyRole[8];
        var next = 0;

        void Place(PartyRole role, Vector2 at)
        {
            coords[next] = at;
            roles[next++] = role;
        }

        var shared = LeftToRightOrder(state.At(1, 0), state.At(3, 0));
        var unmarked = LeftToRightOrder(state.At(0, 0), state.At(0, 1));
        for (var side = 0; side < 2; side++)
        {
            Place(shared[side], TransitionStacks[sample][side]);
            Place(unmarked[side], TransitionStacks[sample][side]);
        }

        var spread = LeftToRightOrder(state.At(1, 1), state.At(2, 0), state.At(2, 1), state.At(3, 1));
        for (var i = 0; i < spread.Length; i++) Place(spread[i], TransitionSpreads[sample][i]);

        return AiMove.Create(coords).Assignments(roles);
    }

    private static PartyRole[] LeftToRightOrder(params PartyRole[] roles)
    {
        var ordered = (PartyRole[])roles.Clone();
        Array.Sort(ordered, (a, b) => Array.IndexOf(LeftToRight, a) - Array.IndexOf(LeftToRight, b));
        return ordered;
    }

    // Before the first towers: tethered pairs inside Omega's hitbox, poison pairs just
    // outside it, so nobody is standing where a tower is about to land.
    private IAiMove GatherOnTheBoss()
    {
        return ForEachMember((slot, member) =>
            JobForSlot[0][slot] is Job.NearTether or Job.FarTether ? Ray(slot, member) * 2.5f
                                                                   : Ray(slot, member) * 6f);
    }

    // Poison holders stand in their tower. The near pair has to stay apart until it wants
    // its tether to go off, so it waits on the axis at right angles to the Defamation
    // towers; the far pair has the opposite problem and waits shoulder to shoulder between
    // the stack towers.
    private IAiMove TakeTowers(int round)
    {
        var defamation = DefamationTowers[round];
        var stack = StackTowers[round];
        return ForEachMember((slot, member) => JobForSlot[round][slot] switch
        {
            Job.Defamation => defamation[member],
            Job.Stack => stack[member],
            Job.NearTether => Rotate(Normalize(defamation[0] + defamation[1]),
                                     (member == 0 ? -1f : 1f) * MathF.PI / 2f) * 19f,
            _ => Between(stack) * 0.7f + Perpendicular(stack) * (member == 0 ? -1.8f : 1.8f),
        });
    }

    // Towers are spent: only now does the Defamation holder step onto the nearest waymark
    // and the tether pairs walk in to collect. Until the towers land, everyone holds the
    // spot they took, so a poll that fires early is a no-op rather than a false start.
    private IAiMove ReachForThePass(int round)
    {
        var defamation = DefamationTowers[round];
        var stack = StackTowers[round];
        // Each side watches only the holders it is walking to, so the near pair is not held
        // up by a Stack player still owing their tower a visit, or the far pair by a
        // Defamation one.
        var defamationDone = TowerRunFinished(round, Job.Defamation);
        var stackDone = TowerRunFinished(round, Job.Stack);
        var last = round == 3;
        return ForEachMember((slot, member) => JobForSlot[round][slot] switch
        {
            Job.Defamation => defamationDone ? Normalize(defamation[member]) * WaymarkRing : defamation[member],
            Job.Stack => stack[member],
            // The last round hands nothing on, so the near pair joins the far pair on the
            // stack side, clear of the Defamation, and breaks its own tether first.
            Job.NearTether when last => Between(stack) * 0.7f + Perpendicular(stack) * (member == 0 ? -3.5f : 3.5f),
            Job.NearTether => defamationDone ? PassTarget(round, slot, member, Job.Defamation)
                                             : WaitingSpot(round, slot, member),
            _ => stackDone ? PassTarget(round, slot, member, Job.Stack)
                           : WaitingSpot(round, slot, member),
        });
    }

    // The cue is the holders' own debuff being gone, nothing else — no timer, no duration.
    // The Defamation holder carries Performance and the Stack holder Underflow while they
    // still owe the tower a visit. Latched, because "not carrying" is equally true for the
    // whole run-up before it ever lands.
    private bool TowerRunFinished(int round, Job job)
    {
        var carrying = false;
        for (var slot = 0; slot < TopP3HelloWorldState.SlotCount; slot++)
        {
            if (JobForSlot[round][slot] != job) continue;
            for (var member = 0; member < 2; member++)
                if (world.Party.Get(state.At(slot, member)) is { } holder &&
                    (holder.HasStatus(StatusId.LatentDefectUnderflow) ||
                     holder.HasStatus(StatusId.LatentDefectPerformance)))
                    carrying = true;
        }

        var latch = round * 2 + (job == Job.Defamation ? 0 : 1);
        if (carrying) towerBugSeen[latch] = true;
        return towerBugSeen[latch] && !carrying;
    }

    // Send each half of a tethered pair to whichever holder is the shorter walk, so the
    // player waiting on the west side never crosses the arena to a holder their partner is
    // already standing beside.
    private Vector2 PassTarget(int round, int slot, int member, Job holders)
    {
        var target = HolderPositions(round, holders);
        if (target.Length < 2) return CurrentPosition(state.At(slot, member));
        var first = CurrentPosition(state.At(slot, 0));
        var second = CurrentPosition(state.At(slot, 1));
        var straight = Vector2.Distance(first, target[0]) + Vector2.Distance(second, target[1]);
        var crossed = Vector2.Distance(first, target[1]) + Vector2.Distance(second, target[0]);
        var forFirst = straight <= crossed ? 0 : 1;
        return target[member == 0 ? forFirst : 1 - forFirst];
    }

    private Vector2[] HolderPositions(int round, Job job)
    {
        var found = new List<Vector2>();
        for (var slot = 0; slot < TopP3HelloWorldState.SlotCount; slot++)
        {
            if (JobForSlot[round][slot] != job) continue;
            for (var member = 0; member < 2; member++) found.Add(CurrentPosition(state.At(slot, member)));
        }

        return found.ToArray();
    }

    private Vector2 CurrentPosition(PartyRole role) =>
        world.Party.Get(role) is { } member ? new Vector2(member.Position.X, member.Position.Z) : Vector2.Zero;

    private Vector2 WaitingSpot(int round, int slot, int member)
    {
        var defamation = DefamationTowers[round];
        var stack = StackTowers[round];
        return JobForSlot[round][slot] == Job.NearTether
                   ? Rotate(Normalize(defamation[0] + defamation[1]), (member == 0 ? -1f : 1f) * MathF.PI / 2f) * 19f
                   : Between(stack) * 0.7f + Perpendicular(stack) * (member == 0 ? -1.8f : 1.8f);
    }

    private IAiMove ForEachMember(Func<int, int, Vector2> position)
    {
        var coords = new Vector2?[8];
        var roles = new PartyRole[8];
        for (var slot = 0; slot < TopP3HelloWorldState.SlotCount; slot++)
        for (var member = 0; member < 2; member++)
        {
            coords[slot * 2 + member] = position(slot, member);
            roles[slot * 2 + member] = state.At(slot, member);
        }

        return AiMove.Create(coords).Assignments(roles);
    }

    private static Vector2 Ray(int slot, int member)
    {
        var angle = MathF.PI / 4f * (slot * 2 + member);
        return new Vector2(MathF.Sin(angle), -MathF.Cos(angle));
    }

    private static Vector2 Between(Vector2[] pair) => Normalize(pair[0] + pair[1]) * 14f;

    private static Vector2 Rotate(Vector2 v, float radians)
    {
        var cos = MathF.Cos(radians);
        var sin = MathF.Sin(radians);
        return new Vector2(v.X * cos - v.Y * sin, v.X * sin + v.Y * cos);
    }

    private static Vector2 Perpendicular(Vector2[] pair)
    {
        var mid = Normalize(pair[0] + pair[1]);
        return new Vector2(-mid.Y, mid.X);
    }

    private static Vector2 Normalize(Vector2 v)
    {
        var length = v.Length();
        return length < 0.001f ? new Vector2(0f, -1f) : v / length;
    }
}
