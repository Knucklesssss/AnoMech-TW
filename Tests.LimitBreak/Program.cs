using AnoMech;
using AnoMech.Core.Combat;
using AnoMech.Core.Game.Party;
using AnoMech.Core.SimObjects;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using System.Numerics;

unsafe {
    BattleChara native = new() { ClassJob = 19 };
    LimitBreakController gauge = new() { BarCount = 1, BarUnits = 100, CurrentUnits = 20 };
    LimitBreakController.StaticAddressPointers.pInstance = &gauge;
    var world = new SimWorld();
    var player = new SimPlayer { BattleCharaPtr = &native };
    world.Party.Member = player;
    var resolved = 0;
    using var runtime = new PracticeLimitBreakRuntime(world, (_, _, _, _) => resolved++);
    runtime.Tick(0);
    Check(gauge.BarCount == 3 && gauge.CurrentUnits == 300, "Practice gauge projects three bars");
    Check(!runtime.TryStart(PartyRole.MainTank, 999), "Reject wrong job action");
    world.Combat = new() { CastingAction = 12 };
    Check(!runtime.TryStart(PartyRole.MainTank, 199), "Keep normal combat cast intact");
    world.Combat.CastingAction = 0;
    Check(runtime.TryStart(PartyRole.MainTank, 199), "Start LB");
    Check(!runtime.IsAvailable && resolved == 0, "Reserve without resolving");
    Check(!runtime.TryStart(PartyRole.OffTank, 199), "Prevent shared gauge double-spend");
    player.Position = Vector3.UnitX;
    runtime.Tick(0.1f);
    Check(runtime.IsAvailable && resolved == 0, "Movement cancels and refunds");
    Check(runtime.TryStart(PartyRole.MainTank, 199), "Restart after cancellation");
    player.Alive = false;
    runtime.Tick(0.1f);
    Check(runtime.IsAvailable && resolved == 0, "Death cancels and refunds");
    player.Alive = true;
    Check(runtime.TryStart(PartyRole.MainTank, 199), "Start again");
    runtime.Tick(2);
    Check(resolved == 1 && !runtime.IsAvailable && runtime.IsBusy(PartyRole.MainTank), "Resolve once and retain recovery lock");
    runtime.Tick(4);
    Check(resolved == 1 && !runtime.IsBusy(PartyRole.MainTank), "Recovery expires without repeat callback");
    runtime.Refill();
    Check(runtime.IsAvailable, "Scenario refills gauge");
    runtime.Dispose();
    Check(gauge.BarCount == 1 && gauge.CurrentUnits == 20 && gauge.BarUnits == 100, "Restore original native gauge");
    Check(!runtime.TryStart(PartyRole.MainTank, 199), "Disposed runtime rejects actions");
}
Console.WriteLine("Practice LB lifecycle checks passed.");
static void Check(bool value, string message) { if (!value) throw new Exception(message); }
