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
        }
        if (SettingsGrid.Begin("##p3helloworld"))
        {
            DrawPlayerSlot();
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
