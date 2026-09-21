using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using AnoMech.Core;
using AnoMech.Core.Game;
using AnoMech.Core.Game.Party;
using static AnoMech.Scenarios.Top.TopConstants;

namespace AnoMech.Scenarios.Top.P5Delta;

public sealed class Side
{
    public uint DeltaOversampledWaveCannonActionId { get; }
    public uint SwivelCannonActionId { get; }
    public ushort MonitorDebuffId { get; }
    public int Mul { get; }
    public uint ArmUnitId { get; }
    public uint ArmUnitNameId { get; }
    public uint RotateLockonId { get; }

    private Side(uint waveCannonActionId, uint swivelCannonActionId, ushort monitorDebuffId, int mul, uint armUnitId, uint armUnitNameId, uint rotateLockonId)
    {
        DeltaOversampledWaveCannonActionId = waveCannonActionId;
        SwivelCannonActionId = swivelCannonActionId;
        MonitorDebuffId = monitorDebuffId;
        Mul = mul;
        ArmUnitId = armUnitId;
        ArmUnitNameId = armUnitNameId;
        RotateLockonId = rotateLockonId;
    }

    public static readonly Side Right = new(ActionId.OversampledWaveCannonRight, ActionId.SwivelCannonR, StatusId.PlayerMonitorRight,  -1, BNpcBaseId.RightArmUnit, BNpcNameId.RightArmUnit, LockonId.RotateCw);
    public static readonly Side Left = new(ActionId.OversampledWaveCannonLeft, ActionId.SwivelCannonL, StatusId.PlayerMonitorLeft, 1, BNpcBaseId.LeftArmUnit, BNpcNameId.LeftArmUnit, LockonId.RotateCcw);
}

public record NorthSouth(float Mul, byte EffectIndex)
{
    public static readonly NorthSouth North = new(1, 1);
    public static readonly NorthSouth South = new(-1, 5);
}

public sealed class TopP5DeltaState
{
    public IReadOnlyList<PartyRole> TetherOrder { get; }
    public NorthSouth EyeSpawn { get; }
    public IReadOnlyList<int> FistRotations { get; }  // length 6, three +1 and three -1
    public IReadOnlyList<uint> FistColors { get; }    // length 8, each half has two BNpcBaseId.RocketPunchYellow and two RocketPunchBlue
    public IReadOnlyList<Side> ArmHandedness { get; } // length 6, three Left and three Right
    public Side SwivelCannonSide { get; }
    public Side OmegaMonitorSide { get; }
    public Side PlayerMonitorSide { get; }
    public int PlayerMonitorIndex { get; }   // 0..3
    
    public PartyRole PlayerMonitorRole => TetherOrder[PlayerMonitorIndex];
    public int NearWorldTetherIndex { get; } // 0..3, distinct from FarWorldTetherIndex
    public int FarWorldTetherIndex { get; }  // 0..3, distinct from NearWorldTetherIndex

    public bool? BeyondDefenceForPlayer { get; }

    public PartyRole BeyondDefenseTarget { get; set; }
    public PartyRole NearWorldRole { get; set; }
    public PartyRole FarWorldRole { get; set; }
    public bool PunchExplosionUnmitigated { get; set; }
    public List<Vector3>? PunchTargets { get; set; }

    public int BeyondDefenseIndex()
    {
        return TetherOrder.Select((role, i) => (role, i))
                          .Where(t => t.role == BeyondDefenseTarget)
                          .Select(t => t.i)
                          .FirstOrDefault(0);
    }

