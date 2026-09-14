using System.Collections.Generic;
using AnoMech.Core.SimObjects;

namespace AnoMech.Core.Game.Ai;

public static class ScenarioAiRunner
{
    // Clients never run AI: their bots follow the host's positions. On the host (and solo) AI
    // runs inside a HostOnly scope so its random draws don't shift the shared deterministic stream.
    public static void Run<TState>(IReadOnlyList<IScenarioAi> strats, int? selected, TState state, SimWorld world)
    {
        if (selected is not { } index || index < 0 || index >= strats.Count) return;
        if (MultiplayerContext.IsClient) return;
        using var scope = SimRandom.HostOnly();
        ((IScenarioAi<TState>)strats[index]).Run(state, world);
    }
}
