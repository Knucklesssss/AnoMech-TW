using System;
using System.Linq;
using System.Numerics;
using AnoMech.Core.Game;
using Dalamud.Bindings.ImGui;
using SimGame = AnoMech.Core.Game.Game;

namespace AnoMech.Windows;

public sealed class ChainPanel
{
    private readonly Plugin plugin;
    private readonly MainWindow main;

    internal ChainPanel(Plugin plugin, MainWindow main)
    {
        this.plugin = plugin;
        this.main = main;
    }

    public void Draw()
    {
        var chain = Plugin.Chain;
        var config = plugin.Configuration;
        if (chain.Running)
        {
            if (ImGui.Button("停止連戰##chainstop")) chain.Stop();
        }
        else
        {
            ImGui.BeginDisabled(config.Chain.Count == 0);
            if (ImGui.Button("開始連戰##chainstart")) chain.Start();
            ImGui.EndDisabled();
        }
        ImGui.SameLine();
        ImGui.TextDisabled("沒人死亡就接下一個，有人死亡就重來這一場；清單走完就停。");
        if (chain.Status.Length > 0) ImGui.TextUnformatted(chain.Status);
        ImGui.Separator();

        if (ImGui.BeginTable("##chaintable", 2, ImGuiTableFlags.Resizable | ImGuiTableFlags.BordersInnerV))
        {
            ImGui.TableSetupColumn("加入副本", ImGuiTableColumnFlags.WidthFixed, 230);
            ImGui.TableSetupColumn("連戰順序");
            ImGui.TableHeadersRow();
            ImGui.TableNextColumn();
            DrawCatalog();
            ImGui.TableNextColumn();
            DrawList();
            ImGui.EndTable();
        }
    }

    private void DrawCatalog()
    {
        var game = plugin.Game;
        foreach (var zone in game.Zones)
        {
            if (!ImGui.CollapsingHeader($"{zone.Name}##chainzone", ImGuiTreeNodeFlags.DefaultOpen)) continue;
            foreach (var phase in game.PhasesOf(zone))
            foreach (var scenario in game.ScenariosOf(phase))
            {
                var name = SimGame.FullName(scenario);
                if (ImGui.SmallButton($"+##chainadd{name}"))
                {
                    var selected = main.SelectedScenario == scenario;
                    plugin.Configuration.Chain.Add(new ChainEntry
                    {
                        Scenario = name,
                        Strat = selected ? main.SelectedStrat : 0,
                        Waymark = selected ? main.SelectedWaymark : 0,
                    });
                    plugin.Configuration.Save();
                }
                ImGui.SameLine();
                ImGui.TextUnformatted(SimGame.DisplayName(scenario));
            }
        }
    }

    private void DrawList()
    {
        var config = plugin.Configuration;
        var chain = Plugin.Chain;
        ImGui.BeginDisabled(chain.Running || config.Chain.Count == 0);
        if (ImGui.SmallButton("清空##chainclear"))
        {
            config.Chain.Clear();
            config.Save();
        }
        ImGui.EndDisabled();
        if (config.Chain.Count == 0)
        {
            ImGui.TextWrapped("清單是空的：按左邊的「+」加入場景。加入時會帶上主視窗目前選的打法與場地標記。");
            return;
        }

        ImGui.BeginDisabled(chain.Running);
        for (var i = 0; i < config.Chain.Count; i++)
        {
            var entry = config.Chain[i];
            var scenario = plugin.Game.Scenarios.FirstOrDefault(s => SimGame.FullName(s) == entry.Scenario);
            ImGui.PushID(i);
            var label = $"{i + 1}. {(scenario is null ? $"{entry.Scenario}（找不到）" : SimGame.DisplayName(scenario))}";
            if (chain.Running && chain.Index == i) ImGui.TextColored(new Vector4(0.4f, 1f, 0.4f, 1f), $"> {label}");
            else ImGui.TextUnformatted(label);

            if (scenario is { AiStrats.Count: > 1 })
            {
                ImGui.SameLine();
                var names = scenario.AiStrats.Select(s => s.Name).ToArray();
                var strat = Math.Clamp(entry.Strat, 0, names.Length - 1);
                ImGui.SetNextItemWidth(130);
                if (ImGui.Combo("##chainstrat", ref strat, names, names.Length))
                {
                    entry.Strat = strat;
                    config.Save();
                }
            }
            ImGui.SameLine();
            ImGui.BeginDisabled(i == 0);
            if (ImGui.SmallButton("上移")) Swap(i, i - 1);
            ImGui.EndDisabled();
            ImGui.SameLine();
            ImGui.BeginDisabled(i == config.Chain.Count - 1);
            if (ImGui.SmallButton("下移")) Swap(i, i + 1);
            ImGui.EndDisabled();
            ImGui.SameLine();
            if (ImGui.SmallButton("移除"))
            {
                config.Chain.RemoveAt(i);
                config.Save();
                ImGui.PopID();
                break;
            }
            ImGui.PopID();
        }
        ImGui.EndDisabled();
    }

    private void Swap(int a, int b)
    {
        var list = plugin.Configuration.Chain;
        (list[a], list[b]) = (list[b], list[a]);
        plugin.Configuration.Save();
    }
}
