using System;
using System.Linq;
using AnoMech.Scenarios;

namespace AnoMech.Core.Game;

[Serializable]
public sealed class ChainEntry
{
    public string Scenario { get; set; } = "";
    public int Strat { get; set; }
    public int Waymark { get; set; }
}

// Plays Configuration.Chain in order. A run that ends with nobody dead moves on; a run with a
// death is played again. A run has ended once its timeline is empty, or once a death froze it.
internal sealed class ChainRunner(Game game, Configuration config)
{
    private const float GapSeconds = 3f;
    private const float StartTimeoutSeconds = 10f;

    private int index = -1;
    private int awaitedRun;
    private float waited;

    public bool Running => index >= 0;
    public int Index => index;
    public string Status { get; private set; } = "";

    public void Start() => Launch(0, retry: false);

    public void Stop(string status = "已停止")
    {
        index = -1;
        Status = status;
    }

    public void Tick(float deltaSeconds)
    {
        if (!Running) return;
        if (Plugin.Multiplayer.IsClientConnected) { Stop("已加入他人的房間，連戰已停止"); return; }
        if (game.RunCount < awaitedRun)
        {
            if ((waited += deltaSeconds) > StartTimeoutSeconds) Stop("場景無法開始，連戰已停止");
            return;
        }
        if (game.RunCount > awaitedRun || game.ActiveScenario is null) { Stop("已手動開始或重置，連戰已停止"); return; }
        if (game.Events.Count > 0 && !game.Paused) return;
        if ((waited += deltaSeconds) < GapSeconds) return;
        if (game.HadDeath) Launch(index, retry: true);
        else Launch(index + 1, retry: false);
    }

    private void Launch(int next, bool retry)
    {
        if (next >= config.Chain.Count)
        {
            Stop(next == 0 ? "清單是空的" : "連戰完成");
            return;
        }
        var entry = config.Chain[next];
        var scenario = game.Scenarios.FirstOrDefault(s => Game.FullName(s) == entry.Scenario);
        if (scenario is null)
        {
            Stop($"找不到場景「{entry.Scenario}」，連戰已停止");
            return;
        }
        index = next;
        waited = 0f;
        awaitedRun = game.RunCount + 1;
        Status = $"第 {next + 1}/{config.Chain.Count} 場{(retry ? "（有人死亡，重來）" : "")}：{Game.DisplayName(scenario)}";
        var strat = Math.Clamp(entry.Strat, 0, Math.Max(0, scenario.AiStrats.Count - 1));
        var multiplayer = Plugin.Multiplayer;
        if (!multiplayer.HostControlsRun)
        {
            game.RunScenario(scenario, null, strat, entry.Waymark);
            return;
        }
        if (multiplayer.CanHostStart(scenario, out var reason)) multiplayer.HostStartRun(scenario, strat, entry.Waymark);
        else Status += $"（等待：{reason}）";
    }
}
