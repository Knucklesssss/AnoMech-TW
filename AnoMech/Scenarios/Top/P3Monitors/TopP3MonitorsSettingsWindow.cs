using Dalamud.Bindings.ImGui;

namespace AnoMech.Scenarios.Top.P3Monitors;

public sealed class TopP3MonitorsSettingsWindow
{
    public TopP3MonitorsStateOverrides Overrides { get; } = new();

    public void Draw()
    {
        if (ImGui.Button("自動##resetall")) Overrides.PlayerSlot = null;

        if (SettingsGrid.Begin("##p3monitors"))
        {
            DrawPlayerSlot();
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
}
