using System;
using System.Linq;
using System.Numerics;
using AnoMech.Core.Combat;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using Lumina.Excel.Sheets;

namespace AnoMech.Windows;

public sealed class JobSupportWindow : Window, IDisposable
{
    private static readonly Vector4 Supported = new(0.45f, 0.9f, 0.45f, 1f);
    private static readonly (byte Role, string Name)[] Roles = [(1, "坦克"), (4, "治療"), (2, "近戰"), (3, "遠程")];

    public JobSupportWindow() : base("職業支援列表###AnoMechJobSupport")
    {
        Flags = ImGuiWindowFlags.AlwaysAutoResize;
    }

    public void Dispose() { }

    public override void Draw()
    {
        ImGui.TextColored(Supported, "綠色：模擬中可用原本熱鍵練習技能循環");
        ImGui.TextDisabled("灰色：尚未支援，技能維持遊戲原本的樣子");
        ImGui.Separator();
        var current = Plugin.ClientState.IsLoggedIn && Plugin.PlayerState.ClassJob.IsValid ? Plugin.PlayerState.ClassJob.RowId : 0;
        var jobs = Plugin.DataManager.GetExcelSheet<ClassJob>().Where(j => j.JobIndex > 0 && j.Role > 0).ToList();
        foreach (var (role, roleName) in Roles)
        {
            ImGui.TextUnformatted(roleName);
            ImGui.Indent();
            foreach (var job in jobs.Where(j => j.Role == role).OrderBy(j => j.UIPriority))
            {
                var levels = JobCombatRegistry.Entries.Where(e => e.ClassJob == job.RowId).Select(e => $"{e.Level} 級").ToArray();
                var text = job.Name.ExtractText() + (levels.Length > 0 ? $"：支援 {string.Join("、", levels)}" : "：尚未支援")
                    + (job.RowId == current ? "（目前職業）" : "");
                if (levels.Length > 0) ImGui.TextColored(Supported, text);
                else ImGui.TextDisabled(text);
            }
            ImGui.Unindent();
        }
    }
}