    public TopP5DeltaState(TopP5DeltaStateOverrides overrides, PartyRole playerRole)
    {
        var rng = SimRandom.Current;

        var roles = ShuffleRoles(rng);

        // Resolve effective tether assignment, applying forced overrides in priority order:
        //   BD=Yes → close inner (highest priority)
        //   Monitor=Yes or HelloWorld=Near/Far on a far assignment → close any
        var effectiveTether = overrides.TetherAssignment;
        if (overrides.BeyondDefence == true)
            effectiveTether = PlayerTetherAssignment.CloseInner;
        else if ((overrides.Monitor == true ||
                  overrides.HelloWorld is HelloWorldOption.Near or HelloWorldOption.Far) &&
                 effectiveTether is PlayerTetherAssignment.FarAny
                     or PlayerTetherAssignment.FarInner or PlayerTetherAssignment.FarOuter or PlayerTetherAssignment.Auto)
            effectiveTether = PlayerTetherAssignment.CloseAny;

        // Slots 0-1 = close inner, 2-3 = close outer, 4-5 = far inner, 6-7 = far outer.
        int[]? validSlots = effectiveTether switch
        {
            PlayerTetherAssignment.CloseInner => new[] { 0, 1 },
            PlayerTetherAssignment.CloseOuter => new[] { 2, 3 },
            PlayerTetherAssignment.CloseAny   => new[] { 0, 1, 2, 3 },
            PlayerTetherAssignment.FarInner   => new[] { 4, 5 },
            PlayerTetherAssignment.FarOuter   => new[] { 6, 7 },
            PlayerTetherAssignment.FarAny     => new[] { 4, 5, 6, 7 },
            _                                  => null,
        };

        if (validSlots != null)
        {
            var currentIdx = Array.IndexOf(roles, playerRole);
            if (Array.IndexOf(validSlots, currentIdx) < 0)
            {
                var targetSlot = validSlots[rng.Next(validSlots.Length)];
                (roles[currentIdx], roles[targetSlot]) = (roles[targetSlot], roles[currentIdx]);
            }
        }

        ApplyAiTetherPins(roles, overrides, playerRole);

        TetherOrder = roles;
        var playerSlot = Array.IndexOf(roles, playerRole);
        var playerInClose = playerSlot < 4;

        EyeSpawn = overrides.EyeSpawn ?? (rng.Next(2) == 0 ? NorthSouth.North : NorthSouth.South);
        FistRotations = ShuffleInPlace(new[] { 1, 1, 1, -1, -1, -1 }, rng);
        ArmHandedness = ShuffleSides(rng);

        var colors = new uint[8];
        var first = ShuffleInPlace(new[] { BNpcBaseId.RocketPunchYellow, BNpcBaseId.RocketPunchYellow, BNpcBaseId.RocketPunchBlue, BNpcBaseId.RocketPunchBlue }, rng);
        var second = ShuffleInPlace(new[] { BNpcBaseId.RocketPunchYellow, BNpcBaseId.RocketPunchYellow, BNpcBaseId.RocketPunchBlue, BNpcBaseId.RocketPunchBlue }, rng);
        Array.Copy(first, 0, colors, 0, 4);
        Array.Copy(second, 0, colors, 4, 4);
        FistColors = colors;

        SwivelCannonSide = overrides.SwivelCannonSide ?? RandomSide(rng);
        OmegaMonitorSide = RandomSide(rng);
        PlayerMonitorSide = RandomSide(rng);

        PlayerMonitorIndex = (overrides.Monitor, playerInClose) switch
        {
            (true,  true) => playerSlot,
            (false, true) => RandomExclude(rng, 4, playerSlot),
            _             => rng.Next(4),
        };

        // Near/Far world tether index assignment (both are distinct slots from 0-3).
        int near, far;
        if (overrides.HelloWorld == HelloWorldOption.Near && playerInClose)
        {
            near = playerSlot;
            far  = RandomExclude(rng, 4, near);
        }
        else if (overrides.HelloWorld == HelloWorldOption.Far && playerInClose)
        {
            far  = playerSlot;
            near = RandomExclude(rng, 4, far);
        }
        else if (overrides.HelloWorld == HelloWorldOption.No && playerInClose)
        {
            near = RandomExclude(rng, 4, playerSlot);
            far  = RandomExclude2(rng, 4, near, playerSlot);
        }
        else
        {
            near = rng.Next(4);
            far  = rng.Next(3);
            if (far >= near) far++;
        }
        NearWorldTetherIndex = near;
        FarWorldTetherIndex  = far;
        NearWorldRole = TetherOrder[near];
        FarWorldRole  = TetherOrder[far];

        BeyondDefenceForPlayer = overrides.BeyondDefence;
    }

