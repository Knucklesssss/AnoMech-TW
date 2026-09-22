using System.Numerics;
using AnoMech.Core.Game.Ai;
using AnoMech.Core.Game.Party;
using AnoMech.Scenarios.Top.P6AlphaOmega;

// The moogle P6 strat differs from the original in exactly one way: ST and H2
// trade the east and south spots. Everything else must stay identical, in both
// the wave-cannon clock spots and the second arrow's quadrants.
internal static class MoogleP6ClockChecks
{
    public static void Run()
    {
        foreach (var radius in new[] { 2f, 9f, 13.63f })
            CheckSwap(TopP6ClockSpots.Clock(radius, false), TopP6ClockSpots.Clock(radius, true), $"Clock({radius})");

        foreach (var (diagonal, cardinal, offset) in new[] { (11f, 12f, 6f), (9f, 9f, 9f), (11f, 12f, 0f), (6f, 6f, 6f) })
            CheckSwap(TopP6ClockSpots.Arrow(diagonal, cardinal, offset, false),
                TopP6ClockSpots.Arrow(diagonal, cardinal, offset, true), $"Arrow({diagonal},{cardinal},{offset})");

        // D3 stays north and MT stays north-west in both strats, including the meteor spread.
        var moogle = TopP6ClockSpots.Clock(13.63f, true);
        Check(moogle[(int)PartyRole.PhysRangedDps]!.Value.X == 0f && moogle[(int)PartyRole.PhysRangedDps]!.Value.Y < 0f,
            "Moogle D3 must stay due north.");
        Check(moogle[(int)PartyRole.MainTank]!.Value.X < 0f && moogle[(int)PartyRole.MainTank]!.Value.Y < 0f,
            "Moogle MT must stay north-west.");
        Check(moogle[(int)PartyRole.OffTank]!.Value.X > 0f && moogle[(int)PartyRole.OffTank]!.Value.Y == 0f,
            "Moogle ST must stand due east.");
        Check(moogle[(int)PartyRole.ShieldHealer]!.Value.X == 0f && moogle[(int)PartyRole.ShieldHealer]!.Value.Y > 0f,
            "Moogle H2 must stand due south.");
        // Wave cannon stack moves from B (east) to C (south); tanks stay boss-side.
        var b = TopP6ClockSpots.Stack(false);
        var c = TopP6ClockSpots.Stack(true);
        Check(b[(int)PartyRole.MainTank]!.Value == new Vector2(11.63f, 0f), "Original stack must stay on B.");
        Check(c[(int)PartyRole.MainTank]!.Value == new Vector2(0f, 11.63f), "Moogle stack must sit on C.");
        foreach (var role in Enum.GetValues<PartyRole>())
        {
            var tank = role is PartyRole.MainTank or PartyRole.OffTank;
            Check(c[(int)role]!.Value.X == 0f && c[(int)role]!.Value.Y > 0f, $"Moogle stack: {role} must be due south.");
            Check(c[(int)role]!.Value.Length() == (tank ? 11.63f : 13.63f), $"Moogle stack: {role} lost its boss-side offset.");
        }

        Console.WriteLine("PASS: P6 moogle swaps ST/H2, stacks on C in both clock spots and arrow quadrants.");
    }

    private static void CheckSwap(IAiMove original, IAiMove moogle, string what)
    {
        Check(original[(int)PartyRole.OffTank] != original[(int)PartyRole.ShieldHealer],
            $"{what}: the two swapped spots must actually differ.");
        Check(moogle[(int)PartyRole.OffTank] == original[(int)PartyRole.ShieldHealer],
            $"{what}: moogle ST must take the original H2 spot.");
        Check(moogle[(int)PartyRole.ShieldHealer] == original[(int)PartyRole.OffTank],
            $"{what}: moogle H2 must take the original ST spot.");
        foreach (var role in Enum.GetValues<PartyRole>())
        {
            if (role is PartyRole.OffTank or PartyRole.ShieldHealer) continue;
            Check(moogle[(int)role] == original[(int)role], $"{what}: {role} must not move between strats.");
        }
    }

    private static void Check(bool value, string message) { if (!value) throw new Exception(message); }
}
