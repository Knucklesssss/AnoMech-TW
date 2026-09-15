using System.Numerics;
using System.Reflection;
using AnoMech.Core.Game.Ai;
using AnoMech.Core.Game.Party;
using AnoMech.Core.SimObjects;
using AnoMech.Scenarios;
using AnoMech.Scenarios.Top;
using AnoMech.Scenarios.Top.P2PartySynergy;
using AnoMech.Scenarios.Top.P3HelloWorld;
using AnoMech.Scenarios.Top.P3Monitors;
using AnoMech.Scenarios.Top.P5Delta;
using AnoMech.Scenarios.Top.P5Sigma;
using AnoMech.Scenarios.Top.P5Omega;
using AnoMech.Core;

internal static class MoogleChecks
{
    public static void Run()
    {
        var type = typeof(TopP2PartySynergyAi).Assembly.GetType("AnoMech.Scenarios.Top.P2PartySynergy.TopP2PartySynergyMoogleAi");
        Check(type != null, "Moogle strategy must be available for P2.");
        PartyRole[] priority = [PartyRole.RegenHealer, PartyRole.MainTank, PartyRole.OffTank,
            PartyRole.MeleeDpsA, PartyRole.MeleeDpsB, PartyRole.PhysRangedDps, PartyRole.CasterDps, PartyRole.ShieldHealer];
        foreach (var glitch in new[] { GlitchType.Mid, GlitchType.Far })
        for (var first = 0; first < 8; first++)
        for (var second = first + 1; second < 8; second++)
        {
            var world = new SimWorld();
            var state = new TopP2PartySynergyState(world.Party, new() { Glitch = glitch, NewNorthA = Direction.N, NewNorthB = Direction.N });
            state.Stacks.List[0] = state.Order[first];
            state.Stacks.List[1] = state.Order[second];
            var ai = (TopP2PartySynergyAi)Activator.CreateInstance(type!)!;
            ai.Run(state, world);
            var queue = Move(ai, "CongaLine");
            Check(priority.Zip(priority.Skip(1)).All(p => queue[(int)p.First]!.Value.X < queue[(int)p.Second]!.Value.X), "Moogle queue priority is wrong.");
            var spread = Move(ai, "SpreadPositions");
            for (var pair = 0; pair < 4; pair++)
            {
                var a = spread[(int)state.Order[pair * 2]]!.Value;
                var b = spread[(int)state.Order[pair * 2 + 1]]!.Value;
                Check(a.X * b.X < 0, "Pair must split left/right.");
                Check(MathF.Abs(glitch == GlitchType.Far ? a.Y + b.Y : a.Y - b.Y) < .001f, "Far must reverse all four right-side shapes; near keeps matching rows.");
            }
            var stack = Move(ai, "StackPositions");
            var centers = Enumerable.Range(0, 8).Select(i => stack[i]!.Value).Distinct().ToArray();
            Check(centers.Length == 2 && centers.All(c => Enumerable.Range(0,8).Count(i => stack[i] == c) == 4), "Stacks must be four/four.");
            Check(stack[(int)state.Stacks[0]] != stack[(int)state.Stacks[1]], "Both stack markers remained together.");
            var p0 = spread[(int)state.Stacks[0]]!.Value;
            var p1 = spread[(int)state.Stacks[1]]!.Value;
            if (p0.X * p1.X > 0)
            {
                var southRole = p0.Y > p1.Y ? state.Stacks[0] : state.Stacks[1];
                var wasLeft = spread[(int)southRole]!.Value.X < 0;
                Check((stack[(int)southRole]!.Value.X < -1) != wasLeft, "Same-side stacks must swap the southern marker with its tether partner.");
            }
            for (var pair = 0; pair < 4; pair++)
            {
                var distance = Vector2.Distance(stack[(int)state.Order[pair*2]]!.Value, stack[(int)state.Order[pair*2+1]]!.Value);
                Check(glitch == GlitchType.Far ? distance - .6f > 34f : distance - .6f > 21f && distance + .6f < 26f, "Stack distance must remain valid with movement jitter.");
            }
        }
        Console.WriteLine("Moogle P2 checks passed.");
        var assign = typeof(TopP3TransitionRules).GetMethods().SingleOrDefault(m => m.Name == "Assign" && m.GetParameters().Length == 3);
        Check(assign != null, "P2.5 must support a chosen player marker.");
        foreach (var role in priority)
        for (var slot = 0; slot < 8; slot++)
        {
            var roles = (PartyRole[])assign!.Invoke(null, new object?[] { priority, role, slot })!;
            Check(roles[slot] == role && roles.Distinct().Count() == 8, "Chosen transition marker must retain a complete party assignment.");
        }
        var position = typeof(TopP3MonitorRules).GetMethod("MooglePositionFor");
        var facing = typeof(TopP3MonitorRules).GetMethod("MoogleFacingFor");
        Check(position != null && facing != null, "Moogle monitor layouts must be available.");
        var queueMethod = typeof(TopP3MonitorRules).GetMethod("MoogleQueuePositionFor");
        Check(queueMethod != null, "Moogle monitors must queue from waymark 4 to waymark 3.");
        var queueFirst = (Vector3)queueMethod!.Invoke(null, new object[]{PartyRole.RegenHealer})!;
        var queueLast = (Vector3)queueMethod.Invoke(null, new object[]{PartyRole.ShieldHealer})!;
        Check(Vector3.Distance(queueFirst, new(-9.638f,0,-9.638f)) < .01f && Vector3.Distance(queueLast, new(-9.638f,0,9.638f)) < .01f, "Queue endpoints must align with 4/3 waymarks.");
        foreach (var mirror in new[] { false, true })
        foreach (var side in Enum.GetValues<TopP3BossSide>())
        for (var statusBits = 0; statusBits < 8; statusBits++)
        {
            var points = Enumerable.Range(0, 8).Select(i => (Vector3)position!.Invoke(null, new object[] { i, side, mirror })!).ToArray();
            Check(points.All(p => p.Length() < 20), "Monitor positions must remain in the arena.");
            var hits = new int[8];
            var bossTargets = Enumerable.Range(0, 8).Where(i => points[i].X * (side == TopP3BossSide.Right ? 1 : -1) > 0).ToArray();
            Check(bossTargets.Length == 2, "Boss monitor must see exactly two people.");
            foreach (var target in bossTargets) hits[target]++;
            for (var monitor = 0; monitor < 3; monitor++)
            {
                var status = (statusBits & (1 << monitor)) == 0 ? TopP3MonitorStatus.Right : TopP3MonitorStatus.Left;
                var rotation = (float)facing!.Invoke(null, new object[] { monitor, side, mirror, status })!;
                var right = new Vector3(-MathF.Cos(rotation), 0, MathF.Sin(rotation));
                var normal = right * (status == TopP3MonitorStatus.Right ? 1 : -1);
                var targets = Enumerable.Range(0, 8).Where(i => i != monitor && Vector3.Dot(points[i] - points[monitor], normal) > .001f).ToArray();
                Check(targets.Length == 2, $"Monitor {monitor} must see exactly two targets (mirror={mirror}, boss={side}).");
                foreach (var target in targets) hits[target]++;
            }
            Check(hits.All(n => n == 1), "Every monitor participant must receive exactly one circle.");
            for (var i = 0; i < 8; i++)
            for (var j = i+1; j < 8; j++)
                Check(Vector3.Distance(points[i], points[j]) > 7, "Monitor circles must not overlap another player.");
            Check(mirror || points[1].X < 0 && points[2].X < 0 && points[4].X > 0 && points[5].X > 0, "Fixed monitors 2/3 and unmarked 2/3 must not switch sides.");
        }
        Console.WriteLine("Moogle transition and monitor checks passed.");
        CheckP5();
        CheckDeltaStandardLocalTethers();
    }

