using System.Numerics;
using System.Reflection;
using AnoMech.Core;
using AnoMech.Core.Game.Party;
using AnoMech.Core.SimObjects;
using AnoMech.Scenarios;
using AnoMech.Scenarios.Top;
using AnoMech.Scenarios.Top.P5Delta;
using AnoMech.Scenarios.Top.P5Sigma;
using AnoMech.Scenarios.Top.P5Omega;
using static MoogleChecks;

internal static class MoogleP5GuideChecks
{
    public static void Run()
    {
        foreach(var side in new[]{MonitorSide.Left,MonitorSide.Right})
        for(var run=0;run<32;run++)
        {
            var world=new SimWorld();
            var state=new TopP5OmegaState(world.Party,new(){MonitorSide=side});
            var ai=new TopP5OmegaMoogleAi(); ai.Run(state,world);
            var order=(RoleList)typeof(TopP5OmegaAi).GetField("helloWorld1",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(ai)!;
            var move=Move(ai,"HelloWorld1Pos");
            // Looking west: left is south. Looking east: left is north.
            Check(move[(int)order[2]]!.Value.Y*side.Mul > 0,"Higher-priority monitor must stand on viewer-left, on either boss side.");
            Check(move[(int)order[4]]!.Value.Y*side.Mul > move[(int)order[7]]!.Value.Y*side.Mul,"Attacks 1 through 4 must run viewer-left to viewer-right.");
        }
        foreach(var spin in new[]{Rotation.Clockwise,Rotation.CounterClockwise})
        {
            var world=new SimWorld();
            var state=new TopP5SigmaState(world.Party,new(){NewNorthB=Direction.S,SpinnerRotation=spin});
            var ai=new TopP5SigmaMoogleAi(); ai.Run(state,world);
            var marks=(Dictionary<PartyRole,Sign>)typeof(TopP5SigmaMoogleAi).GetMethod("MarkerMapping",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(ai,null)!;
            var move=Move(ai,"HelloWorldPositions");
            var attack1=marks.Single(p=>p.Value==Sign.Attack1).Key;
            var attack2=marks.Single(p=>p.Value==Sign.Attack2).Key;
            var attack4=marks.Single(p=>p.Value==Sign.Attack4).Key;
            Check(move[(int)attack1]!.Value.X*spin.Mul > 19,"Sigma Attack1 must chase the laser to its wall (south F, CW = west).");
            Check(move[(int)attack4]!.Value.X*spin.Mul < -19,"Sigma Attack4 must be opposite Attack1.");
            Check(move[(int)attack2]!.Value.X*spin.Mul > 0 && move[(int)attack2]!.Value.Y>0,"Sigma Attack2 must follow spinner direction to bait (south F, CW = southwest).");
            Check(move[(int)state.HelloWorldTargets[1]]!.Value.X*spin.Mul < -9,"Far world must stand toward Attack4, not Attack1.");
            if(marks.Any(p=>p.Value==Sign.Attack5))
                Check(move[(int)marks.Single(p=>p.Value==Sign.Attack5).Key]!.Value.X*spin.Mul>0,"Fifth attack must take the guide's near-world idle position.");
        }
        foreach(var eye in new[]{NorthSouth.North,NorthSouth.South})
        {
            var world=new SimWorld(); var state=new TopP5DeltaState(new(){EyeSpawn=eye},PartyRole.MainTank);
            var ai=new TopP5DeltaMoogleAi(); ai.Run(state,world);
            var pre=Move(ai,"TetherPrePosition");
            for(var slot=0;slot<8;slot++)
                Check(pre[(int)state.TetherOrder[slot]]!.Value.X*eye.Mul*(slot<4 ? 1 : -1)>0,"Delta blue/remote must start by humanoid; green/local by beetle.");
            var bait=Move(ai,"HyperPulseBaitArms");
            Check(bait[(int)state.TetherOrder[2]]!.Value.X*eye.Mul>10 && bait[(int)state.TetherOrder[6]]!.Value.X*eye.Mul < -10,"Delta outer groups must bait their nearby arm, without crossing the arena.");
        }
        Console.WriteLine("PASS: P5 Moogle guide-relative sides, monitor priority and Sigma marker positions.");
    }
}
