using System;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;

namespace AnoMech.Windows;

public class ConfigWindow : Window, IDisposable
{
    private readonly Configuration configuration;

    public ConfigWindow(Plugin plugin) : base("AnoMech 設定###AnoMechConfig")
    {
        Flags = ImGuiWindowFlags.NoCollapse;

        Size = new Vector2(440, 560);
        SizeCondition = ImGuiCond.FirstUseEver;

        configuration = plugin.Configuration;
    }

    public void Dispose() { }

    public override void Draw()
    {
        var onInn = configuration.OpenSimMenuOnInn;
        if (ImGui.Checkbox("進入旅館／住宅室內時開啟選單##openoninn", ref onInn))
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

        var compact = configuration.CompactSimulationControls;
        if (ImGui.Checkbox("模擬中使用精簡面板##compactcontrols", ref compact))
        {
            configuration.CompactSimulationControls = compact;
            configuration.Save();
        }

        var mitigation = configuration.ShowMitigationFeedback;
        if (ImGui.Checkbox("顯示目標減傷練習提示", ref mitigation))
        {
            configuration.ShowMitigationFeedback = mitigation;
            configuration.Save();
        }

        var ranges = configuration.ShowHitRanges;
        if (ImGui.Checkbox("顯示通用傷害判定範圍（命中後）##hitranges", ref ranges))
        {
            configuration.ShowHitRanges = ranges;
            configuration.Save();
        }
        ImGui.TextDisabled("顯示兩秒；不含各場景自行計算的塔、連線等判定。");

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
        ImGui.TextDisabled("同一個資料夾裡的 net-*.log 是多人連線記錄，一直開著，不受上面的勾選影響。");

        if (ImGui.Button("開啟錯誤日誌資料夾##openerrors"))
            AnoMech.Core.ErrorLog.OpenFolder();
        ImGui.TextDisabled("錯誤日誌會自動記錄問題，回報 BUG 時把最新的檔案傳給開發者。");

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