    // Standard Delta once Swivel Cannon is known, with AI jitter as margin: nobody in the cannon, Hello World reaches one
    // player per hit (near into the beetle-side local tether, far to the beetle's feet then the far edge), the far-side
    // local tether holds and the beetle-side one has broken.
    private static void CheckDeltaStandardLocalTethers()
    {
        const float jitter = .3f;
        var roles = Enum.GetValues<PartyRole>();
        for (var run = 0; run < 256; run++)
        {
            var delta = new TopP5DeltaState(new(), PartyRole.MainTank);
            var ai = new TopP5DeltaAi();
            ai.Run(delta, new SimWorld());
            var dodge = Move(ai, "SwivelDodge");
            var breakTether = Move(ai, "BreakBeetleSideTether");
            Vector2 P(PartyRole role) => (breakTether[(int)role] ?? dodge[(int)role])!.Value;
            var beetle = new Vector2(-20 * delta.EyeSpawn.Mul, 0);
            var cannon = MathF.PI / 2 * delta.EyeSpawn.Mul + delta.SwivelCannonSide.Mul * MathF.PI / 2;
            var forward = new Vector2(MathF.Sin(cannon), MathF.Cos(cannon));
            foreach (var role in roles)
            {
                var offset = P(role) - beetle;
                var angle = MathF.Acos(Vector2.Dot(offset, forward) / offset.Length());
                Check(angle > TopConstants.Geometry.SwivelCannonHalfAngle + MathF.Asin(jitter / offset.Length()) && P(role).Length() < TopConstants.Geometry.ArenaRadius - jitter,
                    "Delta Standard positions must stay out of Swivel Cannon and inside the arena.");
            }
            PartyRole Next(PartyRole from, bool nearest) => nearest
                ? roles.Where(r => r != from).MinBy(r => Vector2.Distance(P(from), P(r)))
                : roles.Where(r => r != from).MaxBy(r => Vector2.Distance(P(from), P(r)));
            bool Alone(PartyRole target, float radius) => roles.All(r => r == target || Vector2.Distance(P(r), P(target)) > radius + 2 * jitter);
            var near1 = Next(delta.NearWorldRole, true);
            var near2 = Next(near1, true);
            var far1 = Next(delta.FarWorldRole, false);
            var far2 = Next(far1, false);
            Check(new[] { near1, near2 }.All(r => r == delta.TetherOrder[4] || r == delta.TetherOrder[5])
                  && far1 == delta.TetherOrder[delta.EyeSpawn.Mul > 0 ? 7 : 6] && far2 == delta.TetherOrder[delta.EyeSpawn.Mul > 0 ? 6 : 7]
                  && near1 != near2,
                "Hello World must go near into the beetle-side local tether and far to the beetle's feet, then the far edge.");
            Check(Alone(delta.NearWorldRole, TopConstants.Geometry.HelloWorldInitialAoeRadius) && Alone(delta.FarWorldRole, TopConstants.Geometry.HelloWorldInitialAoeRadius)
                  && new[] { near1, near2, far1, far2 }.All(r => Alone(r, TopConstants.Geometry.HelloWorldJumpAoeRadius)),
                "Each Hello World hit must reach exactly one player.");
            Check(Vector2.Distance(P(delta.TetherOrder[6]), P(delta.TetherOrder[7])) > TopConstants.Geometry.HwTetherBreakDistance + 2 * jitter
                  && Vector2.Distance(P(delta.TetherOrder[4]), P(delta.TetherOrder[5])) < TopConstants.Geometry.HwTetherBreakDistance - 2 * jitter,
                "The far-side local tether must hold and the beetle-side one must break.");
        }
        Console.WriteLine("PASS: Delta Standard local tethers dodge Swivel Cannon and route Hello World.");
    }

