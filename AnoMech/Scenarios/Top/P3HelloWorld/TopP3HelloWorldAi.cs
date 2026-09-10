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
    private readonly bool[] towersResolved = new bool[8];
    private readonly int?[] towerAssignments = new int?[16];
    private readonly int?[] passAssignments = new int?[16];
    private readonly Vector2?[] departureTargets = new Vector2?[32];
    private readonly bool[] departuresComplete = new bool[32];

    private const float WaymarkRing = 13.63f;

    private static readonly float[] TowersAppear = [53.4f, 74.5f, 95.5f, 116.6f];
    private static readonly float[] Resolve = [63.2f, 84.2f, 105.2f, 126.2f];

    private enum Job { Defamation, Stack, NearTether, FarTether }

    private static readonly Job[][] JobForSlot =
    [
        [Job.Stack,      Job.Defamation, Job.FarTether,  Job.NearTether],
        [Job.NearTether, Job.FarTether,  Job.Stack,      Job.Defamation],
        [Job.Defamation, Job.Stack,      Job.NearTether, Job.FarTether],
        [Job.FarTether,  Job.NearTether, Job.Defamation, Job.Stack],
    ];

    internal static readonly Vector2[][] DefamationTowers =
    [
        [new(14f, 0f), new(0f, -14f)],
        [new(0f, -14f), new(-14f, 0f)],
        [new(9.9f, 9.9f), new(9.9f, -9.9f)],
        [new(-9.9f, -9.9f), new(-9.9f, 9.9f)],
    ];

    internal static readonly Vector2[][] StackTowers =
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
        Array.Clear(towersResolved);
        Array.Clear(towerAssignments);
        Array.Clear(passAssignments);
        Array.Clear(departureTargets);
        Array.Clear(departuresComplete);
        var ai = new AiManager(world);

        ai.Move(7f, () => TransitionSpread(TopP3TransitionStep.Outer), jitter: 0f);
        ai.Move(16.3f, () => TransitionSpread(TopP3TransitionStep.Inner), jitter: 0f);
        ai.Move(22.4f, () => TransitionSpread(TopP3TransitionStep.Outer), jitter: 0f);
        ai.Move(24.4f, () => TransitionSpread(TopP3TransitionStep.Resolve), jitter: 0f);
        ai.Move(29f, GatherOnTheBoss, arrivalTime: 42f);

        for (var i = 0; i < 4; i++)
        {
            var round = i;
            var until = round < 3 ? TowersAppear[round + 1] : Resolve[round] + 12f;
            for (var at = TowersAppear[round]; at < Resolve[round] - 3f; at += 0.2f)
            {
                var holdCenterPoison = round > 0 && at < TowersAppear[round] + 4.5f;
                ai.Move(at, () => ApproachTowers(round, holdCenterPoison), jitter: 0f);
            }
            for (var at = Resolve[round] - 3f; at < until; at += 0.5f)
                ai.Move(at, () => ReachForThePass(round), jitter: 0f);
        }
    }

    private IAiMove TransitionSpread(TopP3TransitionStep step)
    {
        var coords = new Vector2?[8];
        for (var i = 0; i < coords.Length; i++)
            coords[i] = TopP3TransitionRules.PositionFor(i, state.TransitionFirstArmsSouth, step);
        return AiMove.Create(coords).Assignments(state.TransitionRoles);
    }

    private IAiMove GatherOnTheBoss()
    {
        return ForEachMember((slot, member) =>
        {
            var radius = JobForSlot[0][slot] is Job.NearTether or Job.FarTether ? 2.5f : 6f;
            var first = Normalize(TowerPosition(0, slot, 0)) * radius;
            var second = Normalize(TowerPosition(0, slot, 1)) * radius;
            var assignment = NearestPairAssignment(slot, first, second);
            return (member == 0 ? assignment : 1 - assignment) == 0 ? first : second;
        });
    }

    private IAiMove TakeTowers(int round)
    {
        return ForEachMember((slot, member) => TowerPosition(round, slot, TowerMember(round, slot, member)));
    }

    private IAiMove ApproachTowers(int round, bool holdCenterPoison) => RouteToTowers(round, TakeTowers(round), holdCenterPoison);

    private IAiMove RouteToTowers(int round, IAiMove destinations, bool holdCenterPoison = false)
    {
        return ForEachMember((slot, member) =>
        {
            var role = state.At(slot, member);
            var target = destinations[(int)role]!.Value;
            var current = CurrentPosition(role);
            var player = world.Party.Get(role);
            var hasPoison = player?.HasStatus(StatusId.CriticalErrorUnderflow) == true ||
                            player?.HasStatus(StatusId.CriticalErrorPerformance) == true;
            if (holdCenterPoison && hasPoison && current.Length() < 7f) return current;
            var index = round * 8 + slot * 2 + member;
            if (hasPoison && !departuresComplete[index] && (departureTargets[index] != null || current.Length() < 7f))
            {
                var towers = JobForSlot[round][slot] == Job.Defamation ? DefamationTowers[round] : StackTowers[round];
                departureTargets[index] ??= Normalize(towers[TowerMember(round, slot, member)]) * 18f;
                if (Vector2.Distance(current, departureTargets[index]!.Value) > 0.3f) return departureTargets[index]!.Value;
                departuresComplete[index] = true;
            }
            if (current.Length() < 1f || Vector2.Distance(current, target) < 0.3f) return target;
            var angle = MathF.Atan2(current.X * target.Y - current.Y * target.X, Vector2.Dot(current, target));
            if (MathF.Abs(angle) < 0.2f) return target;
            var radius = hasPoison ? 18f : 8f;
            if (MathF.Abs(current.Length() - radius) > 0.3f) return Normalize(current) * radius;
            return Rotate(Normalize(current), Math.Clamp(angle, -0.2f, 0.2f)) * radius;
        });
    }

    private Vector2 TowerPosition(int round, int slot, int member) => JobForSlot[round][slot] switch
    {
        Job.Defamation => SpreadStand(DefamationTowers[round], member),
        Job.Stack => StackStand(StackTowers[round], member),
        _ => WaitingSpot(round, slot, member),
    };

    private int TowerMember(int round, int slot, int member)
    {
        var index = round * 4 + slot;
        towerAssignments[index] ??= NearestPairAssignment(slot, TowerPosition(round, slot, 0), TowerPosition(round, slot, 1));
        return member == 0 ? towerAssignments[index]!.Value : 1 - towerAssignments[index]!.Value;
    }

    private int NearestPairAssignment(int slot, Vector2 firstTarget, Vector2 secondTarget)
    {
        var first = CurrentPosition(state.At(slot, 0));
        var second = CurrentPosition(state.At(slot, 1));
        return Vector2.Distance(first, firstTarget) + Vector2.Distance(second, secondTarget) <=
               Vector2.Distance(first, secondTarget) + Vector2.Distance(second, firstTarget) ? 0 : 1;
    }

    private const float StackLean = 4.5f;
    private const float SpreadStandOff = 3f;

    private static Vector2 StackStand(Vector2[] stack, int member)
    {
        var tower = stack[member];
        return tower + Normalize(Between(stack) - tower) * StackLean;
    }

    private static Vector2 SpreadStand(Vector2[] defamation, int member)
    {
        var tower = defamation[member];
        return Normalize(tower) * (tower.Length() + SpreadStandOff);
    }

    private static Vector2 BesideTheStackTowers(Vector2[] stack, int member) =>
        Normalize(Between(stack)) * 14.8f + Perpendicular(stack) * (member == 0 ? -3.6f : 3.6f);

    private IAiMove ReachForThePass(int round)
    {
        var defamation = DefamationTowers[round];
        var stack = StackTowers[round];
        var defamationDone = TowersResolved(round, Job.Defamation);
        var stackDone = TowersResolved(round, Job.Stack);
        var nearHasPoison = PairHasStatus(round, Job.NearTether, StatusId.CriticalErrorPerformance);
        var farHasPoison = PairHasStatus(round, Job.FarTether, StatusId.CriticalErrorUnderflow);
        var last = round == 3;
        var move = ForEachMember((slot, actualMember) =>
        {
            var member = TowerMember(round, slot, actualMember);
            return JobForSlot[round][slot] switch
            {
                Job.Defamation => defamationDone ? Normalize(defamation[member]) * WaymarkRing
                                                : SpreadStand(defamation, member),
                Job.Stack => StackStand(stack, member),
                Job.NearTether when last => stackDone ? Between(stack) * 0.3f : BesideTheStackTowers(stack, member),
                Job.FarTether when last => stackDone ? Perpendicular(stack) * (member == 0 ? -10f : 10f)
                                                    : WaitingSpot(round, slot, member),
                Job.NearTether when nearHasPoison => Normalize(Between(defamation)) * WaymarkRing,
                Job.NearTether => defamationDone ? PassTarget(round, slot, actualMember, Job.Defamation)
                                                : WaitingSpot(round, slot, member),
                Job.FarTether when farHasPoison && !PairHasStatus(round, Job.FarTether, StatusId.HWRemoteTether) => Between(stack) * 0.3f,
                _ => stackDone ? PassTarget(round, slot, actualMember, Job.Stack)
                               : WaitingSpot(round, slot, member),
            };
        });
        return !defamationDone && !stackDone ? RouteToTowers(round, move) : move;
    }

    private bool TowersResolved(int round, Job job)
    {
        var latch = round * 2 + (job == Job.Defamation ? 0 : 1);
        var towerStatus = job == Job.Defamation ? StatusId.LatentDefectPerformance : StatusId.LatentDefectUnderflow;
        towersResolved[latch] |= PairHasStatus(round, job, towerStatus);
        return towersResolved[latch];
    }

    private bool PairHasStatus(int round, Job job, ushort status)
    {
        for (var slot = 0; slot < TopP3HelloWorldState.SlotCount; slot++)
        {
            if (JobForSlot[round][slot] != job) continue;
            return world.Party.Get(state.At(slot, 0))?.HasStatus(status) == true &&
                   world.Party.Get(state.At(slot, 1))?.HasStatus(status) == true;
        }
        return false;
    }

    private Vector2 PassTarget(int round, int slot, int member, Job holders)
    {
        var target = HolderPositions(round, holders);
        if (target.Length < 2) return CurrentPosition(state.At(slot, member));
        var index = round * 4 + slot;
        passAssignments[index] ??= NearestPairAssignment(slot, target[0], target[1]);
        var forFirst = passAssignments[index]!.Value;
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
        if (round == 3) return BesideTheStackTowers(stack, member);
        return JobForSlot[round][slot] == Job.NearTether
                   ? Rotate(Normalize(defamation[0] + defamation[1]), (member == 0 ? -1f : 1f) * MathF.PI / 2f) * WaymarkRing
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
