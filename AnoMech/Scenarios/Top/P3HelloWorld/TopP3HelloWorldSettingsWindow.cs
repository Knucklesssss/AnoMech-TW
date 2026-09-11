using Dalamud.Bindings.ImGui;

namespace AnoMech.Scenarios.Top.P3HelloWorld;

public sealed class TopP3HelloWorldSettingsWindow
{
    public TopP3HelloWorldStateOverrides Overrides { get; } = new();

    public void Draw()
    {
        if (ImGui.Button("自動##resetall"))
        {
            Overrides.PlayerSlot = null;
            Overrides.TransitionFirstArmsSouth = true;
            Overrides.TransitionPlayerSlot = null;
            Overrides.TransitionAutoMarkers = true;
        }
        if (SettingsGrid.Begin("##p3helloworld"))
        {
            DrawPlayerSlot();
            SettingsGrid.Row("P2.5 標記：");
            var autoMarkers = Overrides.TransitionAutoMarkers;
            if (ImGui.Checkbox("系統自動標", ref autoMarkers)) Overrides.TransitionAutoMarkers = autoMarkers;
            SettingsGrid.Row("P2.5 我的標記：");
            var marker = Overrides.TransitionPlayerSlot is { } slot ? slot + 1 : 0;
            if (ImGui.Combo("##transitionmarker", ref marker,
                "自動\0鎖鏈1（分攤）\0禁止1（無點名）\0鎖鏈2（分攤）\0禁止2（無點名）\0攻擊1（分散）\0攻擊2（分散）\0攻擊3（分散）\0攻擊4（分散）\0"))
                Overrides.TransitionPlayerSlot = marker == 0 ? null : marker - 1;
            SettingsGrid.Row("轉場手臂：");
            if (ImGui.RadioButton("原始排列##arms", Overrides.TransitionFirstArmsSouth))
                Overrides.TransitionFirstArmsSouth = true;
            ImGui.SameLine();
            if (ImGui.RadioButton("反向練習排列##arms", !Overrides.TransitionFirstArmsSouth))
                Overrides.TransitionFirstArmsSouth = false;
            SettingsGrid.End();
        }
    }

    private void DrawPlayerSlot()
    {
        var v = Overrides.PlayerSlot;
        SettingsGrid.Row("我的組別：");
        if (ImGui.RadioButton("自動##slot", v == null)) Overrides.PlayerSlot = null;
        for (var i = 0; i < TopP3HelloWorldState.SlotCount; i++)
        {
            ImGui.SameLine();
            if (ImGui.RadioButton($"第 {i + 1} 組##slot", v == i)) Overrides.PlayerSlot = i;
        }
    }
}