    // Random int in [0, max) excluding `exclude`.
    private static int RandomExclude(Random rng, int max, int exclude)
    {
        var v = rng.Next(max - 1);
        return v >= exclude ? v + 1 : v;
    }

    // Random int in [0, max) excluding both ex1 and ex2 (assumed distinct).
    private static int RandomExclude2(Random rng, int max, int ex1, int ex2)
    {
        var pool = new List<int>(max);
        for (int i = 0; i < max; i++)
            if (i != ex1 && i != ex2) pool.Add(i);
        return pool.Count > 0 ? pool[rng.Next(pool.Count)] : RandomExclude(rng, max, ex1);
    }

    private static Side RandomSide(Random rng) => rng.Next(2) == 0 ? Side.Left : Side.Right;

    // Tether indices: 0-1 close inner, 2-3 close outer, 4-5 far inner, 6-7 far outer.
    private static int[]? GroupIndices(AiTetherGroup group) => group switch
    {
        AiTetherGroup.CloseInner => [0, 1],
        AiTetherGroup.CloseOuter => [2, 3],
        AiTetherGroup.FarInner   => [4, 5],
        AiTetherGroup.FarOuter   => [6, 7],
        _                        => null,
    };

    // Holds AI-driven slots to the group the host pinned them to, leaving every player randomly
    // assigned as before.
    //
    // Two things keep the host and its clients in step. Roles are walked in PartyRole order rather
    // than shuffled order, so the outcome does not depend on how the deal came out; and a pin takes
    // the lowest free index in its group instead of drawing one, so no random draw depends on who is
    // human. That matters because "who is human" is the one input here that is read per machine.
    private static void ApplyAiTetherPins(PartyRole[] roles, TopP5DeltaStateOverrides overrides, PartyRole playerRole)
    {
        if (overrides.AiTetherPins == 0) return;
        var claimed = new bool[roles.Length];
        for (var slot = 0; slot < roles.Length; slot++)
        {
            var role = (PartyRole)slot;
            if (DrivenByPlayer(slot, playerRole)) continue;
            if (GroupIndices(overrides.AiTether(role)) is not { } group) continue;

            var current = Array.IndexOf(roles, role);
            if (Array.IndexOf(group, current) >= 0 && !claimed[current])
            {
                claimed[current] = true;
                continue;
            }
            var target = -1;
            foreach (var index in group)
                if (!claimed[index]) { target = index; break; }
            if (target < 0) continue;   // the group is full; this one stays where the deal put it

            (roles[current], roles[target]) = (roles[target], roles[current]);
            claimed[target] = true;
        }
    }

    // In a multiplayer run every player is in the human mask, which both machines derive from the
    // host's SlotOwners, so the answer is the same on all of them. Solo has no mask and exactly one
    // player, and nothing to stay in step with.
    private static bool DrivenByPlayer(int slot, PartyRole playerRole)
        => MultiplayerContext.InRun ? MultiplayerContext.IsHumanControlled(slot) : slot == (int)playerRole;

    private static PartyRole[] ShuffleRoles(Random rng)
    {
        var roles = (PartyRole[])Enum.GetValues(typeof(PartyRole));
        for (int i = roles.Length - 1; i > 0; i--)
        {
            var j = rng.Next(i + 1);
            (roles[i], roles[j]) = (roles[j], roles[i]);
        }

        return roles;
    }

    private static Side[] ShuffleSides(Random rng)
    {
        var sides = new[] { Side.Left, Side.Left, Side.Left, Side.Right, Side.Right, Side.Right };
        for (int i = sides.Length - 1; i > 0; i--)
        {
            var j = rng.Next(i + 1);
            (sides[i], sides[j]) = (sides[j], sides[i]);
        }

        return sides;
    }

    private static T[] ShuffleInPlace<T>(T[] values, Random rng)
    {
        for (int i = values.Length - 1; i > 0; i--)
        {
            var j = rng.Next(i + 1);
            (values[i], values[j]) = (values[j], values[i]);
        }

        return values;
    }
}
