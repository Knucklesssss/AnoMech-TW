using System.Numerics;
using System.Reflection;
using AnoMech.Core.Game.Ai;
using AnoMech.Core.SimObjects;
using AnoMech.Scenarios;
using AnoMech.Scenarios.Top;
using AnoMech.Scenarios.Top.P5Sigma;

internal static class SigmaTowerChecks
{
    public static void Run()
    {
        var cases = 0;
        foreach (var moogle in new[] { false, true })
        foreach (var glitch in new[] { GlitchType.Far, GlitchType.Mid })
        foreach (var north in Direction.All)
        foreach (var flip in new[] { false, true })
        for (var first = 0; first < 8; first++)
        for (var second = 0; second < 8; second++)
        {
            if (first / 2 == second / 2) continue;
            var world = new SimWorld();
            var state = new TopP5SigmaState(world.Party, new()
            {
                CloseFarTether = glitch, NewNorthA = north, TowerNorthFlip = flip,
            }) { FirstMissing = first, SecondMissing = second };
            TopP5SigmaTuuuflessAi ai = moogle ? new TopP5SigmaMoogleAi() : new TopP5SigmaTuuuflessAi();
            ai.Run(state, world);
            var move = Move(ai, "TowerPositions");
            var points = Enumerable.Range(0, 8).Select(i => move[i]!.Value).ToArray();
            var knockback = Move(ai, "KnockbackPosition");
            var towers = state.Towers.OfType<Tower>().ToArray();
            var label = $"{glitch.StatusId}, north={north.Name()}, flip={flip}, missing={first}/{second}";

            for (var pair = 0; pair < 4; pair++)
            {
                var a = points[(int)state.Order[pair * 2]];
                var b = points[(int)state.Order[pair * 2 + 1]];
                var distance = Vector2.Distance(a, b);
                Check(glitch == GlitchType.Far ? distance - 0.2f >= 34f : distance - 0.2f >= 21f && distance + 0.2f <= 26f,
                    $"Tether distance {distance:F3} fails with 0.1y endpoint jitter: {label}");
                if (glitch != GlitchType.Far) continue;
                foreach (var point in new[] { a, b })
                {
                    var tower = towers.MinBy(t => Vector2.Distance(point, Xz(t.Position)))!;
                    var fromA = Xz(tower.Position) - a;
                    var line = b - a;
                    var perpendicular = MathF.Abs(fromA.X * line.Y - fromA.Y * line.X) / line.Length();
                    var projection = Vector2.Dot(fromA, line) / line.LengthSquared();
                    Check(perpendicular < 0.002f && projection > 0 && projection < 1,
                        $"Far tether must cross both occupied tower centers: {label}");
                }
            }

            foreach (var point in points)
            {
                Check(point.Length() + 0.1f < 20f, $"Destination plus jitter outside arena: {label}");
                Check(towers.Count(t => Vector2.Distance(point, Xz(t.Position)) + 0.1f < 3f) == 1,
                    $"Destination plus jitter must stay inside one tower: {label}");
                if (glitch == GlitchType.Far)
                    Check(point.Length() >= 19.4f, $"Far endpoint not near wall: {label}");
                else
                    Check(towers.Any(t => Vector2.Distance(point, Xz(t.Position)) < 0.002f),
                        $"Mid glitch must retain tower-center positions: {label}");
            }
            foreach (var tower in towers)
                Check(points.Count(p => Vector2.Distance(p, Xz(tower.Position)) < 3f) == tower.MinPlayers,
                    $"Wrong tower occupancy: {label}");
            if (glitch == GlitchType.Far)
                for (var role = 0; role < 8; role++)
                foreach (var landingRadius in new[] { 17f, 19.9f })
                foreach (var angleError in new[] { -0.06f, 0.06f })
                {
                    // Bound the outer-ring landing and the angular error from 0.1y
                    // jitter at the 1.7y knockback spot, without faking native knockback.
                    var direction = Vector2.Normalize(knockback[role]!.Value);
                    var landing = Vector2.Transform(direction, Matrix3x2.CreateRotation(angleError)) * landingRadius;
                    Check(Vector2.Distance(landing, points[role]) + 0.1f < (43.69f - 41.5f - 1f / 30f) * 6f,
                        $"Cannot reach tower before resolution at 1x/30fps: {label}");
                }
            cases++;
        }
        Console.WriteLine($"P5 Sigma tower checks passed ({cases} far/mid, north, flip and pair configurations).");
    }

    private static IAiMove Move(TopP5SigmaTuuuflessAi ai, string method) =>
        (IAiMove)typeof(TopP5SigmaTuuuflessAi).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(ai, null)!;

    private static Vector2 Xz(Vector3 position) => new(position.X, position.Z);

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }
}
