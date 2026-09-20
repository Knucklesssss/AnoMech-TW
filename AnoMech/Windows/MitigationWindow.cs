using System.Numerics;
using System.Linq;
using AnoMech.Core.SimObjects;
using AnoMech.Core.Combat;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;

namespace AnoMech.Windows;

public sealed class MitigationWindow : Window
{
    private readonly Plugin plugin;
    public MitigationWindow(Plugin plugin) : base("目標減傷練習###AnoMechMitigation")
    {
        this.plugin = plugin;
        Size = new Vector2(640, 260);
        SizeCondition = ImGuiCond.FirstUseEver;
        RespectCloseHotkey = false;
    }
    public override void PreOpenCheck() => IsOpen = plugin.Configuration.ShowMitigationFeedback && plugin.Game.ActiveScenario != null;
    public override void OnClose()
    {
        plugin.Configuration.ShowMitigationFeedback = false;
        plugin.Configuration.Save();
    }
    public override void Draw()
    {
        ImGui.TextWrapped("僅記錄本機施放。提示覆蓋狀態，不判定減傷是否足夠，不改血量或生死。");
        ImGui.TextWrapped("持續時間依本次練習規則：雪仇／昏亂／牽制 15 秒，武裝解除 10 秒。未對應來源或傷害類型的機制不判定。");
        ImGui.TextUnformatted(TargetMitigation.LastUse);
        foreach (var enemy in plugin.Game.World.Children.OfType<SimEnemy>().Where(e => e.IsActive))
        {
            var active = TargetMitigation.Active(enemy);
            if (active.Length == 0) continue;
            ImGui.TextWrapped($"{enemy.DisplayName}：" + string.Join("、", active.Select(e => $"{e.Name} {e.Remaining:F1}s")));
        }
        ImGui.TextDisabled("目前支援共用傷害判定、P4 分散／分攤、P6 開場主要攻擊；其他自訂機制尚未全部接入。");
        ImGui.Separator();
        if (TargetMitigation.Reports.Count == 0) ImGui.TextDisabled("等待可判定的機制命中……");
        for (var i = TargetMitigation.Reports.Count - 1; i >= 0; i--)
        {
            var report = TargetMitigation.Reports[i];
            ImGui.TextWrapped($"{report.Time:F1}s　{report.Attack}：{report.Result}");
        }
    }
}
