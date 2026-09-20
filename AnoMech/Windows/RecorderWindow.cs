using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;

namespace AnoMech.Windows;

internal sealed class RecorderWindow : Window
{
    public RecorderWindow() : base("AnoMech 戰鬥錄製器###AnoMechRecorder")
    {
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(520, 300),
            MaximumSize = new Vector2(1400, 1400),
        };
    }

    public override void Draw()
    {
        ImGui.TextWrapped("錄製實際遊戲中的戰鬥事件、位置與特效。切換區域或開始模擬時停止錄製；玩家聊天不會錄入。");
        ImGui.TextWrapped("檔案保存在插件設定目錄的 recordings 資料夾。這是原始事件資料，需轉換為場景資料後才能回放。");
        RecorderPanel.Draw(Plugin.Recorder);
    }
}
