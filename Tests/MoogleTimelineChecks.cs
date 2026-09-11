using System.Numerics;
using System.Reflection;
using AnoMech.Core.Game.Ai;
using AnoMech.Core.Game.Party;
using AnoMech.Core.SimObjects;
using AnoMech.Scenarios;
using AnoMech.Scenarios.Top;
using AnoMech.Scenarios.Top.P5Delta;
using AnoMech.Scenarios.Top.P5Sigma;
using AnoMech.Scenarios.Top.P5Omega;
using static MoogleChecks;

internal static class MoogleTimelineChecks
{
    public static void Run()
    {
        foreach (var fps in new[]{30,60})
        for (var run=0;run<24;run++)
        {
            var world = new SimWorld();
            foreach (var m in world.Party.ActiveMembers()) m.SimulateMovement = true;
            var delta = new TopP5DeltaState(new(), PartyRole.MainTank);
            var dai = new TopP5DeltaMoogleAi();
            dai.Run(delta, world);
            world.Events.Add(35.3f,()=>delta.BeyondDefenseTarget=Enum.GetValues<PartyRole>()
                .MinBy(r=>world.Party.Get(r)!.Position.LengthSquared()));
            var armRotations=new float[6];
            world.Events.Add(35.6f,()=>
            {
                for(var arm=0;arm<6;arm++)
                {
                    var pos=TopConstants.Geometry.ArmUnitPlacements[arm].Position*new Vector3(delta.EyeSpawn.Mul,1,1);
                    var target=world.Party.ActiveMembers().MinBy(m=>Vector3.Distance(m.Position,pos))!.Position-pos;
                    armRotations[arm]=MathF.Atan2(target.X,target.Z);
                }
            });
            for(var pulse=0;pulse<6;pulse++)
            {
                var step=pulse;
                world.Events.Add(pulse==0 ? 38.1f : 38f+pulse*.6f,()=>
                {
                    for(var arm=0;arm<6;arm++)
                    {
                        var pos=TopConstants.Geometry.ArmUnitPlacements[arm].Position*new Vector3(delta.EyeSpawn.Mul,1,1);
                        var rot=armRotations[arm]+step*delta.ArmHandedness[arm].Mul*TopConstants.Geometry.HyperPulseStep;
                        var forward=new Vector2(MathF.Sin(rot),MathF.Cos(rot));
                        foreach(var member in world.Party.ActiveMembers())
                        {
                            var p=Xz(member.Position-pos);
                            var along=Vector2.Dot(forward,p);
                            var across=MathF.Abs(forward.X*p.Y-forward.Y*p.X);
                            Check(along<0 || along>100 || across>4,$"Delta HyperPulse hit: arm={arm}, pulse={step}, member={member.GameObjectId}.");
                        }
                    }
                });
            }
            Advance(world, 30.1f, fps);
            for (var i=0;i<8;i++)
            {
                var point = world.Party.Get(delta.TetherOrder[i])!.Position;
                var mates = Enumerable.Range(0,8).Where(j => i != j && Vector3.Distance(point,
                    world.Party.Get(delta.TetherOrder[j])!.Position) < TopConstants.Geometry.RocketPunchAoeRadius).ToArray();
                    Check(mates.Length == 1 && delta.FistColors[mates[0]] != delta.FistColors[i], "Delta live fist snapshot must have one opposite-color partner.");
            }
            Advance(world,53.2f,fps);
            var beetle=new Vector3(-20*delta.EyeSpawn.Mul,0,0);
            var swivelRotation=MathF.PI/2*(delta.EyeSpawn.Mul+delta.SwivelCannonSide.Mul);
            var swivelForward=new Vector3(MathF.Sin(swivelRotation),0,MathF.Cos(swivelRotation));
            foreach(var member in world.Party.ActiveMembers())
                Check(Vector3.Dot(Vector3.Normalize(member.Position-beetle),swivelForward)<MathF.Cos(TopConstants.Geometry.SwivelCannonHalfAngle),"Delta first transfer must stand outside Swivel Cannon.");
            ResolveWorld(world,delta.NearWorldRole,delta.FarWorldRole,53.2f,fps);
            var sw = new SimWorld();
            foreach (var m in sw.Party.ActiveMembers()) m.SimulateMovement = true;
            var sigma = new TopP5SigmaState(sw.Party,new());
            var sai = new TopP5SigmaMoogleAi();
            sai.Run(sigma,sw);
            void Laser(float at,float rotation)
            {
                sw.Events.Add(at,()=>
                {
                    var forward=Xz(sigma.NewNorthB.Apply(new Vector3(MathF.Sin(rotation),0,MathF.Cos(rotation))));
                    foreach(var member in sw.Party.ActiveMembers())
                    {
                        var p=Xz(member.Position);
                        var along=Vector2.Dot(forward,p);
                        var across=MathF.Abs(forward.X*p.Y-forward.Y*p.X);
                        Check(along<0 || along>50 || across>6,$"Sigma live movement crosses Rear Lasers at {at:F2} ({sigma.SpinnerRotation.Mul}, {member.GameObjectId}).");
                    }
                });
            }
            Laser(57.7f,0);
            for(var i=0;i<13;i++) Laser(58.6f+i*.58f,sigma.SpinnerRotation.Mul*(i+1)*MathF.PI/20);
            Advance(sw,67.9f,fps);
            var expected = Move(sai,"HelloWorldPositions");
            for (var role=0;role<8;role++)
                Check(Vector2.Distance(Xz(sw.Party.Get(role)!.Position),expected[role]!.Value) <= .31f,"Sigma must reach HW positions before resolution.");
            var ow = new SimWorld();
            var omega = new TopP5OmegaState(ow.Party,new());
            foreach (var role in Enum.GetValues<PartyRole>())
            {
                var m=ow.Party.Get(role)!;
                m.SimulateMovement=true;
                m.AddStatus(TopConstants.StatusId.QuickeningDynamis, stacks: omega.DoubleDynamicTargets.List.Contains(role) ? 2 : 1);
            }
            var oai = new TopP5OmegaMoogleAi();
            oai.Run(omega,ow);
            ResolveWorld(ow,omega.HelloWorldTargets[0],omega.HelloWorldTargets[1],41.25f,fps);
            Advance(ow,46.2f,fps);
            var second=(RoleList)typeof(TopP5OmegaAi).GetField("helloWorld2",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(oai)!;
            Check(second.List.Length==8 && second.List.Distinct().Count()==8,"Fourth transfer must retain all eight assigned roles.");
            Check(second.List.Skip(2).Take(2).All(r=>ow.Party.Get(r)!.FindStatus(TopConstants.StatusId.QuickeningDynamis)?.Stacks==3),"Beetle tethers must go to the two three-stack players.");
            ResolveWorld(ow,omega.HelloWorldTargets[2],omega.HelloWorldTargets[3],59.27f,fps);
            Check(ow.Party.ActiveMembers().All(m=>m.FindStatus(TopConstants.StatusId.QuickeningDynamis)?.Stacks==3),"Two Omega transfers must finish with three stacks on all eight players.");
        }
        Console.WriteLine("PASS: Moogle Delta fists, Sigma arrivals and both Omega transfers at 6y/s, 30/60fps.");
    }

    private static void ResolveWorld(SimWorld world, PartyRole near, PartyRole far, float at, int fps)
    {
        var visited = new HashSet<PartyRole>();
        for(var step=0;step<3;step++)
        {
            Advance(world,at+step,fps);
            if(step>0)
            {
                var roles=Enum.GetValues<PartyRole>();
                var n=world.Party.Get(near)!.Position;
                var f=world.Party.Get(far)!.Position;
                near=roles.Where(r=>r!=near).MinBy(r=>Vector3.Distance(n,world.Party.Get(r)!.Position));
                far=roles.Where(r=>r!=far).MaxBy(r=>Vector3.Distance(f,world.Party.Get(r)!.Position));
            }
            foreach(var role in new[]{near,far})
            {
                Check(visited.Add(role),"HW live jumps must never revisit a target or overlap the other chain.");
                var member=world.Party.Get(role)!;
                Check((member.FindStatus(TopConstants.StatusId.QuickeningDynamis)?.Stacks ?? 0)<3,"HW must not hit a player already at three stacks.");
                member.AddStatus(TopConstants.StatusId.QuickeningDynamis);
            }
        }
    }

    private static void Advance(SimWorld world,float until,int fps)
    { while(world.Elapsed < until) world.Tick(MathF.Min(1f/fps,until-world.Elapsed)); }
    private static Vector2 Xz(Vector3 p)=>new(p.X,p.Z);
}