    private static void CheckP5()
    {
        var assembly = typeof(TopP5DeltaAi).Assembly;
        var deltaType = assembly.GetType("AnoMech.Scenarios.Top.P5Delta.TopP5DeltaMoogleAi");
        var sigmaType = assembly.GetType("AnoMech.Scenarios.Top.P5Sigma.TopP5SigmaMoogleAi");
        var omegaType = assembly.GetType("AnoMech.Scenarios.Top.P5Omega.TopP5OmegaMoogleAi");
        Check(deltaType != null && sigmaType != null && omegaType != null, "All three P5 Moogle strategies must be available.");
        Check(new TopP5DeltaAi().Group != null && new TopP5OmegaAi().Group != null && new TopP3HelloWorldAi().Group != null,
            "Original strategies must retain a selectable region when a grouped Moogle strategy is added.");
        for (var run = 0; run < 128; run++)
        {
            var world = new SimWorld();
            var delta = new TopP5DeltaState(new(), PartyRole.MainTank);
            var ai = (TopP5DeltaAi)Activator.CreateInstance(deltaType!)!;
            ai.Run(delta, world);
            var fist = Move(ai, "FistResolveSlots");
            var settle = Move(ai, "TetherResolveStep");
            for (var half = 0; half < 8; half += 4)
            {
                var swap = delta.FistColors[half] == delta.FistColors[half+2];
                Check(fist[(int)delta.TetherOrder[half]]!.Value.Y < 0, "Inner fist pair must keep its north/south order.");
                Check((fist[(int)delta.TetherOrder[half+2]]!.Value.Y > 0) == swap, "Same-color fists must swap the outer pair.");
                for (var i = half; i < half + 4; i++)
                {
                    Vector2 Point(int index) => settle[(int)delta.TetherOrder[index]] ?? fist[(int)delta.TetherOrder[index]]!.Value;
                    var mates = Enumerable.Range(half,4).Where(j => i != j && Vector2.Distance(Point(i), Point(j)) < TopConstants.Geometry.RocketPunchAoeRadius).ToArray();
                    Check(mates.Length == 1 && delta.FistColors[mates[0]] != delta.FistColors[i], "Each fist must overlap exactly one opposite-color fist.");
                }
            }
            var sigmaWorld = new SimWorld();
            var sigma = new TopP5SigmaState(sigmaWorld.Party, new());
            var sai = (TopP5SigmaAi)Activator.CreateInstance(sigmaType!)!;
            sai.Run(sigma, sigmaWorld);
            var mapping = (Dictionary<PartyRole, Sign>)sigmaType!.GetMethod("MarkerMapping", BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(sai,null)!;
            Check(sigma.HelloWorldTargets.List.All(r => !mapping.ContainsKey(r)), "Sigma HW holders must not receive role markers.");
            var hw = Move(sai, "HelloWorldPositions");
            var free = TopP3MonitorRules.Priority.Where(r => !sigma.HelloWorldTargets.List.Contains(r) && sigma.DynamisTargets.List.Contains(r)).ToArray();
            Check(mapping[free[0]] == Sign.Attack1 && mapping[free[1]] == Sign.Attack2 && mapping[free[2]] == Sign.Attack3 && mapping[free[3]] == Sign.Attack4, "Sigma attack numbering must follow Moogle priority.");
            CheckHelloChain(hw, sigma.HelloWorldTargets[0], sigma.HelloWorldTargets[1], mapping, Sign.Attack1, Sign.Attack4);
            var omegaWorld = new SimWorld();
            var omega = new TopP5OmegaState(omegaWorld.Party, new());
            var oai = (TopP5OmegaAi)Activator.CreateInstance(omegaType!)!;
            oai.Run(omega, omegaWorld);
            var first = Move(oai, "HelloWorld1Pos");
            var list = (RoleList)typeof(TopP5OmegaAi).GetField("helloWorld1", BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(oai)!;
            var om = (Dictionary<PartyRole, Sign>)omegaType!.GetMethod("HelloWorldMarkers", BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(oai, new object[]{list})!;
            Check(omega.HelloWorldTargets.List.Take(2).All(r => !om.ContainsKey(r)), "First HW must remain unmarked in Omega.");
            Check(omega.DoubleDynamicTargets.List.Where(r => omega.HelloWorldTargets.List.Skip(2).Contains(r)).All(r => list.List.Skip(2).Take(2).Contains(r)), "Two-stack second HW must take first monitors.");
            CheckHelloChain(first, list[0], list[1], new Dictionary<PartyRole, Sign> { [list[4]]=Sign.Attack1, [list[7]]=Sign.Attack4 }, Sign.Attack4, Sign.Attack1);
        }
        Console.WriteLine("Moogle P5 assignments, fist swaps and HW routing checks passed.");
    }

    private static void CheckHelloChain(IAiMove move, PartyRole near, PartyRole far, Dictionary<PartyRole, Sign> marks, Sign firstFar, Sign secondFar)
    {
        var others = Enum.GetValues<PartyRole>();
        PartyRole Next(PartyRole from, bool nearest) => nearest
            ? others.Where(r=>r!=from).MinBy(r=>Vector2.Distance(move[(int)from]!.Value, move[(int)r]!.Value))
            : others.Where(r=>r!=from).MaxBy(r=>Vector2.Distance(move[(int)from]!.Value, move[(int)r]!.Value));
        var a=Next(far,false); var b=Next(a,false);
        Check(marks[a]==firstFar && marks[b]==secondFar, "Far world must follow the intended first/second wall receivers.");
        var c=Next(near,true); var d=Next(c,true);
        Check(new[]{near,far,a,b,c,d}.Distinct().Count()==6, "Hello World routes must not overlap or jump back.");
    }

    internal static IAiMove Move(object ai, string method)
    {
        for(var type=ai.GetType();type!=null;type=type.BaseType)
            if(type.GetMethod(method,BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.DeclaredOnly) is {} found)
                return (IAiMove)found.Invoke(ai,null)!;
        throw new MissingMethodException(method);
    }
    internal static void Check(bool value, string message) { if (!value) throw new Exception(message); }
}
