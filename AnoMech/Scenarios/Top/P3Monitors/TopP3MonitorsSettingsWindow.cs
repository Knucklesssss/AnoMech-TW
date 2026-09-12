using Dalamud.Bindings.ImGui;
using System.Linq;

namespace AnoMech.Scenarios.Top.P3Monitors;

public sealed class TopP3MonitorsSettingsWindow
{
    public TopP3MonitorsOverrides Overrides { get; } = new();
    public TopP3MonitorsState? CurrentState { get; set; }
    public bool CrossStrategy { get; set; }

    public void Draw()
    {
        if (ImGui.Button("重置為隨機")) ResetAll();
        if (SettingsGrid.Begin("##p3monitors"))
        {
            DrawBossSide();
            DrawPlayerMonitor();
            SettingsGrid.Row("莫古力小電視：");
            if (ImGui.RadioButton("固定式", !Overrides.MoogleMirror)) Overrides.MoogleMirror = false;
            ImGui.SameLine();
            if (ImGui.RadioButton("鏡像換邊式", Overrides.MoogleMirror)) Overrides.MoogleMirror = true;
            SettingsGrid.End();
        }

        if (CurrentState is not { } state) return;
        ImGui.Separator();
        if (CrossStrategy)
        {
            DrawCross(state);
            return;
        }
        if (!state.BuffStarted)
        {
            ImGui.TextUnformatted($"排隊順序：{TopP3MonitorRules.PriorityLabel}");
            ImGui.TextUnformatted(
                $"自身練習分工：{TopP3MonitorRules.RoleLabel(state.PlayerRole)}（第 {state.PlayerPriority + 1} 位）");
            ImGui.TextUnformatted("準備中：AI先到左側排隊，約等待 5 秒；真人請自行走到空位。");
            return;
        }

        ImGui.TextUnformatted($"本輪王側：{state.BossSideLabel}");
        ImGui.TextUnformatted($"Owner順位：{TopP3MonitorRules.PriorityLabel}");
        ImGui.TextUnformatted($"自身順位：{state.PlayerSlotLabel}");
        ImGui.TextUnformatted($"固定優先序：第 {state.PlayerPriority + 1} 位（{TopP3MonitorRules.RoleLabel(state.PlayerRole)}）");
        if (state.PlayerHasMonitor)
            ImGui.TextUnformatted($"自身螢幕方向：{state.PlayerStatusLabel}");
    }

    private static void DrawCross(TopP3MonitorsState state)
    {
        ImGui.TextUnformatted("十字法：TN 北／東，DPS 南／西；內外各自換位。");
        ImGui.TextUnformatted("北：H1 外、MT 內；東：ST 內、H2 外；南：D1 內、D3 外；西：D2 內、D4 外。");
        var initial = TopP3MonitorsCrossRules.InitialPositionFor(state.PlayerRole);
        ImGui.TextUnformatted($"自身分工：{TopP3MonitorRules.RoleLabel(state.PlayerRole)}，起點 {TopP3MonitorsCrossRules.PositionLabel(initial)}");
        if (!state.BuffStarted)
        {
            ImGui.TextUnformatted("準備中：AI先站十字，約等待 5 秒；真人請自行站位。");
            return;
        }
        var move = TopP3MonitorsCrossRules.PlanMoves(state.Assignment, state.BossSide, state.MonitorStatuses,
            (Core.Game.Party.PartyRole)(-1)).Single(m => m.Role == state.PlayerRole);
        ImGui.TextUnformatted($"本輪王側：{state.BossSideLabel}；自身終點：{TopP3MonitorsCrossRules.PositionLabel(move.Target)}");
        ImGui.TextUnformatted("每組 0 螢幕不換；1 螢幕放橫線；2 螢幕同軸時內側換；3 螢幕讓無螢幕留直線。");
        ImGui.TextUnformatted("橫線無螢幕留軸上；兩名橫線螢幕相較：靠左向北挪並朝北、靠右向南挪並朝南。");
        ImGui.TextUnformatted("直線往王安全半場移：無螢幕一步、有螢幕兩步；螢幕面朝安全側。");
        if (state.PlayerHasMonitor)
            ImGui.TextUnformatted($"自身螢幕狀態：{state.PlayerStatusLabel}（依螢幕面調整人物朝向）");
    }

    private void ResetAll()
    {
        Overrides.BossSide = TopP3BossSideOption.Auto;
        Overrides.PlayerMonitor = TopP3PlayerMonitorOption.Auto;
    }

    private void DrawBossSide()
    {
        var value = Overrides.BossSide;
        SettingsGrid.Row("王側（隨機／左／右）：");
        if (ImGui.RadioButton("隨機##p3boss", value == TopP3BossSideOption.Auto))
            Overrides.BossSide = TopP3BossSideOption.Auto;
        ImGui.SameLine();
        if (ImGui.RadioButton("左##p3boss", value == TopP3BossSideOption.Left))
            Overrides.BossSide = TopP3BossSideOption.Left;
        ImGui.SameLine();
        if (ImGui.RadioButton("右##p3boss", value == TopP3BossSideOption.Right))
            Overrides.BossSide = TopP3BossSideOption.Right;
    }

    private void DrawPlayerMonitor()
    {
        var value = Overrides.PlayerMonitor;
        SettingsGrid.Row("自身螢幕（隨機／有／無）：");
        if (ImGui.RadioButton("隨機##p3self", value == TopP3PlayerMonitorOption.Auto))
            Overrides.PlayerMonitor = TopP3PlayerMonitorOption.Auto;
        ImGui.SameLine();
        if (ImGui.RadioButton("有##p3self", value == TopP3PlayerMonitorOption.WithMonitor))
            Overrides.PlayerMonitor = TopP3PlayerMonitorOption.WithMonitor;
        ImGui.SameLine();
        if (ImGui.RadioButton("無##p3self", value == TopP3PlayerMonitorOption.WithoutMonitor))
            Overrides.PlayerMonitor = TopP3PlayerMonitorOption.WithoutMonitor;
    }
}
