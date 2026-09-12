using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using AnoMech.Core.Game.Party;

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
        var localCombat = configuration.EnableLocalCombat;
        if (ImGui.Checkbox("90 級戰士本機輸出預覽（下次開始場景生效）", ref localCombat))
        {
            configuration.EnableLocalCombat = localCombat;
            configuration.Save();
        }
        ImGui.TextWrapped("使用原本熱鍵手動攻擊。其他職業不可用；治療、護盾與減傷不在此預覽範圍。");
        if (Plugin.GameInstance is { } game)
        {
            ImGui.TextWrapped(game.World.CombatReason);
            if (game.World.Combat is { } combat)
            {
                var s = combat.Stats;
                ImGui.TextWrapped($"同步屬性：力量 {s.Strength}／武器傷害 {s.WeaponDamage}／暴擊 {s.CriticalHit}／直擊 {s.DirectHit}／信念 {s.Determination}／堅韌 {s.Tenacity}／技速 {s.SkillSpeed}／武器間隔 {s.WeaponDelay:F2}s");
                ImGui.TextUnformatted($"上次傷害 {combat.LastDamage:N0}　累積傷害 {combat.TotalDamage:N0}");
            }
        }
        ImGui.Separator();
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

        DrawPartyListOrder();

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

    private void DrawPartyListOrder()
    {
        var enabled = configuration.CustomPartyListOrder;
        if (ImGui.Checkbox("自訂模擬隊伍列表順序", ref enabled))
        {
            configuration.CustomPartyListOrder = enabled;
            configuration.Save();
        }
        if (!enabled) return;
        ImGui.TextWrapped("只調整八人的顯示位置，不改角色分工。原生隊員編號與快捷鍵仍跟隨原隊員，不重新編號。");
        var order = PartyListOrderRules.Normalize(configuration.PartyListOrder);
        for (var i = 0; i < order.Length; i++)
        {
            ImGui.PushID(i);
            ImGui.TextUnformatted($"{i + 1}. {RoleName(order[i])}");
            ImGui.SameLine(100);
            ImGui.BeginDisabled(i == 0);
            if (ImGui.SmallButton("上移"))
            {
                (order[i - 1], order[i]) = (order[i], order[i - 1]);
                configuration.PartyListOrder = order;
                configuration.Save();
            }
            ImGui.EndDisabled();
            ImGui.SameLine();
            ImGui.BeginDisabled(i == order.Length - 1);
            if (ImGui.SmallButton("下移"))
            {
                (order[i + 1], order[i]) = (order[i], order[i + 1]);
                configuration.PartyListOrder = order;
                configuration.Save();
            }
            ImGui.EndDisabled();
            ImGui.PopID();
        }
        if (ImGui.Button("還原 MT、ST、H1、H2、D1–D4"))
        {
            configuration.PartyListOrder = Enum.GetValues<PartyRole>();
            configuration.Save();
        }
    }

    private static string RoleName(PartyRole role) => role switch
    {
        PartyRole.MainTank => "MT", PartyRole.OffTank => "ST",
        PartyRole.RegenHealer => "H1", PartyRole.ShieldHealer => "H2",
        PartyRole.MeleeDpsA => "D1", PartyRole.MeleeDpsB => "D2",
        PartyRole.PhysRangedDps => "D3", PartyRole.CasterDps => "D4",
        _ => role.ToString(),
    };
}
