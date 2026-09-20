using System.Numerics;
using AnoMech.Core.Game;
using AnoMech.Core.Game.Ai;

var path = Path.Combine(Path.GetTempPath(), "anomech-positions-" + Guid.NewGuid() + ".json");
try
{
    var store = new FilePracticePositionStore(() => path);
    var practice = new PracticePositions(store);
    Vector2?[] original = [new(1, 2), null, new(3, 4), null, null, null, null, null];
    practice.Begin("scene", "Scene", "Strat", "OtherScenario");
    var step = practice.Register(5, 10);
    Check(practice.Resolve(step, original).SequenceEqual(original), "Default targets remain identical including null slots");
    Check(practice.TrySetStepSlot(step.Id, 0, new(5, 6)), "Persist an observed target");
    Check(practice.Resolve(step, original).SequenceEqual(original), "Saving without explicit Custom mode keeps original route");
    Check(practice.SetMode(PracticePositionMode.Custom), "Explicit custom selection persists");
    Check(practice.Resolve(step, original)[0] == new Vector2(5, 6), "Custom target applies");
    practice = new PracticePositions(store);
    practice.Begin("scene", "Scene", "Strat", "OtherScenario");
    step = practice.Register(5, 10);
    Check(practice.Resolve(step, original)[0] == new Vector2(5, 6), "Saved mode and target survive a fresh store instance");
    var branch = (Vector2?[])original.Clone(); branch[2] = new(8, 9);
    Check(practice.Resolve(step, branch).SequenceEqual(branch), "Changed original branch bypasses stored targets");
    MultiplayerContext.Begin(MultiplayerRole.Host, 0, null);
    Check(practice.Resolve(step, original).SequenceEqual(original), "Persisted custom mode cannot affect a network run");
    MultiplayerContext.End();
    foreach (var scenarioType in new[] { "AnoMech.Scenarios.Top.P3HelloWorld.TopP3HelloWorldScenario", "AnoMech.Scenarios.Top.P3Monitors.TopP3MonitorsScenario" })
    {
        practice.Begin("scene", "Scene", "Strat", scenarioType);
        Check(!practice.IsActive && practice.Resolve(practice.Register(5, 10), original).SequenceEqual(original), "Protected P3 routes bypass all customization");
    }
    practice.Begin("scene", "Scene", "Strat", "OtherScenario");
    step = practice.Register(5, 10); practice.Resolve(step, original);
    Check(!practice.TrySetStepSlot(step.Id, 0, new(float.NaN, 1)), "Reject non-finite persisted coordinates");
    Check(practice.TryAddInterval(5, 6, 2, new(10, 10), new(20, 20)), "Save observed interval");
    Check(practice.Resolve(step, original)[2] == new Vector2(10, 10), "Interval includes its start boundary");
    Check(practice.Resolve(step, branch).SequenceEqual(branch), "Interval also rejects a changed branch");
    var singlePlayer = true;
    var isolated = new PracticePositions(store, () => singlePlayer);
    isolated.Begin("scene", "Scene", "Strat", "OtherScenario");
    var isolatedStep = isolated.Register(5, 10);
    singlePlayer = false;
    Check(!isolated.IsActive && isolated.Resolve(isolatedStep, original).SequenceEqual(original), "A lobby connection also bypasses persisted customization");
    var world = new AnoMech.Core.SimObjects.SimWorld(practice);
    practice.Begin("arrival", "Arrival", "Original", "OtherScenario");
    var manager = new AiManager(world);
    manager.Move(1, () => new Moves([new(12, 0), new(4, 4), null, null, null, null, null, null]), jitter: 0, arrivalTime: 5);
    world.Events.Tick(1);
    Check(world.Party.Members[0].Moves.Count == 0, "Original timed movement still defers departure");
    world.Events.Tick(1);
    Check(world.Party.Members[0].Moves.Count == 0, "Arrival delay remains unchanged");
    world.Events.Tick(1);
    Check(world.Party.Members[0].Moves.SequenceEqual(new[] { new Vector3(12, 0, 0) }), "Original destination and normal-speed arrival preserved");
    MultiplayerContext.Begin(MultiplayerRole.Host, 1, null);
    world.Events.Clear(); world.Party.Members[0].Moves.Clear(); world.Party.Members[1].Moves.Clear();
    manager.Move(0, () => new Moves([new(20, 0), new(7, 8), null, null, null, null, null, null]), jitter: 0);
    world.Events.Tick(0);
    Check(world.Party.Members[0].Moves.Count == 0 && world.Party.Members[1].Moves.SequenceEqual(new[] { new Vector3(7, 0, 8) }), "Human-controlled slots stay untouched while bot movement survives");
    MultiplayerContext.End();
    practice.End();
    Check(!practice.IsActive, "End clears run registry");
    File.WriteAllText(path, "{ invalid");
    practice = new PracticePositions(store);
    practice.Begin("scene", "Scene", "Strat", "OtherScenario");
    Check(!practice.SetMode(PracticePositionMode.Custom) && File.ReadAllText(path) == "{ invalid", "Do not overwrite an unreadable saved file");
    Console.WriteLine("Practice position isolation, persistence, and branch checks passed.");
}
finally { MultiplayerContext.End(); if (File.Exists(path)) File.Delete(path); }
static void Check(bool value, string description) { if (!value) throw new Exception(description); }

