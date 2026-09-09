using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;

namespace AnoMech.Windows;

public class ConfigWindow : Window, IDisposable
{
    private readonly Configuration configuration;

    public ConfigWindow(Plugin plugin) : base("AnoMech 設定###AnoMechConfig")
    {
        Flags = ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoScrollbar |
                ImGuiWindowFlags.NoScrollWithMouse;

        Size = new Vector2(380, 280);
        SizeCondition = ImGuiCond.Always;

        configuration = plugin.Configuration;
    }

    public void Dispose() { }

    public override void Draw()
    {
        var onInn = configuration.OpenSimMenuOnInn;
        if (ImGui.Checkbox("進入旅館時自動開啟模擬選單##openoninn", ref onInn))
        {
            configuration.OpenSimMenuOnInn = onInn;
            configuration.Save();
        }

        var suppressBgm = configuration.SuppressBgm;
        if (ImGui.Checkbox("關閉場景背景音樂##suppressbgm", ref suppressBgm))
        {
            configuration.SuppressBgm = suppressBgm;
            configuration.Save();
        }

        ImGui.Separator();

        var logging = configuration.EnableEventLogging;
        if (ImGui.Checkbox("啟用事件記錄##eventlog", ref logging))
        {
            configuration.EnableEventLogging = logging;
            configuration.Save();
            if (logging) Plugin.LogManager.Open();
            else Plugin.LogManager.Close();
        }
        ImGui.SameLine();
        if (ImGui.Button("開啟記錄資料夾##openlogs"))
            Plugin.LogManager.OpenLogsFolder();

#if DEBUG
        ImGui.Separator();

        var safeMode = configuration.SafeMode;
        if (ImGui.Checkbox("安全模式（偵錯）##safemode", ref safeMode))
        {
            configuration.SafeMode = safeMode;
            configuration.Save();
        }
        if (safeMode)
            ImGui.TextWrapped(
                "安全模式會在你身處模擬區域時切斷伺服器封包。" +
                "你不會看到隊員加入或離開、準備確認，" +
                "也不會收到任務配對通知。");
        else
            ImGui.TextWrapped(
                "安全模式已關閉 —— 伺服器封包會進入引擎，所以你會看到" +
                "隊伍更新、準備確認和任務配對通知。這樣比較容易把模擬區域弄壞。" +
                "身處副本中時，你仍然無法向伺服器送出任何東西。");
#endif
    }
}
