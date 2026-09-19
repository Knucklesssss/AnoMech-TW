using AnoMech.Core.Game.Party;
using AnoMech.Scenarios.Top;
using AnoMech.Scenarios.Top.P6AlphaOmega;

internal static class P6OpeningChecks
{
    public static void Run()
    {
        var distances = new float[] { 64, 256, 36, 36, 36, 36, 36, 36 };
        if (TopP6AutoAttackTargets.Select(distances, false, PartyRole.MainTank) != (0, 1))
            throw new Exception("P6 autos must select MT and farthest ST.");
        distances[0] = 400;
        if (TopP6AutoAttackTargets.Select(distances, false, PartyRole.MainTank) != (0, 0))
            throw new Exception("Double-bait must remain a failure, not silently choose another target.");
        foreach (var role in Enum.GetValues<PartyRole>())
        {
            var expected = role == PartyRole.MainTank ? (0, -1) : role == PartyRole.OffTank ? (-1, 1) : (-1, -1);
            if (TopP6AutoAttackTargets.Select(distances, true, role) != expected)
                throw new Exception("Solo auto targeting must respect the selected role.");
        }
        Array.Fill(distances, float.PositiveInfinity);
        if (TopP6AutoAttackTargets.Select(distances, false, PartyRole.MainTank) != (-1, -1))
            throw new Exception("Dead slots cannot receive autos.");
        foreach (var inside in new[] { false, true })
        {
            var (early, initial) = TopP6CosmoArrow.Pattern(inside);
            var waves = TopP6CosmoArrow.Init(early, initial, []);
            for (var pulse = 0; pulse < 7; pulse++)
            {
                if (waves.Any(w => Math.Abs(w.Line) > 20)) throw new Exception("Arrow escaped arena.");
                waves = pulse == 0 ? TopP6CosmoArrow.Init(early, initial, waves) : TopP6CosmoArrow.Progress(waves);
            }
            if (waves.Count != 0) throw new Exception("Arrow sequence must end.");
        }
        Console.WriteLine("PASS: P6 tank autos, solo/dead targets and both arrow sequences.");
    }
}
