using Dalamud.Bindings.ImGui;

namespace AnoMech.Scenarios.Top.P3Monitors;

public sealed class TopP3MonitorsSettingsWindow
{
    public TopP3MonitorsStateOverrides Overrides { get; } = new();

    public void Draw()
    {
        if (ImGui.Button("自動##resetall")) { Overrides.PlayerSlot = null; Overrides.ScreenFacesEast = null; Overrides.Markers = MarkerMode.System; }
        if (SettingsGrid.Begin("##p3monitors"))
        {
            DrawPlayerSlot();
            DrawScreenFacing();
            DrawMarkers();
            SettingsGrid.End();
        }
    }

    private void DrawPlayerSlot()
    {
        var v = Overrides.PlayerSlot;
        SettingsGrid.Row("我的位置：");
        if (ImGui.RadioButton("自動##slot", v == null)) Overrides.PlayerSlot = null;
        for (var i = 0; i < TopP3MonitorsState.SlotCount; i++)
        {
            ImGui.SameLine();
            var label = System.Array.IndexOf(TopP3MonitorsState.MonitorSlots, i) >= 0 ? $"{i + 1}★" : $"{i + 1}";
            if (ImGui.RadioButton($"{label}##slot", v == i)) Overrides.PlayerSlot = i;
        }

        ImGui.TextDisabled("★ 會拿到螢幕");
    }

    private void DrawScreenFacing()
    {
        var v = Overrides.ScreenFacesEast;
        SettingsGrid.Row("歐米茄螢幕方向：");
        if (ImGui.RadioButton("自動##face", v == null)) Overrides.ScreenFacesEast = null;
        ImGui.SameLine();
        if (ImGui.RadioButton("東##face", v == true)) Overrides.ScreenFacesEast = true;
        ImGui.SameLine();
        if (ImGui.RadioButton("西##face", v == false)) Overrides.ScreenFacesEast = false;
    }

    private void DrawMarkers()
    {
        var v = Overrides.Markers;
        SettingsGrid.Row("標記方式：");
        if (ImGui.RadioButton("系統標##marks", v == MarkerMode.System)) Overrides.Markers = MarkerMode.System;
        ImGui.SameLine();
        if (ImGui.RadioButton("玩家手標##marks", v == MarkerMode.Manual)) Overrides.Markers = MarkerMode.Manual;
    }
}
